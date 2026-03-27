using System.Net;
using FluentAssertions;
using GvResearch.Shared.RateLimiting;
using GvResearch.Shared.Services;

namespace GvResearch.Shared.Tests.Services;

public sealed class GvSmsClientTests : IDisposable
{
    private readonly GvRateLimiter _rateLimiter = new(perMinuteLimit: 100, perDayLimit: 1000);

    [Fact]
    public async Task SendAsync_ReturnsSuccess()
    {
        var json = """[null,"t.+15551234567","msg-hash",1234567890000,[1,2]]""";
        using var httpClient = new HttpClient(new FakeHttpMessageHandler(HttpStatusCode.OK, json))
            { BaseAddress = new Uri("https://clients6.google.com") };
        var sut = new GvSmsClient(httpClient, _rateLimiter);

        var result = await sut.SendAsync("+15551234567", "Hello!");

        result.Success.Should().BeTrue();
        result.ThreadId.Should().Be("t.+15551234567");
    }

    public void Dispose() => _rateLimiter.Dispose();
}
