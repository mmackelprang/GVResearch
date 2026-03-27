using System.Text;
using System.Text.Json;
using GvResearch.Shared.Exceptions;
using GvResearch.Shared.Models;
using GvResearch.Shared.Protocol;
using GvResearch.Shared.RateLimiting;

namespace GvResearch.Shared.Services;

public sealed class GvSmsClient : IGvSmsClient
{
    private const string Endpoint = "api2thread/sendsms";
    private readonly HttpClient _httpClient;
    private readonly GvRateLimiter _rateLimiter;

    public GvSmsClient(HttpClient httpClient, GvRateLimiter rateLimiter)
    {
        _httpClient = httpClient;
        _rateLimiter = rateLimiter;
    }

    public async Task<GvSmsResult> SendAsync(string toNumber, string message, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        if (!await _rateLimiter.TryAcquireAsync(Endpoint, ct).ConfigureAwait(false))
            throw new GvRateLimitException(Endpoint);

        var body = GvRequestBuilder.BuildSendSmsRequest(toNumber, message);
        using var content = new StringContent(body, Encoding.UTF8, "application/json+protobuf");
        var response = await _httpClient
            .PostAsync(new Uri($"voice/v1/voiceclient/{Endpoint}?alt=protojson", UriKind.Relative), content, ct)
            .ConfigureAwait(false);

        if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
            throw new GvAuthException($"Authentication failed: HTTP {(int)response.StatusCode}", (int)response.StatusCode);

        if (!response.IsSuccessStatusCode)
            throw new GvApiException($"Send SMS failed: HTTP {(int)response.StatusCode}", (int)response.StatusCode);

        var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        return GvProtobufJsonParser.ParseSendSms(JsonDocument.Parse(json).RootElement);
    }
}
