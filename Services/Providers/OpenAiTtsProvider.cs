using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using VoiceServiceDemo.Models;

namespace VoiceServiceDemo.Services.Providers;

public sealed class OpenAiTtsProvider : ITtsProvider
{
    private readonly HttpClient _httpClient;
    private readonly SettingsService _settingsService;

    public OpenAiTtsProvider(HttpClient httpClient, SettingsService settingsService)
    {
        _httpClient = httpClient;
        _settingsService = settingsService;
    }

    public string VendorId => "openai";

    public async Task<(bool Success, string Message)> TestConnectivityAsync(
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var req = new HttpRequestMessage(HttpMethod.Get, "https://api.openai.com/v1/models");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        var resp = await _httpClient.SendAsync(req, cancellationToken);
        return resp.IsSuccessStatusCode
            ? (true, "OpenAI 连接成功 ✓")
            : (false, $"鉴权失败 ({resp.StatusCode})");
    }

    public async Task<TtsResult> GenerateAsync(
        TtsRequest request,
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/audio/speech");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        httpRequest.Content = new StringContent(BuildSpeechRequestJson(request), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(cancellationToken);
            return new TtsResult { Success = false, ErrorMessage = $"OpenAI API 错误 ({response.StatusCode}): {err}" };
        }

        var audioBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        var filePath = GetOutputFilePath(request.OutputFormat);
        await File.WriteAllBytesAsync(filePath, audioBytes, cancellationToken);

        var vendor = VendorRegistry.GetById("openai");
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

    public static string BuildSpeechRequestJson(TtsRequest request)
    {
        var body = new Dictionary<string, object?>
        {
            ["model"] = request.ModelId,
            ["input"] = request.Text,
            ["voice"] = request.VoiceId,
            ["speed"] = request.Speed,
            ["response_format"] = NormalizeResponseFormat(request.OutputFormat)
        };

        if (SupportsInstructions(request.ModelId) && !string.IsNullOrWhiteSpace(request.Instructions))
            body["instructions"] = request.Instructions.Trim();

        return JsonSerializer.Serialize(body);
    }

    public static bool SupportsInstructions(string modelId) =>
        modelId.StartsWith("gpt-4o", StringComparison.OrdinalIgnoreCase);

    public static string GetResponseFormatExtension(string responseFormat) =>
        "." + NormalizeResponseFormat(responseFormat);

    private static string NormalizeResponseFormat(string responseFormat)
    {
        var normalized = (responseFormat ?? "").Trim().ToLowerInvariant();
        return normalized is "mp3" or "opus" or "aac" or "flac" or "wav" or "pcm"
            ? normalized
            : "mp3";
    }

    private string GetOutputFilePath(string responseFormat)
    {
        var dir = _settingsService.Settings.OutputDirectory;
        return AudioOutputPath.Reserve(dir, "openai", GetResponseFormatExtension(responseFormat));
    }
}
