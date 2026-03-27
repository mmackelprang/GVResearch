namespace GvResearch.Shared.Auth;

public interface IGvAuthService
{
    Task<GvCookieSet> GetValidCookiesAsync(CancellationToken ct = default);
    Task LoginInteractiveAsync(CancellationToken ct = default);
    string ComputeSapisidHash(string sapisid, string origin);
}
