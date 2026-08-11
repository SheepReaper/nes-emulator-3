using Xunit;

namespace Sheep.Emulation.Nes.Tests;

public sealed class CpuUnofficialAbsoluteIndexedTests : CpuTestFixture
{
    [Theory]
    [InlineData(0x1F, true, 0x81, 0x10, 0x20, 0x02, 0x12)]
    [InlineData(0x3F, true, 0x80, 0xF0, 0x21, 0x01, 0x00)]
    [InlineData(0x5F, true, 0x03, 0xF0, 0x20, 0x01, 0xF1)]
    [InlineData(0x7F, true, 0x01, 0x7F, 0x20, 0x00, 0x80)]
    [InlineData(0xDF, true, 0x10, 0x0F, 0x60, 0x0F, 0x0F)]
    [InlineData(0xFF, true, 0x7F, 0x00, 0x21, 0x80, 0x80)]
    [InlineData(0x1B, false, 0x81, 0x10, 0x20, 0x02, 0x12)]
    [InlineData(0x3B, false, 0x80, 0xF0, 0x21, 0x01, 0x00)]
    [InlineData(0x5B, false, 0x03, 0xF0, 0x20, 0x01, 0xF1)]
    [InlineData(0x7B, false, 0x01, 0x7F, 0x20, 0x00, 0x80)]
    [InlineData(0xDB, false, 0x10, 0x0F, 0x60, 0x0F, 0x0F)]
    [InlineData(0xFB, false, 0x7F, 0x00, 0x21, 0x80, 0x80)]
    public void UnofficialReadModifyWriteAbsoluteIndexed_UsesSelectedIndexAndTakesSevenCycles(
        byte opcode, bool usesX, byte memory, byte accumulator, byte status,
        byte expectedMemory, byte expectedAccumulator)
    {
        Bus.Load(0x8000, [opcode, 0x30, 0x12]);
        Bus.Write(0x1235, memory);
        SetPc(0x8000);
        SetA(accumulator);
        SetX(usesX ? (byte)5 : (byte)1);
        SetY(usesX ? (byte)1 : (byte)5);
        SetP(status);

        Assert.Equal(7UL, Cpu.Step());
        Assert.Equal(expectedMemory, Bus.Read(0x1235));
        Assert.Equal(expectedAccumulator, GetA());
        Assert.Equal(0x8003, GetPc());
    }

    [Theory]
    [InlineData(0x9C)]
    [InlineData(0x9E)]
    public void ShyAndShxAbsoluteIndexed_ReplaceAddressHighByteOnPageCross(byte opcode)
    {
        Bus.Load(0x8000, [opcode, 0xFF, 0x12]);
        Bus.Write(0x1301, 0x55);
        SetPc(0x8000);
        SetX(2);
        SetY(2);
        SetP(0xE5);

        Assert.Equal(5UL, Cpu.Step());
        Assert.Equal(0x02, Bus.Read(0x0201));
        Assert.Equal(0x55, Bus.Read(0x1301));
        Assert.Equal(0xE5, GetP());
    }

    [Theory]
    [InlineData(0x9C)]
    [InlineData(0x9E)]
    public void ShyAndShxAbsoluteIndexed_StoreAtEffectiveAddressWithoutPageCross(byte opcode)
    {
        Bus.Load(0x8000, [opcode, 0x34, 0x12]);
        SetPc(0x8000);
        SetX(2);
        SetY(2);

        Assert.Equal(5UL, Cpu.Step());
        Assert.Equal(0x02, Bus.Read(0x1236));
    }
}
