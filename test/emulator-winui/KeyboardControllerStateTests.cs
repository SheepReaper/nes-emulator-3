using Xunit;
using SR.Emulation.Nes;

namespace EmuSheep.Tests;

public sealed class KeyboardControllerStateTests
{
    [Fact]
    public void PressingAndReleasingKeysMaintainsCombinedButtonState()
    {
        var keyboard = new KeyboardControllerState();

        Assert.True(keyboard.SetPressed(ControllerKey.Left, true));
        Assert.True(keyboard.SetPressed(ControllerKey.A, true));
        Assert.Equal(NesControllerButton.Left | NesControllerButton.A, keyboard.Buttons);

        Assert.True(keyboard.SetPressed(ControllerKey.Left, false));
        Assert.Equal(NesControllerButton.A, keyboard.Buttons);
    }

    [Fact]
    public void RepeatedKeyStateDoesNotReportAChange()
    {
        var keyboard = new KeyboardControllerState();

        Assert.True(keyboard.SetPressed(ControllerKey.Start, true));
        Assert.False(keyboard.SetPressed(ControllerKey.Start, true));
    }

    [Fact]
    public void ClearReleasesEveryButton()
    {
        var keyboard = new KeyboardControllerState();
        keyboard.SetPressed(ControllerKey.Down, true);
        keyboard.SetPressed(ControllerKey.B, true);

        Assert.True(keyboard.Clear());
        Assert.Equal(NesControllerButton.None, keyboard.Buttons);
        Assert.False(keyboard.Clear());
    }
}
