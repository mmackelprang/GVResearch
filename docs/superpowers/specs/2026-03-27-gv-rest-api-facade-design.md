# GV REST API Facade — Design Spec

**Date:** 2026-03-27
**Status:** Approved
**Approach:** SDK-First with Optional REST (Approach B)

## Overview

Build an SDK-first Google Voice client (`IGvClient`) in `GvResearch.Shared` that encapsulates all GV API research (authentication, protobuf-JSON protocol, 14 documented endpoints). The REST API in `GvResearch.Api` is a thin host on top. In-process consumers (CLI, Softphone, SIP) use the SDK directly; external/non-.NET consumers hit the REST API over HTTP.

### Goals

- Single source of truth for GV protocol knowledge (auth, request/response format, rate limiting)
- All consumers in the repo use `IGvClient` — no direct GV API calls elsewhere
- Clean boundaries so the SDK can be extracted to a NuGet package later
- Transport-agnostic call support (SIP first, WebRTC/Callback pluggable later)

### Scope — Initial Build

**Tier 1 (in scope):**
- Account — `account/get`
- Threads — `api2thread/list`, `api2thread/get`, `thread/batchupdateattributes`, `thread/batchdelete`, `thread/markallread`, `threadinginfo/get`
- SMS — `api2thread/sendsms`
- Search — `api2thread/search`
- Voice calls — transport-agnostic with SIP as initial transport

**Deferred:**
- Real-time push (Signaler long-poll)
- gRPC-Web services (contacts, ring groups)
- WebRTC call transport
- Number porting/transfer endpoints
- Account settings update
- Call forwarding rules

---

## Section 1: SDK Surface — `IGvClient`

Single entry point with domain-specific sub-clients:

```csharp
public interface IGvClient : IAsyncDisposable
{
    IGvAccountClient Account { get; }
    IGvThreadClient Threads { get; }
    IGvSmsClient Sms { get; }
    IGvCallClient Calls { get; }
}
```

### Sub-client interfaces

**Account** — maps to `account/get`:
```csharp
public interface IGvAccountClient
{
    Task<GvAccount> GetAsync(CancellationToken ct = default);
}
```

**Threads** — maps to `api2thread/list`, `api2thread/get`, `thread/batch*`, `thread/markallread`, `threadinginfo/get`, `api2thread/search`:
```csharp
public interface IGvThreadClient
{
    Task<GvThreadPage> ListAsync(GvThreadListOptions? options = null, CancellationToken ct = default);
    Task<GvThread> GetAsync(string threadId, CancellationToken ct = default);
    Task MarkReadAsync(IEnumerable<string> threadIds, CancellationToken ct = default);
    Task ArchiveAsync(IEnumerable<string> threadIds, CancellationToken ct = default);
    Task MarkSpamAsync(IEnumerable<string> threadIds, CancellationToken ct = default);
    Task DeleteAsync(IEnumerable<string> threadIds, CancellationToken ct = default);
    Task MarkAllReadAsync(CancellationToken ct = default);
    Task<GvUnreadCounts> GetUnreadCountsAsync(CancellationToken ct = default);
    Task<GvThreadPage> SearchAsync(string query, CancellationToken ct = default);
}
```

**SMS** — maps to `api2thread/sendsms`:
```csharp
public interface IGvSmsClient
{
    Task<GvSmsResult> SendAsync(string toNumber, string message, CancellationToken ct = default);
}
```

**Calls** — transport-agnostic:
```csharp
public interface IGvCallClient
{
    Task<GvCallResult> InitiateAsync(string toNumber, CancellationToken ct = default);
    Task<GvCallStatus> GetStatusAsync(string callId, CancellationToken ct = default);
    Task HangupAsync(string callId, CancellationToken ct = default);
}
```

### Design decisions

- **Flat return types** — no `Result<T>` wrappers. Failures throw typed exceptions (`GvApiException`, `GvAuthException`, `GvRateLimitException`).
- **Options pattern for complex queries** — `GvThreadListOptions` holds pagination cursor, thread type filter, max results.
- **`ICallTransport` behind `IGvCallClient`** — the call client delegates to a pluggable transport. Transport is injected via DI.

