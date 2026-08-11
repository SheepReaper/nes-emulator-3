using Xunit;

namespace Sheep.Emulation.Nes.Tests;

public sealed class CpuBranchAndJumpTests : CpuTestFixture
{
    [Fact]
    public void JSR_RTS_SubroutineCallAndReturn()
    {
        Bus.Load(0x8000, [0x20, 0x00, 0x90]);
        Bus.Load(0x9000, [0x60]);

        SetPc(0x8000);
        SetSp(0xFD);

        Clock(6);

        Assert.Equal(0x9000, GetPc());
        Assert.Equal(0xFB, GetSp());
        Assert.Equal(0x80, Bus.Read(0x01FD));
        Assert.Equal(0x02, Bus.Read(0x01FC));

        Clock(6);

        Assert.Equal(0x8003, GetPc());
        Assert.Equal(0xFD, GetSp());
    }

    [Theory]
    [InlineData(0xF0, 'Z', false, 0x8000, 0x05, 2, 0x8002)]
    [InlineData(0xF0, 'Z', true, 0x8000, 0x05, 3, 0x8007)]
    [InlineData(0xD0, 'Z', false, 0x8005, -5, 3, 0x8002)]
    [InlineData(0xF0, 'Z', true, 0x80FD, 0x05, 4, 0x8104)]
    [InlineData(0xD0, 'Z', false, 0x8102, -5, 4, 0x80FF)]
    public void BranchInstructions_HaveCorrectBehaviorAndCycles(
        byte opcode, char flag, bool flagValue, ushort startPc, int offset, int expectedCycles, ushort expectedPc)
    {
        Bus.Load(startPc, [opcode, (byte)offset]);
        SetPc(startPc);
        SetP(0);
        SetFlag(flag, flagValue);

        Clock(expectedCycles);

        Assert.Equal(0, GetCycles());
        Assert.Equal(expectedPc, GetPc());
    }

    [Theory]
    [InlineData(0x10, 'N', false)]
    [InlineData(0x30, 'N', true)]
    [InlineData(0x50, 'V', false)]
    [InlineData(0x70, 'V', true)]
    [InlineData(0x90, 'C', false)]
    [InlineData(0xB0, 'C', true)]
    [InlineData(0xD0, 'Z', false)]
    [InlineData(0xF0, 'Z', true)]
    public void EveryBranchCondition_HandlesTakenAndNotTakenPaths(byte opcode, char flag, bool takenValue)
    {
        Bus.Load(0x8000, [opcode, 0x02]);
        SetPc(0x8000);
        SetP(0);
        SetFlag(flag, takenValue);

        Assert.Equal(3UL, Cpu.Step());
        Assert.Equal(0x8004, GetPc());

        SetPc(0x8000);
        SetFlag(flag, !takenValue);

        Assert.Equal(2UL, Cpu.Step());
        Assert.Equal(0x8002, GetPc());
    }

    [Theory]
    [InlineData(0x80FD, 0x05, 0x8104, 4UL)]
    [InlineData(0x8102, unchecked((byte)-5), 0x80FF, 4UL)]
    public void BvcTaken_HandlesForwardAndBackwardPageCrossings(
        ushort startPc, byte offset, ushort expectedPc, ulong expectedCycles)
    {
        Bus.Load(startPc, [0x50, offset]);
        SetPc(startPc);
        SetP(0x20);

        Assert.Equal(expectedCycles, Cpu.Step());
        Assert.Equal(expectedPc, GetPc());
        Assert.False(GetFlag('V'));
    }
}
