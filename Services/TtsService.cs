using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using VoiceServiceDemo.Helpers;
using VoiceServiceDemo.Models;
using VoiceServiceDemo.Services.Providers;

namespace VoiceServiceDemo.Services;

/// <summary>
/// TTS 语音合成服务 - 负责调用各大厂商的 API 生成音频
/// </summary>
public class TtsService
{
    private readonly HttpClient _httpClient = new();
    private readonly SettingsService _settingsService;
    private readonly AliyunTtsProvider _aliyunProvider;
    private readonly HuoshanTtsProvider _huoshanProvider;

    public TtsService(SettingsService settingsService)
    {
        _settingsService = settingsService;
        _httpClient.Timeout = TimeSpan.FromSeconds(60);
        _aliyunProvider = new AliyunTtsProvider(_httpClient, _settingsService);
        _huoshanProvider = new HuoshanTtsProvider(_httpClient, _settingsService);
    }

    /// <summary>
    /// 测试 API Key 连通性（仅验证鉴权，不消耗 Token）
    /// </summary>
    public async Task<(bool Success, string Message)> TestConnectivityAsync(string vendorId)
    {
        var apiKey = _settingsService.GetApiKey(vendorId);
        if (string.IsNullOrWhiteSpace(apiKey))
            return (false, "API Key 为空，请先填写。");

        try
        {
            return vendorId switch
            {
                "openai" => await TestOpenAiAsync(apiKey),
                "aliyun" => await _aliyunProvider.TestConnectivityAsync(apiKey),
                "huoshan" => await _huoshanProvider.TestConnectivityAsync(apiKey),
                "tencent" => await TestTencentAsync(apiKey),
                "baidu" => await TestBaiduAsync(apiKey),
                "azure" => await TestAzureAsync(apiKey),
                "google" => await TestGoogleAsync(apiKey),
                _ => (false, "该厂商暂不支持连通性测试。")
            };
        }
        catch (Exception ex)
        {
            return (false, $"连接失败: {ex.Message}");
        }
    }

    public async Task<List<VoiceOption>> FetchVoicesAsync(string vendorId)
    {
        var apiKey = _settingsService.GetApiKey(vendorId);
        if (vendorId != "aliyun" && string.IsNullOrWhiteSpace(apiKey)) return new List<VoiceOption>();

        try
        {
            if (vendorId == "aliyun") return await _aliyunProvider.FetchVoicesAsync();
            if (vendorId == "huoshan") return await _huoshanProvider.FetchVoicesAsync(apiKey);
            if (vendorId == "tencent") return await FetchTencentVoicesAsync(apiKey);
            return new List<VoiceOption>();
        }
        catch
        {
            return new List<VoiceOption>();
        }
    }

    private static string? TryGetStr(JsonElement elem, params string[] names)
    {
        foreach (var n in names)
            if (elem.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String)
                return v.GetString();
        return null;
    }

    // ===== 连通性测试方法 =====

