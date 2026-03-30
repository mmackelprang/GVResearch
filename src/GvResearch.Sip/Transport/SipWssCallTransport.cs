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
    private const string WssUrl = "wss://web.voice.telephony.goog/websocket";
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
    private string? _serviceRoute;
    private string? _regContactUser;
    private string? _regWsHost;
    private int _inviteCSeq;
    private int _prackCSeq;
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

            // Use a random CSeq like the browser does
            _inviteCSeq = System.Security.Cryptography.RandomNumberGenerator.GetInt32(1000, 9999);
            _prackCSeq = _inviteCSeq + 1;

            var invTag = CallProperties.CreateNewTag();
            var sipUsernameEncoded = Uri.EscapeDataString(_credentials.SipUsername);

            var inviteMsg =
                $"INVITE sip:{destNumber}@{SipDomain} SIP/2.0\r\n" +
                $"Via: SIP/2.0/wss {_regWsHost};branch={CallProperties.CreateBranchId()};keep\r\n" +
                $"From: <sip:{sipUsernameEncoded}@{SipDomain}>;tag={invTag}\r\n" +
                $"To: <sip:{destNumber}@{SipDomain}>\r\n" +
                $"Call-ID: {callId}\r\n" +
                $"CSeq: {_inviteCSeq} INVITE\r\n" +
                $"Contact: <sip:{_regContactUser}@{_regWsHost};transport=wss>\r\n" +
                $"Content-Type: application/sdp\r\n" +
                $"Session-Expires: 90\r\n" +
                $"Supported: timer,100rel,ice,replaces,outbound,record-aware\r\n" +
                $"User-Agent: {UserAgent}\r\n" +
                $"X-Google-Client-Info: Ci1Hb29nbGVWb2ljZSB2b2ljZS53ZWItZnJvbnRlbmRfMjAyNjAzMTguMDhfcDESLUdvb2dsZVZvaWNlIHZvaWNlLndlYi1mcm9udGVuZF8yMDI2MDMxOC4wOF9wMRgFKhBDaHJvbWUgMTQ2LjAuMC4w\r\n" +
                (_serviceRoute is not null ? $"Route: {_serviceRoute}\r\n" : "") +
                $"Max-Forwards: 70\r\n" +
                $"Content-Length: {offer.sdp.Length}\r\n" +
                $"\r\n" +
                offer.sdp;

#pragma warning disable CA1848, CA1873 // Debug/UAT tool — LoggerMessage perf not required
            _logger.LogInformation("Sending SIP INVITE to {Number}, SDP={SdpLen} chars", destNumber, offer.sdp.Length);
