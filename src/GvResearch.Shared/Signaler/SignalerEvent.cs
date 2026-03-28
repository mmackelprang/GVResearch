namespace GvResearch.Shared.Signaler;

public abstract record SignalerEvent(DateTimeOffset Timestamp);

public sealed record IncomingSdpOfferEvent(
    string CallId, string Sdp, DateTimeOffset Timestamp) : SignalerEvent(Timestamp);

public sealed record SdpAnswerEvent(
    string CallId, string Sdp, DateTimeOffset Timestamp) : SignalerEvent(Timestamp);

public sealed record CallHangupEvent(
    string CallId, DateTimeOffset Timestamp) : SignalerEvent(Timestamp);

public sealed record CallRingingEvent(
    string CallId, DateTimeOffset Timestamp) : SignalerEvent(Timestamp);

public sealed record UnknownEvent(
    string RawPayload, DateTimeOffset Timestamp) : SignalerEvent(Timestamp);
