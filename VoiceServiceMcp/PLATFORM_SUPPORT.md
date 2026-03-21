# 平台支持说明

## 🌍 支持的平台

VoiceServiceMcp 基于 .NET 8 和 ASP.NET Core 构建，**原生支持跨平台**。

| 平台 | 支持状态 | 运行时标识符 | 说明 |
|------|---------|------------|------|
| **Windows** | ✅ 完全支持 | `win-x64` | 原始开发平台 |
| **macOS (Intel)** | ✅ 完全支持 | `osx-x64` | Intel 芯片 Mac |
| **macOS (Apple Silicon)** | ✅ 完全支持 | `osx-arm64` | M1/M2/M3 芯片 Mac |
| **Linux** | ✅ 完全支持 | `linux-x64` | Ubuntu, Debian, CentOS 等 |

---

## 📊 平台差异对比

### 代码兼容性

| 组件 | Windows | macOS | Linux | 说明 |
|------|---------|-------|-------|------|
| **核心 TTS 逻辑** | ✅ | ✅ | ✅ | 100% 兼容 |
| **HTTP 服务器** | ✅ | ✅ | ✅ | ASP.NET Core 跨平台 |
| **文件 I/O** | ✅ | ✅ | ✅ | 自动处理路径分隔符 |
| **环境变量** | ✅ | ✅ | ✅ | 统一接口 |
| **网络通信** | ✅ | ✅ | ✅ | 标准 HTTP/HTTPS |
| **JSON 处理** | ✅ | ✅ | ✅ | System.Text.Json |

### 启动脚本

| 平台 | 脚本文件 | 说明 |
|------|---------|------|
| Windows | `start.bat` | 批处理脚本 |
| macOS/Linux | `start.sh` | Bash 脚本 |

### 防火墙配置

| 平台 | 配置方式 | 是否必需 |
|------|---------|---------|
| Windows | PowerShell 命令 | ✅ 通常需要 |
| macOS | 系统偏好设置 | ⚠️ 可选（默认允许） |
| Linux | `ufw` 或 `iptables` | ⚠️ 取决于发行版 |

---

## 🚀 快速开始（各平台）

### Windows

```powershell
# 1. 配置环境变量
copy .env.example .env
notepad .env

# 2. 启动服务器
start.bat

# 3. 配置防火墙
New-NetFirewallRule -DisplayName "MCP TTS Service" -Direction Inbound -LocalPort 5000 -Protocol TCP -Action Allow
```

### macOS

```bash
# 1. 安装 .NET 8
brew install dotnet@8

# 2. 配置环境变量
cp .env.example .env
nano .env

# 3. 启动服务器
chmod +x start.sh
./start.sh
```

### Linux (Ubuntu/Debian)

```bash
# 1. 安装 .NET 8
wget https://dot.net/v1/dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 8.0

# 2. 配置环境变量
cp .env.example .env
nano .env

# 3. 启动服务器
chmod +x start.sh
./start.sh

# 4. 配置防火墙（如果需要）
sudo ufw allow 5000/tcp
```

---

## 📦 发布（各平台）

### 单文件可执行程序

**Windows (x64):**
```bash
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
# 输出: bin/Release/net8.0/win-x64/publish/VoiceServiceMcp.exe
```

**macOS (Intel):**
```bash
dotnet publish -c Release -r osx-x64 --self-contained -p:PublishSingleFile=true
# 输出: bin/Release/net8.0/osx-x64/publish/VoiceServiceMcp
```

**macOS (Apple Silicon):**
```bash
dotnet publish -c Release -r osx-arm64 --self-contained -p:PublishSingleFile=true
# 输出: bin/Release/net8.0/osx-arm64/publish/VoiceServiceMcp
```

**Linux (x64):**
```bash
dotnet publish -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true
# 输出: bin/Release/net8.0/linux-x64/publish/VoiceServiceMcp
```

### 运行发布的程序

**Windows:**
```powershell
.\VoiceServiceMcp.exe
```

**macOS/Linux:**
```bash
chmod +x VoiceServiceMcp
./VoiceServiceMcp
```

---

## 🔧 平台特定配置

### Windows

**作为 Windows 服务运行:**
```powershell
sc create VoiceServiceMcp binPath="C:\path\to\VoiceServiceMcp.exe"
sc start VoiceServiceMcp
```

### macOS

**作为 launchd 服务运行:**
```bash
# 创建 plist 文件
nano ~/Library/LaunchAgents/com.voiceservice.mcp.plist

# 加载服务
launchctl load ~/Library/LaunchAgents/com.voiceservice.mcp.plist
```

详细配置参考 `MACOS_GUIDE.md`。

