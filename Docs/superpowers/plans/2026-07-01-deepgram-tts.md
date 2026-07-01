# Deepgram TTS Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add Deepgram Aura text-to-speech generation and model-derived voice refresh to the desktop app.

**Architecture:** Add a focused `DeepgramTtsProvider` that calls `POST /v1/speak`, sends `Authorization: Token <key>`, maps the selected voice/model into the `model` query parameter, writes returned audio bytes directly, and parses `GET /v1/models` TTS entries into `VoiceOption`. Register Deepgram in the desktop vendor registry, route generation/connectivity/voice fetch through `TtsService`, and reuse the existing speed and output format controls.

**Tech Stack:** .NET 8, WPF + BlazorWebView, `HttpClient`, `System.Text.Json`, local console self-check project.

---

### Task 1: Provider Tests

**Files:**
- Modify: `VoiceServiceDemo.Tests/Program.cs`

- [x] **Step 1: Write failing provider tests**

Cover request JSON, speak URI query parameters, output format normalization, fake audio generation, `Token` auth, `/v1/models` TTS parsing/fetch, registry fields, Settings credential copy, and local icon markers.

- [x] **Step 2: Run tests to verify RED**

Run: `dotnet run --project .\VoiceServiceDemo.Tests\VoiceServiceDemo.Tests.csproj`

Observed: compile failure because `DeepgramTtsProvider` does not exist yet. The run also surfaced the existing nullable warning in `App.xaml.cs`; that warning predates this provider work.

### Task 2: Provider Implementation

**Files:**
- Create: `Services/Providers/DeepgramTtsProvider.cs`
- Modify: `Services/TtsService.cs`

- [x] **Step 1: Implement provider**

Use `POST https://api.deepgram.com/v1/speak`.
Send `Authorization: Token <key>`, JSON body `{ "text": request.Text }`, and query parameters for `model`, `encoding`, optional `container`, and clamped `speed`.
Write returned audio bytes to the configured output directory.

- [x] **Step 2: Route provider**

Instantiate the provider in `TtsService` and add `deepgram` branches for connectivity, generation, and voice refresh.

- [x] **Step 3: Run provider tests**

Run: `dotnet run --project .\VoiceServiceDemo.Tests\VoiceServiceDemo.Tests.csproj`

Observed: Deepgram provider, registry, UI marker, and icon assertions passed after Task 3 implementation.

### Task 3: Registry And UI Markers

**Files:**
- Modify: `Services/VendorRegistry.cs`
- Modify: `Components/Pages/Home.razor`
- Modify: `Components/Pages/Settings.razor`
- Create: `wwwroot/assets/vendor-icons/deepgram.svg`

- [x] **Step 1: Register vendor**

Add `deepgram` in the overseas vendor section with Aura 2 models, official example voices, official docs links, speed support, no volume support, voice fetch enabled, and output formats `mp3`, `wav`, `opus`, `flac`.

- [x] **Step 2: Add UI markers**

Add Deepgram icon mapping in `Home.razor` and `DEEPGRAM_API_KEY` copy in `Settings.razor`.

- [x] **Step 3: Add icon**

Add a local Deepgram brand icon asset at `wwwroot/assets/vendor-icons/deepgram.svg`.

### Task 4: Verify, Document, Commit, Push

**Files:**
- Modify: `Docs/project/TTS_TOOL_GAP_CHECKLIST_2026-05-02.md`
- Modify: `Docs/superpowers/plans/2026-07-01-deepgram-tts.md`

- [x] **Step 1: Run full self-check**

Run: `dotnet run --project .\VoiceServiceDemo.Tests\VoiceServiceDemo.Tests.csproj`

Observed: self-check passed after Deepgram provider, registry, UI marker, and icon changes.

- [x] **Step 2: Run solution build**

Run: `dotnet build .\VoiceServiceDemo.slnx`

Observed: solution build passed with 0 warnings and 0 errors.

- [x] **Step 3: Update todo document**

Observed: todo document now includes the checked Deepgram integration record.

- [ ] **Step 4: Commit**

Stage only Deepgram-related files and commit with message: `接入 Deepgram TTS 生成`

- [ ] **Step 5: Push**

Run: `git push origin master`
