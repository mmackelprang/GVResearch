# GVResearch — Claude Code Context

## Current Status (2026-03-30): End-to-end VoIP calls WORKING

Outbound calls to real phone numbers work end-to-end: SIP signaling, DTLS-SRTP media, and Opus audio playback are all functional. The phone rings, the call connects, and incoming audio plays through the speaker.

**What works:**
- SIP over WebSocket: REGISTER, INVITE, PRACK, 180, 200 OK, ACK, BYE
- DTLS-SRTP media: ICE connectivity + DTLS handshake + SRTP encryption/decryption
- Incoming audio: Opus decode → PCM → NAudio speaker playback (48kHz mono)
- Cookie auto-refresh: health check every 4h + reactive 401 retry from Chrome CDP
- Incoming BYE handling: responds 200 OK, cleans up session

**What still needs work:**
1. **Outbound audio** — Microphone capture → Opus encode → outbound RTP (other side can't hear us yet)
2. **Incoming call support** — Listen for SIP INVITE on WebSocket, surface to UI
3. **Session timer refresh** — Re-INVITE every 90 seconds (Google requires `refresher=uac`)
4. **BYE 200 OK delivery** — Google retransmits BYE; our 200 OK Via headers may need fixing
5. **Voicemail service** — List, play, delete, transcription

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
- **Auth strategy:** Chrome CDP cookie extraction → encrypted to disk → proactive health check every 4h → reactive 401 retry → auto re-extract from Chrome when expired
- `GvHttpClientHandler.cs` injects SAPISIDHASH + full cookie set on every outgoing request; on 401, auto-calls `RefreshCookiesAsync()` and retries
- `GvAuthService` implements `GetValidCookiesAsync()` (with health check), `LoginInteractiveAsync()`, `RefreshCookiesAsync()`, and `ComputeSapisidHash()`
- `CookieRetriever` (in `GvResearch.Shared/Auth/`) extracts cookies via Chrome CDP — shared between CLI and softphone
- Cookie lifetimes: SAPISID/SID ~13 months, COMPASS ~10 days, `__Secure-*PSIDRTS` rotates daily
- See `docs/api-research/headless-integration-guide.md` for the full auth flow

### Environment Setup (REQUIRED)

The GV API key must be set as an environment variable before running any project:

```powershell
# Windows (run once — persists across sessions):
.\setup-env.ps1

# Linux/macOS:
source ./setup-env.sh
```

This sets `GvResearch__ApiKey` which .NET configuration binds to `GvResearch:ApiKey`. The key is Google's public browser-scoped Voice API key — not a secret, but kept out of source to avoid GitGuardian flags.

### API Protocol
- **Base URL:** `https://clients6.google.com/voice/v1/voiceclient/`
- **Format:** Protobuf serialized as JSON arrays (`application/json+protobuf` with `alt=protojson`)
- **API Key:** Set via `GvResearch__ApiKey` env var (see Environment Setup above)
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

### Cookie Retrieval (BUILT — `GvResearch.Client.Cli`)

The CLI tool automates cookie extraction via Chrome DevTools Protocol:

```bash
# Extract cookies (auto-launches Chrome with debug port):
dotnet run --project src/GvResearch.Client.Cli -- --output=D:/prj/GVResearch

# Also capture signaler/API traffic for debugging:
dotnet run --project src/GvResearch.Client.Cli -- --capture-signaler --output=D:/prj/GVResearch
```

**How it works:**
1. Kills any existing Chrome, launches fresh Chrome with `--remote-debugging-port=9222` using a persistent debug profile at `%LOCALAPPDATA%/GvResearch/chrome-debug-profile`
2. Connects via CDP, navigates to voice.google.com (first run: user logs in; subsequent: already authenticated)
3. Extracts ALL cookies (24+) for google.com domains via `context.CookiesAsync()`
4. Builds `GvCookieSet` with `RawCookieHeader` (full cookie string, not just the 7 core cookies)
5. Encrypts to `cookies.enc` + `key.bin` using AES-256 (`TokenEncryption`)
6. Verifies with `account/get` health check (200 OK)
7. Optionally captures signaler/API traffic via CDP `Network.enable` for protocol debugging

**Cookie lifetime:** Core auth cookies (SAPISID, SID) last ~13 months. COMPASS cookies expire in ~10 days. `__Secure-*PSIDRTS` rotate daily.

**Auto-refresh:** The softphone automatically refreshes cookies when they expire:
- `GvAuthService.GetValidCookiesAsync()` runs a health check (`account/get`) every 4 hours
- `GvHttpClientHandler` detects 401 responses → calls `RefreshCookiesAsync()` → re-extracts from Chrome → retries
- No manual intervention needed as long as Chrome has an active Google session

### SIP Call Transport (BUILT — `SipWssCallTransport`)

Real VoIP calls working end-to-end with audio:
- `SipWssCallTransport` implements `ICallTransport` using raw SIP over WebSocket
- `GvSipWebSocketChannel` handles WebSocket with `"sip"` subprotocol
- `GvSipCredentialProvider` fetches credentials from `sipregisterinfo/get`
- Opus decoding via Concentus (48kHz stereo → mono downmix)
- Full flow: REGISTER → 401 → Digest → 200 OK → INVITE → 100 → 183 → PRACK → 180 → PRACK → 200 → ACK → **audio flows**
- Incoming BYE handled with 200 OK response

### DTLS-SRTP Media (RESOLVED — 2026-03-30)

**Root cause of DTLS failure:** Google's media relay uses RSA cipher suites (`TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256`), NOT ECDSA. Confirmed via Chrome WebRTC stats dump (`captures/iaet-exports/gv-rtcstats-full-dump.txt`).

**Required SIPSorcery configuration:**
- `X_UseRsaForDtlsCertificate = true` — use RSA cipher suites (NOT ECDSA)
- `X_UseRtpFeedbackProfile = true` — use `UDP/TLS/RTP/SAVPF` like Chrome
- Must remove `encrypt_then_mac` extension from DTLS ClientHello (Google rejects it)
- Must include `extended_master_secret` extension
- SRTP profiles limited to Chrome-compatible set: AES_128_GCM, AES_256_GCM, AES128_CM_HMAC_SHA1_80/32

**Modified SIPSorcery build:** `local-packages/SIPSorcery.10.0.6-diag.nupkg` — forked from sipsorcery-org/sipsorcery master with:
- DTLS ClientHello extension filtering (remove encrypt_then_mac, status_request)
- Extended master secret always included
- Chrome-compatible SRTP protection profiles (4 instead of 12)
- Diagnostic logging at DTLS handshake, packet, and SRTP levels

**Negotiated parameters (confirmed working):**
- DTLS cipher: `TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256` (0xC02F)
- SRTP profile: `AES_CM_128_HMAC_SHA1_80` (0x0001)
- DTLS version: 1.2 (FEFD)

### Call Signaling + Audio Status (VERIFIED — 2026-03-30)

Full call flow verified end-to-end with real phone call + audio:
- REGISTER → 401 → Digest → 200 OK ✅
- INVITE → 100 Trying ✅
- 183 Session Progress (SDP answer) → PRACK → 200 OK ✅
- 180 Ringing → PRACK → 200 OK ✅ — **phone rings!**
- 200 OK (INVITE) → ACK → **call connected** ✅
- ICE → connected, DTLS → handshake complete, SRTP → keys derived ✅
- **Incoming audio plays through speaker** (Opus 48kHz → PCM) ✅
- Remote BYE received → 200 OK ✅
- Our BYE sent → call ended ✅

### What Still Needs to Be Built
1. **Outbound audio (mic → Opus → RTP)** — Microphone capture via NAudio, Opus encode via Concentus, send as RTP through RTCPeerConnection. Currently the remote side cannot hear us.
2. **Incoming call support** — Listen for SIP INVITE on the WebSocket, auto-answer or surface to UI
3. **Session timer refresh** — Re-INVITE every 90 seconds (Google's SDP has `Session-Expires: 90`, `refresher=uac`)
4. **BYE 200 OK Via headers** — Google retransmits BYE 3-4 times, suggesting our 200 OK response may have incorrect Via headers
5. **Voicemail service** — List, play (signed URL), delete, transcription access

### Audio Pipeline Status (PARTIALLY WORKING — 2026-03-30)

**Inbound audio: WORKING**
- Google sends Opus RTP (pt=111, 48kHz/2ch, ~50 pkt/sec)
- Decoded via Concentus stereo decoder → downmixed to mono → 48kHz 16-bit PCM
- Played through NAudio `WaveOutEvent` at 48kHz
- ~500 packets per 10-second call, only 2-3 occasional SRTP unprotect failures (HMAC, negligible)

**Outbound audio: NOT YET IMPLEMENTED**
- AudioEngine captures mic at 48kHz but `SendAudio()` path needs Opus encoding
- `RTCPeerConnection.SendAudio()` expects encoded samples — needs proper duration/timestamp handling
- Google may stop sending audio if it doesn't receive any RTP from us (no keepalive)

## Coding Conventions

- .NET 10, C# 13, nullable enabled, TreatWarningsAsErrors
- xUnit + FluentAssertions for testing
- Microsoft.Extensions.Logging (console + file) — softphone logs to `D:/prj/GVResearch/logs/softphone.log`
- No sensitive data in source (tokens encrypted at rest, API key via env var, .gitignore covers captures/logs)
- Rate limits: 10 req/min, 100 req/day per endpoint (enforced by `GvRateLimiter`)
- `--allow-destructive` flag required for delete operations

## Project Structure

```
src/
  GvResearch.Api/          — ASP.NET REST facade (thin host over IGvClient)
    Endpoints/             — AccountEndpoints, ThreadEndpoints, SmsEndpoints, CallEndpoints
    Auth/                  — BearerSchemeHandler (placeholder, Phase 2)
  GvResearch.Shared/       — Core SDK: IGvClient, auth, protocol, services, models
    Auth/                  — IGvAuthService, GvAuthService, GvCookieSet, TokenEncryption, CookieRetriever
    Exceptions/            — GvApiException, GvAuthException, GvRateLimitException
    Http/                  — GvHttpClientHandler (SAPISIDHASH injection)
    Models/                — Domain records (GvAccount, GvThread, GvMessage, etc.)
    Protocol/              — GvProtobufJsonParser, GvRequestBuilder
    RateLimiting/          — GvRateLimiter (per-endpoint dual-window)
    Services/              — IGvClient, GvClient, sub-client implementations
    Signaler/              — IGvSignalerClient, GvSignalerClient, SignalerEvent types, SignalerMessageParser
    Transport/             — ICallTransport, TransportCallResult, TransportCallStatus
  GvResearch.Client.Cli/   — CLI tool for headless GV operations (future)
  GvResearch.Sip/          — SIP gateway (SIP-over-WebSocket + DTLS-SRTP media)
    Transport/             — SipWssCallTransport, GvSipWebSocketChannel, GvSipCredentialProvider, GvDtlsSrtpClient
  GvResearch.Softphone/    — Softphone UI (Avalonia) + audio engine (NAudio + Opus)
    Audio/                 — AudioEngine (mic capture + speaker playback at 48kHz)
    Phone/                 — GvPhoneClient (call orchestrator)
docs/
  api-research/            — API reference and integration guides (READ FIRST)
  superpowers/plans/       — Implementation plans
  superpowers/specs/       — Design specs
captures/
  iaet-exports/            — Raw .iaet.json capture files from IAET investigation
```
