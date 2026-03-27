# Google Voice Headless Integration Guide

How to use the discovered GV API without any browser.

---

## Authentication

GV uses Google's cookie-based auth, not OAuth2 bearer tokens. To make API calls headless, you need:

### Required Cookies/Headers
1. **SAPISID** — Google API session cookie (long-lived, from browser session)
2. **SAPISIDHASH** — HMAC of `timestamp_SAPISID_origin` used in `Authorization` header
3. **SID, HSID, SSID, APISID** — Additional session cookies

### How to Obtain Tokens
**Option A: Extract from browser (recommended for research)**
1. Log into `voice.google.com` in any browser
2. Open DevTools → Application → Cookies
3. Copy `SAPISID`, `SID`, `HSID`, `SSID`, `APISID`, `__Secure-1PSID`, `__Secure-3PSID`
4. Store encrypted via `TokenEncryption.cs`

**Option B: Programmatic login via Playwright (one-time)**
1. Use Playwright to navigate to Google login
2. Human enters credentials once
3. Extract cookies from browser context
4. Save to encrypted token store
5. Reuse until expired (tokens last weeks/months)

### Building the Authorization Header
```
SAPISIDHASH <timestamp>_<sha1(timestamp + " " + SAPISID + " " + origin)>
```
Where `origin` = `https://voice.google.com`

### Required Request Headers
```http
POST /voice/v1/voiceclient/api2thread/list?alt=protojson&key=AIzaSyDTYc1N4xiODyrQYK0Kl6g_y279LjYkrBg HTTP/1.1
Host: clients6.google.com
Content-Type: application/json+protobuf
Authorization: SAPISIDHASH <hash>
Cookie: SID=<sid>; HSID=<hsid>; SSID=<ssid>; APISID=<apisid>; SAPISID=<sapisid>
Origin: https://voice.google.com
X-Goog-AuthUser: 0
```

---

## Making API Calls (HttpClient)

### Basic Pattern
```csharp
public class GvVoiceClient
{
    private readonly HttpClient _http;
    private readonly string _apiKey = "AIzaSyDTYc1N4xiODyrQYK0Kl6g_y279LjYkrBg";
    private readonly string _baseUrl = "https://clients6.google.com/voice/v1/voiceclient";

    public async Task<string> CallEndpointAsync(string endpoint, string protojsonBody)
    {
        var url = $"{_baseUrl}/{endpoint}?alt=protojson&key={_apiKey}";
        var content = new StringContent(protojsonBody, Encoding.UTF8, "application/json+protobuf");
        var response = await _http.PostAsync(url, content);
        return await response.Content.ReadAsStringAsync();
    }
}
```

### List Calls
```csharp
var calls = await client.CallEndpointAsync("api2thread/list", "[1,20,15,null,null,[null,1,1,1]]");
// Type 1 = calls, 20 = page size, 15 = flags
```

### List Voicemails
```csharp
var voicemails = await client.CallEndpointAsync("api2thread/list", "[4,20,15,null,null,[null,1,1,1]]");
```

### Send SMS
```csharp
var sms = await client.CallEndpointAsync("api2thread/sendsms",
    $"[null,null,null,null,\"{messageText}\",\"t.{phoneNumber}\",null,null,[{deviceId}]]");
```

### Delete Thread
```csharp
var result = await client.CallEndpointAsync("thread/batchdelete",
    $"[[[\"{threadId}\"]]]");
```

### Archive Thread
```csharp
var result = await client.CallEndpointAsync("thread/batchupdateattributes",
    $"[[[[\"{threadId}\",null,null,null,null,1],[null,null,null,null,null,1],1]]]");
```

### Search
```csharp
var results = await client.CallEndpointAsync("api2thread/search",
    $"[\"{query}\",20]");
```

### Get Account Info
```csharp
var account = await client.CallEndpointAsync("account/get", "[null,1]");
// Returns 28KB with all settings, devices, linked numbers
```

---

## Making Calls Without a Browser

VoIP calls require WebRTC, which needs a browser or a WebRTC library. Options:

