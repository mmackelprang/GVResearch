using System.Collections.Concurrent;
using GvResearch.Shared.Models;
using GvResearch.Shared.Signaler;
using GvResearch.Shared.Transport;
using Microsoft.Extensions.Logging;
using SIPSorcery.Net;

namespace GvResearch.Sip.Transport;

public sealed class WebRtcCallTransport : ICallTransport
{
    private static readonly Action<ILogger, string, string, Exception?> LogInitiating =
        LoggerMessage.Define<string, string>(LogLevel.Information, new EventId(1, "CallInitiating"),
            "Initiating outgoing call {CallId} to {ToNumber}");

    private static readonly Action<ILogger, string, Exception?> LogAnswerReceived =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(2, "CallAnswerReceived"),
            "Outgoing call {CallId} SDP answer received");

    private static readonly Action<ILogger, string, Exception?> LogIncoming =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(3, "CallIncoming"),
            "Incoming call {CallId}, sending SDP answer");

    private static readonly Action<ILogger, string, Exception?> LogRenegotiating =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(4, "CallRenegotiating"),
            "Renegotiating call {CallId}");

    private static readonly Action<ILogger, string, Exception?> LogHangingUp =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(5, "CallHangingUp"),
            "Hanging up call {CallId}");

    private static readonly Action<ILogger, string, Exception?> LogRemoteHangup =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(6, "CallRemoteHangup"),
            "Remote hangup for call {CallId}");

    private static readonly Action<ILogger, Exception?> LogEventHandlerError =
        LoggerMessage.Define(LogLevel.Warning, new EventId(7, "EventHandlerError"),
            "Unhandled exception in signaler event handler.");

    private readonly IGvSignalerClient _signaler;
    private readonly ILogger<WebRtcCallTransport> _logger;
    private readonly ConcurrentDictionary<string, WebRtcCallSession> _activeCalls = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _pendingAnswers = new();

    public event EventHandler<IncomingCallEventArgs>? IncomingCallReceived;
    public event EventHandler<AudioDataEventArgs>? AudioReceived;

    public WebRtcCallTransport(IGvSignalerClient signaler, ILogger<WebRtcCallTransport> logger)
    {
        ArgumentNullException.ThrowIfNull(signaler);
        ArgumentNullException.ThrowIfNull(logger);
        _signaler = signaler;
        _logger = logger;
        _signaler.EventReceived += OnSignalerEvent;
    }

    public async Task<TransportCallResult> InitiateAsync(string toNumber, CancellationToken ct = default)
    {
        var callId = $"out-{Guid.NewGuid():N}";
        LogInitiating(_logger, callId, toNumber, null);
        var session = new WebRtcCallSession(callId);
        WireSessionAudio(session);
        _activeCalls[callId] = session;

        try
        {
            var offer = session.PeerConnection.createOffer();
            await session.PeerConnection.setLocalDescription(offer).ConfigureAwait(false);
            var sdp = offer.sdp;

#pragma warning disable CA1848, CA1873 // Debug logging for UAT
            _logger.LogInformation("SDP offer created ({Length} chars):\n{Sdp}",
                sdp.Length, sdp[..Math.Min(500, sdp.Length)]);
#pragma warning restore CA1848, CA1873

            var answerTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingAnswers[callId] = answerTcs;

            await _signaler.SendSdpOfferAsync(callId, sdp, ct).ConfigureAwait(false);
#pragma warning disable CA1848
            _logger.LogInformation("SDP offer sent, waiting for answer (30s timeout)...");
#pragma warning restore CA1848

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));

            string answerSdp;
            try
            {
                answerSdp = await answerTcs.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
            }
            finally
            {
                _pendingAnswers.TryRemove(callId, out _);
            }

            LogAnswerReceived(_logger, callId, null);

            var answer = new RTCSessionDescriptionInit
            {
                type = RTCSdpType.answer,
                sdp = answerSdp
            };
            var setResult = session.PeerConnection.setRemoteDescription(answer);
            if (setResult != SetDescriptionResultEnum.OK)
            {
                session.Dispose();
                _activeCalls.TryRemove(callId, out _);
                return new TransportCallResult(callId, false, $"Failed to set remote SDP: {setResult}");
            }

            session.UpdateStatus(CallStatusType.Ringing);
            return new TransportCallResult(callId, true, null);
        }
#pragma warning disable CA1031 // Catch-all is intentional: return failure result rather than propagating
        catch (Exception ex)
