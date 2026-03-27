namespace GvResearch.Shared.Models;

public sealed record GvThread(
    string Id,
    GvThreadType Type,
    IReadOnlyList<GvMessage> Messages,
    IReadOnlyList<string> Participants,
    DateTimeOffset Timestamp,
    bool IsRead);

public sealed record GvMessage(
    string Id,
    string? Text,
    string? SenderNumber,
    DateTimeOffset Timestamp,
    GvMessageType Type);

public sealed record GvThreadPage(
    IReadOnlyList<GvThread> Threads,
    string? NextCursor,
    int TotalCount);

public sealed record GvThreadListOptions(
    string? Cursor = null,
    GvThreadType? Type = null,
    int MaxResults = 50);
