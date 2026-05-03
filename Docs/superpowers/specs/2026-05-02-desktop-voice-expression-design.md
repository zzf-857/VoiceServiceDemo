# Desktop Voice Expression Design

## Scope

This pass improves the Windows desktop app only. MCP behavior, MCP schemas, and MCP provider parity are intentionally out of scope until the desktop workflow is stable.

## Goals

- Make voice selection remain the center of the workspace flow.
- Expose expressive controls only when they can be mapped to the selected desktop provider.
- Add practical SSML support for Azure Speech.
- Add practical emotion/style support for Volcengine voices where the selected voice or built-in mapping supports it.
- Keep plain text generation as the default for every provider.

## Provider Behavior

- Azure uses SSML as its request body. The app builds valid SSML from plain text, speed, volume, voice, and optional speaking style. Advanced users can switch to raw SSML and edit the full markup.
- Volcengine uses provider request parameters rather than generic SSML. The app maps selected emotion/style values into the V3 request body.
- Aliyun keeps the existing text flow for this pass and can later add instruction text for qwen3/cosyvoice.
- Tencent keeps the existing text flow for this pass and can later expose provider-specific style parameters after they are verified.
- OpenAI and Google remain plain text for this pass.

## UI Behavior

- The generation panel includes an expression section.
- Azure shows input mode controls: plain text or SSML. Plain text mode can add a style wrapper; SSML mode sends the raw SSML editor content.
- Volcengine shows emotion/style chips when supported. If online voice metadata contains emotion entries, those are used first; otherwise a small built-in emotion preset is offered for known expressive Volcengine voices.
- Providers without expression support do not show inactive controls.

## Data Model

`TtsRequest` gains optional expression fields:

- `InputFormat`: plain text or SSML.
- `Style`: provider-specific style key.
- `StyleDegree`: Azure style intensity.
- `Emotion`: provider-specific emotion key.
- `Role`: Azure role key, reserved for future UI.
- `SsmlText`: raw SSML content for Azure advanced mode.

## Testing

The existing console self-test project covers:

- Azure SSML is built with escaped text, selected voice, speed, volume, and `mstts:express-as` when style is present.
- Raw SSML mode preserves the provided SSML body.
- Volcengine V3 request body includes `emotion` only when an emotion is selected.

