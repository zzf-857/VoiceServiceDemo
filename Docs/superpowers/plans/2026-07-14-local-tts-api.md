# VoiceOps Local TTS API Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Embed a secured, Dify-friendly local HTTP TTS API in the VoiceOps desktop lifecycle while reusing every desktop provider, credential, capability, and output directory.

**Architecture:** Add a cross-platform `VoiceServiceLocalApi` module that owns HTTP contracts, authentication, validation, OpenAPI, downloads, and Kestrel hosting behind `ILocalTtsGateway`. The Windows desktop implements the gateway over its existing registry and `TtsService`, starts the host with the application, and exposes settings without duplicating provider logic.

**Tech Stack:** .NET 8, WPF + BlazorWebView, ASP.NET Core Minimal APIs/Kestrel, Swashbuckle OpenAPI 3, xUnit, ASP.NET Core TestServer, existing provider fake HTTP handlers.

---

## File map

New focused units:

- `Services/AudioOutputPath.cs`: reserves collision-free output paths for every desktop provider.
- `Services/Providers/ITtsProvider.cs`: desktop provider contracts and voice-catalog result semantics.
- `Services/Providers/TtsProviderRegistry.cs`: validated `vendorId -> provider` lookup.
- `VoiceServiceLocalApi/Contracts.cs`: stable external DTOs and gateway contracts.
- `VoiceServiceLocalApi/LocalApiOptions.cs`: binding, token, concurrency, and validation limits.
- `VoiceServiceLocalApi/LocalApiToken.cs`: secure token generation and constant-time verification.
- `VoiceServiceLocalApi/LocalTtsRequestValidator.cs`: provider-capability-aware request validation.
- `VoiceServiceLocalApi/AudioFileAccess.cs`: MIME mapping and output-directory boundary enforcement.
- `VoiceServiceLocalApi/LocalApiApplication.cs`: service registration, middleware, REST endpoints, and OpenAPI.
- `VoiceServiceLocalApi/LocalApiHost.cs`: production Kestrel start/stop wrapper.
- `Services/DesktopTtsGateway.cs`: mapping between external API DTOs and desktop models/services.
- `Services/DesktopLocalApiService.cs`: desktop configuration, host lifecycle, status, and restart control.
- `VoiceServiceLocalApi.Tests/*`: standard unit and real HTTP integration tests.

Existing files changed by responsibility:

- `Services/Providers/*.cs`: implement provider contracts, accept cancellation, and use the shared path allocator.
- `Services/TtsService.cs`: replace three vendor switches with registry lookup and expose detailed voice refresh results.
- `Models/VendorConfig.cs`: persist `LocalApiSettings` under `AppSettings`.
- `Services/SettingsService.cs`: normalize local API defaults and securely generate a missing token.
- `MainWindow.xaml.cs`: own the service provider and start/stop the local API with the window.
- `Components/Pages/Settings.razor` and `wwwroot/css/app.css`: local API controls and status.
- `VoiceServiceDemo.Tests/Program.cs`: desktop regression and markup checks.
- `VoiceServiceDemo.csproj`, `VoiceServiceDemo.slnx`: API references and new test projects.
- `Docs/project/TTS_TOOL_GAP_CHECKLIST_2026-05-02.md`, `README.md`, `VoiceServiceMcp/README.md`: verified status and Dify instructions.

---

### Task 1: Collision-free audio output paths

**Files:**

- Create: `Services/AudioOutputPath.cs`
- Modify: all 12 files under `Services/Providers/*TtsProvider.cs`
- Modify: `VoiceServiceDemo.Tests/Program.cs`
- Modify: `Docs/project/TTS_TOOL_GAP_CHECKLIST_2026-05-02.md`

- [ ] **Step 1: Write the failing concurrency test**

Add near the start of `VoiceServiceDemo.Tests/Program.cs`:

