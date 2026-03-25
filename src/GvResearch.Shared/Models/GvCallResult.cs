namespace GvResearch.Shared.Models;

/// <summary>
/// Result returned from a Google Voice call operation.
/// </summary>
/// <param name="GvCallId">The GV-assigned call identifier, or empty if the call failed.</param>
/// <param name="Success">Whether the operation succeeded.</param>
/// <param name="ErrorMessage">Human-readable error message when <see cref="Success"/> is false.</param>
public sealed record GvCallResult(string GvCallId, bool Success, string? ErrorMessage)
{
    /// <summary>Creates a successful result.</summary>
    public static GvCallResult Ok(string callId) => new(callId, Success: true, ErrorMessage: null);

    /// <summary>Creates a failure result.</summary>
    public static GvCallResult Fail(string errorMessage) => new(string.Empty, Success: false, errorMessage);
}
