using System.Collections.Concurrent;
using System.Diagnostics;
using Iaet.Core.Models;
using Microsoft.Playwright;

namespace Iaet.Capture;

public sealed class CdpNetworkListener
{
    private readonly ConcurrentDictionary<string, PendingRequest> _pending = new();
    private readonly ConcurrentQueue<CapturedRequest> _completed = new();
    private readonly Guid _sessionId;

    public CdpNetworkListener(Guid sessionId)
    {
        _sessionId = sessionId;
    }

    public void Attach(IPage page)
    {
        ArgumentNullException.ThrowIfNull(page);

        page.Request += (_, request) =>
        {
            if (!IsXhrOrFetch(request)) return;
            _pending[request.Url + request.Method] = new PendingRequest(
                Stopwatch.StartNew(),
                request
            );
        };

        page.Response += async (_, response) =>
        {
            var key = response.Request.Url + response.Request.Method;
            if (!_pending.TryRemove(key, out var pending)) return;
            pending.Stopwatch.Stop();

            string? requestBody = null;
            try { requestBody = response.Request.PostData; }
#pragma warning disable CA1031 // Intentionally catching all exceptions: some requests have no body and may throw unpredictably
            catch { /* some requests have no body */ }
#pragma warning restore CA1031

            string? responseBody = null;
            try { responseBody = await response.TextAsync().ConfigureAwait(false); }
#pragma warning disable CA1031 // Intentionally catching all exceptions: binary or failed responses may throw unpredictably
            catch { /* binary or failed */ }
#pragma warning restore CA1031

            var captured = new CapturedRequest
            {
                Id = Guid.NewGuid(),
                SessionId = _sessionId,
                Timestamp = DateTimeOffset.UtcNow,
                HttpMethod = response.Request.Method,
                Url = response.Request.Url,
                RequestHeaders = RequestSanitizer.SanitizeHeaders(
                    await response.Request.AllHeadersAsync().ConfigureAwait(false)),
                RequestBody = requestBody,
                ResponseStatus = response.Status,
                ResponseHeaders = RequestSanitizer.SanitizeHeaders(
                    await response.AllHeadersAsync().ConfigureAwait(false)),
                ResponseBody = responseBody,
                DurationMs = pending.Stopwatch.ElapsedMilliseconds,
            };

            _completed.Enqueue(captured);
        };
    }

    public IReadOnlyList<CapturedRequest> DrainCaptured()
    {
        var result = new List<CapturedRequest>();
        while (_completed.TryDequeue(out var item))
            result.Add(item);
        return result;
    }

    private static bool IsXhrOrFetch(IRequest request) =>
        request.ResourceType is "xhr" or "fetch";

    private sealed record PendingRequest(Stopwatch Stopwatch, IRequest Request);
}
