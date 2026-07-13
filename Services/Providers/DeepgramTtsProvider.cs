using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using VoiceServiceDemo.Models;

namespace VoiceServiceDemo.Services.Providers;

public sealed class DeepgramTtsProvider
{
    private const string SpeakEndpoint = "https://api.deepgram.com/v1/speak";
    private const string ModelsEndpoint = "https://api.deepgram.com/v1/models";
    private const string DefaultModel = "aura-2-thalia-en";

    private readonly HttpClient _httpClient;
    private readonly SettingsService _settingsService;

    public DeepgramTtsProvider(HttpClient httpClient, SettingsService settingsService)
    {
        _httpClient = httpClient;
        _settingsService = settingsService;
    }

    public async Task<(bool Success, string Message)> TestConnectivityAsync(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return (false, "Deepgram API Key 为空，请先填写。");

        var req = new HttpRequestMessage(HttpMethod.Get, ModelsEndpoint);
        req.Headers.Authorization = new AuthenticationHeaderValue("Token", apiKey.Trim());
        var resp = await _httpClient.SendAsync(req);

        return resp.IsSuccessStatusCode
            ? (true, "Deepgram 连接成功，可以刷新 Aura TTS 模型列表。")
            : (false, $"Deepgram 鉴权或模型列表请求失败 ({resp.StatusCode})");
    }

    public async Task<TtsResult> GenerateAsync(TtsRequest request, string apiKey)
    {
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, BuildSpeakUri(request));
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Token", apiKey.Trim());
        httpRequest.Content = new StringContent(BuildSpeakRequestJson(request), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(httpRequest);
        var audioBytes = await response.Content.ReadAsByteArrayAsync();

        if (!response.IsSuccessStatusCode)
        {
            var error = DecodeResponseText(audioBytes);
            return new TtsResult { Success = false, ErrorMessage = $"Deepgram API 错误 ({response.StatusCode}): {error}" };
        }

        if (audioBytes.Length == 0)
            return new TtsResult { Success = false, ErrorMessage = "Deepgram 返回了空音频数据。" };

        var outputFormat = NormalizeOutputFormat(request.OutputFormat);
        var filePath = GetOutputFilePath(outputFormat);
        await File.WriteAllBytesAsync(filePath, audioBytes);

        var vendor = VendorRegistry.GetById("deepgram");
        var modelId = ResolveModelId(request);
        return new TtsResult
        {
            Success = true,
            FilePath = filePath,
            VendorName = vendor?.Name ?? "",
            ModelName = modelId,
            VoiceName = modelId,
            Text = request.Text
        };
    }

    public async Task<List<VoiceOption>> FetchVoicesAsync(string apiKey)
    {
        var httpRequest = new HttpRequestMessage(HttpMethod.Get, ModelsEndpoint);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Token", apiKey.Trim());

        var response = await _httpClient.SendAsync(httpRequest);
        if (!response.IsSuccessStatusCode)
            return new List<VoiceOption>();

        var json = await response.Content.ReadAsStringAsync();
        return ParseModelsJson(json);
    }

    public static string BuildSpeakRequestJson(TtsRequest request)
    {
        var body = new
        {
            text = request.Text
        };

        return JsonSerializer.Serialize(body);
    }

    public static Uri BuildSpeakUri(TtsRequest request)
    {
        var outputFormat = NormalizeOutputFormat(request.OutputFormat);
        var media = GetMediaSettings(outputFormat);
        var query = new List<string>
        {
            $"model={Uri.EscapeDataString(ResolveModelId(request))}",
            $"encoding={Uri.EscapeDataString(media.Encoding)}"
        };

        if (!string.IsNullOrWhiteSpace(media.Container))
            query.Add($"container={Uri.EscapeDataString(media.Container)}");

        query.Add($"speed={FormatDouble(Clamp(request.Speed, 0.7, 1.5, 1.0))}");
        return new Uri($"{SpeakEndpoint}?{string.Join("&", query)}");
    }

