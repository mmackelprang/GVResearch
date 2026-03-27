# GV Research Platform — Vertical Slice Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the GV Research Platform from empty repo through a working vertical slice where a softphone can place an outbound call through Google Voice via a SIP gateway.

**Architecture:** Three-component system (IAET toolkit + GV REST facade + SIP gateway) with two SIP clients (Avalonia softphone + Grandstream phone). IAET captures and documents GV's internal APIs; GvResearch.Shared provides a shared GV API client consumed by both the REST facade and SIP gateway; the SIP gateway bridges SIP/RTP to GV's call system.

**Tech Stack:** .NET 8/9, C# 12+, ASP.NET Core 8, EF Core 8 + SQLite, Playwright .NET, SIPSorcery, Avalonia UI, NAudio, xUnit + FluentAssertions + NSubstitute, Serilog

**Spec:** `docs/superpowers/specs/2026-03-25-gv-research-platform-design.md`
**PRD:** `gv-research-plan.md`

---

## File Map

### Phase 0 — Repo Scaffolding

| File | Action | Purpose |
|---|---|---|
| `global.json` | Create | Pin .NET SDK version |
| `Directory.Build.props` | Create | Shared build settings (REQ-TECH-014) |
| `GvResearch.sln` | Create | Solution file with all projects |
| `.gitignore` | Create | .NET + captures/ + secrets |
| `.editorconfig` | Create | Code style consistency |
| `src/Iaet.Core/Iaet.Core.csproj` | Create | Core abstractions project |
| `src/Iaet.Core/Abstractions/ICaptureSession.cs` | Create | Capture session contract |
| `src/Iaet.Core/Abstractions/IEndpointCatalog.cs` | Create | Endpoint catalog contract |
| `src/Iaet.Core/Models/EndpointGroup.cs` | Create | Endpoint group model |
| `src/Iaet.Core/Abstractions/ISchemaInferrer.cs` | Create | Schema inference contract |
| `src/Iaet.Core/Abstractions/IReplayEngine.cs` | Create | Replay engine contract |
| `src/Iaet.Core/Abstractions/IApiAdapter.cs` | Create | Pluggable adapter contract |
| `src/Iaet.Core/Models/CapturedRequest.cs` | Create | Core request/response model |
| `src/Iaet.Core/Models/EndpointSignature.cs` | Create | Normalized endpoint identity |
| `src/Iaet.Core/Models/CaptureSessionInfo.cs` | Create | Session metadata |
| `tests/Iaet.Core.Tests/Iaet.Core.Tests.csproj` | Create | Core tests project |
| `tests/Iaet.Core.Tests/Models/EndpointSignatureTests.cs` | Create | Signature normalization tests |
| `scripts/build.ps1` | Create | Cross-platform build script (REQ-TECH-015) |

### Phase 1 — IAET Foundation

| File | Action | Purpose |
|---|---|---|
| `src/Iaet.Capture/Iaet.Capture.csproj` | Create | Playwright CDP capture project |
| `src/Iaet.Capture/PlaywrightCaptureSession.cs` | Create | ICaptureSession implementation |
| `src/Iaet.Capture/CdpNetworkListener.cs` | Create | CDP network event handler |
| `src/Iaet.Capture/RequestSanitizer.cs` | Create | Redact auth headers (REQ-IAET-002) |
| `src/Iaet.Catalog/Iaet.Catalog.csproj` | Create | EF Core SQLite catalog project |
| `src/Iaet.Catalog/CatalogDbContext.cs` | Create | EF Core DbContext |
| `src/Iaet.Catalog/Entities/CapturedRequestEntity.cs` | Create | DB entity for captured requests |
| `src/Iaet.Catalog/Entities/EndpointGroupEntity.cs` | Create | DB entity for grouped endpoints |
| `src/Iaet.Catalog/Entities/CaptureSessionEntity.cs` | Create | DB entity for sessions |
| `src/Iaet.Catalog/SqliteCatalog.cs` | Create | IEndpointCatalog implementation |
| `src/Iaet.Catalog/EndpointNormalizer.cs` | Create | URL path normalization (REQ-IAET-003) |
| `src/Iaet.Catalog/Migrations/` | Create | EF Core migrations folder |
| `tests/Iaet.Catalog.Tests/Iaet.Catalog.Tests.csproj` | Create | Catalog tests project |
| `tests/Iaet.Catalog.Tests/SqliteCatalogTests.cs` | Create | Catalog CRUD tests |
| `tests/Iaet.Catalog.Tests/EndpointNormalizerTests.cs` | Create | Normalization logic tests |
| `src/Iaet.Cli/Iaet.Cli.csproj` | Create | dotnet tool CLI project |
| `src/Iaet.Cli/Program.cs` | Create | CLI entry point |
| `src/Iaet.Cli/Commands/CaptureCommand.cs` | Create | `iaet capture start/stop` |
| `src/Iaet.Cli/Commands/CatalogCommand.cs` | Create | `iaet catalog list/show` |

### Phase 2 — Vertical Slice

| File | Action | Purpose |
|---|---|---|
| `src/Iaet.Adapters.GoogleVoice/Iaet.Adapters.GoogleVoice.csproj` | Create | GV adapter project |
| `src/Iaet.Adapters.GoogleVoice/GoogleVoiceAdapter.cs` | Create | IApiAdapter implementation |
| `src/Iaet.Adapters.GoogleVoice/GvEndpointPatterns.cs` | Create | GV-specific URL/payload patterns |
| `src/GvResearch.Shared/GvResearch.Shared.csproj` | Create | Shared GV client project |
| `src/GvResearch.Shared/Models/GvCallResult.cs` | Create | Call result model |
| `src/GvResearch.Shared/Models/GvCallStatus.cs` | Create | Call status model |
| `src/GvResearch.Shared/Models/GvCallEvent.cs` | Create | Call event model |
| `src/GvResearch.Shared/Services/IGvCallService.cs` | Create | Call service contract |
| `src/GvResearch.Shared/Services/GvCallService.cs` | Create | Call service implementation |
| `src/GvResearch.Shared/Auth/IGvTokenService.cs` | Create | Token service contract |
| `src/GvResearch.Shared/Auth/EncryptedFileTokenService.cs` | Create | AES-256 encrypted token store |
| `src/GvResearch.Shared/Auth/TokenEncryption.cs` | Create | AES-256 encrypt/decrypt helpers |
| `src/GvResearch.Shared/Http/GvHttpClientHandler.cs` | Create | Token-injecting delegating handler |
| `src/GvResearch.Shared/RateLimiting/GvRateLimiter.cs` | Create | Per-endpoint rate limiting |
| `tests/GvResearch.Shared.Tests/GvResearch.Shared.Tests.csproj` | Create | Shared tests project |
| `tests/GvResearch.Shared.Tests/Auth/EncryptedFileTokenServiceTests.cs` | Create | Token service tests |
| `tests/GvResearch.Shared.Tests/Auth/TokenEncryptionTests.cs` | Create | Encryption round-trip tests |
| `tests/GvResearch.Shared.Tests/Services/GvCallServiceTests.cs` | Create | Call service tests |
| `tests/GvResearch.Shared.Tests/RateLimiting/GvRateLimiterTests.cs` | Create | Rate limiter tests |
| `src/GvResearch.Api/GvResearch.Api.csproj` | Create | ASP.NET Core REST facade project |
| `src/GvResearch.Api/Program.cs` | Create | API entry point + DI setup |
| `src/GvResearch.Api/Endpoints/CallEndpoints.cs` | Create | Calls Minimal API routes |
| `src/GvResearch.Api/Models/CallRecord.cs` | Create | REST API call model (PRD 6.3) |
| `src/GvResearch.Api/Models/PagedResult.cs` | Create | Cursor-based pagination model |
| `tests/GvResearch.Api.Tests/GvResearch.Api.Tests.csproj` | Create | API tests project |
| `tests/GvResearch.Api.Tests/CallEndpointsTests.cs` | Create | Integration tests via WebApplicationFactory |
| `src/GvResearch.Sip/GvResearch.Sip.csproj` | Create | SIP gateway project |
| `src/GvResearch.Sip/Program.cs` | Create | SIP gateway entry point |
| `src/GvResearch.Sip/Registrar/SipRegistrar.cs` | Create | SIP REGISTER handler |
| `src/GvResearch.Sip/Registrar/RegistrationStore.cs` | Create | In-memory SIP registration table |
| `src/GvResearch.Sip/Registrar/DigestAuthenticator.cs` | Create | SIP digest auth |
| `src/GvResearch.Sip/Calls/SipCallController.cs` | Create | INVITE/BYE handler |
| `src/GvResearch.Sip/Calls/CallSession.cs` | Create | Per-call state tracker |
| `src/GvResearch.Sip/Media/RtpBridge.cs` | Create | RTP packet forwarding |
| `src/GvResearch.Sip/Media/IGvAudioChannel.cs` | Create | GV-side audio abstraction |
| `src/GvResearch.Sip/Media/WebRtcGvAudioChannel.cs` | Create | WebRTC implementation (post-discovery) |
| `src/GvResearch.Sip/Configuration/SipGatewayOptions.cs` | Create | Options pattern config |
| `tests/GvResearch.Sip.Tests/GvResearch.Sip.Tests.csproj` | Create | SIP gateway tests project |
| `tests/GvResearch.Sip.Tests/Registrar/SipRegistrarTests.cs` | Create | Registration tests |
| `tests/GvResearch.Sip.Tests/Calls/SipCallControllerTests.cs` | Create | Call flow state machine tests |
| `src/GvResearch.Softphone/GvResearch.Softphone.csproj` | Create | Avalonia softphone project |
| `src/GvResearch.Softphone/App.axaml` | Create | Avalonia app definition |
| `src/GvResearch.Softphone/App.axaml.cs` | Create | App code-behind |
| `src/GvResearch.Softphone/Program.cs` | Create | Entry point |
| `src/GvResearch.Softphone/ViewModels/MainWindowViewModel.cs` | Create | Root ViewModel |
| `src/GvResearch.Softphone/ViewModels/DialerViewModel.cs` | Create | Dialer logic |
| `src/GvResearch.Softphone/ViewModels/ActiveCallViewModel.cs` | Create | Active call logic |
| `src/GvResearch.Softphone/Views/MainWindow.axaml` | Create | Root window |
| `src/GvResearch.Softphone/Views/DialerView.axaml` | Create | Dialer UI |
| `src/GvResearch.Softphone/Views/ActiveCallView.axaml` | Create | Active call UI |
| `src/GvResearch.Softphone/Sip/SoftphoneSipClient.cs` | Create | SIPSorcery UA wrapper |
| `src/GvResearch.Softphone/Audio/AudioEngine.cs` | Create | NAudio capture/playback |
| `src/GvResearch.Softphone/Configuration/SoftphoneSettings.cs` | Create | Settings model |
| `tests/GvResearch.Softphone.Tests/GvResearch.Softphone.Tests.csproj` | Create | Softphone tests project |
| `tests/GvResearch.Softphone.Tests/ViewModels/DialerViewModelTests.cs` | Create | Dialer VM tests |
| `tests/GvResearch.Softphone.Tests/ViewModels/ActiveCallViewModelTests.cs` | Create | Active call VM tests |

---

## Phase 0: Repo Scaffolding

### Task 1: Initialize Git Repository and Solution

**Files:**
- Create: `.gitignore`
- Create: `global.json`
- Create: `Directory.Build.props`
- Create: `.editorconfig`
- Create: `GvResearch.sln`

- [ ] **Step 1: Initialize git repo**

```bash
cd D:/prj/GVResearch
git init
```

- [ ] **Step 2: Create .gitignore**

```gitignore
## .NET
bin/
obj/
*.user
*.suo
*.vs/
.idea/

## Build output
publish/
artifacts/

## Secrets and captures
captures/
*.enc
appsettings.*.json
!appsettings.json
!appsettings.Development.json

## User secrets are in %APPDATA% - never in repo

## Coverage
TestResults/
coverage/
*.cobertura.xml

## SQLite databases (catalog)
*.db
*.db-journal
*.db-wal

## OS
Thumbs.db
.DS_Store
```

- [ ] **Step 3: Create global.json**

```json
{
  "sdk": {
    "version": "8.0.400",
    "rollForward": "latestMinor",
    "allowPrerelease": false
  }
}
```

- [ ] **Step 4: Create Directory.Build.props (REQ-TECH-014)**

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <AnalysisMode>All</AnalysisMode>
    <LangVersion>12</LangVersion>
  </PropertyGroup>
</Project>
```

- [ ] **Step 5: Create .editorconfig**

```ini
root = true

[*]
indent_style = space
indent_size = 4
end_of_line = lf
charset = utf-8
trim_trailing_whitespace = true
insert_final_newline = true

[*.{csproj,props,targets,json,xml,yml,yaml}]
indent_size = 2

[*.cs]
# File-scoped namespaces (REQ-TECH-001)
csharp_style_namespace_declarations = file_scoped:warning
```

- [ ] **Step 6: Create empty solution**

```bash
dotnet new sln -n GvResearch -o D:/prj/GVResearch
```

- [ ] **Step 7: Create directory structure**

```bash
mkdir -p src/{Iaet.Core,Iaet.Capture,Iaet.Catalog,Iaet.Schema,Iaet.Replay,Iaet.Cli,Iaet.Adapters.GoogleVoice,GvResearch.Shared,GvResearch.Api,GvResearch.Sip,GvResearch.Softphone,GvResearch.Client.Cli}
mkdir -p tests/{Iaet.Core.Tests,Iaet.Catalog.Tests,Iaet.Schema.Tests,Iaet.Replay.Tests,GvResearch.Shared.Tests,GvResearch.Api.Tests,GvResearch.Sip.Tests,GvResearch.Softphone.Tests}
mkdir -p docs scripts captures
```

- [ ] **Step 8: Commit**

```bash
git add .gitignore global.json Directory.Build.props .editorconfig GvResearch.sln
git commit -m "chore: initialize repo with solution structure and build settings"
```

---

### Task 2: Iaet.Core — Abstractions and Models

**Files:**
- Create: `src/Iaet.Core/Iaet.Core.csproj`
- Create: `src/Iaet.Core/Abstractions/ICaptureSession.cs`
- Create: `src/Iaet.Core/Abstractions/IEndpointCatalog.cs`
- Create: `src/Iaet.Core/Abstractions/ISchemaInferrer.cs`
- Create: `src/Iaet.Core/Abstractions/IReplayEngine.cs`
- Create: `src/Iaet.Core/Abstractions/IApiAdapter.cs`
- Create: `src/Iaet.Core/Models/CapturedRequest.cs`
- Create: `src/Iaet.Core/Models/EndpointSignature.cs`
- Create: `src/Iaet.Core/Models/CaptureSessionInfo.cs`
- Test: `tests/Iaet.Core.Tests/Iaet.Core.Tests.csproj`
- Test: `tests/Iaet.Core.Tests/Models/EndpointSignatureTests.cs`

- [ ] **Step 1: Create Iaet.Core project**

```bash
dotnet new classlib -n Iaet.Core -o src/Iaet.Core
rm src/Iaet.Core/Class1.cs
dotnet sln add src/Iaet.Core/Iaet.Core.csproj
```

- [ ] **Step 2: Create test project**

```bash
dotnet new xunit -n Iaet.Core.Tests -o tests/Iaet.Core.Tests
rm tests/Iaet.Core.Tests/UnitTest1.cs
dotnet sln add tests/Iaet.Core.Tests/Iaet.Core.Tests.csproj
dotnet add tests/Iaet.Core.Tests reference src/Iaet.Core
```

Add test packages to `tests/Iaet.Core.Tests/Iaet.Core.Tests.csproj`:

```bash
dotnet add tests/Iaet.Core.Tests package FluentAssertions
dotnet add tests/Iaet.Core.Tests package NSubstitute
```

- [ ] **Step 3: Write EndpointSignature model and tests (TDD)**

Write test first — `tests/Iaet.Core.Tests/Models/EndpointSignatureTests.cs`:

```csharp
using FluentAssertions;
using Iaet.Core.Models;

namespace Iaet.Core.Tests.Models;

public class EndpointSignatureTests
{
    [Theory]
    [InlineData("GET", "/api/users/123/posts/456", "GET /api/users/{id}/posts/{id}")]
    [InlineData("POST", "/api/v1/messages", "POST /api/v1/messages")]
    [InlineData("GET", "/api/items/a]8f3b2c1d", "GET /api/items/{id}")]
    public void Normalize_ReplacesIdSegments(string method, string path, string expected)
    {
        var sig = EndpointSignature.FromRequest(method, path);
        sig.Normalized.Should().Be(expected);
    }

    [Fact]
    public void Equals_SameNormalized_AreEqual()
    {
        var a = EndpointSignature.FromRequest("GET", "/api/users/123");
        var b = EndpointSignature.FromRequest("GET", "/api/users/456");
        a.Should().Be(b);
    }

    [Fact]
    public void Equals_DifferentMethod_AreNotEqual()
    {
        var a = EndpointSignature.FromRequest("GET", "/api/users/123");
        var b = EndpointSignature.FromRequest("POST", "/api/users/123");
        a.Should().NotBe(b);
    }
}
```

- [ ] **Step 4: Run test to verify it fails**

```bash
dotnet test tests/Iaet.Core.Tests --filter "EndpointSignatureTests" -v n
```

Expected: FAIL — `EndpointSignature` does not exist.

- [ ] **Step 5: Implement EndpointSignature**

Create `src/Iaet.Core/Models/EndpointSignature.cs`:

```csharp
using System.Text.RegularExpressions;

namespace Iaet.Core.Models;

public sealed partial record EndpointSignature
{
    public string Method { get; }
    public string NormalizedPath { get; }
    public string Normalized => $"{Method} {NormalizedPath}";