---

## Section 2: Authentication — `GvAuthService`

Replaces `EncryptedFileTokenService` + `GvHttpClientHandler` GAPS cookie approach with the real SAPISIDHASH protocol.

### Auth flow

```
GvAuthService.GetValidCookiesAsync()
+-- Cookies in memory + not expired? -> return cached
+-- Encrypted cookie file on disk?
|   +-- Decrypt, extract SAPISID
|   +-- Health check: POST threadinginfo/get
|   |   +-- 200 -> cache + return
|   |   +-- 401/403 -> TryRefreshSessionAsync()
|   |       +-- success -> cache + return
|   |       +-- fail -> LoginInteractiveAsync()
+-- No file -> LoginInteractiveAsync()
```

### Components

**`IGvAuthService`** — replaces `IGvTokenService`:
```csharp
public interface IGvAuthService
{
    Task<GvCookieSet> GetValidCookiesAsync(CancellationToken ct = default);
    Task LoginInteractiveAsync(CancellationToken ct = default);
    string ComputeSapisidHash(string sapisid, string origin);
}
```

**`GvCookieSet`** — holds required cookies: SAPISID, SID, HSID, SSID, APISID, \_\_Secure-1PSID, \_\_Secure-3PSID.

**Updated `GvHttpClientHandler`** — injects SAPISIDHASH + full cookie set + required headers:
```csharp
protected override async Task<HttpResponseMessage> SendAsync(...)
{
    var cookies = await _authService.GetValidCookiesAsync(ct);
    var hash = _authService.ComputeSapisidHash(cookies.Sapisid, "https://voice.google.com");

    request.Headers.Add("Authorization", $"SAPISIDHASH {hash}");
    request.Headers.Add("Cookie", cookies.ToCookieHeader());
    request.Headers.Add("X-Goog-AuthUser", "0");
    request.Headers.Add("Origin", "https://voice.google.com");
    request.Headers.Add("Referer", "https://voice.google.com/");
}
```

### What stays
- `TokenEncryption.cs` (AES-256) — encrypts/decrypts the full cookie set instead of a single token
- `GvRateLimiter` — unchanged, enforced per-endpoint in each service

### What changes
- `EncryptedFileTokenService` -> absorbed into `GvAuthService`
- `IGvTokenService` -> replaced by `IGvAuthService`
- `GvHttpClientHandler` -> rewritten for SAPISIDHASH + full cookie set

---

## Section 3: Protobuf-JSON Protocol Layer

GV returns nested arrays (positional by .proto schema), not JSON objects. Two static classes handle translation.

### Response parsing

```csharp
public static class GvProtobufJsonParser
{
    public static GvAccount ParseAccount(JsonElement root);
    public static GvThreadPage ParseThreadList(JsonElement root);
    public static GvThread ParseThread(JsonElement root);
    public static GvUnreadCounts ParseUnreadCounts(JsonElement root);
    public static GvSmsResult ParseSendSms(JsonElement root);
}
```

### Request building

```csharp
public static class GvRequestBuilder
{
    public static string BuildAccountGetRequest();
    public static string BuildThreadListRequest(GvThreadListOptions? options);
    public static string BuildThreadGetRequest(string threadId);
    public static string BuildSendSmsRequest(string toNumber, string message);
    public static string BuildSearchRequest(string query);
    public static string BuildBatchUpdateRequest(IEnumerable<string> threadIds, string action);
    public static string BuildBatchDeleteRequest(IEnumerable<string> threadIds);
    public static string BuildMarkAllReadRequest();
    public static string BuildThreadingInfoRequest();
    public static string BuildSipRegisterInfoRequest();
}
```

### Why two static classes instead of a generic serializer
- Each endpoint has a different positional schema — no single proto definition to deserialize against
- Explicit per-type parsing is easier to test, debug, and update when GV changes field positions
- The captured `.iaet.json` files provide concrete examples for validation

### File locations
- `GvResearch.Shared/Protocol/GvProtobufJsonParser.cs`
- `GvResearch.Shared/Protocol/GvRequestBuilder.cs`

