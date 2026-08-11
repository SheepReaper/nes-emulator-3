using Xunit;

namespace Sheep.Emulation.Nes.Tests;

public sealed class CpuUnofficialIndirectTests : CpuTestFixture
{
    [Theory]
    [InlineData(0x03, 0x81, 0x10, 0x20, 0x02, 0x12)]
    [InlineData(0x23, 0x80, 0xF0, 0x21, 0x01, 0x00)]
    [InlineData(0x43, 0x03, 0xF0, 0x20, 0x01, 0xF1)]
    [InlineData(0x63, 0x01, 0x7F, 0x20, 0x00, 0x80)]
    [InlineData(0xC3, 0x10, 0x0F, 0x60, 0x0F, 0x0F)]
    [InlineData(0xE3, 0x7F, 0x00, 0x21, 0x80, 0x80)]
    public void UnofficialReadModifyWriteIndexedIndirect_WrapsPointerAndTakesEightCycles(
        byte opcode, byte memory, byte accumulator, byte status,
        byte expectedMemory, byte expectedAccumulator)
    {
        PrepareIndexedIndirect(opcode, memory, accumulator, status);

        Assert.Equal(8UL, Cpu.Step());
        Assert.Equal(expectedMemory, Bus.Read(0x1234));
        Assert.Equal(expectedAccumulator, GetA());
        Assert.Equal(0x8002, GetPc());
    }

    [Fact]
    public void AaxIndexedIndirect_StoresAccumulatorAndXIntersectionWithoutChangingFlags()
    {
        PrepareIndexedIndirect(0x83, 0, accumulator: 0x03, status: 0xE5);

        Assert.Equal(6UL, Cpu.Step());
        Assert.Equal(0x02, Bus.Read(0x1234));
        Assert.Equal(0xE5, GetP());
    }

    [Fact]
    public void LaxIndexedIndirect_LoadsAccumulatorAndXAndSetsZeroAndNegative()
    {
        PrepareIndexedIndirect(0xA3, 0x80, status: 0x61);

        Assert.Equal(6UL, Cpu.Step());
        Assert.Equal(0x80, GetA());
        Assert.Equal(0x80, GetX());
        Assert.True(GetFlag('N'));
        Assert.False(GetFlag('Z'));
        Assert.True(GetFlag('C'));
        Assert.True(GetFlag('V'));
    }

    private void PrepareIndexedIndirect(byte opcode, byte memory, byte accumulator = 0, byte status = 0x20)
    {
        Bus.Load(0x8000, [opcode, 0xFD]);
        Bus.Write(0x00FF, 0x34);
        Bus.Write(0x0000, 0x12);
        Bus.Write(0x0100, 0x99);
        Bus.Write(0x1234, memory);
        SetPc(0x8000);
        SetA(accumulator);
        SetX(2);
        SetP(status);
    }

    [Theory]
    [InlineData(0x13, 0x81, 0x10, 0x20, 0x02, 0x12)]
    [InlineData(0x33, 0x80, 0xF0, 0x21, 0x01, 0x00)]
    [InlineData(0x53, 0x03, 0xF0, 0x20, 0x01, 0xF1)]
    [InlineData(0x73, 0x01, 0x7F, 0x20, 0x00, 0x80)]
    public void UnofficialReadModifyWriteIndirectIndexed_WrapsPointerAndAlwaysTakesEightCycles(
        byte opcode, byte memory, byte accumulator, byte status,
        byte expectedMemory, byte expectedAccumulator)
    {
        Bus.Load(0x8000, [opcode, 0xFF]);
        Bus.Write(0x00FF, 0xFF);
        Bus.Write(0x0000, 0x12);
        Bus.Write(0x0100, 0x99);
        Bus.Write(0x1301, memory);
        SetPc(0x8000);
        SetA(accumulator);
        SetY(2);
        SetP(status);

        Assert.Equal(8UL, Cpu.Step());
        Assert.Equal(expectedMemory, Bus.Read(0x1301));
        Assert.Equal(expectedAccumulator, GetA());
        Assert.Equal(0x8002, GetPc());
    }
}
