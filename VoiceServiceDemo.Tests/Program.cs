using VoiceService.Shared;
using VoiceServiceDemo.Models;
using VoiceServiceDemo.Services;
using VoiceServiceDemo.Services.Providers;
using System.Text.Json;

static void AssertEqual<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new Exception($"{message}: expected '{expected}', got '{actual}'");
}

static void AssertTrue(bool condition, string message)
{
    if (!condition)
        throw new Exception(message);
}

static void AssertFalse(bool condition, string message)
{
    if (condition)
        throw new Exception(message);
}

static bool TryGetNested(JsonElement root, out JsonElement value, params string[] names)
{
    value = root;
    foreach (var name in names)
    {
        if (!value.TryGetProperty(name, out value))
            return false;
    }

    return true;
}

var basic = VoiceService.Shared.HuoshanCredentials.Parse("app-123|token-abc");
AssertEqual("app-123", basic.AppId, "parses app id");
AssertEqual("token-abc", basic.AccessToken, "parses access token");
AssertEqual("", basic.Cluster, "missing cluster is empty");
AssertTrue(basic.HasSpeechCredentials, "two-part credentials are enough for speech generation");
AssertFalse(basic.HasOpenApiCredentials, "two-part credentials do not enable OpenAPI voice refresh");
AssertEqual("volcano_tts", basic.ClusterOrDefault, "default cluster is volcano_tts");
AssertEqual("app-123|token-abc", basic.ToStorageString(), "basic storage format trims optional empties");

var full = VoiceService.Shared.HuoshanCredentials.Parse("app-123|token-abc|custom_cluster|ak-1|sk-2");
AssertEqual("custom_cluster", full.Cluster, "parses optional cluster");
AssertEqual("ak-1", full.AccessKey, "parses access key");
AssertEqual("sk-2", full.SecretKey, "parses secret key");
AssertTrue(full.HasOpenApiCredentials, "full credentials enable OpenAPI voice refresh");
AssertEqual("custom_cluster", full.ClusterOrDefault, "custom cluster is preserved");
AssertEqual("app-123|token-abc|custom_cluster|ak-1|sk-2", full.ToStorageString(), "full storage format round trips");

var withOnlyAk = VoiceService.Shared.HuoshanCredentials.Parse("app-123|token-abc||ak-1");
AssertFalse(withOnlyAk.HasOpenApiCredentials, "AK without SK does not enable OpenAPI");
AssertEqual("app-123|token-abc||ak-1", withOnlyAk.ToStorageString(), "interior empty cluster is preserved when AK exists");

Console.WriteLine("Huoshan credential parser tests passed.");

var mcpBasic = VoiceService.Shared.HuoshanCredentials.Parse("app-123|token-abc");
AssertTrue(mcpBasic.HasSpeechCredentials, "MCP two-part credentials are enough for speech generation");
AssertFalse(mcpBasic.HasOpenApiCredentials, "MCP two-part credentials do not enable OpenAPI voice refresh");
AssertEqual("app-123|token-abc", mcpBasic.ToStorageString(), "MCP basic storage format trims optional empties");

Console.WriteLine("MCP Huoshan credential parser tests passed.");

var v3ApiKey = VoiceService.Shared.HuoshanCredentials.Parse("app-123|token-abc|volcano_tts|ak-1|sk-2|api-key-3|seed-tts-2.0");
AssertEqual("api-key-3", v3ApiKey.ApiKey, "parses V3 API key");
AssertEqual("seed-tts-2.0", v3ApiKey.ResourceId, "parses V3 resource id");
AssertTrue(v3ApiKey.HasV3Credentials, "api key plus resource enables V3 speech");
AssertEqual("app-123|token-abc|volcano_tts|ak-1|sk-2|api-key-3|seed-tts-2.0", v3ApiKey.ToStorageString(), "V3 storage format round trips");

var v3App = VoiceService.Shared.HuoshanCredentials.Parse("app-123|token-abc|||||seed-tts-2.0");
AssertTrue(v3App.HasV3Credentials, "AppID plus AccessToken also enables V3 speech when resource id is defaulted");
AssertEqual("seed-tts-2.0", v3App.ResourceIdOrDefault, "default V3 resource id is seed-tts-2.0");

