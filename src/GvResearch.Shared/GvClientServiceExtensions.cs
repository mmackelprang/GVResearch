using System.Diagnostics.CodeAnalysis;
using GvResearch.Shared.Auth;
using GvResearch.Shared.Http;
using GvResearch.Shared.RateLimiting;
using GvResearch.Shared.Services;
using GvResearch.Shared.Transport;
using Microsoft.Extensions.DependencyInjection;

namespace GvResearch.Shared;

public static class GvClientServiceExtensions
{
    public static IServiceCollection AddGvClient(
        this IServiceCollection services,
        Action<GvClientOptions>? configure = null)
    {
        var options = new GvClientOptions();
        configure?.Invoke(options);

        var cookiePath = options.CookiePath;
        var keyPath = options.KeyPath;
        services.AddSingleton<IGvAuthService>(_ => new GvAuthService(cookiePath, keyPath));
        services.AddSingleton(new GvApiConfig { ApiKey = options.ApiKey });

        services.AddSingleton<GvRateLimiter>();

        services.AddHttpClient("GvApi", client =>
        {
            client.BaseAddress = new Uri("https://clients6.google.com");
        })
        .ConfigurePrimaryHttpMessageHandler(sp =>
        {
            var auth = sp.GetRequiredService<IGvAuthService>();
            return new GvHttpClientHandler(auth, new HttpClientHandler());
        });

        services.AddHttpClient("GvSignaler", client =>
        {
            client.BaseAddress = new Uri("https://signaler-pa.clients6.google.com");
            client.Timeout = TimeSpan.FromMinutes(5);
        });

        services.AddSingleton<IGvAccountClient>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            return new GvAccountClient(factory.CreateClient("GvApi"), sp.GetRequiredService<GvRateLimiter>(), sp.GetRequiredService<GvApiConfig>());
        });

        services.AddSingleton<IGvThreadClient>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            return new GvThreadClient(factory.CreateClient("GvApi"), sp.GetRequiredService<GvRateLimiter>(), sp.GetRequiredService<GvApiConfig>());
        });

        services.AddSingleton<IGvSmsClient>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            return new GvSmsClient(factory.CreateClient("GvApi"), sp.GetRequiredService<GvRateLimiter>(), sp.GetRequiredService<GvApiConfig>());
        });

        if (!services.Any(d => d.ServiceType == typeof(ICallTransport)))
        {
            services.AddSingleton<ICallTransport, NullCallTransport>();
        }

        services.AddSingleton<IGvCallClient>(sp =>
            new GvCallClient(
                sp.GetRequiredService<ICallTransport>(),
                sp.GetRequiredService<GvRateLimiter>()));

        services.AddSingleton<IGvClient>(sp =>
            new GvClient(
                sp.GetRequiredService<IGvAccountClient>(),
                sp.GetRequiredService<IGvThreadClient>(),
                sp.GetRequiredService<IGvSmsClient>(),
                sp.GetRequiredService<IGvCallClient>()));

        return services;
    }
}

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instantiated by the DI container via reflection.")]
internal sealed class NullCallTransport : ICallTransport
{
    public event EventHandler<IncomingCallEventArgs>? IncomingCallReceived
    {
        add { }
        remove { }
    }

    public Task<TransportCallResult> InitiateAsync(string toNumber, CancellationToken ct) =>
        throw new NotImplementedException("No call transport configured. Register an ICallTransport implementation.");

    public Task<TransportCallStatus> GetStatusAsync(string callId, CancellationToken ct) =>
        throw new NotImplementedException("No call transport configured.");

    public Task HangupAsync(string callId, CancellationToken ct) =>
        throw new NotImplementedException("No call transport configured.");

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
