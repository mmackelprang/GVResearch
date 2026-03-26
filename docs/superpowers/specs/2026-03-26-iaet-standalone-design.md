# IAET Standalone — Design Specification

**Version:** 1.0 | **Date:** 2026-03-26 | **Status:** Draft

## 1. Summary

IAET (Internal API Extraction Toolkit) is a general-purpose CLI toolkit and browser extension ecosystem for discovering, capturing, analyzing, and documenting undocumented browser-based internal APIs from any web application — for educational and research purposes.

This spec defines the extraction of IAET from the GVResearch repo into its own standalone project, plus the new capabilities needed to make it a complete investigation toolkit.

**Core workflow:** Discover → Capture → Catalog → Analyze → Document → Explore

**Target framework:** .NET 10 (LTS), C# 13+

**Distribution:** `dotnet tool install -g iaet`. Browser extensions sideloaded or published to Chrome Web Store.

## 2. Architecture

```
┌──────────────────────────────────────────────────────────────┐
│                     IAET Toolkit                              │
│                                                              │
│  Iaet.Core          (contracts + models)                     │
│  Iaet.Capture       (Playwright CDP capture)                 │
│  Iaet.Catalog       (SQLite endpoint catalog)                │
│  Iaet.Schema        (JSON Schema / C# / OpenAPI inference)   │
│  Iaet.Replay        (HTTP replay + diff engine)              │
│  Iaet.Crawler       (semi-autonomous site crawler)           │
│  Iaet.Export        (reports, Postman, OpenAPI, HAR, C#)     │
│  Iaet.Explorer      (local Swagger-like web UI)              │
│  Iaet.Cli           (dotnet tool entry point)                │
│                                                              │
│  Iaet.Adapters.*    (pluggable per-target adapters)          │
│                                                              │
├──────────────────────────────────────────────────────────────┤
│  Browser Extensions                                          │
│                                                              │
│  iaet-devtools/     (Chrome DevTools panel)                   │
│  iaet-capture/      (Content script background capture)       │
└──────────────────────────────────────────────────────────────┘
```

**Dependency rules:**
- `Iaet.Core` has zero external dependencies — pure contracts and models. Contains all interfaces (`ICaptureSession`, `IEndpointCatalog`, `ISchemaInferrer`, `IReplayEngine`, `IApiAdapter`) and their associated result types (`SchemaResult`, `ReplayResult`, `FieldDiff`, `EndpointDescriptor`).
- **Leaf assemblies** (depend only on `Iaet.Core`): `Iaet.Capture`, `Iaet.Catalog`, `Iaet.Schema`, `Iaet.Replay`.
- **Composite assemblies** (depend on Core + other assemblies):
  - `Iaet.Crawler` → Core + Capture + Catalog (needs browser automation and persistence)
  - `Iaet.Export` → Core + Catalog + Schema (reads catalog data, includes inferred schemas)
  - `Iaet.Explorer` → Core + Catalog + Schema + Replay + Export (full-featured UI)
- `Iaet.Cli` depends on all assemblies and is the main entry point.
- Browser extensions are standalone TypeScript projects that export captures in IAET's `.iaet.json` format.
- Adapters are optional plugins — IAET works fully without any adapter, adapters add target-specific intelligence. Adapters are discovered via `--adapter <path>` CLI flag or by placing assemblies in `~/.iaet/adapters/`.
- No adapters ship with IAET. They live in consumer repos.

**Composition strategy:** The CLI uses `Microsoft.Extensions.Hosting` (`Host.CreateDefaultBuilder`) for DI, logging, and configuration. All assemblies register their services via `IServiceCollection` extension methods (e.g., `services.AddIaetCapture()`, `services.AddIaetCatalog(connectionString)`). This enables proper `IHttpClientFactory` injection for Replay, consistent Serilog logging, and clean testability. The Explorer (ASP.NET Core app) uses the same DI registrations.

## 3. Assemblies — Existing (Ported from GVResearch)

These assemblies already exist in GVResearch and will be migrated to the IAET repo with minimal changes (primarily retargeting from net8.0 to net10.0).

### 3.1 Iaet.Core — Contracts and Models

