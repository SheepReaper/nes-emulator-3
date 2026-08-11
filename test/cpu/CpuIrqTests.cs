using Xunit;

namespace Sheep.Emulation.Nes.Tests;

public sealed class CpuIrqTests : CpuTestFixture
{
    [Fact]
    public void NmiEdge_RemainsPendingAfterTheInputLineFalls()
    {
        Bus.Write(0xFFFA, 0x00);
        Bus.Write(0xFFFB, 0x90);
        SetPc(0x8000);
        SetCycles(1);
        Interrupts.Nmi = true;

        Cpu.Clock();
        Interrupts.Nmi = false;
        Cpu.Clock();

        Assert.Equal(0x9000, GetPc());
    }

    [Fact]
    public void Cli_DelaysAnAssertedIrqUntilAfterTheFollowingInstruction()
    {
        Bus.Load(0x8000, [0x58, 0xEA, 0xEA]);
        Bus.Write(0xFFFE, 0x00);
        Bus.Write(0xFFFF, 0x90);
        SetPc(0x8000);
        SetP(0b0010_0100);
        SetCycles(0);
        Interrupts.Irq = true;

        Clock(4);

        Assert.Equal(0x8002, GetPc());

        Cpu.Clock();

        Assert.Equal(0x9000, GetPc());
    }

    [Theory]
    [InlineData(false, 0x9000, true)]
    [InlineData(true, 0x8001, true)]
    public void IRQ_TriggersInterruptSequence_OnlyWhenInterruptsEnabled(
        bool initialInterruptFlag, ushort expectedPc, bool expectedInterruptFlag)
    {
        Bus.Load(0x8000, [0xEA]);
        Bus.Write(0xFFFE, 0x00);
        Bus.Write(0xFFFF, 0x90);

        SetPc(0x8000);
        SetSp(0xFD);
        SetP(0);
        SetFlag('I', initialInterruptFlag);
        Interrupts.Irq = true;
        SetCycles(0);

        Clock(initialInterruptFlag ? 2 : 7);

        Assert.Equal(expectedPc, GetPc());
        Assert.Equal(expectedInterruptFlag, GetFlag('I'));
        if (!initialInterruptFlag)
        {
            Assert.Equal(0xFA, GetSp());
        }
    }

    [Fact]
    public void IrqFirstAssertedAtInstructionBoundaryWaitsThroughTheNextInstruction()
    {
        Bus.Load(0x8000, [0xEA, 0xEA]);
        Bus.Write(0xFFFE, 0x00);
        Bus.Write(0xFFFF, 0x90);
        SetPc(0x8000);
        SetP(0);
        SetCycles(0);
        Interrupts.Irq = true;

        Clock(2);

        Assert.Equal(0x8001, GetPc());

        Cpu.Clock();

        Assert.Equal(0x9000, GetPc());
    }

    [Fact]
    public void IrqLineRemainsAssertedAfterInterruptIsServiced()
    {
        Bus.Write(0xFFFE, 0x00);
        Bus.Write(0xFFFF, 0x90);
        SetPc(0x8000);
        SetP(0);
        Interrupts.Irq = true;

        Assert.Equal(7UL, Cpu.Step());
        Assert.True(Interrupts.Irq);
        Assert.Equal(0x9000, GetPc());
    }
}
