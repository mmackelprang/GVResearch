# Google Voice Headless Integration Guide

How to use the discovered GV API without any browser.

---

## Authentication

GV uses Google's cookie-based auth, not OAuth2 bearer tokens. A browser is needed **once** to sign in; after that, the entire API can be driven headless for weeks or months.

### Auth Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Auth Flow Decision Tree                    │
│                                                              │
│  Start → Check encrypted cookie file on disk                 │
│    │                                                         │
│    ├─ Cookies exist + SAPISID present                        │
│    │   └─ Try health check: POST threadinginfo/get           │
│    │       ├─ 200 OK → USE COOKIES (fully headless)          │
│    │       └─ 401/403 → Try cookie refresh                   │
│    │           ├─ Refresh succeeds → USE NEW COOKIES          │
│    │           └─ Refresh fails → BROWSER LOGIN NEEDED        │
│    │                                                         │
│    └─ No cookies on disk → BROWSER LOGIN NEEDED              │
│                                                              │
│  BROWSER LOGIN (rare — once per weeks/months):               │
│    1. Launch Playwright → accounts.google.com                │
│    2. Human types email + password + 2FA (~10 seconds)       │
│    3. Extract all cookies from browser context               │
│    4. Encrypt and save to disk                               │
│    5. Close browser → fully headless from here               │
└─────────────────────────────────────────────────────────────┘
```

### Required Cookies

| Cookie | Domain | Lifetime | Purpose |
|---|---|---|---|
| **SAPISID** | `.google.com` | Months | Used to compute SAPISIDHASH authorization |
| **SID** | `.google.com` | Months | Primary session identifier |
| **HSID** | `.google.com` | Months | HTTP-only session ID |
| **SSID** | `.google.com` | Months | Secure session ID |
| **APISID** | `.google.com` | Months | API session ID |
| `__Secure-1PSID` | `.google.com` | Months | Secure primary session (backup) |
| `__Secure-3PSID` | `.google.com` | Months | Secure tertiary session (backup) |

### Building the SAPISIDHASH Authorization Header

```csharp
public static string BuildSapisidHash(string sapisid, string origin = "https://voice.google.com")
{
    var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    var input = $"{timestamp} {sapisid} {origin}";
    var hash = Convert.ToHexStringLower(SHA1.HashData(Encoding.UTF8.GetBytes(input)));
    return $"SAPISIDHASH {timestamp}_{hash}";
}
```

### Required Request Headers

```http
POST /voice/v1/voiceclient/api2thread/list?alt=protojson&key={GV_API_KEY} HTTP/1.1
Host: clients6.google.com
Content-Type: application/json+protobuf
Authorization: SAPISIDHASH <timestamp>_<sha1hash>
Cookie: SID=<sid>; HSID=<hsid>; SSID=<ssid>; APISID=<apisid>; SAPISID=<sapisid>
Origin: https://voice.google.com
Referer: https://voice.google.com/
X-Goog-AuthUser: 0
```

### Obtaining Cookies — Playwright One-Time Login

```csharp
public class GvAuthService
{
    private readonly string _cookiePath;  // encrypted cookie file

    /// <summary>
    /// Interactive login — launches browser, human enters credentials.
    /// Call this only when headless auth fails.
    /// </summary>
    public async Task<GvCookieSet> LoginInteractiveAsync()
    {
        using var playwright = await Playwright.CreateAsync();
        var browser = await playwright.Chromium.LaunchAsync(new() { Headless = false });
        var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync("https://accounts.google.com/ServiceLogin?continue=https://voice.google.com");

        // Wait for human to complete login + 2FA
        // Detect success by watching for voice.google.com URL
        await page.WaitForURLAsync("**/voice.google.com/**", new() { Timeout = 120_000 });

        // Extract all cookies
        var cookies = await context.CookiesAsync();
        var cookieSet = new GvCookieSet
        {
            Sapisid  = cookies.First(c => c.Name == "SAPISID").Value,
            Sid      = cookies.First(c => c.Name == "SID").Value,
            Hsid     = cookies.First(c => c.Name == "HSID").Value,
            Ssid     = cookies.First(c => c.Name == "SSID").Value,
            Apisid   = cookies.First(c => c.Name == "APISID").Value,
            ExtraSecure = cookies.Where(c => c.Name.StartsWith("__Secure"))
                                 .ToDictionary(c => c.Name, c => c.Value),
            ObtainedAt = DateTimeOffset.UtcNow
        };

        // Encrypt and save
        var json = JsonSerializer.Serialize(cookieSet);
        var encrypted = TokenEncryption.Encrypt(json, _encryptionKey);
        await File.WriteAllBytesAsync(_cookiePath, encrypted);

        await browser.CloseAsync();
        return cookieSet;
    }