Pure abstractions: `ICaptureSession`, `IEndpointCatalog`, `ISchemaInferrer`, `IReplayEngine`, `IApiAdapter`. Associated result/descriptor types co-located with their interfaces: `SchemaResult`, `ReplayResult`, `FieldDiff`, `EndpointDescriptor`. Models in their own namespace: `CapturedRequest`, `CaptureSessionInfo`, `EndpointSignature`, `EndpointGroup`. Zero dependencies.

**Migration note:** During extraction, move `EndpointDescriptor` from the Abstractions namespace to Models for consistency. Move `SchemaResult`, `ReplayResult`, and `FieldDiff` to Models as well. The interfaces (`ISchemaInferrer`, `IReplayEngine`) already exist in Core — the new `Iaet.Schema` and `Iaet.Replay` assemblies are implementations, not new contracts.

### 3.2 Iaet.Capture — Playwright CDP Capture Engine

`PlaywrightCaptureSession` implements `ICaptureSession`. Uses Playwright .NET SDK to launch Chromium, attach CDP network listeners, capture all XHR/Fetch traffic. `RequestSanitizer` redacts sensitive headers. `CdpNetworkListener` manages concurrent request/response correlation.

**Migration improvements:**
- Add `--headless` flag support (currently hardcoded to headful). Essential for crawler and CI scenarios.
- Improve request correlation key (currently `URL + Method` which can collide on concurrent identical requests). Use Playwright's request ID or a sequence counter.
- Harden `StopAsync` for partial-start scenarios.
- Add `Iaet.Capture.Tests` project covering `RequestSanitizer` and `CdpNetworkListener` logic.

### 3.3 Iaet.Catalog — SQLite Endpoint Catalog

`SqliteCatalog` implements `IEndpointCatalog`. EF Core + SQLite for persistence. `EndpointNormalizer` normalizes URLs into endpoint signatures. Automatic endpoint grouping and deduplication by signature.

### 3.4 Iaet.Cli — dotnet Tool Entry Point

System.CommandLine-based CLI. Currently has `capture start` and `catalog sessions/endpoints` commands. Will be extended with all new commands.

## 4. Assemblies — New

### 4.1 Iaet.Schema — Schema Inference Engine

Given N captured JSON response bodies for the same endpoint, produces:

- **Merged JSON Schema (draft-07)** — handles type conflicts across captures (e.g., field is `string` in one, `null` in another → `string | null`). Arrays infer item types from all observed elements.
- **C# record definition** — strongly typed, nullable-aware, ready to paste into a project. Uses `required` members, `init` properties.
- **OpenAPI 3.1 schema fragment** — plugs into a generated spec.
- **Conflict warnings** — "field `status` was `string` in 3 captures, `int` in 1"

```csharp
public interface ISchemaInferrer
{
    Task<SchemaResult> InferAsync(IReadOnlyList<string> jsonBodies, CancellationToken ct = default);
}
```

**Implementation approach:** Parse each JSON body into a structural type map (field → observed types). Merge across all bodies. Generate outputs from the merged map. Use `System.Text.Json` for parsing, string templates for C# generation, JSON Schema libraries for schema output.

### 4.2 Iaet.Replay — HTTP Replay + Diff Engine

Re-issues a captured request against the live API and compares:

- Status code match
- Field-level JSON diff (added/removed/changed fields, using JSONPath)
- Response time comparison
- Header diff (optional)

```csharp
public interface IReplayEngine
{
    Task<ReplayResult> ReplayAsync(CapturedRequest original, CancellationToken ct = default);
}
```

**Features:**
- `IHttpClientFactory` with Polly retry + circuit breaker
- Configurable rate limiting (default: 10 req/min, 100 req/day per endpoint)
- Pluggable auth provider (`IReplayAuthProvider`) — adapters can inject credentials
- Dry-run mode (show what would be sent without actually sending)
- Diff output in multiple formats (text, JSON, HTML)

### 4.3 Iaet.Crawler — Semi-Autonomous Site Discovery

Given a starting URL and boundary rules, systematically explores a web application:

1. Launches Playwright browser, navigates to starting URL
2. Discovers interactive elements via DOM inspection (links, buttons, forms, dropdowns)
3. Systematically interacts with them while capturing API traffic via `Iaet.Capture`
4. Builds a site map: page → actions → API calls triggered
5. Handles SPAs (waits for route changes, monitors `pushState`/`popstate`)

