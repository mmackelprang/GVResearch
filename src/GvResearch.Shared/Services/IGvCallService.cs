using GvResearch.Shared.Models;

namespace GvResearch.Shared.Services;

/// <summary>
/// Provides operations for initiating and managing Google Voice calls.
/// </summary>
public interface IGvCallService : IDisposable
{
    /// <summary>
    /// Initiates a call from <paramref name="fromNumber"/> to <paramref name="toNumber"/>.
    /// </summary>
    /// <param name="fromNumber">The Google Voice number to call from.</param>
    /// <param name="toNumber">The destination phone number.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="GvCallResult"/> indicating success or failure.</returns>
    Task<GvCallResult> InitiateCallAsync(
        string fromNumber,
        string toNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the current status of a call.
    /// </summary>
    /// <param name="gvCallId">The GV-assigned call identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<GvCallStatus> GetCallStatusAsync(
        string gvCallId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Hangs up an active call.
    /// </summary>
    /// <param name="gvCallId">The GV-assigned call identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<GvCallResult> HangupAsync(
        string gvCallId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams real-time events for a call. Currently a placeholder that yields nothing.
    /// </summary>
    /// <param name="gvCallId">The GV-assigned call identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    IAsyncEnumerable<GvCallEvent> ListenForEventsAsync(
        string gvCallId,
        CancellationToken cancellationToken = default);
}