```csharp
var outputPathTestDir = Path.Combine(Path.GetTempPath(), "VoiceOpsOutputPathTests", Guid.NewGuid().ToString("N"));
var fixedOutputTime = new DateTimeOffset(2026, 7, 14, 8, 9, 10, 123, TimeSpan.Zero);
var firstOutputPath = AudioOutputPath.Reserve(outputPathTestDir, "fish_audio", ".mp3", fixedOutputTime);
var secondOutputPath = AudioOutputPath.Reserve(outputPathTestDir, "fish_audio", ".mp3", fixedOutputTime);
AssertFalse(firstOutputPath == secondOutputPath, "same-millisecond output reservations are unique");
AssertTrue(File.Exists(firstOutputPath), "first output path is atomically reserved");
AssertTrue(File.Exists(secondOutputPath), "second output path is atomically reserved");
AssertTrue(Path.GetFileName(firstOutputPath).StartsWith("fish_audio_20260714_080910_123_"), "output filename keeps vendor and millisecond timestamp");
AssertTrue(firstOutputPath.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase), "output filename keeps normalized extension");
Directory.Delete(outputPathTestDir, recursive: true);
```

- [ ] **Step 2: Run the self-check and verify RED**

Run:

```powershell
dotnet run --project VoiceServiceDemo.Tests/VoiceServiceDemo.Tests.csproj --no-restore
```

Expected: compile failure because `AudioOutputPath` does not exist.

- [ ] **Step 3: Implement atomic path reservation**

Create `Services/AudioOutputPath.cs`:

```csharp
using System.Security.Cryptography;

namespace VoiceServiceDemo.Services;

public static class AudioOutputPath
{
    public static string Reserve(
        string directory,
        string vendorId,
        string extension,
        DateTimeOffset? timestamp = null)
    {
        Directory.CreateDirectory(directory);
        var safeVendor = string.Concat(vendorId.Select(c => char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_'));
        var safeExtension = "." + extension.Trim().TrimStart('.').ToLowerInvariant();
        var time = timestamp ?? DateTimeOffset.Now;

        for (var attempt = 0; attempt < 32; attempt++)
        {
            var suffix = Convert.ToHexString(RandomNumberGenerator.GetBytes(4)).ToLowerInvariant();
            var name = $"{safeVendor}_{time:yyyyMMdd_HHmmss_fff}_{suffix}{safeExtension}";
            var path = Path.Combine(directory, name);
            try
            {
                using var reservation = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                return path;
            }
            catch (IOException) when (File.Exists(path))
            {
            }
        }

        throw new IOException("Unable to reserve a unique audio output path after 32 attempts.");
    }
}
```

Replace each provider's timestamp-only `GetOutputFilePath` body with `AudioOutputPath.Reserve` while retaining that provider's existing format-to-extension method. Use these vendor IDs exactly: `aliyun`, `azure`, `baidu`, `deepgram`, `elevenlabs`, `fish_audio`, `google`, `huoshan`, `minimax`, `openai`, `tencent`, `xiaomi_mimo`.

- [ ] **Step 4: Verify GREEN and regression safety**

Run:

```powershell
dotnet run --project VoiceServiceDemo.Tests/VoiceServiceDemo.Tests.csproj --no-restore
dotnet build VoiceServiceDemo.slnx --no-restore
```

Expected: every self-check passes; build reports 0 warnings and 0 errors.

- [ ] **Step 5: Record the completed slice**

In `Docs/project/TTS_TOOL_GAP_CHECKLIST_2026-05-02.md`, add a checked 2026-07-14 iteration entry with the test evidence and change `P1-17` to checked, stating that millisecond timestamp plus cryptographic suffix and atomic reservation prevent collisions.

- [ ] **Step 6: Commit and push**

```powershell
git add Services/AudioOutputPath.cs Services/Providers VoiceServiceDemo.Tests/Program.cs Docs/project/TTS_TOOL_GAP_CHECKLIST_2026-05-02.md
git commit -m "修复：避免并发生成覆盖音频文件"
git push origin HEAD
```

---

### Task 2: Unified desktop provider registry and cancellation

**Files:**

- Create: `Services/Providers/ITtsProvider.cs`
- Create: `Services/Providers/TtsProviderRegistry.cs`
- Modify: all 12 files under `Services/Providers/*TtsProvider.cs`
- Modify: `Services/TtsService.cs`
- Modify: `VoiceServiceDemo.Tests/Program.cs`
- Modify: `Docs/project/TTS_TOOL_GAP_CHECKLIST_2026-05-02.md`

- [ ] **Step 1: Write failing provider-registry tests**

Add to `VoiceServiceDemo.Tests/Program.cs`:

