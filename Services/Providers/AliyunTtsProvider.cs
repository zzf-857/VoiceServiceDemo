using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using VoiceServiceDemo.Models;

namespace VoiceServiceDemo.Services.Providers;

public sealed class AliyunTtsProvider
{
    private const string TextToAudioEndpoint = "https://dashscope.aliyuncs.com/api/v1/services/aigc/text2audio/generation";
    private const string QwenTtsEndpoint = "https://dashscope.aliyuncs.com/api/v1/services/aigc/multimodal-generation/generation";
    private const string CosyVoiceEndpoint = "https://dashscope.aliyuncs.com/api/v1/services/audio/tts/SpeechSynthesizer";

    private readonly HttpClient _httpClient;
    private readonly SettingsService _settingsService;

    public AliyunTtsProvider(HttpClient httpClient, SettingsService settingsService)
    {
        _httpClient = httpClient;
        _settingsService = settingsService;
    }

    public async Task<(bool Success, string Message)> TestConnectivityAsync(string apiKey)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, TextToAudioEndpoint);
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

        if (IsQwen3Model(request.ModelId))
        {
            httpRequest = new HttpRequestMessage(HttpMethod.Post, QwenTtsEndpoint);
            httpRequest.Headers.Add("Authorization", $"Bearer {apiKey}");
            httpRequest.Content = new StringContent(BuildGenerateRequestJson(request), Encoding.UTF8, "application/json");
        }
        else if (IsCosyVoiceSpeechSynthesizerModel(request.ModelId))
        {
            httpRequest = new HttpRequestMessage(HttpMethod.Post, CosyVoiceEndpoint);
            httpRequest.Headers.Add("Authorization", $"Bearer {apiKey}");
            httpRequest.Content = new StringContent(BuildGenerateRequestJson(request), Encoding.UTF8, "application/json");
        }
        else
        {
            httpRequest = new HttpRequestMessage(HttpMethod.Post, TextToAudioEndpoint);
            httpRequest.Headers.Add("Authorization", $"Bearer {apiKey}");
            httpRequest.Content = new StringContent(BuildGenerateRequestJson(request), Encoding.UTF8, "application/json");
        }

        var response = await _httpClient.SendAsync(httpRequest);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync();
            return new TtsResult { Success = false, ErrorMessage = $"阿里云 API 错误 ({response.StatusCode}): {err}" };
        }

        if (IsQwen3Model(request.ModelId) || IsCosyVoiceSpeechSynthesizerModel(request.ModelId))
        {
            var jsonStr = await response.Content.ReadAsStringAsync();
            try
            {
                if (TryExtractAudioBytes(jsonStr, out var inlineAudioBytes))
                    return await SaveAudioBytesAsync(inlineAudioBytes, request);

                var audioUrl = TryExtractAudioUrl(jsonStr);

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

    public static string BuildGenerateRequestJson(TtsRequest request)
    {
        if (IsQwen3Model(request.ModelId))
        {
            var body = new Dictionary<string, object?>
            {
                ["model"] = request.ModelId,
                ["input"] = new
                {
                    text = request.Text,
                    voice = request.VoiceId
                }
            };

            if (SupportsInstructions(request.ModelId) && !string.IsNullOrWhiteSpace(request.Instructions))
            {
                body["parameters"] = new
                {
                    instructions = request.Instructions.Trim()
                };
            }

            return JsonSerializer.Serialize(body);
        }

        if (IsCosyVoiceSpeechSynthesizerModel(request.ModelId))
        {
            var body = new
            {
                model = request.ModelId,
                input = new
                {
                    text = request.Text,
                    voice = request.VoiceId,
                    format = NormalizeCosyVoiceOutputFormat(request.OutputFormat),
                    sample_rate = 24000
                }
            };

            return JsonSerializer.Serialize(body);
        }

        var legacyBody = new
        {
            model = request.ModelId,
            input = new { text = request.Text },
            parameters = new { voice = request.VoiceId }
        };

        return JsonSerializer.Serialize(legacyBody);
    }

    public static bool SupportsInstructions(string modelId) =>
        IsQwen3InstructModel(modelId);

    public static bool IsQwen3Model(string modelId) =>
        modelId.StartsWith("qwen3-tts", StringComparison.OrdinalIgnoreCase);

    public static bool IsQwen3InstructModel(string modelId) =>
        modelId.StartsWith("qwen3-tts-instruct", StringComparison.OrdinalIgnoreCase);

    public static bool IsCosyVoiceSpeechSynthesizerModel(string modelId)
    {
        return modelId.Equals("cosyvoice-v2", StringComparison.OrdinalIgnoreCase) ||
               modelId.StartsWith("cosyvoice-v3", StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<string> GetSupportedOutputFormatsForModel(string modelId) =>
        IsQwen3Model(modelId)
            ? new[] { "wav" }
            : new[] { "mp3", "pcm", "wav", "opus" };

    public static string NormalizeOutputFormat(string modelId, string outputFormat) =>
        IsQwen3Model(modelId) ? "wav" : NormalizeCosyVoiceOutputFormat(outputFormat);

    public static string GetOutputFormatExtension(string modelId, string outputFormat) =>
        NormalizeOutputFormat(modelId, outputFormat) switch
        {
            "pcm" => ".pcm",
            "wav" => ".wav",
            "opus" => ".opus",
            _ => ".mp3"
        };

    private static string NormalizeCosyVoiceOutputFormat(string outputFormat)
    {
        var normalized = (outputFormat ?? "").Trim().ToLowerInvariant();
        return normalized is "mp3" or "pcm" or "wav" or "opus" ? normalized : "mp3";
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
        var filePath = GetOutputFilePath(request.ModelId, request.OutputFormat);
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

    private string GetOutputFilePath(string modelId, string outputFormat)
    {
        var dir = _settingsService.Settings.OutputDirectory;
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, $"aliyun_{DateTime.Now:yyyyMMdd_HHmmss}{GetOutputFormatExtension(modelId, outputFormat)}");
    }

    private static string? TryExtractAudioUrl(string jsonStr)
    {
        using var jDoc = JsonDocument.Parse(jsonStr);

        if (TryGetNested(jDoc.RootElement, out var nestedUrl, "output", "audio", "url") &&
            nestedUrl.ValueKind == JsonValueKind.String)
            return nestedUrl.GetString();

        if (TryGetNested(jDoc.RootElement, out var outputUrl, "output", "url") &&
            outputUrl.ValueKind == JsonValueKind.String)
            return outputUrl.GetString();

        return null;
    }

    private static bool TryExtractAudioBytes(string jsonStr, out byte[] audioBytes)
    {
        audioBytes = Array.Empty<byte>();

        try
        {
            using var jDoc = JsonDocument.Parse(jsonStr);

            if (TryGetNested(jDoc.RootElement, out var audioData, "output", "audio", "data") &&
                TryParseBase64String(audioData, out audioBytes))
                return true;

            if (TryGetNested(jDoc.RootElement, out var audio, "output", "audio") &&
                TryParseBase64String(audio, out audioBytes))
                return true;
        }
        catch
        {
            audioBytes = Array.Empty<byte>();
        }

        return false;
    }

    private static bool TryParseBase64String(JsonElement element, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        if (element.ValueKind != JsonValueKind.String)
            return false;

        var value = element.GetString();
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var buffer = new byte[value.Length];
        if (!Convert.TryFromBase64String(value, buffer, out var bytesWritten))
            return false;

        bytes = buffer[..bytesWritten];
        return true;
    }

    private static bool TryGetNested(JsonElement root, out JsonElement value, params string[] names)
    {
        value = root;
        foreach (var name in names)
        {
            if (!value.TryGetProperty(name, out value))
                return false;
        }

        return true;
    }

    private static string? TryGetStr(JsonElement elem, params string[] names)
    {
        foreach (var n in names)
            if (elem.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String)
                return v.GetString();
        return null;
    }
}
