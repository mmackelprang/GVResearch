# GVResearch — Claude Code Context

## Project Overview

GVResearch is a .NET research platform for Google Voice's undocumented internal API. The goal is to build a headless GV client that can send/receive SMS, manage calls, voicemails, and threads — all without a browser.

## API Research (CRITICAL — read these first)

The Google Voice API has been fully mapped through live traffic capture. Before writing any code, read:

1. **`docs/api-research/gv-api-reference.md`** — Complete endpoint reference (20 endpoints, request/response formats, protobuf-JSON structures, WebRTC signaling, thread types, message type codes)
2. **`docs/api-research/headless-integration-guide.md`** — How to authenticate, make API calls, parse protobuf-JSON, handle real-time push, and make calls without a browser
3. **`captures/iaet-exports/*.iaet.json`** — Raw captured request/response data from 5 investigation sessions (56 total requests)

## Key Technical Facts

### Authentication
- GV uses **cookie-based auth** (SAPISID, SID, HSID, SSID, APISID), NOT OAuth2 bearer tokens
- Authorization header: `SAPISIDHASH <timestamp>_<sha1(timestamp + " " + SAPISID + " " + origin)>`
- **Auth strategy:** One-time Playwright login (human types creds ~10s) → cookies encrypted to disk → reused headless for weeks/months → health check + refresh on each invocation → re-login only when refresh fails
- The existing `GvHttpClientHandler.cs` injects tokens but needs updating to use SAPISIDHASH format
- See `docs/api-research/headless-integration-guide.md` for the full `GvAuthService` implementation pattern with `LoginInteractiveAsync()`, `GetValidCookiesAsync()`, and `TryRefreshSessionAsync()`

### API Protocol
- **Base URL:** `https://clients6.google.com/voice/v1/voiceclient/`
- **Format:** Protobuf serialized as JSON arrays (`application/json+protobuf` with `alt=protojson`)
- **API Key:** `{GV_API_KEY}` (public, scoped to Voice)
- Responses are **nested arrays, not objects** — field position is determined by .proto schema

### Existing Code Status
- `GvCallService.cs` has placeholder endpoints (`/voice/api/calls/*`) that are **WRONG** — the real endpoints are `api2thread/list`, `api2thread/sendsms`, etc. as documented in the API reference
- `GvHttpClientHandler.cs` injects token as GAPS cookie — needs updating to SAPISIDHASH
- Rate limiting (`GvRateLimiter.cs`) is correct and ready to use
- Auth encryption (`TokenEncryption.cs`, `EncryptedFileTokenService.cs`) is ready

### What Needs to Be Built
1. **Update `GvHttpClientHandler`** — Switch from GAPS cookie to SAPISIDHASH authorization header
2. **Replace placeholder endpoints** in `GvCallService` with real ones from the API reference
3. **Build protobuf-JSON parser** — Positional array access for GV responses (see headless guide)
4. **Build `GvThreadService`** — Thread CRUD (list, get, delete, archive, spam, search)
5. **Build `GvSmsService`** — Send/receive SMS via `api2thread/sendsms` + signaler polling
6. **Build `GvVoicemailService`** — List, play (HTTP GET on signed URL), delete, transcription access
7. **Build `GvSignalerClient`** — Long-poll push channel for real-time notifications
8. **Optionally: SIP integration** — Use `sipregisterinfo/get` tokens with a SIP library for VoIP

## Coding Conventions

- .NET 10, C# 13, nullable enabled, TreatWarningsAsErrors
- xUnit + FluentAssertions for testing
- Serilog for structured logging
- No sensitive data in source (tokens encrypted at rest, .gitignore covers captures)
- Rate limits: 10 req/min, 100 req/day per endpoint (enforced by `GvRateLimiter`)
- `--allow-destructive` flag required for delete operations

## Project Structure

```
src/
  GvResearch.Api/          — ASP.NET REST facade (exposes clean REST over GV's internal API)
  GvResearch.Shared/       — Core logic: auth, HTTP client, services, models
  GvResearch.Client.Cli/   — CLI tool for headless GV operations
  GvResearch.Sip/          — SIP/VoIP integration (future)
  GvResearch.Softphone/    — Softphone UI (future)
docs/
  api-research/            — API reference and integration guides (READ FIRST)
  superpowers/plans/       — Implementation plans
  superpowers/specs/       — Design specs
captures/
  iaet-exports/            — Raw .iaet.json capture files from IAET investigation
```
