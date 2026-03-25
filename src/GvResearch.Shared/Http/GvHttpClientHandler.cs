using GvResearch.Shared.Authentication;

namespace GvResearch.Shared.Http;

/// <summary>
/// A <see cref="DelegatingHandler"/> that injects a Google Voice auth token into every
/// outgoing request via the Cookie header.
/// </summary>
/// <remarks>
/// TODO (Phase 1): Determine the exact header name/format required by GV after research.
/// Currently injects the token as a Cookie value named "GAPS".
/// </remarks>
public sealed class GvHttpClientHandler : DelegatingHandler
{
    private readonly IGvTokenService _tokenService;

    /// <summary>
    /// Initialises the handler with the required token service.
    /// The caller is responsible for providing a non-null <paramref name="innerHandler"/>;
    /// use <c>new HttpClientHandler()</c> for production or a test double for tests.
    /// </summary>
    /// <param name="tokenService">Source of the current GV auth token.</param>
    /// <param name="innerHandler">The inner HTTP handler.</param>
    public GvHttpClientHandler(IGvTokenService tokenService, HttpMessageHandler innerHandler)
        : base(innerHandler)
    {
        ArgumentNullException.ThrowIfNull(tokenService);
        _tokenService = tokenService;
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var token = await _tokenService.GetValidTokenAsync(cancellationToken).ConfigureAwait(false);

        // TODO (Phase 1): Replace "GAPS" with the actual cookie/header name used by GV.
        request.Headers.Add("Cookie", $"GAPS={token}");

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
