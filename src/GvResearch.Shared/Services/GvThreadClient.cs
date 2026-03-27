using System.Text;
using System.Text.Json;
using GvResearch.Shared.Exceptions;
using GvResearch.Shared.Models;
using GvResearch.Shared.Protocol;
using GvResearch.Shared.RateLimiting;

namespace GvResearch.Shared.Services;

public sealed class GvThreadClient : IGvThreadClient
{
    private readonly HttpClient _httpClient;
    private readonly GvRateLimiter _rateLimiter;
    private readonly GvApiConfig _apiConfig;

    public GvThreadClient(HttpClient httpClient, GvRateLimiter rateLimiter, GvApiConfig apiConfig)
    {
        _httpClient = httpClient;
        _rateLimiter = rateLimiter;
        _apiConfig = apiConfig;
    }

    public Task<GvThreadPage> ListAsync(GvThreadListOptions? options = null, CancellationToken ct = default) =>
        PostAndParseAsync("api2thread/list", GvRequestBuilder.BuildThreadListRequest(options),
            GvProtobufJsonParser.ParseThreadList, ct);

    public Task<GvThread> GetAsync(string threadId, CancellationToken ct = default) =>
        PostAndParseAsync("api2thread/get", GvRequestBuilder.BuildThreadGetRequest(threadId),
            GvProtobufJsonParser.ParseThread, ct);

    public Task MarkReadAsync(IEnumerable<string> threadIds, CancellationToken ct = default) =>
        PostAsync("thread/batchupdateattributes",
            GvRequestBuilder.BuildBatchUpdateRequest(threadIds, "read"), ct);

    public Task ArchiveAsync(IEnumerable<string> threadIds, CancellationToken ct = default) =>
        PostAsync("thread/batchupdateattributes",
            GvRequestBuilder.BuildBatchUpdateRequest(threadIds, "archive"), ct);

    public Task MarkSpamAsync(IEnumerable<string> threadIds, CancellationToken ct = default) =>
        PostAsync("thread/batchupdateattributes",
            GvRequestBuilder.BuildBatchUpdateRequest(threadIds, "spam"), ct);

    public Task DeleteAsync(IEnumerable<string> threadIds, CancellationToken ct = default) =>
        PostAsync("thread/batchdelete",
            GvRequestBuilder.BuildBatchDeleteRequest(threadIds), ct);

    public Task MarkAllReadAsync(CancellationToken ct = default) =>
        PostAsync("thread/markallread",
            GvRequestBuilder.BuildMarkAllReadRequest(), ct);

    public Task<GvUnreadCounts> GetUnreadCountsAsync(CancellationToken ct = default) =>
        PostAndParseAsync("threadinginfo/get", GvRequestBuilder.BuildThreadingInfoRequest(),
            GvProtobufJsonParser.ParseUnreadCounts, ct);

    public Task<GvThreadPage> SearchAsync(string query, CancellationToken ct = default) =>
        PostAndParseAsync("api2thread/search", GvRequestBuilder.BuildSearchRequest(query),
            GvProtobufJsonParser.ParseThreadList, ct);

    private async Task<T> PostAndParseAsync<T>(string endpoint, string body,
        Func<JsonElement, T> parser, CancellationToken ct)
    {
        if (!await _rateLimiter.TryAcquireAsync(endpoint, ct).ConfigureAwait(false))
            throw new GvRateLimitException(endpoint);

        using var content = new StringContent(body, Encoding.UTF8, "application/json+protobuf");
        var response = await _httpClient
            .PostAsync(new Uri($"voice/v1/voiceclient/{endpoint}?{_apiConfig.QueryString}", UriKind.Relative), content, ct)
            .ConfigureAwait(false);

        ThrowOnAuthError(response, endpoint);
        if (!response.IsSuccessStatusCode)
            throw new GvApiException($"{endpoint} failed: HTTP {(int)response.StatusCode}", (int)response.StatusCode);

        var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        return parser(doc.RootElement);
    }

    private async Task PostAsync(string endpoint, string body, CancellationToken ct)
    {
        if (!await _rateLimiter.TryAcquireAsync(endpoint, ct).ConfigureAwait(false))
            throw new GvRateLimitException(endpoint);

        using var content = new StringContent(body, Encoding.UTF8, "application/json+protobuf");
        var response = await _httpClient
            .PostAsync(new Uri($"voice/v1/voiceclient/{endpoint}?{_apiConfig.QueryString}", UriKind.Relative), content, ct)
            .ConfigureAwait(false);

        ThrowOnAuthError(response, endpoint);
        if (!response.IsSuccessStatusCode)
            throw new GvApiException($"{endpoint} failed: HTTP {(int)response.StatusCode}", (int)response.StatusCode);
    }

    private static void ThrowOnAuthError(HttpResponseMessage response, string endpoint)
    {
        if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
            throw new GvAuthException($"Authentication failed for {endpoint}: HTTP {(int)response.StatusCode}", (int)response.StatusCode);
    }
}
