# VoiceServiceMcp - 多厂商 TTS MCP 服务器

将多个 TTS 厂商（火山引擎、OpenAI、阿里云等）封装为 MCP (Model Context Protocol) 服务，支持跨设备网络调用。

## 🌟 特性

- ✅ **跨平台支持**：Windows、macOS、Linux
- ✅ **多厂商支持**：火山引擎、OpenAI、阿里云、百度、Azure、Google
- ✅ **MCP 协议**：标准 JSON-RPC 2.0 over SSE
- ✅ **跨设备访问**：局域网 HTTP API
- ✅ **完全解耦**：独立于桌面应用
- ✅ **单文件部署**：无需安装运行时

## 📚 文档

- **Windows 用户**：参考本文档
- **macOS 用户**：参考 [MACOS_GUIDE.md](MACOS_GUIDE.md)
- **快速开始**：参考 [QUICKSTART.md](QUICKSTART.md)
- **详细指南**：参考 [USAGE_GUIDE.md](USAGE_GUIDE.md)

## 🚀 快速开始

### 1. 配置环境变量

复制 `.env.example` 为 `.env` 并填写 API 密钥：

**Windows:**
```bash
copy .env.example .env
```

**macOS/Linux:**
```bash
cp .env.example .env
```

编辑 `.env` 文件，填写你的 API Key：

```bash
# 火山引擎（格式: AppID|AccessToken|Cluster|AK|SK）
HUOSHAN_API_KEY=123456|your_token|volcano_tts|your_ak|your_sk

# OpenAI
OPENAI_API_KEY=sk-xxx

# 其他厂商...
```

### 2. 运行服务器

**Windows:**
```bash
start.bat
# 或
dotnet run
```

**macOS/Linux:**
```bash
chmod +x start.sh
./start.sh
# 或
dotnet run
```

服务器将在 `http://0.0.0.0:5000` 启动。

### 3. 配置防火墙

**Windows:**
```powershell
New-NetFirewallRule -DisplayName "MCP TTS Service" -Direction Inbound -LocalPort 5000 -Protocol TCP -Action Allow
```

**macOS:**
通常不需要配置，如有问题请参考 `MACOS_GUIDE.md`

### 4. 在 Mac 上配置 OpenClaw/Claude Desktop

编辑 MCP 配置文件（通常在 `~/.config/claude/mcp.json` 或类似路径）：

**跨设备访问（从 Mac 访问 Windows/另一台 Mac）:**
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

**本地访问（同一台 Mac）:**
```json
{
  "mcpServers": {
    "volcengine-tts": {
      "url": "http://localhost:5000/mcp/sse",
      "transport": "sse"
    }
  }
}
```

将 `192.168.1.100` 替换为你的服务器 IP 地址。

## 📡 MCP 工具列表

### 1. `generate_tts`
生成语音音频文件

**参数**：
- `vendor` (string): 厂商 ID（huoshan, openai, aliyun, baidu, azure, google）
- `voice_id` (string): 音色 ID
- `text` (string): 要转换的文本
- `model_id` (string, 可选): 模型 ID
- `speed` (number, 可选): 语速（默认 1.0）

**返回**：
- `success` (boolean): 是否成功
- `file_path` (string): 生成的音频文件路径
- `file_url` (string): 可访问的 HTTP URL
- `error_message` (string): 错误信息（如果失败）

### 2. `list_voices`
获取指定厂商的可用音色列表

**参数**：
- `vendor` (string): 厂商 ID

**返回**：
- 音色列表数组，每个音色包含：
  - `id`: 音色 ID
  - `name`: 音色名称
  - `gender`: 性别
  - `language`: 语言
  - `is_big_tts`: 是否为 BigTTS（仅火山引擎）

### 3. `test_connection`
测试厂商 API 连接状态

**参数**：
- `vendor` (string): 厂商 ID

**返回**：
- `success` (boolean): 是否连接成功
- `message` (string): 连接状态消息

## 🔧 开发

### 构建

```bash
dotnet build
```

### 发布为单文件可执行程序

**Windows:**
```bash
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

**macOS (Intel):**
```bash
dotnet publish -c Release -r osx-x64 --self-contained -p:PublishSingleFile=true
```

**macOS (Apple Silicon):**
```bash
dotnet publish -c Release -r osx-arm64 --self-contained -p:PublishSingleFile=true
```

**Linux:**
```bash
dotnet publish -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true
```

输出文件：`bin/Release/net8.0/{runtime}/publish/VoiceServiceMcp[.exe]`

## 📁 项目结构

```
VoiceServiceMcp/
├── Core/                    # 核心业务逻辑
│   ├── TtsService.cs       # TTS 服务
│   ├── Models.cs           # 数据模型
│   ├── VendorRegistry.cs   # 厂商配置
│   └── VolcengineSigner.cs # 火山引擎签名
├── McpServer/              # MCP 协议层
│   ├── Program.cs          # 服务器入口
│   ├── McpController.cs    # SSE 端点
│   └── TtsTools.cs         # 工具定义
├── Config/                 # 配置文件
│   └── appsettings.json
└── README.md
```

## 🌐 网络配置

### 获取 Windows IP 地址

```powershell
ipconfig
```

查找 "IPv4 地址"，例如 `192.168.1.100`。

### 测试连接

在 Mac 上测试：

```bash
curl http://192.168.1.100:5000/health
```

## 🔒 安全建议

- 仅在受信任的局域网内使用
- 考虑添加 API Key 认证
- 或使用 VPN/SSH 隧道加密传输
- 不要将服务暴露到公网

## 📝 许可证

MIT
