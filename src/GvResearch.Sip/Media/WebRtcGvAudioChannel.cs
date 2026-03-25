using SIPSorcery.Net;

namespace GvResearch.Sip.Media;

/// <summary>
/// Placeholder implementation of <see cref="IGvAudioChannel"/> that will bridge
/// the SIP leg to the Google Voice WebRTC audio path once Phase 1 research is
/// complete. Currently throws <see cref="NotImplementedException"/>.
/// </summary>
public sealed class WebRtcGvAudioChannel : IGvAudioChannel
{
    // TODO (Phase 1): Implement real WebRTC/SRTP establishment to Google Voice
    //                 once the transport details are discovered via research.

    /// <inheritdoc />
    /// <exception cref="NotImplementedException">
    /// Always thrown — implementation is pending Phase 1 research.
    /// </exception>
    public Task<RTPSession> EstablishAsync(
        string gvCallId,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException(
            "WebRTC GV audio channel is not yet implemented. " +
            "Pending Phase 1 discovery of Google Voice transport details.");
    }
}
