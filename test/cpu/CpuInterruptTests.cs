using Xunit;

namespace Sheep.Emulation.Nes.Tests;

public sealed class CpuInterruptTests : CpuTestFixture
{
    [Fact]
    public void BRK_RTI_BreakAndReturnFromInterrupt()
    {
        Bus.Load(0x8000, [0x00]);
        Bus.Load(0x9000, [0x40]);
        Bus.Write(0xFFFE, 0x00);
        Bus.Write(0xFFFF, 0x90);

        SetPc(0x8000);
        SetSp(0xFD);
        SetP(0b10000001);

        Clock(7);

        Assert.Equal(0x9000, GetPc());
        Assert.Equal(0xFA, GetSp());
        Assert.True(GetFlag('I'));
        Assert.Equal(0x80, Bus.Read(0x01FD));
        Assert.Equal(0x02, Bus.Read(0x01FC));
        Assert.Equal(0b10110001, Bus.Read(0x01FB));

        Clock(6);

        Assert.Equal(0x8002, GetPc());
        Assert.Equal(0xFD, GetSp());
        Assert.Equal(0b10100001, GetP());
    }

    [Fact]
    public void NMI_TriggersInterruptSequence()
    {
        Bus.Write(0xFFFA, 0x00);
        Bus.Write(0xFFFB, 0x90);

        SetPc(0x8000);
        SetSp(0xFD);
        SetP(0b10000101);
        Interrupts.Nmi = true;
        SetCycles(1);

        Clock(8);

        Assert.Equal(0x9000, GetPc());
        Assert.Equal(0xFA, GetSp());
        Assert.True(GetFlag('I'));
        Assert.False(Interrupts.Nmi);
        Assert.Equal(0x80, Bus.Read(0x01FD));
        Assert.Equal(0x00, Bus.Read(0x01FC));
        Assert.Equal(0b10100101, Bus.Read(0x01FB));
    }

    [Fact]
    public void NmiEdgeFirstObservedAtInstructionBoundaryWaitsThroughTheNextInstruction()
    {
        Bus.Load(0x8000, [0xEA, 0xEA]);
        Bus.Write(0xFFFA, 0x00);
        Bus.Write(0xFFFB, 0x90);
        SetPc(0x8000);
        SetCycles(0);
        Interrupts.Nmi = true;

        Clock(2);

        Assert.Equal(0x8001, GetPc());
        Assert.True(Interrupts.Nmi);

        Cpu.Clock();

        Assert.Equal(0x9000, GetPc());
        Assert.False(Interrupts.Nmi);
    }

    [Fact]
    public void DelayedNmi_AllowsTheFollowingInstructionToCompleteBeforeInterrupting()
    {
        Bus.Load(0x8000, [0xEA, 0xEA]);
        Bus.Write(0xFFFA, 0x00);
        Bus.Write(0xFFFB, 0x90);
        SetPc(0x8000);
        SetCycles(0);
        Interrupts.Nmi = true;
        typeof(InterruptLines).GetProperty("DelayNmiOneInstruction", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(Interrupts, true);

        Clock(2);

        Assert.Equal(0x8001, GetPc());
        Assert.True(Interrupts.Nmi);

        Cpu.Clock();

        Assert.Equal(0x9000, GetPc());
        Assert.False(Interrupts.Nmi);
    }
}