    /// <summary>
    /// Headless auth — loads cookies from disk, validates, refreshes if needed.
    /// Returns null if browser login is required.
    /// </summary>
    public async Task<GvCookieSet?> GetValidCookiesAsync()
    {
        if (!File.Exists(_cookiePath))
            return null;

        var encrypted = await File.ReadAllBytesAsync(_cookiePath);
        var json = TokenEncryption.Decrypt(encrypted, _encryptionKey);
        var cookies = JsonSerializer.Deserialize<GvCookieSet>(json);

        if (cookies?.Sapisid is null)
            return null;

        // Health check — try a lightweight API call
        if (await IsSessionValidAsync(cookies))
            return cookies;

        // Try cookie refresh
        var refreshed = await TryRefreshSessionAsync(cookies);
        if (refreshed is not null)
        {
            // Save refreshed cookies
            var newJson = JsonSerializer.Serialize(refreshed);
            var newEncrypted = TokenEncryption.Encrypt(newJson, _encryptionKey);
            await File.WriteAllBytesAsync(_cookiePath, newEncrypted);
            return refreshed;
        }

        return null; // Browser login needed
    }

    private async Task<bool> IsSessionValidAsync(GvCookieSet cookies)
    {
        // threadinginfo/get is lightweight and tells us if auth works
        try
        {
            var response = await CallGvApiAsync(cookies, "threadinginfo/get", "[]");
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    private async Task<GvCookieSet?> TryRefreshSessionAsync(GvCookieSet cookies)
    {
        // Google's RotateCookies endpoint may extend the session
        // This is undocumented and may not always work
        try
        {
            using var http = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Post,
                "https://accounts.google.com/RotateCookies");
            request.Headers.Add("Cookie", cookies.ToCookieHeader());
            var response = await http.SendAsync(request);

            if (!response.IsSuccessStatusCode)
                return null;

            // Extract refreshed cookies from Set-Cookie headers
            // ... parse and return updated GvCookieSet
            return null; // TODO: implement cookie parsing from response
        }
        catch { return null; }
    }
}
```

### CLI Auth Commands

The recommended CLI flow:

```bash
# First time — opens browser for login (human interaction ~10 seconds)
gvresearch auth login

# Check if cookies are still valid (headless)
gvresearch auth status
# Output: "Session valid. SAPISID expires in ~47 days. Last validated: 2 minutes ago."

# Force refresh without browser (may fail if session too old)
gvresearch auth refresh

# All other commands work headless using stored cookies
gvresearch sms send --to "+19193718044" --text "Hello from CLI"
gvresearch calls list --limit 20
gvresearch voicemail list
gvresearch thread search "appointment"
```

### Cookie Lifetime and Refresh Strategy

| Cookie | Typical Lifetime | Refresh Method |
|---|---|---|
| SAPISID | 2+ months | Cannot refresh — lasts until Google invalidates |
| SID/HSID | 2+ months | `RotateCookies` may extend |
| SSID | 2+ months | Rotated with SID |
| APISID | 2+ months | Rotated with SID |

**Recommended refresh strategy:**
1. **On every CLI invocation:** Check `ObtainedAt` age. If < 24 hours, skip validation.
2. **If > 24 hours since last validation:** Run `threadinginfo/get` health check.
3. **If health check fails:** Try `RotateCookies`.
4. **If refresh fails:** Print `"Session expired. Run 'gvresearch auth login' to re-authenticate."` and exit.
5. **Never auto-launch a browser** — always require explicit `auth login` command.

### Alternative: Android App Token Path (experimental, fully browserless)

For truly zero-browser auth, the Google Voice Android app uses a different auth mechanism:

1. Install Google Voice APK in an Android emulator
2. Sign in via Google Play Services (one-time emulator interaction)
3. Extract the OAuth2 refresh token from the app's token store:
   ```bash
   adb shell "su -c 'cat /data/data/com.google.android.apps.googlevoice/shared_prefs/oauth2_tokens.xml'"
   ```
4. Use the refresh token to get access tokens programmatically:
   ```
   POST https://oauth2.googleapis.com/token
   grant_type=refresh_token&refresh_token=<token>&client_id=<gv_android_client_id>
   ```
5. Use the access token with GV's mobile API endpoints (may differ from web endpoints)

**Status:** Unverified. The mobile API surface has not been investigated. Web API endpoints documented here may or may not accept OAuth2 bearer tokens.

---

## Making API Calls (HttpClient)

### Basic Pattern
```csharp
public class GvVoiceClient
{
    private readonly HttpClient _http;
    private readonly string _apiKey = "{GV_API_KEY}";
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
