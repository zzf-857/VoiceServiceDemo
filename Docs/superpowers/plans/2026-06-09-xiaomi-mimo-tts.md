# Xiaomi MiMo TTS Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add Xiaomi MiMo V2.5 built-in voice TTS generation to the desktop app.

**Architecture:** Add a focused `XiaomiMimoTtsProvider` that calls the OpenAI-compatible chat completions endpoint and extracts `choices[0].message.audio.data` as base64 audio. Register Xiaomi MiMo in the desktop vendor registry, route `TtsService` connectivity and generation to the provider, and reuse the existing Workspace instructions and output format controls.

**Tech Stack:** .NET 8 WPF + BlazorWebView, `HttpClient`, `System.Text.Json`, existing console smoke tests.

---

### Task 1: Provider Contract Tests

**Files:**
- Modify: `VoiceServiceDemo.Tests/Program.cs`

- [x] **Step 1: Write failing tests**
  - Add tests for MiMo request JSON:
    - `model` is `mimo-v2.5-tts`.
    - `messages` contains a `user` instruction message only when instructions are non-empty.
    - `assistant` message contains the target synthesis text.
    - `audio.voice` maps from `TtsRequest.VoiceId`.
    - `audio.format` supports `wav` and `pcm16`, with invalid formats falling back to `wav`.
  - Add tests for extracting base64 audio from `choices[0].message.audio.data`.
  - Add extension tests: `wav -> .wav`, `pcm16 -> .pcm`, invalid -> `.wav`.

- [x] **Step 2: Run tests and confirm failure**
  - Command: `dotnet run --project .\VoiceServiceDemo.Tests\VoiceServiceDemo.Tests.csproj`
  - Expected: compile failure because `XiaomiMimoTtsProvider` does not exist yet.

### Task 2: Desktop Provider And Routing

**Files:**
- Create: `Services/Providers/XiaomiMimoTtsProvider.cs`
- Modify: `Services/TtsService.cs`

- [x] **Step 1: Implement provider**
  - Endpoint: `https://api.xiaomimimo.com/v1/chat/completions`.
  - Auth header: `api-key: <key>`.
  - Body shape:
    - `model`
    - `messages`
    - `audio.format`
    - `audio.voice`
    - `stream=false`
  - Response parsing: decode `choices[0].message.audio.data`.

- [x] **Step 2: Route from TtsService**
  - Instantiate provider.
  - Add `xiaomi_mimo` cases to connectivity and generation.

- [x] **Step 3: Run tests**
  - Command: `dotnet run --project .\VoiceServiceDemo.Tests\VoiceServiceDemo.Tests.csproj`
  - Expected: Xiaomi provider tests pass.

### Task 3: Vendor Registry And UI Markers

**Files:**
- Modify: `Services/VendorRegistry.cs`
- Modify: `Components/Pages/Workspace.razor`
- Modify: `Components/Pages/Settings.razor`
- Modify: `Components/Pages/Home.razor`
- Modify: `VoiceServiceDemo.Tests/Program.cs`

- [x] **Step 1: Register Xiaomi MiMo**
  - Add vendor id `xiaomi_mimo`.
  - Add default model `mimo-v2.5-tts`.
  - Add built-in voices: `mimo_default`, `冰糖`, `茉莉`, `苏打`, `白桦`, `Mia`, `Chloe`, `Milo`, `Dean`.
  - Declare instructions support and output formats `wav`, `pcm16`.

- [x] **Step 2: Reuse UI controls**
  - Allow instructions controls for `mimo-v2.5-tts`.
  - Add Xiaomi-specific expression hint.
  - Add settings placeholder/help text.
  - Add home icon mapping for `sparkles`.

- [x] **Step 3: Run tests and build**
  - Commands:
    - `dotnet run --project .\VoiceServiceDemo.Tests\VoiceServiceDemo.Tests.csproj`
    - `dotnet build .\VoiceServiceDemo.slnx`
  - Expected: both commands exit 0.

### Task 4: Todo And Commit

**Files:**
- Modify: `Docs/project/TTS_TOOL_GAP_CHECKLIST_2026-05-02.md`

- [x] **Step 1: Update iteration record**
  - Add a checked 2026-06-09 record describing Xiaomi MiMo built-in voice TTS, tests, and remaining voice design/voice clone scope.

- [x] **Step 2: Stage only Xiaomi MiMo files**
  - Avoid staging unrelated existing untracked `openclaw-skills/` work unless it becomes part of a separate task.

- [x] **Step 3: Commit with Chinese message**
  - Commit message: `接入小米 MiMo TTS 生成`
