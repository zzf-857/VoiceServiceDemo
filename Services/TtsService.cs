using System.IO;
using System.Net.Http;
using System.Text.Json;
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
    private readonly TencentTtsProvider _tencentProvider;
    private readonly GoogleTtsProvider _googleProvider;
    private readonly OpenAiTtsProvider _openAiProvider;
    private readonly AzureTtsProvider _azureProvider;

    public TtsService(SettingsService settingsService)
    {
        _settingsService = settingsService;
        _httpClient.Timeout = TimeSpan.FromSeconds(60);
        _aliyunProvider = new AliyunTtsProvider(_httpClient, _settingsService);
        _huoshanProvider = new HuoshanTtsProvider(_httpClient, _settingsService);
        _tencentProvider = new TencentTtsProvider(_httpClient, _settingsService);
        _googleProvider = new GoogleTtsProvider(_httpClient, _settingsService);
        _openAiProvider = new OpenAiTtsProvider(_httpClient, _settingsService);
        _azureProvider = new AzureTtsProvider(_httpClient, _settingsService);
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
                "openai" => await _openAiProvider.TestConnectivityAsync(apiKey),
                "aliyun" => await _aliyunProvider.TestConnectivityAsync(apiKey),
                "huoshan" => await _huoshanProvider.TestConnectivityAsync(apiKey),
                "tencent" => await _tencentProvider.TestConnectivityAsync(apiKey),
                "baidu" => await TestBaiduAsync(apiKey),
                "azure" => await _azureProvider.TestConnectivityAsync(apiKey),
                "google" => await _googleProvider.TestConnectivityAsync(apiKey),
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
            if (vendorId == "tencent") return await _tencentProvider.FetchVoicesAsync(apiKey);
            if (vendorId == "google") return await _googleProvider.FetchVoicesAsync(apiKey);
            if (vendorId == "azure") return await _azureProvider.FetchVoicesAsync(apiKey);
            return new List<VoiceOption>();
        }
        catch
        {
            return new List<VoiceOption>();
        }
    }

    // ===== 连通性测试方法 =====

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
                "openai" => await _openAiProvider.GenerateAsync(request, apiKey),
                "aliyun" => await _aliyunProvider.GenerateAsync(request, apiKey),
                "huoshan" => await _huoshanProvider.GenerateAsync(request, apiKey),
                "tencent" => await _tencentProvider.GenerateAsync(request, apiKey),
                "baidu" => await GenerateBaiduAsync(request, apiKey),
                "azure" => await _azureProvider.GenerateAsync(request, apiKey),
                "google" => await _googleProvider.GenerateAsync(request, apiKey),
                _ => new TtsResult { Success = false, ErrorMessage = $"该厂商 ({vendor.Name}) 暂未实现联调接口。" }
            };
        }
        catch (Exception ex)
        {
            return new TtsResult { Success = false, ErrorMessage = $"请求失败: {ex.Message}" };
        }
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
