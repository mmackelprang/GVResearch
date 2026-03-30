# RotaryPhone GVBridge Integration — SIP-over-WebSocket + DTLS-SRTP Audio

## Goal

Replace the current GVBridge audio path (Chrome extension WebSocket relay) with direct SIP-over-WebSocket + DTLS-SRTP audio, eliminating the Chrome dependency entirely. The GVApiAdapter should make and receive calls through Google Voice's SIP infrastructure with bidirectional Opus audio, bridged to the HT801 ATA via the existing RTP bridge.

## Context

The GVResearch project (`D:\prj\GVResearch`) has a working softphone that makes/receives Google Voice calls with bidirectional audio. All the VoIP code lives in `src/GvResearch.Sip/Transport/SipWssCallTransport.cs`. This needs to be ported into the RotaryPhone project's `GVBridge` module at `D:\prj\RotaryPhone\src\RotaryPhoneController.GVBridge/`.

**Copy the code directly** into the RotaryPhone project (do not add GVResearch as a subproject reference).

## Architecture Overview

```
Rotary Phone ←→ HT801 ATA ←→ SIPSorceryAdapter (local SIP) ←→ CallManager
                                                                    ↕
                                                              GVApiAdapter (ICallAdapter)
                                                                    ↕
                                                         SIP-over-WebSocket to Google Voice
                                                         (SipWssCallTransport equivalent)
                                                                    ↕
                                                         DTLS-SRTP ← → Google Media Relay
                                                         (Opus 48kHz ↔ RTP)
```

Audio flow when a call is active:
- **Outbound (rotary → cell):** HT801 sends G.711 RTP → RtpAudioBridge resamples → Opus encode → SRTP → Google
- **Inbound (cell → rotary):** Google SRTP → Opus decode → RtpAudioBridge resamples → G.711 RTP → HT801

## Files to Copy from GVResearch

