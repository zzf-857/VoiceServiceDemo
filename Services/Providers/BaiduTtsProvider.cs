using System.IO;
using System.Net.Http;
using System.Text.Json;
using VoiceServiceDemo.Models;

namespace VoiceServiceDemo.Services.Providers;

public sealed class BaiduTtsProvider
{
    private readonly HttpClient _httpClient;
    private readonly SettingsService _settingsService;

    public BaiduTtsProvider(HttpClient httpClient, SettingsService settingsService)
    {
        _httpClient = httpClient;
        _settingsService = settingsService;
    }

    public async Task<(bool Success, string Message)> TestConnectivityAsync(string apiKey)
    {
        var keys = ParseCredentials(apiKey);
        if (keys == null)
            return (false, "格式错误，应为: api_key|secret_key");

        var tokenJson = await RequestAccessTokenAsync(keys.Value.ApiKey, keys.Value.SecretKey);
        using var doc = JsonDocument.Parse(tokenJson);
        if (doc.RootElement.TryGetProperty("access_token", out _))
            return (true, "百度 连接成功 ✓");

        return (false, "access_token 获取失败，请检查 Key");
    }

    public async Task<TtsResult> GenerateAsync(TtsRequest request, string apiKey)
    {
        var keys = ParseCredentials(apiKey);
        if (keys == null)
            return new TtsResult { Success = false, ErrorMessage = "百度 API Key 格式应为: api_key|secret_key" };

        var tokenJson = await RequestAccessTokenAsync(keys.Value.ApiKey, keys.Value.SecretKey);
        using var tokenDoc = JsonDocument.Parse(tokenJson);
        if (!tokenDoc.RootElement.TryGetProperty("access_token", out var tokenElem))
            return new TtsResult { Success = false, ErrorMessage = "百度 access_token 获取失败: " + tokenJson };

        var response = await _httpClient.GetAsync(BuildSynthesisUrl(request, tokenElem.GetString() ?? ""));
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync();
            return new TtsResult { Success = false, ErrorMessage = $"百度 API 错误 ({response.StatusCode}): {err}" };
        }

        var audioBytes = await response.Content.ReadAsByteArrayAsync();
        var filePath = GetOutputFilePath(request.OutputFormat);
        await File.WriteAllBytesAsync(filePath, audioBytes);

        var vendor = VendorRegistry.GetById("baidu");
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

    public static string BuildSynthesisUrl(TtsRequest request, string accessToken)
    {
        var text = Uri.EscapeDataString(request.Text);
        return "https://tsn.baidu.com/text2audio" +
               $"?tex={text}" +
               $"&tok={Uri.EscapeDataString(accessToken)}" +
               "&cuid=voice_ops" +
               "&ctp=1" +
               "&lan=zh" +
               $"&spd={(int)Math.Round(request.Speed)}" +
               "&pit=5" +
               $"&vol={(int)Math.Round(request.Volume)}" +
               $"&per={Uri.EscapeDataString(request.VoiceId)}" +
               $"&aue={GetAue(request.OutputFormat)}";
    }

    private async Task<string> RequestAccessTokenAsync(string apiKey, string secretKey)
    {
        var tokenUrl = "https://aip.baidubce.com/oauth/2.0/token" +
                       "?grant_type=client_credentials" +
                       $"&client_id={Uri.EscapeDataString(apiKey)}" +
                       $"&client_secret={Uri.EscapeDataString(secretKey)}";
        var tokenResp = await _httpClient.PostAsync(tokenUrl, null);
        return await tokenResp.Content.ReadAsStringAsync();
    }

    public static string GetOutputFormatExtension(string outputFormat) =>
        NormalizeOutputFormat(outputFormat) switch
        {
            "wav" => ".wav",
            "pcm_16k" or "pcm_8k" => ".pcm",
            _ => ".mp3"
        };

    private static int GetAue(string outputFormat) =>
        NormalizeOutputFormat(outputFormat) switch
        {
            "pcm_16k" => 4,
            "pcm_8k" => 5,
            "wav" => 6,
            _ => 3
        };

    private static string NormalizeOutputFormat(string outputFormat)
    {
        var normalized = (outputFormat ?? "").Trim().ToLowerInvariant();
        return normalized is "mp3" or "pcm_16k" or "pcm_8k" or "wav" ? normalized : "mp3";
    }

    private string GetOutputFilePath(string outputFormat)
    {
        var dir = _settingsService.Settings.OutputDirectory;
        return AudioOutputPath.Reserve(dir, "baidu", GetOutputFormatExtension(outputFormat));
    }

    private static (string ApiKey, string SecretKey)? ParseCredentials(string apiKey)
    {
        var keys = apiKey.Split('|', StringSplitOptions.TrimEntries);
        if (keys.Length < 2 || string.IsNullOrWhiteSpace(keys[0]) || string.IsNullOrWhiteSpace(keys[1]))
            return null;
        return (keys[0], keys[1]);
    }
}