```csharp
var registrySettings = new SettingsService();
var registryService = new TtsService(registrySettings);
AssertEqual(VendorRegistry.All.Count, registryService.RegisteredProviderIds.Count, "every desktop vendor has one provider registration");
AssertTrue(VendorRegistry.All.All(v => registryService.RegisteredProviderIds.Contains(v.Id)), "provider registry covers all vendor IDs");
var unknownCatalog = await registryService.FetchVoiceCatalogAsync("missing-vendor");
AssertFalse(unknownCatalog.Success, "unknown vendor catalog lookup fails explicitly");
AssertEqual("vendor_not_found", unknownCatalog.ErrorCode, "unknown vendor has stable error code");

var duplicateProvider = new FakeTtsProvider("duplicate");
AssertThrows<ArgumentException>(
    () => new TtsProviderRegistry(new ITtsProvider[] { duplicateProvider, duplicateProvider }),
    "provider registry rejects duplicate IDs");
```

Add an `AssertThrows<TException>` helper and a minimal `FakeTtsProvider` implementing the wished-for interface. Add a `BlockingHttpHandler` test that calls one real Provider with a cancelled token and expects `OperationCanceledException`, proving the token reaches `HttpClient`.

- [ ] **Step 2: Run the self-check and verify RED**

Expected compile failures for `ITtsProvider`, `TtsProviderRegistry`, `RegisteredProviderIds`, and `FetchVoiceCatalogAsync`.

- [ ] **Step 3: Add the provider contracts**

Create `Services/Providers/ITtsProvider.cs`:

```csharp
using VoiceServiceDemo.Models;

namespace VoiceServiceDemo.Services.Providers;

public interface ITtsProvider
{
    string VendorId { get; }
    Task<(bool Success, string Message)> TestConnectivityAsync(string apiKey, CancellationToken cancellationToken = default);
    Task<TtsResult> GenerateAsync(TtsRequest request, string apiKey, CancellationToken cancellationToken = default);
}

public interface IVoiceCatalogProvider
{
    Task<List<VoiceOption>> FetchVoicesAsync(string apiKey, CancellationToken cancellationToken = default);
}

public sealed record VoiceCatalogResult(
    bool Success,
    bool Supported,
    List<VoiceOption> Voices,
    string? ErrorCode = null,
    string? ErrorMessage = null);
```

Create `Services/Providers/TtsProviderRegistry.cs` with a case-insensitive dictionary, duplicate/blank-ID validation, `AllIds`, and `TryGet`.

- [ ] **Step 4: Implement the contracts in all providers**

For every Provider:

- declare `ITtsProvider` and the exact `VendorId` from Task 1;
- add optional `CancellationToken cancellationToken = default` to connectivity and generation;
- implement `IVoiceCatalogProvider` only for Aliyun, Azure, Deepgram, ElevenLabs, Fish Audio, Google, Huoshan, MiniMax, and Tencent;
- pass the token to `SendAsync`, `GetAsync`, `PostAsync`, `ReadAsStringAsync`, `ReadAsByteArrayAsync`, `ReadAsStreamAsync`, `File.WriteAllBytesAsync`, `File.ReadAllTextAsync`, and polling delays used by that operation;
- call `cancellationToken.ThrowIfCancellationRequested()` before CPU-only parsing or synchronous credential work.

Aliyun keeps its local catalog but changes the signature to:

```csharp
public async Task<List<VoiceOption>> FetchVoicesAsync(
    string apiKey,
    CancellationToken cancellationToken = default)
```

The unused `apiKey` preserves the common interface; local JSON reads receive the cancellation token.

- [ ] **Step 5: Replace `TtsService` switches with registry lookup**

Construct the 12 providers once, pass them to `TtsProviderRegistry`, and expose:

```csharp
public IReadOnlyCollection<string> RegisteredProviderIds => _providers.AllIds;

public async Task<VoiceCatalogResult> FetchVoiceCatalogAsync(
    string vendorId,
    CancellationToken cancellationToken = default)
```

`TestConnectivityAsync`, `FetchVoicesAsync`, and `GenerateAsync` use `TryGet`; the legacy `FetchVoicesAsync` remains as a compatibility wrapper returning `result.Voices`. Do not catch `OperationCanceledException`; rethrow it. Convert provider exceptions into explicit `VoiceCatalogResult` or `TtsResult` errors without returning a silent empty list.

