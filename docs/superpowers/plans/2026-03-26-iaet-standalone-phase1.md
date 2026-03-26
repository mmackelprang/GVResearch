# IAET Standalone — Phase 1: Extract + Retarget Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extract IAET assemblies from GVResearch into a standalone GitHub repo (`mmackelprang/IAET`), retarget to .NET 10, apply migration improvements, add missing tests, set up CI/NuGet packaging, and write comprehensive documentation.

**Architecture:** Port 4 existing assemblies (Core, Capture, Catalog, Cli) into a new repo. Retarget from net8.0 to net10.0. Move result types to Models namespace. Add DI service registration. Switch from EnsureCreated() to EF Core Migrations. Create stub projects for future phases. Set up GitHub Actions CI and NuGet packaging.

**Tech Stack:** .NET 10 (LTS), C# 13+, EF Core 10, Playwright .NET, System.CommandLine, Serilog, xUnit + FluentAssertions + NSubstitute, GitHub Actions

**Spec:** `docs/superpowers/specs/2026-03-26-iaet-standalone-design.md`
**Source code to port:** `D:/prj/GVResearch/src/Iaet.*` and `D:/prj/GVResearch/tests/Iaet.*`

---

## Phase 1 Scope

This plan covers only Phase 1 from the spec (Section 17). By the end, the IAET repo will have:
- Working `iaet capture start` + `iaet catalog sessions/endpoints` on .NET 10
- All existing tests passing + new Capture tests
- DI-based composition in the CLI
- EF Core Migrations for schema evolution
- GitHub Actions CI (build + test on push)
- NuGet package generation (local + GitHub Packages)
- Comprehensive README + per-assembly documentation
- Stub projects for all future assemblies (Schema, Replay, Crawler, Export, Explorer)

Phases 2-9 will have separate plans.

---

## Task 1: Create GitHub Repo and Initial Scaffolding

**Files:**
- Create: `D:/prj/IAET/.gitignore`
- Create: `D:/prj/IAET/global.json`
- Create: `D:/prj/IAET/Directory.Build.props`
- Create: `D:/prj/IAET/.editorconfig`
- Create: `D:/prj/IAET/Iaet.sln`
- Create: `D:/prj/IAET/LICENSE`
- Create: `D:/prj/IAET/nuget.config`

- [ ] **Step 1: Create GitHub repo**

```bash
gh repo create mmackelprang/IAET --public --description "Internal API Extraction Toolkit — discover, capture, analyze, and document undocumented browser-based APIs" --clone --license MIT
cd D:/prj/IAET
```

- [ ] **Step 2: Create .gitignore**

Same as GVResearch .gitignore plus Node.js entries for browser extensions:

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

## Coverage
TestResults/
coverage/
*.cobertura.xml

## SQLite databases
*.db
*.db-journal
*.db-wal

## Node.js (browser extensions)
node_modules/
dist/

## NuGet
*.nupkg

## OS
Thumbs.db
.DS_Store
```

- [ ] **Step 3: Create global.json**

```json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestMinor",
    "allowPrerelease": false
  }
}
```

- [ ] **Step 4: Create Directory.Build.props**

Extends from GVResearch with .NET 10 + NuGet packaging properties:

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <AnalysisMode>All</AnalysisMode>
    <LangVersion>13</LangVersion>
    <Version>0.1.0</Version>
  </PropertyGroup>

  <!-- NuGet packaging defaults -->
  <PropertyGroup>
    <Authors>IAET Contributors</Authors>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageProjectUrl>https://github.com/mmackelprang/IAET</PackageProjectUrl>
    <RepositoryUrl>https://github.com/mmackelprang/IAET</RepositoryUrl>
    <RepositoryType>git</RepositoryType>
    <PackageReadmeFile>README.md</PackageReadmeFile>
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
csharp_style_namespace_declarations = file_scoped:warning
```

- [ ] **Step 6: Create nuget.config for GitHub Packages**

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <add key="github" value="https://nuget.pkg.github.com/mmackelprang/index.json" />
  </packageSources>
