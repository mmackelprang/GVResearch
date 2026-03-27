# WebRTC Audio Channel & Signaler Client Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the real-time audio path — a signaler client for GV's long-poll push channel and a WebRTC call transport using SIPSorcery — enabling real VoIP calls without a browser.

**Architecture:** `GvSignalerClient` in `GvResearch.Shared` manages the long-poll push channel (session lifecycle, call signaling events, SDP exchange). `WebRtcCallTransport` in `GvResearch.Sip` implements `ICallTransport` using SIPSorcery's `RTCPeerConnection` for DTLS-SRTP media. The signaler carries SDP offers/answers; the transport handles media establishment.

**Tech Stack:** .NET 10, C# 13, SIPSorcery 10.0.3, xUnit + FluentAssertions + NSubstitute

**Spec:** `docs/superpowers/specs/2026-03-27-webrtc-signaler-design.md`

---

## File Map

### New files (GvResearch.Shared)
| File | Responsibility |
|------|---------------|
| `src/GvResearch.Shared/Signaler/SignalerEvent.cs` | Event type hierarchy (abstract base + 5 concrete records) |
| `src/GvResearch.Shared/Signaler/IGvSignalerClient.cs` | Interface: connect, disconnect, send SDP, events |
| `src/GvResearch.Shared/Signaler/SignalerMessageParser.cs` | Parse raw long-poll responses into typed events |
| `src/GvResearch.Shared/Signaler/GvSignalerClient.cs` | Implementation: session lifecycle, poll loop, reconnection |

### New files (GvResearch.Sip)
| File | Responsibility |
|------|---------------|
| `src/GvResearch.Sip/Transport/WebRtcCallSession.cs` | Per-call RTCPeerConnection + state + audio events |
| `src/GvResearch.Sip/Transport/WebRtcCallTransport.cs` | ICallTransport: outgoing/incoming calls via signaler + WebRTC |

### Modified files
| File | Change |
|------|--------|
| `src/GvResearch.Shared/Transport/ICallTransport.cs` | Add `IncomingCallReceived` event + `IncomingCallInfo` record |
| `src/GvResearch.Shared/Services/IGvClient.cs` | Add `IncomingCallReceived` to `IGvCallClient` |
| `src/GvResearch.Shared/Services/GvCallClient.cs` | Forward `IncomingCallReceived` from transport |
| `src/GvResearch.Shared/GvClientServiceExtensions.cs` | Register `"GvSignaler"` HttpClient, update NullCallTransport |
| `src/GvResearch.Sip/Program.cs` | Register `IGvSignalerClient` + `WebRtcCallTransport` |

### Deleted files
| File | Reason |
|------|--------|
| `src/GvResearch.Sip/Media/IGvAudioChannel.cs` | Replaced by WebRtcCallTransport |
| `src/GvResearch.Sip/Media/WebRtcGvAudioChannel.cs` | Replaced by WebRtcCallTransport |

### New test files
| File | Responsibility |
|------|---------------|
| `tests/GvResearch.Shared.Tests/Signaler/SignalerMessageParserTests.cs` | Parser unit tests |
| `tests/GvResearch.Shared.Tests/Signaler/GvSignalerClientTests.cs` | Client lifecycle + reconnection tests |
| `tests/GvResearch.Sip.Tests/Transport/WebRtcCallTransportTests.cs` | Transport tests with mocked signaler |

---

## Task 1: SignalerEvent Types

**Files:**
- Create: `src/GvResearch.Shared/Signaler/SignalerEvent.cs`

- [ ] **Step 1: Create `SignalerEvent.cs`**

```csharp
namespace GvResearch.Shared.Signaler;

public abstract record SignalerEvent(DateTimeOffset Timestamp);

public sealed record IncomingSdpOfferEvent(
    string CallId, string Sdp, DateTimeOffset Timestamp) : SignalerEvent(Timestamp);

public sealed record SdpAnswerEvent(
    string CallId, string Sdp, DateTimeOffset Timestamp) : SignalerEvent(Timestamp);

public sealed record CallHangupEvent(
    string CallId, DateTimeOffset Timestamp) : SignalerEvent(Timestamp);

public sealed record CallRingingEvent(
    string CallId, DateTimeOffset Timestamp) : SignalerEvent(Timestamp);

public sealed record UnknownEvent(
    string RawPayload, DateTimeOffset Timestamp) : SignalerEvent(Timestamp);
```

- [ ] **Step 2: Commit**

```bash
cd D:/prj/GVResearch
git add src/GvResearch.Shared/Signaler/SignalerEvent.cs
git commit -m "feat(signaler): add SignalerEvent type hierarchy

Abstract base + 5 concrete event records for call signaling:
IncomingSdpOfferEvent, SdpAnswerEvent, CallHangupEvent,
CallRingingEvent, UnknownEvent."
```

---

## Task 2: SignalerMessageParser + Tests

**Files:**
- Create: `src/GvResearch.Shared/Signaler/SignalerMessageParser.cs`
- Test: `tests/GvResearch.Shared.Tests/Signaler/SignalerMessageParserTests.cs`

The signaler returns data in Google's proprietary long-poll format: a length-prefixed series of numbered arrays. Each array contains event data. The exact format is based on captured traffic analysis.

- [ ] **Step 1: Write failing tests**

