# GVResearch — Claude Code Context

## IMMEDIATE PRIORITY: SIP WebSocket Connection Fix

The WebSocket connection to Google's SIP proxy was failing because we had the **wrong URL**.

**The fix:**
- **WRONG:** `wss://216.239.36.145:443` (this is the SIP route, not the WebSocket endpoint)
- **RIGHT:** `wss://web.voice.telephony.goog/websocket`

**What was blocking:** The server silently ignored our WebSocket upgrade because we were connecting to the SIP proxy IP directly. The actual WebSocket endpoint is a hostname with a `/websocket` path.

**What to do now:**
1. Read `captures/iaet-exports/gv-websocket-upgrade-headers.json` — has the exact upgrade request/response headers
2. Update `GvSipWebSocketChannel` (or equivalent) to connect to `wss://web.voice.telephony.goog/websocket`
3. Set `Host: web.voice.telephony.goog`, `Origin: https://voice.google.com`, `Sec-WebSocket-Protocol: sip`
4. Do NOT send cookies or auth in the upgrade — auth is post-connection via SIP REGISTER
5. After WebSocket opens, send SIP REGISTER (server will 401, then resend with Bearer token)
6. The `Service-Route` in the 200 OK response provides `216.239.36.145` as the SIP route for subsequent INVITE/BYE messages
7. Test with a real call to verify end-to-end

**All research is complete.** See `docs/investigations/gv-complete-research-summary.md` for the full 6-layer architecture reference.

---

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
- **`GvSignalerClient`** — long-poll push channel for real-time notifications (NOT call signaling — see below)

### Call Signaling Architecture (CRITICAL — updated 2026-03-29)
**Calls use SIP over WebSocket, NOT the signaler channel.**

#### WebSocket Connection (RESOLVED 2026-03-29)
- **URL: `wss://web.voice.telephony.goog/websocket`** (NOT the IP `216.239.36.145`)
- **Host header: `web.voice.telephony.goog`**
- **Path: `/websocket`**
- **Subprotocol: `sip`**
- **NO auth in WebSocket upgrade** — no cookies, no Authorization header
- Auth happens AFTER connection via SIP REGISTER (401 challenge → Bearer token response)
- Extensions: `permessage-deflate; client_max_window_bits`
- Origin: `https://voice.google.com`
- Full upgrade headers captured: `captures/iaet-exports/gv-websocket-upgrade-headers.json`

#### SIP Protocol
- GV embeds a full TsSIP (TypeScript SIP) User Agent in the browser
- SIP domain: `web.c.pbx.voice.sip.google.com`
- SIP proxy (Route header): `216.239.36.145:443` (this is NOT the WebSocket host)
- SIP identity: encoded token from `sipregisterinfo/get` as SIP URI username
- Uses 100rel (PRACK), session timers (90s, refresher=uac), Record-Route
- Google-specific headers: `X-Google-Client-Info`, `P-Preferred-Identity`, `X-GV-PlaceCallContext`

#### SIP REGISTER Flow (captured)
```
1. WebSocket opens to wss://web.voice.telephony.goog/websocket (101 OK)
2. Client sends REGISTER (no auth)
3. Server responds 401 Unauthorized (with realm + nonce)
4. Client sends REGISTER with Bearer token:
   Authorization: Bearer token="{sipToken}", username="{phone}", realm="web.c.pbx.voice.sip.google.com"
5. Server responds 200 OK + Service-Route header (contains 216.239.36.145 as SIP route)
6. Client is now registered and can make/receive calls
```

#### Call Flow (14 frames captured)
INVITE(SDP) → 100 → 183(SDP,100rel) → PRACK → 200(PRACK) → 180(100rel) → PRACK → 200(PRACK) → 200(INVITE) → ACK → [audio] → BYE → 200(BYE)

#### Key Captures
- `captures/iaet-exports/gv-websocket-upgrade-headers.json` — WebSocket handshake (THE BLOCKER FIX)
- `captures/iaet-exports/gv-websocket-sip-capture.json` — Full 14-frame SIP call
- `captures/iaet-exports/gv-rtcstats-full-dump.txt` — WebRTC SDP + ICE + timing
- The signaler BrowserChannel is only for post-call notifications, NOT SDP exchange
- See `docs/investigations/gv-complete-research-summary.md` for everything
- Rate limiting (`GvRateLimiter.cs`) enforced per-endpoint in every sub-client
- Auth encryption (`TokenEncryption.cs`) encrypts/decrypts the full cookie set (AES-256)

### What Still Needs to Be Built
1. **Automated cookie retrieval** — Playwright-based tool to open Chrome, login to Google, extract ALL cookies (including SIDCC, __Secure-*PSIDTS, NID, etc.), encrypt to disk. Should support: (a) one-time interactive login, (b) headless refresh when cookies near expiry, (c) CLI command (`dotnet run -- extract-cookies`) for easy use
2. **`LoginInteractiveAsync()`** — Integrate cookie extraction into `GvAuthService` so it auto-triggers on 401
3. **`TryRefreshSessionAsync()`** — Health check via `threadinginfo/get` + cookie refresh cascade
4. **`SipCallTransport`** — `ICallTransport` using SIPSorcery SIP-over-WebSocket:
   - Connect to `wss://web.voice.telephony.goog/websocket` (NOT the IP address)
   - WebSocket upgrade: NO auth headers, just `Sec-WebSocket-Protocol: sip`, `Origin: https://voice.google.com`
   - After connect: SIP REGISTER with `Authorization: Bearer token="{sipToken}", username="{phone}", realm="web.c.pbx.voice.sip.google.com"`
   - The sipToken comes from `sipregisterinfo/get` REST API call
   - Server returns 401 first (challenge), then 200 OK with `Service-Route` header containing `216.239.36.145` as the SIP route
   - Then INVITE with SDP for calls. Full 14-frame capture: `gv-websocket-sip-capture.json`
   - Full upgrade headers: `gv-websocket-upgrade-headers.json`
5. **Voicemail service** — List, play (signed URL), delete, transcription access
6. **Opus codec support** — Replace G.711 fallback with native Opus for better audio quality (requires native lib or managed Opus decoder)

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