AssertEqual("seed-icl-2.0", HuoshanTtsProtocol.InferResourceId("saturn_voice_123", ""), "saturn voices use ICL 2.0 resource");
AssertEqual("seed-tts-1.0", HuoshanTtsProtocol.InferResourceId("BV001_streaming", "seed-tts-1.0"), "explicit seed model wins for legacy public voices");
AssertEqual("seed-tts-2.0", HuoshanTtsProtocol.InferResourceId("zh_female_cancan_mars_bigtts", ""), "bigtts voices default to seed tts 2.0");
AssertEqual("seed-tts-1.0", HuoshanTtsProtocol.InferResourceId(
    "zh_male_jingqiangkanye_emo_mars_bigtts",
    "seed-tts-2.0",
    "",
    "seed-tts-1.0"), "voice catalog resource wins over stale model dropdown resource");

var sseAudioEvent = HuoshanTtsProtocol.ParseV3StreamLine("data: {\"code\":0,\"data\":\"AQID\"}");
AssertTrue(sseAudioEvent.HasAudio, "SSE data line is parsed as audio");
AssertEqual(3, sseAudioEvent.AudioBytes.Length, "base64 audio payload is decoded");

var terminalEvent = HuoshanTtsProtocol.ParseV3StreamLine("{\"code\":20000000,\"message\":\"ok\"}");
AssertTrue(terminalEvent.IsTerminal, "terminal JSON line is recognized");

var errorEvent = HuoshanTtsProtocol.ParseV3StreamLine("data: {\"code\":45000000,\"message\":\"speaker permission denied\"}");
AssertTrue(errorEvent.IsError, "non-success V3 code is parsed as error");
AssertTrue(errorEvent.ErrorMessage.Contains("speaker permission denied"), "V3 error message is preserved");

var huoshanEmotionBody = HuoshanTtsProtocol.BuildV3RequestBody(
    "今天真开心。",
    "zh_female_wenrouxiaoya_moon_bigtts",
    1.0,
    1.0,
    "voice_ops",
    "happy");
var huoshanEmotionJson = HuoshanTtsProtocol.Serialize(huoshanEmotionBody);
using (var huoshanEmotionDoc = JsonDocument.Parse(huoshanEmotionJson))
{
    AssertTrue(
        TryGetNested(huoshanEmotionDoc.RootElement, out var v3Emotion, "req_params", "audio_params", "emotion"),
        "Huoshan V3 request includes selected emotion under audio_params");
    AssertEqual("happy", v3Emotion.GetString(), "Huoshan V3 request sends selected emotion value");
    AssertFalse(
        TryGetNested(huoshanEmotionDoc.RootElement, out _, "req_params", "emotion"),
        "Huoshan V3 request does not place emotion beside audio_params");
}

var huoshanNeutralBody = HuoshanTtsProtocol.BuildV3RequestBody(
    "今天真开心。",
    "zh_female_wenrouxiaoya_moon_bigtts",
    1.0,
    1.0,
    "voice_ops",
    "");
var huoshanNeutralJson = HuoshanTtsProtocol.Serialize(huoshanNeutralBody);
AssertFalse(huoshanNeutralJson.Contains("\"emotion\""), "Huoshan V3 request omits empty emotion");

var huoshanLegacyEmotionBody = HuoshanTtsProtocol.BuildLegacyRequestBody(
    "今天真开心。",
    "zh_female_wenrouxiaoya_moon_bigtts",
    "app-123",
    "",
    1.0,
    1.0,
    "happy");
var huoshanLegacyEmotionJson = HuoshanTtsProtocol.Serialize(huoshanLegacyEmotionBody);
AssertTrue(huoshanLegacyEmotionJson.Contains("\"emotion\":\"happy\""), "Huoshan legacy request includes selected emotion when V3 falls back");

var huoshanAsyncEmotionBody = HuoshanTtsProtocol.BuildV3AsyncSubmitBody(
    "这是一段长文本。",
    "zh_female_wenrouxiaoya_moon_bigtts",
    1.0,
    1.0,
    "voice_ops",
    "req-123",
    "storytelling");
