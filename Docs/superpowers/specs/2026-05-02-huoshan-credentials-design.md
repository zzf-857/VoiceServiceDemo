# Huoshan Credentials Simplification Design

## Goal

Simplify the Huoshan TTS setup so daily speech generation only requires `AppID` and `Access Token`.

## Current Behavior

The settings page asks for five Huoshan fields in one row: `AppID`, `Access Token`, `Cluster`, `AK`, and `SK`. The generation API only needs `AppID` and `Access Token`; `Cluster` is optional and already inferred for BigTTS voices. `AK` and `SK` are only needed when refreshing the full voice library through `ListBigModelTTSTimbres`.

## Design

The desktop settings page will show only `AppID` and `Access Token` by default. A Huoshan advanced area will expose optional `Cluster`, `Access Key`, and `Secret Key` fields for users who need non-default clusters or full voice-list refresh.

The stored value remains backward compatible with the existing pipe-delimited format:

```text
AppID|AccessToken|Cluster|AK|SK
```

New code will parse missing optional fields safely, preserve old saved values, and format new values without trailing empty fields. Generation and connectivity checks will require only `AppID` and `Access Token`. Full voice refresh will use `AK/SK` when present and otherwise return an empty list, allowing the UI to fall back to built-in or cached voices.

## Files

- `Helpers/HuoshanCredentials.cs`: shared parser and formatter for desktop UI/service code.
- `Components/Pages/Settings.razor`: two-field default UI plus optional advanced fields.
- `Services/TtsService.cs`: replace ad hoc `Split('|')` parsing with the helper.
- `VoiceServiceMcp/Core/TtsService.cs`: mirror parsing behavior for the MCP service.
- `VoiceServiceDemo.Tests`: lightweight console tests for parser behavior.

## Verification

Run the parser tests and build both desktop and MCP projects.
