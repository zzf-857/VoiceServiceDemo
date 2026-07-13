# VoiceOps 本地 TTS API 设计

## 目标

让 VoiceOps 桌面软件在运行期间提供稳定、受保护的本地 HTTP API。外部程序可以复用桌面端已经保存的厂商凭证和全部 TTS Provider，传入文本、厂商、模型、音色及表达参数，获得可下载的音频地址或直接获得音频字节。接口需要适配 Dify 的 HTTP Request 节点和 OpenAPI 自定义工具导入。

本设计是第一阶段交付。完成后再单独规划并接入下一批 TTS 厂商，避免把“本地 API 基础设施”和“新厂商协议适配”混在同一批改动中。

## 已确认的现状

- 桌面端已经注册并路由 12 个厂商：火山引擎、腾讯云、阿里云、小米 MiMo、MiniMax、百度、Fish Audio、Deepgram、ElevenLabs、Azure、Google 和 OpenAI。
- `VoiceServiceMcp` 需要单独启动，配置和 Provider 已经落后于桌面端；工具 schema 名义支持 6 家，注册表实际只有火山和 OpenAI。
- MCP 只有一次性 `POST /mcp/sse`，没有适合 Dify HTTP Request 节点的普通 REST TTS 接口。
- 桌面端输出文件名主要精确到秒，并发生成存在覆盖风险。
- 桌面凭证保存在 `%APPDATA%/VoiceOps/config.json`；本地 API 不应再维护第二套凭证。

## 方案比较与决策

### 采用：桌面进程内嵌模块化 Kestrel

新增独立的 `VoiceServiceLocalApi` 模块。该模块只负责 HTTP、鉴权、参数校验、OpenAPI、文件下载和错误映射，不引用 WPF 或具体厂商 Provider。桌面端实现 `ILocalTtsGateway`，把外部请求映射为现有 `TtsRequest` 并调用桌面 `TtsService`。

桌面软件启动时启动 Kestrel，退出时优雅关闭。这样外部 API 与桌面界面使用同一套凭证、注册表、Provider 和输出目录，不再复制厂商实现。

### 不采用：把 `VoiceServiceMcp` 作为子进程

该方案仍需要管理两个进程、两套配置和两个生命周期，并且必须先重写已经分叉的 MCP Provider。它不能从根本上保证桌面端和 API 能力一致。

### 不采用：在 WPF 项目中直接堆叠 Minimal API

该方案文件较少，但会把 HTTP 生命周期、鉴权和接口契约耦合进桌面入口，难以通过无界面的真实 HTTP 测试验证，也不利于后续独立部署。

## 总体架构

```text
Dify / curl / 外部程序
          |
          | HTTP + Bearer Token
          v
VoiceServiceLocalApi (net8.0)
  - REST / OpenAPI
  - 鉴权与请求验证
  - 音频文件安全下载
  - ProblemDetails 错误响应
          |
          | ILocalTtsGateway
          v
DesktopTtsGateway (net8.0-windows)
          |
          v
桌面 TtsService -> Provider 注册表 -> 12 个厂商 API
```

### 模块边界

`VoiceServiceLocalApi`：

- 定义外部 DTO、`ILocalTtsGateway`、`LocalApiOptions` 和网关结果模型。
- 创建和停止 Kestrel Host。
- 实现 Bearer Token 校验、端点、OpenAPI 和统一错误响应。
- 只能通过 `ILocalTtsGateway` 获取厂商能力、音色和生成结果。
- 不读取厂商 API Key，不引用 `VoiceServiceDemo`，不包含任何具体厂商分支。

桌面端：

- `DesktopTtsGateway` 负责外部 DTO 与桌面模型之间的映射。
- 缺省模型、音色、语速、音量和输出格式从 `VendorRegistry` 的能力定义解析。
- 使用桌面 `SettingsService` 中的现有凭证调用 `TtsService`。
- 将生成文件、厂商错误和能力信息转换为 API 网关结果。

Provider 层：

