# TTS 生成工具不足清单

> 审查日期：2026-05-02  
> 审查范围：桌面端为主，兼顾后续 MCP 适配风险。  
> 优先级：P0 = 阻碍核心生成体验；P1 = 明显影响可用性或厂商能力完整度；P2 = 影响效率、质量或稳定性；P3 = 工程、文档、安全和长期维护。

## 总体判断

当前项目已经具备“选择厂商 -> 配置凭证 -> 选择音色 -> 生成播放”的主流程，但距离成熟的 TTS 生产/测试工具还有明显差距。最大短板不是界面是否能点通，而是厂商能力抽象还不够完整：音色库、模型、语言、情感、SSML、采样率、音频格式、长文本、试听、批量生成、历史管理和错误诊断都还没有形成统一的能力模型。

## 2026-07-14 迭代记录

- [x] **建立本地 TTS API 安全契约与标准测试项目**
  - 已完成：新增跨平台 `VoiceServiceLocalApi` 模块和标准 xUnit 项目，定义外部 TTS 请求、厂商/模型/音色能力、网关结果与 `ILocalTtsGateway`，API 层不依赖 WPF 或具体 Provider。
  - 已完成：新增 256 位 Base64URL Token 生成与固定时间校验、端口/并发/文本上限配置验证、厂商能力驱动的参数默认值与拒绝规则，以及音频 MIME 映射和输出目录边界检查。
  - 已完成：根 WPF 项目排除两个新子项目的源码与构建目录，避免 SDK 默认 glob 重复编译子项目程序集；新项目依赖全部使用固定版本。
  - 已验证：按 TDD 先创建 Token、参数验证和路径安全测试，修正 xUnit 全局 using 后初次编译因缺少 `LocalVendorInfo` 报 `CS0246`；实现后 `dotnet test VoiceServiceLocalApi.Tests/VoiceServiceLocalApi.Tests.csproj --no-restore` 共 18 项全部通过，旧桌面自检全部通过，解决方案构建 0 警告、0 错误。
  - 关联缺口：推进 P0-01、P0-02、P2-10、P2-21、P3-03 和 P3-06；HTTP 端点、桌面生命周期与 Dify 文档在后续同日迭代继续完成。

- [x] **统一桌面端 TTS Provider 注册与取消传播**
  - 已完成：新增 `ITtsProvider`、`IVoiceCatalogProvider` 和大小写不敏感的 `TtsProviderRegistry`，12 个桌面厂商全部通过唯一 ID 注册；连通性、音色刷新和生成不再维护三组厂商 switch。
  - 已完成：新增 `VoiceCatalogResult`，明确区分未知厂商、厂商不支持在线刷新、未配置凭证和上游刷新失败；旧的 `FetchVoicesAsync` 保留为兼容包装，不再由核心路由静默吞掉错误。
  - 已完成：12 个 Provider 的公开异步方法接收 `CancellationToken`，并继续传给 HTTP、文件读写、火山流读取和长文本轮询延时；`TtsService` 不吞掉取消异常，为本地 API 的请求中止和软件退出打好基础。
  - 已验证：按 TDD 先加入注册覆盖、重复 ID、未知厂商错误和真实 `HttpMessageHandler` 取消探针测试，初次编译因缺少 `ITtsProvider` 报 `CS0246`；实现后全部桌面自检通过，12 个既有 Provider fake HTTP 回归保持通过。
  - 关联缺口：推进 P2-10、P2-11 和 P3-06；桌面端 Provider 路由已统一，但旧 `VoiceServiceMcp` 仍是独立实现，因此暂不把 MCP 分叉项标记完成。

- [x] **修复桌面端并发生成覆盖音频文件**
  - 已完成：新增统一的 `AudioOutputPath.Reserve`，文件名使用毫秒时间戳、4 字节密码学随机后缀，并通过 `FileMode.CreateNew` 原子预留；同时规范厂商标识和扩展名。
  - 已完成：阿里云、Azure、百度、Deepgram、ElevenLabs、Fish Audio、Google、火山、MiniMax、OpenAI、腾讯和小米 MiMo 共 12 个桌面端 TTS Provider 已统一接入，继续复用各 Provider 原有的实际格式到扩展名映射；本次未改动 `VoiceServiceMcp`。
  - 已验证：按 TDD 先加入固定时间连续预留测试，`dotnet run --project VoiceServiceDemo.Tests/VoiceServiceDemo.Tests.csproj --no-restore` 初次因缺少 `AudioOutputPath` 报 `CS0103`；实现后全部自检通过（仅出现既有 `App.xaml.cs:16 CS8604` 可空警告），`dotnet build VoiceServiceDemo.slnx --no-restore` 通过且 0 警告、0 错误。
  - 关联缺口：完成 P1-17；输出命名模板、历史记录与 MCP 能力统一仍按各自未完成项继续跟踪。

## 2026-07-01 迭代记录

- [x] **接入 Deepgram Aura TTS HTTP 生成与模型音色刷新**
  - 已完成：新增 `DeepgramTtsProvider`，按官方接口调用 `POST https://api.deepgram.com/v1/speak`，使用 `Authorization: Token <key>` 鉴权，并将 Deepgram 的 Aura voice model 写入 `model` 查询参数。
  - 已完成：Deepgram 请求体保持官方最小 `{ text }` 结构，输出格式通过查询参数映射为 `mp3`、`wav`（`linear16 + wav`）、`opus`（`opus + ogg`）和 `flac`，语速按官方 voice controls 范围夹取到 `0.7..1.5`。
  - 已完成：Deepgram 注册表新增 Aura 2 示例模型/音色 `aura-2-thalia-en`、`aura-2-andromeda-en`、`aura-2-apollo-en`、`aura-2-arcas-en`、`aura-2-zeus-en`，并声明支持刷新模型派生音色库。
  - 已完成：接入 `GET https://api.deepgram.com/v1/models`，解析 `tts[].canonical_name`、`name`、`architecture`、`language` 和 `metadata.gender/accent/tags` 为桌面端 `VoiceOption`。
  - 已完成：Settings 增加 `DEEPGRAM_API_KEY` 凭证文案，首页增加 Deepgram 官方 lettermark 本地品牌图标。
  - 已验证：新增自检覆盖 Deepgram 请求体、Speak URI 查询参数、输出格式回落、fake HTTP 生成落盘、`Token` 鉴权、模型列表解析/拉取、注册表能力和 Settings/Home 标记；自检通过。
  - 关联缺口：继续推进“接入更多厂商 API TTS 生成能力”和 P0-01/P1-07/P2-11；长文本策略、实时流式播放、多语言筛选、项目私有模型授权状态和更细的采样率/码率控制暂未接入，后续需扩展统一高级参数与音色库元数据。

