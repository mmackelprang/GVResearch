using System.Text;
using Microsoft.Extensions.Logging;

namespace GvResearch.Shared.Signaler;

public sealed class GvSignalerClient : IGvSignalerClient
{
    private static readonly Action<ILogger, string, Exception?> LogConnected =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(1, "SignalerConnected"),
            "Signaler connected, SID={Sid}");

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

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GvApiConfig _apiConfig;
    private readonly ILogger<GvSignalerClient> _logger;

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

        // 1. Choose server
        using var chooseBody = new StringContent("{}", Encoding.UTF8, "application/json");
        var chooseResponse = await client
            .PostAsync(
                new Uri("punctual/v1/chooseServer", UriKind.Relative),
                chooseBody,
                ct)
            .ConfigureAwait(false);
        chooseResponse.EnsureSuccessStatusCode();

        // 2. Open channel — get SID
        _rid = 0;
        var openUrl = BuildChannelUrl(rid: _rid);
        Interlocked.Increment(ref _rid);

        var openResponse = await client
            .PostAsync(new Uri(openUrl, UriKind.Relative), null, ct)
            .ConfigureAwait(false);
        openResponse.EnsureSuccessStatusCode();

        var openBody = await openResponse.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        _sessionId = ExtractSessionId(openBody);
        _aid = 0;

        // 3. Start poll loop — use a standalone CTS so the loop only stops on Disconnect/Dispose,
        //    not when the caller's token is cancelled after ConnectAsync returns.
        _pollCts = new CancellationTokenSource();
        _pollTask = PollLoopAsync(_pollCts.Token);
        IsConnected = true;
        LogConnected(_logger, _sessionId, null);
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

        var body = data is not null
            ? $"count=1&ofs=0&req0_type={Uri.EscapeDataString(type)}&req0_callid={Uri.EscapeDataString(callId)}&req0_data={Uri.EscapeDataString(data)}"
            : $"count=1&ofs=0&req0_type={Uri.EscapeDataString(type)}&req0_callid={Uri.EscapeDataString(callId)}";

        using var content = new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded");
        var response = await client
            .PostAsync(new Uri(url, UriKind.Relative), content, ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    private async Task PollLoopAsync(CancellationToken ct)
    {
        var backoff = TimeSpan.FromSeconds(1);
        var maxBackoff = TimeSpan.FromSeconds(30);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var client = CreateClient();
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
                var events = SignalerMessageParser.Parse(body);

                LogPollReceived(_logger, events.Count, null);

                foreach (var evt in events)
                {
                    EventReceived?.Invoke(this, new SignalerEventArgs(evt));
                }

                Interlocked.Increment(ref _aid);
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
        var sb = new StringBuilder("punctual/multi-watch/channel?VER=8&CVER=22");
        sb.Append("&key=").Append(Uri.EscapeDataString(_apiConfig.ApiKey));

        if (_sessionId is not null)
            sb.Append("&SID=").Append(Uri.EscapeDataString(_sessionId));

        sb.Append("&AID=").Append(_aid);
        sb.Append("&RID=").Append(rid ?? _rid);

        if (type is not null)
            sb.Append("&TYPE=").Append(type);

        return sb.ToString();
    }

    private static string ExtractSessionId(string openResponse)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(openResponse);
            return doc.RootElement[0][1][1].GetString()
                ?? throw new InvalidOperationException("SID not found in channel open response.");
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                $"Failed to parse channel open response: {openResponse}", ex);
        }
    }

    private HttpClient CreateClient() =>
        _httpClientFactory.CreateClient("GvSignaler");

    public async ValueTask DisposeAsync()
    {
        if (IsConnected)
            await DisconnectAsync().ConfigureAwait(false);
    }
}
