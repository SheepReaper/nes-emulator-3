using Xunit;

namespace Sheep.Emulation.Nes.Tests;

public sealed class CpuUnofficialZeroPageXAndYTests : CpuTestFixture
{
    [Fact]
    public void AaxZeroPage_StoresAccumulatorAndXIntersectionWithoutChangingFlags()
    {
        PrepareUnofficialZeroPage(0x87, 0, accumulator: 0xCC, x: 0xAA, status: 0xE5);
        Assert.Equal(3UL, Cpu.Step());
        Assert.Equal(0x88, Bus.Read(0x0042));
        Assert.Equal(0xE5, GetP());
    }

    [Fact]
    public void LaxZeroPage_LoadsAccumulatorAndXAndSetsZeroAndNegative()
    {
        PrepareUnofficialZeroPage(0xA7, 0x80, status: 0x61);
        Assert.Equal(3UL, Cpu.Step());
        Assert.Equal(0x80, GetA());
        Assert.Equal(0x80, GetX());
        Assert.True(GetFlag('N'));
        Assert.False(GetFlag('Z'));
        Assert.True(GetFlag('C'));
        Assert.True(GetFlag('V'));
    }

    [Fact]
    public void DcpZeroPage_DecrementsMemoryThenComparesAccumulator()
    {
        PrepareUnofficialZeroPage(0xC7, 0x10, accumulator: 0x0F, status: 0x60);
        Assert.Equal(5UL, Cpu.Step());
        Assert.Equal(0x0F, Bus.Read(0x0042));
        Assert.True(GetFlag('C'));
        Assert.True(GetFlag('Z'));
        Assert.True(GetFlag('V'));
    }

    [Fact]
    public void IscZeroPage_IncrementsMemoryThenSubtractsWithCarry()
    {
        PrepareUnofficialZeroPage(0xE7, 0x7F, accumulator: 0, status: 0x21);
        Assert.Equal(5UL, Cpu.Step());
        Assert.Equal(0x80, Bus.Read(0x0042));
        Assert.Equal(0x80, GetA());
        Assert.False(GetFlag('C'));
        Assert.True(GetFlag('V'));
        Assert.True(GetFlag('N'));
    }

    [Theory]
    [InlineData(0x17, 0x81, 0x10, 0x20, 0x02, 0x12)]
    [InlineData(0x37, 0x80, 0xF0, 0x21, 0x01, 0x00)]
    [InlineData(0x57, 0x03, 0xF0, 0x20, 0x01, 0xF1)]
    [InlineData(0x77, 0x01, 0x7F, 0x20, 0x00, 0x80)]
    [InlineData(0xD7, 0x10, 0x0F, 0x60, 0x0F, 0x0F)]
    [InlineData(0xF7, 0x7F, 0x00, 0x21, 0x80, 0x80)]
    public void UnofficialReadModifyWriteZeroPageX_WrapsAndTakesSixCycles(
        byte opcode, byte memory, byte accumulator, byte status,
        byte expectedMemory, byte expectedAccumulator)
    {
        Bus.Load(0x8000, [opcode, 0xFE]);
        Bus.Write(0x0003, memory);
        SetPc(0x8000);
        SetA(accumulator);
        SetX(5);
        SetP(status);

        Assert.Equal(6UL, Cpu.Step());
        Assert.Equal(expectedMemory, Bus.Read(0x0003));
        Assert.Equal(expectedAccumulator, GetA());
        Assert.Equal(0x8002, GetPc());
    }

    private void PrepareUnofficialZeroPage(
        byte opcode, byte memory, byte accumulator = 0, byte x = 0, byte status = 0x20)
    {
        Bus.Load(0x8000, [opcode, 0x42]);
        Bus.Write(0x0042, memory);
        SetPc(0x8000);
        SetA(accumulator);
        SetX(x);
        SetP(status);
    }
}
