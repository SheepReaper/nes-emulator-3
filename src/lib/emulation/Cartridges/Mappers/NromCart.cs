using System;

namespace Sheep.Emulation.Nes.Cartridges.Mappers;

public class NromCart(
    byte[] prgRom,
    byte[] chrRom,
    NametableMirroring nametableMirroring = NametableMirroring.Horizontal,
    bool chrWritable = true)
    : Cartridge(prgRom, chrRom, nametableMirroring, chrWritable)
{
    private readonly int _prgAddressMask = prgRom.Length - 1;
    private readonly byte[] _prgRam = new byte[0x2000];

    public override byte CpuRead(ushort address)
    {
        return address is >= 0x6000 and <= 0x7FFF
            ? _prgRam[address - 0x6000]
            : address < 0x8000
                ? (byte)0
                : _prgRom[(address - 0x8000) & _prgAddressMask];
    }

    public override void CpuWrite(ushort address, byte value)
    {
        if (address is >= 0x6000 and <= 0x7FFF) _prgRam[address - 0x6000] = value;
    }

    public override byte PpuRead(ushort address) =>
        address <= 0x1FFF ? _chrRom[address] : (byte)0;

    public override void PpuWrite(ushort address, byte value)
    {
        if (address <= 0x1FFF && IsChrWritable) _chrRom[address] = value;
    }

    internal override int CartridgeRamSize => _prgRam.Length;
    internal override void CopyCartridgeRam(int offset, Span<byte> destination) =>
        _prgRam.AsSpan(offset, destination.Length).CopyTo(destination);
    internal override void WriteCartridgeRam(int offset, ReadOnlySpan<byte> source) =>
        source.CopyTo(_prgRam.AsSpan(offset, source.Length));
}