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
    public static string? AutoDialNumber { get; private set; }

    private static readonly string LogDir = Path.Combine("D:", "prj", "GVResearch", "logs");
    private static readonly string LogFile = Path.Combine(LogDir, "softphone.log");

    [STAThread]
    public static void Main(string[] args)
    {
        // Support: dotnet run --project src/GvResearch.Softphone -- 9193718044
        AutoDialNumber = args.FirstOrDefault(a => !a.StartsWith('-'));
        if (AutoDialNumber is not null)
            Console.WriteLine($"Auto-dial: {AutoDialNumber}");

        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var cookiePath = config["GvResearch:CookiePath"] ?? "cookies.enc";
        var keyPath = config["GvResearch:KeyPath"] ?? "key.bin";

        // Cookie retrieval is now handled automatically by GvAuthService.GetValidCookiesAsync:
        // - If cookies.enc exists and is valid → uses cached cookies
        // - If cookies.enc is expired (account/get returns 401) → auto-refreshes from Chrome
        // - If cookies.enc is missing → auto-retrieves from Chrome via CDP

        var serviceCollection = new ServiceCollection();

        // File + console logging
        Directory.CreateDirectory(LogDir);
        serviceCollection.AddLogging(b =>
        {
            b.AddConsole();
            b.AddProvider(new FileLoggerProvider(LogFile));
            b.SetMinimumLevel(LogLevel.Debug);
        });

        // Register signaler for push notifications
        serviceCollection.AddSingleton<IGvSignalerClient, GvSignalerClient>();

        // Register SIP credential provider and SIP-over-WebSocket call transport
        serviceCollection.AddSingleton<GvSipCredentialProvider>();
        serviceCollection.AddSingleton<ICallTransport>(sp =>
            new SipWssCallTransport(
                sp.GetRequiredService<ILogger<SipWssCallTransport>>(),
                () => sp.GetRequiredService<GvSipCredentialProvider>().GetCredentialsAsync(),
                sp.GetRequiredService<ILoggerFactory>()));

        serviceCollection.AddGvClient(options =>
        {
            options.CookiePath = cookiePath;
            options.KeyPath = keyPath;
            options.ApiKey = config["GvResearch:ApiKey"] ?? string.Empty;
        });

        serviceCollection.AddSingleton<Audio.AudioEngine>();
        serviceCollection.AddSingleton<GvPhoneClient>();

        Services = serviceCollection.BuildServiceProvider();

        Console.WriteLine($"Logs: {LogFile}");

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<SoftphoneApp>()
            .UsePlatformDetect()
            .LogToTrace();
}