    private EndpointSignature(string method, string normalizedPath)
    {
        Method = method;
        NormalizedPath = normalizedPath;
    }

    public static EndpointSignature FromRequest(string method, string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var normalized = string.Join("/",
            segments.Select(s => IdPattern().IsMatch(s) ? "{id}" : s));
        return new EndpointSignature(method.ToUpperInvariant(), "/" + normalized);
    }

    // Matches: pure digits, GUIDs, hex strings 8+ chars
    [GeneratedRegex(@"^(\d+|[0-9a-f]{8,}|[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})$", RegexOptions.IgnoreCase)]
    private static partial Regex IdPattern();
}
```

- [ ] **Step 6: Run test to verify it passes**

```bash
dotnet test tests/Iaet.Core.Tests --filter "EndpointSignatureTests" -v n
```

Expected: All 5 tests PASS.

- [ ] **Step 7: Create core models**

Create `src/Iaet.Core/Models/CapturedRequest.cs`:

```csharp
namespace Iaet.Core.Models;

public sealed record CapturedRequest
{
    public required Guid Id { get; init; }
    public required Guid SessionId { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required string HttpMethod { get; init; }
    public required string Url { get; init; }
    public required Dictionary<string, string> RequestHeaders { get; init; }
    public string? RequestBody { get; init; }
    public required int ResponseStatus { get; init; }
    public required Dictionary<string, string> ResponseHeaders { get; init; }
    public string? ResponseBody { get; init; }
    public required long DurationMs { get; init; }
    public string? Tag { get; init; }
}
```

Create `src/Iaet.Core/Models/CaptureSessionInfo.cs`:

```csharp
namespace Iaet.Core.Models;

public sealed record CaptureSessionInfo
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string TargetApplication { get; init; }
    public required string Profile { get; init; }
    public required DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset? StoppedAt { get; init; }
    public int CapturedRequestCount { get; init; }
}
```

- [ ] **Step 8: Create core abstractions**

Create `src/Iaet.Core/Abstractions/ICaptureSession.cs`:

```csharp
using Iaet.Core.Models;

namespace Iaet.Core.Abstractions;

public interface ICaptureSession : IAsyncDisposable
{
    Guid SessionId { get; }
    string TargetApplication { get; }
    bool IsRecording { get; }
    Task StartAsync(string url, CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);
    IAsyncEnumerable<CapturedRequest> GetCapturedRequestsAsync(CancellationToken ct = default);
}
```

Create `src/Iaet.Core/Abstractions/IEndpointCatalog.cs`:

```csharp
using Iaet.Core.Models;

namespace Iaet.Core.Abstractions;

