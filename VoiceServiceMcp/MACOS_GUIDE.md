# macOS 部署指南

## 🍎 在 macOS 上运行 VoiceServiceMcp

好消息！这个项目基于 .NET 8，完全支持 macOS，无需修改代码。

---

## 📋 前置要求

### 1. 安装 .NET 8 SDK

**方式 A：使用 Homebrew（推荐）**
```bash
brew install dotnet@8
```

**方式 B：官方安装包**
下载并安装：https://dotnet.microsoft.com/download/dotnet/8.0

**验证安装**
```bash
dotnet --version
# 应显示: 8.0.x
```

---

## 🚀 快速开始

### 第 1 步：配置环境变量

```bash
# 复制模板
cp .env.example .env

# 编辑配置文件
nano .env
# 或使用你喜欢的编辑器: vim, code, etc.
```

填写 API Keys：
```bash
# 火山引擎（格式: AppID|AccessToken|Cluster|AK|SK）
HUOSHAN_API_KEY=123456|your_token|volcano_tts|your_ak|your_sk

# OpenAI
OPENAI_API_KEY=sk-xxx

# 服务器配置
PORT=5000
OUTPUT_DIR=./output
```

### 第 2 步：启动服务器

**方式 A：使用启动脚本（推荐）**
```bash
# 添加执行权限
chmod +x start.sh

# 运行
./start.sh
```

**方式 B：直接运行**
```bash
# 加载环境变量
export $(grep -v '^#' .env | xargs)

# 启动服务器
dotnet run
```

### 第 3 步：验证服务

打开新终端窗口：
```bash
curl http://localhost:5000/health
```

预期响应：
```json
{
  "status": "healthy",
  "timestamp": "2024-03-21T10:00:00Z"
}
```

---

## 🌐 网络配置

### 获取 Mac 的 IP 地址

```bash
# 方式 1：使用 ifconfig
ifconfig | grep "inet " | grep -v 127.0.0.1

# 方式 2：使用系统偏好设置
# 系统偏好设置 -> 网络 -> 查看 IP 地址
```

记录你的局域网 IP，例如：`192.168.1.200`

### 配置防火墙（如果需要）

macOS 默认防火墙通常允许入站连接，但如果遇到问题：

1. 打开 "系统偏好设置" -> "安全性与隐私" -> "防火墙"
2. 点击 "防火墙选项"
3. 添加 `dotnet` 到允许列表

---

## 💻 本地使用（同一台 Mac）

如果你想在同一台 Mac 上使用（不需要跨设备），配置更简单：

### 配置 MCP 客户端

编辑 MCP 配置文件：
- Claude Desktop: `~/Library/Application Support/Claude/claude_desktop_config.json`
- 或其他客户端的配置文件

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

**注意**：使用 `localhost` 而不是 IP 地址。

---

## 🔧 开发模式

### 热重载开发

```bash
dotnet watch run
```

代码修改后会自动重启服务器。

### 查看日志

服务器日志会直接输出到终端。

---

## 📦 生产部署

### 发布为单文件可执行程序

```bash
# macOS (Intel)
dotnet publish -c Release -r osx-x64 --self-contained -p:PublishSingleFile=true

# macOS (Apple Silicon / M1/M2/M3)
dotnet publish -c Release -r osx-arm64 --self-contained -p:PublishSingleFile=true
```

输出位置：
```
bin/Release/net8.0/osx-x64/publish/VoiceServiceMcp
# 或
bin/Release/net8.0/osx-arm64/publish/VoiceServiceMcp
```

### 运行发布的程序

```bash
# 添加执行权限
chmod +x bin/Release/net8.0/osx-arm64/publish/VoiceServiceMcp

# 运行
./bin/Release/net8.0/osx-arm64/publish/VoiceServiceMcp
```

---

## 🔄 作为后台服务运行

### 使用 launchd（macOS 系统服务）

1. 创建 plist 文件：
```bash
nano ~/Library/LaunchAgents/com.voiceservice.mcp.plist
```

2. 添加以下内容：
```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>Label</key>
    <string>com.voiceservice.mcp</string>
    <key>ProgramArguments</key>
    <array>
        <string>/usr/local/share/dotnet/dotnet</string>
        <string>run</string>
    </array>
    <key>WorkingDirectory</key>
    <string>/path/to/VoiceServiceMcp</string>
    <key>EnvironmentVariables</key>
    <dict>
        <key>HUOSHAN_API_KEY</key>
        <string>your_api_key_here</string>
        <key>PORT</key>
        <string>5000</string>
    </dict>
    <key>RunAtLoad</key>
    <true/>
    <key>KeepAlive</key>
    <true/>
    <key>StandardOutPath</key>
    <string>/tmp/voiceservice-mcp.log</string>
    <key>StandardErrorPath</key>
    <string>/tmp/voiceservice-mcp-error.log</string>
</dict>
</plist>
```

