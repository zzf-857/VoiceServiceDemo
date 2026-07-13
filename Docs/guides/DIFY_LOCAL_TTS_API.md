# Dify 调用 VoiceOps 本地 TTS API

VoiceOps 桌面软件会在本机启动一个受 Bearer Token 保护的 REST API。外部程序传入厂商、文本、音色和可选参数后，可以取得带下载地址的 JSON，或直接取得音频字节。API 复用桌面设置中保存的厂商凭证和输出目录，请勿在请求体中传递厂商 API Key。

## 1. 启动与地址选择

在“设置 → 本地 TTS API”中确认：

1. 勾选“启用本地 API”。
2. 保持默认端口 `5055`，或选择 `1024..65535` 的空闲端口。
3. 复制 Bearer Token。
4. 点击“保存并重启 API”，状态应显示为“运行中”。

按调用方所在位置选择地址：

| 调用方 | API 地址 | 设置要求 |
| --- | --- | --- |
| Windows 本机程序、curl | `http://127.0.0.1:5055` | 可保持“允许远程访问”关闭 |
| 同一台 Windows 上的 Dify Docker | `http://host.docker.internal:5055` | 开启“允许 Docker / 局域网访问” |
| 同一局域网的其他设备 | `http://<Windows局域网IP>:5055` | 开启远程访问，并按需配置 Windows 防火墙入站规则 |

开启远程访问会让服务绑定到所有本机网络接口，但软件不会自动修改防火墙。不要把该 HTTP 端口直接映射到公网。

匿名健康检查：

```powershell
curl.exe http://127.0.0.1:5055/health
```

OpenAPI 文档：

```text
http://127.0.0.1:5055/openapi/v1.json
```

## 2. 鉴权

除 `/health` 和 `/openapi/v1.json` 外，所有 `/api/v1/*` 路由都需要：

```http
Authorization: Bearer <设置页显示的 Token>
```

先把 Token 放在当前 PowerShell 进程的环境变量中，避免反复粘贴：

```powershell
$env:VOICEOPS_LOCAL_API_TOKEN = "在此粘贴 Token"
```

验证未鉴权请求返回 HTTP 401：

```powershell
curl.exe -i http://127.0.0.1:5055/api/v1/vendors
```

读取厂商及能力列表：

```powershell
curl.exe http://127.0.0.1:5055/api/v1/vendors `
  -H "Authorization: Bearer $env:VOICEOPS_LOCAL_API_TOKEN"
```

Token 只用于本地 API，不是任一家 TTS 厂商的凭证。点击“重新生成 Token”会保存新 Token、重启 API，并立即使旧 Token 失效。

## 3. 请求参数

最小请求需要 `vendor`、`text` 和 `voice_id`：

```json
{
  "vendor": "openai",
  "text": "你好，这是来自 Dify 的语音测试。",
  "voice_id": "alloy"
}
```

可选字段包括：

- `model_id`
- `speed`、`volume`
- `input_format`：`text` 或厂商支持时使用 `ssml`
- `ssml_text`
- `style`、`style_degree`
- `emotion`、`emotion_intensity`
- `output_format`
- `resource_id`
- `instructions`

先调用 `/api/v1/vendors` 获取每家厂商的默认模型、参数范围、输出格式和表达能力。API 会拒绝目标厂商不支持的字段，而不会静默忽略。

## 4. Dify OpenAPI 自定义工具

适用于希望把 TTS 暴露为 Dify 工具的场景：

1. 在 Dify 打开“工具 → 自定义工具 → 创建自定义工具”。
2. 导入 `http://<Dify可访问的主机>:5055/openapi/v1.json`。
   - Dify Docker 使用 `http://host.docker.internal:5055/openapi/v1.json`。
   - 非容器本机部署可使用 `http://127.0.0.1:5055/openapi/v1.json`。
3. 鉴权类型选择 API Key / Bearer，并填入设置页 Token。
4. 优先使用 `POST /api/v1/tts`。它返回 JSON，适合工作流继续处理。
5. 从响应的 `audio_url` 取得下载地址；下载时同样携带 Bearer Token。

典型 JSON 响应：

```json
{
  "request_id": "0HN...",
  "vendor": "openai",
  "model_id": "tts-1",
  "voice_id": "alloy",
  "format": "mp3",
  "content_type": "audio/mpeg",
  "size_bytes": 12345,
  "generated_at": "2026-07-14T00:00:00+00:00",
  "audio_url": "http://127.0.0.1:5055/api/v1/audio/openai_....mp3"
}
```

`audio_url` 只对当前桌面软件进程登记过的生成文件有效。重启软件后，旧文件仍可能存在于输出目录，但不会继续通过 API 下载端点公开。

## 5. Dify HTTP Request 节点

