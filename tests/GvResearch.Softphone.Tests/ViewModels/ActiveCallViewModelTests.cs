using FluentAssertions;
using GvResearch.Softphone.ViewModels;

namespace GvResearch.Softphone.Tests.ViewModels;

public sealed class ActiveCallViewModelTests : IDisposable
{
    private readonly ActiveCallViewModel _vm = new ActiveCallViewModel();

    public void Dispose() => _vm.Dispose();

    [Fact]
    public void StartCall_SetsRemoteNumber()
    {
        _vm.StartCall("555-1234");

        _vm.RemoteNumber.Should().Be("555-1234");
    }

    [Fact]
    public void StartCall_SetsCallingStatus()
    {
        _vm.StartCall("555-1234");

        _vm.CallStatus.Should().Contain("Calling");
    }

    [Fact]
    public void StartCall_ResetsDurationToZero()
    {
        _vm.StartCall("555-1234");

        _vm.CallDuration.Should().Be("00:00");
    }

    [Fact]
    public void CallConnected_UpdatesStatus()
    {
        _vm.StartCall("555-1234");

        _vm.CallConnected();

        _vm.CallStatus.Should().Be("Connected");
    }

    [Fact]
    public void ToggleMute_FlipsMuteState()
    {
        _vm.IsMuted.Should().BeFalse();

        _vm.ToggleMuteCommand.Execute(null);

        _vm.IsMuted.Should().BeTrue();
    }

    [Fact]
    public void ToggleMute_Twice_ReturnsMuteStateToFalse()
    {
        _vm.ToggleMuteCommand.Execute(null);
        _vm.ToggleMuteCommand.Execute(null);

        _vm.IsMuted.Should().BeFalse();
    }

    [Fact]
    public void MuteButtonText_IsMute_WhenNotMuted()
    {
        _vm.IsMuted.Should().BeFalse();

        _vm.MuteButtonText.Should().Be("Mute");
    }

    [Fact]
    public void MuteButtonText_IsUnmute_WhenMuted()
    {
        _vm.ToggleMuteCommand.Execute(null);

        _vm.MuteButtonText.Should().Be("Unmute");
    }

    [Fact]
    public void Hangup_SetsEndedStatus()
    {
        _vm.StartCall("555-1234");

        _vm.HangupCommand.Execute(null);

        _vm.CallStatus.Should().Be("Ended");
    }

    [Fact]
    public void Hangup_RaisesHangupRequestedEvent()
    {
        _vm.StartCall("555-1234");
        var eventRaised = false;
        _vm.HangupRequested += (_, _) => eventRaised = true;

        _vm.HangupCommand.Execute(null);

        eventRaised.Should().BeTrue();
    }
}
