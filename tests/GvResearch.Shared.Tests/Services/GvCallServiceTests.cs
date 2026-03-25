using System.Net;
using FluentAssertions;
using GvResearch.Shared.RateLimiting;
using GvResearch.Shared.Services;

namespace GvResearch.Shared.Tests.Services;

public sealed class GvCallServiceTests : IDisposable
{
    private readonly GvRateLimiter _rateLimiter;

    public GvCallServiceTests()
    {
        _rateLimiter = new GvRateLimiter(perMinuteLimit: 100, perDayLimit: 1000);
    }

    private static HttpClient CreateHttpClient(HttpStatusCode responseCode, string responseBody = "{}")
    {
        var handler = new FakeHttpMessageHandler(responseCode, responseBody);
        return new HttpClient(handler) { BaseAddress = new Uri("https://voice.google.com") };
    }

    [Fact]
    public async Task InitiateCallAsync_WhenHttpSucceeds_ReturnsSuccessResult()
    {
        using var httpClient = CreateHttpClient(HttpStatusCode.OK, "{}");
        using var svc = new GvCallService(httpClient, _rateLimiter);

        var result = await svc.InitiateCallAsync("+15551234567", "+15559876543");

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task InitiateCallAsync_WhenRateLimitExceeded_ReturnsFailureResult()
    {
        // A rate limiter with limit=1; exhaust it before calling the service.
        using var exhaustedLimiter = new GvRateLimiter(perMinuteLimit: 1, perDayLimit: 100);
        await exhaustedLimiter.TryAcquireAsync("initiate"); // consume the one permit
        using var httpClient = CreateHttpClient(HttpStatusCode.OK);
        using var svc = new GvCallService(httpClient, exhaustedLimiter);

        var result = await svc.InitiateCallAsync("+15551234567", "+15559876543");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("rate limit");
    }

    [Fact]
    public async Task InitiateCallAsync_WhenHttpFails_ReturnsFailureResult()
    {
        using var httpClient = CreateHttpClient(HttpStatusCode.InternalServerError);
        using var svc = new GvCallService(httpClient, _rateLimiter);

        var result = await svc.InitiateCallAsync("+15551234567", "+15559876543");

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task ListenForEventsAsync_YieldsNoEvents()
    {
        using var httpClient = CreateHttpClient(HttpStatusCode.OK);
        using var svc = new GvCallService(httpClient, _rateLimiter);

        var events = new List<GvResearch.Shared.Models.GvCallEvent>();
        await foreach (var evt in svc.ListenForEventsAsync("call-id-123"))
        {
            events.Add(evt);
        }

        events.Should().BeEmpty();
    }

    public void Dispose() => _rateLimiter.Dispose();
}

/// <summary>Minimal fake HTTP handler for testing.</summary>
internal sealed class FakeHttpMessageHandler(HttpStatusCode statusCode, string body = "{}") : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body),
        };
        return Task.FromResult(response);
    }
}
