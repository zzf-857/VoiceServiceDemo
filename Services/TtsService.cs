using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using VoiceServiceDemo.Models;

namespace VoiceServiceDemo.Services;

/// <summary>
/// TTS 语音合成服务 - 负责调用各大厂商的 API 生成音频
/// </summary>
public class TtsService
{
    private readonly HttpClient _httpClient = new();
    private readonly SettingsService _settingsService;

    public TtsService(SettingsService settingsService)
    {
        _settingsService = settingsService;
        _httpClient.Timeout = TimeSpan.FromSeconds(60);
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
                "aliyun" => await TestAliyunAsync(apiKey),
                "huoshan" => await TestHuoshanAsync(apiKey),
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
            if (vendorId == "aliyun") return await FetchAliyunVoicesAsync();
            if (vendorId == "huoshan") return await FetchHuoshanVoicesAsync(apiKey);
            if (vendorId == "tencent") return await FetchTencentVoicesAsync(apiKey);
            return new List<VoiceOption>();
        }
        catch
        {
            return new List<VoiceOption>();
        }
    }

    private async Task<List<VoiceOption>> FetchAliyunVoicesAsync()
    {
        var options = new List<VoiceOption>();
        try
        {
            // 阿里云没有公开的音色列表 API（控制台接口需要浏览器 session 认证），
            // 因此从本地预置的 JSON 文件读取音色数据。
            var jsonPath = Path.Combine(AppContext.BaseDirectory, "aliyun_voices_raw.json");
            if (!File.Exists(jsonPath))
                jsonPath = Path.Combine(Directory.GetCurrentDirectory(), "aliyun_voices_raw.json");
            if (!File.Exists(jsonPath)) return options;

            var json = await File.ReadAllTextAsync(jsonPath);
            using var doc = JsonDocument.Parse(json);

            // 解析结构: { "data": { "DataV2": { "data": { "data": { "voiceConfigList": [...] } } } } }
            // 兼容旧结构: { "data": { "DataV2": { "data": { "data": [...] } } } }
            if (doc.RootElement.TryGetProperty("data", out var dataWrapper) &&
                dataWrapper.TryGetProperty("DataV2", out var dataV2) &&
                dataV2.TryGetProperty("data", out var innerData) &&
                innerData.TryGetProperty("data", out var dataContent))
            {
                JsonElement voiceArray;

                // 新结构: data.data 是对象，包含 voiceConfigList
                if (dataContent.ValueKind == JsonValueKind.Object &&
                    dataContent.TryGetProperty("voiceConfigList", out var vcl))
                {
                    voiceArray = vcl;
                }
                // 旧结构: data.data 直接是数组
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
                    // 过滤掉非阿里云原生模型（如 MiniMax）
                    var modelId = TryGetStr(item, "defaultModelId") ?? "";
                    if (modelId.StartsWith("MiniMax", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (item.TryGetProperty("ttsVoiceConfig", out var cfg))
                    {
                        ParseAliyunVoiceConfig(cfg, options);
                    }
                }
            }

            // 写入调试日志
            try { await File.WriteAllTextAsync(Path.Combine(_settingsService.Settings.OutputDirectory, "aliyun_voices_debug.txt"), $"Loaded {options.Count} Aliyun voices from local JSON"); } catch { }
        }
        catch { }
        return options;
    }

    /// <summary>
    /// 解析单个阿里云音色配置
    /// </summary>
    private void ParseAliyunVoiceConfig(JsonElement cfg, List<VoiceOption> options)
    {
        var voiceId = TryGetStr(cfg, "voice") ?? "";
        if (string.IsNullOrEmpty(voiceId) || options.Any(o => o.Id == voiceId)) return;

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

    private async Task<List<VoiceOption>> FetchHuoshanVoicesAsync(string apiKey)
    {
        var keys = apiKey.Split('|');
        if (keys.Length < 5 || string.IsNullOrWhiteSpace(keys[3]) || string.IsNullOrWhiteSpace(keys[4]))
            return new List<VoiceOption>(); // 需要 AK 和 SK 才能调用 OpenAPI

        var ak = keys[3];
        var sk = keys[4];

        // 官方文档：POST /?Action=ListBigModelTTSTimbres&Version=2025-05-20，Body 为 {}
        var bodyBytes = Encoding.UTF8.GetBytes("{}");

        var req = new HttpRequestMessage(HttpMethod.Post, "https://open.volcengineapi.com/?Action=ListBigModelTTSTimbres&Version=2025-05-20");
        req.Content = new ByteArrayContent(bodyBytes);
        // content-type 必须与签名器里写死的值完全一致（大写 UTF-8）
        req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "UTF-8" };

        VoiceServiceDemo.Helpers.VolcengineSigner.SignRequest(req, ak, sk, "cn-beijing", "speech_saas_prod", bodyBytes);

        var resp = await _httpClient.SendAsync(req);
        var json = await resp.Content.ReadAsStringAsync();

        // 写入调试日志（无论成功或失败都写入）
        try { await File.WriteAllTextAsync(Path.Combine(_settingsService.Settings.OutputDirectory, "huoshan_voices_debug.json"), json); } catch { }

        if (!resp.IsSuccessStatusCode) return new List<VoiceOption>();

        var options = new List<VoiceOption>();
        try
        {
            var doc = JsonDocument.Parse(json);

            // 官方响应结构: { "Result": { "Timbres": [ { "SpeakerID": "...", "TimbreInfos": [{ "SpeakerName": "...", "Gender": "...", "DemoURL": "..." }] } ] } }
            if (!doc.RootElement.TryGetProperty("Result", out var result)) return options;

            JsonElement timbres;
            if (!result.TryGetProperty("Timbres", out timbres))
            {
                // 兜底：尝试其他可能的字段名
                foreach (var propName in new[] { "SpeakerList", "TimbreList", "Data" })
                    if (result.TryGetProperty(propName, out timbres)) break;
            }

            if (timbres.ValueKind != JsonValueKind.Array) return options;

            foreach (var timbre in timbres.EnumerateArray())
            {
                var speakerId = TryGetStr(timbre, "SpeakerID", "VoiceType", "speaker_id", "voice_type") ?? "";
                if (string.IsNullOrEmpty(speakerId)) continue;

                // 从 TimbreInfos 嵌套数组中取详细信息
                if (timbre.TryGetProperty("TimbreInfos", out var infos) && infos.ValueKind == JsonValueKind.Array)
                {
                    foreach (var info in infos.EnumerateArray())
                    {
                        var name = TryGetStr(info, "SpeakerName", "Name", "DisplayName") ?? speakerId;
                        var gender = TryGetStr(info, "Gender", "gender") ?? "";
                        var age = TryGetStr(info, "Age", "age") ?? "";
                        string? demoUrl = null;

                        // 解析 Categories
                        var categories = new List<string>();
                        if (info.TryGetProperty("Categories", out var cats) && cats.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var cat in cats.EnumerateArray())
                            {
                                var c = TryGetStr(cat, "Category");
                                if (!string.IsNullOrEmpty(c)) categories.Add(c);
                                // 子分类（如 多语种→美式英语）
                                if (cat.TryGetProperty("NextCategory", out var next))
                                {
                                    var nc = TryGetStr(next, "Category");
                                    if (!string.IsNullOrEmpty(nc)) categories.Add(nc);
                                }
                            }
                        }

                        // 解析 Emotions
                        var emotions = new List<EmotionInfo>();
                        if (info.TryGetProperty("Emotions", out var emos) && emos.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var emo in emos.EnumerateArray())
                            {
                                var ei = new EmotionInfo
                                {
                                    Emotion = TryGetStr(emo, "Emotion") ?? "",
                                    EmotionType = TryGetStr(emo, "EmotionType") ?? "",
                                    DemoText = TryGetStr(emo, "DemoText"),
                                    DemoUrl = TryGetStr(emo, "DemoURL", "DemoUrl")
                                };
                                emotions.Add(ei);
                                if (demoUrl == null) demoUrl = ei.DemoUrl;
                            }
                        }

                        // 判断语言
                        var lang = categories.Contains("多语种") && categories.Count > 1
                            ? categories.LastOrDefault(c => c != "多语种") ?? "中文"
                            : "中文";

                        options.Add(new VoiceOption
                        {
                            Id = speakerId,
                            Name = name,
                            Language = lang,
                            Gender = gender,
                            Age = age,
                            SampleUrl = demoUrl,
                            Categories = categories,
                            Emotions = emotions,
                            IsBigTTS = true  // 所有从 ListBigModelTTSTimbres API 获取的音色都是 BigTTS
                        });
                    }
                }
                else
                {
                    // 如果字段直接在 timbre 对象上（兜底）
                    var name = TryGetStr(timbre, "SpeakerName", "Name", "DisplayName") ?? speakerId;
                    var gender = TryGetStr(timbre, "Gender", "gender") ?? "";
                    var demoUrl = TryGetStr(timbre, "DemoURL", "DemoUrl", "AudioUrl");
                    options.Add(new VoiceOption { Id = speakerId, Name = name, Language = "中文", Gender = gender, SampleUrl = demoUrl, IsBigTTS = true });
                }
            }
        }
        catch { }
        return options;
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

    private async Task<(bool, string)> TestAliyunAsync(string apiKey)
    {
        // 发送一个极短的请求来验证 Key 有效性
        var req = new HttpRequestMessage(HttpMethod.Post,
            "https://dashscope.aliyuncs.com/api/v1/services/aigc/text2audio/generation");
        req.Headers.Add("Authorization", $"Bearer {apiKey}");
        req.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        var resp = await _httpClient.SendAsync(req);
        var body = await resp.Content.ReadAsStringAsync();
        // 401/403 = key无效，其余（如 400 参数不对）= key有效
        if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized || resp.StatusCode == System.Net.HttpStatusCode.Forbidden)
            return (false, $"鉴权失败 ({resp.StatusCode})");
        return (true, "阿里云 连接成功 ✓");
    }

    private async Task<(bool, string)> TestHuoshanAsync(string apiKey)
    {
        var keys = apiKey.Split('|');
        if (keys.Length < 2)
            return (false, "格式错误，应为: AppID|AccessToken 或 AppID|AccessToken|Cluster");

        var appId = keys[0];
        var token = keys[1];
        var cluster = keys.Length >= 3 && !string.IsNullOrWhiteSpace(keys[2]) ? keys[2] : "volcano_tts";

        var body = new { app = new { appid = appId, token = "access_token", cluster = cluster },
                         user = new { uid = "388808087185088" },
                         audio = new { voice_type = "zh_female_cancan", encoding = "mp3", speed_ratio = 1.0, volume_ratio = 1.0, pitch_ratio = 1.0 },
                         request = new { reqid = Guid.NewGuid().ToString(), text = "test", text_type = "plain", operation = "query", with_frontend = 1, frontend_type = "unitTson" } };
        var req = new HttpRequestMessage(HttpMethod.Post, "https://openspeech.bytedance.com/api/v1/tts");
        req.Headers.TryAddWithoutValidation("Authorization", $"Bearer;{token}");
        req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var resp = await _httpClient.SendAsync(req);
        if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized || resp.StatusCode == System.Net.HttpStatusCode.Forbidden)
            return (false, $"鉴权失败 ({resp.StatusCode})");
        return (true, "火山引擎 连接成功 ✓");
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
                "aliyun" => await GenerateAliyunAsync(request, apiKey),
                "huoshan" => await GenerateHuoshanAsync(request, apiKey),
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

    // ========== 阿里云 CosyVoice ==========
    private async Task<TtsResult> GenerateAliyunAsync(TtsRequest request, string apiKey)
    {
        HttpRequestMessage httpRequest;

        // qwen3-tts 系列使用 multimodal-generation 端点
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
            // 旧版 qwen-tts / cosyvoice 使用 DashScope 原生端点
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

        // qwen3-tts 非流式返回 JSON（含音频 URL），需要额外下载
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

                // 下载音频文件
                var audioBytes = await _httpClient.GetByteArrayAsync(audioUrl);
                var filePath = GetOutputFilePath("aliyun");
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
            catch (Exception ex)
            {
                return new TtsResult { Success = false, ErrorMessage = $"阿里云响应解析失败: {ex.Message}\n原始响应: {jsonStr}" };
            }
        }

        return await SaveAudioResponse(response, request, "aliyun");
    }

    // ========== 火山引擎 ==========
    private async Task<TtsResult> GenerateHuoshanAsync(TtsRequest request, string apiKey)
    {
        var keys = apiKey.Split('|');
        if (keys.Length < 2)
            return new TtsResult { Success = false, ErrorMessage = "火山引擎 Key 格式应为: AppID|AccessToken 或 AppID|AccessToken|Cluster" };

        var appId = keys[0];
        var token = keys[1];
        
        // 判断是否为 BigTTS 音色（通过音色 ID 后缀或前缀判断）
        var isBigTTS = request.VoiceId.EndsWith("_bigtts") || 
                       request.VoiceId.EndsWith("_tob") ||
                       request.VoiceId.Contains("_moon_") ||
                       request.VoiceId.Contains("_mars_") ||
                       request.VoiceId.Contains("_wvae_") ||
                       request.VoiceId.StartsWith("ICL_") ||
                       request.VoiceId.StartsWith("multi_");
        
        // BigTTS 不需要 cluster 参数，标准 TTS 使用 volcano_tts
        var cluster = isBigTTS 
            ? "" 
            : (keys.Length >= 3 && !string.IsNullOrWhiteSpace(keys[2]) ? keys[2] : "volcano_tts");

        var body = new
        {
            app = new { appid = appId, token = "access_token", cluster = cluster },
            user = new { uid = "388808087185088" },
            audio = new { voice_type = request.VoiceId, encoding = "mp3", speed_ratio = request.Speed, volume_ratio = request.Volume, pitch_ratio = 1.0 },
            request = new { reqid = Guid.NewGuid().ToString(), text = request.Text, text_type = "plain", operation = "query", with_frontend = 1, frontend_type = "unitTson" }
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://openspeech.bytedance.com/api/v1/tts");
        httpRequest.Headers.TryAddWithoutValidation("Authorization", $"Bearer;{token}");
        httpRequest.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(httpRequest);
        var respJson = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return new TtsResult { Success = false, ErrorMessage = $"火山引擎 API 错误 ({response.StatusCode}): {respJson}" };
        }

        // 解析火山返回的 JSON，获取 base64 编码的二进制音频
        var doc = JsonDocument.Parse(respJson);
        if (doc.RootElement.TryGetProperty("data", out var audioBase64) && audioBase64.ValueKind == JsonValueKind.String)
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

        return new TtsResult { Success = false, ErrorMessage = "火山引擎返回结果无法解析 data: " + respJson };
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