- [ ] **Step 6: Verify GREEN**

Run the existing self-check and solution build. Expected: all existing provider request/response assertions plus the new registry and cancellation assertions pass; build has 0 warnings/errors.

- [ ] **Step 7: Update TODO, commit, and push**

Add a checked 2026-07-14 record describing unified registry routing, explicit catalog errors, and cancellation evidence. Do not mark MCP divergence `P3-06` complete because the old MCP transport still exists.

```powershell
git add Services/Providers Services/TtsService.cs VoiceServiceDemo.Tests/Program.cs Docs/project/TTS_TOOL_GAP_CHECKLIST_2026-05-02.md
git commit -m "重构：统一桌面端TTS厂商适配接口"
git push origin HEAD
```

---

### Task 3: Local API contracts, authentication, validation, and tests

**Files:**

- Create: `VoiceServiceLocalApi/VoiceServiceLocalApi.csproj`
- Create: `VoiceServiceLocalApi/Contracts.cs`
- Create: `VoiceServiceLocalApi/LocalApiOptions.cs`
- Create: `VoiceServiceLocalApi/LocalApiToken.cs`
- Create: `VoiceServiceLocalApi/LocalTtsRequestValidator.cs`
- Create: `VoiceServiceLocalApi/AudioFileAccess.cs`
- Create: `VoiceServiceLocalApi.Tests/VoiceServiceLocalApi.Tests.csproj`
- Create: `VoiceServiceLocalApi.Tests/TokenTests.cs`
- Create: `VoiceServiceLocalApi.Tests/RequestValidatorTests.cs`
- Create: `VoiceServiceLocalApi.Tests/AudioFileAccessTests.cs`
- Modify: `VoiceServiceDemo.slnx`
- Modify: `Docs/project/TTS_TOOL_GAP_CHECKLIST_2026-05-02.md`

- [ ] **Step 1: Scaffold only the test project references**

Use pinned dependencies:

```xml
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
<PackageReference Include="xunit" Version="2.9.2" />
<PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
```

`VoiceServiceLocalApi.csproj` targets `net8.0`, references `Microsoft.AspNetCore.App`, and pins `Swashbuckle.AspNetCore` to `6.6.2`. Add both projects to `VoiceServiceDemo.slnx`.

- [ ] **Step 2: Write RED unit tests**

Cover these exact behaviors:

```csharp
[Fact]
public void Generated_token_has_256_bits_and_validates() { /* generated token decodes to 32 bytes; correct succeeds; wrong fails */ }

[Fact]
public void Request_requires_vendor_text_and_voice() { /* returns validation_error fields */ }

[Fact]
public void Request_rejects_unsupported_output_and_expression_controls() { /* capabilities drive errors */ }

[Fact]
public void Request_applies_vendor_defaults() { /* nullable model/speed/volume/format become registry defaults */ }

[Fact]
public void Audio_path_rejects_parent_and_absolute_paths() { /* ../, ..\\ and rooted names fail */ }

[Theory]
[InlineData("result.mp3", "audio/mpeg")]
[InlineData("result.wav", "audio/wav")]
[InlineData("result.flac", "audio/flac")]
[InlineData("result.opus", "audio/ogg")]
[InlineData("result.pcm", "application/octet-stream")]
public void Content_type_matches_extension(string file, string expected) { }
```

Run `dotnet test VoiceServiceLocalApi.Tests/VoiceServiceLocalApi.Tests.csproj`; expected compile failures because production types do not exist.

- [ ] **Step 3: Implement stable contracts**

`Contracts.cs` defines:

```csharp
public sealed record LocalTtsRequest(
    string Vendor,
    string Text,
    string VoiceId,
    string? ModelId = null,
    double? Speed = null,
    double? Volume = null,
    string InputFormat = "text",
    string? Style = null,
    double? StyleDegree = null,
    string? Emotion = null,
    int? EmotionIntensity = null,
    string? OutputFormat = null,
    string? ResourceId = null,
    string? Instructions = null,
    string? SsmlText = null);

public sealed record LocalTtsGatewayResult(
    bool Success,
    string? FilePath,
    string Vendor,
    string ModelId,
    string VoiceId,
    DateTimeOffset GeneratedAt,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public interface ILocalTtsGateway
{
    Task<IReadOnlyList<LocalVendorInfo>> GetVendorsAsync(CancellationToken cancellationToken);
    Task<LocalVoiceCatalog> GetVoicesAsync(string vendor, bool refresh, CancellationToken cancellationToken);
    Task<LocalTtsGatewayResult> GenerateAsync(LocalTtsRequest request, CancellationToken cancellationToken);
    string OutputDirectory { get; }
}
```

