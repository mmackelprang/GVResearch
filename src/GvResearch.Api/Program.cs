using System.Globalization;
using GvResearch.Api.Endpoints;
using GvResearch.Shared.Authentication;
using GvResearch.Shared.Http;
using GvResearch.Shared.RateLimiting;
using GvResearch.Shared.Services;
using Microsoft.AspNetCore.Authentication;
using Serilog;

// Bootstrap Serilog early so startup errors are captured.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // ── Serilog ────────────────────────────────────────────────────────────────
    builder.Host.UseSerilog((ctx, lc) =>
        lc.ReadFrom.Configuration(ctx.Configuration));

    // ── Authentication / Authorization ─────────────────────────────────────────
    builder.Services
        .AddAuthentication("Bearer")
        .AddScheme<AuthenticationSchemeOptions, GvResearch.Api.Auth.BearerSchemeHandler>(
            "Bearer", _ => { });

    builder.Services.AddAuthorization();

    // ── OpenAPI (net8 style) ───────────────────────────────────────────────────
    builder.Services.AddEndpointsApiExplorer();

    // ── GV Services ───────────────────────────────────────────────────────────
    builder.Services.AddSingleton<GvRateLimiter>();

    // Register token service — reads paths from configuration.
    builder.Services.AddSingleton<IGvTokenService>(sp =>
    {
        var cfg = sp.GetRequiredService<IConfiguration>();
        var tokenPath = cfg["GvResearch:TokenPath"] ?? string.Empty;
        var keyPath = cfg["GvResearch:KeyPath"] ?? string.Empty;
        return new EncryptedFileTokenService(
            string.IsNullOrWhiteSpace(tokenPath) ? "token.enc" : tokenPath,
            string.IsNullOrWhiteSpace(keyPath) ? "key.bin" : keyPath);
    });

    // Register GvHttpClientHandler as a transient delegating handler.
    // GvHttpClientHandler requires an explicit inner handler so we construct it manually.
    builder.Services.AddTransient<GvHttpClientHandler>(sp =>
    {
        var tokenService = sp.GetRequiredService<IGvTokenService>();
        return new GvHttpClientHandler(tokenService, new HttpClientHandler());
    });

    // Register the typed HTTP client for GvCallService with standard resilience.
    builder.Services
        .AddHttpClient<IGvCallService, GvCallService>((_, client) =>
        {
            client.BaseAddress = new Uri("https://voice.google.com");
        })
        .AddStandardResilienceHandler();

    // ── JSON ──────────────────────────────────────────────────────────────────
    builder.Services.ConfigureHttpJsonOptions(opts =>
    {
        opts.SerializerOptions.PropertyNamingPolicy =
            System.Text.Json.JsonNamingPolicy.CamelCase;
        opts.SerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

    var app = builder.Build();

    // ── Middleware ────────────────────────────────────────────────────────────
    app.UseSerilogRequestLogging();
    app.UseAuthentication();
    app.UseAuthorization();

    // ── Endpoints ─────────────────────────────────────────────────────────────
    app.MapCallEndpoints();

    app.Run();
}
catch (Exception ex) when (ex is not OperationCanceledException)
{
    Log.Fatal(ex, "Host terminated unexpectedly.");
}
finally
{
    Log.CloseAndFlush();
}

// Make Program accessible to WebApplicationFactory in tests.
#pragma warning disable CA1050 // Declare types in namespaces — Program is intentionally top-level
public partial class Program { }
#pragma warning restore CA1050