```csharp
using FluentAssertions;
using GvResearch.Shared.Signaler;

namespace GvResearch.Shared.Tests.Signaler;

public sealed class SignalerMessageParserTests
{
    [Fact]
    public void Parse_SdpOfferEvent_ReturnsIncomingSdpOfferEvent()
    {
        // Signaler wraps events in numbered array format:
        // [[<arrayId>,[["noop"],["sdp-offer",<callId>,<sdp>]]]]
        var raw = """[[1,[["sdp-offer","call-123","v=0\r\no=xavier 123 1 IN IP4 74.125.39.157\r\n"]]]]""";

        var events = SignalerMessageParser.Parse(raw);

        events.Should().ContainSingle();
        var offer = events[0].Should().BeOfType<IncomingSdpOfferEvent>().Subject;
        offer.CallId.Should().Be("call-123");
        offer.Sdp.Should().Contain("xavier");
    }

    [Fact]
    public void Parse_SdpAnswerEvent_ReturnsSdpAnswerEvent()
    {
        var raw = """[[2,[["sdp-answer","call-456","v=0\r\no=- 123 2 IN IP4 127.0.0.1\r\n"]]]]""";

        var events = SignalerMessageParser.Parse(raw);

        events.Should().ContainSingle();
        var answer = events[0].Should().BeOfType<SdpAnswerEvent>().Subject;
        answer.CallId.Should().Be("call-456");
    }

    [Fact]
    public void Parse_HangupEvent_ReturnsCallHangupEvent()
    {
        var raw = """[[3,[["hangup","call-789"]]]]""";

        var events = SignalerMessageParser.Parse(raw);

        events.Should().ContainSingle();
        var hangup = events[0].Should().BeOfType<CallHangupEvent>().Subject;
        hangup.CallId.Should().Be("call-789");
    }

    [Fact]
    public void Parse_RingingEvent_ReturnsCallRingingEvent()
    {
        var raw = """[[4,[["ringing","call-101"]]]]""";

        var events = SignalerMessageParser.Parse(raw);

        events.Should().ContainSingle();
        events[0].Should().BeOfType<CallRingingEvent>()
            .Which.CallId.Should().Be("call-101");
    }

    [Fact]
    public void Parse_UnknownEvent_ReturnsUnknownEvent()
    {
        var raw = """[[5,[["noop"]]]]""";

        var events = SignalerMessageParser.Parse(raw);

        events.Should().ContainSingle();
        events[0].Should().BeOfType<UnknownEvent>();
    }

    [Fact]
    public void Parse_MultipleEvents_ReturnsAll()
    {
        var raw = """[[1,[["ringing","c1"]]],[2,[["sdp-offer","c1","sdp-data"]]]]""";

        var events = SignalerMessageParser.Parse(raw);

        events.Should().HaveCount(2);
        events[0].Should().BeOfType<CallRingingEvent>();
        events[1].Should().BeOfType<IncomingSdpOfferEvent>();
    }

    [Fact]
    public void Parse_EmptyResponse_ReturnsEmpty()
    {
        var events = SignalerMessageParser.Parse("");
        events.Should().BeEmpty();
    }

    [Fact]
    public void Parse_NullResponse_ReturnsEmpty()
    {
        var events = SignalerMessageParser.Parse(null!);
        events.Should().BeEmpty();
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
cd D:/prj/GVResearch && dotnet test tests/GvResearch.Shared.Tests --filter "FullyQualifiedName~SignalerMessageParserTests" -v minimal
```

Expected: FAIL — `SignalerMessageParser` does not exist.

- [ ] **Step 3: Create `SignalerMessageParser.cs`**

```csharp
using System.Text.Json;

namespace GvResearch.Shared.Signaler;

public static class SignalerMessageParser
{
    public static IReadOnlyList<SignalerEvent> Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return [];

        var events = new List<SignalerEvent>();
        var now = DateTimeOffset.UtcNow;

        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Array)
                return [];

            foreach (var entry in root.EnumerateArray())
            {
                // Each entry: [<arrayId>, [<event1>, <event2>, ...]]
                if (entry.ValueKind != JsonValueKind.Array || entry.GetArrayLength() < 2)
                    continue;

                var eventArray = entry[1];
                if (eventArray.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var evt in eventArray.EnumerateArray())
                {
                    if (evt.ValueKind != JsonValueKind.Array || evt.GetArrayLength() < 1)
                        continue;

                    var eventType = evt[0].GetString();
                    var parsed = eventType switch
                    {
                        "sdp-offer" when evt.GetArrayLength() >= 3 =>
                            (SignalerEvent)new IncomingSdpOfferEvent(
                                evt[1].GetString() ?? string.Empty,
                                evt[2].GetString() ?? string.Empty,
                                now),

                        "sdp-answer" when evt.GetArrayLength() >= 3 =>
                            new SdpAnswerEvent(
                                evt[1].GetString() ?? string.Empty,
                                evt[2].GetString() ?? string.Empty,
                                now),

                        "hangup" when evt.GetArrayLength() >= 2 =>
                            new CallHangupEvent(
                                evt[1].GetString() ?? string.Empty,
                                now),

                        "ringing" when evt.GetArrayLength() >= 2 =>
                            new CallRingingEvent(
                                evt[1].GetString() ?? string.Empty,
                                now),

                        _ => new UnknownEvent(evt.ToString(), now),
                    };

                    events.Add(parsed);
                }
            }
        }
        catch (JsonException)
        {
            // Malformed response — return what we have so far
        }

        return events;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
cd D:/prj/GVResearch && dotnet test tests/GvResearch.Shared.Tests --filter "FullyQualifiedName~SignalerMessageParserTests" -v minimal
```

Expected: 8 tests pass.

- [ ] **Step 5: Commit**

```bash
cd D:/prj/GVResearch
git add src/GvResearch.Shared/Signaler/SignalerMessageParser.cs tests/GvResearch.Shared.Tests/Signaler/SignalerMessageParserTests.cs
git commit -m "feat(signaler): add SignalerMessageParser with tests

Parses Google's proprietary long-poll response format into typed
SignalerEvent records. Handles SDP offer, answer, hangup, ringing,
and unknown event types."
```

---

## Task 3: IGvSignalerClient Interface

**Files:**
- Create: `src/GvResearch.Shared/Signaler/IGvSignalerClient.cs`

- [ ] **Step 1: Create `IGvSignalerClient.cs`**

