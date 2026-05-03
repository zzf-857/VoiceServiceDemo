namespace VoiceServiceMcp.Core;

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

    public string ToStorageString()
    {
        var parts = new List<string> { AppId, AccessToken, Cluster, AccessKey, SecretKey };
        while (parts.Count > 0 && string.IsNullOrEmpty(parts[^1]))
            parts.RemoveAt(parts.Count - 1);
        return string.Join("|", parts);
    }

    private static string GetPart(string[] parts, int index) =>
        index < parts.Length ? parts[index].Trim() : "";
}
