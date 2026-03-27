GV Research Platform — Product Requirements Document (PRD)Version: 1.1  |  Date: March 25, 2026  |  Author: Senior Design Project Team  |  Status: Draft  
1\. Overview and PurposeThis document defines the requirements for the GV Research Platform, a senior design project with two distinct but integrated components:  
1\. A general-purpose Internal API Extraction Toolkit (IAET) — a reusable framework for capturing, cataloging, replaying, and documenting undocumented internal browser-based APIs from any web application.

2\. A Google Voice Research Module — a concrete application of IAET targeting Google Voice, producing an educational REST facade covering voice calls, SMS, and voicemail.

All work is strictly for educational and research purposes, limited to a single researcher-controlled Google account, and governed by the ethical and legal constraints defined in Section 3\. The platform is implemented in modern C\# / ASP.NET Core 8+ and targets Windows 11 / WSL2 / Linux as primary runtime environments.

2\. Goals and Non-Goals

Goals:  
\- Build a reusable browser traffic capture and analysis toolkit that can be applied to any web application, not just Google Voice.  
\- Document the observed internal API behavior of Google Voice for calls, SMS, and voicemail.  
\- Produce a clean, well-typed educational REST facade (ASP.NET Core 8 Minimal API / Controller API) over the observed behavior.  
\- Demonstrate the REST facade with three working client examples.  
\- Make the toolkit reusable for future research projects (e.g., Google Calendar, Spotify Web, Discord Web).

Non-Goals:  
\- Building a production or commercial Google Voice client.  
\- Automating multiple user accounts or bypassing rate limits or CAPTCHAs.  
\- Publishing captured tokens, cookies, or session data.  
\- Distributing a working Google Voice client library to third parties.  
\- Modifying or reverse-engineering Google's client-side JavaScript code directly.

3\. Legal, Ethical, and Safety Requirements

REQ-LEGAL-001: All research activities must comply with the Google Terms of Service, Google Voice Terms of Service, and applicable law (CFAA, ECPA). Any activity that appears non-compliant must be halted immediately and redesigned or removed.

REQ-LEGAL-002: Experiments are restricted to a single dedicated Google account owned and controlled by the researcher. No other user accounts may be accessed or automated.

REQ-LEGAL-003: All captured session tokens, cookies, and personal data (phone numbers, message content) must be stored locally, encrypted at rest (AES-256 minimum), and never committed to version control.

REQ-LEGAL-004: Rate limiting must be enforced in all replay and automation scripts. Hard caps: maximum 10 requests per minute, maximum 100 requests per day per endpoint.

REQ-LEGAL-005: Destructive operations (delete voicemail, delete SMS) must be disabled by default and require an explicit \--allow-destructive flag at runtime.

REQ-LEGAL-006: No captured internal endpoint URLs, authentication patterns, or session data may be published in any public-facing repository, paper, or report. Published materials describe techniques and patterns only.

4\. Technology Stack Requirements

4.1 Core Platform

REQ-TECH-001: All server-side code must target .NET 8 (LTS) or .NET 9, using C\# 12+ language features including primary constructors, collection expressions, required members, and file-scoped namespaces.

REQ-TECH-002: The REST facade API must be implemented as an ASP.NET Core 8 application using Minimal APIs for simple CRUD endpoints and Controller-based routing for complex endpoints requiring filters, model validation, and versioning.

REQ-TECH-003: The project must use the built-in ASP.NET Core Dependency Injection container. No third-party DI frameworks. Services are registered using IServiceCollection extension methods in dedicated registration classes.

REQ-TECH-004: Configuration must use the ASP.NET Core Options pattern (IOptions\<T\>, IOptionsSnapshot\<T\>) bound from appsettings.json, appsettings.{Environment}.json, environment variables, and user secrets (dotnet user-secrets). No hardcoded credentials or tokens.

REQ-TECH-005: All HTTP client interactions (both to observed internal APIs and to test servers) must use IHttpClientFactory with named or typed clients. Raw HttpClient instantiation is prohibited.

