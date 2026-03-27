namespace GvResearch.Shared.Models;

public sealed record GvCallStatus(string CallId, CallStatusType Status, DateTimeOffset RetrievedAt);
