namespace Sheep.Emulation.Nes.Cartridges.Mappers;

/// <summary>
/// Manages shift register serial write protocol and register latching for MMC1.
/// </summary>
internal sealed class Mmc1ShiftRegister
{
    private byte _shift = 0x10;

    internal byte Control { get; private set; } = 0x0C;
    internal byte ChrBank0 { get; private set; }
    internal byte ChrBank1 { get; private set; }
    internal byte PrgBank { get; private set; }

    internal bool Write(ushort address, byte value, out bool controlChanged)
    {
        controlChanged = false;
        if ((value & 0x80) != 0)
        {
            _shift = 0x10;
            Control |= 0x0C;
            controlChanged = true;
            return false;
        }

        var completesWrite = (_shift & 1) != 0;
        _shift = (byte)((_shift >> 1) | ((value & 1) << 4));
        if (!completesWrite)
        {
            return false;
        }

        switch (address & 0xE000)
        {
            case 0x8000:
                Control = _shift;
                controlChanged = true;
                break;
            case 0xA000:
                ChrBank0 = _shift;
                break;
            case 0xC000:
                ChrBank1 = _shift;
                break;
            case 0xE000:
                PrgBank = _shift;
                break;
        }

        _shift = 0x10;
        return true;
    }

    internal void Reset()
    {
        _shift = 0x10;
        Control = 0x0C;
        ChrBank0 = 0;
        ChrBank1 = 0;
        PrgBank = 0;
    }
}
