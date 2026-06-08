# ElevenLabs TTS Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add ElevenLabs text-to-speech generation and online voice library refresh to the desktop app.

**Architecture:** Add a focused `ElevenLabsTtsProvider` that calls ElevenLabs `POST /v1/text-to-speech/{voice_id}` with `xi-api-key`, writes returned audio bytes directly, and parses `GET /v2/voices` into `VoiceOption`. Register ElevenLabs in the desktop vendor registry, route connectivity/generation/voice fetch through `TtsService`, and reuse existing output-format and speed controls.

**Tech Stack:** .NET 8, WPF + BlazorWebView, `HttpClient`, `System.Text.Json`, local console self-check project.

---

### Task 1: Provider Tests

**Files:**
- Modify: `VoiceServiceDemo.Tests/Program.cs`

- [x] **Step 1: Write failing provider tests**

Cover request JSON, output format normalization, fake audio generation, `xi-api-key`, `/v2/voices` parsing, registry fields, Settings credential copy, and local icon markers.

- [x] **Step 2: Run tests to verify RED**

Run: `dotnet run --project .\VoiceServiceDemo.Tests\VoiceServiceDemo.Tests.csproj`

Observed: compile failure because `ElevenLabsTtsProvider` does not exist yet.

### Task 2: Provider Implementation

**Files:**
- Create: `Services/Providers/ElevenLabsTtsProvider.cs`
- Modify: `Services/TtsService.cs`

- [x] **Step 1: Implement provider**

Use `POST https://api.elevenlabs.io/v1/text-to-speech/{voice_id}?output_format={format}`.
Send `xi-api-key`, JSON body `{ text, model_id, voice_settings }`.
Write returned audio bytes to the configured output directory.

- [x] **Step 2: Route provider**

Instantiate the provider in `TtsService` and add `elevenlabs` branches for connectivity, generation, and voice refresh.

- [x] **Step 3: Run provider tests**

Run: `dotnet run --project .\VoiceServiceDemo.Tests\VoiceServiceDemo.Tests.csproj`

Observed: ElevenLabs provider assertions passed after provider and route implementation; remaining registry/UI/icon assertions were completed in Task 3.

### Task 3: Registry And UI Markers

**Files:**
- Modify: `Services/VendorRegistry.cs`
- Modify: `Components/Pages/Home.razor`
- Modify: `Components/Pages/Settings.razor`
- Create: `wwwroot/assets/vendor-icons/elevenlabs.svg`

- [x] **Step 1: Register vendor**

Add `elevenlabs` in the overseas vendor section with default models, default voices, official docs links, speed support, no volume support, voice fetch enabled, and output formats `mp3_44100_128`, `opus_48000_32`, `pcm_16000`, `ulaw_8000`.

- [x] **Step 2: Add UI markers**

Add ElevenLabs icon mapping in `Home.razor` and `ELEVENLABS_API_KEY` copy in `Settings.razor`.

- [x] **Step 3: Add icon**

Add a local ElevenLabs brand icon asset at `wwwroot/assets/vendor-icons/elevenlabs.svg`.

### Task 4: Verify, Document, Commit

**Files:**
- Modify: `Docs/project/TTS_TOOL_GAP_CHECKLIST_2026-05-02.md`
- Modify: `Docs/superpowers/plans/2026-06-09-elevenlabs-tts.md`

- [x] **Step 1: Run full self-check**

Run: `dotnet run --project .\VoiceServiceDemo.Tests\VoiceServiceDemo.Tests.csproj`

Observed: self-check passed after ElevenLabs provider, registry, UI marker, and icon changes.

- [x] **Step 2: Run solution build**

Run: `dotnet build .\VoiceServiceDemo.slnx`

Observed: solution build passed with 0 warnings and 0 errors.

- [x] **Step 3: Update todo document**

Add a checked 2026-06-09 ElevenLabs TTS record with implemented scope, verification, and remaining advanced voice settings/model refresh scope.

Observed: todo document now includes the checked ElevenLabs integration record.

- [ ] **Step 4: Commit**

Stage only ElevenLabs-related files and commit with message: `接入 ElevenLabs TTS 生成`
