using Avalonia;
using GvResearch.Shared;
using GvResearch.Shared.Signaler;
using GvResearch.Shared.Transport;
using GvResearch.Sip.Transport;
using GvResearch.Softphone.Phone;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GvResearch.Softphone;

internal sealed class Program
{
    public static ServiceProvider? Services { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging(b => b.AddConsole());

        // Register signaler and transport BEFORE AddGvClient so the NullCallTransport is skipped.
        serviceCollection.AddSingleton<IGvSignalerClient, GvSignalerClient>();
        serviceCollection.AddSingleton<ICallTransport, WebRtcCallTransport>();

        serviceCollection.AddGvClient(options =>
        {
            options.CookiePath = config["GvResearch:CookiePath"] ?? "cookies.enc";
            options.KeyPath = config["GvResearch:KeyPath"] ?? "key.bin";
            options.ApiKey = config["GvResearch:ApiKey"] ?? string.Empty;
        });

        serviceCollection.AddSingleton<Audio.AudioEngine>();
        serviceCollection.AddSingleton<GvPhoneClient>();

        Services = serviceCollection.BuildServiceProvider();

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<SoftphoneApp>()
            .UsePlatformDetect()
            .LogToTrace();
}
