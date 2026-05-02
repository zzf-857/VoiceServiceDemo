using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using VoiceServiceDemo.Helpers;
using VoiceServiceDemo.Models;

namespace VoiceServiceDemo.Services.Providers;

public sealed class TencentTtsProvider
{
    private readonly HttpClient _httpClient;
    private readonly SettingsService _settingsService;

    public TencentTtsProvider(HttpClient httpClient, SettingsService settingsService)
    {
        _httpClient = httpClient;
        _settingsService = settingsService;
    }

    public async Task<(bool Success, string Message)> TestConnectivityAsync(string apiKey)
    {
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

        var resp = await _httpClient.SendAsync(req);
        var json = await resp.Content.ReadAsStringAsync();
        if (json.Contains("AuthFailure", StringComparison.OrdinalIgnoreCase) ||
            json.Contains("UnauthorizedOperation", StringComparison.OrdinalIgnoreCase))
            return (false, "鉴权失败，请检查 SecretId/SecretKey");

        return (true, "腾讯云 连接成功 ✓");
    }

    public async Task<List<VoiceOption>> FetchVoicesAsync(string apiKey)
    {
        var keys = apiKey.Split('|');
        if (keys.Length < 2) return new List<VoiceOption>();

        var secretId = keys.Length >= 3 ? keys[1] : keys[0];
        var secretKey = keys.Length >= 3 ? keys[2] : keys[1];

        var body = "{\"WebsiteType\":1}";
        var bodyBytes = Encoding.UTF8.GetBytes(body);

        var req = new HttpRequestMessage(HttpMethod.Post, "https://tts.tencentcloudapi.com");
        req.Headers.Add("X-TC-Action", "DescribeVoices");
        req.Headers.Add("X-TC-Version", "2019-08-23");
        req.Headers.Add("X-TC-Region", "ap-shanghai");

        TencentSigner.SignRequest(req, secretId, secretKey, "tts", bodyBytes);

        var resp = await _httpClient.SendAsync(req);
        var json = await resp.Content.ReadAsStringAsync();

        try { await File.WriteAllTextAsync(Path.Combine(_settingsService.Settings.OutputDirectory, "tencent_voices_debug.json"), json); } catch { }

        if (!resp.IsSuccessStatusCode)
            return new List<VoiceOption>();

        try
        {
            var errDoc = JsonDocument.Parse(json);
            if (errDoc.RootElement.TryGetProperty("Response", out var errResp) &&
                errResp.TryGetProperty("Error", out _))
                return new List<VoiceOption>();
        }
        catch { }

        var options = new List<VoiceOption>();
        try
        {
            var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("Response", out var response)) return options;
            if (!response.TryGetProperty("CategoryVoiceList", out var categoryList)) return options;
            if (categoryList.ValueKind != JsonValueKind.Array) return options;

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
        }
        catch { }

        return options;
    }

    public async Task<TtsResult> GenerateAsync(TtsRequest request, string apiKey)
    {
        var keys = apiKey.Split('|');
        if (keys.Length < 2)
            return new TtsResult { Success = false, ErrorMessage = "腾讯云 Key 格式应为: SecretId|SecretKey" };

        var secretId = keys.Length >= 3 ? keys[1] : keys[0];
        var secretKey = keys.Length >= 3 ? keys[2] : keys[1];

        var bodyObj = new
        {
            Text = request.Text,
            SessionId = Guid.NewGuid().ToString(),
            VoiceType = long.Parse(request.VoiceId),
            Codec = "mp3",
            Speed = (int)Math.Round(request.Speed),
            Volume = (int)Math.Round(request.Volume)
        };

        var body = JsonSerializer.Serialize(bodyObj);
        var bodyBytes = Encoding.UTF8.GetBytes(body);

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://tts.tencentcloudapi.com");
        httpRequest.Headers.Add("X-TC-Action", "TextToVoice");
        httpRequest.Headers.Add("X-TC-Version", "2019-08-23");
        httpRequest.Headers.Add("X-TC-Region", "ap-shanghai");

        TencentSigner.SignRequest(httpRequest, secretId, secretKey, "tts", bodyBytes);

        var response = await _httpClient.SendAsync(httpRequest);
        var respJson = await response.Content.ReadAsStringAsync();

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
                return await SaveAudioBytesAsync(audioBytes, request);
            }
        }

        return new TtsResult { Success = false, ErrorMessage = "腾讯云返回结果无法解析: " + respJson };
    }

    private async Task<TtsResult> SaveAudioBytesAsync(byte[] audioBytes, TtsRequest request)
    {
        var filePath = GetOutputFilePath();
        await File.WriteAllBytesAsync(filePath, audioBytes);

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

    private string GetOutputFilePath()
    {
        var dir = _settingsService.Settings.OutputDirectory;
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, $"tencent_{DateTime.Now:yyyyMMdd_HHmmss}.mp3");
    }

    private static string? TryGetStr(JsonElement elem, params string[] names)
    {
        foreach (var n in names)
            if (elem.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String)
                return v.GetString();
        return null;
    }
}
