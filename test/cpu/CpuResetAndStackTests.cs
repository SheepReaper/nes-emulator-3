using Xunit;

namespace Sheep.Emulation.Nes.Tests;

public sealed class CpuResetAndStackTests : CpuTestFixture
{
    [Fact]
    public void Reset_SetsInitialStateCorrectly()
    {
        Bus.Write(0xFFFC, 0x00);
        Bus.Write(0xFFFD, 0x80);

        Cpu.Reset();

        Assert.Equal(0x8000, GetPc());
        Assert.Equal(0x00, GetA());
        Assert.Equal(0x00, GetX());
        Assert.Equal(0x00, GetY());
        Assert.Equal(0xFD, GetSp());
        Assert.Equal(0b0010_0100, GetP());
        Assert.Equal(7, GetCycles());
    }

    [Fact]
    public void PHA_PushesAccumulatorToStack()
    {
        Bus.Load(0x8000, [0x48]);
        SetPc(0x8000);
        SetA(0x42);
        SetSp(0xFD);

        Clock(3);

        Assert.Equal(0xFC, GetSp());
        Assert.Equal(0x42, Bus.Read(0x01FD));
    }

    [Fact]
    public void PLA_PullsValueFromStackAndSetsFlags()
    {
        Bus.Load(0x8000, [0x68]);
        Bus.Write(0x01FD, 0x8F);
        SetPc(0x8000);
        SetSp(0xFC);
        SetP(0);

        Clock(4);

        Assert.Equal(0xFD, GetSp());
        Assert.Equal(0x8F, GetA());
        Assert.True(GetFlag('N'));
        Assert.False(GetFlag('Z'));
    }

    [Fact]
    public void PLA_SetsZeroFlagWhenPullingZero()
    {
        Bus.Load(0x8000, [0x68]);
        Bus.Write(0x0100, 0x00);
        SetPc(0x8000);
        SetSp(0xFF);
        SetP(0x80);

        Assert.Equal(4UL, Cpu.Step());
        Assert.Equal(0x00, GetA());
        Assert.True(GetFlag('Z'));
        Assert.False(GetFlag('N'));
        Assert.Equal(0x00, GetSp());
    }

    [Fact]
    public void PHA_StackPointerWrapsFrom00ToFF()
    {
        Bus.Load(0x8000, [0x48]);
        SetPc(0x8000);
        SetSp(0x00);
        SetA(0x42);

        Assert.Equal(3UL, Cpu.Step());
        Assert.Equal(0x42, Bus.Read(0x0100));
        Assert.Equal(0xFF, GetSp());
    }

    [Fact]
    public void ResetDelayCompletesBeforeFirstOpcodeExecutes()
    {
        Bus.Write(0xFFFC, 0x00);
        Bus.Write(0xFFFD, 0x80);
        Bus.Load(0x8000, [0xE8]);
        Cpu.Reset();

        Assert.Equal(7UL, Cpu.Step());
        Assert.Equal(0, GetX());
        Assert.Equal(0x8000, GetPc());

        Assert.Equal(2UL, Cpu.Step());
        Assert.Equal(1, GetX());
        Assert.Equal(0x8001, GetPc());
    }
}
