# Huoshan Credentials Simplification Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reduce Huoshan TTS credential entry to `AppID` and `Access Token` for daily generation while keeping optional full voice refresh credentials.

**Architecture:** Add shared Huoshan parser/protocol helpers and route UI plus desktop/MCP services through them. Keep the existing persisted pipe-delimited value for compatibility, extended for V3 API Key and ResourceId.

**Tech Stack:** .NET 8, WPF Blazor Hybrid, Razor components, console-based parser tests.

---

### Task 1: Credential Parser

**Files:**
- Create: `Helpers/HuoshanCredentials.cs`
- Create: `VoiceServiceShared/HuoshanTtsProtocol.cs`
- Create: `VoiceServiceDemo.Tests/VoiceServiceDemo.Tests.csproj`
- Create: `VoiceServiceDemo.Tests/Program.cs`

- [x] Add tests that assert two-part credentials are valid for generation, optional fields are parsed, and formatting trims trailing empty fields.
- [x] Run `dotnet run --project VoiceServiceDemo.Tests/VoiceServiceDemo.Tests.csproj` and confirm it fails because `HuoshanCredentials` is missing.
- [x] Implement `HuoshanCredentials` with `Parse`, `ToStorageString`, `HasSpeechCredentials`, `HasOpenApiCredentials`, `HasV3Credentials`, `ClusterOrDefault`, and `ResourceIdOrDefault`.
- [x] Run the test command again and confirm it passes.

### Task 2: Desktop Integration

**Files:**
- Modify: `Components/Pages/Settings.razor`
- Modify: `Services/TtsService.cs`

- [x] Replace the five always-visible Huoshan inputs with two default inputs and an advanced details block for `Cluster`, `AK`, `SK`, `V3 API Key`, and `ResourceId`.
- [x] Replace ad hoc Huoshan `Split('|')` logic with `HuoshanCredentials.Parse`.
- [x] Keep generation and connectivity checks requiring only `AppID` and `Access Token`, or V3 API Key in advanced mode.
- [x] Keep full voice fetch requiring only `AK/SK`; otherwise it returns no fetched voices.

### Task 3: MCP And Docs

**Files:**
- Modify: `VoiceServiceMcp/Core/TtsService.cs`
- Modify: `VoiceServiceMcp/README.md`
- Modify: `VoiceServiceMcp/QUICKSTART.md`
- Modify: `VoiceServiceMcp/USAGE_GUIDE.md`
- Modify: `VoiceServiceMcp/DEPLOYMENT_CHECKLIST.md`

- [x] Add a local MCP parser equivalent or shared lightweight parsing logic.
- [x] Update docs to say `HUOSHAN_API_KEY=AppID|AccessToken` is enough for generation.
- [x] Document `AppID|AccessToken|Cluster|AK|SK|V3ApiKey|ResourceId` as the optional advanced format for full voice refresh and V3 explicit configuration.

### Task 4: Verification

- [x] Run `dotnet run --project VoiceServiceDemo.Tests/VoiceServiceDemo.Tests.csproj`.
- [x] Run `dotnet build VoiceServiceDemo.csproj`.
- [x] Run `dotnet build VoiceServiceMcp/VoiceServiceMcp.csproj`.
