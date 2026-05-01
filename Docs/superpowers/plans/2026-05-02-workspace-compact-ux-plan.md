# Workspace Compact UX Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Keep the voice browsing and preview area usable in small windows while adding clearer workspace action feedback.

**Architecture:** Keep the existing Blazor WebView pages and service APIs unchanged. Adjust `Workspace.razor` state/rendering for readiness, filtering, and demo preview feedback, and move layout resilience into `wwwroot/css/app.css`.

**Tech Stack:** .NET 8, WPF BlazorWebView, Razor components, CSS.

---

### Task 1: Prove Current Layout Risk

**Files:**
- Inspect: `Components/Pages/Workspace.razor`
- Inspect: `wwwroot/css/app.css`

- [ ] Run static checks showing the workspace still forces hidden overflow, the voice body has no non-zero minimum height, and the voice grid relies on `height: 100%`.

```powershell
Select-String -Path Components/Pages/Workspace.razor -Pattern "overflow: hidden"
Select-String -Path wwwroot/css/app.css -Pattern "min-height: clamp\(220px|flex: 1 1 auto|workspace-compact-hint"
```

Expected before implementation: the first command finds the hidden overflow rule; the second command finds no compact layout safeguards.

### Task 2: Improve Workspace Feedback

**Files:**
- Modify: `Components/Pages/Workspace.razor`

- [ ] Add filter result count and a clear-filters action.
- [ ] Add loading state for sample voice preview so the play button does not look like playback started while audio is still downloading.
- [ ] Add a compact generation readiness line near the action buttons explaining whether the user needs text, a selected voice, or can generate.
- [ ] Add a friendly error wrapper that gives a short next step while keeping technical details available.

### Task 3: Fix Compact Layout

**Files:**
- Modify: `Components/Pages/Workspace.razor`
- Modify: `wwwroot/css/app.css`

- [ ] Change workspace page overflow from hidden to vertical scrolling.
- [ ] Give `.ws-body` and `.voice-card-grid` stable minimum heights so the middle voice browsing area cannot collapse to zero.
- [ ] Add compact-height CSS rules that reduce spacing and textarea height before the voice area is squeezed.

### Task 4: Verify

**Files:**
- Build: `VoiceServiceDemo.csproj`

- [ ] Re-run the static checks and confirm compact layout safeguards are present.
- [ ] Run `dotnet build VoiceServiceDemo.csproj`.

Expected after implementation: static checks find the new safeguards, and build completes with 0 errors.