</configuration>
```

- [ ] **Step 7: Create empty solution and directory structure**

```bash
dotnet new sln -n Iaet -o D:/prj/IAET
mkdir -p src/{Iaet.Core,Iaet.Capture,Iaet.Catalog,Iaet.Schema,Iaet.Replay,Iaet.Crawler,Iaet.Export,Iaet.Explorer,Iaet.Cli}
mkdir -p tests/{Iaet.Core.Tests,Iaet.Capture.Tests,Iaet.Catalog.Tests,Iaet.Schema.Tests,Iaet.Replay.Tests,Iaet.Crawler.Tests,Iaet.Export.Tests,Iaet.Explorer.Tests}
mkdir -p extensions/{iaet-devtools,iaet-capture}
mkdir -p docs/{tutorials,recipes}
mkdir -p scripts captures
```

- [ ] **Step 8: Commit**

```bash
git add .gitignore global.json Directory.Build.props .editorconfig nuget.config Iaet.sln LICENSE
git commit -m "chore: initialize IAET repo with .NET 10 scaffolding and NuGet config"
```

---

## Task 2: Port Iaet.Core — Contracts and Models

**Source:** `D:/prj/GVResearch/src/Iaet.Core/`
**Destination:** `D:/prj/IAET/src/Iaet.Core/`

- [ ] **Step 1: Create Iaet.Core project**

```bash
cd D:/prj/IAET
dotnet new classlib -n Iaet.Core -o src/Iaet.Core
rm src/Iaet.Core/Class1.cs
dotnet sln add src/Iaet.Core/Iaet.Core.csproj
```

- [ ] **Step 2: Copy and adapt model files**

**IMPORTANT:** Only copy `.cs` source files from GVResearch, NOT `.csproj` files. The `dotnet new classlib` in Step 1 creates a fresh `.csproj` that inherits from the new `Directory.Build.props`. After running `dotnet new`, strip the redundant `<PropertyGroup>` contents from the generated `.csproj` (leaving only the SDK attribute and any package/project references), since `Directory.Build.props` already sets `TargetFramework`, `Nullable`, etc.

Copy from GVResearch:
- `src/Iaet.Core/Models/CapturedRequest.cs`
- `src/Iaet.Core/Models/CaptureSessionInfo.cs`
- `src/Iaet.Core/Models/EndpointSignature.cs`
- `src/Iaet.Core/Models/EndpointGroup.cs`

These need no changes beyond verifying they compile on .NET 10.

- [ ] **Step 3: Copy interface files and extract result types to Models**

Copy from GVResearch:
- `src/Iaet.Core/Abstractions/ICaptureSession.cs`
- `src/Iaet.Core/Abstractions/IEndpointCatalog.cs`
- `src/Iaet.Core/Abstractions/IApiAdapter.cs`

For `ISchemaInferrer.cs` and `IReplayEngine.cs`, split the result types out:

Create `src/Iaet.Core/Abstractions/ISchemaInferrer.cs` (interface only):
```csharp
using Iaet.Core.Models;

namespace Iaet.Core.Abstractions;

public interface ISchemaInferrer
{
    Task<SchemaResult> InferAsync(IReadOnlyList<string> jsonBodies, CancellationToken ct = default);
}
```

Create `src/Iaet.Core/Models/SchemaResult.cs`:
```csharp
namespace Iaet.Core.Models;

public sealed record SchemaResult(
    string JsonSchema,
    string CSharpRecord,
    string OpenApiFragment,
    IReadOnlyList<string> Warnings
);
```

Create `src/Iaet.Core/Abstractions/IReplayEngine.cs` (interface only):
```csharp
using Iaet.Core.Models;

namespace Iaet.Core.Abstractions;

public interface IReplayEngine
{
    Task<ReplayResult> ReplayAsync(CapturedRequest original, CancellationToken ct = default);
}
```

Create `src/Iaet.Core/Models/ReplayResult.cs`:
```csharp
namespace Iaet.Core.Models;

public sealed record ReplayResult(
    int ResponseStatus,
    string? ResponseBody,
    IReadOnlyList<FieldDiff> Diffs,
    long DurationMs
);

public sealed record FieldDiff(string Path, string? Expected, string? Actual);
```

Move `EndpointDescriptor` from `IApiAdapter.cs` to its own file:

Create `src/Iaet.Core/Models/EndpointDescriptor.cs`:
```csharp
namespace Iaet.Core.Models;

public sealed record EndpointDescriptor(
    string HumanName,
    string Category,
    bool IsDestructive,
    string? Notes
);
```

Update `IApiAdapter.cs` to import from Models:
```csharp
using Iaet.Core.Models;

namespace Iaet.Core.Abstractions;

public interface IApiAdapter
{
    string TargetName { get; }
    bool CanHandle(CapturedRequest request);
    EndpointDescriptor Describe(CapturedRequest request);
}
```

- [ ] **Step 4: Add new stream models (from spec Section 4.2)**

Create `src/Iaet.Core/Models/CapturedStream.cs`:
```csharp
namespace Iaet.Core.Models;

public sealed record CapturedStream
{
    public required Guid Id { get; init; }
    public required Guid SessionId { get; init; }
    public required StreamProtocol Protocol { get; init; }
    public required string Url { get; init; }
    public required DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset? EndedAt { get; init; }
    public required StreamMetadata Metadata { get; init; }
    public IReadOnlyList<StreamFrame>? Frames { get; init; }
    public string? SamplePayloadPath { get; init; }
    public string? Tag { get; init; }
}

public enum StreamProtocol
{
    WebSocket,
    ServerSentEvents,
    WebRtc,
    HlsStream,
    DashStream,
    GrpcWeb,
    WebAudio,
    Unknown
}

public sealed record StreamMetadata(
    Dictionary<string, string> Properties
);

