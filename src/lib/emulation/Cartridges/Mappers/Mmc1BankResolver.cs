using System;

namespace Sheep.Emulation.Nes.Cartridges.Mappers;

/// <summary>
/// PRG and CHR bank resolution logic for MMC1 mapper.
/// </summary>
internal sealed class Mmc1BankResolver
{
    private const int PrgBankSize = 0x4000;
    private const int ChrBankSize = 0x1000;

    internal static int GetPrgBank(ushort address, int prgRomLength, byte control, byte prgBank, byte chrBank0)
    {
        var totalBanks = prgRomLength / PrgBankSize;
        var outerBank = totalBanks > 16 && (chrBank0 & 0x10) != 0 ? 16 : 0;
        var banksInBlock = Math.Min(16, totalBanks - outerBank);
        var selectedBank = outerBank + ((prgBank & 0x0F) % banksInBlock);
        var firstBank = outerBank;
        var lastBank = outerBank + banksInBlock - 1;
        var mode = (control >> 2) & 0x03;

        if (mode <= 1)
        {
            var lowerBank = outerBank + ((prgBank & 0x0E) % banksInBlock);
            return address < 0xC000 ? lowerBank : Math.Min(lowerBank + 1, lastBank);
        }

        return mode == 2 ? address < 0xC000 ? firstBank : selectedBank : address < 0xC000 ? selectedBank : lastBank;
    }

    internal static int GetChrAddress(ushort address, int chrRomLength, byte control, byte chrBank0, byte chrBank1)
    {
        var totalBanks = chrRomLength / ChrBankSize;
        int bank;
        if ((control & 0x10) == 0)
        {
            var lowerBank = (chrBank0 & 0x1E) % totalBanks;
            bank = address < 0x1000 ? lowerBank : (lowerBank + 1) % totalBanks;
        }
        else
        {
            bank = (address < 0x1000 ? chrBank0 : chrBank1) % totalBanks;
        }

        return (bank * ChrBankSize) + (address & (ChrBankSize - 1));
    }
}
