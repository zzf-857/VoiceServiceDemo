using System.Net.Http;
using System.Text.Json;

namespace VoiceService.Shared;

public sealed record HuoshanCredentials
{
    public string AppId { get; init; } = "";
    public string AccessToken { get; init; } = "";
    public string Cluster { get; init; } = "";
    public string AccessKey { get; init; } = "";
    public string SecretKey { get; init; } = "";
    public string ApiKey { get; init; } = "";
    public string ResourceId { get; init; } = "";

    public bool HasSpeechCredentials =>
        !string.IsNullOrWhiteSpace(AppId) && !string.IsNullOrWhiteSpace(AccessToken);

    public bool HasOpenApiCredentials =>
        !string.IsNullOrWhiteSpace(AccessKey) && !string.IsNullOrWhiteSpace(SecretKey);

    public bool HasV3ApiKeyCredentials =>
        !string.IsNullOrWhiteSpace(ApiKey);

    public bool HasV3AppCredentials => HasSpeechCredentials;

    public bool HasV3Credentials => HasV3ApiKeyCredentials || HasV3AppCredentials;

    public string ClusterOrDefault =>
        string.IsNullOrWhiteSpace(Cluster) ? "volcano_tts" : Cluster;

    public string ResourceIdOrDefault =>
        string.IsNullOrWhiteSpace(ResourceId) ? HuoshanTtsProtocol.DefaultResourceId : ResourceId;

    public static HuoshanCredentials Parse(string? value)
    {
        var parts = (value ?? "").Split('|');
        return new HuoshanCredentials
        {
            AppId = GetPart(parts, 0),
            AccessToken = GetPart(parts, 1),
            Cluster = GetPart(parts, 2),
            AccessKey = GetPart(parts, 3),
            SecretKey = GetPart(parts, 4),
            ApiKey = GetPart(parts, 5),
            ResourceId = GetPart(parts, 6)
        };
    }

    public HuoshanCredentials WithPart(int index, string value)
    {
        return index switch
        {
            0 => this with { AppId = value.Trim() },
            1 => this with { AccessToken = value.Trim() },
            2 => this with { Cluster = value.Trim() },
            3 => this with { AccessKey = value.Trim() },
            4 => this with { SecretKey = value.Trim() },
            5 => this with { ApiKey = value.Trim() },
            6 => this with { ResourceId = value.Trim() },
            _ => this
        };
    }

    public string ToStorageString()
    {
        var parts = new List<string> { AppId, AccessToken, Cluster, AccessKey, SecretKey, ApiKey, ResourceId };
        while (parts.Count > 0 && string.IsNullOrEmpty(parts[^1]))
            parts.RemoveAt(parts.Count - 1);
        return string.Join("|", parts);
    }

    private static string GetPart(string[] parts, int index) =>
        index < parts.Length ? parts[index].Trim() : "";
}

public static class HuoshanTtsProtocol
{
    public const string DefaultResourceId = "seed-tts-2.0";
    public const int AsyncTextThreshold = 3000;

    public static string InferResourceId(string voiceId, string modelId, string configuredResourceId = "")
    {
        if (!string.IsNullOrWhiteSpace(configuredResourceId))
            return configuredResourceId.Trim();

        if (modelId.StartsWith("seed-", StringComparison.OrdinalIgnoreCase))
            return modelId;

        if (voiceId.StartsWith("saturn_", StringComparison.OrdinalIgnoreCase) ||
            voiceId.StartsWith("ICL_", StringComparison.OrdinalIgnoreCase))
            return "seed-icl-2.0";

        if (modelId.Contains("1.0", StringComparison.OrdinalIgnoreCase) ||
            voiceId.StartsWith("BV", StringComparison.OrdinalIgnoreCase) ||
            voiceId.StartsWith("VC_BV", StringComparison.OrdinalIgnoreCase))
            return "seed-tts-1.0";

        return DefaultResourceId;
    }

    public static object BuildV3RequestBody(string text, string speaker, double speed, double volume, string uid)
    {
        return new
        {
            user = new { uid },
            req_params = new
            {
                text,
                speaker,
                audio_params = new
                {
                    format = "mp3",
                    sample_rate = 24000,
                    speech_rate = ToV3Rate(speed),
                    loudness_rate = ToV3Rate(volume)
                }
            }
        };
    }

