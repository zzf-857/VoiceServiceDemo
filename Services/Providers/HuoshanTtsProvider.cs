using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using VoiceService.Shared;
using VoiceServiceDemo.Helpers;
using VoiceServiceDemo.Models;
using SharedHuoshanCredentials = VoiceService.Shared.HuoshanCredentials;

namespace VoiceServiceDemo.Services.Providers;

public sealed class HuoshanTtsProvider : ITtsProvider, IVoiceCatalogProvider
{
    private readonly HttpClient _httpClient;
    private readonly SettingsService _settingsService;

    public HuoshanTtsProvider(HttpClient httpClient, SettingsService settingsService)
    {
        _httpClient = httpClient;
        _settingsService = settingsService;
    }

    public string VendorId => "huoshan";

    public async Task<(bool Success, string Message)> TestConnectivityAsync(
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var credentials = SharedHuoshanCredentials.Parse(apiKey);
        if (!credentials.HasSpeechCredentials && !credentials.HasV3ApiKeyCredentials)
            return (false, "格式错误，应填写 AppID|AccessToken，或在高级项填写 V3 API Key。");

        if (credentials.HasOpenApiCredentials)
        {
            var result = await SendOpenApiAsync(credentials, "ListSpeakers", HuoshanTtsProtocol.Serialize(HuoshanTtsProtocol.BuildListSpeakersBody(1, 1)), cancellationToken);
            return !result.Success
                ? (false, FormatOpenApiError(result))
                : (true, "火山引擎控制面连接成功，未发起语音合成。");
        }

        return (true, "火山凭证格式完整。未配置 AK/SK 时，连通测试不会发起语音合成以避免消耗额度。");
    }

    public async Task<List<VoiceOption>> FetchVoicesAsync(
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var credentials = SharedHuoshanCredentials.Parse(apiKey);
        if (!credentials.HasOpenApiCredentials)
            throw new InvalidOperationException("刷新火山音色库需要配置 AK/SK。");

        var speakers = await FetchSpeakersAsync(credentials, cancellationToken);
        var timbres = await FetchBigModelTimbresAsync(credentials, cancellationToken);

        if (speakers.Count == 0)
            return timbres;

        var byId = speakers.ToDictionary(v => v.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var timbre in timbres)
        {
            if (byId.TryGetValue(timbre.Id, out var existing))
            {
                if (string.IsNullOrWhiteSpace(existing.SampleUrl))
                    existing.SampleUrl = timbre.SampleUrl;
                foreach (var category in timbre.Categories)
                    if (!existing.Categories.Contains(category))
                        existing.Categories.Add(category);
                existing.Emotions = timbre.Emotions;
                existing.IsBigTTS = true;
            }
            else
            {
                speakers.Add(timbre);
            }
        }

        return speakers;
    }

    public async Task<TtsResult> GenerateAsync(
        TtsRequest request,
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var credentials = SharedHuoshanCredentials.Parse(apiKey);
        if (!credentials.HasSpeechCredentials && !credentials.HasV3ApiKeyCredentials)
            return new TtsResult { Success = false, ErrorMessage = "火山引擎凭证格式应为: AppID|AccessToken，或在高级项填写 V3 API Key。" };

        if (request.Text.Length > HuoshanTtsProtocol.AsyncTextThreshold)
        {
            if (!credentials.HasV3Credentials)
                return new TtsResult { Success = false, ErrorMessage = "长文本需要火山 V3 凭证，请配置 AppID/AccessToken 或 V3 API Key 与 ResourceId。" };
            return await GenerateLongTextV3Async(request, credentials, cancellationToken);
        }

        if (credentials.HasV3Credentials)
        {
            var v3Result = await GenerateV3Async(request, credentials, cancellationToken);
            if (v3Result.Success || credentials.HasV3ApiKeyCredentials)
                return v3Result;

            var legacyResult = await GenerateLegacyAsync(request, credentials, cancellationToken);
            return legacyResult.Success ? legacyResult : v3Result;
        }

        return await GenerateLegacyAsync(request, credentials, cancellationToken);
    }