- 引入 `ITtsProvider` 和 `vendorId -> ITtsProvider` 字典，替代 `TtsService` 中连通性、音色刷新和生成的三组 switch。
- `ITtsProvider` 统一暴露厂商 ID、连通性、音色刷新和生成方法；不支持的音色刷新明确返回“不支持”，不再和鉴权失败一起静默变成空列表。
- 方法接收 `CancellationToken`。HTTP Provider 把令牌传入 `HttpClient`；请求断开或软件退出时可以取消仍在进行的上游调用。

## 生命周期与配置

本地 API 配置加入现有 `AppSettings`，由设置页管理：

| 字段 | 默认值 | 行为 |
| --- | --- | --- |
| `Enabled` | `true` | 软件启动后自动启动本地 API |
| `Port` | `5055` | 合法范围 `1024..65535` |
| `AllowRemote` | `false` | `false` 绑定 `127.0.0.1`；`true` 绑定 `0.0.0.0` |
| `AccessToken` | 首次启动随机生成 | 32 字节加密安全随机数的 Base64URL 字符串 |
| `MaxConcurrentRequests` | `2` | API 生成请求的并发上限 |
| `MaxTextLength` | `20000` | API 层允许的最大字符数，Provider 仍可施加更小限制 |

设置页增加“本地 API”卡片，显示运行状态、监听地址、端口、访问模式和 Dify 地址；支持启停、保存端口、开启本机 Docker/局域网访问、复制/重新生成 Token。开启远程访问时明确提示：端口将暴露给本机网络接口，但不会自动修改 Windows 防火墙。

配置变化采用显式“保存并重启 API”，不在文本输入过程中反复重启服务。端口占用或 Host 启动失败不会阻止桌面界面打开；设置页显示具体错误并允许修改端口后重试。软件退出时等待 Host 优雅停止，最长 5 秒。

## 安全设计

- 除 `/health` 和 `/openapi/v1.json` 外，所有 `/api/v1/*` 端点都要求 `Authorization: Bearer <token>`。
- Token 使用固定时间比较，日志不输出 Token、厂商凭证、完整文本或完整上游响应。
- API 请求体不能携带厂商 API Key；调用始终使用桌面设置页保存的凭证。
- 默认仅监听回环地址。Docker 或局域网调用必须由用户显式打开 `AllowRemote`。
- 不启用 `AllowAnyOrigin`。Dify 和 curl 是服务端 HTTP 调用，不依赖浏览器 CORS。
- 请求体上限为 1 MiB，文本不能为空且不得超过配置限制。
- 文件下载只接受 API 生成记录返回的安全文件名；解析后的绝对路径必须位于当前输出目录内，拒绝 `..`、绝对路径和路径分隔符。
- OpenAPI 文档不包含 Token 或厂商凭证，只声明 HTTP Bearer 安全方案。

本设计不把 API 直接暴露到公网。Dify Cloud 需要另行配置 HTTPS 隧道、VPN 或反向代理；该部署必须在本地 Token 之外增加传输加密和边界访问控制。

## REST 接口契约

外部 JSON 字段统一使用 `snake_case`，接口版本固定为 `/api/v1`。

### `GET /health`

匿名健康检查。返回服务状态、版本和 UTC 时间，不探测付费厂商 API。

```json
{
  "status": "healthy",
  "version": "1.0.0",
  "timestamp": "2026-07-14T00:00:00Z"
}
```

### `GET /api/v1/vendors`

返回桌面注册表中的全部厂商、模型、默认值、参数范围、支持的输入/输出格式和表达能力。响应不包含凭证内容，只返回 `configured: true|false`。

### `GET /api/v1/vendors/{vendor}/voices?refresh=false`

返回内置、缓存或在线音色。`refresh=true` 时主动调用支持在线音色库的 Provider；不支持刷新、凭证无效和上游失败使用不同的错误代码。

### `POST /api/v1/tts`

生成一次语音并返回 JSON 元数据和受 Token 保护的下载地址，适合 Dify OpenAPI 工具和普通 HTTP 编排。

请求：

```json
{
  "vendor": "openai",
  "text": "欢迎使用 VoiceOps 本地语音服务。",
  "voice_id": "alloy",
  "model_id": "gpt-4o-mini-tts",
  "speed": 1.0,
  "volume": 1.0,
  "input_format": "text",
  "style": "",
  "style_degree": 1.0,
  "emotion": "",
  "emotion_intensity": 100,
  "output_format": "mp3",
  "resource_id": "",
  "instructions": "用自然、清晰的语气朗读"
}
```

