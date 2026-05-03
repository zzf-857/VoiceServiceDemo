# VoiceServiceMcp 部署检查清单

## 📋 部署前检查

### 1. 环境准备
- [ ] 已安装 .NET 8.0 SDK 或运行时
- [ ] Windows 系统（或其他支持 .NET 的系统）
- [ ] 网络连接正常

### 2. 配置文件
- [ ] 已复制 `.env.example` 为 `.env`
- [ ] 已填写至少一个厂商的 API Key
- [ ] 火山引擎生成语音格式正确（`AppID|AccessToken`）；如需全量音色库，使用 `AppID|AccessToken|Cluster|AK|SK`
- [ ] 端口号未被占用（默认 5000）

### 3. 编译测试
- [ ] 运行 `dotnet build` 成功
- [ ] 无编译错误或警告

---

## 🚀 Windows 部署步骤

### 步骤 1：配置环境变量

```bash
# 复制模板
copy .env.example .env

# 编辑 .env 文件，填写 API Keys
notepad .env
```

### 步骤 2：测试运行

```bash
# 开发模式运行
dotnet run

# 或使用启动脚本
start.bat
```

### 步骤 3：验证服务

打开浏览器访问：
```
http://localhost:5000/health
```

预期响应：
```json
{
  "status": "healthy",
  "timestamp": "2024-03-21T10:00:00Z"
}
```

### 步骤 4：配置防火墙

```powershell
# 以管理员身份运行 PowerShell
New-NetFirewallRule -DisplayName "MCP TTS Service" -Direction Inbound -LocalPort 5000 -Protocol TCP -Action Allow
```

### 步骤 5：获取 IP 地址

```powershell
ipconfig
```

记录 "IPv4 地址"，例如：`192.168.1.100`

---

## 🍎 Mac 客户端配置步骤

### 步骤 1：测试连接

```bash
# 替换为你的 Windows IP
ping 192.168.1.100

# 测试 HTTP 连接
curl http://192.168.1.100:5000/health
```

### 步骤 2：配置 MCP 客户端

找到配置文件（根据你使用的客户端）：
- OpenClaw: `~/.config/openclaw/mcp.json`
- Claude Desktop: `~/.config/claude/mcp.json` 或 `~/Library/Application Support/Claude/mcp.json`

编辑配置文件：
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

### 步骤 3：重启客户端

重启 OpenClaw 或 Claude Desktop。

### 步骤 4：验证工具

在聊天中询问：
```
请列出可用的 TTS 工具
```

预期响应应包含：
- `generate_tts`
- `list_voices`
- `test_connection`

---

## 🔍 故障排查检查清单

### 服务器无法启动
- [ ] 检查 `.env` 文件是否存在
- [ ] 检查端口是否被占用：`netstat -ano | findstr :5000`
- [ ] 检查 .NET 运行时是否安装：`dotnet --version`
- [ ] 查看错误日志

### Mac 无法连接
- [ ] Windows 和 Mac 在同一局域网
- [ ] 防火墙规则已添加
- [ ] IP 地址正确
- [ ] 端口号正确
- [ ] 服务器正在运行

### API Key 无效
- [ ] 格式正确（火山引擎生成语音需要 `AppID|AccessToken`；全量音色库才需要 5 个部分）
- [ ] 没有多余的空格或换行
- [ ] API Key 未过期
- [ ] 重启服务器以重新加载环境变量

### 工具调用失败
- [ ] 参数格式正确
- [ ] 音色 ID 有效
- [ ] 文本内容不为空
- [ ] 网络连接正常
- [ ] 查看服务器日志

---

## 📦 生产部署（可选）

### 发布为单文件可执行程序

```bash
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

输出位置：
```
bin/Release/net8.0/win-x64/publish/VoiceServiceMcp.exe
```

### 部署步骤

1. 复制 `VoiceServiceMcp.exe` 到目标机器
2. 在同目录创建 `.env` 文件
3. 运行 `VoiceServiceMcp.exe`

### 作为 Windows 服务运行（高级）

```powershell
# 安装为服务
sc create VoiceServiceMcp binPath="C:\path\to\VoiceServiceMcp.exe"

# 启动服务
sc start VoiceServiceMcp

# 设置自动启动
sc config VoiceServiceMcp start=auto
```

---

## ✅ 部署完成验证

### Windows 端
- [ ] 服务器成功启动
- [ ] 控制台显示 "服务器启动成功"
- [ ] 显示已配置的 API Keys
- [ ] 健康检查端点可访问

### Mac 端
- [ ] 可以 ping 通 Windows IP
- [ ] 可以访问健康检查端点
- [ ] MCP 配置文件正确
- [ ] 客户端可以列出工具

### 功能测试
- [ ] 可以列出音色
- [ ] 可以测试连接
- [ ] 可以生成语音
- [ ] 可以下载音频文件

---

## 📝 部署记录

**部署日期**：_____________

**Windows IP**：_____________

**端口号**：_____________

**配置的厂商**：
- [ ] 火山引擎
- [ ] OpenAI
- [ ] 阿里云
- [ ] 百度
- [ ] Azure
- [ ] Google

**测试结果**：
- [ ] 健康检查通过
- [ ] Mac 连接成功
- [ ] 工具调用成功
- [ ] 音频生成成功

**备注**：
_________________________________
_________________________________
_________________________________

---

## 🔒 安全检查

- [ ] 仅在受信任的局域网内使用
- [ ] 未将服务暴露到公网
- [ ] API Keys 保存在 `.env` 文件中（未提交到 Git）
- [ ] 考虑使用 VPN 或 SSH 隧道（如果需要）
- [ ] 定期更新 API Keys

---

## 📞 支持

如遇问题，请参考：
1. `USAGE_GUIDE.md` - 详细使用指南
2. `README.md` - 项目说明
3. `PROJECT_SUMMARY.md` - 项目总结
4. 服务器控制台日志

---

**检查清单完成日期**：_____________
**检查人**：_____________
