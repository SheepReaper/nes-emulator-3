using Xunit;

namespace Sheep.Emulation.Nes.Tests;

public sealed class CpuStoreAndLoadIndexTests : CpuTestFixture
{
    [Theory]
    [InlineData(0x85, 'A', 0x42, new byte[] { 0x80 }, 0, 0, 0x0080, 3)]
    [InlineData(0x95, 'A', 0x42, new byte[] { 0x80 }, 0x10, 0, 0x0090, 4)]
    [InlineData(0x8D, 'A', 0x42, new byte[] { 0x34, 0x12 }, 0, 0, 0x1234, 4)]
    [InlineData(0x9D, 'A', 0x42, new byte[] { 0x34, 0x12 }, 0x10, 0, 0x1244, 5)]
    [InlineData(0x86, 'X', 0x42, new byte[] { 0x80 }, 0x42, 0, 0x0080, 3)]
    [InlineData(0x96, 'X', 0x42, new byte[] { 0x80 }, 0x42, 0x10, 0x0090, 4)]
    [InlineData(0x8C, 'Y', 0x42, new byte[] { 0x34, 0x12 }, 0, 0x42, 0x1234, 4)]
    public void StoreInstructions_WriteToMemoryCorrectly(
        byte opcode, char register, byte valueToStore, byte[] operands, byte initialX, byte initialY,
        ushort expectedAddress, int expectedCycles)
    {
        var instruction = new byte[] { opcode }.Concat(operands).ToArray();
        Bus.Load(0x8000, instruction);
        SetPc(0x8000);
        SetP(0b11111111);

        switch (register)
        {
            case 'A': SetA(valueToStore); break;
            case 'X': SetX(valueToStore); break;
            case 'Y': SetY(valueToStore); break;
        }
        SetX(initialX);
        SetY(initialY);

        Clock(expectedCycles);

        Assert.Equal(valueToStore, Bus.Read(expectedAddress));
        Assert.Equal(0, GetCycles());
        Assert.Equal(0b11111111, GetP());
    }

    [Theory]
    [InlineData(0xA2, 'X', new byte[] { 0x80 }, 0, 0, 0, 2)]
    [InlineData(0xA6, 'X', new byte[] { 0x42 }, 0, 0, 0x0042, 3)]
    [InlineData(0xB6, 'X', new byte[] { 0x42 }, 0, 1, 0x0043, 4)]
    [InlineData(0xAE, 'X', new byte[] { 0x34, 0x12 }, 0, 0, 0x1234, 4)]
    [InlineData(0xBE, 'X', new byte[] { 0x34, 0x12 }, 0, 1, 0x1235, 4)]
    [InlineData(0xA0, 'Y', new byte[] { 0x80 }, 0, 0, 0, 2)]
    [InlineData(0xA4, 'Y', new byte[] { 0x42 }, 0, 0, 0x0042, 3)]
    [InlineData(0xB4, 'Y', new byte[] { 0x42 }, 1, 0, 0x0043, 4)]
    [InlineData(0xAC, 'Y', new byte[] { 0x34, 0x12 }, 0, 0, 0x1234, 4)]
    [InlineData(0xBC, 'Y', new byte[] { 0x34, 0x12 }, 1, 0, 0x1235, 4)]
    public void LoadIndexInstructions_LoadEveryAddressingModeAndSetFlags(
        byte opcode, char register, byte[] operands, byte initialX, byte initialY,
        ushort effectiveAddress, ulong expectedCycles)
    {
        Bus.Load(0x8000, new byte[] { opcode }.Concat(operands).ToArray());
        if (effectiveAddress != 0) Bus.Write(effectiveAddress, 0x80);
        SetPc(0x8000);
        SetX(initialX);
        SetY(initialY);
        SetP(0);

        Assert.Equal(expectedCycles, Cpu.Step());
        Assert.Equal(0x80, register == 'X' ? GetX() : GetY());
        Assert.True(GetFlag('N'));
        Assert.False(GetFlag('Z'));
    }

    [Theory]
    [InlineData(0xBE, 'X')]
    [InlineData(0xBC, 'Y')]
    public void LoadIndexAbsoluteIndexed_AddsCycleWhenCrossingPage(byte opcode, char register)
    {
        Bus.Load(0x8000, [opcode, 0xFF, 0x20]);
        Bus.Write(0x2100, 0x42);
        SetPc(0x8000);
        SetX(1);
        SetY(1);

        Assert.Equal(5UL, Cpu.Step());
        Assert.Equal(0x42, register == 'X' ? GetX() : GetY());
    }

    [Theory]
    [InlineData(0xA2, 'X')]
    [InlineData(0xA0, 'Y')]
    public void LoadIndexImmediate_SetsZeroFlagForZero(byte opcode, char register)
    {
        Bus.Load(0x8000, [opcode, 0x00]);
        SetPc(0x8000);
        SetP(0);

        Assert.Equal(2UL, Cpu.Step());
        Assert.Equal(0, register == 'X' ? GetX() : GetY());
        Assert.True(GetFlag('Z'));
        Assert.False(GetFlag('N'));
    }
}