```csharp
namespace GvResearch.Shared.Signaler;

public interface IGvSignalerClient : IAsyncDisposable
{
    event Action<SignalerEvent>? EventReceived;
    event Action<Exception>? ErrorOccurred;

    Task ConnectAsync(CancellationToken ct = default);
    Task DisconnectAsync(CancellationToken ct = default);
    bool IsConnected { get; }

    Task SendSdpOfferAsync(string callId, string sdp, CancellationToken ct = default);
    Task SendSdpAnswerAsync(string callId, string sdp, CancellationToken ct = default);
    Task SendHangupAsync(string callId, CancellationToken ct = default);
}
```

- [ ] **Step 2: Commit**

```bash
cd D:/prj/GVResearch
git add src/GvResearch.Shared/Signaler/IGvSignalerClient.cs
git commit -m "feat(signaler): add IGvSignalerClient interface

Connect/disconnect, send SDP offer/answer/hangup, event callbacks."
```

---

## Task 4: GvSignalerClient Implementation + Tests

**Files:**
- Create: `src/GvResearch.Shared/Signaler/GvSignalerClient.cs`
- Test: `tests/GvResearch.Shared.Tests/Signaler/GvSignalerClientTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
using System.Net;
using FluentAssertions;
using GvResearch.Shared.Auth;
using GvResearch.Shared.Signaler;
using NSubstitute;

namespace GvResearch.Shared.Tests.Signaler;

public sealed class GvSignalerClientTests : IAsyncDisposable
{
    private readonly IGvAuthService _authService = Substitute.For<IGvAuthService>();
    private readonly GvApiConfig _apiConfig = new() { ApiKey = "test-key" };

    public GvSignalerClientTests()
    {
        _authService.GetValidCookiesAsync(Arg.Any<CancellationToken>())
            .Returns(new GvCookieSet
            {
                Sapisid = "test", Sid = "s", Hsid = "h", Ssid = "ss", Apisid = "a"
            });
    }

    [Fact]
    public async Task ConnectAsync_SetsIsConnected()
    {
        var handler = new FakeSignalerHandler();
        handler.EnqueueResponse(HttpStatusCode.OK, """{"server":"test-server"}"""); // chooseServer
        handler.EnqueueResponse(HttpStatusCode.OK, """[[0,["c","sid-123","",[8]]]]"""); // channel open -> SID
        handler.EnqueueLongPollBlock(); // poll blocks forever

        using var httpClient = new HttpClient(handler)
            { BaseAddress = new Uri("https://signaler-pa.clients6.google.com") };
        var factory = CreateFactory(httpClient);
        await using var sut = new GvSignalerClient(factory, _authService, _apiConfig);

        await sut.ConnectAsync();

        sut.IsConnected.Should().BeTrue();
    }

    [Fact]
    public async Task ConnectAsync_ThenDisconnect_SetsIsConnectedFalse()
    {
        var handler = new FakeSignalerHandler();
        handler.EnqueueResponse(HttpStatusCode.OK, """{"server":"test-server"}""");
        handler.EnqueueResponse(HttpStatusCode.OK, """[[0,["c","sid-456","",[8]]]]""");
        handler.EnqueueLongPollBlock();

        using var httpClient = new HttpClient(handler)
            { BaseAddress = new Uri("https://signaler-pa.clients6.google.com") };
        var factory = CreateFactory(httpClient);
        await using var sut = new GvSignalerClient(factory, _authService, _apiConfig);

        await sut.ConnectAsync();
        await sut.DisconnectAsync();

        sut.IsConnected.Should().BeFalse();
    }

    [Fact]
    public async Task PollLoop_FiresEventReceived()
    {
        var handler = new FakeSignalerHandler();
        handler.EnqueueResponse(HttpStatusCode.OK, """{"server":"s"}""");
        handler.EnqueueResponse(HttpStatusCode.OK, """[[0,["c","sid-789","",[8]]]]""");
        // First poll returns an SDP offer event
        handler.EnqueueResponse(HttpStatusCode.OK,
            """[[1,[["sdp-offer","call-1","v=0\r\n"]]]]""");
        handler.EnqueueLongPollBlock(); // subsequent polls block

        using var httpClient = new HttpClient(handler)
            { BaseAddress = new Uri("https://signaler-pa.clients6.google.com") };
        var factory = CreateFactory(httpClient);
        await using var sut = new GvSignalerClient(factory, _authService, _apiConfig);

        SignalerEvent? received = null;
        sut.EventReceived += evt => received = evt;

        await sut.ConnectAsync();

        // Give poll loop time to process
        await Task.Delay(200);

        received.Should().NotBeNull();
        received.Should().BeOfType<IncomingSdpOfferEvent>()
            .Which.CallId.Should().Be("call-1");
    }

    [Fact]
    public async Task SendSdpOfferAsync_PostsToChannel()
    {
        var handler = new FakeSignalerHandler();
        handler.EnqueueResponse(HttpStatusCode.OK, """{"server":"s"}""");
        handler.EnqueueResponse(HttpStatusCode.OK, """[[0,["c","sid-send","",[8]]]]""");
        handler.EnqueueLongPollBlock();
        handler.EnqueueResponse(HttpStatusCode.OK, ""); // response to send

        using var httpClient = new HttpClient(handler)
            { BaseAddress = new Uri("https://signaler-pa.clients6.google.com") };
        var factory = CreateFactory(httpClient);
        await using var sut = new GvSignalerClient(factory, _authService, _apiConfig);

        await sut.ConnectAsync();
        await sut.SendSdpOfferAsync("call-x", "v=0\r\n");

        handler.SentRequests.Should().Contain(r =>
            r.Method == HttpMethod.Post &&
            r.RequestUri!.ToString().Contains("sid-send"));
    }

    private static IHttpClientFactory CreateFactory(HttpClient client)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("GvSignaler").Returns(client);
        return factory;
    }

    public async ValueTask DisposeAsync()
    {
        // Tests clean up via using/await using
        await Task.CompletedTask;
    }
}

/// <summary>
/// Fake HTTP handler that queues responses and can block to simulate long-poll.
/// </summary>
internal sealed class FakeSignalerHandler : HttpMessageHandler
{
    private readonly Queue<(HttpStatusCode Code, string Body)> _responses = new();
    private readonly TaskCompletionSource _longPollBlock = new();
    public List<HttpRequestMessage> SentRequests { get; } = [];

    public void EnqueueResponse(HttpStatusCode code, string body) =>
        _responses.Enqueue((code, body));

    public void EnqueueLongPollBlock() { } // Long poll will block on _longPollBlock

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        SentRequests.Add(request);

        if (_responses.Count > 0)
        {
            var (code, body) = _responses.Dequeue();
            return new HttpResponseMessage(code) { Content = new StringContent(body) };
        }

        // Simulate long-poll: block until cancelled
        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("[]")
        };
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
cd D:/prj/GVResearch && dotnet test tests/GvResearch.Shared.Tests --filter "FullyQualifiedName~GvSignalerClientTests" -v minimal
```

