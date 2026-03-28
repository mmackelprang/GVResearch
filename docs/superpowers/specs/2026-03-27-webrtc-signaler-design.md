# WebRTC Audio Channel & Signaler Client — Design Spec

**Date:** 2026-03-27
**Status:** Approved
**Approach:** Signaler in Shared, WebRTC Transport in Sip (Approach A)

## Overview

Build the real-time audio path for GVResearch: a signaler client for Google Voice's long-poll push channel (call signaling, SDP exchange) and a WebRTC call transport using SIPSorcery (DTLS-SRTP, Opus audio). Together these unlock real VoIP — making and receiving phone calls without a browser.

### Goals

- Real-time call signaling via GV's signaler long-poll channel
- Outgoing calls: send SDP offer, receive answer, establish audio
- Incoming calls: receive SDP offer push, send answer, establish audio
- Transport-agnostic: implements `ICallTransport` so all existing SDK consumers work unchanged
- Raw RTP packet events for audio I/O (consumers handle codec/playback)

### Scope

**In scope:**
- `GvSignalerClient` — session lifecycle (choose server, open channel, poll loop, reconnect)
- Call signaling events only (SDP offer, answer, hangup, ringing)
- `WebRtcCallTransport` — `ICallTransport` implementation using SIPSorcery `RTCPeerConnection`
- Outgoing and incoming call flows
- SDP renegotiation handling (~6s after incoming call connect)
- Integration with existing SDK (`IGvCallClient`, `GvCallClient`, DI registration)

**Out of scope (deferred):**
- SMS/voicemail push notifications via signaler (signaler infrastructure supports it later)
- Audio codec decode/encode (consumers handle this)
- Playwright interactive login (still deferred)
- SIP-based transport (separate from WebRTC transport)

---

## Section 1: GvSignalerClient — Channel Management

### Interface

```csharp
public interface IGvSignalerClient : IAsyncDisposable
{
    event Action<SignalerEvent> EventReceived;
    event Action<Exception> ErrorOccurred;

    Task ConnectAsync(CancellationToken ct = default);
    Task DisconnectAsync(CancellationToken ct = default);
    bool IsConnected { get; }

    Task SendSdpOfferAsync(string callId, string sdp, CancellationToken ct = default);
    Task SendSdpAnswerAsync(string callId, string sdp, CancellationToken ct = default);
    Task SendHangupAsync(string callId, CancellationToken ct = default);
}
```

### SignalerEvent types

```csharp
public abstract record SignalerEvent(DateTimeOffset Timestamp);
public sealed record IncomingSdpOfferEvent(string CallId, string Sdp, DateTimeOffset Timestamp) : SignalerEvent(Timestamp);
public sealed record SdpAnswerEvent(string CallId, string Sdp, DateTimeOffset Timestamp) : SignalerEvent(Timestamp);
public sealed record CallHangupEvent(string CallId, DateTimeOffset Timestamp) : SignalerEvent(Timestamp);
public sealed record CallRingingEvent(string CallId, DateTimeOffset Timestamp) : SignalerEvent(Timestamp);
public sealed record UnknownEvent(string RawPayload, DateTimeOffset Timestamp) : SignalerEvent(Timestamp);
```

### Session lifecycle

```
ConnectAsync()
+-- POST chooseServer -> get server assignment
+-- POST channel?VER=8&RID=0&CVER=22 -> get SID (session ID)
+-- Start poll loop (background task):
    +-- GET channel?VER=8&SID={sid}&AID={aid}&RID=rpc&TYPE=xmlhttp
        +-- Parse response -> fire EventReceived for each event
        +-- Increment AID (acknowledgment ID)
        +-- On timeout/error -> reconnect with backoff
        +-- Loop until DisconnectAsync() or disposed
```

### Sending messages

Outgoing SDP offers/answers and hangups are sent as POST requests to the channel endpoint with incrementing `RID` values.

