using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace VoiceServiceMcp.Core;

/// <summary>
/// TTS 语音合成服务 - MCP 独立版本
/// </summary>
public class TtsService
{
    private readonly HttpClient _httpClient = new();
    private readonly string _outputDirectory;

    public TtsService(string outputDirectory)
    {
        _outputDirectory = outputDirectory;
        _httpClient.Timeout = TimeSpan.FromSeconds(60);
        Directory.CreateDirectory(_outputDirectory);
    }

    /// <summary>
    /// 测试 API Key 连通性（仅验证鉴权，不消耗 Token）
    /// </summary>
    public async Task<(bool Success, string Message)> TestConnectivityAsync(string vendorId, string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return (false, "API Key 为空，请先填写。");

        try
        {
            return vendorId switch
            {
                "openai" => await TestOpenAiAsync(apiKey),
                "aliyun" => await TestAliyunAsync(apiKey),
                "huoshan" => await TestHuoshanAsync(apiKey),
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

    public async Task<List<VoiceOption>> FetchVoicesAsync(string vendorId, string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) return new List<VoiceOption>();

        try
        {
            if (vendorId == "huoshan") return await FetchHuoshanVoicesAsync(apiKey);
            return new List<VoiceOption>();
        }
        catch
        {
            return new List<VoiceOption>();
        }
    }

    private async Task<List<VoiceOption>> FetchHuoshanVoicesAsync(string apiKey)
    {
        var keys = apiKey.Split('|');
        if (keys.Length < 5 || string.IsNullOrWhiteSpace(keys[3]) || string.IsNullOrWhiteSpace(keys[4]))
            return new List<VoiceOption>();

        var ak = keys[3];
        var sk = keys[4];

        var bodyBytes = Encoding.UTF8.GetBytes("{}");
        var req = new HttpRequestMessage(HttpMethod.Post, "https://open.volcengineapi.com/?Action=ListBigModelTTSTimbres&Version=2025-05-20");
        req.Content = new ByteArrayContent(bodyBytes);
        req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "UTF-8" };

        VolcengineSigner.SignRequest(req, ak, sk, "cn-beijing", "speech_saas_prod", bodyBytes);

        var resp = await _httpClient.SendAsync(req);
        var json = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode) return new List<VoiceOption>();

        var options = new List<VoiceOption>();
        try
        {
            var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("Result", out var result)) return options;

            JsonElement timbres;
            if (!result.TryGetProperty("Timbres", out timbres))
            {
                foreach (var propName in new[] { "SpeakerList", "TimbreList", "Data" })
                    if (result.TryGetProperty(propName, out timbres)) break;
            }

            if (timbres.ValueKind != JsonValueKind.Array) return options;

            foreach (var timbre in timbres.EnumerateArray())
            {
                var speakerId = TryGetStr(timbre, "SpeakerID", "VoiceType", "speaker_id", "voice_type") ?? "";
                if (string.IsNullOrEmpty(speakerId)) continue;

                if (timbre.TryGetProperty("TimbreInfos", out var infos) && infos.ValueKind == JsonValueKind.Array)
                {
                    foreach (var info in infos.EnumerateArray())
                    {
                        var name = TryGetStr(info, "SpeakerName", "Name", "DisplayName") ?? speakerId;
                        var gender = TryGetStr(info, "Gender", "gender") ?? "";
                        var age = TryGetStr(info, "Age", "age") ?? "";
                        string? demoUrl = null;

                        var categories = new List<string>();
                        if (info.TryGetProperty("Categories", out var cats) && cats.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var cat in cats.EnumerateArray())
                            {
                                var c = TryGetStr(cat, "Category");
                                if (!string.IsNullOrEmpty(c)) categories.Add(c);
                                if (cat.TryGetProperty("NextCategory", out var next))
                                {
                                    var nc = TryGetStr(next, "Category");
                                    if (!string.IsNullOrEmpty(nc)) categories.Add(nc);
                                }
                            }
                        }

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
                            IsBigTTS = true
                        });
                    }
                }
                else
                {
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
        var req = new HttpRequestMessage(HttpMethod.Post,
            "https://dashscope.aliyuncs.com/api/v1/services/aigc/text2audio/generation");
        req.Headers.Add("Authorization", $"Bearer {apiKey}");
        req.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        var resp = await _httpClient.SendAsync(req);
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

    /// <summary>
    /// 通用语音合成入口
    /// </summary>
    public async Task<TtsResult> GenerateAsync(TtsRequest request, string apiKey)
    {
        var vendor = VendorRegistry.GetById(request.VendorId);
        if (vendor == null)
            return new TtsResult { Success = false, ErrorMessage = $"找不到厂商: {request.VendorId}" };

        if (string.IsNullOrWhiteSpace(apiKey))
            return new TtsResult { Success = false, ErrorMessage = "请先配置该厂商的 API Key。" };

        try
        {
            return request.VendorId switch
            {
                "openai" => await GenerateOpenAiAsync(request, apiKey),
                "aliyun" => await GenerateAliyunAsync(request, apiKey),
                "huoshan" => await GenerateHuoshanAsync(request, apiKey),
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
        var body = new
        {
            model = request.ModelId,
            input = new { text = request.Text },
            parameters = new { voice = request.VoiceId }
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post,
            "https://dashscope.aliyuncs.com/api/v1/services/aigc/text2audio/generation");
        httpRequest.Headers.Add("Authorization", $"Bearer {apiKey}");
        httpRequest.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(httpRequest);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync();
            return new TtsResult { Success = false, ErrorMessage = $"阿里云 API 错误 ({response.StatusCode}): {err}" };
        }

        return await SaveAudioResponse(response, request, "aliyun");
    }

    // ========== 火山引擎 ==========
    private async Task<TtsResult> GenerateHuoshanAsync(TtsRequest request, string apiKey)
    {
        var keys = apiKey.Split('|');
        if (keys.Length < 2)
            return new TtsResult { Success = false, ErrorMessage = "火山引擎 Key 格式应为: AppID|AccessToken 或 AppID|AccessToken|Cluster|AK|SK" };

        var appId = keys[0];
        var token = keys[1];
        
        var isBigTTS = request.VoiceId.EndsWith("_bigtts") || 
                       request.VoiceId.EndsWith("_tob") ||
                       request.VoiceId.Contains("_moon_") ||
                       request.VoiceId.Contains("_mars_") ||
                       request.VoiceId.Contains("_wvae_") ||
                       request.VoiceId.StartsWith("ICL_") ||
                       request.VoiceId.StartsWith("multi_");
        
        var cluster = isBigTTS 
            ? "" 
            : (keys.Length >= 3 && !string.IsNullOrWhiteSpace(keys[2]) ? keys[2] : "volcano_tts");

        var body = new
        {
            app = new { appid = appId, token = "access_token", cluster = cluster },
            user = new { uid = "388808087185088" },
            audio = new { voice_type = request.VoiceId, encoding = "mp3", speed_ratio = request.Speed, volume_ratio = 1.0, pitch_ratio = 1.0 },
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
        var url = $"https://tsn.baidu.com/text2audio?tex={text}&tok={accessToken}&cuid=voice_ops&ctp=1&lan=zh&spd=5&pit=5&vol=5&per={request.VoiceId}&aue=3";

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
        var keys = apiKey.Split('|');
        if (keys.Length < 2)
            return new TtsResult { Success = false, ErrorMessage = "Azure Key 格式应为: subscription_key|region (如 xxxxx|eastasia)" };

        var ssml = $@"<speak version='1.0' xml:lang='zh-CN'>
            <voice name='{request.VoiceId}'>{System.Security.SecurityElement.Escape(request.Text)}</voice>
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
            audioConfig = new { audioEncoding = "MP3", speakingRate = request.Speed }
        };

        var url = $"https://texttospeech.googleapis.com/v1/text:synthesize?key={apiKey}";
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
        httpRequest.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(httpRequest);
        var respJson = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return new TtsResult { Success = false, ErrorMessage = $"Google API 错误 ({response.StatusCode}): {respJson}" };

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
        Directory.CreateDirectory(_outputDirectory);
        return Path.Combine(_outputDirectory, $"{vendorId}_{DateTime.Now:yyyyMMdd_HHmmss}.mp3");
    }
}