    private async Task<List<VoiceOption>> FetchSpeakersAsync(
        SharedHuoshanCredentials credentials,
        CancellationToken cancellationToken)
    {
        var options = new List<VoiceOption>();
        var page = 1;
        const int limit = 100;

        while (true)
        {
            var body = HuoshanTtsProtocol.Serialize(HuoshanTtsProtocol.BuildListSpeakersBody(page, limit));
            var apiResult = await SendOpenApiAsync(credentials, "ListSpeakers", body, cancellationToken);
            var json = apiResult.Body;
            try { await File.WriteAllTextAsync(Path.Combine(_settingsService.Settings.OutputDirectory, $"huoshan_speakers_page_{page}.json"), json, cancellationToken); }
            catch (OperationCanceledException) { throw; }
            catch { }
            if (!apiResult.Success)
                throw new HttpRequestException(FormatOpenApiError(apiResult), null, apiResult.StatusCode);
            if (string.IsNullOrWhiteSpace(json))
                break;

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("Result", out var result))
                throw new InvalidDataException("火山音色响应缺少 Result。");

            var speakers = TryGetArray(result, "Speakers", "speakers", "SpeakerList", "Data");
            if (speakers.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException("火山音色响应缺少 Speakers 数组。");

            var pageCount = 0;
            foreach (var speaker in speakers.EnumerateArray())
            {
                pageCount++;
                var voiceType = TryGetStr(speaker, "VoiceType", "voice_type", "SpeakerID", "speaker_id") ?? "";
                if (string.IsNullOrWhiteSpace(voiceType))
                    continue;

                var resourceId = TryGetStr(speaker, "ResourceID", "ResourceId", "resource_id") ?? "";
                var categories = new List<string>();
                if (!string.IsNullOrWhiteSpace(resourceId))
                    categories.Add(resourceId);
                AddLabelCategories(speaker, categories);

                options.Add(new VoiceOption
                {
                    Id = voiceType,
                    Name = TryGetStr(speaker, "Name", "name", "SpeakerName", "speaker_name") ?? voiceType,
                    Gender = TryGetStr(speaker, "Gender", "gender"),
                    Language = ParseLanguage(speaker),
                    SampleUrl = TryGetStr(speaker, "TrialURL", "TrialUrl", "trial_url", "ShortTrialURL", "ShortTrialUrl", "short_trial_url"),
                    Categories = categories,
                    IsBigTTS = resourceId.Contains("seed-tts-2.0", StringComparison.OrdinalIgnoreCase)
                });
            }

            var total = TryGetInt(result, "Total", "total") ?? options.Count;
            if (pageCount == 0 || page * limit >= total)
                break;
            page++;
        }

        return options
            .GroupBy(v => v.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
    }

    private async Task<List<VoiceOption>> FetchBigModelTimbresAsync(
        SharedHuoshanCredentials credentials,
        CancellationToken cancellationToken)
    {
        var apiResult = await SendOpenApiAsync(credentials, "ListBigModelTTSTimbres", "{}", cancellationToken);
        var json = apiResult.Body;
        try { await File.WriteAllTextAsync(Path.Combine(_settingsService.Settings.OutputDirectory, "huoshan_timbres_debug.json"), json, cancellationToken); }
        catch (OperationCanceledException) { throw; }
        catch { }

        if (!apiResult.Success)
            throw new HttpRequestException(FormatOpenApiError(apiResult), null, apiResult.StatusCode);

        var options = new List<VoiceOption>();
        if (string.IsNullOrWhiteSpace(json))
            return options;

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("Result", out var result))
            throw new InvalidDataException("火山大模型音色响应缺少 Result。");

        var timbres = TryGetArray(result, "Timbres", "timbres", "SpeakerList", "TimbreList", "Data");
        if (timbres.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("火山大模型音色响应缺少音色数组。");

        foreach (var timbre in timbres.EnumerateArray())
        {
            var speakerId = TryGetStr(timbre, "SpeakerID", "SpeakerId", "speaker_id", "VoiceType", "voice_type") ?? "";
            if (string.IsNullOrWhiteSpace(speakerId))
                continue;

            if (timbre.TryGetProperty("TimbreInfos", out var infos) && infos.ValueKind == JsonValueKind.Array)
            {
                foreach (var info in infos.EnumerateArray())
                    options.Add(ParseTimbreInfo(speakerId, info));
            }
            else
            {
                options.Add(ParseTimbreInfo(speakerId, timbre));
            }
        }

        return options
            .GroupBy(v => v.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
    }

    private async Task<OpenApiResult> SendOpenApiAsync(
        SharedHuoshanCredentials credentials,
        string action,
        string body,
        CancellationToken cancellationToken)
    {
        var bodyBytes = Encoding.UTF8.GetBytes(body);
        var req = new HttpRequestMessage(HttpMethod.Post, $"https://open.volcengineapi.com/?Action={action}&Version=2025-05-20");
        req.Content = new ByteArrayContent(bodyBytes);
        req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "UTF-8" };

        VolcengineSigner.SignRequest(req, credentials.AccessKey, credentials.SecretKey, "cn-beijing", "speech_saas_prod", bodyBytes);

        var resp = await _httpClient.SendAsync(req, cancellationToken);
        var json = await resp.Content.ReadAsStringAsync(cancellationToken);
        return new OpenApiResult(resp.IsSuccessStatusCode, resp.StatusCode, json);
    }

    private static string FormatOpenApiError(OpenApiResult result)
    {
        var detail = ExtractOpenApiError(result.Body);
        return string.IsNullOrWhiteSpace(detail)
            ? $"控制面 OpenAPI 验证失败 ({result.StatusCode})。AK/SK 可能正确，但接口返回了空错误；请检查 IAM 权限、语音服务是否开通，以及 ListSpeakers 接口权限。"
            : $"控制面 OpenAPI 验证失败 ({result.StatusCode}): {detail}";
    }

    private static string ExtractOpenApiError(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return "";

        try
        {
            using var doc = JsonDocument.Parse(body);
            var code = ExtractString(doc.RootElement, "Code");
            var message = ExtractString(doc.RootElement, "Message");
            if (!string.IsNullOrWhiteSpace(code) && !string.IsNullOrWhiteSpace(message))
                return $"{code} - {message}";
            if (!string.IsNullOrWhiteSpace(code))
                return code;
            if (!string.IsNullOrWhiteSpace(message))
                return message;
        }
        catch (JsonException)
        {
        }

        return body.Length <= 500 ? body : body[..500] + "...";
    }

    private VoiceOption ParseTimbreInfo(string speakerId, JsonElement info)
    {
        var categories = new List<string>();
        if (info.TryGetProperty("Categories", out var cats) && cats.ValueKind == JsonValueKind.Array)
        {
            foreach (var cat in cats.EnumerateArray())
            {
                var c = TryGetStr(cat, "Category");
                if (!string.IsNullOrEmpty(c)) categories.Add(c);
                if (cat.TryGetProperty("NextCategory", out var next))
                {
                    var nc = TryGetStr(next, "Category");
                    if (!string.IsNullOrEmpty(nc)) categories.Add(nc);
                }
            }
        }

        var emotions = new List<EmotionInfo>();
        string? demoUrl = null;
        if (info.TryGetProperty("Emotions", out var emos) && emos.ValueKind == JsonValueKind.Array)
        {
            foreach (var emo in emos.EnumerateArray())
            {
                var emotion = new EmotionInfo
                {
                    Emotion = TryGetStr(emo, "Emotion", "emotion") ?? "",
                    EmotionType = TryGetStr(emo, "EmotionType", "emotion_type") ?? "",
                    DemoText = TryGetStr(emo, "DemoText", "demo_text"),
                    DemoUrl = TryGetStr(emo, "DemoURL", "DemoUrl", "demo_url")
                };
                emotions.Add(emotion);
                demoUrl ??= emotion.DemoUrl;
            }
        }

        return new VoiceOption
        {
            Id = speakerId,
            Name = TryGetStr(info, "SpeakerName", "Name", "DisplayName", "speaker_name") ?? speakerId,
            Gender = TryGetStr(info, "Gender", "gender"),
            Age = TryGetStr(info, "Age", "age"),
            Language = categories.Contains("多语种") && categories.Count > 1
                ? categories.LastOrDefault(c => c != "多语种") ?? "中文"
                : "中文",
            SampleUrl = demoUrl ?? TryGetStr(info, "DemoURL", "DemoUrl", "AudioUrl"),
            Categories = categories,
            Emotions = emotions,
            IsBigTTS = true
        };
    }

    private async Task<TtsResult> GenerateLegacyAsync(
        TtsRequest request,
        SharedHuoshanCredentials credentials,
        CancellationToken cancellationToken)
    {
        var isBigTTS = request.VoiceId.EndsWith("_bigtts") ||
                       request.VoiceId.EndsWith("_tob") ||
                       request.VoiceId.Contains("_moon_") ||
                       request.VoiceId.Contains("_mars_") ||
                       request.VoiceId.Contains("_wvae_") ||
                       request.VoiceId.StartsWith("ICL_") ||
                       request.VoiceId.StartsWith("multi_");

        var cluster = isBigTTS ? "" : credentials.ClusterOrDefault;

        var body = HuoshanTtsProtocol.BuildLegacyRequestBody(
            request.Text,
            request.VoiceId,
            credentials.AppId,
            cluster,
            request.Speed,
            request.Volume,
            request.Emotion,
            request.OutputFormat);

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://openspeech.bytedance.com/api/v1/tts");
        httpRequest.Headers.TryAddWithoutValidation("Authorization", $"Bearer;{credentials.AccessToken}");
        httpRequest.Content = new StringContent(HuoshanTtsProtocol.Serialize(body), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var respJson = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            return new TtsResult { Success = false, ErrorMessage = $"火山引擎 API 错误 ({response.StatusCode}): {respJson}" };

        var doc = JsonDocument.Parse(respJson);
        if (doc.RootElement.TryGetProperty("data", out var audioBase64) && audioBase64.ValueKind == JsonValueKind.String)
        {
            var audioBytes = Convert.FromBase64String(audioBase64.GetString()!);
            return await SaveAudioBytesAsync(audioBytes, request, cancellationToken);
        }

        return new TtsResult { Success = false, ErrorMessage = "火山引擎返回结果无法解析 data: " + respJson };
    }

    private async Task<TtsResult> GenerateV3Async(
        TtsRequest request,
        SharedHuoshanCredentials credentials,
        CancellationToken cancellationToken)
    {
        var requestId = Guid.NewGuid().ToString();
        var resourceId = HuoshanTtsProtocol.InferResourceId(request.VoiceId, request.ModelId, credentials.ResourceId, request.ResourceId);
        var body = HuoshanTtsProtocol.Serialize(HuoshanTtsProtocol.BuildV3RequestBody(
            request.Text,
            request.VoiceId,
            request.Speed,
            request.Volume,
            "voice_ops",
            request.Emotion,
            request.OutputFormat));

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://openspeech.bytedance.com/api/v3/tts/unidirectional");
        HuoshanTtsProtocol.AddV3Headers(httpRequest, credentials, resourceId, requestId);
        httpRequest.Content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(cancellationToken);
            return new TtsResult { Success = false, ErrorMessage = $"火山 V3 API 错误 ({response.StatusCode}, reqid={requestId}): {HuoshanTtsProtocol.DescribeHuoshanError(err)}" };
        }

        var chunks = new List<byte[]>();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            var parsed = HuoshanTtsProtocol.ParseV3StreamLine(line);
            if (parsed.HasAudio)
                chunks.Add(parsed.AudioBytes);
            else if (parsed.IsError)
                return new TtsResult { Success = false, ErrorMessage = $"火山 V3 合成失败 (reqid={requestId}, code={parsed.Code}): {HuoshanTtsProtocol.DescribeHuoshanError(parsed.ErrorMessage)}" };
            else if (parsed.IsTerminal)
                break;
        }

        if (chunks.Count == 0)
            return new TtsResult { Success = false, ErrorMessage = $"火山 V3 未返回音频数据 (reqid={requestId})" };

        var audioBytes = chunks.SelectMany(c => c).ToArray();
        return await SaveAudioBytesAsync(audioBytes, request, cancellationToken);
    }

