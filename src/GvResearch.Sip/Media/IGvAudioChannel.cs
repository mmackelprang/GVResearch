using SIPSorcery.Net;

namespace GvResearch.Sip.Media;

/// <summary>
/// Abstraction for establishing an audio channel between the SIP leg and the
/// Google Voice back-end.
/// </summary>
public interface IGvAudioChannel
{
    /// <summary>
    /// Establishes an audio channel for the specified GV call and returns the
    /// <see cref="RTPSession"/> representing the media endpoint.
    /// </summary>
    /// <param name="gvCallId">The GV-assigned call identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<RTPSession> EstablishAsync(string gvCallId, CancellationToken cancellationToken = default);
}
