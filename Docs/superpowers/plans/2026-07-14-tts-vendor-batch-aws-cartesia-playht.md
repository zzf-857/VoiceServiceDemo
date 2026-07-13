# Amazon Polly、Cartesia 与 PlayHT TTS 厂商批次实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在桌面端及其本地 REST API 中新增 Cartesia、PlayHT、Amazon Polly 三家可生成、可刷新音色且有完整 fake HTTP 回归的 TTS Provider。

**Architecture:** 每家实现独立 `ITtsProvider`/`IVoiceCatalogProvider`，注入现有 `HttpClient` 与 `SettingsService`，使用 `AudioOutputPath.Reserve` 写文件，再注册到 `TtsProviderRegistry` 与 `VendorRegistry`。本地 API 继续通过 `DesktopTtsGateway` 自动读取注册表，不新增厂商专用 HTTP 路由；AWS SigV4 单独放入可测试 helper。

**Tech Stack:** .NET 8、WPF/BlazorWebView、`HttpClient`、`System.Text.Json`、AWS Signature V4、控制台自检、xUnit Local API 集成测试。

---

## 文件结构

- `Services/Providers/CartesiaTtsProvider.cs`：Cartesia 鉴权、bytes 生成、音色列表解析。
- `Services/Providers/PlayHtTtsProvider.cs`：PlayHT 双凭证、stream 生成、预置音色解析。
- `Helpers/AwsSignatureV4.cs`：通用 SigV4 canonical request 与签名头。
- `Services/Providers/AmazonPollyTtsProvider.cs`：Polly 凭证、SynthesizeSpeech、DescribeVoices 分页。
- `Services/TtsService.cs`：把三家 Provider 加入统一注册表。
- `Services/VendorRegistry.cs`：声明三家模型、音色、参数、格式、能力和链接。
- `Components/Pages/Settings.razor`：凭证格式说明；本地 API 厂商数改为动态。
- `Components/Pages/Home.razor`：三家品牌卡片图标。
- `Components/Pages/Workspace.razor`：Cartesia 通用 emotion 控件和提示。
- `wwwroot/assets/vendor-icons/*.svg`：本地品牌标记。
- `VoiceServiceDemo.Tests/Program.cs`：逐家 RED/GREEN 请求、解析、落盘、注册与 UI 标记。
- `README.md`：逐家同步当前厂商数量和凭证格式。
- `Docs/project/TTS_TOOL_GAP_CHECKLIST_2026-05-02.md`：逐家记录测试证据和未覆盖高级能力。

---

### Task 1: Cartesia bytes TTS 与在线音色

**Files:**

- Create: `Services/Providers/CartesiaTtsProvider.cs`
- Create: `wwwroot/assets/vendor-icons/cartesia.svg`
- Modify: `Services/TtsService.cs`
- Modify: `Services/VendorRegistry.cs`
- Modify: `Components/Pages/Settings.razor`
- Modify: `Components/Pages/Home.razor`
- Modify: `Components/Pages/Workspace.razor`
- Modify: `VoiceServiceDemo.Tests/Program.cs`
- Modify: `README.md`
- Modify: `Docs/project/TTS_TOOL_GAP_CHECKLIST_2026-05-02.md`

- [ ] **Step 1: 写 Cartesia RED 请求与解析测试**

在 `VoiceServiceDemo.Tests/Program.cs` 添加固定 handler，捕获请求并返回音频：