只有 `vendor`、`text` 和 `voice_id` 必填。省略模型、语速、音量或输出格式时，由桌面注册表选择该厂商的默认值。`input_format` 只接受 `text` 或 `ssml`；厂商不支持的参数返回校验错误，而不是静默忽略。

成功响应：

```json
{
  "request_id": "01JZVOICEOPS00000000000001",
  "vendor": "openai",
  "model_id": "gpt-4o-mini-tts",
  "voice_id": "alloy",
  "format": "mp3",
  "content_type": "audio/mpeg",
  "size_bytes": 48231,
  "generated_at": "2026-07-14T00:00:00Z",
  "audio_url": "http://127.0.0.1:5055/api/v1/audio/openai_20260714_000000_123_a1b2c3d4.mp3"
}
```

`audio_url` 以当前 API 配置的公开基地址生成，不盲目信任任意 Host 请求头。回环模式使用 `127.0.0.1`；远程模式使用请求中已验证的 Host，文档同时提示 Docker 使用 `host.docker.internal` 替换主机名。

### `POST /api/v1/tts/audio`

请求体与 `/api/v1/tts` 相同，但成功时直接返回音频字节，并设置真实的 `Content-Type`、`Content-Length` 和安全的下载文件名。该端点适合希望把 HTTP 响应直接作为文件处理的 Dify HTTP Request 节点。

### `GET /api/v1/audio/{file_name}`

下载 `/api/v1/tts` 生成的音频文件。要求 Bearer Token，执行输出目录边界检查，并根据扩展名返回真实 MIME 类型。

### `GET /openapi/v1.json`

返回可导入 Dify 自定义工具的 OpenAPI 3 文档，包含 Bearer 安全定义、DTO schema、错误 schema 和调用示例。OpenAPI 由端点元数据生成，依赖版本固定，不使用通配符包版本。

## 生成文件与并发

所有桌面 Provider 统一使用一个输出路径生成器。文件名格式为：

```text
{vendor}_{yyyyMMdd_HHmmss_fff}_{8位随机后缀}.{extension}
```

生成器清理厂商和扩展名中的非法字符，并使用 `FileMode.CreateNew` 或等价的唯一性保证，消除同秒并发覆盖。格式与扩展名由 Provider 的实际输出决定，不由 API 猜测。

API 使用 `SemaphoreSlim` 按 `MaxConcurrentRequests` 限制生成并发；超过并发上限的请求等待可用槽位，并响应客户端取消。首版沿用用户现有输出目录和文件保留策略，不自动删除历史文件。

## 错误模型

错误统一使用 `application/problem+json`，至少包含：

```json
{
  "type": "https://voiceops.local/problems/provider_error",
  "title": "TTS provider request failed",
  "status": 502,
  "code": "provider_error",
  "detail": "厂商返回了可安全展示的错误信息。",
  "request_id": "01JZVOICEOPS00000000000001"
}
```

状态码映射：

| HTTP | `code` | 场景 |
| --- | --- | --- |
| 400 | `validation_error` | 缺少字段、范围错误、厂商不支持所选能力 |
| 401 | `unauthorized` | Token 缺失或错误 |
| 404 | `vendor_not_found` / `file_not_found` | 未知厂商或文件不存在 |
| 409 | `concurrency_limit` | 服务正在停止或无法再接受生成请求 |
| 422 | `credential_not_configured` | 桌面端未配置目标厂商凭证 |
| 502 | `provider_error` | 上游厂商拒绝、返回无效数据或下载失败 |
| 504 | `provider_timeout` | 上游调用超时 |
| 500 | `internal_error` | 未预期内部错误 |

返回给客户端的错误会去除凭证、授权头和过长的上游响应；完整异常只写本地诊断日志，并仍执行脱敏。

## Dify 使用方式

支持两种集成：

1. 导入 `http://<host>:5055/openapi/v1.json` 作为 Dify 自定义工具，并配置 Bearer Token。
2. 使用 HTTP Request 节点调用 `/api/v1/tts` 获取 `audio_url`，或调用 `/api/v1/tts/audio` 直接得到文件响应。

