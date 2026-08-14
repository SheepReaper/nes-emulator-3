namespace Sheep.Nes.Lab.Tests;

public sealed class EvidenceSpineTests
{
    [Fact]
    public void Apply_RequiresImplementationTestReferenceAndVerificationEvidence()
    {
        ContextEvidence[] candidates =
        [
            new(ContextEvidenceKind.Declaration, 100, "class Mapper", "src/Mapper.cs"),
            new(ContextEvidenceKind.AffectedTest, 90, "class MapperTests", "test/MapperTests.cs"),
            new(ContextEvidenceKind.Reference, 80, "MMC3 banking rules", "reference://mmc3")
        ];

        var result = EvidenceSpine.Apply("fix MMC3 banking", candidates,
            new SubsystemClassification("mapper", true, new Dictionary<string, double> { ["mapper"] = 200 }, []));
        var packet = ContextPacketBuilder.Build(result, 2_048);

        Assert.All(new[] { "implementation", "test", "contract", "verification" }, category =>
            Assert.Contains(packet.EvidenceSpine!, item => item.Category == category &&
                item.Status is EvidenceSpineStatus.Complete or EvidenceSpineStatus.Excerpted));
        Assert.Contains("verify --scope mapper", packet.Content);
    }

    [Fact]
    public void Apply_ReportsUnavailableFocusedTestInsteadOfClaimingCompleteness()
    {
        var result = EvidenceSpine.Apply("fix mapper", [
            new ContextEvidence(ContextEvidenceKind.Declaration, 100, "class Mapper", "src/Mapper.cs")
        ], new SubsystemClassification("mapper", true, new Dictionary<string, double> { ["mapper"] = 100 }, []));

        var packet = ContextPacketBuilder.Build(result, 2_048);

        Assert.Contains(packet.EvidenceSpine!, item => item.Category == "test" &&
            item.Status == EvidenceSpineStatus.Unavailable);
    }
}
