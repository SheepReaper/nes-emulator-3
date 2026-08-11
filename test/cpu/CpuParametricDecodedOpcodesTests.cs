using Xunit;

namespace Sheep.Emulation.Nes.Tests;

public sealed class CpuParametricDecodedOpcodesTests : CpuTestFixture
{
    [Theory]
    [MemberData(nameof(CpuTests.DecodedOpcodes), MemberType = typeof(CpuTests))]
    public void EveryDecodedOpcode_CanExecuteToCompletion(byte opcode)
    {
        Bus.Load(0x8000, [opcode, 0x00, 0x20]);
        Bus.Write(0xFFFE, 0x00);
        Bus.Write(0xFFFF, 0x90);
        SetPc(0x8000);
        SetSp(0xFD);

        var cycles = Cpu.Step();

        Assert.InRange(cycles, 2UL, 7UL);
        Assert.Equal(0, GetCycles());
    }

    [Theory]
    [MemberData(nameof(CpuTests.IndexedReadCycleCases), MemberType = typeof(CpuTests))]
    public void IndexedReadInstructions_AddOneCycleOnlyWhenCrossingAPage(
        byte opcode, string addressingMode, bool crossesPage, ulong expectedCycles)
    {
        SetPc(0x8000);
        SetA(0x55);
        SetX(1);
        SetY(1);

        var lowByte = crossesPage ? (byte)0xFF : (byte)0x10;
        if (addressingMode == "IndirectY")
        {
            Bus.Load(0x8000, [opcode, 0x40]);
            Bus.Write(0x0040, lowByte);
            Bus.Write(0x0041, 0x20);
        }
        else
        {
            Bus.Load(0x8000, [opcode, lowByte, 0x20]);
        }

        Assert.Equal(expectedCycles, Cpu.Step());
        Assert.Equal(0, GetCycles());
    }

    [Theory]
    [InlineData(0x9D, 5)]
    [InlineData(0x99, 5)]
    [InlineData(0x91, 6)]
    public void IndexedStoreInstructions_HaveFixedCyclesAcrossPageBoundaries(byte opcode, ulong expectedCycles)
    {
        SetPc(0x8000);
        SetA(0x42);
        SetX(1);
        SetY(1);

        if (opcode == 0x91)
        {
            Bus.Load(0x8000, [opcode, 0x40]);
            Bus.Write(0x0040, 0xFF);
            Bus.Write(0x0041, 0x20);
        }
        else
        {
            Bus.Load(0x8000, [opcode, 0xFF, 0x20]);
        }

        Assert.Equal(expectedCycles, Cpu.Step());
        Assert.Equal(0x42, Bus.Read(0x2100));
    }

    [Fact]
    public void LDA_AbsoluteX_AddsCycleWhenEffectiveAddressCrossesPageBoundary()
    {
        Bus.Load(0x8000, [0xBD, 0xFF, 0x20]);
        Bus.Write(0x2100, 0x42);
        SetPc(0x8000);
        SetX(1);

        var cycles = Cpu.Step();

        Assert.Equal(5UL, cycles);
        Assert.Equal(0x42, GetA());
        Assert.Equal(0, GetCycles());
    }
}
