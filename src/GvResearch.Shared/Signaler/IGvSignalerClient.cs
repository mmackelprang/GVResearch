namespace GvResearch.Shared.Signaler;

public interface IGvSignalerClient : IAsyncDisposable
{
    event Action<SignalerEvent>? EventReceived;
    event Action<Exception>? ErrorOccurred;

    Task ConnectAsync(CancellationToken ct = default);
    Task DisconnectAsync(CancellationToken ct = default);
    bool IsConnected { get; }

    Task SendSdpOfferAsync(string callId, string sdp, CancellationToken ct = default);
    Task SendSdpAnswerAsync(string callId, string sdp, CancellationToken ct = default);
    Task SendHangupAsync(string callId, CancellationToken ct = default);
}
