namespace Sheep.Nes.Lab;

public sealed record TraceQuery(
    ushort? AddressStart = null,
    ushort? AddressEnd = null,
    string? Actor = null,
    bool InterruptEdgesOnly = false,
    bool DmaOverlapOnly = false,
    bool InstructionBoundariesOnly = false,
    int MaximumRecords = 64);

public sealed record TraceQueryResult(
    int SchemaVersion,
    int MatchCount,
    bool Truncated,
    IReadOnlyList<TraceClockRecord> Records)
{
    public const int CurrentSchemaVersion = 1;
}

public static class TraceQueryEngine
{
    public static TraceQueryResult Query(TraceArtifact artifact, TraceQuery query)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentNullException.ThrowIfNull(query);
        if (query.MaximumRecords <= 0)
            throw new ArgumentOutOfRangeException(nameof(query.MaximumRecords));
        if (query.AddressEnd.HasValue && !query.AddressStart.HasValue)
            throw new ArgumentException("An address range end requires a start address.", nameof(query));
        if (query.AddressStart.HasValue && query.AddressEnd < query.AddressStart)
            throw new ArgumentException("The address range end cannot precede its start.", nameof(query));

        var retainedRecords = RetainedRecords(artifact);
        var matches = new List<TraceClockRecord>();
        for (var index = 0; index < retainedRecords.Count; index++)
        {
            var record = retainedRecords[index];
            if (!Matches(record, index, retainedRecords, query)) continue;
            matches.Add(record);
        }

        var dropped = Math.Max(0, matches.Count - query.MaximumRecords);
        return new TraceQueryResult(
            TraceQueryResult.CurrentSchemaVersion,
            matches.Count,
            dropped != 0,
            matches.Skip(dropped).ToArray());
    }

    private static IReadOnlyList<TraceClockRecord> RetainedRecords(TraceArtifact artifact) =>
        artifact.Records
            .Concat((artifact.Windows ?? []).SelectMany(window => window.Records))
            .GroupBy(record => record.CpuClock)
            .Select(group => group.First())
            .OrderBy(record => record.CpuClock)
            .ToArray();

    private static bool Matches(
        TraceClockRecord record,
        int index,
        IReadOnlyList<TraceClockRecord> records,
        TraceQuery query)
    {
        if (query.AddressStart is ushort start)
        {
            var end = query.AddressEnd ?? start;
            if (!record.BusAccesses.Any(access => access.Address >= start && access.Address <= end))
                return false;
        }

        if (query.Actor is not null &&
            !string.Equals(record.Actor, query.Actor, StringComparison.OrdinalIgnoreCase))
            return false;

        if (query.InstructionBoundariesOnly && !record.Cpu.IsInstructionBoundary)
            return false;

        if (query.InterruptEdgesOnly)
        {
            if (index == 0) return false;
            var previous = records[index - 1];
            if (previous.CpuClock + 1 != record.CpuClock) return false;
            if (previous.NmiLine == record.NmiLine && previous.IrqLine == record.IrqLine)
                return false;
        }

        if (query.DmaOverlapOnly && !IsDmaOverlap(record))
            return false;

        return true;
    }

    private static bool IsDmaOverlap(TraceClockRecord record) =>
        record.CpuBus.OamDmaActive &&
        (string.Equals(record.Actor, "dmcDma", StringComparison.OrdinalIgnoreCase) ||
         record.CpuBus.DmcDmaPending || record.CpuBus.DmcDmaCycles > 0);
}
