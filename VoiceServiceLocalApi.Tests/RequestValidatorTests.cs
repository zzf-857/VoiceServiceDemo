using VoiceServiceLocalApi;

namespace VoiceServiceLocalApi.Tests;

public sealed class RequestValidatorTests
{
    [Fact]
    public void Request_requires_vendor_text_and_voice()
    {
        var result = LocalTtsRequestValidator.Validate(
            new LocalTtsRequest("", "", ""),
            vendor: null,
            maxTextLength: 20_000);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Field == "vendor");
        Assert.Contains(result.Errors, error => error.Field == "text");
        Assert.Contains(result.Errors, error => error.Field == "voice_id");
    }

    [Fact]
    public void Request_rejects_unsupported_output_and_expression_controls()
    {
        var vendor = CreateVendor();
        var result = LocalTtsRequestValidator.Validate(
            new LocalTtsRequest(
                Vendor: vendor.Id,
                Text: "hello",
                VoiceId: "voice-1",
                OutputFormat: "wav",
                Style: "cheerful",
                Emotion: "happy",
                Instructions: "whisper",
                ResourceId: "seed-tts-2.0",
                InputFormat: "ssml"),
            vendor,
            20_000);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Field == "output_format");
        Assert.Contains(result.Errors, error => error.Field == "style");
        Assert.Contains(result.Errors, error => error.Field == "emotion");
        Assert.Contains(result.Errors, error => error.Field == "instructions");
        Assert.Contains(result.Errors, error => error.Field == "resource_id");
        Assert.Contains(result.Errors, error => error.Field == "input_format");
    }

    [Fact]
    public void Request_applies_vendor_defaults()
    {
        var vendor = CreateVendor();
        var result = LocalTtsRequestValidator.Validate(
            new LocalTtsRequest(vendor.Id, "hello", "voice-1"),
            vendor,
            20_000);

        Assert.True(result.Success);
        Assert.NotNull(result.NormalizedRequest);
        Assert.Equal("model-default", result.NormalizedRequest!.ModelId);
        Assert.Equal(1.0, result.NormalizedRequest.Speed);
        Assert.Equal(0.8, result.NormalizedRequest.Volume);
        Assert.Equal("mp3", result.NormalizedRequest.OutputFormat);
        Assert.Equal("text", result.NormalizedRequest.InputFormat);
    }

    [Fact]
    public void Request_rejects_text_over_configured_limit_and_numeric_ranges()
    {
        var vendor = CreateVendor();
        var result = LocalTtsRequestValidator.Validate(
            new LocalTtsRequest(vendor.Id, "123456", "voice-1", Speed: 3, Volume: -1),
            vendor,
            maxTextLength: 5);

        Assert.Contains(result.Errors, error => error.Field == "text");
        Assert.Contains(result.Errors, error => error.Field == "speed");
        Assert.Contains(result.Errors, error => error.Field == "volume");
    }

    private static LocalVendorInfo CreateVendor() => new(
        Id: "test",
        Name: "Test Vendor",
        Configured: true,
        Models: new[] { new LocalModelInfo("model-default", "Default") },
        DefaultModelId: "model-default",
        Speed: new LocalParameterDefinition(true, 0.5, 2.0, 1.0, 0.1),
        Volume: new LocalParameterDefinition(true, 0.0, 1.0, 0.8, 0.1),
        Capabilities: new LocalVendorCapabilities(
            SupportedInputFormats: new[] { "text" },
            SupportedOutputFormats: new[] { "mp3" },
            SupportsSsml: false,
            SupportsStyle: false,
            SupportsStyleDegree: false,
            SupportsEmotion: false,
            SupportsInstructions: false));
}