Also define `LocalVendorInfo`, `LocalModelInfo`, `LocalVoiceInfo`, `LocalVendorCapabilities`, `LocalParameterDefinition`, and `LocalVoiceCatalog` with all fields required by the design spec. Public JSON DTO properties must be serializable with snake_case policy and contain no credential value.

- [ ] **Step 4: Implement options, token, validation, and file safety**

`LocalApiOptions` validates port `1024..65535`, positive concurrency, max text `1..20000`, and a non-empty token. `LocalApiToken.Generate()` returns 32 random bytes as Base64URL. `Matches()` hashes supplied/expected UTF-8 bytes with SHA-256 and uses `CryptographicOperations.FixedTimeEquals`.

`LocalTtsRequestValidator` returns a normalized request or field errors. It rejects unsupported input/output/style/emotion/instructions instead of silently discarding them.

`AudioFileAccess.Resolve(outputDirectory, fileName)` rejects rooted paths and any separator, combines only `Path.GetFileName(fileName)`, resolves full paths, and confirms the result starts with the normalized output directory plus a directory separator using `OrdinalIgnoreCase` on Windows.

- [ ] **Step 5: Verify GREEN**

```powershell
dotnet test VoiceServiceLocalApi.Tests/VoiceServiceLocalApi.Tests.csproj
dotnet run --project VoiceServiceDemo.Tests/VoiceServiceDemo.Tests.csproj --no-restore
dotnet build VoiceServiceDemo.slnx --no-restore
```

Expected: all unit tests and existing self-checks pass; 0 build warnings/errors.

- [ ] **Step 6: Update TODO, commit, and push**

Record the checked API contract/security slice with exact test counts.

```powershell
git add VoiceServiceLocalApi VoiceServiceLocalApi.Tests VoiceServiceDemo.slnx Docs/project/TTS_TOOL_GAP_CHECKLIST_2026-05-02.md
git commit -m "功能：建立本地API安全契约"
git push origin HEAD
```

---

### Task 4: Real HTTP endpoints and OpenAPI

**Files:**

- Create: `VoiceServiceLocalApi/LocalApiApplication.cs`
- Create: `VoiceServiceLocalApi/LocalApiHost.cs`
- Create: `VoiceServiceLocalApi/LocalApiProblem.cs`
- Create: `VoiceServiceLocalApi.Tests/FakeLocalTtsGateway.cs`
- Create: `VoiceServiceLocalApi.Tests/LocalApiHttpTests.cs`
- Modify: `VoiceServiceLocalApi.Tests/VoiceServiceLocalApi.Tests.csproj`
- Modify: `Docs/project/TTS_TOOL_GAP_CHECKLIST_2026-05-02.md`

- [ ] **Step 1: Add TestServer and write RED HTTP tests**

Pin `Microsoft.AspNetCore.TestHost` to `8.0.19`. Build a TestServer with a fake gateway and assert:

```csharp
[Fact] public async Task Health_is_anonymous_and_snake_case() { }
[Fact] public async Task Openapi_declares_bearer_and_tts_routes_without_token_value() { }
[Fact] public async Task Protected_route_rejects_missing_and_wrong_token() { }
[Fact] public async Task Vendors_return_capabilities_without_credentials() { }
[Fact] public async Task Voice_refresh_preserves_unsupported_and_provider_error_codes() { }
[Fact] public async Task Json_generation_returns_metadata_and_protected_audio_url() { }
[Fact] public async Task Loopback_audio_url_ignores_an_untrusted_host_header() { }
[Fact] public async Task Binary_generation_returns_exact_bytes_mime_and_filename() { }
[Fact] public async Task Audio_download_requires_token_and_rejects_traversal() { }
[Fact] public async Task Request_body_larger_than_one_mebibyte_is_rejected() { }
[Fact] public async Task Validation_and_gateway_errors_use_problem_json() { }
[Fact] public async Task Concurrent_generation_obeys_the_configured_limit() { }
```

