namespace Sheep.Emulation.Nes.Cartridges.Mappers;

/// <summary>
/// PRG and CHR banking control registers for MMC5 mapper.
/// </summary>
internal sealed class Mmc5Registers
{
    internal readonly byte[] PrgBanks = new byte[5];
    internal readonly ushort[] SpriteChrBanks = new ushort[8];
    internal readonly ushort[] BackgroundChrBanks = new ushort[4];
    internal byte PrgMode = 3;
    internal byte ChrMode;
    internal byte PrgProtect1;
    internal byte PrgProtect2;
    internal byte ExRamMode;
    internal byte NametableMapping;
    internal byte FillTile;
    internal byte FillAttribute;
    internal byte ChrUpperBits;
    internal bool UseBackgroundChrBanks;

    internal bool PrgRamWritable => PrgProtect1 == 2 && PrgProtect2 == 1;

    internal void Write(ushort address, byte value)
    {
        switch (address)
        {
            case 0x5100:
                PrgMode = (byte)(value & 3);
                break;
            case 0x5101:
                ChrMode = (byte)(value & 3);
                break;
            case 0x5102:
                PrgProtect1 = (byte)(value & 3);
                break;
            case 0x5103:
                PrgProtect2 = (byte)(value & 3);
                break;
            case 0x5104:
                ExRamMode = (byte)(value & 3);
                break;
            case 0x5105:
                NametableMapping = value;
                break;
            case 0x5106:
                FillTile = value;
                break;
            case 0x5107:
                FillAttribute = (byte)((value & 3) * 0x55);
                break;
            case >= 0x5113 and <= 0x5117:
                PrgBanks[address - 0x5113] = value;
                break;
            case >= 0x5120 and <= 0x5127:
                SpriteChrBanks[address - 0x5120] = (ushort)((ChrUpperBits << 8) | value);
                UseBackgroundChrBanks = false;
                break;
            case >= 0x5128 and <= 0x512B:
                BackgroundChrBanks[address - 0x5128] = (ushort)((ChrUpperBits << 8) | value);
                UseBackgroundChrBanks = true;
                break;
            case 0x5130:
                ChrUpperBits = (byte)(value & 3);
                break;
        }
    }

    internal void Reset()
    {
        PrgMode = 3;
        PrgBanks[4] = 0xFF;
    }
}
