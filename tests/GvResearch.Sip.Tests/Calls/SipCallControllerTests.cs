using FluentAssertions;
using GvResearch.Shared.Models;
using GvResearch.Shared.Services;
using GvResearch.Sip.Calls;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace GvResearch.Sip.Tests.Calls;

public sealed class SipCallControllerTests : IDisposable
{
    private readonly IGvCallService _callService = Substitute.For<IGvCallService>();
    private readonly SipCallController _controller;

    public SipCallControllerTests()
    {
        _controller = new SipCallController(_callService, NullLogger<SipCallController>.Instance);
    }

    public void Dispose()
    {
        _controller.Dispose();
        _callService.Dispose();
    }

    [Fact]
    public async Task HandleOutboundCall_InitiatesGvCall()
    {
        // Arrange
        const string sipCallId = "call-abc-123";
        const string fromNumber = "+16505551234";
        const string destination = "+14155559876";
        const string gvCallId = "gv-call-xyz";

        _callService
            .InitiateCallAsync(fromNumber, destination, Arg.Any<CancellationToken>())
            .Returns(GvCallResult.Ok(gvCallId));

        // Act
        var session = await _controller.CreateOutboundCallAsync(
            sipCallId, fromNumber, destination);

        // Assert
        session.Should().NotBeNull();
        session!.CallId.Should().Be(sipCallId);
        session.GvCallId.Should().Be(gvCallId);
        session.State.Should().Be(CallState.Ringing);
        session.DestinationNumber.Should().Be(destination);

        await _callService.Received(1)
            .InitiateCallAsync(fromNumber, destination, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleOutboundCall_GvFailure_ReturnsNull()
    {
        // Arrange
        const string sipCallId = "call-fail-456";
        const string fromNumber = "+16505551234";
        const string destination = "+14155559876";

        _callService
            .InitiateCallAsync(fromNumber, destination, Arg.Any<CancellationToken>())
            .Returns(GvCallResult.Fail("GV service unavailable"));

        // Act
        var session = await _controller.CreateOutboundCallAsync(
            sipCallId, fromNumber, destination);

        // Assert
        session.Should().BeNull();

        // Verify the controller did not leave a dangling session.
        _controller.TryGetSession(sipCallId).Should().BeNull();
    }

    [Fact]
    public async Task HangupAsync_CallsGvHangupAndRemovesSession()
    {
        // Arrange
        const string sipCallId = "call-hangup-789";
        const string gvCallId = "gv-hangup-xyz";

        _callService
            .InitiateCallAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(GvCallResult.Ok(gvCallId));

        _callService
            .HangupAsync(gvCallId, Arg.Any<CancellationToken>())
            .Returns(GvCallResult.Ok(gvCallId));

        await _controller.CreateOutboundCallAsync(sipCallId, "+1111", "+2222");

        // Act
        await _controller.HangupAsync(sipCallId);

        // Assert
        _controller.TryGetSession(sipCallId).Should().BeNull();

        await _callService.Received(1)
            .HangupAsync(gvCallId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HangupAsync_UnknownCallId_DoesNotThrow()
    {
        // Act
        var act = () => _controller.HangupAsync("unknown-call-id");

        // Assert
        await act.Should().NotThrowAsync();
    }
}
