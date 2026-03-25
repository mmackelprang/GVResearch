using GvResearch.Softphone.Configuration;
using Microsoft.Extensions.Logging;
using SIPSorcery.Net;
using SIPSorcery.SIP;
using SIPSorcery.SIP.App;

namespace GvResearch.Softphone.Sip;

/// <summary>
/// SIPSorcery-based SIP user agent that handles registration and outbound calls.
/// </summary>
public sealed class SoftphoneSipClient : IDisposable
{
    private static readonly Action<ILogger, string, string, Exception?> LogRegistrationStarted =
        LoggerMessage.Define<string, string>(LogLevel.Information, new EventId(1, "RegistrationStarted"),
            "SIP registration started for {Username}@{Domain}.");

    private static readonly Action<ILogger, string, Exception?> LogRegistrationOk =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(2, "RegistrationOk"),
            "SIP registration successful: {Uri}.");

    private static readonly Action<ILogger, string, string, Exception?> LogRegistrationFail =
        LoggerMessage.Define<string, string>(LogLevel.Warning, new EventId(3, "RegistrationFail"),
            "SIP registration failed for {Uri}: {Message}.");

    private static readonly Action<ILogger, string, Exception?> LogCallInitiated =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(4, "CallInitiated"),
            "Initiating call to {Destination}.");

    private static readonly Action<ILogger, string, Exception?> LogCallFailed =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(5, "CallFailed"),
            "Call failed: {Error}.");

    private static readonly Action<ILogger, Exception?> LogHangup =
        LoggerMessage.Define(LogLevel.Information, new EventId(6, "Hangup"),
            "Call hung up.");

    private static readonly Action<ILogger, Exception?> LogRemoteHangup =
        LoggerMessage.Define(LogLevel.Information, new EventId(7, "RemoteHangup"),
            "Call hung up by remote.");

    private readonly SoftphoneSettings _settings;
    private readonly ILogger<SoftphoneSipClient> _logger;
    private SIPUserAgent? _userAgent;
    private SIPRegistrationUserAgent? _registrationAgent;
    private SIPTransport? _transport;
    private RTPSession? _currentRtpSession;
    private bool _disposed;

    /// <summary>Fired when a call is answered. The <see cref="RTPSession"/> is ready for media.</summary>
    public event EventHandler<RtpSessionEventArgs>? CallAnswered;

    /// <summary>Fired when an active call ends.</summary>
    public event EventHandler? CallEnded;

    /// <summary>Fired when the SIP client status changes.</summary>
    public event EventHandler<StatusEventArgs>? StatusChanged;

    public SoftphoneSipClient(SoftphoneSettings settings, ILogger<SoftphoneSipClient> logger)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(logger);
        _settings = settings;
        _logger = logger;
    }

    /// <summary>Starts the SIP transport and registers with the configured server.</summary>
    public Task RegisterAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        _transport = new SIPTransport();

        _registrationAgent = new SIPRegistrationUserAgent(
            _transport,
            _settings.SipUsername,
            _settings.SipPassword,
            _settings.SipServer,
            3600);

        _registrationAgent.RegistrationSuccessful += OnRegistrationSuccessful;
        _registrationAgent.RegistrationFailed += OnRegistrationFailed;
        _registrationAgent.Start();

        LogRegistrationStarted(_logger, _settings.SipUsername, _settings.SipDomain, null);

        return Task.CompletedTask;
    }

    /// <summary>Places an outbound call to <paramref name="destination"/>.</summary>
    public async Task<bool> CallAsync(string destination, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);

        if (_transport is null)
        {
            throw new InvalidOperationException("SIP client is not registered. Call RegisterAsync first.");
        }

        _userAgent?.Dispose();
        _userAgent = new SIPUserAgent(_transport, null);
        _userAgent.ClientCallAnswered += OnCallAnswered;
        _userAgent.ClientCallFailed += OnCallFailed;
        _userAgent.OnCallHungup += OnCallHungup;

        var dstUri = SIPURI.ParseSIPURI(FormattableString.Invariant($"sip:{destination}@{_settings.SipServer}:{_settings.SipPort}"));
        if (dstUri is null)
        {
            StatusChanged?.Invoke(this, new StatusEventArgs("Invalid destination URI"));
            return false;
        }

        LogCallInitiated(_logger, destination, null);
        StatusChanged?.Invoke(this, new StatusEventArgs($"Calling {destination}"));

        // Dispose any previous session before creating a new one.
        _currentRtpSession?.Dispose();
        _currentRtpSession = new RTPSession(false, false, false);
        _currentRtpSession.AcceptRtpFromAny = true;

        // Ownership of the RTP session is transferred to the user agent.
        // We retain the field reference so we can dispose it in our own Dispose().
        var result = await _userAgent.Call(
                dstUri.ToString(), _settings.SipUsername, _settings.SipPassword, _currentRtpSession)
            .ConfigureAwait(false);

        if (!result)
        {
            _currentRtpSession.Dispose();
            _currentRtpSession = null;
        }

        return result;
    }

    /// <summary>Hangs up the active call, if any.</summary>
    public void Hangup()
    {
        if (_userAgent?.IsCallActive == true)
        {
            _userAgent.Hangup();
            LogHangup(_logger, null);
        }
    }

    private void OnRegistrationSuccessful(SIPURI registerUri, SIPResponse response)
    {
        LogRegistrationOk(_logger, registerUri.ToString(), null);
        StatusChanged?.Invoke(this, new StatusEventArgs($"Registered: {registerUri}"));
    }

    private void OnRegistrationFailed(SIPURI registerUri, SIPResponse? failureResponse, string? errorMessage)
    {
        var message = failureResponse is not null
            ? $"Registration failed: {failureResponse.Status} {failureResponse.ReasonPhrase}"
            : $"Registration failed: {errorMessage}";
        LogRegistrationFail(_logger, registerUri.ToString(), message, null);
        StatusChanged?.Invoke(this, new StatusEventArgs(message));
    }

    private void OnCallAnswered(ISIPClientUserAgent uac, SIPResponse sipResponse)
    {
        if (_userAgent?.MediaSession is RTPSession rtpSession)
        {
            CallAnswered?.Invoke(this, new RtpSessionEventArgs(rtpSession));
        }

        StatusChanged?.Invoke(this, new StatusEventArgs("Call answered"));
    }

    private void OnCallFailed(ISIPClientUserAgent uac, string error, SIPResponse? errorResponse)
    {
        LogCallFailed(_logger, error, null);
        StatusChanged?.Invoke(this, new StatusEventArgs($"Call failed: {error}"));
    }

    private void OnCallHungup(SIPDialogue dialogue)
    {
        LogRemoteHangup(_logger, null);
        CallEnded?.Invoke(this, EventArgs.Empty);
        StatusChanged?.Invoke(this, new StatusEventArgs("Call ended"));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _registrationAgent?.Stop();
            _userAgent?.Dispose();
            _currentRtpSession?.Dispose();
            _transport?.Shutdown();
            _transport?.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>Event args carrying an <see cref="RTPSession"/>.</summary>
public sealed class RtpSessionEventArgs : EventArgs
{
    public RtpSessionEventArgs(RTPSession rtpSession)
    {
        ArgumentNullException.ThrowIfNull(rtpSession);
        RtpSession = rtpSession;
    }

    public RTPSession RtpSession { get; }
}

/// <summary>Event args carrying a status message.</summary>
public sealed class StatusEventArgs : EventArgs
{
    public StatusEventArgs(string message)
    {
        Message = message;
    }

    public string Message { get; }
}
