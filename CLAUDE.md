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
- `GvHttpClientHandler.cs` injects SAPISIDHASH + full cookie set on every outgoing request
- `GvAuthService` implements `GetValidCookiesAsync()` and `ComputeSapisidHash()` — `LoginInteractiveAsync()` and `TryRefreshSessionAsync()` are deferred
- See `docs/api-research/headless-integration-guide.md` for the full auth flow including health check and refresh

### API Protocol
- **Base URL:** `https://clients6.google.com/voice/v1/voiceclient/`
- **Format:** Protobuf serialized as JSON arrays (`application/json+protobuf` with `alt=protojson`)
- **API Key:** `{GV_API_KEY}` (public, scoped to Voice)
- Responses are **nested arrays, not objects** — field position is determined by .proto schema

### SDK Architecture (IGvClient)
- **`IGvClient`** is the single entry point — all consumers use it via DI (`services.AddGvClient()`)
- Sub-clients: `IGvAccountClient`, `IGvThreadClient`, `IGvSmsClient`, `IGvCallClient`
- **`GvHttpClientHandler`** injects SAPISIDHASH authorization + cookie headers on every request
- **`GvAuthService`** loads encrypted cookies from disk, caches in memory, computes SAPISIDHASH
- **`GvProtobufJsonParser`** / **`GvRequestBuilder`** handle protobuf-JSON ↔ C# model translation
- **`ICallTransport`** abstracts voice calls — SIP first, WebRTC/Callback pluggable later
- **`GvSignalerClient`** — long-poll push channel for real-time call signaling events
- **`WebRtcCallTransport`** — `ICallTransport` implementation using SIPSorcery `RTCPeerConnection` for WebRTC audio transport
- Rate limiting (`GvRateLimiter.cs`) enforced per-endpoint in every sub-client
- Auth encryption (`TokenEncryption.cs`) encrypts/decrypts the full cookie set (AES-256)

### What Still Needs to Be Built
1. **`SipCallTransport`** — `ICallTransport` implementation using SIPSorcery + `sipregisterinfo/get`
2. **`LoginInteractiveAsync()`** — Playwright-based one-time browser login to populate cookies
3. **`TryRefreshSessionAsync()`** — Health check via `threadinginfo/get` + cookie refresh cascade
4. **Voicemail service** — List, play (signed URL), delete, transcription access

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
  GvResearch.Api/          — ASP.NET REST facade (thin host over IGvClient)
    Endpoints/             — AccountEndpoints, ThreadEndpoints, SmsEndpoints, CallEndpoints
    Auth/                  — BearerSchemeHandler (placeholder, Phase 2)
  GvResearch.Shared/       — Core SDK: IGvClient, auth, protocol, services, models
    Auth/                  — IGvAuthService, GvAuthService, GvCookieSet, TokenEncryption
    Exceptions/            — GvApiException, GvAuthException, GvRateLimitException
    Http/                  — GvHttpClientHandler (SAPISIDHASH injection)
    Models/                — Domain records (GvAccount, GvThread, GvMessage, etc.)
    Protocol/              — GvProtobufJsonParser, GvRequestBuilder
    RateLimiting/          — GvRateLimiter (per-endpoint dual-window)
    Services/              — IGvClient, GvClient, sub-client implementations
    Signaler/              — IGvSignalerClient, GvSignalerClient, SignalerEvent types, SignalerMessageParser
    Transport/             — ICallTransport, TransportCallResult, TransportCallStatus
  GvResearch.Client.Cli/   — CLI tool for headless GV operations (future)
  GvResearch.Sip/          — SIP gateway (uses IGvClient + IGvSignalerClient + WebRtcCallTransport)
    Transport/             — WebRtcCallTransport, WebRtcCallSession
  GvResearch.Softphone/    — Softphone UI (Avalonia, future)
docs/
  api-research/            — API reference and integration guides (READ FIRST)
  superpowers/plans/       — Implementation plans
  superpowers/specs/       — Design specs
captures/
  iaet-exports/            — Raw .iaet.json capture files from IAET investigation
```
