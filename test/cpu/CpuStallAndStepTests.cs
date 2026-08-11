using Xunit;

namespace Sheep.Emulation.Nes.Tests;

public sealed class CpuStallAndStepTests : CpuTestFixture
{
    [Fact]
    public void Stall_AddsCyclesToCurrentInstruction()
    {
        SetPc(0x8000);
        Cpu.Stall(10);
        Assert.Equal(10, GetCycles());
    }

    [Fact]
    public void ConsecutiveSteps_ReportCyclesForEachInstructionIndependently()
    {
        Bus.Load(0x8000, [0xEA, 0x48]);
        SetPc(0x8000);

        Assert.Equal(2UL, Cpu.Step());
        Assert.Equal(3UL, Cpu.Step());
        Assert.Equal(0x8002, GetPc());
    }

    [Fact]
    public void ClockDoesNotFetchNextOpcodeWhileCyclesRemain()
    {
        Bus.Load(0x8000, [0xEA, 0xE8]);
        SetPc(0x8000);
        SetX(0);

        Cpu.Clock();
        Cpu.Clock();

        Assert.Equal(0x8001, GetPc());
        Assert.Equal(0, GetX());
        Assert.Equal(0, GetCycles());
    }

    [Fact]
    public void StallAddsToCyclesAlreadyRemaining()
    {
        Bus.Load(0x8000, [0xEA, 0xE8]);
        SetPc(0x8000);

        Cpu.Clock();
        Cpu.Stall(3);
        Clock(4);

        Assert.Equal(0x8001, GetPc());
        Assert.Equal(0, GetCycles());
    }
}
