using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace GvResearch.Api.Auth;

/// <summary>
/// Minimal bearer-token authentication handler.
/// Validates that the request carries an Authorization: Bearer &lt;token&gt; header.
/// The token value is not verified against a store — this is a placeholder
/// until a proper token-validation strategy is defined in Phase 2.
/// </summary>
public sealed class BearerSchemeHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    /// <param name="options">Scheme options.</param>
    /// <param name="logger">Logger factory.</param>
    /// <param name="encoder">URL encoder.</param>
    public BearerSchemeHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    /// <inheritdoc />
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authHeader = Request.Headers.Authorization.ToString();

        if (string.IsNullOrWhiteSpace(authHeader) ||
            !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var token = authHeader["Bearer ".Length..].Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            return Task.FromResult(AuthenticateResult.Fail("Empty bearer token."));
        }

        // Placeholder: accept any non-empty token.
        // TODO (Phase 2): validate token signature / expiry.
        var claims = new[] { new Claim(ClaimTypes.Name, "api-caller") };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