public sealed record StreamFrame(
    DateTimeOffset Timestamp,
    StreamFrameDirection Direction,
    string? TextPayload,
    byte[]? BinaryPayload,
    long SizeBytes
);

public enum StreamFrameDirection { Sent, Received }
```

- [ ] **Step 5: Add Annotation and StabilityRating models (from spec Section 12.1)**

Create `src/Iaet.Core/Models/Annotation.cs`:
```csharp
namespace Iaet.Core.Models;

public sealed record Annotation(
    string? HumanName,
    string? Description,
    IReadOnlyList<string> Tags,
    StabilityRating Stability,
    bool IsDestructive
);

public enum StabilityRating { Unknown, Stable, Unstable, Deprecated }
```

- [ ] **Step 6: Add ICdpSession and IProtocolListener interfaces (from spec Section 4.3.1)**

Create `src/Iaet.Core/Abstractions/ICdpSession.cs` (stub — full implementation in Phase 2):
```csharp
namespace Iaet.Core.Abstractions;

/// <summary>
/// Abstraction over Chrome DevTools Protocol session.
/// Full implementation provided by Iaet.Capture in Phase 2.
/// </summary>
public interface ICdpSession
{
    Task SubscribeToDomainAsync(string domain, CancellationToken ct = default);
    Task UnsubscribeFromDomainAsync(string domain, CancellationToken ct = default);
}
```

Create `src/Iaet.Core/Abstractions/IProtocolListener.cs` (matches spec exactly):
```csharp
using Iaet.Core.Models;

namespace Iaet.Core.Abstractions;

public interface IProtocolListener
{
    string ProtocolName { get; }
    StreamProtocol Protocol { get; }
    bool CanAttach(ICdpSession cdpSession);
    Task AttachAsync(ICdpSession cdpSession, IStreamCatalog catalog, CancellationToken ct = default);
    Task DetachAsync(CancellationToken ct = default);
}
```

- [ ] **Step 7: Add IStreamCatalog interface**

Create `src/Iaet.Core/Abstractions/IStreamCatalog.cs`:
```csharp
using Iaet.Core.Models;

namespace Iaet.Core.Abstractions;

public interface IStreamCatalog
{
    Task SaveStreamAsync(CapturedStream stream, CancellationToken ct = default);
    Task<IReadOnlyList<CapturedStream>> GetStreamsBySessionAsync(Guid sessionId, CancellationToken ct = default);
}
```

- [ ] **Step 8: Verify build**

```bash
dotnet build src/Iaet.Core
```

- [ ] **Step 9: Commit**

```bash
git add src/Iaet.Core/
git commit -m "feat: port Iaet.Core with contracts, models, stream types, and protocol listener interface"
```

---

## Task 3: Port Iaet.Core.Tests

**Source:** `D:/prj/GVResearch/tests/Iaet.Core.Tests/`
**Destination:** `D:/prj/IAET/tests/Iaet.Core.Tests/`

- [ ] **Step 1: Create test project**

```bash
cd D:/prj/IAET
dotnet new xunit -n Iaet.Core.Tests -o tests/Iaet.Core.Tests
rm tests/Iaet.Core.Tests/UnitTest1.cs
dotnet sln add tests/Iaet.Core.Tests/Iaet.Core.Tests.csproj
dotnet add tests/Iaet.Core.Tests reference src/Iaet.Core
dotnet add tests/Iaet.Core.Tests package FluentAssertions
dotnet add tests/Iaet.Core.Tests package NSubstitute
```

- [ ] **Step 2: Copy EndpointSignatureTests.cs from GVResearch**

Copy `D:/prj/GVResearch/tests/Iaet.Core.Tests/Models/EndpointSignatureTests.cs` to `D:/prj/IAET/tests/Iaet.Core.Tests/Models/EndpointSignatureTests.cs`.

Verify it compiles and passes on .NET 10.

- [ ] **Step 3: Run tests**

```bash
dotnet test tests/Iaet.Core.Tests -v n
```

Expected: All tests pass.

- [ ] **Step 4: Commit**

```bash
git add tests/Iaet.Core.Tests/
git commit -m "feat: port Iaet.Core.Tests with endpoint signature tests"
```

---

## Task 4: Port Iaet.Capture with Migration Improvements

**Source:** `D:/prj/GVResearch/src/Iaet.Capture/`
**Destination:** `D:/prj/IAET/src/Iaet.Capture/`

- [ ] **Step 1: Create project and copy files**

```bash
dotnet new classlib -n Iaet.Capture -o src/Iaet.Capture
rm src/Iaet.Capture/Class1.cs
dotnet sln add src/Iaet.Capture/Iaet.Capture.csproj
dotnet add src/Iaet.Capture reference src/Iaet.Core
dotnet add src/Iaet.Capture package Microsoft.Playwright
dotnet add src/Iaet.Capture package Microsoft.Extensions.DependencyInjection.Abstractions
```

Copy from GVResearch:
- `src/Iaet.Capture/RequestSanitizer.cs`
- `src/Iaet.Capture/CdpNetworkListener.cs`
- `src/Iaet.Capture/PlaywrightCaptureSession.cs`

- [ ] **Step 2: Add headless flag support**

Modify `PlaywrightCaptureSession` constructor to accept options:

```csharp
public sealed class PlaywrightCaptureSession : ICaptureSession
{
    private readonly CaptureOptions _options;
    // ... existing fields ...

