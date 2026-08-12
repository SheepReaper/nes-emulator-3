namespace Sheep.Emulation.Nes.ConformanceTests;

internal enum AccuracyCoinOutcome
{
    Passed,
    Failed,
    TimedOut
}

internal sealed record AccuracyCoinResultByte(ushort Address, byte Value);

internal sealed record AccuracyCoinSingleResult(
    AccuracyCoinOutcome Outcome,
    byte Value,
    long ElapsedPpuDots);

internal sealed record AccuracyCoinRunResult(
    AccuracyCoinOutcome Outcome,
    byte Total,
    byte Passed,
    byte Skipped,
    long ElapsedPpuDots,
    IReadOnlyList<AccuracyCoinResultByte> NonPassingResults);
