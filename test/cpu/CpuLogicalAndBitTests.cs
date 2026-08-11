using Xunit;

namespace Sheep.Emulation.Nes.Tests;

public sealed class CpuLogicalAndBitTests : CpuTestFixture
{
    [Fact]
    public void LDA_Immediate_LoadsAccumulatorAndSetsFlags()
    {
        Bus.Load(0x8000, [0xA9, 0x42]);
        SetPc(0x8000);
        SetP(0);

        Clock(2);

        Assert.Equal(0x42, GetA());
        Assert.False(GetFlag('Z'));
        Assert.False(GetFlag('N'));
    }

    [Fact]
    public void LDA_Immediate_SetsZeroFlag()
    {
        Bus.Load(0x8000, [0xA9, 0x00]);
        SetPc(0x8000);
        SetP(0);

        Clock(2);

        Assert.Equal(0x00, GetA());
        Assert.True(GetFlag('Z'));
        Assert.False(GetFlag('N'));
    }

    [Theory]
    [InlineData(0x29, 0b11001100, 0b10101010, 0b10001000, false, true)]
    [InlineData(0x29, 0b00110011, 0b11001100, 0b00000000, true, false)]
    [InlineData(0x49, 0b10101010, 0b10101010, 0b00000000, true, false)]
    [InlineData(0x49, 0b01010101, 0b10101010, 0b11111111, false, true)]
    [InlineData(0x09, 0b11000000, 0b00001100, 0b11001100, false, true)]
    [InlineData(0x09, 0b00000000, 0b00000000, 0b00000000, true, false)]
    public void LogicalInstructions_SetFlagsCorrectly(
        byte opcode, byte initialA, byte operand,
        byte expectedA, bool expectedZ, bool expectedN)
    {
        Bus.Load(0x8000, [opcode, operand]);
        SetPc(0x8000);
        SetA(initialA);
        SetP(0);

        Clock(2);

        Assert.Equal(expectedA, GetA());
        Assert.Equal(expectedZ, GetFlag('Z'));
        Assert.Equal(expectedN, GetFlag('N'));
    }

    [Theory]
    [InlineData(0xAA, 0x55, true, true, false)]
    [InlineData(0x55, 0xAA, true, false, true)]
    [InlineData(0x0F, 0xF0, true, true, true)]
    [InlineData(0xFF, 0x01, false, false, false)]
    public void BitInstruction_SetsFlagsCorrectly(
        byte initialA, byte operand, bool expectedZ, bool expectedV, bool expectedN)
    {
        Bus.Load(0x8000, [0x24, 0x42]);
        Bus.Write(0x0042, operand);
        SetPc(0x8000);
        SetA(initialA);

        Clock(4);

        Assert.Equal(expectedZ, GetFlag('Z'));
        Assert.Equal(expectedV, GetFlag('V'));
        Assert.Equal(expectedN, GetFlag('N'));
    }

    [Fact]
    public void BIT_Absolute_ReadsOperandAndSetsAllResultFlags()
    {
        Bus.Load(0x8000, [0x2C, 0x34, 0x12]);
        Bus.Write(0x1234, 0xC0);
        SetPc(0x8000);
        SetA(0x0F);

        Assert.Equal(4UL, Cpu.Step());
        Assert.True(GetFlag('Z'));
        Assert.True(GetFlag('V'));
        Assert.True(GetFlag('N'));
    }
}
