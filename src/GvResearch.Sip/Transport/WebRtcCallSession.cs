using System.Net;
using GvResearch.Shared.Models;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;

namespace GvResearch.Sip.Transport;

internal sealed class WebRtcCallSession : IDisposable
{
    private static readonly RTCConfiguration StunConfig = new()
    {
        iceServers = [new RTCIceServer { urls = "stun:stun.l.google.com:19302" }]
    };

    private bool _disposed;

    public string CallId { get; }
    public RTCPeerConnection PeerConnection { get; }
    public CallStatusType Status { get; private set; } = CallStatusType.Unknown;

    public event Action<RTPPacket>? AudioReceived;

    public WebRtcCallSession(string callId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(callId);
        CallId = callId;

        PeerConnection = new RTCPeerConnection(StunConfig);

        var audioTrack = new MediaStreamTrack(
            SDPMediaTypesEnum.audio,
            false,
            new List<SDPAudioVideoMediaFormat>
            {
                // Opus: dynamic payload type 111, 48 kHz, 2 channels
                new(new AudioFormat(AudioCodecsEnum.OPUS, 111, 48000, 2, null)),
                new(SDPWellKnownMediaFormatsEnum.G722),
                new(SDPWellKnownMediaFormatsEnum.PCMU),
                new(SDPWellKnownMediaFormatsEnum.PCMA),
            });
        PeerConnection.addTrack(audioTrack);

        PeerConnection.OnRtpPacketReceived += OnRtpPacketReceived;

        PeerConnection.onconnectionstatechange += state =>
        {
            Status = state switch
            {
                RTCPeerConnectionState.connecting => CallStatusType.Ringing,
                RTCPeerConnectionState.connected => CallStatusType.Active,
                RTCPeerConnectionState.closed => CallStatusType.Completed,
                RTCPeerConnectionState.failed => CallStatusType.Failed,
                _ => Status,
            };
        };
    }

    public void SendAudio(RTPPacket packet)
    {
        if (_disposed || PeerConnection.connectionState != RTCPeerConnectionState.connected)
            return;

        PeerConnection.SendRtpRaw(
            SDPMediaTypesEnum.audio,
            packet.Payload,
            packet.Header.Timestamp,
            packet.Header.MarkerBit,
            packet.Header.PayloadType);
    }

    public void UpdateStatus(CallStatusType status)
    {
        Status = status;
    }

    private void OnRtpPacketReceived(IPEndPoint remoteEndPoint, SDPMediaTypesEnum mediaType, RTPPacket packet)
    {
        if (mediaType == SDPMediaTypesEnum.audio)
        {
            AudioReceived?.Invoke(packet);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        PeerConnection.OnRtpPacketReceived -= OnRtpPacketReceived;
        PeerConnection.close();
    }
}
