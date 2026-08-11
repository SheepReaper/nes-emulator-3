using System;

namespace Sheep.Emulation.Nes.Cartridges.Mappers;

/// <summary>
/// PRG RAM (64 KB) and ExRAM (1 KB) storage and access for MMC5 mapper.
/// </summary>
internal sealed class Mmc5Ram
{
    private const int PrgBankSize = 0x2000;
    private readonly byte[] _prgRam = new byte[0x10000];
    private readonly byte[] _exRam = new byte[0x0400];

    internal byte[] ExRam => _exRam;
    internal int CartridgeRamSize => _prgRam.Length;

    internal byte ReadExRam(ushort address) => _exRam[address - 0x5C00];
    internal void WriteExRam(ushort address, byte value) => _exRam[address - 0x5C00] = value;

    internal byte ReadBank(int bank, ushort address) =>
        _prgRam[(bank * PrgBankSize) + (address & 0x1FFF)];

    internal void WriteBank(int bank, ushort address, byte value) =>
        _prgRam[(bank * PrgBankSize) + (address & 0x1FFF)] = value;

    internal void CopyCartridgeRam(int offset, Span<byte> destination) =>
        _prgRam.AsSpan(offset, destination.Length).CopyTo(destination);

    internal void WriteCartridgeRam(int offset, ReadOnlySpan<byte> source) =>
        source.CopyTo(_prgRam.AsSpan(offset, source.Length));
}
