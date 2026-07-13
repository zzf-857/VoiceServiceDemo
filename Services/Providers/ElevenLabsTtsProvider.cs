using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using VoiceServiceDemo.Models;

namespace VoiceServiceDemo.Services.Providers;

public sealed class ElevenLabsTtsProvider
{
    private const string TtsEndpointBase = "https://api.elevenlabs.io/v1/text-to-speech";
    private const string VoicesEndpoint = "https://api.elevenlabs.io/v2/voices";
    private const string DefaultModel = "eleven_multilingual_v2";
    private const string DefaultVoice = "JBFqnCBsd6RMkjVDRZzb";

    private readonly HttpClient _httpClient;
    private readonly SettingsService _settingsService;

    public ElevenLabsTtsProvider(HttpClient httpClient, SettingsService settingsService)
    {
        _httpClient = httpClient;
        _settingsService = settingsService;
    }

    public Task<(bool Success, string Message)> TestConnectivityAsync(string apiKey)
    {
        return Task.FromResult(string.IsNullOrWhiteSpace(apiKey)
            ? (false, "ElevenLabs API Key 为空，请先填写。")
            : (true, "ElevenLabs API Key 已填写。连通性将在生成或刷新音色库时验证，测试按钮不会发起语音合成以避免消耗额度。"));
    }

    public async Task<TtsResult> GenerateAsync(TtsRequest request, string apiKey)
    {
        var voiceId = string.IsNullOrWhiteSpace(request.VoiceId) ? DefaultVoice : request.VoiceId.Trim();
        var outputFormat = NormalizeOutputFormat(request.OutputFormat);
        var endpoint = $"{TtsEndpointBase}/{Uri.EscapeDataString(voiceId)}?output_format={Uri.EscapeDataString(outputFormat)}";

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
        httpRequest.Headers.Add("xi-api-key", apiKey.Trim());
        httpRequest.Content = new StringContent(BuildTextToSpeechRequestJson(request), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(httpRequest);
        var audioBytes = await response.Content.ReadAsByteArrayAsync();

        if (!response.IsSuccessStatusCode)
        {
            var error = DecodeResponseText(audioBytes);
            return new TtsResult { Success = false, ErrorMessage = $"ElevenLabs API 错误 ({response.StatusCode}): {error}" };
        }

        if (audioBytes.Length == 0)
            return new TtsResult { Success = false, ErrorMessage = "ElevenLabs 返回了空音频数据。" };

        var filePath = GetOutputFilePath(outputFormat);
        await File.WriteAllBytesAsync(filePath, audioBytes);

        var vendor = VendorRegistry.GetById("elevenlabs");
        return new TtsResult
        {
            Success = true,
            FilePath = filePath,
            VendorName = vendor?.Name ?? "",
            ModelName = string.IsNullOrWhiteSpace(request.ModelId) ? DefaultModel : request.ModelId,
            VoiceName = voiceId,
            Text = request.Text
        };
    }

    public async Task<List<VoiceOption>> FetchVoicesAsync(string apiKey)
    {
        var httpRequest = new HttpRequestMessage(HttpMethod.Get, VoicesEndpoint);
        httpRequest.Headers.Add("xi-api-key", apiKey.Trim());

        var response = await _httpClient.SendAsync(httpRequest);
        if (!response.IsSuccessStatusCode)
            return new List<VoiceOption>();

        var json = await response.Content.ReadAsStringAsync();
        return ParseVoicesJson(json);
    }

    public static string BuildTextToSpeechRequestJson(TtsRequest request)
    {
        var body = new
        {
            text = request.Text,
            model_id = string.IsNullOrWhiteSpace(request.ModelId) ? DefaultModel : request.ModelId,
            voice_settings = new
            {
                stability = 0.5,
                similarity_boost = 0.75,
                style = 0,
                use_speaker_boost = true,
                speed = Clamp(request.Speed, 0.7, 2.0, 1.0)
            }
        };

        return JsonSerializer.Serialize(body);
    }

    public static List<VoiceOption> ParseVoicesJson(string json)
    {
        var voices = new List<VoiceOption>();

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("voices", out var array) || array.ValueKind != JsonValueKind.Array)
                return voices;

            foreach (var item in array.EnumerateArray())
            {
                var id = TryGetString(item, "voice_id");
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                var name = TryGetString(item, "name");
                var category = TryGetString(item, "category");
                var labels = item.TryGetProperty("labels", out var labelElement) && labelElement.ValueKind == JsonValueKind.Object
                    ? labelElement
                    : default;
                var verifiedLanguage = GetFirstVerifiedLanguage(item);
                var language = TryGetString(verifiedLanguage, "language") ?? TryGetString(labels, "language") ?? "多语言";
                var previewUrl = TryGetString(verifiedLanguage, "preview_url") ?? TryGetString(item, "preview_url") ?? "";
                var categories = BuildCategories(category, labels);

                voices.Add(new VoiceOption
                {
                    Id = id,
                    Name = string.IsNullOrWhiteSpace(name) ? id : name,
                    Gender = NormalizeGender(TryGetString(labels, "gender")),
                    Language = language,
                    SampleUrl = previewUrl,
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

    public static string GetOutputFormatExtension(string outputFormat) =>
        NormalizeOutputFormat(outputFormat) switch
        {
            "opus_48000_32" => ".opus",
            "pcm_16000" => ".pcm",
            "ulaw_8000" => ".ulaw",
            _ => ".mp3"
        };

    public static string NormalizeOutputFormat(string outputFormat)
    {
        var normalized = (outputFormat ?? "").Trim().ToLowerInvariant();
        return normalized is "mp3_44100_128" or "opus_48000_32" or "pcm_16000" or "ulaw_8000"
            ? normalized
            : "mp3_44100_128";
    }

    private string GetOutputFilePath(string outputFormat)
    {
        var dir = _settingsService.Settings.OutputDirectory;
        return AudioOutputPath.Reserve(dir, "elevenlabs", GetOutputFormatExtension(outputFormat));
    }

    private static List<string> BuildCategories(string? category, JsonElement labels)
    {
        var values = new List<string>();
        AddCategory(values, category);
        AddCategory(values, TryGetString(labels, "accent"));
        AddCategory(values, TryGetString(labels, "age"));
        AddCategory(values, TryGetString(labels, "use_case"));
        return values;
    }

    private static void AddCategory(List<string> values, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        var normalized = value.Trim();
        if (!values.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            values.Add(normalized);
    }

    private static JsonElement GetFirstVerifiedLanguage(JsonElement voice)
    {
        if (!voice.TryGetProperty("verified_languages", out var languages) || languages.ValueKind != JsonValueKind.Array)
            return default;

        foreach (var language in languages.EnumerateArray())
        {
            if (language.ValueKind == JsonValueKind.Object)
                return language;
        }

        return default;
    }

    private static string NormalizeGender(string? value)
    {
        var normalized = (value ?? "").Trim().ToLowerInvariant();
        return normalized switch
        {
            "male" or "man" => "男",
            "female" or "woman" => "女",
            _ => "中性"
        };
    }

    private static double Clamp(double value, double min, double max, double fallback)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
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