    private async Task<(bool, string)> TestOpenAiAsync(string apiKey)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "https://api.openai.com/v1/models");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        var resp = await _httpClient.SendAsync(req);
        return resp.IsSuccessStatusCode
            ? (true, "OpenAI 连接成功 ✓")
            : (false, $"鉴权失败 ({resp.StatusCode})");
    }

    private async Task<(bool, string)> TestBaiduAsync(string apiKey)
    {
        var keys = apiKey.Split('|');
        if (keys.Length < 2)
            return (false, "格式错误，应为: api_key|secret_key");
        var tokenUrl = $"https://aip.baidubce.com/oauth/2.0/token?grant_type=client_credentials&client_id={keys[0]}&client_secret={keys[1]}";
        var resp = await _httpClient.PostAsync(tokenUrl, null);
        var json = await resp.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("access_token", out _))
            return (true, "百度 连接成功 ✓");
        return (false, "access_token 获取失败，请检查 Key");
    }

    private async Task<(bool, string)> TestAzureAsync(string apiKey)
    {
        var keys = apiKey.Split('|');
        if (keys.Length < 2)
            return (false, "格式错误，应为: subscription_key|region");
        var url = $"https://{keys[1]}.api.cognitive.microsoft.com/sts/v1.0/issuetoken";
        var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Add("Ocp-Apim-Subscription-Key", keys[0]);
        req.Content = new StringContent("", Encoding.UTF8);
        var resp = await _httpClient.SendAsync(req);
        return resp.IsSuccessStatusCode
            ? (true, "Azure 连接成功 ✓")
            : (false, $"鉴权失败 ({resp.StatusCode})");
    }

    private async Task<(bool, string)> TestGoogleAsync(string apiKey)
    {
        var url = $"https://texttospeech.googleapis.com/v1/voices?key={apiKey}";
        var resp = await _httpClient.GetAsync(url);
        return resp.IsSuccessStatusCode
            ? (true, "Google 连接成功 ✓")
            : (false, $"鉴权失败 ({resp.StatusCode})");
    }

    private async Task<(bool, string)> TestTencentAsync(string apiKey)
    {
        var keys = apiKey.Split('|');
        if (keys.Length < 2)
            return (false, "格式错误，应为: SecretId|SecretKey");

        // 兼容旧的 AppID|SecretId|SecretKey 格式，以及新的 SecretId|SecretKey 格式
        var secretId = keys.Length >= 3 ? keys[1] : keys[0];
        var secretKey = keys.Length >= 3 ? keys[2] : keys[1];

        var body = "{\"WebsiteType\":1}";
        var bodyBytes = Encoding.UTF8.GetBytes(body);

        var req = new HttpRequestMessage(HttpMethod.Post, "https://tts.tencentcloudapi.com");
        // Content 由 TencentSigner.SignRequest 内部设置（ByteArrayContent + application/json）
        req.Headers.Add("X-TC-Action", "DescribeVoices");
        req.Headers.Add("X-TC-Version", "2019-08-23");
        req.Headers.Add("X-TC-Region", "ap-shanghai");

        Helpers.TencentSigner.SignRequest(req, secretId, secretKey, "tts", bodyBytes);

        var resp = await _httpClient.SendAsync(req);
        var respText = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
            return (false, $"HTTP 错误 ({resp.StatusCode})");

        var doc = JsonDocument.Parse(respText);
        if (doc.RootElement.TryGetProperty("Response", out var response) &&
            response.TryGetProperty("Error", out var error))
        {
            var code = error.TryGetProperty("Code", out var c) ? c.GetString() : "";
            var msg = error.TryGetProperty("Message", out var m) ? m.GetString() : "";
            return (false, $"鉴权失败: [{code}] {msg}");
        }

        return (true, "腾讯云 连接成功 ✓");
    }

    private async Task<List<VoiceOption>> FetchTencentVoicesAsync(string apiKey)
    {
        var keys = apiKey.Split('|');
        if (keys.Length < 2) return new List<VoiceOption>();

        var secretId = keys.Length >= 3 ? keys[1] : keys[0];
        var secretKey = keys.Length >= 3 ? keys[2] : keys[1];

        var body = "{\"WebsiteType\":1}";
        var bodyBytes = Encoding.UTF8.GetBytes(body);

        var req = new HttpRequestMessage(HttpMethod.Post, "https://tts.tencentcloudapi.com");
        // Content 由 TencentSigner.SignRequest 内部设置（ByteArrayContent + application/json）
        req.Headers.Add("X-TC-Action", "DescribeVoices");
        req.Headers.Add("X-TC-Version", "2019-08-23");
        req.Headers.Add("X-TC-Region", "ap-shanghai");

        Helpers.TencentSigner.SignRequest(req, secretId, secretKey, "tts", bodyBytes);

        var resp = await _httpClient.SendAsync(req);
        var json = await resp.Content.ReadAsStringAsync();

        // 写入调试日志（无论成功或失败都写入）
        try { await File.WriteAllTextAsync(Path.Combine(_settingsService.Settings.OutputDirectory, "tencent_voices_debug.json"), json); } catch { }

        if (!resp.IsSuccessStatusCode) return new List<VoiceOption>();

        // 检查 API 层面的错误（如签名错误）
        try
        {
            var errDoc = JsonDocument.Parse(json);
            if (errDoc.RootElement.TryGetProperty("Response", out var errResp) &&
                errResp.TryGetProperty("Error", out _))
            {
                // API 返回了错误，调试日志已保存
                return new List<VoiceOption>();
            }
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

    private async Task<TtsResult> GenerateTencentAsync(TtsRequest request, string apiKey)
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
        // Content 由 TencentSigner.SignRequest 内部设置（ByteArrayContent + application/json）
        httpRequest.Headers.Add("X-TC-Action", "TextToVoice");
        httpRequest.Headers.Add("X-TC-Version", "2019-08-23");
        httpRequest.Headers.Add("X-TC-Region", "ap-shanghai");

        Helpers.TencentSigner.SignRequest(httpRequest, secretId, secretKey, "tts", bodyBytes);

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
                var filePath = GetOutputFilePath("tencent");
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
        }

        return new TtsResult { Success = false, ErrorMessage = "腾讯云返回结果无法解析: " + respJson };
    }

    /// <summary>
    /// 通用语音合成入口
    /// </summary>
    public async Task<TtsResult> GenerateAsync(TtsRequest request)
    {
        var vendor = VendorRegistry.GetById(request.VendorId);
        if (vendor == null)
            return new TtsResult { Success = false, ErrorMessage = $"找不到厂商: {request.VendorId}" };

        var apiKey = _settingsService.GetApiKey(request.VendorId);
        if (string.IsNullOrWhiteSpace(apiKey))
            return new TtsResult { Success = false, ErrorMessage = "请先在设置中配置该厂商的 API Key。" };

        try
        {
            return request.VendorId switch
            {
                "openai" => await GenerateOpenAiAsync(request, apiKey),
                "aliyun" => await _aliyunProvider.GenerateAsync(request, apiKey),
                "huoshan" => await _huoshanProvider.GenerateAsync(request, apiKey),
                "tencent" => await GenerateTencentAsync(request, apiKey),
                "baidu" => await GenerateBaiduAsync(request, apiKey),
                "azure" => await GenerateAzureAsync(request, apiKey),
                "google" => await GenerateGoogleAsync(request, apiKey),
                _ => new TtsResult { Success = false, ErrorMessage = $"该厂商 ({vendor.Name}) 暂未实现联调接口。" }
            };
        }
        catch (Exception ex)
        {
            return new TtsResult { Success = false, ErrorMessage = $"请求失败: {ex.Message}" };
        }
    }

    // ========== OpenAI ==========
    private async Task<TtsResult> GenerateOpenAiAsync(TtsRequest request, string apiKey)
    {
        var body = new
        {
            model = request.ModelId,
            input = request.Text,
            voice = request.VoiceId,
            speed = request.Speed,
            response_format = "mp3"
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/audio/speech");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        httpRequest.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(httpRequest);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync();
            return new TtsResult { Success = false, ErrorMessage = $"OpenAI API 错误 ({response.StatusCode}): {err}" };
        }

        return await SaveAudioResponse(response, request, "openai");
    }

    // ========== 百度 ==========
    private async Task<TtsResult> GenerateBaiduAsync(TtsRequest request, string apiKey)
    {
        // 百度的 apiKey 格式: "{api_key}|{secret_key}"
        // 先获取 access_token
        var keys = apiKey.Split('|');
        if (keys.Length < 2)
            return new TtsResult { Success = false, ErrorMessage = "百度 API Key 格式应为: api_key|secret_key" };

        var tokenUrl = $"https://aip.baidubce.com/oauth/2.0/token?grant_type=client_credentials&client_id={keys[0]}&client_secret={keys[1]}";
        var tokenResp = await _httpClient.PostAsync(tokenUrl, null);
        var tokenJson = await tokenResp.Content.ReadAsStringAsync();
        var tokenDoc = JsonDocument.Parse(tokenJson);
        if (!tokenDoc.RootElement.TryGetProperty("access_token", out var tokenElem))
            return new TtsResult { Success = false, ErrorMessage = "百度 access_token 获取失败: " + tokenJson };

        var accessToken = tokenElem.GetString();
        var text = Uri.EscapeDataString(request.Text);
        var url = $"https://tsn.baidu.com/text2audio?tex={text}&tok={accessToken}&cuid=voice_ops&ctp=1&lan=zh&spd={(int)Math.Round(request.Speed)}&pit=5&vol={(int)Math.Round(request.Volume)}&per={request.VoiceId}&aue=3";

        var response = await _httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync();
            return new TtsResult { Success = false, ErrorMessage = $"百度 API 错误 ({response.StatusCode}): {err}" };
        }

        return await SaveAudioResponse(response, request, "baidu");
    }

    // ========== Microsoft Azure ==========
    private async Task<TtsResult> GenerateAzureAsync(TtsRequest request, string apiKey)
    {
        // apiKey 格式: "{subscription_key}|{region}"
        var keys = apiKey.Split('|');
        if (keys.Length < 2)
            return new TtsResult { Success = false, ErrorMessage = "Azure Key 格式应为: subscription_key|region (如 xxxxx|eastasia)" };

        var ssml = $@"<speak version='1.0' xml:lang='zh-CN'>
            <voice name='{request.VoiceId}'>
                <prosody rate='{request.Speed:0.00}' volume='{request.Volume:0.00}'>{System.Security.SecurityElement.Escape(request.Text)}</prosody>
            </voice>
        </speak>";

        var url = $"https://{keys[1]}.tts.speech.microsoft.com/cognitiveservices/v1";
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
        httpRequest.Headers.Add("Ocp-Apim-Subscription-Key", keys[0]);
        httpRequest.Content = new StringContent(ssml, Encoding.UTF8, "application/ssml+xml");
        httpRequest.Headers.Add("X-Microsoft-OutputFormat", "audio-16khz-128kbitrate-mono-mp3");

        var response = await _httpClient.SendAsync(httpRequest);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync();
            return new TtsResult { Success = false, ErrorMessage = $"Azure API 错误 ({response.StatusCode}): {err}" };
        }

        return await SaveAudioResponse(response, request, "azure");
    }

    // ========== Google TTS ==========
    private async Task<TtsResult> GenerateGoogleAsync(TtsRequest request, string apiKey)
    {
        var body = new
        {
            input = new { text = request.Text },
            voice = new { languageCode = "cmn-CN", name = request.VoiceId },
            audioConfig = new { audioEncoding = "MP3", speakingRate = request.Speed, volumeGainDb = request.Volume }
        };

        var url = $"https://texttospeech.googleapis.com/v1/text:synthesize?key={apiKey}";
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
        httpRequest.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(httpRequest);
        var respJson = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return new TtsResult { Success = false, ErrorMessage = $"Google API 错误 ({response.StatusCode}): {respJson}" };

        // Google 返回 base64
        var doc = JsonDocument.Parse(respJson);
        if (doc.RootElement.TryGetProperty("audioContent", out var audioBase64))
        {
            var audioBytes = Convert.FromBase64String(audioBase64.GetString()!);
            var filePath = GetOutputFilePath(request.VendorId);
            await File.WriteAllBytesAsync(filePath, audioBytes);

            var vendor = VendorRegistry.GetById(request.VendorId);
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

    // ========== 通用保存 ==========
    private async Task<TtsResult> SaveAudioResponse(HttpResponseMessage response, TtsRequest request, string vendorId)
    {
        var audioBytes = await response.Content.ReadAsByteArrayAsync();
        var filePath = GetOutputFilePath(vendorId);
        await File.WriteAllBytesAsync(filePath, audioBytes);

        var vendor = VendorRegistry.GetById(vendorId);
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

    private string GetOutputFilePath(string vendorId)
    {
        var dir = _settingsService.Settings.OutputDirectory;
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, $"{vendorId}_{DateTime.Now:yyyyMMdd_HHmmss}.mp3");
    }
}