var huoshanAsyncEmotionJson = HuoshanTtsProtocol.Serialize(huoshanAsyncEmotionBody);
using (var huoshanAsyncEmotionDoc = JsonDocument.Parse(huoshanAsyncEmotionJson))
{
    AssertTrue(
        TryGetNested(huoshanAsyncEmotionDoc.RootElement, out var asyncEmotion, "req_params", "audio_params", "emotion"),
        "Huoshan async V3 request includes selected emotion under audio_params");
    AssertEqual("storytelling", asyncEmotion.GetString(), "Huoshan async V3 request sends selected emotion value");
    AssertFalse(
        TryGetNested(huoshanAsyncEmotionDoc.RootElement, out _, "req_params", "emotion"),
        "Huoshan async V3 request does not place emotion beside audio_params");
}

var huoshanListSpeakersJson = HuoshanTtsProtocol.Serialize(HuoshanTtsProtocol.BuildListSpeakersBody(1, 100));
AssertTrue(huoshanListSpeakersJson.Contains("\"Limit\":100"), "Huoshan ListSpeakers request sends numeric Limit");
AssertTrue(huoshanListSpeakersJson.Contains("\"Page\":1"), "Huoshan ListSpeakers request sends numeric Page");
AssertFalse(huoshanListSpeakersJson.Contains("\"Limit\":\"100\""), "Huoshan ListSpeakers request does not send rejected string Limit");

Console.WriteLine("Shared Huoshan protocol tests passed.");

var verifiedEmotionVoice = new VoiceOption
{
    Id = "zh_male_junlangnanyou_emo_v2_mars_bigtts",
    Name = "俊朗男友",
    Emotions = new List<EmotionInfo>
    {
        new() { Emotion = "开心", EmotionType = "happy" },
        new() { Emotion = "厌恶", EmotionType = "hate" }
    }
};
var verifiedEmotionOptions = HuoshanEmotionPolicy.GetOptions(verifiedEmotionVoice).ToList();
AssertTrue(verifiedEmotionOptions.Any(e => e.Id == "hate"), "Huoshan policy exposes provider-declared emotion options");
AssertEqual("sad", HuoshanEmotionPolicy.ToRequestEmotion(" sad "), "Huoshan policy trims real request emotions");
AssertEqual("", HuoshanEmotionPolicy.ToRequestEmotion("general"), "Huoshan policy omits general from request emotion");
AssertEqual("", HuoshanEmotionPolicy.ToRequestEmotion("neutral"), "Huoshan policy omits neutral from request emotion");

var onlineVoiceWithoutEmotion = new VoiceOption
{
    Id = "zh_female_wenroumama_uranus_bigtts",
    Name = "温柔妈妈 2.0",
    IsBigTTS = true,
    SampleUrl = "https://example.com/sample.mp3",
    Categories = new List<string> { "seed-tts-2.0" }
};
AssertFalse(HuoshanEmotionPolicy.GetOptions(onlineVoiceWithoutEmotion).Any(), "Huoshan policy does not guess emotions for fetched voices without provider emotion metadata");

var builtInFallbackVoice = new VoiceOption
{
    Id = "zh_female_wenrouxiaoya_moon_bigtts",
    Name = "温柔小雅",
    IsBigTTS = true
};
AssertTrue(HuoshanEmotionPolicy.GetOptions(builtInFallbackVoice).Any(e => e.Id == "happy"), "Huoshan policy keeps fallback options for built-in offline voices");

var catalogMultiEmotionOnlyVoice = new VoiceOption
{
    Id = "zh_male_jingqiangkanye_moon_bigtts",
    Name = "京腔侃爷",
    IsBigTTS = true,
    SampleUrl = "https://example.com/jingqiang.mp3",
    Categories = new List<string> { "seed-tts-1.0", "多情感" },
    Emotions = new List<EmotionInfo>
    {
        new() { Emotion = "通用", EmotionType = "general" }
    }
};
AssertFalse(HuoshanEmotionPolicy.HasSelectableEmotionControls(catalogMultiEmotionOnlyVoice), "Huoshan policy does not treat a catalog 多情感 tag as selectable emotion controls");
AssertFalse(HuoshanEmotionPolicy.MatchesCategory(catalogMultiEmotionOnlyVoice, "多情感"), "Huoshan 多情感 filter only matches voices with real selectable emotion controls");
var catalogOnlyTags = HuoshanEmotionPolicy.GetDisplayTags(catalogMultiEmotionOnlyVoice).ToList();
AssertFalse(catalogOnlyTags.Contains("多情感"), "Huoshan display tags hide catalog 多情感 when no alternative emotion is declared");
AssertFalse(catalogOnlyTags.Any(t => t.Contains("语气")), "Huoshan display tags do not show an emotion count for general-only voices");

