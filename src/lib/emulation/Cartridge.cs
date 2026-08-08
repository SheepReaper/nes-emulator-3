using System;

namespace SR.Emulation.Nes;

public abstract class Cartridge(
    byte[] prgRom,
    byte[] chrRom,
    NametableMirroring nametableMirroring = NametableMirroring.Horizontal,
    bool chrWritable = true)
{
    protected readonly byte[] _prgRom = prgRom;
    protected readonly byte[] _chrRom = chrRom;

    public NametableMirroring NametableMirroring { get; protected set; } = nametableMirroring;
    public bool IsChrWritable { get; } = chrWritable;

    public abstract byte CpuRead(ushort address);

    public abstract void CpuWrite(ushort address, byte value);
    public abstract byte PpuRead(ushort address);

    public abstract void PpuWrite(ushort address, byte value);
    public virtual byte CpuPeek(ushort address) => CpuRead(address);
    public virtual byte PpuPeek(ushort address) => PpuRead(address);
    internal virtual void NotifyPpuAddress(ushort address, ulong ppuCycle) { }
    internal virtual void NotifyCpuClock() { }
    internal virtual void Reset() { }

    internal int PrgRomSize => _prgRom.Length;
    internal int ChrSize => _chrRom.Length;
    internal virtual int CartridgeRamSize => 0;
    internal void CopyPrgRom(int offset, Span<byte> destination) => _prgRom.AsSpan(offset, destination.Length).CopyTo(destination);
    internal void CopyChr(int offset, Span<byte> destination) => _chrRom.AsSpan(offset, destination.Length).CopyTo(destination);
    internal void WriteChr(int offset, ReadOnlySpan<byte> source)
    {
        if (!IsChrWritable) throw new InvalidOperationException("CHR ROM is read-only.");
        source.CopyTo(_chrRom.AsSpan(offset, source.Length));
    }
    internal virtual void CopyCartridgeRam(int offset, Span<byte> destination) { }
    internal virtual void WriteCartridgeRam(int offset, ReadOnlySpan<byte> source) { }
}
