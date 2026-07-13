using VoiceServiceDemo.Models;
using VoiceServiceDemo.Services.Providers;
using VoiceServiceLocalApi;

namespace VoiceServiceDemo.Services;

public sealed class DesktopTtsGateway : ILocalTtsGateway
{
    private readonly TtsService _ttsService;
    private readonly SettingsService _settingsService;

    public DesktopTtsGateway(TtsService ttsService, SettingsService settingsService)
    {
        _ttsService = ttsService;
        _settingsService = settingsService;
    }

    public string OutputDirectory => _settingsService.Settings.OutputDirectory;

    public Task<IReadOnlyList<LocalVendorInfo>> GetVendorsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<LocalVendorInfo> vendors = VendorRegistry.All
            .Select(vendor => ToLocalVendor(vendor, !string.IsNullOrWhiteSpace(_settingsService.GetApiKey(vendor.Id))))
            .ToList();
        return Task.FromResult(vendors);
    }

    public async Task<LocalVoiceCatalog> GetVoicesAsync(
        string vendor,
        bool refresh,
        CancellationToken cancellationToken)
    {
        var vendorConfig = VendorRegistry.GetById(vendor);
        if (vendorConfig is null)
            return new LocalVoiceCatalog(false, false, Array.Empty<LocalVoiceInfo>(), "vendor_not_found", $"找不到厂商: {vendor}");

        if (!refresh)
        {
            return new LocalVoiceCatalog(
                true,
                vendorConfig.SupportsVoiceFetch,
                vendorConfig.DefaultVoices.Select(ToLocalVoice).ToList());
        }

        var catalog = await _ttsService.FetchVoiceCatalogAsync(vendor, cancellationToken);
        return new LocalVoiceCatalog(
            catalog.Success,
            catalog.Supported,
            catalog.Voices.Select(ToLocalVoice).ToList(),
            catalog.ErrorCode,
            catalog.ErrorMessage);
    }

    public async Task<LocalTtsGatewayResult> GenerateAsync(
        LocalTtsRequest request,
        CancellationToken cancellationToken)
    {
        var vendor = VendorRegistry.GetById(request.Vendor);
        if (vendor is null)
            return Failure(request, "vendor_not_found", $"找不到厂商: {request.Vendor}");

        var localVendor = ToLocalVendor(vendor, configured: false);
        var validation = LocalTtsRequestValidator.Validate(
            request,
            localVendor,
            _settingsService.Settings.LocalApi.MaxTextLength);
        if (!validation.Success)
            return Failure(request, "validation_error", string.Join("; ", validation.Errors.Select(error => $"{error.Field}: {error.Message}")));

        if (string.IsNullOrWhiteSpace(_settingsService.GetApiKey(vendor.Id)))
            return Failure(validation.NormalizedRequest!, "credential_not_configured", $"未配置 {vendor.Name} 的 API 凭证。");

        var desktopRequest = MapNormalizedRequest(validation.NormalizedRequest!);
        var result = await _ttsService.GenerateAsync(desktopRequest, cancellationToken);
        if (!result.Success || string.IsNullOrWhiteSpace(result.FilePath))
            return Failure(validation.NormalizedRequest!, "provider_error", result.ErrorMessage ?? "厂商未返回音频文件。");

        return new LocalTtsGatewayResult(
            true,
            result.FilePath,
            vendor.Id,
            desktopRequest.ModelId,
            desktopRequest.VoiceId,
            new DateTimeOffset(result.GeneratedAt));
    }

    public static TtsRequest CreateDesktopRequest(LocalTtsRequest request, VendorConfig vendor)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(vendor);
        var validation = LocalTtsRequestValidator.Validate(request, ToLocalVendor(vendor, configured: false), 20_000);
        if (!validation.Success)
            throw new ArgumentException(string.Join("; ", validation.Errors.Select(error => $"{error.Field}: {error.Message}")), nameof(request));
        return MapNormalizedRequest(validation.NormalizedRequest!);
    }

    private static TtsRequest MapNormalizedRequest(LocalTtsRequest request) => new()
    {
        VendorId = request.Vendor,
        ModelId = request.ModelId ?? "",
        VoiceId = request.VoiceId,
        Text = request.Text,
        Speed = request.Speed ?? 1.0,
        Volume = request.Volume ?? 1.0,
        InputFormat = string.Equals(request.InputFormat, "ssml", StringComparison.OrdinalIgnoreCase)
            ? TtsInputFormat.Ssml
            : TtsInputFormat.PlainText,
        Style = request.Style ?? "",
        StyleDegree = request.StyleDegree ?? 1.0,
        Emotion = request.Emotion ?? "",
        EmotionIntensity = request.EmotionIntensity ?? 100,
        OutputFormat = request.OutputFormat ?? "mp3",
        ResourceId = request.ResourceId ?? "",
        Instructions = request.Instructions ?? "",
        SsmlText = string.Equals(request.InputFormat, "ssml", StringComparison.OrdinalIgnoreCase)
            ? request.SsmlText ?? request.Text
            : request.SsmlText ?? ""
    };

    private static LocalTtsGatewayResult Failure(LocalTtsRequest request, string code, string message) => new(
        false,
        null,
        request.Vendor,
        request.ModelId ?? "",
        request.VoiceId,
        DateTimeOffset.UtcNow,
        code,
        message);

    private static LocalVendorInfo ToLocalVendor(VendorConfig vendor, bool configured) => new(
        vendor.Id,
        vendor.Name,
        configured,
        vendor.DefaultModels.Select(model => new LocalModelInfo(model.Id, model.Name)).ToList(),
        vendor.DefaultModels.FirstOrDefault()?.Id ?? "",
        new LocalParameterDefinition(
            vendor.SpeedDef.IsSupported,
            vendor.SpeedDef.Min,
            vendor.SpeedDef.Max,
            vendor.SpeedDef.Default,
            vendor.SpeedDef.Step),
        new LocalParameterDefinition(
            vendor.VolumeDef.IsSupported,
            vendor.VolumeDef.Min,
            vendor.VolumeDef.Max,
            vendor.VolumeDef.Default,
            vendor.VolumeDef.Step),
        new LocalVendorCapabilities(
            vendor.Capabilities.SupportedInputFormats.Select(format => format == TtsInputFormat.Ssml ? "ssml" : "text").ToList(),
            vendor.Capabilities.SupportedOutputFormats.ToList(),
            vendor.Capabilities.SupportsSsml,
            vendor.Capabilities.SupportsStyle,
            vendor.Capabilities.SupportsStyleDegree,
            vendor.Capabilities.SupportsEmotion,
            vendor.Capabilities.SupportsInstructions,
            SupportsResourceId: vendor.Id == "huoshan"),
        vendor.Description);

    private static LocalVoiceInfo ToLocalVoice(VoiceOption voice) => new(
        voice.Id,
        voice.Name,
        voice.Gender,
        voice.Language,
        voice.SampleUrl,
        voice.Age,
        voice.Categories.ToList());
}
