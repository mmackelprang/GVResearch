using System.Collections.Concurrent;
using System.Net;
using GvResearch.Shared.Models;
using GvResearch.Shared.Transport;
using Microsoft.Extensions.Logging;
using SIPSorcery.Net;
using SIPSorcery.SIP;
using SIPSorcery.SIP.App;
using SIPSorceryMedia.Abstractions;

namespace GvResearch.Sip.Transport;

/// <summary>
/// ICallTransport implementation using SIP over WebSocket (RFC 7118)
/// to Google Voice's SIP proxy at 216.239.36.145:443.
///
/// Call flow (from captured traffic):
///   1. sipregisterinfo/get → Bearer token + SIP identity
///   2. WSS connect to 216.239.36.145:443 (subprotocol "sip")
///   3. SIP REGISTER with Bearer auth
///   4. SIP INVITE sip:+{phone}@web.c.pbx.voice.sip.google.com
///   5. 183 Session Progress (SDP answer, early media)
///   6. PRACK → 200 OK
///   7. 180 Ringing → PRACK → 200 OK
///   8. 200 OK (INVITE) → ACK → audio flows
///   9. BYE to hangup
/// </summary>
public sealed class SipWssCallTransport : ICallTransport
{
    private const string SipDomain = "web.c.pbx.voice.sip.google.com";
    private const string SipProxyHost = "216.239.36.145";
    private const int SipProxyPort = 443;
    private const string UserAgent = "GoogleVoice voice.web-frontend_20260318.08_p1";

    private static readonly Action<ILogger, Exception?> LogRegistering =
        LoggerMessage.Define(LogLevel.Information, new EventId(1, "SipRegistering"),
            "SIP REGISTER to Google Voice...");

    private static readonly Action<ILogger, Exception?> LogRegistered =
        LoggerMessage.Define(LogLevel.Information, new EventId(2, "SipRegistered"),
            "SIP registration successful");

    private static readonly Action<ILogger, string, string, Exception?> LogInviting =
        LoggerMessage.Define<string, string>(LogLevel.Information, new EventId(3, "SipInviting"),
            "SIP INVITE {CallId} to {ToNumber}");

