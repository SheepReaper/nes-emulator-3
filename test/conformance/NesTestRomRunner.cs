namespace SR.Emulation.Nes.ConformanceTests;

internal interface INesTestMachine
{
    void RunForPpuDots(int count);
    void Reset();
    byte PeekCpuMemory(ushort address);
    ushort ProgramCounter { get; }
}

internal enum NesTestOutcome
{
    Passed,
    Failed,
    TimedOut
}

internal sealed record NesTestRunResult(
    NesTestOutcome Outcome,
    byte? Code,
    string Output,
    long ElapsedPpuDots,
    int ResetCount);

internal sealed class NesTestRomRunner(
    INesTestMachine machine,
    int chunkSize = 10_000,
    int resetDelayDots = 341 * 262 * 6,
    ushort? legacyResultAddress = null,
    long legacyMinimumPpuDots = 0,
    ushort? successProgramCounter = null)
{
    internal NesTestRunResult Run(long maximumPpuDots)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumPpuDots);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunkSize);
        ArgumentOutOfRangeException.ThrowIfNegative(resetDelayDots);

        long elapsed = 0;
        var resetCount = 0;
        var waitingForResetRequestToClear = false;
        while (elapsed < maximumPpuDots)
        {
            var dots = (int)Math.Min(chunkSize, maximumPpuDots - elapsed);
            machine.RunForPpuDots(dots);
            elapsed += dots;

            if (successProgramCounter.HasValue && machine.ProgramCounter == successProgramCounter.Value)
                return new NesTestRunResult(NesTestOutcome.Passed, 0, "", elapsed, resetCount);

            if (legacyResultAddress.HasValue && elapsed >= legacyMinimumPpuDots)
            {
                var legacyCode = machine.PeekCpuMemory(legacyResultAddress.Value);
                if (legacyCode != 0)
                    return new NesTestRunResult(legacyCode == 1 ? NesTestOutcome.Passed : NesTestOutcome.Failed,
                        legacyCode, "", elapsed, resetCount);
            }
            var status = BlarggProtocol.Read(machine.PeekCpuMemory);
            if (status is null) continue;
            if (status.State == BlarggTestState.Running)
            {
                waitingForResetRequestToClear = false;
                continue;
            }
            if (status.State == BlarggTestState.ResetRequested)
            {
                if (waitingForResetRequestToClear) continue;
                var delay = (int)Math.Min(resetDelayDots, maximumPpuDots - elapsed);
                if (delay > 0)
                {
                    machine.RunForPpuDots(delay);
                    elapsed += delay;
                }
                machine.Reset();
                resetCount++;
                waitingForResetRequestToClear = true;
                continue;
            }

            waitingForResetRequestToClear = false;

            return new NesTestRunResult(
                status.Code == 0 ? NesTestOutcome.Passed : NesTestOutcome.Failed,
                status.Code,
                status.Output,
                elapsed,
                resetCount);
        }

        var finalStatus = BlarggProtocol.Read(machine.PeekCpuMemory);
        return new NesTestRunResult(
            NesTestOutcome.TimedOut,
            finalStatus?.Code,
            finalStatus?.Output ?? "",
            elapsed,
            resetCount);
    }
}

internal sealed class NesTestMachine(SR.Emulation.Nes.Nes nes) : INesTestMachine
{
    public void RunForPpuDots(int count) => nes.RunForPpuDots(count);
    public void Reset() => nes.Reset();
    public byte PeekCpuMemory(ushort address) => nes.Debugger.PeekCpuMemory(address);
    public ushort ProgramCounter => nes.Debugger.CaptureSnapshot(new NesDebugSnapshotOptions
    {
        Sections = NesDebugSnapshotSections.Cpu
    }).Cpu!.ProgramCounter;
}
