using System.Text.Json;
using Iaet.Catalog.Entities;
using Iaet.Core.Abstractions;
using Iaet.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Iaet.Catalog;

public sealed class SqliteCatalog : IEndpointCatalog
{
    private readonly CatalogDbContext _db;

    public SqliteCatalog(CatalogDbContext db)
    {
        _db = db;
    }

    public async Task SaveSessionAsync(CaptureSessionInfo session, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        _db.Sessions.Add(new CaptureSessionEntity
        {
            Id = session.Id,
            Name = session.Name,
            TargetApplication = session.TargetApplication,
            Profile = session.Profile,
            StartedAt = session.StartedAt,
            StoppedAt = session.StoppedAt
        });
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task SaveRequestAsync(CapturedRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalized = EndpointNormalizer.Normalize(request.HttpMethod, request.Url);

        _db.Requests.Add(new CapturedRequestEntity
        {
            Id = request.Id,
            SessionId = request.SessionId,
            Timestamp = request.Timestamp,
            HttpMethod = request.HttpMethod,
            Url = request.Url,
            NormalizedSignature = normalized,
            RequestHeaders = JsonSerializer.Serialize(request.RequestHeaders),
            RequestBody = request.RequestBody,
            ResponseStatus = request.ResponseStatus,
            ResponseHeaders = JsonSerializer.Serialize(request.ResponseHeaders),
            ResponseBody = request.ResponseBody,
            DurationMs = request.DurationMs,
            Tag = request.Tag
        });

        var group = await _db.EndpointGroups
            .FirstOrDefaultAsync(g => g.SessionId == request.SessionId
                && g.NormalizedSignature == normalized, ct).ConfigureAwait(false);

        if (group is null)
        {
            _db.EndpointGroups.Add(new EndpointGroupEntity
            {
                Id = Guid.NewGuid(),
                SessionId = request.SessionId,
                NormalizedSignature = normalized,
                ObservationCount = 1,
                FirstSeen = request.Timestamp,
                LastSeen = request.Timestamp
            });
        }
        else
        {
            group.ObservationCount++;
            group.LastSeen = request.Timestamp;
        }

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<CaptureSessionInfo>> ListSessionsAsync(CancellationToken ct = default)
    {
        // Materialize from DB first, then sort in memory — SQLite does not support
        // ORDER BY on DateTimeOffset columns directly.
        var raw = await _db.Sessions
            .Include(s => s.Requests)
            .ToListAsync(ct).ConfigureAwait(false);

        return raw
            .OrderByDescending(s => s.StartedAt)
            .Select(s => new CaptureSessionInfo
            {
                Id = s.Id,
                Name = s.Name,
                TargetApplication = s.TargetApplication,
                Profile = s.Profile,
                StartedAt = s.StartedAt,
                StoppedAt = s.StoppedAt,
                CapturedRequestCount = s.Requests.Count
            })
            .ToList();
    }

    public async Task<IReadOnlyList<CapturedRequest>> GetRequestsBySessionAsync(
        Guid sessionId, CancellationToken ct = default)
    {
        // Materialize from DB first, then sort+map in memory — EF cannot translate
        // JsonSerializer.Deserialize to SQL, and SQLite does not support ORDER BY on
        // DateTimeOffset columns directly.
        var raw = await _db.Requests
            .Where(r => r.SessionId == sessionId)
            .ToListAsync(ct).ConfigureAwait(false);

        return raw.OrderBy(r => r.Timestamp).Select(r => new CapturedRequest
        {
            Id = r.Id,
            SessionId = r.SessionId,
            Timestamp = r.Timestamp,
            HttpMethod = r.HttpMethod,
            Url = r.Url,
            RequestHeaders = r.RequestHeaders != null
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(r.RequestHeaders)!
                : new Dictionary<string, string>(),
            RequestBody = r.RequestBody,
            ResponseStatus = r.ResponseStatus,
            ResponseHeaders = r.ResponseHeaders != null
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(r.ResponseHeaders)!
                : new Dictionary<string, string>(),
            ResponseBody = r.ResponseBody,
            DurationMs = r.DurationMs,
            Tag = r.Tag
        }).ToList();
    }

    public async Task<IReadOnlyList<EndpointGroup>> GetEndpointGroupsAsync(
        Guid sessionId, CancellationToken ct = default)
    {
        // Materialize from DB first, then map in memory — EF cannot translate
        // EndpointSignature.FromRequest or string.Split to SQL.
        var raw = await _db.EndpointGroups
            .Where(g => g.SessionId == sessionId)
            .OrderByDescending(g => g.ObservationCount)
            .ToListAsync(ct).ConfigureAwait(false);

        return raw.Select(g =>
        {
            var parts = g.NormalizedSignature.Split(' ', 2);
            return new EndpointGroup(
                EndpointSignature.FromRequest(parts[0], parts[1]),
                g.ObservationCount,
                g.FirstSeen,
                g.LastSeen);
        }).ToList();
    }
}
