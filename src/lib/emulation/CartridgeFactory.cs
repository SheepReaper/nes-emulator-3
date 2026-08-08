using System;
using System.Linq;

namespace SR.Emulation.Nes;

public sealed class CartridgeFactory(InterruptLines? interrupts = null)
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
        var flags7 = header[7];

        var nametableMirroring = (flags6 & 0x08) != 0
            ? NametableMirroring.FourScreen
            : (flags6 & 0x01) != 0
                ? NametableMirroring.Vertical
                : NametableMirroring.Horizontal;

        var mapperNumber = (flags6 >> 4) | (flags7 & 0xF0);

        var prgRomSize = prgRomSizeIn16Kb * 0x4000; // 16KB
        var chrRomSize = chrRomSizeIn8Kb * 0x2000; // 8KB

        var dataOffset = 16 + ((flags6 & 0x04) != 0 ? 512 : 0);
        var requiredLength = dataOffset + prgRomSize + chrRomSize;
        if (romData.Length < requiredLength)
            throw new ArgumentException("Invalid ROM data: File is shorter than the sizes declared in its header.", nameof(romData));

        var prgRom = romData.AsSpan(dataOffset, prgRomSize).ToArray();
        var hasChrRam = chrRomSize == 0;
        var chrRom = hasChrRam
            ? new byte[0x2000]
            : romData.AsSpan(dataOffset + prgRomSize, chrRomSize).ToArray();

        if (mapperNumber == 1)
            return new Mmc1Cart(prgRom, chrRom, nametableMirroring, hasChrRam);
        if (mapperNumber == 4)
            return new Mmc3Cart(prgRom, chrRom, nametableMirroring, hasChrRam, interrupts ?? new InterruptLines());
        if (mapperNumber != 0)
            throw new NotSupportedException($"Mapper {mapperNumber} is not supported yet.");

        return prgRomSizeIn16Kb switch
        {
            1 or 2 => new NromCart(prgRom, chrRom, nametableMirroring, hasChrRam),
            _ => throw new NotSupportedException($"NROM with {prgRomSizeIn16Kb} * 16KB PRG-ROM is not supported.")
        };
    }
}
