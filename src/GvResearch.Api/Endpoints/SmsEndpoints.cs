using GvResearch.Shared.Exceptions;
using GvResearch.Shared.Services;

namespace GvResearch.Api.Endpoints;

public static class SmsEndpoints
{
    public static IEndpointRouteBuilder MapSmsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/sms")
            .RequireAuthorization()
            .WithTags("SMS");

        group.MapPost("/", async (SendSmsRequest request, IGvClient client, CancellationToken ct) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.ToNumber))
                    return Results.BadRequest("ToNumber is required.");
                if (string.IsNullOrWhiteSpace(request.Message))
                    return Results.BadRequest("Message is required.");

                var result = await client.Sms.SendAsync(request.ToNumber, request.Message, ct).ConfigureAwait(false);
                return Results.Created($"/api/v1/threads/{result.ThreadId}", result);
            }
            catch (GvAuthException) { return Results.Unauthorized(); }
            catch (GvRateLimitException) { return Results.StatusCode(429); }
            catch (GvApiException ex) { return Results.Problem(detail: ex.Message, statusCode: 502); }
        })
        .WithName("SendSms")
        .WithSummary("Send an SMS message.");

        return app;
    }
}

public sealed record SendSmsRequest(string ToNumber, string Message);
