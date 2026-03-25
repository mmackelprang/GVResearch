using System.Net;
using SIPSorcery.Net;

namespace GvResearch.Sip.Media;

/// <summary>
/// Forwards RTP audio packets bidirectionally between two <see cref="RTPSession"/> instances.
/// Attach via <see cref="Start"/> and detach via <see cref="Stop"/> or <see cref="Dispose"/>.
/// </summary>
public sealed class RtpBridge : IDisposable
{
    private readonly RTPSession _sessionA;
    private readonly RTPSession _sessionB;
    private bool _started;
    private bool _disposed;

    /// <param name="sessionA">First RTP session (SIP leg).</param>
    /// <param name="sessionB">Second RTP session (GV leg).</param>
    public RtpBridge(RTPSession sessionA, RTPSession sessionB)
    {
        ArgumentNullException.ThrowIfNull(sessionA);
        ArgumentNullException.ThrowIfNull(sessionB);

        _sessionA = sessionA;
        _sessionB = sessionB;
    }

    /// <summary>
    /// Attaches the packet-forwarding handlers on both sessions.
    /// Idempotent — safe to call more than once.
    /// </summary>
    public void Start()
    {
        if (_started || _disposed)
        {
            return;
        }

        _started = true;
        _sessionA.OnRtpPacketReceived += ForwardAtoB;
        _sessionB.OnRtpPacketReceived += ForwardBtoA;
    }

    /// <summary>
    /// Detaches the packet-forwarding handlers from both sessions.
    /// </summary>
    public void Stop()
    {
        if (!_started)
        {
            return;
        }

        _started = false;
        _sessionA.OnRtpPacketReceived -= ForwardAtoB;
        _sessionB.OnRtpPacketReceived -= ForwardBtoA;
    }

    // Forwards a packet received on session A to session B.
    private void ForwardAtoB(IPEndPoint remoteEndPoint, SDPMediaTypesEnum mediaType, RTPPacket packet)
    {
        if (_disposed || _sessionB.IsClosed)
        {
            return;
        }

        _sessionB.SendRtpRaw(
            SDPMediaTypesEnum.audio,
            packet.Payload,
            packet.Header.Timestamp,
            packet.Header.MarkerBit,
            packet.Header.PayloadType);
    }

    // Forwards a packet received on session B to session A.
    private void ForwardBtoA(IPEndPoint remoteEndPoint, SDPMediaTypesEnum mediaType, RTPPacket packet)
    {
        if (_disposed || _sessionA.IsClosed)
        {
            return;
        }

        _sessionA.SendRtpRaw(
            SDPMediaTypesEnum.audio,
            packet.Payload,
            packet.Header.Timestamp,
            packet.Header.MarkerBit,
            packet.Header.PayloadType);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
    }
}
