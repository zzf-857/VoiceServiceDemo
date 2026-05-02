using VoiceService.Shared;
using VoiceServiceDemo.Services;
using VoiceServiceDemo.Services.Providers;

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

var sseAudioEvent = HuoshanTtsProtocol.ParseV3StreamLine("data: {\"code\":0,\"data\":\"AQID\"}");
AssertTrue(sseAudioEvent.HasAudio, "SSE data line is parsed as audio");
AssertEqual(3, sseAudioEvent.AudioBytes.Length, "base64 audio payload is decoded");

var terminalEvent = HuoshanTtsProtocol.ParseV3StreamLine("{\"code\":20000000,\"message\":\"ok\"}");
AssertTrue(terminalEvent.IsTerminal, "terminal JSON line is recognized");

var errorEvent = HuoshanTtsProtocol.ParseV3StreamLine("data: {\"code\":45000000,\"message\":\"speaker permission denied\"}");
AssertTrue(errorEvent.IsError, "non-success V3 code is parsed as error");
AssertTrue(errorEvent.ErrorMessage.Contains("speaker permission denied"), "V3 error message is preserved");

Console.WriteLine("Shared Huoshan protocol tests passed.");

var huoshanProvider = new HuoshanTtsProvider(new HttpClient(), new SettingsService());
var noNetworkConnectivity = await huoshanProvider.TestConnectivityAsync("app-123|token-abc");
AssertTrue(noNetworkConnectivity.Success, "Huoshan provider accepts basic speech credentials without OpenAPI call");
AssertTrue(noNetworkConnectivity.Message.Contains("未配置 AK/SK"), "Huoshan provider preserves the no-quota connectivity message");

Console.WriteLine("Huoshan provider boundary tests passed.");

var copiedAliyunData = Path.Combine(AppContext.BaseDirectory, "aliyun_voices_raw.json");
AssertTrue(File.Exists(copiedAliyunData), "Aliyun voice data is copied to output root for runtime loading");

Console.WriteLine("Runtime asset copy tests passed.");

var aliyunProvider = new AliyunTtsProvider(new HttpClient(), new SettingsService());
var aliyunVoices = await aliyunProvider.FetchVoicesAsync();
AssertTrue(aliyunVoices.Count > 0, "Aliyun provider loads built-in voice data");
AssertTrue(aliyunVoices.Any(v => !string.IsNullOrWhiteSpace(v.Id) && !string.IsNullOrWhiteSpace(v.Name)), "Aliyun provider returns usable voice ids and names");

Console.WriteLine("Aliyun provider boundary tests passed.");

var tencentProvider = new TencentTtsProvider(new HttpClient(), new SettingsService());
var invalidTencent = await tencentProvider.TestConnectivityAsync("only-secret-id");
AssertFalse(invalidTencent.Success, "Tencent provider rejects one-part credentials without network call");
AssertTrue(invalidTencent.Message.Contains("SecretId|SecretKey"), "Tencent provider explains credential format");

Console.WriteLine("Tencent provider boundary tests passed.");
