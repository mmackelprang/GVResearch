using System.Net;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
using SIPSorcery.Net;

namespace GvResearch.Softphone.Audio;

/// <summary>
/// NAudio-based audio engine that wires microphone input into an RTP session
/// and plays back received RTP audio through the speaker.
/// </summary>
public sealed class AudioEngine : IDisposable
{
    private const int SampleRate = 8000;
    private const int Channels = 1;
    private const int BitsPerSample = 16;

    private static readonly Action<ILogger, int, int, int, Exception?> LogStarted =
        LoggerMessage.Define<int, int, int>(LogLevel.Information, new EventId(1, "AudioStarted"),
            "Audio engine started: {SampleRate}Hz {Channels}ch {Bits}bit.");

    private static readonly Action<ILogger, Exception?> LogStopped =
        LoggerMessage.Define(LogLevel.Information, new EventId(2, "AudioStopped"),
            "Audio engine stopped.");

    private static readonly Action<ILogger, bool, Exception?> LogMuted =
        LoggerMessage.Define<bool>(LogLevel.Information, new EventId(3, "AudioMuted"),
            "Audio mute set to {Muted}.");

    private readonly ILogger<AudioEngine> _logger;
    private WaveInEvent? _waveIn;
    private WaveOutEvent? _waveOut;
    private BufferedWaveProvider? _waveProvider;
    private RTPSession? _rtpSession;
    private bool _disposed;
    private bool _isMuted;

    public AudioEngine(ILogger<AudioEngine> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <summary>Starts audio capture and playback using the given RTP session.</summary>
    public void Start(RTPSession rtpSession)
    {
        ArgumentNullException.ThrowIfNull(rtpSession);
        ObjectDisposedException.ThrowIf(_disposed, this);

        _rtpSession = rtpSession;

        var waveFormat = new WaveFormat(SampleRate, BitsPerSample, Channels);

        // Set up microphone capture
        _waveIn = new WaveInEvent
        {
            WaveFormat = waveFormat,
            BufferMilliseconds = 20
        };
        _waveIn.DataAvailable += OnMicDataAvailable;
        _waveIn.StartRecording();

        // Set up speaker playback
        _waveProvider = new BufferedWaveProvider(waveFormat)
        {
            BufferDuration = TimeSpan.FromSeconds(1),
            DiscardOnBufferOverflow = true
        };

        _waveOut = new WaveOutEvent();
        _waveOut.Init(_waveProvider);
        _waveOut.Play();

        // Receive RTP audio packets
        rtpSession.OnRtpPacketReceived += OnRtpPacketReceived;

        LogStarted(_logger, SampleRate, Channels, BitsPerSample, null);
    }

    /// <summary>Stops audio capture and playback.</summary>
    public void Stop()
    {
        if (_waveIn is not null)
        {
            _waveIn.StopRecording();
            _waveIn.DataAvailable -= OnMicDataAvailable;
            _waveIn.Dispose();
            _waveIn = null;
        }

        if (_waveOut is not null)
        {
            _waveOut.Stop();
            _waveOut.Dispose();
            _waveOut = null;
        }

        if (_rtpSession is not null)
        {
            _rtpSession.OnRtpPacketReceived -= OnRtpPacketReceived;
            _rtpSession = null;
        }

        LogStopped(_logger, null);
    }

    /// <summary>Sets microphone mute state. When muted captured audio is not sent via RTP.</summary>
    public void SetMute(bool muted)
    {
        _isMuted = muted;
        LogMuted(_logger, muted, null);
    }

    private void OnMicDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (_isMuted || _rtpSession is null)
        {
            return;
        }

        // G.711 encoding would happen here before sending via RTP.
        // Stub: send raw PCM samples as-is.
        _rtpSession.SendAudio(
            (uint)(e.BytesRecorded / 2),
            e.Buffer[..e.BytesRecorded]);
    }

    private void OnRtpPacketReceived(IPEndPoint remoteEndPoint, SDPMediaTypesEnum mediaType, RTPPacket packet)
    {
        if (mediaType != SDPMediaTypesEnum.audio || _waveProvider is null)
        {
            return;
        }

        // G.711 decoding would happen here.
        // Stub: write the raw payload bytes directly to the wave provider.
        _waveProvider.AddSamples(packet.Payload, 0, packet.Payload.Length);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            Stop();
            _disposed = true;
        }
    }
}
