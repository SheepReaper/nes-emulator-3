using Xunit;

namespace Sheep.Emulation.Nes.Tests;

public sealed class DmaAndControllerBusTests
{
    [Fact]
    public void CpuBus_OamDmaCopiesPageAndStallsCpu()
    {
        var (bus, _, ppu, cpu) = BusTestHelper.CreateCpuBus();
        for (var i = 0; i < 256; i++)
        {
            bus.Write((ushort)(0x0200 + i), (byte)i);
        }
        ppu.Write(0x2003, 0x80);

        bus.Write(0x4014, 0x02);
        for (ulong cycle = 0; cycle < 513; cycle++)
        {
            Assert.True(bus.ClockDma(cycle));
        }

        Assert.False(bus.ClockDma(513));
        ppu.Write(0x2003, 0x80);
        Assert.Equal(0x00, ppu.Read(0x2004));
        ppu.Write(0x2003, 0x7F);
        Assert.Equal(0xFF, ppu.Read(0x2004));
    }

    [Fact]
    public void CpuBus_OamDmaAddsAlignmentCycleOnOddCpuCycle()
    {
        var (bus, _, _, cpu) = BusTestHelper.CreateCpuBus();
        bus.Write(0x4014, 0x00);
        for (ulong cycle = 1; cycle < 515; cycle++)
        {
            Assert.True(bus.ClockDma(cycle));
        }

        Assert.False(bus.ClockDma(515));
    }

    [Theory]
    [InlineData(510, 514)]
    [InlineData(512, 516)]
    public void CpuBus_DmcDmaAtEndOfOamDmaUsesHardwareOverlapLength(int requestCycle, int expectedCycles)
    {
        var (bus, _, _, _) = BusTestHelper.CreateCpuBus();
        bus.Write(0x4014, 0x00);

        var busyCycles = 0;
        for (ulong cycle = 0; cycle < 600; cycle++)
        {
            if (cycle == (ulong)requestCycle)
            {
                bus.RequestDmcDma(0x8000, _ => { });
            }
            if (!bus.ClockDma(cycle))
            {
                break;
            }
            busyCycles++;
        }

        Assert.Equal(expectedCycles, busyCycles);
    }

    [Fact]
    public void CpuBus_ImplicitDmcAbortRequestIsCancelledByCpuWrite()
    {
        var (bus, _, _, _) = BusTestHelper.CreateCpuBus();
        bus.RequestDmcDma(0x8000, null);

        Assert.True(bus.ClockDma(0));
        bus.Write(0x4015, 0x00);

        Assert.False(bus.ClockDma(1));
    }

    [Fact]
    public void CpuDmcDmaState_ImplicitAbortIsCancelledInsteadOfRequeued()
    {
        var state = new CpuDmcDmaState();
        state.Request(0x8000, null);
        state.Pending = true;
        state.Cycles = 0;
        state.HaltLatched = true;

        state.Abort();

        Assert.False(state.Pending);
        Assert.Equal(0, state.Cycles);
        Assert.Null(state.Completed);
    }

    [Fact]
    public void CpuDmcDmaState_ImplicitAbortClearsControllerReadClockedState()
    {
        var state = new CpuDmcDmaState();
        state.Request(0x8000, null);
        state.ControllerReadClocked = true;

        state.Abort();

        Assert.False(state.ControllerReadClocked);
    }

    [Fact]
    public void CpuDmcDmaState_ExplicitAbortKeepsFinalHaltCycle()
    {
        var state = new CpuDmcDmaState();
        state.Request(0x8000, _ => { });
        state.Cycles = 1;

        state.Abort();

        Assert.True(state.Pending);
        Assert.Equal(1, state.Cycles);
        Assert.Null(state.Completed);
    }

    [Fact]
    public void CpuBus_ControllerPortsLatchAndShiftBothControllers()
    {
        var (bus, _, _, _) = BusTestHelper.CreateCpuBus();
        bus.SetControllerState(0, 0b0101_0101);
        bus.SetControllerState(1, 0b1010_1010);

        bus.Write(0x4016, 1);
        bus.Write(0x4016, 0);

        byte[] controller1 = new byte[8];
        byte[] controller2 = new byte[8];
        for (var i = 0; i < 8; i++)
        {
            controller1[i] = bus.Read(0x4016);
            controller2[i] = bus.Read(0x4017);
        }

        Assert.Equal(new byte[] { 1, 0, 1, 0, 1, 0, 1, 0 }, controller1);
        Assert.Equal(new byte[] { 0, 1, 0, 1, 0, 1, 0, 1 }, controller2);
        Assert.Equal(1, bus.Read(0x4016));
        Assert.Equal(1, bus.Read(0x4017));
    }

    [Fact]
    public void Nes_ControllerStateIsExposedThroughTheCpuControllerPort()
    {
        var nes = new NesSystem();
        nes.SetControllerState(0, NesControllerButton.A | NesControllerButton.Start | NesControllerButton.Left);
        Assert.Equal(1, nes.Debugger.PeekCpuMemory(0x4016));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void Nes_ControllerStateRejectsInvalidControllerIndex(int controller)
    {
        var nes = new NesSystem();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            nes.SetControllerState(controller, NesControllerButton.A));
    }
}
