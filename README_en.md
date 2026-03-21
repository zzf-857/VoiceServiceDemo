# VoiceOps (AI Voice Service Demo) 🎙️

<div align="center">

[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows-lightgrey.svg)]()

**A unified, multi-vendor Text-To-Speech (TTS) integration and testing platform.**

**🇺🇸 English** | [🇨🇳 简体中文 (Chinese Version)](README.md)

</div>

---

## Introduction
**VoiceOps** is a comprehensive TTS (Text-to-Speech) demonstration application built with .NET 8 and Blazor. It aims to solve the fragmentation of integrating different AI voice services by providing a highly unified interface while natively implementing the complex signature verification algorithms for each cloud provider.

## Key Features
* **Multi-Vendor Support**: Out-of-the-box support for API integrations including OpenAI, Microsoft Azure, Google Cloud, Tencent Cloud, Volcengine, Baidu, and Aliyun CosyVoice.
* **Native Signature Generation**: Implements request signature algorithms natively in C# (e.g., Tencent's `TC3-HMAC-SHA256`, Volcengine's custom HMAC-v1) without requiring heavy official SDKs.
* **Dynamic Parameter Binding**: Automatically shifts speed (`Speed`) and volume (`Volume`) limits, steps, and default values based on the specific constraints of the currently selected vendor.
* **Online Voice Fetching**: Allows online querying of all supported voice models (including BigTTS multi-emotional models) with local JSON caching to reduce redundant network calls.
* **Base64 Audio Player**: Converts generated MP3/WAV files to base64 and plays them directly inside the Blazor WebView alongside dynamic component updates.

## Getting Started
1. **Prerequisites**: Install [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).
2. **Build and Run**:
   ```bash
   git clone <your-repo-url>
   cd VoiceServiceDemo
   dotnet build
   dotnet run
   ```
3. **Configuration**: Launch the app, navigate to the **Settings** menu, and input the respective `API Key` or `SecretId|SecretKey` pairs for the vendors you wish to test.

## Folder Structure
```text
VoiceServiceDemo/
 ├── Components/     # Blazor frontend views and interactive components (Workspace, Settings, etc.)
 ├── Helpers/        # Native custom signature generators for cloud vendors (TencentSigner, VolcengineSigner, etc.)
 ├── Models/         # Entities and configuration interfaces (VendorConfig, TtsRequest/Result, VoiceOption)
 ├── Services/       # Core TTS business layer (TtsService adapts all vendor requests and responses)
 ├── notbook/        # Developer integration notes
 └── wwwroot/        # Static resources, CSS styles, and audio interop JS
```

## License
This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
