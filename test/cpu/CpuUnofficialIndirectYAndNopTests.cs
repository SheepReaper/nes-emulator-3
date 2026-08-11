using Xunit;

namespace Sheep.Emulation.Nes.Tests;

public sealed class CpuUnofficialIndirectYAndNopTests : CpuTestFixture
{
    [Theory]
    [InlineData(false, 5UL)]
    [InlineData(true, 6UL)]
    public void LAX_IndirectIndexed_LoadsAccumulatorAndX_WithReadPageCrossTiming(
        bool crossesPage, ulong expectedCycles)
    {
        SetPc(0x8000);
        SetY(1);
        Bus.Load(0x8000, [0xB3, 0x40]);
        Bus.Write(0x0040, crossesPage ? (byte)0xFF : (byte)0x33);
        Bus.Write(0x0041, 0x12);
        Bus.Write(crossesPage ? (ushort)0x1300 : (ushort)0x1234, 0x80);

        Assert.Equal(expectedCycles, Cpu.Step());
        Assert.Equal(0x80, GetA());
        Assert.Equal(0x80, GetX());
        Assert.True(GetFlag('N'));
        Assert.False(GetFlag('Z'));
    }

    [Theory]
    [InlineData(0xD3, 0x40, 0x3F, 0x40, true)]
    [InlineData(0xF3, 0x3F, 0x40, 0x40, true)]
    public void DcpAndIsc_IndirectIndexed_ModifyThenCompareOrSubtract(
        byte opcode, byte operand, byte expectedMemory, byte initialA, bool initialCarry)
    {
        SetPc(0x8000);
        SetA(initialA);
        SetY(1);
        SetP(0);
        SetFlag('C', initialCarry);
        Bus.Load(0x8000, [opcode, 0xFF]);
        Bus.Write(0x00FF, 0xFF);
        Bus.Write(0x0000, 0x12);
        Bus.Write(0x1300, operand);

        Assert.Equal(8UL, Cpu.Step());
        Assert.Equal(expectedMemory, Bus.Read(0x1300));
        Assert.Equal(opcode == 0xF3 ? (byte)0x00 : initialA, GetA());
        Assert.Equal(opcode == 0xF3, GetFlag('Z'));
        Assert.True(GetFlag('C'));
    }

    public static TheoryData<byte, byte[], byte, ulong> UnofficialNopCases()
    {
        var cases = new TheoryData<byte, byte[], byte, ulong>();
        foreach (var opcode in new byte[] { 0x04, 0x44, 0x64 }) cases.Add(opcode, [0x42], 0, 3);
        cases.Add(0x0C, [0x34, 0x12], 0, 4);
        foreach (var opcode in new byte[] { 0x14, 0x34, 0x54, 0x74, 0xD4, 0xF4 }) cases.Add(opcode, [0x42], 1, 4);
        foreach (var opcode in new byte[] { 0x1C, 0x3C, 0x5C, 0x7C, 0xDC, 0xFC }) cases.Add(opcode, [0x34, 0x12], 1, 4);
        foreach (var opcode in new byte[] { 0x80, 0x82, 0x89, 0xC2, 0xE2 }) cases.Add(opcode, [0x42], 0, 2);
        return cases;
    }

    [Theory]
    [MemberData(nameof(UnofficialNopCases))]
    public void UnofficialNops_ConsumeOperandsAndDocumentedCycles(
        byte opcode, byte[] operands, byte initialX, ulong expectedCycles)
    {
        Bus.Load(0x8000, new byte[] { opcode }.Concat(operands).ToArray());
        SetPc(0x8000);
        SetX(initialX);
        SetA(0x55);
        SetP(0xA5);

        Assert.Equal(expectedCycles, Cpu.Step());
        Assert.Equal((ushort)(0x8001 + operands.Length), GetPc());
        Assert.Equal(0x55, GetA());
        Assert.Equal(0xA5, GetP());
    }

    [Fact]
    public void UnsupportedOpcode_CompletesWithoutCrashing()
    {
        Bus.Load(0x8000, [0x02]);
        SetPc(0x8000);

        var cycles = Cpu.Step();

        Assert.Equal(2UL, cycles);
        Assert.Equal(0x8001, GetPc());
        Assert.Equal(0, GetCycles());
    }
}
