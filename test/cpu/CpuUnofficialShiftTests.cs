using Xunit;

namespace Sheep.Emulation.Nes.Tests;

public sealed class CpuUnofficialShiftTests : CpuTestFixture
{
    [Fact]
    public void AsrImmediate_AndsThenLogicallyShiftsAccumulator()
    {
        Bus.Load(0x8000, [0x4B, 0x0F]);
        SetPc(0x8000);
        SetA(0xFF);
        SetP(0x60);

        Assert.Equal(2UL, Cpu.Step());
        Assert.Equal(0x07, GetA());
        Assert.True(GetFlag('C'));
        Assert.False(GetFlag('N'));
        Assert.False(GetFlag('Z'));
        Assert.True(GetFlag('V'));
    }

    [Theory]
    [InlineData(0xFF, 0x00, false, false, false, false)]
    [InlineData(0xFF, 0x40, false, false, true, false)]
    [InlineData(0xFF, 0x80, true, true, true, true)]
    public void ArrImmediate_UsesRotatedBitsSixAndFiveForCarryAndOverflow(
        byte accumulator, byte operand, bool initialCarry,
        bool expectedCarry, bool expectedOverflow, bool expectedNegative)
    {
        Bus.Load(0x8000, [0x6B, operand]);
        SetPc(0x8000);
        SetA(accumulator);
        SetP((byte)(0x20 | (initialCarry ? 1 : 0)));

        Assert.Equal(2UL, Cpu.Step());
        Assert.Equal((byte)(((accumulator & operand) >> 1) | (initialCarry ? 0x80 : 0)), GetA());
        Assert.Equal(expectedCarry, GetFlag('C'));
        Assert.Equal(expectedOverflow, GetFlag('V'));
        Assert.Equal(expectedNegative, GetFlag('N'));
    }

    [Fact]
    public void AtxImmediate_UsesNes2A03MagicConstantAndLoadsAccumulatorAndX()
    {
        Bus.Load(0x8000, [0xAB, 0xF3]);
        SetPc(0x8000);
        SetA(0x01);
        SetX(0x55);
        SetP(0x61);

        Assert.Equal(2UL, Cpu.Step());
        Assert.Equal(0xF3, GetA());
        Assert.Equal(0xF3, GetX());
        Assert.True(GetFlag('N'));
        Assert.False(GetFlag('Z'));
        Assert.True(GetFlag('C'));
        Assert.True(GetFlag('V'));
    }
}