```
SendAsync(message)
+-- POST channel?VER=8&SID={sid}&RID={nextRid}&AID={aid}
    Body: count=1&ofs=0&req0_type=sdp&req0_data={sdp_json}
+-- Increment RID (thread-safe via Interlocked.Increment)
```

### Reconnection strategy

- Exponential backoff: 1s, 2s, 4s, 8s, max 30s
- On reconnect, re-choose server and open new session
- Fire `ErrorOccurred` on each failure
- Never silently die — always retry or throw on dispose

### Dependencies

- `HttpClient` via `IHttpClientFactory` (named `"GvSignaler"`, 5-minute timeout for long-poll)
- `IGvAuthService` (cookies — signaler uses same auth as API)
- `GvApiConfig` (API key in query string)

### File locations

- `src/GvResearch.Shared/Signaler/IGvSignalerClient.cs`
- `src/GvResearch.Shared/Signaler/GvSignalerClient.cs`
- `src/GvResearch.Shared/Signaler/SignalerEvent.cs`
- `src/GvResearch.Shared/Signaler/SignalerMessageParser.cs`

---

## Section 2: WebRtcCallTransport — ICallTransport Implementation

### Class

```csharp
public sealed class WebRtcCallTransport : ICallTransport
{
    private readonly IGvSignalerClient _signaler;
    private readonly ConcurrentDictionary<string, WebRtcCallSession> _activeCalls = new();

    Task<TransportCallResult> InitiateAsync(string toNumber, CancellationToken ct);
    Task<TransportCallStatus> GetStatusAsync(string callId, CancellationToken ct);
    Task HangupAsync(string callId, CancellationToken ct);

    event Action<IncomingCallInfo>? IncomingCallReceived;
}
```

### WebRtcCallSession — per-call state

```csharp
internal sealed class WebRtcCallSession : IDisposable
{
    public string CallId { get; }
    public RTCPeerConnection PeerConnection { get; }
    public CallStatusType Status { get; private set; }

    public event Action<RTPPacket>? AudioReceived;
    public void SendAudio(RTPPacket packet);
}
```

### Outgoing call flow

```
InitiateAsync("+15551234567")
1. Create RTCPeerConnection with STUN (stun:stun.l.google.com:19302)
2. Add audio track (Opus, G722, PCMU, PCMA, telephone-event)
3. CreateOffer() -> local SDP
4. SetLocalDescription(offer)
5. signaler.SendSdpOfferAsync(callId, sdp)
6. Wait for SdpAnswerEvent from signaler (matching callId)
7. SetRemoteDescription(answer)
8. DTLS-SRTP handshake completes automatically
9. Audio flows — fire AudioReceived events
10. Return TransportCallResult(callId, success: true)

Expected: ~1.3 seconds to connected
```

### Incoming call flow

```
Signaler fires IncomingSdpOfferEvent
1. Create RTCPeerConnection with same STUN config
2. SetRemoteDescription(offer) — Google's SDP from "xavier"
3. CreateAnswer() — initially recvonly
4. SetLocalDescription(answer)
5. signaler.SendSdpAnswerAsync(callId, sdp)
6. DTLS-SRTP handshake -> audio connected in ~260ms
7. At ~6 seconds: handle renegotiation
8. Store session in _activeCalls, fire IncomingCallReceived

Expected: ~260ms to audio
```

### SDP renegotiation

Google re-offers ~6 seconds after incoming call connects. The transport listens for a second `IncomingSdpOfferEvent` with the same `callId`:

1. Receive re-offer via signaler
2. `SetRemoteDescription(re-offer)`
3. `CreateAnswer()` with full ICE candidates
4. `SetLocalDescription(answer)`
5. `signaler.SendSdpAnswerAsync(callId, answer)`

### Hangup

```
HangupAsync(callId)
1. signaler.SendHangupAsync(callId)
2. Close RTCPeerConnection
3. Remove from _activeCalls
4. Dispose session
```

### SIPSorcery configuration

