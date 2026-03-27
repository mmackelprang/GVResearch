using System.Net;
using FluentAssertions;
using GvResearch.Shared.Exceptions;
using GvResearch.Shared.Models;
using GvResearch.Shared.RateLimiting;
using GvResearch.Shared.Services;

namespace GvResearch.Shared.Tests.Services;

public sealed class GvThreadClientTests : IDisposable
{
    private readonly GvRateLimiter _rateLimiter = new(perMinuteLimit: 100, perDayLimit: 1000);

    [Fact]
    public async Task ListAsync_ReturnsThreadPage()
    {
        var json = """[[["t.+15551234567",1,[["msg1",1000,"+1",["+2"],10,1,null,null,null,null,null,"Hi",null,null,0,1]]]]]""";
        using var httpClient = CreateHttpClient(HttpStatusCode.OK, json);
        var sut = new GvThreadClient(httpClient, _rateLimiter);

        var page = await sut.ListAsync();

        page.Threads.Should().ContainSingle();
        page.Threads[0].Id.Should().Be("t.+15551234567");
    }

    [Fact]
    public async Task GetAsync_ReturnsThread()
    {
        var json = """["t.+15551234567",0,[["msg1",1000,"+1",["+2"],10,0,null,null,null,null,null,"Hello",null,null,0,0]]]""";
        using var httpClient = CreateHttpClient(HttpStatusCode.OK, json);
        var sut = new GvThreadClient(httpClient, _rateLimiter);

        var thread = await sut.GetAsync("t.+15551234567");

        thread.Id.Should().Be("t.+15551234567");
        thread.Messages.Should().ContainSingle();
    }

    [Fact]
    public async Task DeleteAsync_WhenUnauthorized_ThrowsAuthException()
    {
        using var httpClient = CreateHttpClient(HttpStatusCode.Forbidden, "");
        var sut = new GvThreadClient(httpClient, _rateLimiter);

        var act = () => sut.DeleteAsync(["t.+1111"]);

        await act.Should().ThrowAsync<GvAuthException>();
    }

    [Fact]
    public async Task GetUnreadCountsAsync_ReturnsCounts()
    {
        var json = """[[[[1,null,5],[4,null,2],[3,null,10],[5,null,0],[6,null,1]]]]""";
        using var httpClient = CreateHttpClient(HttpStatusCode.OK, json);
        var sut = new GvThreadClient(httpClient, _rateLimiter);

        var counts = await sut.GetUnreadCountsAsync();

        counts.Sms.Should().Be(10);
        counts.Voicemail.Should().Be(2);
        counts.Missed.Should().Be(5);
    }

    private static HttpClient CreateHttpClient(HttpStatusCode code, string body) =>
        new(new FakeHttpMessageHandler(code, body)) { BaseAddress = new Uri("https://clients6.google.com") };

    public void Dispose() => _rateLimiter.Dispose();
}
