using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace GvResearch.Shared.Signaler;

public sealed class GvSignalerClient : IGvSignalerClient
{
    private static readonly Action<ILogger, string, string, Exception?> LogConnected =
        LoggerMessage.Define<string, string>(LogLevel.Information, new EventId(1, "SignalerConnected"),
            "Signaler connected, gsessionid={GSessionId}, SID={Sid}");

    private static readonly Action<ILogger, Exception?> LogDisconnected =
        LoggerMessage.Define(LogLevel.Information, new EventId(2, "SignalerDisconnected"),
            "Signaler disconnected");

    private static readonly Action<ILogger, string, string, Exception?> LogSending =
        LoggerMessage.Define<string, string>(LogLevel.Debug, new EventId(3, "SignalerSending"),
            "Sending {Type} for call {CallId}");

    private static readonly Action<ILogger, double, Exception?> LogPollError =
        LoggerMessage.Define<double>(LogLevel.Warning, new EventId(4, "SignalerPollError"),
            "Signaler poll error, retrying in {Backoff}s");

    private static readonly Action<ILogger, int, Exception?> LogPollReceived =
        LoggerMessage.Define<int>(LogLevel.Debug, new EventId(5, "SignalerPollReceived"),
            "Signaler received {Count} events");

    private static readonly Action<ILogger, string, Exception?> LogChooseServer =
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(6, "SignalerChooseServer"),
            "Signaler chooseServer response: {Response}");

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GvApiConfig _apiConfig;
    private readonly ILogger<GvSignalerClient> _logger;

    private string? _gsessionId;
    private string? _sessionId;
    private int _aid;
    private int _rid;
    private CancellationTokenSource? _pollCts;
    private Task? _pollTask;

    public event EventHandler<SignalerEventArgs>? EventReceived;
    public event EventHandler<SignalerErrorEventArgs>? ErrorOccurred;
    public bool IsConnected { get; private set; }

    public GvSignalerClient(
        IHttpClientFactory httpClientFactory,
        GvApiConfig apiConfig,
        ILogger<GvSignalerClient> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(apiConfig);
        ArgumentNullException.ThrowIfNull(logger);

        _httpClientFactory = httpClientFactory;
        _apiConfig = apiConfig;
        _logger = logger;
    }

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        var client = CreateClient();

        // 1. Choose server — returns gsessionid
        var chooseRequestBody = "[[null,null,null,[9,5],null,[null,[null,1],[[[\"1\"]]]]],null,null,0,0]";
        using var chooseContent = new StringContent(chooseRequestBody, Encoding.UTF8, "application/json+protobuf");
        var chooseResponse = await client
            .PostAsync(
                new Uri($"punctual/v1/chooseServer?key={Uri.EscapeDataString(_apiConfig.ApiKey)}", UriKind.Relative),
                chooseContent,
                ct)
            .ConfigureAwait(false);
        chooseResponse.EnsureSuccessStatusCode();

        var chooseBody = await chooseResponse.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        LogChooseServer(_logger, chooseBody, null);
        _gsessionId = ExtractGSessionId(chooseBody);

        // 2. Open channel — POST with subscription registrations, get SID
        _rid = (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 100000);
        var openUrl = BuildChannelUrl(rid: _rid);
        Interlocked.Increment(ref _rid);

        // Build subscription body matching what the browser sends
        var subscriptions = BuildSubscriptionBody();
        using var openContent = new StringContent(subscriptions, Encoding.UTF8, "application/x-www-form-urlencoded");

        var openResponse = await client
            .PostAsync(new Uri(openUrl, UriKind.Relative), openContent, ct)
            .ConfigureAwait(false);
        openResponse.EnsureSuccessStatusCode();

        var openBody = await openResponse.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        _sessionId = ExtractSessionId(openBody);
        _aid = 0;

        // 3. Start poll loop
        _pollCts = new CancellationTokenSource();
        _pollTask = PollLoopAsync(_pollCts.Token);
        IsConnected = true;
        LogConnected(_logger, _gsessionId, _sessionId, null);
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        IsConnected = false;

        if (_pollCts is not null)
        {
            await _pollCts.CancelAsync().ConfigureAwait(false);
        }

        if (_pollTask is not null)
        {
            try { await _pollTask.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }

        _pollCts?.Dispose();
        _pollCts = null;
        _pollTask = null;
        _sessionId = null;
        _gsessionId = null;
        LogDisconnected(_logger, null);
    }

    public async Task SendSdpOfferAsync(string callId, string sdp, CancellationToken ct = default) =>
        await SendMessageAsync("sdp-offer", callId, sdp, ct).ConfigureAwait(false);

    public async Task SendSdpAnswerAsync(string callId, string sdp, CancellationToken ct = default) =>
        await SendMessageAsync("sdp-answer", callId, sdp, ct).ConfigureAwait(false);

    public async Task SendHangupAsync(string callId, CancellationToken ct = default) =>
        await SendMessageAsync("hangup", callId, null, ct).ConfigureAwait(false);

    private async Task SendMessageAsync(string type, string callId, string? data, CancellationToken ct)
    {
        if (_sessionId is null)
            throw new InvalidOperationException("Not connected. Call ConnectAsync first.");

        LogSending(_logger, type, callId, null);

        var client = CreateClient();
        var rid = Interlocked.Increment(ref _rid);
        var url = BuildChannelUrl(rid: rid);

        // Use req0___data__ format (triple underscore) matching browser behavior
        var encodedData = data is not null
            ? Uri.EscapeDataString($"[[\"{type}\",\"{callId}\",{JsonSerializer.Serialize(data)}]]")
            : Uri.EscapeDataString($"[[\"{type}\",\"{callId}\"]]");

        var body = $"count=1&ofs=0&req0___data__={encodedData}";

        using var content = new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded");
        var response = await client
            .PostAsync(new Uri(url, UriKind.Relative), content, ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    private async Task PollLoopAsync(CancellationToken ct)
    {
        var client = CreateClient();
        var backoff = TimeSpan.FromSeconds(1);
        var maxBackoff = TimeSpan.FromSeconds(30);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var url = BuildChannelUrl(rid: "rpc", type: "xmlhttp");

                var response = await client
                    .GetAsync(new Uri(url, UriKind.Relative), ct)
                    .ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    ErrorOccurred?.Invoke(this, new SignalerErrorEventArgs(
                        new HttpRequestException($"Signaler poll returned {(int)response.StatusCode}")));
                    LogPollError(_logger, backoff.TotalSeconds, null);
                    await Task.Delay(backoff, ct).ConfigureAwait(false);
                    backoff = TimeSpan.FromSeconds(Math.Min(backoff.TotalSeconds * 2, maxBackoff.TotalSeconds));
                    continue;
                }

                var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                var jsonBody = StripLengthPrefix(body);
                var events = SignalerMessageParser.Parse(jsonBody);

                LogPollReceived(_logger, events.Count, null);

                foreach (var evt in events)
                {
                    EventReceived?.Invoke(this, new SignalerEventArgs(evt));
                }

                if (events.Count > 0)
                    Interlocked.Add(ref _aid, events.Count);
                backoff = TimeSpan.FromSeconds(1);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
#pragma warning disable CA1031 // Catch-all is intentional: poll loop must stay alive across any transient failure
            catch (Exception ex)
#pragma warning restore CA1031
            {
                ErrorOccurred?.Invoke(this, new SignalerErrorEventArgs(ex));
                LogPollError(_logger, backoff.TotalSeconds, ex);
                await Task.Delay(backoff, ct).ConfigureAwait(false);
                backoff = TimeSpan.FromSeconds(Math.Min(backoff.TotalSeconds * 2, maxBackoff.TotalSeconds));
            }
        }
    }

    private string BuildChannelUrl(object? rid = null, string? type = null)
    {
        var sb = new StringBuilder("punctual/multi-watch/channel?VER=8");

        if (_gsessionId is not null)
            sb.Append("&gsessionid=").Append(Uri.EscapeDataString(_gsessionId));

        sb.Append("&key=").Append(Uri.EscapeDataString(_apiConfig.ApiKey));

        if (_sessionId is not null)
            sb.Append("&SID=").Append(Uri.EscapeDataString(_sessionId));

        sb.Append("&AID=").Append(_aid);

        if (rid is not null)
            sb.Append("&RID=").Append(rid);

        if (type is null)
            sb.Append("&CVER=22");

        if (type is not null)
        {
            sb.Append("&CI=0");
            sb.Append("&TYPE=").Append(type);
        }

        // Random cache-buster and protocol version
        sb.Append("&zx=").Append(Guid.NewGuid().ToString("N")[..12]);
        sb.Append("&t=1");

        return sb.ToString();
    }

    /// <summary>
    /// Builds the subscription body for the channel open request.
    /// Each req subscribes to a different notification type (calls, SMS, voicemail, etc.).
    /// Format: count=6&amp;ofs=0&amp;req0___data__=...&amp;req1___data__=...
    /// </summary>
    private static string BuildSubscriptionBody()
    {
        // These match the exact subscriptions the GV web app sends
        // Each is a watch registration for a different thread type
        var sb = new StringBuilder();
        sb.Append("count=6&ofs=0");

        // Subscription types: 1=calls, 2=messages(all), 3=SMS, 4=voicemail, 5=spam, 6=archive(?)
        // Thread type mappings inside: "1"=calls, "3"=SMS, "1"=calls, "1"=calls, "2"=messages, "3"=SMS
        string[] threadTypes = ["1", "3", "1", "1", "2", "3"];

        for (int i = 0; i < 6; i++)
        {
            var subId = i + 1;
            var data = $"[[[{subId},[null,null,null,[9,5],null,[null,[null,1],[[[\"{threadTypes[i]}\"]]]]],null,null,1],null,3]]";
            sb.Append("&req").Append(i).Append("___data__=").Append(Uri.EscapeDataString(data));
        }

        return sb.ToString();
    }

    private static string ExtractGSessionId(string chooseServerResponse)
    {
        // Response: ["<gsessionid>",3,null,"<timestamp1>","<timestamp2>"]
        try
        {
            using var doc = JsonDocument.Parse(chooseServerResponse);
            return doc.RootElement[0].GetString()
                ?? throw new InvalidOperationException("gsessionid not found in chooseServer response.");
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                $"Failed to parse chooseServer response: {chooseServerResponse}", ex);
        }
    }

    private static string ExtractSessionId(string openResponse)
    {
        // Response is length-prefixed: "51\n[[0,["c","<SID>","",8,14,30000]]]\n"
        var jsonBody = StripLengthPrefix(openResponse);

        try
        {
            using var doc = JsonDocument.Parse(jsonBody);
            return doc.RootElement[0][1][1].GetString()
                ?? throw new InvalidOperationException("SID not found in channel open response.");
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                $"Failed to parse channel open response: {openResponse}", ex);
        }
    }

    /// <summary>
    /// Strips the length prefix from signaler responses.
    /// Format: "51\n[[data]]\n" → "[[data]]"
    /// </summary>
    private static string StripLengthPrefix(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return response;

        // Find the first newline — everything before it is the byte count
        var newlineIdx = response.IndexOf('\n', StringComparison.Ordinal);
        if (newlineIdx < 0)
            return response;

        // Check if the part before newline is a number
        var prefix = response[..newlineIdx].Trim();
        if (int.TryParse(prefix, System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture, out _))
        {
            return response[(newlineIdx + 1)..].Trim();
        }

        return response;
    }

    private HttpClient CreateClient() =>
        _httpClientFactory.CreateClient("GvSignaler");

    public async ValueTask DisposeAsync()
    {
        if (IsConnected)
            await DisconnectAsync().ConfigureAwait(false);
    }
}