var realMultiEmotionVoice = new VoiceOption
{
    Id = "zh_male_jingqiangkanye_emo_mars_bigtts",
    Name = "京腔侃爷",
    IsBigTTS = true,
    SampleUrl = "https://example.com/jingqiang.mp3",
    Categories = new List<string> { "seed-tts-1.0", "多情感" },
    Emotions = new List<EmotionInfo>
    {
        new() { Emotion = "通用", EmotionType = "general" },
        new() { Emotion = "开心", EmotionType = "happy" },
        new() { Emotion = "生气", EmotionType = "angry" },
        new() { Emotion = "惊讶", EmotionType = "surprised" },
        new() { Emotion = "厌恶", EmotionType = "hate" },
        new() { Emotion = "中性", EmotionType = "neutral" }
    }
};
AssertTrue(HuoshanEmotionPolicy.HasSelectableEmotionControls(realMultiEmotionVoice), "Huoshan policy exposes controls only when alternative emotions are declared");
AssertTrue(HuoshanEmotionPolicy.MatchesCategory(realMultiEmotionVoice, "多情感"), "Huoshan 多情感 filter includes voices with provider-declared alternatives");
var realEmotionTags = HuoshanEmotionPolicy.GetDisplayTags(realMultiEmotionVoice).ToList();
AssertFalse(realEmotionTags.Contains("多情感"), "Huoshan display tags avoid showing catalog 多情感 as if it were a control");
AssertTrue(realEmotionTags.Contains("6 种语气"), "Huoshan display tags show the real provider-declared emotion count");

Console.WriteLine("Huoshan emotion policy tests passed.");

var azurePlainRequest = new TtsRequest
{
    VendorId = "azure",
    ModelId = "neural",
    VoiceId = "zh-CN-XiaoxiaoNeural",
    Text = "你好 <朋友>",
    Speed = 1.2,
    Volume = 80,
    InputFormat = TtsInputFormat.PlainText,
    Style = "cheerful",
    StyleDegree = 1.5
};
var azureSsml = AzureSsmlBuilder.Build(azurePlainRequest);
AssertTrue(azureSsml.Contains("xmlns:mstts=\"https://www.w3.org/2001/mstts\""), "Azure SSML declares mstts namespace");
AssertTrue(azureSsml.Contains("voice name=\"zh-CN-XiaoxiaoNeural\""), "Azure SSML uses selected voice");
AssertTrue(azureSsml.Contains("style=\"cheerful\""), "Azure SSML includes selected style");
AssertTrue(azureSsml.Contains("styledegree=\"1.5\""), "Azure SSML includes selected style degree");
AssertTrue(azureSsml.Contains("你好 &lt;朋友&gt;"), "Azure SSML escapes plain text");

var rawSsmlRequest = new TtsRequest
{
    VendorId = "azure",
    VoiceId = "zh-CN-XiaoxiaoNeural",
    Text = "ignored",
    InputFormat = TtsInputFormat.Ssml,
    SsmlText = "<speak version=\"1.0\">raw</speak>"
};
AssertEqual("<speak version=\"1.0\">raw</speak>", AzureSsmlBuilder.Build(rawSsmlRequest), "Azure raw SSML mode preserves user markup");

Console.WriteLine("Azure SSML builder tests passed.");

