using System.Net;
using FluentAssertions;
using GvResearch.Shared.Signaler;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace GvResearch.Shared.Tests.Signaler;

public sealed class GvSignalerClientTests : IAsyncDisposable
{
    private readonly GvApiConfig _apiConfig = new() { ApiKey = "test-key" };

    [Fact]
    public async Task ConnectAsync_SetsIsConnected()
    {
        var handler = new FakeSignalerHandler();
        handler.EnqueueResponse(HttpStatusCode.OK, """{"server":"test-server"}""");
        handler.EnqueueResponse(HttpStatusCode.OK, """[[0,["c","sid-123","",[8]]]]""");
        handler.EnqueueLongPollBlock();

        using var httpClient = new HttpClient(handler)
            { BaseAddress = new Uri("https://signaler-pa.clients6.google.com") };
        var factory = CreateFactory(httpClient);
        await using var sut = new GvSignalerClient(factory, _apiConfig, Substitute.For<ILogger<GvSignalerClient>>());

        await sut.ConnectAsync();

        sut.IsConnected.Should().BeTrue();
    }

    [Fact]
    public async Task ConnectAsync_ThenDisconnect_SetsIsConnectedFalse()
    {
        var handler = new FakeSignalerHandler();
        handler.EnqueueResponse(HttpStatusCode.OK, """{"server":"test-server"}""");
        handler.EnqueueResponse(HttpStatusCode.OK, """[[0,["c","sid-456","",[8]]]]""");
        handler.EnqueueLongPollBlock();

        using var httpClient = new HttpClient(handler)
            { BaseAddress = new Uri("https://signaler-pa.clients6.google.com") };
        var factory = CreateFactory(httpClient);
        await using var sut = new GvSignalerClient(factory, _apiConfig, Substitute.For<ILogger<GvSignalerClient>>());

        await sut.ConnectAsync();
        await sut.DisconnectAsync();

        sut.IsConnected.Should().BeFalse();
    }

    [Fact]
    public async Task PollLoop_FiresEventReceived()
    {
        var handler = new FakeSignalerHandler();
        handler.EnqueueResponse(HttpStatusCode.OK, """{"server":"s"}""");
        handler.EnqueueResponse(HttpStatusCode.OK, """[[0,["c","sid-789","",[8]]]]""");
        handler.EnqueueResponse(HttpStatusCode.OK,
            """[[1,[["sdp-offer","call-1","v=0\r\n"]]]]""");
        handler.EnqueueLongPollBlock();

        using var httpClient = new HttpClient(handler)
            { BaseAddress = new Uri("https://signaler-pa.clients6.google.com") };
        var factory = CreateFactory(httpClient);
        await using var sut = new GvSignalerClient(factory, _apiConfig, Substitute.For<ILogger<GvSignalerClient>>());

        SignalerEvent? received = null;
        sut.EventReceived += (_, args) => received = args.Event;

        await sut.ConnectAsync();
        await Task.Delay(200);

        received.Should().NotBeNull();
        received.Should().BeOfType<IncomingSdpOfferEvent>()
            .Which.CallId.Should().Be("call-1");
    }

    [Fact]
    public async Task SendSdpOfferAsync_PostsToChannel()
    {
        var handler = new FakeSignalerHandler();
        handler.EnqueueResponse(HttpStatusCode.OK, """{"server":"s"}""");
        handler.EnqueueResponse(HttpStatusCode.OK, """[[0,["c","sid-send","",[8]]]]""");
        handler.EnqueueLongPollBlock();
        handler.EnqueueResponse(HttpStatusCode.OK, "");

        using var httpClient = new HttpClient(handler)
            { BaseAddress = new Uri("https://signaler-pa.clients6.google.com") };
        var factory = CreateFactory(httpClient);
        await using var sut = new GvSignalerClient(factory, _apiConfig, Substitute.For<ILogger<GvSignalerClient>>());

        await sut.ConnectAsync();
        await sut.SendSdpOfferAsync("call-x", "v=0\r\n");

        handler.SentRequests.Should().Contain(r =>
            r.Method == HttpMethod.Post &&
            r.RequestUri!.ToString().Contains("sid-send"));
    }

    private static IHttpClientFactory CreateFactory(HttpClient client)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("GvSignaler").Returns(client);
        return factory;
    }

    public async ValueTask DisposeAsync() => await Task.CompletedTask;
}

internal sealed class FakeSignalerHandler : HttpMessageHandler
{
    private readonly Queue<(HttpStatusCode Code, string Body)> _responses = new();
    public List<HttpRequestMessage> SentRequests { get; } = [];

    public void EnqueueResponse(HttpStatusCode code, string body) =>
        _responses.Enqueue((code, body));

    public void EnqueueLongPollBlock() { }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        SentRequests.Add(request);

        if (_responses.Count > 0)
        {
            var (code, body) = _responses.Dequeue();
            return new HttpResponseMessage(code) { Content = new StringContent(body) };
        }

        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("[]")
        };
    }
}