#pragma warning restore CA1031
        {
            session.Dispose();
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

    public void SendAudio(string callId, ReadOnlyMemory<byte> pcmData, int sampleRate)
    {
        if (_activeCalls.TryGetValue(callId, out var session))
        {
            session.SendPcmAudio(pcmData.ToArray(), sampleRate);
        }
    }

    public async Task HangupAsync(string callId, CancellationToken ct = default)
    {
        LogHangingUp(_logger, callId, null);
        await _signaler.SendHangupAsync(callId, ct).ConfigureAwait(false);

        if (_activeCalls.TryRemove(callId, out var session))
        {
            session.Dispose();
        }
    }

    private void OnSignalerEvent(object? sender, SignalerEventArgs args)
    {
        switch (args.Event)
        {
            case IncomingSdpOfferEvent offer:
                HandleIncomingSdpOffer(offer);
                break;
            case SdpAnswerEvent answer:
                HandleSdpAnswer(answer);
                break;
            case CallHangupEvent hangup:
                HandleRemoteHangup(hangup);
                break;
        }
    }

    private void HandleIncomingSdpOffer(IncomingSdpOfferEvent offer)
    {
        if (_activeCalls.TryGetValue(offer.CallId, out var existingSession))
        {
            _ = SafeFireAndForgetAsync(HandleRenegotiationAsync(existingSession, offer));
            return;
        }

        _ = SafeFireAndForgetAsync(HandleNewIncomingCallAsync(offer));
    }

    private async Task SafeFireAndForgetAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Catch-all is intentional: unhandled errors from signaler event handlers must not crash the poll loop
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogEventHandlerError(_logger, ex);
        }
    }

    private async Task HandleNewIncomingCallAsync(IncomingSdpOfferEvent offer)
    {
        LogIncoming(_logger, offer.CallId, null);
        var session = new WebRtcCallSession(offer.CallId);
        WireSessionAudio(session);
        _activeCalls[offer.CallId] = session;

        try
        {
            var remoteDesc = new RTCSessionDescriptionInit
            {
                type = RTCSdpType.offer,
                sdp = offer.Sdp
            };
            session.PeerConnection.setRemoteDescription(remoteDesc);

            var answer = session.PeerConnection.createAnswer();
            await session.PeerConnection.setLocalDescription(answer).ConfigureAwait(false);

            await _signaler.SendSdpAnswerAsync(offer.CallId, answer.sdp).ConfigureAwait(false);

            session.UpdateStatus(CallStatusType.Ringing);

            IncomingCallReceived?.Invoke(this, new IncomingCallEventArgs(
                new IncomingCallInfo(offer.CallId, "unknown")));
        }
#pragma warning disable CA1031 // Catch-all is intentional: failed incoming-call setup must not crash the signaler event handler
        catch
#pragma warning restore CA1031
        {
            session.Dispose();
            _activeCalls.TryRemove(offer.CallId, out _);
        }
    }

    private async Task HandleRenegotiationAsync(WebRtcCallSession session, IncomingSdpOfferEvent offer)
    {
        LogRenegotiating(_logger, offer.CallId, null);
        try
        {
            var remoteDesc = new RTCSessionDescriptionInit
            {
                type = RTCSdpType.offer,
                sdp = offer.Sdp
            };
            session.PeerConnection.setRemoteDescription(remoteDesc);

            var answer = session.PeerConnection.createAnswer();
            await session.PeerConnection.setLocalDescription(answer).ConfigureAwait(false);

            await _signaler.SendSdpAnswerAsync(offer.CallId, answer.sdp).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Catch-all is intentional: renegotiation failure is non-fatal
        catch
#pragma warning restore CA1031
        {
            // Renegotiation failure is non-fatal
        }
    }

    private void WireSessionAudio(WebRtcCallSession session)
    {
        session.PcmAudioReceived += (pcmData, sampleRate) =>
        {
            AudioReceived?.Invoke(this, new AudioDataEventArgs(session.CallId, pcmData, sampleRate));
        };
    }

    private void HandleSdpAnswer(SdpAnswerEvent answer)
    {
        if (_pendingAnswers.TryGetValue(answer.CallId, out var tcs))
        {
            tcs.TrySetResult(answer.Sdp);
        }
    }

    private void HandleRemoteHangup(CallHangupEvent hangup)
    {
        LogRemoteHangup(_logger, hangup.CallId, null);
        if (_activeCalls.TryRemove(hangup.CallId, out var session))
        {
            session.UpdateStatus(CallStatusType.Completed);
            session.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _signaler.EventReceived -= OnSignalerEvent;

        // Cancel any pending outgoing call SDP answer waits
        foreach (var tcs in _pendingAnswers.Values)
        {
            tcs.TrySetCanceled();
        }
        _pendingAnswers.Clear();

        foreach (var session in _activeCalls.Values)
        {
            session.Dispose();
        }

        _activeCalls.Clear();
        await Task.CompletedTask.ConfigureAwait(false);
    }
}