var azureVoices = AzureTtsProvider.ParseVoicesJson("""
[
  {
    "Name": "Microsoft Server Speech Text to Speech Voice (zh-CN, XiaoxiaoNeural)",
    "DisplayName": "Xiaoxiao",
    "LocalName": "晓晓",
    "ShortName": "zh-CN-XiaoxiaoNeural",
    "Gender": "Female",
    "Locale": "zh-CN",
    "LocaleName": "Chinese (Mandarin, Simplified)",
    "VoiceType": "Neural",
    "StyleList": ["cheerful", "sad"]
  },
  {
    "DisplayName": "Jenny",
    "ShortName": "en-US-JennyNeural",
    "Gender": "Female",
    "Locale": "en-US",
    "VoiceType": "Neural"
  }
]
""");
AssertEqual(2, azureVoices.Count, "Azure voice parser returns all voices");
AssertEqual("zh-CN-XiaoxiaoNeural", azureVoices[0].Id, "Azure voice parser maps ShortName to id");
AssertEqual("晓晓 (zh-CN)", azureVoices[0].Name, "Azure voice parser prefers LocalName and includes locale");
AssertEqual("女", azureVoices[0].Gender, "Azure voice parser localizes female gender");
AssertEqual("zh-CN", azureVoices[0].Language, "Azure voice parser maps locale to language");
AssertTrue(azureVoices[0].Categories.Contains("Neural"), "Azure voice parser includes voice type category");
AssertTrue(azureVoices[0].Categories.Contains("style:cheerful"), "Azure voice parser preserves style metadata as categories");
var azureStyleOptions = AzureStylePolicy.GetOptions(azureVoices[0]).ToList();
AssertTrue(azureStyleOptions.Any(style => style.Id == "cheerful" && style.Name == "愉快"), "Azure style policy exposes voice-supported cheerful style");
AssertTrue(azureStyleOptions.Any(style => style.Id == "sad" && style.Name == "悲伤"), "Azure style policy exposes voice-supported sad style");
AssertFalse(azureStyleOptions.Any(style => style.Id == "angry"), "Azure style policy hides unsupported styles for selected voice");

Console.WriteLine("Azure voice parser tests passed.");

var huoshanProvider = new HuoshanTtsProvider(new HttpClient(), new SettingsService());
var noNetworkConnectivity = await huoshanProvider.TestConnectivityAsync("app-123|token-abc");
AssertTrue(noNetworkConnectivity.Success, "Huoshan provider accepts basic speech credentials without OpenAPI call");
AssertTrue(noNetworkConnectivity.Message.Contains("未配置 AK/SK"), "Huoshan provider preserves the no-quota connectivity message");

var huoshanOpenApiFailure = new HuoshanTtsProvider(
    new HttpClient(new StaticResponseHandler(
        System.Net.HttpStatusCode.Forbidden,
        "{\"ResponseMetadata\":{\"Error\":{\"Code\":\"AccessDenied\",\"Message\":\"missing speech permission\"}}}")),
    new SettingsService());
var openApiFailure = await huoshanOpenApiFailure.TestConnectivityAsync("app-123|token-abc||ak-1|sk-2");
AssertFalse(openApiFailure.Success, "Huoshan provider reports failed OpenAPI validation");
AssertTrue(openApiFailure.Message.Contains("Forbidden"), "Huoshan OpenAPI failure includes HTTP status");
AssertTrue(openApiFailure.Message.Contains("AccessDenied"), "Huoshan OpenAPI failure includes provider error code");

Console.WriteLine("Huoshan provider boundary tests passed.");

var copiedAliyunData = Path.Combine(AppContext.BaseDirectory, "aliyun_voices_raw.json");
AssertTrue(File.Exists(copiedAliyunData), "Aliyun voice data is copied to output root for runtime loading");

Console.WriteLine("Runtime asset copy tests passed.");

var aliyunProvider = new AliyunTtsProvider(new HttpClient(), new SettingsService());
var aliyunVoices = await aliyunProvider.FetchVoicesAsync();
AssertTrue(aliyunVoices.Count > 0, "Aliyun provider loads built-in voice data");
AssertTrue(aliyunVoices.Any(v => !string.IsNullOrWhiteSpace(v.Id) && !string.IsNullOrWhiteSpace(v.Name)), "Aliyun provider returns usable voice ids and names");

Console.WriteLine("Aliyun provider boundary tests passed.");

var aliyunInstructionJson = AliyunTtsProvider.BuildGenerateRequestJson(new TtsRequest
{
    VendorId = "aliyun",
    ModelId = "qwen3-tts-instruct-flash-2026-01-26",
    VoiceId = "Cherry",
    Text = "欢迎使用 VoiceOps。",
    Instructions = "用亲切的客服语气朗读。"
});
using (var aliyunInstructionDoc = JsonDocument.Parse(aliyunInstructionJson))
{
    AssertTrue(
        TryGetNested(aliyunInstructionDoc.RootElement, out var instructions, "parameters", "instructions"),
        "Aliyun instruct request includes parameters.instructions");
    AssertEqual("用亲切的客服语气朗读。", instructions.GetString(), "Aliyun instruct request preserves instructions");
}