---

## Section 4: Call Transport Abstraction

### Interface

```csharp
public interface ICallTransport : IAsyncDisposable
{
    Task<TransportCallResult> InitiateAsync(string toNumber, CancellationToken ct = default);
    Task<TransportCallStatus> GetStatusAsync(string callId, CancellationToken ct = default);
    Task HangupAsync(string callId, CancellationToken ct = default);
}
```

### Implementations

```
ICallTransport
+-- SipCallTransport        <-- initial (uses GvResearch.Sip + sipregisterinfo/get)
+-- WebRtcCallTransport     <-- future
+-- CallbackCallTransport   <-- future (ring linked phone)
```

### SipCallTransport flow
1. Calls `sipregisterinfo/get` via the SDK to get SIP credentials
2. Registers with GV's SIP server using SIPSorcery
3. Places/manages calls through standard SIP INVITE/BYE
4. Maps SIP call state to `TransportCallStatus`

### GvCallClient usage

```csharp
public class GvCallClient : IGvCallClient
{
    private readonly ICallTransport _transport;
    private readonly GvRateLimiter _rateLimiter;

    public async Task<GvCallResult> InitiateAsync(string toNumber, CancellationToken ct)
    {
        if (!await _rateLimiter.TryAcquireAsync("calls/initiate", ct))
            throw new GvRateLimitException("calls/initiate");

        var result = await _transport.InitiateAsync(toNumber, ct);
        return new GvCallResult(result.CallId, true, null);
    }
}
```

### DI registration

```csharp
services.AddGvClient(options => {
    options.CallTransport = CallTransportType.Sip;  // default
});
```

Transport is selected at startup via configuration. One active at a time.

---

## Section 5: REST API Endpoints

Thin host that maps HTTP routes to `IGvClient` methods.

### Routes

```
/api/v1/account
  GET  /                    -> Account.GetAsync()

/api/v1/threads
  GET  /                    -> Threads.ListAsync(options)     [?cursor=&type=&maxResults=]
  GET  /{threadId}          -> Threads.GetAsync(threadId)
  GET  /unread              -> Threads.GetUnreadCountsAsync()
  GET  /search?q=           -> Threads.SearchAsync(query)
  POST /markread            -> Threads.MarkReadAsync(ids)
  POST /archive             -> Threads.ArchiveAsync(ids)
  POST /spam                -> Threads.MarkSpamAsync(ids)
  POST /markallread         -> Threads.MarkAllReadAsync()
  DELETE /                  -> Threads.DeleteAsync(ids)       [requires --allow-destructive]

/api/v1/sms
  POST /                    -> Sms.SendAsync(to, message)

/api/v1/calls
  POST /                    -> Calls.InitiateAsync(toNumber)
  GET  /{callId}/status     -> Calls.GetStatusAsync(callId)
  POST /{callId}/hangup     -> Calls.HangupAsync(callId)
```

### Endpoint implementation pattern

Each group is a static class with `Map*Endpoints()` extension method. Every endpoint:
- Validates input (returns 400 on bad request)
- Catches `GvAuthException` -> 401
- Catches `GvRateLimitException` -> 429
- Catches `GvApiException` -> 502
- Returns standard JSON responses (not protobuf-JSON)

### Consumer integration

| Consumer | Integration | Notes |
|----------|-------------|-------|
| CLI (`GvResearch.Client.Cli`) | Direct `IGvClient` via DI | In-process, no HTTP hop |
| Softphone (`GvResearch.Softphone`) | Direct `IGvClient` via DI | In-process, no HTTP hop |
| SIP gateway (`GvResearch.Sip`) | Direct `IGvClient.Calls` | Uses `SipCallTransport` |
| External / non-.NET | HTTP to REST API | Standard REST client |

### DI registration (one-liner for all consumers)

```csharp
services.AddGvClient(options => {
    options.CookiePath = "/path/to/encrypted/cookies";
    options.KeyPath = "/path/to/key";
    options.CallTransport = CallTransportType.Sip;
});
```

Registers `GvAuthService`, `GvHttpClientHandler`, all sub-clients, rate limiter, and selected call transport.

