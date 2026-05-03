# 🚀 快速开始指南

5 分钟内启动你的 TTS MCP 服务器！

---

## 第 1 步：配置 API Key（2 分钟）

### 方式 A：使用火山引擎（推荐）

1. 复制环境变量模板：
```bash
copy .env.example .env
```

2. 编辑 `.env` 文件，填写火山引擎 API Key：
```bash
HUOSHAN_API_KEY=你的AppID|你的Token
```

**获取方式**：
- AppID + Token: https://console.volcengine.com/speech/service/8
- AK + SK: https://console.volcengine.com/iam/keymanage（仅刷新全量音色库时需要）

### 方式 B：使用 OpenAI

```bash
OPENAI_API_KEY=sk-你的OpenAI密钥
```

---

## 第 2 步：启动服务器（1 分钟）

双击运行：
```
start.bat
```

或手动运行：
```bash
dotnet run
```

看到以下输出表示成功：
```
🚀 VoiceServiceMcp 服务器启动成功！
📡 监听地址: http://0.0.0.0:5000
```

---

## 第 3 步：配置防火墙（1 分钟）

以管理员身份运行 PowerShell：
```powershell
New-NetFirewallRule -DisplayName "MCP TTS Service" -Direction Inbound -LocalPort 5000 -Protocol TCP -Action Allow
```

---

## 第 4 步：获取 IP 地址（30 秒）

```powershell
ipconfig
```

记录 "IPv4 地址"，例如：`192.168.1.100`

---

## 第 5 步：Mac 端配置（30 秒）

编辑 MCP 配置文件（根据你的客户端）：

```json
{
  "mcpServers": {
    "volcengine-tts": {
      "url": "http://你的Windows_IP:5000/mcp/sse",
      "transport": "sse"
    }
  }
}
```

重启客户端。

---

## ✅ 验证

在 Mac 的 AI 客户端中询问：
```
请列出可用的 TTS 工具
```

应该看到 3 个工具：
- `generate_tts`
- `list_voices`
- `test_connection`

---

## 🎉 开始使用

试试这个命令：
```
请使用火山引擎生成一段语音："你好，欢迎使用语音合成服务。"
```

---

## ❓ 遇到问题？

1. **服务器无法启动**
   - 检查 `.env` 文件是否存在
   - 检查端口 5000 是否被占用

2. **Mac 无法连接**
   - 确认防火墙规则已添加
   - 确认 IP 地址正确
   - 尝试：`curl http://你的IP:5000/health`

3. **API Key 无效**
   - 检查格式是否正确
   - 火山引擎生成语音只需要 `AppID|AccessToken`
   - 刷新全量音色库时才需要高级格式 `AppID|AccessToken|Cluster|AK|SK`
   - 重启服务器以重新加载

---

## 📚 更多信息

- 详细使用指南：`USAGE_GUIDE.md`
- 项目说明：`README.md`
- 部署检查清单：`DEPLOYMENT_CHECKLIST.md`

---

**就这么简单！享受你的 TTS MCP 服务吧！** 🎵