### Option A: SIP Gateway (recommended)
The `sipregisterinfo/get` endpoint returns SIP registration tokens. Use these with a SIP client:
1. Call `sipregisterinfo/get` with `[3,"<deviceId>"]`
2. Extract SIP credentials from response
3. Register with Google's SIP server using a SIP library (e.g., `pjsip`, `oSIP`)
4. Place/receive calls via SIP→PSTN bridge

### Option B: Headless WebRTC
Use a Node.js or .NET WebRTC library (e.g., `node-webrtc`, `SIPSorcery`):
1. Get SIP registration tokens
2. Connect to signaler channel for call setup
3. Create RTCPeerConnection with `stun:stun.l.google.com:19302`
4. Handle SDP offer/answer exchange via signaler
5. Stream audio via Opus codec

### Option C: Callback to Phone (simplest)
GV can bridge calls through a linked phone number:
1. Call `account/get` to find linked devices
2. Initiate call via the web API (the exact endpoint wasn't captured — this may use the signaler channel)
3. GV rings your linked phone first, then bridges to the destination

---

## Real-Time Notifications Without a Browser

The signaler long-poll channel provides real-time push. To replicate headless:

```csharp
// 1. Choose server
var server = await PostAsync("signaler-pa.clients6.google.com/punctual/v1/chooseServer", "{}");

// 2. Open channel (long-poll GET that blocks until data arrives)
var channelUrl = $"signaler-pa.clients6.google.com/punctual/multi-watch/channel" +
    $"?VER=8&key={apiKey}&RID=rpc&SID={sessionId}&AID={ackId}&CI=0&TYPE=xmlhttp";

// 3. Poll in a loop
while (true) {
    var events = await GetAsync(channelUrl); // blocks until server pushes
    // Parse events: incoming calls, new SMS, voicemail notifications
    // Update AID (acknowledgment ID) for next poll
}
```

---

## Protobuf-JSON Parsing

GV responses are **protobuf serialized as nested JSON arrays**, not JSON objects. Field positions are fixed by the .proto schema. To work with this:

### Approach 1: Positional Access (quick and dirty)
```csharp
var doc = JsonDocument.Parse(response);
var threads = doc.RootElement[0]; // first element is thread array
foreach (var thread in threads.EnumerateArray())
{
    var threadId = thread[0].GetString();       // position 0 = thread ID
    var messages = thread[2];                    // position 2 = message array
    var firstMsg = messages[0];
    var text = firstMsg[11].GetString();         // position 11 = SMS text
    var timestamp = firstMsg[1].GetInt64();      // position 1 = epoch ms
}
```

### Approach 2: Typed Deserializers (production-quality)
Build C# records that map field positions to named properties:
```csharp
public record GvThread(string Id, int ReadStatus, GvMessage[] Messages);
public record GvMessage(string Hash, long Timestamp, string AccountPhone,
    GvParty RemoteParty, int Type, int ReadStatus,
    GvTranscription? Transcription, int? Duration, string? Text);
```

### Approach 3: Proto Definition Reconstruction
Reverse-engineer `.proto` definitions from the observed data and use `Google.Protobuf` for proper deserialization.

---

## Rate Limiting Recommendations

Based on observed GV behavior:
- **Thread listing:** Called frequently (every tab switch) — safe at 10+ req/min
- **Account get:** Called on every navigation — safe at 10+ req/min
- **Send SMS:** Presumably rate-limited server-side — limit to 1 req/5sec
- **Batch operations:** Limit to 5 req/min
- **Search:** Limit to 3 req/min
- **SIP registration:** Once per session (token lasts hours)

---

## What Cannot Be Done Without a Browser

| Action | Reason | Workaround |
|---|---|---|
| Initial Google login | Requires CAPTCHA, 2FA interaction | One-time Playwright login, then reuse cookies |
| Active VoIP calls | Requires WebRTC media stack | SIP library or callback-to-phone |
| Voicemail audio playback | Signed URLs work via HTTP GET (no browser needed!) | Direct `GET` on the `voice/media/svm/` URL with cookies |
| Real-time incoming call handling | Requires WebRTC answer | SIP library for SDP negotiation |
