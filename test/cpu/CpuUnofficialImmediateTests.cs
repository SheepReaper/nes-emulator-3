using Xunit;

namespace Sheep.Emulation.Nes.Tests;

public sealed class CpuUnofficialImmediateTests : CpuTestFixture
{
    [Theory]
    [InlineData(0x1A)]
    [InlineData(0x3A)]
    [InlineData(0x5A)]
    [InlineData(0x7A)]
    [InlineData(0xDA)]
    [InlineData(0xFA)]
    public void UnofficialImpliedNop_DoesNothingAndTakesTwoCycles(byte opcode)
    {
        Bus.Load(0x8000, [opcode]);
        SetPc(0x8000);
        SetA(0x12);
        SetX(0x34);
        SetY(0x56);
        SetSp(0x78);
        SetP(0xA5);

        var cycles = Cpu.Step();

        Assert.Equal(2UL, cycles);
        Assert.Equal(0x8001, GetPc());
        Assert.Equal(0x12, GetA());
        Assert.Equal(0x34, GetX());
        Assert.Equal(0x56, GetY());
        Assert.Equal(0x78, GetSp());
        Assert.Equal(0xA5, GetP());
    }

    [Fact]
    public void UnofficialSbcImmediateAlias_MatchesOfficialSbc()
    {
        Bus.Load(0x8000, [0xEB, 0x20]);
        SetPc(0x8000);
        SetA(0x80);
        SetP(0x21);

        Assert.Equal(2UL, Cpu.Step());
        Assert.Equal(0x60, GetA());
        Assert.True(GetFlag('C'));
        Assert.True(GetFlag('V'));
        Assert.Equal(0x8002, GetPc());
    }

    [Theory]
    [InlineData(0x0B)]
    [InlineData(0x2B)]
    public void AacImmediate_AndsAccumulatorAndCopiesNegativeToCarry(byte opcode)
    {
        Bus.Load(0x8000, [opcode, 0xF0]);
        SetPc(0x8000);
        SetA(0x8F);
        SetP(0x60);

        Assert.Equal(2UL, Cpu.Step());
        Assert.Equal(0x80, GetA());
        Assert.True(GetFlag('N'));
        Assert.False(GetFlag('Z'));
        Assert.True(GetFlag('C'));
        Assert.True(GetFlag('V'));
        Assert.Equal(0x8002, GetPc());
    }
}
