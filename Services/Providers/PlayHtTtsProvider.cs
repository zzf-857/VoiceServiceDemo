using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using VoiceServiceDemo.Models;

namespace VoiceServiceDemo.Services.Providers;

public sealed class PlayHtCredentials
{
    private PlayHtCredentials(string userId, string apiKey)
    {
        UserId = userId;
        ApiKey = apiKey;
    }

    public string UserId { get; }
    public string ApiKey { get; }

    public static PlayHtCredentials Parse(string value)
    {
        var parts = (value ?? "").Split('|');
        if (parts.Length != 2 || parts.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("PlayHT 凭证格式应为 user_id|api_key。", nameof(value));

        return new PlayHtCredentials(parts[0].Trim(), parts[1].Trim());
    }

    public override string ToString() => $"{UserId}|***";
}

public sealed class PlayHtTtsProvider : ITtsProvider, IVoiceCatalogProvider
{
    private const string TtsEndpoint = "https://api.play.ht/api/v2/tts/stream";
    private const string VoicesEndpoint = "https://api.play.ht/api/v2/voices";
    private const string DefaultEngine = "Play3.0-mini";
    private const string DefaultVoice = "larry";

    private readonly HttpClient _httpClient;
    private readonly SettingsService _settingsService;

    public PlayHtTtsProvider(HttpClient httpClient, SettingsService settingsService)
    {
        _httpClient = httpClient;
        _settingsService = settingsService;
    }

    public string VendorId => "playht";

    public async Task<(bool Success, string Message)> TestConnectivityAsync(
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PlayHtCredentials credentials;
        try
        {
            credentials = PlayHtCredentials.Parse(apiKey);
        }
        catch (ArgumentException ex)
        {
            return (false, ex.Message);
        }

        using var request = CreateRequest(HttpMethod.Get, VoicesEndpoint, credentials);
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        return response.IsSuccessStatusCode
            ? (true, "PlayHT 连接成功，可以刷新预置音色库。")
            : (false, $"PlayHT 鉴权或音色列表请求失败 ({response.StatusCode})");
    }

    public async Task<TtsResult> GenerateAsync(
        TtsRequest request,
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PlayHtCredentials credentials;
        try
        {
            credentials = PlayHtCredentials.Parse(apiKey);
        }
        catch (ArgumentException ex)
        {
            return new TtsResult { Success = false, ErrorMessage = ex.Message };
        }

        var outputFormat = NormalizeOutputFormat(request.OutputFormat);
        using var httpRequest = CreateRequest(HttpMethod.Post, TtsEndpoint, credentials);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(GetAcceptType(outputFormat)));
        httpRequest.Content = new StringContent(BuildRequestJson(request), Encoding.UTF8, "application/json");
        using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var audioBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return new TtsResult
            {
                Success = false,
                ErrorMessage = $"PlayHT API 错误 ({response.StatusCode}): {DecodeSafeText(audioBytes)}"
            };
        }

        if (audioBytes.Length == 0)
            return new TtsResult { Success = false, ErrorMessage = "PlayHT 返回了空音频数据。" };

        var filePath = AudioOutputPath.Reserve(
            _settingsService.Settings.OutputDirectory,
            VendorId,
            GetOutputExtension(outputFormat));
        try
        {
            await File.WriteAllBytesAsync(filePath, audioBytes, cancellationToken);
        }
        catch
        {
            TryDelete(filePath);
            throw;
        }

