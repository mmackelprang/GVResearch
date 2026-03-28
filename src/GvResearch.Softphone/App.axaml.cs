using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using GvResearch.Softphone.Phone;
using GvResearch.Softphone.ViewModels;
using GvResearch.Softphone.Views;
using Microsoft.Extensions.DependencyInjection;

namespace GvResearch.Softphone;

public sealed class SoftphoneApp : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var phoneClient = Program.Services?.GetService<GvPhoneClient>();
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(phoneClient)
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