- [x] **接入 Fish Audio TTS HTTP 生成与在线音色模型库**
  - 已完成：新增 `FishAudioTtsProvider`，按官方接口调用 `POST https://api.fish.audio/v1/tts`，使用 `Authorization: Bearer <token>` 鉴权，并按官方要求通过 `model` 请求头选择 `s2-pro` / `s2.1-pro` 等模型。
  - 已完成：Fish Audio 请求体接入 `text`、可选 `reference_id`、`format`、`mp3_bitrate`、`opus_bitrate`、`latency`、`normalize`、`temperature`、`top_p` 和 `prosody.speed/volume/normalize_loudness`，并将返回音频字节直接落盘。
  - 已完成：Fish Audio 注册表新增 `s2-pro`、`s2.1-pro` 模型，预置官方示例音色 `Energetic Male`、`E-Girl Voice`，并声明 `mp3` / `wav` / `pcm` / `opus` 输出格式。
  - 已完成：接入 `GET https://api.fish.audio/model` 在线模型/音色库刷新，解析 `_id`、`title`、`tags`、`languages`、`visibility` 和 `samples.audio` 为桌面端 `VoiceOption`。
  - 已完成：Settings 增加 `FISH_AUDIO_API_KEY` 凭证文案，首页增加 Fish Audio 本地品牌图标。
  - 已验证：新增自检覆盖 Fish Audio 请求体、输出格式回落、fake HTTP 生成落盘、Bearer 鉴权、`model` 请求头、在线模型解析/拉取、注册表能力和 Settings/Home 标记；自检通过，解决方案构建通过且 0 警告 0 错误。
  - 关联缺口：继续推进“接入更多厂商 API TTS 生成能力”和 P0-01/P1-07/P2-11；WebSocket 流式生成、多说话人 dialogue、即时 voice cloning、`references` 上传和模型创建/训练状态管理暂未接入，后续需单独扩展高级参数与音色来源面板。

## 2026-06-09 迭代记录

- [x] **接入 ElevenLabs TTS HTTP 生成与在线音色库**
  - 已完成：新增 `ElevenLabsTtsProvider`，按官方接口调用 `POST https://api.elevenlabs.io/v1/text-to-speech/{voice_id}`，使用 `xi-api-key` 鉴权，通过 `output_format` 查询参数选择输出格式，并将返回的音频字节直接落盘。
  - 已完成：ElevenLabs 注册表新增 `eleven_multilingual_v2`、`eleven_flash_v2_5`、`eleven_turbo_v2_5` 等模型，预置官方示例音色，并声明 `mp3_44100_128` / `opus_48000_32` / `pcm_16000` / `ulaw_8000` 输出格式。
  - 已完成：接入 `GET https://api.elevenlabs.io/v2/voices` 在线音色库刷新，解析 `voice_id`、`name`、`category`、`labels`、`preview_url` 和 `verified_languages` 为桌面端 `VoiceOption`。
  - 已完成：Settings 增加 `ELEVENLABS_API_KEY` 凭证文案，首页增加 ElevenLabs 官方 SVG favicon 作为本地真实品牌图标。
  - 已验证：新增自检覆盖 ElevenLabs 请求体、官方输出格式回落、fake HTTP 生成落盘、`xi-api-key` 请求头、在线音色解析/拉取、注册表能力和 Workspace/Settings/Home 标记；自检与解决方案构建通过。
  - 关联缺口：继续推进“接入更多厂商 API TTS 生成能力”和 P0-01/P1-07/P2-11；更细的 stability、similarity_boost、style、speaker boost、seed、previous/next text 和在线模型刷新暂未接入，后续需扩展统一高级参数面板。

- [x] **接入 MiniMax T2A HTTP TTS 生成与在线音色库**
  - 已完成：新增 `MiniMaxTtsProvider`，按官方 T2A HTTP 接口调用 `https://api.minimax.io/v1/t2a_v2`，使用 Bearer API Key 鉴权，非流式请求 `output_format=hex`，从 `data.audio` 解码 hex 音频并落盘。
  - 已完成：MiniMax 注册表新增 `speech-2.8-hd`、`speech-2.8-turbo` 等模型，预置中文、英文、日文系统音色，并声明 `mp3` / `wav` / `flac` / `pcm` 输出格式。
  - 已完成：接入 `https://api.minimax.io/v1/get_voice` 在线音色库刷新，解析 `system_voice`、`voice_cloning`、`voice_generation` 为桌面端 `VoiceOption`。
  - 已完成：Settings 增加 `MINIMAX_API_KEY` 凭证文案，首页增加 MiniMax 官方 favicon，本次也把工作区音色刷新失败提示改为通用凭证文案。
  - 已验证：新增自检覆盖 MiniMax 请求体、输出格式回落、hex 音频解析、fake HTTP 生成落盘、在线音色解析/拉取、注册表能力和 Workspace/Settings/Home 标记；自检与解决方案构建通过。
  - 关联缺口：继续推进“接入更多厂商 API TTS 生成能力”和 P0-01/P1-07；流式 T2A、字幕、voice modify、混音权重和音色克隆管理暂未接入，后续需单独设计控制面板和音色来源。

- [x] **修复窗口缩放布局稳定性并替换厂商真实图标**
  - 已完成：首页厂商卡片改为优先加载本地品牌图标资源，覆盖火山引擎、腾讯云、阿里云、小米 MiMo、百度智能云、Microsoft Azure、Google TTS、OpenAI。
  - 已完成：工作区外壳取消 `100vw` 依赖，避免 WebView/滚动条在窗口缩放时引发横向跳动。
  - 已完成：工作区双栏断点提前到 `1360px`，并把主内容列改为弹性 `minmax(0, 1fr)`，避免扣除侧栏和 padding 后内容区被固定最小列宽挤爆。
  - 已完成：音色卡片网格改为按内容区自动分列，减少窗口尺寸变化时的突然挤压和不稳定重排。
  - 已验证：新增自检覆盖真实图标资源、首页图标渲染标记、窗口缩放相关 CSS 规则；自检与解决方案构建通过。
  - 关联缺口：推进 P2-19 空状态/视觉引导和 P2-20 窗口尺寸适配；后续仍需继续做更系统的工作区移动端/极窄宽度视觉回归。

