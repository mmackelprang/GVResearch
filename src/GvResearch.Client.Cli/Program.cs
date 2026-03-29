using System.Diagnostics;
using System.Text;
using System.Text.Json;
using GvResearch.Shared.Auth;
using Microsoft.Playwright;

var captureSignaler = args.Contains("--capture-signaler", StringComparer.OrdinalIgnoreCase);
var outputDir = args.FirstOrDefault(a => a.StartsWith("--output=", StringComparison.OrdinalIgnoreCase))
    ?["--output=".Length..] ?? Directory.GetCurrentDirectory();
var debugPort = 9222;

Console.WriteLine("GV Cookie Extractor");
Console.WriteLine("===================");
Console.WriteLine();

// Step 1: Ensure Chrome is running with remote debugging
var chromeConnected = false;
IBrowser? browser = null;

// Check if Chrome is already running with debugging port
using var playwright = await Playwright.CreateAsync().ConfigureAwait(false);

try
{
    browser = await playwright.Chromium.ConnectOverCDPAsync($"http://127.0.0.1:{debugPort}").ConfigureAwait(false);
    Console.WriteLine($"Connected to Chrome on port {debugPort}.");
    chromeConnected = true;
}
#pragma warning disable CA1031
catch
#pragma warning restore CA1031
{
    // Chrome not running with debugging port
}

if (!chromeConnected)
{
    Console.WriteLine($"Chrome is not running with remote debugging on port {debugPort}.");
    Console.WriteLine();
    Console.WriteLine("Please restart Chrome with this command (close Chrome first):");
    Console.WriteLine();

    var chromePath = FindChromePath();
    var profilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Google", "Chrome", "User Data");

    Console.WriteLine($"  \"{chromePath}\" --remote-debugging-port={debugPort} --user-data-dir=\"{profilePath}\"");
    Console.WriteLine();
    Console.WriteLine("Or on Windows, from Run (Win+R):");
    Console.WriteLine($"  chrome --remote-debugging-port={debugPort}");
    Console.WriteLine();
    Console.Write("Press Enter once Chrome is running with debugging enabled... ");
    Console.ReadLine();

    try
    {
        browser = await playwright.Chromium.ConnectOverCDPAsync($"http://127.0.0.1:{debugPort}").ConfigureAwait(false);
        Console.WriteLine("Connected!");
        chromeConnected = true;
    }
    catch (PlaywrightException ex)
    {
        Console.Error.WriteLine($"Failed to connect: {ex.Message.Split('\n')[0]}");
        Console.Error.WriteLine("Make sure Chrome is running with --remote-debugging-port=9222");
        return 1;
    }
}

// Step 2: Get existing contexts or create a page
var contexts = browser!.Contexts;
IBrowserContext context;
IPage page;

if (contexts.Count > 0)
{
    context = contexts[0];
    page = context.Pages.FirstOrDefault(p =>
        p.Url.Contains("voice.google.com", StringComparison.OrdinalIgnoreCase))
        ?? context.Pages[0];
    Console.WriteLine($"Using existing page: {page.Url}");
}
else
{
    context = await browser.NewContextAsync().ConfigureAwait(false);
    page = await context.NewPageAsync().ConfigureAwait(false);
}

// Step 3: Navigate to voice.google.com if not already there
if (!page.Url.Contains("voice.google.com", StringComparison.OrdinalIgnoreCase))
{
    Console.WriteLine("Navigating to voice.google.com...");
    await page.GotoAsync("https://voice.google.com", new PageGotoOptions
    {
        WaitUntil = WaitUntilState.NetworkIdle,
        Timeout = 30_000,
    }).ConfigureAwait(false);
}

// Check if logged in
if (!page.Url.Contains("voice.google.com", StringComparison.OrdinalIgnoreCase) ||
    page.Url.Contains("workspace.google.com", StringComparison.OrdinalIgnoreCase))
{
    Console.WriteLine("Not logged in to Google Voice. Please log in via Chrome and re-run.");
    return 1;
}

Console.WriteLine($"Logged in! URL: {page.Url}");

// Step 4: Extract ALL cookies
var allCookies = await context.CookiesAsync([
    "https://voice.google.com",
    "https://clients6.google.com",
    "https://signaler-pa.clients6.google.com",
    "https://www.google.com",
]).ConfigureAwait(false);

Console.WriteLine($"Extracted {allCookies.Count} cookies.");

// Build raw cookie header
var cookieHeader = string.Join("; ", allCookies.Select(c => $"{c.Name}={c.Value}"));

