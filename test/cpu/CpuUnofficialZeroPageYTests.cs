using Xunit;

namespace Sheep.Emulation.Nes.Tests;

public sealed class CpuUnofficialZeroPageYTests : CpuTestFixture
{
    [Fact]
    public void AaxZeroPageY_UsesYIndexAndWrapsWithinZeroPage()
    {
        Bus.Load(0x8000, [0x97, 0xFE]);
        Bus.Write(0x00FF, 0x55);
        SetPc(0x8000);
        SetA(0xCC);
        SetX(0xAA);
        SetY(5);
        SetP(0xE5);

        Assert.Equal(4UL, Cpu.Step());
        Assert.Equal(0x88, Bus.Read(0x0003));
        Assert.Equal(0x55, Bus.Read(0x00FF));
        Assert.Equal(0xE5, GetP());
    }

    [Fact]
    public void LaxZeroPageY_UsesYIndexAndWrapsWithinZeroPage()
    {
        Bus.Load(0x8000, [0xB7, 0xFE]);
        Bus.Write(0x0003, 0x80);
        Bus.Write(0x00FF, 0x11);
        SetPc(0x8000);
        SetX(1);
        SetY(5);
        SetP(0x61);

        Assert.Equal(4UL, Cpu.Step());
        Assert.Equal(0x80, GetA());
        Assert.Equal(0x80, GetX());
        Assert.True(GetFlag('N'));
        Assert.True(GetFlag('C'));
        Assert.True(GetFlag('V'));
    }
}
