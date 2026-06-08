using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using VoiceServiceDemo.Models;

namespace VoiceServiceDemo.Services.Providers;

public sealed class GoogleTtsProvider
{
    private readonly HttpClient _httpClient;
    private readonly SettingsService _settingsService;

    public GoogleTtsProvider(HttpClient httpClient, SettingsService settingsService)
    {
        _httpClient = httpClient;
        _settingsService = settingsService;
    }

    public async Task<(bool Success, string Message)> TestConnectivityAsync(string apiKey)
    {
        var url = $"https://texttospeech.googleapis.com/v1/voices?key={apiKey}";
        var resp = await _httpClient.GetAsync(url);
        return resp.IsSuccessStatusCode
            ? (true, "Google 连接成功 ✓")
            : (false, $"鉴权失败 ({resp.StatusCode})");
    }

    public async Task<TtsResult> GenerateAsync(TtsRequest request, string apiKey)
    {
        var url = $"https://texttospeech.googleapis.com/v1/text:synthesize?key={apiKey}";
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
        httpRequest.Content = new StringContent(BuildSynthesizeRequestJson(request), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(httpRequest);
        var respJson = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return new TtsResult { Success = false, ErrorMessage = $"Google API 错误 ({response.StatusCode}): {respJson}" };

        using var doc = JsonDocument.Parse(respJson);
        if (doc.RootElement.TryGetProperty("audioContent", out var audioBase64))
        {
            var audioBytes = Convert.FromBase64String(audioBase64.GetString()!);
            var filePath = GetOutputFilePath();
            await File.WriteAllBytesAsync(filePath, audioBytes);

            var vendor = VendorRegistry.GetById("google");
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

        return new TtsResult { Success = false, ErrorMessage = "Google 返回结果无效: " + respJson };
    }

    public async Task<List<VoiceOption>> FetchVoicesAsync(string apiKey)
    {
        var url = $"https://texttospeech.googleapis.com/v1/voices?key={apiKey}";
        var response = await _httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
            return new List<VoiceOption>();

        var json = await response.Content.ReadAsStringAsync();
        return ParseVoicesJson(json);
    }

    public static List<VoiceOption> ParseVoicesJson(string json)
    {
        var voices = new List<VoiceOption>();
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("voices", out var voiceArray) || voiceArray.ValueKind != JsonValueKind.Array)
            return voices;

        foreach (var voice in voiceArray.EnumerateArray())
        {
            var name = TryGetString(voice, "name") ?? "";
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var language = "";
            if (voice.TryGetProperty("languageCodes", out var languageCodes) && languageCodes.ValueKind == JsonValueKind.Array)
            {
                language = string.Join(" / ", languageCodes
                    .EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString())
                    .Where(value => !string.IsNullOrWhiteSpace(value)));
            }

            voices.Add(new VoiceOption
            {
                Id = name,
                Name = string.IsNullOrWhiteSpace(language) ? name : $"{name} ({language})",
                Gender = LocalizeGender(TryGetString(voice, "ssmlGender")),
                Language = language,
                Categories = new List<string> { "Google Voice" }
            });
        }

        return voices;
    }

    public static string BuildSynthesizeRequestJson(TtsRequest request)
    {
        var input = request.InputFormat == TtsInputFormat.Ssml
            ? new Dictionary<string, string> { ["ssml"] = request.SsmlText }
            : new Dictionary<string, string> { ["text"] = request.Text };

        var body = new
        {
            input,
            voice = new
            {
                languageCode = InferLanguageCode(request.VoiceId),
                name = request.VoiceId
            },
            audioConfig = new
            {
                audioEncoding = "MP3",
                speakingRate = request.Speed,
                volumeGainDb = request.Volume
            }
        };

        return JsonSerializer.Serialize(body);
    }

    private string GetOutputFilePath()
    {
        var dir = _settingsService.Settings.OutputDirectory;
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, $"google_{DateTime.Now:yyyyMMdd_HHmmss}.mp3");
    }

    private static string InferLanguageCode(string voiceId)
    {
        if (string.IsNullOrWhiteSpace(voiceId))
            return "cmn-CN";

        var parts = voiceId.Split('-', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? $"{parts[0]}-{parts[1]}" : voiceId;
    }

    private static string? TryGetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string LocalizeGender(string? gender) =>
        gender?.ToUpperInvariant() switch
        {
            "MALE" => "男",
            "FEMALE" => "女",
            "NEUTRAL" => "中性",
            _ => gender ?? ""
        };
}