- [x] **接入小米 MiMo V2.5 TTS 内置音色生成**
  - 已完成：新增 `XiaomiMimoTtsProvider`，按官方 OpenAI-compatible Chat Completions 接口调用 `https://api.xiaomimimo.com/v1/chat/completions`，将合成文本放入 `assistant` 消息，将朗读指导作为可选 `user` 消息。
  - 已完成：小米 MiMo 注册表新增 `mimo-v2.5-tts` 模型和官方内置音色 `mimo_default`、`冰糖`、`茉莉`、`苏打`、`白桦`、`Mia`、`Chloe`、`Milo`、`Dean`。
  - 已完成：Workspace 复用现有“朗读指导”和“输出格式”控件，支持 `wav` / `pcm16`，Settings 增加 `MIMO_API_KEY` 凭证文案和关键链接。
  - 已验证：新增自检覆盖小米 MiMo 请求体、指令消息省略逻辑、输出格式回落、base64 音频解析、fake HTTP 生成落盘、注册表能力和 Workspace/Settings 标记；自检与解决方案构建通过。
  - 关联缺口：继续推进“接入更多厂商 API TTS 生成能力”和 P2-11；文本音色设计 `mimo-v2.5-tts-voicedesign`、音色复刻 `mimo-v2.5-tts-voiceclone` 暂未接入，后续需单独设计音色来源和上传/引用流程。

## 2026-06-08 迭代记录

- [x] **补充火山引擎关键链接与设置页长反馈换行**
  - 已完成：`VendorConfig` 增加 `ImportantLinks`，火山引擎注册表补充凭证参数获取、TTS 模型实验室、豆包语音体验入口。
  - 已完成：设置页和工作区统一渲染厂商关键链接；设置页连通性测试结果改为可换行显示，避免长错误信息挤压表单。
  - 已验证：新增自检覆盖火山关键链接注册、设置页/工作区链接渲染标记、长反馈换行 CSS。
  - 关联缺口：缓解 P2-19 空状态/下一步引导和 P3-05 文档与实际能力偏差，但不代表这些大项已经完成。
- [x] **新增客户端厂商能力模型并接入表达面板入口**
  - 已完成：`VendorConfig` 增加 `VendorCapabilities`，先声明 Azure SSML/style/style degree 与火山 emotion 这两类已经暴露到客户端的能力。
  - 已完成：Workspace 的表达面板入口、SSML 文本判断、生成请求中的 style/emotion/SSML 字段改为读取能力对象，减少继续新增厂商时的硬编码入口。
  - 已验证：新增自检覆盖 Azure/火山/OpenAI 能力声明，以及 Workspace 不再用固定 `IsAzure || IsHuoshan` 组合控制表达面板。
  - 关联缺口：推进 P0-01 和 P1-07，但能力模型仍需继续扩展到格式、采样率、长文本、instructions、Google SSML 等能力。
- [x] **接入 Google SSML 生成交互**
  - 已完成：Google 注册表声明 SSML 输入能力，Workspace 会为 Google 显示普通文本/SSML 切换和 Google 专属提示。
  - 已完成：新增 `GoogleTtsProvider`，普通文本请求发送 `input.text`，SSML 模式发送 `input.ssml`，并接回桌面端 `TtsService` 分发。
  - 已验证：新增自检覆盖 Google plain text / SSML 请求体二选一，以及 Google SSML 能力声明；解决方案构建通过。
  - 关联缺口：完成 P1-13 的基础生成接入，并推进 P2-11 的供应商拆分；Google 在线音色刷新仍归 P2-14。
- [x] **接入 OpenAI 朗读指导交互**
  - 已完成：OpenAI 注册表声明 instructions 能力；Workspace 在支持 instructions 的 `gpt-4o*` TTS 模型下显示“朗读指导”输入框。
  - 已完成：新增 `OpenAiTtsProvider`，支持模型会发送 `instructions`，旧 `tts-1` / `tts-1-hd` 请求会自动省略该字段。
  - 已验证：新增自检覆盖 OpenAI instructions 请求体、旧模型省略逻辑、OpenAI 能力声明和 Workspace 提示；解决方案构建通过。
  - 关联缺口：完成 P1-14 的基础接入，并推进 P2-11 的供应商拆分；OpenAI 模型/音色外部配置仍归 P2-15。
- [x] **接入 Google 在线音色刷新**
  - 已完成：`GoogleTtsProvider` 增加 voices/list 解析和在线拉取，桌面端 `TtsService.FetchVoicesAsync("google")` 已接入 provider。
  - 已完成：Google 在线音色会规范化为 `VoiceOption`，映射 `name`、`languageCodes` 和 `ssmlGender`。
  - 已验证：新增自检覆盖 Google voices JSON 解析；自检和解决方案顺序构建通过。
  - 关联缺口：完成 P2-14 的基础在线音色刷新；更细的语言筛选和缓存版本策略仍归 P1-03/P1-05。
- [x] **接入 Azure 在线音色刷新**
  - 已完成：新增 `AzureTtsProvider`，承接 Azure 连通性测试、SSML 生成和 voices/list 在线拉取。
  - 已完成：Azure 在线音色会规范化为 `VoiceOption`，映射 `ShortName`、`LocalName/DisplayName`、`Gender`、`Locale`、`VoiceType` 和 `StyleList`。
  - 已验证：新增自检覆盖 Azure voices/list JSON 解析；自检和解决方案顺序构建通过。
  - 关联缺口：完成 P2-13，并继续推进 P2-11；Azure style 按音色动态过滤仍归 P1-09。
- [x] **按 Azure 音色动态过滤 speaking style**
  - 已完成：新增 `AzureStylePolicy`，从 Azure 在线音色的 `style:*` 元数据生成当前音色可用的 style chips。
  - 已完成：Workspace 的 Azure style 列表改为按选中音色动态显示；切换音色后会清空不再支持的已选 style。
  - 已验证：新增自检覆盖 `cheerful` / `sad` 可见、`angry` 被隐藏的动态过滤；自检和解决方案顺序构建通过。
  - 关联缺口：完成 P1-09 的基础动态过滤；更完整的 style 中文名表和离线内置音色 style 元数据可继续扩展。
- [x] **拆分百度 TTS Provider**
  - 已完成：新增 `BaiduTtsProvider`，承接百度凭证格式校验、access_token 获取、短文本合成 URL 构造和音频保存。
  - 已完成：桌面端 `TtsService` 现在只负责路由，所有已注册客户端厂商的生成/连通性细节都在 `Services/Providers/` 下。
  - 已验证：新增自检覆盖百度凭证格式本地拦截和合成 URL 参数映射；自检和解决方案顺序构建通过。
  - 关联缺口：推进 P2-11；百度仍只覆盖旧短文本合成能力，P2-12 继续保留。