public interface IEndpointCatalog
{
    Task SaveSessionAsync(CaptureSessionInfo session, CancellationToken ct = default);
    Task SaveRequestAsync(CapturedRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<CaptureSessionInfo>> ListSessionsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CapturedRequest>> GetRequestsBySessionAsync(Guid sessionId, CancellationToken ct = default);
    Task<IReadOnlyList<EndpointGroup>> GetEndpointGroupsAsync(Guid sessionId, CancellationToken ct = default);
}
```

Create `src/Iaet.Core/Models/EndpointGroup.cs`:

```csharp
namespace Iaet.Core.Models;

public sealed record EndpointGroup(
    EndpointSignature Signature,
    int ObservationCount,
    DateTimeOffset FirstSeen,
    DateTimeOffset LastSeen
);
```

Create `src/Iaet.Core/Abstractions/ISchemaInferrer.cs`:

```csharp
namespace Iaet.Core.Abstractions;

public interface ISchemaInferrer
{
    Task<SchemaResult> InferAsync(IReadOnlyList<string> jsonBodies, CancellationToken ct = default);
}

public sealed record SchemaResult(
    string JsonSchema,
    string CSharpRecord,
    string OpenApiFragment,
    IReadOnlyList<string> Warnings
);
```

Create `src/Iaet.Core/Abstractions/IReplayEngine.cs`:

```csharp
using Iaet.Core.Models;

namespace Iaet.Core.Abstractions;

public interface IReplayEngine
{
    Task<ReplayResult> ReplayAsync(CapturedRequest original, CancellationToken ct = default);
}

public sealed record ReplayResult(
    int ResponseStatus,
    string? ResponseBody,
    IReadOnlyList<FieldDiff> Diffs,
    long DurationMs
);

public sealed record FieldDiff(string Path, string? Expected, string? Actual);
```

Create `src/Iaet.Core/Abstractions/IApiAdapter.cs`:

```csharp
using Iaet.Core.Models;

namespace Iaet.Core.Abstractions;

public interface IApiAdapter
{
    string TargetName { get; }
    bool CanHandle(CapturedRequest request);
    EndpointDescriptor Describe(CapturedRequest request);
}

public sealed record EndpointDescriptor(
    string HumanName,
    string Category,
    bool IsDestructive,
    string? Notes
);
```

- [ ] **Step 9: Verify everything builds**

```bash
dotnet build GvResearch.sln
dotnet test tests/Iaet.Core.Tests -v n
```

Expected: Build succeeds, all tests pass.

- [ ] **Step 10: Commit**

```bash
git add src/Iaet.Core/ tests/Iaet.Core.Tests/ GvResearch.sln
git commit -m "feat: add Iaet.Core with abstractions, models, and endpoint signature normalization"
```

---

### Task 3: Build Script

**Files:**
- Create: `scripts/build.ps1`

- [ ] **Step 1: Create build.ps1 (REQ-TECH-015)**

```powershell
#!/usr/bin/env pwsh
# Cross-platform build script for GV Research Platform
# Works on Windows 11 and Ubuntu 22.04+

param(
    [ValidateSet('clean', 'restore', 'build', 'test', 'publish', 'docker-build')]
    [string]$Target = 'build'
)

$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $PSScriptRoot
$Solution = Join-Path $RepoRoot 'GvResearch.sln'

function Invoke-Clean {
    Write-Host "Cleaning..." -ForegroundColor Cyan
    dotnet clean $Solution -v q
    Get-ChildItem $RepoRoot -Recurse -Directory -Include bin, obj | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
}

function Invoke-Restore {
    Write-Host "Restoring..." -ForegroundColor Cyan
    dotnet restore $Solution
}

function Invoke-Build {
    Invoke-Restore
    Write-Host "Building..." -ForegroundColor Cyan
    dotnet build $Solution --no-restore -c Release
}

function Invoke-Test {
    Invoke-Build
    Write-Host "Testing..." -ForegroundColor Cyan
    dotnet test $Solution --no-build -c Release `
        --collect:"XPlat Code Coverage" `
        --results-directory (Join-Path $RepoRoot 'TestResults') `
        -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura
}

function Invoke-Publish {
    Invoke-Build
    Write-Host "Publishing..." -ForegroundColor Cyan
    $projects = @(
        'src/GvResearch.Api/GvResearch.Api.csproj',
        'src/GvResearch.Sip/GvResearch.Sip.csproj',
        'src/GvResearch.Softphone/GvResearch.Softphone.csproj',
        'src/Iaet.Cli/Iaet.Cli.csproj'
    )
    foreach ($proj in $projects) {
        $fullPath = Join-Path $RepoRoot $proj
        if (Test-Path $fullPath) {
            dotnet publish $fullPath --no-build -c Release -o (Join-Path $RepoRoot "artifacts/$(Split-Path -LeafBase $proj)")
        }
    }
}

function Invoke-DockerBuild {
    Write-Host "Building Docker images..." -ForegroundColor Cyan
    docker compose -f (Join-Path $RepoRoot 'scripts/docker-compose.yml') build
}

switch ($Target) {
    'clean'        { Invoke-Clean }
    'restore'      { Invoke-Restore }
    'build'        { Invoke-Build }
    'test'         { Invoke-Test }
    'publish'      { Invoke-Publish }
    'docker-build' { Invoke-DockerBuild }
}

Write-Host "Done: $Target" -ForegroundColor Green
```

- [ ] **Step 2: Test the build script**

```bash
pwsh scripts/build.ps1 -Target build
```

Expected: Solution builds successfully.

- [ ] **Step 3: Commit**

```bash
git add scripts/build.ps1
git commit -m "chore: add cross-platform build script with clean/restore/build/test/publish targets"
```

---

## Phase 1: IAET Foundation

### Task 4: Iaet.Capture — Playwright CDP Capture Engine

**Files:**
- Create: `src/Iaet.Capture/Iaet.Capture.csproj`
- Create: `src/Iaet.Capture/PlaywrightCaptureSession.cs`
- Create: `src/Iaet.Capture/CdpNetworkListener.cs`
- Create: `src/Iaet.Capture/RequestSanitizer.cs`

- [ ] **Step 1: Create Iaet.Capture project with Playwright dependency**

```bash
dotnet new classlib -n Iaet.Capture -o src/Iaet.Capture
rm src/Iaet.Capture/Class1.cs
dotnet sln add src/Iaet.Capture/Iaet.Capture.csproj
dotnet add src/Iaet.Capture reference src/Iaet.Core
dotnet add src/Iaet.Capture package Microsoft.Playwright
```

- [ ] **Step 2: Create RequestSanitizer**

This is independently testable logic — handles REQ-IAET-002 (redact auth headers).

Create `src/Iaet.Capture/RequestSanitizer.cs`:

```csharp
namespace Iaet.Capture;

public static class RequestSanitizer
{
    private static readonly HashSet<string> SensitiveHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "authorization", "cookie", "set-cookie", "x-goog-authuser",
        "x-csrf-token", "x-xsrf-token"
    };

    public static Dictionary<string, string> SanitizeHeaders(IDictionary<string, string> headers)
    {
        return headers.ToDictionary(
            kvp => kvp.Key,
            kvp => SensitiveHeaders.Contains(kvp.Key) ? "<REDACTED>" : kvp.Value,
            StringComparer.OrdinalIgnoreCase
        );
    }
}
```

- [ ] **Step 3: Create CdpNetworkListener**

Create `src/Iaet.Capture/CdpNetworkListener.cs`:

```csharp
using System.Collections.Concurrent;
using System.Diagnostics;
using Iaet.Core.Models;
using Microsoft.Playwright;

namespace Iaet.Capture;

public sealed class CdpNetworkListener
{
    private readonly ConcurrentDictionary<string, PendingRequest> _pending = new();
    private readonly ConcurrentQueue<CapturedRequest> _completed = new();
    private readonly Guid _sessionId;

    public CdpNetworkListener(Guid sessionId)
    {
        _sessionId = sessionId;
    }

    public void Attach(IPage page)
    {
        page.Request += (_, request) =>
        {
            if (!IsXhrOrFetch(request)) return;
            _pending[request.Url + request.Method] = new PendingRequest(
                Stopwatch.StartNew(),
                request
            );
        };

        page.Response += async (_, response) =>
        {
            var key = response.Request.Url + response.Request.Method;
            if (!_pending.TryRemove(key, out var pending)) return;
            pending.Stopwatch.Stop();

            string? requestBody = null;
            try { requestBody = response.Request.PostData; } catch { /* some requests have no body */ }

            string? responseBody = null;
            try { responseBody = await response.TextAsync(); } catch { /* binary or failed */ }

            var captured = new CapturedRequest
            {
                Id = Guid.NewGuid(),
                SessionId = _sessionId,
                Timestamp = DateTimeOffset.UtcNow,
                HttpMethod = response.Request.Method,
                Url = response.Request.Url,
                RequestHeaders = RequestSanitizer.SanitizeHeaders(
                    await response.Request.AllHeadersAsync()),
                RequestBody = requestBody,
                ResponseStatus = response.Status,
                ResponseHeaders = RequestSanitizer.SanitizeHeaders(
                    await response.AllHeadersAsync()),
                ResponseBody = responseBody,
                DurationMs = pending.Stopwatch.ElapsedMilliseconds,
            };

            _completed.Enqueue(captured);
        };
    }

    public IReadOnlyList<CapturedRequest> DrainCaptured()
    {
        var result = new List<CapturedRequest>();
        while (_completed.TryDequeue(out var item))
            result.Add(item);
        return result;
    }

    private static bool IsXhrOrFetch(IRequest request) =>
        request.ResourceType is "xhr" or "fetch";

    private sealed record PendingRequest(Stopwatch Stopwatch, IRequest Request);
}
```

- [ ] **Step 4: Create PlaywrightCaptureSession**

Create `src/Iaet.Capture/PlaywrightCaptureSession.cs`:

```csharp
using Iaet.Core.Abstractions;
using Iaet.Core.Models;
using Microsoft.Playwright;

namespace Iaet.Capture;

public sealed class PlaywrightCaptureSession : ICaptureSession
{
    private readonly string _targetApplication;
    private readonly string _profile;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IPage? _page;
    private CdpNetworkListener? _listener;

    public Guid SessionId { get; } = Guid.NewGuid();
    public string TargetApplication => _targetApplication;
    public bool IsRecording { get; private set; }

    public PlaywrightCaptureSession(string targetApplication, string profile)
    {
        _targetApplication = targetApplication;
        _profile = profile;
    }

    public async Task StartAsync(string url, CancellationToken ct = default)
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = false,
            Args = [$"--profile-directory={_profile}"]
        });

        _page = await _browser.NewPageAsync();
        _listener = new CdpNetworkListener(SessionId);
        _listener.Attach(_page);
        IsRecording = true;

        await _page.GotoAsync(url);
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        IsRecording = false;
        if (_browser is not null) await _browser.CloseAsync();
        _playwright?.Dispose();
    }

    public async IAsyncEnumerable<CapturedRequest> GetCapturedRequestsAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        if (_listener is null) yield break;
        foreach (var request in _listener.DrainCaptured())
        {
            yield return request;
        }
        await Task.CompletedTask; // satisfy async requirement
    }

    public async ValueTask DisposeAsync()
    {
        if (IsRecording) await StopAsync();
    }
}
```

- [ ] **Step 5: Verify it builds**

```bash
dotnet build src/Iaet.Capture
```

Expected: Build succeeds. (No automated tests for capture — requires live browser.)

- [ ] **Step 6: Commit**

```bash
git add src/Iaet.Capture/
git commit -m "feat: add Iaet.Capture with Playwright CDP network interception and header sanitization"
```

---

### Task 5: Iaet.Catalog — EF Core SQLite Catalog

**Files:**
- Create: `src/Iaet.Catalog/Iaet.Catalog.csproj`
- Create: `src/Iaet.Catalog/CatalogDbContext.cs`
- Create: `src/Iaet.Catalog/Entities/CapturedRequestEntity.cs`
- Create: `src/Iaet.Catalog/Entities/EndpointGroupEntity.cs`
- Create: `src/Iaet.Catalog/Entities/CaptureSessionEntity.cs`
- Create: `src/Iaet.Catalog/SqliteCatalog.cs`
- Create: `src/Iaet.Catalog/EndpointNormalizer.cs`
- Test: `tests/Iaet.Catalog.Tests/Iaet.Catalog.Tests.csproj`
- Test: `tests/Iaet.Catalog.Tests/EndpointNormalizerTests.cs`
- Test: `tests/Iaet.Catalog.Tests/SqliteCatalogTests.cs`

- [ ] **Step 1: Create Iaet.Catalog project**

```bash
dotnet new classlib -n Iaet.Catalog -o src/Iaet.Catalog
rm src/Iaet.Catalog/Class1.cs
dotnet sln add src/Iaet.Catalog/Iaet.Catalog.csproj
dotnet add src/Iaet.Catalog reference src/Iaet.Core
dotnet add src/Iaet.Catalog package Microsoft.EntityFrameworkCore.Sqlite
dotnet add src/Iaet.Catalog package Microsoft.EntityFrameworkCore.Design
```

- [ ] **Step 2: Create test project**

```bash
dotnet new xunit -n Iaet.Catalog.Tests -o tests/Iaet.Catalog.Tests
rm tests/Iaet.Catalog.Tests/UnitTest1.cs
dotnet sln add tests/Iaet.Catalog.Tests/Iaet.Catalog.Tests.csproj
dotnet add tests/Iaet.Catalog.Tests reference src/Iaet.Catalog
dotnet add tests/Iaet.Catalog.Tests reference src/Iaet.Core
dotnet add tests/Iaet.Catalog.Tests package FluentAssertions
dotnet add tests/Iaet.Catalog.Tests package NSubstitute
dotnet add tests/Iaet.Catalog.Tests package Microsoft.EntityFrameworkCore.Sqlite
```

- [ ] **Step 3: Write EndpointNormalizer tests (TDD)**

Create `tests/Iaet.Catalog.Tests/EndpointNormalizerTests.cs`:

```csharp
using FluentAssertions;
using Iaet.Catalog;

namespace Iaet.Catalog.Tests;

public class EndpointNormalizerTests
{
    [Theory]
    [InlineData("https://voice.google.com/api/v1/users/12345/calls",
                "GET",
                "GET /api/v1/users/{id}/calls")]
    [InlineData("https://voice.google.com/api/messages",
                "POST",
                "POST /api/messages")]
    [InlineData("https://example.com/rpc/list?key=abc123",
                "POST",
                "POST /rpc/list")]
    public void NormalizeUrl_ExtractsPathAndNormalizesIds(string url, string method, string expected)
    {
        var result = EndpointNormalizer.Normalize(method, url);
        result.Should().Be(expected);
    }
}
```

- [ ] **Step 4: Run test to verify it fails**

```bash
dotnet test tests/Iaet.Catalog.Tests --filter "EndpointNormalizerTests" -v n
```

Expected: FAIL — `EndpointNormalizer` does not exist.

- [ ] **Step 5: Implement EndpointNormalizer**

Create `src/Iaet.Catalog/EndpointNormalizer.cs`:

```csharp
using Iaet.Core.Models;

namespace Iaet.Catalog;

public static class EndpointNormalizer
{
    public static string Normalize(string method, string fullUrl)
    {
        var uri = new Uri(fullUrl);
        var sig = EndpointSignature.FromRequest(method, uri.AbsolutePath);
        return sig.Normalized;
    }
}
```

- [ ] **Step 6: Run test to verify it passes**

```bash
dotnet test tests/Iaet.Catalog.Tests --filter "EndpointNormalizerTests" -v n
```

Expected: PASS.

- [ ] **Step 7: Create EF Core entities**

Create `src/Iaet.Catalog/Entities/CaptureSessionEntity.cs`:

```csharp
namespace Iaet.Catalog.Entities;

public class CaptureSessionEntity
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string TargetApplication { get; set; }
    public required string Profile { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? StoppedAt { get; set; }

    public List<CapturedRequestEntity> Requests { get; set; } = [];
}
```

Create `src/Iaet.Catalog/Entities/CapturedRequestEntity.cs`:

```csharp
namespace Iaet.Catalog.Entities;

public class CapturedRequestEntity
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public required string HttpMethod { get; set; }
    public required string Url { get; set; }
    public required string NormalizedSignature { get; set; }
    public string? RequestHeaders { get; set; } // JSON serialized
    public string? RequestBody { get; set; }
    public int ResponseStatus { get; set; }
    public string? ResponseHeaders { get; set; } // JSON serialized
    public string? ResponseBody { get; set; }
    public long DurationMs { get; set; }
    public string? Tag { get; set; }

    public CaptureSessionEntity? Session { get; set; }
}
```

Create `src/Iaet.Catalog/Entities/EndpointGroupEntity.cs`:

```csharp
namespace Iaet.Catalog.Entities;

public class EndpointGroupEntity
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public required string NormalizedSignature { get; set; }
    public int ObservationCount { get; set; }
    public DateTimeOffset FirstSeen { get; set; }
    public DateTimeOffset LastSeen { get; set; }

    public CaptureSessionEntity? Session { get; set; }
}
```

- [ ] **Step 8: Create CatalogDbContext**

Create `src/Iaet.Catalog/CatalogDbContext.cs`:

```csharp
using Iaet.Catalog.Entities;
using Microsoft.EntityFrameworkCore;

namespace Iaet.Catalog;

public class CatalogDbContext : DbContext
{
    public DbSet<CaptureSessionEntity> Sessions => Set<CaptureSessionEntity>();
    public DbSet<CapturedRequestEntity> Requests => Set<CapturedRequestEntity>();
    public DbSet<EndpointGroupEntity> EndpointGroups => Set<EndpointGroupEntity>();

    public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CaptureSessionEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasMany(x => x.Requests).WithOne(x => x.Session).HasForeignKey(x => x.SessionId);
        });

        modelBuilder.Entity<CapturedRequestEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.SessionId);
            e.HasIndex(x => x.NormalizedSignature);
        });

        modelBuilder.Entity<EndpointGroupEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.SessionId, x.NormalizedSignature }).IsUnique();
        });
    }
}
```

- [ ] **Step 9: Write SqliteCatalog tests (TDD)**

Create `tests/Iaet.Catalog.Tests/SqliteCatalogTests.cs`:

```csharp
using FluentAssertions;
using Iaet.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Iaet.Catalog.Tests;

public class SqliteCatalogTests : IDisposable
{
    private readonly CatalogDbContext _db;
    private readonly SqliteCatalog _catalog;

    public SqliteCatalogTests()
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new CatalogDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
        _catalog = new SqliteCatalog(_db);
    }

    [Fact]
    public async Task SaveAndListSession_RoundTrips()
    {
        var session = new CaptureSessionInfo
        {
            Id = Guid.NewGuid(),
            Name = "test-session",
            TargetApplication = "TestApp",
            Profile = "test-profile",
            StartedAt = DateTimeOffset.UtcNow
        };

        await _catalog.SaveSessionAsync(session);
        var sessions = await _catalog.ListSessionsAsync();
        sessions.Should().ContainSingle().Which.Name.Should().Be("test-session");
    }

    [Fact]
    public async Task SaveRequest_GroupsBySignature()
    {
        var sessionId = Guid.NewGuid();
        var session = new CaptureSessionInfo
        {
            Id = sessionId, Name = "s1", TargetApplication = "App",
            Profile = "p1", StartedAt = DateTimeOffset.UtcNow
        };
        await _catalog.SaveSessionAsync(session);

        // Two requests to same endpoint with different IDs
        await _catalog.SaveRequestAsync(MakeRequest(sessionId, "GET", "https://api.test/users/123"));
        await _catalog.SaveRequestAsync(MakeRequest(sessionId, "GET", "https://api.test/users/456"));

        var groups = await _catalog.GetEndpointGroupsAsync(sessionId);
        groups.Should().ContainSingle()
            .Which.ObservationCount.Should().Be(2);
    }

    private static CapturedRequest MakeRequest(Guid sessionId, string method, string url) => new()
    {
        Id = Guid.NewGuid(),
        SessionId = sessionId,
        Timestamp = DateTimeOffset.UtcNow,
        HttpMethod = method,
        Url = url,
        RequestHeaders = new Dictionary<string, string>(),
        ResponseStatus = 200,
        ResponseHeaders = new Dictionary<string, string>(),
        DurationMs = 50
    };

    public void Dispose() => _db.Dispose();
}
```

- [ ] **Step 10: Run test to verify it fails**

```bash
dotnet test tests/Iaet.Catalog.Tests --filter "SqliteCatalogTests" -v n
```

Expected: FAIL — `SqliteCatalog` does not exist.

- [ ] **Step 11: Implement SqliteCatalog**

Create `src/Iaet.Catalog/SqliteCatalog.cs`:

```csharp
using System.Text.Json;
using Iaet.Catalog.Entities;
using Iaet.Core.Abstractions;
using Iaet.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Iaet.Catalog;

public sealed class SqliteCatalog : IEndpointCatalog
{
    private readonly CatalogDbContext _db;

    public SqliteCatalog(CatalogDbContext db)
    {
        _db = db;
    }

    public async Task SaveSessionAsync(CaptureSessionInfo session, CancellationToken ct = default)
    {
        _db.Sessions.Add(new CaptureSessionEntity
        {
            Id = session.Id,
            Name = session.Name,
            TargetApplication = session.TargetApplication,
            Profile = session.Profile,
            StartedAt = session.StartedAt,
            StoppedAt = session.StoppedAt
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task SaveRequestAsync(CapturedRequest request, CancellationToken ct = default)
    {
        var normalized = EndpointNormalizer.Normalize(request.HttpMethod, request.Url);

        _db.Requests.Add(new CapturedRequestEntity
        {
            Id = request.Id,
            SessionId = request.SessionId,
            Timestamp = request.Timestamp,
            HttpMethod = request.HttpMethod,
            Url = request.Url,
            NormalizedSignature = normalized,
            RequestHeaders = JsonSerializer.Serialize(request.RequestHeaders),
            RequestBody = request.RequestBody,
            ResponseStatus = request.ResponseStatus,
            ResponseHeaders = JsonSerializer.Serialize(request.ResponseHeaders),
            ResponseBody = request.ResponseBody,
            DurationMs = request.DurationMs,
            Tag = request.Tag
        });

        // Upsert endpoint group
        var group = await _db.EndpointGroups
            .FirstOrDefaultAsync(g => g.SessionId == request.SessionId
                && g.NormalizedSignature == normalized, ct);

        if (group is null)
        {
            _db.EndpointGroups.Add(new EndpointGroupEntity
            {
                Id = Guid.NewGuid(),
                SessionId = request.SessionId,
                NormalizedSignature = normalized,
                ObservationCount = 1,
                FirstSeen = request.Timestamp,
                LastSeen = request.Timestamp
            });
        }
        else
        {
            group.ObservationCount++;
            group.LastSeen = request.Timestamp;
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<CaptureSessionInfo>> ListSessionsAsync(CancellationToken ct = default)
    {
        return await _db.Sessions
            .OrderByDescending(s => s.StartedAt)
            .Select(s => new CaptureSessionInfo
            {
                Id = s.Id,
                Name = s.Name,
                TargetApplication = s.TargetApplication,
                Profile = s.Profile,
                StartedAt = s.StartedAt,
                StoppedAt = s.StoppedAt,
                CapturedRequestCount = s.Requests.Count
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CapturedRequest>> GetRequestsBySessionAsync(
        Guid sessionId, CancellationToken ct = default)
    {
        return await _db.Requests
            .Where(r => r.SessionId == sessionId)
            .OrderBy(r => r.Timestamp)
            .Select(r => new CapturedRequest
            {
                Id = r.Id,
                SessionId = r.SessionId,
                Timestamp = r.Timestamp,
                HttpMethod = r.HttpMethod,
                Url = r.Url,
                RequestHeaders = r.RequestHeaders != null
                    ? JsonSerializer.Deserialize<Dictionary<string, string>>(r.RequestHeaders)!
                    : new Dictionary<string, string>(),
                RequestBody = r.RequestBody,
                ResponseStatus = r.ResponseStatus,
                ResponseHeaders = r.ResponseHeaders != null
                    ? JsonSerializer.Deserialize<Dictionary<string, string>>(r.ResponseHeaders)!
                    : new Dictionary<string, string>(),
                ResponseBody = r.ResponseBody,
                DurationMs = r.DurationMs,
                Tag = r.Tag
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<EndpointGroup>> GetEndpointGroupsAsync(
        Guid sessionId, CancellationToken ct = default)
    {
        // Materialize from DB first, then map in memory (EF cannot translate
        // EndpointSignature.FromRequest or string.Split to SQL)
        var raw = await _db.EndpointGroups
            .Where(g => g.SessionId == sessionId)
            .OrderByDescending(g => g.ObservationCount)
            .ToListAsync(ct);

        return raw.Select(g =>
        {
            var parts = g.NormalizedSignature.Split(' ', 2);
            return new EndpointGroup(
                EndpointSignature.FromRequest(parts[0], parts[1]),
                g.ObservationCount,
                g.FirstSeen,
                g.LastSeen);
        }).ToList();
    }
}
```

- [ ] **Step 12: Run tests**

```bash
dotnet test tests/Iaet.Catalog.Tests -v n
```

Expected: All tests PASS.

- [ ] **Step 13: Create initial EF Core migration**

```bash
dotnet tool install --global dotnet-ef || true
dotnet ef migrations add InitialCreate --project src/Iaet.Catalog --startup-project src/Iaet.Catalog -- --provider Microsoft.EntityFrameworkCore.Sqlite
```

Note: You may need a temporary design-time factory. If the migration command fails, add to `src/Iaet.Catalog/CatalogDbContextFactory.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Iaet.Catalog;

public class CatalogDbContextFactory : IDesignTimeDbContextFactory<CatalogDbContext>
{
    public CatalogDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseSqlite("DataSource=catalog.db")
            .Options;
        return new CatalogDbContext(options);
    }
}
```

Then re-run the migration command.

- [ ] **Step 14: Commit**

```bash
git add src/Iaet.Catalog/ tests/Iaet.Catalog.Tests/
git commit -m "feat: add Iaet.Catalog with SQLite persistence, endpoint grouping, and deduplication"
```

---

### Task 6: Iaet.Cli — dotnet Tool with Capture and Catalog Commands

**Files:**
- Create: `src/Iaet.Cli/Iaet.Cli.csproj`
- Create: `src/Iaet.Cli/Program.cs`
- Create: `src/Iaet.Cli/Commands/CaptureCommand.cs`
- Create: `src/Iaet.Cli/Commands/CatalogCommand.cs`

- [ ] **Step 1: Create Iaet.Cli project as a dotnet tool**

```bash
dotnet new console -n Iaet.Cli -o src/Iaet.Cli
rm src/Iaet.Cli/Program.cs
dotnet sln add src/Iaet.Cli/Iaet.Cli.csproj
dotnet add src/Iaet.Cli reference src/Iaet.Core
dotnet add src/Iaet.Cli reference src/Iaet.Capture
dotnet add src/Iaet.Cli reference src/Iaet.Catalog
dotnet add src/Iaet.Cli package System.CommandLine --prerelease
dotnet add src/Iaet.Cli package Microsoft.EntityFrameworkCore.Sqlite
dotnet add src/Iaet.Cli package Serilog.Extensions.Logging
dotnet add src/Iaet.Cli package Serilog.Sinks.Console
dotnet add src/Iaet.Cli package Serilog.Sinks.File
```

Edit `src/Iaet.Cli/Iaet.Cli.csproj` to add dotnet tool metadata — add inside the first `<PropertyGroup>`:

```xml
<PackAsTool>true</PackAsTool>
<ToolCommandName>iaet</ToolCommandName>
<PackageOutputPath>./nupkg</PackageOutputPath>
```

- [ ] **Step 2: Create CaptureCommand**

Create `src/Iaet.Cli/Commands/CaptureCommand.cs`:

```csharp
using System.CommandLine;
using Iaet.Capture;
using Iaet.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Iaet.Cli.Commands;

public static class CaptureCommand
{
    public static Command Create()
    {
        var captureCmd = new Command("capture", "Manage capture sessions");

        var startCmd = new Command("start", "Start a new capture session");
        var targetOption = new Option<string>("--target", "Target application name") { IsRequired = true };
        var profileOption = new Option<string>("--profile", () => "default", "Browser profile name");
        var urlOption = new Option<string>("--url", "Starting URL to navigate to") { IsRequired = true };
        var sessionOption = new Option<string>("--session", "Session name") { IsRequired = true };
        var dbOption = new Option<string>("--db", () => "catalog.db", "SQLite database path");

        startCmd.AddOption(targetOption);
        startCmd.AddOption(profileOption);
        startCmd.AddOption(urlOption);
        startCmd.AddOption(sessionOption);
        startCmd.AddOption(dbOption);

        startCmd.SetHandler(async (target, profile, url, sessionName, dbPath) =>
        {
            Console.WriteLine($"Starting capture session '{sessionName}' for {target}...");
            Console.WriteLine("Browser will open. Perform actions, then press Enter to stop.");

            var options = new DbContextOptionsBuilder<CatalogDbContext>()
                .UseSqlite($"DataSource={dbPath}")
                .Options;
            await using var db = new CatalogDbContext(options);
            await db.Database.EnsureCreatedAsync();
            var catalog = new SqliteCatalog(db);

            await using var session = new PlaywrightCaptureSession(target, profile);
            var sessionInfo = new Iaet.Core.Models.CaptureSessionInfo
            {
                Id = session.SessionId,
                Name = sessionName,
                TargetApplication = target,
                Profile = profile,
                StartedAt = DateTimeOffset.UtcNow
            };
            await catalog.SaveSessionAsync(sessionInfo);

            await session.StartAsync(url);
            Console.WriteLine($"Recording... Session ID: {session.SessionId}");
            Console.ReadLine();

            // Drain captured requests to catalog
            var count = 0;
            await foreach (var request in session.GetCapturedRequestsAsync())
            {
                await catalog.SaveRequestAsync(request);
                count++;
            }

            await session.StopAsync();
            Console.WriteLine($"Captured {count} requests.");

        }, targetOption, profileOption, urlOption, sessionOption, dbOption);

        captureCmd.AddCommand(startCmd);
        return captureCmd;
    }
}
```

- [ ] **Step 3: Create CatalogCommand**

Create `src/Iaet.Cli/Commands/CatalogCommand.cs`:

```csharp
using System.CommandLine;
using Iaet.Catalog;
using Microsoft.EntityFrameworkCore;

namespace Iaet.Cli.Commands;

public static class CatalogCommand
{
    public static Command Create()
    {
        var catalogCmd = new Command("catalog", "Browse the endpoint catalog");
        var dbOption = new Option<string>("--db", () => "catalog.db", "SQLite database path");

        var listCmd = new Command("sessions", "List capture sessions");
        listCmd.AddOption(dbOption);
        listCmd.SetHandler(async (dbPath) =>
        {
            var options = new DbContextOptionsBuilder<CatalogDbContext>()
                .UseSqlite($"DataSource={dbPath}")
                .Options;
            await using var db = new CatalogDbContext(options);

            var catalog = new SqliteCatalog(db);
            var sessions = await catalog.ListSessionsAsync();

            if (sessions.Count == 0)
            {
                Console.WriteLine("No sessions found.");
                return;
            }

            Console.WriteLine($"{"ID",-38} {"Name",-20} {"Target",-20} {"Requests",-10} {"Started"}");
            Console.WriteLine(new string('-', 110));
            foreach (var s in sessions)
            {
                Console.WriteLine($"{s.Id,-38} {s.Name,-20} {s.TargetApplication,-20} {s.CapturedRequestCount,-10} {s.StartedAt:g}");
            }
        }, dbOption);

        var endpointsCmd = new Command("endpoints", "List discovered endpoints for a session");
        var sessionIdOption = new Option<Guid>("--session-id", "Session ID to inspect") { IsRequired = true };
        endpointsCmd.AddOption(sessionIdOption);
        endpointsCmd.AddOption(dbOption);
        endpointsCmd.SetHandler(async (sessionId, dbPath) =>
        {
            var options = new DbContextOptionsBuilder<CatalogDbContext>()
                .UseSqlite($"DataSource={dbPath}")
                .Options;
            await using var db = new CatalogDbContext(options);

            var catalog = new SqliteCatalog(db);
            var groups = await catalog.GetEndpointGroupsAsync(sessionId);

            if (groups.Count == 0)
            {
                Console.WriteLine("No endpoints found for this session.");
                return;
            }

            Console.WriteLine($"{"Endpoint",-50} {"Count",-8} {"First Seen",-22} {"Last Seen"}");
            Console.WriteLine(new string('-', 105));
            foreach (var g in groups)
            {
                Console.WriteLine($"{g.Signature.Normalized,-50} {g.ObservationCount,-8} {g.FirstSeen:g,-22} {g.LastSeen:g}");
            }
        }, sessionIdOption, dbOption);

        catalogCmd.AddCommand(listCmd);
        catalogCmd.AddCommand(endpointsCmd);
        return catalogCmd;
    }
}
```

- [ ] **Step 4: Create Program.cs entry point**

Create `src/Iaet.Cli/Program.cs`:

```csharp
using System.CommandLine;
using Iaet.Cli.Commands;

var rootCommand = new RootCommand("IAET - Internal API Extraction Toolkit")
{
    CaptureCommand.Create(),
    CatalogCommand.Create()
};

return await rootCommand.InvokeAsync(args);
```

- [ ] **Step 5: Verify it builds and shows help**

```bash
dotnet run --project src/Iaet.Cli -- --help
dotnet run --project src/Iaet.Cli -- capture --help
dotnet run --project src/Iaet.Cli -- catalog --help
```

Expected: Help text shows `capture start` and `catalog sessions/endpoints` commands.

- [ ] **Step 6: Install Playwright browsers**

```bash
pwsh src/Iaet.Capture/bin/Debug/net8.0/playwright.ps1 install chromium
```

- [ ] **Step 7: Commit**

```bash
git add src/Iaet.Cli/
git commit -m "feat: add Iaet.Cli dotnet tool with capture start and catalog browse commands"
```

---

### Task 7: Phase 1 Risk Gate — Manual GV Capture Session

This is a manual research step, not automated code. It validates the IAET pipeline and discovers GV's internal API patterns.

- [ ] **Step 1: Run a GV capture session**

```bash
dotnet run --project src/Iaet.Cli -- capture start --target "Google Voice" --profile gv-research --url https://voice.google.com --session gv-call-discovery-001 --db captures/gv-catalog.db
```

1. Log in to your research Google account in the browser that opens.
2. Place an outbound call from the GV web interface.
3. Wait for the call to connect, speak briefly, then hang up.
4. Press Enter in the terminal to stop capture.

- [ ] **Step 2: Inspect captured endpoints**

```bash
dotnet run --project src/Iaet.Cli -- catalog sessions --db captures/gv-catalog.db
dotnet run --project src/Iaet.Cli -- catalog endpoints --session-id <SESSION_ID> --db captures/gv-catalog.db
```

- [ ] **Step 3: Document findings**

Create `docs/gv-discovery-notes.md` with:
- List of discovered call-related endpoints
- Audio transport observations (WebRTC? SRTP? Other?)
- Authentication patterns (cookies? Bearer tokens? Custom headers?)
- Any Protobuf or binary payloads observed
- Inbound call detection mechanism (if observable)

**This is the Phase 1 risk gate.** Based on findings:
- If audio is standard WebRTC → proceed to Phase 2 as planned.
- If audio is non-standard → adjust `IGvAudioChannel` implementation strategy per spec Section 4.3.1.
- If no audio transport is accessible → pivot to call-control-only gateway.

- [ ] **Step 4: Commit findings**

```bash
git add docs/gv-discovery-notes.md
git commit -m "docs: add GV API discovery findings from Phase 1 capture sessions"
```

---

## Phase 2: Vertical Slice

> **Note:** Tasks 8-14 assume Phase 1 discovery confirmed standard WebRTC audio. If findings differ, tasks will be adjusted per spec Section 4.3.1 before proceeding.

### Task 8: Iaet.Adapters.GoogleVoice — Call Endpoint Mapping

**Files:**
- Create: `src/Iaet.Adapters.GoogleVoice/Iaet.Adapters.GoogleVoice.csproj`
- Create: `src/Iaet.Adapters.GoogleVoice/GoogleVoiceAdapter.cs`
- Create: `src/Iaet.Adapters.GoogleVoice/GvEndpointPatterns.cs`

- [ ] **Step 1: Create adapter project**

```bash
dotnet new classlib -n Iaet.Adapters.GoogleVoice -o src/Iaet.Adapters.GoogleVoice
rm src/Iaet.Adapters.GoogleVoice/Class1.cs
dotnet sln add src/Iaet.Adapters.GoogleVoice/Iaet.Adapters.GoogleVoice.csproj
dotnet add src/Iaet.Adapters.GoogleVoice reference src/Iaet.Core
```

- [ ] **Step 2: Create GvEndpointPatterns**

This file maps discovered GV URL patterns to human-readable categories. Update the patterns based on Phase 1 discovery findings.

Create `src/Iaet.Adapters.GoogleVoice/GvEndpointPatterns.cs`:

```csharp
using System.Text.RegularExpressions;

namespace Iaet.Adapters.GoogleVoice;

public static partial class GvEndpointPatterns
{
    // TODO: Update these patterns based on Phase 1 capture findings
    // Placeholder patterns based on expected GV internal API structure
    public static readonly (Regex Pattern, string Category, string HumanName, bool IsDestructive)[] CallPatterns =
    [
        (CallInitiatePattern(), "calls", "Initiate Call", false),
        (CallStatusPattern(), "calls", "Get Call Status", false),
        (CallHangupPattern(), "calls", "Hangup Call", false),
        (CallHistoryPattern(), "calls", "Call History", false),
    ];

    [GeneratedRegex(@"/voice/call/initiate", RegexOptions.IgnoreCase)]
    private static partial Regex CallInitiatePattern();

    [GeneratedRegex(@"/voice/call/status", RegexOptions.IgnoreCase)]
    private static partial Regex CallStatusPattern();

    [GeneratedRegex(@"/voice/call/hangup", RegexOptions.IgnoreCase)]
    private static partial Regex CallHangupPattern();

    [GeneratedRegex(@"/voice/call/history", RegexOptions.IgnoreCase)]
    private static partial Regex CallHistoryPattern();
}
```

- [ ] **Step 3: Create GoogleVoiceAdapter**

Create `src/Iaet.Adapters.GoogleVoice/GoogleVoiceAdapter.cs`:

```csharp
using Iaet.Core.Abstractions;
using Iaet.Core.Models;

namespace Iaet.Adapters.GoogleVoice;

public sealed class GoogleVoiceAdapter : IApiAdapter
{
    public string TargetName => "Google Voice";

    public bool CanHandle(CapturedRequest request)
    {
        return request.Url.Contains("voice.google.com", StringComparison.OrdinalIgnoreCase)
            || request.Url.Contains("clients6.google.com", StringComparison.OrdinalIgnoreCase);
    }

    public EndpointDescriptor Describe(CapturedRequest request)
    {
        foreach (var (pattern, category, name, isDestructive) in GvEndpointPatterns.CallPatterns)
        {
            if (pattern.IsMatch(request.Url))
            {
                return new EndpointDescriptor(name, category, isDestructive, Notes: null);
            }
        }

        return new EndpointDescriptor(
            HumanName: "Unknown GV Endpoint",
            Category: "unknown",
            IsDestructive: false,
            Notes: $"URL: {request.Url}"
        );
    }
}
```

- [ ] **Step 4: Verify it builds**

```bash
dotnet build src/Iaet.Adapters.GoogleVoice
```

- [ ] **Step 5: Commit**

```bash
git add src/Iaet.Adapters.GoogleVoice/
git commit -m "feat: add GoogleVoice adapter with call endpoint pattern matching"
```

---

### Task 9: GvResearch.Shared — Token Service and Encryption

**Files:**
- Create: `src/GvResearch.Shared/GvResearch.Shared.csproj`
- Create: `src/GvResearch.Shared/Auth/IGvTokenService.cs`
- Create: `src/GvResearch.Shared/Auth/TokenEncryption.cs`
- Create: `src/GvResearch.Shared/Auth/EncryptedFileTokenService.cs`
- Test: `tests/GvResearch.Shared.Tests/GvResearch.Shared.Tests.csproj`
- Test: `tests/GvResearch.Shared.Tests/Auth/TokenEncryptionTests.cs`
- Test: `tests/GvResearch.Shared.Tests/Auth/EncryptedFileTokenServiceTests.cs`

- [ ] **Step 1: Create GvResearch.Shared project**

```bash
dotnet new classlib -n GvResearch.Shared -o src/GvResearch.Shared
rm src/GvResearch.Shared/Class1.cs
dotnet sln add src/GvResearch.Shared/GvResearch.Shared.csproj
dotnet add src/GvResearch.Shared reference src/Iaet.Core
dotnet add src/GvResearch.Shared package Microsoft.Extensions.Http
dotnet add src/GvResearch.Shared package Microsoft.Extensions.Options
dotnet add src/GvResearch.Shared package System.Threading.RateLimiting
```

- [ ] **Step 2: Create test project**

```bash
dotnet new xunit -n GvResearch.Shared.Tests -o tests/GvResearch.Shared.Tests
rm tests/GvResearch.Shared.Tests/UnitTest1.cs
dotnet sln add tests/GvResearch.Shared.Tests/GvResearch.Shared.Tests.csproj
dotnet add tests/GvResearch.Shared.Tests reference src/GvResearch.Shared
dotnet add tests/GvResearch.Shared.Tests package FluentAssertions
dotnet add tests/GvResearch.Shared.Tests package NSubstitute
```

- [ ] **Step 3: Write TokenEncryption tests (TDD)**

Create `tests/GvResearch.Shared.Tests/Auth/TokenEncryptionTests.cs`:

```csharp
using FluentAssertions;
using GvResearch.Shared.Auth;

namespace GvResearch.Shared.Tests.Auth;

public class TokenEncryptionTests
{
    [Fact]
    public void EncryptDecrypt_RoundTrips()
    {
        var key = TokenEncryption.GenerateKey();
        var plaintext = "my-secret-token-value";

        var encrypted = TokenEncryption.Encrypt(plaintext, key);
        var decrypted = TokenEncryption.Decrypt(encrypted, key);

        decrypted.Should().Be(plaintext);
    }

    [Fact]
    public void Encrypt_ProducesDifferentCiphertextEachTime()
    {
        var key = TokenEncryption.GenerateKey();
        var plaintext = "same-input";

        var a = TokenEncryption.Encrypt(plaintext, key);
        var b = TokenEncryption.Encrypt(plaintext, key);

        a.Should().NotEqual(b); // Different IVs
    }

    [Fact]
    public void Decrypt_WithWrongKey_Throws()
    {
        var key1 = TokenEncryption.GenerateKey();
        var key2 = TokenEncryption.GenerateKey();
        var encrypted = TokenEncryption.Encrypt("secret", key1);

        var act = () => TokenEncryption.Decrypt(encrypted, key2);
        act.Should().Throw<System.Security.Cryptography.CryptographicException>();
    }
}
```

- [ ] **Step 4: Run test to verify it fails**

```bash
dotnet test tests/GvResearch.Shared.Tests --filter "TokenEncryptionTests" -v n
```

Expected: FAIL — `TokenEncryption` does not exist.

- [ ] **Step 5: Implement TokenEncryption**

Create `src/GvResearch.Shared/Auth/TokenEncryption.cs`:

```csharp
using System.Security.Cryptography;

namespace GvResearch.Shared.Auth;

public static class TokenEncryption
{
    private const int KeySize = 32; // AES-256
    private const int IvSize = 16;

    public static byte[] GenerateKey()
    {
        return RandomNumberGenerator.GetBytes(KeySize);
    }

    public static byte[] Encrypt(string plaintext, byte[] key)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var plaintextBytes = System.Text.Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = encryptor.TransformFinalBlock(plaintextBytes, 0, plaintextBytes.Length);

        // Prepend IV to ciphertext
        var result = new byte[IvSize + ciphertext.Length];
        aes.IV.CopyTo(result, 0);
        ciphertext.CopyTo(result, IvSize);
        return result;
    }

    public static string Decrypt(byte[] encryptedData, byte[] key)
    {
        var iv = encryptedData[..IvSize];
        var ciphertext = encryptedData[IvSize..];

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        var plaintextBytes = decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
        return System.Text.Encoding.UTF8.GetString(plaintextBytes);
    }
}
```

- [ ] **Step 6: Run test to verify it passes**

```bash
dotnet test tests/GvResearch.Shared.Tests --filter "TokenEncryptionTests" -v n
```

Expected: All 3 tests PASS.

- [ ] **Step 7: Create IGvTokenService interface**

Create `src/GvResearch.Shared/Auth/IGvTokenService.cs`:

```csharp
namespace GvResearch.Shared.Auth;

public interface IGvTokenService
{
    Task<string> GetValidTokenAsync(CancellationToken ct = default);
    Task RefreshTokenAsync(CancellationToken ct = default);
    event EventHandler<TokenExpiredEventArgs>? TokenExpired;
}

public sealed class TokenExpiredEventArgs : EventArgs
{
    public required string Reason { get; init; }
}
```

- [ ] **Step 8: Write EncryptedFileTokenService tests (TDD)**

Create `tests/GvResearch.Shared.Tests/Auth/EncryptedFileTokenServiceTests.cs`:

```csharp
using FluentAssertions;
using GvResearch.Shared.Auth;

namespace GvResearch.Shared.Tests.Auth;

public class EncryptedFileTokenServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _tokenPath;
    private readonly string _keyPath;

    public EncryptedFileTokenServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        _tokenPath = Path.Combine(_tempDir, "tokens.enc");
        _keyPath = Path.Combine(_tempDir, "key.bin");

        // Pre-create a key file
        var key = TokenEncryption.GenerateKey();
        File.WriteAllBytes(_keyPath, key);

        // Pre-create an encrypted token file
        var encrypted = TokenEncryption.Encrypt("test-auth-token", key);
        File.WriteAllBytes(_tokenPath, encrypted);
    }

    [Fact]
    public async Task GetValidTokenAsync_ReadsEncryptedFile()
    {
        var service = new EncryptedFileTokenService(_tokenPath, _keyPath);
        var token = await service.GetValidTokenAsync();
        token.Should().Be("test-auth-token");
    }

    [Fact]
    public async Task GetValidTokenAsync_CachesInMemory()
    {
        var service = new EncryptedFileTokenService(_tokenPath, _keyPath);
        var token1 = await service.GetValidTokenAsync();
        File.Delete(_tokenPath); // Remove file
        var token2 = await service.GetValidTokenAsync(); // Should use cache
        token2.Should().Be(token1);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }
}
```

- [ ] **Step 9: Run test to verify it fails**

```bash
dotnet test tests/GvResearch.Shared.Tests --filter "EncryptedFileTokenServiceTests" -v n
```

Expected: FAIL — `EncryptedFileTokenService` does not exist.

- [ ] **Step 10: Implement EncryptedFileTokenService**

Create `src/GvResearch.Shared/Auth/EncryptedFileTokenService.cs`:

```csharp
namespace GvResearch.Shared.Auth;

public sealed class EncryptedFileTokenService : IGvTokenService
{
    private readonly string _tokenPath;
    private readonly string _keyPath;
    private string? _cachedToken;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public event EventHandler<TokenExpiredEventArgs>? TokenExpired;

    public EncryptedFileTokenService(string tokenPath, string keyPath)
    {
        _tokenPath = tokenPath;
        _keyPath = keyPath;
    }

    public async Task<string> GetValidTokenAsync(CancellationToken ct = default)
    {
        if (_cachedToken is not null) return _cachedToken;

        await _lock.WaitAsync(ct);
        try
        {
            if (_cachedToken is not null) return _cachedToken;

            var key = await File.ReadAllBytesAsync(_keyPath, ct);
            var encrypted = await File.ReadAllBytesAsync(_tokenPath, ct);
            _cachedToken = TokenEncryption.Decrypt(encrypted, key);
            return _cachedToken;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task RefreshTokenAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            _cachedToken = null;
            // Re-read from file (assumes an external process has updated the token file)
            var key = await File.ReadAllBytesAsync(_keyPath, ct);
            var encrypted = await File.ReadAllBytesAsync(_tokenPath, ct);
            _cachedToken = TokenEncryption.Decrypt(encrypted, key);
        }
        catch (Exception ex)
        {
            TokenExpired?.Invoke(this, new TokenExpiredEventArgs
            {
                Reason = $"Token refresh failed: {ex.Message}. Run 'iaet capture' to obtain fresh tokens."
            });
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }
}
```

- [ ] **Step 11: Run tests**

```bash
dotnet test tests/GvResearch.Shared.Tests -v n
```

Expected: All tests PASS.

- [ ] **Step 12: Commit**

```bash
git add src/GvResearch.Shared/Auth/ tests/GvResearch.Shared.Tests/
git commit -m "feat: add encrypted token service with AES-256 file storage and in-memory caching"
```

---

### Task 10: GvResearch.Shared — Call Service and Rate Limiter

**Files:**
- Create: `src/GvResearch.Shared/Models/GvCallResult.cs`
- Create: `src/GvResearch.Shared/Models/GvCallStatus.cs`
- Create: `src/GvResearch.Shared/Models/GvCallEvent.cs`
- Create: `src/GvResearch.Shared/Services/IGvCallService.cs`
- Create: `src/GvResearch.Shared/Services/GvCallService.cs`
- Create: `src/GvResearch.Shared/Http/GvHttpClientHandler.cs`
- Create: `src/GvResearch.Shared/RateLimiting/GvRateLimiter.cs`
- Test: `tests/GvResearch.Shared.Tests/RateLimiting/GvRateLimiterTests.cs`
- Test: `tests/GvResearch.Shared.Tests/Services/GvCallServiceTests.cs`

- [ ] **Step 1: Create models**

Create `src/GvResearch.Shared/Models/GvCallResult.cs`:

```csharp
namespace GvResearch.Shared.Models;

public sealed record GvCallResult(
    string GvCallId,
    bool Success,
    string? ErrorMessage
);
```

Create `src/GvResearch.Shared/Models/GvCallStatus.cs`:

```csharp
namespace GvResearch.Shared.Models;

public enum GvCallStatusType { Ringing, Active, Ended, Failed }

public sealed record GvCallStatus(
    string GvCallId,
    GvCallStatusType Status,
    DateTimeOffset Timestamp
);
```

Create `src/GvResearch.Shared/Models/GvCallEvent.cs`:

```csharp
namespace GvResearch.Shared.Models;

public sealed record GvCallEvent(
    string GvCallId,
    GvCallEventType EventType,
    string? RemoteNumber,
    DateTimeOffset Timestamp
);

public enum GvCallEventType
{
    IncomingRing,
    Answered,
    Ended,
    Missed,
    VoicemailStarted
}
```

- [ ] **Step 2: Create IGvCallService interface**

Create `src/GvResearch.Shared/Services/IGvCallService.cs`:

```csharp
using GvResearch.Shared.Models;

namespace GvResearch.Shared.Services;

public interface IGvCallService
{
    Task<GvCallResult> InitiateCallAsync(string destinationNumber, CancellationToken ct = default);
    Task<GvCallStatus> GetCallStatusAsync(string gvCallId, CancellationToken ct = default);
    Task HangupAsync(string gvCallId, CancellationToken ct = default);
    IAsyncEnumerable<GvCallEvent> ListenForEventsAsync(CancellationToken ct = default);
}
```

- [ ] **Step 3: Write rate limiter tests (TDD)**

Create `tests/GvResearch.Shared.Tests/RateLimiting/GvRateLimiterTests.cs`:

```csharp
using FluentAssertions;
using GvResearch.Shared.RateLimiting;

namespace GvResearch.Shared.Tests.RateLimiting;

public class GvRateLimiterTests
{
    [Fact]
    public async Task AcquireAsync_AllowsWithinLimit()
    {
        var limiter = new GvRateLimiter(requestsPerMinute: 5, requestsPerDay: 100);

        var acquired = await limiter.TryAcquireAsync("test-endpoint");
        acquired.Should().BeTrue();
    }

    [Fact]
    public async Task AcquireAsync_BlocksWhenPerMinuteLimitExceeded()
    {
        var limiter = new GvRateLimiter(requestsPerMinute: 2, requestsPerDay: 100);

        (await limiter.TryAcquireAsync("ep")).Should().BeTrue();
        (await limiter.TryAcquireAsync("ep")).Should().BeTrue();
        (await limiter.TryAcquireAsync("ep")).Should().BeFalse();
    }

    [Fact]
    public async Task AcquireAsync_TracksPerEndpoint()
    {
        var limiter = new GvRateLimiter(requestsPerMinute: 1, requestsPerDay: 100);

        (await limiter.TryAcquireAsync("ep-a")).Should().BeTrue();
        (await limiter.TryAcquireAsync("ep-b")).Should().BeTrue(); // Different endpoint
        (await limiter.TryAcquireAsync("ep-a")).Should().BeFalse(); // Same endpoint, over limit
    }
}
```

- [ ] **Step 4: Run test to verify it fails**

```bash
dotnet test tests/GvResearch.Shared.Tests --filter "GvRateLimiterTests" -v n
```

Expected: FAIL.

- [ ] **Step 5: Implement GvRateLimiter**

Create `src/GvResearch.Shared/RateLimiting/GvRateLimiter.cs`:

```csharp
using System.Collections.Concurrent;
using System.Threading.RateLimiting;

namespace GvResearch.Shared.RateLimiting;

public sealed class GvRateLimiter : IDisposable
{
    private readonly ConcurrentDictionary<string, FixedWindowRateLimiter> _minuteLimiters = new();
    private readonly ConcurrentDictionary<string, FixedWindowRateLimiter> _dayLimiters = new();
    private readonly int _requestsPerMinute;
    private readonly int _requestsPerDay;

    public GvRateLimiter(int requestsPerMinute = 10, int requestsPerDay = 100)
    {
        _requestsPerMinute = requestsPerMinute;
        _requestsPerDay = requestsPerDay;
    }

    public async Task<bool> TryAcquireAsync(string endpoint, CancellationToken ct = default)
    {
        var minuteLimiter = _minuteLimiters.GetOrAdd(endpoint, _ =>
            new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
            {
                PermitLimit = _requestsPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

        var dayLimiter = _dayLimiters.GetOrAdd(endpoint, _ =>
            new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
            {
                PermitLimit = _requestsPerDay,
                Window = TimeSpan.FromDays(1),
                QueueLimit = 0
            }));

        using var minuteLease = await minuteLimiter.AcquireAsync(1, ct);
        if (!minuteLease.IsAcquired) return false;

        using var dayLease = await dayLimiter.AcquireAsync(1, ct);
        return dayLease.IsAcquired;
    }

    public void Dispose()
    {
        foreach (var limiter in _minuteLimiters.Values) limiter.Dispose();
        foreach (var limiter in _dayLimiters.Values) limiter.Dispose();
    }
}
```

- [ ] **Step 6: Run test to verify it passes**

```bash
dotnet test tests/GvResearch.Shared.Tests --filter "GvRateLimiterTests" -v n
```

Expected: All 3 tests PASS.

- [ ] **Step 7: Create GvHttpClientHandler (token-injecting delegating handler)**

Create `src/GvResearch.Shared/Http/GvHttpClientHandler.cs`:

```csharp
using GvResearch.Shared.Auth;

namespace GvResearch.Shared.Http;

public sealed class GvHttpClientHandler : DelegatingHandler
{
    private readonly IGvTokenService _tokenService;

    public GvHttpClientHandler(IGvTokenService tokenService)
    {
        _tokenService = tokenService;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await _tokenService.GetValidTokenAsync(cancellationToken);
        // TODO: Determine correct header format from Phase 1 findings
        // Could be Cookie, Authorization: Bearer, or custom header
        request.Headers.TryAddWithoutValidation("Cookie", token);
        return await base.SendAsync(request, cancellationToken);
    }
}
```

- [ ] **Step 8: Write GvCallService tests (TDD)**

Create `tests/GvResearch.Shared.Tests/Services/GvCallServiceTests.cs`:

```csharp
using System.Net;
using System.Text.Json;
using FluentAssertions;
using GvResearch.Shared.Models;
using GvResearch.Shared.RateLimiting;
using GvResearch.Shared.Services;
using NSubstitute;

namespace GvResearch.Shared.Tests.Services;

public class GvCallServiceTests
{
    private readonly GvRateLimiter _rateLimiter = new(requestsPerMinute: 10, requestsPerDay: 100);

    [Fact]
    public async Task InitiateCallAsync_SendsRequestAndReturnsResult()
    {
        var handler = new FakeHttpHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(
                new { callId = "call-123", success = true }))
        });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://voice.google.com") };
        var service = new GvCallService(httpClient, _rateLimiter);

        var result = await service.InitiateCallAsync("+15551234567");

        result.Should().NotBeNull();
        result.GvCallId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task InitiateCallAsync_RateLimited_ReturnsError()
    {
        var handler = new FakeHttpHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(
                new { callId = "call-1", success = true }))
        });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://voice.google.com") };
        var limiter = new GvRateLimiter(requestsPerMinute: 1, requestsPerDay: 100);
        var service = new GvCallService(httpClient, limiter);

        await service.InitiateCallAsync("+15551111111"); // Uses up the limit
        var result = await service.InitiateCallAsync("+15552222222"); // Should be rate limited

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("rate limit");
    }

    private sealed class FakeHttpHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(response);
    }
}
```

- [ ] **Step 9: Run test to verify it fails**

```bash
dotnet test tests/GvResearch.Shared.Tests --filter "GvCallServiceTests" -v n
```

Expected: FAIL — `GvCallService` does not exist.

- [ ] **Step 10: Implement GvCallService**

Create `src/GvResearch.Shared/Services/GvCallService.cs`:

```csharp
using System.Runtime.CompilerServices;
using System.Text.Json;
using GvResearch.Shared.Models;
using GvResearch.Shared.RateLimiting;
using Microsoft.Extensions.Logging;

namespace GvResearch.Shared.Services;

public sealed class GvCallService : IGvCallService
{
    private readonly HttpClient _httpClient;
    private readonly GvRateLimiter _rateLimiter;
    private readonly ILogger<GvCallService>? _logger;

    // TODO: Update endpoint paths based on Phase 1 discovery findings
    private const string InitiateEndpoint = "/voice/call/initiate";
    private const string StatusEndpoint = "/voice/call/status";
    private const string HangupEndpoint = "/voice/call/hangup";

    public GvCallService(HttpClient httpClient, GvRateLimiter rateLimiter, ILogger<GvCallService>? logger = null)
    {
        _httpClient = httpClient;
        _rateLimiter = rateLimiter;
        _logger = logger;
    }

    public async Task<GvCallResult> InitiateCallAsync(string destinationNumber, CancellationToken ct = default)
    {
        if (!await _rateLimiter.TryAcquireAsync(InitiateEndpoint, ct))
        {
            _logger?.LogWarning("Rate limit exceeded for call initiation");
            return new GvCallResult(GvCallId: "", Success: false, ErrorMessage: "rate limit exceeded");
        }

        try
        {
            // TODO: Update request format based on Phase 1 discovery
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["outgoing_number"] = destinationNumber,
            });

            var response = await _httpClient.PostAsync(InitiateEndpoint, content, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                return new GvCallResult("", false, $"HTTP {response.StatusCode}: {body}");
            }

            // TODO: Parse actual GV response format
            var callId = Guid.NewGuid().ToString(); // Placeholder
            return new GvCallResult(callId, true, null);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initiate call to {Destination}", destinationNumber);
            return new GvCallResult("", false, ex.Message);
        }
    }

    public async Task<GvCallStatus> GetCallStatusAsync(string gvCallId, CancellationToken ct = default)
    {
        if (!await _rateLimiter.TryAcquireAsync(StatusEndpoint, ct))
        {
            return new GvCallStatus(gvCallId, GvCallStatusType.Failed, DateTimeOffset.UtcNow);
        }

        // TODO: Implement based on Phase 1 discovery
        return new GvCallStatus(gvCallId, GvCallStatusType.Active, DateTimeOffset.UtcNow);
    }

    public async Task HangupAsync(string gvCallId, CancellationToken ct = default)
    {
        if (!await _rateLimiter.TryAcquireAsync(HangupEndpoint, ct))
        {
            _logger?.LogWarning("Rate limit exceeded for hangup");
            return;
        }

        // TODO: Implement based on Phase 1 discovery
        await Task.CompletedTask;
    }

    public async IAsyncEnumerable<GvCallEvent> ListenForEventsAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // TODO: Implement based on Phase 1 discovery (polling, WebSocket, or SSE)
        // Placeholder: yields nothing until implemented
        await Task.CompletedTask;
        yield break;
    }
}
```

- [ ] **Step 11: Run tests**

```bash
dotnet test tests/GvResearch.Shared.Tests -v n
```

Expected: All tests PASS.

- [ ] **Step 12: Commit**

```bash
git add src/GvResearch.Shared/ tests/GvResearch.Shared.Tests/
git commit -m "feat: add GvResearch.Shared with call service, rate limiter, and HTTP handler"
```

---

### Task 11: GvResearch.Api — Calls REST Endpoints

**Files:**
- Create: `src/GvResearch.Api/GvResearch.Api.csproj`
- Create: `src/GvResearch.Api/Program.cs`
- Create: `src/GvResearch.Api/Endpoints/CallEndpoints.cs`
- Create: `src/GvResearch.Api/Models/CallRecord.cs`
- Create: `src/GvResearch.Api/Models/PagedResult.cs`
- Test: `tests/GvResearch.Api.Tests/GvResearch.Api.Tests.csproj`
- Test: `tests/GvResearch.Api.Tests/CallEndpointsTests.cs`

- [ ] **Step 1: Create GvResearch.Api project**

```bash
dotnet new web -n GvResearch.Api -o src/GvResearch.Api
rm src/GvResearch.Api/Program.cs
dotnet sln add src/GvResearch.Api/GvResearch.Api.csproj
dotnet add src/GvResearch.Api reference src/GvResearch.Shared
dotnet add src/GvResearch.Api package Asp.Versioning.Http
dotnet add src/GvResearch.Api package Serilog.AspNetCore
dotnet add src/GvResearch.Api package Microsoft.AspNetCore.OpenApi
dotnet add src/GvResearch.Api package Microsoft.Extensions.Http.Resilience
dotnet add src/GvResearch.Api package Microsoft.AspNetCore.Authentication.BearerToken
```

- [ ] **Step 2: Create models**

Create `src/GvResearch.Api/Models/CallRecord.cs`:

```csharp
namespace GvResearch.Api.Models;

public readonly record struct CallId(Guid Value);
public readonly record struct PhoneNumber(string Value);

public enum CallDirection { Inbound, Outbound }
public enum CallStatus { Ringing, Active, Completed, Missed, Voicemail, Failed }

public sealed record CallRecord(
    CallId Id,
    CallDirection Direction,
    PhoneNumber FromNumber,
    PhoneNumber ToNumber,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    int? DurationSeconds,
    CallStatus Status
);

public sealed record InitiateCallRequest(string FromNumber, string ToNumber);
```

Create `src/GvResearch.Api/Models/PagedResult.cs`:

```csharp
namespace GvResearch.Api.Models;

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    string? NextCursor,
    int Total
);
```

- [ ] **Step 3: Create CallEndpoints**

Create `src/GvResearch.Api/Endpoints/CallEndpoints.cs`:

```csharp
using GvResearch.Api.Models;
using GvResearch.Shared.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace GvResearch.Api.Endpoints;

public static class CallEndpoints
{
    public static RouteGroupBuilder MapCallEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/calls")
            .WithTags("Calls");

        group.MapGet("/", GetCalls)
            .WithName("GetCalls")
            .WithSummary("List call history");

        group.MapGet("/{id:guid}", GetCallById)
            .WithName("GetCallById")
            .WithSummary("Get single call record");

        group.MapPost("/", InitiateCall)
            .WithName("InitiateCall")
            .WithSummary("Initiate a call via Google Voice");

        return group;
    }

    private static Ok<PagedResult<CallRecord>> GetCalls(
        [FromQuery] int limit = 20,
        [FromQuery] string? cursor = null,
        [FromQuery] string? direction = null,
        [FromQuery] string? status = null)
    {
        // TODO: Query from GV or local cache
        // Placeholder: return empty result
        var result = new PagedResult<CallRecord>([], null, 0);
        return TypedResults.Ok(result);
    }

    private static Results<Ok<CallRecord>, NotFound> GetCallById(Guid id)
    {
        // TODO: Lookup from local cache/catalog
        return TypedResults.NotFound();
    }

    private static async Task<Results<Created<CallRecord>, BadRequest<ProblemDetails>>> InitiateCall(
        InitiateCallRequest request,
        IGvCallService callService)
    {
        if (string.IsNullOrWhiteSpace(request.ToNumber))
        {
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Invalid request",
                Detail = "ToNumber is required",
                Status = 400
            });
        }

        var result = await callService.InitiateCallAsync(request.ToNumber);

        if (!result.Success)
        {
            return TypedResults.BadRequest(new ProblemDetails
            {
                Title = "Call initiation failed",
                Detail = result.ErrorMessage,
                Status = 400
            });
        }

        var record = new CallRecord(
            Id: new CallId(Guid.NewGuid()),
            Direction: CallDirection.Outbound,
            FromNumber: new PhoneNumber(request.FromNumber),
            ToNumber: new PhoneNumber(request.ToNumber),
            StartedAt: DateTimeOffset.UtcNow,
            EndedAt: null,
            DurationSeconds: null,
            Status: CallStatus.Ringing
        );

        return TypedResults.Created($"/api/v1/calls/{record.Id.Value}", record);
    }
}
```

- [ ] **Step 4: Create Program.cs**

Create `src/GvResearch.Api/Program.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using GvResearch.Api.Endpoints;
using GvResearch.Shared.Auth;
using GvResearch.Shared.RateLimiting;
using GvResearch.Shared.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog (REQ-TECH-006)
builder.Host.UseSerilog((context, config) => config
    .ReadFrom.Configuration(context.Configuration)
    .WriteTo.Console()
    .WriteTo.File("logs/gvresearch-api-.log", rollingInterval: RollingInterval.Day));

// JSON serialization (REQ-GV-003)
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// OpenAPI (REQ-TECH-007)
builder.Services.AddOpenApi();

// Authentication (REQ-GV-002)
builder.Services.AddAuthentication("Bearer")
    .AddBearerToken("Bearer");
builder.Services.AddAuthorization();

// GV services
builder.Services.AddSingleton<GvRateLimiter>();
builder.Services.AddSingleton<IGvTokenService>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var tokenPath = config["GvResearch:TokenPath"] ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".gvresearch", "tokens.enc");
    var keyPath = config["GvResearch:KeyPath"] ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".gvresearch", "key.bin");
    return new EncryptedFileTokenService(tokenPath, keyPath);
});

builder.Services.AddHttpClient<IGvCallService, GvCallService>((sp, client) =>
{
    client.BaseAddress = new Uri("https://voice.google.com");
})
.AddHttpMessageHandler(sp =>
    new GvResearch.Shared.Http.GvHttpClientHandler(
        sp.GetRequiredService<IGvTokenService>()))
.AddStandardResilienceHandler(); // REQ-NFR-003: Polly retry + circuit breaker

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapCallEndpoints().RequireAuthorization();

app.Run();

// Make the implicit Program class public for WebApplicationFactory
public partial class Program { }
```

- [ ] **Step 5: Create appsettings.json**

Create `src/GvResearch.Api/appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft.AspNetCore": "Warning"
      }
    }
  },
  "GvResearch": {
    "TokenPath": "",
    "KeyPath": ""
  }
}
```

Create `src/GvResearch.Api/appsettings.Development.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug"
    }
  }
}
```

- [ ] **Step 6: Create test project and integration tests**

```bash
dotnet new xunit -n GvResearch.Api.Tests -o tests/GvResearch.Api.Tests
rm tests/GvResearch.Api.Tests/UnitTest1.cs
dotnet sln add tests/GvResearch.Api.Tests/GvResearch.Api.Tests.csproj
dotnet add tests/GvResearch.Api.Tests reference src/GvResearch.Api
dotnet add tests/GvResearch.Api.Tests reference src/GvResearch.Shared
dotnet add tests/GvResearch.Api.Tests package FluentAssertions
dotnet add tests/GvResearch.Api.Tests package NSubstitute
dotnet add tests/GvResearch.Api.Tests package Microsoft.AspNetCore.Mvc.Testing
```

Create `tests/GvResearch.Api.Tests/CallEndpointsTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using GvResearch.Api.Models;
using GvResearch.Shared.Models;
using GvResearch.Shared.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace GvResearch.Api.Tests;

public class CallEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public CallEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Bypass authentication for integration tests
                services.AddAuthentication("Test")
                    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions,
                        TestAuthHandler>("Test", _ => { });

                // Replace real GvCallService with mock
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(IGvCallService));
                if (descriptor is not null) services.Remove(descriptor);

                var mockCallService = Substitute.For<IGvCallService>();
                mockCallService.InitiateCallAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                    .Returns(new GvCallResult("call-test-123", true, null));

                services.AddSingleton(mockCallService);
            });
        }).CreateClient();
    }

    [Fact]
    public async Task GetCalls_ReturnsOkWithEmptyList()
    {
        var response = await _client.GetAsync("/api/v1/calls");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PagedResult<CallRecord>>();
        result.Should().NotBeNull();
        result!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task InitiateCall_ValidRequest_ReturnsCreated()
    {
        var request = new InitiateCallRequest("+15551111111", "+15552222222");
        var response = await _client.PostAsJsonAsync("/api/v1/calls", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task InitiateCall_MissingToNumber_ReturnsBadRequest()
    {
        var request = new InitiateCallRequest("+15551111111", "");
        var response = await _client.PostAsJsonAsync("/api/v1/calls", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}

// Test auth handler that auto-authenticates all requests
public class TestAuthHandler : Microsoft.AspNetCore.Authentication.AuthenticationHandler<
    Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions>
{
    public TestAuthHandler(
        Microsoft.Extensions.Options.IOptionsMonitor<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions> options,
        Microsoft.Extensions.Logging.ILoggerFactory logger,
        System.Text.Encodings.Web.UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<Microsoft.AspNetCore.Authentication.AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity = new System.Security.Claims.ClaimsIdentity("Test");
        var principal = new System.Security.Claims.ClaimsPrincipal(identity);
        var ticket = new Microsoft.AspNetCore.Authentication.AuthenticationTicket(principal, "Test");
        return Task.FromResult(Microsoft.AspNetCore.Authentication.AuthenticateResult.Success(ticket));
    }
}
```

- [ ] **Step 6: Run tests**

```bash
dotnet test tests/GvResearch.Api.Tests -v n
```

Expected: All 3 tests PASS.

- [ ] **Step 7: Commit**

```bash
git add src/GvResearch.Api/ tests/GvResearch.Api.Tests/
git commit -m "feat: add GvResearch.Api REST facade with calls endpoints and integration tests"
```

---

### Task 12: GvResearch.Sip — SIP Registrar

**Files:**
- Create: `src/GvResearch.Sip/GvResearch.Sip.csproj`
- Create: `src/GvResearch.Sip/Configuration/SipGatewayOptions.cs`
- Create: `src/GvResearch.Sip/Registrar/RegistrationStore.cs`
- Create: `src/GvResearch.Sip/Registrar/DigestAuthenticator.cs`
- Create: `src/GvResearch.Sip/Registrar/SipRegistrar.cs`
- Test: `tests/GvResearch.Sip.Tests/GvResearch.Sip.Tests.csproj`
- Test: `tests/GvResearch.Sip.Tests/Registrar/SipRegistrarTests.cs`

- [ ] **Step 1: Create GvResearch.Sip project**

```bash
dotnet new console -n GvResearch.Sip -o src/GvResearch.Sip
rm src/GvResearch.Sip/Program.cs
dotnet sln add src/GvResearch.Sip/GvResearch.Sip.csproj
dotnet add src/GvResearch.Sip reference src/GvResearch.Shared
dotnet add src/GvResearch.Sip package SIPSorcery
dotnet add src/GvResearch.Sip package Microsoft.Extensions.Hosting
dotnet add src/GvResearch.Sip package Serilog.Extensions.Hosting
dotnet add src/GvResearch.Sip package Serilog.Sinks.Console
dotnet add src/GvResearch.Sip package Serilog.Sinks.File
dotnet add src/GvResearch.Sip package Microsoft.Extensions.Http.Resilience
```

- [ ] **Step 2: Create test project**

```bash
dotnet new xunit -n GvResearch.Sip.Tests -o tests/GvResearch.Sip.Tests
rm tests/GvResearch.Sip.Tests/UnitTest1.cs
dotnet sln add tests/GvResearch.Sip.Tests/GvResearch.Sip.Tests.csproj
dotnet add tests/GvResearch.Sip.Tests reference src/GvResearch.Sip
dotnet add tests/GvResearch.Sip.Tests package FluentAssertions
dotnet add tests/GvResearch.Sip.Tests package NSubstitute
dotnet add tests/GvResearch.Sip.Tests package SIPSorcery
```

- [ ] **Step 3: Create SipGatewayOptions**

Create `src/GvResearch.Sip/Configuration/SipGatewayOptions.cs`:

```csharp
namespace GvResearch.Sip.Configuration;

public sealed class SipGatewayOptions
{
    public const string SectionName = "SipGateway";

    public int SipPort { get; set; } = 5060;
    public string SipDomain { get; set; } = "gvresearch.local";
    public List<SipAccountOptions> Accounts { get; set; } = [];
}

public sealed class SipAccountOptions
{
    public required string Username { get; set; }
    public required string Password { get; set; }
    public string? DisplayName { get; set; }
}
```

- [ ] **Step 4: Create RegistrationStore**

Create `src/GvResearch.Sip/Registrar/RegistrationStore.cs`:

```csharp
using System.Collections.Concurrent;
using SIPSorcery.SIP;

namespace GvResearch.Sip.Registrar;

public sealed class RegistrationStore
{
    private readonly ConcurrentDictionary<string, SipRegistration> _registrations = new();

    public void AddOrUpdate(string sipUri, SipRegistration registration)
    {
        _registrations[sipUri] = registration;
    }

    public bool TryGet(string sipUri, out SipRegistration? registration)
    {
        return _registrations.TryGetValue(sipUri, out registration);
    }

    public void Remove(string sipUri)
    {
        _registrations.TryRemove(sipUri, out _);
    }

    public IReadOnlyList<SipRegistration> GetAll()
    {
        return _registrations.Values
            .Where(r => r.Expiry > DateTimeOffset.UtcNow)
            .ToList();
    }
}

public sealed record SipRegistration(
    string SipUri,
    string ContactUri,
    string RemoteEndPoint,
    DateTimeOffset RegisteredAt,
    DateTimeOffset Expiry
);
```

- [ ] **Step 5: Write RegistrationStore tests (TDD)**

Create `tests/GvResearch.Sip.Tests/Registrar/RegistrationStoreTests.cs`:

```csharp
using FluentAssertions;
using GvResearch.Sip.Registrar;

namespace GvResearch.Sip.Tests.Registrar;

public class RegistrationStoreTests
{
    [Fact]
    public void AddOrUpdate_StoresRegistration()
    {
        var store = new RegistrationStore();
        var reg = MakeRegistration("sip:alice@gvresearch.local");

        store.AddOrUpdate(reg.SipUri, reg);
        store.TryGet("sip:alice@gvresearch.local", out var found).Should().BeTrue();
        found!.ContactUri.Should().Be(reg.ContactUri);
    }

    [Fact]
    public void GetAll_ExcludesExpired()
    {
        var store = new RegistrationStore();
        var active = MakeRegistration("sip:alice@local", expiresIn: TimeSpan.FromHours(1));
        var expired = MakeRegistration("sip:bob@local", expiresIn: TimeSpan.FromSeconds(-1));

        store.AddOrUpdate(active.SipUri, active);
        store.AddOrUpdate(expired.SipUri, expired);

        store.GetAll().Should().ContainSingle().Which.SipUri.Should().Be("sip:alice@local");
    }

    [Fact]
    public void Remove_DeletesRegistration()
    {
        var store = new RegistrationStore();
        var reg = MakeRegistration("sip:alice@local");
        store.AddOrUpdate(reg.SipUri, reg);

        store.Remove("sip:alice@local");
        store.TryGet("sip:alice@local", out _).Should().BeFalse();
    }

    private static SipRegistration MakeRegistration(string uri, TimeSpan? expiresIn = null)
        => new(
            SipUri: uri,
            ContactUri: $"<{uri}>",
            RemoteEndPoint: "192.168.1.100:5060",
            RegisteredAt: DateTimeOffset.UtcNow,
            Expiry: DateTimeOffset.UtcNow + (expiresIn ?? TimeSpan.FromHours(1))
        );
}
```

- [ ] **Step 6: Run tests**

```bash
dotnet test tests/GvResearch.Sip.Tests --filter "RegistrationStoreTests" -v n
```

Expected: All 3 tests PASS.

- [ ] **Step 7: Create DigestAuthenticator**

Create `src/GvResearch.Sip/Registrar/DigestAuthenticator.cs`:

```csharp
using GvResearch.Sip.Configuration;
using Microsoft.Extensions.Options;
using SIPSorcery.SIP;

namespace GvResearch.Sip.Registrar;

public sealed class DigestAuthenticator
{
    private readonly SipGatewayOptions _options;

    public DigestAuthenticator(IOptions<SipGatewayOptions> options)
    {
        _options = options.Value;
    }

    public bool TryAuthenticate(string username, string realm, string nonce,
        string uri, string response, out SipAccountOptions? account)
    {
        account = _options.Accounts.FirstOrDefault(a =>
            a.Username.Equals(username, StringComparison.OrdinalIgnoreCase));

        if (account is null) return false;

        // Verify digest response
        var ha1 = SIPAuthorisation.GetDigest(username, realm, account.Password);
        var ha2 = SIPAuthorisation.GetDigest("REGISTER", uri);
        var expectedResponse = SIPAuthorisation.GetDigest(ha1, nonce, ha2);

        return response == expectedResponse;
    }
}
```

- [ ] **Step 8: Create SipRegistrar**

Create `src/GvResearch.Sip/Registrar/SipRegistrar.cs`:

```csharp
using GvResearch.Sip.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SIPSorcery.SIP;

namespace GvResearch.Sip.Registrar;

public sealed class SipRegistrar
{
    private readonly SIPTransport _transport;
    private readonly RegistrationStore _store;
    private readonly DigestAuthenticator _auth;
    private readonly SipGatewayOptions _options;
    private readonly ILogger<SipRegistrar> _logger;

    public SipRegistrar(
        SIPTransport transport,
        RegistrationStore store,
        DigestAuthenticator auth,
        IOptions<SipGatewayOptions> options,
        ILogger<SipRegistrar> logger)
    {
        _transport = transport;
        _store = store;
        _auth = auth;
        _options = options.Value;
        _logger = logger;

        _transport.SIPTransportRequestReceived += OnRequestReceived;
    }

    private Task OnRequestReceived(SIPEndPoint localSIPEndPoint, SIPEndPoint remoteEndPoint,
        SIPRequest sipRequest)
    {
        return sipRequest.Method switch
        {
            SIPMethodsEnum.REGISTER => HandleRegisterAsync(sipRequest, remoteEndPoint),
            SIPMethodsEnum.INVITE => HandleInviteAsync(sipRequest, remoteEndPoint),
            SIPMethodsEnum.BYE => HandleByeAsync(sipRequest, remoteEndPoint),
            _ => Task.CompletedTask
        };
    }

    private async Task HandleInviteAsync(SIPRequest request, SIPEndPoint remoteEndPoint)
    {
        var destNumber = request.URI.User; // e.g., +15551234567
        _logger.LogInformation("INVITE to {Destination} from {Remote}", destNumber, remoteEndPoint);

        // Send 100 Trying immediately
        var tryingResponse = SIPResponse.GetResponse(request,
            SIPResponseStatusCodesEnum.Trying, null);
        await _transport.SendResponseAsync(tryingResponse);

        // Delegate to call controller
        InviteReceived?.Invoke(this, new SipInviteEventArgs(request, remoteEndPoint, destNumber));
    }

    private async Task HandleByeAsync(SIPRequest request, SIPEndPoint remoteEndPoint)
    {
        _logger.LogInformation("BYE from {Remote}", remoteEndPoint);
        var okResponse = SIPResponse.GetResponse(request, SIPResponseStatusCodesEnum.Ok, null);
        await _transport.SendResponseAsync(okResponse);
        ByeReceived?.Invoke(this, new SipByeEventArgs(request));
    }

    public event EventHandler<SipInviteEventArgs>? InviteReceived;
    public event EventHandler<SipByeEventArgs>? ByeReceived;

    private async Task HandleRegisterAsync(SIPRequest request, SIPEndPoint remoteEndPoint)
    {
        var sipUri = request.Header.From.FromURI.ToParameterlessString();
        _logger.LogInformation("REGISTER from {SipUri} at {Remote}", sipUri, remoteEndPoint);

        // Check for authorization header
        if (request.Header.AuthenticationHeaders.Count == 0)
        {
            // Challenge with 401
            var challengeResponse = SIPResponse.GetResponse(request,
                SIPResponseStatusCodesEnum.Unauthorised, null);
            var nonce = Guid.NewGuid().ToString("N");
            challengeResponse.Header.AuthenticationHeaders.Add(
                new SIPAuthenticationHeader(SIPAuthorisationHeadersEnum.WWWAuthenticate,
                    _options.SipDomain, nonce));
            await _transport.SendResponseAsync(challengeResponse);
            return;
        }

        // Validate credentials
        var authHeader = request.Header.AuthenticationHeaders.First();
        if (!_auth.TryAuthenticate(
            authHeader.SIPDigest.Username,
            authHeader.SIPDigest.Realm,
            authHeader.SIPDigest.Nonce,
            authHeader.SIPDigest.URI,
            authHeader.SIPDigest.Response,
            out _))
        {
            _logger.LogWarning("Auth failed for {SipUri}", sipUri);
            var forbiddenResponse = SIPResponse.GetResponse(request,
                SIPResponseStatusCodesEnum.Forbidden, null);
            await _transport.SendResponseAsync(forbiddenResponse);
            return;
        }

        // Register
        var expiry = request.Header.Expires > 0 ? request.Header.Expires : 3600;
        var contactUri = request.Header.Contact.FirstOrDefault()?.ContactURI?.ToString() ?? sipUri;
        _store.AddOrUpdate(sipUri, new SipRegistration(
            SipUri: sipUri,
            ContactUri: contactUri,
            RemoteEndPoint: remoteEndPoint.ToString(),
            RegisteredAt: DateTimeOffset.UtcNow,
            Expiry: DateTimeOffset.UtcNow.AddSeconds(expiry)
        ));

        _logger.LogInformation("Registered {SipUri}, expires in {Expiry}s", sipUri, expiry);
        var okResponse = SIPResponse.GetResponse(request, SIPResponseStatusCodesEnum.Ok, null);
        okResponse.Header.Expires = expiry;
        await _transport.SendResponseAsync(okResponse);
    }
}

public sealed class SipInviteEventArgs(SIPRequest request, SIPEndPoint remoteEndPoint, string destinationNumber) : EventArgs
{
    public SIPRequest Request { get; } = request;
    public SIPEndPoint RemoteEndPoint { get; } = remoteEndPoint;
    public string DestinationNumber { get; } = destinationNumber;
}

public sealed class SipByeEventArgs(SIPRequest request) : EventArgs
{
    public SIPRequest Request { get; } = request;
}
```

- [ ] **Step 9: Verify it builds**

```bash
dotnet build src/GvResearch.Sip
```

Expected: Build succeeds.

- [ ] **Step 10: Commit**

```bash
git add src/GvResearch.Sip/ tests/GvResearch.Sip.Tests/
git commit -m "feat: add SIP registrar with digest authentication and in-memory registration store"
```

---

### Task 13: GvResearch.Sip — Call Controller and RTP Bridge

**Files:**
- Create: `src/GvResearch.Sip/Media/IGvAudioChannel.cs`
- Create: `src/GvResearch.Sip/Media/RtpBridge.cs`
- Create: `src/GvResearch.Sip/Media/WebRtcGvAudioChannel.cs`
- Create: `src/GvResearch.Sip/Calls/CallSession.cs`
- Create: `src/GvResearch.Sip/Calls/SipCallController.cs`
- Create: `src/GvResearch.Sip/Program.cs`
- Test: `tests/GvResearch.Sip.Tests/Calls/SipCallControllerTests.cs`

- [ ] **Step 1: Create IGvAudioChannel**

Create `src/GvResearch.Sip/Media/IGvAudioChannel.cs`:

```csharp
using SIPSorcery.Net;

namespace GvResearch.Sip.Media;

public interface IGvAudioChannel
{
    Task<RTPSession> EstablishAudioAsync(string gvCallId, CancellationToken ct = default);
    Task HangupAsync(string gvCallId, CancellationToken ct = default);
}
```

- [ ] **Step 2: Create CallSession**

Create `src/GvResearch.Sip/Calls/CallSession.cs`:

```csharp
using SIPSorcery.SIP;
using SIPSorcery.Net;

namespace GvResearch.Sip.Calls;

public sealed class CallSession : IDisposable
{
    public string CallId { get; } = Guid.NewGuid().ToString();
    public string? GvCallId { get; set; }
    public SIPDialogue? SipDialogue { get; set; }
    public RTPSession? SipRtpSession { get; set; }
    public RTPSession? GvRtpSession { get; set; }
    public CallSessionState State { get; set; } = CallSessionState.Initiating;
    public string DestinationNumber { get; }
    public DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;

    public CallSession(string destinationNumber)
    {
        DestinationNumber = destinationNumber;
    }

    public void Dispose()
    {
        SipRtpSession?.Close(null);
        GvRtpSession?.Close(null);
    }
}

public enum CallSessionState
{
    Initiating,
    Ringing,
    Active,
    Ending,
    Ended
}
```

- [ ] **Step 3: Create RtpBridge**

Create `src/GvResearch.Sip/Media/RtpBridge.cs`:

```csharp
using SIPSorcery.Net;
using Microsoft.Extensions.Logging;

namespace GvResearch.Sip.Media;

public sealed class RtpBridge : IDisposable
{
    private readonly ILogger<RtpBridge> _logger;
    private bool _disposed;

    public RtpBridge(ILogger<RtpBridge> logger)
    {
        _logger = logger;
    }

    public void Bridge(RTPSession sipSession, RTPSession gvSession)
    {
        _logger.LogDebug("Bridging RTP sessions");

        // Forward audio from SIP device to GV
        sipSession.OnRtpPacketReceived += (ep, mediaType, rtpPacket) =>
        {
            if (!_disposed && mediaType == SDPMediaTypesEnum.audio)
            {
                gvSession.SendRtpRaw(mediaType, rtpPacket.Payload,
                    rtpPacket.Header.Timestamp, rtpPacket.Header.MarkerBit,
                    rtpPacket.Header.PayloadType);
            }
        };

        // Forward audio from GV to SIP device
        gvSession.OnRtpPacketReceived += (ep, mediaType, rtpPacket) =>
        {
            if (!_disposed && mediaType == SDPMediaTypesEnum.audio)
            {
                sipSession.SendRtpRaw(mediaType, rtpPacket.Payload,
                    rtpPacket.Header.Timestamp, rtpPacket.Header.MarkerBit,
                    rtpPacket.Header.PayloadType);
            }
        };
    }

    public void Dispose()
    {
        _disposed = true;
    }
}
```

- [ ] **Step 4: Write SipCallController tests (TDD)**

Create `tests/GvResearch.Sip.Tests/Calls/SipCallControllerTests.cs`:

```csharp
using FluentAssertions;
using GvResearch.Shared.Models;
using GvResearch.Shared.Services;
using GvResearch.Sip.Calls;
using GvResearch.Sip.Configuration;
using GvResearch.Sip.Media;
using GvResearch.Sip.Registrar;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace GvResearch.Sip.Tests.Calls;

public class SipCallControllerTests
{
    [Fact]
    public async Task HandleOutboundCall_InitiatesGvCall()
    {
        var callService = Substitute.For<IGvCallService>();
        callService.InitiateCallAsync("+15551234567", Arg.Any<CancellationToken>())
            .Returns(new GvCallResult("gv-call-1", true, null));

        var audioChannel = Substitute.For<IGvAudioChannel>();
        var controller = new SipCallController(
            callService, audioChannel,
            new RegistrationStore(),
            NullLogger<SipCallController>.Instance);

        var session = await controller.CreateOutboundCallAsync("+15551234567");

        session.Should().NotBeNull();
        session.GvCallId.Should().Be("gv-call-1");
        session.State.Should().Be(CallSessionState.Initiating);
        await callService.Received(1).InitiateCallAsync("+15551234567", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleOutboundCall_GvFailure_ReturnsNull()
    {
        var callService = Substitute.For<IGvCallService>();
        callService.InitiateCallAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GvCallResult("", false, "GV error"));

        var audioChannel = Substitute.For<IGvAudioChannel>();
        var controller = new SipCallController(
            callService, audioChannel,
            new RegistrationStore(),
            NullLogger<SipCallController>.Instance);

        var session = await controller.CreateOutboundCallAsync("+15559999999");
        session.Should().BeNull();
    }
}
```

- [ ] **Step 5: Run test to verify it fails**

```bash
dotnet test tests/GvResearch.Sip.Tests --filter "SipCallControllerTests" -v n
```

Expected: FAIL — `SipCallController.CreateOutboundCallAsync` does not exist.

- [ ] **Step 6: Implement SipCallController**

Create `src/GvResearch.Sip/Calls/SipCallController.cs`:

```csharp
using System.Collections.Concurrent;
using GvResearch.Shared.Services;
using GvResearch.Sip.Media;
using GvResearch.Sip.Registrar;
using Microsoft.Extensions.Logging;
using SIPSorcery.SIP;

namespace GvResearch.Sip.Calls;

public sealed class SipCallController
{
    private readonly IGvCallService _callService;
    private readonly IGvAudioChannel _audioChannel;
    private readonly RegistrationStore _registrationStore;
    private readonly ILogger<SipCallController> _logger;
    private readonly ConcurrentDictionary<string, CallSession> _activeCalls = new();

    public SipCallController(
        IGvCallService callService,
        IGvAudioChannel audioChannel,
        RegistrationStore registrationStore,
        ILogger<SipCallController> logger)
    {
        _callService = callService;
        _audioChannel = audioChannel;
        _registrationStore = registrationStore;
        _logger = logger;
    }

    public async Task<CallSession?> CreateOutboundCallAsync(string destinationNumber,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Initiating outbound call to {Destination}", destinationNumber);

        var result = await _callService.InitiateCallAsync(destinationNumber, ct);
        if (!result.Success)
        {
            _logger.LogWarning("GV call initiation failed: {Error}", result.ErrorMessage);
            return null;
        }

        var session = new CallSession(destinationNumber)
        {
            GvCallId = result.GvCallId
        };
        _activeCalls[session.CallId] = session;

        _logger.LogInformation("Call {CallId} initiated, GV call ID: {GvCallId}",
            session.CallId, session.GvCallId);
        return session;
    }

    public async Task HangupAsync(string callId, CancellationToken ct = default)
    {
        if (!_activeCalls.TryRemove(callId, out var session)) return;

        _logger.LogInformation("Hanging up call {CallId}", callId);
        session.State = CallSessionState.Ending;

        if (session.GvCallId is not null)
        {
            await _callService.HangupAsync(session.GvCallId, ct);
            await _audioChannel.HangupAsync(session.GvCallId, ct);
        }

        session.State = CallSessionState.Ended;
        session.Dispose();
    }

    public CallSession? GetActiveCall(string callId)
    {
        _activeCalls.TryGetValue(callId, out var session);
        return session;
    }

    public IReadOnlyList<CallSession> GetAllActiveCalls() =>
        _activeCalls.Values.ToList();
}
```

- [ ] **Step 7: Run tests**

```bash
dotnet test tests/GvResearch.Sip.Tests -v n
```

Expected: All tests PASS.

- [ ] **Step 8: Create placeholder WebRtcGvAudioChannel**

Create `src/GvResearch.Sip/Media/WebRtcGvAudioChannel.cs`:

```csharp
using Microsoft.Extensions.Logging;
using SIPSorcery.Net;

namespace GvResearch.Sip.Media;

/// <summary>
/// WebRTC-based GV audio channel implementation.
/// TODO: Complete after Phase 1 discovery confirms WebRTC audio transport.
/// </summary>
public sealed class WebRtcGvAudioChannel : IGvAudioChannel
{
    private readonly ILogger<WebRtcGvAudioChannel> _logger;

    public WebRtcGvAudioChannel(ILogger<WebRtcGvAudioChannel> logger)
    {
        _logger = logger;
    }

    public Task<RTPSession> EstablishAudioAsync(string gvCallId, CancellationToken ct = default)
    {
        _logger.LogWarning("WebRTC audio channel not yet implemented — awaiting Phase 1 discovery");
        // TODO: Implement WebRTC peer connection to GV
        // 1. Create RTCPeerConnection
        // 2. Set remote SDP from GV's offer
        // 3. Create local SDP answer
        // 4. Return the RTPSession for bridging
        throw new NotImplementedException(
            "WebRTC audio channel implementation pending Phase 1 API discovery");
    }

    public Task HangupAsync(string gvCallId, CancellationToken ct = default)
    {
        // TODO: Close WebRTC peer connection
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 9: Create appsettings.json for SIP gateway**

Create `src/GvResearch.Sip/appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "GvResearch.Sip.Media": "Debug"
      }
    }
  },
  "SipGateway": {
    "SipPort": 5060,
    "SipDomain": "gvresearch.local",
    "Accounts": [
      {
        "Username": "softphone",
        "Password": "changeme",
        "DisplayName": "GV Softphone"
      }
    ]
  },
  "GvResearch": {
    "TokenPath": "",
    "KeyPath": ""
  }
}
```

- [ ] **Step 10: Create SIP gateway Program.cs entry point**

Create `src/GvResearch.Sip/Program.cs`:

```csharp
using GvResearch.Shared.Auth;
using GvResearch.Shared.RateLimiting;
using GvResearch.Shared.Services;
using GvResearch.Sip.Calls;
using GvResearch.Sip.Configuration;
using GvResearch.Sip.Media;
using GvResearch.Sip.Registrar;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;
using SIPSorcery.SIP;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog(config => config
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.Console()
    .WriteTo.File("logs/gvresearch-sip-.log", rollingInterval: RollingInterval.Day));

