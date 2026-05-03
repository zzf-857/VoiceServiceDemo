# Desktop Voice Expression Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add practical desktop-only expression controls for Azure SSML and Volcengine emotion generation.

**Architecture:** Extend the existing desktop request model with optional expression fields. Keep provider-specific mapping in provider/service helpers so the Blazor page stays focused on selection state and UI behavior.

**Tech Stack:** .NET 8, WPF BlazorWebView, C#, System.Text.Json, existing console self-test project.

---

### Task 1: Add Expression Model And Tests

**Files:**
- Modify: `Models/VendorConfig.cs`
- Modify: `VoiceServiceShared/HuoshanTtsProtocol.cs`
- Modify: `VoiceServiceDemo.Tests/Program.cs`

- [ ] Add `TtsInputFormat`, `TtsExpressionOption`, and optional expression fields to `TtsRequest`.
- [ ] Write failing tests that assert Azure SSML helper behavior and Volcengine emotion JSON behavior.
- [ ] Implement only enough protocol/model code to pass the tests.

### Task 2: Add Azure SSML Builder

**Files:**
- Create: `Services/AzureSsmlBuilder.cs`
- Modify: `Services/TtsService.cs`
- Modify: `VoiceServiceDemo.Tests/Program.cs`

- [ ] Add tests for generated SSML and raw SSML pass-through.
- [ ] Use generated SSML in Azure plain text mode.
- [ ] Use `request.SsmlText` directly in Azure SSML mode.

### Task 3: Add Desktop Expression UI

**Files:**
- Modify: `Components/Pages/Workspace.razor`
- Modify: `wwwroot/css/app.css`

- [ ] Add provider-aware expression controls in the generation sidebar.
- [ ] Show Azure plain/SSML mode and style chips.
- [ ] Show Volcengine emotion chips from selected voice metadata or built-in presets.
- [ ] Pass selected expression fields into `TtsRequest`.

### Task 4: Verify

**Files:**
- No production file changes.

- [ ] Run `dotnet build VoiceServiceDemo.slnx --no-restore`.
- [ ] Run `dotnet run --project VoiceServiceDemo.Tests\VoiceServiceDemo.Tests.csproj --no-build`.
- [ ] Review the desktop app code paths to confirm MCP files were not modified.

