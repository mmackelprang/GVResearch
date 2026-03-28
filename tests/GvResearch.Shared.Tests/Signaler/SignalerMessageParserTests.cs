using FluentAssertions;
using GvResearch.Shared.Signaler;

namespace GvResearch.Shared.Tests.Signaler;

public sealed class SignalerMessageParserTests
{
    [Fact]
    public void Parse_SdpOfferEvent_ReturnsIncomingSdpOfferEvent()
    {
        var raw = """[[1,[["sdp-offer","call-123","v=0\r\no=xavier 123 1 IN IP4 74.125.39.157\r\n"]]]]""";
        var events = SignalerMessageParser.Parse(raw);
        events.Should().ContainSingle();
        var offer = events[0].Should().BeOfType<IncomingSdpOfferEvent>().Subject;
        offer.CallId.Should().Be("call-123");
        offer.Sdp.Should().Contain("xavier");
    }

    [Fact]
    public void Parse_SdpAnswerEvent_ReturnsSdpAnswerEvent()
    {
        var raw = """[[2,[["sdp-answer","call-456","v=0\r\no=- 123 2 IN IP4 127.0.0.1\r\n"]]]]""";
        var events = SignalerMessageParser.Parse(raw);
        events.Should().ContainSingle();
        events[0].Should().BeOfType<SdpAnswerEvent>()
            .Which.CallId.Should().Be("call-456");
    }

    [Fact]
    public void Parse_HangupEvent_ReturnsCallHangupEvent()
    {
        var raw = """[[3,[["hangup","call-789"]]]]""";
        var events = SignalerMessageParser.Parse(raw);
        events.Should().ContainSingle();
        events[0].Should().BeOfType<CallHangupEvent>()
            .Which.CallId.Should().Be("call-789");
    }

    [Fact]
    public void Parse_RingingEvent_ReturnsCallRingingEvent()
    {
        var raw = """[[4,[["ringing","call-101"]]]]""";
        var events = SignalerMessageParser.Parse(raw);
        events.Should().ContainSingle();
        events[0].Should().BeOfType<CallRingingEvent>()
            .Which.CallId.Should().Be("call-101");
    }

    [Fact]
    public void Parse_UnknownEvent_ReturnsUnknownEvent()
    {
        var raw = """[[5,[["noop"]]]]""";
        var events = SignalerMessageParser.Parse(raw);
        events.Should().ContainSingle();
        events[0].Should().BeOfType<UnknownEvent>();
    }

    [Fact]
    public void Parse_MultipleEvents_ReturnsAll()
    {
        var raw = """[[1,[["ringing","c1"]]],[2,[["sdp-offer","c1","sdp-data"]]]]""";
        var events = SignalerMessageParser.Parse(raw);
        events.Should().HaveCount(2);
        events[0].Should().BeOfType<CallRingingEvent>();
        events[1].Should().BeOfType<IncomingSdpOfferEvent>();
    }

    [Fact]
    public void Parse_EmptyResponse_ReturnsEmpty()
    {
        SignalerMessageParser.Parse("").Should().BeEmpty();
    }

    [Fact]
    public void Parse_NullResponse_ReturnsEmpty()
    {
        SignalerMessageParser.Parse(null!).Should().BeEmpty();
    }
}