    public PlaywrightCaptureSession(CaptureOptions options)
    {
        _options = options;
    }

    public async Task StartAsync(string url, CancellationToken ct = default)
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = _options.Headless,
            Args = string.IsNullOrEmpty(_options.Profile)
                ? []
                : [$"--profile-directory={_options.Profile}"]
        });
        // ... rest unchanged
    }
}

public sealed class CaptureOptions
{
    public required string TargetApplication { get; init; }
    public string? Profile { get; init; }
    public bool Headless { get; init; }
}
```

- [ ] **Step 3: Fix request correlation key in CdpNetworkListener**

Replace the string key with a sequence-based approach. Note: Playwright's `IRequest` reference identity between Request event and `response.Request` is not guaranteed, so use URL + Method + monotonic counter to avoid collisions:

```csharp
private long _nextRequestId;

public void Attach(IPage page)
{
    page.Request += (_, request) =>
    {
        if (!IsXhrOrFetch(request)) return;
        var id = Interlocked.Increment(ref _nextRequestId);
        // Use counter as suffix to avoid collision on concurrent identical requests
        var key = $"{request.Method}:{request.Url}:{id}";
        _pending[key] = new PendingRequest(Stopwatch.StartNew(), request, key);
    };

    page.Response += async (_, response) =>
    {
        // Find matching pending request by URL + Method prefix (lowest counter = FIFO)
        var prefix = $"{response.Request.Method}:{response.Request.Url}:";
        var matchKey = _pending.Keys
            .Where(k => k.StartsWith(prefix))
            .OrderBy(k => long.Parse(k[(prefix.Length)..]))
            .FirstOrDefault();
        if (matchKey is null || !_pending.TryRemove(matchKey, out var pending)) return;
        pending.Stopwatch.Stop();
        // ... rest unchanged
    };
}
```

Update `PendingRequest` to include the key:
```csharp
private sealed record PendingRequest(Stopwatch Stopwatch, IRequest Request, string Key);
```
```

- [ ] **Step 4: Harden StopAsync for partial-start**

```csharp
public async Task StopAsync(CancellationToken ct = default)
{
    IsRecording = false;
    if (_browser is not null)
    {
        try { await _browser.CloseAsync(); }
        catch (PlaywrightException) { /* browser may already be closed */ }
    }
    _playwright?.Dispose();
}
```

- [ ] **Step 5: Add factory for creating capture sessions and DI registration**

`CaptureOptions` comes from CLI arguments at invocation time, not from DI. Use a factory pattern:

Create `src/Iaet.Capture/ICaptureSessionFactory.cs`:
```csharp
using Iaet.Core.Abstractions;

namespace Iaet.Capture;

public interface ICaptureSessionFactory
{
    ICaptureSession Create(CaptureOptions options);
}

public sealed class PlaywrightCaptureSessionFactory : ICaptureSessionFactory
{
    public ICaptureSession Create(CaptureOptions options) => new PlaywrightCaptureSession(options);
}
```

Create `src/Iaet.Capture/ServiceCollectionExtensions.cs`:
```csharp
using Microsoft.Extensions.DependencyInjection;

namespace Iaet.Capture;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIaetCapture(this IServiceCollection services)
    {
        services.AddSingleton<ICaptureSessionFactory, PlaywrightCaptureSessionFactory>();
        return services;
    }
}
```

CLI commands resolve `ICaptureSessionFactory` and call `factory.Create(new CaptureOptions { ... })` with CLI-provided values.

- [ ] **Step 6: Build**

```bash
dotnet build src/Iaet.Capture
```

- [ ] **Step 7: Commit**

```bash
git add src/Iaet.Capture/
git commit -m "feat: port Iaet.Capture with headless flag, improved correlation, and DI registration"
```

---

## Task 5: Add Iaet.Capture.Tests (New)

**Files:**
- Create: `tests/Iaet.Capture.Tests/Iaet.Capture.Tests.csproj`
- Create: `tests/Iaet.Capture.Tests/RequestSanitizerTests.cs`

- [ ] **Step 1: Create test project**

```bash
dotnet new xunit -n Iaet.Capture.Tests -o tests/Iaet.Capture.Tests
rm tests/Iaet.Capture.Tests/UnitTest1.cs
dotnet sln add tests/Iaet.Capture.Tests/Iaet.Capture.Tests.csproj
dotnet add tests/Iaet.Capture.Tests reference src/Iaet.Capture
dotnet add tests/Iaet.Capture.Tests package FluentAssertions
```

