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
    private readonly TtsProviderRegistry _providers;

    public TtsService(SettingsService settingsService)
    {
        _settingsService = settingsService;
        _httpClient.Timeout = TimeSpan.FromSeconds(60);
        _providers = new TtsProviderRegistry(new ITtsProvider[]
        {
            new AliyunTtsProvider(_httpClient, _settingsService),
            new HuoshanTtsProvider(_httpClient, _settingsService),
            new TencentTtsProvider(_httpClient, _settingsService),
            new GoogleTtsProvider(_httpClient, _settingsService),
            new OpenAiTtsProvider(_httpClient, _settingsService),
            new AzureTtsProvider(_httpClient, _settingsService),
            new BaiduTtsProvider(_httpClient, _settingsService),
            new XiaomiMimoTtsProvider(_httpClient, _settingsService),
            new MiniMaxTtsProvider(_httpClient, _settingsService),
            new ElevenLabsTtsProvider(_httpClient, _settingsService),
            new FishAudioTtsProvider(_httpClient, _settingsService),
            new DeepgramTtsProvider(_httpClient, _settingsService),
            new CartesiaTtsProvider(_httpClient, _settingsService),
            new PlayHtTtsProvider(_httpClient, _settingsService),
            new AmazonPollyTtsProvider(_httpClient, _settingsService)
        });
    }

    public IReadOnlyCollection<string> RegisteredProviderIds => _providers.AllIds;

    /// <summary>
    /// 测试 API Key 连通性（仅验证鉴权，不消耗 Token）
    /// </summary>
    public async Task<(bool Success, string Message)> TestConnectivityAsync(
        string vendorId,
        CancellationToken cancellationToken = default)
    {
        var apiKey = _settingsService.GetApiKey(vendorId);
        if (string.IsNullOrWhiteSpace(apiKey))
            return (false, "API Key 为空，请先填写。");

        try
        {
            if (!_providers.TryGet(vendorId, out var provider))
                return (false, "找不到该厂商的 TTS Provider。");

            return await provider.TestConnectivityAsync(apiKey, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return (false, $"连接失败: {ex.Message}");
        }
    }

    public async Task<VoiceCatalogResult> FetchVoiceCatalogAsync(
        string vendorId,
        CancellationToken cancellationToken = default)
    {
        if (!_providers.TryGet(vendorId, out var provider))
            return new VoiceCatalogResult(false, false, new(), "vendor_not_found", $"找不到厂商: {vendorId}");

        if (provider is not IVoiceCatalogProvider catalogProvider)
            return new VoiceCatalogResult(false, false, new(), "voice_fetch_not_supported", "该厂商不支持在线音色刷新。");

        var apiKey = _settingsService.GetApiKey(vendorId);
        if (vendorId != "aliyun" && string.IsNullOrWhiteSpace(apiKey))
            return new VoiceCatalogResult(false, true, new(), "credential_not_configured", "请先配置该厂商的 API Key。");

        try
        {
            var voices = await catalogProvider.FetchVoicesAsync(apiKey, cancellationToken);
            return new VoiceCatalogResult(true, true, voices);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new VoiceCatalogResult(false, true, new(), "voice_fetch_failed", ex.Message);
        }
    }

    public async Task<List<VoiceOption>> FetchVoicesAsync(
        string vendorId,
        CancellationToken cancellationToken = default) =>
        (await FetchVoiceCatalogAsync(vendorId, cancellationToken)).Voices;

    /// <summary>
    /// 通用语音合成入口
    /// </summary>
    public async Task<TtsResult> GenerateAsync(
        TtsRequest request,
        CancellationToken cancellationToken = default)
    {
        var vendor = VendorRegistry.GetById(request.VendorId);
        if (vendor == null)
            return new TtsResult { Success = false, ErrorMessage = $"找不到厂商: {request.VendorId}" };

        var apiKey = _settingsService.GetApiKey(request.VendorId);
        if (string.IsNullOrWhiteSpace(apiKey))
            return new TtsResult { Success = false, ErrorMessage = "请先在设置中配置该厂商的 API Key。" };

        try
        {
            if (!_providers.TryGet(request.VendorId, out var provider))
                return new TtsResult { Success = false, ErrorMessage = $"该厂商 ({vendor.Name}) 暂未实现联调接口。" };

            return await provider.GenerateAsync(request, apiKey, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new TtsResult { Success = false, ErrorMessage = $"请求失败: {ex.Message}" };
        }
    }

}
