using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using VoiceServiceDemo.Helpers;
using VoiceServiceDemo.Models;

namespace VoiceServiceDemo.Services.Providers;

public sealed class AmazonPollyTtsProvider : ITtsProvider, IVoiceCatalogProvider
{
    private const int MaxVoicePages = 20;
    private const string DefaultEngine = "neural";
    private const string DefaultVoice = "Joanna";

    private static readonly HashSet<string> SupportedEngines = new(
        new[] { "standard", "neural", "long-form", "generative" },
        StringComparer.Ordinal);
    private static readonly HashSet<string> SupportedOutputFormats = new(
        new[] { "mp3", "ogg_vorbis", "pcm" },
        StringComparer.Ordinal);

    private readonly HttpClient _httpClient;
    private readonly SettingsService _settingsService;
    private readonly Func<DateTimeOffset> _utcNow;

    public AmazonPollyTtsProvider(
        HttpClient httpClient,
        SettingsService settingsService,
        Func<DateTimeOffset>? utcNow = null)
    {
        _httpClient = httpClient;
        _settingsService = settingsService;
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    public string VendorId => "aws-polly";

    public async Task<(bool Success, string Message)> TestConnectivityAsync(
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AwsPollyCredentials credentials;
        try
        {
            credentials = AwsPollyCredentials.Parse(apiKey);
        }
        catch (ArgumentException ex)
        {
            return (false, ex.Message);
        }

        using var request = CreateSignedRequest(
            HttpMethod.Get,
            BuildEndpoint(credentials.Region, "/v1/voices"),
            ReadOnlySpan<byte>.Empty,
            credentials);
        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        return response.IsSuccessStatusCode
            ? (true, "Amazon Polly 连接成功，可以刷新在线音色库。")
            : (false, $"Amazon Polly 鉴权或音色列表请求失败 ({response.StatusCode})");
    }

    public async Task<TtsResult> GenerateAsync(
        TtsRequest request,
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AwsPollyCredentials credentials;
        try
        {
            credentials = AwsPollyCredentials.Parse(apiKey);
        }
        catch (ArgumentException ex)
        {
            return new TtsResult { Success = false, ErrorMessage = ex.Message };
        }

        var engine = NormalizeEngine(request.ModelId);
        var outputFormat = NormalizeOutputFormat(request.OutputFormat);
        var voiceId = NormalizeVoice(request.VoiceId);
        var textType = request.InputFormat == TtsInputFormat.Ssml ? "ssml" : "text";
        var text = request.InputFormat == TtsInputFormat.Ssml ? request.SsmlText : request.Text;
        var payload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            VoiceId = voiceId,
            Engine = engine,
            OutputFormat = outputFormat,
            TextType = textType,
            Text = text
        });
        var filePath = AudioOutputPath.Reserve(
            _settingsService.Settings.OutputDirectory,
            VendorId,
            GetOutputExtension(outputFormat));

        try
        {
            using var httpRequest = CreateSignedRequest(
                HttpMethod.Post,
                BuildEndpoint(credentials.Region, "/v1/speech"),
                payload,
                credentials);
            httpRequest.Content = new ByteArrayContent(payload);
            httpRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json")
            {
                CharSet = "utf-8"
            };

            using var response = await _httpClient.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            var audioBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                TryDelete(filePath);
                return new TtsResult
                {
                    Success = false,
                    ErrorMessage = $"Amazon Polly API 请求失败 ({response.StatusCode})。"
                };
            }

            if (audioBytes.Length == 0)
            {
                TryDelete(filePath);
                return new TtsResult
                {
                    Success = false,
                    ErrorMessage = "Amazon Polly 返回了空音频数据。"
                };
            }

