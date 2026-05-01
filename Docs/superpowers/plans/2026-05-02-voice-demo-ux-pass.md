# Voice Demo UX Pass Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the first-run and day-to-day TTS testing flow smoother without changing provider API behavior.

**Architecture:** Keep the existing WPF + Blazor WebView structure. Improve page-level state and UI affordances in `Settings.razor` and `Workspace.razor`, then move repeated inline presentation into `app.css` and one safe JavaScript helper in `index.html`.

**Tech Stack:** .NET 8, WPF BlazorWebView, Razor components, local CSS, small browser JS interop helpers.

---

### Task 1: Settings Focus Flow

**Files:**
- Modify: `Components/Pages/Settings.razor`
- Modify: `wwwroot/css/app.css`

- [ ] Read the `focus` query string on the settings page.
- [ ] Highlight the matching credential row and scroll it into view after first render.
- [ ] Remember where the user came from so saving can route back to the selected vendor workspace when appropriate.
- [ ] Verify by building `VoiceServiceDemo.csproj`.

### Task 2: Workspace First-Run Flow

**Files:**
- Modify: `Components/Pages/Workspace.razor`
- Modify: `wwwroot/css/app.css`

- [ ] For online voice vendors, load cached voices first, otherwise show built-in default voices instead of an empty blocker.
- [ ] Add a small source/status line that distinguishes cached, default, and freshly fetched voice lists.
- [ ] Keep the selected voice valid whenever the voice list changes.
- [ ] Verify by building `VoiceServiceDemo.csproj`.

### Task 3: Accurate Controls And Feedback

**Files:**
- Modify: `Components/Pages/Workspace.razor`
- Modify: `wwwroot/css/app.css`
- Modify: `wwwroot/index.html`

- [ ] Use each vendor's declared slider step values.
- [ ] Hide unsupported parameter controls, especially volume for OpenAI and Aliyun.
- [ ] Replace silent copy/play/open-folder failures with visible status messages.
- [ ] Replace unsafe demo audio `eval` playback with a small `demoAudioInterop` helper.
- [ ] Verify with `dotnet build`.

### Self-Review

- Scope is limited to interaction and feedback polish.
- No provider request payloads or authentication code are changed.
- Build verification is the acceptance check for this pass.