### Linux

**作为 systemd 服务运行:**
```bash
# 创建服务文件
sudo nano /etc/systemd/system/voiceservice-mcp.service

# 启动服务
sudo systemctl start voiceservice-mcp
sudo systemctl enable voiceservice-mcp
```

---

## 🐳 Docker 支持（所有平台）

### Dockerfile

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
  --name voiceservice-mcp \
  voiceservice-mcp
```

Docker 容器可以在任何支持 Docker 的平台上运行。

---

## 🌐 网络配置（各平台）

### 获取 IP 地址

**Windows:**
```powershell
ipconfig
# 查找 "IPv4 地址"
```

**macOS:**
```bash
ifconfig | grep "inet " | grep -v 127.0.0.1
# 或
ipconfig getifaddr en0
```

**Linux:**
```bash
ip addr show
# 或
hostname -I
```

### 测试连接

所有平台：
```bash
curl http://localhost:5000/health
```

---

## 📝 平台特定注意事项

### Windows

1. **路径分隔符**：使用 `\`，但代码中使用 `Path.Combine()` 自动处理
2. **防火墙**：通常需要手动配置
3. **权限**：通常不需要管理员权限（除非配置防火墙）

### macOS

1. **路径分隔符**：使用 `/`，代码自动兼容
2. **防火墙**：默认通常允许，可能弹出提示
3. **权限**：可能需要授予网络权限
4. **Apple Silicon**：使用 `osx-arm64` 运行时以获得最佳性能

### Linux

1. **路径分隔符**：使用 `/`，代码自动兼容
2. **防火墙**：取决于发行版（`ufw`, `firewalld`, `iptables`）
3. **权限**：监听 1024 以下端口需要 root 权限（5000 不需要）
4. **依赖**：某些发行版可能需要安装额外的库

---

## ✅ 兼容性验证

### 已测试的环境

| 平台 | 版本 | .NET 版本 | 状态 |
|------|------|----------|------|
| Windows 11 | 23H2 | .NET 8.0 | ✅ 通过 |
| Windows 10 | 22H2 | .NET 8.0 | ✅ 通过 |
| macOS Sonoma | 14.x | .NET 8.0 | ✅ 通过 |
| macOS Ventura | 13.x | .NET 8.0 | ✅ 通过 |
| Ubuntu | 22.04 LTS | .NET 8.0 | ✅ 通过 |
| Debian | 12 | .NET 8.0 | ✅ 通过 |

### 测试清单

- [x] 编译成功
- [x] 服务器启动
- [x] HTTP 端点可访问
- [x] MCP 协议工作正常
- [x] 文件 I/O 正常
- [x] 环境变量加载
- [x] 跨平台路径处理

---

## 🎯 推荐配置

### 开发环境

- **Windows**：Visual Studio 2022 或 VS Code
- **macOS**：VS Code 或 Rider
- **Linux**：VS Code 或 Vim/Emacs

### 生产环境

- **Windows**：Windows Server 2019+ 或 Windows 10/11
- **macOS**：macOS 12+ (Monterey 或更新)
- **Linux**：Ubuntu 20.04+ 或 Debian 11+

### 硬件要求

- **CPU**：1 核心（推荐 2 核心）
- **内存**：512 MB（推荐 1 GB）
- **磁盘**：100 MB（不含音频文件）
- **网络**：局域网或互联网连接

---

## 🔄 迁移指南

### 从 Windows 迁移到 macOS

1. 复制整个项目目录
2. 重新配置 `.env` 文件（路径相同）
3. 使用 `start.sh` 而不是 `start.bat`
4. 无需修改代码

### 从 macOS 迁移到 Linux

1. 复制整个项目目录
2. 确保 `.env` 文件存在
3. 使用 `start.sh`（相同）
4. 配置防火墙（如果需要）

### 跨平台部署

使用 Docker 镜像可以在任何平台上运行，无需考虑平台差异。

---

## 📞 获取帮助

- **Windows 问题**：参考 `README.md` 和 `USAGE_GUIDE.md`
- **macOS 问题**：参考 `MACOS_GUIDE.md`
- **Linux 问题**：参考 `MACOS_GUIDE.md`（大部分相同）
- **通用问题**：参考 `USAGE_GUIDE.md`

---

## 🎉 总结

✅ **代码 100% 跨平台兼容**
- 无需修改核心代码
- 只需使用对应平台的启动脚本

✅ **部署简单**
- 单文件可执行程序
- 或使用 Docker 容器

✅ **功能完全一致**
- 所有平台提供相同的 API
- 相同的 MCP 工具
- 相同的性能

**选择你喜欢的平台，开始使用吧！** 🚀
