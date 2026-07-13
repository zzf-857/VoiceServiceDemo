using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using VoiceServiceDemo.Models;

namespace VoiceServiceDemo.Services.Providers;

public sealed class CartesiaTtsProvider : ITtsProvider, IVoiceCatalogProvider
{
    private const string TtsEndpoint = "https://api.cartesia.ai/tts/bytes";
    private const string VoicesEndpoint = "https://api.cartesia.ai/voices";
    private const string DefaultModel = "sonic-3.5";
    private const string DefaultVoice = "db6b0ed5-d5d3-463d-ae85-518a07d3c2b4";

    public const string ApiVersion = "2026-03-01";

    private readonly HttpClient _httpClient;
    private readonly SettingsService _settingsService;

    public CartesiaTtsProvider(HttpClient httpClient, SettingsService settingsService)
    {
        _httpClient = httpClient;
        _settingsService = settingsService;
    }

    public string VendorId => "cartesia";

    public async Task<(bool Success, string Message)> TestConnectivityAsync(
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(apiKey))
            return (false, "Cartesia API Key 为空，请先填写。");

        using var request = CreateRequest(HttpMethod.Get, VoicesEndpoint, apiKey);
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        return response.IsSuccessStatusCode
            ? (true, "Cartesia 连接成功，可以刷新在线音色库。")
            : (false, $"Cartesia 鉴权或音色列表请求失败 ({response.StatusCode})");
    }

    public async Task<TtsResult> GenerateAsync(
        TtsRequest request,
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(apiKey))
            return new TtsResult { Success = false, ErrorMessage = "Cartesia API Key 为空，请先填写。" };

        using var httpRequest = CreateRequest(HttpMethod.Post, TtsEndpoint, apiKey);
        httpRequest.Content = new StringContent(BuildRequestJson(request), Encoding.UTF8, "application/json");
        using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var audioBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return new TtsResult
            {
                Success = false,
                ErrorMessage = $"Cartesia API 错误 ({response.StatusCode}): {DecodeSafeText(audioBytes)}"
            };
        }

        if (audioBytes.Length == 0)
            return new TtsResult { Success = false, ErrorMessage = "Cartesia 返回了空音频数据。" };

        var outputFormat = NormalizeOutputFormat(request.OutputFormat);
        var filePath = AudioOutputPath.Reserve(
            _settingsService.Settings.OutputDirectory,
            VendorId,
            outputFormat == "wav" ? ".wav" : ".mp3");
        try
        {
            await File.WriteAllBytesAsync(filePath, audioBytes, cancellationToken);
        }
        catch
        {
            TryDelete(filePath);
            throw;
        }

        var vendor = VendorRegistry.GetById(VendorId);
        return new TtsResult
        {
            Success = true,
            FilePath = filePath,
            VendorName = vendor?.Name ?? "Cartesia",
            ModelName = ResolveModel(request.ModelId),
            VoiceName = ResolveVoice(request.VoiceId),
            Text = request.Text
        };
    }

    public async Task<List<VoiceOption>> FetchVoicesAsync(
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("Cartesia API Key 为空。", nameof(apiKey));

        using var request = CreateRequest(HttpMethod.Get, VoicesEndpoint, apiKey);
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseVoicesJson(json);
    }

    public static string BuildRequestJson(TtsRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var outputFormat = NormalizeOutputFormat(request.OutputFormat);
        var output = outputFormat == "wav"
            ? new Dictionary<string, object>
            {
                ["container"] = "wav",
                ["encoding"] = "pcm_s16le",
                ["sample_rate"] = 44_100
            }
            : new Dictionary<string, object>
            {
                ["container"] = "mp3",
                ["sample_rate"] = 44_100,
                ["bit_rate"] = 128_000
            };

        var generationConfig = new Dictionary<string, object>
        {
            ["speed"] = Clamp(request.Speed, 0.6, 1.5, 1.0),
            ["volume"] = Clamp(request.Volume, 0.5, 2.0, 1.0)
        };
        if (!string.IsNullOrWhiteSpace(request.Emotion))
            generationConfig["emotion"] = request.Emotion.Trim();

        var transcript = request.InputFormat == TtsInputFormat.Ssml && !string.IsNullOrWhiteSpace(request.SsmlText)
            ? request.SsmlText
            : request.Text;
        var body = new Dictionary<string, object>
        {
            ["model_id"] = ResolveModel(request.ModelId),
            ["transcript"] = transcript,
            ["voice"] = new Dictionary<string, string>
            {
                ["mode"] = "id",
                ["id"] = ResolveVoice(request.VoiceId)
            },
            ["output_format"] = output,
            ["generation_config"] = generationConfig
        };

        return JsonSerializer.Serialize(body);
    }

    public static List<VoiceOption> ParseVoicesJson(string json)
    {
        var voices = new List<VoiceOption>();
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var items = root.ValueKind == JsonValueKind.Array
                ? root
                : root.ValueKind == JsonValueKind.Object &&
                  root.TryGetProperty("data", out var data) &&
                  data.ValueKind == JsonValueKind.Array
                    ? data
                    : default;
            if (items.ValueKind != JsonValueKind.Array)
                return voices;

            foreach (var item in items.EnumerateArray())
            {
                var id = GetString(item, "id");
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                var metadata = item.TryGetProperty("metadata", out var metadataElement) && metadataElement.ValueKind == JsonValueKind.Object
                    ? metadataElement
                    : default;
                var language = GetString(item, "language") ?? GetString(metadata, "language") ?? ReadFirstString(item, "languages") ?? "多语言";
                var description = GetString(item, "description") ?? GetString(metadata, "description");
                var categories = new List<string>();
                AddCategory(categories, description);
                foreach (var tag in ReadStrings(item, "tags"))
                    AddCategory(categories, tag);
                foreach (var tag in ReadStrings(metadata, "tags"))
                    AddCategory(categories, tag);

                voices.Add(new VoiceOption
                {
                    Id = id,
                    Name = GetString(item, "name") ?? id,
                    Gender = NormalizeGender(GetString(item, "gender") ?? GetString(metadata, "gender")),
                    Language = language,
                    SampleUrl = GetString(item, "preview_url") ?? "",
                    Categories = categories
                });
            }
        }
        catch (JsonException)
        {
            return new List<VoiceOption>();
        }

        return voices;
    }

    public static string NormalizeOutputFormat(string? outputFormat) =>
        string.Equals(outputFormat?.Trim(), "wav", StringComparison.OrdinalIgnoreCase) ? "wav" : "mp3";

    private static HttpRequestMessage CreateRequest(HttpMethod method, string endpoint, string apiKey)
    {
        var request = new HttpRequestMessage(method, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
        request.Headers.Add("Cartesia-Version", ApiVersion);
        return request;
    }

    private static string ResolveModel(string? modelId) =>
        string.IsNullOrWhiteSpace(modelId) ? DefaultModel : modelId.Trim();

    private static string ResolveVoice(string? voiceId) =>
        string.IsNullOrWhiteSpace(voiceId) ? DefaultVoice : voiceId.Trim();

    private static double Clamp(double value, double min, double max, double fallback)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
            return fallback;
        return Math.Min(max, Math.Max(min, value));
    }

    private static string NormalizeGender(string? value) => (value ?? "").Trim().ToLowerInvariant() switch
    {
        "female" or "woman" => "女",
        "male" or "man" => "男",
        _ => "中性"
    };

    private static string? GetString(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(propertyName, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? ReadFirstString(JsonElement element, string propertyName) =>
        ReadStrings(element, propertyName).FirstOrDefault();

    private static IEnumerable<string> ReadStrings(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(propertyName, out var values) ||
            values.ValueKind != JsonValueKind.Array)
            return Array.Empty<string>();

        return values.EnumerateArray()
            .Where(value => value.ValueKind == JsonValueKind.String)
            .Select(value => value.GetString())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToArray();
    }

    private static void AddCategory(List<string> categories, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;
        var normalized = value.Trim();
        if (!categories.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            categories.Add(normalized);
    }

    private static string DecodeSafeText(byte[] bytes)
    {
        if (bytes.Length == 0)
            return "空响应";
        var text = Encoding.UTF8.GetString(bytes).Replace("\r", " ").Replace("\n", " ").Trim();
        return text.Length <= 512 ? text : text[..512];
    }

    private static void TryDelete(string filePath)
    {
        try
        {
            File.Delete(filePath);
        }
        catch
        {
            // Best-effort cleanup after a failed write.
        }
    }
}
