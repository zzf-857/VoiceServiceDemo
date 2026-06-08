# MiniMax TTS Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add MiniMax synchronous HTTP TTS generation and online voice library refresh to the desktop app.

**Architecture:** Add a focused `MiniMaxTtsProvider` that calls MiniMax T2A HTTP (`/v1/t2a_v2`) with Bearer API key auth, requests non-streaming hex audio, decodes `data.audio`, writes the generated audio file, and parses `/v1/get_voice` results into `VoiceOption`. Register MiniMax in the desktop vendor registry and route generation/connectivity/voice fetch through `TtsService`.

**Tech Stack:** .NET 8 WPF + BlazorWebView, `HttpClient`, `System.Text.Json`, existing console smoke tests.

---

### Task 1: Provider Contract Tests

**Files:**
- Modify: `VoiceServiceDemo.Tests/Program.cs`

- [x] **Step 1: Write failing tests**
  - Add request JSON tests for `model`, `text`, `stream=false`, `language_boost=auto`, `output_format=hex`, `voice_setting`, and `audio_setting`.
  - Add hex response parsing and output extension tests.
  - Add fake HTTP generation test for endpoint, Bearer header, request body, decoded file bytes.
  - Add `get_voice` parser and fake fetch tests.

- [x] **Step 2: Run tests and confirm failure**
  - Command: `dotnet run --project .\VoiceServiceDemo.Tests\VoiceServiceDemo.Tests.csproj`
  - Observed: compile failure because `MiniMaxTtsProvider` did not exist yet.

### Task 2: Desktop Provider And Routing

**Files:**
- Create: `Services/Providers/MiniMaxTtsProvider.cs`
- Modify: `Services/TtsService.cs`

- [x] **Step 1: Implement provider**
  - Endpoint: `https://api.minimax.io/v1/t2a_v2`.
  - Auth header: `Authorization: Bearer <key>`.
  - Body shape: non-streaming T2A request with hex output and stable audio settings.
  - Response parsing: decode hex from `data.audio`.

- [x] **Step 2: Implement voice fetch**
  - Endpoint: `https://api.minimax.io/v1/get_voice`.
  - Request body: `{ "voice_type": "all" }`.
  - Parse `system_voice`, `voice_cloning`, and `voice_generation`.

- [x] **Step 3: Route from TtsService**
  - Instantiate provider.
  - Add `minimax` cases to connectivity, generation, and voice fetch.

### Task 3: Vendor Registry And UI Markers

**Files:**
- Modify: `Services/VendorRegistry.cs`
- Modify: `Components/Pages/Home.razor`
- Modify: `Components/Pages/Settings.razor`
- Modify: `Components/Pages/Workspace.razor`
- Add: `wwwroot/assets/vendor-icons/minimax.ico`

- [x] **Step 1: Register MiniMax**
  - Add vendor id `minimax`.
  - Add official model options including `speech-2.8-hd` and `speech-2.8-turbo`.
  - Add default voices and online voice refresh capability.
  - Declare supported output formats `mp3`, `wav`, `flac`, `pcm`.

- [x] **Step 2: Update UI**
  - Add MiniMax credential placeholder/help text.
  - Add MiniMax brand icon mapping.
  - Make voice refresh error text generic for single-key providers.

### Task 4: Verify, Todo, Commit

**Files:**
- Modify: `Docs/project/TTS_TOOL_GAP_CHECKLIST_2026-05-02.md`

- [x] **Step 1: Run tests and build**
  - Commands:
    - `dotnet run --project .\VoiceServiceDemo.Tests\VoiceServiceDemo.Tests.csproj`
    - `dotnet build .\VoiceServiceDemo.slnx`
  - Observed: both commands exit 0; build has 0 warnings/errors.

- [x] **Step 2: Update iteration record**
  - Add a checked 2026-06-09 MiniMax TTS record.

- [x] **Step 3: Commit with Chinese message**
  - Commit message: `接入 MiniMax TTS 生成`
