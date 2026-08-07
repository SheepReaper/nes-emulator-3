using System;

namespace SR.Emulation.Nes;

public sealed class Nrom128Cart(byte[] prgRom, byte[] chrRom) : Cartridge(prgRom, chrRom)
{
    public override byte CpuRead(ushort address)
    {
        return address switch
        {
            >= 0x8000 and <= 0xBFFF => _prgRom[address - 0x8000],
            >= 0xC000 and <= 0xFFFF => _prgRom[address - 0xC000], // Mirrored
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
            // NROM is typically PRG RAM or read-only, but we'll allow writes for now.
            // In a real scenario, this might do nothing or write to RAM if present.
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
