# GV SDP Envelope — DECODED

**Date:** 2026-03-29
**Method:** Reverse-engineering GV's 4.8MB minified JavaScript bundle
**Status:** Architecture fully understood

---

## The Answer: SIP over WebSocket

Google Voice uses a **full SIP User Agent** (based on TsSIP, a TypeScript fork of JsSIP) running in the browser. SDP is carried as the **body of standard SIP INVITE/ACK messages**, transported over a **WebSocket connection** to Google's SIP infrastructure.

This is NOT a custom protocol — it's standard RFC 3261 SIP + RFC 7118 SIP over WebSocket.

```
┌─────────────────────────────────────────────────────────────────┐
│              GV Call Architecture (fully decoded)                 │
│                                                                   │
│  Browser                        Google                            │
│    │                               │                              │
│    │  WebSocket (wss://)           │                              │
│    ├──────────────────────────────►│  SIP Proxy/Registrar         │
│    │  SIP REGISTER                 │                              │
│    │  (Bearer token auth)          │                              │
│    │                               │                              │
│    │  SIP INVITE                   │                              │
│    │  (SDP offer in body)  ───────►│  "xavier" SIP UA             │
│    │                               │  at 74.125.39.x              │
│    │  SIP 200 OK                   │                              │
│    │◄──── (SDP answer in body) ────│                              │
│    │                               │                              │
│    │  SIP ACK                      │                              │
│    ├──────────────────────────────►│                              │
│    │                               │                              │
│    │  WebRTC DTLS + SRTP           │                              │
│    │◄═══════════════════════════►  │  SIP-to-PSTN gateway         │
│    │  Opus audio on port 26500     │                              │
│    │                               │                              │
│    │  SIP BYE                      │                              │
│    ├──────────────────────────────►│  (or from remote)            │
│    │                               │                              │
│  Signaler (separate channel)       │                              │
│    │  Long-poll push ◄────────────│  Call state notifications     │
│    │  (thread updates, not SDP)    │  (post-call only)            │
└─────────────────────────────────────────────────────────────────┘
```

---

## Evidence from JavaScript Analysis

### 1. TsSIP Library Embedded

The GV bundle contains a complete TsSIP (TypeScript SIP) implementation:
- Full SIP message parser (INVITE, ACK, BYE, CANCEL, REGISTER, OPTIONS, PRACK, UPDATE, REFER, INFO, MESSAGE, SUBSCRIBE, NOTIFY)
- SIP digest authentication + OAuth Bearer token auth
- SIP transaction state machines (IST, NIST, ICT, NICT)
- Dialog management (early, confirmed, terminated)
- SDP negotiation via SIP offer/answer model

Key identifier: `vh("TsSIP.sanityCheck")`, `vh("TsSIP.Transport")`, `vh("TsSIP.DigestAuthentication")`

### 2. WebSocket Transport

```javascript
// From the bundle (de-minified):
const uri = `sip:${host}${port ? `:${port}` : ""};transport=ws`;
const ws = new WebSocket(this.url, "sip");  // "sip" subprotocol
ws.binaryType = "arraybuffer";
```

The WebSocket connects with subprotocol `"sip"` per RFC 7118.

### 3. SIP INVITE with SDP Body

```javascript
// Call initiation (de-minified):
g = CF(a.ka, "INVITE", h, {
    q2: e ? "Anonymous" : void 0,
    fromTag: f,
    Iha: e ? "sip:anonymous@anonymous.invalid" : void 0,
    Az: c.Az,
    callId: c.callId,
    extraHeaders: g
});
// The SDP is set as the INVITE body
```

### 4. SDP Answer Processing

```javascript
// Answer received (de-minified):
originator: "remote",
type: "answer",
sdp: k.body   // k is the SIP response, body is the SDP
// ...
yield t.setRemoteDescription(m)  // m = new RTCSessionDescription
```

### 5. Bearer Token Authentication

```javascript
// OAuth-based SIP auth:
{
    Tea: [
        `Bearer token="${b.accessToken}"`,
        `username="${this.ka}"`,
        `realm="${this.realm}"`
    ].join(", "),
    ttl: b.ttl
}
```

### 6. Two RTCPeerConnection STUN Servers

