using System.Collections.Concurrent;
using GvResearch.Shared.Services;
using Microsoft.Extensions.Logging;

namespace GvResearch.Sip.Calls;

/// <summary>
/// Manages the lifecycle of active SIP↔GV bridged calls.
/// </summary>
public sealed class SipCallController : IDisposable
{
    private static readonly Action<ILogger, string, string, Exception?> LogCallCreating =
        LoggerMessage.Define<string, string>(LogLevel.Information, new EventId(1, "CallCreating"),
            "Creating outbound GV call for SIP call {CallId} to {Destination}.");

    private static readonly Action<ILogger, string, Exception?> LogCallCreated =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(2, "CallCreated"),
            "GV call {GvCallId} initiated successfully.");

    private static readonly Action<ILogger, string, string, Exception?> LogCallFailed =
        LoggerMessage.Define<string, string>(LogLevel.Warning, new EventId(3, "CallFailed"),
            "GV call initiation failed for SIP call {CallId}: {Error}.");

    private static readonly Action<ILogger, string, Exception?> LogHangup =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(4, "Hangup"),
            "Hanging up call {CallId}.");

    private static readonly Action<ILogger, string, Exception?> LogHangupNotFound =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(5, "HangupNotFound"),
            "Hangup requested for unknown call {CallId}.");

    private readonly IGvCallService _callService;
    private readonly ILogger<SipCallController> _logger;
    private readonly ConcurrentDictionary<string, CallSession> _activeCalls =
        new(StringComparer.Ordinal);

    private bool _disposed;

    /// <param name="callService">GV call service for initiating and hanging up back-end calls.</param>
    /// <param name="logger">Logger.</param>
    public SipCallController(
        IGvCallService callService,
        ILogger<SipCallController> logger)
    {
        ArgumentNullException.ThrowIfNull(callService);
        ArgumentNullException.ThrowIfNull(logger);

        _callService = callService;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new outbound GV call for the given SIP INVITE.
    /// </summary>
    /// <param name="sipCallId">The SIP Call-ID header value.</param>
    /// <param name="fromNumber">The GV number to dial from.</param>
    /// <param name="destinationNumber">The number to call.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The created <see cref="CallSession"/> on success, or <see langword="null"/> if
    /// the GV call could not be initiated.
    /// </returns>
    public async Task<CallSession?> CreateOutboundCallAsync(
        string sipCallId,
        string fromNumber,
        string destinationNumber,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(sipCallId);
        ArgumentException.ThrowIfNullOrWhiteSpace(fromNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationNumber);

        LogCallCreating(_logger, sipCallId, destinationNumber, null);

        var session = new CallSession(sipCallId, destinationNumber);
        _activeCalls[sipCallId] = session;

        var result = await _callService
            .InitiateCallAsync(fromNumber, destinationNumber, cancellationToken)
            .ConfigureAwait(false);

        if (!result.Success)
        {
            LogCallFailed(_logger, sipCallId, result.ErrorMessage ?? "unknown error", null);
            _activeCalls.TryRemove(sipCallId, out _);
            session.Dispose();
            return null;
        }

        session.GvCallId = result.GvCallId;
        session.TransitionTo(CallState.Ringing);
        LogCallCreated(_logger, result.GvCallId, null);
        return session;
    }

    /// <summary>
    /// Tears down both legs of an active call.
    /// </summary>
    /// <param name="sipCallId">The SIP Call-ID to hang up.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task HangupAsync(string sipCallId, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(sipCallId);

        if (!_activeCalls.TryRemove(sipCallId, out var session))
        {
            LogHangupNotFound(_logger, sipCallId, null);
            return;
        }

        LogHangup(_logger, sipCallId, null);
        session.TransitionTo(CallState.Ending);

        if (session.GvCallId is not null)
        {
            await _callService.HangupAsync(session.GvCallId, cancellationToken).ConfigureAwait(false);
        }

        session.Dispose();
    }

    /// <summary>
    /// Returns the active session for <paramref name="sipCallId"/>, or <see langword="null"/>.
    /// </summary>
    public CallSession? TryGetSession(string sipCallId) =>
        _activeCalls.TryGetValue(sipCallId, out var s) ? s : null;

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var session in _activeCalls.Values)
        {
            session.Dispose();
        }

        _activeCalls.Clear();
    }
}
