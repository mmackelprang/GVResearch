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

    private static readonly Action<ILogger, string, string, string, Exception?> LogIncomingCall =
        LoggerMessage.Define<string, string, string>(LogLevel.Information, new EventId(6, "IncomingCall"),
            "Incoming GV call {GvCallId} from {Caller}, registered as SIP call {CallId}.");

    private readonly IGvCallClient _callClient;
    private readonly ILogger<SipCallController> _logger;
    private readonly ConcurrentDictionary<string, CallSession> _activeCalls =
        new(StringComparer.Ordinal);

    private bool _disposed;

    /// <param name="callClient">GV call client for initiating and hanging up back-end calls.</param>
    /// <param name="logger">Logger.</param>
    public SipCallController(
        IGvCallClient callClient,
        ILogger<SipCallController> logger)
    {
        ArgumentNullException.ThrowIfNull(callClient);
        ArgumentNullException.ThrowIfNull(logger);

        _callClient = callClient;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new outbound GV call for the given SIP INVITE.
    /// </summary>
    /// <param name="sipCallId">The SIP Call-ID header value.</param>
    /// <param name="destinationNumber">The number to call.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The created <see cref="CallSession"/> on success, or <see langword="null"/> if
    /// the GV call could not be initiated.
    /// </returns>
    public async Task<CallSession?> CreateOutboundCallAsync(
        string sipCallId,
        string destinationNumber,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(sipCallId);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationNumber);

        LogCallCreating(_logger, sipCallId, destinationNumber, null);

        var session = new CallSession(sipCallId, destinationNumber);
        _activeCalls[sipCallId] = session;

        var result = await _callClient
            .InitiateAsync(destinationNumber, cancellationToken)
            .ConfigureAwait(false);

        if (!result.Success)
        {
            LogCallFailed(_logger, sipCallId, result.ErrorMessage ?? "unknown error", null);
            _activeCalls.TryRemove(sipCallId, out _);
            session.Dispose();
            return null;
        }

        session.GvCallId = result.CallId;
        session.TransitionTo(CallState.Ringing);
        LogCallCreated(_logger, result.CallId, null);
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
            await _callClient.HangupAsync(session.GvCallId, cancellationToken).ConfigureAwait(false);
        }

        session.Dispose();
    }

    /// <summary>
    /// Registers an incoming GV call as a new ringing session.
    /// </summary>
    /// <param name="gvCallId">The GV-assigned call identifier.</param>
    /// <param name="callerNumber">The caller's phone number.</param>
    /// <returns>The created <see cref="CallSession"/> in the <see cref="CallState.Ringing"/> state.</returns>
    public CallSession HandleIncomingCall(string gvCallId, string callerNumber)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(gvCallId);
        ArgumentException.ThrowIfNullOrWhiteSpace(callerNumber);

        var sipCallId = $"incoming-{gvCallId}";
        var session = new CallSession(sipCallId, callerNumber);
        session.GvCallId = gvCallId;
        session.TransitionTo(CallState.Ringing);
        _activeCalls[sipCallId] = session;

        LogIncomingCall(_logger, gvCallId, callerNumber, sipCallId, null);
        return session;
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
