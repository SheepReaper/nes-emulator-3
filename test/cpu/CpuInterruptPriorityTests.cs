using Xunit;

namespace Sheep.Emulation.Nes.Tests;

public sealed class CpuInterruptPriorityTests : CpuTestFixture
{
    [Fact]
    public void NmiTakesPriorityWhenNmiAndIrqAreBothPending()
    {
        Bus.Write(0xFFFA, 0x00);
        Bus.Write(0xFFFB, 0x90);
        Bus.Write(0xFFFE, 0x00);
        Bus.Write(0xFFFF, 0xA0);
        SetPc(0x8000);
        SetSp(0xFD);
        SetP(0);
        SetCycles(1);
        Interrupts.Nmi = true;
        Interrupts.Irq = true;

        Cpu.Clock();
        Assert.Equal(7UL, Cpu.Step());
        Assert.Equal(0x9000, GetPc());
        Assert.False(Interrupts.Nmi);
        Assert.True(Interrupts.Irq);
    }
}
