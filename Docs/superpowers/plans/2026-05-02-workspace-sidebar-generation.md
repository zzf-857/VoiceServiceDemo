# Workspace Sidebar Generation Layout Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move generation settings and playback into a fixed right-side panel so the center workspace focuses on paged voice selection.

**Architecture:** Keep the existing voice fetching and TTS generation code unchanged. Refactor `Workspace.razor` into a two-column layout with a voice stage on the left and a sticky generation sidebar on the right, then update `app.css` for responsive behavior.

**Tech Stack:** .NET 8, WPF BlazorWebView, Razor components, CSS.

---

### Task 1: Layout Structure

**Files:**
- Modify: `Components/Pages/Workspace.razor`

- [ ] Add a `workspace-main-grid` wrapper below the topbar.
- [ ] Put the voice library in the left column.
- [ ] Put generation settings and audio result in a right `generation-sidebar`.

### Task 2: Layout Styling

**Files:**
- Modify: `wwwroot/css/app.css`

- [ ] Add a two-column grid with a 360-400px sidebar.
- [ ] Make the sidebar sticky.
- [ ] Reduce voice-card columns when the sidebar is visible so text remains readable.
- [ ] Collapse to one column on narrow screens.

### Task 3: Verify

**Files:**
- Build: `VoiceServiceDemo.csproj`

- [ ] Run static checks for sidebar CSS and markup.
- [ ] Run `dotnet build VoiceServiceDemo.csproj`.
