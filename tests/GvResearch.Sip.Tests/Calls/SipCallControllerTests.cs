using FluentAssertions;
using GvResearch.Shared.Models;
using GvResearch.Shared.Services;
using GvResearch.Sip.Calls;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace GvResearch.Sip.Tests.Calls;

public sealed class SipCallControllerTests : IDisposable
{
    private readonly IGvCallClient _callClient = Substitute.For<IGvCallClient>();
    private readonly SipCallController _controller;

    public SipCallControllerTests()
    {
        _controller = new SipCallController(_callClient, NullLogger<SipCallController>.Instance);
    }

    public void Dispose()
    {
        _controller.Dispose();
    }

    [Fact]
    public async Task HandleOutboundCall_InitiatesGvCall()
    {
        // Arrange
        const string sipCallId = "call-abc-123";
        const string destination = "+14155559876";
        const string gvCallId = "gv-call-xyz";

        _callClient
            .InitiateAsync(destination, Arg.Any<CancellationToken>())
            .Returns(GvCallResult.Ok(gvCallId));

        // Act
        var session = await _controller.CreateOutboundCallAsync(sipCallId, destination);

        // Assert
        session.Should().NotBeNull();
        session!.CallId.Should().Be(sipCallId);
        session.GvCallId.Should().Be(gvCallId);
        session.State.Should().Be(CallState.Ringing);
        session.DestinationNumber.Should().Be(destination);

        await _callClient.Received(1)
            .InitiateAsync(destination, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleOutboundCall_GvFailure_ReturnsNull()
    {
        // Arrange
        const string sipCallId = "call-fail-456";
        const string destination = "+14155559876";

        _callClient
            .InitiateAsync(destination, Arg.Any<CancellationToken>())
            .Returns(GvCallResult.Fail("GV service unavailable"));

        // Act
        var session = await _controller.CreateOutboundCallAsync(sipCallId, destination);

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

        _callClient
            .InitiateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(GvCallResult.Ok(gvCallId));

        _callClient
            .HangupAsync(gvCallId, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await _controller.CreateOutboundCallAsync(sipCallId, "+2222");

        // Act
        await _controller.HangupAsync(sipCallId);

        // Assert
        _controller.TryGetSession(sipCallId).Should().BeNull();

        await _callClient.Received(1)
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
