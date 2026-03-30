using Microsoft.Extensions.Logging;
using NAudio.Wave;
using GvResearch.Shared.Transport;

namespace GvResearch.Softphone.Audio;

/// <summary>
/// NAudio-based audio engine that wires microphone input to the call transport
/// and plays back received audio through the speaker.
/// Handles sample rate conversion between the transport (48kHz for Opus)
/// and the local audio devices.
/// </summary>
public sealed class AudioEngine : IDisposable
{
    // Opus decodes to 48kHz PCM; match that for playback
    private const int DeviceSampleRate = 48000;
    private const int Channels = 1;
    private const int BitsPerSample = 16;

    private static readonly Action<ILogger, int, Exception?> LogStarted =
        LoggerMessage.Define<int>(LogLevel.Information, new EventId(1, "AudioStarted"),
            "Audio engine started at {SampleRate}Hz.");

    private static readonly Action<ILogger, Exception?> LogStopped =
        LoggerMessage.Define(LogLevel.Information, new EventId(2, "AudioStopped"),
            "Audio engine stopped.");

    private readonly ILogger<AudioEngine> _logger;
    private readonly ICallTransport _transport;
    private WaveInEvent? _waveIn;
    private WaveOutEvent? _waveOut;
    private BufferedWaveProvider? _waveProvider;
    private string? _activeCallId;
    private bool _disposed;
    private bool _isMuted;

    public AudioEngine(ILogger<AudioEngine> logger, ICallTransport transport)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(transport);
        _logger = logger;
        _transport = transport;
    }

    /// <summary>Starts audio capture and playback for the given call.</summary>
    public void Start(string callId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(callId);
        ObjectDisposedException.ThrowIf(_disposed, this);

        _activeCallId = callId;

        var waveFormat = new WaveFormat(DeviceSampleRate, BitsPerSample, Channels);

        // Microphone capture
        _waveIn = new WaveInEvent
        {
            WaveFormat = waveFormat,
            BufferMilliseconds = 20
        };
        _waveIn.DataAvailable += OnMicDataAvailable;
        _waveIn.StartRecording();

        // Speaker playback
        _waveProvider = new BufferedWaveProvider(waveFormat)
        {
            BufferDuration = TimeSpan.FromSeconds(1),
            DiscardOnBufferOverflow = true
        };
        _waveOut = new WaveOutEvent();
        _waveOut.Init(_waveProvider);
        _waveOut.Play();

        // Subscribe to transport audio
        _transport.AudioReceived += OnAudioReceived;

        LogStarted(_logger, DeviceSampleRate, null);
    }

    /// <summary>Stops audio capture and playback.</summary>
    public void Stop()
    {
        _transport.AudioReceived -= OnAudioReceived;

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

        _waveProvider = null;
        _activeCallId = null;

        LogStopped(_logger, null);
    }

    /// <summary>Sets microphone mute state.</summary>
    public void SetMute(bool muted) => _isMuted = muted;

    private void OnMicDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (_isMuted || _activeCallId is null)
            return;

        // Send PCM directly to transport — it handles encoding
        var pcm = new byte[e.BytesRecorded];
        Buffer.BlockCopy(e.Buffer, 0, pcm, 0, e.BytesRecorded);
        _transport.SendAudio(_activeCallId, pcm, DeviceSampleRate);
    }

    private void OnAudioReceived(object? sender, AudioDataEventArgs args)
    {
        if (_waveProvider is null || args.CallId != _activeCallId)
            return;

        // Write decoded PCM to speaker buffer
        var data = args.PcmData.Span;
        var buffer = new byte[data.Length];
        data.CopyTo(buffer);
        _waveProvider.AddSamples(buffer, 0, buffer.Length);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Stop();
            _disposed = true;
        }
    }
}
