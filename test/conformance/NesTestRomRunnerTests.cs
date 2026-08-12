using Xunit;

namespace Sheep.Emulation.Nes.ConformanceTests;

public sealed class NesTestRomRunnerTests
{
    [Fact]
    public void Run_ReturnsPassedWhenTheRomReportsCodeZero()
    {
        var machine = new FakeTestMachine(0x80, 0x80, 0);
        var result = new NesTestRomRunner(machine, chunkSize: 10).Run(100);

        Assert.Equal(NesTestOutcome.Passed, result.Outcome);
        Assert.Equal(30, result.ElapsedPpuDots);
    }

    [Fact]
    public void Run_ReturnsFailureCodeAndDiagnosticOutput()
    {
        var machine = new FakeTestMachine(3) { Output = "scanline too late" };
        var result = new NesTestRomRunner(machine).Run(100);

        Assert.Equal(NesTestOutcome.Failed, result.Outcome);
        Assert.Equal((byte?)3, result.Code);
        Assert.Equal("scanline too late", result.Output);
    }

    [Fact]
    public void Run_HonorsDelayedResetRequests()
    {
        var machine = new FakeTestMachine(0x81, 0);
        var result = new NesTestRomRunner(machine, chunkSize: 10, resetDelayDots: 60).Run(100);

        Assert.Equal(NesTestOutcome.Passed, result.Outcome);
        Assert.Equal(1, machine.ResetCount);
        Assert.Equal(80, result.ElapsedPpuDots);
    }

    [Fact]
    public void Run_DoesNotResetAgainWhileRomStillExposesTheAcknowledgedRequest()
    {
        var machine = new FakeTestMachine(0x81, 0x81, 0x80, 0);
        var result = new NesTestRomRunner(machine, chunkSize: 10, resetDelayDots: 0).Run(100);

        Assert.Equal(NesTestOutcome.Passed, result.Outcome);
        Assert.Equal(1, machine.ResetCount);
    }

    [Fact]
    public void Run_SupportsLegacyNonzeroTerminalResultProtocol()
    {
        var machine = new FakeTestMachine(0x80) { LegacyResult = 1 };
        var result = new NesTestRomRunner(machine, legacyResultAddress: 0x00F0).Run(100);

        Assert.Equal(NesTestOutcome.Passed, result.Outcome);
        Assert.Equal((byte?)1, result.Code);
    }

    [Fact]
    public void Run_TimesOutUsingEmulatedDotsRatherThanWallClock()
    {
        var machine = new FakeTestMachine(0x80);
        var result = new NesTestRomRunner(machine, chunkSize: 10).Run(25);

        Assert.Equal(NesTestOutcome.TimedOut, result.Outcome);
        Assert.Equal(25, result.ElapsedPpuDots);
    }

    [Theory]
    [InlineData("Passed", true)]
    [InlineData("Failed", false)]
    public void Run_DetectsLegacyTextConsoleTerminalResult(string text, bool expectedPassed)
    {
        var machine = new FakeTestMachine(0x80) { ScreenText = text };
        var result = new NesTestRomRunner(machine, detectTextConsoleResult: true).Run(100);

        Assert.Equal(expectedPassed ? NesTestOutcome.Passed : NesTestOutcome.Failed, result.Outcome);
    }
}