- [ ] **Step 2: Write RequestSanitizer tests (TDD)**

Create `tests/Iaet.Capture.Tests/RequestSanitizerTests.cs`:

```csharp
using FluentAssertions;
using Iaet.Capture;

namespace Iaet.Capture.Tests;

public class RequestSanitizerTests
{
    [Fact]
    public void SanitizeHeaders_RedactsAuthorizationHeader()
    {
        var headers = new Dictionary<string, string>
        {
            ["Authorization"] = "Bearer secret-token-123",
            ["Content-Type"] = "application/json"
        };

        var result = RequestSanitizer.SanitizeHeaders(headers);

        result["Authorization"].Should().Be("<REDACTED>");
        result["Content-Type"].Should().Be("application/json");
    }

    [Fact]
    public void SanitizeHeaders_RedactsCookies()
    {
        var headers = new Dictionary<string, string>
        {
            ["Cookie"] = "session=abc123; token=xyz",
            ["Accept"] = "text/html"
        };

        var result = RequestSanitizer.SanitizeHeaders(headers);

        result["Cookie"].Should().Be("<REDACTED>");
        result["Accept"].Should().Be("text/html");
    }

    [Fact]
    public void SanitizeHeaders_RedactsAllSensitiveHeaders()
    {
        var headers = new Dictionary<string, string>
        {
            ["authorization"] = "secret",
            ["cookie"] = "secret",
            ["set-cookie"] = "secret",
            ["x-goog-authuser"] = "secret",
            ["x-csrf-token"] = "secret",
            ["x-xsrf-token"] = "secret",
            ["user-agent"] = "Mozilla/5.0"
        };

        var result = RequestSanitizer.SanitizeHeaders(headers);

        result.Where(kv => kv.Value == "<REDACTED>").Should().HaveCount(6);
        result["user-agent"].Should().Be("Mozilla/5.0");
    }

    [Fact]
    public void SanitizeHeaders_IsCaseInsensitive()
    {
        var headers = new Dictionary<string, string>
        {
            ["AUTHORIZATION"] = "Bearer token",
            ["COOKIE"] = "session=abc"
        };

        var result = RequestSanitizer.SanitizeHeaders(headers);

        result["AUTHORIZATION"].Should().Be("<REDACTED>");
        result["COOKIE"].Should().Be("<REDACTED>");
    }

    [Fact]
    public void SanitizeHeaders_EmptyDictionary_ReturnsEmpty()
    {
        var result = RequestSanitizer.SanitizeHeaders(new Dictionary<string, string>());
        result.Should().BeEmpty();
    }
}
```

- [ ] **Step 3: Run tests**

```bash
dotnet test tests/Iaet.Capture.Tests -v n
```

Expected: All 5 tests pass.

- [ ] **Step 4: Commit**

```bash
git add tests/Iaet.Capture.Tests/
git commit -m "feat: add Iaet.Capture.Tests with RequestSanitizer tests"
```

---

## Task 6: Port Iaet.Catalog with EF Core Migrations

**Source:** `D:/prj/GVResearch/src/Iaet.Catalog/`
**Destination:** `D:/prj/IAET/src/Iaet.Catalog/`

- [ ] **Step 1: Create project and copy files**

```bash
dotnet new classlib -n Iaet.Catalog -o src/Iaet.Catalog
rm src/Iaet.Catalog/Class1.cs
dotnet sln add src/Iaet.Catalog/Iaet.Catalog.csproj
dotnet add src/Iaet.Catalog reference src/Iaet.Core
dotnet add src/Iaet.Catalog package Microsoft.EntityFrameworkCore.Sqlite
dotnet add src/Iaet.Catalog package Microsoft.EntityFrameworkCore.Design
dotnet add src/Iaet.Catalog package Microsoft.Extensions.DependencyInjection.Abstractions
```

Copy from GVResearch:
- `src/Iaet.Catalog/CatalogDbContext.cs`
- `src/Iaet.Catalog/SqliteCatalog.cs`
- `src/Iaet.Catalog/EndpointNormalizer.cs`
- `src/Iaet.Catalog/CatalogDbContextFactory.cs`
- `src/Iaet.Catalog/Entities/CaptureSessionEntity.cs`
- `src/Iaet.Catalog/Entities/CapturedRequestEntity.cs`
- `src/Iaet.Catalog/Entities/EndpointGroupEntity.cs`

- [ ] **Step 2: Add stream entity for CapturedStream storage**

Create `src/Iaet.Catalog/Entities/CapturedStreamEntity.cs`:

```csharp
namespace Iaet.Catalog.Entities;

public class CapturedStreamEntity
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public required string Protocol { get; set; }
    public required string Url { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public string? MetadataJson { get; set; }
    public string? FramesJson { get; set; }
    public string? SamplePayloadPath { get; set; }
    public string? Tag { get; set; }

    public CaptureSessionEntity? Session { get; set; }
}
```

