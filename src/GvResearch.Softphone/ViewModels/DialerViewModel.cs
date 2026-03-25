using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace GvResearch.Softphone.ViewModels;

/// <summary>Event args carrying the number to call.</summary>
public sealed class CallRequestedEventArgs : EventArgs
{
    public CallRequestedEventArgs(string number)
    {
        Number = number;
    }

    public string Number { get; }
}

/// <summary>ViewModel for the dial pad. Handles digit entry and initiating calls.</summary>
public sealed partial class DialerViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CallCommand))]
    private string _dialNumber = "";

    /// <summary>Raised when the user requests a call. Contains the number to dial.</summary>
    public event EventHandler<CallRequestedEventArgs>? CallRequested;

    [RelayCommand]
    private void DialDigit(string digit)
    {
        DialNumber += digit;
    }

    [RelayCommand]
    private void Clear()
    {
        DialNumber = "";
    }

    [RelayCommand]
    private void Backspace()
    {
        if (DialNumber.Length > 0)
        {
            DialNumber = DialNumber[..^1];
        }
    }

    [RelayCommand(CanExecute = nameof(CanCall))]
    private void Call()
    {
        CallRequested?.Invoke(this, new CallRequestedEventArgs(DialNumber));
    }

    private bool CanCall() => !string.IsNullOrEmpty(DialNumber);
}
