namespace GvResearch.Shared.Models;

/// <summary>Known states a Google Voice call can be in.</summary>
public enum GvCallStatusType
{
    /// <summary>Status is unknown or not yet retrieved.</summary>
    Unknown,

    /// <summary>Call is ringing at the destination.</summary>
    Ringing,

    /// <summary>Call is active / in-progress.</summary>
    Active,

    /// <summary>Call ended normally.</summary>
    Completed,

    /// <summary>Call failed to connect.</summary>
    Failed,
}

/// <summary>
/// Status snapshot for a Google Voice call.
/// </summary>
/// <param name="GvCallId">The GV-assigned call identifier.</param>
/// <param name="Status">The current status.</param>
/// <param name="RetrievedAt">When this status was retrieved.</param>
public sealed record GvCallStatus(string GvCallId, GvCallStatusType Status, DateTimeOffset RetrievedAt);
