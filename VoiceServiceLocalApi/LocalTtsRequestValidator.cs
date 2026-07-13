namespace VoiceServiceLocalApi;

public static class LocalTtsRequestValidator
{
    public static LocalTtsValidationResult Validate(
        LocalTtsRequest request,
        LocalVendorInfo? vendor,
        int maxTextLength)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (maxTextLength is < 1 or > 20_000)
            throw new ArgumentOutOfRangeException(nameof(maxTextLength));

        var errors = new List<LocalTtsValidationError>();
        if (string.IsNullOrWhiteSpace(request.Vendor))
            errors.Add(new("vendor", "vendor is required"));
        if (string.IsNullOrWhiteSpace(request.Text))
            errors.Add(new("text", "text is required"));
        else if (request.Text.Length > maxTextLength)
            errors.Add(new("text", $"text cannot exceed {maxTextLength} characters"));
        if (string.IsNullOrWhiteSpace(request.VoiceId))
            errors.Add(new("voice_id", "voice_id is required"));

        if (vendor is null)
        {
            if (!string.IsNullOrWhiteSpace(request.Vendor))
                errors.Add(new("vendor", "vendor was not found"));
            return new(null, errors);
        }

        if (!string.Equals(request.Vendor, vendor.Id, StringComparison.OrdinalIgnoreCase))
            errors.Add(new("vendor", "vendor does not match the selected vendor"));

        var modelId = string.IsNullOrWhiteSpace(request.ModelId) ? vendor.DefaultModelId : request.ModelId.Trim();
        if (vendor.Models.Count > 0 && !vendor.Models.Any(model => string.Equals(model.Id, modelId, StringComparison.OrdinalIgnoreCase)))
            errors.Add(new("model_id", "model_id is not supported by this vendor"));

        var inputFormat = string.IsNullOrWhiteSpace(request.InputFormat) ? "text" : request.InputFormat.Trim().ToLowerInvariant();
        if (!vendor.Capabilities.SupportedInputFormats.Contains(inputFormat, StringComparer.OrdinalIgnoreCase))
            errors.Add(new("input_format", "input_format is not supported by this vendor"));
        if (inputFormat == "ssml" && !vendor.Capabilities.SupportsSsml)
            errors.Add(new("input_format", "this vendor does not support SSML"));

        var outputFormat = string.IsNullOrWhiteSpace(request.OutputFormat)
            ? vendor.Capabilities.SupportedOutputFormats.FirstOrDefault() ?? "mp3"
            : request.OutputFormat.Trim().ToLowerInvariant();
        if (!vendor.Capabilities.SupportedOutputFormats.Contains(outputFormat, StringComparer.OrdinalIgnoreCase))
            errors.Add(new("output_format", "output_format is not supported by this vendor"));

        var speed = request.Speed ?? vendor.Speed.Default;
        ValidateParameter("speed", request.Speed, speed, vendor.Speed, errors);
        var volume = request.Volume ?? vendor.Volume.Default;
        ValidateParameter("volume", request.Volume, volume, vendor.Volume, errors);

        if (!string.IsNullOrWhiteSpace(request.Style) && !vendor.Capabilities.SupportsStyle)
            errors.Add(new("style", "style is not supported by this vendor"));
        if (request.StyleDegree.HasValue && !vendor.Capabilities.SupportsStyleDegree)
            errors.Add(new("style_degree", "style_degree is not supported by this vendor"));
        if (!string.IsNullOrWhiteSpace(request.Emotion) && !vendor.Capabilities.SupportsEmotion)
            errors.Add(new("emotion", "emotion is not supported by this vendor"));
        if (request.EmotionIntensity.HasValue && !vendor.Capabilities.SupportsEmotion)
            errors.Add(new("emotion_intensity", "emotion_intensity is not supported by this vendor"));
        if (!string.IsNullOrWhiteSpace(request.Instructions) && !vendor.Capabilities.SupportsInstructions)
            errors.Add(new("instructions", "instructions are not supported by this vendor"));
        if (!string.IsNullOrWhiteSpace(request.ResourceId) && !vendor.Capabilities.SupportsResourceId)
            errors.Add(new("resource_id", "resource_id is not supported by this vendor"));
        if (!string.IsNullOrWhiteSpace(request.SsmlText) && !vendor.Capabilities.SupportsSsml)
            errors.Add(new("ssml_text", "ssml_text is not supported by this vendor"));

        var normalized = request with
        {
            Vendor = vendor.Id,
            ModelId = modelId,
            Speed = speed,
            Volume = volume,
            InputFormat = inputFormat,
            OutputFormat = outputFormat
        };

        return new(errors.Count == 0 ? normalized : null, errors);
    }

    private static void ValidateParameter(
        string field,
        double? requestedValue,
        double normalizedValue,
        LocalParameterDefinition definition,
        List<LocalTtsValidationError> errors)
    {
        if (!definition.IsSupported && requestedValue.HasValue)
        {
            errors.Add(new(field, $"{field} is not supported by this vendor"));
            return;
        }

        if (definition.IsSupported && (normalizedValue < definition.Min || normalizedValue > definition.Max))
            errors.Add(new(field, $"{field} must be between {definition.Min} and {definition.Max}"));
    }
}