string GetCookie(string name) =>
    allCookies.FirstOrDefault(c => c.Name == name)?.Value ?? string.Empty;

var sapisid = GetCookie("SAPISID");
if (string.IsNullOrEmpty(sapisid))
{
    Console.Error.WriteLine("ERROR: SAPISID cookie not found.");
    Console.Error.WriteLine("Make sure you're logged in to Google Voice in Chrome.");
    return 1;
}

Console.WriteLine($"SAPISID: {sapisid[..Math.Min(10, sapisid.Length)]}... ({sapisid.Length} chars)");
Console.WriteLine($"SID: {(GetCookie("SID").Length > 0 ? "present" : "MISSING")}");
Console.WriteLine($"SIDCC: {(GetCookie("SIDCC").Length > 0 ? "present" : "MISSING")}");
Console.WriteLine($"__Secure-1PSIDTS: {(GetCookie("__Secure-1PSIDTS").Length > 0 ? "present" : "MISSING")}");
Console.WriteLine($"Cookie header: {cookieHeader.Length} chars total");

// Step 5: Capture signaler traffic if requested
if (captureSignaler)
{
    Console.WriteLine();
    Console.WriteLine("Capturing signaler traffic for 15 seconds...");
    var signalerCaptures = new List<object>();

    // Use CDP to capture network traffic (more reliable than page events for existing pages)
    var cdpSession = await page.Context.NewCDPSessionAsync(page).ConfigureAwait(false);
    await cdpSession.SendAsync("Network.enable").ConfigureAwait(false);

    var requestBodies = new Dictionary<string, string>();

    cdpSession.Event("Network.requestWillBeSent").OnEvent += (sender, args) =>
    {
        var json = JsonSerializer.Deserialize<JsonElement>(args.ToString()!);
        var reqUrl = json.GetProperty("request").GetProperty("url").GetString() ?? "";
        if (reqUrl.Contains("signaler-pa.clients6.google.com", StringComparison.OrdinalIgnoreCase))
        {
            var requestId = json.GetProperty("requestId").GetString() ?? "";
            var method = json.GetProperty("request").GetProperty("method").GetString();
            var headers = json.GetProperty("request").GetProperty("headers");
            string? postData = null;
            if (json.GetProperty("request").TryGetProperty("postData", out var pd))
                postData = pd.GetString();

            signalerCaptures.Add(new
            {
                Type = "request",
                Timestamp = DateTimeOffset.UtcNow.ToString("o"),
                Url = reqUrl,
                Method = method,
                RequestId = requestId,
                Headers = headers.ToString(),
                PostData = postData,
            });
            Console.WriteLine($"  [REQ] {method} {reqUrl[..Math.Min(100, reqUrl.Length)]}...");
        }
    };

    cdpSession.Event("Network.responseReceived").OnEvent += (sender, args) =>
    {
        var json = JsonSerializer.Deserialize<JsonElement>(args.ToString()!);
        var respUrl = json.GetProperty("response").GetProperty("url").GetString() ?? "";
        if (respUrl.Contains("signaler-pa.clients6.google.com", StringComparison.OrdinalIgnoreCase))
        {
            var status = json.GetProperty("response").GetProperty("status").GetInt32();
            var requestId = json.GetProperty("requestId").GetString() ?? "";
            signalerCaptures.Add(new
            {
                Type = "response",
                Timestamp = DateTimeOffset.UtcNow.ToString("o"),
                Url = respUrl,
                Status = status,
                RequestId = requestId,
            });
            Console.WriteLine($"  [RES] {status} {respUrl[..Math.Min(100, respUrl.Length)]}...");

            // Try to get response body
            _ = Task.Run(async () =>
            {
                try
                {
                    var bodyResult = await cdpSession.SendAsync("Network.getResponseBody",
                        new Dictionary<string, object> { ["requestId"] = requestId }).ConfigureAwait(false);
                    var body = bodyResult?.Deserialize<JsonElement>();
                    if (body is not null)
                    {
                        var bodyText = body.Value.GetProperty("body").GetString();
                        signalerCaptures.Add(new
                        {
                            Type = "response-body",
                            RequestId = requestId,
                            Body = bodyText?[..Math.Min(10000, bodyText?.Length ?? 0)],
                        });
                    }
                }
#pragma warning disable CA1031
                catch { /* body may not be available */ }
#pragma warning restore CA1031
            });
        }
    };

    // Reload to trigger signaler connection
    await page.ReloadAsync(new PageReloadOptions { WaitUntil = WaitUntilState.NetworkIdle }).ConfigureAwait(false);
    Console.WriteLine("Waiting 15 seconds for signaler traffic...");
    await Task.Delay(15_000).ConfigureAwait(false);

    await cdpSession.SendAsync("Network.disable").ConfigureAwait(false);

    Console.WriteLine($"Captured {signalerCaptures.Count} signaler entries.");

    // Save captures
    var capturesDir = Path.Combine(outputDir, "captures");
    Directory.CreateDirectory(capturesDir);
    var captureFile = Path.Combine(capturesDir,
        $"signaler-capture-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.json");
    var captureJson = JsonSerializer.Serialize(signalerCaptures,
        new JsonSerializerOptions { WriteIndented = true });
    await File.WriteAllTextAsync(captureFile, captureJson).ConfigureAwait(false);
    Console.WriteLine($"Saved: {captureFile}");
}

