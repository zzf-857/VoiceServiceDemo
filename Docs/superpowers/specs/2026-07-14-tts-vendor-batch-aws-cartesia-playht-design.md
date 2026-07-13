# Amazon Polly、Cartesia 与 PlayHT TTS 厂商批次设计

## 背景与目标

VoiceOps 已有 12 家桌面 TTS Provider，并通过 `ITtsProvider`、`IVoiceCatalogProvider` 和 `TtsProviderRegistry` 统一路由。桌面内置 REST API 会从 `VendorRegistry.All` 自动发布厂商能力，因此新增桌面 Provider 后，Dify 和其他外部调用方无需新增专用路由即可使用。

本批次新增三家互补厂商：

1. **Cartesia**：面向实时语音应用的现代低延迟 TTS，提供稳定的 HTTPS bytes 与音色列表接口。
2. **PlayHT**：提供低延迟 HTTP 音频流、丰富预置/克隆音色和多输出格式。
3. **Amazon Polly**：补齐 AWS 云平台，支持标准/神经/长文本/生成式引擎、SSML 和 DescribeVoices。

每家必须独立完成：RED 测试、最小实现、全量回归、TODO 记录、中文提交并推送 GitHub。三家不共用未提交的实现批次。

## 路线比较与选择

### 方案 A：三个简单 Bearer REST 厂商

例如 Cartesia、PlayHT、IBM Watson。实现快，全部可用 `HttpMessageHandler` fake 测试，但项目已经覆盖 Azure、Google 等传统云平台，继续增加相似的简单 REST Provider 价值递减，并会留下 AWS 空缺。

### 方案 B：Amazon Polly + Cartesia + PlayHT（采用）

同时覆盖主流 AWS 云平台和两个现代低延迟语音平台。Cartesia、PlayHT 可以快速复用现有 HTTP Provider 模式；Polly 需要新增可独立测试的 SigV4 签名器，但仓库已有腾讯 TC3 签名测试经验，复杂度可控。三家都能在没有真实密钥时用固定响应完成请求、解析、落盘和错误处理回归。

### 方案 C：中国厂商优先

例如讯飞开放平台等。用户相关性高，但主流接口大量依赖 WebSocket 握手、时间签名和流式分片，现有测试基础主要围绕 `HttpClient`。本轮强行接入会把“新增厂商”扩大成 WebSocket 传输抽象重构。该路线保留为后续独立批次。

## 共同架构

每家遵循现有边界：

- Provider 位于 `Services/Providers/`，实现 `ITtsProvider`；支持在线音色时同时实现 `IVoiceCatalogProvider`。
- Provider 只负责凭证解析、HTTP/签名、厂商请求与响应、输出文件写入。
- 输出路径继续使用 `AudioOutputPath.Reserve`，避免并发覆盖。
- `TtsService` 只把新 Provider 加入 `TtsProviderRegistry`，不恢复 vendor switch。
- `VendorRegistry` 声明模型、默认音色、速度/音量范围、输入/输出格式、能力和官方链接。
- 设置页仅增加凭证格式帮助；不得把凭证写入日志或外部 API 响应。
- 首页增加本地 SVG 品牌标记；不依赖运行时网络图片。
- 本地 REST API 自动通过现有 `DesktopTtsGateway` 发布新厂商、音色与生成能力。
- 每个 Provider 的测试使用固定 `HttpMessageHandler`，不调用付费 API。

## Cartesia 设计

### 官方契约

- 生成：`POST https://api.cartesia.ai/tts/bytes`
- 音色：`GET https://api.cartesia.ai/voices`
- 鉴权：`Authorization: Bearer <token>`
- 版本头：`Cartesia-Version: 2026-03-01`
- 官方文档：
  - <https://docs.cartesia.ai/api-reference/tts/bytes>
  - <https://docs.cartesia.ai/api-reference/voices/list>

本设计按 2026-07-14 官方文档当前契约实现。模型允许 `sonic-3.5`、`sonic-3`、`sonic-latest`，默认 `sonic-3.5`。

### 凭证与请求

凭证格式为单个 Cartesia API Key。

最小请求：

```json
{
  "model_id": "sonic-3.5",
  "transcript": "Hello",
  "voice": {
    "mode": "id",
    "id": "db6b0ed5-d5d3-463d-ae85-518a07d3c2b4"
  },
  "output_format": {
    "container": "mp3",
    "sample_rate": 44100,
    "bit_rate": 128000
  },
  "generation_config": {
    "speed": 1.0,
    "volume": 1.0
  }
}
```

映射规则：

- `mp3` → MP3、44100 Hz、128 kbps。
- `wav` → WAV、`pcm_s16le`、44100 Hz。
- 语速 `0.6..1.5`，音量 `0.5..2.0`。
- `Emotion` 非空时写入 `generation_config.emotion`；不发送统一模型中的 `EmotionIntensity`。
- SSML 模式把 `SsmlText` 写入 `transcript`；普通模式使用 `Text`。

### 音色

解析 `data[]` 中的 `id`、`name`、`language`、`gender`、`description` 等可用字段为 `VoiceOption`。列表接口失败必须抛出/返回上游错误，不能伪装为空列表。

## PlayHT 设计

### 官方契约

- 生成：`POST https://api.play.ht/api/v2/tts/stream`
- 音色：`GET https://api.play.ht/api/v2/voices`
- 鉴权：
  - `Authorization: Bearer <api-key>`
  - `X-USER-ID: <user-id>`
