using FluentAssertions;
using GvResearch.Shared.RateLimiting;

namespace GvResearch.Shared.Tests.RateLimiting;

public sealed class GvRateLimiterTests : IDisposable
{
    private readonly GvRateLimiter _limiter;

    public GvRateLimiterTests()
    {
        // Use a small per-minute limit for testing
        _limiter = new GvRateLimiter(perMinuteLimit: 3, perDayLimit: 100);
    }

    [Fact]
    public async Task TryAcquireAsync_WithinLimit_ReturnsTrue()
    {
        var acquired = await _limiter.TryAcquireAsync("initiate");
        acquired.Should().BeTrue();
    }

    [Fact]
    public async Task TryAcquireAsync_ExceedsPerMinuteLimit_ReturnsFalse()
    {
        const string endpoint = "initiate";

        // Exhaust the 3-per-minute limit
        for (var i = 0; i < 3; i++)
        {
            var ok = await _limiter.TryAcquireAsync(endpoint);
            ok.Should().BeTrue($"call {i + 1} should succeed");
        }

        // This one should be rejected
        var rejected = await _limiter.TryAcquireAsync(endpoint);
        rejected.Should().BeFalse();
    }

    [Fact]
    public async Task TryAcquireAsync_DifferentEndpoints_TrackSeparately()
    {
        const string endpoint1 = "initiate";
        const string endpoint2 = "hangup";

        // Exhaust limit for endpoint1
        for (var i = 0; i < 3; i++)
        {
            await _limiter.TryAcquireAsync(endpoint1);
        }

        // endpoint2 should still be available
        var canCallEndpoint2 = await _limiter.TryAcquireAsync(endpoint2);
        canCallEndpoint2.Should().BeTrue();
    }

    [Fact]
    public async Task TryAcquireAsync_ExceedsPerDayLimit_ReturnsFalse()
    {
        // perMinuteLimit must be higher than perDayLimit so minute-window does not block first
        using var dayLimiter = new GvRateLimiter(perMinuteLimit: 100, perDayLimit: 2);
        const string endpoint = "status";

        var first = await dayLimiter.TryAcquireAsync(endpoint);
        first.Should().BeTrue();
        var second = await dayLimiter.TryAcquireAsync(endpoint);
        second.Should().BeTrue();

        var rejected = await dayLimiter.TryAcquireAsync(endpoint);
        rejected.Should().BeFalse();
    }

    public void Dispose() => _limiter.Dispose();
}