    private static readonly Action<ILogger, string, Exception?> LogCallConnected =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(5, "SipConnected"),
            "SIP call {CallId} connected — audio flowing");

    private static readonly Action<ILogger, string, Exception?> LogCallEnded =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(6, "SipEnded"),
            "SIP call {CallId} ended");

    private static readonly Action<ILogger, string, Exception?> LogError =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(7, "SipError"),
            "SIP error: {Message}");

    private readonly ILogger<SipWssCallTransport> _logger;
    private readonly Func<Task<SipCredentials>> _getCredentials;
    private readonly ConcurrentDictionary<string, SipCallSession> _activeCalls = new();

    private SIPTransport? _sipTransport;
    private SIPUserAgent? _userAgent;
    private bool _registered;

    // IncomingCallReceived is never raised by this transport (GV SIP is outbound-only);
    // explicit accessors suppress CA1030/unused-event warnings.
    public event EventHandler<IncomingCallEventArgs>? IncomingCallReceived
    {
        add { }
        remove { }
    }

    public event EventHandler<AudioDataEventArgs>? AudioReceived;

    /// <param name="logger">Logger</param>
    /// <param name="getCredentials">Async factory that calls sipregisterinfo/get and returns credentials</param>
    public SipWssCallTransport(
        ILogger<SipWssCallTransport> logger,
        Func<Task<SipCredentials>> getCredentials)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(getCredentials);
        _logger = logger;
        _getCredentials = getCredentials;
    }

    public async Task<TransportCallResult> InitiateAsync(string toNumber, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(toNumber);

        // Ensure registered
        if (!_registered)
        {
            await RegisterAsync(ct).ConfigureAwait(false);
        }

        var callId = $"gv-{Guid.NewGuid():N}";

        // Format phone number
        var sipUri = toNumber.StartsWith('+')
            ? $"sip:{toNumber}@{SipDomain}"
            : $"sip:+1{toNumber}@{SipDomain}";

        LogInviting(_logger, callId, toNumber, null);

        try
        {
            // Create RTP session for audio
            var rtpSession = new RTPSession(false, false, false);
            var audioTrack = new MediaStreamTrack(
                SDPMediaTypesEnum.audio,
                false,
                new List<SDPAudioVideoMediaFormat>
                {
                    new(new AudioFormat(AudioCodecsEnum.OPUS, 111, 48000, 2, "minptime=10;useinbandfec=1")),
                    new(SDPWellKnownMediaFormatsEnum.G722),
                    new(SDPWellKnownMediaFormatsEnum.PCMU),
                    new(SDPWellKnownMediaFormatsEnum.PCMA),
                });
            rtpSession.addTrack(audioTrack);
            rtpSession.AcceptRtpFromAny = true;

            // Wire audio events
            rtpSession.OnRtpPacketReceived += (ep, mt, pkt) =>
            {
                if (mt == SDPMediaTypesEnum.audio)
                {
                    AudioReceived?.Invoke(this, new AudioDataEventArgs(
                        callId, pkt.Payload, 48000));
                }
            };

            var session = new SipCallSession(callId, rtpSession);
            _activeCalls[callId] = session;

            // Make the call — use string overload (SIPURI overload removed in SIPSorcery 10.x)
            var callResult = await _userAgent!.Call(sipUri, null, null, rtpSession).ConfigureAwait(false);

            if (!callResult)
            {
                _activeCalls.TryRemove(callId, out _);
                session.Dispose();
                return new TransportCallResult(callId, false, "SIP INVITE failed");
            }

            LogCallConnected(_logger, callId, null);
            session.Status = CallStatusType.Active;

            // Listen for remote hangup
            _userAgent!.OnCallHungup += (dialog) =>
            {
                LogCallEnded(_logger, callId, null);
                session.Status = CallStatusType.Completed;
                _activeCalls.TryRemove(callId, out _);
                session.Dispose();
            };

            return new TransportCallResult(callId, true, null);
        }
#pragma warning disable CA1031 // Catch-all is intentional: return failure result rather than propagating SIP exceptions
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogError(_logger, ex.Message, ex);
            _activeCalls.TryRemove(callId, out _);
            return new TransportCallResult(callId, false, ex.Message);
        }
    }

    public Task<TransportCallStatus> GetStatusAsync(string callId, CancellationToken ct = default)
    {
        var status = _activeCalls.TryGetValue(callId, out var session)
            ? session.Status
            : CallStatusType.Unknown;
        return Task.FromResult(new TransportCallStatus(callId, status));
    }

    public async Task HangupAsync(string callId, CancellationToken ct = default)
    {
        LogCallEnded(_logger, callId, null);

        if (_userAgent?.IsCallActive == true)
        {
            _userAgent.Hangup();
        }

        if (_activeCalls.TryRemove(callId, out var session))
        {
            session.Status = CallStatusType.Completed;
            session.Dispose();
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

    public void SendAudio(string callId, ReadOnlyMemory<byte> pcmData, int sampleRate)
    {
        if (_activeCalls.TryGetValue(callId, out var session))
        {
            // Send raw PCM — SIPSorcery's RTPSession handles encoding
            session.RtpSession.SendAudio((uint)(pcmData.Length / 2), pcmData.ToArray());
        }
    }

    private async Task RegisterAsync(CancellationToken ct)
    {
        LogRegistering(_logger, null);

        var creds = await _getCredentials().ConfigureAwait(false);

        // Create SIP transport with WebSocket channel
        _sipTransport = new SIPTransport();

#pragma warning disable CA2000 // SIPTransport takes ownership of the channel and disposes it
        var wsChannel = new SIPClientWebSocketChannel();
#pragma warning restore CA2000
        _sipTransport.AddSIPChannel(wsChannel);

        // Create SIP user agent
        _userAgent = new SIPUserAgent(_sipTransport, null);

        // Build REGISTER request manually with Bearer auth
        var registerUri = SIPURI.ParseSIPURI($"sip:{SipDomain}");
        var fromUri = SIPURI.ParseSIPURI($"sip:{creds.SipUsername}@{SipDomain}");

        var regRequest = SIPRequest.GetRequest(
            SIPMethodsEnum.REGISTER,
            registerUri,
            new SIPToHeader(null, fromUri, null),
            new SIPFromHeader(null, fromUri, CallProperties.CreateNewTag()));

        regRequest.Header.Contact = [new SIPContactHeader(null, fromUri)];
        regRequest.Header.Expires = 600;
        regRequest.Header.UserAgent = UserAgent;

        // Bearer token auth (RFC 6750 style, as used by GV).
        // SIPSorcery's SIPAuthenticationHeader only supports Digest — add Bearer as a raw unknown header.
        regRequest.Header.UnknownHeaders.Add(
            $"Authorization: Bearer token=\"{creds.BearerToken}\", username=\"{creds.PhoneNumber}\", realm=\"{SipDomain}\"");

        // Set the proxy route
        var proxyUri = SIPURI.ParseSIPURI($"sip:{SipProxyHost}:{SipProxyPort};transport=wss;lr");
        regRequest.Header.Routes.PushRoute(new SIPRoute(proxyUri));

        // Send REGISTER
        var regTcs = new TaskCompletionSource<bool>();

        _sipTransport.SIPTransportResponseReceived += (localEp, remoteEp, resp) =>
        {
            if (resp.Header.CSeqMethod == SIPMethodsEnum.REGISTER)
            {
                if (resp.Status == SIPResponseStatusCodesEnum.Ok)
                {
                    LogRegistered(_logger, null);
                    _registered = true;
                    regTcs.TrySetResult(true);
                }
                else
                {
                    LogError(_logger, $"REGISTER failed: {(int)resp.Status} {resp.ReasonPhrase}", null);
                    regTcs.TrySetResult(false);
                }
            }

            return Task.CompletedTask;
        };

        // Connect and send
        var remoteEp = new SIPEndPoint(SIPProtocolsEnum.wss,
            new IPEndPoint(IPAddress.Parse(SipProxyHost), SipProxyPort));

        await _sipTransport.SendRequestAsync(remoteEp, regRequest).ConfigureAwait(false);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));

        var success = await regTcs.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
        if (!success)
        {
            throw new InvalidOperationException("SIP REGISTER failed");
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var session in _activeCalls.Values)
        {
            session.Dispose();
        }
        _activeCalls.Clear();

        _userAgent?.Dispose();
        _sipTransport?.Dispose();

        await Task.CompletedTask.ConfigureAwait(false);
    }
}

/// <summary>Credentials obtained from sipregisterinfo/get</summary>
public sealed record SipCredentials(
    string SipUsername,
    string BearerToken,
    string PhoneNumber,
    int ExpirySeconds);

/// <summary>Per-call state</summary>
internal sealed class SipCallSession : IDisposable
{
    public string CallId { get; }
    public RTPSession RtpSession { get; }
    public CallStatusType Status { get; set; } = CallStatusType.Unknown;

    public SipCallSession(string callId, RTPSession rtpSession)
    {
        CallId = callId;
        RtpSession = rtpSession;
    }

    public void Dispose()
    {
        RtpSession.Close("call ended");
    }
}