// Configuration
builder.Services.Configure<SipGatewayOptions>(
    builder.Configuration.GetSection(SipGatewayOptions.SectionName));

// SIP transport
builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<IOptions<SipGatewayOptions>>().Value;
    var transport = new SIPTransport();
    transport.AddSIPChannel(new SIPUDPChannel(
        new System.Net.IPEndPoint(System.Net.IPAddress.Any, options.SipPort)));
    return transport;
});

// Registrar
builder.Services.AddSingleton<RegistrationStore>();
builder.Services.AddSingleton<DigestAuthenticator>();
builder.Services.AddSingleton<SipRegistrar>();

// Call controller
builder.Services.AddSingleton<SipCallController>();
builder.Services.AddSingleton<IGvAudioChannel, WebRtcGvAudioChannel>();
builder.Services.AddSingleton<RtpBridge>();

// GV services
builder.Services.AddSingleton<GvRateLimiter>();
builder.Services.AddSingleton<IGvTokenService>(sp =>
{
    var config = sp.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
    var tokenPath = config["GvResearch:TokenPath"] ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".gvresearch", "tokens.enc");
    var keyPath = config["GvResearch:KeyPath"] ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".gvresearch", "key.bin");
    return new EncryptedFileTokenService(tokenPath, keyPath);
});

