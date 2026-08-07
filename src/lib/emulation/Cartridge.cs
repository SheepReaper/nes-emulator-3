namespace SR.Emulation.Nes;

public abstract class Cartridge(byte[] prgRom, byte[] chrRom)
{
    protected readonly byte[] _prgRom = prgRom;
    protected readonly byte[] _chrRom = chrRom;

    public abstract byte CpuRead(ushort address);

    public abstract void CpuWrite(ushort address, byte value);
    public abstract byte PpuRead(ushort address);

    public abstract void PpuWrite(ushort address, byte value);
}