- [ ] **Step 3: Update CatalogDbContext to include stream entity and use migrations**

Add to `CatalogDbContext`:
```csharp
public DbSet<CapturedStreamEntity> Streams => Set<CapturedStreamEntity>();
```

Add entity configuration in `OnModelCreating`:
```csharp
modelBuilder.Entity<CapturedStreamEntity>(e =>
{
    e.HasKey(x => x.Id);
    e.HasIndex(x => x.SessionId);
    e.HasIndex(x => x.Protocol);
    e.HasOne(x => x.Session).WithMany().HasForeignKey(x => x.SessionId);
});
```

- [ ] **Step 4: Add DI service registration**

Create `src/Iaet.Catalog/ServiceCollectionExtensions.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Iaet.Catalog;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIaetCatalog(this IServiceCollection services,
        string connectionString = "DataSource=catalog.db")
    {
        services.AddDbContext<CatalogDbContext>(options =>
            options.UseSqlite(connectionString));
        services.AddScoped<Iaet.Core.Abstractions.IEndpointCatalog, SqliteCatalog>();
        return services;
    }
}
```

- [ ] **Step 5: Install dotnet-ef and create initial EF Core migration**

Ensure `CatalogDbContextFactory.cs` is copied (from Step 1) before running migrations — `dotnet ef` needs it.

```bash
dotnet new tool-manifest
dotnet tool install dotnet-ef
dotnet ef migrations add InitialCreate --project src/Iaet.Catalog --startup-project src/Iaet.Catalog
```

- [ ] **Step 6: Build**

```bash
dotnet build src/Iaet.Catalog
```

- [ ] **Step 7: Commit**

```bash
git add src/Iaet.Catalog/ .config/
git commit -m "feat: port Iaet.Catalog with stream entity, DI registration, and EF Core migrations"
```

---

## Task 7: Port Iaet.Catalog.Tests

**Source:** `D:/prj/GVResearch/tests/Iaet.Catalog.Tests/`

- [ ] **Step 1: Create test project**

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

- [ ] **Step 2: Copy test files from GVResearch**

Copy:
- `EndpointNormalizerTests.cs`
- `SqliteCatalogTests.cs`

Adapt for .NET 10 if needed (likely no changes).

**IMPORTANT:** Tests should keep using `_db.Database.EnsureCreated()` for in-memory SQLite databases. This is intentional — EF Core Migrations are for persistent databases only. In-memory test databases don't need migration history.

- [ ] **Step 3: Run tests**

```bash
dotnet test tests/Iaet.Catalog.Tests -v n
```

- [ ] **Step 4: Commit**

```bash
git add tests/Iaet.Catalog.Tests/
git commit -m "feat: port Iaet.Catalog.Tests"
```

---

## Task 8: Port Iaet.Cli with DI Host Builder

**Source:** `D:/prj/GVResearch/src/Iaet.Cli/`

- [ ] **Step 1: Create project**

```bash
dotnet new console -n Iaet.Cli -o src/Iaet.Cli
rm src/Iaet.Cli/Program.cs
dotnet sln add src/Iaet.Cli/Iaet.Cli.csproj
dotnet add src/Iaet.Cli reference src/Iaet.Core
dotnet add src/Iaet.Cli reference src/Iaet.Capture
dotnet add src/Iaet.Cli reference src/Iaet.Catalog
dotnet add src/Iaet.Cli package System.CommandLine --prerelease
dotnet add src/Iaet.Cli package Microsoft.Extensions.Hosting
dotnet add src/Iaet.Cli package Serilog.Extensions.Hosting
dotnet add src/Iaet.Cli package Serilog.Sinks.Console
dotnet add src/Iaet.Cli package Serilog.Sinks.File
```

Add tool metadata to csproj:
```xml
<PackAsTool>true</PackAsTool>
<ToolCommandName>iaet</ToolCommandName>
<PackageOutputPath>./nupkg</PackageOutputPath>
```

- [ ] **Step 2: Create DI-based Program.cs**

Replace the manual construction from GVResearch with a proper host builder:

```csharp
using System.CommandLine;
using Iaet.Capture;
using Iaet.Catalog;
using Iaet.Cli.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/iaet-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

var host = Host.CreateDefaultBuilder(args)
    .UseSerilog()
    .ConfigureServices((context, services) =>
    {
        var dbPath = context.Configuration["Iaet:DatabasePath"] ?? "catalog.db";
        services.AddIaetCapture();
        services.AddIaetCatalog($"DataSource={dbPath}");
    })
    .Build();

var rootCommand = new RootCommand("IAET - Internal API Extraction Toolkit")
{
    CaptureCommand.Create(host.Services),
    CatalogCommand.Create(host.Services)
};

return await rootCommand.InvokeAsync(args);
```