var aliyunLegacyJson = AliyunTtsProvider.BuildGenerateRequestJson(new TtsRequest
{
    VendorId = "aliyun",
    ModelId = "cosyvoice-v1",
    VoiceId = "longxiaochun",
    Text = "欢迎使用 VoiceOps。",
    Instructions = "用亲切的客服语气朗读。"
});
using (var aliyunLegacyDoc = JsonDocument.Parse(aliyunLegacyJson))
{
    AssertFalse(
        TryGetNested(aliyunLegacyDoc.RootElement, out _, "parameters", "instructions"),
        "Aliyun non-instruct request omits unsupported instructions");
}

Console.WriteLine("Aliyun provider request body tests passed.");

var tencentProvider = new TencentTtsProvider(new HttpClient(), new SettingsService());
var invalidTencent = await tencentProvider.TestConnectivityAsync("only-secret-id");
AssertFalse(invalidTencent.Success, "Tencent provider rejects one-part credentials without network call");
AssertTrue(invalidTencent.Message.Contains("SecretId|SecretKey"), "Tencent provider explains credential format");

Console.WriteLine("Tencent provider boundary tests passed.");

var baiduProvider = new BaiduTtsProvider(new HttpClient(), new SettingsService());
var invalidBaidu = await baiduProvider.TestConnectivityAsync("only-api-key");
AssertFalse(invalidBaidu.Success, "Baidu provider rejects one-part credentials without network call");
AssertTrue(invalidBaidu.Message.Contains("api_key|secret_key"), "Baidu provider explains credential format");

var baiduUrl = BaiduTtsProvider.BuildSynthesisUrl(new TtsRequest
{
    VendorId = "baidu",
    VoiceId = "3",
    Text = "你好 百度",
    Speed = 6,
    Volume = 9
}, "token-123");
AssertTrue(baiduUrl.StartsWith("https://tsn.baidu.com/text2audio?"), "Baidu synthesis URL uses text2audio endpoint");
AssertTrue(baiduUrl.Contains("tex=%E4%BD%A0%E5%A5%BD%20%E7%99%BE%E5%BA%A6"), "Baidu synthesis URL encodes text");
AssertTrue(baiduUrl.Contains("tok=token-123"), "Baidu synthesis URL includes access token");
AssertTrue(baiduUrl.Contains("spd=6"), "Baidu synthesis URL maps speed");
AssertTrue(baiduUrl.Contains("vol=9"), "Baidu synthesis URL maps volume");
AssertTrue(baiduUrl.Contains("per=3"), "Baidu synthesis URL maps voice id");

Console.WriteLine("Baidu provider boundary tests passed.");

var openAiInstructionJson = OpenAiTtsProvider.BuildSpeechRequestJson(new TtsRequest
{
    VendorId = "openai",
    ModelId = "gpt-4o-mini-tts",
    VoiceId = "nova",
    Text = "欢迎使用 VoiceOps。",
    Speed = 1.0,
    Instructions = "用温柔但专业的语气朗读。"
});
using (var openAiInstructionDoc = JsonDocument.Parse(openAiInstructionJson))
{
    AssertTrue(
        TryGetNested(openAiInstructionDoc.RootElement, out var instructions, "instructions"),
        "OpenAI supported TTS model request includes instructions");
    AssertEqual("用温柔但专业的语气朗读。", instructions.GetString(), "OpenAI request preserves reading instructions");
}

var openAiLegacyJson = OpenAiTtsProvider.BuildSpeechRequestJson(new TtsRequest
{
    VendorId = "openai",
    ModelId = "tts-1",
    VoiceId = "nova",
    Text = "欢迎使用 VoiceOps。",
    Speed = 1.0,
    Instructions = "用温柔但专业的语气朗读。"
});
using (var openAiLegacyDoc = JsonDocument.Parse(openAiLegacyJson))
{
    AssertFalse(
        TryGetNested(openAiLegacyDoc.RootElement, out _, "instructions"),
        "OpenAI legacy TTS request omits unsupported instructions");
}

Console.WriteLine("OpenAI provider request body tests passed.");

var googlePlainJson = GoogleTtsProvider.BuildSynthesizeRequestJson(new TtsRequest
{
    VendorId = "google",
    VoiceId = "cmn-CN-Wavenet-A",
    Text = "你好，Google。",
    Speed = 1.1,
    Volume = 2,
    InputFormat = TtsInputFormat.PlainText
});
using (var googlePlainDoc = JsonDocument.Parse(googlePlainJson))
{
    AssertTrue(
        TryGetNested(googlePlainDoc.RootElement, out var googleTextInput, "input", "text"),
        "Google plain text request sends input.text");
    AssertEqual("你好，Google。", googleTextInput.GetString(), "Google plain text request preserves text");
    AssertFalse(
        TryGetNested(googlePlainDoc.RootElement, out _, "input", "ssml"),
        "Google plain text request does not send input.ssml");
}

