using System.CommandLine;
using Iaet.Capture;
using Iaet.Catalog;
using Microsoft.EntityFrameworkCore;

namespace Iaet.Cli.Commands;

public static class CaptureCommand
{
    public static Command Create()
    {
        var captureCmd = new Command("capture", "Manage capture sessions");

        var startCmd = new Command("start", "Start a new capture session");

        var targetOption = new Option<string>("--target") { Description = "Target application name", Required = true };
        var profileOption = new Option<string>("--profile") { Description = "Browser profile name", DefaultValueFactory = _ => "default" };
        var urlOption = new Option<string>("--url") { Description = "Starting URL to navigate to", Required = true };
        var sessionOption = new Option<string>("--session") { Description = "Session name", Required = true };
        var dbOption = new Option<string>("--db") { Description = "SQLite database path", DefaultValueFactory = _ => "catalog.db" };

        startCmd.Add(targetOption);
        startCmd.Add(profileOption);
        startCmd.Add(urlOption);
        startCmd.Add(sessionOption);
        startCmd.Add(dbOption);

        startCmd.SetAction(async (parseResult) =>
        {
            var target = parseResult.GetRequiredValue(targetOption);
            var profile = parseResult.GetValue(profileOption)!;
            var url = parseResult.GetRequiredValue(urlOption);
            var sessionName = parseResult.GetRequiredValue(sessionOption);
            var dbPath = parseResult.GetValue(dbOption)!;

            Console.WriteLine($"Starting capture session '{sessionName}' for {target}...");
            Console.WriteLine("Browser will open. Perform actions, then press Enter to stop.");

            var dbOptions = new DbContextOptionsBuilder<CatalogDbContext>()
                .UseSqlite($"DataSource={dbPath}")
                .Options;
            var db = new CatalogDbContext(dbOptions);
            await using (db.ConfigureAwait(false))
            {
                await db.Database.EnsureCreatedAsync().ConfigureAwait(false);
                var catalog = new SqliteCatalog(db);

                var session = new PlaywrightCaptureSession(target, profile);
                await using (session.ConfigureAwait(false))
                {
                    var sessionInfo = new Iaet.Core.Models.CaptureSessionInfo
                    {
                        Id = session.SessionId,
                        Name = sessionName,
                        TargetApplication = target,
                        Profile = profile,
                        StartedAt = DateTimeOffset.UtcNow
                    };
                    await catalog.SaveSessionAsync(sessionInfo).ConfigureAwait(false);

                    await session.StartAsync(url).ConfigureAwait(false);
                    Console.WriteLine($"Recording... Session ID: {session.SessionId}");
                    Console.ReadLine();

                    var count = 0;
                    await foreach (var request in session.GetCapturedRequestsAsync())
                    {
                        await catalog.SaveRequestAsync(request).ConfigureAwait(false);
                        count++;
                    }

                    await session.StopAsync().ConfigureAwait(false);
                    Console.WriteLine($"Captured {count} requests.");
                }
            }
        });

        captureCmd.Add(startCmd);
        return captureCmd;
    }
}
