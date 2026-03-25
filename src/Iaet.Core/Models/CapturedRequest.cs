namespace Iaet.Core.Models;

public sealed record CapturedRequest
{
    public required Guid Id { get; init; }
    public required Guid SessionId { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required string HttpMethod { get; init; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056:URI-like properties should not be strings")]
    public required string Url { get; init; }
    public required Dictionary<string, string> RequestHeaders { get; init; }
    public string? RequestBody { get; init; }
    public required int ResponseStatus { get; init; }
    public required Dictionary<string, string> ResponseHeaders { get; init; }
    public string? ResponseBody { get; init; }
    public required long DurationMs { get; init; }
    public string? Tag { get; init; }
}
