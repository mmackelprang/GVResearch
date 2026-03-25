using Avalonia;
using GvResearch.Softphone;

AppBuilder.Configure<SoftphoneApp>()
    .UsePlatformDetect()
    .LogToTrace()
    .StartWithClassicDesktopLifetime(args);
