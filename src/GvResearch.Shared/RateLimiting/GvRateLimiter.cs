using System.Collections.Concurrent;
using System.Threading.RateLimiting;

namespace GvResearch.Shared.RateLimiting;

/// <summary>
/// Per-endpoint rate limiter using two <see cref="FixedWindowRateLimiter"/> windows:
/// one per minute and one per day. Both limits must pass for a request to proceed.
/// </summary>
public sealed class GvRateLimiter : IDisposable
{
    private readonly int _perMinuteLimit;
    private readonly int _perDayLimit;

    private readonly ConcurrentDictionary<string, FixedWindowRateLimiter> _minuteLimiters = new();
    private readonly ConcurrentDictionary<string, FixedWindowRateLimiter> _dayLimiters = new();

    private bool _disposed;

    /// <param name="perMinuteLimit">Maximum requests per endpoint per minute (default 10).</param>
    /// <param name="perDayLimit">Maximum requests per endpoint per day (default 100).</param>
    public GvRateLimiter(int perMinuteLimit = 10, int perDayLimit = 100)
    {
        _perMinuteLimit = perMinuteLimit;
        _perDayLimit = perDayLimit;
    }

    /// <summary>
    /// Attempts to acquire a permit for <paramref name="endpoint"/>.
    /// Returns <c>true</c> if both the per-minute and per-day limits allow the request.
    /// </summary>
    public async ValueTask<bool> TryAcquireAsync(
        string endpoint,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);

        var minuteLimiter = _minuteLimiters.GetOrAdd(endpoint, CreateMinuteLimiter);
        var dayLimiter = _dayLimiters.GetOrAdd(endpoint, CreateDayLimiter);

        using var minuteLease = await minuteLimiter.AcquireAsync(permitCount: 1, cancellationToken).ConfigureAwait(false);
        if (!minuteLease.IsAcquired)
        {
            return false;
        }

        using var dayLease = await dayLimiter.AcquireAsync(permitCount: 1, cancellationToken).ConfigureAwait(false);
        return dayLease.IsAcquired;
    }

    private FixedWindowRateLimiter CreateMinuteLimiter(string _) =>
        new(new FixedWindowRateLimiterOptions
        {
            PermitLimit = _perMinuteLimit,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0,
        });

    private FixedWindowRateLimiter CreateDayLimiter(string _) =>
        new(new FixedWindowRateLimiterOptions
        {
            PermitLimit = _perDayLimit,
            Window = TimeSpan.FromDays(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0,
        });

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var limiter in _minuteLimiters.Values)
        {
            limiter.Dispose();
        }

        foreach (var limiter in _dayLimiters.Values)
        {
            limiter.Dispose();
        }
    }
}
