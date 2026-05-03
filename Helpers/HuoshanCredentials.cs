namespace VoiceServiceDemo.Helpers;

public sealed class HuoshanCredentials
{
    public string AppId { get; init; } = "";
    public string AccessToken { get; init; } = "";
    public string Cluster { get; init; } = "";
    public string AccessKey { get; init; } = "";
    public string SecretKey { get; init; } = "";

    public bool HasSpeechCredentials =>
        !string.IsNullOrWhiteSpace(AppId) && !string.IsNullOrWhiteSpace(AccessToken);

    public bool HasOpenApiCredentials =>
        !string.IsNullOrWhiteSpace(AccessKey) && !string.IsNullOrWhiteSpace(SecretKey);

    public string ClusterOrDefault =>
        string.IsNullOrWhiteSpace(Cluster) ? "volcano_tts" : Cluster;

    public static HuoshanCredentials Parse(string? value)
    {
        var parts = (value ?? "").Split('|');
        return new HuoshanCredentials
        {
            AppId = GetPart(parts, 0),
            AccessToken = GetPart(parts, 1),
            Cluster = GetPart(parts, 2),
            AccessKey = GetPart(parts, 3),
            SecretKey = GetPart(parts, 4)
        };
    }

    public HuoshanCredentials WithPart(int index, string value)
    {
        return index switch
        {
            0 => WithAppId(value),
            1 => WithAccessToken(value),
            2 => WithCluster(value),
            3 => WithAccessKey(value),
            4 => WithSecretKey(value),
            _ => this
        };
    }

    public string ToStorageString()
    {
        var parts = new List<string> { AppId, AccessToken, Cluster, AccessKey, SecretKey };
        while (parts.Count > 0 && string.IsNullOrEmpty(parts[^1]))
            parts.RemoveAt(parts.Count - 1);
        return string.Join("|", parts);
    }

    private static string GetPart(string[] parts, int index) =>
        index < parts.Length ? parts[index].Trim() : "";

    private HuoshanCredentials WithAppId(string value) => new()
    {
        AppId = value.Trim(),
        AccessToken = AccessToken,
        Cluster = Cluster,
        AccessKey = AccessKey,
        SecretKey = SecretKey
    };

    private HuoshanCredentials WithAccessToken(string value) => new()
    {
        AppId = AppId,
        AccessToken = value.Trim(),
        Cluster = Cluster,
        AccessKey = AccessKey,
        SecretKey = SecretKey
    };

    private HuoshanCredentials WithCluster(string value) => new()
    {
        AppId = AppId,
        AccessToken = AccessToken,
        Cluster = value.Trim(),
        AccessKey = AccessKey,
        SecretKey = SecretKey
    };

    private HuoshanCredentials WithAccessKey(string value) => new()
    {
        AppId = AppId,
        AccessToken = AccessToken,
        Cluster = Cluster,
        AccessKey = value.Trim(),
        SecretKey = SecretKey
    };

    private HuoshanCredentials WithSecretKey(string value) => new()
    {
        AppId = AppId,
        AccessToken = AccessToken,
        Cluster = Cluster,
        AccessKey = AccessKey,
        SecretKey = value.Trim()
    };
}
