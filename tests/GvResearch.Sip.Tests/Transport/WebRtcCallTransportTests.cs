using FluentAssertions;
using GvResearch.Shared.Models;
using GvResearch.Shared.Signaler;
using GvResearch.Shared.Transport;
using NSubstitute;

namespace GvResearch.Sip.Tests.Transport;

public sealed class WebRtcCallTransportTests : IAsyncDisposable
{
    private readonly IGvSignalerClient _signaler = Substitute.For<IGvSignalerClient>();

    [Fact]
    public async Task HangupAsync_SendsHangupViaSignaler()
    {
        await using var sut = new GvResearch.Sip.Transport.WebRtcCallTransport(_signaler);

        await sut.HangupAsync("call-123");

        await _signaler.Received(1).SendHangupAsync("call-123", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetStatusAsync_ReturnsUnknownForNonexistentCall()
    {
        await using var sut = new GvResearch.Sip.Transport.WebRtcCallTransport(_signaler);

        var status = await sut.GetStatusAsync("nonexistent");

        status.Status.Should().Be(CallStatusType.Unknown);
    }

    [Fact]
    public async Task IncomingSdpOffer_FiresIncomingCallReceived()
    {
        await using var sut = new GvResearch.Sip.Transport.WebRtcCallTransport(_signaler);

        IncomingCallInfo? received = null;
        sut.IncomingCallReceived += (_, args) => received = args.CallInfo;

        // Simulate incoming SDP offer from signaler
        _signaler.EventReceived += Raise.Event<EventHandler<SignalerEventArgs>>(
            _signaler,
            new SignalerEventArgs(
                new IncomingSdpOfferEvent("incoming-1",
                    "v=0\r\no=xavier 123 1 IN IP4 74.125.39.157\r\ns=SIP Call\r\nc=IN IP4 74.125.39.157\r\nt=0 0\r\nm=audio 26500 RTP/AVP 111\r\na=rtpmap:111 opus/48000/2\r\n",
                    DateTimeOffset.UtcNow)));

        await Task.Delay(500);

        received.Should().NotBeNull();
        received!.CallId.Should().Be("incoming-1");
    }

    public async ValueTask DisposeAsync() => await _signaler.DisposeAsync();
}
