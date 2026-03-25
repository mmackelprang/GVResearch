namespace Iaet.Catalog.Entities;

public class EndpointGroupEntity
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public required string NormalizedSignature { get; set; }
    public int ObservationCount { get; set; }
    public DateTimeOffset FirstSeen { get; set; }
    public DateTimeOffset LastSeen { get; set; }
    public CaptureSessionEntity? Session { get; set; }
}