```csharp
var cartesiaHandler = new RecordingQueueHandler(new HttpResponseMessage(HttpStatusCode.OK)
{
    Content = new ByteArrayContent(new byte[] { 0x49, 0x44, 0x33, 0x04 })
});
var cartesiaProvider = new CartesiaTtsProvider(new HttpClient(cartesiaHandler), providerRegistrySettings);
var cartesiaResult = await cartesiaProvider.GenerateAsync(
    new TtsRequest
    {
        VendorId = "cartesia",
        ModelId = "sonic-3.5",
        VoiceId = "db6b0ed5-d5d3-463d-ae85-518a07d3c2b4",
        Text = "hello",
        Speed = 1.2,
        Volume = 0.8,
        Emotion = "happy",
        OutputFormat = "mp3"
    },
    "test-cartesia-key");
AssertTrue(cartesiaResult.Success, "Cartesia fake HTTP generation succeeds");
AssertTrue(cartesiaHandler.RequestAuthorizationHeaders.Single().StartsWith("Bearer "), "Cartesia uses Bearer auth");
AssertEqual("2026-03-01", cartesiaHandler.RequestHeaderValues.Single()["Cartesia-Version"], "Cartesia pins API version");
AssertTrue(cartesiaHandler.RequestBodies.Single().Contains("\"generation_config\""), "Cartesia sends generation config");
AssertTrue(cartesiaHandler.RequestBodies.Single().Contains("\"emotion\":\"happy\""), "Cartesia sends emotion");
AssertTrue((await File.ReadAllBytesAsync(cartesiaResult.FilePath!)).SequenceEqual(new byte[] { 0x49, 0x44, 0x33, 0x04 }), "Cartesia writes exact audio bytes");
```

添加音色解析断言：

```csharp
var cartesiaVoices = CartesiaTtsProvider.ParseVoices("""
{"data":[{"id":"voice-1","name":"Skylar","language":"en","gender":"female","description":"Friendly guide"}]}
""");
AssertEqual("voice-1", cartesiaVoices.Single().Id, "Cartesia parses voice id");
AssertEqual("Skylar", cartesiaVoices.Single().Name, "Cartesia parses voice name");
```

- [ ] **Step 2: 运行 RED 并确认缺少 Provider**

Run:

```powershell
dotnet run --project VoiceServiceDemo.Tests/VoiceServiceDemo.Tests.csproj --no-restore
```

Expected: `CS0246`，找不到 `CartesiaTtsProvider`。

- [ ] **Step 3: 实现 Cartesia Provider**

Provider 必须包含：

```csharp
public sealed class CartesiaTtsProvider : ITtsProvider, IVoiceCatalogProvider
{
    public string VendorId => "cartesia";
    public const string ApiVersion = "2026-03-01";

    public Task<(bool Success, string Message)> TestConnectivityAsync(
        string apiKey,
        CancellationToken cancellationToken = default);

    public Task<List<VoiceOption>> FetchVoicesAsync(
        string apiKey,
        CancellationToken cancellationToken = default);

    public Task<TtsResult> GenerateAsync(
        TtsRequest request,
        string apiKey,
        CancellationToken cancellationToken = default);

    public static List<VoiceOption> ParseVoices(string json);
}
```

请求体使用 `JsonSerializer.SerializeToUtf8Bytes`，MP3 映射 44100/128000，WAV 映射 `pcm_s16le`/44100。只在成功且非空响应后保留预留文件；异常删除空文件并返回安全错误。

- [ ] **Step 4: 注册能力与桌面 UI**

在 `VendorRegistry` 添加：

```csharp
new VendorConfig
{
    Id = "cartesia",
    Name = "Cartesia",
    ApiBaseUrl = "https://api.cartesia.ai",
    DocumentationUrl = "https://docs.cartesia.ai/api-reference/tts/bytes",
    SupportsVoiceFetch = true,
    SpeedDef = new TtsParameterDef { IsSupported = true, Min = 0.6, Max = 1.5, Default = 1.0, Step = 0.1 },
    VolumeDef = new TtsParameterDef { IsSupported = true, Min = 0.5, Max = 2.0, Default = 1.0, Step = 0.1 },
    Capabilities = new VendorCapabilities
    {
        SupportedInputFormats = new() { TtsInputFormat.PlainText, TtsInputFormat.Ssml },
        SupportedOutputFormats = new() { "mp3", "wav" },
        SupportsSsml = true,
        SupportsEmotion = true
    }
}
```

默认模型为 `sonic-3.5`，默认音色包含官方示例 Skylar。`Workspace.razor` 新增 Cartesia emotion 列表并让 `GetRequestEmotion()` 返回所选值。本地 API 设置文案的厂商数改为 `@VendorRegistry.All.Count`，自检对 README 数量使用动态断言。

- [ ] **Step 5: GREEN 与全量验证**

Run:

