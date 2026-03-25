namespace GvResearch.Api.Models;

/// <summary>A cursor-paginated result set.</summary>
/// <typeparam name="T">The item type.</typeparam>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, string? NextCursor, int Total);
