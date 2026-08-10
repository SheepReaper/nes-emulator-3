using SR.Emulation.Nes;

namespace EmuSheep;

internal enum ControllerKey
{
    A,
    B,
    Select,
    Start,
    Up,
    Down,
    Left,
    Right
}

internal sealed class KeyboardControllerState
{
    public NesControllerButton Buttons { get; private set; }

    public bool SetPressed(ControllerKey key, bool pressed)
    {
        var button = key switch
        {
            ControllerKey.A => NesControllerButton.A,
            ControllerKey.B => NesControllerButton.B,
            ControllerKey.Select => NesControllerButton.Select,
            ControllerKey.Start => NesControllerButton.Start,
            ControllerKey.Up => NesControllerButton.Up,
            ControllerKey.Down => NesControllerButton.Down,
            ControllerKey.Left => NesControllerButton.Left,
            ControllerKey.Right => NesControllerButton.Right,
            _ => throw new ArgumentOutOfRangeException(nameof(key))
        };
        var updated = pressed ? Buttons | button : Buttons & ~button;
        if (updated == Buttons) return false;
        Buttons = updated;
        return true;
    }

    public bool Clear()
    {
        if (Buttons == NesControllerButton.None) return false;
        Buttons = NesControllerButton.None;
        return true;
    }
}
