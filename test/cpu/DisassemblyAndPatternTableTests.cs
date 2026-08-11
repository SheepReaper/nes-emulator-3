using Sheep.Emulation.Nes.Debugging;

using Xunit;

namespace Sheep.Emulation.Nes.Tests;

public sealed class DisassemblyAndPatternTableTests
{
    [Fact]
    public void DisassemblyReturnsStructuredOperandsAndUnsupportedDataBytes()
    {
        var rom = DebuggerTestHelper.CreateRom(chrBanks: 1);
        new byte[] { 0xA9, 0x42, 0x8D, 0x00, 0x20, 0xD0, 0xFC, 0x6C, 0xFF, 0x10, 0x02 }
            .CopyTo(rom, 16);
        var nes = new NesSystem();
        nes.LoadRom(rom);

        var lines = nes.Debugger.Disassemble(0x8000, 5);

        Assert.Equal("LDA", lines[0].Mnemonic);
        Assert.Equal("#$42", lines[0].Operand);
        Assert.Equal(CpuAddressingMode.Immediate, lines[0].AddressingMode);
        Assert.True(lines[0].IsCurrent);
        Assert.Equal("$2000", lines[1].Operand);
        Assert.Equal("$8003", lines[2].Operand);
        Assert.Equal("($10FF)", lines[3].Operand);
        Assert.Equal(".db", lines[4].Mnemonic);
        Assert.Equal("$02", lines[4].Operand);
    }

    [Fact]
    public void DisassemblyWrapsMappedCpuAddressesWithoutSideEffects()
    {
        var nes = new NesSystem();
        var rom = DebuggerTestHelper.CreateRom(chrBanks: 1);
        rom[16 + 0x3FFF] = 0xA9;
        nes.LoadRom(rom);
        nes.Debugger.Pause();
        nes.Debugger.WriteMemoryRegion(NesMemoryRegion.CpuRam, 0, [0x7F]);

        var lines = nes.Debugger.Disassemble(0xFFFF, 1);

        Assert.Equal(2, lines[0].Length);
        Assert.Equal(0x7F, lines[0].Bytes.Span[1]);
    }

    [Fact]
    public void PatternTableIsDecodedAsRgbaWithTheSelectedPalette()
    {
        var rom = DebuggerTestHelper.CreateRom(chrBanks: 1);
        for (var row = 0; row < 8; row++)
        {
            rom[16 + 0x4000 + row] = 0xFF;
        }
        var nes = new NesSystem();
        nes.LoadRom(rom);
        nes.Debugger.Pause();
        nes.Debugger.WriteMemoryRegion(NesMemoryRegion.PaletteRam, 1, [0x30]);

        var pattern = nes.Debugger.CapturePatternTable(0, 0);

        Assert.Equal(0, pattern.TableIndex);
        Assert.Equal(PatternTableSnapshot.Width * PatternTableSnapshot.Height * 4, pattern.Rgba.Length);
        Assert.Equal(new byte[] { 236, 238, 236, 255 }, pattern.Rgba.Span[..4].ToArray());
    }

    [Fact]
    public void SnapshotCanIncludeDisassemblyAndBothPatternTables()
    {
        var nes = new NesSystem();
        nes.LoadRom(DebuggerTestHelper.CreateRom(chrBanks: 1));
        var snapshot = nes.Debugger.CaptureSnapshot(new NesDebugSnapshotOptions
        {
            Sections = NesDebugSnapshotSections.Disassembly | NesDebugSnapshotSections.PatternTables,
            DisassemblyInstructionCount = 3,
            PatternPalette = 0
        });

        Assert.Equal(3, snapshot.Disassembly!.Count);
        Assert.Equal(2, snapshot.PatternTables!.Count);
    }
}
