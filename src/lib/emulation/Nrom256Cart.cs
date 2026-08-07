using System;

namespace SR.Emulation.Nes;

public sealed class Nrom256Cart : Cartridge
{
    public Nrom256Cart(byte[] prgRom, byte[] chrRom) : base(prgRom, chrRom)
    {
    }

    public override byte CpuRead(ushort address)
    {
        return address switch
        {
            >= 0x8000 and <= 0xFFFF => _prgRom[address - 0x8000],
            _ => 0
        };
    }

    public override byte PpuRead(ushort address)
    {
        return address switch
        {
            >= 0x0000 and <= 0x1FFF => _chrRom[address],
            _ => 0
        };
    }

    public override void CpuWrite(ushort address, byte value)
    {
        Action write = address switch
        {
            // NROM is typically PRG RAM or read-only.
            _ => () => { }
        };

        write();
    }

    public override void PpuWrite(ushort address, byte value)
    {
        Action write = address switch
        {
            >= 0x0000 and <= 0x1FFF => () => _chrRom[address] = value, // CHR-RAM
            _ => () => { }
        };

        write();
    }
}
