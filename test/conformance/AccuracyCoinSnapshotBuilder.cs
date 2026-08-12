namespace Sheep.Emulation.Nes.ConformanceTests;

/// <summary>
/// Result aggregator and zero-page snapshot builder for AccuracyCoin.
/// </summary>
internal static class AccuracyCoinSnapshotBuilder
{
    private const ushort PassedAddress = 0x0038;
    private const ushort SkippedAddress = 0x003F;
    private const ushort FirstResultAddress = 0x0400;
    private const ushort LastResultAddress = 0x0492;

    internal static AccuracyCoinRunResult Complete(INesTestMachine machine, byte total, long elapsed) =>
        Snapshot(machine, AccuracyCoinOutcome.Passed, total, elapsed, determineOutcome: true);

    internal static AccuracyCoinRunResult Snapshot(
        INesTestMachine machine,
        AccuracyCoinOutcome outcome,
        byte total,
        long elapsed,
        bool determineOutcome = false)
    {
        var passed = machine.PeekCpuMemory(PassedAddress);
        var skipped = machine.PeekCpuMemory(SkippedAddress);
        List<AccuracyCoinResultByte> nonPassing = [];
        for (var address = FirstResultAddress; address <= LastResultAddress; address++)
        {
            var value = machine.PeekCpuMemory(address);
            if (value != 0 && value != 0xFF && (value & 1) == 0)
            {
                nonPassing.Add(new AccuracyCoinResultByte(address, value));
            }
        }

        if (determineOutcome)
        {
            outcome = passed + skipped == total ? AccuracyCoinOutcome.Passed : AccuracyCoinOutcome.Failed;
        }
        return new AccuracyCoinRunResult(outcome, total, passed, skipped, elapsed, nonPassing);
    }
}
