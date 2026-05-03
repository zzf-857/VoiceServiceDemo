# VoiceOps (AI Voice Service Demo) 🎙️

<div align="center">

[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows-lightgrey.svg)]()

**一个面向桌面端的多厂商 TTS 音色测试、语气控制与语音生成工作台。**

[🇺🇸 English Version](README_en.md) | **🇨🇳 简体中文**

</div>

---

## 项目定位

VoiceOps 是一个基于 **.NET 8 + WPF + BlazorWebView** 构建的本地桌面端 TTS 工具。它的目标不是只封装一个接口，而是帮助开发者和内容生产者在同一个界面里完成多厂商凭证配置、音色拉取、音色试听、参数调节、语气/SSML 标注和语音生成验证。

当前开发重点已经转向 **桌面端应用体验优先**：MCP 服务端保留在仓库中，但桌面端的音色库、生成流程、凭证配置和语气能力会优先完善，MCP 适配会在桌面端能力稳定后继续跟进。

## 当前能力

### 多厂商 TTS 工作台

| 厂商 | 桌面端状态 | 音色能力 | 语气/SSML |
| --- | --- | --- | --- |
| 火山引擎 | 已接入 | 支持在线/缓存音色，支持 BigTTS 音色元数据 | 支持按音色 emotion 参数选择语气 |
| OpenAI | 已接入 | 内置官方常用音色 | 暂按模型文本指令表达情绪 |
| Microsoft Azure | 已接入 | 支持在线音色列表 | 支持普通文本 speaking style，也支持完整 SSML 输入 |
| 腾讯云 | 已接入 | 内置常用音色，可扩展在线音色 | 暂未做厂商级语气面板 |
| 阿里云 CosyVoice | 已接入 | 使用本地/厂商音色数据 | 暂未做厂商级语气面板 |
| 百度智能云 | 已接入 | 内置基础音色 | 暂未做厂商级语气面板 |
| Google TTS | 已接入 | 内置常用音色 | 暂未做厂商级语气面板 |

### 桌面端交互

- 供应商切换后自动匹配该厂商的语速、音量范围、步长和默认值。
- 支持音色搜索、分页、标签展示、示例音频播放和生成结果回放。
- 支持生成后复制输出路径，并把音频文件保存到本地输出目录。
- 设置页已经改为长期可见字段标签，不再依赖 placeholder 才知道输入框含义。
- 火山引擎和腾讯云凭证已经拆成结构化字段，避免普通用户手写长串 `|` 分隔凭证时填错。
- 凭证输入支持显示/隐藏、字段说明、获取入口和连通性测试。

### 语气与 SSML

- Azure：
  - 普通文本模式下可选择 speaking style，并调节 style degree。
  - SSML 模式下可直接输入完整 `<speak>...</speak>`，适合测试停顿、韵律、角色、风格等高级标注。
- 火山引擎：
  - 对带情感元数据的音色展示可选语气。
  - 对 BigTTS/BV 类音色提供默认语气选项兜底。
  - 生成请求会把选中的 emotion 写入火山协议请求体。

### 厂商协议与签名

- 腾讯云 API 3.0 `TC3-HMAC-SHA256` 请求签名。
- 火山引擎 HMAC-v1 / V3 请求字段构造。
- Azure REST TTS SSML 包装与转义。
- 各厂商请求参数按本地统一模型映射，减少前端直接关心厂商差异。

## 快速开始

### 环境要求

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### 编译和运行

```bash
git clone https://github.com/zzf-857/VoiceServiceDemo.git
cd VoiceServiceDemo
dotnet build VoiceServiceDemo.slnx
dotnet run --project VoiceServiceDemo.csproj
```

如果只想快速启动桌面端，也可以在仓库根目录执行：

```bash
dotnet run
```

### 运行自检

```bash
dotnet run --project VoiceServiceDemo.Tests/VoiceServiceDemo.Tests.csproj --no-restore
dotnet build VoiceServiceDemo.slnx --no-restore
```

当前自检覆盖火山凭证解析、共享火山协议、Azure SSML 构造、主要厂商 provider 边界、运行时资源复制和设置页凭证体验标记。

## 凭证配置

运行软件后进入“系统设置”页面，按厂商填写凭证。设置页会显示每个字段的固定标签和说明，保存后即可进入对应工作区测试音色和生成。

常用格式如下：

| 厂商 | 凭证格式 |
| --- | --- |
| 火山引擎 | 基础生成填写 `AppID` 和 `Access Token`；高级能力可补充 `Cluster`、`AK`、`SK`、`V3 API Key`、`ResourceId` |
| 腾讯云 | `AppID` 可选记录；生成语音需要 `SecretId` 和 `SecretKey` |
| Azure | `subscription_key|region`，例如 `key|eastasia` |
| 百度智能云 | `api_key|secret_key` |
| OpenAI | OpenAI API Key |
| Google | Google Cloud Text-to-Speech API Key |
| 阿里云 | DashScope API Key |

> 说明：当前版本仍以本地配置为主，适合开发和测试环境。若要用于长期生产环境，后续需要继续加强本地密钥加密、权限隔离和团队级凭证管理。

## 使用建议

1. 先在“系统设置”里配置目标厂商凭证，并使用“测试连接”确认凭证可用。
2. 进入目标厂商工作区，优先点击“刷新音色”获取最新音色数据。
3. 选择音色并试听示例音频，确认音色风格。
4. 根据厂商能力调节语速、音量、Azure style 或火山语气。
5. 输入文本或 SSML，点击“生成语音”查看本地音频结果。

## 目录结构

```text
VoiceServiceDemo/
 ├── VoiceServiceDemo.slnx   # 解决方案入口，包含桌面端、MCP、共享库和自检项目
 ├── Components/             # Blazor 前端视图与页面交互组件
 ├── Helpers/                # 桌面端辅助类与凭证解析
 ├── Models/                 # 实体类、厂商配置和统一 TTS 请求模型
 ├── Services/               # 桌面端 TTS 业务服务与厂商 Provider
 ├── VoiceServiceShared/     # 桌面端和 MCP 端共享的协议、凭证、请求构造逻辑
 ├── VoiceServiceMcp/        # MCP 服务端，后续在桌面端稳定后继续适配
 ├── VoiceServiceDemo.Tests/ # 自检项目
 ├── Docs/                   # 项目文档、供应商接入资料、优化清单和实现计划
 ├── data/                   # 供应商原始数据或解析后的静态数据
 ├── scripts/                # 辅助脚本
 ├── assets/                 # 图标、截图等非代码资源
 └── wwwroot/                # 静态资源、CSS 样式、音频互操作 JS
```

更详细的项目地图见 [Docs/project/PROJECT_STRUCTURE.md](Docs/project/PROJECT_STRUCTURE.md)。

## 近期优化方向

当前项目的完整功能缺口清单见 [Docs/project/TTS_TOOL_GAP_CHECKLIST_2026-05-02.md](Docs/project/TTS_TOOL_GAP_CHECKLIST_2026-05-02.md)。优先级较高的方向包括：

- 扩展更多厂商的在线音色拉取和示例音频数据。
- 为阿里云、腾讯云、Google 等厂商补齐可视化语气/风格能力。
- 增强 SSML 编辑体验，例如模板、预览、片段插入和合法性校验。
- 增加生成历史、批量生成、任务队列和失败重试。
- 在桌面端能力稳定后再统一 MCP 的 schema、注册表和厂商能力声明。

## 开源协议

本项目采用 MIT 开源协议，详情请查看 [LICENSE](LICENSE) 文件。
