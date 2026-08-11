using Xunit;

namespace Sheep.Emulation.Nes.Tests;

public sealed class CpuJumpTests : CpuTestFixture
{
    [Fact]
    public void JMP_Absolute_SetsProgramCounterAndTakesThreeCycles()
    {
        Bus.Load(0x8000, [0x4C, 0x34, 0x12]);
        SetPc(0x8000);

        Assert.Equal(3UL, Cpu.Step());
        Assert.Equal(0x1234, GetPc());
    }

    [Fact]
    public void JMP_Indirect_Uses6502PageBoundaryWrapAndTakesFiveCycles()
    {
        Bus.Load(0x8000, [0x6C, 0xFF, 0x12]);
        Bus.Write(0x12FF, 0x34);
        Bus.Write(0x1200, 0x12);
        Bus.Write(0x1300, 0x99);
        SetPc(0x8000);

        Assert.Equal(5UL, Cpu.Step());
        Assert.Equal(0x1234, GetPc());
    }

    [Fact]
    public void JMP_IndirectWithoutPageBoundaryReadsConsecutiveBytes()
    {
        Bus.Load(0x8000, [0x6C, 0x34, 0x12]);
        Bus.Write(0x1234, 0x78);
        Bus.Write(0x1235, 0x56);
        SetPc(0x8000);

        Assert.Equal(5UL, Cpu.Step());
        Assert.Equal(0x5678, GetPc());
    }
}
