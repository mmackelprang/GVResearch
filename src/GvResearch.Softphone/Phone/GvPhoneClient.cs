using GvResearch.Shared.Signaler;
using GvResearch.Shared.Transport;
using GvResearch.Shared.Services;
using GvResearch.Softphone.Audio;

namespace GvResearch.Softphone.Phone;

/// <summary>Event args carrying a plain string message (e.g. status text).</summary>
public sealed class StringEventArgs(string value) : EventArgs
{
    public string Value { get; } = value;
}

/// <summary>
/// Wraps <see cref="IGvClient"/> and <see cref="ICallTransport"/> to provide
/// a simple phone-call API for the Softphone UI layer.
/// </summary>
public sealed class GvPhoneClient : IAsyncDisposable
{
    private readonly IGvClient _client;
    private readonly ICallTransport _transport;
    private readonly IGvSignalerClient _signaler;
    private readonly AudioEngine _audioEngine;
    private string? _activeCallId;
    private bool _signalerConnected;

    public event EventHandler<StringEventArgs>? StatusChanged;
    public event EventHandler<StringEventArgs>? IncomingCallReceived;
    public event EventHandler? CallAnswered;
    public event EventHandler? CallEnded;

    public GvPhoneClient(IGvClient client, ICallTransport transport, IGvSignalerClient signaler, AudioEngine audioEngine)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(signaler);
        ArgumentNullException.ThrowIfNull(audioEngine);
        _client = client;
        _transport = transport;
        _signaler = signaler;
        _audioEngine = audioEngine;
        _transport.IncomingCallReceived += OnIncomingCall;
    }

    public async Task<bool> CallAsync(string destination, CancellationToken ct = default)
    {
        if (!_signalerConnected)
        {
            StatusChanged?.Invoke(this, new StringEventArgs("Connecting signaler..."));
            await _signaler.ConnectAsync(ct).ConfigureAwait(false);
            _signalerConnected = true;
        }

        StatusChanged?.Invoke(this, new StringEventArgs($"Calling {destination}..."));

        var result = await _client.Calls.InitiateAsync(destination, ct).ConfigureAwait(false);
        if (!result.Success)
        {
            StatusChanged?.Invoke(this, new StringEventArgs($"Call failed: {result.ErrorMessage}"));
            return false;
        }

        _activeCallId = result.CallId;
        StatusChanged?.Invoke(this, new StringEventArgs("Ringing..."));
        _audioEngine.Start(result.CallId);
        CallAnswered?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public async Task HangupAsync(CancellationToken ct = default)
    {
        if (_activeCallId is null) return;

        _audioEngine.Stop();
        await _client.Calls.HangupAsync(_activeCallId, ct).ConfigureAwait(false);
        _activeCallId = null;
        StatusChanged?.Invoke(this, new StringEventArgs("Call ended"));
        CallEnded?.Invoke(this, EventArgs.Empty);
    }

    public string? ActiveCallId => _activeCallId;

    private void OnIncomingCall(object? sender, IncomingCallEventArgs args)
    {
        _activeCallId = args.CallInfo.CallId;
        _audioEngine.Start(args.CallInfo.CallId);
        IncomingCallReceived?.Invoke(this, new StringEventArgs(args.CallInfo.CallerNumber));
        StatusChanged?.Invoke(this, new StringEventArgs($"Incoming call from {args.CallInfo.CallerNumber}"));
        CallAnswered?.Invoke(this, EventArgs.Empty);
    }

    public async ValueTask DisposeAsync()
    {
        _transport.IncomingCallReceived -= OnIncomingCall;
        if (_activeCallId is not null)
        {
            try { await HangupAsync().ConfigureAwait(false); }
#pragma warning disable CA1031 // best-effort hangup on dispose
            catch { /* best effort */ }
#pragma warning restore CA1031
        }
    }
}
