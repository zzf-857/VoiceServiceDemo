using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using VoiceServiceDemo.Models;

namespace VoiceServiceDemo.Services.Providers;

public sealed class XiaomiMimoTtsProvider
{
    private const string Endpoint = "https://api.xiaomimimo.com/v1/chat/completions";
    private const string DefaultModel = "mimo-v2.5-tts";

    private readonly HttpClient _httpClient;
    private readonly SettingsService _settingsService;

    public XiaomiMimoTtsProvider(HttpClient httpClient, SettingsService settingsService)
    {
        _httpClient = httpClient;
        _settingsService = settingsService;
    }

    public Task<(bool Success, string Message)> TestConnectivityAsync(string apiKey)
    {
        return Task.FromResult(string.IsNullOrWhiteSpace(apiKey)
            ? (false, "小米 MiMo API Key 为空，请先填写。")
            : (true, "小米 MiMo API Key 已填写。连通性将在生成时验证，测试按钮不会发起语音合成以避免消耗额度。"));
    }

    public async Task<TtsResult> GenerateAsync(TtsRequest request, string apiKey)
    {
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        httpRequest.Headers.Add("api-key", apiKey);
        httpRequest.Content = new StringContent(BuildChatCompletionRequestJson(request), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(httpRequest);
        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return new TtsResult { Success = false, ErrorMessage = $"小米 MiMo API 错误 ({response.StatusCode}): {json}" };

        if (!TryExtractAudioBytes(json, out var audioBytes))
            return new TtsResult { Success = false, ErrorMessage = "小米 MiMo 返回结果无法解析音频数据: " + json };

        var filePath = GetOutputFilePath(request.OutputFormat);
        await File.WriteAllBytesAsync(filePath, audioBytes);

        var vendor = VendorRegistry.GetById("xiaomi_mimo");
        return new TtsResult
        {
            Success = true,
            FilePath = filePath,
            VendorName = vendor?.Name ?? "",
            ModelName = string.IsNullOrWhiteSpace(request.ModelId) ? DefaultModel : request.ModelId,
            VoiceName = request.VoiceId,
            Text = request.Text
        };
    }

    public static string BuildChatCompletionRequestJson(TtsRequest request)
    {
        var messages = new List<Dictionary<string, string>>();
        if (!string.IsNullOrWhiteSpace(request.Instructions))
        {
            messages.Add(new Dictionary<string, string>
            {
                ["role"] = "user",
                ["content"] = request.Instructions.Trim()
            });
        }

        messages.Add(new Dictionary<string, string>
        {
            ["role"] = "assistant",
            ["content"] = request.Text
        });

        var body = new
        {
            model = string.IsNullOrWhiteSpace(request.ModelId) ? DefaultModel : request.ModelId,
            messages,
            audio = new
            {
                format = NormalizeOutputFormat(request.OutputFormat),
                voice = string.IsNullOrWhiteSpace(request.VoiceId) ? "mimo_default" : request.VoiceId
            },
            stream = false
        };

        return JsonSerializer.Serialize(body);
    }

    public static bool TryExtractAudioBytes(string json, out byte[] audioBytes)
    {
        audioBytes = Array.Empty<byte>();

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!TryGetNested(doc.RootElement, out var data, "choices", "0", "message", "audio", "data") ||
                data.ValueKind != JsonValueKind.String)
                return false;

            var value = data.GetString();
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var buffer = new byte[value.Length];
            if (!Convert.TryFromBase64String(value, buffer, out var bytesWritten))
                return false;

            audioBytes = buffer[..bytesWritten];
            return true;
        }
        catch
        {
            audioBytes = Array.Empty<byte>();
            return false;
        }
    }

    public static string GetOutputFormatExtension(string outputFormat) =>
        NormalizeOutputFormat(outputFormat) switch
        {
            "pcm16" => ".pcm",
            _ => ".wav"
        };

    public static string NormalizeOutputFormat(string outputFormat)
    {
        var normalized = (outputFormat ?? "").Trim().ToLowerInvariant();
        return normalized is "wav" or "pcm16" ? normalized : "wav";
    }

    private string GetOutputFilePath(string outputFormat)
    {
        var dir = _settingsService.Settings.OutputDirectory;
        return AudioOutputPath.Reserve(dir, "xiaomi_mimo", GetOutputFormatExtension(outputFormat));
    }

    private static bool TryGetNested(JsonElement root, out JsonElement value, params string[] names)
    {
        value = root;
        foreach (var name in names)
        {
            if (value.ValueKind == JsonValueKind.Array && int.TryParse(name, out var index))
            {
                if (index < 0 || index >= value.GetArrayLength())
                    return false;

                value = value[index];
                continue;
            }

            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(name, out value))
                return false;
        }

        return true;
    }
}
