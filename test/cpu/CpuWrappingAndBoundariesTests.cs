using Xunit;

namespace Sheep.Emulation.Nes.Tests;

public sealed class CpuWrappingAndBoundariesTests : CpuTestFixture
{
    [Fact]
    public void ZeroPageX_AddressingWrapsFromFFTo00()
    {
        Bus.Load(0x8000, [0xB5, 0xFF]);
        Bus.Write(0x0000, 0x42);
        SetPc(0x8000);
        SetX(1);

        Assert.Equal(4UL, Cpu.Step());
        Assert.Equal(0x42, GetA());
    }

    [Fact]
    public void ZeroPageY_AddressingWrapsFromFFTo00()
    {
        Bus.Load(0x8000, [0xB6, 0xFF]);
        Bus.Write(0x0000, 0x42);
        SetPc(0x8000);
        SetY(1);

        Assert.Equal(4UL, Cpu.Step());
        Assert.Equal(0x42, GetX());
    }

    [Fact]
    public void IndexedIndirect_PointerIndexWrapsWithinZeroPage()
    {
        Bus.Load(0x8000, [0xA1, 0xFF]);
        Bus.Write(0x0000, 0x34);
        Bus.Write(0x0001, 0x12);
        Bus.Write(0x1234, 0x42);
        SetPc(0x8000);
        SetX(1);

        Assert.Equal(6UL, Cpu.Step());
        Assert.Equal(0x42, GetA());
    }

    [Fact]
    public void IndirectIndexed_PointerReadWrapsWithinZeroPage()
    {
        Bus.Load(0x8000, [0xB1, 0xFF]);
        Bus.Write(0x00FF, 0x33);
        Bus.Write(0x0000, 0x12);
        Bus.Write(0x1234, 0x42);
        SetPc(0x8000);
        SetY(1);

        Assert.Equal(5UL, Cpu.Step());
        Assert.Equal(0x42, GetA());
    }

    [Fact]
    public void AbsoluteIndexed_AddressWrapsFromFFFFTo0000()
    {
        Bus.Load(0x8000, [0xBD, 0xFF, 0xFF]);
        Bus.Write(0x0000, 0x42);
        SetPc(0x8000);
        SetX(1);

        Assert.Equal(5UL, Cpu.Step());
        Assert.Equal(0x42, GetA());
    }

    [Fact]
    public void OperandFetch_WrapsProgramCounterFromFFFFTo0000()
    {
        Bus.Write(0xFFFF, 0xA9);
        Bus.Write(0x0000, 0x42);
        SetPc(0xFFFF);

        Assert.Equal(2UL, Cpu.Step());
        Assert.Equal(0x42, GetA());
        Assert.Equal(0x0001, GetPc());
    }

    [Fact]
    public void Test6_IFlagLatency_RtiUpdatesIFlagBeforePolling()
    {
        Bus.Load(0xFFFC, [0x00, 0x80]);
        Bus.Load(0xFFFE, [0x00, 0x06]);
        Bus.Load(0x0600, [0x4C, 0x00, 0x90]);
        Bus.Load(0x9000, [0x86, 0x50, 0x40]);

        Bus.Load(0x8020, [0xE8, 0xE8, 0xA5, 0x50, 0xC9, 0x5A, 0x4C, 0x24, 0x80]);
        Bus.Load(0x8000, [0xA9, 0x80, 0x48, 0xA9, 0x20, 0x48, 0xA9, 0x04, 0x48, 0xA9, 0x5A, 0x85, 0x50, 0x58, 0x40]);

        Cpu.Reset();
        for (int i = 0; i < 7; i++) Cpu.Clock();

        SetFlag('I', true);
        Interrupts.ApuDmcIrq = true;

        for (int i = 0; i < 60; i++) Cpu.Clock();

        Assert.Equal(0x5A, Bus.Read(0x50));
    }
}