- [x] **接入阿里云 Instruct 语气提示**
  - 已完成：阿里云注册表声明 instructions 能力；Workspace 在 `qwen3-tts-instruct*` 模型下显示“朗读指导”输入框和阿里专属提示。
  - 已完成：`AliyunTtsProvider` 会在 Instruct 模型下把用户输入映射到 `parameters.instructions`；非 Instruct 模型不会误发该字段。
  - 已验证：新增自检覆盖阿里云 instructions 请求体、非 Instruct 模型省略逻辑；自检和解决方案顺序构建通过。
  - 关联缺口：完成 P1-11 的基础接入；更多模板化语气预设可继续并入 P2-05。
- [x] **接入腾讯情感参数**
  - 已完成：腾讯注册表声明 emotion 能力；Workspace 显示官方 `EmotionCategory` 语气选项，并在选择具体语气后显示 `EmotionIntensity` 强度滑块。
  - 已完成：`TencentTtsProvider` 提供可测试的 `TextToVoice` 请求体构建器，只有选中官方支持的情感类别时才发送 `EmotionCategory` / `EmotionIntensity`，强度按官方 `[50,200]` 范围夹取。
  - 已验证：新增自检覆盖腾讯基础请求不误发情感字段、情感请求发送类别和强度、强度夹取、Workspace 腾讯表达入口；自检和解决方案顺序构建通过。
  - 关联缺口：完成 P1-12 的基础接入；腾讯官方说明该参数仅支持多情感音色，普通音色是否生效以接口返回为准。
- [x] **接入 OpenAI 输出格式选择**
  - 已完成：能力模型增加 `SupportedOutputFormats`，OpenAI 注册表声明 `mp3/opus/aac/flac/wav/pcm`。
  - 已完成：Workspace 根据能力对象显示“输出格式”选择；`OpenAiTtsProvider` 将用户选择映射到 `response_format`，非法值回落到 `mp3`，保存文件扩展名随格式变化。
  - 已验证：新增自检覆盖 OpenAI `response_format`、非法格式回落、扩展名映射、Workspace 输出格式入口；自检和解决方案顺序构建通过。
  - 关联缺口：推进 P1-15；该大项仍需继续接入 Azure、Google、腾讯等厂商的格式/采样率能力。
- [x] **接入 Google 输出编码选择**
  - 已完成：Google 注册表声明 `mp3/linear16/ogg_opus/mulaw/alaw` 输出格式，复用 Workspace 的通用“输出格式”选择。
  - 已完成：`GoogleTtsProvider` 将客户端格式映射到官方 `audioConfig.audioEncoding`，非法值回落到 `MP3`；`LINEAR16/MULAW/ALAW` 保存为 `.wav`，`OGG_OPUS` 保存为 `.ogg`。
  - 已验证：新增自检覆盖 Google `audioEncoding` 映射、非法格式回落、扩展名映射和能力声明；自检和解决方案顺序构建通过。
  - 关联缺口：继续推进 P1-15；Google 采样率 `sampleRateHertz` 和声道/码率仍待后续能力模型扩展。
- [x] **接入腾讯输出格式选择**
  - 已完成：腾讯注册表声明 `mp3/wav/pcm` 输出格式，复用 Workspace 的通用“输出格式”选择。
  - 已完成：`TencentTtsProvider` 将客户端格式映射到官方 `TextToVoice.Codec`，非法值回落到 `mp3`，保存文件扩展名随格式变化。
  - 已验证：新增自检覆盖腾讯默认 codec、PCM codec、非法 codec 回落、扩展名映射和能力声明；自检和解决方案顺序构建通过。
  - 关联缺口：继续推进 P1-15；腾讯采样率 `SampleRate` 仍待后续能力模型扩展。
- [x] **接入 Azure 输出格式选择**
  - 已完成：Azure 注册表声明 `mp3_16k/mp3_24k/riff_16k_pcm/riff_24k_pcm/raw_16k_pcm/ogg_24k_opus` 输出格式，复用 Workspace 的通用“输出格式”选择。
  - 已完成：`AzureTtsProvider` 将客户端格式映射到 REST 请求头 `X-Microsoft-OutputFormat`，非法值回落到 `audio-16khz-128kbitrate-mono-mp3`，保存文件扩展名随格式变化。
  - 已验证：新增自检覆盖 Azure 输出格式 header 映射、非法格式回落、扩展名映射和能力声明；自检和解决方案顺序构建通过。
  - 关联缺口：继续推进 P1-15；更细的采样率/码率/声道枚举可后续扩展到统一格式元数据。
- [x] **接入百度输出格式选择**
  - 已完成：百度注册表声明 `mp3/pcm_16k/pcm_8k/wav` 输出格式，复用 Workspace 的通用“输出格式”选择。
  - 已完成：`BaiduTtsProvider` 将客户端格式映射到短文本合成 URL 参数 `aue`，非法值回落到 `3`（MP3），保存文件扩展名随格式变化。
  - 已验证：新增自检覆盖百度默认 aue、PCM 16K aue、非法格式回落、扩展名映射和能力声明；自检和解决方案顺序构建通过。
  - 关联缺口：继续推进 P1-15；百度音质/采样率更细说明可后续并入统一格式元数据。
- [x] **接入火山输出格式选择**
  - 已完成：火山注册表声明 `mp3/pcm/ogg_opus` 输出格式，复用 Workspace 的通用“输出格式”选择。
  - 已完成：桌面端火山 V3、V3 长文本和旧版 `/api/v1/tts` 都会把客户端格式分别映射到 `audio_params.format` / `audio.encoding`，非法值回落到 `mp3`，保存文件扩展名随格式变化。
  - 已验证：新增自检覆盖火山 V3 format、V3 长文本 format、旧版 encoding、非法格式回落、扩展名映射和能力声明；自检和解决方案顺序构建通过。
  - 关联缺口：继续推进 P1-15；采样率仍固定 24k，后续可并入统一格式元数据。