---

## Section 6: Domain Models

### Core models

```csharp
// Account
public sealed record GvAccount(
    IReadOnlyList<GvPhoneNumber> PhoneNumbers,
    IReadOnlyList<GvDevice> Devices,
    GvSettings Settings);

public sealed record GvPhoneNumber(string Number, PhoneNumberType Type, bool IsPrimary);
public sealed record GvDevice(string DeviceId, string Name, DeviceType Type);
public sealed record GvSettings(bool DoNotDisturb, string? VoicemailGreetingUrl);

// Threads
public sealed record GvThread(
    string Id,
    GvThreadType Type,
    IReadOnlyList<GvMessage> Messages,
    IReadOnlyList<string> Participants,
    DateTimeOffset Timestamp,
    bool IsRead);

public sealed record GvMessage(
    string Id,
    string? Text,
    string? SenderNumber,
    DateTimeOffset Timestamp,
    GvMessageType Type);

public sealed record GvThreadPage(
    IReadOnlyList<GvThread> Threads,
    string? NextCursor,
    int TotalCount);

public sealed record GvThreadListOptions(
    string? Cursor = null,
    GvThreadType? Type = null,
    int MaxResults = 50);

public sealed record GvUnreadCounts(
    int Sms, int Voicemail, int Missed, int Total);

// SMS
public sealed record GvSmsResult(string ThreadId, bool Success);

// Calls
public sealed record GvCallResult(string CallId, bool Success, string? ErrorMessage);
public sealed record GvCallStatus(string CallId, CallStatusType Status, DateTimeOffset RetrievedAt);

// Enums
public enum GvThreadType { All, Sms, Calls, Voicemail, Missed }
public enum GvMessageType { Sms, Voicemail, MissedCall, RecordedCall }
public enum CallStatusType { Unknown, Ringing, Active, Completed, Failed }
public enum PhoneNumberType { Mobile, Landline, GoogleVoice }
public enum DeviceType { Phone, Web, Unknown }
```

### Existing model disposition

| Existing File | Action | Reason |
|---------------|--------|--------|
| `CallRecord.cs` (API) | Delete | API maps from SDK models directly |
| `InitiateCallRequest` (API) | Delete | Replaced by request DTOs |
| `PagedResult<T>` (API) | Keep | Used for REST responses |
| `GvCallResult.cs` (Shared) | Rewrite | Simplified |
| `GvCallStatus.cs` (Shared) | Rewrite | Simplified |
| `GvCallEvent.cs` (Shared) | Delete | Not needed without event streaming |

### Error types

```csharp
public class GvApiException : Exception           // GV returned non-200
public class GvAuthException : GvApiException      // 401/403
public class GvRateLimitException : GvApiException // local rate limit exceeded
```

---

## Section 7: Testing Strategy

### Unit tests (`GvResearch.Shared.Tests`)

**Protocol parsing** (highest value):
- `GvProtobufJsonParserTests` — one test per parse method using real response samples from `.iaet.json` captures as test fixtures
- `GvRequestBuilderTests` — verify request bodies match expected protobuf-JSON format

**Auth:**
- `GvAuthServiceTests` — SAPISIDHASH computation against known inputs, cookie caching, health check flow (mock HTTP)
- `GvHttpClientHandlerTests` — verify correct headers injected

**Services:**
- Each sub-client gets tests: mock `HttpClient`, verify correct endpoint called, parser invoked, rate limiter checked
- `GvCallClientTests` — verify transport delegation, rate limiting

**Rate limiter:**
- Existing tests are solid — keep as-is

### Integration tests (`GvResearch.Api.Tests`)

- Expand existing `WebApplicationFactory` pattern
- Mock `IGvClient` (not individual services) — test HTTP routes map correctly to SDK methods
- Test error mapping: `GvAuthException` -> 401, `GvRateLimitException` -> 429, `GvApiException` -> 502
- Test input validation: bad phone numbers, missing fields -> 400

### What we do NOT automate
- No live GV API calls in automated tests (fragile, rate-limited, requires auth)
- No Playwright login flow in tests
- No SIP transport tests (needs network)

