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
    private bool _disposed;

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
            if (_cachedCookies is not null)
                return _cachedCookies;

            if (!File.Exists(_cookiePath) || !File.Exists(_keyPath))
                throw new GvAuthException("Cookie file not found. Run LoginInteractiveAsync() first.");

            var key = await File.ReadAllBytesAsync(_keyPath, ct).ConfigureAwait(false);
            var ciphertext = await File.ReadAllBytesAsync(_cookiePath, ct).ConfigureAwait(false);
            var json = TokenEncryption.Decrypt(ciphertext, key);
            _cachedCookies = GvCookieSet.Deserialize(json);
            return _cachedCookies;
        }
        finally
        {
            _lock.Release();
        }
    }

    public Task LoginInteractiveAsync(CancellationToken ct = default)
    {
        // Playwright-based interactive login — deferred to a follow-up plan.
        // For now, the user must manually populate the encrypted cookie file.
        throw new NotImplementedException(
            "Interactive login not yet implemented. " +
            "Manually populate the encrypted cookie file.");
    }

    // NOTE: TryRefreshSessionAsync (from the spec's auth flow diagram) is deferred.
    // The full health-check + refresh + re-login cascade requires an HttpClient
    // (for the threadinginfo/get health check) and Playwright (for interactive login).
    // This plan implements the core cookie-loading + SAPISIDHASH path.
    // The refresh/re-login cascade will be added in a follow-up plan alongside
    // LoginInteractiveAsync and the Playwright dependency.

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
        return string.Concat(
            "SAPISIDHASH ",
            timestamp.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "_",
            hash);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cachedCookies = null;
        _lock.Dispose();
    }
}
