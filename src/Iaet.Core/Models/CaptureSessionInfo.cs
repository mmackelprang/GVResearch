namespace Iaet.Core.Models;

public sealed record CaptureSessionInfo
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string TargetApplication { get; init; }
    public required string Profile { get; init; }
    public required DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset? StoppedAt { get; init; }
    public int CapturedRequestCount { get; init; }
}
