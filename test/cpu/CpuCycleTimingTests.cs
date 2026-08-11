using Xunit;

namespace Sheep.Emulation.Nes.Tests;

public sealed class CpuCycleTimingTests : CpuTestFixture
{
    [Fact]
    public void AbsoluteIoWriteOccursOnTheInstructionsFinalCycle()
    {
        Bus.Load(0x8000, [0x8D, 0x00, 0x20]);
        SetPc(0x8000);
        SetA(0x42);
        SetCycles(0);

        Clock(3);
        Assert.Equal(0, Bus.Read(0x2000));

        Cpu.Clock();
        Assert.Equal(0x42, Bus.Read(0x2000));
    }

    [Fact]
    public void AbsoluteIoReadSamplesTheBusOnTheInstructionsFinalCycle()
    {
        Bus.Load(0x8000, [0xAD, 0x02, 0x20]);
        Bus.Write(0x2002, 0x11);
        SetPc(0x8000);
        SetCycles(0);

        Cpu.Clock();
        Bus.Write(0x2002, 0x22);
        Clock(3);

        Assert.Equal(0x22, GetA());
    }

    [Fact]
    public void ZeroPageIncrementWritesOnTheInstructionsFinalCycle()
    {
        Bus.Load(0x8000, [0xE6, 0x10]);
        Bus.Write(0x0010, 0x41);
        SetPc(0x8000);
        SetCycles(0);

        Clock(4);
        Assert.Equal(0x41, Bus.Read(0x0010));

        Cpu.Clock();

        Assert.Equal(0x42, Bus.Read(0x0010));
    }

    [Fact]
    public void PendingBusAddressUsesIndexedIoReadAddressBeforeFinalRead()
    {
        Bus.Load(0x8000, [0xBD, 0xF7, 0x20]);
        SetPc(0x8000);
        SetX(0x10);
        SetCycles(0);

        Clock(3);

        Assert.Equal((ushort)0x2107, Cpu.PendingBusAddress);
    }

    [Fact]
    public void IsOnOddCycle_ReturnsTrueAfterOddMasterClock()
    {
        Bus.Load(0x8000, [0xEA]);
        SetPc(0x8000);
        Cpu.Clock(1);
        Assert.True(Cpu.IsOnOddCycle());
    }

    [Fact]
    public void IsOnOddCycle_ReturnsFalseAfterEvenMasterClock()
    {
        Bus.Load(0x8000, [0xEA]);
        SetPc(0x8000);
        Cpu.Clock(2);
        Assert.False(Cpu.IsOnOddCycle());
    }
}