**Boundary rules (configurable):**
- URL path whitelist/blacklist patterns
- Max depth (link hops from start)
- Max pages
- Max duration
- Excluded selectors (e.g., `.delete-btn`, `[data-destructive]`)
- Form fill strategy (skip, fill with test data, use provided values)

**Output:** `CrawlReport` — discovered pages, interaction graph, and per-page API call map.

**Also supports script-driven mode:** user provides a Playwright recipe (C# script or TypeScript) that defines the interaction sequence. IAET captures traffic during execution. Recipes are reusable and shareable.

```bash
# Automated crawl
iaet crawl --url https://open.spotify.com --max-pages 20 --max-depth 3

# Script-driven capture
iaet capture run --recipe docs/recipes/spotify-playlist-capture.ts
```

### 4.4 Iaet.Export — Report and Artifact Generation

Produces output from the catalog:

| Format | Command | Description |
|---|---|---|
| Markdown Report | `iaet export report` | API Investigation Report with endpoint catalog, examples, schemas, Mermaid sequence diagrams, stability ratings |
| OpenAPI 3.1 YAML | `iaet export openapi` | Full spec with sanitized examples and inferred schemas |
| Postman Collection | `iaet export postman` | JSON with `<REDACTED>` credential placeholders |
| C# Typed Client | `iaet export csharp` | Record types + partial HttpClient wrapper |
| HAR | `iaet export har` | Standard HTTP Archive for browser/tool import |
| HTML Report | `iaet export html` | Self-contained HTML version of the Markdown report |

All exports operate on a session or set of sessions from the catalog. All sanitize credentials.

### 4.5 Iaet.Explorer — Local Web UI

A local ASP.NET Core app launched via `iaet explore`:

- **Endpoint browser** — list all discovered endpoints, filter by category/method/status
- **Request/response viewer** — syntax-highlighted JSON, headers, timing
- **Schema viewer** — inferred types with field descriptions
- **Interactive replay** — send a request from the UI, see the live response + diff against captured baseline
- **Session timeline** — visual timeline of captured requests
- **Export controls** — generate any export format from the UI

**Tech:** ASP.NET Core Minimal API + Razor Pages (or Blazor Server for interactivity). Functional, not fancy. Serves on `http://localhost:9200` by default.

```bash
iaet explore --db catalog.db --port 9200
# Opens browser to http://localhost:9200
```

## 5. Browser Extensions

### 5.1 iaet-devtools — Chrome DevTools Panel

A panel inside Chrome DevTools:

- **Real-time capture view** — XHR/Fetch requests grouped by normalized endpoint signature
- **Inline tagging** — click to tag requests ("login", "search", "add-to-cart")
- **Pattern detection** — auto-identifies REST CRUD, GraphQL queries/mutations, gRPC-Web
- **Filter presets** — hide analytics (GA, Segment, Mixpanel), ads, telemetry
- **Export to IAET** — one-click export as `.iaet.json`

**Tech:** Chrome Extension Manifest V3, TypeScript, Vite. Uses `chrome.devtools.network` API.

### 5.2 iaet-capture — Background Content Script

Captures without DevTools open:

- **Passive mode** — injects `fetch`/`XMLHttpRequest` interceptors. Records all API calls while browsing normally.
- **Session management** — start/stop from extension popup. Name sessions, set target app.
- **Auto-discovery mode** — builds endpoint map, highlights new endpoints (badge count)
- **Export** — `.iaet.json` file or POST to `iaet import --listen`

**Tech:** Chrome Extension Manifest V3, TypeScript, service worker background capture.

### 5.3 CLI Integration — Import Command

```bash
# Import from extension export file
iaet import --file ~/Downloads/spotify-session.iaet.json --db catalog.db

# Run a local server that extensions POST to directly
iaet import --listen --port 9222 --db catalog.db
```

The `.iaet.json` format is a documented interchange schema (see `docs/capture-format.md`). Both extensions and the CLI produce/consume the same format.

### 5.4 `.iaet.json` Interchange Format

Top-level structure:

```json
{
  "version": "1.0",
  "exportedAt": "2026-03-26T10:30:00Z",
  "source": "iaet-devtools/1.0.0",
  "session": {
    "id": "uuid",
    "name": "spotify-research-001",
    "targetApplication": "Spotify Web Player",
    "startedAt": "2026-03-26T10:00:00Z",
    "stoppedAt": "2026-03-26T10:25:00Z"
  },
  "requests": [
    {
      "id": "uuid",
      "timestamp": "2026-03-26T10:01:23Z",
      "httpMethod": "GET",
      "url": "https://api.spotify.com/v1/me/player",
      "requestHeaders": { "accept": "application/json" },
      "requestBody": null,
      "responseStatus": 200,
      "responseHeaders": { "content-type": "application/json" },
      "responseBody": "{\"device\":{...}}",
      "durationMs": 142,
      "tag": "player-state"
    }
  ]
}
```

**Rules:**
- All sensitive headers (authorization, cookies, CSRF tokens) must be redacted to `<REDACTED>` before export.
- `source` identifies the producer (extension name + version, or `iaet-cli/<version>`).
- `requests[].tag` is optional — set by user tagging in extensions.
- Format is JSON, file extension `.iaet.json`, UTF-8 encoded.
- Full JSON Schema published at `docs/capture-format.md`.

## 6. CLI Command Reference

```
iaet
├── capture
│   ├── start          Start interactive capture session (Playwright browser)
│   ├── stop           Stop a running capture session
│   └── run            Execute a Playwright recipe script
├── crawl              Run semi-autonomous site crawler
├── catalog
│   ├── sessions       List capture sessions
│   ├── endpoints      List discovered endpoints for a session
│   ├── requests       List captured requests (filterable)
│   └── annotate       Add annotations to endpoints
├── schema
│   ├── infer          Infer schemas from captured responses
│   └── show           Display inferred schema for an endpoint
├── replay
│   ├── run            Replay a captured request and diff
│   └── batch          Replay all endpoints in a session
├── export
│   ├── report         Generate Markdown investigation report
│   ├── html           Generate self-contained HTML report
│   ├── openapi        Generate OpenAPI 3.1 YAML spec
│   ├── postman        Generate Postman collection JSON
│   ├── csharp         Generate C# typed client
│   └── har            Export as HTTP Archive
├── explore            Launch local web UI
├── import             Import captures from browser extension
│   ├── --file         Import from .iaet.json file
│   └── --listen       Start import server for live extension push
└── investigate        Guided interactive investigation wizard
```

## 7. Investigation Wizard

Guided interactive mode for beginners:

```
$ iaet investigate

Welcome to IAET! Let's investigate a web application.

? Target application name: Spotify Web Player
? Starting URL: https://open.spotify.com
? Capture method:
  > Interactive (you browse, IAET captures)
    Automated crawler (IAET explores on its own)
    Import from browser extension

Starting capture session 'spotify-web-player-001'...
Browser opened. Perform actions, then press Enter when done.

[Enter pressed]

Captured 47 requests across 12 unique endpoints.

? What next:
  > View discovered endpoints
    Infer schemas
    Export as OpenAPI
    Generate investigation report
    Open interactive explorer
    Start another capture session
```

The wizard wraps the same CLI commands with interactive prompts and helpful defaults. It's an onramp, not a separate system.

## 8. Documentation Structure

### Top-Level README.md

- What is IAET (elevator pitch)
- Quick Start (install → capture → view, under 5 minutes)
- Guided Tutorial ("Investigating the Spotify Web Player" end-to-end)
- CLI Reference (every command with examples)
- Investigation Wizard usage
- Writing Adapters guide (with template)
- Browser Extensions (install, usage, export/import)
- Automated Discovery (crawler, Playwright recipes, boundary rules)
- Architecture Overview (assembly diagram)
- Legal & Ethical Guidelines

### Per-Assembly README.md

Each `src/Iaet.*/` directory has:
- Purpose (2-3 sentences)
- Public API (key interfaces/classes with usage examples)
- Dependencies (what and why)
- Configuration
- Testing (how to run, what's covered)

### docs/ Directory

- `docs/architecture.md` — detailed assembly diagram, data flow, design decisions
- `docs/capture-format.md` — `.iaet.json` interchange format JSON Schema
- `docs/adapter-guide.md` — full adapter authoring guide with real examples
- `docs/tutorials/investigating-spotify.md` — step-by-step tutorial
- `docs/tutorials/investigating-google-maps.md` — second tutorial
- `docs/recipes/spotify-playlist-capture.ts` — Playwright recipe examples
- `docs/recipes/github-api-discovery.ts` — another recipe

## 9. GVResearch Integration Update

After IAET extraction, GVResearch becomes a pure consumer:

**Removed from GVResearch:**
- `src/Iaet.Core/`, `src/Iaet.Capture/`, `src/Iaet.Catalog/`, `src/Iaet.Cli/`
- `tests/Iaet.Core.Tests/`, `tests/Iaet.Catalog.Tests/`
- `src/Iaet.Schema/`, `src/Iaet.Replay/` (empty directories)

**Stays in GVResearch:**
- `Iaet.Adapters.GoogleVoice` — references `Iaet.Core` NuGet package
- `GvResearch.Shared` — references `Iaet.Core` NuGet package
- `GvResearch.Api`, `GvResearch.Sip`, `GvResearch.Softphone` — unchanged

**New dependency chain:**
```
IAET repo (NuGet packages):
  Iaet.Core, Iaet.Capture, Iaet.Catalog, etc.
  iaet CLI (dotnet tool)

GVResearch repo (consumes IAET):
  Iaet.Adapters.GoogleVoice  → Iaet.Core package
  GvResearch.Shared           → Iaet.Core package
  GvResearch.Api              → GvResearch.Shared
  GvResearch.Sip              → GvResearch.Shared
  GvResearch.Softphone        → no IAET dependency
```

GVResearch also retargets to .NET 10 to match IAET.

## 10. Repo Structure

```
iaet/
  src/
    Iaet.Core/                          # Contracts + models
    Iaet.Capture/                       # Playwright CDP capture
    Iaet.Catalog/                       # SQLite endpoint catalog
    Iaet.Schema/                        # Schema inference engine
    Iaet.Replay/                        # HTTP replay + diff
    Iaet.Crawler/                       # Semi-autonomous crawler
    Iaet.Export/                        # Report + artifact generation
    Iaet.Explorer/                      # Local web UI
    Iaet.Cli/                           # dotnet tool entry point
  tests/
    Iaet.Core.Tests/
    Iaet.Capture.Tests/                 # RequestSanitizer + CdpNetworkListener logic
    Iaet.Catalog.Tests/
    Iaet.Schema.Tests/
    Iaet.Replay.Tests/
    Iaet.Crawler.Tests/
    Iaet.Export.Tests/
    Iaet.Explorer.Tests/
  extensions/
    iaet-devtools/                      # Chrome DevTools panel (TS/Vite)
      src/
      manifest.json
      package.json
      README.md
    iaet-capture/                       # Background capture extension (TS/Vite)
      src/
      manifest.json
      package.json
      README.md
  docs/
    architecture.md
    capture-format.md
    adapter-guide.md
    tutorials/
      investigating-spotify.md
      investigating-google-maps.md
    recipes/
      spotify-playlist-capture.ts
      github-api-discovery.ts
  scripts/
    build.ps1
  README.md
  Directory.Build.props
  global.json
  Iaet.sln
  LICENSE
  .gitignore
  .editorconfig
```

## 11. Catalog Evolution and Migration Strategy

The SQLite catalog schema will grow as new assemblies store additional data (crawl results, schema inference results, replay diffs, annotations). Strategy:

- **EF Core Migrations** replace `EnsureCreated()` starting in Phase 1. The CLI runs `Database.MigrateAsync()` on startup.
- All assemblies that extend the schema contribute their own entity configurations to `CatalogDbContext` via `IEntityTypeConfiguration<T>`.
- Users with existing catalog databases get automatic schema upgrades on next `iaet` command invocation.
- Non-destructive migrations only — no dropping columns or tables.

### 11.1 Catalog Interface Extensions

`IEndpointCatalog` will be extended with:
- `GetResponseBodiesAsync(Guid sessionId, string normalizedSignature)` — needed by Schema inference
- `GetRequestsAsync(RequestFilter filter)` — generic filtered query for Export and Explorer
- `GetEndpointGroupsAcrossSessionsAsync(IReadOnlyList<Guid> sessionIds)` — cross-session analysis
- `SaveAnnotationAsync(Guid endpointGroupId, Annotation annotation)` — for `catalog annotate`

```csharp
public sealed record Annotation(
    string? HumanName,
    string? Description,
    IReadOnlyList<string> Tags,
    StabilityRating Stability,
    bool IsDestructive
);

public enum StabilityRating { Unknown, Stable, Unstable, Deprecated }
```

## 12. NuGet Packaging Strategy

- **Package ID prefix:** `Iaet.` (e.g., `Iaet.Core`, `Iaet.Capture`, `Iaet.Catalog`)
- **Versioning:** All packages share the same SemVer version, released together. Simplifies dependency management for consumers. Version format: `MAJOR.MINOR.PATCH` (e.g., `1.0.0`, `1.1.0`, `2.0.0`).
- **Publishing:** GitHub Packages initially (free for public repos), migrate to nuget.org once stable.
- **CI/CD:** GitHub Actions workflow: build → test → pack → publish on tagged commits (`v1.0.0`).
- **Local development:** Consumers (like GVResearch) use a local NuGet package source during development: `nuget.config` pointing to `../iaet/artifacts/`.

## 13. Recipe Runner Architecture

Recipes are Playwright scripts that define browser interactions while IAET captures traffic.

**TypeScript recipes only** (not C# scripts). Rationale:
- Playwright's TypeScript API is the primary/best-documented API
- Users investigating web apps are likely comfortable with JS/TS
- No Roslyn scripting dependency needed
- Recipes are portable — work with `npx playwright` too

**Execution model:**
- `iaet capture run --recipe recipes/spotify.ts` spawns a Node.js subprocess
- IAET launches Playwright browser via its .NET capture engine with `--remote-debugging-port`
- The TS recipe connects to the same browser instance via Playwright's `connect` API
- Recipe controls navigation; IAET captures traffic via CDP
- Recipe exits → IAET drains captured requests to catalog

**Recipe API surface:**
- Full Playwright `Page` API (navigate, click, fill, wait, etc.)
- No direct catalog/schema access — recipes are pure browser automation
- Recipes can export tags via `page.evaluate(() => window.__iaet_tag = "login")` convention

## 14. Capture Tests (Iaet.Capture.Tests)

Unit tests for `RequestSanitizer` (redacts correct headers, preserves non-sensitive ones) and `CdpNetworkListener` logic (drains captured requests, handles empty state). Browser-dependent integration tests (actual Playwright capture) remain manual.

## 15. Technology Stack

| Concern | Choice | Rationale |
|---|---|---|
| Runtime | .NET 10 (LTS) | Latest LTS, C# 13+ features |
| Language | C# 13+ | Primary constructors, collection expressions, etc. |
| Browser automation | Playwright .NET | CDP access, cross-browser, headless/headful |
| Database | SQLite via EF Core 10 | Portable, zero-config, file-based catalog |
| CLI framework | System.CommandLine | .NET native, tree-based command structure |
| HTTP client | IHttpClientFactory + Polly | Resilience, rate limiting, DI integration |
| Logging | Serilog | Structured JSON + console logging |
| Schema generation | System.Text.Json + NJsonSchema | JSON parsing + JSON Schema draft-07 generation |
| Web UI (Explorer) | ASP.NET Core Minimal API + Razor Pages | Lightweight, built-in to .NET |
| Browser extensions | TypeScript + Vite | Modern Chrome Extension Manifest V3 |
| Testing | xUnit + FluentAssertions + NSubstitute | Same as GVResearch for consistency |
| Build | PowerShell (pwsh) | Cross-platform build script |

## 16. Phased Implementation

| Phase | Focus | Deliverables |
|---|---|---|
| **1 — Extract + Retarget** | Move existing Iaet.* code to new repo, retarget to .NET 10, set up CI | Working `iaet capture` + `iaet catalog` on .NET 10 |
| **2 — Schema + Replay** | Build Iaet.Schema and Iaet.Replay | `iaet schema infer` + `iaet replay run` |
| **3 — Export + Documentation** | Build Iaet.Export, write all documentation and tutorials | All export formats, comprehensive README, tutorials |
| **4 — Crawler** | Build Iaet.Crawler with boundary rules and recipe support | `iaet crawl` + recipe runner |
| **5 — Explorer** | Build Iaet.Explorer local web UI | `iaet explore` with endpoint browser and interactive replay |
| **6 — Browser Extensions** | Build iaet-devtools and iaet-capture Chrome extensions | Both extensions + `iaet import` command |
| **7 — Investigation Wizard** | Build guided interactive mode | `iaet investigate` |
| **8 — GVResearch Update** | Update GVResearch to consume IAET packages, retarget to .NET 10 | GVResearch builds against IAET NuGet packages |
