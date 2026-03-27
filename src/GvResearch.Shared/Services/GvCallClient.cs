using GvResearch.Shared.Exceptions;
using GvResearch.Shared.Models;
using GvResearch.Shared.RateLimiting;
using GvResearch.Shared.Transport;

namespace GvResearch.Shared.Services;

public sealed class GvCallClient : IGvCallClient
{
    private readonly ICallTransport _transport;
    private readonly GvRateLimiter _rateLimiter;

    public GvCallClient(ICallTransport transport, GvRateLimiter rateLimiter)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(rateLimiter);
        _transport = transport;
        _rateLimiter = rateLimiter;
    }

    public async Task<GvCallResult> InitiateAsync(string toNumber, CancellationToken ct = default)
    {
        if (!await _rateLimiter.TryAcquireAsync("calls/initiate", ct).ConfigureAwait(false))
            throw new GvRateLimitException("calls/initiate");

        var result = await _transport.InitiateAsync(toNumber, ct).ConfigureAwait(false);
        return new GvCallResult(result.CallId, result.Success, result.ErrorMessage);
    }

    public async Task<GvCallStatus> GetStatusAsync(string callId, CancellationToken ct = default)
    {
        if (!await _rateLimiter.TryAcquireAsync("calls/status", ct).ConfigureAwait(false))
            throw new GvRateLimitException("calls/status");

        var result = await _transport.GetStatusAsync(callId, ct).ConfigureAwait(false);
        return new GvCallStatus(result.CallId, result.Status, DateTimeOffset.UtcNow);
    }

    public async Task HangupAsync(string callId, CancellationToken ct = default)
    {
        if (!await _rateLimiter.TryAcquireAsync("calls/hangup", ct).ConfigureAwait(false))
            throw new GvRateLimitException("calls/hangup");

        await _transport.HangupAsync(callId, ct).ConfigureAwait(false);
    }
}
