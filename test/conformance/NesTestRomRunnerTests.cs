using Xunit;

namespace SR.Emulation.Nes.ConformanceTests;

public sealed class NesTestRomRunnerTests
{
    [Fact]
    public void Run_ReturnsPassedWhenTheRomReportsCodeZero()
    {
        var machine = new FakeMachine(0x80, 0x80, 0);

        var result = new NesTestRomRunner(machine, chunkSize: 10).Run(100);

        Assert.Equal(NesTestOutcome.Passed, result.Outcome);
        Assert.Equal(30, result.ElapsedPpuDots);
    }

    [Fact]
    public void Run_ReturnsFailureCodeAndDiagnosticOutput()
    {
        var machine = new FakeMachine(3) { Output = "scanline too late" };

        var result = new NesTestRomRunner(machine).Run(100);

        Assert.Equal(NesTestOutcome.Failed, result.Outcome);
        Assert.Equal((byte?)3, result.Code);
        Assert.Equal("scanline too late", result.Output);
    }

    [Fact]
    public void Run_HonorsDelayedResetRequests()
    {
        var machine = new FakeMachine(0x81, 0);

        var result = new NesTestRomRunner(machine, chunkSize: 10, resetDelayDots: 60).Run(100);

        Assert.Equal(NesTestOutcome.Passed, result.Outcome);
        Assert.Equal(1, machine.ResetCount);
        Assert.Equal(80, result.ElapsedPpuDots);
    }

    [Fact]
    public void Run_DoesNotResetAgainWhileRomStillExposesTheAcknowledgedRequest()
    {
        var machine = new FakeMachine(0x81, 0x81, 0x80, 0);

        var result = new NesTestRomRunner(machine, chunkSize: 10, resetDelayDots: 0).Run(100);

        Assert.Equal(NesTestOutcome.Passed, result.Outcome);
        Assert.Equal(1, machine.ResetCount);
    }

    [Fact]
    public void Run_SupportsLegacyNonzeroTerminalResultProtocol()
    {
        var machine = new FakeMachine(0x80) { LegacyResult = 1 };

        var result = new NesTestRomRunner(machine, legacyResultAddress: 0x00F0).Run(100);

        Assert.Equal(NesTestOutcome.Passed, result.Outcome);
        Assert.Equal((byte?)1, result.Code);
    }

    [Fact]
    public void Run_TimesOutUsingEmulatedDotsRatherThanWallClock()
    {
        var machine = new FakeMachine(0x80);

        var result = new NesTestRomRunner(machine, chunkSize: 10).Run(25);

        Assert.Equal(NesTestOutcome.TimedOut, result.Outcome);
        Assert.Equal(25, result.ElapsedPpuDots);
    }

    private sealed class FakeMachine(params byte[] statuses) : INesTestMachine
    {
        private readonly Queue<byte> _statuses = new(statuses);
        private byte _status = 0x80;
        public int ResetCount { get; private set; }
        public string Output { get; init; } = "";
        public byte LegacyResult { get; init; }
        public ushort ProgramCounter { get; set; }

        public void RunForPpuDots(int count)
        {
            if (_statuses.Count > 0) _status = _statuses.Dequeue();
        }

        public void Reset() => ResetCount++;

        public byte PeekCpuMemory(ushort address) => address switch
        {
            0x00F0 => LegacyResult,
            0x6000 => _status,
            0x6001 => 0xDE,
            0x6002 => 0xB0,
            0x6003 => 0x61,
            >= 0x6004 when address - 0x6004 < Output.Length => (byte)Output[address - 0x6004],
            _ => 0
        };
    }
}
