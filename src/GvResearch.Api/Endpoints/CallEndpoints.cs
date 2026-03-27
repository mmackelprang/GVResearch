using GvResearch.Shared.Exceptions;
using GvResearch.Shared.Services;

namespace GvResearch.Api.Endpoints;

public static class CallEndpoints
{
    public static IEndpointRouteBuilder MapCallEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/calls")
            .RequireAuthorization()
            .WithTags("Calls");

        group.MapPost("/", async (InitiateCallRequest request, IGvClient client, CancellationToken ct) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.ToNumber))
                    return Results.BadRequest("ToNumber is required.");

                var result = await client.Calls.InitiateAsync(request.ToNumber, ct).ConfigureAwait(false);
                if (!result.Success)
                    return Results.Problem(detail: result.ErrorMessage, statusCode: 502, title: "Call initiation failed.");

                return Results.Created($"/api/v1/calls/{result.CallId}/status", result);
            }
            catch (GvAuthException) { return Results.Unauthorized(); }
            catch (GvRateLimitException) { return Results.StatusCode(429); }
            catch (GvApiException ex) { return Results.Problem(detail: ex.Message, statusCode: 502); }
        })
        .WithName("InitiateCall")
        .WithSummary("Initiate an outbound call.");

        group.MapGet("/{callId}/status", async (string callId, IGvClient client, CancellationToken ct) =>
        {
            try
            {
                var status = await client.Calls.GetStatusAsync(callId, ct).ConfigureAwait(false);
                return Results.Ok(status);
            }
            catch (GvAuthException) { return Results.Unauthorized(); }
            catch (GvRateLimitException) { return Results.StatusCode(429); }
            catch (GvApiException ex) { return Results.Problem(detail: ex.Message, statusCode: 502); }
        })
        .WithName("GetCallStatus")
        .WithSummary("Get the status of an active call.");

        group.MapPost("/{callId}/hangup", async (string callId, IGvClient client, CancellationToken ct) =>
        {
            try
            {
                await client.Calls.HangupAsync(callId, ct).ConfigureAwait(false);
                return Results.NoContent();
            }
            catch (GvAuthException) { return Results.Unauthorized(); }
            catch (GvRateLimitException) { return Results.StatusCode(429); }
            catch (GvApiException ex) { return Results.Problem(detail: ex.Message, statusCode: 502); }
        })
        .WithName("HangupCall")
        .WithSummary("Hang up an active call.");

        return app;
    }
}

public sealed record InitiateCallRequest(string ToNumber);
