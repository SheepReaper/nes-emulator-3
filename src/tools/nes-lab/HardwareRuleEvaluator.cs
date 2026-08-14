namespace Sheep.Nes.Lab;

public sealed record HardwareRuleObservation(string RuleId, string Classification, string Observation,
    string ReferenceId, string ReferenceDigest, string ReferenceSection, IReadOnlyList<ulong> CpuClocks);

public static class HardwareRuleEvaluator
{
    public static IReadOnlyList<HardwareRuleObservation> Evaluate(TraceArtifact trace,
        IReadOnlyDictionary<string, ReferenceDocument> references)
    {
        if (trace.SchemaVersion < 4) return [];
        List<HardwareRuleObservation> observations = [];
        if (references.TryGetValue("nesdev-dma", out var dma))
        {
            var controller = trace.Records.Where(record => record.BusAccesses.Any(access =>
                access.Address is 0x4016 or 0x4017)).ToArray();
            var repeated = controller.Zip(controller.Skip(1)).Where(pair =>
                pair.Second.CpuClock - pair.First.CpuClock <= 2 && pair.First.BusAccesses.Any(left =>
                    pair.Second.BusAccesses.Any(right => left.Address == right.Address))).ToArray();
            if (repeated.Length > 0)
                observations.Add(new("dma-controller-repeated-read", "cited-observation",
                    "The captured DMA window contains repeated side-effecting controller-port reads on adjacent CPU clocks. This is an observation, not a causality claim.",
                    dma.Entry.Id, dma.Sha256, "DMC DMA conflicts", repeated.SelectMany(pair =>
                        new[] { pair.First.CpuClock, pair.Second.CpuClock }).Distinct().Take(16).ToArray()));

            var overlap = trace.Records.Where(record => record.CpuBus.OamDmaActive &&
                (record.CpuBus.DmcDmaPending || record.Actor == "dmcDma")).ToArray();
            if (overlap.Length > 0)
                observations.Add(new("dmc-oam-arbitration", "cited-observation",
                    "The captured window contains clocks where OAM DMA is active while DMC DMA is pending or owns the bus. Phase labels expose the observed arbitration order without assigning causality.",
                    dma.Entry.Id, dma.Sha256, "DMC DMA during OAM DMA", overlap.Select(item => item.CpuClock).Take(16).ToArray()));

            var dmcPhases = trace.Records.Where(record =>
                record.Phase?.DmaEvent.StartsWith("dmc", StringComparison.OrdinalIgnoreCase) == true).ToArray();
            if (dmcPhases.Length > 0 && overlap.Length == 0)
                observations.Add(new("dmc-phase-sequence", "cited-observation",
                    "The captured window contains explicitly labeled DMC request, halt, alignment, dummy, or fetch phases. The labels expose observed sampling order without assigning causality.",
                    dma.Entry.Id, dma.Sha256, "DMC DMA", dmcPhases.Select(item => item.CpuClock).Take(16).ToArray()));
        }
        return observations;
    }
}