### Test data
- Extract representative responses from `.iaet.json` capture files into `tests/GvResearch.Shared.Tests/Fixtures/`
- One fixture per endpoint (e.g., `thread-list-response.json`, `account-get-response.json`)

### Human UAT Testing

Manual testing checklist for validating the system against the live Google Voice API. Requires a real Google account with Google Voice enabled.

**Prerequisites:**
- Encrypted cookie file populated via `LoginInteractiveAsync()` (one-time Playwright login)
- A second phone number to send/receive SMS and test calls
- Access to the Google Voice web UI for cross-referencing results

**UAT Test Cases:**

1. **Authentication flow:**
   - [ ] First-time login: `LoginInteractiveAsync()` opens browser, user authenticates, cookies encrypted to disk
   - [ ] Subsequent startup: cookies loaded from disk, health check passes, no browser needed
   - [ ] Cookie expiry: after expiry, `TryRefreshSessionAsync()` extends session without browser
   - [ ] Full re-auth: if refresh fails, `LoginInteractiveAsync()` triggered automatically

2. **Account:**
   - [ ] `GET /api/v1/account` returns correct phone numbers, devices, and settings
   - [ ] Cross-reference with GV web UI settings page

3. **Thread listing:**
   - [ ] `GET /api/v1/threads` returns threads matching GV inbox
   - [ ] Pagination: follow `nextCursor` through multiple pages
   - [ ] Type filter: `?type=sms`, `?type=voicemail`, `?type=calls` return correct subsets
   - [ ] Unread counts: `GET /api/v1/threads/unread` matches GV badge counts

4. **Thread operations:**
   - [ ] `GET /api/v1/threads/{id}` returns full message history
   - [ ] `POST /api/v1/threads/markread` marks threads as read (verify in GV web UI)
   - [ ] `POST /api/v1/threads/archive` archives threads (verify in GV web UI)
   - [ ] `DELETE /api/v1/threads` deletes threads (verify in GV web UI)
   - [ ] `POST /api/v1/threads/markallread` clears all unread indicators

5. **SMS:**
   - [ ] `POST /api/v1/sms` sends SMS to test number
   - [ ] Verify message received on test phone
   - [ ] Reply from test phone, verify it appears in thread listing
   - [ ] Send to invalid number — verify 400 or appropriate error

6. **Search:**
   - [ ] `GET /api/v1/threads/search?q=<keyword>` returns matching threads
   - [ ] Search for known message content — verify correct thread returned

7. **Voice calls (SIP transport):**
   - [ ] `POST /api/v1/calls` initiates call to test number
   - [ ] Test phone rings, caller ID shows GV number
   - [ ] `GET /api/v1/calls/{id}/status` reflects call state transitions (ringing -> active -> completed)
   - [ ] `POST /api/v1/calls/{id}/hangup` terminates active call
   - [ ] Call to invalid number — verify appropriate error

8. **Rate limiting:**
   - [ ] Rapid-fire requests to a single endpoint — verify 429 returned after limit exceeded
   - [ ] Verify rate limit resets after window expires

9. **Error handling:**
   - [ ] Revoke cookies manually — verify 401 returned, re-auth triggered
   - [ ] Call with malformed request body — verify 400 with descriptive error
   - [ ] Simulate GV downtime (block network) — verify 502 returned

**UAT sign-off:**
- All test cases pass
- No data loss or corruption observed
- Performance acceptable (thread listing < 3s, SMS send < 2s, call setup < 5s)
- Error messages are clear and actionable

---

## Section 8: Project Structure

### New/modified files in `GvResearch.Shared`

