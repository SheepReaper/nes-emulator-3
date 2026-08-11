namespace Sheep.Emulation.Nes.Cartridges.Mappers;

/// <summary>
/// PRG and CHR address mapping calculations for MMC3 mapper.
/// </summary>
internal static class Mmc3BankResolver
{
    private const int PrgBankSize = 0x2000;
    private const int ChrBankSize = 0x0400;

    internal static int GetPrgBank(int slot, int prgRomLength, byte bankSelect, byte[] bankRegisters)
    {
        var lastBank = (prgRomLength / PrgBankSize) - 1;
        var secondLastBank = lastBank - 1;
        var prgMode = (bankSelect & 0x40) != 0;
        var bank = slot switch
        {
            0 => prgMode ? secondLastBank : bankRegisters[6] & 0x3F,
            1 => bankRegisters[7] & 0x3F,
            2 => prgMode ? bankRegisters[6] & 0x3F : secondLastBank,
            _ => lastBank
        };
        return bank % (lastBank + 1);
    }

    internal static int GetChrAddress(ushort address, int chrRomLength, byte bankSelect, byte[] bankRegisters)
    {
        var slot = address / ChrBankSize;
        var inverted = (bankSelect & 0x80) != 0;
        var bank = (inverted, slot) switch
        {
            (false, 0) => bankRegisters[0] & 0xFE,
            (false, 1) => bankRegisters[0] | 0x01,
            (false, 2) => bankRegisters[1] & 0xFE,
            (false, 3) => bankRegisters[1] | 0x01,
            (false, 4) => bankRegisters[2],
            (false, 5) => bankRegisters[3],
            (false, 6) => bankRegisters[4],
            (false, _) => bankRegisters[5],
            (true, 0) => bankRegisters[2],
            (true, 1) => bankRegisters[3],
            (true, 2) => bankRegisters[4],
            (true, 3) => bankRegisters[5],
            (true, 4) => bankRegisters[0] & 0xFE,
            (true, 5) => bankRegisters[0] | 0x01,
            (true, 6) => bankRegisters[1] & 0xFE,
            (true, _) => bankRegisters[1] | 0x01
        };
        bank %= chrRomLength / ChrBankSize;
        return (bank * ChrBankSize) + (address & (ChrBankSize - 1));
    }
}
