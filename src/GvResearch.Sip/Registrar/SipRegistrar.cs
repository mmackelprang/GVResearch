using GvResearch.Sip.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SIPSorcery.SIP;
using SIPSorcery.SIP.App;

namespace GvResearch.Sip.Registrar;

// ---------------------------------------------------------------------------
// Event args
// ---------------------------------------------------------------------------

/// <summary>Event arguments carrying a received <see cref="SIPRequest"/>.</summary>
public sealed class SipRequestEventArgs : EventArgs
{
    /// <param name="request">The received SIP request.</param>
    public SipRequestEventArgs(SIPRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        Request = request;
    }

    /// <summary>The received SIP request.</summary>
    public SIPRequest Request { get; }
}

// ---------------------------------------------------------------------------
// SipRegistrar
// ---------------------------------------------------------------------------

/// <summary>
/// Handles incoming SIP REGISTER requests with digest authentication,
/// fires events for INVITE and BYE methods.
/// </summary>
public sealed class SipRegistrar : IDisposable
{
    // ------------------------------------------------------------------
    // Log messages
    // ------------------------------------------------------------------
    private static readonly Action<ILogger, string, Exception?> LogRegisterChallenging =
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(1, "RegisterChallenging"),
            "Challenging REGISTER from {Username}.");

    private static readonly Action<ILogger, string, Exception?> LogRegisterSuccess =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(2, "RegisterSuccess"),
            "Registration accepted for {Username}.");

    private static readonly Action<ILogger, string, Exception?> LogRegisterFailed =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(3, "RegisterFailed"),
            "Authentication failed for {Username}.");

    private static readonly Action<ILogger, string, Exception?> LogRegisterUnknown =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(4, "RegisterUnknown"),
            "REGISTER from unknown username {Username}.");

    private static readonly Action<ILogger, string, Exception?> LogInviteReceived =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(5, "InviteReceived"),
            "INVITE received for {Uri}.");

    private static readonly Action<ILogger, string, Exception?> LogByeReceived =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(6, "ByeReceived"),
            "BYE received for call {CallId}.");

    // ------------------------------------------------------------------
    // Fields
    // ------------------------------------------------------------------
    private readonly SIPTransport _transport;
    private readonly RegistrationStore _store;
    private readonly SipGatewayOptions _options;
    private readonly ILogger<SipRegistrar> _logger;
    private bool _disposed;

    // ------------------------------------------------------------------
    // Events
    // ------------------------------------------------------------------

    /// <summary>
    /// Fired when an INVITE is received from the transport.
    /// </summary>
    public event EventHandler<SipRequestEventArgs>? InviteReceived;

    /// <summary>
    /// Fired when a BYE is received from the transport.
    /// </summary>
    public event EventHandler<SipRequestEventArgs>? ByeReceived;

    // ------------------------------------------------------------------
    // Constructor
    // ------------------------------------------------------------------

    /// <param name="transport">The shared SIP transport layer.</param>
    /// <param name="store">The in-memory registration store.</param>
    /// <param name="options">Gateway configuration.</param>
    /// <param name="logger">Logger.</param>
    public SipRegistrar(
        SIPTransport transport,
        RegistrationStore store,
        IOptions<SipGatewayOptions> options,
        ILogger<SipRegistrar> logger)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _transport = transport;
        _store = store;
        _options = options.Value;
        _logger = logger;

        _transport.SIPTransportRequestReceived += OnRequestReceivedAsync;
    }

    // ------------------------------------------------------------------
    // Core handler
    // ------------------------------------------------------------------

    private async Task OnRequestReceivedAsync(
        SIPEndPoint localEndPoint,
        SIPEndPoint remoteEndPoint,
        SIPRequest request)
    {
        switch (request.Method)
        {
            case SIPMethodsEnum.REGISTER:
                await HandleRegisterAsync(localEndPoint, remoteEndPoint, request).ConfigureAwait(false);
                break;

            case SIPMethodsEnum.INVITE:
                LogInviteReceived(_logger, request.URI.ToString(), null);
                await SendOkResponseAsync(request, SIPResponseStatusCodesEnum.Trying).ConfigureAwait(false);
                InviteReceived?.Invoke(this, new SipRequestEventArgs(request));
                break;

            case SIPMethodsEnum.BYE:
                LogByeReceived(_logger, request.Header.CallId, null);
                await SendOkResponseAsync(request, SIPResponseStatusCodesEnum.Ok).ConfigureAwait(false);
                ByeReceived?.Invoke(this, new SipRequestEventArgs(request));
                break;

            default:
                await SendOkResponseAsync(request, SIPResponseStatusCodesEnum.MethodNotAllowed).ConfigureAwait(false);
                break;
        }
    }

    // ------------------------------------------------------------------
    // REGISTER handler
    // ------------------------------------------------------------------

    private async Task HandleRegisterAsync(
        SIPEndPoint localEndPoint,
        SIPEndPoint remoteEndPoint,
        SIPRequest request)
    {
        var fromUser = request.Header.From.FromURI.User;

        // Find the account in configuration.
        var account = _options.Accounts
            .FirstOrDefault(a => string.Equals(a.Username, fromUser, StringComparison.OrdinalIgnoreCase));

        if (account is null)
        {
            LogRegisterUnknown(_logger, fromUser, null);
            await SendOkResponseAsync(request, SIPResponseStatusCodesEnum.Forbidden).ConfigureAwait(false);
            return;
        }

        // Wrap config in ISIPAccount adapter.
        var sipAccount = new SipAccountAdapter(account, _options.SipDomain);

        // Ask SIPSorcery to authenticate.
        var authResult = SIPRequestAuthenticator.AuthenticateSIPRequest(
            localEndPoint,
            remoteEndPoint,
            request,
            sipAccount);

        if (!authResult.Authenticated)
        {
            if (authResult.AuthenticationRequiredHeader is not null)
            {
                // Issue 401 challenge.
                LogRegisterChallenging(_logger, fromUser, null);
                var challengeResponse = SIPResponse.GetResponse(request, SIPResponseStatusCodesEnum.Unauthorised, null);
                challengeResponse.Header.AuthenticationHeaders.Add(authResult.AuthenticationRequiredHeader);
                await _transport.SendResponseAsync(challengeResponse).ConfigureAwait(false);
                return;
            }

            LogRegisterFailed(_logger, fromUser, null);
            await SendOkResponseAsync(request, SIPResponseStatusCodesEnum.Forbidden).ConfigureAwait(false);
            return;
        }

        // Determine expiry.
        var expiresSeconds = DetermineExpiry(request);

        if (expiresSeconds <= 0)
        {
            // De-registration.
            _store.Remove(fromUser);
        }
        else
        {
            var contactUriString = request.Header.Contact?.FirstOrDefault()?.ContactURI?.ToString()
                ?? remoteEndPoint.ToString();
            var contactUri = new Uri(contactUriString, UriKind.RelativeOrAbsolute);
            var expiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresSeconds);
            _store.AddOrUpdate(fromUser, contactUri, expiresAt);
            LogRegisterSuccess(_logger, fromUser, null);
        }

        await SendOkResponseAsync(request, SIPResponseStatusCodesEnum.Ok).ConfigureAwait(false);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static long DetermineExpiry(SIPRequest request)
    {
        // Contact-level expiry takes precedence.
        var contact = request.Header.Contact?.FirstOrDefault();
        if (contact is not null && contact.Expires > 0)
        {
            return contact.Expires;
        }

        // Fall back to header-level Expires.
        if (request.Header.Expires > 0)
        {
            return request.Header.Expires;
        }

        // Default.
        return 3600;
    }

    private async Task SendOkResponseAsync(SIPRequest request, SIPResponseStatusCodesEnum status)
    {
        var response = SIPResponse.GetResponse(request, status, null);
        await _transport.SendResponseAsync(response).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _transport.SIPTransportRequestReceived -= OnRequestReceivedAsync;
    }

    // ------------------------------------------------------------------
    // ISIPAccount adapter
    // ------------------------------------------------------------------

    private sealed class SipAccountAdapter : ISIPAccount
    {
        private readonly SipAccountOptions _account;

        public SipAccountAdapter(SipAccountOptions account, string domain)
        {
            _account = account;
            SIPDomain = domain;
        }

        public string ID => _account.Username;
        public string SIPUsername => _account.Username;
        public string SIPPassword => _account.Password;

        /// <summary>
        /// HA1 = MD5(username:realm:password) — not pre-computed here;
        /// returning empty string tells SIPSorcery to derive it from SIPPassword.
        /// </summary>
        public string HA1Digest => string.Empty;

        public string SIPDomain { get; }
        public bool IsDisabled => false;
    }
}
