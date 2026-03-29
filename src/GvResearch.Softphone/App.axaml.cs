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
            var vm = new MainWindowViewModel(phoneClient);
            desktop.MainWindow = new MainWindow
            {
                DataContext = vm
            };

            // Auto-dial if a phone number was passed as CLI argument
            if (Program.AutoDialNumber is not null)
            {
                vm.Dialer.DialNumber = Program.AutoDialNumber;
                // Trigger the call after the UI is loaded
                desktop.MainWindow.Opened += (_, _) => vm.Dialer.CallCommand.Execute(null);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