builder.Services.AddHttpClient<IGvCallService, GvCallService>((sp, client) =>
{
    client.BaseAddress = new Uri("https://voice.google.com");
})
.AddHttpMessageHandler(sp =>
    new GvResearch.Shared.Http.GvHttpClientHandler(
        sp.GetRequiredService<IGvTokenService>()))
.AddStandardResilienceHandler(); // REQ-SIP-008: Polly retry + circuit breaker

var host = builder.Build();

// Initialize registrar and wire INVITE to call controller
var registrar = host.Services.GetRequiredService<SipRegistrar>();
var callController = host.Services.GetRequiredService<SipCallController>();

registrar.InviteReceived += async (_, e) =>
{
    var session = await callController.CreateOutboundCallAsync(e.DestinationNumber);
    if (session is null)
    {
        // Send 502 Bad Gateway if GV call failed
        var errorResponse = SIPResponse.GetResponse(e.Request,
            SIPResponseStatusCodesEnum.BadGateway, "GV call initiation failed");
        var transport = host.Services.GetRequiredService<SIPTransport>();
        await transport.SendResponseAsync(errorResponse);
    }
};

registrar.ByeReceived += async (_, e) =>
{
    // TODO: Look up call session by SIP dialog and hang up
};

var logger = host.Services.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Program>>();
var options = host.Services.GetRequiredService<IOptions<SipGatewayOptions>>().Value;
logger.LogInformation("SIP Gateway started on port {Port}, domain {Domain}",
    options.SipPort, options.SipDomain);

