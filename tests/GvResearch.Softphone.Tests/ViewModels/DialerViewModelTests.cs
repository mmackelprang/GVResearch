using FluentAssertions;
using GvResearch.Softphone.ViewModels;

namespace GvResearch.Softphone.Tests.ViewModels;

public sealed class DialerViewModelTests
{
    [Fact]
    public void DialDigit_AppendsDigitToNumber()
    {
        var vm = new DialerViewModel();

        vm.DialDigitCommand.Execute("5");

        vm.DialNumber.Should().Be("5");
    }

    [Fact]
    public void DialDigit_MultipleDigits_AppendsAll()
    {
        var vm = new DialerViewModel();

        vm.DialDigitCommand.Execute("1");
        vm.DialDigitCommand.Execute("2");
        vm.DialDigitCommand.Execute("3");

        vm.DialNumber.Should().Be("123");
    }

    [Fact]
    public void Clear_ResetsNumberToEmpty()
    {
        var vm = new DialerViewModel();
        vm.DialDigitCommand.Execute("4");
        vm.DialDigitCommand.Execute("2");

        vm.ClearCommand.Execute(null);

        vm.DialNumber.Should().BeEmpty();
    }

    [Fact]
    public void Backspace_RemovesLastDigit()
    {
        var vm = new DialerViewModel();
        vm.DialDigitCommand.Execute("9");
        vm.DialDigitCommand.Execute("8");

        vm.BackspaceCommand.Execute(null);

        vm.DialNumber.Should().Be("9");
    }

    [Fact]
    public void Backspace_OnEmptyNumber_DoesNotThrow()
    {
        var vm = new DialerViewModel();

        var act = () => vm.BackspaceCommand.Execute(null);

        act.Should().NotThrow();
        vm.DialNumber.Should().BeEmpty();
    }

    [Fact]
    public void CanCall_IsFalse_WhenNumberIsEmpty()
    {
        var vm = new DialerViewModel();

        vm.CallCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void CanCall_IsTrue_WhenNumberIsNonEmpty()
    {
        var vm = new DialerViewModel();
        vm.DialDigitCommand.Execute("7");

        vm.CallCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void Call_RaisesCallRequestedEvent_WithCurrentNumber()
    {
        var vm = new DialerViewModel();
        vm.DialDigitCommand.Execute("5");
        vm.DialDigitCommand.Execute("5");
        vm.DialDigitCommand.Execute("5");

        CallRequestedEventArgs? receivedArgs = null;
        vm.CallRequested += (_, args) => receivedArgs = args;
        vm.CallCommand.Execute(null);

        receivedArgs.Should().NotBeNull();
        receivedArgs!.Number.Should().Be("555");
    }

    [Fact]
    public void CanCall_BecomesFalse_AfterClear()
    {
        var vm = new DialerViewModel();
        vm.DialDigitCommand.Execute("1");
        vm.CallCommand.CanExecute(null).Should().BeTrue();

        vm.ClearCommand.Execute(null);

        vm.CallCommand.CanExecute(null).Should().BeFalse();
    }
}
