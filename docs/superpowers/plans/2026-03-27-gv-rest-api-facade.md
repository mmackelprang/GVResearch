# GV REST API Facade Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build an SDK-first Google Voice client (`IGvClient`) with sub-clients for Account, Threads, SMS, and Calls, a protobuf-JSON protocol layer, SAPISIDHASH authentication, and a thin REST API host — then update all consumers to use the SDK.

**Architecture:** `IGvClient` in `GvResearch.Shared` encapsulates all GV protocol knowledge. In-process consumers (SIP, Softphone) use it directly via DI. The REST API in `GvResearch.Api` is a thin host that maps HTTP routes to `IGvClient` methods. Voice calls are transport-agnostic via `ICallTransport` (SIP first).

**Tech Stack:** .NET 10, C# 13, xUnit + FluentAssertions + NSubstitute, Serilog, System.Text.Json, SIPSorcery

**Spec:** `docs/superpowers/specs/2026-03-27-gv-rest-api-facade-design.md`

---

## File Map

### New files (GvResearch.Shared)
| File | Responsibility |
|------|---------------|
| `src/GvResearch.Shared/Models/Enums.cs` | All shared enums (GvThreadType, GvMessageType, CallStatusType, PhoneNumberType, DeviceType) |
| `src/GvResearch.Shared/Models/GvAccount.cs` | GvAccount, GvPhoneNumber, GvDevice, GvSettings records |
| `src/GvResearch.Shared/Models/GvThread.cs` | GvThread, GvMessage, GvThreadPage, GvThreadListOptions records |
| `src/GvResearch.Shared/Models/GvUnreadCounts.cs` | GvUnreadCounts record |
| `src/GvResearch.Shared/Models/GvSmsResult.cs` | GvSmsResult record |
| `src/GvResearch.Shared/Exceptions/GvApiException.cs` | Base exception for GV API errors |
| `src/GvResearch.Shared/Exceptions/GvAuthException.cs` | Auth-specific (401/403) exception |
| `src/GvResearch.Shared/Exceptions/GvRateLimitException.cs` | Rate limit exceeded exception |
| `src/GvResearch.Shared/Auth/IGvAuthService.cs` | Interface replacing IGvTokenService |
| `src/GvResearch.Shared/Auth/GvCookieSet.cs` | Cookie container with ToCookieHeader() |
| `src/GvResearch.Shared/Auth/GvAuthService.cs` | SAPISIDHASH computation, cookie management, health check, interactive login |
| `src/GvResearch.Shared/Protocol/GvProtobufJsonParser.cs` | Static parser: positional arrays -> domain models |
| `src/GvResearch.Shared/Protocol/GvRequestBuilder.cs` | Static builder: domain inputs -> protobuf-JSON request bodies |
| `src/GvResearch.Shared/Services/IGvClient.cs` | IGvClient + IGvAccountClient + IGvThreadClient + IGvSmsClient + IGvCallClient interfaces |
| `src/GvResearch.Shared/Services/GvAccountClient.cs` | Account sub-client implementation |
| `src/GvResearch.Shared/Services/GvThreadClient.cs` | Thread sub-client implementation |
| `src/GvResearch.Shared/Services/GvSmsClient.cs` | SMS sub-client implementation |
| `src/GvResearch.Shared/Services/GvCallClient.cs` | Call sub-client (delegates to ICallTransport) |
| `src/GvResearch.Shared/Services/GvClient.cs` | IGvClient implementation (composes sub-clients) |
| `src/GvResearch.Shared/Transport/ICallTransport.cs` | Transport abstraction + TransportCallResult + TransportCallStatus |
| `src/GvResearch.Shared/GvClientOptions.cs` | Options for AddGvClient() |
| `src/GvResearch.Shared/GvClientServiceExtensions.cs` | services.AddGvClient() DI registration |

