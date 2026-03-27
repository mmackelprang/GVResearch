namespace GvResearch.Shared;

public enum CallTransportType { Sip, WebRtc, Callback }

public sealed class GvClientOptions
{
    public string CookiePath { get; set; } = "cookies.enc";
    public string KeyPath { get; set; } = "key.bin";
    public CallTransportType CallTransport { get; set; } = CallTransportType.Sip;
}
