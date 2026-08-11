using Xunit;

namespace Sheep.Emulation.Nes.Tests;

public sealed class CpuUnofficialAbsoluteTests : CpuTestFixture
{
    [Theory]
    [InlineData(0x0F, 0x81, 0x10, 0x20, 0x02, 0x12)]
    [InlineData(0x2F, 0x80, 0xF0, 0x21, 0x01, 0x00)]
    [InlineData(0x4F, 0x03, 0xF0, 0x20, 0x01, 0xF1)]
    [InlineData(0x6F, 0x01, 0x7F, 0x20, 0x00, 0x80)]
    [InlineData(0xCF, 0x10, 0x0F, 0x60, 0x0F, 0x0F)]
    [InlineData(0xEF, 0x7F, 0x00, 0x21, 0x80, 0x80)]
    public void UnofficialReadModifyWriteAbsolute_UsesFullAddressAndTakesSixCycles(
        byte opcode, byte memory, byte accumulator, byte status,
        byte expectedMemory, byte expectedAccumulator)
    {
        Bus.Load(0x8000, [opcode, 0x34, 0x12]);
        Bus.Write(0x1234, memory);
        SetPc(0x8000);
        SetA(accumulator);
        SetP(status);

        Assert.Equal(6UL, Cpu.Step());
        Assert.Equal(expectedMemory, Bus.Read(0x1234));
        Assert.Equal(expectedAccumulator, GetA());
        Assert.Equal(0x8003, GetPc());
    }

    [Fact]
    public void AaxAbsolute_StoresAccumulatorAndXIntersectionWithoutChangingFlags()
    {
        Bus.Load(0x8000, [0x8F, 0x34, 0x12]);
        SetPc(0x8000);
        SetA(0xCC);
        SetX(0xAA);
        SetP(0xE5);

        Assert.Equal(4UL, Cpu.Step());
        Assert.Equal(0x88, Bus.Read(0x1234));
        Assert.Equal(0xE5, GetP());
    }

    [Fact]
    public void LaxAbsolute_LoadsAccumulatorAndXAndSetsZeroAndNegative()
    {
        Bus.Load(0x8000, [0xAF, 0x34, 0x12]);
        Bus.Write(0x1234, 0x80);
        SetPc(0x8000);
        SetP(0x61);

        Assert.Equal(4UL, Cpu.Step());
        Assert.Equal(0x80, GetA());
        Assert.Equal(0x80, GetX());
        Assert.True(GetFlag('N'));
        Assert.False(GetFlag('Z'));
        Assert.True(GetFlag('C'));
        Assert.True(GetFlag('V'));
    }

    [Theory]
    [InlineData(0x1230, 4UL)]
    [InlineData(0x12FF, 5UL)]
    public void LaxAbsoluteY_AddsPageCrossCycle(ushort baseAddress, ulong expectedCycles)
    {
        Bus.Load(0x8000, [0xBF, (byte)baseAddress, (byte)(baseAddress >> 8)]);
        Bus.Write((ushort)(baseAddress + 1), 0x80);
        SetPc(0x8000);
        SetY(1);
        SetP(0x61);

        Assert.Equal(expectedCycles, Cpu.Step());
        Assert.Equal(0x80, GetA());
        Assert.Equal(0x80, GetX());
        Assert.True(GetFlag('N'));
        Assert.True(GetFlag('C'));
        Assert.True(GetFlag('V'));
    }
}
