namespace VoiceServiceLocalApi;

public sealed record LocalTtsRequest(
    string Vendor,
    string Text,
    string VoiceId,
    string? ModelId = null,
    double? Speed = null,
    double? Volume = null,
    string InputFormat = "text",
    string? Style = null,
    double? StyleDegree = null,
    string? Emotion = null,
    int? EmotionIntensity = null,
    string? OutputFormat = null,
    string? ResourceId = null,
    string? Instructions = null,
    string? SsmlText = null);

public sealed record LocalTtsGatewayResult(
    bool Success,
    string? FilePath,
    string Vendor,
    string ModelId,
    string VoiceId,
    DateTimeOffset GeneratedAt,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public sealed record LocalModelInfo(string Id, string Name);

public sealed record LocalVoiceInfo(
    string Id,
    string Name,
    string? Gender = null,
    string? Language = null,
    string? SampleUrl = null,
    string? Age = null,
    IReadOnlyList<string>? Categories = null);

public sealed record LocalParameterDefinition(
    bool IsSupported,
    double Min,
    double Max,
    double Default,
    double Step);

public sealed record LocalVendorCapabilities(
    IReadOnlyList<string> SupportedInputFormats,
    IReadOnlyList<string> SupportedOutputFormats,
    bool SupportsSsml,
    bool SupportsStyle,
    bool SupportsStyleDegree,
    bool SupportsEmotion,
    bool SupportsInstructions,
    bool SupportsResourceId = false);

public sealed record LocalVendorInfo(
    string Id,
    string Name,
    bool Configured,
    IReadOnlyList<LocalModelInfo> Models,
    string DefaultModelId,
    LocalParameterDefinition Speed,
    LocalParameterDefinition Volume,
    LocalVendorCapabilities Capabilities,
    string Description = "");

public sealed record LocalVoiceCatalog(
    bool Success,
    bool Supported,
    IReadOnlyList<LocalVoiceInfo> Voices,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public sealed record LocalTtsValidationError(string Field, string Message);

public sealed record LocalTtsValidationResult(
    LocalTtsRequest? NormalizedRequest,
    IReadOnlyList<LocalTtsValidationError> Errors)
{
    public bool Success => Errors.Count == 0;
}

public interface ILocalTtsGateway
{
    Task<IReadOnlyList<LocalVendorInfo>> GetVendorsAsync(CancellationToken cancellationToken);

    Task<LocalVoiceCatalog> GetVoicesAsync(
        string vendor,
        bool refresh,
        CancellationToken cancellationToken);

    Task<LocalTtsGatewayResult> GenerateAsync(
        LocalTtsRequest request,
        CancellationToken cancellationToken);

    string OutputDirectory { get; }
}