Run the test file; expected compile failures for `LocalApiApplication` and routes.

- [ ] **Step 2: Implement services and middleware**

`LocalApiApplication.ConfigureServices` registers the supplied options/gateway, a `SemaphoreSlim`, endpoint explorer, and Swagger. Configure JSON with:

```csharp
options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
options.SerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower;
```

Add middleware that:

- permits `/health` and `/openapi/v1.json` anonymously;
- checks Bearer token for `/api/v1/*`;
- assigns a 32-character request ID;
- rejects a declared request body larger than 1 MiB before JSON binding;
- converts unhandled non-cancellation exceptions to sanitized `internal_error` ProblemDetails;
- never logs authorization headers or full text.

Do not register a permissive CORS policy. Configure production Kestrel's `MaxRequestBodySize` to `1_048_576` bytes and apply the same limit middleware in TestServer.

- [ ] **Step 3: Implement all specified routes**

Map exactly:

```text
GET  /health
GET  /openapi/v1.json
GET  /api/v1/vendors
GET  /api/v1/vendors/{vendor}/voices?refresh=false
POST /api/v1/tts
POST /api/v1/tts/audio
GET  /api/v1/audio/{file_name}
```

Both POST routes share one generation function and one semaphore acquisition path. JSON generation returns absolute `audio_url`, actual file size/MIME/format, and normalized vendor/model/voice. Binary generation reads the file before releasing the semaphore. Download uses `AudioFileAccess` and `Results.File` with range processing disabled.

In loopback mode, build `audio_url` from the configured `127.0.0.1` and port and ignore the request Host header. In remote mode, accept the request Host only when the hostname passes `Uri.CheckHostName`, contains no path/user-info characters, and its explicit port equals `LocalApiOptions.Port`; otherwise fall back to the configured loopback base. This prevents Host-header injection while preserving `host.docker.internal` and LAN-IP calls.

Use `application/problem+json` with the exact codes/status mapping from the design. A missing desktop credential maps to 422; provider failures map to 502; `OperationCanceledException` caused by an expired provider timeout maps to 504, while `RequestAborted` writes no replacement response.

- [ ] **Step 4: Implement the production host wrapper**

`LocalApiHost.StartAsync` creates a `WebApplication`, calls the shared configuration methods, and binds:

```csharp
var host = options.AllowRemote ? "0.0.0.0" : "127.0.0.1";
builder.WebHost.UseUrls($"http://{host}:{options.Port}");
```

`StopAsync` uses a linked five-second timeout and disposes the application. Repeated start/stop calls are idempotent and protected by a lock.

- [ ] **Step 5: Verify GREEN**

Run Local API tests, desktop self-check, and solution build. Confirm tests inspect response bodies/content types rather than only status codes.

- [ ] **Step 6: Update TODO, commit, and push**

Record HTTP routes, authorization, OpenAPI, binary/URL results, traversal defense, and integration-test evidence.

```powershell
git add VoiceServiceLocalApi VoiceServiceLocalApi.Tests Docs/project/TTS_TOOL_GAP_CHECKLIST_2026-05-02.md
git commit -m "功能：开放本地TTS HTTP接口"
git push origin HEAD
```

---

### Task 5: Desktop gateway, persistence, and lifecycle

**Files:**

- Create: `Services/DesktopTtsGateway.cs`
- Create: `Services/DesktopLocalApiService.cs`
- Modify: `Models/VendorConfig.cs`
- Modify: `Services/SettingsService.cs`
- Modify: `MainWindow.xaml.cs`
- Modify: `VoiceServiceDemo.csproj`
- Modify: `VoiceServiceDemo.slnx`
- Modify: `VoiceServiceDemo.Tests/Program.cs`
- Create: `VoiceServiceLocalApi.Tests/LocalApiHostTests.cs`
- Modify: `Docs/project/TTS_TOOL_GAP_CHECKLIST_2026-05-02.md`

- [ ] **Step 1: Write RED mapping/default/persistence/lifecycle tests**

Add desktop self-check assertions that:

