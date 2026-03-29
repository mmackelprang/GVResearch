using System.Text;
using System.Text.Json;
using GvResearch.Shared;
using Microsoft.Extensions.Logging;

namespace GvResearch.Sip.Transport;

/// <summary>
/// Fetches SIP credentials from the GV sipregisterinfo/get endpoint.
/// Returns Bearer token, SIP username, phone number, and expiry.
/// </summary>
public sealed class GvSipCredentialProvider
{
    private static readonly Action<ILogger, Exception?> LogFetching =
        LoggerMessage.Define(LogLevel.Information, new EventId(1, "FetchingSipCreds"),
            "Fetching SIP credentials from sipregisterinfo/get...");

    private static readonly Action<ILogger, int, Exception?> LogFetched =
        LoggerMessage.Define<int>(LogLevel.Information, new EventId(2, "SipCredsFetched"),
            "SIP credentials fetched, expires in {Expiry}s");

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GvApiConfig _apiConfig;
    private readonly ILogger<GvSipCredentialProvider> _logger;

    public GvSipCredentialProvider(
        IHttpClientFactory httpClientFactory,
        GvApiConfig apiConfig,
        ILogger<GvSipCredentialProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _apiConfig = apiConfig;
        _logger = logger;
    }

    public async Task<SipCredentials> GetCredentialsAsync(CancellationToken ct = default)
    {
        LogFetching(_logger, null);

        var client = _httpClientFactory.CreateClient("GvApi");
        var requestBody = "[]"; // sipregisterinfo/get takes empty array

        using var content = new StringContent(requestBody, Encoding.UTF8, "application/json+protobuf");
        var response = await client
            .PostAsync(
                new Uri($"voice/v1/voiceclient/sipregisterinfo/get?{_apiConfig.QueryString}", UriKind.Relative),
                content, ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        // Response format: [["sipToken",expiry],null,null,["authToken","cryptoKey"]]
        // Or possibly more complex — parse defensively
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Extract SIP token (position 0)
        var sipToken = "";
        var expiry = 600;
        if (root.GetArrayLength() > 0 && root[0].ValueKind == JsonValueKind.Array)
        {
            sipToken = root[0][0].GetString() ?? "";
            if (root[0].GetArrayLength() > 1)
                expiry = root[0][1].GetInt32();
        }

        // Extract auth token (position 3)
        var bearerToken = "";
        if (root.GetArrayLength() > 3 && root[3].ValueKind == JsonValueKind.Array)
        {
            bearerToken = root[3][0].GetString() ?? "";
        }

        // Get phone number from account (we'll use the SIP token as username)
        // The SIP URI username is the encoded token
        var sipUsername = Uri.EscapeDataString(sipToken);

        LogFetched(_logger, expiry, null);

        return new SipCredentials(
            SipUsername: sipUsername,
            BearerToken: bearerToken,
            PhoneNumber: "+19196706660", // TODO: get from account/get
            ExpirySeconds: expiry);
    }
}
