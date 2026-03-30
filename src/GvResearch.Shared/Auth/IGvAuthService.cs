namespace GvResearch.Shared.Auth;

public interface IGvAuthService
{
    Task<GvCookieSet> GetValidCookiesAsync(CancellationToken ct = default);
    Task LoginInteractiveAsync(CancellationToken ct = default);
    string ComputeSapisidHash(string sapisid, string origin);

    /// <summary>
    /// Invalidates cached cookies and re-extracts from Chrome on next access.
    /// Call this when a 401 is received from the GV API.
    /// </summary>
    Task RefreshCookiesAsync(CancellationToken ct = default);
}