- `AppSettings.LocalApi.Enabled` defaults true;
- port is 5055 and remote access defaults false;
- loading settings with a blank Token generates and saves a valid token;
- `DesktopTtsGateway` exposes all 12 vendors and only a `configured` boolean;
- omitted model/speed/volume/output fields map to `VendorRegistry` defaults;
- unsupported SSML/output/style/emotion/instructions return `validation_error` without calling a Provider;
- a missing desktop credential maps to `credential_not_configured`.

Add a Kestrel lifecycle test on a dynamically allocated loopback port: start, call `/health`, stop, and prove a second start on the same wrapper works.

- [ ] **Step 2: Persist normalized local API settings**

Extend `AppSettings`:

```csharp
public LocalApiSettings LocalApi { get; set; } = new();

public sealed class LocalApiSettings
{
    public bool Enabled { get; set; } = true;
    public int Port { get; set; } = 5055;
    public bool AllowRemote { get; set; }
    public string AccessToken { get; set; } = "";
    public int MaxConcurrentRequests { get; set; } = 2;
    public int MaxTextLength { get; set; } = 20000;
}
```

Refactor `SettingsService` to accept an optional config path for tests, normalize invalid port/concurrency/text limits, generate a missing token, and save only when normalization changes persisted state.

- [ ] **Step 3: Implement `DesktopTtsGateway`**

Map `VendorRegistry.All` to `LocalVendorInfo`; map configured state with `SettingsService.GetApiKey`. For generation:

1. find the vendor;
2. normalize via `LocalTtsRequestValidator` and registry capabilities;
3. create the desktop `TtsRequest`, including SSML/style/emotion/instructions/output fields;
4. call `TtsService.GenerateAsync(request, cancellationToken)`;
5. return actual file path and stable error code.

Do not accept or return credential content.

- [ ] **Step 4: Implement `DesktopLocalApiService` and window lifecycle**

The service exposes `Status`, `LastError`, `BaseUrl`, `StartAsync`, `StopAsync`, and `RestartAsync`. It snapshots normalized settings into `LocalApiOptions` for each start and raises a status-changed event for Blazor.

In `MainWindow`:

- store the built `ServiceProvider` in a field;
- register `DesktopTtsGateway`, `DesktopLocalApiService`, and the `ILocalTtsGateway` mapping;
- start only after the window loads when Enabled is true;
- catch startup errors and keep the desktop UI alive;
- on close, stop with a five-second timeout and dispose the service provider.

Add a pinned `FrameworkReference`/project reference from the WPF project to `VoiceServiceLocalApi`; do not reference the WPF project from the API module.

- [ ] **Step 5: Verify GREEN**

Run Local API tests, desktop self-check, and full build. Also start the API host test twice to detect leaked sockets.

- [ ] **Step 6: Update TODO, commit, and push**

Record that the API now shares desktop providers/credentials and follows software lifecycle, with test evidence.

```powershell
git add Services/DesktopTtsGateway.cs Services/DesktopLocalApiService.cs Models/VendorConfig.cs Services/SettingsService.cs MainWindow.xaml.cs VoiceServiceDemo.csproj VoiceServiceDemo.slnx VoiceServiceDemo.Tests VoiceServiceLocalApi.Tests Docs/project/TTS_TOOL_GAP_CHECKLIST_2026-05-02.md
git commit -m "功能：本地API随桌面软件启停"
git push origin HEAD
```

---

### Task 6: Settings UI, Dify documentation, and runtime smoke verification

**Files:**

- Modify: `Components/Pages/Settings.razor`
- Modify: `wwwroot/css/app.css`
- Modify: `VoiceServiceDemo.Tests/Program.cs`
- Modify: `README.md`
- Modify: `VoiceServiceMcp/README.md`
- Create: `Docs/guides/DIFY_LOCAL_TTS_API.md`
- Create: `scripts/local_api_smoke.ps1`
- Modify: `Docs/project/TTS_TOOL_GAP_CHECKLIST_2026-05-02.md`

- [ ] **Step 1: Write RED markup and documentation assertions**

Extend the desktop self-check to require these stable markers:

