namespace Sheep.Emulation.Nes.Video;

/// <summary>
/// Loopy scrolling registers (v, t, x, w) and address calculation for PPU.
/// </summary>
internal sealed class PpuScrollRegisters
{
    internal ushort VramAddress { get; set; }
    internal ushort TempVramAddress { get; set; }
    internal byte FineXScroll { get; set; }
    internal bool WriteToggle { get; set; }

    internal void Reset()
    {
        VramAddress = 0;
        TempVramAddress = 0;
        FineXScroll = 0;
        WriteToggle = false;
    }

    internal void WriteControl(byte value)
    {
        TempVramAddress = (ushort)((TempVramAddress & 0xF3FF) | ((value & 0x03) << 10));
    }

    internal void WriteScroll(byte value)
    {
        if (!WriteToggle)
        {
            FineXScroll = (byte)(value & 0x07);
            TempVramAddress = (ushort)((TempVramAddress & 0xFFE0) | (value >> 3));
        }
        else
        {
            TempVramAddress = (ushort)((TempVramAddress & 0x8FFF) | ((value & 0x07) << 12));
            TempVramAddress = (ushort)((TempVramAddress & 0xFC1F) | ((value & 0xF8) << 2));
        }
        WriteToggle = !WriteToggle;
    }

    internal bool WriteAddress(byte value)
    {
        if (!WriteToggle)
        {
            TempVramAddress = (ushort)((TempVramAddress & 0x00FF) | ((value & 0x3F) << 8));
        }
        else
        {
            TempVramAddress = (ushort)((TempVramAddress & 0xFF00) | value);
            VramAddress = TempVramAddress;
        }

        var completed = WriteToggle;
        WriteToggle = !WriteToggle;
        return completed;
    }

    internal void IncrementHorizontal()
    {
        if ((VramAddress & 0x001F) == 31)
        {
            VramAddress &= 0xFFE0;
            VramAddress ^= 0x0400;
        }
        else
        {
            VramAddress++;
        }
    }

    internal void IncrementVertical()
    {
        if ((VramAddress & 0x7000) != 0x7000)
        {
            VramAddress += 0x1000;
            return;
        }

        VramAddress &= 0x8FFF;
        var coarseY = (VramAddress & 0x03E0) >> 5;
        if (coarseY == 29)
        {
            coarseY = 0;
            VramAddress ^= 0x0800;
        }
        else if (coarseY == 31)
        {
            coarseY = 0;
        }
        else
        {
            coarseY++;
        }
        VramAddress = (ushort)((VramAddress & 0xFC1F) | (coarseY << 5));
    }

    internal void CopyHorizontal() =>
        VramAddress = (ushort)((VramAddress & 0xFBE0) | (TempVramAddress & 0x041F));

    internal void CopyVertical() =>
        VramAddress = (ushort)((VramAddress & 0x841F) | (TempVramAddress & 0x7BE0));
}
