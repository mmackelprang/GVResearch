namespace Iaet.Core.Models;

public sealed record EndpointGroup(
    EndpointSignature Signature,
    int ObservationCount,
    DateTimeOffset FirstSeen,
    DateTimeOffset LastSeen
);