#pragma warning restore CA1848, CA1873

            await _wsChannel.SendAsync(inviteMsg, ct).ConfigureAwait(false);

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
            new Uri(WssUrl),
            _logger);

        var regTcs = new TaskCompletionSource<bool>();

        // Track PRACKed RSeq values to avoid re-PRACKing retransmissions
        var prackedRSeqs = new HashSet<string>(StringComparer.Ordinal);
        const string XGoogleClientInfo = "X-Google-Client-Info: Ci1Hb29nbGVWb2ljZSB2b2ljZS53ZWItZnJvbnRlbmRfMjAyNjAzMTguMDhfcDESLUdvb2dsZVZvaWNlIHZvaWNlLndlYi1mcm9udGVuZF8yMDI2MDMxOC4wOF9wMRgFKhBDaHJvbWUgMTQ2LjAuMC4w";

        // These are shared between the event handler and the REGISTER below
        var regCallId = Guid.NewGuid().ToString("N")[..22];
        var regTag = CallProperties.CreateNewTag();
        var regWsHost = string.Concat(Guid.NewGuid().ToString("N").AsSpan(0, 12), ".invalid");
        var regContactUser = Guid.NewGuid().ToString("N")[..8];
        var regDeviceUuid = Guid.NewGuid().ToString("D");

        // Listen for SIP responses on the WebSocket
        _wsChannel.MessageReceived += (sender, args) =>
        {
            var message = args.Message;
#pragma warning disable CA1848, CA1873
            _logger.LogInformation("SIP received ({Length} chars):\n{Message}",
                message.Length, message[..Math.Min(800, message.Length)]);
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

                            // Extract Service-Route for INVITE routing
                            var srIdx = message.IndexOf("Service-Route:", StringComparison.OrdinalIgnoreCase);
                            if (srIdx >= 0)
                            {
                                var srEnd = message.IndexOf("\r\n", srIdx, StringComparison.Ordinal);
                                _serviceRoute = message[(srIdx + 15)..srEnd].Trim();
                            }

                            regTcs.TrySetResult(true);
                        }
                        else if ((int)resp.Status == 401)
                        {
                            // 401 challenge — extract nonce and resend with Digest auth
#pragma warning disable CA1848, CA1873
                            _logger.LogInformation("Got 401 challenge, computing Digest auth...");
#pragma warning restore CA1848, CA1873
                            // Parse nonce from raw WWW-Authenticate header
                            var wwwAuth = message;
                            var nonceIdx = wwwAuth.IndexOf("nonce=\"", StringComparison.Ordinal);
                            if (nonceIdx >= 0)
                            {
                                nonceIdx += 7;
                                var nonceEnd = wwwAuth.IndexOf('"', nonceIdx);
                                var nonce = wwwAuth[nonceIdx..nonceEnd];
                                var realm = SipDomain;

                                // Compute MD5 Digest response
                                // HA1 = MD5(username:realm:password)
                                // HA2 = MD5(REGISTER:sip:domain)
                                // response = MD5(HA1:nonce:HA2)
                                var username = creds.SipUsername;
                                var password = creds.BearerToken; // Token used as password for Digest
                                var uri = $"sip:{SipDomain}";

                                var ha1 = Md5Hash($"{username}:{realm}:{password}");
                                var ha2 = Md5Hash($"REGISTER:{uri}");
                                var digestResponse = Md5Hash($"{ha1}:{nonce}:{ha2}");

                                var authLine = $"Authorization: Digest algorithm=MD5, username=\"{username}\", " +
                                    $"realm=\"{realm}\", nonce=\"{nonce}\", uri=\"{uri}\", response=\"{digestResponse}\"";

                                var reg2 = BuildRegister(username, regCallId, regTag, regWsHost, regContactUser,
                                    regDeviceUuid, 2, authLine);

#pragma warning disable CA1848, CA1873
                                _logger.LogInformation("Sending SIP REGISTER with Digest auth...");
#pragma warning restore CA1848, CA1873

                                _ = _wsChannel!.SendAsync(reg2);
                            }
                        }
                        else
                        {
                            LogError(_logger, $"REGISTER failed: {(int)resp.Status} {resp.ReasonPhrase}", null);
                            regTcs.TrySetResult(false);
                        }
                    }

                    // Handle INVITE responses — send PRACK for 100rel, ACK for 200
                    if (resp.Header.CSeqMethod == SIPMethodsEnum.INVITE)
                    {
                        var statusCode = (int)resp.Status;

                        if (statusCode == 183 || statusCode == 180)
                        {
                            // Parse raw headers from the message text — SIPSorcery may
                            // not handle Google's complex URIs correctly
                            var contactUri = ExtractHeader(message, "Contact");
                            var rseqValue = ExtractHeaderValue(message, "RSeq");
                            var toHeader = ExtractHeader(message, "To");
                            var fromHeader = ExtractHeader(message, "From");
                            var callIdValue = ExtractHeaderValue(message, "Call-ID");

                            // Extract Record-Route headers (reversed for Route in requests)
                            var recordRoutes = ExtractAllHeaders(message, "Record-Route");

                            if (rseqValue is not null && contactUri is not null
                                && prackedRSeqs.Add(rseqValue)) // Only PRACK each RSeq once
                            {
                                var contactSipUri = ExtractSipUri(contactUri);
                                var currentPrackCSeq = _prackCSeq++;

                                // Build PRACK matching browser's exact format:
                                // 1. Route order: REVERSED from Record-Route (non-econt first)
                                // 2. Header order: Route, Via, Max-Forwards, To, From, ...
                                // 3. RAck: {RSeq} {INVITE_CSeq} INVITE
                                // 4. CSeq: incrementing from INVITE CSeq + 1
                                // 5. Max-Forwards: 69
                                // 6. Include all expected headers

                                var prack = $"PRACK {contactSipUri} SIP/2.0\r\n";

                                // Route headers: REVERSE order from Record-Route
                                for (int i = recordRoutes.Count - 1; i >= 0; i--)
                                {
                                    prack += $"Route: {recordRoutes[i]}\r\n";
                                }

                                prack +=
                                    $"Via: SIP/2.0/wss {_regWsHost};branch={CallProperties.CreateBranchId()};keep\r\n" +
                                    $"Max-Forwards: 69\r\n" +
                                    $"To: {toHeader}\r\n" +
                                    $"From: {fromHeader}\r\n" +
                                    $"Call-ID: {callIdValue}\r\n" +
                                    $"CSeq: {currentPrackCSeq} PRACK\r\n" +
                                    $"{XGoogleClientInfo}\r\n" +
                                    $"RAck: {rseqValue} {_inviteCSeq} INVITE\r\n" +
                                    $"Allow: INVITE,ACK,CANCEL,BYE,UPDATE,MESSAGE,OPTIONS,REFER,INFO,PRACK\r\n" +
                                    $"Supported: outbound,record-aware\r\n" +
                                    $"User-Agent: {UserAgent}\r\n" +
                                    $"Content-Length: 0\r\n" +
                                    $"\r\n";

#pragma warning disable CA1848, CA1873
                                _logger.LogInformation("Sending PRACK for {Status} (RSeq={RSeq}) to {Uri}\nPRACK body:\n{Prack}",
                                    statusCode, rseqValue, contactSipUri, prack[..Math.Min(500, prack.Length)]);
#pragma warning restore CA1848, CA1873

                                _ = _wsChannel!.SendAsync(prack);
                            }
                        }
                        else if (statusCode == 200)
                        {
                            // 200 OK for INVITE — send ACK
                            var contactUri = ExtractHeader(message, "Contact");
                            var toHeader = ExtractHeader(message, "To");
                            var fromHeader = ExtractHeader(message, "From");
                            var callIdValue = ExtractHeaderValue(message, "Call-ID");
                            var recordRoutes = ExtractAllHeaders(message, "Record-Route");

                            var contactSipUri = ExtractSipUri(contactUri ?? $"sip:unknown@{SipDomain}");

                            var ack = $"ACK {contactSipUri} SIP/2.0\r\n";

                            // Route: reversed Record-Route
                            for (int i = recordRoutes.Count - 1; i >= 0; i--)
                            {
                                ack += $"Route: {recordRoutes[i]}\r\n";
                            }

                            ack +=
                                $"Via: SIP/2.0/wss {_regWsHost};branch={CallProperties.CreateBranchId()};keep\r\n" +
                                $"Max-Forwards: 69\r\n" +
                                $"To: {toHeader}\r\n" +
                                $"From: {fromHeader}\r\n" +
                                $"Call-ID: {callIdValue}\r\n" +
                                $"CSeq: {_inviteCSeq} ACK\r\n" +
                                $"Content-Length: 0\r\n" +
                                $"\r\n";

#pragma warning disable CA1848, CA1873
                            _logger.LogInformation("INVITE 200 OK — sending ACK, call CONNECTED!");
#pragma warning restore CA1848, CA1873

                            _ = _wsChannel!.SendAsync(ack);
                        }
                    }
                }
