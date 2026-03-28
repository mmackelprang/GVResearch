namespace GvResearch.Shared.Signaler;

public sealed class SignalerEventArgs(SignalerEvent signalerEvent) : EventArgs
{
    public SignalerEvent Event { get; } = signalerEvent;
}

public sealed class SignalerErrorEventArgs(Exception exception) : EventArgs
{
    public Exception Exception { get; } = exception;
}

public interface IGvSignalerClient : IAsyncDisposable
{
    event EventHandler<SignalerEventArgs>? EventReceived;
    event EventHandler<SignalerErrorEventArgs>? ErrorOccurred;

    Task ConnectAsync(CancellationToken ct = default);
    Task DisconnectAsync(CancellationToken ct = default);
    bool IsConnected { get; }

    Task SendSdpOfferAsync(string callId, string sdp, CancellationToken ct = default);
    Task SendSdpAnswerAsync(string callId, string sdp, CancellationToken ct = default);
    Task SendHangupAsync(string callId, CancellationToken ct = default);
}
