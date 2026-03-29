using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text;
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

    private static readonly Action<ILogger, string, Exception?> LogCallEnded =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(6, "SipEnded"),
            "SIP call {CallId} ended");

    private static readonly Action<ILogger, string, Exception?> LogError =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(7, "SipError"),
            "SIP error: {Message}");

    private readonly ILogger<SipWssCallTransport> _logger;
    private readonly Func<Task<SipCredentials>> _getCredentials;
    private readonly ConcurrentDictionary<string, SipCallSession> _activeCalls = new();

    private GvSipWebSocketChannel? _wsChannel;
    private SipCredentials? _credentials;
    private bool _registered;

    // IncomingCallReceived is never raised by this transport (GV SIP is outbound-only);
    // explicit accessors suppress CA1030/unused-event warnings.
    public event EventHandler<IncomingCallEventArgs>? IncomingCallReceived
    {
        add { }
        remove { }
    }

    // AudioReceived is part of ICallTransport; not yet wired to RTP receive path.
    // Explicit accessors suppress CS0067 (event never used).
    public event EventHandler<AudioDataEventArgs>? AudioReceived
    {
        add { }
        remove { }
    }

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

        var callId = Guid.NewGuid().ToString("D").ToUpperInvariant();

        // Format phone number
        var destNumber = toNumber.StartsWith('+') ? toNumber : $"+1{toNumber}";

        LogInviting(_logger, callId, destNumber, null);

        if (_wsChannel is null || _credentials is null)
            return new TransportCallResult(callId, false, "Not registered");

        try
        {
            // Create SDP offer using SIPSorcery's RTCPeerConnection
            using var pc = new SIPSorcery.Net.RTCPeerConnection(new SIPSorcery.Net.RTCConfiguration
            {
                iceServers = [new SIPSorcery.Net.RTCIceServer { urls = "stun:stun.l.google.com:19302" }]
            });

            var audioTrack = new MediaStreamTrack(
                SDPMediaTypesEnum.audio, false,
                new List<SDPAudioVideoMediaFormat>
                {
                    new(new AudioFormat(AudioCodecsEnum.OPUS, 111, 48000, 2, "minptime=10;useinbandfec=1")),
                    new(SDPWellKnownMediaFormatsEnum.G722),
                    new(SDPWellKnownMediaFormatsEnum.PCMU),
                    new(SDPWellKnownMediaFormatsEnum.PCMA),
                });
            pc.addTrack(audioTrack);

            var offer = pc.createOffer();
            await pc.setLocalDescription(offer).ConfigureAwait(false);

            // Build SIP INVITE with SDP
            var branch = CallProperties.CreateBranchId();
            var tag = CallProperties.CreateNewTag();
            var wsHost = $"{Guid.NewGuid():N}.invalid";

            // SIP messages are ASCII protocol text; invariant culture is correct here.
#pragma warning disable CA1305
            var invite = new StringBuilder();
            invite.AppendLine($"INVITE sip:{destNumber}@{SipDomain} SIP/2.0");
            invite.AppendLine($"Via: SIP/2.0/wss {wsHost};branch={branch};keep");
            invite.AppendLine($"From: <sip:{_credentials.SipUsername}@{SipDomain}>;tag={tag}");
            invite.AppendLine($"To: <sip:{destNumber}@{SipDomain}>");
            invite.AppendLine($"Call-ID: {callId}");
            invite.AppendLine($"CSeq: 1 INVITE");
            invite.AppendLine($"Contact: <sip:{Guid.NewGuid():N}@{wsHost};transport=wss>");
            invite.AppendLine($"Content-Type: application/sdp");
            invite.AppendLine($"Session-Expires: 90");
            invite.AppendLine($"Supported: timer,100rel,ice,replaces,outbound");
            invite.AppendLine($"User-Agent: {UserAgent}");
            invite.AppendLine($"Authorization: Bearer token=\"{_credentials.BearerToken}\", username=\"{_credentials.PhoneNumber}\", realm=\"{SipDomain}\"");
            invite.AppendLine($"Max-Forwards: 70");
            invite.AppendLine($"Content-Length: {offer.sdp.Length}");
            invite.AppendLine(); // empty line
#pragma warning restore CA1305
            invite.Append(offer.sdp); // SDP body

#pragma warning disable CA1848, CA1873 // Debug/UAT tool — LoggerMessage perf not required
            _logger.LogInformation("Sending SIP INVITE to {Number}, SDP={SdpLen} chars", destNumber, offer.sdp.Length);
#pragma warning restore CA1848, CA1873

            await _wsChannel.SendAsync(invite.ToString(), ct).ConfigureAwait(false);

            // The WebSocket receive loop will log the response
            // For now, return success (we'll add proper response handling later)
            return new TransportCallResult(callId, true, null);
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogError(_logger, ex.Message, ex);
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

        // TODO: send SIP BYE via _wsChannel when active call tracking is wired up
        // (SIPUserAgent/_userAgent removed; we now use raw WebSocket directly)

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

        // Connect to Google's SIP proxy via our custom WebSocket channel
        // (bypasses SSL cert name mismatch for raw IP connection)
        _wsChannel = new GvSipWebSocketChannel(
            new Uri($"wss://{SipProxyHost}:{SipProxyPort}"),
            _logger);

        var regTcs = new TaskCompletionSource<bool>();

        // Listen for SIP responses on the WebSocket
        _wsChannel.MessageReceived += (sender, args) =>
        {
            var message = args.Message;
#pragma warning disable CA1848, CA1873 // Debug/UAT tool — LoggerMessage perf not required
            _logger.LogInformation("SIP received:\n{Message}", message[..Math.Min(500, message.Length)]);
#pragma warning restore CA1848, CA1873

            if (message.StartsWith("SIP/2.0", StringComparison.Ordinal))
            {
                try
                {
                    var resp = SIPResponse.ParseSIPResponse(message);
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
                }
#pragma warning disable CA1031
                catch (Exception ex)
                {
#pragma warning disable CA1848, CA1873 // Debug/UAT tool — LoggerMessage perf not required
                    _logger.LogWarning(ex, "Failed to parse SIP response");
#pragma warning restore CA1848, CA1873
                }
#pragma warning restore CA1031
            }
        };

        await _wsChannel.ConnectAsync(ct).ConfigureAwait(false);

        // Store credentials for INVITE
        _credentials = creds;

        // Build REGISTER request
        var fromUri = SIPURI.ParseSIPURI($"sip:{creds.SipUsername}@{SipDomain}");
        var callId = Guid.NewGuid().ToString();
        var branch = CallProperties.CreateBranchId();
        var tag = CallProperties.CreateNewTag();

        // SIP messages are ASCII protocol text; invariant culture is correct here.
#pragma warning disable CA1305
        var register = new StringBuilder();
        register.AppendLine($"REGISTER sip:{SipDomain} SIP/2.0");
        register.AppendLine($"Via: SIP/2.0/wss {Guid.NewGuid():N}.invalid;branch={branch};keep");
        register.AppendLine($"To: <sip:{creds.SipUsername}@{SipDomain}>");
        register.AppendLine($"From: <sip:{creds.SipUsername}@{SipDomain}>;tag={tag}");
        register.AppendLine($"Call-ID: {callId}");
        register.AppendLine($"CSeq: 1 REGISTER");
        register.AppendLine($"Contact: <sip:{creds.SipUsername}@{Guid.NewGuid():N}.invalid;transport=wss>");
        register.AppendLine($"Expires: 600");
        register.AppendLine($"Authorization: Bearer token=\"{creds.BearerToken}\", username=\"{creds.PhoneNumber}\", realm=\"{SipDomain}\"");
        register.AppendLine($"User-Agent: {UserAgent}");
        register.AppendLine($"Max-Forwards: 70");
        register.AppendLine($"Content-Length: 0");
        register.AppendLine(); // empty line terminates headers
#pragma warning restore CA1305

#pragma warning disable CA1848, CA1873 // Debug/UAT tool — LoggerMessage perf not required
        _logger.LogInformation("Sending SIP REGISTER:\n{Register}", register.ToString()[..Math.Min(500, register.Length)]);
#pragma warning restore CA1848, CA1873

        await _wsChannel.SendAsync(register.ToString(), ct).ConfigureAwait(false);

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

        // _userAgent and _sipTransport not used in current implementation
        if (_wsChannel is not null)
        {
            await _wsChannel.CloseAsync().ConfigureAwait(false);
            _wsChannel.Dispose();
        }

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
#pragma warning disable CA1812 // Instantiated via ConcurrentDictionary in active-call tracking
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
#pragma warning restore CA1812