Expected: FAIL — `GvSignalerClient` does not exist.

- [ ] **Step 3: Create `GvSignalerClient.cs`**

```csharp
using System.Text;
using GvResearch.Shared.Auth;
using Microsoft.Extensions.DependencyInjection;

namespace GvResearch.Shared.Signaler;

public sealed class GvSignalerClient : IGvSignalerClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IGvAuthService _authService;
    private readonly GvApiConfig _apiConfig;

    private string? _sessionId;
    private int _aid; // acknowledgment ID
    private int _rid; // request ID (for outgoing messages)
    private CancellationTokenSource? _pollCts;
    private Task? _pollTask;

    public event Action<SignalerEvent>? EventReceived;
    public event Action<Exception>? ErrorOccurred;
    public bool IsConnected { get; private set; }

    public GvSignalerClient(
        IHttpClientFactory httpClientFactory,
        IGvAuthService authService,
        GvApiConfig apiConfig)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(authService);
        ArgumentNullException.ThrowIfNull(apiConfig);

        _httpClientFactory = httpClientFactory;
        _authService = authService;
        _apiConfig = apiConfig;
    }

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        var client = CreateClient();

        // 1. Choose server
        var chooseResponse = await client
            .PostAsync(
                new Uri("punctual/v1/chooseServer", UriKind.Relative),
                new StringContent("{}", Encoding.UTF8, "application/json"),
                ct)
            .ConfigureAwait(false);
        chooseResponse.EnsureSuccessStatusCode();

        // 2. Open channel — get SID
        _rid = 0;
        var openUrl = BuildChannelUrl(rid: _rid);
        Interlocked.Increment(ref _rid);

        var openResponse = await client
            .PostAsync(new Uri(openUrl, UriKind.Relative), null, ct)
            .ConfigureAwait(false);
        openResponse.EnsureSuccessStatusCode();

        var openBody = await openResponse.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        _sessionId = ExtractSessionId(openBody);
        _aid = 0;

        // 3. Start poll loop
        _pollCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _pollTask = PollLoopAsync(_pollCts.Token);
        IsConnected = true;
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        IsConnected = false;

        if (_pollCts is not null)
        {
            await _pollCts.CancelAsync().ConfigureAwait(false);
        }

        if (_pollTask is not null)
        {
            try
            {
                await _pollTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }

        _pollCts?.Dispose();
        _pollCts = null;
        _pollTask = null;
        _sessionId = null;
    }

    public async Task SendSdpOfferAsync(string callId, string sdp, CancellationToken ct = default)
    {
        await SendMessageAsync("sdp-offer", callId, sdp, ct).ConfigureAwait(false);
    }

    public async Task SendSdpAnswerAsync(string callId, string sdp, CancellationToken ct = default)
    {
        await SendMessageAsync("sdp-answer", callId, sdp, ct).ConfigureAwait(false);
    }

    public async Task SendHangupAsync(string callId, CancellationToken ct = default)
    {
        await SendMessageAsync("hangup", callId, null, ct).ConfigureAwait(false);
    }

    private async Task SendMessageAsync(string type, string callId, string? data, CancellationToken ct)
    {
        if (_sessionId is null)
            throw new InvalidOperationException("Not connected. Call ConnectAsync first.");

        var client = CreateClient();
        var rid = Interlocked.Increment(ref _rid);
        var url = BuildChannelUrl(rid: rid);

        var body = data is not null
            ? $"count=1&ofs=0&req0_type={Uri.EscapeDataString(type)}&req0_callid={Uri.EscapeDataString(callId)}&req0_data={Uri.EscapeDataString(data)}"
            : $"count=1&ofs=0&req0_type={Uri.EscapeDataString(type)}&req0_callid={Uri.EscapeDataString(callId)}";

        using var content = new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded");
        var response = await client
            .PostAsync(new Uri(url, UriKind.Relative), content, ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    private async Task PollLoopAsync(CancellationToken ct)
    {
        var backoff = TimeSpan.FromSeconds(1);
        var maxBackoff = TimeSpan.FromSeconds(30);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var client = CreateClient();
                var url = BuildChannelUrl(rid: "rpc", type: "xmlhttp");

                var response = await client
                    .GetAsync(new Uri(url, UriKind.Relative), ct)
                    .ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    ErrorOccurred?.Invoke(new HttpRequestException(
                        $"Signaler poll returned {(int)response.StatusCode}"));
                    await Task.Delay(backoff, ct).ConfigureAwait(false);
                    backoff = TimeSpan.FromSeconds(Math.Min(backoff.TotalSeconds * 2, maxBackoff.TotalSeconds));
                    continue;
                }

                var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                var events = SignalerMessageParser.Parse(body);

                foreach (var evt in events)
                {
                    EventReceived?.Invoke(evt);
                }

                Interlocked.Increment(ref _aid);
                backoff = TimeSpan.FromSeconds(1); // Reset on success
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(ex);
                await Task.Delay(backoff, ct).ConfigureAwait(false);
                backoff = TimeSpan.FromSeconds(Math.Min(backoff.TotalSeconds * 2, maxBackoff.TotalSeconds));
            }
        }
    }

    private string BuildChannelUrl(object? rid = null, string? type = null)
    {
        var sb = new StringBuilder("punctual/multi-watch/channel?VER=8&CVER=22");
        sb.Append("&key=").Append(Uri.EscapeDataString(_apiConfig.ApiKey));

        if (_sessionId is not null)
            sb.Append("&SID=").Append(Uri.EscapeDataString(_sessionId));

        sb.Append("&AID=").Append(_aid);
        sb.Append("&RID=").Append(rid ?? _rid);

        if (type is not null)
            sb.Append("&TYPE=").Append(type);

        return sb.ToString();
    }

    private static string ExtractSessionId(string openResponse)
    {
        // Response format: [[0,["c","<SID>","",<options>]]]
        // Extract SID from position [0][1][1]
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(openResponse);
            return doc.RootElement[0][1][1].GetString()
                ?? throw new InvalidOperationException("SID not found in channel open response.");
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                $"Failed to parse channel open response: {openResponse}", ex);
        }
    }

    private HttpClient CreateClient() =>
        _httpClientFactory.CreateClient("GvSignaler");

    public async ValueTask DisposeAsync()
    {
        if (IsConnected)
        {
            await DisconnectAsync().ConfigureAwait(false);
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
cd D:/prj/GVResearch && dotnet test tests/GvResearch.Shared.Tests --filter "FullyQualifiedName~GvSignalerClientTests" -v minimal
```