    public static List<VoiceOption> ParseModelsJson(string json)
    {
        var voices = new List<VoiceOption>();

        try
        {
            using var doc = JsonDocument.Parse(json);
            foreach (var item in EnumerateTtsModelItems(doc.RootElement))
            {
                var id = TryGetString(item, "canonical_name") ?? TryGetString(item, "id");
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                var friendlyName = TryGetString(item, "name") ?? TryGetString(item, "display_name");
                var metadata = item.TryGetProperty("metadata", out var metadataElement) && metadataElement.ValueKind == JsonValueKind.Object
                    ? metadataElement
                    : default;

                voices.Add(new VoiceOption
                {
                    Id = id,
                    Name = BuildDisplayName(id, friendlyName),
                    Gender = NormalizeGender(TryGetString(metadata, "gender") ?? TryGetString(item, "gender")),
                    Language = TryGetString(item, "language") ?? ReadStringArray(item, "languages").FirstOrDefault() ?? InferLanguage(id),
                    Categories = BuildCategories(item, metadata)
                });
            }
        }
        catch
        {
            return new List<VoiceOption>();
        }

        return voices;
    }

    public static string NormalizeOutputFormat(string outputFormat)
    {
        var normalized = (outputFormat ?? "").Trim().ToLowerInvariant();
        return normalized is "mp3" or "wav" or "opus" or "flac" ? normalized : "mp3";
    }

    public static string GetOutputFormatExtension(string outputFormat) =>
        NormalizeOutputFormat(outputFormat) switch
        {
            "wav" => ".wav",
            "opus" => ".opus",
            "flac" => ".flac",
            _ => ".mp3"
        };

    private static IEnumerable<JsonElement> EnumerateTtsModelItems(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
                yield return item;
            yield break;
        }

        if (root.ValueKind != JsonValueKind.Object)
            yield break;

        if (root.TryGetProperty("tts", out var tts) && tts.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in tts.EnumerateArray())
                yield return item;
        }

        if (root.TryGetProperty("models", out var models) && models.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in models.EnumerateArray())
            {
                var id = TryGetString(item, "canonical_name") ?? TryGetString(item, "id") ?? "";
                var type = TryGetString(item, "type") ?? TryGetString(item, "model_type") ?? "";
                if (id.StartsWith("aura-", StringComparison.OrdinalIgnoreCase) ||
                    type.Equals("tts", StringComparison.OrdinalIgnoreCase))
                    yield return item;
            }
        }
    }

    private static string ResolveModelId(TtsRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.VoiceId))
            return request.VoiceId.Trim();

        if (!string.IsNullOrWhiteSpace(request.ModelId))
            return request.ModelId.Trim();

        return DefaultModel;
    }

    private static (string Encoding, string Container) GetMediaSettings(string outputFormat)
    {
        return NormalizeOutputFormat(outputFormat) switch
        {
            "wav" => ("linear16", "wav"),
            "opus" => ("opus", "ogg"),
            "flac" => ("flac", ""),
            _ => ("mp3", "")
        };
    }

    private string GetOutputFilePath(string outputFormat)
    {
        var dir = _settingsService.Settings.OutputDirectory;
        return AudioOutputPath.Reserve(dir, "deepgram", GetOutputFormatExtension(outputFormat));
    }

    private static List<string> BuildCategories(JsonElement item, JsonElement metadata)
    {
        var values = new List<string>();
        AddCategory(values, TryGetString(item, "architecture"));
        AddCategory(values, TryGetString(item, "model"));
        AddCategory(values, TryGetString(metadata, "accent"));
        foreach (var tag in ReadStringArray(metadata, "tags"))
            AddCategory(values, tag);
        return values;
    }

    private static string BuildDisplayName(string id, string? friendlyName)
    {
        if (!string.IsNullOrWhiteSpace(friendlyName) &&
            !friendlyName.Equals(id, StringComparison.OrdinalIgnoreCase))
            return $"{friendlyName.Trim()} ({id})";

        var parts = id.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 3)
            return $"{CultureInfo.InvariantCulture.TextInfo.ToTitleCase(parts[^2])} ({id})";

        return id;
    }

    private static string NormalizeGender(string? value)
    {
        var normalized = (value ?? "").Trim().ToLowerInvariant();
        return normalized switch
        {
            "female" or "woman" => "女",
            "male" or "man" => "男",
            _ => "中性"
        };
    }

    private static string InferLanguage(string id)
    {
        var parts = id.Split('-', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? "多语言" : parts[^1];
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

    private static void AddCategory(List<string> values, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        var normalized = value.Trim();
        if (!values.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            values.Add(normalized);
    }

    private static double Clamp(double value, double min, double max, double fallback)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
            return fallback;
        return Math.Min(max, Math.Max(min, value));
    }

    private static string FormatDouble(double value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);

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
