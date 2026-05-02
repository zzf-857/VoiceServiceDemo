# 阿里云语音合成 (CosyVoice/Qwen-TTS) 从零对接避坑指南

本文档总结了从零开始对接阿里云百炼平台的大模型语音合成（CosyVoice 和 Qwen-TTS 系列）API 时遇到的各种问题及解决方案，供其他开发者参考。

## 1. 准备工作：必须的凭证与链接

在开始写代码前，请确保您已经完成了以下准备工作：

- **产品控制台**: [阿里云百炼大模型服务平台](https://bailian.console.aliyun.com/)
- **API 密钥获取**: 您需要在 [API-KEY 管理页面](https://bailian.console.aliyun.com/?apiKey=1#/api-key) 获取您的凭据。
- **必需的 Keys**:
  - `API-KEY` (如 `sk-xxxxxxxxxxxxxxxx`)：只需要这一个 Key 即可，无需如其他平台那样区分 AppId/SecretId。通过在 HTTP Headers 中设置 `Authorization: Bearer YOUR_API_KEY` 来进行接口鉴权。

---

## 2. 常见“坑”与避坑防线

### 💥 坑 #1：API 端点 (Endpoint) 混乱导致 404 或 url error
阿里云的 TTS 模型非常多，而且新老模型、不同系列的 API 端点和请求体格式并不一致！很容易报 `InvalidParameter: url error` 或 HTTP `404 NotFound`。

**避坑指南**:
- **旧版模型 (如 `qwen-tts`, `cosyvoice-v1`)**：
  - 端点：`/api/v1/services/aigc/text2audio/generation`
  - 请求体格式：`{ "model": "...", "input": { "text": "..." }, "parameters": { "voice": "..." } }`
- **新版模型 (如 `qwen3-tts-instruct-flash` 等 Qwen3-TTS 系列)**：
  - 端点：`/api/v1/services/aigc/multimodal-generation/generation`
  - 请求体格式：`{ "model": "...", "input": { "text": "...", "voice": "..." } }`
  - *注意*：虽然部分文档提及百炼支持 OpenAI 兼容模式 (`/compatible-mode/v1/audio/speech`)，但在某些特定 instruct 模型上使用兼容模式可能会失败报 404 等错误，**强烈建议直接使用阿里云原生 `multimodal-generation` 接口最稳妥**。

### 💥 坑 #2：非流式请求并没有直接返回音频流文件
当你调通了 `multimodal-generation/generation` 接口，你会发现无论怎么将 response body 保存为 mp3/wav，文件都会报损坏或无法播放。

**避坑指南**:
阿里云的非流式（`stream: false`）新版接口，返回的其实是一段 **JSON 字符串**，而不是直接的二进制音频流！
- **处理流程**：调用接口 $\rightarrow$ 解析响应 JSON $\rightarrow$ 提取 `output.audio.url` $\rightarrow$ **发起第二次 HTTP GET 请求去下载该 URL 的音频文件** $\rightarrow$ 保存为本地文件。
- **返回 JSON 示例**：
  ```json
  {
      "output": {
          "audio": {
              "url": "http://dashscope-result-bj.oss-cn-beijing.aliyuncs.com/..."
          }
      }
  }
  ```

### 💥 坑 #3：找不到获取全量“系统音色列表”的 API 接口
其他厂商（如火山、腾讯）通常提供一个 `DescribeVoices` 的接口来拉取所有可用的音色列表，但在阿里云百炼的官方 OpenAPI 中，**目前并没有公开提供获取系统音色列表的接口**。

**避坑指南**:
- **解决方案**：开发者只能手动从 [阿里云百炼语音合成体验中心](https://bailian.console.aliyun.com/cn-beijing?tab=model#/efm/model_experience_center/voice) 的网页端抓包。
  - 打开浏览器开发者工具 (F12) $\rightarrow$ Network $\rightarrow$ 刷新体验中心页面 $\rightarrow$ 找到返回音色列表的内部 API 请求。
  - 把返回的完整 JSON 复制并保存到本地项目中（例如存为 `aliyun_voices_raw.json`）。
  - 在代码里通过读取本地 JSON 文件的方式来动态加载音色下拉框，并解析出 `ttsVoiceConfig.voice` 字段作为调用接口时传的 VoiceId。

### 💥 坑 #4：Quota 免费额度耗尽 (`AllocationQuota.FreeTierOnly`)
调用时频繁返回：`The free tier of the model has been exhausted. If you wish to continue access the model on a paid basis, please disable the "use free tier only" mode...`

**避坑指南**:
- 阿里云上的部分模型分为“稳定版”（缩写，如 `qwen3-tts-instruct-flash`）和带日期的“快照版”（如 `qwen3-tts-instruct-flash-2026-01-26`）。
- 你的体验免费额度往往只绑定在特定的**快照版模型**上。如果在代码里填了短名字的稳定版，可能会被认为是付费调用从而报错。请在模型体验中心仔细核对你当前账号拥有免费额度的是哪个具体的模型 ID（带有日期后缀），并在代码中强制指定使用该模型 ID。

---

## 3. 官方调试工具推荐

如果您对接口参数仍有疑问，推荐使用阿里云官方工具：
- **[API Explorer (OpenAPI 门户)](https://next.api.aliyun.com/)**：可以自动生成各个语言的 SDK 代码。但请注意由于 `DashScope` (百炼) 平台迭代较快，很多新功能并未完全接入旧版的普通云产品 SDK 体系中。如果遇到 SDK 依赖问题或跑不通，**直接参考文档中的 HTTP cURL 请求方式，手写 HttpClient 调用是最简单直接的**。

---

## 4. 必备参考资料与推荐网址大全

- **[开通 DashScope 并获取 API Key](https://help.aliyun.com/zh/model-studio/get-api-key)**
  大模型平台账号、API Key 准备全流程。

- **[千问语音合成 (Qwen-TTS) 产品说明](https://help.aliyun.com/zh/model-studio/qwen-tts)**
  最新的千问语音合成介绍，包含各个模型的适用场景、音色特点及最佳实践。

- **[千问语音合成 API 参考文档](https://help.aliyun.com/zh/model-studio/qwen-tts-api)**
  **极其重要极其核心！** 包含 `qwen3-tts` 系列非流式/流式调用的 HTTP 请求/返回参数详解（特别是本文中强调的 `multimodal-generation` 接口）。

- **[CosyVoice 语音合成 (旧版) API 参考](https://help.aliyun.com/zh/model-studio/developer-reference/cosyvoice)**
  若你需要使用老版本的 CosyVoice 或普通 qwen-tts 模型，可参考此文档，参数结构为 `$input.text` 和 `$parameters.voice`。

- **[百炼大模型计费说明与免费额度](https://help.aliyun.com/zh/model-studio/new-free-quota)**
  查阅你选择的 TTS 模型是否在免费层范围内，以及字符计算和扣费规则，防止产生预期外扣费。
