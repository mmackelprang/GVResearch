using System.Collections.Concurrent;
using GvResearch.Shared.Models;
using GvResearch.Shared.Signaler;
using GvResearch.Shared.Transport;
using SIPSorcery.Net;

namespace GvResearch.Sip.Transport;

public sealed class WebRtcCallTransport : ICallTransport
{
    private readonly IGvSignalerClient _signaler;
    private readonly ConcurrentDictionary<string, WebRtcCallSession> _activeCalls = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _pendingAnswers = new();

    public event EventHandler<IncomingCallEventArgs>? IncomingCallReceived;

    public WebRtcCallTransport(IGvSignalerClient signaler)
    {
        ArgumentNullException.ThrowIfNull(signaler);
        _signaler = signaler;
        _signaler.EventReceived += OnSignalerEvent;
    }

    public async Task<TransportCallResult> InitiateAsync(string toNumber, CancellationToken ct = default)
    {
        var callId = $"out-{Guid.NewGuid():N}";
        var session = new WebRtcCallSession(callId);
        _activeCalls[callId] = session;

        try
        {
            var offer = session.PeerConnection.createOffer();
            await session.PeerConnection.setLocalDescription(offer).ConfigureAwait(false);
            var sdp = offer.sdp;

            var answerTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingAnswers[callId] = answerTcs;

            await _signaler.SendSdpOfferAsync(callId, sdp, ct).ConfigureAwait(false);

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

    public async Task HangupAsync(string callId, CancellationToken ct = default)
    {
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
            _ = HandleRenegotiationAsync(existingSession, offer);
            return;
        }

        _ = HandleNewIncomingCallAsync(offer);
    }

    private async Task HandleNewIncomingCallAsync(IncomingSdpOfferEvent offer)
    {
        var session = new WebRtcCallSession(offer.CallId);
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

    private void HandleSdpAnswer(SdpAnswerEvent answer)
    {
        if (_pendingAnswers.TryGetValue(answer.CallId, out var tcs))
        {
            tcs.TrySetResult(answer.Sdp);
        }
    }

    private void HandleRemoteHangup(CallHangupEvent hangup)
    {
        if (_activeCalls.TryRemove(hangup.CallId, out var session))
        {
            session.UpdateStatus(CallStatusType.Completed);
            session.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _signaler.EventReceived -= OnSignalerEvent;

        foreach (var session in _activeCalls.Values)
        {
            session.Dispose();
        }

        _activeCalls.Clear();
        await Task.CompletedTask.ConfigureAwait(false);
    }
}
