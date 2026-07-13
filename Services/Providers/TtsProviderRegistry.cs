namespace VoiceServiceDemo.Services.Providers;

public sealed class TtsProviderRegistry
{
    private readonly IReadOnlyDictionary<string, ITtsProvider> _providers;

    public TtsProviderRegistry(IEnumerable<ITtsProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        var byId = new Dictionary<string, ITtsProvider>(StringComparer.OrdinalIgnoreCase);
        foreach (var provider in providers)
        {
            ArgumentNullException.ThrowIfNull(provider);
            if (string.IsNullOrWhiteSpace(provider.VendorId))
                throw new ArgumentException("TTS provider IDs cannot be blank.", nameof(providers));
            if (!byId.TryAdd(provider.VendorId, provider))
                throw new ArgumentException($"Duplicate TTS provider ID: {provider.VendorId}", nameof(providers));
        }

        _providers = byId;
    }

    public IReadOnlyCollection<string> AllIds => _providers.Keys.ToArray();

    public bool TryGet(string vendorId, out ITtsProvider provider) =>
        _providers.TryGetValue(vendorId, out provider!);
}
