using System.Net.Security;
using System.Net.WebSockets;
using System.Text;
using Microsoft.Extensions.Logging;
using SIPSorcery.SIP;

namespace GvResearch.Sip.Transport;

public sealed class SipMessageEventArgs(string message) : EventArgs
{
    public string Message { get; } = message;
}

/// <summary>
/// Custom SIP-over-WebSocket channel that bypasses SSL certificate validation.
/// Needed because Google's SIP proxy at 216.239.36.145 serves a cert
/// for a Google domain, but we connect by raw IP address.
///
/// This replaces SIPSorcery's SIPClientWebSocketChannel which doesn't
/// expose certificate validation options.
/// </summary>
public sealed class GvSipWebSocketChannel : IDisposable
{
    private readonly ILogger _logger;
    private readonly string _serverUri;
    private ClientWebSocket? _ws;
    private CancellationTokenSource? _receiveCts;
    private Task? _receiveTask;
    private bool _disposed;

    public event EventHandler<SipMessageEventArgs>? MessageReceived;

    public GvSipWebSocketChannel(Uri serverUri, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(serverUri);
        ArgumentNullException.ThrowIfNull(logger);
        _serverUri = serverUri.ToString();
        _logger = logger;
    }

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        _ws = new ClientWebSocket();
        _ws.Options.AddSubProtocol("sip");

        // Bypass cert validation — the server at 216.239.36.145 serves a cert
        // for "telephony.goog" but we connect by IP address
#pragma warning disable CA5359 // Connecting to known Google SIP proxy IP
        _ws.Options.RemoteCertificateValidationCallback =
            static (_, _, _, _) => true;
#pragma warning restore CA5359

        // Use an HttpMessageInvoker with cert bypass + HTTP/1.1 for WebSocket upgrade
#pragma warning disable CA5359, CA2000 // Handler lifetime managed by the invoker
        var handler = new SocketsHttpHandler
        {
            SslOptions = new System.Net.Security.SslClientAuthenticationOptions
            {
                RemoteCertificateValidationCallback = static (_, _, _, _) => true,
                // Set the target host for SNI to match the cert subject
                TargetHost = "telephony.goog",
            },
        };
#pragma warning restore CA5359, CA2000
        using var invoker = new HttpMessageInvoker(handler);

        // Set Host header to match the cert CN
        _ws.Options.SetRequestHeader("Host", "telephony.goog");

#pragma warning disable CA1848, CA1873 // Debug/UAT tool
        _logger.LogInformation("Connecting WebSocket to {Uri}...", _serverUri);
#pragma warning restore CA1848, CA1873

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));

        await _ws.ConnectAsync(new Uri(_serverUri), invoker, timeoutCts.Token).ConfigureAwait(false);

#pragma warning disable CA1848, CA1873
        _logger.LogInformation("WebSocket connected! State={State}", _ws.State);
#pragma warning restore CA1848, CA1873

        // Start receive loop
        _receiveCts = new CancellationTokenSource();
        _receiveTask = ReceiveLoopAsync(_receiveCts.Token);
    }

    public async Task SendAsync(string sipMessage, CancellationToken ct = default)
    {
        if (_ws?.State != WebSocketState.Open)
            throw new InvalidOperationException("WebSocket not connected");

        var bytes = Encoding.UTF8.GetBytes(sipMessage);
        await _ws.SendAsync(bytes, WebSocketMessageType.Text, true, ct).ConfigureAwait(false);
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[16384];

        while (!ct.IsCancellationRequested && _ws?.State == WebSocketState.Open)
        {
            try
            {
                var result = await _ws.ReceiveAsync(buffer, ct).ConfigureAwait(false);

                if (result.MessageType == WebSocketMessageType.Close)
                {
#pragma warning disable CA1848, CA1873 // Debug/UAT tool — LoggerMessage perf not required
                    _logger.LogInformation("WebSocket closed by server");
#pragma warning restore CA1848, CA1873
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    MessageReceived?.Invoke(this, new SipMessageEventArgs(message));
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
#pragma warning disable CA1031
            catch (Exception ex)
#pragma warning restore CA1031
            {
#pragma warning disable CA1848, CA1873 // Debug/UAT tool — LoggerMessage perf not required
                _logger.LogWarning(ex, "WebSocket receive error");
#pragma warning restore CA1848, CA1873
                break;
            }
        }
    }

    public async Task CloseAsync()
    {
        if (_receiveCts is not null)
        {
            await _receiveCts.CancelAsync().ConfigureAwait(false);
        }

        if (_ws?.State == WebSocketState.Open)
        {
            try
            {
                await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "closing", CancellationToken.None)
                    .ConfigureAwait(false);
            }
#pragma warning disable CA1031
            catch { /* best effort */ }
#pragma warning restore CA1031
        }

        if (_receiveTask is not null)
        {
            try { await _receiveTask.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }

        _receiveCts?.Dispose();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _ws?.Dispose();
        _receiveCts?.Dispose();
    }
}