本机 Docker 中的 Dify 使用 `http://host.docker.internal:5055`，并在 VoiceOps 设置中打开“本机 Docker/局域网访问”。局域网其他机器使用运行 VoiceOps 的主机 IP。Dify Cloud 不可直接访问用户电脑的 `localhost`。

## 测试策略

新增标准 xUnit 项目 `VoiceServiceLocalApi.Tests`，并保留现有控制台自检。测试分层如下：

### 单元测试

- 请求字段、范围、厂商能力和最大文本长度验证。
- Token 生成、解析、固定时间比较和未授权响应。
- MIME 类型映射、唯一文件名及输出目录边界检查。
- 外部 DTO 到桌面 `TtsRequest` 的默认值和表达参数映射。
- Provider 注册字典拒绝重复 ID，未知厂商返回明确结果。

### HTTP 集成测试

使用 ASP.NET Core TestServer 和 fake `ILocalTtsGateway` 调用真实端点：

- `/health` 和 `/openapi/v1.json` 可访问且不泄露 Token。
- 受保护端点拒绝缺失或错误 Token。
- 厂商与音色端点返回预期 schema。
- `/api/v1/tts` 返回可下载 URL 和正确元数据。
- `/api/v1/tts/audio` 返回真实字节、MIME 和文件名。
- 下载端点拒绝路径穿越。
- 验证错误、未配置凭证、Provider 失败和超时映射为正确 ProblemDetails。
- 两个并发生成得到不同文件且内容不互相覆盖。

### 桌面集成与回归

- 现有 12 个 Provider 的 fake HTTP 自检继续通过。
- API Host 可以在随机空闲端口启动、响应并在取消后停止。
- 设置页包含 API 状态、端口、远程访问和 Token 控件。
- `dotnet test VoiceServiceLocalApi.Tests/VoiceServiceLocalApi.Tests.csproj` 通过。
- `dotnet run --project VoiceServiceDemo.Tests/VoiceServiceDemo.Tests.csproj --no-restore` 通过。
- `dotnet build VoiceServiceDemo.slnx --no-restore` 达到 0 警告、0 错误。
- 启动桌面软件后用 `curl.exe` 完成健康检查、鉴权失败检查以及至少一个已配置厂商的真实生成；没有可用付费凭证时，用 fake 网关 Kestrel 测试作为自动化证据，并在文档中明确真实厂商验证未执行。

## 交付拆分

每个实现批次都遵循“先写失败测试、确认失败、最小实现、确认全量通过、更新 TODO、中文提交、推送 GitHub”：

1. 统一 Provider 接口、取消令牌和唯一输出文件名。
2. 创建 Local API 契约、鉴权、验证和标准测试项目。
3. 实现厂商、音色、JSON 生成、二进制生成、文件下载和 OpenAPI 端点。
4. 实现桌面网关、生命周期和设置持久化。
5. 完成本地 API 设置界面、Dify 使用文档和运行时冒烟验证。
6. 本地 API 稳定后，按独立规格选择并接入下一批 TTS 厂商。

## 非目标

- 本轮不部署公网服务，不内置 HTTPS 证书或第三方隧道。
- 本轮不允许请求携带临时厂商 API Key。
- 本轮不实现用户账号、多租户、计费或跨设备同步。
- 本轮不自动删除用户已经生成的音频文件。
- 本轮不把 MCP transport 当作 Dify 的主要接入方式；现有 MCP 保留，后续可改为调用同一网关。

## 验收标准

满足以下全部条件才视为本地 API 完成：

- API 随桌面软件启动和退出，启动失败不拖垮桌面界面。
- 外部请求可以使用桌面已配置的任意已接入厂商生成语音。
- JSON URL 与直接二进制两种结果模式均通过真实 HTTP 集成测试。
- Dify 可导入 OpenAPI 文档，或通过 HTTP Request 节点按文档完成调用。
- 付费端点必须通过 Bearer Token；默认不暴露到局域网。
- 并发生成不会覆盖输出文件，下载不能逃逸输出目录。
- 新旧自动化测试和解决方案构建全部通过且无警告。
- TODO、README 和 Dify 使用文档与实际行为一致。
- 每个实现批次都有中文 Git 提交并已经推送到 GitHub。