```powershell
dotnet run --project VoiceServiceDemo.Tests/VoiceServiceDemo.Tests.csproj --no-restore
dotnet test VoiceServiceLocalApi.Tests/VoiceServiceLocalApi.Tests.csproj --no-restore
dotnet build VoiceServiceDemo.slnx --no-restore
git diff --check
```

Expected: 桌面全部自检通过；Local API 35/35；构建 0 警告、0 错误。

- [ ] **Step 6: 更新 TODO、中文提交并推送**

记录 bytes 请求、版本头、音色刷新、模型/格式、fake 音频证据，并明确暂不含 WebSocket/SSE/克隆/词典。

```powershell
git add Services/Providers/CartesiaTtsProvider.cs Services/TtsService.cs Services/VendorRegistry.cs Components/Pages/Settings.razor Components/Pages/Home.razor Components/Pages/Workspace.razor wwwroot/assets/vendor-icons/cartesia.svg VoiceServiceDemo.Tests/Program.cs README.md Docs/project/TTS_TOOL_GAP_CHECKLIST_2026-05-02.md
git commit -m "接入：支持Cartesia TTS生成与音色刷新"
git push origin codex/tts-vendor-batch
```

---

### Task 2: PlayHT HTTP stream 与预置音色

**Files:**

- Create: `Services/Providers/PlayHtTtsProvider.cs`
- Create: `wwwroot/assets/vendor-icons/playht.svg`
- Modify: `Services/TtsService.cs`
- Modify: `Services/VendorRegistry.cs`
- Modify: `Components/Pages/Settings.razor`
- Modify: `Components/Pages/Home.razor`
- Modify: `VoiceServiceDemo.Tests/Program.cs`
- Modify: `README.md`
- Modify: `Docs/project/TTS_TOOL_GAP_CHECKLIST_2026-05-02.md`

- [ ] **Step 1: 写 PlayHT RED 双凭证、请求与音色测试**

```csharp
var playHtCredentials = PlayHtCredentials.Parse("user-123|key-456");
AssertEqual("user-123", playHtCredentials.UserId, "PlayHT parses user id");
AssertEqual("key-456", playHtCredentials.ApiKey, "PlayHT parses API key");
AssertThrows<ArgumentException>(() => PlayHtCredentials.Parse("only-one-part"), "PlayHT rejects incomplete credentials");

var playHtProvider = new PlayHtTtsProvider(new HttpClient(playHtHandler), providerRegistrySettings);
var playHtResult = await playHtProvider.GenerateAsync(
    new TtsRequest
    {
        VendorId = "playht",
        ModelId = "Play3.0-mini",
        VoiceId = "larry",
        Text = "hello",
        Speed = 1.1,
        OutputFormat = "ogg"
    },
    "user-123|key-456");
AssertEqual("user-123", playHtHandler.RequestHeaderValues.Single()["X-USER-ID"], "PlayHT sends user id header");
AssertTrue(playHtHandler.RequestAuthorizationHeaders.Single().StartsWith("Bearer "), "PlayHT uses Bearer auth");
AssertTrue(playHtHandler.RequestBodies.Single().Contains("\"voice_engine\":\"Play3.0-mini\""), "PlayHT sends engine");
AssertTrue(playHtResult.FilePath!.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase), "PlayHT keeps Ogg extension");
```

音色 JSON 同时覆盖 `voiceId` 与旧 `value` fallback。

- [ ] **Step 2: 运行 RED**

Run: `dotnet run --project VoiceServiceDemo.Tests/VoiceServiceDemo.Tests.csproj --no-restore`

Expected: `CS0246`，缺少 `PlayHtCredentials`/`PlayHtTtsProvider`。

- [ ] **Step 3: 实现 Provider 与注册表**

实现：

```csharp
public sealed record PlayHtCredentials(string UserId, string ApiKey)
{
    public static PlayHtCredentials Parse(string value);
}

public sealed class PlayHtTtsProvider : ITtsProvider, IVoiceCatalogProvider
{
    public string VendorId => "playht";
    public static List<VoiceOption> ParseVoices(string json);
}
```

固定 endpoint `/api/v2/tts/stream` 与 `/api/v2/voices`。支持 `Play3.0-mini`、`PlayDialog`、`PlayDialog-turbo`、`PlayHT2.0`，输出 `mp3/wav/ogg/flac/mulaw`，速度 `0.1..5.0`，音量不支持。错误凭证在网络前失败。