- [x] **接入阿里云 CosyVoice 输出格式选择**
  - 已完成：阿里云注册表补充 `cosyvoice-v3-flash`、`cosyvoice-v3-plus`、`cosyvoice-v2`，并声明 `mp3/pcm/wav/opus` 输出格式。
  - 已完成：`AliyunTtsProvider` 对 CosyVoice V2/V3 使用官方 `SpeechSynthesizer` 接口，将客户端格式映射到 `input.format`，非法值回落到 `mp3`，保存文件扩展名随格式变化。
  - 已完成：Qwen3-TTS 非实时 HTTP 按官方固定 WAV 处理，不发送未确认的 `format` / `response_format` 字段；Workspace 在 Qwen3 模型下自动固定 WAV，在 CosyVoice 模型下显示可选格式。
  - 已验证：新增自检覆盖 CosyVoice `input.format`、非法格式回落、Qwen3 不误发格式字段、Qwen3/CosyVoice 保存扩展名、阿里云能力声明；自检和解决方案构建通过。
  - 关联缺口：完成 P1-15 的厂商输出格式选择；采样率、码率和声道仍归 P1-16 的音频元数据继续推进。

## P0：核心生成链路

- [ ] **P0-01 统一生成能力模型**
  - 现状：`TtsRequest` 已开始加入 `InputFormat`、`Style`、`Emotion`、`SsmlText`，但还没有统一描述每个厂商支持哪些能力。
  - 影响：UI 只能靠 `vendorId` 写条件分支，后续接入更多厂商会继续膨胀。
  - 建议：新增 `VendorCapabilities`，描述 `SupportsSsml`、`SupportsEmotion`、`SupportsStyle`、`SupportsRole`、`SupportsPitch`、`SupportsSampleRate`、`SupportsStreaming`、`SupportsLongText`、`SupportedFormats`。
  - 验收：Workspace 根据能力对象渲染控件，不再散落大量 `IsAzure`、`IsHuoshan` 判断。

- [ ] **P0-02 音色选择与生成参数未强绑定校验**
  - 现状：用户选择的 `VoiceId`、`ModelId`、`Speed`、`Volume` 直接进入 provider，部分 provider 做隐式转换。
  - 影响：音色和模型不匹配、参数越界、音色 ID 类型错误时会在请求后才失败。
  - 建议：生成前做 `ValidateRequest`，返回清晰的 UI 错误。
  - 验收：不合法组合无法点击生成，或生成前给出具体错误，例如“该音色需要 seed-tts-2.0”。

- [ ] **P0-03 缺少生成历史**
  - 现状：每次生成只显示当前结果，历史音频依赖用户去文件夹找。
  - 影响：TTS 测试工具最常见的“对比多次生成结果”很难做。
  - 建议：新增本地生成历史列表，记录厂商、模型、音色、文本摘要、参数、文件路径、时长、生成时间。
  - 验收：用户能回放、打开、复制参数、重新生成历史条目。

- [ ] **P0-04 缺少批量生成**
  - 现状：一次只能输入一段文本生成一个音频。
  - 影响：无法做多句台词、多音色、多参数对比测试。
  - 建议：支持多行/CSV 批量任务，提供队列、进度、失败重试和导出。
  - 验收：用户可用同一音色批量生成多条文本，也可一条文本批量对比多个音色。

- [ ] **P0-05 长文本策略不统一**
  - 现状：火山有 V3 长文本路径，其他厂商缺少统一策略。
  - 影响：长文本在不同厂商下失败方式不一致。
  - 建议：统一文本长度上限、自动分段、合并音频、异步任务轮询策略。
  - 验收：长文本生成前能提示预计处理方式，生成后得到可播放的合并音频。

## P1：厂商音色库与模型库

- [ ] **P1-01 模型库不能在线刷新**
  - 现状：`SupportsModelFetch` 基本都是 `false`，模型列表主要硬编码在 `VendorRegistry`。
  - 影响：模型升级后项目需要改代码才能跟进。
  - 建议：按厂商实现模型刷新或半自动更新文件。
  - 验收：至少 Azure、OpenAI、Google、阿里可展示当前可用模型或可维护的本地模型源。

- [ ] **P1-02 音色库字段过少**
  - 现状：`VoiceOption` 有 Id、Name、Gender、Language、SampleUrl、Age、Categories、Emotions，但缺少 locale、provider family、quality tier、price tier、style support、sample text、许可状态。
  - 影响：筛选和能力判断弱，用户难以理解音色适合什么场景。
  - 建议：扩展音色元数据，并把在线拉取结果规范化。
  - 验收：音色卡片可显示语言、场景、情绪、模型/资源、可试听、是否已授权。

- [ ] **P1-03 音色库缓存没有版本、过期和刷新策略**
  - 现状：缓存文件按 `voices_{vendor}.json` 保存，没有 schema version、来源、失败状态、过期策略。
  - 影响：旧缓存可能覆盖新结构，音色数据异常时用户不知道。
  - 建议：缓存包装成 `{ schemaVersion, vendorId, fetchedAt, source, voices }`。
  - 验收：缓存可过期、可清空、可查看刷新时间和失败原因。

- [ ] **P1-04 试听样音链路不稳定**
  - 现状：试听在 Razor 中临时 `new HttpClient()` 下载 SampleUrl，部分资源依赖 Referer，MIME 类型只看后缀。
  - 影响：试听失败定位困难，试听下载可能卡 UI 流程。
  - 建议：抽成 `VoicePreviewService`，统一超时、Referer、MIME 探测、缓存、错误文案。
  - 验收：试听失败显示“防盗链/超时/格式不支持/无样音”等具体原因。

- [ ] **P1-05 搜索筛选能力较弱**
  - 现状：搜索只按名称和 ID，筛选主要按分类和性别。
  - 影响：全量音色几百上千时查找效率不足。
  - 建议：支持语言、场景、情感、年龄、模型、是否可试听、是否已授权、多选筛选。
  - 验收：常见问题“找中文女声、开心语气、可试听、大模型”可以一次筛出。

- [ ] **P1-06 缺少音色收藏和最近使用**
  - 现状：每次进入厂商页默认选第一个音色。
  - 影响：重复测试常用音色不方便。
  - 建议：支持收藏、最近使用、默认音色、按厂商保存上次选择。
  - 验收：重启后仍能恢复用户常用音色。

## P1：SSML、语气与表达控制

- [ ] **P1-07 表达能力仍是厂商条件分支**
  - 现状：Azure 和火山已有初步表达控件，但 UI 仍直接判断厂商。
  - 影响：腾讯、阿里、Google 后续加入风格/情感时会继续复制逻辑。
  - 建议：改为能力驱动的 `ExpressionControls` 子组件。
  - 验收：新增一个厂商的表达能力只需要更新能力描述和 provider 映射。