REQ-TECH-006: Logging must use Microsoft.Extensions.Logging with Serilog as the sink provider, writing structured JSON logs to file (rolling daily) and console (human-readable). Log levels must be configurable per-namespace via appsettings.

REQ-TECH-007: The solution must include an OpenAPI/Swagger specification (via Swashbuckle or Microsoft.AspNetCore.OpenApi in .NET 9\) for the educational REST facade, auto-generated from code and available at /swagger in Development environment.

4.2 Data and Persistence

REQ-TECH-008: The capture catalog and schema inference results must be persisted using SQLite via Microsoft.EntityFrameworkCore.Sqlite with EF Core 8 code-first migrations. No raw ADO.NET or Dapper for catalog storage.

REQ-TECH-009: All EF Core entities must use strongly typed IDs (record structs wrapping Guid) to prevent ID mixing bugs. Example: record struct CallId(Guid Value).

REQ-TECH-010: Captured request/response payloads must be stored as JSON in the SQLite database using EF Core's JSON column support (.NET 8 ToJson()) or as serialized strings, with full round-trip fidelity.

4.3 Testing

REQ-TECH-011: Unit tests must use xUnit as the test framework, FluentAssertions for assertions, and NSubstitute for mocking. Test project names must follow the convention \<ProjectName\>.Tests.

REQ-TECH-012: Integration tests for the REST API must use ASP.NET Core's WebApplicationFactory\<TEntryPoint\> to spin up the application in-process. No manual port management.

REQ-TECH-013: Code coverage must be measured using Coverlet and reported in Cobertura XML format. Minimum 70% line coverage is required for all non-capture, non-replay code.

4.4 Tooling and Build

REQ-TECH-014: The solution must use a Directory.Build.props file at the repo root to enforce consistent TargetFramework, Nullable, ImplicitUsings, TreatWarningsAsErrors, and AnalysisMode=All settings across all projects.

REQ-TECH-015: A PowerShell (pwsh) build script (build.ps1) must provide targets: clean, restore, build, test, publish, and docker-build. The script must work on Windows 11 and Ubuntu 22.04+.

REQ-TECH-016: The REST facade must be containerizable via a multi-stage Dockerfile (sdk image for build, aspnet runtime image for final stage). docker-compose.yml must support local development with a volume-mounted SQLite file.

5\. Component 1: Internal API Extraction Toolkit (IAET)

The IAET is the crown jewel of the research platform. It is a general-purpose, target-agnostic toolkit that enables structured discovery, capture, replay, schema inference, and documentation of any undocumented browser-based internal API. The GV Research Module (Section 6\) is implemented as a plugin/adapter on top of IAET.

5.1 IAET Architecture

The toolkit consists of the following assemblies:

Iaet.Core \- Shared abstractions: ICaptureSession, IEndpointCatalog, ISchemaInferrer, IReplayEngine, IApiAdapter. Defines all contracts. No implementations.  
Iaet.Capture \- Chrome DevTools Protocol (CDP) capture engine using the Playwright .NET SDK (headful or headless Chrome) to intercept all XHR/Fetch network events during a recording session.  
Iaet.Catalog \- SQLite-backed endpoint catalog. Stores raw captures, inferred schemas, annotations, and replay results.  
Iaet.Schema \- Schema inference engine. Converts captured JSON payloads into C\# record type definitions and OpenAPI schema fragments.  
Iaet.Replay \- HTTP replay engine. Uses IHttpClientFactory to re-issue captured requests with injected credentials from secure local config.  
Iaet.Cli \- dotnet tool CLI entry point. Commands: capture, catalog, infer, replay, export, annotate.  
Iaet.Adapters.GoogleVoice \- GV-specific IApiAdapter implementation. Maps GV-specific patterns (e.g., Protobuf-encoded fields, RPC-style endpoints) to the canonical IAET schema.

5.2 IAET Functional Requirements

REQ-IAET-001 (Capture Session): The toolkit must support starting a named capture session (iaet capture start \--target "Google Voice" \--profile gv-research) that launches a Playwright-controlled Chromium browser, attaches CDP network listeners, and begins recording all XHR/Fetch traffic to the catalog.