    private async Task<TtsResult> GenerateLongTextV3Async(
        TtsRequest request,
        SharedHuoshanCredentials credentials,
        CancellationToken cancellationToken)
    {
        var requestId = Guid.NewGuid().ToString();
        var resourceId = HuoshanTtsProtocol.InferResourceId(request.VoiceId, request.ModelId, credentials.ResourceId, request.ResourceId);
        var submitBody = HuoshanTtsProtocol.Serialize(HuoshanTtsProtocol.BuildV3AsyncSubmitBody(
            request.Text,
            request.VoiceId,
            request.Speed,
            request.Volume,
            "voice_ops",
            requestId,
            request.Emotion,
            request.OutputFormat));

        var submit = new HttpRequestMessage(HttpMethod.Post, "https://openspeech.bytedance.com/api/v3/tts/submit");
        HuoshanTtsProtocol.AddV3Headers(submit, credentials, resourceId, requestId);
        submit.Content = new StringContent(submitBody, Encoding.UTF8, "application/json");

        var submitResponse = await _httpClient.SendAsync(submit, cancellationToken);
        var submitJson = await submitResponse.Content.ReadAsStringAsync(cancellationToken);
        if (!submitResponse.IsSuccessStatusCode)
            return new TtsResult { Success = false, ErrorMessage = $"火山 V3 长文本提交失败 ({submitResponse.StatusCode}, reqid={requestId}): {HuoshanTtsProtocol.DescribeHuoshanError(submitJson)}" };

        var taskId = ExtractString(submitJson, "task_id");
        if (string.IsNullOrWhiteSpace(taskId))
            return new TtsResult { Success = false, ErrorMessage = $"火山 V3 长文本提交结果无法解析 task_id: {submitJson}" };

        for (var attempt = 0; attempt < 30; attempt++)
        {
            await Task.Delay(TimeSpan.FromSeconds(attempt == 0 ? 1 : 2), cancellationToken);
            var queryId = Guid.NewGuid().ToString();
            var query = new HttpRequestMessage(HttpMethod.Post, "https://openspeech.bytedance.com/api/v3/tts/query");
            HuoshanTtsProtocol.AddV3Headers(query, credentials, resourceId, queryId);
            query.Content = new StringContent(HuoshanTtsProtocol.Serialize(HuoshanTtsProtocol.BuildV3AsyncQueryBody(taskId)), Encoding.UTF8, "application/json");

            var queryResponse = await _httpClient.SendAsync(query, cancellationToken);
            var queryJson = await queryResponse.Content.ReadAsStringAsync(cancellationToken);
            if (!queryResponse.IsSuccessStatusCode)
                return new TtsResult { Success = false, ErrorMessage = $"火山 V3 长文本查询失败 ({queryResponse.StatusCode}, task_id={taskId}): {HuoshanTtsProtocol.DescribeHuoshanError(queryJson)}" };

            var status = ExtractInt(queryJson, "task_status");
            if (status == 3)
                return new TtsResult { Success = false, ErrorMessage = $"火山 V3 长文本任务失败 (task_id={taskId}): {HuoshanTtsProtocol.DescribeHuoshanError(queryJson)}" };

            var audioUrl = ExtractString(queryJson, "audio_url");
            if (status == 2 && !string.IsNullOrWhiteSpace(audioUrl))
            {
                var audioBytes = await _httpClient.GetByteArrayAsync(audioUrl, cancellationToken);
                return await SaveAudioBytesAsync(audioBytes, request, cancellationToken);
            }
        }

        return new TtsResult { Success = false, ErrorMessage = $"火山 V3 长文本任务已提交但尚未完成，task_id={taskId}。请稍后查询或缩短文本。" };
    }