- [ ] **Step 3: Copy and adapt CaptureCommand.cs and CatalogCommand.cs**

Copy from GVResearch, modify to resolve services from DI instead of manual construction. The commands receive an `IServiceProvider` and create scopes per invocation.

Key change for CaptureCommand: resolve `ICaptureSessionFactory` from DI, then call `factory.Create(new CaptureOptions { ... })` with CLI-provided target/profile/headless values:

```csharp
// OLD (GVResearch): await using var session = new PlaywrightCaptureSession(target, profile);
// NEW (IAET):
var factory = scope.ServiceProvider.GetRequiredService<ICaptureSessionFactory>();
await using var session = factory.Create(new CaptureOptions
{
    TargetApplication = target,
    Profile = profile,
    Headless = headless
});
```

Key change for CatalogCommand: resolve `IEndpointCatalog` from DI instead of manually constructing `CatalogDbContext` + `SqliteCatalog`.

Key change for BOTH commands — scope creation and migrations:
```csharp
// EVERY command handler must create a scope (services are registered as Scoped):
using var scope = serviceProvider.CreateScope();
var catalog = scope.ServiceProvider.GetRequiredService<IEndpointCatalog>();

// Run migrations on first use (replaces EnsureCreatedAsync per spec Section 12):
var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
await db.Database.MigrateAsync();
```

Key change for CaptureCommand — add `--headless` CLI option:
```csharp
var headlessOption = new Option<bool>("--headless", "Run browser in headless mode");
startCmd.Add(headlessOption);
```

- [ ] **Step 4: Verify CLI works**

```bash
dotnet run --project src/Iaet.Cli -- --help
dotnet run --project src/Iaet.Cli -- capture --help
dotnet run --project src/Iaet.Cli -- catalog --help
```

- [ ] **Step 5: Commit**

```bash
git add src/Iaet.Cli/
git commit -m "feat: port Iaet.Cli with DI host builder and Serilog logging"
```

---

## Task 9: Create Stub Projects for Future Phases

Create empty classlib projects for assemblies built in later phases. This ensures the solution structure matches the spec from day one.

- [ ] **Step 1: Create stub projects**

```bash
for proj in Iaet.Schema Iaet.Replay Iaet.Crawler Iaet.Export Iaet.Explorer; do
    dotnet new classlib -n $proj -o src/$proj
    rm src/$proj/Class1.cs
    dotnet sln add src/$proj/$proj.csproj
    dotnet add src/$proj reference src/Iaet.Core
done
```

For composite assemblies, add their additional dependencies:
```bash
dotnet add src/Iaet.Crawler reference src/Iaet.Capture
dotnet add src/Iaet.Crawler reference src/Iaet.Catalog
dotnet add src/Iaet.Export reference src/Iaet.Catalog
dotnet add src/Iaet.Export reference src/Iaet.Schema
dotnet add src/Iaet.Explorer reference src/Iaet.Catalog
dotnet add src/Iaet.Explorer reference src/Iaet.Schema
dotnet add src/Iaet.Explorer reference src/Iaet.Replay
dotnet add src/Iaet.Explorer reference src/Iaet.Export
```

- [ ] **Step 2: Add a placeholder class to each to prevent empty-project warnings**

Each stub project gets a `NotYetImplemented.cs`:

```csharp
namespace Iaet.<AssemblyName>;

// This assembly will be implemented in a future phase.
// See docs/superpowers/specs/2026-03-26-iaet-standalone-design.md for details.
internal static class PlaceholderForFuturePhase { }
```

- [ ] **Step 3: Build full solution**

```bash
dotnet build Iaet.sln
```

- [ ] **Step 4: Commit**

```bash
git add src/Iaet.Schema/ src/Iaet.Replay/ src/Iaet.Crawler/ src/Iaet.Export/ src/Iaet.Explorer/
git commit -m "chore: add stub projects for Schema, Replay, Crawler, Export, and Explorer"
```

---

## Task 10: Build Script and GitHub Actions CI

**Files:**
- Create: `scripts/build.ps1`
- Create: `.github/workflows/ci.yml`

- [ ] **Step 1: Create build.ps1**

Same structure as GVResearch build script, adapted for IAET:

```powershell
#!/usr/bin/env pwsh
param(
    [ValidateSet('clean', 'restore', 'build', 'test', 'pack', 'publish')]
    [string]$Target = 'build'
)

$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $PSScriptRoot
$Solution = Join-Path $RepoRoot 'Iaet.sln'

function Invoke-Clean {
    Write-Host "Cleaning..." -ForegroundColor Cyan
    dotnet clean $Solution -v q
    Get-ChildItem $RepoRoot -Recurse -Directory -Include bin, obj |
        Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
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

function Invoke-Pack {
    Invoke-Build
    Write-Host "Packing NuGet packages..." -ForegroundColor Cyan
    dotnet pack $Solution --no-build -c Release -o (Join-Path $RepoRoot 'artifacts')
}

function Invoke-Publish {
    Invoke-Build
    Write-Host "Publishing..." -ForegroundColor Cyan
    dotnet publish src/Iaet.Cli/Iaet.Cli.csproj --no-build -c Release -o (Join-Path $RepoRoot 'artifacts/cli')
}

switch ($Target) {
    'clean'   { Invoke-Clean }
    'restore' { Invoke-Restore }
    'build'   { Invoke-Build }
    'test'    { Invoke-Test }
    'pack'    { Invoke-Pack }
    'publish' { Invoke-Publish }
}

Write-Host "Done: $Target" -ForegroundColor Green
```

