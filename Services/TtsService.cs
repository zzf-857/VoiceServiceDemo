using System.IO;
using System.Net.Http;
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
    private readonly BaiduTtsProvider _baiduProvider;

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
        _baiduProvider = new BaiduTtsProvider(_httpClient, _settingsService);
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
                "baidu" => await _baiduProvider.TestConnectivityAsync(apiKey),
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
                "baidu" => await _baiduProvider.GenerateAsync(request, apiKey),
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

}