var googleSsmlJson = GoogleTtsProvider.BuildSynthesizeRequestJson(new TtsRequest
{
    VendorId = "google",
    VoiceId = "en-US-Wavenet-D",
    Text = "ignored",
    Speed = 1,
    Volume = 0,
    InputFormat = TtsInputFormat.Ssml,
    SsmlText = "<speak>Hello from Google.</speak>"
});
using (var googleSsmlDoc = JsonDocument.Parse(googleSsmlJson))
{
    AssertTrue(
        TryGetNested(googleSsmlDoc.RootElement, out var googleSsmlInput, "input", "ssml"),
        "Google SSML request sends input.ssml");
    AssertEqual("<speak>Hello from Google.</speak>", googleSsmlInput.GetString(), "Google SSML request preserves SSML markup");
    AssertFalse(
        TryGetNested(googleSsmlDoc.RootElement, out _, "input", "text"),
        "Google SSML request does not send input.text");
}

Console.WriteLine("Google provider request body tests passed.");

var googleVoices = GoogleTtsProvider.ParseVoicesJson("""
{
  "voices": [
    {
      "languageCodes": ["en-US"],
      "name": "en-US-Wavenet-D",
      "ssmlGender": "MALE",
      "naturalSampleRateHertz": 24000
    },
    {
      "languageCodes": ["cmn-CN"],
      "name": "cmn-CN-Wavenet-A",
      "ssmlGender": "FEMALE",
      "naturalSampleRateHertz": 24000
    }
  ]
}
""");
AssertEqual(2, googleVoices.Count, "Google voice parser returns all voices");
AssertEqual("en-US-Wavenet-D", googleVoices[0].Id, "Google voice parser maps name to id");
AssertEqual("en-US-Wavenet-D (en-US)", googleVoices[0].Name, "Google voice parser includes locale in display name");
AssertEqual("男", googleVoices[0].Gender, "Google voice parser localizes male gender");
AssertEqual("en-US", googleVoices[0].Language, "Google voice parser maps language code");

Console.WriteLine("Google voice parser tests passed.");

var huoshanVendor = VendorRegistry.GetById("huoshan") ?? throw new Exception("Huoshan vendor config is missing");
AssertTrue(
    huoshanVendor.ImportantLinks.Any(link =>
        link.Label == "凭证参数获取" &&
        link.Url.StartsWith("https://www.volcengine.com/docs/6561/196768?lang=zh#q1", StringComparison.Ordinal)),
    "Huoshan vendor exposes the official credential parameter guide as an important link");
AssertTrue(
    huoshanVendor.ImportantLinks.Any(link =>
        link.Label == "TTS 模型实验室" &&
        link.Url == "https://console.volcengine.com/ark/region:ark+cn-beijing/experience/voice?modelId=doubao-seed-tts-2-0&tab=TTS"),
    "Huoshan vendor exposes the Ark TTS model lab as an important link");
AssertTrue(
    huoshanVendor.ImportantLinks.Any(link =>
        link.Label == "豆包语音体验" &&
        link.Url == "https://console.volcengine.com/speech/new/overview?projectName=default"),
    "Huoshan vendor exposes the Doubao speech experience entry as an important link");

Console.WriteLine("Vendor important link registry tests passed.");

var azureVendor = VendorRegistry.GetById("azure") ?? throw new Exception("Azure vendor config is missing");
AssertTrue(azureVendor.Capabilities.SupportsSsml, "Azure vendor declares SSML support");
AssertTrue(azureVendor.Capabilities.SupportsStyle, "Azure vendor declares speaking style support");
AssertTrue(azureVendor.Capabilities.SupportsStyleDegree, "Azure vendor declares style degree support");
AssertTrue(azureVendor.Capabilities.SupportedInputFormats.Contains(TtsInputFormat.Ssml), "Azure vendor exposes SSML as a supported input format");

AssertTrue(huoshanVendor.Capabilities.SupportsEmotion, "Huoshan vendor declares emotion controls");
AssertFalse(huoshanVendor.Capabilities.SupportsSsml, "Huoshan vendor does not declare generic SSML support");