Copy these files from `D:\prj\GVResearch\src\GvResearch.Sip\Transport\` into `D:\prj\RotaryPhone\src\RotaryPhoneController.GVBridge\Sip\` (new directory), adjusting namespaces:

| Source (GVResearch) | Destination (RotaryPhone) | Purpose |
|---|---|---|
| `SipWssCallTransport.cs` | `Sip/GvSipTransport.cs` | SIP signaling + DTLS-SRTP + Opus encode/decode |
| `GvSipWebSocketChannel.cs` | `Sip/GvSipWebSocketChannel.cs` | WebSocket with `sip` subprotocol |
| `GvSipCredentialProvider.cs` | `Sip/GvSipCredentialProvider.cs` | Fetches SIP creds from `sipregisterinfo/get` |
| `GvDtlsSrtpClient.cs` | `Sip/GvDtlsSrtpClient.cs` | Custom DTLS client (ECDSA cipher suites) |

Also copy the modified SIPSorcery NuGet package:
- `D:\prj\GVResearch\local-packages\SIPSorcery.10.0.6-diag.nupkg` → `D:\prj\RotaryPhone\local-packages\`
- `D:\prj\GVResearch\local-packages\SIPSorceryMedia.Abstractions.10.0.6-diag.nupkg` → same

## Critical SIPSorcery Configuration

The stock SIPSorcery NuGet (8.0.23 currently in RotaryPhone) will NOT work for DTLS. You MUST use the modified `10.0.6-diag` build which:
- Removes `encrypt_then_mac` TLS extension from DTLS ClientHello (Google rejects it)
- Removes `status_request` TLS extension
- Ensures `extended_master_secret` extension is always present
- Limits SRTP profiles to Chrome-compatible set (4 instead of 12)

Update `RotaryPhoneController.GVBridge.csproj`:
```xml
<PackageReference Include="SIPSorcery" Version="10.0.6-diag" />
```

And add to `nuget.config`:
```xml
<add key="local-packages" value="./local-packages" />
```

## RTCPeerConnection Configuration (CRITICAL)

```csharp
var pc = new RTCPeerConnection(new RTCConfiguration
{
    iceServers = [new RTCIceServer { urls = "stun:stun.l.google.com:19302" }],
    X_UseRsaForDtlsCertificate = true,    // Google uses RSA cipher suites, NOT ECDSA
    X_UseRtpFeedbackProfile = true,       // UDP/TLS/RTP/SAVPF profile
});
```

**Why RSA:** Google's media relay negotiates `TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256`. Setting ECDSA causes immediate `handshake_failure(40)`.

## SIP Call Flow (Verified Working)

```
1. sipregisterinfo/get → SIP username + Digest password
2. WSS connect to wss://web.voice.telephony.goog/websocket (subprotocol: "sip")
3. REGISTER (no auth) → 401 → REGISTER with MD5 Digest → 200 OK + Service-Route
4. INVITE sip:+1XXXXXXXXXX@web.c.pbx.voice.sip.google.com (with SDP offer)
5. 100 Trying
6. 183 Session Progress (SDP answer with Google's media endpoint) → PRACK → 200 OK
7. 180 Ringing → PRACK → 200 OK
8. 200 OK (INVITE) → ACK → call connected
9. ICE → connected, DTLS → handshake complete, SRTP keys derived
10. Bidirectional Opus audio flows
11. BYE → 200 OK (either direction)
```

## Key Technical Details

### SIP Domain and Endpoints
- SIP domain: `web.c.pbx.voice.sip.google.com`
- WebSocket: `wss://web.voice.telephony.goog/websocket`
- SIP proxy (Route): `216.239.36.145:443` (from Service-Route in REGISTER 200 OK)
- Media relay: `74.125.39.x:26500` (from SDP answer `a=candidate:`)

### Authentication
- WebSocket upgrade: NO auth (no cookies, no Authorization header)
- SIP REGISTER: MD5 Digest auth (username from sipregisterinfo/get, password = token)
- `sipregisterinfo/get` API call: Uses SAPISIDHASH cookie auth (same as other GV API calls)
- Body: `[3,"deviceId"]`
- Response: `[["timestamp",expiryMs],null,null,["sipIdentityToken","digestPassword"]]`

### DTLS-SRTP Parameters (Confirmed Working)
- DTLS cipher: `TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256` (0xC02F)
- SRTP profile: `AES_CM_128_HMAC_SHA1_80` (0x0001)
- DTLS version: 1.2 (FEFD)
- Google's SDP: `a=setup:passive` (we are DTLS client)
- Google uses `ice-lite` with fixed candidate

### Opus Audio
- Google sends: Opus 48kHz stereo (pt=111, `OPUS/48000/2`)
- Decode with Concentus: `OpusCodecFactory.CreateDecoder(48000, 2)` → stereo → downmix to mono
- Encode with Concentus: `OpusCodecFactory.CreateEncoder(48000, 1, OPUS_APPLICATION_VOIP)`
- Frame size: 960 samples (20ms at 48kHz)

### Session Timer
- Google's 200 OK includes `Session-Expires: 90;refresher=uac`
- Must send re-INVITE every ~45 seconds (half the interval) to keep call alive
- Re-INVITE includes current SDP and `Session-Expires: 90;refresher=uac`

### BYE Handling
- Incoming BYE has 3 Via headers (multi-hop) — must echo ALL in 200 OK
- Use `ExtractAllHeaders(message, "Via")` to get all Via headers

### Incoming Calls
- Handle SIP INVITE requests on the WebSocket
- Parse caller from From header: `sip:+1XXXXXXXXXX@domain`
- Send 180 Ringing → 200 OK with SDP answer
- Wire DTLS-SRTP + Opus for inbound media

## Integration with GVApiAdapter

The current `GVApiAdapter.PlaceCallAsync()` uses the HTTP API's `callcreate` endpoint (which triggers Google to call you back). Replace this with direct SIP INVITE.

### Modifications to GVApiAdapter

1. **Add SIP transport as a field:**
```csharp
private GvSipTransport? _sipTransport;  // replaces _callClient for actual call control
```

2. **In ActivateAsync():** After cookie/health check, create and register the SIP transport:
```csharp
_sipTransport = new GvSipTransport(logger, () => credentialProvider.GetCredentialsAsync(), loggerFactory);
// SIP REGISTER happens automatically on first call
```

3. **PlaceCallAsync():** Use SIP INVITE instead of HTTP API:
```csharp
public async Task<string> PlaceCallAsync(string e164Number, CancellationToken ct)
{
    var result = await _sipTransport!.InitiateAsync(e164Number, ct);
    if (!result.Success) throw new InvalidOperationException(result.ErrorMessage);
    _activeCallId = result.CallId;
    return result.CallId;
}
```

4. **HangUpAsync():** Use SIP BYE:
```csharp
await _sipTransport!.HangupAsync(_activeCallId!, ct);
```

5. **OnCallAnsweredOnRotaryPhoneAsync():** Start the audio bridge between SIP transport and HT801:
- Subscribe to `_sipTransport.AudioReceived` → resample Opus 48kHz PCM to 8kHz G.711 → send to HT801 RTP
- Capture HT801 RTP (8kHz G.711) → resample to 48kHz PCM → `_sipTransport.SendAudio()`

6. **Incoming calls:** Subscribe to `_sipTransport.IncomingCallReceived`:
```csharp
_sipTransport.IncomingCallReceived += (_, args) => OnIncomingCall?.Invoke(args.CallInfo.CallerNumber);
```

### Audio Bridge Modifications

The existing `GVAudioBridgeService` bridges Chrome extension WebSocket audio ↔ HT801 RTP. Replace the Chrome extension side with SIP transport audio:

**Inbound (Google → HT801):**
```
SipTransport.AudioReceived (48kHz 16-bit mono PCM)
    → AudioResampler.Resample(48000 → 8000)
    → G.711 µ-law encode
    → RTP packet → HT801
```

**Outbound (HT801 → Google):**
```
HT801 RTP → G.711 µ-law decode
    → AudioResampler.Resample(8000 → 48000)
    → SipTransport.SendAudio(pcmBytes, 48000)
    (SendAudio internally encodes to Opus via Concentus)
```

The `AudioResampler` class already exists in GVBridge — it handles sample rate conversion.

## Environment Variable

The GV API key must be set as an environment variable:
```
GvResearch__ApiKey=AIzaSyDTYc1N4xiODyrQYK0Kl6g_y279LjYkrBg
```

Or use the existing `GVBridgeConfig.GvApiKey` config property.

## What to Remove After Integration

Once SIP-over-WebSocket audio is working:
1. Remove `GVBridgeService` (Chrome extension WebSocket relay — no longer needed)
2. Remove Chrome extension dependency from `GVAudioBridgeService`
3. Remove `ExtensionMessage` models
4. Remove `GVBridgeHub` SignalR hub (extension communication)
5. Remove `GvLoginTool` (replaced by `CookieRetriever` from GVResearch)

## Testing Checklist

- [ ] SIP REGISTER succeeds (401 → Digest → 200 OK)
- [ ] Outbound call: INVITE → phone rings → call connects
- [ ] Inbound audio: Opus decode → resample → G.711 → HT801 → rotary phone speaker
- [ ] Outbound audio: rotary phone mic → HT801 → G.711 → resample → Opus encode → Google
- [ ] Incoming call: SIP INVITE → 180 → 200 OK → audio flows
- [ ] BYE: both directions, 200 OK stops retransmission
- [ ] Session timer: re-INVITE every ~45s keeps call alive beyond 90s
- [ ] Cookie auto-refresh: health check + 401 retry
