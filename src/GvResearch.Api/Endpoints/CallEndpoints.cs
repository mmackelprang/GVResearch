using GvResearch.Api.Models;
using GvResearch.Shared.Services;
using Microsoft.AspNetCore.Mvc;

namespace GvResearch.Api.Endpoints;

/// <summary>
/// Registers the /api/v1/calls endpoint group.
/// </summary>
public static class CallEndpoints
{
    /// <summary>Maps all call-related endpoints onto the supplied route builder.</summary>
    public static IEndpointRouteBuilder MapCallEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/calls")
            .RequireAuthorization()
            .WithTags("Calls");

        group.MapGet("/", ListCallsAsync)
            .WithName("ListCalls")
            .WithSummary("List calls (placeholder — returns empty paged result).");

        group.MapGet("/{id:guid}", GetCallAsync)
            .WithName("GetCall")
            .WithSummary("Get a single call by ID (placeholder — always returns 404).");

        group.MapPost("/", InitiateCallAsync)
            .WithName("InitiateCall")
            .WithSummary("Initiate an outbound call via Google Voice.");

        return app;
    }

    private static IResult ListCallsAsync()
    {
        var result = new PagedResult<CallRecord>(
            Items: Array.Empty<CallRecord>(),
            NextCursor: null,
            Total: 0);

        return Results.Ok(result);
    }

    private static IResult GetCallAsync(Guid id)
    {
        _ = id; // placeholder
        return Results.NotFound();
    }

    private static async Task<IResult> InitiateCallAsync(
        [FromBody] InitiateCallRequest request,
        IGvCallService callService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ToNumber))
        {
            return Results.BadRequest("ToNumber is required.");
        }

        if (string.IsNullOrWhiteSpace(request.FromNumber))
        {
            return Results.BadRequest("FromNumber is required.");
        }

        var gvResult = await callService.InitiateCallAsync(
            request.FromNumber,
            request.ToNumber,
            cancellationToken).ConfigureAwait(false);

        if (!gvResult.Success)
        {
            return Results.Problem(
                detail: gvResult.ErrorMessage,
                statusCode: StatusCodes.Status502BadGateway,
                title: "Call initiation failed.");
        }

        var callId = new CallId(Guid.NewGuid());
        var record = new CallRecord(
            Id: callId,
            Direction: CallDirection.Outbound,
            FromNumber: new PhoneNumber(request.FromNumber),
            ToNumber: new PhoneNumber(request.ToNumber),
            StartedAt: DateTimeOffset.UtcNow,
            EndedAt: null,
            DurationSeconds: null,
            Status: CallStatus.Ringing);

        return Results.Created($"/api/v1/calls/{callId.Value}", record);
    }
}
