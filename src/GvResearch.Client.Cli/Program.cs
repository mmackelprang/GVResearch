using System.Text;
using System.Text.Json;
using GvResearch.Shared.Auth;
using Microsoft.Playwright;

var captureSignaler = args.Contains("--capture-signaler", StringComparer.OrdinalIgnoreCase);
var outputDir = args.FirstOrDefault(a => a.StartsWith("--output=", StringComparison.OrdinalIgnoreCase))
    ?["--output=".Length..] ?? Directory.GetCurrentDirectory();

Console.WriteLine("GV Cookie Extractor");
Console.WriteLine("===================");

// Install Playwright browsers if needed
Console.WriteLine("Checking Playwright browsers...");
var exitCode = Microsoft.Playwright.Program.Main(["install", "chromium"]);
if (exitCode != 0)
{
    Console.Error.WriteLine($"Playwright browser install failed (exit {exitCode}).");
    return 1;
}

using var playwright = await Playwright.CreateAsync().ConfigureAwait(false);

// Try launching with user's Chrome profile first
IBrowserContext? browserContext = null;
IBrowser? standaloneBrowser = null;

var chromeProfilePath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "Google", "Chrome", "User Data");

if (Directory.Exists(chromeProfilePath))
{
    Console.WriteLine("Attempting to launch with your Chrome profile...");
    try
    {
        browserContext = await playwright.Chromium.LaunchPersistentContextAsync(
            chromeProfilePath,
            new BrowserTypeLaunchPersistentContextOptions
            {
                Channel = "chrome",
                Headless = false,
                Args = ["--disable-blink-features=AutomationControlled"],
            }).ConfigureAwait(false);
        Console.WriteLine("Chrome launched with your profile.");
    }
#pragma warning disable CA1031 // Profile lock is expected when Chrome is running
    catch (PlaywrightException ex)
#pragma warning restore CA1031
    {
        Console.WriteLine($"Could not use Chrome profile: {ex.Message.Split('\n')[0]}");
        Console.WriteLine();
        Console.WriteLine("Chrome is likely running. Options:");
        Console.WriteLine("  1. Close Chrome and re-run this tool");
        Console.WriteLine("  2. Press Enter to open a fresh browser (you'll need to log in)");
        Console.Write("> ");
        Console.ReadLine();
    }
}

if (browserContext is null)
{
    Console.WriteLine("Launching fresh Chromium...");
    standaloneBrowser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
    {
        Headless = false,
    }).ConfigureAwait(false);
    browserContext = await standaloneBrowser.NewContextAsync().ConfigureAwait(false);
}

// Get or create a page
var page = browserContext.Pages.Count > 0
    ? browserContext.Pages[0]
    : await browserContext.NewPageAsync().ConfigureAwait(false);

// Navigate to voice.google.com
Console.WriteLine("Navigating to voice.google.com...");
await page.GotoAsync("https://voice.google.com", new PageGotoOptions
{
    WaitUntil = WaitUntilState.NetworkIdle,
    Timeout = 120_000,
}).ConfigureAwait(false);

// Check if we need to log in
var url = page.Url;
if (url.Contains("accounts.google.com", StringComparison.OrdinalIgnoreCase) ||
    url.Contains("signin", StringComparison.OrdinalIgnoreCase))
{
    Console.WriteLine();
    Console.WriteLine("You need to log in to Google. Complete the login in the browser window.");
    Console.WriteLine("Waiting for voice.google.com to load (timeout: 120s)...");

    await page.WaitForURLAsync("**/voice.google.com/**", new PageWaitForURLOptions
    {
        Timeout = 120_000,
    }).ConfigureAwait(false);

    // Wait for page to fully load after login redirect
    await page.WaitForLoadStateAsync(LoadState.NetworkIdle).ConfigureAwait(false);
}

Console.WriteLine($"Logged in! Current URL: {page.Url}");

// Extract ALL cookies for google.com domains
var allCookies = await browserContext.CookiesAsync([
    "https://voice.google.com",
    "https://clients6.google.com",
    "https://signaler-pa.clients6.google.com",
]).ConfigureAwait(false);

