using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using VoiceServiceDemo.Models;

namespace VoiceServiceDemo.Services.Providers;

public sealed class AliyunTtsProvider
{
    private readonly HttpClient _httpClient;
    private readonly SettingsService _settingsService;

    public AliyunTtsProvider(HttpClient httpClient, SettingsService settingsService)
    {
        _httpClient = httpClient;
        _settingsService = settingsService;
    }

    public async Task<(bool Success, string Message)> TestConnectivityAsync(string apiKey)
    {
        var req = new HttpRequestMessage(HttpMethod.Post,
            "https://dashscope.aliyuncs.com/api/v1/services/aigc/text2audio/generation");
        req.Headers.Add("Authorization", $"Bearer {apiKey}");
        req.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        var resp = await _httpClient.SendAsync(req);
        _ = await resp.Content.ReadAsStringAsync();

        if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized || resp.StatusCode == System.Net.HttpStatusCode.Forbidden)
            return (false, $"鉴权失败 ({resp.StatusCode})");

        return (true, "阿里云 连接成功 ✓");
    }

    public async Task<List<VoiceOption>> FetchVoicesAsync()
    {
        var options = new List<VoiceOption>();
        try
        {
            var jsonPath = Path.Combine(AppContext.BaseDirectory, "aliyun_voices_raw.json");
            if (!File.Exists(jsonPath))
                jsonPath = Path.Combine(Directory.GetCurrentDirectory(), "aliyun_voices_raw.json");
            if (!File.Exists(jsonPath))
                return options;

            var json = await File.ReadAllTextAsync(jsonPath);
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("data", out var dataWrapper) &&
                dataWrapper.TryGetProperty("DataV2", out var dataV2) &&
                dataV2.TryGetProperty("data", out var innerData) &&
                innerData.TryGetProperty("data", out var dataContent))
            {
                JsonElement voiceArray;

                if (dataContent.ValueKind == JsonValueKind.Object &&
                    dataContent.TryGetProperty("voiceConfigList", out var vcl))
                {
                    voiceArray = vcl;
                }
                else if (dataContent.ValueKind == JsonValueKind.Array)
                {
                    voiceArray = dataContent;
                }
                else
                {
                    return options;
                }

                foreach (var item in voiceArray.EnumerateArray())
                {
                    var modelId = TryGetStr(item, "defaultModelId") ?? "";
                    if (modelId.StartsWith("MiniMax", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (item.TryGetProperty("ttsVoiceConfig", out var cfg))
                        ParseVoiceConfig(cfg, options);
                }
            }

            try { await File.WriteAllTextAsync(Path.Combine(_settingsService.Settings.OutputDirectory, "aliyun_voices_debug.txt"), $"Loaded {options.Count} Aliyun voices from local JSON"); } catch { }
        }
        catch { }

        return options;
    }

    public async Task<TtsResult> GenerateAsync(TtsRequest request, string apiKey)
    {
        HttpRequestMessage httpRequest;

        if (request.ModelId.StartsWith("qwen3-tts", StringComparison.OrdinalIgnoreCase))
        {
            var body = new
            {
                model = request.ModelId,
                input = new
                {
                    text = request.Text,
                    voice = request.VoiceId
                }
            };

            httpRequest = new HttpRequestMessage(HttpMethod.Post,
                "https://dashscope.aliyuncs.com/api/v1/services/aigc/multimodal-generation/generation");
            httpRequest.Headers.Add("Authorization", $"Bearer {apiKey}");
            httpRequest.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        }
        else
        {
            var body = new
            {
                model = request.ModelId,
                input = new { text = request.Text },
                parameters = new { voice = request.VoiceId }
            };

            httpRequest = new HttpRequestMessage(HttpMethod.Post,
                "https://dashscope.aliyuncs.com/api/v1/services/aigc/text2audio/generation");
            httpRequest.Headers.Add("Authorization", $"Bearer {apiKey}");
            httpRequest.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        }

        var response = await _httpClient.SendAsync(httpRequest);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync();
            return new TtsResult { Success = false, ErrorMessage = $"阿里云 API 错误 ({response.StatusCode}): {err}" };
        }

        if (request.ModelId.StartsWith("qwen3-tts", StringComparison.OrdinalIgnoreCase))
        {
            var jsonStr = await response.Content.ReadAsStringAsync();
            try
            {
                using var jDoc = JsonDocument.Parse(jsonStr);
                var audioUrl = jDoc.RootElement
                    .GetProperty("output")
                    .GetProperty("audio")
                    .GetProperty("url")
                    .GetString();

                if (string.IsNullOrEmpty(audioUrl))
                    return new TtsResult { Success = false, ErrorMessage = "阿里云返回的音频 URL 为空" };

                var audioBytes = await _httpClient.GetByteArrayAsync(audioUrl);
                return await SaveAudioBytesAsync(audioBytes, request);
            }
            catch (Exception ex)
            {
                return new TtsResult { Success = false, ErrorMessage = $"阿里云响应解析失败: {ex.Message}\n原始响应: {jsonStr}" };
            }
        }

        return await SaveAudioResponseAsync(response, request);
    }

    private void ParseVoiceConfig(JsonElement cfg, List<VoiceOption> options)
    {
        var voiceId = TryGetStr(cfg, "voice") ?? "";
        if (string.IsNullOrEmpty(voiceId) || options.Any(o => o.Id == voiceId))
            return;

        var name = TryGetStr(cfg, "name") ?? voiceId;
        var profile = TryGetStr(cfg, "profile") ?? "";
        var genderRaw = TryGetStr(cfg, "gender") ?? "";
        var gender = "男";
        if (genderRaw.Contains("女") || genderRaw.Contains("Female", StringComparison.OrdinalIgnoreCase))
            gender = "女";

        var langRaw = TryGetStr(cfg, "language") ?? "";
        var lang = "中文";
        if (langRaw.Contains("多语") || langRaw.Contains("中英") || langRaw.Contains("English"))
            lang = "多语言";
        if (langRaw.Contains("方言") || langRaw.Contains("Dialect"))
            lang = "方言";
        if (langRaw.Contains("小语种") || langRaw.Contains("Minority"))
            lang = lang == "中文" ? "小语种" : lang;

        var sampleUrl = TryGetStr(cfg, "illustrationAudio") ?? "";
        var scenario = "";
        if (cfg.TryGetProperty("scenario", out var sc))
            scenario = TryGetStr(sc, "name") ?? "";

        var displayName = !string.IsNullOrEmpty(profile)
            ? $"{name} ({profile})"
            : $"{name} ({gender})";

        options.Add(new VoiceOption
        {
            Id = voiceId,
            Name = displayName,
            Gender = gender,
            Language = lang,
            SampleUrl = sampleUrl,
            Categories = !string.IsNullOrEmpty(scenario) ? new List<string> { scenario } : new List<string>()
        });
    }

    private async Task<TtsResult> SaveAudioResponseAsync(HttpResponseMessage response, TtsRequest request)
    {
        var audioBytes = await response.Content.ReadAsByteArrayAsync();
        return await SaveAudioBytesAsync(audioBytes, request);
    }

    private async Task<TtsResult> SaveAudioBytesAsync(byte[] audioBytes, TtsRequest request)
    {
        var filePath = GetOutputFilePath();
        await File.WriteAllBytesAsync(filePath, audioBytes);

        var vendor = VendorRegistry.GetById("aliyun");
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

    private string GetOutputFilePath()
    {
        var dir = _settingsService.Settings.OutputDirectory;
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, $"aliyun_{DateTime.Now:yyyyMMdd_HHmmss}.mp3");
    }

    private static string? TryGetStr(JsonElement elem, params string[] names)
    {
        foreach (var n in names)
            if (elem.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String)
                return v.GetString();
        return null;
    }
}