- [ ] **Step 2: Create GitHub Actions CI workflow**

Create `.github/workflows/ci.yml`:

```yaml
name: CI

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  build-and-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - name: Restore tools
        run: dotnet tool restore
      - name: Restore
        run: dotnet restore Iaet.sln
      - name: Build
        run: dotnet build Iaet.sln --no-restore -c Release
      - name: Test
        run: dotnet test Iaet.sln --no-build -c Release --collect:"XPlat Code Coverage"
      - name: Pack
        run: dotnet pack Iaet.sln --no-build -c Release -o artifacts/
      - uses: actions/upload-artifact@v4
        with:
          name: nuget-packages
          path: artifacts/*.nupkg
```

- [ ] **Step 3: Test build script locally**

```bash
pwsh scripts/build.ps1 -Target test
```

- [ ] **Step 4: Commit**

```bash
git add scripts/build.ps1 .github/
git commit -m "chore: add build script and GitHub Actions CI workflow"
```

---

## Task 11: Write Documentation

**Files:**
- Create: `README.md`
- Create: `src/Iaet.Core/README.md`
- Create: `src/Iaet.Capture/README.md`
- Create: `src/Iaet.Catalog/README.md`
- Create: `src/Iaet.Cli/README.md`
- Create: `docs/architecture.md`
- Create: `docs/capture-format.md`

- [ ] **Step 1: Write top-level README.md**

Comprehensive README covering:
- What is IAET (elevator pitch)
- Quick Start (install → first capture → view endpoints)
- CLI command overview
- Architecture diagram (text-based)
- Assembly descriptions table
- Legal & ethical guidelines
- Development setup instructions
- Links to detailed docs

- [ ] **Step 2: Write per-assembly READMEs**

One README per `src/Iaet.*` directory:
- Purpose
- Public API
- Dependencies
- Configuration
- How to test

- [ ] **Step 3: Write docs/architecture.md**

Assembly dependency diagram, data flow from capture → catalog → analysis → export.

- [ ] **Step 4: Write docs/capture-format.md**

Full JSON Schema for `.iaet.json` interchange format, including the `streams` array for media data.

- [ ] **Step 5: Commit**

```bash
git add README.md src/*/README.md docs/
git commit -m "docs: add comprehensive README, per-assembly docs, architecture, and capture format spec"
```

---

## Task 12: Integration Verification and Tag

- [ ] **Step 1: Full solution build**

```bash
cd D:/prj/IAET
dotnet build Iaet.sln -c Release
```

Expected: 0 warnings, 0 errors.

- [ ] **Step 2: Run all tests**

```bash
dotnet test Iaet.sln -c Release
```

Expected: All tests pass.

- [ ] **Step 3: Verify CLI**

```bash
dotnet run --project src/Iaet.Cli -- --help
dotnet run --project src/Iaet.Cli -- capture --help
dotnet run --project src/Iaet.Cli -- catalog --help
```

Expected: Help text for all commands.

- [ ] **Step 4: Build NuGet packages**

```bash
pwsh scripts/build.ps1 -Target pack
ls artifacts/
```

Expected: `Iaet.Core.0.1.0.nupkg`, `Iaet.Capture.0.1.0.nupkg`, `Iaet.Catalog.0.1.0.nupkg`, `Iaet.Cli.0.1.0.nupkg`, plus stubs.

- [ ] **Step 5: Push to GitHub**

```bash
git push origin main
```

- [ ] **Step 6: Tag v0.1.0**

```bash
git tag v0.1.0 -m "Phase 1 complete: IAET extracted to standalone repo on .NET 10 with CI, NuGet packaging, and documentation"
git push origin v0.1.0
```

---

## What's Next

After Phase 1, the IAET repo has:
- 4 working assemblies (Core, Capture, Catalog, Cli) on .NET 10
- 5 stub assemblies (Schema, Replay, Crawler, Export, Explorer)
- Full test suite passing
- GitHub Actions CI
- NuGet packages ready for consumption
- Comprehensive documentation

**Phase 2 (Stream Capture)** will be planned separately and covers:
- WebSocket, WebRTC, HLS/DASH, gRPC-Web, SSE listeners
- `CapturedStream` catalog storage
- `iaet streams` CLI commands
- `--capture-samples` flag for selective payload capture
