using GvResearch.Shared.Models;

namespace GvResearch.Shared.Transport;

public interface ICallTransport : IAsyncDisposable
{
    event EventHandler<IncomingCallEventArgs>? IncomingCallReceived;

    /// <summary>
    /// Fired when decoded PCM audio is received from the remote party.
    /// Payload is 16-bit PCM at the negotiated sample rate.
    /// </summary>
    event EventHandler<AudioDataEventArgs>? AudioReceived;

    Task<TransportCallResult> InitiateAsync(string toNumber, CancellationToken ct = default);
    Task<TransportCallStatus> GetStatusAsync(string callId, CancellationToken ct = default);
    Task HangupAsync(string callId, CancellationToken ct = default);

    /// <summary>Send PCM audio to the active call. The transport encodes to the negotiated codec.</summary>
    void SendAudio(string callId, ReadOnlyMemory<byte> pcmData, int sampleRate);
}

public sealed record TransportCallResult(string CallId, bool Success, string? ErrorMessage);
public sealed record TransportCallStatus(string CallId, CallStatusType Status);
public sealed record IncomingCallInfo(string CallId, string CallerNumber);

public sealed class IncomingCallEventArgs(IncomingCallInfo callInfo) : EventArgs
{
    public IncomingCallInfo CallInfo { get; } = callInfo;
}

public sealed class AudioDataEventArgs(string callId, ReadOnlyMemory<byte> pcmData, int sampleRate) : EventArgs
{
    public string CallId { get; } = callId;
    public ReadOnlyMemory<byte> PcmData { get; } = pcmData;
    public int SampleRate { get; } = sampleRate;
}
