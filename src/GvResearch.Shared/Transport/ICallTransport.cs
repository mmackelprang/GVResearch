using GvResearch.Shared.Models;

namespace GvResearch.Shared.Transport;

public interface ICallTransport : IAsyncDisposable
{
    Task<TransportCallResult> InitiateAsync(string toNumber, CancellationToken ct = default);
    Task<TransportCallStatus> GetStatusAsync(string callId, CancellationToken ct = default);
    Task HangupAsync(string callId, CancellationToken ct = default);
}

public sealed record TransportCallResult(string CallId, bool Success, string? ErrorMessage);
public sealed record TransportCallStatus(string CallId, CallStatusType Status);
