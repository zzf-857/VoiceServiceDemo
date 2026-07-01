# Fish Audio TTS Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add Fish Audio text-to-speech generation and online voice model refresh to the desktop app.

**Architecture:** Add a focused `FishAudioTtsProvider` that calls Fish Audio `POST /v1/tts` with Bearer auth and the required `model` header, writes returned audio bytes directly, and parses `GET /model` results into `VoiceOption`. Register Fish Audio in the desktop vendor registry, route generation/connectivity/voice fetch through `TtsService`, and reuse the existing speed, volume, and output format controls.

**Tech Stack:** .NET 8, WPF + BlazorWebView, `HttpClient`, `System.Text.Json`, local console self-check project.

---

### Task 1: Provider Tests

**Files:**
- Modify: `VoiceServiceDemo.Tests/Program.cs`

- [x] **Step 1: Write failing provider tests**

Cover request JSON, output format normalization, fake audio generation, Bearer auth, Fish Audio `model` header, `/model` parsing, registry fields, Settings credential copy, and local icon markers.

- [x] **Step 2: Run tests to verify RED**

Run: `dotnet run --project .\VoiceServiceDemo.Tests\VoiceServiceDemo.Tests.csproj`

Observed: compile failure because `FishAudioTtsProvider` does not exist yet. The run also surfaced an existing nullable warning in `App.xaml.cs`; that warning predates this provider work.

### Task 2: Provider Implementation

**Files:**
- Create: `Services/Providers/FishAudioTtsProvider.cs`
- Modify: `Services/TtsService.cs`

- [x] **Step 1: Implement provider**

Use `POST https://api.fish.audio/v1/tts`.
Send `Authorization: Bearer <token>`, `model: <model id>`, and JSON body with `text`, optional `reference_id`, `format`, `mp3_bitrate`, `opus_bitrate`, `latency`, `normalize`, `temperature`, `top_p`, and `prosody`.
Write returned audio bytes to the configured output directory.

- [x] **Step 2: Route provider**

Instantiate the provider in `TtsService` and add `fish_audio` branches for connectivity, generation, and voice refresh.

- [x] **Step 3: Run provider tests**

Run: `dotnet run --project .\VoiceServiceDemo.Tests\VoiceServiceDemo.Tests.csproj`

Observed: Fish Audio provider assertions passed after provider and route implementation; remaining registry/UI/icon assertions failed until Task 3 is complete.

### Task 3: Registry And UI Markers

**Files:**
- Modify: `Services/VendorRegistry.cs`
- Modify: `Components/Pages/Home.razor`
- Modify: `Components/Pages/Settings.razor`
- Create: `wwwroot/assets/vendor-icons/fish_audio.svg`

- [x] **Step 1: Register vendor**

Add `fish_audio` in the overseas vendor section with default model headers, official example voices, official docs links, speed and volume ranges, voice fetch enabled, and output formats `mp3`, `wav`, `pcm`, `opus`.

- [x] **Step 2: Add UI markers**

Add Fish Audio icon mapping in `Home.razor` and `FISH_AUDIO_API_KEY` copy in `Settings.razor`.

- [x] **Step 3: Add icon**

Add a local Fish Audio brand icon asset at `wwwroot/assets/vendor-icons/fish_audio.svg`.

### Task 4: Verify, Document, Commit, Push

**Files:**
- Modify: `Docs/project/TTS_TOOL_GAP_CHECKLIST_2026-05-02.md`
- Modify: `Docs/superpowers/plans/2026-07-01-fish-audio-tts.md`

- [x] **Step 1: Run full self-check**

Run: `dotnet run --project .\VoiceServiceDemo.Tests\VoiceServiceDemo.Tests.csproj`

Observed: self-check passed after Fish Audio provider, registry, UI marker, and icon changes. The run still reports the existing `App.xaml.cs` nullable warning.

- [x] **Step 2: Run solution build**

Run: `dotnet build .\VoiceServiceDemo.slnx`

Observed: solution build passed with 0 warnings and 0 errors.

- [x] **Step 3: Update todo document**

Add a checked 2026-07-01 Fish Audio TTS record with implemented scope, verification, and remaining streaming/dialogue/instant-clone scope.

Observed: todo document now includes the checked Fish Audio integration record.

- [ ] **Step 4: Commit**

Stage only Fish Audio-related files and commit with message: `接入 Fish Audio TTS 生成`

- [ ] **Step 5: Push**

Run: `git push origin master`