3. 加载服务：
```bash
launchctl load ~/Library/LaunchAgents/com.voiceservice.mcp.plist
```

4. 管理服务：
```bash
# 启动
launchctl start com.voiceservice.mcp

# 停止
launchctl stop com.voiceservice.mcp

# 卸载
launchctl unload ~/Library/LaunchAgents/com.voiceservice.mcp.plist
```

---

## 🐳 使用 Docker（可选）

### 创建 Dockerfile

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /app
COPY bin/Release/net8.0/publish/ .

EXPOSE 5000

ENTRYPOINT ["dotnet", "VoiceServiceMcp.dll"]
```

### 构建和运行

```bash
# 构建镜像
docker build -t voiceservice-mcp .

# 运行容器
docker run -d \
  -p 5000:5000 \
  -e HUOSHAN_API_KEY="your_key" \
  -e PORT=5000 \
  --name voiceservice-mcp \
  voiceservice-mcp
```

---

## 🔍 故障排查

### 问题 1：端口被占用

```bash
# 查看端口占用
lsof -i :5000

# 杀死占用进程
kill -9 <PID>

# 或修改 .env 中的 PORT
```

### 问题 2：权限问题

```bash
# 确保脚本有执行权限
chmod +x start.sh

# 确保输出目录可写
chmod 755 output/
```

### 问题 3：.NET 未找到

```bash
# 检查 .NET 安装
which dotnet

# 如果未找到，添加到 PATH
export PATH="/usr/local/share/dotnet:$PATH"

# 永久添加（添加到 ~/.zshrc 或 ~/.bash_profile）
echo 'export PATH="/usr/local/share/dotnet:$PATH"' >> ~/.zshrc
source ~/.zshrc
```

### 问题 4：环境变量未加载

```bash
# 手动加载 .env
export $(grep -v '^#' .env | xargs)

# 验证
echo $HUOSHAN_API_KEY
```

---

## 📊 性能优化

### 使用 Release 模式

```bash
dotnet run -c Release
```

### 限制内存使用

```bash
# 设置最大堆大小（例如 512MB）
export DOTNET_GCHeapHardLimit=0x20000000
dotnet run
```

---

## 🔒 安全建议

1. **不要将 .env 文件提交到 Git**
   ```bash
   # 已在 .gitignore 中配置
   ```

2. **使用环境变量而非硬编码**
   ```bash
   # 好的做法
   export HUOSHAN_API_KEY="xxx"
   
   # 避免在代码中硬编码
   ```

3. **限制网络访问**
   ```bash
   # 仅监听本地
   export ASPNETCORE_URLS="http://localhost:5000"
   ```

---

## 📝 macOS 特定注意事项

### 1. 文件路径
- macOS 使用 `/` 作为路径分隔符（已兼容）
- 输出目录默认为 `./output`（跨平台）

### 2. 权限
- macOS 可能需要授予网络权限
- 首次运行时可能弹出防火墙提示，选择"允许"

### 3. Apple Silicon (M1/M2/M3)
- .NET 8 原生支持 ARM64
- 使用 `osx-arm64` 运行时标识符

### 4. 系统集成
- 可以使用 launchd 作为系统服务
- 可以添加到 Dock 或 Spotlight

---

## ✅ 验证清单

- [ ] .NET 8 已安装
- [ ] .env 文件已配置
- [ ] 服务器成功启动
- [ ] 健康检查端点可访问
- [ ] 可以生成语音
- [ ] 日志正常输出

---

## 🎯 使用场景

### 场景 1：本地开发
在同一台 Mac 上运行服务器和客户端，使用 `localhost`。

### 场景 2：局域网服务
在 Mac 上运行服务器，其他设备（Mac/Windows/iPad）通过局域网访问。

### 场景 3：远程服务
使用 VPN 或 SSH 隧道，从任何地方访问。

---

## 📞 获取帮助

如遇问题：
1. 查看终端日志
2. 检查 `.env` 配置
3. 参考 `USAGE_GUIDE.md`
4. 使用 `test-api.http` 测试 API

---

## 🎉 完成！

你的 MCP 服务器现在可以在 macOS 上运行了！

**下一步**：
1. 启动服务器：`./start.sh`
2. 配置 MCP 客户端
3. 开始使用 TTS 功能

---

**提示**：macOS 和 Windows 版本使用相同的代码，只是启动脚本不同（`start.sh` vs `start.bat`）。
