using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using VoiceServiceDemo.Models;

namespace VoiceServiceDemo.Services.Providers;

public sealed class AzureTtsProvider
{
    private readonly HttpClient _httpClient;
    private readonly SettingsService _settingsService;

    public AzureTtsProvider(HttpClient httpClient, SettingsService settingsService)
    {
        _httpClient = httpClient;
        _settingsService = settingsService;
    }

    public async Task<(bool Success, string Message)> TestConnectivityAsync(string apiKey)
    {
        var keys = ParseCredentials(apiKey);
        if (keys == null)
            return (false, "格式错误，应为: subscription_key|region");

        var url = $"https://{keys.Value.Region}.api.cognitive.microsoft.com/sts/v1.0/issuetoken";
        var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Add("Ocp-Apim-Subscription-Key", keys.Value.SubscriptionKey);
        req.Content = new StringContent("", Encoding.UTF8);
        var resp = await _httpClient.SendAsync(req);
        return resp.IsSuccessStatusCode
            ? (true, "Azure 连接成功 ✓")
            : (false, $"鉴权失败 ({resp.StatusCode})");
    }

    public async Task<List<VoiceOption>> FetchVoicesAsync(string apiKey)
    {
        var keys = ParseCredentials(apiKey);
        if (keys == null)
            return new List<VoiceOption>();

        var req = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://{keys.Value.Region}.tts.speech.microsoft.com/cognitiveservices/voices/list");
        req.Headers.Add("Ocp-Apim-Subscription-Key", keys.Value.SubscriptionKey);
        var resp = await _httpClient.SendAsync(req);
        if (!resp.IsSuccessStatusCode)
            return new List<VoiceOption>();

        var json = await resp.Content.ReadAsStringAsync();
        return ParseVoicesJson(json);
    }

    public async Task<TtsResult> GenerateAsync(TtsRequest request, string apiKey)
    {
        var keys = ParseCredentials(apiKey);
        if (keys == null)
            return new TtsResult { Success = false, ErrorMessage = "Azure Key 格式应为: subscription_key|region (如 xxxxx|eastasia)" };

        var ssml = AzureSsmlBuilder.Build(request);

        var url = $"https://{keys.Value.Region}.tts.speech.microsoft.com/cognitiveservices/v1";
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
        httpRequest.Headers.Add("Ocp-Apim-Subscription-Key", keys.Value.SubscriptionKey);
        httpRequest.Content = new StringContent(ssml, Encoding.UTF8, "application/ssml+xml");
        httpRequest.Headers.Add("X-Microsoft-OutputFormat", GetOutputFormatHeader(request.OutputFormat));

        var response = await _httpClient.SendAsync(httpRequest);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync();
            return new TtsResult { Success = false, ErrorMessage = $"Azure API 错误 ({response.StatusCode}): {err}" };
        }

        var audioBytes = await response.Content.ReadAsByteArrayAsync();
        var filePath = GetOutputFilePath(request.OutputFormat);
        await File.WriteAllBytesAsync(filePath, audioBytes);

        var vendor = VendorRegistry.GetById("azure");
        return new TtsResult
        {
            Success = true,
            FilePath = filePath,
            VendorName = vendor?.Name ?? "",
            ModelName = request.ModelId,
            VoiceName = request.VoiceId,
            Text = request.Text
        };
    }

    public static List<VoiceOption> ParseVoicesJson(string json)
    {
        var voices = new List<VoiceOption>();
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            return voices;

        foreach (var voice in doc.RootElement.EnumerateArray())
        {
            var id = TryGetString(voice, "ShortName", "shortName") ?? "";
            if (string.IsNullOrWhiteSpace(id))
                continue;

            var locale = TryGetString(voice, "Locale", "locale") ?? "";
            var displayName = TryGetString(voice, "LocalName", "localName", "DisplayName", "displayName") ?? id;
            var categories = BuildCategories(voice);

            voices.Add(new VoiceOption
            {
                Id = id,
                Name = string.IsNullOrWhiteSpace(locale) ? displayName : $"{displayName} ({locale})",
                Gender = LocalizeGender(TryGetString(voice, "Gender", "gender")),
                Language = locale,
                Categories = categories
            });
        }

        return voices;
    }

    public static string GetOutputFormatHeader(string outputFormat) =>
        NormalizeOutputFormat(outputFormat) switch
        {
            "mp3_24k" => "audio-24khz-160kbitrate-mono-mp3",
            "riff_16k_pcm" => "riff-16khz-16bit-mono-pcm",
            "riff_24k_pcm" => "riff-24khz-16bit-mono-pcm",
            "raw_16k_pcm" => "raw-16khz-16bit-mono-pcm",
            "ogg_24k_opus" => "ogg-24khz-16bit-mono-opus",
            _ => "audio-16khz-128kbitrate-mono-mp3"
        };

    public static string GetOutputFormatExtension(string outputFormat) =>
        NormalizeOutputFormat(outputFormat) switch
        {
            "riff_16k_pcm" or "riff_24k_pcm" => ".wav",
            "raw_16k_pcm" => ".pcm",
            "ogg_24k_opus" => ".ogg",
            _ => ".mp3"
        };

    private static string NormalizeOutputFormat(string outputFormat)
    {
        var normalized = (outputFormat ?? "").Trim().ToLowerInvariant();
        return normalized is "mp3_16k" or "mp3_24k" or "riff_16k_pcm" or "riff_24k_pcm" or "raw_16k_pcm" or "ogg_24k_opus"
            ? normalized
            : "mp3_16k";
    }

    private string GetOutputFilePath(string outputFormat)
    {
        var dir = _settingsService.Settings.OutputDirectory;
        return AudioOutputPath.Reserve(dir, "azure", GetOutputFormatExtension(outputFormat));
    }

    private static (string SubscriptionKey, string Region)? ParseCredentials(string apiKey)
    {
        var keys = apiKey.Split('|', StringSplitOptions.TrimEntries);
        if (keys.Length < 2 || string.IsNullOrWhiteSpace(keys[0]) || string.IsNullOrWhiteSpace(keys[1]))
            return null;
        return (keys[0], keys[1]);
    }

    private static List<string> BuildCategories(JsonElement voice)
    {
        var categories = new List<string>();
        AddIfPresent(categories, TryGetString(voice, "VoiceType", "voiceType"));

        if (voice.TryGetProperty("StyleList", out var styles) && styles.ValueKind == JsonValueKind.Array)
        {
            foreach (var style in styles.EnumerateArray())
            {
                if (style.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(style.GetString()))
                    categories.Add($"style:{style.GetString()}");
            }
        }

        return categories;
    }

    private static void AddIfPresent(List<string> values, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            values.Add(value);
    }

    private static string? TryGetString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
                return value.GetString();
        }

        return null;
    }

    private static string LocalizeGender(string? gender) =>
        gender?.ToUpperInvariant() switch
        {
            "MALE" => "男",
            "FEMALE" => "女",
            "NEUTRAL" => "中性",
            _ => gender ?? ""
        };
}