- [ ] **Step 4: 设置页、首页与 README**

设置页文案明确 `PLAYHT_USER_ID|PLAYHT_API_KEY` 两段格式；首页添加本地 SVG；README 厂商数和凭证表变为当前 14 家。

- [ ] **Step 5: 全量验证**

运行 Task 1 相同四条命令，要求桌面自检、35 项 Local API、0/0 构建全部通过。

- [ ] **Step 6: TODO、提交、推送**

```powershell
git add Services/Providers/PlayHtTtsProvider.cs Services/TtsService.cs Services/VendorRegistry.cs Components/Pages/Settings.razor Components/Pages/Home.razor wwwroot/assets/vendor-icons/playht.svg VoiceServiceDemo.Tests/Program.cs README.md Docs/project/TTS_TOOL_GAP_CHECKLIST_2026-05-02.md
git commit -m "接入：支持PlayHT TTS生成与音色刷新"
git push origin codex/tts-vendor-batch
```

TODO 明确本轮不含多角色、克隆、sample rate、seed、temperature 和异步批量任务。

---

### Task 3: Amazon Polly SigV4、合成与 DescribeVoices

**Files:**

- Create: `Helpers/AwsSignatureV4.cs`
- Create: `Services/Providers/AmazonPollyTtsProvider.cs`
- Create: `wwwroot/assets/vendor-icons/aws_polly.svg`
- Modify: `Services/TtsService.cs`
- Modify: `Services/VendorRegistry.cs`
- Modify: `Components/Pages/Settings.razor`
- Modify: `Components/Pages/Home.razor`
- Modify: `VoiceServiceDemo.Tests/Program.cs`
- Modify: `README.md`
- Modify: `Docs/project/TTS_TOOL_GAP_CHECKLIST_2026-05-02.md`

- [ ] **Step 1: 写 AWS 凭证与 SigV4 RED 测试**

```csharp
var awsCredentials = AwsPollyCredentials.Parse("AKIDEXAMPLE|secret-example|us-east-1|session-token");
AssertEqual("us-east-1", awsCredentials.Region, "AWS parses region");
AssertTrue(awsCredentials.HasSessionToken, "AWS detects session token");
AssertThrows<ArgumentException>(() => AwsPollyCredentials.Parse("AK|SK|https://evil.example"), "AWS rejects invalid region");

var signed = AwsSignatureV4.Sign(
    HttpMethod.Post,
    new Uri("https://polly.us-east-1.amazonaws.com/v1/speech"),
    System.Text.Encoding.UTF8.GetBytes("{}"),
    awsCredentials,
    "polly",
    new DateTimeOffset(2026, 7, 14, 0, 0, 0, TimeSpan.Zero));
AssertEqual("20260714T000000Z", signed.AmzDate, "SigV4 uses fixed UTC timestamp");
AssertTrue(signed.Authorization.StartsWith("AWS4-HMAC-SHA256 Credential=AKIDEXAMPLE/20260714/us-east-1/polly/aws4_request"), "SigV4 builds credential scope");
AssertTrue(signed.SignedHeaders.Contains("x-amz-security-token"), "SigV4 signs session token");
```

再添加 query 排序测试：输入 `?NextToken=b%2B2&Engine=neural`，canonical query 必须按 `Engine`、`NextToken` 排序且保持 RFC3986 编码。

- [ ] **Step 2: 运行 RED**

Expected: `CS0103`/`CS0246`，缺少 `AwsSignatureV4` 与 `AwsPollyCredentials`。

- [ ] **Step 3: 实现 SigV4 helper**

公开测试边界：

```csharp
public sealed record AwsSignatureResult(
    string Authorization,
    string AmzDate,
    string PayloadHash,
    string SignedHeaders,
    string CanonicalRequest);

public static class AwsSignatureV4
{
    public static AwsSignatureResult Sign(
        HttpMethod method,
        Uri uri,
        ReadOnlySpan<byte> payload,
        AwsPollyCredentials credentials,
        string service,
        DateTimeOffset timestamp);
}
```

使用 `CryptographicOperations`/`HMACSHA256`，不把 secret 写入返回值、异常或日志。