Expected: 4 tests pass.

- [ ] **Step 5: Commit**

```bash
cd D:/prj/GVResearch
git add src/GvResearch.Shared/Signaler/GvSignalerClient.cs tests/GvResearch.Shared.Tests/Signaler/
git commit -m "feat(signaler): add GvSignalerClient with long-poll loop

Session lifecycle: chooseServer -> open channel -> poll loop.
Exponential backoff reconnection (1s to 30s). Send SDP offer/answer/hangup
via channel POST with incrementing RID."
```

---

## Task 5: Update ICallTransport + IGvCallClient for Incoming Calls

**Files:**
- Modify: `src/GvResearch.Shared/Transport/ICallTransport.cs`
- Modify: `src/GvResearch.Shared/Services/IGvClient.cs`
- Modify: `src/GvResearch.Shared/Services/GvCallClient.cs`
- Modify: `src/GvResearch.Shared/GvClientServiceExtensions.cs`

- [ ] **Step 1: Update `ICallTransport.cs`**

Add `IncomingCallReceived` event and `IncomingCallInfo` record:

```csharp
using GvResearch.Shared.Models;

namespace GvResearch.Shared.Transport;

public interface ICallTransport : IAsyncDisposable
{
    event Action<IncomingCallInfo>? IncomingCallReceived;

    Task<TransportCallResult> InitiateAsync(string toNumber, CancellationToken ct = default);
    Task<TransportCallStatus> GetStatusAsync(string callId, CancellationToken ct = default);
    Task HangupAsync(string callId, CancellationToken ct = default);
}

public sealed record TransportCallResult(string CallId, bool Success, string? ErrorMessage);
public sealed record TransportCallStatus(string CallId, CallStatusType Status);
public sealed record IncomingCallInfo(string CallId, string CallerNumber);
```

- [ ] **Step 2: Update `IGvClient.cs` — add event to `IGvCallClient`**

Add to the `IGvCallClient` interface:

```csharp
public interface IGvCallClient
{
    event Action<IncomingCallInfo>? IncomingCallReceived;

    Task<GvCallResult> InitiateAsync(string toNumber, CancellationToken ct = default);
    Task<GvCallStatus> GetStatusAsync(string callId, CancellationToken ct = default);
    Task HangupAsync(string callId, CancellationToken ct = default);
}
```

Add `using GvResearch.Shared.Transport;` to the file's usings.

- [ ] **Step 3: Update `GvCallClient.cs` — forward the event**

Add event forwarding in the constructor:

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

    public event Action<IncomingCallInfo>? IncomingCallReceived;

    public GvCallClient(ICallTransport transport, GvRateLimiter rateLimiter)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(rateLimiter);
        _transport = transport;
        _rateLimiter = rateLimiter;
        _transport.IncomingCallReceived += info => IncomingCallReceived?.Invoke(info);
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

- [ ] **Step 4: Update `NullCallTransport` in `GvClientServiceExtensions.cs`**

Add the event (never fires):

```csharp
internal sealed class NullCallTransport : ICallTransport
{
    public event Action<IncomingCallInfo>? IncomingCallReceived;

    public Task<TransportCallResult> InitiateAsync(string toNumber, CancellationToken ct) =>
        throw new NotImplementedException("No call transport configured.");

    public Task<TransportCallStatus> GetStatusAsync(string callId, CancellationToken ct) =>
        throw new NotImplementedException("No call transport configured.");

    public Task HangupAsync(string callId, CancellationToken ct) =>
        throw new NotImplementedException("No call transport configured.");

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
```

Also add the `"GvSignaler"` named HttpClient registration inside `AddGvClient()`, after the `"GvApi"` registration:

```csharp
services.AddHttpClient("GvSignaler", client =>
{
    client.BaseAddress = new Uri("https://signaler-pa.clients6.google.com");
    client.Timeout = TimeSpan.FromMinutes(5);
});
```

- [ ] **Step 5: Build to verify**

```bash
cd D:/prj/GVResearch && dotnet build src/GvResearch.Shared/GvResearch.Shared.csproj
```

Expected: Build succeeds.

- [ ] **Step 6: Run existing tests**

