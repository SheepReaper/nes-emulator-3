using System;

namespace Sheep.Emulation.Nes.Cartridges.Mappers;

/// <summary>Implements iNES mapper 3 (CNROM).</summary>
public sealed class CnromCart : Cartridge
{
    private const int ChrBankSize = 0x2000;
    private readonly byte[] _prgRam = new byte[0x2000];
    private int _chrBank;

    public CnromCart(byte[] prgRom, byte[] chrRom, NametableMirroring mirroring)
        : base(prgRom, chrRom, mirroring, false)
    {
        if (prgRom.Length is not (0x4000 or 0x8000))
            throw new ArgumentException("CNROM PRG ROM must be 16 KB or 32 KB.", nameof(prgRom));
        if (chrRom.Length < ChrBankSize || chrRom.Length % ChrBankSize != 0)
            throw new ArgumentException("CNROM CHR ROM must contain complete 8 KB banks.", nameof(chrRom));
    }

    public override byte CpuRead(ushort address) => address switch
    {
        >= 0x6000 and <= 0x7FFF => _prgRam[address - 0x6000],
        >= 0x8000 => _prgRom[(address - 0x8000) % _prgRom.Length],
        _ => 0
    };

    public override void CpuWrite(ushort address, byte value)
    {
        if (address is >= 0x6000 and <= 0x7FFF)
            _prgRam[address - 0x6000] = value;
        else if (address >= 0x8000)
            _chrBank = (value & CpuRead(address)) % (_chrRom.Length / ChrBankSize);
    }

    public override byte PpuRead(ushort address) =>
        address <= 0x1FFF ? _chrRom[_chrBank * ChrBankSize + address] : (byte)0;

    public override void PpuWrite(ushort address, byte value) { }

    internal override void Reset() => _chrBank = 0;
    internal override int CartridgeRamSize => _prgRam.Length;
    internal override void CopyCartridgeRam(int offset, Span<byte> destination) =>
        _prgRam.AsSpan(offset, destination.Length).CopyTo(destination);
    internal override void WriteCartridgeRam(int offset, ReadOnlySpan<byte> source) =>
        source.CopyTo(_prgRam.AsSpan(offset, source.Length));
}
