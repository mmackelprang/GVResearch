using GvResearch.Shared.Exceptions;
using GvResearch.Shared.Models;
using GvResearch.Shared.Services;

namespace GvResearch.Api.Endpoints;

public static class ThreadEndpoints
{
    public static IEndpointRouteBuilder MapThreadEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/threads")
            .RequireAuthorization()
            .WithTags("Threads");

        group.MapGet("/", async (string? cursor, GvThreadType? type, int? maxResults,
            IGvClient client, CancellationToken ct) =>
        {
            try
            {
                var options = new GvThreadListOptions(cursor, type, maxResults ?? 50);
                var page = await client.Threads.ListAsync(options, ct).ConfigureAwait(false);
                return Results.Ok(page);
            }
            catch (GvAuthException) { return Results.Unauthorized(); }
            catch (GvRateLimitException) { return Results.StatusCode(429); }
            catch (GvApiException ex) { return Results.Problem(detail: ex.Message, statusCode: 502); }
        })
        .WithName("ListThreads")
        .WithSummary("List conversation threads with optional filtering and pagination.");

        group.MapGet("/{threadId}", async (string threadId, IGvClient client, CancellationToken ct) =>
        {
            try
            {
                var thread = await client.Threads.GetAsync(threadId, ct).ConfigureAwait(false);
                return Results.Ok(thread);
            }
            catch (GvAuthException) { return Results.Unauthorized(); }
            catch (GvRateLimitException) { return Results.StatusCode(429); }
            catch (GvApiException ex) { return Results.Problem(detail: ex.Message, statusCode: 502); }
        })
        .WithName("GetThread")
        .WithSummary("Get a single thread with full message history.");

        group.MapGet("/unread", async (IGvClient client, CancellationToken ct) =>
        {
            try
            {
                var counts = await client.Threads.GetUnreadCountsAsync(ct).ConfigureAwait(false);
                return Results.Ok(counts);
            }
            catch (GvAuthException) { return Results.Unauthorized(); }
            catch (GvRateLimitException) { return Results.StatusCode(429); }
            catch (GvApiException ex) { return Results.Problem(detail: ex.Message, statusCode: 502); }
        })
        .WithName("GetUnreadCounts")
        .WithSummary("Get unread counts per thread type.");

        group.MapGet("/search", async (string q, IGvClient client, CancellationToken ct) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(q))
                    return Results.BadRequest("Query parameter 'q' is required.");
                var page = await client.Threads.SearchAsync(q, ct).ConfigureAwait(false);
                return Results.Ok(page);
            }
            catch (GvAuthException) { return Results.Unauthorized(); }
            catch (GvRateLimitException) { return Results.StatusCode(429); }
            catch (GvApiException ex) { return Results.Problem(detail: ex.Message, statusCode: 502); }
        })
        .WithName("SearchThreads")
        .WithSummary("Full-text search across all threads.");

        group.MapPost("/markread", async (ThreadIdsRequest request, IGvClient client, CancellationToken ct) =>
        {
            try
            {
                await client.Threads.MarkReadAsync(request.ThreadIds, ct).ConfigureAwait(false);
                return Results.NoContent();
            }
            catch (GvAuthException) { return Results.Unauthorized(); }
            catch (GvRateLimitException) { return Results.StatusCode(429); }
            catch (GvApiException ex) { return Results.Problem(detail: ex.Message, statusCode: 502); }
        })
        .WithName("MarkThreadsRead")
        .WithSummary("Mark threads as read.");

        group.MapPost("/archive", async (ThreadIdsRequest request, IGvClient client, CancellationToken ct) =>
        {
            try
            {
                await client.Threads.ArchiveAsync(request.ThreadIds, ct).ConfigureAwait(false);
                return Results.NoContent();
            }
            catch (GvAuthException) { return Results.Unauthorized(); }
            catch (GvRateLimitException) { return Results.StatusCode(429); }
            catch (GvApiException ex) { return Results.Problem(detail: ex.Message, statusCode: 502); }
        })
        .WithName("ArchiveThreads")
        .WithSummary("Archive threads.");

        group.MapPost("/spam", async (ThreadIdsRequest request, IGvClient client, CancellationToken ct) =>
        {
            try
            {
                await client.Threads.MarkSpamAsync(request.ThreadIds, ct).ConfigureAwait(false);
                return Results.NoContent();
            }
            catch (GvAuthException) { return Results.Unauthorized(); }
            catch (GvRateLimitException) { return Results.StatusCode(429); }
            catch (GvApiException ex) { return Results.Problem(detail: ex.Message, statusCode: 502); }
        })
        .WithName("MarkThreadsSpam")
        .WithSummary("Mark threads as spam.");

        group.MapPost("/markallread", async (IGvClient client, CancellationToken ct) =>
        {
            try
            {
                await client.Threads.MarkAllReadAsync(ct).ConfigureAwait(false);
                return Results.NoContent();
            }
            catch (GvAuthException) { return Results.Unauthorized(); }
            catch (GvRateLimitException) { return Results.StatusCode(429); }
            catch (GvApiException ex) { return Results.Problem(detail: ex.Message, statusCode: 502); }
        })
        .WithName("MarkAllRead")
        .WithSummary("Mark all threads as read.");

        group.MapDelete("/", async (ThreadIdsRequest request, IGvClient client, CancellationToken ct) =>
        {
            try
            {
                await client.Threads.DeleteAsync(request.ThreadIds, ct).ConfigureAwait(false);
                return Results.NoContent();
            }
            catch (GvAuthException) { return Results.Unauthorized(); }
            catch (GvRateLimitException) { return Results.StatusCode(429); }
            catch (GvApiException ex) { return Results.Problem(detail: ex.Message, statusCode: 502); }
        })
        .WithName("DeleteThreads")
        .WithSummary("Permanently delete threads.");

        return app;
    }
}

public sealed record ThreadIdsRequest(IReadOnlyList<string> ThreadIds);
