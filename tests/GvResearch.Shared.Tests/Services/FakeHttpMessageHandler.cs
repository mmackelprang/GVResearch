using System.Net;

namespace GvResearch.Shared.Tests.Services;

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
