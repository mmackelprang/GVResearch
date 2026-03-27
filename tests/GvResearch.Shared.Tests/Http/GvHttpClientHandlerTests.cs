using System.Net;
using FluentAssertions;
using GvResearch.Shared.Auth;
using GvResearch.Shared.Http;
using NSubstitute;

namespace GvResearch.Shared.Tests.Http;

public sealed class GvHttpClientHandlerTests : IDisposable
{
    private readonly IGvAuthService _authService = Substitute.For<IGvAuthService>();
    private readonly FakeInnerHandler _innerHandler = new();
    private readonly GvHttpClientHandler _sut;

    public GvHttpClientHandlerTests()
    {
        _authService.GetValidCookiesAsync(Arg.Any<CancellationToken>())
            .Returns(new GvCookieSet
            {
                Sapisid = "test-sapisid",
                Sid = "test-sid",
                Hsid = "test-hsid",
                Ssid = "test-ssid",
                Apisid = "test-apisid"
            });
        _authService.ComputeSapisidHash("test-sapisid", "https://voice.google.com")
            .Returns("SAPISIDHASH 12345_abc123");

        _sut = new GvHttpClientHandler(_authService, _innerHandler);
    }

    [Fact]
    public async Task SendAsync_InjectsAuthorizationHeader()
    {
        using var client = new HttpClient(_sut) { BaseAddress = new Uri("https://clients6.google.com") };

        await client.GetAsync(new Uri("/test", UriKind.Relative));

        _innerHandler.LastRequest!.Headers.GetValues("Authorization")
            .Should().ContainSingle()
            .Which.Should().Be("SAPISIDHASH 12345_abc123");
    }

    [Fact]
    public async Task SendAsync_InjectsCookieHeader()
    {
        using var client = new HttpClient(_sut) { BaseAddress = new Uri("https://clients6.google.com") };

        await client.GetAsync(new Uri("/test", UriKind.Relative));

        _innerHandler.LastRequest!.Headers.GetValues("Cookie")
            .Should().ContainSingle()
            .Which.Should().Contain("SAPISID=test-sapisid")
            .And.Contain("SID=test-sid");
    }

    [Fact]
    public async Task SendAsync_InjectsOriginAndReferer()
    {
        using var client = new HttpClient(_sut) { BaseAddress = new Uri("https://clients6.google.com") };

        await client.GetAsync(new Uri("/test", UriKind.Relative));

        _innerHandler.LastRequest!.Headers.GetValues("Origin")
            .Should().ContainSingle().Which.Should().Be("https://voice.google.com");
        _innerHandler.LastRequest!.Headers.GetValues("Referer")
            .Should().ContainSingle().Which.Should().Be("https://voice.google.com/");
    }

    [Fact]
    public async Task SendAsync_InjectsXGoogAuthUser()
    {
        using var client = new HttpClient(_sut) { BaseAddress = new Uri("https://clients6.google.com") };

        await client.GetAsync(new Uri("/test", UriKind.Relative));

        _innerHandler.LastRequest!.Headers.GetValues("X-Goog-AuthUser")
            .Should().ContainSingle().Which.Should().Be("0");
    }

    public void Dispose() => _sut.Dispose();
}

internal sealed class FakeInnerHandler : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }
}