```bash
cd D:/prj/GVResearch && dotnet test tests/GvResearch.Shared.Tests -v minimal
```

Expected: All pass (the existing `GvCallClientTests` may need updating for the new event — check if NSubstitute handles it).

- [ ] **Step 7: Commit**

```bash
cd D:/prj/GVResearch
git add src/GvResearch.Shared/Transport/ICallTransport.cs src/GvResearch.Shared/Services/IGvClient.cs src/GvResearch.Shared/Services/GvCallClient.cs src/GvResearch.Shared/GvClientServiceExtensions.cs
git commit -m "feat(sdk): add IncomingCallReceived event to ICallTransport + IGvCallClient

Enables incoming call notifications from transport to SDK consumers.
Register GvSignaler named HttpClient in AddGvClient().
NullCallTransport gets no-op event."
```

---

## Task 6: WebRtcCallSession

**Files:**
- Create: `src/GvResearch.Sip/Transport/WebRtcCallSession.cs`

- [ ] **Step 1: Create `WebRtcCallSession.cs`**

```csharp
using System.Net;
using GvResearch.Shared.Models;
using SIPSorcery.Net;

namespace GvResearch.Sip.Transport;

internal sealed class WebRtcCallSession : IDisposable
{
    private static readonly RTCConfiguration StunConfig = new()
    {
        iceServers = [new RTCIceServer { urls = "stun:stun.l.google.com:19302" }]
    };

    private bool _disposed;

    public string CallId { get; }
    public RTCPeerConnection PeerConnection { get; }
    public CallStatusType Status { get; private set; } = CallStatusType.Unknown;

    public event Action<RTPPacket>? AudioReceived;

    public WebRtcCallSession(string callId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(callId);
        CallId = callId;

        PeerConnection = new RTCPeerConnection(StunConfig);

        var audioTrack = new MediaStreamTrack(
            SDPMediaTypesEnum.audio,
            false,
            new List<SDPAudioVideoMediaFormat>
            {
                new(SDPWellKnownMediaFormatsEnum.OPUS),
                new(SDPWellKnownMediaFormatsEnum.G722),
                new(SDPWellKnownMediaFormatsEnum.PCMU),
                new(SDPWellKnownMediaFormatsEnum.PCMA),
            });
        PeerConnection.addTrack(audioTrack);

        PeerConnection.OnRtpPacketReceived += OnRtpPacketReceived;

        PeerConnection.onconnectionstatechange += state =>
        {
            Status = state switch
            {
                RTCPeerConnectionState.connecting => CallStatusType.Ringing,
                RTCPeerConnectionState.connected => CallStatusType.Active,
                RTCPeerConnectionState.closed => CallStatusType.Completed,
                RTCPeerConnectionState.failed => CallStatusType.Failed,
                _ => Status,
            };
        };
    }

    public void SendAudio(RTPPacket packet)
    {
        if (_disposed || PeerConnection.connectionState != RTCPeerConnectionState.connected)
            return;

        PeerConnection.SendRtpRaw(
            SDPMediaTypesEnum.audio,
            packet.Payload,
            packet.Header.Timestamp,
            packet.Header.MarkerBit,
            packet.Header.PayloadType);
    }

    public void UpdateStatus(CallStatusType status)
    {
        Status = status;
    }

    private void OnRtpPacketReceived(IPEndPoint remoteEndPoint, SDPMediaTypesEnum mediaType, RTPPacket packet)
    {
        if (mediaType == SDPMediaTypesEnum.audio)
        {
            AudioReceived?.Invoke(packet);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        PeerConnection.OnRtpPacketReceived -= OnRtpPacketReceived;
        PeerConnection.close();
    }
}
```

- [ ] **Step 2: Commit**

```bash
cd D:/prj/GVResearch
git add src/GvResearch.Sip/Transport/WebRtcCallSession.cs
git commit -m "feat(webrtc): add WebRtcCallSession per-call state

Wraps RTCPeerConnection with STUN config, Opus/G722/PCMU/PCMA codecs,
audio packet events, and connection state tracking."
```

---

## Task 7: WebRtcCallTransport + Tests

**Files:**
- Create: `src/GvResearch.Sip/Transport/WebRtcCallTransport.cs`
- Test: `tests/GvResearch.Sip.Tests/Transport/WebRtcCallTransportTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
using FluentAssertions;
using GvResearch.Shared.Models;
using GvResearch.Shared.Signaler;
using GvResearch.Shared.Transport;
using NSubstitute;

namespace GvResearch.Sip.Tests.Transport;

public sealed class WebRtcCallTransportTests : IAsyncDisposable
{
    private readonly IGvSignalerClient _signaler = Substitute.For<IGvSignalerClient>();

    [Fact]
    public async Task InitiateAsync_SendsSdpOfferViaSignaler()
    {
        await using var sut = new GvResearch.Sip.Transport.WebRtcCallTransport(_signaler);

        // Start initiation (don't await — it waits for SDP answer)
        var initiateTask = sut.InitiateAsync("+15551234567");

        // Give it time to create offer and send
        await Task.Delay(500);

        await _signaler.Received(1).SendSdpOfferAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());

        // Simulate answer to unblock
        _signaler.EventReceived += Raise.Event<Action<SignalerEvent>>(
            new SdpAnswerEvent("pending-call-id", "v=0\r\n", DateTimeOffset.UtcNow));

        // Cancel if still waiting
        if (!initiateTask.IsCompleted)
        {
            // Transport may timeout — that's OK for this test
        }
    }

    [Fact]
    public async Task HangupAsync_SendsHangupViaSignaler()
    {
        await using var sut = new GvResearch.Sip.Transport.WebRtcCallTransport(_signaler);

        await sut.HangupAsync("call-123");

        await _signaler.Received(1).SendHangupAsync("call-123", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetStatusAsync_ReturnsUnknownForNonexistentCall()
    {
        await using var sut = new GvResearch.Sip.Transport.WebRtcCallTransport(_signaler);

        var status = await sut.GetStatusAsync("nonexistent");

        status.Status.Should().Be(CallStatusType.Unknown);
    }

    [Fact]
    public async Task IncomingSdpOffer_FiresIncomingCallReceived()
    {
        await using var sut = new GvResearch.Sip.Transport.WebRtcCallTransport(_signaler);

        IncomingCallInfo? received = null;
        sut.IncomingCallReceived += info => received = info;

        // Simulate incoming SDP offer from signaler
        _signaler.EventReceived += Raise.Event<Action<SignalerEvent>>(
            new IncomingSdpOfferEvent("incoming-1", "v=0\r\no=xavier 123 1 IN IP4 74.125.39.157\r\n", DateTimeOffset.UtcNow));

        await Task.Delay(200);

        received.Should().NotBeNull();
        received!.CallId.Should().Be("incoming-1");
    }

    public async ValueTask DisposeAsync()
    {
        await Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
cd D:/prj/GVResearch && dotnet test tests/GvResearch.Sip.Tests --filter "FullyQualifiedName~WebRtcCallTransportTests" -v minimal
```