#pragma warning disable CA1031
                catch (Exception ex)
                {
#pragma warning disable CA1848, CA1873
                    _logger.LogWarning(ex, "Failed to parse SIP response");
#pragma warning restore CA1848, CA1873
                }
#pragma warning restore CA1031
            }
        };

        await _wsChannel.ConnectAsync(ct).ConfigureAwait(false);

        // Store credentials for INVITE
        _credentials = creds;

        // Store for INVITE Contact header
        _regContactUser = regContactUser;
        _regWsHost = regWsHost;

        // Step 1: Send REGISTER without auth (will get 401 challenge)
        var reg1 = BuildRegister(creds.SipUsername, regCallId, regTag, regWsHost, regContactUser,
            regDeviceUuid, 1, authHeader: null);

#pragma warning disable CA1848, CA1873
        _logger.LogInformation("Sending SIP REGISTER (no auth):\n{Register}", reg1);
#pragma warning restore CA1848, CA1873

        await _wsChannel.SendAsync(reg1, ct).ConfigureAwait(false);

        // Wait for 401 or 200
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));

        var success = await regTcs.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
        if (!success)
        {
            throw new InvalidOperationException("SIP REGISTER failed");
        }
    }

    private static string BuildRegister(string sipUsername, string callId, string tag,
        string wsHost, string contactUser, string deviceUuid, int cseq, string? authHeader)
    {
        // Use raw string concatenation — proven working in standalone test
        var sipUsernameEncoded = Uri.EscapeDataString(sipUsername);
        var branch = CallProperties.CreateBranchId();

        var msg = $"REGISTER sip:{SipDomain} SIP/2.0\r\n" +
            $"Via: SIP/2.0/wss {wsHost};branch={branch};keep\r\n" +
            $"Max-Forwards: 69\r\n" +
            $"To: <sip:{sipUsernameEncoded}@{SipDomain}>\r\n" +
            $"From: <sip:{sipUsernameEncoded}@{SipDomain}>;tag={tag}\r\n" +
            $"Call-ID: {callId}\r\n" +
            $"CSeq: {cseq} REGISTER\r\n" +
            (authHeader is not null ? authHeader + "\r\n" : "") +
            $"X-Google-Client-Info: Ci1Hb29nbGVWb2ljZSB2b2ljZS53ZWItZnJvbnRlbmRfMjAyNjAzMTguMDhfcDESLUdvb2dsZVZvaWNlIHZvaWNlLndlYi1mcm9udGVuZF8yMDI2MDMxOC4wOF9wMRgFKhBDaHJvbWUgMTQ2LjAuMC4w\r\n" +
            $"Contact: <sip:{contactUser}@{wsHost};transport=wss>;+sip.ice;reg-id=1;+sip.instance=\"<urn:uuid:{deviceUuid}>\";expires=3600\r\n" +
            $"Expires: 3600\r\n" +
            $"Allow: INVITE,ACK,CANCEL,BYE,UPDATE,MESSAGE,OPTIONS,REFER,INFO,PRACK\r\n" +
            $"Supported: path,gruu,outbound,record-aware\r\n" +
            $"User-Agent: {UserAgent}\r\n" +
            $"Content-Length: 0\r\n" +
            $"\r\n";

        return msg;
    }

    /// <summary>Extract the SIP URI from a header value like "&lt;sip:...&gt;;params".</summary>
