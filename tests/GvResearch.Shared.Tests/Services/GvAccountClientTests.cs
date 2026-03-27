using System.Net;
using FluentAssertions;
using GvResearch.Shared.Exceptions;
using GvResearch.Shared.RateLimiting;
using GvResearch.Shared.Services;

namespace GvResearch.Shared.Tests.Services;

public sealed class GvAccountClientTests : IDisposable
{
    private readonly GvRateLimiter _rateLimiter = new(perMinuteLimit: 100, perDayLimit: 1000);

    [Fact]
    public async Task GetAsync_ReturnsAccount()
    {
        var responseJson = """["+19196706660",null,[],null,null,[]]""";
        using var httpClient = CreateHttpClient(HttpStatusCode.OK, responseJson);
        var sut = new GvAccountClient(httpClient, _rateLimiter);

        var account = await sut.GetAsync();

        account.PhoneNumbers.Should().ContainSingle();
        account.PhoneNumbers[0].Number.Should().Be("+19196706660");
    }

    [Fact]
    public async Task GetAsync_WhenRateLimited_Throws()
    {
        using var limiter = new GvRateLimiter(perMinuteLimit: 1, perDayLimit: 100);
        await limiter.TryAcquireAsync("account/get");
        using var httpClient = CreateHttpClient(HttpStatusCode.OK, "[]");
        var sut = new GvAccountClient(httpClient, limiter);

        var act = () => sut.GetAsync();

        await act.Should().ThrowAsync<GvRateLimitException>();
    }

    [Fact]
    public async Task GetAsync_WhenUnauthorized_ThrowsAuthException()
    {
        using var httpClient = CreateHttpClient(HttpStatusCode.Unauthorized, "");
        var sut = new GvAccountClient(httpClient, _rateLimiter);

        var act = () => sut.GetAsync();

        await act.Should().ThrowAsync<GvAuthException>();
    }

    private static HttpClient CreateHttpClient(HttpStatusCode code, string body) =>
        new(new FakeHttpMessageHandler(code, body)) { BaseAddress = new Uri("https://clients6.google.com") };

    public void Dispose() => _rateLimiter.Dispose();
}