        var vendor = VendorRegistry.GetById(VendorId);
        return new TtsResult
        {
            Success = true,
            FilePath = filePath,
            VendorName = vendor?.Name ?? "PlayHT",
            ModelName = ResolveEngine(request.ModelId),
            VoiceName = ResolveVoice(request.VoiceId),
            Text = request.Text
        };
    }

    public async Task<List<VoiceOption>> FetchVoicesAsync(
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var credentials = PlayHtCredentials.Parse(apiKey);
        using var request = CreateRequest(HttpMethod.Get, VoicesEndpoint, credentials);
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseVoicesJson(json);
    }

    public static string BuildRequestJson(TtsRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return JsonSerializer.Serialize(new
        {
            text = request.Text,
            voice = ResolveVoice(request.VoiceId),
            voice_engine = ResolveEngine(request.ModelId),
            output_format = NormalizeOutputFormat(request.OutputFormat),
            speed = Clamp(request.Speed, 0.1, 5.0, 1.0)
        });
    }

    public static List<VoiceOption> ParseVoicesJson(string json)
    {
        var voices = new List<VoiceOption>();
        try
        {
            using var document = JsonDocument.Parse(json);
            var items = document.RootElement.ValueKind == JsonValueKind.Array
                ? document.RootElement
                : document.RootElement.ValueKind == JsonValueKind.Object &&
                  document.RootElement.TryGetProperty("voices", out var voiceArray) &&
                  voiceArray.ValueKind == JsonValueKind.Array
                    ? voiceArray
                    : default;
            if (items.ValueKind != JsonValueKind.Array)
                return voices;

            foreach (var item in items.EnumerateArray())
            {
                var id = GetString(item, "voiceId") ?? GetString(item, "voice_id") ?? GetString(item, "value");
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                var language = GetString(item, "languageCode") ?? GetString(item, "language_code") ?? GetString(item, "language") ?? "多语言";
                var categories = new List<string>();
                AddCategory(categories, GetString(item, "age"));
                AddCategory(categories, GetString(item, "accent"));
                AddCategory(categories, GetString(item, "voiceType") ?? GetString(item, "voice_type"));
                foreach (var style in ReadStrings(item, "styles"))
                    AddCategory(categories, style);

                voices.Add(new VoiceOption
                {
                    Id = id,
                    Name = GetString(item, "name") ?? id,
                    Gender = NormalizeGender(GetString(item, "gender")),
                    Language = language,
                    SampleUrl = GetString(item, "sample") ?? GetString(item, "preview_url") ?? "",
                    Categories = categories
                });
            }
        }
        catch (JsonException)
        {
            return new List<VoiceOption>();
        }

        return voices;
    }

    public static string NormalizeOutputFormat(string? outputFormat)
    {
        var normalized = (outputFormat ?? "").Trim().ToLowerInvariant();
        return normalized is "mp3" or "wav" or "ogg" or "flac" or "mulaw" ? normalized : "mp3";
    }

    public static string GetOutputExtension(string? outputFormat) => NormalizeOutputFormat(outputFormat) switch
    {
        "wav" => ".wav",
        "ogg" => ".ogg",
        "flac" => ".flac",
        "mulaw" => ".ulaw",
        _ => ".mp3"
    };

    private static HttpRequestMessage CreateRequest(HttpMethod method, string endpoint, PlayHtCredentials credentials)
    {
        var request = new HttpRequestMessage(method, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credentials.ApiKey);
        request.Headers.Add("X-USER-ID", credentials.UserId);
        return request;
    }

    private static string ResolveEngine(string? modelId) =>
        string.IsNullOrWhiteSpace(modelId) ? DefaultEngine : modelId.Trim();

    private static string ResolveVoice(string? voiceId) =>
        string.IsNullOrWhiteSpace(voiceId) ? DefaultVoice : voiceId.Trim();

    private static double Clamp(double value, double min, double max, double fallback)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
            return fallback;
        return Math.Min(max, Math.Max(min, value));
    }

    private static string GetAcceptType(string outputFormat) => outputFormat switch
    {
        "wav" => "audio/wav",
        "ogg" => "audio/ogg",
        "flac" => "audio/flac",
        "mulaw" => "audio/basic",
        _ => "audio/mpeg"
    };

    private static string NormalizeGender(string? value) => (value ?? "").Trim().ToLowerInvariant() switch
    {
        "female" or "woman" => "女",
        "male" or "man" => "男",
        _ => "中性"
    };

    private static string? GetString(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(propertyName, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static IEnumerable<string> ReadStrings(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(propertyName, out var values) ||
            values.ValueKind != JsonValueKind.Array)
            return Array.Empty<string>();

        return values.EnumerateArray()
            .Where(value => value.ValueKind == JsonValueKind.String)
            .Select(value => value.GetString())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToArray();
    }

    private static void AddCategory(List<string> categories, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;
        var normalized = value.Trim();
        if (!categories.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            categories.Add(normalized);
    }

    private static string DecodeSafeText(byte[] bytes)
    {
        if (bytes.Length == 0)
            return "空响应";
        var text = Encoding.UTF8.GetString(bytes).Replace("\r", " ").Replace("\n", " ").Trim();
        return text.Length <= 512 ? text : text[..512];
    }

    private static void TryDelete(string filePath)
    {
        try
        {
            File.Delete(filePath);
        }
        catch
        {
            // Best-effort cleanup after a failed write.
        }
    }
}