- 官方文档：
  - <https://docs.play.ht/reference/api-generate-tts-audio-stream>
  - <https://docs.play.ht/reference/api-list-ultra-realistic-voices>

### 凭证与请求

设置页凭证格式：`user_id|api_key`。必须精确包含两个非空部分；错误格式在发请求前失败。

请求：

```json
{
  "text": "Hello",
  "voice": "larry",
  "voice_engine": "Play3.0-mini",
  "output_format": "mp3",
  "speed": 1.0
}
```

支持模型：`Play3.0-mini`、`PlayDialog`、`PlayDialog-turbo`、`PlayHT2.0`，默认 `Play3.0-mini`。支持 `mp3`、`wav`、`ogg`、`flac`、`mulaw`；语速范围按官方 `>0..5.0` 收敛为 UI 友好的 `0.1..5.0`。本轮不暴露 sample rate、seed、temperature、多角色对话或克隆管理。

响应必须是非空音频字节；错误 JSON、空响应或非成功状态不得落成伪音频文件。

### 音色

解析数组元素中的 `voiceId`，兼容旧字段 `value`；同时读取 `name`、`sample`、`gender`、`age`、`language`/`languageCode`。生成请求使用 `voiceId`/`value` 原值。

## Amazon Polly 设计

### 官方契约

- 生成：`POST https://polly.<region>.amazonaws.com/v1/speech`
- 音色：`GET https://polly.<region>.amazonaws.com/v1/voices`
- 服务名：`polly`
- 签名：AWS Signature Version 4
- 官方文档：
  - <https://docs.aws.amazon.com/polly/latest/dg/API_SynthesizeSpeech.html>
  - <https://docs.aws.amazon.com/polly/latest/dg/API_DescribeVoices.html>

### 凭证

格式：

```text
access_key_id|secret_access_key|region[|session_token]
```

前三段必填，第四段用于 STS 临时凭证。解析器不得回显 Secret 或 Session Token。region 只允许 AWS region 形式的字母、数字和连字符，防止构造任意 Host。

### SigV4

新增专用 `AwsSignatureV4`：

1. 计算 payload SHA-256。
2. 规范化 method、path、排序并 RFC3986 编码的 query。
3. 签名 `host;x-amz-date`，临时凭证额外包含 `x-amz-security-token`。
4. 派生 `AWS4` 日期、region、`polly`、`aws4_request` signing key。
5. 生成 `Authorization: AWS4-HMAC-SHA256 ...`。

测试使用 AWS 官方风格的固定时间/固定凭证向量，验证 canonical request、签名稳定性、query 排序和 session token 头。生产代码通过可注入的 UTC 时间函数或显式签名时间保持可测试性。

### 生成与音色

模型 ID 直接映射 Polly `Engine`：`standard`、`neural`、`long-form`、`generative`，默认 `neural`。请求字段：

- `VoiceId`
- `Engine`
- `OutputFormat`：`mp3`、`ogg_vorbis`、`pcm`
- `TextType`：`text` 或 `ssml`
- `Text`

Polly 不直接支持统一速度/音量字段，因此注册表将两者标为不支持；需要韵律时使用 SSML。

DescribeVoices 解析 `Voices[]` 的 `Id`、`Name`、`Gender`、`LanguageCode`、`LanguageName`、`SupportedEngines` 和 `AdditionalLanguageCodes`。如果返回 `NextToken`，继续签名分页请求，最多 20 页，防止异常上游造成无限循环。

## 错误与安全

- 凭证缺失或格式错误在任何网络调用前返回可操作的中文错误。
- 401/403、429、5xx 和厂商错误响应只保留安全摘要；不记录 Authorization、Secret、Session Token 或完整生成文本。
- 请求取消必须传到 `HttpClient.SendAsync`、读取响应和文件写入。
- 音频文件只在请求成功且响应非空后保留；失败时删除预留空文件。
- AWS endpoint 只由已验证 region 构造；Cartesia/PlayHT endpoint 固定。

## 测试与交付

### 每家 RED/GREEN

每家至少覆盖：

- 注册表模型、默认音色、能力、格式和官方链接。
- 凭证格式与缺失字段。
- 请求 URI、鉴权头、厂商版本/签名头和 JSON body。
- 格式到扩展名/MIME 的映射。
- fake HTTP 音频落盘、非空内容和取消传播。
- 音色 JSON 解析和在线拉取。
- 设置页帮助、首页图标和本地 API 自动厂商计数。

### 提交顺序

1. Cartesia：`接入：支持 Cartesia TTS 生成与音色刷新`
2. PlayHT：`接入：支持 PlayHT TTS 生成与音色刷新`
3. Amazon Polly：`接入：支持 Amazon Polly TTS 生成与音色刷新`

每个提交前运行：

```powershell
dotnet test VoiceServiceLocalApi.Tests/VoiceServiceLocalApi.Tests.csproj --no-restore
dotnet run --project VoiceServiceDemo.Tests/VoiceServiceDemo.Tests.csproj --no-restore
dotnet build VoiceServiceDemo.slnx --no-restore
git diff --check
```

## 非目标

- 不实现实时 WebSocket 播放或统一流式传输抽象。
- 不实现克隆、批量、多角色对话、词典和时间戳管理。
- 不请求或使用真实付费凭证。
- 不修改独立旧版 `VoiceServiceMcp` 的厂商实现。
- 不在本批次重构整个 `VendorRegistry` 或前端工作区布局。
