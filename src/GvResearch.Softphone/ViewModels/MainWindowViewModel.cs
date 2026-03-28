using CommunityToolkit.Mvvm.ComponentModel;
using GvResearch.Softphone.Phone;

namespace GvResearch.Softphone.ViewModels;

/// <summary>Root ViewModel that manages navigation between the dialer and active-call views.</summary>
public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly GvPhoneClient? _phoneClient;

    [ObservableProperty]
    private bool _isInCall;

    [ObservableProperty]
    private string _statusText = "Ready";

    public DialerViewModel Dialer { get; }
    public ActiveCallViewModel ActiveCall { get; }

    /// <summary>Parameterless constructor for Avalonia design-time preview and unit tests.</summary>
    public MainWindowViewModel() : this(null) { }

    public MainWindowViewModel(GvPhoneClient? phoneClient)
    {
        _phoneClient = phoneClient;

        Dialer = new DialerViewModel();
        ActiveCall = new ActiveCallViewModel();

        Dialer.CallRequested += OnCallRequested;
        ActiveCall.HangupRequested += OnHangupRequested;

        if (_phoneClient is not null)
        {
            _phoneClient.StatusChanged += (_, e) => StatusText = e.Value;
            _phoneClient.IncomingCallReceived += (_, e) =>
            {
                IsInCall = true;
                ActiveCall.StartCall(e.Value);
            };
            _phoneClient.CallEnded += (_, _) =>
            {
                IsInCall = false;
                StatusText = "Ready";
            };
        }
    }

    private async void OnCallRequested(object? sender, CallRequestedEventArgs e)
    {
        IsInCall = true;
        ActiveCall.StartCall(e.Number);

        if (_phoneClient is not null)
        {
            var success = await _phoneClient.CallAsync(e.Number).ConfigureAwait(false);
            if (success)
                ActiveCall.CallConnected();
            else
                IsInCall = false;
        }
        else
        {
            StatusText = $"Calling {e.Number}";
        }
    }

    private async void OnHangupRequested(object? sender, EventArgs e)
    {
        if (_phoneClient is not null)
            await _phoneClient.HangupAsync().ConfigureAwait(false);

        IsInCall = false;
        StatusText = "Ready";
    }
}