```csharp
AssertTrue(settingsMarkup.Contains("本地 TTS API"), "Settings exposes local API controls");
AssertTrue(settingsMarkup.Contains("DesktopLocalApiService"), "Settings reads live API status");
AssertTrue(settingsMarkup.Contains("host.docker.internal"), "Settings explains local Docker address");
AssertTrue(settingsMarkup.Contains("重新生成 Token"), "Settings can rotate the access token");
AssertTrue(appCss.Contains(".local-api-status"), "Local API status has dedicated styling");
AssertTrue(File.Exists(Path.Combine(repositoryRoot, "Docs", "guides", "DIFY_LOCAL_TTS_API.md")), "Dify guide exists");
```

Run the self-check; expected assertion failure because UI/docs do not exist.

- [ ] **Step 2: Implement the settings card**

Add controls for enabled, port, remote access, masked/reveal/copy/regenerate token, current state, base URL, OpenAPI URL, and “保存并重启 API”. Subscribe/unsubscribe to service status changes and marshal updates through `InvokeAsync`.

Remote access copy warns that it binds all interfaces and does not edit the firewall. Token regeneration uses `LocalApiToken.Generate`, persists settings, restarts the API, and invalidates the prior token immediately.

- [ ] **Step 3: Write Dify and curl documentation**

`Docs/guides/DIFY_LOCAL_TTS_API.md` includes:

- loopback, Docker (`host.docker.internal`), and LAN address selection;
- Bearer header setup;
- OpenAPI import steps;
- HTTP Request examples for JSON URL and binary modes;
- `curl.exe` commands for health, unauthorized, vendors, JSON generation, binary generation, and download;
- a note that Dify Cloud needs an HTTPS tunnel/VPN/reverse proxy and must not expose the port without transport security;
- troubleshooting for port conflicts, 401, 422, 502, Docker connectivity, firewall, and output directory permissions.

Update `README.md` to list all 12 current desktop vendors, describe the API, and link the guide. Mark the old MCP service as legacy/independent so users do not confuse `/mcp/sse` with the new REST API.

- [ ] **Step 4: Add a non-secret runtime smoke script**

`scripts/local_api_smoke.ps1` accepts `-BaseUrl`, `-Token`, and optional `-RequestFile`; it always checks health, unauthorized behavior, authorized vendors, OpenAPI, and optionally executes a user-provided generation request. It must not print or persist the Token.

- [ ] **Step 5: Verify the full slice**

Run:

```powershell
dotnet test VoiceServiceLocalApi.Tests/VoiceServiceLocalApi.Tests.csproj --no-restore
dotnet run --project VoiceServiceDemo.Tests/VoiceServiceDemo.Tests.csproj --no-restore
dotnet build VoiceServiceDemo.slnx --no-restore
pwsh -NoProfile -File scripts/local_api_smoke.ps1 -BaseUrl http://127.0.0.1:5055 -Token $env:VOICEOPS_LOCAL_API_TOKEN
```

Expected: tests pass; build is 0/0; smoke script verifies health, 401, authorized vendor list, and OpenAPI. If a real paid credential and request file are available, also require a non-empty audio file with correct signature; otherwise record that upstream paid generation was not executed and rely on fake-gateway HTTP integration evidence.

- [ ] **Step 6: Complete TODO and commit**

Add a final checked 2026-07-14 local API record summarizing routes, lifecycle, security, Dify, automated test count, build output, and smoke output. Do not mark unrelated public hosting, credential encryption, MCP protocol modernization, or new-vendor work complete.

```powershell
git add Components/Pages/Settings.razor wwwroot/css/app.css VoiceServiceDemo.Tests/Program.cs README.md VoiceServiceMcp/README.md Docs/guides/DIFY_LOCAL_TTS_API.md scripts/local_api_smoke.ps1 Docs/project/TTS_TOOL_GAP_CHECKLIST_2026-05-02.md
git commit -m "功能：完成本地API设置与Dify接入"
git push origin HEAD
```

- [ ] **Step 7: Final local API audit**

Inspect `git status`, `git log`, and `git rev-list HEAD...origin/<branch>`; confirm only the user's pre-existing untracked OpenClaw files remain, all six Chinese commits are on GitHub, and every local API acceptance criterion in the design spec has direct evidence.

After this audit, start a new brainstorming/spec/plan cycle for the next provider batch rather than appending provider-specific code to this plan.