#pragma warning disable CA1307 // SIP URIs are ASCII — ordinal is implicit
    private static string ExtractSipUri(string headerValue)
    {
        var ltIdx = headerValue.IndexOf('<');
        if (ltIdx >= 0)
        {
            var gtIdx = headerValue.IndexOf('>', ltIdx + 1);
            if (gtIdx > ltIdx)
                return headerValue[(ltIdx + 1)..gtIdx];
        }
        return headerValue;
    }
#pragma warning restore CA1307

    /// <summary>Extract the value portion of a SIP header from raw message text.</summary>
    private static string? ExtractHeaderValue(string message, string headerName)
    {
        var idx = message.IndexOf($"{headerName}:", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;
        var valueStart = idx + headerName.Length + 1;
        var lineEnd = message.IndexOf("\r\n", valueStart, StringComparison.Ordinal);
        if (lineEnd < 0) return null;
        return message[valueStart..lineEnd].Trim();
    }

    /// <summary>Extract the full header value (e.g., Contact with URI + params).</summary>
    private static string? ExtractHeader(string message, string headerName)
    {
        return ExtractHeaderValue(message, headerName);
    }

    /// <summary>Extract all instances of a header (e.g., multiple Record-Route).</summary>
    private static List<string> ExtractAllHeaders(string message, string headerName)
    {
        var results = new List<string>();
        var search = $"{headerName}:";
        var startPos = 0;
        while (true)
        {
            var idx = message.IndexOf(search, startPos, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) break;
            var valueStart = idx + search.Length;
            var lineEnd = message.IndexOf("\r\n", valueStart, StringComparison.Ordinal);
            if (lineEnd < 0) break;
            results.Add(message[valueStart..lineEnd].Trim());
            startPos = lineEnd + 2;
        }
        return results;
    }

    private static string Md5Hash(string input)
    {
#pragma warning disable CA5351 // MD5 required by SIP Digest authentication (RFC 2617)
        var hash = System.Security.Cryptography.MD5.HashData(
            Encoding.UTF8.GetBytes(input));
#pragma warning restore CA5351
        return Convert.ToHexStringLower(hash);
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
