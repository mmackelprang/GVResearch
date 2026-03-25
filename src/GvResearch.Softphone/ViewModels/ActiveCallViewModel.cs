using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace GvResearch.Softphone.ViewModels;

public sealed partial class ActiveCallViewModel : ObservableObject, IDisposable
{
    private System.Timers.Timer? _durationTimer;
    private int _elapsedSeconds;
    private bool _disposed;

    [ObservableProperty]
    private string _remoteNumber = "";

    [ObservableProperty]
    private string _callDuration = "00:00";

    [ObservableProperty]
    private string _callStatus = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MuteButtonText))]
    private bool _isMuted;

    public string MuteButtonText => IsMuted ? "Unmute" : "Mute";

    public event EventHandler? HangupRequested;

    public void StartCall(string number)
    {
        RemoteNumber = number;
        CallStatus = "Calling…";
        CallDuration = "00:00";
        _elapsedSeconds = 0;

        _durationTimer?.Dispose();
        _durationTimer = new System.Timers.Timer(1000);
        _durationTimer.Elapsed += OnTimerElapsed;
        _durationTimer.AutoReset = true;
        _durationTimer.Start();
    }

    public void CallConnected()
    {
        CallStatus = "Connected";
    }

    private void OnTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        _elapsedSeconds++;
        var minutes = _elapsedSeconds / 60;
        var seconds = _elapsedSeconds % 60;
        CallDuration = $"{minutes:D2}:{seconds:D2}";
    }

    [RelayCommand]
    private void Hangup()
    {
        CallStatus = "Ended";
        StopTimer();
        HangupRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void ToggleMute()
    {
        IsMuted = !IsMuted;
    }

    private void StopTimer()
    {
        if (_durationTimer is not null)
        {
            _durationTimer.Stop();
            _durationTimer.Dispose();
            _durationTimer = null;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            StopTimer();
            _disposed = true;
        }
    }
}
