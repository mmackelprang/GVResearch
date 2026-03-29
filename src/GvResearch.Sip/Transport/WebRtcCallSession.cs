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
    private volatile CallStatusType _status = CallStatusType.Unknown;

    public string CallId { get; }
    public RTCPeerConnection PeerConnection { get; }
    public CallStatusType Status => _status;

    /// <summary>Fired with decoded PCM audio (16-bit) and the sample rate.</summary>
    public event Action<byte[], int>? PcmAudioReceived;

    public WebRtcCallSession(string callId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(callId);
        CallId = callId;

        PeerConnection = new RTCPeerConnection(StunConfig);

        // Prefer G.711 codecs for simplicity — mu-law/a-law decode is trivial.
        // Opus is included for when the native codec is available.
        var audioTrack = new MediaStreamTrack(
            SDPMediaTypesEnum.audio,
            false,
            new List<SDPAudioVideoMediaFormat>
            {
                new(SDPWellKnownMediaFormatsEnum.PCMU),  // G.711 mu-law 8kHz — easy decode
                new(SDPWellKnownMediaFormatsEnum.PCMA),  // G.711 a-law 8kHz
                new(SDPWellKnownMediaFormatsEnum.G722),  // G.722 16kHz
            });
        PeerConnection.addTrack(audioTrack);

        PeerConnection.OnRtpPacketReceived += OnRtpPacketReceived;

        PeerConnection.onconnectionstatechange += state =>
        {
            _status = state switch
            {
                RTCPeerConnectionState.connecting => CallStatusType.Ringing,
                RTCPeerConnectionState.connected => CallStatusType.Active,
                RTCPeerConnectionState.closed => CallStatusType.Completed,
                RTCPeerConnectionState.failed => CallStatusType.Failed,
                _ => _status,
            };
        };
    }

    /// <summary>Send PCM audio (16-bit, 8kHz) to the remote party. Encodes to G.711 mu-law.</summary>
    public void SendPcmAudio(byte[] pcmData, int sampleRate)
    {
        if (_disposed || PeerConnection.connectionState != RTCPeerConnectionState.connected)
            return;

        // Encode 16-bit PCM to G.711 mu-law
        var encoded = new byte[pcmData.Length / 2];
        for (int i = 0; i < encoded.Length; i++)
        {
            short sample = (short)(pcmData[i * 2] | (pcmData[i * 2 + 1] << 8));
            encoded[i] = LinearToMuLaw(sample);
        }

        PeerConnection.SendAudio((uint)encoded.Length, encoded);
    }

    public void UpdateStatus(CallStatusType status) => _status = status;

    private void OnRtpPacketReceived(IPEndPoint remoteEndPoint, SDPMediaTypesEnum mediaType, RTPPacket packet)
    {
        if (mediaType != SDPMediaTypesEnum.audio)
            return;

        // Decode based on payload type
        byte[] pcm;
        int rate;

        switch (packet.Header.PayloadType)
        {
            case 0: // PCMU (G.711 mu-law)
                pcm = DecodeMuLaw(packet.Payload);
                rate = 8000;
                break;
            case 8: // PCMA (G.711 a-law)
                pcm = DecodeALaw(packet.Payload);
                rate = 8000;
                break;
            default:
                // Unknown codec — skip
                return;
        }

        PcmAudioReceived?.Invoke(pcm, rate);
    }

    #region G.711 Codec

    private static byte[] DecodeMuLaw(byte[] muLawData)
    {
        var pcm = new byte[muLawData.Length * 2];
        for (int i = 0; i < muLawData.Length; i++)
        {
            short sample = MuLawToLinear(muLawData[i]);
            pcm[i * 2] = (byte)(sample & 0xFF);
            pcm[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
        }
        return pcm;
    }

    private static byte[] DecodeALaw(byte[] aLawData)
    {
        var pcm = new byte[aLawData.Length * 2];
        for (int i = 0; i < aLawData.Length; i++)
        {
            short sample = ALawToLinear(aLawData[i]);
            pcm[i * 2] = (byte)(sample & 0xFF);
            pcm[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
        }
        return pcm;
    }

    private static short MuLawToLinear(byte muLaw)
    {
        muLaw = (byte)~muLaw;
        int sign = (muLaw & 0x80) != 0 ? -1 : 1;
        int exponent = (muLaw >> 4) & 0x07;
        int mantissa = muLaw & 0x0F;
        int magnitude = ((mantissa << 3) + 0x84) << exponent;
        magnitude -= 0x84;
        return (short)(sign * magnitude);
    }

    private static byte LinearToMuLaw(short sample)
    {
        const int Bias = 0x84;
        const int Max = 32635;

        int sign = (sample >> 8) & 0x80;
        if (sign != 0) sample = (short)-sample;
        if (sample > Max) sample = Max;

        sample = (short)(sample + Bias);
        int exponent = MuLawCompressTable[(sample >> 7) & 0xFF];
        int mantissa = (sample >> (exponent + 3)) & 0x0F;
        int compressedByte = ~(sign | (exponent << 4) | mantissa);
        return (byte)(compressedByte & 0xFF);
    }

    private static short ALawToLinear(byte aLaw)
    {
        aLaw ^= 0xD5;
        int sign = (aLaw & 0x80) != 0 ? -1 : 1;
        int exponent = (aLaw >> 4) & 0x07;
        int mantissa = aLaw & 0x0F;
        int magnitude = exponent == 0
            ? (mantissa << 4) + 8
            : ((mantissa << 4) + 0x108) << (exponent - 1);
        return (short)(sign * magnitude);
    }

    private static readonly byte[] MuLawCompressTable =
    [
        0,0,1,1,2,2,2,2,3,3,3,3,3,3,3,3,
        4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,4,
        5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,
        5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,5,
        6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,
        6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,
        6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,
        6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,6,
        7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,
        7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,
        7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,
        7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,
        7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,
        7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,
        7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,
        7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,7,
    ];

    #endregion

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        PeerConnection.OnRtpPacketReceived -= OnRtpPacketReceived;
        PeerConnection.close();
    }
}