```
src/GvResearch.Shared/
+-- GvClient.cs                          # IGvClient implementation
+-- GvClientOptions.cs                   # Configuration for AddGvClient()
+-- GvClientServiceExtensions.cs         # services.AddGvClient() DI registration
+-- Auth/
|   +-- IGvAuthService.cs                # replaces IGvTokenService
|   +-- GvAuthService.cs                 # SAPISIDHASH, cookie management, health check
|   +-- GvCookieSet.cs                   # cookie container
|   +-- TokenEncryption.cs               # kept as-is (AES-256)
|   +-- EncryptedFileTokenService.cs     # DELETE
|   +-- IGvTokenService.cs               # DELETE
+-- Http/
|   +-- GvHttpClientHandler.cs           # REWRITE
+-- Protocol/
|   +-- GvProtobufJsonParser.cs          # response parsing
|   +-- GvRequestBuilder.cs              # request body construction
+-- Services/
|   +-- GvAccountClient.cs               # IGvAccountClient
|   +-- GvThreadClient.cs                # IGvThreadClient
|   +-- GvSmsClient.cs                   # IGvSmsClient
|   +-- GvCallClient.cs                  # IGvCallClient (replaces GvCallService)
|   +-- IGvCallService.cs                # DELETE
|   +-- GvCallService.cs                 # DELETE
+-- Transport/
|   +-- ICallTransport.cs                # transport abstraction
|   +-- TransportCallResult.cs           # transport-level result
|   +-- TransportCallStatus.cs           # transport-level status
+-- Models/
|   +-- GvAccount.cs
|   +-- GvThread.cs
|   +-- GvSmsResult.cs
|   +-- GvCallResult.cs                  # REWRITE
|   +-- GvCallStatus.cs                  # REWRITE
|   +-- GvCallEvent.cs                   # DELETE
|   +-- GvUnreadCounts.cs
|   +-- GvThreadListOptions.cs
|   +-- Enums.cs
+-- Exceptions/
|   +-- GvApiException.cs
|   +-- GvAuthException.cs
|   +-- GvRateLimitException.cs
+-- RateLimiting/
    +-- GvRateLimiter.cs                 # kept as-is
```

### Changes in `GvResearch.Api`

```
src/GvResearch.Api/
+-- Program.cs                           # UPDATE — register IGvClient
+-- Endpoints/
|   +-- AccountEndpoints.cs              # NEW
|   +-- ThreadEndpoints.cs               # NEW
|   +-- SmsEndpoints.cs                  # NEW
|   +-- CallEndpoints.cs                 # REWRITE
+-- Models/
|   +-- CallRecord.cs                    # DELETE
|   +-- PagedResult.cs                   # KEEP
+-- Auth/
    +-- BearerSchemeHandler.cs           # KEEP (placeholder for Phase 2)
```

### Changes in consumers

```
src/GvResearch.Client.Cli/               # UPDATE — use IGvClient directly
src/GvResearch.Softphone/                # UPDATE — use IGvClient directly
src/GvResearch.Sip/
    +-- SipCallTransport.cs              # NEW — ICallTransport implementation
```

### Test changes

```
tests/GvResearch.Shared.Tests/
+-- Fixtures/                            # NEW — JSON samples from .iaet.json
+-- Protocol/
|   +-- GvProtobufJsonParserTests.cs     # NEW
|   +-- GvRequestBuilderTests.cs         # NEW
+-- Auth/
|   +-- GvAuthServiceTests.cs            # NEW
+-- Services/
|   +-- GvAccountClientTests.cs          # NEW
|   +-- GvThreadClientTests.cs           # NEW
|   +-- GvSmsClientTests.cs              # NEW
|   +-- GvCallClientTests.cs             # REWRITE
+-- Http/
    +-- GvHttpClientHandlerTests.cs      # REWRITE

tests/GvResearch.Api.Tests/
    +-- *EndpointsTests.cs               # REWRITE — mock IGvClient
```

### File deletions summary

| File | Reason |
|------|--------|
| `IGvTokenService.cs` | Replaced by `IGvAuthService` |
| `EncryptedFileTokenService.cs` | Absorbed into `GvAuthService` |
| `IGvCallService.cs` | Replaced by `IGvCallClient` |
| `GvCallService.cs` | Replaced by `GvCallClient` |
| `GvCallEvent.cs` | Not needed without event streaming |
| `CallRecord.cs` (API) | API maps from SDK models |

### Documentation updates

- Update `CLAUDE.md` to reflect new structure
- Human UAT testing section included in this spec (Section 7)
- Update `docs/api-research/` if findings change during implementation
