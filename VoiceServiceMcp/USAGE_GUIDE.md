# VoiceServiceMcp 使用指南

## 📋 目录

1. [快速开始](#快速开始)
2. [配置 API Keys](#配置-api-keys)
3. [启动服务器](#启动服务器)
4. [网络配置](#网络配置)
5. [Mac 客户端配置](#mac-客户端配置)
6. [使用示例](#使用示例)
7. [故障排查](#故障排查)

---

## 🚀 快速开始

### 第 1 步：配置环境变量

1. 复制环境变量模板：
```bash
copy .env.example .env
```

2. 编辑 `.env` 文件，填写你的 API Keys：

```bash
# 火山引擎（生成语音必填，格式: AppID|AccessToken）
# V3 / 全量音色库高级格式: AppID|AccessToken|Cluster|AK|SK|V3ApiKey|ResourceId
HUOSHAN_API_KEY=123456|your_access_token

# 其他厂商（可选）
OPENAI_API_KEY=sk-xxx
ALIYUN_API_KEY=xxx
```

**火山引擎 API Key 获取方式**：
- AppID + AccessToken: [语音技术控制台 -> 应用管理](https://console.volcengine.com/speech/service/8)
- AK + SK: [API 访问密钥](https://console.volcengine.com/iam/keymanage)（仅刷新全量音色库时需要）

### 第 2 步：启动服务器

**方式 1：使用启动脚本（推荐）**
```bash
start.bat
```

**方式 2：手动启动**
```bash
dotnet run
```

服务器将在 `http://0.0.0.0:5000` 启动。

### 第 3 步：配置防火墙

允许端口 5000 入站连接：

```powershell
New-NetFirewallRule -DisplayName "MCP TTS Service" -Direction Inbound -LocalPort 5000 -Protocol TCP -Action Allow
```

### 第 4 步：获取 Windows IP 地址

```powershell
ipconfig
```

查找 "IPv4 地址"，例如 `192.168.1.100`。

---

## 🔑 配置 API Keys

### 火山引擎（Volcengine）

生成语音格式：`AppID|AccessToken`

V3 / 全量音色库高级格式：`AppID|AccessToken|Cluster|AK|SK|V3ApiKey|ResourceId`

示例：
```
HUOSHAN_API_KEY=123456|abc123token
```

**参数说明**：
- `AppID`: 应用 ID
- `AccessToken`: 访问令牌
- `Cluster`: 可选，集群名称（标准 TTS 默认 `volcano_tts`，BigTTS 自动留空）
- `V3ApiKey`: 可选，新版控制台 API Key；填写后数据面优先使用 `X-Api-Key`
- `ResourceId`: 可选，默认 `seed-tts-2.0`，1.0 音色可填 `seed-tts-1.0`
- `AK`: 可选，Access Key ID（仅用于获取全量音色列表）
- `SK`: 可选，Secret Access Key（仅用于获取全量音色列表）

### OpenAI

格式：`sk-xxx`

```
OPENAI_API_KEY=sk-proj-xxxxxxxxxxxxx
```

### 其他厂商

参考 `.env.example` 文件中的格式说明。

---

## 🌐 网络配置

### 确保 Windows 和 Mac 在同一局域网

1. 检查 Windows IP：
```powershell
ipconfig
```

2. 在 Mac 上测试连通性：
```bash
ping 192.168.1.100
```

3. 测试 HTTP 连接：
```bash
curl http://192.168.1.100:5000/health
```

预期响应：
```json
{
  "status": "healthy",
  "timestamp": "2024-03-21T10:00:00Z"
}
```

---

## 💻 Mac 客户端配置

### OpenClaw / Claude Desktop

1. 找到配置文件位置（通常是以下之一）：
   - `~/.config/openclaw/mcp.json`
   - `~/.config/claude/mcp.json`
   - `~/Library/Application Support/Claude/mcp.json`

2. 编辑配置文件，添加 MCP 服务器：

```json
{
  "mcpServers": {
    "volcengine-tts": {
      "url": "http://192.168.1.100:5000/mcp/sse",
      "transport": "sse"
    }
  }
}
```

**重要**：将 `192.168.1.100` 替换为你的 Windows PC 的实际 IP 地址。

3. 重启 OpenClaw/Claude Desktop

4. 验证连接：在聊天中询问 AI：
```
请列出可用的 TTS 工具
```

---

## 📝 使用示例

### 示例 1：生成语音

在 OpenClaw/Claude 中：

```
请使用火山引擎的"温柔小雅"音色，将以下文本转换为语音：
"你好，欢迎使用语音合成服务。"
```

AI 将调用 `generate_tts` 工具，返回音频文件的下载链接。

### 示例 2：查看可用音色

```
请列出火山引擎所有可用的音色
```

AI 将调用 `list_voices` 工具，返回音色列表。

### 示例 3：测试连接

```
请测试火山引擎 API 的连接状态
```

AI 将调用 `test_connection` 工具，验证 API Key 是否有效。

---

## 🔧 故障排查

### 问题 1：服务器启动失败

**症状**：运行 `dotnet run` 后报错

**解决方案**：
1. 检查 `.env` 文件是否存在
2. 检查端口 5000 是否被占用：
```powershell
netstat -ano | findstr :5000
```
3. 如果被占用，修改 `.env` 中的 `PORT` 变量

### 问题 2：Mac 无法连接到服务器

**症状**：`curl` 命令超时或连接被拒绝

**解决方案**：
1. 确认防火墙规则已添加
2. 检查 Windows 防火墙是否阻止了连接：
   - 打开 "Windows Defender 防火墙"
   - 点击 "允许应用通过防火墙"
   - 确保 "dotnet.exe" 或 "VoiceServiceMcp.exe" 被允许
3. 尝试临时关闭防火墙测试（不推荐长期使用）

### 问题 3：API Key 无效

**症状**：工具调用返回 "鉴权失败" 或 "未配置 API Key"

**解决方案**：
1. 检查 `.env` 文件中的 API Key 格式是否正确
2. 确认 API Key 没有过期
3. 重启服务器以重新加载环境变量
4. 查看服务器启动日志，确认 API Key 已加载：
```
配置的 API Keys:
  火山引擎: ✓ 已配置
  OpenAI: ✗ 未配置
```

### 问题 4：生成的音频无法访问

**症状**：返回的 `file_url` 无法下载

**解决方案**：
1. 检查 `output` 目录是否存在
2. 确认文件确实生成了（查看 `output` 目录）
3. 尝试直接访问：`http://192.168.1.100:5000/audio/文件名.mp3`

### 问题 5：火山引擎无法获取音色列表

**症状**：`list_voices` 返回默认音色，而非完整列表

**解决方案**：
1. 确认 API Key 包含了 AK 和 SK（第 4 和第 5 个参数）
2. 格式示例：`AppID|Token|Cluster|AK|SK`
3. 检查 AK/SK 是否有 `speech_saas_prod` 服务的权限

---

## 🎯 高级配置

### 修改端口

编辑 `.env` 文件：
```bash
PORT=8080
```

### 修改输出目录

编辑 `.env` 文件：
```bash
OUTPUT_DIR=D:/TTS_Output
```

### 发布为单文件可执行程序

```bash
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

输出文件：`bin/Release/net8.0/win-x64/publish/VoiceServiceMcp.exe`

可以直接运行，无需安装 .NET 运行时。

---

## 📞 获取帮助

如果遇到问题：

1. 查看服务器日志（控制台输出）
2. 检查 `.env` 配置
3. 使用 `test-api.http` 文件测试 API 端点
4. 参考 `README.md` 了解更多信息

---

## 🔒 安全提示

- ⚠️ 仅在受信任的局域网内使用
- ⚠️ 不要将服务暴露到公网
- ⚠️ 定期更新 API Keys
- ⚠️ 考虑使用 VPN 或 SSH 隧道加密传输