- [ ] **Step 4: 写 Provider RED 请求、分页与落盘测试**

固定时间注入 Provider，捕获 `POST /v1/speech`：

```csharp
AssertEqual("polly.us-east-1.amazonaws.com", awsHandler.RequestUris.Single()!.Host, "Polly builds region endpoint");
AssertTrue(awsHandler.RequestAuthorizationHeaders.Single().StartsWith("AWS4-HMAC-SHA256 "), "Polly sends SigV4 authorization");
AssertTrue(awsHandler.RequestBodies.Single().Contains("\"Engine\":\"neural\""), "Polly sends engine");
AssertTrue(awsHandler.RequestBodies.Single().Contains("\"TextType\":\"ssml\""), "Polly sends SSML text type");
```

DescribeVoices fake 先返回 `NextToken`，再返回第二页；断言两页音色合并、第二个 URI 含编码 token、最多 20 页。

- [ ] **Step 5: 实现 Polly Provider 与 UI**

凭证：`access_key_id|secret_access_key|region[|session_token]`。生成支持 `standard/neural/long-form/generative`，输出 `mp3/ogg_vorbis/pcm`，输入 `text/ssml`，不支持统一 speed/volume。

`VoiceOption.Categories` 存 `SupportedEngines`，`Language` 用 `LanguageCode`，`Gender` 转小写。首页添加 AWS Polly 图标；设置页显示四段格式；README 厂商数更新为 15。

- [ ] **Step 6: 全量 GREEN**

运行四条标准验证命令。额外扫描：

```powershell
rg -n "secret-example|session-token|Authorization" Docs/project README.md Services/Providers/AmazonPollyTtsProvider.cs Helpers/AwsSignatureV4.cs
```

测试 fixture 可出现占位凭证；生产代码、TODO 和 README 不得回显真实或测试 Secret。

- [ ] **Step 7: TODO、提交、推送**

```powershell
git add Helpers/AwsSignatureV4.cs Services/Providers/AmazonPollyTtsProvider.cs Services/TtsService.cs Services/VendorRegistry.cs Components/Pages/Settings.razor Components/Pages/Home.razor wwwroot/assets/vendor-icons/aws_polly.svg VoiceServiceDemo.Tests/Program.cs README.md Docs/project/TTS_TOOL_GAP_CHECKLIST_2026-05-02.md
git commit -m "接入：支持Amazon Polly TTS生成与音色刷新"
git push origin codex/tts-vendor-batch
```

TODO 明确本轮不含 lexicon、speech marks、异步长文本任务 API 和区域自动发现。

---

### Task 4: 厂商批次最终验收与合并

**Files:**

- Modify only if review finds a tested defect.

- [ ] **Step 1: 逐项核对规格**

确认：15 家注册表与 Provider ID 一致；三家出现在桌面首页、设置页、工作区和 `/api/v1/vendors`；凭证不外泄；README 当前数量正确；历史 TODO 记录不被篡改。

- [ ] **Step 2: 最终全量验证**

```powershell
dotnet restore VoiceServiceDemo.slnx
dotnet test VoiceServiceLocalApi.Tests/VoiceServiceLocalApi.Tests.csproj --no-restore
dotnet run --project VoiceServiceDemo.Tests/VoiceServiceDemo.Tests.csproj --no-restore
dotnet build VoiceServiceDemo.slnx --no-restore
git diff --check
git status --short --branch
git rev-list --left-right --count HEAD...origin/codex/tts-vendor-batch
```

- [ ] **Step 3: 最终代码审查**

审查重点：签名正确性、凭证泄漏、错误响应落成音频、预留空文件清理、音色 schema 兼容、取消传播、本地 API 能力声明准确性。

- [ ] **Step 4: 快进 master 并推送**

确认原仓库只有用户既有未跟踪 OpenClaw 文件后：

```powershell
git switch master
git merge --ff-only codex/tts-vendor-batch
dotnet test VoiceServiceLocalApi.Tests/VoiceServiceLocalApi.Tests.csproj --no-restore
dotnet run --project VoiceServiceDemo.Tests/VoiceServiceDemo.Tests.csproj --no-restore
dotnet build VoiceServiceDemo.slnx --no-restore
git push origin master
```
