using GvResearch.Shared.Auth;

namespace GvResearch.Shared.Http;

public sealed class GvHttpClientHandler : DelegatingHandler
{
    private readonly IGvAuthService _authService;

    public GvHttpClientHandler(IGvAuthService authService, HttpMessageHandler innerHandler)
        : base(innerHandler)
    {
        ArgumentNullException.ThrowIfNull(authService);
        _authService = authService;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var response = await SendWithAuthAsync(request, cancellationToken).ConfigureAwait(false);

        // If 401, cookies may have expired — refresh and retry once
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            await _authService.RefreshCookiesAsync(cancellationToken).ConfigureAwait(false);
            response = await SendWithAuthAsync(request, cancellationToken).ConfigureAwait(false);
        }

        return response;
    }

    private async Task<HttpResponseMessage> SendWithAuthAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        var cookies = await _authService.GetValidCookiesAsync(ct).ConfigureAwait(false);
        var hash = _authService.ComputeSapisidHash(cookies.Sapisid, "https://voice.google.com");

        // Remove before adding so retries do not duplicate headers.
        request.Headers.Remove("Authorization");
        request.Headers.Remove("Cookie");
        request.Headers.Remove("X-Goog-AuthUser");
        request.Headers.Remove("Origin");
        request.Headers.Remove("Referer");

        request.Headers.Add("Authorization", hash);
        request.Headers.Add("Cookie", cookies.ToCookieHeader());
        request.Headers.Add("X-Goog-AuthUser", "0");
        request.Headers.Add("Origin", "https://voice.google.com");
        request.Headers.Add("Referer", "https://voice.google.com/");

        return await base.SendAsync(request, ct).ConfigureAwait(false);
    }
}
