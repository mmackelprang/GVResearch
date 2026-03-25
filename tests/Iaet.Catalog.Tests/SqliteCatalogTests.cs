using FluentAssertions;
using Iaet.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Iaet.Catalog.Tests;

public sealed class SqliteCatalogTests : IDisposable
{
    private readonly CatalogDbContext _db;
    private readonly SqliteCatalog _catalog;

    public SqliteCatalogTests()
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new CatalogDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
        _catalog = new SqliteCatalog(_db);
    }

    [Fact]
    public async Task SaveAndListSession_RoundTrips()
    {
        var session = new CaptureSessionInfo
        {
            Id = Guid.NewGuid(),
            Name = "test-session",
            TargetApplication = "TestApp",
            Profile = "test-profile",
            StartedAt = DateTimeOffset.UtcNow
        };

        await _catalog.SaveSessionAsync(session);
        var sessions = await _catalog.ListSessionsAsync();
        sessions.Should().ContainSingle().Which.Name.Should().Be("test-session");
    }

    [Fact]
    public async Task SaveRequest_GroupsBySignature()
    {
        var sessionId = Guid.NewGuid();
        var session = new CaptureSessionInfo
        {
            Id = sessionId, Name = "s1", TargetApplication = "App",
            Profile = "p1", StartedAt = DateTimeOffset.UtcNow
        };
        await _catalog.SaveSessionAsync(session);

        await _catalog.SaveRequestAsync(MakeRequest(sessionId, "GET", "https://api.test/users/123"));
        await _catalog.SaveRequestAsync(MakeRequest(sessionId, "GET", "https://api.test/users/456"));

        var groups = await _catalog.GetEndpointGroupsAsync(sessionId);
        groups.Should().ContainSingle()
            .Which.ObservationCount.Should().Be(2);
    }

    private static CapturedRequest MakeRequest(Guid sessionId, string method, string url) => new()
    {
        Id = Guid.NewGuid(),
        SessionId = sessionId,
        Timestamp = DateTimeOffset.UtcNow,
        HttpMethod = method,
        Url = url,
        RequestHeaders = new Dictionary<string, string>(),
        ResponseStatus = 200,
        ResponseHeaders = new Dictionary<string, string>(),
        DurationMs = 50
    };

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }
}
