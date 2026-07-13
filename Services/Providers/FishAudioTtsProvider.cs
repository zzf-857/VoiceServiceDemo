using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using VoiceServiceDemo.Models;

namespace VoiceServiceDemo.Services.Providers;

public sealed class FishAudioTtsProvider
{
    private const string TtsEndpoint = "https://api.fish.audio/v1/tts";
    private const string ModelsEndpoint = "https://api.fish.audio/model?page_size=50&page_number=1&sort_by=task_count";
    private const string DefaultModel = "s2-pro";

    private readonly HttpClient _httpClient;
    private readonly SettingsService _settingsService;

    public FishAudioTtsProvider(HttpClient httpClient, SettingsService settingsService)
    {
        _httpClient = httpClient;
        _settingsService = settingsService;
    }

    public Task<(bool Success, string Message)> TestConnectivityAsync(string apiKey)
    {
        return Task.FromResult(string.IsNullOrWhiteSpace(apiKey)
            ? (false, "Fish Audio API Key 为空，请先填写。")
            : (true, "Fish Audio API Key 已填写。连通性将在生成或刷新音色库时验证，测试按钮不会发起语音合成以避免消耗额度。"));
    }

    public async Task<TtsResult> GenerateAsync(TtsRequest request, string apiKey)
    {
        var outputFormat = NormalizeOutputFormat(request.OutputFormat);
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, TtsEndpoint);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
        httpRequest.Headers.Add("model", NormalizeModelId(request.ModelId));
        httpRequest.Content = new StringContent(BuildTtsRequestJson(request), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(httpRequest);
        var audioBytes = await response.Content.ReadAsByteArrayAsync();

        if (!response.IsSuccessStatusCode)
        {
            var error = DecodeResponseText(audioBytes);
            return new TtsResult { Success = false, ErrorMessage = $"Fish Audio API 错误 ({response.StatusCode}): {error}" };
        }

        if (audioBytes.Length == 0)
            return new TtsResult { Success = false, ErrorMessage = "Fish Audio 返回了空音频数据。" };

        var filePath = GetOutputFilePath(outputFormat);
        await File.WriteAllBytesAsync(filePath, audioBytes);

        var vendor = VendorRegistry.GetById("fish_audio");
        return new TtsResult
        {
            Success = true,
            FilePath = filePath,
            VendorName = vendor?.Name ?? "",
            ModelName = NormalizeModelId(request.ModelId),
            VoiceName = string.IsNullOrWhiteSpace(request.VoiceId) ? "Fish Audio 默认音色" : request.VoiceId,
            Text = request.Text
        };
    }

    public async Task<List<VoiceOption>> FetchVoicesAsync(string apiKey)
    {
        var httpRequest = new HttpRequestMessage(HttpMethod.Get, ModelsEndpoint);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());

        var response = await _httpClient.SendAsync(httpRequest);
        if (!response.IsSuccessStatusCode)
            return new List<VoiceOption>();

        var json = await response.Content.ReadAsStringAsync();
        return ParseModelsJson(json);
    }

    public static string BuildTtsRequestJson(TtsRequest request)
    {
        var body = new Dictionary<string, object?>
        {
            ["text"] = request.Text,
            ["format"] = NormalizeOutputFormat(request.OutputFormat),
            ["mp3_bitrate"] = 128,
            ["opus_bitrate"] = 32,
            ["latency"] = "balanced",
            ["normalize"] = true,
            ["temperature"] = 0.7,
            ["top_p"] = 0.7,
            ["prosody"] = new
            {
                speed = Clamp(request.Speed, 0.5, 2.0, 1.0),
                volume = Clamp(request.Volume, -20.0, 20.0, 0.0),
                normalize_loudness = true
            }
        };

        if (!string.IsNullOrWhiteSpace(request.VoiceId))
            body["reference_id"] = request.VoiceId.Trim();

        return JsonSerializer.Serialize(body);
    }

    public static List<VoiceOption> ParseModelsJson(string json)
    {
        var voices = new List<VoiceOption>();

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
                return voices;

            foreach (var item in items.EnumerateArray())
            {
                var id = TryGetString(item, "_id");
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                var tags = ReadStringArray(item, "tags");
                var languages = ReadStringArray(item, "languages");
                var visibility = TryGetString(item, "visibility");
                var categories = tags
                    .Concat(string.IsNullOrWhiteSpace(visibility) ? Array.Empty<string>() : new[] { visibility })
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                voices.Add(new VoiceOption
                {
                    Id = id,
                    Name = TryGetString(item, "title") ?? id,
                    Gender = InferGender(tags),
                    Language = languages.Any() ? string.Join(", ", languages) : "多语言",
                    SampleUrl = TryGetFirstSampleAudio(item),
                    Categories = categories
                });
            }
        }
        catch
        {
            return new List<VoiceOption>();
        }

        return voices;
    }

    public static string NormalizeModelId(string modelId)
    {
        var normalized = (modelId ?? "").Trim();
        return string.IsNullOrWhiteSpace(normalized) ? DefaultModel : normalized;
    }

    public static string NormalizeOutputFormat(string outputFormat)
    {
        var normalized = (outputFormat ?? "").Trim().ToLowerInvariant();
        return normalized is "mp3" or "wav" or "pcm" or "opus" ? normalized : "mp3";
    }

    public static string GetOutputFormatExtension(string outputFormat) =>
        NormalizeOutputFormat(outputFormat) switch
        {
            "wav" => ".wav",
            "pcm" => ".pcm",
            "opus" => ".opus",
            _ => ".mp3"
        };

    private string GetOutputFilePath(string outputFormat)
    {
        var dir = _settingsService.Settings.OutputDirectory;
        return AudioOutputPath.Reserve(dir, "fish_audio", GetOutputFormatExtension(outputFormat));
    }

    private static string InferGender(IReadOnlyList<string> tags)
    {
        var combined = string.Join(" ", tags).ToLowerInvariant();
        if (combined.Contains("female") || combined.Contains("woman") || combined.Contains("女"))
            return "女";
        if (combined.Contains("male") || combined.Contains("man") || combined.Contains("男"))
            return "男";
        return "中性";
    }

    private static string? TryGetFirstSampleAudio(JsonElement item)
    {
        if (!item.TryGetProperty("samples", out var samples) || samples.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var sample in samples.EnumerateArray())
        {
            var audio = TryGetString(sample, "audio");
            if (!string.IsNullOrWhiteSpace(audio))
                return audio;

            if (sample.TryGetProperty("audio", out var audioElement) && audioElement.ValueKind == JsonValueKind.Object)
            {
                audio = TryGetString(audioElement, "url") ?? TryGetString(audioElement, "src");
                if (!string.IsNullOrWhiteSpace(audio))
                    return audio;
            }
        }

        return null;
    }

    private static List<string> ReadStringArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var array) || array.ValueKind != JsonValueKind.Array)
            return new List<string>();

        return array
            .EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToList();
    }

    private static double Clamp(double value, double min, double max, double fallback)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            return fallback;
        return Math.Min(max, Math.Max(min, value));
    }

    private static string DecodeResponseText(byte[] bytes)
    {
        if (bytes.Length == 0)
            return "";

        try
        {
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return Convert.ToBase64String(bytes);
        }
    }

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object &&
               element.TryGetProperty(propertyName, out var value) &&
               value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }
}
