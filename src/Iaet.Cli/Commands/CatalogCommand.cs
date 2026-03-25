using System.CommandLine;
using Iaet.Catalog;
using Microsoft.EntityFrameworkCore;

namespace Iaet.Cli.Commands;

public static class CatalogCommand
{
    public static Command Create()
    {
        var catalogCmd = new Command("catalog", "Browse the endpoint catalog");
        var dbOption = new Option<string>("--db") { Description = "SQLite database path", DefaultValueFactory = _ => "catalog.db" };

        var listCmd = new Command("sessions", "List capture sessions");
        listCmd.Add(dbOption);
        listCmd.SetAction(async (parseResult) =>
        {
            var dbPath = parseResult.GetValue(dbOption)!;

            var options = new DbContextOptionsBuilder<CatalogDbContext>()
                .UseSqlite($"DataSource={dbPath}")
                .Options;
            var db = new CatalogDbContext(options);
            await using (db.ConfigureAwait(false))
            {
                var catalog = new SqliteCatalog(db);
                var sessions = await catalog.ListSessionsAsync().ConfigureAwait(false);

                if (sessions.Count == 0)
                {
                    Console.WriteLine("No sessions found.");
                    return;
                }

                Console.WriteLine($"{"ID",-38} {"Name",-20} {"Target",-20} {"Requests",-10} {"Started"}");
                Console.WriteLine(new string('-', 110));
                foreach (var s in sessions)
                {
                    Console.WriteLine($"{s.Id,-38} {s.Name,-20} {s.TargetApplication,-20} {s.CapturedRequestCount,-10} {s.StartedAt:g}");
                }
            }
        });

        var endpointsCmd = new Command("endpoints", "List discovered endpoints for a session");
        var sessionIdOption = new Option<Guid>("--session-id") { Description = "Session ID to inspect", Required = true };
        endpointsCmd.Add(sessionIdOption);
        endpointsCmd.Add(dbOption);
        endpointsCmd.SetAction(async (parseResult) =>
        {
            var sessionId = parseResult.GetRequiredValue(sessionIdOption);
            var dbPath = parseResult.GetValue(dbOption)!;

            var options = new DbContextOptionsBuilder<CatalogDbContext>()
                .UseSqlite($"DataSource={dbPath}")
                .Options;
            var db = new CatalogDbContext(options);
            await using (db.ConfigureAwait(false))
            {
                var catalog = new SqliteCatalog(db);
                var groups = await catalog.GetEndpointGroupsAsync(sessionId).ConfigureAwait(false);

                if (groups.Count == 0)
                {
                    Console.WriteLine("No endpoints found for this session.");
                    return;
                }

                Console.WriteLine($"{"Endpoint",-50} {"Count",-8} {"First Seen",-22} {"Last Seen"}");
                Console.WriteLine(new string('-', 105));
                foreach (var g in groups)
                {
                    Console.WriteLine($"{g.Signature.Normalized,-50} {g.ObservationCount,-8} {g.FirstSeen:g,-22} {g.LastSeen:g}");
                }
            }
        });

        catalogCmd.Add(listCmd);
        catalogCmd.Add(endpointsCmd);
        return catalogCmd;
    }
}
