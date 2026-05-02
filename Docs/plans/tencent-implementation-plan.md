# 新增腾讯云 TTS 模块

在现有多厂商 TTS 聚合平台中，新增**腾讯云语音合成**作为一个完整的 vendor 模块，严格遵循现有 [VendorRegistry](file:///e:/AIMadeupTools/04_AILabs/AIVoice/VoiceServiceDemo/Services/VendorRegistry.cs#8-164) + [TtsService](file:///e:/AIMadeupTools/04_AILabs/AIVoice/VoiceServiceDemo/Services/TtsService.cs#18-23) 的代码模式。

## Proposed Changes

### 1. VendorRegistry — 注册腾讯云厂商

#### [MODIFY] [VendorRegistry.cs](file:///e:/AIMadeupTools/04_AILabs/AIVoice/VoiceServiceDemo/Services/VendorRegistry.cs)

在中国厂商区域（火山引擎之后）新增一个 [VendorConfig](file:///e:/AIMadeupTools/04_AILabs/AIVoice/VoiceServiceDemo/Models/VendorConfig.cs#6-19)：

- **Id**: `"tencent"`
- **Name**: `"腾讯云"`
- **Description**: `"腾讯云语音合成服务，提供多种高拟真度音色，支持大模型超自然语音。"`
- **IconName**: `"message-square"` (Lucide icon)
- **ApiBaseUrl**: `"https://tts.tencentcloudapi.com"`
- **DocumentationUrl**: `"https://cloud.tencent.com/document/product/1073"`
- **SupportsVoiceFetch**: `true`（通过控制台 API 拉取全量音色）
- **DefaultModels**: 标准版 + 大模型版
- **DefaultVoices**: 预置 5-6 个高质量音色（从 API 返回的 VoiceQuality=4 中精选）

---

### 2. TtsService — 实现腾讯云的三个核心方法

#### [MODIFY] [TtsService.cs](file:///e:/AIMadeupTools/04_AILabs/AIVoice/VoiceServiceDemo/Services/TtsService.cs)

严格遵循现有模式（如 [TestHuoshanAsync](file:///e:/AIMadeupTools/04_AILabs/AIVoice/VoiceServiceDemo/Services/TtsService.cs#231-253) / [FetchHuoshanVoicesAsync](file:///e:/AIMadeupTools/04_AILabs/AIVoice/VoiceServiceDemo/Services/TtsService.cs#68-195) / [GenerateHuoshanAsync](file:///e:/AIMadeupTools/04_AILabs/AIVoice/VoiceServiceDemo/Services/TtsService.cs#375-442)），新增：

**a) `TestTencentAsync(apiKey)`** — 连通性测试
- API Key 格式: `SecretId|SecretKey`
- 使用 TC3-HMAC-SHA256 签名，调用 `DescribeVoices` 接口验证鉴权

**b) `FetchTencentVoicesAsync(apiKey)`** — 获取音色列表
- 调用腾讯云官方 `DescribeVoices` API
- 解析 `CategoryVoiceList > VoiceList`，映射为 [VoiceOption](file:///e:/AIMadeupTools/04_AILabs/AIVoice/VoiceServiceDemo/Models/VendorConfig.cs#32-48) 列表
- 字段映射: `VoiceType` → Id, `VoiceName` → Name, `VoiceGender` → Gender, `VoiceAudio` → SampleUrl, `Category` → Categories

**c) `GenerateTencentAsync(request, apiKey)`** — 语音合成
- 调用 `TextToVoice` API（腾讯云官方 TTS 接口）
- 使用 TC3-HMAC-SHA256 签名
- 解析返回的 Base64 音频 → 保存为 mp3 文件

**d) 在 switch 分支中注册** `"tencent"` 到 [TestConnectivityAsync](file:///e:/AIMadeupTools/04_AILabs/AIVoice/VoiceServiceDemo/Services/TtsService.cs#24-51)、[FetchVoicesAsync](file:///e:/AIMadeupTools/04_AILabs/AIVoice/VoiceServiceDemo/Services/TtsService.cs#52-67)、[GenerateAsync](file:///e:/AIMadeupTools/04_AILabs/AIVoice/VoiceServiceDemo/Services/TtsService.cs#292-323)

---

### 3. 新增签名 Helper

#### [NEW] [TencentSigner.cs](file:///e:/AIMadeupTools/04_AILabs/AIVoice/VoiceServiceDemo/Helpers/TencentSigner.cs)

实现腾讯云 TC3-HMAC-SHA256 签名算法（参考已有的 `VolcengineSigner.cs` 模式）

---

### 4. 清理临时文件

#### [DELETE] [fetch_voices.py](file:///e:/AIMadeupTools/04_AILabs/AIVoice/VoiceServiceDemo/fetch_voices.py)、[count_voices.py](file:///e:/AIMadeupTools/04_AILabs/AIVoice/VoiceServiceDemo/count_voices.py)、[voices_response.json](file:///e:/AIMadeupTools/04_AILabs/AIVoice/VoiceServiceDemo/voices_response.json)

这些测试脚本不再需要。

## Verification Plan

### 自动验证
1. 运行 `dotnet build` 确认编译通过无错误
2. 运行 `dotnet run`，在 UI 中确认"腾讯云"出现在厂商列表中

### 手动验证（请用户操作）
1. 打开应用 → 在厂商列表中选择"腾讯云"
2. 在设置页面填入 `SecretId|SecretKey` 格式的 API Key
3. 点击"测试连接"确认连通性
4. 选择音色 → 输入文本 → 点击合成 → 确认能正常生成音频
