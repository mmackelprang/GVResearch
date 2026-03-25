namespace Iaet.Catalog.Entities;

public class CaptureSessionEntity
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string TargetApplication { get; set; }
    public required string Profile { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? StoppedAt { get; set; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002:Do not expose generic lists")]
    public List<CapturedRequestEntity> Requests { get; set; } = [];
}
