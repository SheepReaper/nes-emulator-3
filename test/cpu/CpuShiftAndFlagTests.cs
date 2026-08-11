using Xunit;

namespace Sheep.Emulation.Nes.Tests;

public sealed class CpuShiftAndFlagTests : CpuTestFixture
{
    [Theory]
    [InlineData(0xE8, 'X', 0x41, 0x42, false, false)]
    [InlineData(0xE8, 'X', 0xFF, 0x00, true, false)]
    [InlineData(0xCA, 'X', 0x00, 0xFF, false, true)]
    [InlineData(0xC8, 'Y', 0x80, 0x81, false, true)]
    [InlineData(0x88, 'Y', 0x01, 0x00, true, false)]
    public void IncrementDecrementRegister_SetsFlagsCorrectly(
        byte opcode, char register, byte initialValue, byte expectedValue, bool expectedZ, bool expectedN)
    {
        Bus.Load(0x8000, [opcode]);
        SetPc(0x8000);
        SetP(0);
        if (register == 'X') SetX(initialValue);
        else SetY(initialValue);

        Clock(2);

        var finalValue = register == 'X' ? GetX() : GetY();
        Assert.Equal(expectedValue, finalValue);
        Assert.Equal(expectedZ, GetFlag('Z'));
        Assert.Equal(expectedN, GetFlag('N'));
    }

    [Theory]
    [InlineData(0xE6, 0x41, 0x42, 5, false, false)]
    [InlineData(0xC6, 0x00, 0xFF, 5, false, true)]
    public void IncrementDecrementMemory_SetsFlagsCorrectly(
        byte opcode, byte initialValue, byte expectedValue, int expectedCycles, bool expectedZ, bool expectedN)
    {
        Bus.Load(0x8000, [opcode, 0x42]);
        Bus.Write(0x0042, initialValue);
        SetPc(0x8000);
        SetP(0);

        Clock(expectedCycles);

        Assert.Equal(expectedValue, Bus.Read(0x0042));
        Assert.Equal(expectedZ, GetFlag('Z'));
        Assert.Equal(expectedN, GetFlag('N'));
    }

    [Theory]
    [InlineData(0x0A, 'A', 0x42, false, 0x84, false, false, true, 2)]
    [InlineData(0x0A, 'A', 0x80, false, 0x00, true, true, false, 2)]
    [InlineData(0x06, 'M', 0x01, false, 0x02, false, false, false, 5)]
    [InlineData(0x4A, 'A', 0x01, false, 0x00, true, true, false, 2)]
    [InlineData(0x4A, 'A', 0x84, false, 0x42, false, false, false, 2)]
    [InlineData(0x2A, 'A', 0x80, false, 0x00, true, true, false, 2)]
    [InlineData(0x2A, 'A', 0x01, true, 0x03, false, false, false, 2)]
    [InlineData(0x6A, 'A', 0x01, false, 0x00, true, true, false, 2)]
    [InlineData(0x6A, 'A', 0x00, true, 0x80, false, false, true, 2)]
    public void ShiftAndRotateInstructions_SetFlagsCorrectly(
        byte opcode, char mode, byte initialValue, bool initialCarry,
        byte expectedValue, bool expectedC, bool expectedZ, bool expectedN, int expectedCycles)
    {
        SetPc(0x8000);
        SetP(0);
        SetFlag('C', initialCarry);

        if (mode == 'A')
        {
            Bus.Load(0x8000, [opcode]);
            SetA(initialValue);
        }
        else
        {
            Bus.Load(0x8000, [opcode, 0x42]);
            Bus.Write(0x0042, initialValue);
        }

        Clock(expectedCycles);

        var finalValue = (mode == 'A') ? GetA() : Bus.Read(0x0042);
        Assert.Equal(expectedValue, finalValue);
        Assert.Equal(expectedC, GetFlag('C'));
        Assert.Equal(expectedZ, GetFlag('Z'));
        Assert.Equal(expectedN, GetFlag('N'));
    }

    [Theory]
    [InlineData(0x18, 'C', false, true)]
    [InlineData(0x38, 'C', true, false)]
    [InlineData(0x58, 'I', false, true)]
    [InlineData(0x78, 'I', true, false)]
    [InlineData(0xB8, 'V', false, true)]
    [InlineData(0xD8, 'D', false, true)]
    [InlineData(0xF8, 'D', true, false)]
    public void FlagInstructions_SetFlagsCorrectly(byte opcode, char flag, bool expectedValue, bool initialValue)
    {
        Bus.Load(0x8000, [opcode]);
        SetPc(0x8000);
        SetP(0);
        SetFlag(flag, initialValue);

        Clock(2);

        Assert.Equal(expectedValue, GetFlag(flag));
    }
}