```csharp
var config = new RTCConfiguration
{
    iceServers = [new RTCIceServer { urls = "stun:stun.l.google.com:19302" }]
};
var pc = new RTCPeerConnection(config);

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
pc.addTrack(audioTrack);
```

### File locations

- `src/GvResearch.Sip/Transport/WebRtcCallTransport.cs`
- `src/GvResearch.Sip/Transport/WebRtcCallSession.cs`

---

## Section 3: Integration with Existing SDK

### ICallTransport changes

Add incoming call notification to the existing interface:

```csharp
// Addition to ICallTransport:
event Action<IncomingCallInfo>? IncomingCallReceived;

public sealed record IncomingCallInfo(string CallId, string CallerNumber);
```

### IGvCallClient changes

Surface incoming calls:

```csharp
// Addition to IGvCallClient:
event Action<IncomingCallInfo>? IncomingCallReceived;
```

`GvCallClient` forwards `IncomingCallReceived` from its transport.

### DI registration

```csharp
// In GvResearch.Sip Program.cs:
services.AddSingleton<IGvSignalerClient, GvSignalerClient>();
services.AddSingleton<ICallTransport, WebRtcCallTransport>();

services.AddGvClient(options => { ... });
// AddGvClient sees ICallTransport already registered, skips NullCallTransport
```

Signaler HttpClient registration (in `AddGvClient` or a new `AddGvSignaler` extension):

```csharp
services.AddHttpClient("GvSignaler", client =>
{
    client.BaseAddress = new Uri("https://signaler-pa.clients6.google.com");
    client.Timeout = TimeSpan.FromMinutes(5);
});
```

### Existing code changes

| File | Change |
|------|--------|
| `ICallTransport.cs` | Add `IncomingCallReceived` event + `IncomingCallInfo` record |
| `IGvClient.cs` | Add `IncomingCallReceived` event to `IGvCallClient` |
| `GvCallClient.cs` | Forward `IncomingCallReceived` from transport |
| `NullCallTransport` | Add no-op event (never fires) |
| `GvClientServiceExtensions.cs` | Register `"GvSignaler"` named HttpClient |
| `GvResearch.Sip/Program.cs` | Register signaler + WebRtcCallTransport |
| `WebRtcGvAudioChannel.cs` | Delete (replaced by WebRtcCallTransport) |
| `IGvAudioChannel.cs` | Delete (no longer needed) |

### What stays unchanged

- `RtpBridge` — still used for SIP-to-WebRTC packet forwarding
- `CallSession` — still tracks per-call state
- `SipCallController` — gains incoming call handling, outgoing flow unchanged
- `SipRegistrar` — unchanged
- All REST API endpoints — unchanged

---

## Section 4: Testing Strategy

### Unit tests (GvResearch.Shared.Tests)

**SignalerClient tests** (`GvSignalerClientTests`):
- `ConnectAsync_ChoosesServerAndOpensSession` — verify POST to chooseServer then channel open
- `PollLoop_ParsesSdpOfferEvent` — mock response with SDP offer, verify EventReceived fires
- `PollLoop_ParsesSdpAnswerEvent` — same for answers
- `PollLoop_ParsesHangupEvent` — same for hangup
- `PollLoop_ReconnectsOnError` — simulate HTTP failure, verify reconnect with backoff
- `SendSdpOfferAsync_PostsToChannel` — verify correct POST body and RID increment
- `DisconnectAsync_StopsPollLoop` — verify clean shutdown

**SignalerMessageParser tests** (`SignalerMessageParserTests`):
- Parse raw signaler response format into typed events
- Fixture data from `.iaet.json` captures

### Unit tests (GvResearch.Sip.Tests)

**WebRtcCallTransport tests** (`WebRtcCallTransportTests`):
- `InitiateAsync_SendsSdpOffer` — verify offer sent through signaler
- `InitiateAsync_WaitsForAnswer` — simulate signaler delivering answer event
- `IncomingSdpOffer_CreatesSession` — simulate signaler delivering offer, verify session
- `HangupAsync_SendsHangupAndClosesPeerConnection` — verify cleanup
- `GetStatusAsync_ReturnsCurrentState` — verify status tracking

