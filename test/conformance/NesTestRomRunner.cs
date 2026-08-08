namespace SR.Emulation.Nes.ConformanceTests;

internal interface INesTestMachine
{
    void RunForPpuDots(int count);
    void Reset();
    byte PeekCpuMemory(ushort address);
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
    int resetDelayDots = 341 * 262 * 6)
{
    internal NesTestRunResult Run(long maximumPpuDots)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumPpuDots);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunkSize);
        ArgumentOutOfRangeException.ThrowIfNegative(resetDelayDots);

        long elapsed = 0;
        var resetCount = 0;
        while (elapsed < maximumPpuDots)
        {
            var dots = (int)Math.Min(chunkSize, maximumPpuDots - elapsed);
            machine.RunForPpuDots(dots);
            elapsed += dots;

            var status = BlarggProtocol.Read(machine.PeekCpuMemory);
            if (status is null || status.State == BlarggTestState.Running) continue;
            if (status.State == BlarggTestState.ResetRequested)
            {
                var delay = (int)Math.Min(resetDelayDots, maximumPpuDots - elapsed);
                if (delay > 0)
                {
                    machine.RunForPpuDots(delay);
                    elapsed += delay;
                }
                machine.Reset();
                resetCount++;
                continue;
            }

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
}