Expected: FAIL — `WebRtcCallTransport` does not exist.

- [ ] **Step 3: Create `WebRtcCallTransport.cs`**

```csharp
using System.Collections.Concurrent;
using GvResearch.Shared.Models;
using GvResearch.Shared.Signaler;
using GvResearch.Shared.Transport;
using SIPSorcery.Net;

namespace GvResearch.Sip.Transport;

public sealed class WebRtcCallTransport : ICallTransport
{
    private readonly IGvSignalerClient _signaler;
    private readonly ConcurrentDictionary<string, WebRtcCallSession> _activeCalls = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _pendingAnswers = new();

    public event Action<IncomingCallInfo>? IncomingCallReceived;

    public WebRtcCallTransport(IGvSignalerClient signaler)
    {
        ArgumentNullException.ThrowIfNull(signaler);
        _signaler = signaler;
        _signaler.EventReceived += OnSignalerEvent;
    }

    public async Task<TransportCallResult> InitiateAsync(string toNumber, CancellationToken ct = default)
    {
        var callId = $"out-{Guid.NewGuid():N}";
        var session = new WebRtcCallSession(callId);
        _activeCalls[callId] = session;

        try
        {
            // Create SDP offer
            var offer = session.PeerConnection.createOffer();
            await session.PeerConnection.setLocalDescription(offer).ConfigureAwait(false);
            var sdp = offer.sdp;

            // Wait for answer
            var answerTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingAnswers[callId] = answerTcs;

            // Send offer via signaler
            await _signaler.SendSdpOfferAsync(callId, sdp, ct).ConfigureAwait(false);

            // Wait for SDP answer (with timeout)
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));

            string answerSdp;
            try
            {
                answerSdp = await answerTcs.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
            }
            finally
            {
                _pendingAnswers.TryRemove(callId, out _);
            }

            // Set remote description
            var answer = new RTCSessionDescriptionInit
            {
                type = RTCSdpType.answer,
                sdp = answerSdp
            };
            var setResult = session.PeerConnection.setRemoteDescription(answer);
            if (setResult != SetDescriptionResultEnum.OK)
            {
                session.Dispose();
                _activeCalls.TryRemove(callId, out _);
                return new TransportCallResult(callId, false, $"Failed to set remote SDP: {setResult}");
            }

            session.UpdateStatus(CallStatusType.Ringing);
            return new TransportCallResult(callId, true, null);
        }
        catch (Exception ex)
        {
            session.Dispose();
            _activeCalls.TryRemove(callId, out _);
            return new TransportCallResult(callId, false, ex.Message);
        }
    }

    public Task<TransportCallStatus> GetStatusAsync(string callId, CancellationToken ct = default)
    {
        var status = _activeCalls.TryGetValue(callId, out var session)
            ? session.Status
            : CallStatusType.Unknown;

        return Task.FromResult(new TransportCallStatus(callId, status));
    }

    public async Task HangupAsync(string callId, CancellationToken ct = default)
    {
        await _signaler.SendHangupAsync(callId, ct).ConfigureAwait(false);

        if (_activeCalls.TryRemove(callId, out var session))
        {
            session.Dispose();
        }
    }

    private void OnSignalerEvent(SignalerEvent evt)
    {
        switch (evt)
        {
            case IncomingSdpOfferEvent offer:
                HandleIncomingSdpOffer(offer);
                break;

            case SdpAnswerEvent answer:
                HandleSdpAnswer(answer);
                break;

            case CallHangupEvent hangup:
                HandleRemoteHangup(hangup);
                break;
        }
    }

    private void HandleIncomingSdpOffer(IncomingSdpOfferEvent offer)
    {
        // Check if this is a renegotiation for an existing call
        if (_activeCalls.TryGetValue(offer.CallId, out var existingSession))
        {
            // Renegotiation — Google re-offers ~6s after incoming call connects
            _ = HandleRenegotiationAsync(existingSession, offer);
            return;
        }

        // New incoming call
        _ = HandleNewIncomingCallAsync(offer);
    }

    private async Task HandleNewIncomingCallAsync(IncomingSdpOfferEvent offer)
    {
        var session = new WebRtcCallSession(offer.CallId);
        _activeCalls[offer.CallId] = session;

        try
        {
            // Set remote description (Google's offer)
            var remoteDesc = new RTCSessionDescriptionInit
            {
                type = RTCSdpType.offer,
                sdp = offer.Sdp
            };
            session.PeerConnection.setRemoteDescription(remoteDesc);

            // Create answer
            var answer = session.PeerConnection.createAnswer();
            await session.PeerConnection.setLocalDescription(answer).ConfigureAwait(false);

            // Send answer via signaler
            await _signaler.SendSdpAnswerAsync(offer.CallId, answer.sdp).ConfigureAwait(false);

            session.UpdateStatus(CallStatusType.Ringing);

            // Notify consumers
            IncomingCallReceived?.Invoke(new IncomingCallInfo(offer.CallId, "unknown"));
        }
        catch
        {
            session.Dispose();
            _activeCalls.TryRemove(offer.CallId, out _);
        }
    }

    private async Task HandleRenegotiationAsync(WebRtcCallSession session, IncomingSdpOfferEvent offer)
    {
        try
        {
            var remoteDesc = new RTCSessionDescriptionInit
            {
                type = RTCSdpType.offer,
                sdp = offer.Sdp
            };
            session.PeerConnection.setRemoteDescription(remoteDesc);

            var answer = session.PeerConnection.createAnswer();
            await session.PeerConnection.setLocalDescription(answer).ConfigureAwait(false);

            await _signaler.SendSdpAnswerAsync(offer.CallId, answer.sdp).ConfigureAwait(false);
        }
        catch
        {
            // Renegotiation failure is non-fatal — audio may continue on existing path
        }
    }

    private void HandleSdpAnswer(SdpAnswerEvent answer)
    {
        // Deliver to pending outgoing call
        if (_pendingAnswers.TryGetValue(answer.CallId, out var tcs))
        {
            tcs.TrySetResult(answer.Sdp);
        }
    }

    private void HandleRemoteHangup(CallHangupEvent hangup)
    {
        if (_activeCalls.TryRemove(hangup.CallId, out var session))
        {
            session.UpdateStatus(CallStatusType.Completed);
            session.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _signaler.EventReceived -= OnSignalerEvent;

        foreach (var session in _activeCalls.Values)
        {
            session.Dispose();
        }

        _activeCalls.Clear();
        await Task.CompletedTask.ConfigureAwait(false);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
cd D:/prj/GVResearch && dotnet test tests/GvResearch.Sip.Tests --filter "FullyQualifiedName~WebRtcCallTransportTests" -v minimal
```

