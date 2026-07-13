using VoiceServiceDemo.Models;

namespace VoiceServiceDemo.Services.Providers;

public interface ITtsProvider
{
    string VendorId { get; }

    Task<(bool Success, string Message)> TestConnectivityAsync(
        string apiKey,
        CancellationToken cancellationToken = default);

    Task<TtsResult> GenerateAsync(
        TtsRequest request,
        string apiKey,
        CancellationToken cancellationToken = default);
}

public interface IVoiceCatalogProvider
{
    Task<List<VoiceOption>> FetchVoicesAsync(
        string apiKey,
        CancellationToken cancellationToken = default);
}

public sealed record VoiceCatalogResult(
    bool Success,
    bool Supported,
    List<VoiceOption> Voices,
    string? ErrorCode = null,
    string? ErrorMessage = null);