// Step 6: Encrypt cookies
var cookieSet = new GvCookieSet
{
    Sapisid = sapisid,
    Sid = GetCookie("SID"),
    Hsid = GetCookie("HSID"),
    Ssid = GetCookie("SSID"),
    Apisid = GetCookie("APISID"),
    Secure1Psid = GetCookie("__Secure-1PSID"),
    Secure3Psid = GetCookie("__Secure-3PSID"),
    RawCookieHeader = cookieHeader,
};

var cookieSetJson = cookieSet.Serialize();
var encKey = new byte[32];
System.Security.Cryptography.RandomNumberGenerator.Fill(encKey);
var ciphertext = TokenEncryption.Encrypt(cookieSetJson, encKey);

var cookiePath = Path.Combine(outputDir, "cookies.enc");
var keyPath = Path.Combine(outputDir, "key.bin");
File.WriteAllBytes(cookiePath, ciphertext);
File.WriteAllBytes(keyPath, encKey);

Console.WriteLine();
Console.WriteLine($"cookies.enc: {ciphertext.Length} bytes -> {cookiePath}");
Console.WriteLine($"key.bin: 32 bytes -> {keyPath}");

// Step 7: Verify
Console.WriteLine();
Console.WriteLine("Verifying cookies with account/get...");
using var httpClient = new HttpClient();
var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
var hashInput = $"{timestamp} {sapisid} https://voice.google.com";
#pragma warning disable CA5350
var hash = Convert.ToHexStringLower(
    System.Security.Cryptography.SHA1.HashData(Encoding.UTF8.GetBytes(hashInput)));
#pragma warning restore CA5350
var authHeader = $"SAPISIDHASH {timestamp}_{hash} SAPISID1PHASH {timestamp}_{hash} SAPISID3PHASH {timestamp}_{hash}";

var verifyReq = new HttpRequestMessage(HttpMethod.Post,
    "https://clients6.google.com/voice/v1/voiceclient/account/get?alt=protojson&key=AIzaSyDTYc1N4xiODyrQYK0Kl6g_y279LjYkrBg");
verifyReq.Headers.TryAddWithoutValidation("Authorization", authHeader);
verifyReq.Headers.TryAddWithoutValidation("Cookie", cookieHeader);
verifyReq.Headers.TryAddWithoutValidation("X-Goog-AuthUser", "0");
verifyReq.Headers.TryAddWithoutValidation("Origin", "https://voice.google.com");
verifyReq.Headers.TryAddWithoutValidation("Referer", "https://voice.google.com/");
verifyReq.Content = new StringContent("[null,1]", Encoding.UTF8, "application/json+protobuf");

var verifyResp = await httpClient.SendAsync(verifyReq).ConfigureAwait(false);
Console.WriteLine($"account/get: {(int)verifyResp.StatusCode} {verifyResp.StatusCode}");

if (verifyResp.IsSuccessStatusCode)
    Console.WriteLine("Cookie extraction successful! Ready for GV API calls.");
else
    Console.Error.WriteLine("WARNING: Verification failed. Cookies may be expired.");

// Don't close browser — user keeps using Chrome
Console.WriteLine("Done. (Chrome stays open)");
return 0;

static string FindChromePath()
{
    var candidates = new[]
    {
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Google", "Chrome", "Application", "chrome.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Google", "Chrome", "Application", "chrome.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google", "Chrome", "Application", "chrome.exe"),
    };
    return candidates.FirstOrDefault(File.Exists) ?? "chrome";
}