- [ ] **P1-08 Azure SSML 编辑缺少校验和模板**
  - 现状：raw SSML 可输入，但没有语法校验、模板、预览结构。
  - 影响：用户写错 SSML 后只能等接口报错。
  - 建议：提供 SSML 模板、语法轻校验、常用标签插入。
  - 验收：缺失 `<speak>`、voice 不匹配、XML 不闭合可在生成前提示。

- [x] **P1-09 Azure speaking style 不是按音色动态过滤**
  - 现状：Azure 在线音色刷新会把 `StyleList` 保存为 `style:*` 元数据；Workspace 根据当前音色动态显示 style chips。
  - 已修正：当当前音色声明 style 支持时，用户只看到该音色可用的 style；没有 style 元数据的内置/缓存音色保留常用 fallback。
  - 后续影响：更完整的 Azure style 中文名表、内置音色 style 配表和 style 说明仍可继续增强。
  - 验收：用户只看到当前音色可用的 style。

- [ ] **P1-10 火山 emotion 能力仍未完整产品化**
  - 现状：桌面端已经优先使用在线音色库返回的 `Emotions`，并且在线音色未声明 emotion 时不再猜测；内置离线推荐音色仍保留常用 fallback。
  - 已修正：V3 请求体里的 emotion 已放到 `req_params.audio_params.emotion`；`general` / `neutral` 不再作为实际请求 emotion 发送。
  - 剩余影响：离线 fallback 仍无法证明账号/音色真实支持；`emotion_scale` 未接入；MCP 生成请求还没有 emotion 入参。
  - 建议：把离线 fallback 标记为“实验”，新增 emotion 强度字段，并让 MCP schema 复用同一套 emotion/resource 规则。
  - 验收：UI 能区分“官方返回支持”和“实验尝试”；桌面端与 MCP 都能发送同一套 emotion 参数，并能选择是否传 `emotion_scale`。

- [x] **P1-11 阿里云 qwen3/CosyVoice 的指令式语气未产品化**
  - 现状：阿里云 Instruct 模型已显示“朗读指导”输入框，并把用户输入映射到请求体 `parameters.instructions`。
  - 已修正：非 Instruct 模型不会发送不支持的 instructions 字段，避免旧模型误用。
  - 后续影响：客服、旁白、新闻、亲切等模板化预设仍可继续并入 P2-05 参数预设。
  - 验收：用户可选择“客服、旁白、新闻、亲切”等提示模板。

- [x] **P1-12 腾讯情感/风格参数未接入**
  - 现状：腾讯工作区已接入官方 `TextToVoice` 的 `EmotionCategory` 与 `EmotionIntensity`，基础请求不会误发情感字段。
  - 已修正：腾讯注册表声明 emotion 能力；Workspace 显示官方语气 chips 和强度滑块；provider 只发送白名单情感类别并夹取强度。
  - 剩余影响：官方当前字段只声明情感类别和强度，且仅多情感音色实际支持；更细的风格/场景参数暂未发现可验证 API 字段。
  - 验收：腾讯工作区出现经过验证的表达控件，而不是占位 UI。

- [x] **P1-13 Google SSML 未接入**
  - 现状：Google 已支持普通文本/SSML 切换；SSML 模式下 provider 发送 `input.ssml`，普通文本模式下继续发送 `input.text`。
  - 已修正：Workspace 复用能力模型展示 Google SSML 交互，并使用不含 Azure `mstts` 命名空间的基础 `<speak>` 模板。
  - 后续影响：Google SSML 语法校验、标签模板和更细的兼容性提示可继续并入 P0-02/P1-08 的校验与模板体系。
  - 验收：Google 可在 SSML 模式下生成 `<speak>` 输入。

- [x] **P1-14 OpenAI 指令式语气未接入**
  - 现状：OpenAI 在支持 instructions 的 `gpt-4o*` TTS 模型下显示“朗读指导”字段，并把用户输入映射到请求体 `instructions`。
  - 已修正：旧 `tts-1` / `tts-1-hd` 模型不会发送不支持的 instructions 字段，避免用户误以为所有 OpenAI TTS 模型都支持该能力。
  - 后续影响：更多模型更新、音色配置外置和模型级能力差异仍归 P2-15 继续处理。
  - 验收：OpenAI 生成可带“用温柔但专业的语气朗读”等指令。

## P1：音频输出与播放

- [x] **P1-15 缺少音频格式选择**
  - 现状：OpenAI 已可选择 `mp3/opus/aac/flac/wav/pcm`；Google 已可选择 `mp3/linear16/ogg_opus/mulaw/alaw`；腾讯已可选择 `mp3/wav/pcm`；Azure 已可选择常用 MP3/RIFF PCM/RAW PCM/OGG Opus；百度已可选择 `mp3/pcm_16k/pcm_8k/wav`；火山已可选择 `mp3/pcm/ogg_opus`；阿里云 CosyVoice V2/V3 已可选择 `mp3/pcm/wav/opus`。
  - 已修正：各 provider 都会把客户端格式映射到官方字段或请求头，非法值回落到安全默认格式，保存文件扩展名随格式变化；阿里云 Qwen3-TTS 非实时 HTTP 没有官方格式参数，因此 UI 固定 WAV 且 provider 不发送未确认字段。
  - 剩余影响：采样率、码率、声道和生成后媒体探测仍没有统一格式元数据，继续归 P1-16 推进。
  - 验收：支持至少 MP3/WAV/PCM 的厂商可在 UI 选择。

- [ ] **P1-16 缺少音频时长、采样率、码率等元数据**
  - 现状：`DurationSeconds` 字段存在但没有实际填充。
  - 影响：无法做质量评估、成本估算和历史对比。
  - 建议：生成后用 NAudio 或媒体探测读取时长、格式、采样率、声道。
  - 验收：结果卡片显示时长、格式、大小、采样率。

- [x] **P1-17 毫秒时间戳 + 密码学随机后缀 + 原子预留避免覆盖**
  - 现状：12 个桌面端 Provider 统一使用 `{vendor}_{yyyyMMdd_HHmmss_fff}_{8位随机后缀}.{ext}` 输出文件名。
  - 实现：随机后缀来自 4 字节密码学安全随机数，并用 `FileMode.CreateNew` 原子预留；仅在同名文件已存在时重试，防止并发请求互相覆盖。
  - 验收：固定同一时间连续预留会返回不同路径，两个文件均已落盘，厂商前缀、毫秒时间戳和原格式扩展名保持正确。

