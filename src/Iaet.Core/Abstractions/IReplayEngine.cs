using Iaet.Core.Models;

namespace Iaet.Core.Abstractions;

public interface IReplayEngine
{
    Task<ReplayResult> ReplayAsync(CapturedRequest original, CancellationToken ct = default);
}

public sealed record ReplayResult(
    int ResponseStatus,
    string? ResponseBody,
    IReadOnlyList<FieldDiff> Diffs,
    long DurationMs
);

public sealed record FieldDiff(string Path, string? Expected, string? Actual);