            await File.WriteAllBytesAsync(filePath, audioBytes, cancellationToken);
            var vendor = VendorRegistry.GetById(VendorId);
            return new TtsResult
            {
                Success = true,
                FilePath = filePath,
                VendorName = vendor?.Name ?? "Amazon Polly",
                ModelName = engine,
                VoiceName = voiceId,
                Text = text
            };
        }
        catch
        {
            TryDelete(filePath);
            throw;
        }
    }

    public async Task<List<VoiceOption>> FetchVoicesAsync(
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var credentials = AwsPollyCredentials.Parse(apiKey);
        var voices = new List<VoiceOption>();
        string? nextToken = null;

        for (var page = 0; page < MaxVoicePages; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var endpoint = BuildEndpoint(credentials.Region, "/v1/voices", nextToken);
            using var request = CreateSignedRequest(
                HttpMethod.Get,
                endpoint,
                ReadOnlySpan<byte>.Empty,
                credentials);
            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var parsedPage = ParseVoicesPage(json);
            voices.AddRange(parsedPage.Voices);
            nextToken = parsedPage.NextToken;
            if (string.IsNullOrWhiteSpace(nextToken))
                break;
        }

        return voices;
    }

    public static string GetOutputExtension(string? outputFormat) => NormalizeOutputFormat(outputFormat) switch
    {
        "ogg_vorbis" => ".ogg",
        "pcm" => ".pcm",
        _ => ".mp3"
    };

    private HttpRequestMessage CreateSignedRequest(
        HttpMethod method,
        Uri endpoint,
        ReadOnlySpan<byte> payload,
        AwsPollyCredentials credentials)
    {
        var signature = AwsSignatureV4.Sign(
            method,
            endpoint,
            payload,
            credentials,
            "polly",
            _utcNow());
        var request = new HttpRequestMessage(method, endpoint);
        const string scheme = "AWS4-HMAC-SHA256";
        request.Headers.Authorization = new AuthenticationHeaderValue(
            scheme,
            signature.Authorization[(scheme.Length + 1)..]);
        request.Headers.Add("x-amz-date", signature.AmzDate);
        if (credentials.HasSessionToken)
            request.Headers.Add("x-amz-security-token", credentials.SessionToken!);
        return request;
    }

    private static Uri BuildEndpoint(string region, string path, string? nextToken = null)
    {
        var endpoint = $"https://polly.{region}.amazonaws.com{path}";
        return string.IsNullOrWhiteSpace(nextToken)
            ? new Uri(endpoint)
            : new Uri($"{endpoint}?NextToken={Uri.EscapeDataString(nextToken)}");
    }

    private static (List<VoiceOption> Voices, string? NextToken) ParseVoicesPage(string json)
    {
        using var document = JsonDocument.Parse(json);
        var voices = new List<VoiceOption>();
        if (document.RootElement.TryGetProperty("Voices", out var items) &&
            items.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in items.EnumerateArray())
            {
                var id = GetString(item, "Id");
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                var name = GetString(item, "Name");
                var languageCode = GetString(item, "LanguageCode");
                _ = GetString(item, "LanguageName");
                _ = ReadStrings(item, "AdditionalLanguageCodes");
                voices.Add(new VoiceOption
                {
                    Id = id,
                    Name = string.IsNullOrWhiteSpace(name) ? id : name,
                    Gender = (GetString(item, "Gender") ?? string.Empty).Trim().ToLowerInvariant(),
                    Language = languageCode ?? string.Empty,
                    Categories = ReadStrings(item, "SupportedEngines").ToList()
                });
            }
        }

        return (voices, GetString(document.RootElement, "NextToken"));
    }

    private static string NormalizeEngine(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        return SupportedEngines.Contains(normalized) ? normalized : DefaultEngine;
    }

    private static string NormalizeOutputFormat(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        return SupportedOutputFormats.Contains(normalized) ? normalized : "mp3";
    }

    private static string NormalizeVoice(string? value) =>
        string.IsNullOrWhiteSpace(value) ? DefaultVoice : value.Trim();

    private static string? GetString(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(propertyName, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static IEnumerable<string> ReadStrings(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(propertyName, out var values) ||
            values.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return values.EnumerateArray()
            .Where(value => value.ValueKind == JsonValueKind.String)
            .Select(value => value.GetString())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .ToArray();
    }

    private static void TryDelete(string filePath)
    {
        try
        {
            File.Delete(filePath);
        }
        catch
        {
            // Best-effort cleanup for failed, empty, or cancelled synthesis.
        }
    }
}