await host.RunAsync();
```

- [ ] **Step 10: Verify it builds**

```bash
dotnet build src/GvResearch.Sip
```

Expected: Build succeeds.

- [ ] **Step 11: Commit**

```bash
git add src/GvResearch.Sip/ tests/GvResearch.Sip.Tests/
git commit -m "feat: add SIP call controller, RTP bridge, and gateway entry point"
```

---

### Task 14: GvResearch.Softphone — Avalonia UI with SIP Client

**Files:**
- Create: `src/GvResearch.Softphone/GvResearch.Softphone.csproj`
- Create: `src/GvResearch.Softphone/Program.cs`
- Create: `src/GvResearch.Softphone/App.axaml`
- Create: `src/GvResearch.Softphone/App.axaml.cs`
- Create: `src/GvResearch.Softphone/Configuration/SoftphoneSettings.cs`
- Create: `src/GvResearch.Softphone/ViewModels/MainWindowViewModel.cs`
- Create: `src/GvResearch.Softphone/ViewModels/DialerViewModel.cs`
- Create: `src/GvResearch.Softphone/ViewModels/ActiveCallViewModel.cs`
- Create: `src/GvResearch.Softphone/Views/MainWindow.axaml`
- Create: `src/GvResearch.Softphone/Views/DialerView.axaml`
- Create: `src/GvResearch.Softphone/Views/ActiveCallView.axaml`
- Create: `src/GvResearch.Softphone/Sip/SoftphoneSipClient.cs`
- Create: `src/GvResearch.Softphone/Audio/AudioEngine.cs`
- Test: `tests/GvResearch.Softphone.Tests/GvResearch.Softphone.Tests.csproj`
- Test: `tests/GvResearch.Softphone.Tests/ViewModels/DialerViewModelTests.cs`

- [ ] **Step 1: Create softphone project**

```bash
dotnet new avalonia.app -n GvResearch.Softphone -o src/GvResearch.Softphone --framework net8.0
dotnet sln add src/GvResearch.Softphone/GvResearch.Softphone.csproj
dotnet add src/GvResearch.Softphone package SIPSorcery
dotnet add src/GvResearch.Softphone package NAudio
dotnet add src/GvResearch.Softphone package CommunityToolkit.Mvvm
```

Note: If `dotnet new avalonia.app` is not available, install the templates first:
```bash
dotnet new install Avalonia.Templates
```

- [ ] **Step 2: Create test project**

```bash
dotnet new xunit -n GvResearch.Softphone.Tests -o tests/GvResearch.Softphone.Tests
rm tests/GvResearch.Softphone.Tests/UnitTest1.cs
dotnet sln add tests/GvResearch.Softphone.Tests/GvResearch.Softphone.Tests.csproj
dotnet add tests/GvResearch.Softphone.Tests reference src/GvResearch.Softphone
dotnet add tests/GvResearch.Softphone.Tests package FluentAssertions
dotnet add tests/GvResearch.Softphone.Tests package NSubstitute
dotnet add tests/GvResearch.Softphone.Tests package CommunityToolkit.Mvvm
```

- [ ] **Step 3: Create SoftphoneSettings**

Create `src/GvResearch.Softphone/Configuration/SoftphoneSettings.cs`:

```csharp
namespace GvResearch.Softphone.Configuration;