### What we do NOT automate

- No live signaler connections (requires auth + Google servers)
- No real DTLS-SRTP handshakes (requires network)
- No real audio streaming

### Human UAT Testing

**Prerequisites:**
- Encrypted cookie file populated (GV auth)
- A second phone number for test calls
- API key configured in appsettings.json

**UAT Test Cases:**

1. **Outgoing call:**
   - [ ] Initiate call via REST API `POST /api/v1/calls`
   - [ ] Verify signaler connects and SDP offer sent
   - [ ] Verify test phone rings
   - [ ] Answer on test phone, verify bidirectional audio
   - [ ] Hangup via `POST /api/v1/calls/{id}/hangup`, verify clean teardown

2. **Incoming call:**
   - [ ] Call GV number from test phone
   - [ ] Verify `IncomingCallReceived` event fires
   - [ ] Verify SDP answer sent via signaler
   - [ ] Verify bidirectional audio (~260ms to connect)
   - [ ] Hangup from test phone, verify `CallHangupEvent` received

3. **SDP renegotiation:**
   - [ ] Incoming call, wait 6+ seconds
   - [ ] Verify renegotiation (second SDP offer/answer exchange)
   - [ ] Verify no audio interruption during renegotiation

4. **Signaler reconnection:**
   - [ ] Kill network briefly while idle
   - [ ] Verify signaler reconnects with backoff
   - [ ] Make a call after reconnection, verify it works

5. **Concurrent calls:**
   - [ ] Two simultaneous outgoing calls
   - [ ] Verify independent audio paths (no cross-talk)

**UAT sign-off:**
- All test cases pass
- Audio quality acceptable (no dropouts under normal network)
- Call setup time within expected range (outgoing ~1.3s, incoming ~260ms)

---

## Section 5: Project Structure

### New files (GvResearch.Shared)

```
src/GvResearch.Shared/Signaler/
+-- IGvSignalerClient.cs         # interface
+-- GvSignalerClient.cs          # long-poll loop, session management, reconnection
+-- SignalerEvent.cs             # event type hierarchy
+-- SignalerMessageParser.cs     # raw response -> typed events
```

### New files (GvResearch.Sip)

```
src/GvResearch.Sip/Transport/
+-- WebRtcCallTransport.cs       # ICallTransport implementation
+-- WebRtcCallSession.cs         # per-call RTCPeerConnection + state
```

### Modified files

| File | Change |
|------|--------|
| `src/GvResearch.Shared/Transport/ICallTransport.cs` | Add `IncomingCallReceived` event + `IncomingCallInfo` |
| `src/GvResearch.Shared/Services/IGvClient.cs` | Add `IncomingCallReceived` to `IGvCallClient` |
| `src/GvResearch.Shared/Services/GvCallClient.cs` | Forward `IncomingCallReceived` from transport |
| `src/GvResearch.Shared/GvClientServiceExtensions.cs` | Register `"GvSignaler"` HttpClient, add `NullCallTransport` event |
| `src/GvResearch.Sip/Program.cs` | Register `IGvSignalerClient` + `WebRtcCallTransport` |
| `src/GvResearch.Sip/Calls/SipCallController.cs` | Handle incoming calls from transport |

### Deleted files

| File | Reason |
|------|--------|
| `src/GvResearch.Sip/Media/WebRtcGvAudioChannel.cs` | Replaced by `WebRtcCallTransport` |
| `src/GvResearch.Sip/Media/IGvAudioChannel.cs` | No longer needed |

### New test files

```
tests/GvResearch.Shared.Tests/Signaler/
+-- GvSignalerClientTests.cs
+-- SignalerMessageParserTests.cs

tests/GvResearch.Sip.Tests/Transport/
+-- WebRtcCallTransportTests.cs
```
