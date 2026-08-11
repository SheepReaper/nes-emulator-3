namespace Sheep.Emulation.Nes.Cartridges.Mappers;

/// <summary>Implements the 46 KB unbanked NROM-368 extension.</summary>
public sealed class Nrom368Cart(
    byte[] prgRom,
    byte[] chr,
    NametableMirroring mirroring,
    bool chrWritable)
    : Cartridge(prgRom, chr, mirroring, chrWritable)
{
    internal override bool CpuReadDrivesDataBus(ushort address) => address >= 0x4800;

    public override byte CpuRead(ushort address) => address >= 0x4800
        ? _prgRom[address - 0x4000]
        : (byte)0;

    public override void CpuWrite(ushort address, byte value) { }

    public override byte PpuRead(ushort address) => address <= 0x1FFF ? _chrRom[address] : (byte)0;

    public override void PpuWrite(ushort address, byte value)
    {
        if (address <= 0x1FFF && IsChrWritable) _chrRom[address] = value;
    }
}
