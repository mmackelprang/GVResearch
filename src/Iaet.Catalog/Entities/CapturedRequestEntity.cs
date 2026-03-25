namespace Iaet.Catalog.Entities;

public class CapturedRequestEntity
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public required string HttpMethod { get; set; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056:URI-like properties should not be strings")]
    public required string Url { get; set; }
    public required string NormalizedSignature { get; set; }
    public string? RequestHeaders { get; set; }
    public string? RequestBody { get; set; }
    public int ResponseStatus { get; set; }
    public string? ResponseHeaders { get; set; }
    public string? ResponseBody { get; set; }
    public long DurationMs { get; set; }
    public string? Tag { get; set; }
    public CaptureSessionEntity? Session { get; set; }
}
