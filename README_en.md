# VoiceOps (AI Voice Service Demo) 🎙️

<div align="center">

[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows-lightgrey.svg)]()

**A Windows desktop workbench for multi-vendor TTS voice testing, expression control, and speech generation.**

**🇺🇸 English** | [🇨🇳 简体中文](README.md)

</div>

---

## Overview

VoiceOps is a local desktop TTS application built with **.NET 8, WPF, and BlazorWebView**. It helps developers and content creators configure multiple speech vendors, fetch and test voices, tune generation parameters, experiment with SSML or emotion controls, and verify generated audio from one unified UI.

The current product direction is **desktop-first**. The MCP server remains in the repository, but voice metadata, generation workflow, credential configuration, and expression controls are being completed in the desktop app before MCP parity work continues.

## Current Capabilities

### Multi-Vendor TTS Workbench

| Vendor | Desktop status | Voice support | Expression / SSML |
| --- | --- | --- | --- |
| Volcengine | Integrated | Online/cached voice metadata, including BigTTS data | Emotion parameter selection per supported voice |
| OpenAI | Integrated | Built-in common voices | Emotion mainly via model text instruction |
| Microsoft Azure | Integrated | Online voice list support | Speaking style controls and raw SSML input |
| Tencent Cloud | Integrated | Built-in voices, extensible online list | Vendor-level expression panel not yet added |
| Aliyun CosyVoice | Integrated | Local/vendor voice data | Vendor-level expression panel not yet added |
| Baidu AI Cloud | Integrated | Built-in basic voices | Vendor-level expression panel not yet added |
| Google TTS | Integrated | Built-in common voices | Vendor-level expression panel not yet added |

### Desktop UX

- Vendor-specific speed and volume limits are applied automatically.
- Voice search, pagination, tags, sample playback, and generated audio playback are available in the workbench.
- Generated files are saved locally and their output path can be copied.
- The settings page now uses persistent field labels instead of placeholder-only hints.
- Volcengine and Tencent credentials are split into structured fields to reduce formatting mistakes.
- Credential fields support show/hide, inline help, documentation links, and connection tests.

### Expression and SSML

- Azure:
  - Plain-text mode supports speaking style and style degree.
  - SSML mode accepts full `<speak>...</speak>` input for advanced prosody, pauses, roles, and style tags.
- Volcengine:
  - Voices with emotion metadata expose selectable emotion options.
  - BigTTS/BV voices get fallback emotion choices.
  - The selected emotion is passed into the Volcengine request payload.

### Vendor Protocols

- Tencent Cloud API 3.0 `TC3-HMAC-SHA256` request signing.
- Volcengine HMAC-v1 / V3 request payload construction.
- Azure REST TTS SSML wrapping and escaping.
- Unified request models for mapping desktop controls into vendor-specific payloads.

## Getting Started

### Requirements

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Build and Run

```bash
git clone https://github.com/zzf-857/VoiceServiceDemo.git
cd VoiceServiceDemo
dotnet build VoiceServiceDemo.slnx
dotnet run --project VoiceServiceDemo.csproj
```

From the repository root, this also launches the desktop app:

```bash
dotnet run
```

### Verification

```bash
dotnet run --project VoiceServiceDemo.Tests/VoiceServiceDemo.Tests.csproj --no-restore
dotnet build VoiceServiceDemo.slnx --no-restore
```

Current checks cover Volcengine credential parsing, shared Volcengine protocol payloads, Azure SSML generation, provider boundary behavior, runtime asset copying, and settings-page credential markup.

## Credential Configuration

Launch the app and open **Settings**. Each vendor row shows persistent labels and short field-level guidance. Save the configuration, then enter the vendor workbench to fetch voices and generate speech.

Common credential formats:

| Vendor | Credential format |
| --- | --- |
| Volcengine | Basic generation uses `AppID` and `Access Token`; advanced fields include `Cluster`, `AK`, `SK`, `V3 API Key`, and `ResourceId` |
| Tencent Cloud | `AppID` is optional for reference; speech generation requires `SecretId` and `SecretKey` |
| Azure | `subscription_key|region`, for example `key|eastasia` |
| Baidu AI Cloud | `api_key|secret_key` |
| OpenAI | OpenAI API key |
| Google | Google Cloud Text-to-Speech API key |
| Aliyun | DashScope API key |

Note: credentials are currently stored as local app configuration, which is appropriate for development and testing. Production use should add stronger local encryption, permission isolation, or team-level secret management.

## Suggested Workflow

1. Configure credentials in Settings and run the connection test.
2. Open the target vendor workbench and refresh the voice list.
3. Choose a voice and play its sample audio when available.
4. Tune speed, volume, Azure style, or Volcengine emotion.
5. Enter plain text or SSML, then generate and review the local audio output.

## Folder Structure

```text
VoiceServiceDemo/
 ├── VoiceServiceDemo.slnx   # Solution entry for desktop app, MCP, shared library, and checks
 ├── Components/             # Blazor views and page interactions
 ├── Helpers/                # Desktop helpers and credential parsers
 ├── Models/                 # Vendor configuration and unified TTS request models
 ├── Services/               # Desktop TTS service and provider implementations
 ├── VoiceServiceShared/     # Shared protocol and request construction logic
 ├── VoiceServiceMcp/        # MCP server; parity work follows desktop stabilization
 ├── VoiceServiceDemo.Tests/ # Self-check project
 ├── Docs/                   # Project docs, vendor notes, gap checklist, and implementation plans
 ├── data/                   # Vendor raw/static voice data
 ├── scripts/                # Helper scripts
 ├── assets/                 # Icons, screenshots, and non-code assets
 └── wwwroot/                # Static assets, CSS, and audio interop JavaScript
```

See [Docs/project/PROJECT_STRUCTURE.md](Docs/project/PROJECT_STRUCTURE.md) for a more detailed project map.

## Roadmap

The current feature gap checklist is tracked in [Docs/project/TTS_TOOL_GAP_CHECKLIST_2026-05-02.md](Docs/project/TTS_TOOL_GAP_CHECKLIST_2026-05-02.md). Near-term priorities include:

- Expanding online voice fetching and sample audio coverage.
- Adding visual expression controls for Aliyun, Tencent, Google, and other vendors.
- Improving SSML editing with templates, snippets, preview, and validation.
- Adding generation history, batch generation, queues, and retry handling.
- Aligning MCP schema, registry, and vendor capability declarations after the desktop app stabilizes.

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE) for details.