Expected: 4 tests pass.

- [ ] **Step 5: Commit**

```bash
cd D:/prj/GVResearch
git add src/GvResearch.Sip/Transport/ tests/GvResearch.Sip.Tests/Transport/
git commit -m "feat(webrtc): add WebRtcCallTransport ICallTransport implementation

Outgoing calls: create SDP offer, send via signaler, wait for answer,
establish DTLS-SRTP. Incoming calls: receive SDP offer from signaler,
create answer, fire IncomingCallReceived event. Handles SDP
renegotiation (~6s after incoming call connects)."
```

---

## Task 8: Update SIP Gateway + Delete Old Audio Channel

**Files:**
- Modify: `src/GvResearch.Sip/Program.cs`
- Delete: `src/GvResearch.Sip/Media/IGvAudioChannel.cs`
- Delete: `src/GvResearch.Sip/Media/WebRtcGvAudioChannel.cs`

- [ ] **Step 1: Update `Program.cs`**

Replace the `IGvAudioChannel` registration with signaler + transport registrations:

In `Program.cs`, change:
```csharp
// ── Media ─────────────────────────────────────────────────
services.AddSingleton<IGvAudioChannel, WebRtcGvAudioChannel>();
```

To:
```csharp
// ── Signaler + WebRTC Transport ─────────────────────────
services.AddSingleton<IGvSignalerClient, GvSignalerClient>();
services.AddSingleton<ICallTransport, WebRtcCallTransport>();
```

Add the required usings:
```csharp
using GvResearch.Shared.Signaler;
using GvResearch.Shared.Transport;
using GvResearch.Sip.Transport;
```

Remove the using:
```csharp
using GvResearch.Sip.Media;
```

- [ ] **Step 2: Delete old audio channel files**

```bash
cd D:/prj/GVResearch
git rm src/GvResearch.Sip/Media/IGvAudioChannel.cs
git rm src/GvResearch.Sip/Media/WebRtcGvAudioChannel.cs
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
git commit -m "refactor(sip): register WebRtcCallTransport, delete old audio channel

Replace IGvAudioChannel/WebRtcGvAudioChannel with IGvSignalerClient +
WebRtcCallTransport registrations. Old placeholder files deleted."
```

---

## Task 9: Full Solution Build & Test

- [ ] **Step 1: Build entire solution**

```bash
cd D:/prj/GVResearch && dotnet build
```

Expected: 0 errors, 0 warnings.

- [ ] **Step 2: Run all tests**

```bash
cd D:/prj/GVResearch && dotnet test --no-restore -v minimal
```

Expected: All tests pass.

- [ ] **Step 3: Fix any remaining compilation issues**

If any project fails due to leftover references to `IGvAudioChannel`, `WebRtcGvAudioChannel`, or the old `IncomingCallReceived` missing from NSubstitute mocks, fix them.

- [ ] **Step 4: Commit any fixes**

```bash
cd D:/prj/GVResearch
git add -A
git commit -m "fix: resolve remaining compilation issues from WebRTC migration"
```

Only commit if there were actual fixes needed.

---

## Task 10: Update Documentation

**Files:**
- Modify: `CLAUDE.md`

- [ ] **Step 1: Update CLAUDE.md**

Update the "SDK Architecture" section to mention the signaler and WebRTC transport. Update "What Still Needs to Be Built" to remove the signaler and WebRTC items (now built) and keep only the remaining items.

Key changes:
- Add `GvSignalerClient` to SDK Architecture section
- Add `WebRtcCallTransport` as the active `ICallTransport` implementation
- Remove signaler/WebRTC from "What Still Needs to Be Built"
- Keep: LoginInteractiveAsync, TryRefreshSessionAsync, Voicemail service

- [ ] **Step 2: Commit**

```bash
cd D:/prj/GVResearch
git add CLAUDE.md
git commit -m "docs: update CLAUDE.md with signaler + WebRTC architecture"
```
