using System.Globalization;
using GvResearch.Api.Endpoints;
using GvResearch.Shared;
using Microsoft.AspNetCore.Authentication;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) =>
    lc.ReadFrom.Configuration(ctx.Configuration)
      .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture));

builder.Services
    .AddAuthentication("Bearer")
    .AddScheme<AuthenticationSchemeOptions, GvResearch.Api.Auth.BearerSchemeHandler>(
        "Bearer", _ => { });
builder.Services.AddAuthorization();

builder.Services.AddEndpointsApiExplorer();

var cfg = builder.Configuration;
builder.Services.AddGvClient(options =>
{
    options.CookiePath = cfg["GvResearch:CookiePath"] ?? "cookies.enc";
    options.KeyPath = cfg["GvResearch:KeyPath"] ?? "key.bin";
});

builder.Services.ConfigureHttpJsonOptions(opts =>
{
    opts.SerializerOptions.PropertyNamingPolicy =
        System.Text.Json.JsonNamingPolicy.CamelCase;
    opts.SerializerOptions.Converters.Add(
        new System.Text.Json.Serialization.JsonStringEnumConverter());
});

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseAuthentication();
app.UseAuthorization();

app.MapAccountEndpoints();
app.MapThreadEndpoints();
app.MapSmsEndpoints();
app.MapCallEndpoints();

app.Run();

#pragma warning disable CA1050
public partial class Program { }
#pragma warning restore CA1050
