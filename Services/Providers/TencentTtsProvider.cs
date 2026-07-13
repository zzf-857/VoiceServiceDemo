using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using VoiceServiceDemo.Helpers;
using VoiceServiceDemo.Models;
using VoiceServiceDemo.Services;

namespace VoiceServiceDemo.Services.Providers;

public sealed class TencentTtsProvider : ITtsProvider, IVoiceCatalogProvider
{
    private readonly HttpClient _httpClient;
    private readonly SettingsService _settingsService;

    public TencentTtsProvider(HttpClient httpClient, SettingsService settingsService)
    {
        _httpClient = httpClient;
        _settingsService = settingsService;
    }

    public string VendorId => "tencent";

    public async Task<(bool Success, string Message)> TestConnectivityAsync(
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var keys = apiKey.Split('|');
        if (keys.Length < 2)
            return (false, "格式错误，应为: SecretId|SecretKey");

        var secretId = keys.Length >= 3 ? keys[1] : keys[0];
        var secretKey = keys.Length >= 3 ? keys[2] : keys[1];

        var body = "{\"Text\":\"test\",\"SessionId\":\"test\",\"VoiceType\":101001,\"Codec\":\"mp3\"}";
        var bodyBytes = Encoding.UTF8.GetBytes(body);

        var req = new HttpRequestMessage(HttpMethod.Post, "https://tts.tencentcloudapi.com");
        req.Headers.Add("X-TC-Action", "TextToVoice");
        req.Headers.Add("X-TC-Version", "2019-08-23");
        req.Headers.Add("X-TC-Region", "ap-shanghai");

        TencentSigner.SignRequest(req, secretId, secretKey, "tts", bodyBytes);

        var resp = await _httpClient.SendAsync(req, cancellationToken);
        var json = await resp.Content.ReadAsStringAsync(cancellationToken);
        if (json.Contains("AuthFailure", StringComparison.OrdinalIgnoreCase) ||
            json.Contains("UnauthorizedOperation", StringComparison.OrdinalIgnoreCase))
            return (false, "鉴权失败，请检查 SecretId/SecretKey");

        return (true, "腾讯云 连接成功 ✓");
    }

    public async Task<List<VoiceOption>> FetchVoicesAsync(
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var keys = apiKey.Split('|');
        if (keys.Length < 2)
            throw new InvalidOperationException("腾讯云 Key 格式应为: SecretId|SecretKey");

        var secretId = keys.Length >= 3 ? keys[1] : keys[0];
        var secretKey = keys.Length >= 3 ? keys[2] : keys[1];

        var body = "{\"WebsiteType\":1}";
        var bodyBytes = Encoding.UTF8.GetBytes(body);

        var req = new HttpRequestMessage(HttpMethod.Post, "https://tts.tencentcloudapi.com");
        req.Headers.Add("X-TC-Action", "DescribeVoices");
        req.Headers.Add("X-TC-Version", "2019-08-23");
        req.Headers.Add("X-TC-Region", "ap-shanghai");

        TencentSigner.SignRequest(req, secretId, secretKey, "tts", bodyBytes);

        var resp = await _httpClient.SendAsync(req, cancellationToken);
        var json = await resp.Content.ReadAsStringAsync(cancellationToken);

        try { await File.WriteAllTextAsync(Path.Combine(_settingsService.Settings.OutputDirectory, "tencent_voices_debug.json"), json, cancellationToken); }
        catch (OperationCanceledException) { throw; }
        catch { }

        resp.EnsureSuccessStatusCode();

        var options = new List<VoiceOption>();
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("Response", out var response))
            throw new InvalidDataException("腾讯云音色响应缺少 Response。");
        if (response.TryGetProperty("Error", out var error))
            throw new InvalidOperationException($"腾讯云音色接口返回业务错误: {error}");
        if (!response.TryGetProperty("CategoryVoiceList", out var categoryList))
            return options;
        if (categoryList.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("腾讯云音色响应的 CategoryVoiceList 不是数组。");

        foreach (var category in categoryList.EnumerateArray())
        {
            var categoryName = TryGetStr(category, "CategoryName") ?? "";
            if (!category.TryGetProperty("VoiceList", out var voiceList)) continue;
            if (voiceList.ValueKind != JsonValueKind.Array) continue;

            foreach (var voice in voiceList.EnumerateArray())
            {
                var voiceType = "";
                if (voice.TryGetProperty("VoiceType", out var vt))
                {
                    voiceType = vt.ValueKind == JsonValueKind.Number
                        ? vt.GetInt64().ToString()
                        : vt.GetString() ?? "";
                }
                if (string.IsNullOrEmpty(voiceType)) continue;

                var voiceName = TryGetStr(voice, "VoiceName") ?? voiceType;
                var voiceDesc = TryGetStr(voice, "VoiceDesc") ?? "";
                var displayName = string.IsNullOrEmpty(voiceDesc)
                    ? voiceName
                    : $"{voiceName} ({voiceDesc})";

                var gender = TryGetStr(voice, "VoiceGender") ?? "";
                gender = gender switch
                {
                    "female" => "女",
                    "male" => "男",
                    "boy" => "男童",
                    "girl" => "女童",
                    _ => gender
                };

                var sampleUrl = TryGetStr(voice, "VoiceAudio");
                var language = categoryName == "外语" ? "英文" : categoryName == "方言" ? "方言" : "中文";

                options.Add(new VoiceOption
                {
                    Id = voiceType,
                    Name = displayName,
                    Gender = gender,
                    Language = language,
                    SampleUrl = sampleUrl,
                    Categories = new List<string> { categoryName }
                });
            }
        }

        return options;
    }

