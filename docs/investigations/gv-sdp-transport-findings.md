# GV SDP Transport — Investigation Findings

**Date:** 2026-03-29
**Status:** SDP format known, transport envelope unknown — JS reverse-engineering needed

---

## What We Know

### SDP Content (fully captured via chrome://webrtc-internals)

**Outgoing call — Browser SDP Offer:**
```
v=0
o=- 5915633941283921579 2 IN IP4 127.0.0.1
s=-
t=0 0
a=group:BUNDLE 0
a=extmap-allow-mixed
a=msid-semantic: WMS
m=audio 9 UDP/TLS/RTP/SAVPF 111 63 9 0 8 13 110 126
c=IN IP4 0.0.0.0
a=rtcp:9 IN IP4 0.0.0.0
a=ice-ufrag:{random}
a=ice-pwd:{random}
a=ice-options:trickle
a=fingerprint:sha-256 {fingerprint}
a=setup:actpass
a=mid:0
a=extmap:1 urn:ietf:params:rtp-hdrext:ssrc-audio-level
a=extmap:2 http://www.webrtc.org/experiments/rtp-hdrext/abs-send-time
a=extmap:3 http://www.ietf.org/id/draft-holmer-rmcat-transport-wide-cc-extensions-01
a=extmap:4 urn:ietf:params:rtp-hdrext:sdes:mid
a=sendrecv
a=msid:- {track-uuid}
a=rtcp-mux
a=rtcp-rsize
a=rtpmap:111 opus/48000/2
a=rtcp-fb:111 transport-cc
a=fmtp:111 minptime=10;useinbandfec=1
a=rtpmap:63 red/48000/2
a=fmtp:63 111/111
a=rtpmap:9 G722/8000
a=rtpmap:0 PCMU/8000
a=rtpmap:8 PCMA/8000
a=rtpmap:13 CN/8000
a=rtpmap:110 telephone-event/48000
a=rtpmap:126 telephone-event/8000
a=ssrc:{ssrc} cname:{cname}
a=ssrc:{ssrc} msid:- {track-uuid}
```

**Outgoing call — Google SDP Answer (from "xavier"):**
```
v=0
o=xavier {session-id} {version} IN IP4 74.125.39.157
s=SIP Call
c=IN IP4 74.125.39.157
t=0 0
a=sendrecv
a=ice-lite
a=ice-ufrag:{random}
a=ice-pwd:{random}
a=group:BUNDLE 0
a=fingerprint:sha-256 {fingerprint}
a=setup:passive
m=audio 26500 UDP/TLS/RTP/SAVPF 111 110
a=rtpmap:111 opus/48000/2
a=rtpmap:110 telephone-event/48000
a=candidate:1 1 UDP 1 74.125.39.157 26500 typ host
a=candidate:2 1 UDP 2 2001:4860:4864:2::7e 26500 typ host
a=mid:0
a=sendrecv
a=rtcp-mux
```

### Call Timeline (verified from rtcstats dump)
```
T+0ms     createOffer
T+0.2ms   setLocalDescription(offer)
T+1.2ms   ICE host candidates (5 UDP + 5 TCP)
T+150ms   STUN srflx candidate
T+484ms   setRemoteDescription(answer) — Google's SDP arrives
T+485ms   ICE checking
T+613ms   ICE connected
T+670ms   connectionState: "connected" — audio flowing
T+call    active call
T+end     close()
```

### getUserMedia Configuration
```json
{
  "echoCancellation": {"ideal": true},
  "autoGainControl": {"ideal": true},
  "noiseSuppression": {"ideal": true},
  "deviceId": {"exact": ["{device-id}"]}
}
```

---

## What We Don't Know: The SDP Envelope

The SDP offer/answer must travel between browser and Google via the signaler channel, but we couldn't capture the exact wire format of how SDP is wrapped/encoded in the signaler messages.

### What We Tried (and why each failed)

| Approach | Result | Why |
|---|---|---|
| **CDP Fetch.enable** | Caught completed responses only | Signaler long-poll is a streaming response — body not available until complete |
| **CDP Network.getResponseBody** | Same limitation | Can't read body of in-progress streaming response |
| **XHR prototype monkey-patch** | Didn't catch existing connections | GV's Closure library holds closure-scoped XHR references |
| **XHR constructor replacement** | New connections not created during call | Signaler reuses existing long-poll connection |
| **Page reload + interceptor** | Interceptor didn't survive | GV's bundle overwrites XMLHttpRequest |
| **Chrome --log-net-log** | Captured TLS handshakes, not app data | QUIC/TLS encryption hides payload |
| **Full network monitoring** | Only 7 HTTP requests during call | SDP is in the streaming long-poll, not a separate request |

### Most Likely Transport Mechanism

The SDP is delivered as a **chunk within the signaler's streaming long-poll GET response**. The signaler response format is:

```
{length}\n[[seqNum, [channel_data]]]
```

During a call, the channel_data on channel 1 or 3 likely contains the SDP wrapped in the signaler's nested array format. The `W10=` (base64 `[]`) we captured earlier was the notification event, not the SDP itself — the actual SDP was in an earlier chunk that our CDP tools couldn't access.

---

## Next Step: GV JavaScript Reverse Engineering

To find the SDP envelope format, reverse-engineer Google Voice's minified JavaScript:

### Target files
- `web-player.*.js` — main GV application bundle
- `vendor~web-player.*.js` — vendor libraries (likely contains BrowserChannel client)

### What to look for
1. **BrowserChannel send implementation** — how the client formats outgoing messages
   - Search for: `req0___data__`, `count=`, `ofs=` (URL-encoded POST format)
   - Search for: `sendSdp`, `setRemoteDescription`, `sdpOffer`, `sdpAnswer`

2. **SDP serialization** — how SDP is encoded before sending
   - Search for: `v=0`, `RTCSessionDescription`, `createOffer`, `createAnswer`
   - The SDP might be base64-encoded, protobuf-wrapped, or JSON-stringified

3. **Signaler event parsing** — how incoming SDP is extracted from channel data
   - Search for: `xavier`, `setRemoteDescription`, `RTCPeerConnection`
   - Follow the code path from signaler event → SDP extraction → peer connection

4. **Channel subscription format** — what the 6 channel subscriptions request
   - The initial POST body (URL-encoded `req0___data__` through `req5___data__`) defines what each channel listens for

### Tools
- Chrome DevTools Sources tab with pretty-print
- Search across all sources for SDP-related strings
- Set breakpoints on `RTCPeerConnection.prototype.setRemoteDescription`
- Watch the call stack when the breakpoint hits during a call — trace back to the signaler handler

---

## Files

| File | Contents |
|---|---|
| `captures/iaet-exports/gv-rtcstats-full-dump.txt` | Complete WebRTC internals dump with SDP + ICE + timing |
| `captures/iaet-exports/gv-rtcstats-dump.gz` | Compressed original dump |
| `captures/iaet-exports/gv-signaler-sdp-capture.json` | 48 signaler events (no SDP, proves it's in streaming chunks) |
| `captures/iaet-exports/gv-fetch-intercept-capture.json` | CDP Fetch intercept (6 completed responses, no SDP) |
| `captures/iaet-exports/gv-full-network-capture.json` | Full network capture during call (7 requests, no SDP) |
| `captures/iaet-exports/gv-call-signaler-capture.json` | Earlier CDP capture with signaler events |
| `captures/iaet-exports/gv-cookies-full.json` | 45 cookies with full metadata |
| `captures/iaet-exports/gv-auth-analysis.json` | Auth state + signaler URLs |