REQ-IAET-002 (Request Record): Each captured network request must be stored with: session ID, timestamp, HTTP method, full URL (path \+ query), sanitized headers (auth values replaced with \<REDACTED\>), request body (raw \+ parsed JSON), response status, response headers, response body (raw \+ parsed JSON), duration in ms, and a researcher-assigned tag (e.g., "send-sms").

REQ-IAET-003 (Endpoint Deduplication): The catalog must automatically group captures by normalized endpoint signature (method \+ path template with IDs replaced by {id} placeholders) and maintain a count of observations per unique endpoint.

REQ-IAET-004 (Schema Inference): The iaet infer command must analyze all captured response bodies for a given endpoint and produce: a merged JSON Schema (draft-07), a C\# record definition, and an OpenAPI 3.1 schema fragment. Conflicts between captures must be flagged as warnings.

REQ-IAET-005 (Replay Engine): The iaet replay command must re-issue a captured request using credentials from secure local config (dotnet user-secrets or encrypted local file) and compare the live response to the captured baseline, reporting field-level diffs.

REQ-IAET-006 (Annotation): Researchers must be able to annotate any catalog entry with: human-readable endpoint name, field-level descriptions, stability rating (stable/unstable/unknown), and whether the endpoint is safe (read-only) or potentially destructive.

REQ-IAET-007 (Export): The iaet export command must produce: a Markdown report (docs/api-catalog.md), a Postman collection JSON (with \<REDACTED\> credential placeholders), and a sanitized OpenAPI 3.1 YAML spec covering all annotated endpoints.

REQ-IAET-008 (Adapter Plugin): The toolkit must support pluggable IApiAdapter implementations loaded via .NET's built-in plugin model (AssemblyLoadContext) so that new target applications (e.g., Spotify Web, Google Calendar) can be added without modifying core toolkit code.

REQ-IAET-009 (Target-Agnostic Design): The Iaet.Core, Iaet.Capture, Iaet.Catalog, Iaet.Schema, and Iaet.Replay assemblies must contain zero Google Voice-specific code. All GV-specific logic must live exclusively in Iaet.Adapters.GoogleVoice.

REQ-IAET-010 (Rate Limiting): The replay engine must enforce configurable per-session rate limits (default: 10 req/min, 100 req/day per endpoint) using the System.Threading.RateLimiting API (TokenBucketRateLimiter or FixedWindowRateLimiter). Exceeding limits must log a warning and block, never throw unhandled exceptions.

5.3 IAET Solution Structure

gv-research/  
  src/  
    Iaet.Core/  
    Iaet.Capture/  
    Iaet.Catalog/  
    Iaet.Schema/  
    Iaet.Replay/  
    Iaet.Cli/  
    Iaet.Adapters.GoogleVoice/  
    GvResearch.Api/              (ASP.NET Core REST facade)  
    GvResearch.Client.Cli/       (Example CLI client)  
  tests/  
    Iaet.Core.Tests/  
    Iaet.Catalog.Tests/  
    Iaet.Schema.Tests/  
    Iaet.Replay.Tests/  
    GvResearch.Api.Tests/  
  docs/  
    gv-research-plan.md  
    api-catalog.md               (generated by iaet export)  
  captures/                      (gitignored \- contains real session data)  
  scripts/  
    build.ps1  
    docker-compose.yml  
  Directory.Build.props  
  global.json  
  GvResearch.sln

6\. Component 2: Google Voice Research Module and REST Facade

6.1 Research Methodology (Phased)

Phase 1 \- Observation (Weeks 1-3): Use iaet capture to record GV web client traffic for: outbound call initiation and termination, inbound call notification, SMS send and receive, voicemail list and playback, and voicemail delete.

