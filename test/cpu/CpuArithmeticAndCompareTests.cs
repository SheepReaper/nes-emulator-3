using Xunit;

namespace Sheep.Emulation.Nes.Tests;

public sealed class CpuArithmeticAndCompareTests : CpuTestFixture
{
    [Theory]
    [InlineData(0x69, 0x10, 0x20, false, 0x30, false, false, false, false)]
    [InlineData(0x69, 0x00, 0x00, false, 0x00, true, false, false, false)]
    [InlineData(0x69, 0xFF, 0x01, false, 0x00, true, true, false, false)]
    [InlineData(0x69, 0x7F, 0x01, false, 0x80, false, false, true, true)]
    [InlineData(0x69, 0x80, 0xFF, false, 0x7F, false, true, true, false)]
    [InlineData(0x69, 0x10, 0x20, true, 0x31, false, false, false, false)]
    [InlineData(0xE9, 0x05, 0x03, true, 0x02, false, true, false, false)]
    [InlineData(0xE9, 0x03, 0x05, true, 0xFE, false, false, false, true)]
    [InlineData(0xE9, 0x00, 0x01, true, 0xFF, false, false, false, true)]
    [InlineData(0xE9, 0x80, 0x01, false, 0x7E, false, true, true, false)]
    public void ArithmeticInstructions_SetFlagsCorrectly(
        byte opcode, byte initialA, byte operand, bool initialCarry,
        byte expectedA, bool expectedZ, bool expectedC, bool expectedV, bool expectedN)
    {
        Bus.Load(0x8000, [opcode, operand]);
        SetPc(0x8000);
        SetA(initialA);
        SetP(0);
        SetFlag('C', initialCarry);

        Clock(2);

        Assert.Equal(expectedA, GetA());
        Assert.Equal(expectedZ, GetFlag('Z'));
        Assert.Equal(expectedC, GetFlag('C'));
        Assert.Equal(expectedV, GetFlag('V'));
        Assert.Equal(expectedN, GetFlag('N'));
    }

    [Theory]
    [InlineData(0xC9, 'A', 0x42, 0x42, true, true, false)]
    [InlineData(0xC9, 'A', 0x43, 0x42, false, true, false)]
    [InlineData(0xC9, 'A', 0x42, 0x43, false, false, true)]
    [InlineData(0xE0, 'X', 0x80, 0x80, true, true, false)]
    [InlineData(0xC0, 'Y', 0x10, 0x20, false, false, true)]
    public void CompareInstructions_SetFlagsCorrectly(
        byte opcode, char register, byte registerValue, byte operand,
        bool expectedZ, bool expectedC, bool expectedN)
    {
        Bus.Load(0x8000, [opcode, operand]);
        SetPc(0x8000);
        SetP(0);
        switch (register)
        {
            case 'A': SetA(registerValue); break;
            case 'X': SetX(registerValue); break;
            case 'Y': SetY(registerValue); break;
        }

        Clock(2);

        Assert.Equal(expectedZ, GetFlag('Z'));
        Assert.Equal(expectedC, GetFlag('C'));
        Assert.Equal(expectedN, GetFlag('N'));
    }

    [Theory]
    [InlineData(0xE4, 'X', 3)]
    [InlineData(0xEC, 'X', 4)]
    [InlineData(0xC4, 'Y', 3)]
    [InlineData(0xCC, 'Y', 4)]
    public void CompareIndexInstructions_ReadMemoryVariants(byte opcode, char register, ulong expectedCycles)
    {
        var absolute = expectedCycles == 4;
        Bus.Load(0x8000, absolute ? [opcode, 0x34, 0x12] : [opcode, 0x42]);
        Bus.Write(absolute ? (ushort)0x1234 : (ushort)0x0042, 0x40);
        SetPc(0x8000);
        if (register == 'X') SetX(0x40); else SetY(0x40);

        Assert.Equal(expectedCycles, Cpu.Step());
        Assert.True(GetFlag('Z'));
        Assert.True(GetFlag('C'));
        Assert.False(GetFlag('N'));
    }

    [Fact]
    public void DecimalFlagDoesNotChangeAdcBehaviorOnNesCpu()
    {
        Bus.Load(0x8000, [0x69, 0x01]);
        SetPc(0x8000);
        SetA(0x09);
        SetP(0);
        SetFlag('D', true);

        Assert.Equal(2UL, Cpu.Step());
        Assert.Equal(0x0A, GetA());
        Assert.True(GetFlag('D'));
    }
}