    public async Task<TtsResult> GenerateAsync(
        TtsRequest request,
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var keys = apiKey.Split('|');
        if (keys.Length < 2)
            return new TtsResult { Success = false, ErrorMessage = "腾讯云 Key 格式应为: SecretId|SecretKey" };

        var secretId = keys.Length >= 3 ? keys[1] : keys[0];
        var secretKey = keys.Length >= 3 ? keys[2] : keys[1];

        var body = BuildTextToVoiceRequestJson(request, Guid.NewGuid().ToString());
        var bodyBytes = Encoding.UTF8.GetBytes(body);

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://tts.tencentcloudapi.com");
        httpRequest.Headers.Add("X-TC-Action", "TextToVoice");
        httpRequest.Headers.Add("X-TC-Version", "2019-08-23");
        httpRequest.Headers.Add("X-TC-Region", "ap-shanghai");

        TencentSigner.SignRequest(httpRequest, secretId, secretKey, "tts", bodyBytes);

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var respJson = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            return new TtsResult { Success = false, ErrorMessage = $"腾讯云 API 错误 ({response.StatusCode}): {respJson}" };

        var doc = JsonDocument.Parse(respJson);
        if (doc.RootElement.TryGetProperty("Response", out var respObj))
        {
            if (respObj.TryGetProperty("Error", out var error))
            {
                var code = error.TryGetProperty("Code", out var c) ? c.GetString() : "";
                var msg = error.TryGetProperty("Message", out var m) ? m.GetString() : "";
                return new TtsResult { Success = false, ErrorMessage = $"腾讯云 API 错误: [{code}] {msg}" };
            }

            if (respObj.TryGetProperty("Audio", out var audioBase64) && audioBase64.ValueKind == JsonValueKind.String)
            {
                var audioBytes = Convert.FromBase64String(audioBase64.GetString()!);
                return await SaveAudioBytesAsync(audioBytes, request, cancellationToken);
            }
        }

        return new TtsResult { Success = false, ErrorMessage = "腾讯云返回结果无法解析: " + respJson };
    }

    public static string BuildTextToVoiceRequestJson(TtsRequest request, string sessionId)
    {
        var body = new Dictionary<string, object?>
        {
            ["Text"] = request.Text,
            ["SessionId"] = sessionId,
            ["VoiceType"] = long.Parse(request.VoiceId),
            ["Codec"] = NormalizeCodec(request.OutputFormat),
            ["Speed"] = (int)Math.Round(request.Speed),
            ["Volume"] = (int)Math.Round(request.Volume)
        };

        var emotion = TencentEmotionPolicy.ToRequestEmotion(request.Emotion);
        if (!string.IsNullOrWhiteSpace(emotion))
        {
            body["EmotionCategory"] = emotion;
            body["EmotionIntensity"] = TencentEmotionPolicy.ClampIntensity(request.EmotionIntensity);
        }

        return JsonSerializer.Serialize(body);
    }

    private async Task<TtsResult> SaveAudioBytesAsync(
        byte[] audioBytes,
        TtsRequest request,
        CancellationToken cancellationToken)
    {
        var filePath = GetOutputFilePath(request.OutputFormat);
        await File.WriteAllBytesAsync(filePath, audioBytes, cancellationToken);

        var vendor = VendorRegistry.GetById("tencent");
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

    public static string GetOutputFormatExtension(string outputFormat) =>
        "." + NormalizeCodec(outputFormat);

    private static string NormalizeCodec(string outputFormat)
    {
        var normalized = (outputFormat ?? "").Trim().ToLowerInvariant();
        return normalized is "mp3" or "wav" or "pcm" ? normalized : "mp3";
    }

    private string GetOutputFilePath(string outputFormat)
    {
        var dir = _settingsService.Settings.OutputDirectory;
        return AudioOutputPath.Reserve(dir, "tencent", GetOutputFormatExtension(outputFormat));
    }

    private static string? TryGetStr(JsonElement elem, params string[] names)
    {
        foreach (var n in names)
            if (elem.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String)
                return v.GetString();
        return null;
    }
}
