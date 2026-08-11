using System;

namespace Sheep.Emulation.Nes.Cartridges.Mappers;

/// <summary>
/// Bank selection and register latch storage for MMC3 mapper.
/// </summary>
internal sealed class Mmc3Registers
{
    internal readonly byte[] BankRegisters = new byte[8];
    internal byte BankSelect;

    internal void WriteBankSelect(byte value) => BankSelect = value;
    internal void WriteBankData(byte value) => BankRegisters[BankSelect & 0x07] = value;

    internal void Reset()
    {
        Array.Clear(BankRegisters, 0, BankRegisters.Length);
        BankSelect = 0;
    }
}
