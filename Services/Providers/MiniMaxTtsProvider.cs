using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using VoiceServiceDemo.Models;

namespace VoiceServiceDemo.Services.Providers;

public sealed class MiniMaxTtsProvider
{
    private const string T2aEndpoint = "https://api.minimax.io/v1/t2a_v2";
    private const string VoiceEndpoint = "https://api.minimax.io/v1/get_voice";
    private const string DefaultModel = "speech-2.8-hd";
    private const string DefaultVoice = "Chinese (Mandarin)_Reliable_Executive";

    private readonly HttpClient _httpClient;
    private readonly SettingsService _settingsService;

    public MiniMaxTtsProvider(HttpClient httpClient, SettingsService settingsService)
    {
        _httpClient = httpClient;
        _settingsService = settingsService;
    }

    public Task<(bool Success, string Message)> TestConnectivityAsync(string apiKey)
    {
        return Task.FromResult(string.IsNullOrWhiteSpace(apiKey)
            ? (false, "MiniMax API Key 为空，请先填写。")
            : (true, "MiniMax API Key 已填写。连通性将在生成或刷新音色库时验证，测试按钮不会发起语音合成以避免消耗额度。"));
    }

    public async Task<TtsResult> GenerateAsync(TtsRequest request, string apiKey)
    {
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, T2aEndpoint);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
        httpRequest.Content = new StringContent(BuildT2aRequestJson(request), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(httpRequest);
        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return new TtsResult { Success = false, ErrorMessage = $"MiniMax API 错误 ({response.StatusCode}): {json}" };

        if (!TryExtractAudioBytes(json, out var audioBytes))
            return new TtsResult { Success = false, ErrorMessage = "MiniMax 返回结果无法解析音频数据: " + json };

        var filePath = GetOutputFilePath(request.OutputFormat);
        await File.WriteAllBytesAsync(filePath, audioBytes);

        var vendor = VendorRegistry.GetById("minimax");
        return new TtsResult
        {
            Success = true,
            FilePath = filePath,
            VendorName = vendor?.Name ?? "",
            ModelName = string.IsNullOrWhiteSpace(request.ModelId) ? DefaultModel : request.ModelId,
            VoiceName = string.IsNullOrWhiteSpace(request.VoiceId) ? DefaultVoice : request.VoiceId,
            Text = request.Text
        };
    }

    public async Task<List<VoiceOption>> FetchVoicesAsync(string apiKey)
    {
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, VoiceEndpoint);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
        httpRequest.Content = new StringContent("""{"voice_type":"all"}""", Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(httpRequest);
        if (!response.IsSuccessStatusCode)
            return new List<VoiceOption>();

        var json = await response.Content.ReadAsStringAsync();
        return ParseVoicesJson(json);
    }

    public static string BuildT2aRequestJson(TtsRequest request)
    {
        var body = new
        {
            model = string.IsNullOrWhiteSpace(request.ModelId) ? DefaultModel : request.ModelId,
            text = request.Text,
            stream = false,
            language_boost = "auto",
            output_format = "hex",
            voice_setting = new
            {
                voice_id = string.IsNullOrWhiteSpace(request.VoiceId) ? DefaultVoice : request.VoiceId,
                speed = Clamp(request.Speed, 0.5, 2.0, 1.0),
                vol = Clamp(request.Volume, 0.1, 10.0, 1.0),
                pitch = 0
            },
            audio_setting = new
            {
                sample_rate = 32000,
                bitrate = 128000,
                format = NormalizeOutputFormat(request.OutputFormat),
                channel = 1
            }
        };

        return JsonSerializer.Serialize(body);
    }

