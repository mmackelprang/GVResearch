using GvResearch.Shared.Models;

namespace GvResearch.Shared.Transport;

public interface ICallTransport : IAsyncDisposable
{
    event EventHandler<IncomingCallEventArgs>? IncomingCallReceived;

    Task<TransportCallResult> InitiateAsync(string toNumber, CancellationToken ct = default);
    Task<TransportCallStatus> GetStatusAsync(string callId, CancellationToken ct = default);
    Task HangupAsync(string callId, CancellationToken ct = default);
}

public sealed record TransportCallResult(string CallId, bool Success, string? ErrorMessage);
public sealed record TransportCallStatus(string CallId, CallStatusType Status);
public sealed record IncomingCallInfo(string CallId, string CallerNumber);

public sealed class IncomingCallEventArgs(IncomingCallInfo callInfo) : EventArgs
{
    public IncomingCallInfo CallInfo { get; } = callInfo;
}
