using VoiceServiceLocalApi;

namespace VoiceServiceLocalApi.Tests;

internal sealed class FakeLocalTtsGateway : ILocalTtsGateway, IDisposable
{
    private int _activeGenerations;
    private int _maxObservedGenerations;

    public FakeLocalTtsGateway()
    {
        OutputDirectory = Path.Combine(Path.GetTempPath(), "VoiceOpsLocalApiTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(OutputDirectory);
    }

    public string OutputDirectory { get; }
    public byte[] GeneratedBytes { get; set; } = [1, 2, 3, 4, 5];
    public TimeSpan GenerateDelay { get; set; }
    public string? GenerateErrorCode { get; set; }
    public string? GenerateErrorMessage { get; set; }
    public string? VoiceErrorCode { get; set; }
    public string? VoiceErrorMessage { get; set; }
    public bool LastRefresh { get; private set; }
    public int MaxObservedGenerations => Volatile.Read(ref _maxObservedGenerations);

    public Task<IReadOnlyList<LocalVendorInfo>> GetVendorsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<LocalVendorInfo> vendors = new[]
        {
            new LocalVendorInfo(
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
                    SupportsInstructions: false))
        };
        return Task.FromResult(vendors);
    }

    public Task<LocalVoiceCatalog> GetVoicesAsync(
        string vendor,
        bool refresh,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LastRefresh = refresh;
        if (!string.IsNullOrWhiteSpace(VoiceErrorCode))
        {
            return Task.FromResult(new LocalVoiceCatalog(
                Success: false,
                Supported: VoiceErrorCode != "voice_fetch_not_supported",
                Voices: Array.Empty<LocalVoiceInfo>(),
                ErrorCode: VoiceErrorCode,
                ErrorMessage: VoiceErrorMessage));
        }

        return Task.FromResult(new LocalVoiceCatalog(
            Success: true,
            Supported: true,
            Voices: new[] { new LocalVoiceInfo("voice-1", "Test Voice", "中性", "中文") }));
    }

    public async Task<LocalTtsGatewayResult> GenerateAsync(
        LocalTtsRequest request,
        CancellationToken cancellationToken)
    {
        var active = Interlocked.Increment(ref _activeGenerations);
        UpdateMaximum(active);
        try
        {
            if (GenerateDelay > TimeSpan.Zero)
                await Task.Delay(GenerateDelay, cancellationToken);

            if (!string.IsNullOrWhiteSpace(GenerateErrorCode))
            {
                return new LocalTtsGatewayResult(
                    false,
                    null,
                    request.Vendor,
                    request.ModelId ?? "",
                    request.VoiceId,
                    DateTimeOffset.UtcNow,
                    GenerateErrorCode,
                    GenerateErrorMessage);
            }

            var path = Path.Combine(OutputDirectory, $"test_{Guid.NewGuid():N}.mp3");
            await File.WriteAllBytesAsync(path, GeneratedBytes, cancellationToken);
            return new LocalTtsGatewayResult(
                true,
                path,
                request.Vendor,
                request.ModelId ?? "model-default",
                request.VoiceId,
                DateTimeOffset.UtcNow);
        }
        finally
        {
            Interlocked.Decrement(ref _activeGenerations);
        }
    }

    private void UpdateMaximum(int value)
    {
        while (true)
        {
            var current = Volatile.Read(ref _maxObservedGenerations);
            if (value <= current || Interlocked.CompareExchange(ref _maxObservedGenerations, value, current) == current)
                return;
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(OutputDirectory))
            Directory.Delete(OutputDirectory, recursive: true);
    }
}
