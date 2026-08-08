using System;

namespace SR.Emulation.Nes;

public class NromCart(
    byte[] prgRom,
    byte[] chrRom,
    NametableMirroring nametableMirroring = NametableMirroring.Horizontal,
    bool chrWritable = true)
    : Cartridge(prgRom, chrRom, nametableMirroring, chrWritable)
{
    private readonly int _prgAddressMask = prgRom.Length - 1;

    public override byte CpuRead(ushort address)
    {
        if (address < 0x8000) return 0;
        return _prgRom[(address - 0x8000) & _prgAddressMask];
    }

    public override void CpuWrite(ushort address, byte value)
    {
    }

    public override byte PpuRead(ushort address) =>
        address <= 0x1FFF ? _chrRom[address] : (byte)0;

    public override void PpuWrite(ushort address, byte value)
    {
        if (address <= 0x1FFF && IsChrWritable) _chrRom[address] = value;
    }
}