Console.WriteLine($"Extracted {allCookies.Count} cookies.");

// Build the raw cookie header (all cookies, semicolon-separated)
var cookieHeader = string.Join("; ", allCookies.Select(c => $"{c.Name}={c.Value}"));

// Extract key cookies for GvCookieSet fields
string GetCookie(string name) =>
    allCookies.FirstOrDefault(c => c.Name == name)?.Value ?? string.Empty;

var sapisid = GetCookie("SAPISID");
if (string.IsNullOrEmpty(sapisid))
{
    Console.Error.WriteLine("ERROR: SAPISID cookie not found. Are you logged in to the correct Google account?");
    if (standaloneBrowser is not null)
        await standaloneBrowser.CloseAsync().ConfigureAwait(false);
    else
        await browserContext.CloseAsync().ConfigureAwait(false);
    return 1;
}

Console.WriteLine($"SAPISID: {sapisid[..10]}... ({sapisid.Length} chars)");
Console.WriteLine($"Cookie header: {cookieHeader.Length} chars total");

// Capture signaler traffic if requested
List<object>? signalerCaptures = null;
if (captureSignaler)
{
    Console.WriteLine();
    Console.WriteLine("Capturing signaler traffic for 15 seconds...");
    signalerCaptures = [];

    page.Request += (_, request) =>
    {
        if (request.Url.Contains("signaler-pa.clients6.google.com", StringComparison.OrdinalIgnoreCase))
        {
            signalerCaptures.Add(new
            {
                Type = "request",
                Timestamp = DateTimeOffset.UtcNow.ToString("o"),
                request.Url,
                Method = request.Method,
                Headers = request.Headers,
                PostData = request.PostData,
            });
            Console.WriteLine($"  [REQ] {request.Method} {request.Url[..Math.Min(120, request.Url.Length)]}...");
        }
    };

    page.Response += (_, response) =>
    {
        if (response.Url.Contains("signaler-pa.clients6.google.com", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"  [RES] {response.Status} {response.Url[..Math.Min(120, response.Url.Length)]}...");
            _ = Task.Run(async () =>
            {
                string? body = null;
                try { body = await response.TextAsync().ConfigureAwait(false); }
#pragma warning disable CA1031
                catch { /* response body may not be available */ }
#pragma warning restore CA1031
                signalerCaptures.Add(new
                {
                    Type = "response",
                    Timestamp = DateTimeOffset.UtcNow.ToString("o"),
                    response.Url,
                    Status = response.Status,
                    ResponseBody = body,
                });
            });
        }
    };

    // Reload to trigger signaler connection
    await page.ReloadAsync(new PageReloadOptions { WaitUntil = WaitUntilState.NetworkIdle }).ConfigureAwait(false);
    Console.WriteLine("Waiting 15 seconds for signaler traffic...");
    await Task.Delay(15_000).ConfigureAwait(false);

    Console.WriteLine($"Captured {signalerCaptures.Count} signaler requests/responses.");

    // Save captures
    var capturesDir = Path.Combine(outputDir, "captures");
    Directory.CreateDirectory(capturesDir);
    var captureFile = Path.Combine(capturesDir,
        $"signaler-capture-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.json");
    var captureJson = JsonSerializer.Serialize(signalerCaptures,
        new JsonSerializerOptions { WriteIndented = true });
    await File.WriteAllTextAsync(captureFile, captureJson).ConfigureAwait(false);
    Console.WriteLine($"Saved signaler capture to: {captureFile}");
}

// Build GvCookieSet and encrypt
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

// Quick verification
Console.WriteLine();
Console.WriteLine("Verifying cookies with account/get...");
using var httpClient = new HttpClient();
var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
var hashInput = $"{timestamp} {sapisid} https://voice.google.com";
#pragma warning disable CA5350 // SHA1 required by Google SAPISIDHASH
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
    Console.Error.WriteLine("WARNING: Cookies may be invalid. Check the output above.");

// Close browser
if (standaloneBrowser is not null)
    await standaloneBrowser.CloseAsync().ConfigureAwait(false);
else
    await browserContext.CloseAsync().ConfigureAwait(false);

Console.WriteLine("Done.");
return 0;
