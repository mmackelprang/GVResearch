using System.Security.Cryptography;
using System.Text;
using GvResearch.Shared.Exceptions;

namespace GvResearch.Shared.Auth;

public sealed class GvAuthService : IGvAuthService, IDisposable
{
    private readonly string _cookiePath;
    private readonly string _keyPath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private GvCookieSet? _cachedCookies;
    private DateTimeOffset _lastHealthCheck;
    private bool _disposed;

    /// <summary>
    /// How often to proactively re-check cookie health.
    /// COMPASS cookies expire ~10 days, __Secure-*PSIDRTS rotates daily.
    /// Checking every 4 hours is conservative enough to catch expiry early.
    /// </summary>
    public static readonly TimeSpan HealthCheckInterval = TimeSpan.FromHours(4);

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
            // If cookies are cached but stale, refresh proactively
            if (_cachedCookies is not null &&
                DateTimeOffset.UtcNow - _lastHealthCheck > HealthCheckInterval)
            {
                if (!await HealthCheckAsync(_cachedCookies, ct).ConfigureAwait(false))
                {
                    _cachedCookies = null; // force re-extraction
                }
                else
                {
                    _lastHealthCheck = DateTimeOffset.UtcNow;
                }
            }

            if (_cachedCookies is not null)
                return _cachedCookies;

            // Try loading from disk first
            if (File.Exists(_cookiePath) && File.Exists(_keyPath))
            {
                var key = await File.ReadAllBytesAsync(_keyPath, ct).ConfigureAwait(false);
                var ciphertext = await File.ReadAllBytesAsync(_cookiePath, ct).ConfigureAwait(false);
                var json = TokenEncryption.Decrypt(ciphertext, key);
                var loaded = GvCookieSet.Deserialize(json);

                // Verify loaded cookies still work
                if (await HealthCheckAsync(loaded, ct).ConfigureAwait(false))
                {
                    _cachedCookies = loaded;
                    _lastHealthCheck = DateTimeOffset.UtcNow;
                    return _cachedCookies;
                }
            }

            // Cookies missing or expired — auto-refresh from Chrome
            await RetrieveFreshCookiesAsync(ct).ConfigureAwait(false);

            return _cachedCookies
                ?? throw new GvAuthException("Failed to obtain valid cookies.");
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task LoginInteractiveAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await RetrieveFreshCookiesAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task RefreshCookiesAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _cachedCookies = null;
            await RetrieveFreshCookiesAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    public string ComputeSapisidHash(string sapisid, string origin)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sapisid);
        ArgumentException.ThrowIfNullOrWhiteSpace(origin);

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var input = string.Concat(
            timestamp.ToString(System.Globalization.CultureInfo.InvariantCulture),
            " ",
            sapisid,
            " ",
            origin);
#pragma warning disable CA5350 // SHA1 is required by the Google SAPISIDHASH specification
        var hash = Convert.ToHexStringLower(
            SHA1.HashData(Encoding.UTF8.GetBytes(input)));
#pragma warning restore CA5350
        var ts = timestamp.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return $"SAPISIDHASH {ts}_{hash} SAPISID1PHASH {ts}_{hash} SAPISID3PHASH {ts}_{hash}";
    }

    private async Task RetrieveFreshCookiesAsync(CancellationToken ct)
    {
        var ok = await CookieRetriever.RetrieveAndSaveAsync(
            _cookiePath, _keyPath, log: null, ct).ConfigureAwait(false);
        if (!ok)
            throw new GvAuthException(
                "Cookie retrieval failed. Ensure Chrome is available and you are logged in to Google Voice.");

        var key = await File.ReadAllBytesAsync(_keyPath, ct).ConfigureAwait(false);
        var ciphertext = await File.ReadAllBytesAsync(_cookiePath, ct).ConfigureAwait(false);
        var json = TokenEncryption.Decrypt(ciphertext, key);
        _cachedCookies = GvCookieSet.Deserialize(json);
        _lastHealthCheck = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Quick health check — calls account/get to see if cookies are still valid.
    /// </summary>
    private static async Task<bool> HealthCheckAsync(GvCookieSet cookies, CancellationToken ct)
    {
        using var http = new HttpClient();
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var input = $"{ts} {cookies.Sapisid} https://voice.google.com";
#pragma warning disable CA5350
        var hash = Convert.ToHexStringLower(
            SHA1.HashData(Encoding.UTF8.GetBytes(input)));
#pragma warning restore CA5350
        var auth = $"SAPISIDHASH {ts}_{hash} SAPISID1PHASH {ts}_{hash} SAPISID3PHASH {ts}_{hash}";

        using var req = new HttpRequestMessage(HttpMethod.Post,
            "https://clients6.google.com/voice/v1/voiceclient/account/get?alt=protojson&key=AIzaSyDTYc1N4xiODyrQYK0Kl6g_y279LjYkrBg");
        req.Headers.TryAddWithoutValidation("Authorization", auth);
        req.Headers.TryAddWithoutValidation("Cookie", cookies.ToCookieHeader());
        req.Headers.TryAddWithoutValidation("X-Goog-AuthUser", "0");
        req.Headers.TryAddWithoutValidation("Origin", "https://voice.google.com");
        req.Headers.TryAddWithoutValidation("Referer", "https://voice.google.com/");
        req.Content = new StringContent("[null,1]", Encoding.UTF8, "application/json+protobuf");

#pragma warning disable CA1031
        try
        {
            var resp = await http.SendAsync(req, ct).ConfigureAwait(false);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false; // network error — treat as invalid
        }
#pragma warning restore CA1031
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cachedCookies = null;
        _lock.Dispose();
    }
}