    private async Task<TtsResult> SaveAudioBytesAsync(
        byte[] audioBytes,
        TtsRequest request,
        CancellationToken cancellationToken)
    {
        var filePath = GetOutputFilePath(request.OutputFormat);
        await File.WriteAllBytesAsync(filePath, audioBytes, cancellationToken);

        var vendor = VendorRegistry.GetById(request.VendorId);
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

    private string GetOutputFilePath(string outputFormat)
    {
        var dir = _settingsService.Settings.OutputDirectory;
        return AudioOutputPath.Reserve(dir, "huoshan", HuoshanTtsProtocol.GetOutputFormatExtension(outputFormat));
    }

    private static JsonElement TryGetArray(JsonElement elem, params string[] names)
    {
        foreach (var name in names)
            if (elem.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array)
                return value;
        return default;
    }

    private static int? TryGetInt(JsonElement elem, params string[] names)
    {
        foreach (var name in names)
        {
            if (!elem.TryGetProperty(name, out var value))
                continue;
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
                return number;
            if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out number))
                return number;
        }
        return null;
    }

    private static string ParseLanguage(JsonElement speaker)
    {
        if (!speaker.TryGetProperty("Languages", out var languages) || languages.ValueKind != JsonValueKind.Array)
            return "中文";

        var vals = new List<string>();
        foreach (var item in languages.EnumerateArray())
        {
            var value = TryGetStr(item, "Language", "language", "Text", "text");
            if (!string.IsNullOrWhiteSpace(value))
                vals.Add(value);
        }
        return vals.Count == 0 ? "中文" : string.Join(" / ", vals);
    }

    private static void AddLabelCategories(JsonElement speaker, List<string> categories)
    {
        foreach (var labelsName in new[] { "NormalLabels", "normal_labels", "SpecialLabels", "special_labels" })
        {
            if (!speaker.TryGetProperty(labelsName, out var labels) || labels.ValueKind != JsonValueKind.Array)
                continue;
            foreach (var label in labels.EnumerateArray())
            {
                var value = label.ValueKind == JsonValueKind.String
                    ? label.GetString()
                    : TryGetStr(label, "Name", "name", "Label", "label");
                if (!string.IsNullOrWhiteSpace(value) && !categories.Contains(value))
                    categories.Add(value);
            }
        }
    }

    private static string? ExtractString(string json, string propertyName)
    {
        using var doc = JsonDocument.Parse(json);
        return ExtractString(doc.RootElement, propertyName);
    }

    private static string? ExtractString(JsonElement elem, string propertyName)
    {
        if (elem.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in elem.EnumerateObject())
            {
                if (string.Equals(prop.Name, propertyName, StringComparison.OrdinalIgnoreCase) && prop.Value.ValueKind == JsonValueKind.String)
                    return prop.Value.GetString();
                var nested = ExtractString(prop.Value, propertyName);
                if (!string.IsNullOrWhiteSpace(nested))
                    return nested;
            }
        }
        return null;
    }

    private static int? ExtractInt(string json, string propertyName)
    {
        using var doc = JsonDocument.Parse(json);
        return ExtractInt(doc.RootElement, propertyName);
    }

    private static int? ExtractInt(JsonElement elem, string propertyName)
    {
        if (elem.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in elem.EnumerateObject())
            {
                if (string.Equals(prop.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetInt32(out var n))
                        return n;
                    if (prop.Value.ValueKind == JsonValueKind.String && int.TryParse(prop.Value.GetString(), out n))
                        return n;
                }
                var nested = ExtractInt(prop.Value, propertyName);
                if (nested.HasValue)
                    return nested;
            }
        }
        return null;
    }

    private static string? TryGetStr(JsonElement elem, params string[] names)
    {
        foreach (var n in names)
            if (elem.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String)
                return v.GetString();
        return null;
    }

    private sealed record OpenApiResult(bool Success, System.Net.HttpStatusCode StatusCode, string Body);
}
