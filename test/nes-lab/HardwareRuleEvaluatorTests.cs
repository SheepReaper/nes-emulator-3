namespace Sheep.Nes.Lab.Tests;

public sealed class HardwareRuleEvaluatorTests
{
    [Fact]
    public void Evaluate_ReportsCitedObservationsWithoutClaimingDivergence()
    {
        var reference = new ReferenceDocument(
            new ReferenceEntry("nesdev-dma", "DMA", "https://www.nesdev.org/wiki/DMA", "", "mediawiki",
                "NESdev", ["dma"], [], new string('a', 64), "1", "cache-only", []), "content", new string('a', 64), null);
        var artifact = new TraceArtifact(4, "nes-cpu-clock-trace", "rom", "source", DateTimeOffset.UnixEpoch,
            new("rom", "Ntsc", "suite", "case"), 2, 0, false,
            [Record(10, true), Record(11, true)]);

        var result = HardwareRuleEvaluator.Evaluate(artifact,
            new Dictionary<string, ReferenceDocument> { ["nesdev-dma"] = reference });

        Assert.Contains(result, item => item.RuleId == "dma-controller-repeated-read");
        Assert.Contains(result, item => item.RuleId == "dmc-oam-arbitration");
        Assert.All(result, item =>
        {
            Assert.Equal("cited-observation", item.Classification);
            Assert.DoesNotContain("divergence", item.Observation, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(reference.Sha256, item.ReferenceDigest);
        });
    }

    [Fact]
    public void Evaluate_RejectsPhaseIncompleteTraceAsRuleEvidence()
    {
        var artifact = new TraceArtifact(3, "nes-cpu-clock-trace", "rom", "source", DateTimeOffset.UnixEpoch,
            new("rom", "Ntsc", null, null), 1, 0, false, [Record(1, true)]);
        Assert.Empty(HardwareRuleEvaluator.Evaluate(artifact, new Dictionary<string, ReferenceDocument>()));
    }

    private static TraceClockRecord Record(ulong clock, bool overlap) => new(clock, 0, 0,
        new(0, 0, 0, 0, 0x8000, 0, 0, 0, clock, true),
        new(0, 0, 0, 0, 0, 0, 0, false, 0, 0, 0, 0, false, 0), "dmcDma", 0x4016,
        false, false, new(0, 0, 0x4016, true, false, overlap, 0, false, overlap, 1, 0xC000),
        [new("read", 0x4016, 0)], new("postCpuClock", "read", "get", 0, false, false, true, "dmcTransfer"));
}