var googleVendor = VendorRegistry.GetById("google") ?? throw new Exception("Google vendor config is missing");
AssertTrue(googleVendor.Capabilities.SupportsSsml, "Google vendor declares SSML support");
AssertTrue(googleVendor.Capabilities.SupportedInputFormats.Contains(TtsInputFormat.Ssml), "Google vendor exposes SSML as a supported input format");

var openAiVendor = VendorRegistry.GetById("openai") ?? throw new Exception("OpenAI vendor config is missing");
AssertTrue(openAiVendor.Capabilities.SupportsInstructions, "OpenAI vendor declares reading instructions support");

Console.WriteLine("Vendor capabilities registry tests passed.");

var settingsRazorPath = Path.Combine(FindRepositoryRoot(AppContext.BaseDirectory), "Components", "Pages", "Settings.razor");
var settingsMarkup = await File.ReadAllTextAsync(settingsRazorPath);
AssertTrue(settingsMarkup.Contains("credential-field-label"), "Settings credential inputs have persistent labels");
AssertTrue(settingsMarkup.Contains("生成语音必填"), "Settings explains required speech generation credentials");
AssertTrue(settingsMarkup.Contains("仅刷新全量音色库"), "Settings explains voice library refresh credentials");
AssertTrue(settingsMarkup.Contains("API Key / 凭证"), "Settings generic credential input has a visible label");
AssertTrue(settingsMarkup.Contains("关键链接"), "Settings exposes the vendor important links section");
AssertTrue(settingsMarkup.Contains("vendor.ImportantLinks"), "Settings renders links from the shared vendor important link registry");

var workspaceRazorPath = Path.Combine(FindRepositoryRoot(AppContext.BaseDirectory), "Components", "Pages", "Workspace.razor");
var workspaceMarkup = await File.ReadAllTextAsync(workspaceRazorPath);
AssertTrue(workspaceMarkup.Contains("_vendor.ImportantLinks"), "Workspace renders links from the shared vendor important link registry");
AssertTrue(workspaceMarkup.Contains("VendorCapabilities"), "Workspace reads the shared vendor capabilities");
AssertFalse(workspaceMarkup.Contains("private bool SupportsExpressionControls => IsAzure || IsHuoshan"), "Workspace expression panel is not gated by a hard-coded vendor pair");
AssertTrue(workspaceMarkup.Contains("Google 使用标准 SSML"), "Workspace gives Google-specific SSML guidance");
AssertTrue(workspaceMarkup.Contains("OpenAI 使用朗读指导"), "Workspace gives OpenAI-specific instruction guidance");

var appCssPath = Path.Combine(FindRepositoryRoot(AppContext.BaseDirectory), "wwwroot", "css", "app.css");
var appCss = await File.ReadAllTextAsync(appCssPath);
AssertTrue(appCss.Contains(".credential-row-input") && appCss.Contains("flex-wrap: wrap"), "Settings credential row can wrap long test feedback without squeezing labels");
AssertTrue(appCss.Contains(".credential-test-result") && appCss.Contains("overflow-wrap: anywhere"), "Settings test feedback breaks very long provider messages");
AssertTrue(appCss.Contains(".credential-test-result") && appCss.Contains("white-space: normal"), "Settings test feedback keeps long messages in normal wrapping flow");

Console.WriteLine("Settings credential UX markup tests passed.");

static string FindRepositoryRoot(string startDirectory)
{
    var dir = new DirectoryInfo(startDirectory);
    while (dir != null)
    {
        if (File.Exists(Path.Combine(dir.FullName, "VoiceServiceDemo.csproj")))
            return dir.FullName;
        dir = dir.Parent;
    }

    throw new DirectoryNotFoundException("Could not locate VoiceServiceDemo.csproj from " + startDirectory);
}

internal sealed class StaticResponseHandler : HttpMessageHandler
{
    private readonly System.Net.HttpStatusCode _statusCode;
    private readonly string _content;

    public StaticResponseHandler(System.Net.HttpStatusCode statusCode, string content)
    {
        _statusCode = statusCode;
        _content = content;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new HttpResponseMessage(_statusCode)
        {
            Content = new StringContent(_content, System.Text.Encoding.UTF8, "application/json")
        });
    }
}
