namespace Sheep.Emulation.Nes.Cartridges.Mappers;

/// <summary>
/// PRG and CHR address and bank calculations for MMC5 mapper.
/// </summary>
internal static class Mmc5BankResolver
{
    private const int PrgBankSize = 0x2000;

    internal static (int Bank, bool IsRom) GetPrgMapping(ushort address, byte prgMode, byte[] prgBanks)
    {
        var slot = (address - 0x8000) / PrgBankSize;
        var register = prgMode switch
        {
            0 => 4,
            1 => slot < 2 ? 2 : 4,
            2 => slot < 2 ? 2 : slot + 1,
            _ => slot + 1
        };
        var sizeIn8K = prgMode switch
        {
            0 => 4,
            1 => 2,
            2 when slot < 2 => 2,
            _ => 1
        };
        var value = prgBanks[register];
        var baseBank = value & 0x7F & ~(sizeIn8K - 1);
        return (baseBank + (slot & (sizeIn8K - 1)), register == 4 || (value & 0x80) != 0);
    }

    internal static int GetChrAddress(
        ushort address,
        byte chrMode,
        bool useBackgroundChrBanks,
        ushort[] backgroundChrBanks,
        ushort[] spriteChrBanks)
    {
        var bankSize = 0x2000 >> chrMode;
        var slot = address / bankSize;
        if (useBackgroundChrBanks)
        {
            var halfAddress = address & 0x0FFF;
            var halfSlot = halfAddress / bankSize;
            var register = chrMode switch
            {
                0 => 3,
                1 => 3,
                2 => halfSlot * 2 + 1,
                _ => halfSlot
            };
            var bgBank = backgroundChrBanks[register];
            return bgBank * bankSize + (halfAddress & (bankSize - 1));
        }

        var spriteRegister = chrMode switch
        {
            0 => 7,
            1 => slot * 4 + 3,
            2 => slot * 2 + 1,
            _ => slot
        };
        var spBank = spriteChrBanks[spriteRegister];
        return spBank * bankSize + (address & (bankSize - 1));
    }
}