```javascript
// Birdsong WASM (media engine):
new RTCPeerConnection({
    iceServers: [{urls: ["stun:lens.l.google.com:19305"]}],
    sdpSemantics: "unified-plan"
});

// SIP UA (signaling):
// Uses stun:stun.l.google.com:19302 (from SIP config)
```

---

## SIP Message Format (reconstructed)

### REGISTER

```
REGISTER sip:voice.telephony.goog SIP/2.0
Via: SIP/2.0/WSS {browser-generated-branch}
From: <sip:{phone}@voice.telephony.goog>;tag={fromTag}
To: <sip:{phone}@voice.telephony.goog>
Call-ID: {callId}
CSeq: {n} REGISTER
Contact: <sip:{phone}@{ws-host};transport=ws>;+sip.ice;reg-id=1;+sip.instance="<urn:uuid:{device-uuid}>"
Expires: 600
Authorization: Bearer token="{oauth-token}", username="{phone}", realm="voice.telephony.goog"
Max-Forwards: 70
Content-Length: 0
```

### INVITE (outgoing call with SDP)

```
INVITE sip:{destination}@voice.telephony.goog SIP/2.0
Via: SIP/2.0/WSS {browser-generated-branch}
From: <sip:{phone}@voice.telephony.goog>;tag={fromTag}
To: <sip:{destination}@voice.telephony.goog>
Call-ID: {callId}
CSeq: {n} INVITE
Contact: <sip:{phone}@{ws-host};transport=ws>
Allow: INVITE,ACK,CANCEL,BYE,UPDATE,MESSAGE,OPTIONS,REFER,INFO,PRACK
Content-Type: application/sdp
Max-Forwards: 70
Content-Length: {sdp-length}

v=0
o=- {session-id} 2 IN IP4 127.0.0.1
s=-
t=0 0
a=group:BUNDLE 0
m=audio 9 UDP/TLS/RTP/SAVPF 111 63 9 0 8 13 110 126
...{full SDP}...
```

### 200 OK (SDP answer from Google)

```
SIP/2.0 200 OK
Via: SIP/2.0/WSS {original-branch}
From: <sip:{phone}@voice.telephony.goog>;tag={fromTag}
To: <sip:{destination}@voice.telephony.goog>;tag={toTag}
Call-ID: {callId}
CSeq: {n} INVITE
Contact: <sip:xavier@{google-ip}:5060;transport=udp>
Content-Type: application/sdp
Content-Length: {sdp-length}

v=0
o=xavier {session-id} {version} IN IP4 74.125.39.157
s=SIP Call
...{Google's SDP answer}...
```

---

## WebSocket Endpoint

The WebSocket URL is dynamically constructed from the `sipregisterinfo/get` response. Based on the code:

```
wss://{host}:{port}
```

Where `{host}` is likely derived from the SIP registrar domain (e.g., `voice.telephony.goog` or a specific server assigned by the registration process).

The `sipregisterinfo/get` response `[["sipToken", expiry], null, null, ["authToken", "cryptoKey"]]` provides:
- SIP authentication credentials
- Token expiry for re-registration
- The WebSocket server endpoint (embedded in the response)

---

## Implications for SIPSorcery Implementation

This is **excellent news** for the GVResearch project:

1. **Standard SIP** — SIPSorcery natively supports SIP over WebSocket (RFC 7118)
2. **No custom signaling** — the signaler channel is only for non-call notifications
3. **Bearer token auth** — simpler than digest auth, just pass the OAuth token
4. **Direct mapping** — the SIPSorcery `SIPUserAgent` can be configured to match exactly what TsSIP does

### What's needed:
1. Call `sipregisterinfo/get` to get SIP credentials + WebSocket URL
2. Connect SIPSorcery to the WebSocket with `"sip"` subprotocol
3. REGISTER with Bearer token auth
4. INVITE with the SDP offer (exact format we captured from rtcstats)
5. Process 200 OK with SDP answer
6. Send ACK
7. WebRTC audio flows via DTLS-SRTP on port 26500

### What's NOT needed:
- Signaler client (not involved in call setup — only post-call notifications)
- Custom SDP envelope format (it's standard SIP Content-Type: application/sdp)
- BrowserChannel parsing (irrelevant for calls)
