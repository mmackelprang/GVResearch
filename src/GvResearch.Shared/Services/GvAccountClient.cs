using System.Text;
using System.Text.Json;
using GvResearch.Shared.Exceptions;
using GvResearch.Shared.Models;
using GvResearch.Shared.Protocol;
using GvResearch.Shared.RateLimiting;

namespace GvResearch.Shared.Services;

public sealed class GvAccountClient : IGvAccountClient
{
    private const string Endpoint = "account/get";
    private readonly HttpClient _httpClient;
    private readonly GvRateLimiter _rateLimiter;

    public GvAccountClient(HttpClient httpClient, GvRateLimiter rateLimiter)
    {
        _httpClient = httpClient;
        _rateLimiter = rateLimiter;
    }

    public async Task<GvAccount> GetAsync(CancellationToken ct = default)
    {
        if (!await _rateLimiter.TryAcquireAsync(Endpoint, ct).ConfigureAwait(false))
            throw new GvRateLimitException(Endpoint);

        var body = GvRequestBuilder.BuildAccountGetRequest();
        using var content = new StringContent(body, Encoding.UTF8, "application/json+protobuf");
        var response = await _httpClient
            .PostAsync(new Uri($"voice/v1/voiceclient/{Endpoint}?alt=protojson", UriKind.Relative), content, ct)
            .ConfigureAwait(false);

        if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
            throw new GvAuthException($"Authentication failed: HTTP {(int)response.StatusCode}", (int)response.StatusCode);

        if (!response.IsSuccessStatusCode)
            throw new GvApiException($"Account get failed: HTTP {(int)response.StatusCode}", (int)response.StatusCode);

        var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        var root = JsonDocument.Parse(json).RootElement;
        return GvProtobufJsonParser.ParseAccount(root);
    }
}