    public static object BuildV3AsyncSubmitBody(string text, string speaker, double speed, double volume, string uid, string uniqueId)
    {
        return new
        {
            user = new { uid },
            unique_id = uniqueId,
            req_params = new
            {
                text,
                speaker,
                audio_params = new
                {
                    format = "mp3",
                    sample_rate = 24000,
                    speech_rate = ToV3Rate(speed),
                    loudness_rate = ToV3Rate(volume)
                }
            }
        };
    }

    public static object BuildV3AsyncQueryBody(string taskId) => new { task_id = taskId };

    public static HuoshanV3StreamEvent ParseV3StreamLine(string rawLine)
    {
        var line = rawLine.Trim();
        if (line.Length == 0)
            return HuoshanV3StreamEvent.Empty;

        if (line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            line = line[5..].Trim();

        if (line.Length == 0 || line == "[DONE]")
            return HuoshanV3StreamEvent.Empty;

        using var doc = JsonDocument.Parse(line);
        var root = doc.RootElement;
        var code = TryGetInt(root, "code") ?? 0;
        var message = TryGetString(root, "message") ?? "";

        if (code == 0 && TryGetString(root, "data") is { Length: > 0 } data)
            return HuoshanV3StreamEvent.Audio(Convert.FromBase64String(data));

        if (code == 20000000)
            return HuoshanV3StreamEvent.Terminal(message);

        if (code != 0)
            return HuoshanV3StreamEvent.Error(code, message);

        return HuoshanV3StreamEvent.Empty;
    }

    public static string DescribeHuoshanError(string raw)
    {
        var lower = raw.ToLowerInvariant();
        if (lower.Contains("speaker permission denied") || lower.Contains("access denied") || raw.Contains("requested resource not granted"))
            return "音色或资源未授权，请检查音色是否已开通，以及 ResourceId 是否与音色模型版本匹配。";
        if (lower.Contains("quota exceeded") || raw.Contains("并发"))
            return "火山配额或并发超限，请降低并发或检查控制台额度。";
        if (raw.Contains("3050") || raw.Contains("音色不存在"))
            return "音色不存在或当前账号不可用，请用在线音色库刷新后重新选择。";
        if (lower.Contains("invalid authorization") || lower.Contains("unauthorized") || raw.Contains("鉴权"))
            return "火山鉴权失败，请检查 AppID/Access Token 或 V3 API Key。";
        return raw;
    }

    public static void AddV3Headers(HttpRequestMessage request, HuoshanCredentials credentials, string resourceId, string requestId)
    {
        request.Headers.TryAddWithoutValidation("X-Api-Resource-Id", resourceId);
        request.Headers.TryAddWithoutValidation("X-Api-Request-Id", requestId);

        if (credentials.HasV3ApiKeyCredentials)
        {
            request.Headers.TryAddWithoutValidation("X-Api-Key", credentials.ApiKey);
            return;
        }

        request.Headers.TryAddWithoutValidation("X-Api-App-Id", credentials.AppId);
        request.Headers.TryAddWithoutValidation("X-Api-Access-Key", credentials.AccessToken);
    }

    public static string Serialize(object value) =>
        JsonSerializer.Serialize(value, new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });

    private static int ToV3Rate(double ratio) =>
        (int)Math.Round((Math.Clamp(ratio, 0.5, 2.0) - 1.0) * 100);

    private static string? TryGetString(JsonElement elem, params string[] names)
    {
        foreach (var name in names)
            if (elem.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
                return value.GetString();
        return null;
    }

    private static int? TryGetInt(JsonElement elem, params string[] names)
    {
        foreach (var name in names)
        {
            if (!elem.TryGetProperty(name, out var value))
                continue;
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
                return number;
            if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out number))
                return number;
        }
        return null;
    }
}

public sealed record HuoshanV3StreamEvent(bool HasAudio, byte[] AudioBytes, bool IsTerminal, bool IsError, int Code, string ErrorMessage)
{
    public static HuoshanV3StreamEvent Empty { get; } = new(false, Array.Empty<byte>(), false, false, 0, "");

    public static HuoshanV3StreamEvent Audio(byte[] audioBytes) =>
        new(true, audioBytes, false, false, 0, "");

    public static HuoshanV3StreamEvent Terminal(string message) =>
        new(false, Array.Empty<byte>(), true, false, 20000000, message);

    public static HuoshanV3StreamEvent Error(int code, string message) =>
        new(false, Array.Empty<byte>(), false, true, code, message);
}
