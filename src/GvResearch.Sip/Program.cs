using System.Globalization;
using System.Net;
using GvResearch.Shared;
using GvResearch.Sip.Calls;
using GvResearch.Sip.Configuration;
using GvResearch.Sip.Media;
using GvResearch.Sip.Registrar;
using GvResearch.Sip;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;
using SIPSorcery.SIP;

// Bootstrap Serilog early so startup errors are captured.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .CreateBootstrapLogger();

try
{
    var host = Host.CreateDefaultBuilder(args)
        .UseSerilog((ctx, lc) => lc.ReadFrom.Configuration(ctx.Configuration))
        .ConfigureServices((ctx, services) =>
        {
            var cfg = ctx.Configuration;

            // ── Configuration ─────────────────────────────────────────────────
            services.Configure<SipGatewayOptions>(
                cfg.GetSection(SipGatewayOptions.SectionName));

            // ── SIPTransport (singleton, shared across registrar/controller) ──
            services.AddSingleton(sp =>
            {
                var opts = sp.GetRequiredService<IOptions<SipGatewayOptions>>().Value;
                var transport = new SIPTransport();
                transport.AddSIPChannel(
                    new SIPUDPChannel(new IPEndPoint(IPAddress.Any, opts.SipPort)));
                return transport;
            });

            // ── Registrar ─────────────────────────────────────────────────────
            services.AddSingleton<RegistrationStore>();
            services.AddSingleton<SipRegistrar>();

            // ── Media ─────────────────────────────────────────────────────────
            services.AddSingleton<IGvAudioChannel, WebRtcGvAudioChannel>();

            // ── GV SDK ───────────────────────────────────────────────────────
            services.AddGvClient(options =>
            {
                var tokenPath = cfg["GvResearch:CookiePath"] ?? string.Empty;
                var keyPath = cfg["GvResearch:KeyPath"] ?? string.Empty;
                options.CookiePath = string.IsNullOrWhiteSpace(tokenPath) ? "cookies.enc" : tokenPath;
                options.KeyPath = string.IsNullOrWhiteSpace(keyPath) ? "key.bin" : keyPath;
            });

            // ── Call controller ───────────────────────────────────────────────
            services.AddSingleton<SipCallController>();

            // ── Hosted service that wires the registrar INVITE event ──────────
            services.AddHostedService<SipGatewayService>();
        })
        .Build();

    await host.RunAsync().ConfigureAwait(false);
}
catch (Exception ex) when (ex is not OperationCanceledException)
{
    Log.Fatal(ex, "SIP gateway terminated unexpectedly.");
}
finally
{
    await Log.CloseAndFlushAsync().ConfigureAwait(false);
}
