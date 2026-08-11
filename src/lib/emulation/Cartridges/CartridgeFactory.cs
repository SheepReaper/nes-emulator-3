using System;
using System.Linq;

namespace Sheep.Emulation.Nes.Cartridges;

public sealed class CartridgeFactory(InterruptLines? interrupts = null)
{
    private static readonly byte[] INES_HEADER_CONSTANT = [0x4E, 0x45, 0x53, 0x1A]; // "NES" + MS-DOS EOF

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
            ? new byte[mapperNumber == 28 ? 0x8000 : 0x2000]
            : romData.AsSpan(dataOffset + prgRomSize, chrRomSize).ToArray();

        return mapperNumber switch
        {
            0 when prgRomSizeIn16Kb is 1 or 2 => new NromCart(prgRom, chrRom, nametableMirroring, hasChrRam),
            0 when prgRomSizeIn16Kb == 3 => new Nrom368Cart(prgRom, chrRom, nametableMirroring, hasChrRam),
            0 => throw new NotSupportedException($"NROM with {prgRomSizeIn16Kb} * 16KB PRG-ROM is not supported."),
            1 => new Mmc1Cart(prgRom, chrRom, nametableMirroring, hasChrRam),
            2 => new UxromCart(prgRom, chrRom, nametableMirroring, hasChrRam),
            3 when !hasChrRam => new CnromCart(prgRom, chrRom, nametableMirroring),
            3 => throw new NotSupportedException("CNROM requires CHR-ROM; this image declares CHR-RAM."),
            4 => new Mmc3Cart(prgRom, chrRom, nametableMirroring, hasChrRam, interrupts ?? new InterruptLines()),
            5 => new Mmc5Cart(prgRom, chrRom, nametableMirroring, hasChrRam, interrupts ?? new InterruptLines()),
            7 when hasChrRam => new AxromCart(prgRom, chrRom),
            7 => throw new NotSupportedException("AxROM requires CHR-RAM; this image declares CHR-ROM."),
            11 when !hasChrRam => new ColorDreamsCart(prgRom, chrRom, nametableMirroring),
            11 => throw new NotSupportedException("Color Dreams requires CHR-ROM; this image declares CHR-RAM."),
            22 when !hasChrRam => new Vrc2aCart(prgRom, chrRom, nametableMirroring),
            22 => throw new NotSupportedException("VRC2a requires CHR-ROM; this image declares CHR-RAM."),
            28 when hasChrRam => new Action53Cart(prgRom, chrRom),
            28 => throw new NotSupportedException("Action 53 requires CHR-RAM; this image declares CHR-ROM."),
            34 when hasChrRam || chrRom.Length <= 0x2000 => new BnromCart(prgRom, chrRom, nametableMirroring, hasChrRam),
            34 when chrRom.Length > 0x2000 => new Nina001Cart(prgRom, chrRom, nametableMirroring),
            34 => throw new NotSupportedException("Mapper 34 requires CHR-RAM, up to 8 KB of unbanked CHR-ROM for BNROM, or banked CHR-ROM for NINA-001."),
            _ => throw new NotSupportedException($"Mapper {mapperNumber} is not supported yet.")
        };
    }
}