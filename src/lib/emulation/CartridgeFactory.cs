using System;
using System.Linq;

namespace SR.Emulation.Nes;

public sealed class CartridgeFactory
{
    private static readonly byte[] INES_HEADER_CONSTANT = { 0x4E, 0x45, 0x53, 0x1A }; // "NES" + MS-DOS EOF

    public Cartridge Create(byte[] romData)
    {
        if (romData.Length < 16)
        {
            throw new ArgumentException("Invalid ROM data: Too short to contain a header.", nameof(romData));
        }

        var header = romData.AsSpan(0, 16);
        var magicNumber = header[0..4];

        if (!magicNumber.SequenceEqual(INES_HEADER_CONSTANT))
        {
            throw new ArgumentException("Invalid ROM data: iNES header magic number not found.", nameof(romData));
        }

        var prgRomSizeIn16Kb = header[4];
        var chrRomSizeIn8Kb = header[5];
        var flags6 = header[6];

        var mapperNumber = (flags6 >> 4); // For now, we only consider the lower nibble

        if (mapperNumber != 0)
        {
            throw new NotSupportedException($"Mapper {mapperNumber} is not supported yet.");
        }

        var prgRomSize = prgRomSizeIn16Kb * 0x4000; // 16KB
        var chrRomSize = chrRomSizeIn8Kb * 0x2000; // 8KB

        var prgRom = romData.AsSpan(16, prgRomSize).ToArray();
        var chrRom = romData.AsSpan(16 + prgRomSize, chrRomSize).ToArray();

        // Based on Mapper 0 (NROM) variants
        return prgRomSizeIn16Kb switch
        {
            1 => new Nrom128Cart(prgRom, chrRom), // 16KB PRG-ROM
            2 => new Nrom256Cart(prgRom, chrRom), // 32KB PRG-ROM
            _ => throw new NotSupportedException($"NROM with {prgRomSizeIn16Kb} * 16KB PRG-ROM is not supported.")
        };
    }
}