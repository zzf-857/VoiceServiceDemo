# VoiceOps (AI Voice Service Demo) 🎙️

<div align="center">

[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows-lightgrey.svg)]()

**一个统一的、支持多厂商接入的纯 C# 本地文本转语音 (TTS) 测试平台。**

[🇺🇸 English Version (英文版)](README_en.md) | **🇨🇳 简体中文**

</div>

---

## 简介
**VoiceOps (Voice Service Demo)** 是一个基于 .NET 8 + Blazor 构建的全方位 TTS (文本转语音) 演示与集成平台。本项目旨在解决开发者在对接不同云厂商语音服务时的复杂性，在提供高度统一的 UI 和调用接口的同时，系统内部原生采用纯 C# 实现了各大厂商苛刻的鉴权签名算法。

## 核心特性
* **主流厂商全覆盖**：开箱即用地支持 OpenAI、微软 Azure、Google Cloud、腾讯云、火山引擎 (字节跳动)、百度智能云、阿里云 CosyVoice 等各平台最新语音生成 API。
* **原生脱敏签名机制**：摆脱臃肿的官方 SDK 依赖，利用纯 C# 手写实现复杂的 HTTP 请求签名算法（如腾讯云最新 API 3.0 的 `TC3-HMAC-SHA256` 规范、火山引擎 HMAC-v1 规范）。
* **动态参数自适应映射**：前端双向绑定的滑动调节器。当切换不同的提供商时，自动读取 `VendorRegistry` 切换合法的 `语速/Speed` 和 `音量/Volume` 的上下限、步长及默认值（例如腾讯云整数范围，火山引擎浮点比例，Azure 相对百分比等），杜绝请求 Payload 越界报错。
* **在线音色库与缓存**：支持在线抓取厂商所有支持的音色（含普通版、复刻版及最新的 BigTTS 多情感大模型），提供性别与风格检索，并自动使用本地 JSON 缓存减少跨域请求。
* **流式/Base64内置播放**：将接口抛出的音频文件落盘后，立即提取元数据转为 Base64 抛给前端 WebView 驱动原生音频组件进行无缝播放。

## 快速开始
1. **环境依赖**：请先安装 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)。
2. **编译运行**：
   ```bash
   git clone <您的仓库地址>
   cd VoiceServiceDemo
   dotnet build
   dotnet run
   ```
3. **API 准入配置**：运行软件后，请先进入“系统设置”页面。在对应云厂商下方填入您的身份凭证。
   - 例如腾讯云：直接填入 `SecretId|SecretKey`
   - 火山引擎：填入 `AppID|AccessToken` 等

## 目录结构
```text
VoiceServiceDemo/
 ├── Components/     # Blazor 前端视图与页面交互组件 (Workspace, Settings 等)
 ├── Helpers/        # 原生手写的各云厂商签名生成器 (TencentSigner, VolcengineSigner 等)
 ├── Models/         # 实体类与配置接口 (VendorConfig, TtsRequest/Result, VoiceOption)
 ├── Services/       # TTS 核心业务层 (TtsService 负责适配所有产商请求及响应)
 ├── notbook/        # 开发者对接笔记 (如 Tencent_TTS_Integration_Guide.md)
 └── wwwroot/        # 静态资源、CSS 样式、音频互操作 JS (audioInterop)
```

## 开源协议
本项目采用 MIT 开源协议 - 详情请查看 [LICENSE](LICENSE) 文件。