### New files (GvResearch.Api)
| File | Responsibility |
|------|---------------|
| `src/GvResearch.Api/Endpoints/AccountEndpoints.cs` | GET /api/v1/account |
| `src/GvResearch.Api/Endpoints/ThreadEndpoints.cs` | GET/POST/DELETE /api/v1/threads/* |
| `src/GvResearch.Api/Endpoints/SmsEndpoints.cs` | POST /api/v1/sms |

### New files (GvResearch.Sip)

> **Note:** `SipCallTransport.cs` is deferred to a follow-up plan. This plan uses `NullCallTransport` as a placeholder.
> The SIP transport requires deeper integration with SIPSorcery (SIP INVITE/BYE, credential fetch via `sipregisterinfo/get`, call state mapping) and deserves its own focused plan.

### New test files
| File | Responsibility |
|------|---------------|
| `tests/GvResearch.Shared.Tests/Protocol/GvProtobufJsonParserTests.cs` | Parser unit tests with fixture data |
| `tests/GvResearch.Shared.Tests/Protocol/GvRequestBuilderTests.cs` | Request builder unit tests |
| `tests/GvResearch.Shared.Tests/Auth/GvAuthServiceTests.cs` | SAPISIDHASH + cookie flow tests |
| `tests/GvResearch.Shared.Tests/Http/GvHttpClientHandlerTests.cs` | Header injection tests |
| `tests/GvResearch.Shared.Tests/Services/GvAccountClientTests.cs` | Account client tests |
| `tests/GvResearch.Shared.Tests/Services/GvThreadClientTests.cs` | Thread client tests |
| `tests/GvResearch.Shared.Tests/Services/GvSmsClientTests.cs` | SMS client tests |
| `tests/GvResearch.Shared.Tests/Services/GvCallClientTests.cs` | Call client tests (rewrite) |
| `tests/GvResearch.Shared.Tests/Fixtures/*.json` | Response samples from .iaet.json captures |

### Modified files
| File | Change |
|------|--------|
| `src/GvResearch.Shared/Models/GvCallResult.cs` | Rewrite: simplified record (CallId, Success, ErrorMessage) |
| `src/GvResearch.Shared/Models/GvCallStatus.cs` | Rewrite: use CallStatusType enum from Enums.cs |
| `src/GvResearch.Shared/Http/GvHttpClientHandler.cs` | Rewrite: SAPISIDHASH + full cookie set + required headers |
| `src/GvResearch.Api/Endpoints/CallEndpoints.cs` | Rewrite: use IGvCallClient |
| `src/GvResearch.Api/Program.cs` | Update: register IGvClient via AddGvClient() |
| `src/GvResearch.Api/appsettings.json` | Add GvClient config section |
| `src/GvResearch.Shared/GvResearch.Shared.csproj` | Add Playwright dependency (for interactive login) |
| `src/GvResearch.Sip/Program.cs` | Update: use IGvClient instead of IGvCallService |
| `src/GvResearch.Sip/Calls/SipCallController.cs` | Update: use IGvCallClient instead of IGvCallService |
| `tests/GvResearch.Api.Tests/CallEndpointsTests.cs` | Rewrite: mock IGvClient |

### Consumer notes
- **`GvResearch.Client.Cli`** — Does not exist yet (empty directory). No changes needed.
- **`GvResearch.Softphone`** — Does not reference `GvResearch.Shared` (no `ProjectReference` in its csproj). Uses SIPSorcery directly for SIP calls. Integration with `IGvClient` is deferred to a follow-up plan when the Softphone needs SMS/thread features.

### Deleted files
| File | Reason |
|------|--------|
| `src/GvResearch.Shared/Auth/IGvTokenService.cs` | Replaced by IGvAuthService |
| `src/GvResearch.Shared/Auth/EncryptedFileTokenService.cs` | Absorbed into GvAuthService |
| `src/GvResearch.Shared/Services/IGvCallService.cs` | Replaced by IGvCallClient |
| `src/GvResearch.Shared/Services/GvCallService.cs` | Replaced by GvCallClient |
| `src/GvResearch.Shared/Models/GvCallEvent.cs` | Not needed without event streaming |
| `src/GvResearch.Api/Models/CallRecord.cs` | API maps from SDK models directly |

---

## Task 1: Domain Models & Enums

**Files:**
- Create: `src/GvResearch.Shared/Models/Enums.cs`
- Create: `src/GvResearch.Shared/Models/GvAccount.cs`
- Create: `src/GvResearch.Shared/Models/GvThread.cs`
- Create: `src/GvResearch.Shared/Models/GvUnreadCounts.cs`
- Create: `src/GvResearch.Shared/Models/GvSmsResult.cs`
- Modify: `src/GvResearch.Shared/Models/GvCallResult.cs`
- Modify: `src/GvResearch.Shared/Models/GvCallStatus.cs`
- Delete: `src/GvResearch.Shared/Models/GvCallEvent.cs`

- [ ] **Step 1: Create `Enums.cs`**

```csharp
namespace GvResearch.Shared.Models;

public enum GvThreadType { All, Sms, Calls, Voicemail, Missed }
public enum GvMessageType { Sms, Voicemail, MissedCall, RecordedCall }
public enum CallStatusType { Unknown, Ringing, Active, Completed, Failed }
public enum PhoneNumberType { Mobile, Landline, GoogleVoice }
public enum DeviceType { Phone, Web, Unknown }
```

- [ ] **Step 2: Create `GvAccount.cs`**

```csharp
namespace GvResearch.Shared.Models;

public sealed record GvAccount(
    IReadOnlyList<GvPhoneNumber> PhoneNumbers,
    IReadOnlyList<GvDevice> Devices,
    GvSettings Settings);

public sealed record GvPhoneNumber(string Number, PhoneNumberType Type, bool IsPrimary);
public sealed record GvDevice(string DeviceId, string Name, DeviceType Type);
public sealed record GvSettings(bool DoNotDisturb, string? VoicemailGreetingUrl);
```

- [ ] **Step 3: Create `GvThread.cs`**

```csharp
namespace GvResearch.Shared.Models;

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
```

- [ ] **Step 4: Create `GvUnreadCounts.cs`**

```csharp
namespace GvResearch.Shared.Models;

public sealed record GvUnreadCounts(int Sms, int Voicemail, int Missed, int Total);
```

- [ ] **Step 5: Create `GvSmsResult.cs`**

```csharp
namespace GvResearch.Shared.Models;

public sealed record GvSmsResult(string ThreadId, bool Success);
```

- [ ] **Step 6: Rewrite `GvCallResult.cs`**

Replace the entire file content with:

```csharp
namespace GvResearch.Shared.Models;

public sealed record GvCallResult(string CallId, bool Success, string? ErrorMessage)
{
    public static GvCallResult Ok(string callId) => new(callId, Success: true, ErrorMessage: null);
    public static GvCallResult Fail(string errorMessage) => new(string.Empty, Success: false, errorMessage);
}
```

Note: The only change is renaming `GvCallId` to `CallId` for consistency with the spec. The factory methods stay.

- [ ] **Step 7: Rewrite `GvCallStatus.cs`**

Replace the entire file content with:

```csharp
namespace GvResearch.Shared.Models;

public sealed record GvCallStatus(string CallId, CallStatusType Status, DateTimeOffset RetrievedAt);
```

This removes the old `GvCallStatusType` enum (now `CallStatusType` in `Enums.cs`).

- [ ] **Step 8: Delete `GvCallEvent.cs`**

```bash
cd D:/prj/GVResearch && git rm src/GvResearch.Shared/Models/GvCallEvent.cs
```

- [ ] **Step 9: Verify build compiles**

```bash
cd D:/prj/GVResearch && dotnet build src/GvResearch.Shared/GvResearch.Shared.csproj
```

Expected: Build will fail because `GvCallService.cs` references `GvCallEvent`, `GvCallStatusType`, and `GvCallId`. These files are being deleted/replaced in later tasks. For now, ensure the new model files themselves have no syntax errors by checking for compilation errors specifically in the new files.

Note: Full solution build will pass after Task 6 (cleanup of old services).

- [ ] **Step 10: Commit**

```bash
cd D:/prj/GVResearch
git add src/GvResearch.Shared/Models/
git commit -m "feat(models): add domain models and enums for GV SDK

Add GvAccount, GvThread, GvMessage, GvUnreadCounts, GvSmsResult records.
Add shared enums (GvThreadType, GvMessageType, CallStatusType, etc.).
Rename GvCallId->CallId, GvCallStatusType->CallStatusType for consistency.
Delete GvCallEvent (not needed without event streaming)."
```

---

## Task 2: Exception Types

**Files:**
- Create: `src/GvResearch.Shared/Exceptions/GvApiException.cs`
- Create: `src/GvResearch.Shared/Exceptions/GvAuthException.cs`
- Create: `src/GvResearch.Shared/Exceptions/GvRateLimitException.cs`

- [ ] **Step 1: Create `GvApiException.cs`**

```csharp
namespace GvResearch.Shared.Exceptions;

public class GvApiException : Exception
{
    public int? StatusCode { get; }

    public GvApiException(string message, int? statusCode = null)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public GvApiException(string message, int? statusCode, Exception innerException)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }
}
```

- [ ] **Step 2: Create `GvAuthException.cs`**

```csharp
namespace GvResearch.Shared.Exceptions;

public class GvAuthException : GvApiException
{
    public GvAuthException(string message, int statusCode = 401)
        : base(message, statusCode)
    {
    }

    public GvAuthException(string message, int statusCode, Exception innerException)
        : base(message, statusCode, innerException)
    {
    }
}
```

- [ ] **Step 3: Create `GvRateLimitException.cs`**

```csharp
namespace GvResearch.Shared.Exceptions;

public class GvRateLimitException : GvApiException
{
    public string Endpoint { get; }

    public GvRateLimitException(string endpoint)
        : base($"Rate limit exceeded for endpoint: {endpoint}", statusCode: 429)
    {
        Endpoint = endpoint;
    }
}
```

- [ ] **Step 4: Commit**

```bash
cd D:/prj/GVResearch
git add src/GvResearch.Shared/Exceptions/
git commit -m "feat(exceptions): add GvApiException hierarchy

GvApiException (base), GvAuthException (401/403), GvRateLimitException (429)."
```

---

## Task 3: Authentication — GvCookieSet, IGvAuthService, GvAuthService

**Files:**
- Create: `src/GvResearch.Shared/Auth/GvCookieSet.cs`
- Create: `src/GvResearch.Shared/Auth/IGvAuthService.cs`
- Create: `src/GvResearch.Shared/Auth/GvAuthService.cs`
- Test: `tests/GvResearch.Shared.Tests/Auth/GvAuthServiceTests.cs`

- [ ] **Step 1: Create `GvCookieSet.cs`**

```csharp
using System.Text;
using System.Text.Json;

namespace GvResearch.Shared.Auth;

public sealed class GvCookieSet
{
    public required string Sapisid { get; init; }
    public required string Sid { get; init; }
    public required string Hsid { get; init; }
    public required string Ssid { get; init; }
    public required string Apisid { get; init; }
    public string? Secure1Psid { get; init; }
    public string? Secure3Psid { get; init; }

    public string ToCookieHeader()
    {
        var sb = new StringBuilder();
        sb.Append($"SAPISID={Sapisid}; SID={Sid}; HSID={Hsid}; SSID={Ssid}; APISID={Apisid}");
        if (Secure1Psid is not null)
            sb.Append($"; __Secure-1PSID={Secure1Psid}");
        if (Secure3Psid is not null)
            sb.Append($"; __Secure-3PSID={Secure3Psid}");
        return sb.ToString();
    }

    public string Serialize() => JsonSerializer.Serialize(this);

    public static GvCookieSet Deserialize(string json) =>
        JsonSerializer.Deserialize<GvCookieSet>(json)
        ?? throw new InvalidOperationException("Failed to deserialize cookie set.");
}
```

- [ ] **Step 2: Create `IGvAuthService.cs`**

```csharp
namespace GvResearch.Shared.Auth;

public interface IGvAuthService
{
    Task<GvCookieSet> GetValidCookiesAsync(CancellationToken ct = default);
    Task LoginInteractiveAsync(CancellationToken ct = default);
    string ComputeSapisidHash(string sapisid, string origin);
}
```

- [ ] **Step 3: Write failing tests for SAPISIDHASH computation**

```csharp
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using GvResearch.Shared.Auth;
using GvResearch.Shared.Authentication;
using NSubstitute;

namespace GvResearch.Shared.Tests.Auth;

public sealed class GvAuthServiceTests
{
    [Fact]
    public void ComputeSapisidHash_ReturnsCorrectFormat()
    {
        var cookiePath = Path.GetTempFileName();
        var keyPath = Path.GetTempFileName();
        try
        {
            // Create a valid encrypted cookie file
            var key = new byte[32];
            RandomNumberGenerator.Fill(key);
            var cookies = new GvCookieSet
            {
                Sapisid = "test-sapisid",
                Sid = "test-sid",
                Hsid = "test-hsid",
                Ssid = "test-ssid",
                Apisid = "test-apisid"
            };
            File.WriteAllBytes(keyPath, key);
            File.WriteAllBytes(cookiePath, TokenEncryption.Encrypt(cookies.Serialize(), key));

            var sut = new GvAuthService(cookiePath, keyPath);

            var result = sut.ComputeSapisidHash("test-sapisid", "https://voice.google.com");

            // Format: "SAPISIDHASH <timestamp>_<sha1hex>"
            result.Should().StartWith("SAPISIDHASH ");
            var parts = result["SAPISIDHASH ".Length..].Split('_');
            parts.Should().HaveCount(2);
            long.TryParse(parts[0], out _).Should().BeTrue("timestamp should be numeric");
            parts[1].Should().HaveLength(40, "SHA1 hex digest is 40 chars");
        }
        finally
        {
            File.Delete(cookiePath);
            File.Delete(keyPath);
        }
    }

    [Fact]
    public void ComputeSapisidHash_IsCorrectSha1()
    {
        var cookiePath = Path.GetTempFileName();
        var keyPath = Path.GetTempFileName();
        try
        {
            var key = new byte[32];
            RandomNumberGenerator.Fill(key);
            var cookies = new GvCookieSet
            {
                Sapisid = "abc123",
                Sid = "s",
                Hsid = "h",
                Ssid = "ss",
                Apisid = "a"
            };
            File.WriteAllBytes(keyPath, key);
            File.WriteAllBytes(cookiePath, TokenEncryption.Encrypt(cookies.Serialize(), key));

            var sut = new GvAuthService(cookiePath, keyPath);

            var result = sut.ComputeSapisidHash("abc123", "https://voice.google.com");

            // Extract timestamp and verify hash
            var payload = result["SAPISIDHASH ".Length..];
            var underscore = payload.IndexOf('_');
            var timestamp = payload[..underscore];
            var hash = payload[(underscore + 1)..];

            // Manually compute expected hash
            var input = $"{timestamp} abc123 https://voice.google.com";
            var expected = Convert.ToHexStringLower(
                SHA1.HashData(Encoding.UTF8.GetBytes(input)));

            hash.Should().Be(expected);
        }
        finally
        {
            File.Delete(cookiePath);
            File.Delete(keyPath);
        }
    }

    [Fact]
    public async Task GetValidCookiesAsync_LoadsFromEncryptedFile()
    {
        var cookiePath = Path.GetTempFileName();
        var keyPath = Path.GetTempFileName();
        try
        {
            var key = new byte[32];
            RandomNumberGenerator.Fill(key);
            var cookies = new GvCookieSet
            {
                Sapisid = "real-sapisid",
                Sid = "real-sid",
                Hsid = "real-hsid",
                Ssid = "real-ssid",
                Apisid = "real-apisid",
                Secure1Psid = "secure1",
                Secure3Psid = "secure3"
            };
            File.WriteAllBytes(keyPath, key);
            File.WriteAllBytes(cookiePath, TokenEncryption.Encrypt(cookies.Serialize(), key));

            var sut = new GvAuthService(cookiePath, keyPath);

            var result = await sut.GetValidCookiesAsync();

            result.Sapisid.Should().Be("real-sapisid");
            result.Sid.Should().Be("real-sid");
            result.Secure1Psid.Should().Be("secure1");
        }
        finally
        {
            File.Delete(cookiePath);
            File.Delete(keyPath);
        }
    }

    [Fact]
    public async Task GetValidCookiesAsync_CachesResult()
    {
        var cookiePath = Path.GetTempFileName();
        var keyPath = Path.GetTempFileName();
        try
        {
            var key = new byte[32];
            RandomNumberGenerator.Fill(key);
            var cookies = new GvCookieSet
            {
                Sapisid = "cached",
                Sid = "s",
                Hsid = "h",
                Ssid = "ss",
                Apisid = "a"
            };
            File.WriteAllBytes(keyPath, key);
            File.WriteAllBytes(cookiePath, TokenEncryption.Encrypt(cookies.Serialize(), key));

            var sut = new GvAuthService(cookiePath, keyPath);

            var first = await sut.GetValidCookiesAsync();
            var second = await sut.GetValidCookiesAsync();

            ReferenceEquals(first, second).Should().BeTrue("should return cached instance");
        }
        finally
        {
            File.Delete(cookiePath);
            File.Delete(keyPath);
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they fail**

```bash
cd D:/prj/GVResearch && dotnet test tests/GvResearch.Shared.Tests --filter "FullyQualifiedName~GvAuthServiceTests" --no-restore -v minimal
```

Expected: FAIL — `GvAuthService` class does not exist yet.

- [ ] **Step 5: Create `GvAuthService.cs`**

```csharp
using System.Security.Cryptography;
using System.Text;
using GvResearch.Shared.Authentication;
using GvResearch.Shared.Exceptions;

namespace GvResearch.Shared.Auth;

public sealed class GvAuthService : IGvAuthService, IDisposable
{
    private readonly string _cookiePath;
    private readonly string _keyPath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private GvCookieSet? _cachedCookies;
    private bool _disposed;

    public GvAuthService(string cookiePath, string keyPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cookiePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(keyPath);
        _cookiePath = cookiePath;
        _keyPath = keyPath;
    }

    public async Task<GvCookieSet> GetValidCookiesAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_cachedCookies is not null)
                return _cachedCookies;

            if (!File.Exists(_cookiePath) || !File.Exists(_keyPath))
                throw new GvAuthException("Cookie file not found. Run LoginInteractiveAsync() first.");

            var key = await File.ReadAllBytesAsync(_keyPath, ct).ConfigureAwait(false);
            var ciphertext = await File.ReadAllBytesAsync(_cookiePath, ct).ConfigureAwait(false);
            var json = TokenEncryption.Decrypt(ciphertext, key);
            _cachedCookies = GvCookieSet.Deserialize(json);
            return _cachedCookies;
        }
        finally
        {
            _lock.Release();
        }
    }

    public Task LoginInteractiveAsync(CancellationToken ct = default)
    {
        // Playwright-based interactive login — deferred to a follow-up plan.
        // For now, the user must manually populate the encrypted cookie file.
        throw new NotImplementedException(
            "Interactive login not yet implemented. " +
            "Manually populate the encrypted cookie file.");
    }

    // NOTE: TryRefreshSessionAsync (from the spec's auth flow diagram) is deferred.
    // The full health-check + refresh + re-login cascade requires an HttpClient
    // (for the threadinginfo/get health check) and Playwright (for interactive login).
    // This plan implements the core cookie-loading + SAPISIDHASH path.
    // The refresh/re-login cascade will be added in a follow-up plan alongside
    // LoginInteractiveAsync and the Playwright dependency.

    public string ComputeSapisidHash(string sapisid, string origin)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sapisid);
        ArgumentException.ThrowIfNullOrWhiteSpace(origin);

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var input = $"{timestamp} {sapisid} {origin}";
        var hash = Convert.ToHexStringLower(
            SHA1.HashData(Encoding.UTF8.GetBytes(input)));
        return $"SAPISIDHASH {timestamp}_{hash}";
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cachedCookies = null;
        _lock.Dispose();
    }
}
```

- [ ] **Step 6: Run tests to verify they pass**

```bash
cd D:/prj/GVResearch && dotnet test tests/GvResearch.Shared.Tests --filter "FullyQualifiedName~GvAuthServiceTests" --no-restore -v minimal
```

Expected: 4 tests pass.

- [ ] **Step 7: Commit**

```bash
cd D:/prj/GVResearch
git add src/GvResearch.Shared/Auth/GvCookieSet.cs src/GvResearch.Shared/Auth/IGvAuthService.cs src/GvResearch.Shared/Auth/GvAuthService.cs tests/GvResearch.Shared.Tests/Auth/GvAuthServiceTests.cs
git commit -m "feat(auth): add GvAuthService with SAPISIDHASH computation

Replace IGvTokenService with IGvAuthService. GvAuthService loads encrypted
cookies from disk, caches them, and computes SAPISIDHASH authorization headers.
Interactive login deferred to later task."
```

---

## Task 4: Rewrite GvHttpClientHandler

**Files:**
- Modify: `src/GvResearch.Shared/Http/GvHttpClientHandler.cs`
- Test: `tests/GvResearch.Shared.Tests/Http/GvHttpClientHandlerTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
using System.Net;
using FluentAssertions;
using GvResearch.Shared.Auth;
using GvResearch.Shared.Http;
using NSubstitute;

namespace GvResearch.Shared.Tests.Http;

public sealed class GvHttpClientHandlerTests : IDisposable
{
    private readonly IGvAuthService _authService = Substitute.For<IGvAuthService>();
    private readonly FakeInnerHandler _innerHandler = new();
    private readonly GvHttpClientHandler _sut;

    public GvHttpClientHandlerTests()
    {
        _authService.GetValidCookiesAsync(Arg.Any<CancellationToken>())
            .Returns(new GvCookieSet
            {
                Sapisid = "test-sapisid",
                Sid = "test-sid",
                Hsid = "test-hsid",
                Ssid = "test-ssid",
                Apisid = "test-apisid"
            });
        _authService.ComputeSapisidHash("test-sapisid", "https://voice.google.com")
            .Returns("SAPISIDHASH 12345_abc123");

        _sut = new GvHttpClientHandler(_authService, _innerHandler);
    }

    [Fact]
    public async Task SendAsync_InjectsAuthorizationHeader()
    {
        using var client = new HttpClient(_sut) { BaseAddress = new Uri("https://clients6.google.com") };

        await client.GetAsync(new Uri("/test", UriKind.Relative));

        _innerHandler.LastRequest!.Headers.GetValues("Authorization")
            .Should().ContainSingle()
            .Which.Should().Be("SAPISIDHASH 12345_abc123");
    }

    [Fact]
    public async Task SendAsync_InjectsCookieHeader()
    {
        using var client = new HttpClient(_sut) { BaseAddress = new Uri("https://clients6.google.com") };

        await client.GetAsync(new Uri("/test", UriKind.Relative));

        _innerHandler.LastRequest!.Headers.GetValues("Cookie")
            .Should().ContainSingle()
            .Which.Should().Contain("SAPISID=test-sapisid")
            .And.Contain("SID=test-sid");
    }

    [Fact]
    public async Task SendAsync_InjectsOriginAndReferer()
    {
        using var client = new HttpClient(_sut) { BaseAddress = new Uri("https://clients6.google.com") };

        await client.GetAsync(new Uri("/test", UriKind.Relative));

        _innerHandler.LastRequest!.Headers.GetValues("Origin")
            .Should().ContainSingle().Which.Should().Be("https://voice.google.com");
        _innerHandler.LastRequest!.Headers.GetValues("Referer")
            .Should().ContainSingle().Which.Should().Be("https://voice.google.com/");
    }

    [Fact]
    public async Task SendAsync_InjectsXGoogAuthUser()
    {
        using var client = new HttpClient(_sut) { BaseAddress = new Uri("https://clients6.google.com") };

        await client.GetAsync(new Uri("/test", UriKind.Relative));

        _innerHandler.LastRequest!.Headers.GetValues("X-Goog-AuthUser")
            .Should().ContainSingle().Which.Should().Be("0");
    }

    public void Dispose() => _sut.Dispose();
}

internal sealed class FakeInnerHandler : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
cd D:/prj/GVResearch && dotnet test tests/GvResearch.Shared.Tests --filter "FullyQualifiedName~GvHttpClientHandlerTests" --no-restore -v minimal
```

Expected: FAIL — current `GvHttpClientHandler` takes `IGvTokenService`, not `IGvAuthService`.

- [ ] **Step 3: Rewrite `GvHttpClientHandler.cs`**

Replace the entire file with:

```csharp
using GvResearch.Shared.Auth;

namespace GvResearch.Shared.Http;

public sealed class GvHttpClientHandler : DelegatingHandler
{
    private readonly IGvAuthService _authService;

    public GvHttpClientHandler(IGvAuthService authService, HttpMessageHandler innerHandler)
        : base(innerHandler)
    {
        ArgumentNullException.ThrowIfNull(authService);
        _authService = authService;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var cookies = await _authService.GetValidCookiesAsync(cancellationToken).ConfigureAwait(false);
        var hash = _authService.ComputeSapisidHash(cookies.Sapisid, "https://voice.google.com");

        request.Headers.Add("Authorization", hash);
        request.Headers.Add("Cookie", cookies.ToCookieHeader());
        request.Headers.Add("X-Goog-AuthUser", "0");
        request.Headers.Add("Origin", "https://voice.google.com");
        request.Headers.Add("Referer", "https://voice.google.com/");

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
cd D:/prj/GVResearch && dotnet test tests/GvResearch.Shared.Tests --filter "FullyQualifiedName~GvHttpClientHandlerTests" --no-restore -v minimal
```

Expected: 4 tests pass.

- [ ] **Step 5: Commit**

```bash
cd D:/prj/GVResearch
git add src/GvResearch.Shared/Http/GvHttpClientHandler.cs tests/GvResearch.Shared.Tests/Http/GvHttpClientHandlerTests.cs
git commit -m "feat(http): rewrite GvHttpClientHandler for SAPISIDHASH auth

Replace GAPS cookie injection with full SAPISIDHASH authorization header,
cookie set, Origin, Referer, and X-Goog-AuthUser headers."
```

---

## Task 5: Protocol Layer — GvRequestBuilder & GvProtobufJsonParser

**Files:**
- Create: `src/GvResearch.Shared/Protocol/GvRequestBuilder.cs`
- Create: `src/GvResearch.Shared/Protocol/GvProtobufJsonParser.cs`
- Test: `tests/GvResearch.Shared.Tests/Protocol/GvRequestBuilderTests.cs`
- Test: `tests/GvResearch.Shared.Tests/Protocol/GvProtobufJsonParserTests.cs`

- [ ] **Step 1: Write failing tests for GvRequestBuilder**

```csharp
using System.Text.Json;
using FluentAssertions;
using GvResearch.Shared.Models;
using GvResearch.Shared.Protocol;

namespace GvResearch.Shared.Tests.Protocol;

public sealed class GvRequestBuilderTests
{
    [Fact]
    public void BuildAccountGetRequest_ReturnsCorrectJson()
    {
        var result = GvRequestBuilder.BuildAccountGetRequest();
        result.Should().Be("[null,1]");
    }

    [Fact]
    public void BuildThreadListRequest_WithDefaults_ReturnsCorrectJson()
    {
        var result = GvRequestBuilder.BuildThreadListRequest(null);
        // Default: type=2 (all messages), pageSize=50, no cursor
        var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;
        root[0].GetInt32().Should().Be(2); // type code for All
        root[1].GetInt32().Should().Be(50); // default page size
    }

    [Fact]
    public void BuildThreadListRequest_WithSmsType_UsesTypeCode3()
    {
        var options = new GvThreadListOptions(Type: GvThreadType.Sms, MaxResults: 20);
        var result = GvRequestBuilder.BuildThreadListRequest(options);
        var doc = JsonDocument.Parse(result);
        doc.RootElement[0].GetInt32().Should().Be(3); // SMS = type code 3
        doc.RootElement[1].GetInt32().Should().Be(20);
    }

    [Fact]
    public void BuildThreadListRequest_WithCursor_IncludesCursor()
    {
        var options = new GvThreadListOptions(Cursor: "next-page-token");
        var result = GvRequestBuilder.BuildThreadListRequest(options);
        result.Should().Contain("next-page-token");
    }

    [Fact]
    public void BuildThreadGetRequest_ReturnsCorrectJson()
    {
        var result = GvRequestBuilder.BuildThreadGetRequest("t.+15551234567");
        var doc = JsonDocument.Parse(result);
        doc.RootElement[0].GetString().Should().Be("t.+15551234567");
    }

    [Fact]
    public void BuildSendSmsRequest_ReturnsCorrectJson()
    {
        var result = GvRequestBuilder.BuildSendSmsRequest("+15551234567", "Hello!");
        var doc = JsonDocument.Parse(result);
        doc.RootElement[4].GetString().Should().Be("Hello!");
        doc.RootElement[5].GetString().Should().Be("t.+15551234567");
    }

    [Fact]
    public void BuildSearchRequest_ReturnsCorrectJson()
    {
        var result = GvRequestBuilder.BuildSearchRequest("hello world");
        var doc = JsonDocument.Parse(result);
        doc.RootElement[0].GetString().Should().Be("hello world");
    }

    [Fact]
    public void BuildBatchDeleteRequest_ReturnsCorrectJson()
    {
        var result = GvRequestBuilder.BuildBatchDeleteRequest(["t.+1111", "t.+2222"]);
        var doc = JsonDocument.Parse(result);
        doc.RootElement[0][0][0].GetString().Should().Be("t.+1111");
        doc.RootElement[0][0][1].GetString().Should().Be("t.+2222");
    }

    [Fact]
    public void BuildMarkAllReadRequest_ReturnsEmptyArray()
    {
        var result = GvRequestBuilder.BuildMarkAllReadRequest();
        result.Should().Be("[]");
    }

    [Fact]
    public void BuildThreadingInfoRequest_ReturnsEmptyArray()
    {
        var result = GvRequestBuilder.BuildThreadingInfoRequest();
        result.Should().Be("[]");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
cd D:/prj/GVResearch && dotnet test tests/GvResearch.Shared.Tests --filter "FullyQualifiedName~GvRequestBuilderTests" --no-restore -v minimal
```

Expected: FAIL — `GvRequestBuilder` class does not exist.

- [ ] **Step 3: Create `GvRequestBuilder.cs`**

```csharp
using System.Text.Json;
using GvResearch.Shared.Models;

namespace GvResearch.Shared.Protocol;

public static class GvRequestBuilder
{
    private static int ThreadTypeToCode(GvThreadType? type) => type switch
    {
        GvThreadType.Calls => 1,
        GvThreadType.Sms => 3,
        GvThreadType.Voicemail => 4,
        GvThreadType.Missed => 1, // missed calls are in calls tab
        _ => 2, // All messages
    };

    public static string BuildAccountGetRequest() => "[null,1]";

    public static string BuildThreadListRequest(GvThreadListOptions? options)
    {
        var type = ThreadTypeToCode(options?.Type);
        var pageSize = options?.MaxResults ?? 50;
        var cursor = options?.Cursor;

        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);
        writer.WriteStartArray();
        writer.WriteNumberValue(type);
        writer.WriteNumberValue(pageSize);
        writer.WriteNullValue(); // flags
        if (cursor is not null)
            writer.WriteStringValue(cursor);
        else
            writer.WriteNullValue();
        writer.WriteNullValue(); // unknown
        // Options array: [null,1,1,1]
        writer.WriteStartArray();
        writer.WriteNullValue();
        writer.WriteNumberValue(1);
        writer.WriteNumberValue(1);
        writer.WriteNumberValue(1);
        writer.WriteEndArray();
        writer.WriteEndArray();
        writer.Flush();
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    public static string BuildThreadGetRequest(string threadId)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);
        writer.WriteStartArray();
        writer.WriteStringValue(threadId);
        writer.WriteNumberValue(50); // page size
        writer.WriteNullValue(); // cursor
        // Options: [null,1,1]
        writer.WriteStartArray();
        writer.WriteNullValue();
        writer.WriteNumberValue(1);
        writer.WriteNumberValue(1);
        writer.WriteEndArray();
        writer.WriteEndArray();
        writer.Flush();
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    public static string BuildSendSmsRequest(string toNumber, string message)
    {
        var threadId = $"t.{toNumber}";
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);
        writer.WriteStartArray();
        writer.WriteNullValue(); // 0
        writer.WriteNullValue(); // 1
        writer.WriteNullValue(); // 2
        writer.WriteNullValue(); // 3
        writer.WriteStringValue(message); // 4: message text
        writer.WriteStringValue(threadId); // 5: thread ID
        writer.WriteNullValue(); // 6
        writer.WriteNullValue(); // 7
        writer.WriteStartArray(); writer.WriteEndArray(); // 8: device IDs (empty = default)
        writer.WriteEndArray();
        writer.Flush();
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    public static string BuildSearchRequest(string query)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);
        writer.WriteStartArray();
        writer.WriteStringValue(query);
        writer.WriteNumberValue(50); // max results
        writer.WriteEndArray();
        writer.Flush();
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    public static string BuildBatchUpdateRequest(IEnumerable<string> threadIds, string action)
    {
        // action determines which attribute position gets set to 1
        // "read" = position 3, "archive" = position 5, "spam" = position 2
        int attr1 = 0, attr2 = 0, attr3 = 0, attr4 = 0, attr5 = 0;
        int mask1 = 0, mask2 = 0, mask3 = 0, mask4 = 0, mask5 = 0;

        switch (action)
        {
            case "read":
                attr3 = 1; mask3 = 1;
                break;
            case "archive":
                attr5 = 1; mask5 = 1;
                break;
            case "spam":
                attr2 = 1; mask2 = 1;
                break;
        }

        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);
        writer.WriteStartArray(); // outer
        writer.WriteStartArray(); // batch
        foreach (var threadId in threadIds)
        {
            writer.WriteStartArray(); // per-thread wrapper
            writer.WriteStartArray(); // attributes
            writer.WriteStringValue(threadId);
            writer.WriteNumberValue(attr1);
            writer.WriteNumberValue(attr2);
            writer.WriteNumberValue(attr3);
            writer.WriteNumberValue(attr4);
            writer.WriteNumberValue(attr5);
            writer.WriteEndArray();
            writer.WriteStartArray(); // masks
            writer.WriteNumberValue(mask1);
            writer.WriteNumberValue(mask2);
            writer.WriteNumberValue(mask3);
            writer.WriteNumberValue(mask4);
            writer.WriteNumberValue(mask5);
            writer.WriteEndArray();
            writer.WriteNumberValue(1); // flag
            writer.WriteEndArray();
        }
        writer.WriteEndArray();
        writer.WriteEndArray();
        writer.Flush();
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    public static string BuildBatchDeleteRequest(IEnumerable<string> threadIds)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);
        writer.WriteStartArray(); // outer
        writer.WriteStartArray(); // batch
        writer.WriteStartArray(); // thread IDs
        foreach (var id in threadIds)
            writer.WriteStringValue(id);
        writer.WriteEndArray();
        writer.WriteEndArray();
        writer.WriteEndArray();
        writer.Flush();
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    public static string BuildMarkAllReadRequest() => "[]";

    public static string BuildThreadingInfoRequest() => "[]";

    public static string BuildSipRegisterInfoRequest()
    {
        // [3,"<deviceId>"] — deviceId is populated at runtime
        return "[3,null]";
    }
}
```

- [ ] **Step 4: Run request builder tests**

```bash
cd D:/prj/GVResearch && dotnet test tests/GvResearch.Shared.Tests --filter "FullyQualifiedName~GvRequestBuilderTests" --no-restore -v minimal
```

Expected: All pass.

- [ ] **Step 5: Write failing tests for GvProtobufJsonParser**

```csharp
using System.Text.Json;
using FluentAssertions;
using GvResearch.Shared.Models;
using GvResearch.Shared.Protocol;

namespace GvResearch.Shared.Tests.Protocol;

public sealed class GvProtobufJsonParserTests
{
    [Fact]
    public void ParseAccount_ExtractsPrimaryPhoneNumber()
    {
        // Minimal account response: [0]=phone number
        var json = """["+19196706660",null,[],null,null,[]]""";
        var root = JsonDocument.Parse(json).RootElement;

        var account = GvProtobufJsonParser.ParseAccount(root);

        account.PhoneNumbers.Should().ContainSingle();
        account.PhoneNumbers[0].Number.Should().Be("+19196706660");
        account.PhoneNumbers[0].IsPrimary.Should().BeTrue();
    }

    [Fact]
    public void ParseThreadList_ExtractsThreads()
    {
        // Minimal thread list: array of thread objects
        // Each thread: [0]=threadId, [1]=readStatus, [2]=messages array
        var json = """[[[
            "t.+15551234567",
            1,
            [["msg1",1234567890000,"+15550000001",["+15551234567"],10,1,null,null,null,null,null,"Hello!",null,null,0,1]]
        ]]]""";
        var root = JsonDocument.Parse(json).RootElement;

        var page = GvProtobufJsonParser.ParseThreadList(root);

        page.Threads.Should().ContainSingle();
        page.Threads[0].Id.Should().Be("t.+15551234567");
        page.Threads[0].IsRead.Should().BeTrue();
        page.Threads[0].Messages.Should().ContainSingle();
        page.Threads[0].Messages[0].Text.Should().Be("Hello!");
    }

    [Fact]
    public void ParseThread_ExtractsMessages()
    {
        // Single thread response
        var json = """[
            "t.+15551234567",
            0,
            [
                ["msg1",1234567890000,"+15550000001",["+15551234567"],10,0,null,null,null,null,null,"First message",null,null,0,0],
                ["msg2",1234567891000,"+15551234567",["+15550000001"],11,1,null,null,null,null,null,"Reply",null,null,0,1]
            ]
        ]""";
        var root = JsonDocument.Parse(json).RootElement;

        var thread = GvProtobufJsonParser.ParseThread(root);

        thread.Id.Should().Be("t.+15551234567");
        thread.IsRead.Should().BeFalse();
        thread.Messages.Should().HaveCount(2);
        thread.Messages[0].Text.Should().Be("First message");
        thread.Messages[0].Type.Should().Be(GvMessageType.Sms);
        thread.Messages[1].Text.Should().Be("Reply");
    }

    [Fact]
    public void ParseUnreadCounts_ExtractsCounts()
    {
        // threadinginfo/get response
        var json = """[[[[1,null,5],[4,null,2],[3,null,10],[5,null,0],[6,null,1]]]]""";
        var root = JsonDocument.Parse(json).RootElement;

        var counts = GvProtobufJsonParser.ParseUnreadCounts(root);

        counts.Missed.Should().Be(5); // calls = position 0 (type 1)
        counts.Voicemail.Should().Be(2); // voicemail = position 1 (type 4)
        counts.Sms.Should().Be(10); // messages = position 2 (type 3)
    }

    [Fact]
    public void ParseSendSms_ExtractsResult()
    {
        var json = """[null,"t.+15551234567","msg-hash-123",1234567890000,[1,2]]""";
        var root = JsonDocument.Parse(json).RootElement;

        var result = GvProtobufJsonParser.ParseSendSms(root);

        result.ThreadId.Should().Be("t.+15551234567");
        result.Success.Should().BeTrue();
    }

    [Theory]
    [InlineData(10, GvMessageType.Sms)]         // received SMS
    [InlineData(11, GvMessageType.Sms)]         // sent SMS
    [InlineData(2, GvMessageType.Voicemail)]    // voicemail
    [InlineData(1, GvMessageType.RecordedCall)] // incoming call answered
    [InlineData(14, GvMessageType.RecordedCall)]// outgoing call
    public void ParseThread_MapsMessageTypeCorrectly(int typeCode, GvMessageType expected)
    {
        // Build a minimal thread with one message of the given type code
        var json = $$"""["t.+1",0,[["m1",1000,"+1",["+2"],{{typeCode}},0,null,null,null,null,null,"text",null,null,0,0]]]""";
        var root = JsonDocument.Parse(json).RootElement;

        var thread = GvProtobufJsonParser.ParseThread(root);

        thread.Messages[0].Type.Should().Be(expected);
    }
}
```

- [ ] **Step 6: Run parser tests to verify they fail**

```bash
cd D:/prj/GVResearch && dotnet test tests/GvResearch.Shared.Tests --filter "FullyQualifiedName~GvProtobufJsonParserTests" --no-restore -v minimal
```

Expected: FAIL — `GvProtobufJsonParser` does not exist.

- [ ] **Step 7: Create `GvProtobufJsonParser.cs`**

```csharp
using System.Text.Json;
using GvResearch.Shared.Models;

namespace GvResearch.Shared.Protocol;

public static class GvProtobufJsonParser
{
    internal static GvMessageType MapMessageType(int code) => code switch
    {
        1 => GvMessageType.RecordedCall,   // incoming call answered
        2 => GvMessageType.Voicemail,
        10 => GvMessageType.Sms,           // received SMS
        11 => GvMessageType.Sms,           // sent SMS
        14 => GvMessageType.RecordedCall,  // outgoing call
        _ => GvMessageType.MissedCall,
    };

    public static GvAccount ParseAccount(JsonElement root)
    {
        var phoneNumber = root[0].GetString() ?? string.Empty;
        var phones = new List<GvPhoneNumber>
        {
            new(phoneNumber, PhoneNumberType.GoogleVoice, IsPrimary: true)
        };

        var devices = new List<GvDevice>();
        if (root.GetArrayLength() > 5 && root[5].ValueKind == JsonValueKind.Array)
        {
            foreach (var dev in root[5].EnumerateArray())
            {
                var id = dev.GetArrayLength() > 0 ? dev[0].GetString() ?? "" : "";
                var name = dev.GetArrayLength() > 1 ? dev[1].GetString() ?? "" : "";
                devices.Add(new GvDevice(id, name, DeviceType.Unknown));
            }
        }

        var settings = new GvSettings(DoNotDisturb: false, VoicemailGreetingUrl: null);
        return new GvAccount(phones, devices, settings);
    }

    public static GvThreadPage ParseThreadList(JsonElement root)
    {
        var threads = new List<GvThread>();
        string? nextCursor = null;

        if (root.GetArrayLength() > 0 && root[0].ValueKind == JsonValueKind.Array)
        {
            var threadArray = root[0];
            if (threadArray.GetArrayLength() > 0 && threadArray[0].ValueKind == JsonValueKind.Array)
            {
                foreach (var threadEl in threadArray[0].EnumerateArray())
                {
                    threads.Add(ParseThread(threadEl));
                }
            }
        }

        // Cursor is typically in root[1] if present
        if (root.GetArrayLength() > 1 && root[1].ValueKind == JsonValueKind.String)
        {
            nextCursor = root[1].GetString();
        }

        return new GvThreadPage(threads, nextCursor, threads.Count);
    }

    public static GvThread ParseThread(JsonElement threadEl)
    {
        var id = threadEl[0].GetString() ?? string.Empty;
        var isRead = threadEl[1].ValueKind == JsonValueKind.Number && threadEl[1].GetInt32() == 1;

        var messages = new List<GvMessage>();
        var participants = new HashSet<string>(StringComparer.Ordinal);

        if (threadEl.GetArrayLength() > 2 && threadEl[2].ValueKind == JsonValueKind.Array)
        {
            foreach (var msgEl in threadEl[2].EnumerateArray())
            {
                var msg = ParseMessage(msgEl);
                messages.Add(msg);
                if (msg.SenderNumber is not null)
                    participants.Add(msg.SenderNumber);
            }
        }

        var threadType = InferThreadType(id, messages);
        var timestamp = messages.Count > 0
            ? messages[^1].Timestamp
            : DateTimeOffset.MinValue;

        return new GvThread(id, threadType, messages, participants.ToList(), timestamp, isRead);
    }

    public static GvUnreadCounts ParseUnreadCounts(JsonElement root)
    {
        // Response: [[[[type,null,count],[type,null,count],...]]]
        int sms = 0, voicemail = 0, missed = 0;

        if (root.GetArrayLength() > 0
            && root[0].ValueKind == JsonValueKind.Array
            && root[0].GetArrayLength() > 0
            && root[0][0].ValueKind == JsonValueKind.Array
            && root[0][0].GetArrayLength() > 0
            && root[0][0][0].ValueKind == JsonValueKind.Array)
        {
            var countsArray = root[0][0][0];
            foreach (var entry in countsArray.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Array || entry.GetArrayLength() < 3)
                    continue;
                var type = entry[0].GetInt32();
                var count = entry[2].GetInt32();
                switch (type)
                {
                    case 1: missed = count; break;   // Calls
                    case 3: sms = count; break;       // Messages
                    case 4: voicemail = count; break;  // Voicemail
                }
            }
        }

        return new GvUnreadCounts(sms, voicemail, missed, sms + voicemail + missed);
    }

    public static GvSmsResult ParseSendSms(JsonElement root)
    {
        // Response: [null,"<threadId>","<messageHash>",<timestamp>,[1,2]]
        var threadId = root.GetArrayLength() > 1
            ? root[1].GetString() ?? string.Empty
            : string.Empty;

        return new GvSmsResult(threadId, Success: !string.IsNullOrEmpty(threadId));
    }

    private static GvMessage ParseMessage(JsonElement msgEl)
    {
        var id = msgEl[0].GetString() ?? string.Empty;

        var timestampMs = msgEl.GetArrayLength() > 1 && msgEl[1].ValueKind == JsonValueKind.Number
            ? msgEl[1].GetInt64()
            : 0;
        var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(timestampMs);

        string? senderNumber = null;
        if (msgEl.GetArrayLength() > 3 && msgEl[3].ValueKind == JsonValueKind.Array && msgEl[3].GetArrayLength() > 0)
        {
            senderNumber = msgEl[3][0].GetString();
        }

        var typeCode = msgEl.GetArrayLength() > 4 && msgEl[4].ValueKind == JsonValueKind.Number
            ? msgEl[4].GetInt32()
            : 0;

        string? text = null;
        if (msgEl.GetArrayLength() > 11 && msgEl[11].ValueKind == JsonValueKind.String)
        {
            text = msgEl[11].GetString();
        }

        return new GvMessage(id, text, senderNumber, timestamp, MapMessageType(typeCode));
    }

    private static GvThreadType InferThreadType(string threadId, List<GvMessage> messages)
    {
        if (threadId.StartsWith("t.", StringComparison.Ordinal))
            return GvThreadType.Sms;
        if (threadId.StartsWith("c.", StringComparison.Ordinal))
            return GvThreadType.Calls;
        if (messages.Count > 0 && messages[0].Type == GvMessageType.Voicemail)
            return GvThreadType.Voicemail;
        return GvThreadType.All;
    }
}
```

- [ ] **Step 8: Run all protocol tests**

```bash
cd D:/prj/GVResearch && dotnet test tests/GvResearch.Shared.Tests --filter "FullyQualifiedName~Protocol" --no-restore -v minimal
```

Expected: All pass.

- [ ] **Step 9: Commit**

```bash
cd D:/prj/GVResearch
git add src/GvResearch.Shared/Protocol/ tests/GvResearch.Shared.Tests/Protocol/
git commit -m "feat(protocol): add GvRequestBuilder and GvProtobufJsonParser

GvRequestBuilder builds protobuf-JSON request bodies for all GV endpoints.
GvProtobufJsonParser parses positional array responses into domain models."
```

---

## Task 6: SDK Interfaces & Client Implementations

**Files:**
- Create: `src/GvResearch.Shared/Services/IGvClient.cs`
- Create: `src/GvResearch.Shared/Transport/ICallTransport.cs`
- Create: `src/GvResearch.Shared/Services/GvAccountClient.cs`
- Create: `src/GvResearch.Shared/Services/GvThreadClient.cs`
- Create: `src/GvResearch.Shared/Services/GvSmsClient.cs`
- Create: `src/GvResearch.Shared/Services/GvCallClient.cs`
- Create: `src/GvResearch.Shared/Services/GvClient.cs`
- Delete: `src/GvResearch.Shared/Services/IGvCallService.cs`
- Delete: `src/GvResearch.Shared/Services/GvCallService.cs`
- Test: `tests/GvResearch.Shared.Tests/Services/GvAccountClientTests.cs`
- Test: `tests/GvResearch.Shared.Tests/Services/GvThreadClientTests.cs`
- Test: `tests/GvResearch.Shared.Tests/Services/GvSmsClientTests.cs`
- Test: `tests/GvResearch.Shared.Tests/Services/GvCallClientTests.cs` (rewrite)

- [ ] **Step 1: Create `IGvClient.cs` with all interfaces**

```csharp
using GvResearch.Shared.Models;

namespace GvResearch.Shared.Services;

public interface IGvClient : IAsyncDisposable
{
    IGvAccountClient Account { get; }
    IGvThreadClient Threads { get; }
    IGvSmsClient Sms { get; }
    IGvCallClient Calls { get; }
}

public interface IGvAccountClient
{
    Task<GvAccount> GetAsync(CancellationToken ct = default);
}

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

public interface IGvSmsClient
{
    Task<GvSmsResult> SendAsync(string toNumber, string message, CancellationToken ct = default);
}

public interface IGvCallClient
{
    Task<GvCallResult> InitiateAsync(string toNumber, CancellationToken ct = default);
    Task<GvCallStatus> GetStatusAsync(string callId, CancellationToken ct = default);
    Task HangupAsync(string callId, CancellationToken ct = default);
}
```

- [ ] **Step 2: Create `ICallTransport.cs` with transport types**

```csharp
using GvResearch.Shared.Models;

namespace GvResearch.Shared.Transport;

public interface ICallTransport : IAsyncDisposable
{
    Task<TransportCallResult> InitiateAsync(string toNumber, CancellationToken ct = default);
    Task<TransportCallStatus> GetStatusAsync(string callId, CancellationToken ct = default);
    Task HangupAsync(string callId, CancellationToken ct = default);
}

public sealed record TransportCallResult(string CallId, bool Success, string? ErrorMessage);
public sealed record TransportCallStatus(string CallId, CallStatusType Status);
```

- [ ] **Step 3: Write failing test for GvAccountClient**

```csharp
using System.Net;
using System.Text.Json;
using FluentAssertions;
using GvResearch.Shared.Exceptions;
using GvResearch.Shared.RateLimiting;
using GvResearch.Shared.Services;

namespace GvResearch.Shared.Tests.Services;

public sealed class GvAccountClientTests : IDisposable
{
    private readonly GvRateLimiter _rateLimiter = new(perMinuteLimit: 100, perDayLimit: 1000);

    [Fact]
    public async Task GetAsync_ReturnsAccount()
    {
        var responseJson = """["+19196706660",null,[],null,null,[]]""";
        using var httpClient = CreateHttpClient(HttpStatusCode.OK, responseJson);
        var sut = new GvAccountClient(httpClient, _rateLimiter);

        var account = await sut.GetAsync();

        account.PhoneNumbers.Should().ContainSingle();
        account.PhoneNumbers[0].Number.Should().Be("+19196706660");
    }

    [Fact]
    public async Task GetAsync_WhenRateLimited_Throws()
    {
        using var limiter = new GvRateLimiter(perMinuteLimit: 1, perDayLimit: 100);
        await limiter.TryAcquireAsync("account/get");
        using var httpClient = CreateHttpClient(HttpStatusCode.OK, "[]");
        var sut = new GvAccountClient(httpClient, limiter);

        var act = () => sut.GetAsync();

        await act.Should().ThrowAsync<GvRateLimitException>();
    }

    [Fact]
    public async Task GetAsync_WhenUnauthorized_ThrowsAuthException()
    {
        using var httpClient = CreateHttpClient(HttpStatusCode.Unauthorized, "");
        var sut = new GvAccountClient(httpClient, _rateLimiter);

        var act = () => sut.GetAsync();

        await act.Should().ThrowAsync<GvAuthException>();
    }

    private static HttpClient CreateHttpClient(HttpStatusCode code, string body)
    {
        var handler = new FakeHttpMessageHandler(code, body);
        return new HttpClient(handler) { BaseAddress = new Uri("https://clients6.google.com") };
    }

    public void Dispose() => _rateLimiter.Dispose();
}
```

- [ ] **Step 4: Create `GvAccountClient.cs`**

```csharp
using System.Text;
using System.Text.Json;
using GvResearch.Shared.Exceptions;
using GvResearch.Shared.Models;
using GvResearch.Shared.Protocol;
using GvResearch.Shared.RateLimiting;

namespace GvResearch.Shared.Services;

public sealed class GvAccountClient : IGvAccountClient
{
    private const string Endpoint = "account/get";
    private readonly HttpClient _httpClient;
    private readonly GvRateLimiter _rateLimiter;

    public GvAccountClient(HttpClient httpClient, GvRateLimiter rateLimiter)
    {
        _httpClient = httpClient;
        _rateLimiter = rateLimiter;
    }

    public async Task<GvAccount> GetAsync(CancellationToken ct = default)
    {
        if (!await _rateLimiter.TryAcquireAsync(Endpoint, ct).ConfigureAwait(false))
            throw new GvRateLimitException(Endpoint);

        var body = GvRequestBuilder.BuildAccountGetRequest();
        using var content = new StringContent(body, Encoding.UTF8, "application/json+protobuf");
        var response = await _httpClient
            .PostAsync(new Uri($"voice/v1/voiceclient/{Endpoint}?alt=protojson", UriKind.Relative), content, ct)
            .ConfigureAwait(false);

        if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
            throw new GvAuthException($"Authentication failed: HTTP {(int)response.StatusCode}", (int)response.StatusCode);

        if (!response.IsSuccessStatusCode)
            throw new GvApiException($"Account get failed: HTTP {(int)response.StatusCode}", (int)response.StatusCode);

        var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        var root = JsonDocument.Parse(json).RootElement;
        return GvProtobufJsonParser.ParseAccount(root);
    }
}
```

- [ ] **Step 5: Run account client tests**

```bash
cd D:/prj/GVResearch && dotnet test tests/GvResearch.Shared.Tests --filter "FullyQualifiedName~GvAccountClientTests" --no-restore -v minimal
```

Expected: 3 tests pass.

- [ ] **Step 6: Write GvThreadClient tests and implementation**

Create `tests/GvResearch.Shared.Tests/Services/GvThreadClientTests.cs`:

```csharp
using System.Net;
using FluentAssertions;
using GvResearch.Shared.Exceptions;
using GvResearch.Shared.Models;
using GvResearch.Shared.RateLimiting;
using GvResearch.Shared.Services;

namespace GvResearch.Shared.Tests.Services;

public sealed class GvThreadClientTests : IDisposable
{
    private readonly GvRateLimiter _rateLimiter = new(perMinuteLimit: 100, perDayLimit: 1000);

    [Fact]
    public async Task ListAsync_ReturnsThreadPage()
    {
        var json = """[[[["t.+15551234567",1,[["msg1",1000,"+1",["+2"],10,1,null,null,null,null,null,"Hi",null,null,0,1]]]]]]""";
        using var httpClient = CreateHttpClient(HttpStatusCode.OK, json);
        var sut = new GvThreadClient(httpClient, _rateLimiter);

        var page = await sut.ListAsync();

        page.Threads.Should().ContainSingle();
        page.Threads[0].Id.Should().Be("t.+15551234567");
    }

    [Fact]
    public async Task GetAsync_ReturnsThread()
    {
        var json = """["t.+15551234567",0,[["msg1",1000,"+1",["+2"],10,0,null,null,null,null,null,"Hello",null,null,0,0]]]""";
        using var httpClient = CreateHttpClient(HttpStatusCode.OK, json);
        var sut = new GvThreadClient(httpClient, _rateLimiter);

        var thread = await sut.GetAsync("t.+15551234567");

        thread.Id.Should().Be("t.+15551234567");
        thread.Messages.Should().ContainSingle();
    }

    [Fact]
    public async Task DeleteAsync_WhenUnauthorized_ThrowsAuthException()
    {
        using var httpClient = CreateHttpClient(HttpStatusCode.Forbidden, "");
        var sut = new GvThreadClient(httpClient, _rateLimiter);

        var act = () => sut.DeleteAsync(["t.+1111"]);

        await act.Should().ThrowAsync<GvAuthException>();
    }

    [Fact]
    public async Task GetUnreadCountsAsync_ReturnsCounts()
    {
        var json = """[[[[1,null,5],[4,null,2],[3,null,10],[5,null,0],[6,null,1]]]]""";
        using var httpClient = CreateHttpClient(HttpStatusCode.OK, json);
        var sut = new GvThreadClient(httpClient, _rateLimiter);

        var counts = await sut.GetUnreadCountsAsync();

        counts.Sms.Should().Be(10);
        counts.Voicemail.Should().Be(2);
        counts.Missed.Should().Be(5);
    }

    private static HttpClient CreateHttpClient(HttpStatusCode code, string body) =>
        new(new FakeHttpMessageHandler(code, body)) { BaseAddress = new Uri("https://clients6.google.com") };

    public void Dispose() => _rateLimiter.Dispose();
}
```

Create `src/GvResearch.Shared/Services/GvThreadClient.cs`:

```csharp
using System.Text;
using System.Text.Json;
using GvResearch.Shared.Exceptions;
using GvResearch.Shared.Models;
using GvResearch.Shared.Protocol;
using GvResearch.Shared.RateLimiting;

namespace GvResearch.Shared.Services;

public sealed class GvThreadClient : IGvThreadClient
{
    private readonly HttpClient _httpClient;
    private readonly GvRateLimiter _rateLimiter;

    public GvThreadClient(HttpClient httpClient, GvRateLimiter rateLimiter)
    {
        _httpClient = httpClient;
        _rateLimiter = rateLimiter;
    }

    public Task<GvThreadPage> ListAsync(GvThreadListOptions? options = null, CancellationToken ct = default) =>
        PostAndParseAsync("api2thread/list", GvRequestBuilder.BuildThreadListRequest(options),
            GvProtobufJsonParser.ParseThreadList, ct);

    public Task<GvThread> GetAsync(string threadId, CancellationToken ct = default) =>
        PostAndParseAsync("api2thread/get", GvRequestBuilder.BuildThreadGetRequest(threadId),
            GvProtobufJsonParser.ParseThread, ct);

    public Task MarkReadAsync(IEnumerable<string> threadIds, CancellationToken ct = default) =>
        PostAsync("thread/batchupdateattributes",
            GvRequestBuilder.BuildBatchUpdateRequest(threadIds, "read"), ct);

    public Task ArchiveAsync(IEnumerable<string> threadIds, CancellationToken ct = default) =>
        PostAsync("thread/batchupdateattributes",
            GvRequestBuilder.BuildBatchUpdateRequest(threadIds, "archive"), ct);

    public Task MarkSpamAsync(IEnumerable<string> threadIds, CancellationToken ct = default) =>
        PostAsync("thread/batchupdateattributes",
            GvRequestBuilder.BuildBatchUpdateRequest(threadIds, "spam"), ct);

    public Task DeleteAsync(IEnumerable<string> threadIds, CancellationToken ct = default) =>
        PostAsync("thread/batchdelete",
            GvRequestBuilder.BuildBatchDeleteRequest(threadIds), ct);

    public Task MarkAllReadAsync(CancellationToken ct = default) =>
        PostAsync("thread/markallread",
            GvRequestBuilder.BuildMarkAllReadRequest(), ct);

    public Task<GvUnreadCounts> GetUnreadCountsAsync(CancellationToken ct = default) =>
        PostAndParseAsync("threadinginfo/get", GvRequestBuilder.BuildThreadingInfoRequest(),
            GvProtobufJsonParser.ParseUnreadCounts, ct);

    public Task<GvThreadPage> SearchAsync(string query, CancellationToken ct = default) =>
        PostAndParseAsync("api2thread/search", GvRequestBuilder.BuildSearchRequest(query),
            GvProtobufJsonParser.ParseThreadList, ct);

    private async Task<T> PostAndParseAsync<T>(string endpoint, string body,
        Func<JsonElement, T> parser, CancellationToken ct)
    {
        if (!await _rateLimiter.TryAcquireAsync(endpoint, ct).ConfigureAwait(false))
            throw new GvRateLimitException(endpoint);

        using var content = new StringContent(body, Encoding.UTF8, "application/json+protobuf");
        var response = await _httpClient
            .PostAsync(new Uri($"voice/v1/voiceclient/{endpoint}?alt=protojson", UriKind.Relative), content, ct)
            .ConfigureAwait(false);

        ThrowOnAuthError(response, endpoint);
        if (!response.IsSuccessStatusCode)
            throw new GvApiException($"{endpoint} failed: HTTP {(int)response.StatusCode}", (int)response.StatusCode);

        var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        return parser(JsonDocument.Parse(json).RootElement);
    }

    private async Task PostAsync(string endpoint, string body, CancellationToken ct)
    {
        if (!await _rateLimiter.TryAcquireAsync(endpoint, ct).ConfigureAwait(false))
            throw new GvRateLimitException(endpoint);

        using var content = new StringContent(body, Encoding.UTF8, "application/json+protobuf");
        var response = await _httpClient
            .PostAsync(new Uri($"voice/v1/voiceclient/{endpoint}?alt=protojson", UriKind.Relative), content, ct)
            .ConfigureAwait(false);

        ThrowOnAuthError(response, endpoint);
        if (!response.IsSuccessStatusCode)
            throw new GvApiException($"{endpoint} failed: HTTP {(int)response.StatusCode}", (int)response.StatusCode);
    }

    private static void ThrowOnAuthError(HttpResponseMessage response, string endpoint)
    {
        if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
            throw new GvAuthException($"Authentication failed for {endpoint}: HTTP {(int)response.StatusCode}", (int)response.StatusCode);
    }
}
```

- [ ] **Step 7: Write GvSmsClient tests and implementation**

Create `tests/GvResearch.Shared.Tests/Services/GvSmsClientTests.cs`:

```csharp
using System.Net;
using FluentAssertions;
using GvResearch.Shared.RateLimiting;
using GvResearch.Shared.Services;

namespace GvResearch.Shared.Tests.Services;

public sealed class GvSmsClientTests : IDisposable
{
    private readonly GvRateLimiter _rateLimiter = new(perMinuteLimit: 100, perDayLimit: 1000);

    [Fact]
    public async Task SendAsync_ReturnsSuccess()
    {
        var json = """[null,"t.+15551234567","msg-hash",1234567890000,[1,2]]""";
        using var httpClient = new HttpClient(new FakeHttpMessageHandler(HttpStatusCode.OK, json))
            { BaseAddress = new Uri("https://clients6.google.com") };
        var sut = new GvSmsClient(httpClient, _rateLimiter);

        var result = await sut.SendAsync("+15551234567", "Hello!");

        result.Success.Should().BeTrue();
        result.ThreadId.Should().Be("t.+15551234567");
    }

    public void Dispose() => _rateLimiter.Dispose();
}
```

Create `src/GvResearch.Shared/Services/GvSmsClient.cs`:

```csharp
using System.Text;
using System.Text.Json;
using GvResearch.Shared.Exceptions;
using GvResearch.Shared.Models;
using GvResearch.Shared.Protocol;
using GvResearch.Shared.RateLimiting;

namespace GvResearch.Shared.Services;

public sealed class GvSmsClient : IGvSmsClient
{
    private const string Endpoint = "api2thread/sendsms";
    private readonly HttpClient _httpClient;
    private readonly GvRateLimiter _rateLimiter;

    public GvSmsClient(HttpClient httpClient, GvRateLimiter rateLimiter)
    {
        _httpClient = httpClient;
        _rateLimiter = rateLimiter;
    }

    public async Task<GvSmsResult> SendAsync(string toNumber, string message, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        if (!await _rateLimiter.TryAcquireAsync(Endpoint, ct).ConfigureAwait(false))
            throw new GvRateLimitException(Endpoint);

        var body = GvRequestBuilder.BuildSendSmsRequest(toNumber, message);
        using var content = new StringContent(body, Encoding.UTF8, "application/json+protobuf");
        var response = await _httpClient
            .PostAsync(new Uri($"voice/v1/voiceclient/{Endpoint}?alt=protojson", UriKind.Relative), content, ct)
            .ConfigureAwait(false);

        if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
            throw new GvAuthException($"Authentication failed: HTTP {(int)response.StatusCode}", (int)response.StatusCode);

        if (!response.IsSuccessStatusCode)
            throw new GvApiException($"Send SMS failed: HTTP {(int)response.StatusCode}", (int)response.StatusCode);

        var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        return GvProtobufJsonParser.ParseSendSms(JsonDocument.Parse(json).RootElement);
    }
}
```

- [ ] **Step 8: Rewrite GvCallClient tests and implementation**

Rewrite `tests/GvResearch.Shared.Tests/Services/GvCallClientTests.cs`:

```csharp
using FluentAssertions;
using GvResearch.Shared.Exceptions;
using GvResearch.Shared.Models;
using GvResearch.Shared.RateLimiting;
using GvResearch.Shared.Services;
using GvResearch.Shared.Transport;
using NSubstitute;

namespace GvResearch.Shared.Tests.Services;

public sealed class GvCallClientTests : IDisposable
{
    private readonly ICallTransport _transport = Substitute.For<ICallTransport>();
    private readonly GvRateLimiter _rateLimiter = new(perMinuteLimit: 100, perDayLimit: 1000);

    [Fact]
    public async Task InitiateAsync_DelegatesToTransport()
    {
        _transport.InitiateAsync("+15551234567", Arg.Any<CancellationToken>())
            .Returns(new TransportCallResult("call-123", true, null));
        var sut = new GvCallClient(_transport, _rateLimiter);

        var result = await sut.InitiateAsync("+15551234567");

        result.Success.Should().BeTrue();
        result.CallId.Should().Be("call-123");
    }

    [Fact]
    public async Task InitiateAsync_WhenRateLimited_Throws()
    {
        using var limiter = new GvRateLimiter(perMinuteLimit: 1, perDayLimit: 100);
        await limiter.TryAcquireAsync("calls/initiate");
        var sut = new GvCallClient(_transport, limiter);

        var act = () => sut.InitiateAsync("+15551234567");

        await act.Should().ThrowAsync<GvRateLimitException>();
    }

    [Fact]
    public async Task HangupAsync_DelegatesToTransport()
    {
        var sut = new GvCallClient(_transport, _rateLimiter);

        await sut.HangupAsync("call-123");

        await _transport.Received(1).HangupAsync("call-123", Arg.Any<CancellationToken>());
    }

    public void Dispose() => _rateLimiter.Dispose();
}
```

Create `src/GvResearch.Shared/Services/GvCallClient.cs`:

```csharp
using GvResearch.Shared.Exceptions;
using GvResearch.Shared.Models;
using GvResearch.Shared.RateLimiting;
using GvResearch.Shared.Transport;

namespace GvResearch.Shared.Services;

public sealed class GvCallClient : IGvCallClient
{
    private readonly ICallTransport _transport;
    private readonly GvRateLimiter _rateLimiter;

    public GvCallClient(ICallTransport transport, GvRateLimiter rateLimiter)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(rateLimiter);
        _transport = transport;
        _rateLimiter = rateLimiter;
    }

    public async Task<GvCallResult> InitiateAsync(string toNumber, CancellationToken ct = default)
    {
        if (!await _rateLimiter.TryAcquireAsync("calls/initiate", ct).ConfigureAwait(false))
            throw new GvRateLimitException("calls/initiate");

        var result = await _transport.InitiateAsync(toNumber, ct).ConfigureAwait(false);
        return new GvCallResult(result.CallId, result.Success, result.ErrorMessage);
    }

    public async Task<GvCallStatus> GetStatusAsync(string callId, CancellationToken ct = default)
    {
        if (!await _rateLimiter.TryAcquireAsync("calls/status", ct).ConfigureAwait(false))
            throw new GvRateLimitException("calls/status");

        var result = await _transport.GetStatusAsync(callId, ct).ConfigureAwait(false);
        return new GvCallStatus(result.CallId, result.Status, DateTimeOffset.UtcNow);
    }

    public async Task HangupAsync(string callId, CancellationToken ct = default)
    {
        if (!await _rateLimiter.TryAcquireAsync("calls/hangup", ct).ConfigureAwait(false))
            throw new GvRateLimitException("calls/hangup");

        await _transport.HangupAsync(callId, ct).ConfigureAwait(false);
    }
}
```

- [ ] **Step 9: Create GvClient (IGvClient implementation)**

```csharp
namespace GvResearch.Shared.Services;

public sealed class GvClient : IGvClient
{
    public GvClient(
        IGvAccountClient account,
        IGvThreadClient threads,
        IGvSmsClient sms,
        IGvCallClient calls)
    {
        Account = account;
        Threads = threads;
        Sms = sms;
        Calls = calls;
    }

    public IGvAccountClient Account { get; }
    public IGvThreadClient Threads { get; }
    public IGvSmsClient Sms { get; }
    public IGvCallClient Calls { get; }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
```

- [ ] **Step 10: Delete old service files**

```bash
cd D:/prj/GVResearch
git rm src/GvResearch.Shared/Services/IGvCallService.cs
git rm src/GvResearch.Shared/Services/GvCallService.cs
```

- [ ] **Step 11: Run all Shared.Tests**

```bash
cd D:/prj/GVResearch && dotnet test tests/GvResearch.Shared.Tests --no-restore -v minimal
```

Expected: Old `GvCallServiceTests` will fail (references deleted types). This is expected — it gets rewritten. The new tests should pass.

- [ ] **Step 12: Delete old GvCallServiceTests**

Replace `tests/GvResearch.Shared.Tests/Services/GvCallServiceTests.cs` entirely with just the `FakeHttpMessageHandler` (needed by other tests):

```csharp
using System.Net;

namespace GvResearch.Shared.Tests.Services;

internal sealed class FakeHttpMessageHandler(HttpStatusCode statusCode, string body = "{}") : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body),
        };
        return Task.FromResult(response);
    }
}
```

Rename the file to `FakeHttpMessageHandler.cs` to match its content:

```bash
cd D:/prj/GVResearch
git mv tests/GvResearch.Shared.Tests/Services/GvCallServiceTests.cs tests/GvResearch.Shared.Tests/Services/FakeHttpMessageHandler.cs
```

- [ ] **Step 13: Run all Shared.Tests again**

```bash
cd D:/prj/GVResearch && dotnet test tests/GvResearch.Shared.Tests --no-restore -v minimal
```

Expected: All tests pass.

- [ ] **Step 14: Commit**

```bash
cd D:/prj/GVResearch
git add src/GvResearch.Shared/Services/ src/GvResearch.Shared/Transport/ tests/GvResearch.Shared.Tests/Services/
git commit -m "feat(sdk): add IGvClient with Account, Thread, SMS, Call sub-clients

IGvClient is the single entry point for all GV operations.
GvCallClient delegates to ICallTransport for transport-agnostic calls.
Delete old IGvCallService/GvCallService (replaced by new SDK)."
```

---

## Task 7: DI Registration — GvClientOptions & AddGvClient()

**Files:**
- Create: `src/GvResearch.Shared/GvClientOptions.cs`
- Create: `src/GvResearch.Shared/GvClientServiceExtensions.cs`

- [ ] **Step 1: Create `GvClientOptions.cs`**

```csharp
namespace GvResearch.Shared;

public enum CallTransportType { Sip, WebRtc, Callback }

public sealed class GvClientOptions
{
    public string CookiePath { get; set; } = "cookies.enc";
    public string KeyPath { get; set; } = "key.bin";
    public CallTransportType CallTransport { get; set; } = CallTransportType.Sip;
}
```

- [ ] **Step 2: Create `GvClientServiceExtensions.cs`**

```csharp
using GvResearch.Shared.Auth;
using GvResearch.Shared.Http;
using GvResearch.Shared.RateLimiting;
using GvResearch.Shared.Services;
using GvResearch.Shared.Transport;
using Microsoft.Extensions.DependencyInjection;

namespace GvResearch.Shared;

public static class GvClientServiceExtensions
{
    public static IServiceCollection AddGvClient(
        this IServiceCollection services,
        Action<GvClientOptions>? configure = null)
    {
        var options = new GvClientOptions();
        configure?.Invoke(options);

        // Auth
        services.AddSingleton<IGvAuthService>(
            new GvAuthService(options.CookiePath, options.KeyPath));

        // HTTP handler
        services.AddTransient<GvHttpClientHandler>(sp =>
        {
            var auth = sp.GetRequiredService<IGvAuthService>();
            return new GvHttpClientHandler(auth, new HttpClientHandler());
        });

        // Rate limiter
        services.AddSingleton<GvRateLimiter>();

        // HTTP client for GV API
        services.AddHttpClient("GvApi", client =>
        {
            client.BaseAddress = new Uri("https://clients6.google.com");
        });

        // Sub-clients
        services.AddSingleton<IGvAccountClient>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            return new GvAccountClient(factory.CreateClient("GvApi"), sp.GetRequiredService<GvRateLimiter>());
        });

        services.AddSingleton<IGvThreadClient>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            return new GvThreadClient(factory.CreateClient("GvApi"), sp.GetRequiredService<GvRateLimiter>());
        });

        services.AddSingleton<IGvSmsClient>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            return new GvSmsClient(factory.CreateClient("GvApi"), sp.GetRequiredService<GvRateLimiter>());
        });

        // Call transport (placeholder — SIP registered in GvResearch.Sip)
        if (!services.Any(d => d.ServiceType == typeof(ICallTransport)))
        {
            services.AddSingleton<ICallTransport, NullCallTransport>();
        }

        services.AddSingleton<IGvCallClient>(sp =>
            new GvCallClient(
                sp.GetRequiredService<ICallTransport>(),
                sp.GetRequiredService<GvRateLimiter>()));

        // Composite client
        services.AddSingleton<IGvClient>(sp =>
            new GvClient(
                sp.GetRequiredService<IGvAccountClient>(),
                sp.GetRequiredService<IGvThreadClient>(),
                sp.GetRequiredService<IGvSmsClient>(),
                sp.GetRequiredService<IGvCallClient>()));

        return services;
    }
}

internal sealed class NullCallTransport : ICallTransport
{
    public Task<TransportCallResult> InitiateAsync(string toNumber, CancellationToken ct) =>
        throw new NotImplementedException("No call transport configured. Register an ICallTransport implementation.");

    public Task<TransportCallStatus> GetStatusAsync(string callId, CancellationToken ct) =>
        throw new NotImplementedException("No call transport configured.");

    public Task HangupAsync(string callId, CancellationToken ct) =>
        throw new NotImplementedException("No call transport configured.");

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
```

- [ ] **Step 3: Add `Microsoft.Extensions.DependencyInjection.Abstractions` to Shared.csproj**

Add to `src/GvResearch.Shared/GvResearch.Shared.csproj`:

```xml
<PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.5" />
```

- [ ] **Step 4: Commit**

```bash
cd D:/prj/GVResearch
git add src/GvResearch.Shared/GvClientOptions.cs src/GvResearch.Shared/GvClientServiceExtensions.cs src/GvResearch.Shared/GvResearch.Shared.csproj
git commit -m "feat(di): add AddGvClient() DI registration extension

One-liner registration for all GV SDK services. Configurable cookie/key
paths and call transport type."
```

---

## Task 8: REST API Endpoints

**Files:**
- Create: `src/GvResearch.Api/Endpoints/AccountEndpoints.cs`
- Create: `src/GvResearch.Api/Endpoints/ThreadEndpoints.cs`
- Create: `src/GvResearch.Api/Endpoints/SmsEndpoints.cs`
- Modify: `src/GvResearch.Api/Endpoints/CallEndpoints.cs`
- Modify: `src/GvResearch.Api/Program.cs`
- Delete: `src/GvResearch.Api/Models/CallRecord.cs`
- Modify: `src/GvResearch.Api/appsettings.json`

- [ ] **Step 1: Create `AccountEndpoints.cs`**

```csharp
using GvResearch.Shared.Exceptions;
using GvResearch.Shared.Services;

namespace GvResearch.Api.Endpoints;

public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/account")
            .RequireAuthorization()
            .WithTags("Account");

        group.MapGet("/", async (IGvClient client, CancellationToken ct) =>
        {
            try
            {
                var account = await client.Account.GetAsync(ct);
                return Results.Ok(account);
            }
            catch (GvAuthException) { return Results.Unauthorized(); }
            catch (GvRateLimitException) { return Results.StatusCode(429); }
            catch (GvApiException ex) { return Results.Problem(detail: ex.Message, statusCode: 502); }
        })
        .WithName("GetAccount")
        .WithSummary("Get account configuration including phone numbers and devices.");

        return app;
    }
}
```

- [ ] **Step 2: Create `ThreadEndpoints.cs`**

```csharp
using GvResearch.Shared.Exceptions;
using GvResearch.Shared.Models;
using GvResearch.Shared.Services;

namespace GvResearch.Api.Endpoints;

public static class ThreadEndpoints
{
    public static IEndpointRouteBuilder MapThreadEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/threads")
            .RequireAuthorization()
            .WithTags("Threads");

        group.MapGet("/", async (string? cursor, GvThreadType? type, int? maxResults,
            IGvClient client, CancellationToken ct) =>
        {
            try
            {
                var options = new GvThreadListOptions(cursor, type, maxResults ?? 50);
                var page = await client.Threads.ListAsync(options, ct);
                return Results.Ok(page);
            }
            catch (GvAuthException) { return Results.Unauthorized(); }
            catch (GvRateLimitException) { return Results.StatusCode(429); }
            catch (GvApiException ex) { return Results.Problem(detail: ex.Message, statusCode: 502); }
        })
        .WithName("ListThreads")
        .WithSummary("List conversation threads with optional filtering and pagination.");

        group.MapGet("/{threadId}", async (string threadId, IGvClient client, CancellationToken ct) =>
        {
            try
            {
                var thread = await client.Threads.GetAsync(threadId, ct);
                return Results.Ok(thread);
            }
            catch (GvAuthException) { return Results.Unauthorized(); }
            catch (GvRateLimitException) { return Results.StatusCode(429); }
            catch (GvApiException ex) { return Results.Problem(detail: ex.Message, statusCode: 502); }
        })
        .WithName("GetThread")
        .WithSummary("Get a single thread with full message history.");

        group.MapGet("/unread", async (IGvClient client, CancellationToken ct) =>
        {
            try
            {
                var counts = await client.Threads.GetUnreadCountsAsync(ct);
                return Results.Ok(counts);
            }
            catch (GvAuthException) { return Results.Unauthorized(); }
            catch (GvRateLimitException) { return Results.StatusCode(429); }
            catch (GvApiException ex) { return Results.Problem(detail: ex.Message, statusCode: 502); }
        })
        .WithName("GetUnreadCounts")
        .WithSummary("Get unread counts per thread type.");

        group.MapGet("/search", async (string q, IGvClient client, CancellationToken ct) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(q))
                    return Results.BadRequest("Query parameter 'q' is required.");
                var page = await client.Threads.SearchAsync(q, ct);
                return Results.Ok(page);
            }
            catch (GvAuthException) { return Results.Unauthorized(); }
            catch (GvRateLimitException) { return Results.StatusCode(429); }
            catch (GvApiException ex) { return Results.Problem(detail: ex.Message, statusCode: 502); }
        })
        .WithName("SearchThreads")
        .WithSummary("Full-text search across all threads.");

        group.MapPost("/markread", async (ThreadIdsRequest request, IGvClient client, CancellationToken ct) =>
        {
            try
            {
                await client.Threads.MarkReadAsync(request.ThreadIds, ct);
                return Results.NoContent();
            }
            catch (GvAuthException) { return Results.Unauthorized(); }
            catch (GvRateLimitException) { return Results.StatusCode(429); }
            catch (GvApiException ex) { return Results.Problem(detail: ex.Message, statusCode: 502); }
        })
        .WithName("MarkThreadsRead")
        .WithSummary("Mark threads as read.");

        group.MapPost("/archive", async (ThreadIdsRequest request, IGvClient client, CancellationToken ct) =>
        {
            try
            {
                await client.Threads.ArchiveAsync(request.ThreadIds, ct);
                return Results.NoContent();
            }
            catch (GvAuthException) { return Results.Unauthorized(); }
            catch (GvRateLimitException) { return Results.StatusCode(429); }
            catch (GvApiException ex) { return Results.Problem(detail: ex.Message, statusCode: 502); }
        })
        .WithName("ArchiveThreads")
        .WithSummary("Archive threads.");

        group.MapPost("/spam", async (ThreadIdsRequest request, IGvClient client, CancellationToken ct) =>
        {
            try
            {
                await client.Threads.MarkSpamAsync(request.ThreadIds, ct);
                return Results.NoContent();
            }
            catch (GvAuthException) { return Results.Unauthorized(); }
            catch (GvRateLimitException) { return Results.StatusCode(429); }
            catch (GvApiException ex) { return Results.Problem(detail: ex.Message, statusCode: 502); }
        })
        .WithName("MarkThreadsSpam")
        .WithSummary("Mark threads as spam.");

        group.MapPost("/markallread", async (IGvClient client, CancellationToken ct) =>
        {
            try
            {
                await client.Threads.MarkAllReadAsync(ct);
                return Results.NoContent();
            }
            catch (GvAuthException) { return Results.Unauthorized(); }
            catch (GvRateLimitException) { return Results.StatusCode(429); }
            catch (GvApiException ex) { return Results.Problem(detail: ex.Message, statusCode: 502); }
        })
        .WithName("MarkAllRead")
        .WithSummary("Mark all threads as read.");

        group.MapDelete("/", async (ThreadIdsRequest request, IGvClient client, CancellationToken ct) =>
        {
            try
            {
                await client.Threads.DeleteAsync(request.ThreadIds, ct);
                return Results.NoContent();
            }
            catch (GvAuthException) { return Results.Unauthorized(); }
            catch (GvRateLimitException) { return Results.StatusCode(429); }
            catch (GvApiException ex) { return Results.Problem(detail: ex.Message, statusCode: 502); }
        })
        .WithName("DeleteThreads")
        .WithSummary("Permanently delete threads.");

        return app;
    }
}

public sealed record ThreadIdsRequest(IReadOnlyList<string> ThreadIds);
```

- [ ] **Step 3: Create `SmsEndpoints.cs`**

```csharp
using GvResearch.Shared.Exceptions;
using GvResearch.Shared.Services;

namespace GvResearch.Api.Endpoints;

public static class SmsEndpoints
{
    public static IEndpointRouteBuilder MapSmsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/sms")
            .RequireAuthorization()
            .WithTags("SMS");

        group.MapPost("/", async (SendSmsRequest request, IGvClient client, CancellationToken ct) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.ToNumber))
                    return Results.BadRequest("ToNumber is required.");
                if (string.IsNullOrWhiteSpace(request.Message))
                    return Results.BadRequest("Message is required.");

                var result = await client.Sms.SendAsync(request.ToNumber, request.Message, ct);
                return Results.Created($"/api/v1/threads/{result.ThreadId}", result);
            }
            catch (GvAuthException) { return Results.Unauthorized(); }
            catch (GvRateLimitException) { return Results.StatusCode(429); }
            catch (GvApiException ex) { return Results.Problem(detail: ex.Message, statusCode: 502); }
        })
        .WithName("SendSms")
        .WithSummary("Send an SMS message.");

        return app;
    }
}

public sealed record SendSmsRequest(string ToNumber, string Message);
```

- [ ] **Step 4: Rewrite `CallEndpoints.cs`**

Replace the entire file:

```csharp
using GvResearch.Shared.Exceptions;
using GvResearch.Shared.Services;

namespace GvResearch.Api.Endpoints;

public static class CallEndpoints
{
    public static IEndpointRouteBuilder MapCallEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/calls")
            .RequireAuthorization()
            .WithTags("Calls");

        group.MapPost("/", async (InitiateCallRequest request, IGvClient client, CancellationToken ct) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.ToNumber))
                    return Results.BadRequest("ToNumber is required.");

                var result = await client.Calls.InitiateAsync(request.ToNumber, ct);
                if (!result.Success)
                    return Results.Problem(detail: result.ErrorMessage, statusCode: 502, title: "Call initiation failed.");

                return Results.Created($"/api/v1/calls/{result.CallId}/status", result);
            }
            catch (GvAuthException) { return Results.Unauthorized(); }
            catch (GvRateLimitException) { return Results.StatusCode(429); }
            catch (GvApiException ex) { return Results.Problem(detail: ex.Message, statusCode: 502); }
        })
        .WithName("InitiateCall")
        .WithSummary("Initiate an outbound call.");

        group.MapGet("/{callId}/status", async (string callId, IGvClient client, CancellationToken ct) =>
        {
            try
            {
                var status = await client.Calls.GetStatusAsync(callId, ct);
                return Results.Ok(status);
            }
            catch (GvAuthException) { return Results.Unauthorized(); }
            catch (GvRateLimitException) { return Results.StatusCode(429); }
            catch (GvApiException ex) { return Results.Problem(detail: ex.Message, statusCode: 502); }
        })
        .WithName("GetCallStatus")
        .WithSummary("Get the status of an active call.");

        group.MapPost("/{callId}/hangup", async (string callId, IGvClient client, CancellationToken ct) =>
        {
            try
            {
                await client.Calls.HangupAsync(callId, ct);
                return Results.NoContent();
            }
            catch (GvAuthException) { return Results.Unauthorized(); }
            catch (GvRateLimitException) { return Results.StatusCode(429); }
            catch (GvApiException ex) { return Results.Problem(detail: ex.Message, statusCode: 502); }
        })
        .WithName("HangupCall")
        .WithSummary("Hang up an active call.");

        return app;
    }
}

public sealed record InitiateCallRequest(string ToNumber);
```

- [ ] **Step 5: Delete `CallRecord.cs`**

```bash
cd D:/prj/GVResearch && git rm src/GvResearch.Api/Models/CallRecord.cs
```

- [ ] **Step 6: Rewrite `Program.cs`**

Replace the entire file:

```csharp
using System.Globalization;
using GvResearch.Api.Endpoints;
using GvResearch.Shared;
using Microsoft.AspNetCore.Authentication;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Serilog
    builder.Host.UseSerilog((ctx, lc) =>
        lc.ReadFrom.Configuration(ctx.Configuration));

    // Authentication / Authorization
    builder.Services
        .AddAuthentication("Bearer")
        .AddScheme<AuthenticationSchemeOptions, GvResearch.Api.Auth.BearerSchemeHandler>(
            "Bearer", _ => { });
    builder.Services.AddAuthorization();

    // OpenAPI
    builder.Services.AddEndpointsApiExplorer();

    // GV Client SDK
    var cfg = builder.Configuration;
    builder.Services.AddGvClient(options =>
    {
        options.CookiePath = cfg["GvResearch:CookiePath"] ?? "cookies.enc";
        options.KeyPath = cfg["GvResearch:KeyPath"] ?? "key.bin";
    });

    // JSON
    builder.Services.ConfigureHttpJsonOptions(opts =>
    {
        opts.SerializerOptions.PropertyNamingPolicy =
            System.Text.Json.JsonNamingPolicy.CamelCase;
        opts.SerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

    var app = builder.Build();

    // Middleware
    app.UseSerilogRequestLogging();
    app.UseAuthentication();
    app.UseAuthorization();

    // Endpoints
    app.MapAccountEndpoints();
    app.MapThreadEndpoints();
    app.MapSmsEndpoints();
    app.MapCallEndpoints();

    app.Run();
}
catch (Exception ex) when (ex is not OperationCanceledException)
{
    Log.Fatal(ex, "Host terminated unexpectedly.");
}
finally
{
    Log.CloseAndFlush();
}

#pragma warning disable CA1050
public partial class Program { }
#pragma warning restore CA1050
```

- [ ] **Step 7: Update `appsettings.json`**

Replace the `GvResearch` section:

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
    "CookiePath": "",
    "KeyPath": ""
  }
}
```

- [ ] **Step 8: Build API project**

```bash
cd D:/prj/GVResearch && dotnet build src/GvResearch.Api/GvResearch.Api.csproj
```

Expected: Build succeeds.

- [ ] **Step 9: Commit**

```bash
cd D:/prj/GVResearch
git add src/GvResearch.Api/
git commit -m "feat(api): add Account, Thread, SMS endpoints + rewrite Calls

All endpoints delegate to IGvClient. Consistent error mapping:
GvAuthException->401, GvRateLimitException->429, GvApiException->502.
Delete old CallRecord model. Update Program.cs to use AddGvClient()."
```

---

## Task 9: Delete Old Auth Files

**Files:**
- Delete: `src/GvResearch.Shared/Auth/IGvTokenService.cs`
- Delete: `src/GvResearch.Shared/Auth/EncryptedFileTokenService.cs`

- [ ] **Step 1: Delete files**

```bash
cd D:/prj/GVResearch
git rm src/GvResearch.Shared/Auth/IGvTokenService.cs
git rm src/GvResearch.Shared/Auth/EncryptedFileTokenService.cs
```

- [ ] **Step 2: Remove old token service tests**

Delete or update `tests/GvResearch.Shared.Tests/Auth/EncryptedFileTokenServiceTests.cs` — these test the deleted class. Check if any content should be preserved for `GvAuthServiceTests`.

```bash
cd D:/prj/GVResearch && git rm tests/GvResearch.Shared.Tests/Auth/EncryptedFileTokenServiceTests.cs
```

- [ ] **Step 3: Build entire solution**

```bash
cd D:/prj/GVResearch && dotnet build
```

Expected: May fail if SIP project still references `IGvTokenService`. This is fixed in Task 10.

- [ ] **Step 4: Commit**

```bash
cd D:/prj/GVResearch
git add -A
git commit -m "chore: remove deprecated IGvTokenService and EncryptedFileTokenService

Replaced by IGvAuthService/GvAuthService in the new SDK."
```

---

## Task 10: Update SIP Consumer

**Files:**
- Modify: `src/GvResearch.Sip/Program.cs`
- Modify: `src/GvResearch.Sip/Calls/SipCallController.cs`

- [ ] **Step 1: Rewrite SIP `Program.cs`**

Replace the service registration section to use `AddGvClient()` and remove old `IGvTokenService`/`IGvCallService` registrations. The SIP-specific registrations (SIPTransport, RegistrationStore, etc.) stay.

Key changes in `Program.cs`:
- Remove `IGvTokenService` registration
- Remove `GvHttpClientHandler` manual registration
- Remove `IGvCallService` typed HTTP client
- Add `services.AddGvClient()` instead
- Keep all SIP-specific registrations

```csharp
// Replace the GV services section with:
services.AddGvClient(options =>
{
    var tokenPath = cfg["GvResearch:CookiePath"] ?? string.Empty;
    var keyPath = cfg["GvResearch:KeyPath"] ?? string.Empty;
    options.CookiePath = string.IsNullOrWhiteSpace(tokenPath) ? "cookies.enc" : tokenPath;
    options.KeyPath = string.IsNullOrWhiteSpace(keyPath) ? "key.bin" : keyPath;
});
```

- [ ] **Step 2: Update `SipCallController.cs`**

Change `IGvCallService` dependency to `IGvCallClient`:

```csharp
// Change field from:
private readonly IGvCallService _callService;
// To:
private readonly IGvCallClient _callClient;

// Update constructor parameter type and assignment
// Update CreateOutboundCallAsync to use:
var result = await _callClient.InitiateAsync(destinationNumber, cancellationToken);
// Note: IGvCallClient.InitiateAsync takes only toNumber (no fromNumber)
// The fromNumber is handled by the call transport

// Update HangupAsync to use:
await _callClient.HangupAsync(session.GvCallId, cancellationToken);

// Update result checking: GvCallResult now has .CallId instead of .GvCallId
session.GvCallId = result.CallId;
```

- [ ] **Step 3: Build SIP project**

```bash
cd D:/prj/GVResearch && dotnet build src/GvResearch.Sip/GvResearch.Sip.csproj
```

Expected: Build succeeds.

- [ ] **Step 4: Commit**

```bash
cd D:/prj/GVResearch
git add src/GvResearch.Sip/
git commit -m "refactor(sip): update to use IGvClient SDK

Replace IGvTokenService/IGvCallService with AddGvClient() registration.
Update SipCallController to use IGvCallClient."
```

---

## Task 11: Rewrite API Integration Tests

**Files:**
- Modify: `tests/GvResearch.Api.Tests/CallEndpointsTests.cs`

- [ ] **Step 1: Rewrite test factory and tests**

Replace the entire file:

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using FluentAssertions;
using GvResearch.Shared.Exceptions;
using GvResearch.Shared.Models;
using GvResearch.Shared.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace GvResearch.Api.Tests;

public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Test";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[] { new Claim(ClaimTypes.Name, "test-user") };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

public sealed class GvApiWebAppFactory : WebApplicationFactory<Program>
{
    public IGvClient Client { get; } = Substitute.For<IGvClient>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ConfigureServices(services =>
        {
            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName, _ => { });

            // Replace IGvClient with mock
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IGvClient));
            if (descriptor is not null) services.Remove(descriptor);
            services.AddSingleton(Client);
        });
    }
}

public sealed class ApiEndpointsTests : IClassFixture<GvApiWebAppFactory>
{
    private readonly GvApiWebAppFactory _factory;

    public ApiEndpointsTests(GvApiWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task GetAccount_ReturnsOk()
    {
        var account = new GvAccount(
            [new GvPhoneNumber("+15551234567", PhoneNumberType.GoogleVoice, true)],
            [], new GvSettings(false, null));
        _factory.Client.Account.GetAsync(Arg.Any<CancellationToken>()).Returns(account);

        using var client = _factory.CreateClient();
        var response = await client.GetAsync(new Uri("/api/v1/account", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetAccount_WhenAuthFails_Returns401()
    {
        _factory.Client.Account.GetAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new GvAuthException("Unauthorized"));

        using var client = _factory.CreateClient();
        var response = await client.GetAsync(new Uri("/api/v1/account", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ListThreads_ReturnsOk()
    {
        _factory.Client.Threads.ListAsync(Arg.Any<GvThreadListOptions>(), Arg.Any<CancellationToken>())
            .Returns(new GvThreadPage([], null, 0));

        using var client = _factory.CreateClient();
        var response = await client.GetAsync(new Uri("/api/v1/threads", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SendSms_WithValidRequest_ReturnsCreated()
    {
        _factory.Client.Sms.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GvSmsResult("t.+15551234567", true));

        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/sms", UriKind.Relative),
            new { ToNumber = "+15551234567", Message = "Hello!" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task SendSms_WithEmptyNumber_ReturnsBadRequest()
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/sms", UriKind.Relative),
            new { ToNumber = "", Message = "Hello!" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task InitiateCall_ReturnsCreated()
    {
        _factory.Client.Calls.InitiateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(GvCallResult.Ok("call-123"));

        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/calls", UriKind.Relative),
            new { ToNumber = "+15551234567" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task InitiateCall_WhenRateLimited_Returns429()
    {
        _factory.Client.Calls.InitiateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new GvRateLimitException("calls/initiate"));

        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/calls", UriKind.Relative),
            new { ToNumber = "+15551234567" });

        response.StatusCode.Should().Be((HttpStatusCode)429);
    }
}
```

- [ ] **Step 2: Run API tests**

```bash
cd D:/prj/GVResearch && dotnet test tests/GvResearch.Api.Tests --no-restore -v minimal
```

Expected: All tests pass.

- [ ] **Step 3: Commit**

```bash
cd D:/prj/GVResearch
git add tests/GvResearch.Api.Tests/
git commit -m "test(api): rewrite integration tests for IGvClient SDK

Mock IGvClient instead of IGvCallService. Add tests for Account,
Thread, SMS, and Call endpoints including error mapping (401/429/502)."
```

---

## Task 12: Full Solution Build & Test

- [ ] **Step 1: Build entire solution**

```bash
cd D:/prj/GVResearch && dotnet build
```

Expected: All projects build successfully.

- [ ] **Step 2: Run all tests**

```bash
cd D:/prj/GVResearch && dotnet test --no-restore -v minimal
```

Expected: All tests pass.

- [ ] **Step 3: Fix any remaining compilation errors**

If any project fails to build due to leftover references to deleted types (`IGvTokenService`, `IGvCallService`, `GvCallEvent`, `GvCallStatusType`, `GvCallId`), fix the references in the failing project.

- [ ] **Step 4: Commit any fixes**

```bash
cd D:/prj/GVResearch
git add -A
git commit -m "fix: resolve remaining compilation issues from SDK migration"
```

Only commit this if there were actual fixes needed. Skip if step 1 and 2 passed cleanly.

---

## Task 13: Update Documentation

**Files:**
- Modify: `CLAUDE.md`

- [ ] **Step 1: Update CLAUDE.md**

Update the "What Needs to Be Built" section to reflect completed work. Update the "Existing Code Status" section to reflect the new SDK architecture. Key changes:

- Update `GvHttpClientHandler.cs` description — now uses SAPISIDHASH
- Remove "placeholder endpoints" warning — endpoints are now real
- Update project structure to show new directories (Protocol/, Transport/, Exceptions/)
- Update the task list to show what's been completed and what remains (interactive login, SIP transport)

- [ ] **Step 2: Commit**

```bash
cd D:/prj/GVResearch
git add CLAUDE.md
git commit -m "docs: update CLAUDE.md to reflect SDK-first architecture"
```
