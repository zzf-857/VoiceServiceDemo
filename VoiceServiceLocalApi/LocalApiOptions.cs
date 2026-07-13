namespace VoiceServiceLocalApi;

public sealed class LocalApiOptions
{
    public bool Enabled { get; init; } = true;
    public int Port { get; init; } = 5055;
    public bool AllowRemote { get; init; }
    public string AccessToken { get; init; } = "";
    public int MaxConcurrentRequests { get; init; } = 2;
    public int MaxTextLength { get; init; } = 20_000;

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(AccessToken))
            errors.Add("Access token is required.");
        if (Port is < 1024 or > 65_535)
            errors.Add("Port must be between 1024 and 65535.");
        if (MaxConcurrentRequests <= 0)
            errors.Add("Max concurrent requests must be positive.");
        if (MaxTextLength is < 1 or > 20_000)
            errors.Add("Max text length must be between 1 and 20000.");
        return errors;
    }
}
