using CommunityToolkit.Mvvm.ComponentModel;

namespace GvResearch.Softphone.ViewModels;

/// <summary>Root ViewModel that manages navigation between the dialer and active-call views.</summary>
public sealed partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isInCall;

    [ObservableProperty]
    private string _statusText = "Ready";

    public DialerViewModel Dialer { get; } = new DialerViewModel();
    public ActiveCallViewModel ActiveCall { get; } = new ActiveCallViewModel();

    public MainWindowViewModel()
    {
        Dialer.CallRequested += OnCallRequested;
        ActiveCall.HangupRequested += OnHangupRequested;
    }

    private void OnCallRequested(object? sender, CallRequestedEventArgs e)
    {
        IsInCall = true;
        StatusText = $"Calling {e.Number}";
        ActiveCall.StartCall(e.Number);
    }

    private void OnHangupRequested(object? sender, EventArgs e)
    {
        IsInCall = false;
        StatusText = "Ready";
    }
}