- [ ] **P1-18 缺少输出命名模板**
  - 现状：文件名只含厂商和时间。
  - 影响：用户难以从文件名识别音色、模型、文本。
  - 建议：支持模板 `{vendor}_{voice}_{model}_{date}_{shortText}`。
  - 验收：用户可在设置里配置命名规则。

- [ ] **P1-19 缺少音频对比播放**
  - 现状：一次只播放当前结果。
  - 影响：调参和选音色效率低。
  - 建议：历史列表支持 A/B 对比、连续播放、相同文本多音色对比。
  - 验收：用户可一键比较多个生成版本。

## P2：文本处理与生成体验

- [ ] **P2-01 文本输入缺少字数/限制感知**
  - 现状：仅显示字数，没有按厂商提示限制或计费影响。
  - 影响：用户不知道何时会失败或走长文本。
  - 建议：按厂商显示字符上限、长文本策略、预估费用/额度影响。
  - 验收：超过限制前就提示。

- [ ] **P2-02 缺少文本预处理工具**
  - 现状：只能直接输入文本。
  - 影响：长文、多段落、字幕台词、小说对白需要手工整理。
  - 建议：增加清理空行、按标点分句、移除 Markdown、导入 txt/srt/csv。
  - 验收：用户可导入文本并拆成批量任务。

- [ ] **P2-03 缺少多角色/旁白台本模式**
  - 现状：一个请求只对应一个音色。
  - 影响：无法生成对话、播客、小说多角色。
  - 建议：支持台本行格式 `角色: 文本`，角色映射音色。
  - 验收：同一台本可按角色生成多个音频片段。

- [ ] **P2-04 缺少停顿、读法和发音词典**
  - 现状：除 SSML 外没有通用读法控制。
  - 影响：专有名词、数字、英文缩写读错时难以修正。
  - 建议：增加发音替换表、术语表、停顿插入工具，并映射到 SSML 或厂商参数。
  - 验收：用户可保存“AI=人工智能/按字母读”等规则。

- [ ] **P2-05 缺少参数预设**
  - 现状：语速/音量/风格每次手动调。
  - 影响：无法快速复用“新闻播报”“温柔客服”“小说旁白”配置。
  - 建议：支持全局和厂商级 preset。
  - 验收：用户可保存、加载、导出 preset。

## P2：错误处理与诊断

- [ ] **P2-06 错误吞掉过多**
  - 现状：多处 `catch { }`，音色解析、缓存、试听失败缺少可见原因。
  - 影响：用户只看到空列表或失败，不知道是权限、网络、格式还是解析问题。
  - 建议：引入轻量日志和用户可读错误状态。
  - 验收：每个刷新/生成失败都有具体原因和建议动作。

- [ ] **P2-07 调试 JSON 写入用户输出目录**
  - 现状：provider 会把 `huoshan_speakers_page_*.json`、`tencent_voices_debug.json` 写到输出目录。
  - 影响：污染用户音频目录，可能混入敏感响应。
  - 建议：改写到 `.local/logs` 或 `%APPDATA%/VoiceOps/Diagnostics`，并受开关控制。
  - 验收：普通用户输出目录只出现音频和明确导出的文件。

- [ ] **P2-08 连通性测试不够准确**
  - 现状：部分 provider 只判断鉴权错误，其他非 2xx 可能误报成功。
  - 影响：配置阶段给用户错误信心。
  - 建议：统一 `ConnectivityResult`，包含 Auth、Quota、Network、ServiceUnavailable、Unsupported。
  - 验收：连通测试能区分凭证错、额度不足、网络错。

- [ ] **P2-09 缺少请求/响应摘要面板**
  - 现状：生成失败只显示错误字符串。
  - 影响：开发者测试 TTS API 时无法快速定位 payload。
  - 建议：增加“技术详情”面板，显示脱敏后的 endpoint、headers 摘要、payload、response。
  - 验收：用户可复制脱敏诊断信息。

- [ ] **P2-10 缺少超时、取消和重试控制**
  - 现状：HttpClient 统一 60 秒，生成中无法取消。
  - 影响：网络卡住时体验差。
  - 建议：每个任务使用 CancellationToken，支持取消、重试、超时配置。
  - 验收：生成中按钮可取消，失败可重试。

## P2：厂商实现完整度

- [ ] **P2-11 Provider 接口没有统一抽象**
  - 现状：阿里、火山、腾讯、Google、OpenAI、Azure、百度已拆成 provider；但 provider 之间还没有共同接口或统一能力声明方法。
  - 影响：功能扩展虽然不再集中堆到 `TtsService`，但仍依赖手写路由和各 provider 自己暴露方法。
  - 建议：引入 `ITtsProvider`，统一 `GenerateAsync`、`FetchVoicesAsync`、`TestConnectivityAsync`、`GetCapabilities`。
  - 验收：`TtsService` 只负责路由和公共流程。

- [ ] **P2-12 百度能力过旧**
  - 现状：百度已拆成独立 provider，但仍只接短文本在线合成，音色列表硬编码。
  - 影响：精品音库、长文本、更多参数未覆盖。
  - 建议：按当前百度接口重新梳理模型、音色、参数能力。
  - 验收：百度工作区能力与当前控制台能力基本一致。

- [x] **P2-13 Azure 音色在线拉取未实现**
  - 现状：桌面端已实现 Azure voices/list 拉取，并把 `ShortName`、`LocalName/DisplayName`、`Gender`、`Locale`、`VoiceType`、`StyleList` 规范化到 `VoiceOption`。
  - 已修正：`TtsService.FetchVoicesAsync("azure")` 已接入 `AzureTtsProvider.FetchVoicesAsync`，工作区刷新音色会走在线接口。
  - 后续影响：按音色动态过滤 Azure speaking style 仍归 P1-09；缓存 schema、过期策略继续归 P1-03。
  - 验收：Azure 可刷新完整音色库。

- [x] **P2-14 Google 音色在线拉取未实现**
  - 现状：桌面端已实现 Google voices/list 拉取，并把 `languageCodes`、`name`、`ssmlGender` 规范化到 `VoiceOption`。
  - 已修正：`TtsService.FetchVoicesAsync("google")` 已接入 `GoogleTtsProvider.FetchVoicesAsync`，工作区刷新音色会走在线接口。
  - 后续影响：缓存 schema、过期策略、语言/性别多选筛选继续归 P1-03/P1-05 处理。
  - 验收：Google 可按语言刷新音色。