public sealed class SoftphoneSettings
{
    public string SipServer { get; set; } = "127.0.0.1";
    public int SipPort { get; set; } = 5060;
    public string SipUsername { get; set; } = "";
    public string SipPassword { get; set; } = "";
    public string SipDomain { get; set; } = "gvresearch.local";
    public string DisplayName { get; set; } = "GV Softphone";
}
```

- [ ] **Step 4: Create SoftphoneSipClient**

Create `src/GvResearch.Softphone/Sip/SoftphoneSipClient.cs`:

```csharp
using SIPSorcery.SIP;
using SIPSorcery.SIP.App;
using SIPSorcery.Net;
using SIPSorcery.Media;
using GvResearch.Softphone.Configuration;

namespace GvResearch.Softphone.Sip;

public sealed class SoftphoneSipClient : IDisposable
{
    private readonly SIPTransport _transport;
    private readonly SoftphoneSettings _settings;
    private SIPUserAgent? _userAgent;
    private RTPSession? _rtpSession;

    public event Action? CallAnswered;
    public event Action? CallEnded;
    public event Action<string>? StatusChanged;

    public bool IsRegistered { get; private set; }
    public bool IsInCall { get; private set; }

    public SoftphoneSipClient(SoftphoneSettings settings)
    {
        _settings = settings;
        _transport = new SIPTransport();
    }

    public async Task RegisterAsync(CancellationToken ct = default)
    {
        var regUserAgent = new SIPRegistrationUserAgent(
            _transport,
            _settings.SipUsername,
            _settings.SipPassword,
            $"sip:{_settings.SipServer}:{_settings.SipPort}",
            120);

        regUserAgent.RegistrationSuccessful += (uri) =>
        {
            IsRegistered = true;
            StatusChanged?.Invoke("Registered");
        };

        regUserAgent.RegistrationFailed += (uri, resp, msg) =>
        {
            IsRegistered = false;
            StatusChanged?.Invoke($"Registration failed: {msg}");
        };

        regUserAgent.Start();
    }