Phase 2 \- Catalog and Schema (Weeks 4-6): Run iaet infer on all captures. Annotate endpoints with iaet annotate. Produce first draft of [api-catalog.md](http://api-catalog.md).

Phase 3 \- Replay Verification (Weeks 7-9): Use iaet replay to verify understanding. Confirm minimum required headers per endpoint. Document any Protobuf or binary payload formats found.

Phase 4 \- REST Facade (Weeks 10-13): Implement GvResearch.Api (ASP.NET Core) backed by the GV adapter. Implement three example clients.

Phase 5 \- Documentation and Review (Weeks 14-16): Produce final sanitized documentation, video walkthrough, and project retrospective.

6.2 GV REST Facade API Requirements

REQ-GV-001: Base URL for local development is https://localhost:5001/api/v1. API versioning via URL prefix (/v1, /v2). Version negotiation must use Asp.Versioning.Http NuGet package.

REQ-GV-002: Authentication to the educational REST facade must use ASP.NET Core's built-in bearer token middleware. A static research token (configured via user-secrets) is acceptable for the student project. No OAuth flow required.

REQ-GV-003: All request and response bodies must be application/json serialized using System.Text.Json with JsonSerializerOptions configured for camelCase property names, enum-as-string serialization, and nullable reference type awareness.

REQ-GV-004: All API responses must use RFC 7807 Problem Details (application/problem+json) for error responses, using ASP.NET Core's built-in ProblemDetails support.

REQ-GV-005: Pagination must use cursor-based pagination for all list endpoints. Request: ?limit=20\&cursor=\<opaque\_token\>. Response: { items: \[...\], nextCursor: string | null, total: int }.

6.3 Voice Calls API

C\# Model:

public record CallRecord(  
    CallId Id,  
    CallDirection Direction,  
    PhoneNumber FromNumber,  
    PhoneNumber ToNumber,  
    DateTimeOffset StartedAt,  
    DateTimeOffset? EndedAt,  
    int? DurationSeconds,  
    CallStatus Status  
);

public enum CallDirection { Inbound, Outbound }  
public enum CallStatus { Ringing, Active, Completed, Missed, Voicemail, Failed }  
public readonly record struct CallId(Guid Value);  
public readonly record struct PhoneNumber(string Value);

Endpoints:

GET    /api/v1/calls                  \- List call history (paginated, filterable by direction, status, dateFrom, dateTo)  
GET    /api/v1/calls/{id}             \- Get single call record  
POST   /api/v1/calls                  \- Initiate a call (body: { fromNumber, toNumber })  
DELETE /api/v1/calls/{id}             \- Delete a call record (requires \--allow-destructive mode)

6.4 SMS API

C\# Model:

public record SmsMessage(  
    SmsId Id,  
    ThreadId ThreadId,  
    MessageDirection Direction,  
    PhoneNumber FromNumber,  
    PhoneNumber ToNumber,  
    string Body,  
    DateTimeOffset SentAt,  
    bool IsRead  
);

public enum MessageDirection { Inbound, Outbound }  
public readonly record struct SmsId(Guid Value);  
public readonly record struct ThreadId(Guid Value);

Endpoints:

GET    /api/v1/sms                    \- List messages (paginated, filter: threadId, direction, isRead)  
GET    /api/v1/sms/{id}               \- Get single message  
POST   /api/v1/sms                    \- Send SMS (body: { fromNumber, toNumber, body })  
PATCH  /api/v1/sms/{id}/read          \- Mark as read  
GET    /api/v1/sms/threads            \- List conversation threads  
GET    /api/v1/sms/threads/{threadId} \- Get all messages in thread

6.5 Voicemail API

C\# Model:

public record VoicemailMessage(  
    VoicemailId Id,  
    PhoneNumber FromNumber,  
    PhoneNumber ToNumber,  
    DateTimeOffset ReceivedAt,  
    int DurationSeconds,  
    string? Transcript,  
    bool IsRead,  
    bool IsDeleted  
);

public readonly record struct VoicemailId(Guid Value);

Endpoints:

GET    /api/v1/voicemail              \- List voicemails (paginated, filter: isRead, isDeleted)  
GET    /api/v1/voicemail/{id}         \- Get voicemail details and transcript  
GET    /api/v1/voicemail/{id}/audio   \- Stream voicemail audio (returns audio/mpeg)  
PATCH  /api/v1/voicemail/{id}/read    \- Mark as read  
DELETE /api/v1/voicemail/{id}         \- Delete voicemail (requires \--allow-destructive mode)

7\. Educational Examples

7.1 Example A: Unread Voicemail Dashboard (Blazor WebAssembly)

Objective: Demonstrate consuming a paginated REST API and streaming media in a browser.

// C\# Blazor page \- simplified example  
@page "/voicemail"  
@inject HttpClient Http

@foreach (var vm in voicemails)  
{  
    \<div class="vm-card"\>  
        \<span\>@vm.FromNumber.Value\</span\>  
        \<span\>@vm.ReceivedAt.ToString("g")\</span\>  
        \<span\>@vm.Transcript?\[..Math.Min(80, vm.Transcript.Length)\]...\</span\>  
        \<audio src="/api/v1/voicemail/@vm.Id.Value/audio" controls /\>  
    \</div\>  
}

@code {  
    private List\<VoicemailMessage\> voicemails \= \[\];  
    protected override async Task OnInitializedAsync()  
    {  
        var result \= await Http.GetFromJsonAsync\<PagedResult\<VoicemailMessage\>\>(  
            "/api/v1/voicemail?isRead=false\&limit=20");  
        voicemails \= result?.Items ?? \[\];  
    }  
}

7.2 Example B: SMS Quick-Reply CLI Tool

Objective: Demonstrate REST API consumption from a C\# console application using System.CommandLine.

// dotnet run \-- sms list \--unread  
// dotnet run \-- sms send \--to \+15550987654 \--body "Hello from GvResearch CLI"

var sendCommand \= new Command("send", "Send an SMS message")  
{  
    new Option\<string\>("--to", "Destination phone number") { IsRequired \= true },  
    new Option\<string\>("--body", "Message body") { IsRequired \= true }  
};

sendCommand.SetHandler(async (to, body) \=\>  
{  
    var client \= host.Services.GetRequiredService\<IGvResearchClient\>();  
    var result \= await client.SendSmsAsync(new SendSmsRequest(  
        FromNumber: config.ResearchPhoneNumber,  
        ToNumber: new PhoneNumber(to),  
        Body: body  
    ));  
    Console.WriteLine($"Sent: {result.Id.Value} | Status: {result.Status}");  
}, toOption, bodyOption);

7.3 Example C: Call History Analyzer (ASP.NET Core \+ [Chart.js](http://Chart.js))

Objective: Demonstrate pagination traversal, LINQ aggregation, and API-driven chart rendering.

// Server-side aggregation endpoint added to GvResearch.Api  
// GET /api/v1/calls/analytics?dateFrom=2026-01-01\&dateTo=2026-03-25

app.MapGet("/api/v1/calls/analytics", async (  
    \[FromQuery\] DateTimeOffset dateFrom,  
    \[FromQuery\] DateTimeOffset dateTo,  
    IGvCallService callService) \=\>  
{  
    var calls \= await callService.GetCallsInRangeAsync(dateFrom, dateTo);  
    var analytics \= calls  
        .GroupBy(c \=\> c.FromNumber.Value)  
        .Select(g \=\> new ContactAnalytics(  
            PhoneNumber: g.Key,  
            TotalCalls: g.Count(),  
            TotalDurationSeconds: g.Sum(c \=\> c.DurationSeconds ?? 0),  
            AvgDurationSeconds: (int)g.Average(c \=\> c.DurationSeconds ?? 0\)  
        ))  
        .OrderByDescending(a \=\> a.TotalDurationSeconds)  
        .Take(10)  
        .ToList();  
    return Results.Ok(analytics);  
});

7.4 Example D: IAET Capture Session (Applying the Toolkit to a New Target)

Objective: Demonstrate how IAET can be applied to a completely different web application (e.g., Spotify Web Player) using the same toolchain, with zero changes to core toolkit code.

\# Start a new capture session targeting Spotify Web  
iaet capture start \--target "Spotify Web" \--profile spotify-research \--url https://open.spotify.com

\# Perform actions in the launched browser: play a song, skip, search, like a track  
\# Then stop the session  
iaet capture stop \--session spotify-001

\# Infer schemas from all captured endpoints  
iaet infer \--session spotify-001

\# Export a sanitized catalog  
iaet export \--session spotify-001 \--output docs/spotify-api-catalog.md

\# The above commands work identically for Google Voice:  
iaet capture start \--target "Google Voice" \--profile gv-research \--url https://voice.google.com  
iaet capture stop \--session gv-001  
iaet infer \--session gv-001 \--adapter Iaet.Adapters.GoogleVoice  
iaet export \--session gv-001 \--output docs/gv-api-catalog.md

8\. Non-Functional Requirements

REQ-NFR-001 (Performance): The REST facade API must respond to list endpoints within 200ms (P95) under a local single-user load of 10 concurrent requests. No external network calls may block a request response path.

REQ-NFR-002 (Security): The REST facade must enforce HTTPS-only (UseHsts, UseHttpsRedirection) in non-Development environments. All bearer tokens must be validated using ASP.NET Core's built-in authentication middleware.

REQ-NFR-003 (Resilience): All outbound HTTP calls in the replay engine must use Polly (via Microsoft.Extensions.Http.Resilience) with retry (3 attempts, exponential backoff) and circuit breaker (5 failures in 30s) policies.

REQ-NFR-004 (Observability): The application must expose a /health endpoint (using ASP.NET Core Health Checks) reporting liveness and database connectivity. Metrics must be exposed via /metrics in Prometheus format using prometheus-net.AspNetCore.

REQ-NFR-005 (Extensibility): Adding a new GV feature area (e.g., contacts, call groups) must not require modifying existing controllers or services. New features are added via new IApiAdapter methods and new controller/endpoint classes only.

9\. Project Timeline

Weeks 1-2:   Repo scaffolding, Directory.Build.props, solution structure, CI/CD pipeline (GitHub Actions)  
Weeks 3-4:   Iaet.Core contracts, Iaet.Capture (Playwright CDP integration)  
Weeks 5-6:   Iaet.Catalog (EF Core SQLite), Iaet.Cli (capture/catalog commands)  
Weeks 7-8:   Iaet.Schema inference engine, iaet infer command  
Weeks 9-10:  Iaet.Replay engine, Iaet.Adapters.GoogleVoice, rate limiting  
Weeks 11-12: GvResearch.Api (ASP.NET Core REST facade, all three resource areas)  
Weeks 13-14: Three educational example clients  
Weeks 15-16: Documentation, testing, code coverage, Docker packaging, retrospective

10\. Deliverables

D-001: GvResearch.sln \- Complete Visual Studio solution with all projects building cleanly on .NET 8/9.  
D-002: iaet dotnet tool \- Installable via dotnet tool install, working capture/catalog/infer/replay/export commands.  
D-003: GvResearch.Api \- Dockerized ASP.NET Core REST facade with Swagger UI at /swagger.  
D-004: Three example clients (Blazor dashboard, CLI tool, analytics endpoint).  
D-005: docs/gv-research-plan.md \- This PRD (maintained and updated throughout the project).  
D-006: docs/api-catalog.md \- Sanitized, annotated catalog of observed GV endpoints (no live credentials).  
D-007: Test suite \- xUnit tests with minimum 70% line coverage, Coverlet report.  
D-008: build.ps1 \- Cross-platform build script covering all targets.  
D-009: Project retrospective and lessons-learned document covering: what worked with IAET, challenges with undocumented APIs, and recommendations for applying IAET to future targets.

11\. Success Criteria

The project is considered successful when all of the following are true:

\- iaet capture successfully records GV traffic for all three feature areas (calls, SMS, voicemail).  
\- iaet infer produces valid C\# record types and OpenAPI schemas for at least 10 distinct GV endpoints.  
\- The GvResearch.Api REST facade passes all integration tests for all three resource areas.  
\- All three educational client examples run without errors against the local facade.  
\- iaet capture successfully records traffic from at least one non-GV target (e.g., Spotify, Google Calendar), demonstrating target-agnostic design.  
\- Code coverage is at or above 70% for all non-capture, non-replay code.  
\- The Docker image builds and the compose stack starts with a single docker-compose up command.  
\- No sensitive credentials, tokens, or real phone numbers appear in any committed file.  
