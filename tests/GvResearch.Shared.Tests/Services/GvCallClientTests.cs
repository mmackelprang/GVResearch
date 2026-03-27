using FluentAssertions;
using GvResearch.Shared.Exceptions;
using GvResearch.Shared.Models;
using GvResearch.Shared.RateLimiting;
using GvResearch.Shared.Services;
using GvResearch.Shared.Transport;
using NSubstitute;

namespace GvResearch.Shared.Tests.Services;

public sealed class GvCallClientTests : IDisposable
{
    private readonly ICallTransport _transport = Substitute.For<ICallTransport>();
    private readonly GvRateLimiter _rateLimiter = new(perMinuteLimit: 100, perDayLimit: 1000);

    [Fact]
    public async Task InitiateAsync_DelegatesToTransport()
    {
        _transport.InitiateAsync("+15551234567", Arg.Any<CancellationToken>())
            .Returns(new TransportCallResult("call-123", true, null));
        var sut = new GvCallClient(_transport, _rateLimiter);

        var result = await sut.InitiateAsync("+15551234567");

        result.Success.Should().BeTrue();
        result.CallId.Should().Be("call-123");
    }

    [Fact]
    public async Task InitiateAsync_WhenRateLimited_Throws()
    {
        using var limiter = new GvRateLimiter(perMinuteLimit: 1, perDayLimit: 100);
        await limiter.TryAcquireAsync("calls/initiate");
        var sut = new GvCallClient(_transport, limiter);

        var act = () => sut.InitiateAsync("+15551234567");

        await act.Should().ThrowAsync<GvRateLimitException>();
    }

    [Fact]
    public async Task HangupAsync_DelegatesToTransport()
    {
        var sut = new GvCallClient(_transport, _rateLimiter);

        await sut.HangupAsync("call-123");

        await _transport.Received(1).HangupAsync("call-123", Arg.Any<CancellationToken>());
    }

    public void Dispose() => _rateLimiter.Dispose();
}