    public static bool TryExtractAudioBytes(string json, out byte[] audioBytes)
    {
        audioBytes = Array.Empty<byte>();

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (TryGetNested(doc.RootElement, out var statusCode, "base_resp", "status_code") &&
                statusCode.ValueKind == JsonValueKind.Number &&
                statusCode.GetInt32() != 0)
                return false;

            if (!TryGetNested(doc.RootElement, out var audio, "data", "audio") ||
                audio.ValueKind != JsonValueKind.String)
                return false;

            var hex = audio.GetString();
            if (string.IsNullOrWhiteSpace(hex))
                return false;

            return TryDecodeHex(hex, out audioBytes);
        }
        catch
        {
            audioBytes = Array.Empty<byte>();
            return false;
        }
    }

    public static List<VoiceOption> ParseVoicesJson(string json)
    {
        var voices = new List<VoiceOption>();

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (TryGetNested(doc.RootElement, out var statusCode, "base_resp", "status_code") &&
                statusCode.ValueKind == JsonValueKind.Number &&
                statusCode.GetInt32() != 0)
                return voices;

            AddVoiceArray(voices, doc.RootElement, "system_voice", "系统音色");
            AddVoiceArray(voices, doc.RootElement, "voice_cloning", "克隆音色");
            AddVoiceArray(voices, doc.RootElement, "voice_generation", "生成音色");
        }
        catch
        {
            return new List<VoiceOption>();
        }

        return voices;
    }

    public static string GetOutputFormatExtension(string outputFormat) =>
        NormalizeOutputFormat(outputFormat) switch
        {
            "wav" => ".wav",
            "flac" => ".flac",
            "pcm" => ".pcm",
            _ => ".mp3"
        };

    public static string NormalizeOutputFormat(string outputFormat)
    {
        var normalized = (outputFormat ?? "").Trim().ToLowerInvariant();
        return normalized is "mp3" or "wav" or "flac" or "pcm" ? normalized : "mp3";
    }

    private string GetOutputFilePath(string outputFormat)
    {
        var dir = _settingsService.Settings.OutputDirectory;
        return AudioOutputPath.Reserve(dir, "minimax", GetOutputFormatExtension(outputFormat));
    }

    private static void AddVoiceArray(List<VoiceOption> voices, JsonElement root, string propertyName, string category)
    {
        if (!root.TryGetProperty(propertyName, out var array) || array.ValueKind != JsonValueKind.Array)
            return;

        foreach (var item in array.EnumerateArray())
        {
            var id = TryGetString(item, "voice_id");
            if (string.IsNullOrWhiteSpace(id))
                continue;

            var voiceName = TryGetString(item, "voice_name");
            var displayName = string.IsNullOrWhiteSpace(voiceName) || voiceName == id
                ? id
                : $"{voiceName} ({id})";
            var description = ReadDescription(item);
            var combined = $"{id} {voiceName} {description}";

            voices.Add(new VoiceOption
            {
                Id = id,
                Name = displayName,
                Gender = InferGender(combined),
                Language = InferLanguage(combined),
                Age = TryGetString(item, "created_time"),
                Categories = new List<string> { category }
            });
        }
    }

    private static string ReadDescription(JsonElement item)
    {
        if (!item.TryGetProperty("description", out var description))
            return "";

        if (description.ValueKind == JsonValueKind.Array)
        {
            return string.Join(" ", description
                .EnumerateArray()
                .Where(value => value.ValueKind == JsonValueKind.String)
                .Select(value => value.GetString())
                .Where(value => !string.IsNullOrWhiteSpace(value)));
        }

        return description.ValueKind == JsonValueKind.String ? description.GetString() ?? "" : "";
    }

    private static string InferGender(string value)
    {
        var lower = value.ToLowerInvariant();
        if (lower.Contains("female") || lower.Contains("lady") || lower.Contains("girl") || value.Contains('女'))
            return "女";
        if (lower.Contains(" male") || lower.Contains("_male") || lower.Contains("(male") || lower.Contains("man ") ||
            lower.Contains("boy") || value.Contains('男'))
            return "男";
        return "中性";
    }

    private static string InferLanguage(string value)
    {
        var lower = value.ToLowerInvariant();
        if (lower.Contains("cantonese") || lower.Contains("yue"))
            return "粤语";
        if (lower.Contains("chinese") || lower.Contains("mandarin") || value.Contains('中'))
            return "中文";
        if (lower.Contains("english"))
            return "英文";
        if (lower.Contains("japanese"))
            return "日文";
        if (lower.Contains("korean"))
            return "韩文";
        return "多语言";
    }

    private static bool TryDecodeHex(string value, out byte[] bytes)
    {
        var hex = new string(value.Where(c => !char.IsWhiteSpace(c)).ToArray());
        bytes = Array.Empty<byte>();
        if (hex.Length == 0 || hex.Length % 2 != 0)
            return false;

        var result = new byte[hex.Length / 2];
        for (var i = 0; i < result.Length; i++)
        {
            var pair = hex.Substring(i * 2, 2);
            if (!byte.TryParse(pair, System.Globalization.NumberStyles.HexNumber, null, out result[i]))
                return false;
        }

        bytes = result;
        return true;
    }

    private static double Clamp(double value, double min, double max, double fallback)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            return fallback;
        return Math.Min(max, Math.Max(min, value));
    }

    private static string? TryGetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool TryGetNested(JsonElement root, out JsonElement value, params string[] names)
    {
        value = root;
        foreach (var name in names)
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(name, out value))
                return false;
        }

        return true;
    }
}
