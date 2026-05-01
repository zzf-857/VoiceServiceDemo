# Workspace Main Stage Redesign Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make voice selection the primary workspace area by removing the nested voice-list scroll and letting the outer page handle scrolling.

**Architecture:** Keep vendor APIs, audio generation, and settings storage unchanged. Refactor only `Workspace.razor` markup and `wwwroot/css/app.css` layout rules so the page flows vertically: compact header, full voice stage, generation panel, result panel.

**Tech Stack:** .NET 8, WPF BlazorWebView, Razor components, CSS.

---

### Task 1: Workspace Markup

**Files:**
- Modify: `Components/Pages/Workspace.razor`

- [ ] Replace nested header/body/footer semantics with a single scrolling workspace surface.
- [ ] Move model selection, refresh, and documentation controls into a compact top action row.
- [ ] Make the voice browser a full-width main section that naturally expands with its card grid.
- [ ] Move selected voice, emotion, sliders, text input, readiness, and actions into a generation panel below the voice browser.

### Task 2: Workspace CSS

**Files:**
- Modify: `wwwroot/css/app.css`

- [ ] Remove fixed-height workspace rules that force the middle voice area to shrink.
- [ ] Remove internal scrolling from `.voice-card-grid`.
- [ ] Add full-page scroll rules and roomy card-grid spacing.
- [ ] Add responsive compact rules that reduce spacing while preserving the voice stage.

### Task 3: Verify

**Files:**
- Build: `VoiceServiceDemo.csproj`

- [ ] Run `dotnet build VoiceServiceDemo.csproj`.
- [ ] Confirm there are 0 errors.