    public async Task<bool> CallAsync(string destinationNumber, CancellationToken ct = default)
    {
        _userAgent = new SIPUserAgent(_transport, null);
        _userAgent.OnCallHungup += (dialogue) =>
        {
            IsInCall = false;
            CallEnded?.Invoke();
        };

        _rtpSession = new RTPSession(false, false, false);
        var audioFormat = new SDPAudioVideoMediaFormat(
            SDPWellKnownMediaFormatsEnum.PCMU);
        _rtpSession.AddTrack(new MediaStreamTrack(audioFormat));

        var destUri = SIPURI.ParseSIPURI(
            $"sip:{destinationNumber}@{_settings.SipServer}:{_settings.SipPort}");
        var result = await _userAgent.Call(destUri.ToString(), null, null, _rtpSession);

        if (result)
        {
            IsInCall = true;
            CallAnswered?.Invoke();
        }
        return result;
    }

    public void Hangup()
    {
        if (_userAgent?.IsCallActive == true)
        {
            _userAgent.Hangup();
        }
        IsInCall = false;
        _rtpSession?.Close(null);
    }

    public RTPSession? GetRtpSession() => _rtpSession;

    public void Dispose()
    {
        Hangup();
        _transport.Shutdown();
    }
}
```

- [ ] **Step 5: Write DialerViewModel tests (TDD)**

Create `tests/GvResearch.Softphone.Tests/ViewModels/DialerViewModelTests.cs`:

```csharp
using FluentAssertions;
using GvResearch.Softphone.Sip;
using GvResearch.Softphone.ViewModels;
using NSubstitute;

namespace GvResearch.Softphone.Tests.ViewModels;

public class DialerViewModelTests
{
    [Fact]
    public void DialDigit_AppendsToNumber()
    {
        var vm = new DialerViewModel();
        vm.DialDigitCommand.Execute("5");
        vm.DialDigitCommand.Execute("5");
        vm.DialDigitCommand.Execute("5");
        vm.DialNumber.Should().Be("555");
    }

    [Fact]
    public void ClearCommand_ClearsNumber()
    {
        var vm = new DialerViewModel();
        vm.DialDigitCommand.Execute("1");
        vm.DialDigitCommand.Execute("2");
        vm.ClearCommand.Execute(null);
        vm.DialNumber.Should().BeEmpty();
    }

    [Fact]
    public void BackspaceCommand_RemovesLastDigit()
    {
        var vm = new DialerViewModel();
        vm.DialDigitCommand.Execute("1");
        vm.DialDigitCommand.Execute("2");
        vm.DialDigitCommand.Execute("3");
        vm.BackspaceCommand.Execute(null);
        vm.DialNumber.Should().Be("12");
    }

    [Fact]
    public void CanCall_EmptyNumber_IsFalse()
    {
        var vm = new DialerViewModel();
        vm.CallCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void CanCall_WithNumber_IsTrue()
    {
        var vm = new DialerViewModel();
        vm.DialDigitCommand.Execute("5");
        vm.CallCommand.CanExecute(null).Should().BeTrue();
    }
}
```

- [ ] **Step 6: Run test to verify it fails**

```bash
dotnet test tests/GvResearch.Softphone.Tests --filter "DialerViewModelTests" -v n
```

Expected: FAIL — DialerViewModel does not exist / missing commands.

- [ ] **Step 7: Implement DialerViewModel**

Create `src/GvResearch.Softphone/ViewModels/DialerViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace GvResearch.Softphone.ViewModels;

public partial class DialerViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CallCommand))]
    private string _dialNumber = "";

    public event Action<string>? CallRequested;

    [RelayCommand]
    private void DialDigit(string digit)
    {
        DialNumber += digit;
    }

    [RelayCommand]
    private void Clear()
    {
        DialNumber = "";
    }

    [RelayCommand]
    private void Backspace()
    {
        if (DialNumber.Length > 0)
            DialNumber = DialNumber[..^1];
    }

    [RelayCommand(CanExecute = nameof(CanCall))]
    private void Call()
    {
        CallRequested?.Invoke(DialNumber);
    }

    private bool CanCall() => !string.IsNullOrWhiteSpace(DialNumber);
}
```

- [ ] **Step 8: Run tests**

```bash
dotnet test tests/GvResearch.Softphone.Tests --filter "DialerViewModelTests" -v n
```

Expected: All 5 tests PASS.

- [ ] **Step 9: Write ActiveCallViewModel tests (TDD)**

Create `tests/GvResearch.Softphone.Tests/ViewModels/ActiveCallViewModelTests.cs`:

```csharp
using FluentAssertions;
using GvResearch.Softphone.ViewModels;

namespace GvResearch.Softphone.Tests.ViewModels;

public class ActiveCallViewModelTests : IDisposable
{
    private readonly ActiveCallViewModel _vm = new();

    [Fact]
    public void StartCall_SetsRemoteNumberAndStatus()
    {
        _vm.StartCall("+15551234567");
        _vm.RemoteNumber.Should().Be("+15551234567");
        _vm.CallStatus.Should().Be("Ringing...");
    }

    [Fact]
    public void CallConnected_UpdatesStatus()
    {
        _vm.StartCall("+15551234567");
        _vm.CallConnected();
        _vm.CallStatus.Should().Be("Connected");
    }

    [Fact]
    public void ToggleMute_FlipsMuteState()
    {
        _vm.IsMuted.Should().BeFalse();
        _vm.ToggleMuteCommand.Execute(null);
        _vm.IsMuted.Should().BeTrue();
        _vm.MuteButtonText.Should().Be("Unmute");
        _vm.ToggleMuteCommand.Execute(null);
        _vm.IsMuted.Should().BeFalse();
        _vm.MuteButtonText.Should().Be("Mute");
    }

    [Fact]
    public void Hangup_SetsStatusToEnded()
    {
        _vm.StartCall("+15551234567");
        _vm.HangupCommand.Execute(null);
        _vm.CallStatus.Should().Be("Ended");
    }

    [Fact]
    public void HangupRequested_EventFires()
    {
        var fired = false;
        _vm.HangupRequested += () => fired = true;
        _vm.HangupCommand.Execute(null);
        fired.Should().BeTrue();
    }

    public void Dispose() => _vm.Dispose();
}
```

- [ ] **Step 10: Run ActiveCallViewModel tests to verify they fail**

```bash
dotnet test tests/GvResearch.Softphone.Tests --filter "ActiveCallViewModelTests" -v n
```

Expected: FAIL — `ActiveCallViewModel` does not exist yet.

- [ ] **Step 11: Create ActiveCallViewModel**

Create `src/GvResearch.Softphone/ViewModels/ActiveCallViewModel.cs`:

```csharp
using System.Timers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace GvResearch.Softphone.ViewModels;

public partial class ActiveCallViewModel : ObservableObject, IDisposable
{
    [ObservableProperty] private string _remoteNumber = "";
    [ObservableProperty] private string _callDuration = "00:00";
    [ObservableProperty] private string _callStatus = "Connecting...";
    [ObservableProperty] private bool _isMuted;

    private readonly System.Timers.Timer _timer;
    private DateTimeOffset _callStarted;

    public event Action? HangupRequested;

    public ActiveCallViewModel()
    {
        _timer = new System.Timers.Timer(1000);
        _timer.Elapsed += OnTimerTick;
    }

    public void StartCall(string number)
    {
        RemoteNumber = number;
        CallStatus = "Ringing...";
        _callStarted = DateTimeOffset.UtcNow;
        _timer.Start();
    }

    public void CallConnected()
    {
        CallStatus = "Connected";
    }

    [RelayCommand]
    private void Hangup()
    {
        _timer.Stop();
        CallStatus = "Ended";
        HangupRequested?.Invoke();
    }

    public string MuteButtonText => IsMuted ? "Unmute" : "Mute";

    [RelayCommand]
    private void ToggleMute()
    {
        IsMuted = !IsMuted;
        OnPropertyChanged(nameof(MuteButtonText));
    }

    private void OnTimerTick(object? sender, ElapsedEventArgs e)
    {
        var elapsed = DateTimeOffset.UtcNow - _callStarted;
        CallDuration = elapsed.ToString(@"mm\:ss");
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Dispose();
    }
}
```

- [ ] **Step 10: Create MainWindowViewModel**

Create `src/GvResearch.Softphone/ViewModels/MainWindowViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;

namespace GvResearch.Softphone.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty] private bool _isInCall;
    [ObservableProperty] private string _statusText = "Not registered";

    public DialerViewModel Dialer { get; } = new();
    public ActiveCallViewModel ActiveCall { get; } = new();
}
```

- [ ] **Step 11: Create Avalonia views**

Create `src/GvResearch.Softphone/Views/DialerView.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:GvResearch.Softphone.ViewModels"
             x:Class="GvResearch.Softphone.Views.DialerView"
             x:DataType="vm:DialerViewModel">

    <StackPanel Spacing="10" Margin="20">
        <TextBox Text="{Binding DialNumber}" FontSize="24" IsReadOnly="True"
                 HorizontalContentAlignment="Center" />

        <UniformGrid Columns="3" Rows="4">
            <Button Content="1" Command="{Binding DialDigitCommand}" CommandParameter="1" />
            <Button Content="2" Command="{Binding DialDigitCommand}" CommandParameter="2" />
            <Button Content="3" Command="{Binding DialDigitCommand}" CommandParameter="3" />
            <Button Content="4" Command="{Binding DialDigitCommand}" CommandParameter="4" />
            <Button Content="5" Command="{Binding DialDigitCommand}" CommandParameter="5" />
            <Button Content="6" Command="{Binding DialDigitCommand}" CommandParameter="6" />
            <Button Content="7" Command="{Binding DialDigitCommand}" CommandParameter="7" />
            <Button Content="8" Command="{Binding DialDigitCommand}" CommandParameter="8" />
            <Button Content="9" Command="{Binding DialDigitCommand}" CommandParameter="9" />
            <Button Content="*" Command="{Binding DialDigitCommand}" CommandParameter="*" />
            <Button Content="0" Command="{Binding DialDigitCommand}" CommandParameter="0" />
            <Button Content="#" Command="{Binding DialDigitCommand}" CommandParameter="#" />
        </UniformGrid>

        <Grid ColumnDefinitions="*,*,*">
            <Button Content="Clear" Command="{Binding ClearCommand}" Grid.Column="0" />
            <Button Content="Call" Command="{Binding CallCommand}" Grid.Column="1"
                    Background="Green" Foreground="White" FontWeight="Bold" />
            <Button Content="Del" Command="{Binding BackspaceCommand}" Grid.Column="2" />
        </Grid>
    </StackPanel>
</UserControl>
```

Create `src/GvResearch.Softphone/Views/DialerView.axaml.cs`:

```csharp
using Avalonia.Controls;

namespace GvResearch.Softphone.Views;

public partial class DialerView : UserControl
{
    public DialerView()
    {
        InitializeComponent();
    }
}
```

Create `src/GvResearch.Softphone/Views/ActiveCallView.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:GvResearch.Softphone.ViewModels"
             x:Class="GvResearch.Softphone.Views.ActiveCallView"
             x:DataType="vm:ActiveCallViewModel">

    <StackPanel Spacing="15" Margin="20" HorizontalAlignment="Center">
        <TextBlock Text="{Binding RemoteNumber}" FontSize="28"
                   HorizontalAlignment="Center" />
        <TextBlock Text="{Binding CallStatus}" FontSize="16"
                   HorizontalAlignment="Center" Opacity="0.7" />
        <TextBlock Text="{Binding CallDuration}" FontSize="36"
                   HorizontalAlignment="Center" FontFamily="Consolas" />

        <StackPanel Orientation="Horizontal" Spacing="20" HorizontalAlignment="Center">
            <Button Content="{Binding MuteButtonText}"
                    Command="{Binding ToggleMuteCommand}" Width="100" />
            <Button Content="Hangup" Command="{Binding HangupCommand}"
                    Background="Red" Foreground="White" FontWeight="Bold" Width="100" />
        </StackPanel>
    </StackPanel>
</UserControl>
```

Create `src/GvResearch.Softphone/Views/ActiveCallView.axaml.cs`:

```csharp
using Avalonia.Controls;

namespace GvResearch.Softphone.Views;

public partial class ActiveCallView : UserControl
{
    public ActiveCallView()
    {
        InitializeComponent();
    }
}
```

Create `src/GvResearch.Softphone/Views/MainWindow.axaml`:

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="using:GvResearch.Softphone.ViewModels"
        xmlns:views="using:GvResearch.Softphone.Views"
        x:Class="GvResearch.Softphone.Views.MainWindow"
        x:DataType="vm:MainWindowViewModel"
        Title="GV Softphone" Width="350" Height="550"
        CanResize="False">

    <DockPanel>
        <TextBlock DockPanel.Dock="Bottom" Text="{Binding StatusText}"
                   Margin="10,5" FontSize="11" Opacity="0.6" />

        <Panel>
            <views:DialerView DataContext="{Binding Dialer}"
                              IsVisible="{Binding !$parent[Window].((vm:MainWindowViewModel)DataContext).IsInCall}" />
            <views:ActiveCallView DataContext="{Binding ActiveCall}"
                                  IsVisible="{Binding $parent[Window].((vm:MainWindowViewModel)DataContext).IsInCall}" />
        </Panel>
    </DockPanel>
</Window>
```

Create `src/GvResearch.Softphone/Views/MainWindow.axaml.cs`:

```csharp
using Avalonia.Controls;

namespace GvResearch.Softphone.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
}
```

- [ ] **Step 12: Create AudioEngine placeholder**

Create `src/GvResearch.Softphone/Audio/AudioEngine.cs`:

```csharp
using NAudio.Wave;
using SIPSorcery.Net;

namespace GvResearch.Softphone.Audio;

public sealed class AudioEngine : IDisposable
{
    private WaveInEvent? _waveIn;
    private WaveOutEvent? _waveOut;
    private BufferedWaveProvider? _waveProvider;

    public void Start(RTPSession rtpSession)
    {
        // Microphone capture -> RTP
        _waveIn = new WaveInEvent
        {
            WaveFormat = new WaveFormat(8000, 16, 1) // 8kHz mono for G.711
        };
        _waveIn.DataAvailable += (s, e) =>
        {
            // TODO: Encode to PCMU and send via RTP
            // This requires PCM-to-PCMU encoding
        };

        // RTP -> Speaker playback
        _waveProvider = new BufferedWaveProvider(new WaveFormat(8000, 16, 1))
        {
            DiscardOnBufferOverflow = true
        };
        _waveOut = new WaveOutEvent();
        _waveOut.Init(_waveProvider);

        rtpSession.OnRtpPacketReceived += (ep, mediaType, rtpPacket) =>
        {
            if (mediaType == SDPMediaTypesEnum.audio)
            {
                // TODO: Decode PCMU to PCM and write to buffer
                // _waveProvider.AddSamples(decoded, 0, decoded.Length);
            }
        };

        _waveIn.StartRecording();
        _waveOut.Play();
    }

    public void Stop()
    {
        _waveIn?.StopRecording();
        _waveOut?.Stop();
    }

    public void SetMute(bool muted)
    {
        // TODO: When muted, stop sending RTP but keep receiving
    }

    public void Dispose()
    {
        Stop();
        _waveIn?.Dispose();
        _waveOut?.Dispose();
    }
}
```

- [ ] **Step 13: Verify it builds**

```bash
dotnet build src/GvResearch.Softphone
dotnet test tests/GvResearch.Softphone.Tests -v n
```

Expected: Build succeeds, DialerViewModel tests pass.

- [ ] **Step 14: Commit**

```bash
git add src/GvResearch.Softphone/ tests/GvResearch.Softphone.Tests/
git commit -m "feat: add Avalonia softphone with dialer, active call views, SIP client, and audio engine"
```

---

### Task 15: Integration Verification — Full Solution Build and Test

- [ ] **Step 1: Build entire solution**

```bash
dotnet build GvResearch.sln
```

Expected: All projects build cleanly with zero warnings (TreatWarningsAsErrors is on).

- [ ] **Step 2: Run all tests**

```bash
dotnet test GvResearch.sln -v n
```

Expected: All tests pass. Should have tests in:
- Iaet.Core.Tests (EndpointSignature tests)
- Iaet.Catalog.Tests (normalizer + SQLite catalog tests)
- GvResearch.Shared.Tests (token encryption + service + rate limiter tests)
- GvResearch.Api.Tests (call endpoint integration tests)
- GvResearch.Sip.Tests (registration store + call controller tests)
- GvResearch.Softphone.Tests (dialer VM tests)

- [ ] **Step 3: Run build script**

```bash
pwsh scripts/build.ps1 -Target test
```

Expected: Clean build and all tests pass via the build script.

- [ ] **Step 4: Commit any fixes**

If any build or test issues, fix them and commit.

```bash
git add -A
git commit -m "fix: resolve integration issues across full solution"
```

- [ ] **Step 5: Tag the vertical slice foundation**

```bash
git tag v0.1.0-slice-foundation -m "Phase 0-2 scaffolding complete. All projects build, core tests pass. Audio bridge and GV API endpoints are stubs pending Phase 1 discovery."
```

---

## What's Next

After this plan is complete, the codebase has:
- Full solution structure with all 12 projects building
- IAET capture pipeline working end-to-end (browser → SQLite)
- GvResearch.Shared with token management and rate-limited GV call service (stubs for actual GV endpoints)
- REST facade with call endpoints
- SIP gateway with registration and call control (audio bridge stubbed)
- Avalonia softphone with dialer UI and SIP client

**Remaining work to achieve the vertical slice success criteria (spec Section 7.2):**
1. Run Phase 1 GV capture sessions (Task 7) to discover actual endpoint URLs and audio transport
2. Update GV endpoint patterns in the adapter and call service based on findings
3. Implement `WebRtcGvAudioChannel` based on discovered audio transport
4. Complete the `AudioEngine` PCM/PCMU encoding
5. End-to-end test: softphone → SIP gateway → GV → audio bridge → call works

These items depend on Phase 1 discovery and will be planned in a follow-up plan.
