using GvResearch.Shared.Exceptions;
using GvResearch.Shared.Services;

namespace GvResearch.Api.Endpoints;

public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/account")
            .RequireAuthorization()
            .WithTags("Account");

        group.MapGet("/", async (IGvClient client, CancellationToken ct) =>
        {
            try
            {
                var account = await client.Account.GetAsync(ct).ConfigureAwait(false);
                return Results.Ok(account);
            }
            catch (GvAuthException) { return Results.Unauthorized(); }
            catch (GvRateLimitException) { return Results.StatusCode(429); }
            catch (GvApiException ex) { return Results.Problem(detail: ex.Message, statusCode: 502); }
        })
        .WithName("GetAccount")
        .WithSummary("Get account configuration including phone numbers and devices.");

        return app;
    }
}
