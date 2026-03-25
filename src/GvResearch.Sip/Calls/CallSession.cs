using SIPSorcery.Net;

namespace GvResearch.Sip.Calls;

/// <summary>
/// Lifecycle state of a call session.
/// </summary>
public enum CallState
{
    /// <summary>Call is being set up; waiting for GV to respond.</summary>
    Initiating,

    /// <summary>GV has accepted; ringing the remote party.</summary>
    Ringing,

    /// <summary>Both legs are connected and media is flowing.</summary>
    Active,

    /// <summary>Teardown in progress.</summary>
    Ending,

    /// <summary>Both legs have been released.</summary>
    Ended,
}

/// <summary>
/// Holds per-call state for a single bridged call.
/// </summary>
public sealed class CallSession : IDisposable
{
    private volatile int _state = (int)CallState.Initiating;
    private bool _disposed;

    /// <param name="callId">The SIP Call-ID from the INVITE.</param>
    /// <param name="destinationNumber">The dialled phone number.</param>
    public CallSession(string callId, string destinationNumber)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(callId);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationNumber);

        CallId = callId;
        DestinationNumber = destinationNumber;
    }

    /// <summary>The SIP Call-ID header value from the originating INVITE.</summary>
    public string CallId { get; }

    /// <summary>The GV-assigned call identifier, set once the back-end call is initiated.</summary>
    public string? GvCallId { get; set; }

    /// <summary>Current lifecycle state.</summary>
    public CallState State => (CallState)_state;

    /// <summary>The dialled phone number.</summary>
    public string DestinationNumber { get; }

    /// <summary>RTP session for the SIP-side audio.</summary>
    public RTPSession? SipRtpSession { get; set; }

    /// <summary>RTP session for the GV-side audio.</summary>
    public RTPSession? GvRtpSession { get; set; }

    /// <summary>Transitions the call to the given state.</summary>
    public void TransitionTo(CallState newState)
    {
        Interlocked.Exchange(ref _state, (int)newState);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        TransitionTo(CallState.Ended);
        SipRtpSession?.Dispose();
        GvRtpSession?.Dispose();
    }
}