### JSON + 下载地址模式

配置 HTTP Request 节点：

- Method：`POST`
- URL：`http://host.docker.internal:5055/api/v1/tts`
- Header：`Authorization = Bearer <Token>`
- Header：`Content-Type = application/json`
- Body：JSON，可把 `text` 绑定到上游变量

示例 Body：

```json
{
  "vendor": "openai",
  "model_id": "tts-1",
  "voice_id": "alloy",
  "text": "{{#上游节点.text#}}",
  "speed": 1.0,
  "output_format": "mp3"
}
```

后续节点读取 `audio_url`，再次发起带 Bearer Token 的 GET 请求即可取得文件。

### 直接音频模式

如果当前 Dify 版本的 HTTP Request 节点支持文件/二进制响应，可调用：

```text
POST http://host.docker.internal:5055/api/v1/tts/audio
```

请求头和 JSON Body 与上面相同，响应为 `audio/mpeg`、`audio/wav` 等真实音频内容。若工作流只擅长处理 JSON，使用 `/api/v1/tts` 更稳妥。

## 6. curl.exe 完整示例

生成 JSON 元数据：

```powershell
$body = @'
{
  "vendor": "openai",
  "model_id": "tts-1",
  "voice_id": "alloy",
  "text": "VoiceOps 本地 API 已连接成功。",
  "output_format": "mp3"
}
'@

$body | curl.exe http://127.0.0.1:5055/api/v1/tts `
  -X POST `
  -H "Authorization: Bearer $env:VOICEOPS_LOCAL_API_TOKEN" `
  -H "Content-Type: application/json" `
  --data-binary "@-"
```

把响应中的 `audio_url` 替换到下面命令：

```powershell
curl.exe "http://127.0.0.1:5055/api/v1/audio/<文件名>.mp3" `
  -H "Authorization: Bearer $env:VOICEOPS_LOCAL_API_TOKEN" `
  --output result.mp3
```

直接生成二进制音频：

```powershell
$body | curl.exe http://127.0.0.1:5055/api/v1/tts/audio `
  -X POST `
  -H "Authorization: Bearer $env:VOICEOPS_LOCAL_API_TOKEN" `
  -H "Content-Type: application/json" `
  --data-binary "@-" `
  --output result.mp3
```

## 7. 自动烟测

软件运行后执行：

```powershell
pwsh -NoProfile -File scripts/local_api_smoke.ps1 `
  -BaseUrl http://127.0.0.1:5055 `
  -Token $env:VOICEOPS_LOCAL_API_TOKEN
```

脚本会检查健康状态、401、已鉴权厂商列表和 OpenAPI，且不会打印或持久化 Token。

如已有可计费厂商凭证，可保存上面的 JSON Body 为 `request.json`，再执行真实生成：

```powershell
pwsh -NoProfile -File scripts/local_api_smoke.ps1 `
  -BaseUrl http://127.0.0.1:5055 `
  -Token $env:VOICEOPS_LOCAL_API_TOKEN `
  -RequestFile .\request.json
```

脚本会要求响应非空，并验证 MP3、WAV、Ogg 或 FLAC 文件签名。

## 8. 常见问题

| 现象 | 原因与处理 |
| --- | --- |
| API 状态“启动失败”或提示 address already in use | 端口已被占用；在设置页换一个空闲端口并“保存并重启 API”。 |
| HTTP 401 / `unauthorized` | Bearer Token 缺失、拼写错误或已经重新生成；重新从设置页复制。 |
| HTTP 400 / `validation_error` | 字段为空、超出范围或厂商不支持所选能力；先读取 `/api/v1/vendors`。 |
| HTTP 422 / `credential_not_configured` | 尚未在桌面设置页配置目标厂商凭证。 |
| HTTP 502 / `provider_error` | 上游厂商拒绝请求、额度不足或返回无效数据；先在桌面端执行“测试连通”。 |
| Docker 无法连接 `127.0.0.1` | 容器里的 localhost 是容器自身；启用远程访问并改用 `host.docker.internal`。 |
| 局域网机器无法访问 | 检查远程访问开关、Windows 防火墙、局域网 IP 和端口。 |
| 生成成功但写文件失败 | 检查设置页输出目录是否存在且当前 Windows 用户有写权限。 |
| Dify Cloud 无法访问 | 云端不能直接访问个人电脑的 localhost；需要 HTTPS 隧道、VPN 或带 TLS 的反向代理。 |

## 9. 公网安全

Dify Cloud 场景必须额外配置 HTTPS、来源访问控制和安全隧道/VPN。Bearer Token 不能代替传输加密；明文 HTTP 暴露到公网会泄漏 Token 和语音内容。推荐优先使用自托管 Dify、同机 Docker 或私有网络。