- [ ] **P2-15 OpenAI 模型和音色可能需要更新机制**
  - 现状：OpenAI 模型/音色硬编码。
  - 影响：模型变化时需要改代码。
  - 建议：用配置文件维护模型与音色，并定期校验。
  - 验收：不改代码即可更新推荐模型列表。

## P2：UI/UX 与信息架构

- [ ] **P2-16 Workspace 文件过大**
  - 现状：`Workspace.razor` 超过 1200 行，包含 UI、状态、缓存、试听、生成、播放、表达逻辑。
  - 影响：后续改动容易互相踩踏。
  - 建议：拆为 `VoiceLibraryPanel`、`GenerationPanel`、`AudioResultPanel`、`ExpressionPanel`、`Toast`。
  - 验收：每个组件职责清晰，Workspace 只编排状态。

- [ ] **P2-17 Settings 文件也偏大**
  - 现状：`Settings.razor` 约 400 行，火山/腾讯特殊凭证 UI 混在一个页面。
  - 影响：新增厂商高级配置会更乱。
  - 建议：拆厂商凭证编辑组件和输出目录组件。
  - 验收：新增厂商凭证表单无需改主设置页面大量代码。

- [ ] **P2-18 首页缺少能力状态总览**
  - 现状：首页只显示是否配置 API Key。
  - 影响：用户不知道厂商是否支持音色刷新、SSML、情感、长文本。
  - 建议：卡片展示能力徽章和最近使用状态。
  - 验收：首页能快速判断进入哪个厂商测试哪类能力。

- [ ] **P2-19 缺少空状态引导**
  - 现状：音色为空时提示较简单。
  - 影响：用户不知道是没配置、权限不足、缓存空还是该厂商不支持刷新。
  - 建议：空状态按原因显示操作按钮：配置凭证、刷新、查看文档、使用内置音色。
  - 验收：每种空状态都有下一步操作。

- [ ] **P2-20 缺少国际化/中英一致性策略**
  - 现状：UI 中文为主，README 中英都有，但代码显示混合中英文。
  - 影响：后续英文版桌面应用成本高。
  - 建议：抽资源文件或至少集中 UI 文案。
  - 验收：核心文案不散落在 Razor 和 provider 中。

## P2：测试与质量保障

- [ ] **P2-21 测试项目不是标准测试框架**
  - 现状：`VoiceServiceDemo.Tests` 是 console smoke test。
  - 影响：无法按测试名定位失败，也不利于 CI。
  - 建议：迁移到 xUnit/NUnit，并保留 smoke test 入口。
  - 验收：`dotnet test` 可运行单元测试。

- [ ] **P2-22 Provider 网络行为缺少可测试性**
  - 现状：provider 直接用 HttpClient，但没有统一 fake handler 测试。
  - 影响：JSON 解析、错误映射、请求体构造难回归。
  - 建议：用 `HttpMessageHandler` fake 或 wrapper，测试每个 provider。
  - 验收：每个 provider 至少有成功、鉴权失败、解析失败测试。

- [ ] **P2-23 缺少 UI 自动化冒烟测试**
  - 现状：没有桌面端 UI flow 测试。
  - 影响：Razor 状态改动可能破坏选择/生成按钮。
  - 建议：可先做组件级 bUnit，或用 Playwright 检查 BlazorWebView 难度后选择替代。
  - 验收：基本流程“进入厂商、选择音色、填文本、按钮状态”有自动检查。

- [ ] **P2-24 缺少真实厂商沙箱/录制测试策略**
  - 现状：真实 API 只能人工测，自动测试不调用网络。
  - 影响：厂商接口变化难以及早发现。
  - 建议：建立手动验收脚本和可选的 secret-driven integration test。
  - 验收：有一份“每个厂商上线前验收清单”。

## P3：安全、配置与工程维护

- [ ] **P3-01 凭证明文存储**
  - 现状：API Key 明文写入 `%APPDATA%/VoiceOps/config.json`。
  - 影响：安全风险，但当前用户明确不是最高优先级。
  - 建议：后续用 DPAPI 或 Windows Credential Manager。
  - 验收：配置文件不再包含明文密钥。

- [ ] **P3-02 `.env.test` 被 Git 跟踪**
  - 现状：仓库中 `.env.test` 是 tracked 文件。
  - 影响：容易误放真实测试密钥。
  - 建议：只保留 `.env.example`，测试密钥用本地未跟踪文件。
  - 验收：`git ls-files` 不包含真实环境文件。

- [ ] **P3-03 依赖版本使用通配符**
  - 现状：csproj 使用 `8.0.*`、`2.2.*`。
  - 影响：构建可重复性较弱。
  - 建议：锁定版本或使用集中包管理。
  - 验收：不同机器 restore 得到一致版本。

- [ ] **P3-04 缺少 CI**
  - 现状：未看到自动构建测试流水线。
  - 影响：改动容易破坏构建。
  - 建议：增加 GitHub Actions 或本地脚本，跑 build/test。
  - 验收：PR 或提交自动执行 `dotnet build` 和 `dotnet test/run`。

- [ ] **P3-05 文档与实际能力有偏差**
  - 现状：README 宣称多厂商全覆盖，但部分能力是内置列表或未完整实现。
  - 影响：用户预期过高。
  - 建议：README 增加能力矩阵，标注“已实现/部分实现/计划中”。
  - 验收：用户能一眼看到每个厂商支持生成、音色刷新、SSML、情感、长文本、批量等情况。

- [ ] **P3-06 MCP 与桌面端能力会继续分叉**
  - 现状：用户当前决定 MCP 以后适配；MCP 已有独立模型和 provider 逻辑。
  - 影响：桌面端越完善，MCP 后续追平成本越高。
  - 建议：桌面端新能力尽量沉到 `VoiceServiceShared` 或 provider 抽象中，MCP 后续复用。
  - 验收：MCP 适配时不需要重写 SSML、emotion、voice catalog 解析。

## 建议实施顺序

1. **第一阶段：稳定核心工作流**
   - P0-01、P0-02、P0-03、P1-15、P1-16、P1-17。

2. **第二阶段：音色库和表达控制产品化**
   - P1-01 到 P1-14，优先 Azure、火山、阿里、腾讯。

3. **第三阶段：批量、历史、文本处理**
   - P0-04、P0-05、P2-01 到 P2-05、P1-19。

4. **第四阶段：工程拆分和测试**
   - P2-11、P2-16、P2-17、P2-21 到 P2-24。

5. **第五阶段：MCP 追平和安全配置**
   - P3-01、P3-02、P3-05、P3-06。

