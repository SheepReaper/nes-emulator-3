using System.Text;

namespace Sheep.Nes.Lab.Tests;

public sealed class ContextPacketBuilderTests
{
    [Fact]
    public void Build_IsDeterministicAndHonorsExactUtf8Budget()
    {
        ContextEvidence[] evidence =
        [
            new(ContextEvidenceKind.Reference, 10, new string('é', 500), "src/Cpu.cs", "abc", 12),
            new(ContextEvidenceKind.Guidance, 100, "Always preserve cycle boundaries.", "src/lib/emulation/AGENTS.md")
        ];

        var first = ContextPacketBuilder.Build(evidence, 600);
        var second = ContextPacketBuilder.Build(evidence.Reverse(), 600);

        Assert.Equal(first.Content, second.Content);
        Assert.Equal(Encoding.UTF8.GetByteCount(first.Content), first.UsedBytes);
        Assert.True(first.UsedBytes <= first.BudgetBytes);
        Assert.Contains("src/lib/emulation/AGENTS.md", first.Content, StringComparison.Ordinal);
        Assert.True(first.Truncated || first.OmittedEvidenceCount > 0);
    }

    [Fact]
    public void Build_PrioritizesEvidenceAndRetainsProvenance()
    {
        var packet = ContextPacketBuilder.Build([
            new(ContextEvidenceKind.EngineeringMemory, 1, new string('x', 500), "knowledge.db#1"),
            new(ContextEvidenceKind.VerificationFailure, 50, "cycles differed", "run.json", "def")
        ], 640);

        Assert.Contains("cycles differed", packet.Content, StringComparison.Ordinal);
        Assert.Contains("run.json", packet.Content, StringComparison.Ordinal);
        Assert.True(packet.Content.IndexOf("run.json", StringComparison.Ordinal) <
            packet.Content.IndexOf("knowledge.db#1", StringComparison.Ordinal));
        Assert.True(packet.Truncated);
    }

    [Fact]
    public void Build_RejectsEvidenceWithoutProvenance()
    {
        Assert.Throws<ArgumentException>(() => ContextPacketBuilder.Build([
            new(ContextEvidenceKind.Reference, 1, "content", "")
        ], 256));
    }

    [Fact]
    public void Build_ReservesSpaceAcrossEvidenceCategories()
    {
        var packet = ContextPacketBuilder.Build([
            new(ContextEvidenceKind.VerificationFailure, 100, new string('f', 2_000), "failure"),
            new(ContextEvidenceKind.Declaration, 90, "void Clock() { Step(); }", "Cpu.cs"),
            new(ContextEvidenceKind.AffectedTest, 80, "void TimingTest() { Assert.True(true); }", "CpuTests.cs"),
            new(ContextEvidenceKind.Guidance, 70, "Preserve clock ordering.", "AGENTS.md"),
            new(ContextEvidenceKind.RomSource, 60, "TEST_Dmc: RTS", "test.s")
        ], 2_048);

        Assert.All(packet.Categories!, category => Assert.True(category.Included > 0, category.Category));
        Assert.True(packet.UsedBytes <= 2_048);
    }

    [Fact]
    public void Build_PreservesHighConfidenceImplementationEvidence()
    {
        var packet = ContextPacketBuilder.Build([
            new(ContextEvidenceKind.Declaration, 250, new string('a', 6_000), "CpuClockDriver.cs"),
            new(ContextEvidenceKind.Declaration, 249, new string('b', 6_000), "CpuInterruptHandler.cs"),
            new(ContextEvidenceKind.Reference, 10, new string('r', 8_000), "unrelated.cs")
        ], 16_384);

        Assert.Contains("CpuClockDriver.cs", packet.Content, StringComparison.Ordinal);
        Assert.Contains("CpuInterruptHandler.cs", packet.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_AlwaysRetainsGuidanceInTightPackets()
    {
        var packet = ContextPacketBuilder.Build([
            new(ContextEvidenceKind.VerificationFailure, 100, new string('f', 900), "run.json"),
            new(ContextEvidenceKind.Declaration, 90, new string('d', 700), "Cpu.cs"),
            new(ContextEvidenceKind.Guidance, 10, "Always preserve the nearest AGENTS guidance when diagnosing a timing failure.", "AGENTS.md")
        ], 512);

        Assert.Contains("AGENTS.md", packet.Content, StringComparison.Ordinal);
        Assert.True(packet.Categories!.Single(category => category.Category == "guidance").Included > 0);
    }

    [Fact]
    public void Build_UsesTokenAwareBudgetWhenProvided()
    {
        var packet = ContextPacketBuilder.Build([
            new(ContextEvidenceKind.Guidance, 10, "Use the local evidence gateway before broad reads.", "AGENTS.md"),
            new(ContextEvidenceKind.Declaration, 9, new string('d', 1_800), "Cpu.cs"),
            new(ContextEvidenceKind.VerificationFailure, 8, new string('f', 1_800), "run.json")
        ], 512, 256);

        Assert.Equal(256, packet.BudgetTokens);
        Assert.True(packet.UsedTokens <= packet.BudgetTokens);
        Assert.Contains("AGENTS.md", packet.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_DefaultTokenBudgetTracksEstimatedContextLoad()
    {
        var packet = ContextPacketBuilder.Build([
            new(ContextEvidenceKind.VerificationFailure, 100, new string('f', 700), "run.json"),
            new(ContextEvidenceKind.Guidance, 10, "Always preserve AGENTS guidance when diagnosing a failure.", "AGENTS.md")
        ], 512);

        Assert.True(packet.UsedTokens > 0);
        Assert.True(packet.BudgetTokens >= packet.UsedTokens);
    }

    [Fact]
    public void Build_GuaranteesRequiredEvidenceAndMarkersInTightPacket()
    {
        var packet = ContextPacketBuilder.Build([
            new(ContextEvidenceKind.Declaration, 90, "class AccuracyCoinHarness { void Run() { } }",
                "test/conformance/AccuracyCoinHarness.cs", Required: true,
                RequiredMarkers: ["AccuracyCoinHarness"]),
            new(ContextEvidenceKind.Declaration, 89,
                "class ConformanceTraceCapture { void MarkCheckpoint() { } }",
                "test/conformance/ConformanceTraceCapture.cs", Required: true,
                RequiredMarkers: ["MarkCheckpoint"]),
            new(ContextEvidenceKind.Guidance, 200, new string('g', 2_000), "AGENTS.md"),
            new(ContextEvidenceKind.Reference, 500, new string('n', 8_000), "noise.txt")
        ], 2_048);

        Assert.Contains("AccuracyCoinHarness", packet.Content, StringComparison.Ordinal);
        Assert.Contains("MarkCheckpoint", packet.Content, StringComparison.Ordinal);
        Assert.All(packet.RequiredEvidence!, item =>
            Assert.True(item.Status is RequiredEvidenceStatus.Complete or RequiredEvidenceStatus.Excerpted));
        Assert.Equal(2, packet.Density!.RequiredIncluded);
        Assert.True(packet.UsedBytes <= 2_048);
    }

    [Fact]
    public void Build_ReportsMissingRequiredEvidenceWithoutClaimingSuccess()
    {
        var packet = ContextPacketBuilder.Build([
            new(ContextEvidenceKind.Declaration, 100, "class Present { }", "Present.cs", Required: true,
                RequiredMarkers: ["ExpectedMarker"])
        ], 512);

        var required = Assert.Single(packet.RequiredEvidence!);
        Assert.Equal(RequiredEvidenceStatus.Missing, required.Status);
        Assert.Equal(0, packet.Density!.RequiredIncluded);
    }

    [Fact]
    public void Build_DeduplicatesEvidenceBeforeBudgetingAndMergesMetadata()
    {
        ContextEvidence[] evidence = [
            new(ContextEvidenceKind.Declaration, 10, "class Cpu { }", "Cpu.cs", "hash", 4,
                1, ["lexical"], "evidence-same"),
            new(ContextEvidenceKind.Declaration, 20, "class Cpu { }", "Cpu.cs", "hash", 4,
                3, ["spine:implementation"], "evidence-same", true, ["Cpu"]),
            new(ContextEvidenceKind.Guidance, 1, "Use focused tests.", "AGENTS.md")
        ];

        var packet = ContextPacketBuilder.Build(evidence, 2_048);

        Assert.Equal(3, packet.InputEvidenceCount);
        Assert.Equal(2, packet.UniqueEvidenceCount);
        Assert.Equal(1, packet.DuplicateEvidenceCount);
        Assert.Equal(1, packet.Content.Split("class Cpu", StringSplitOptions.None).Length - 1);
        Assert.Contains("spine:implementation", packet.Content, StringComparison.Ordinal);
        Assert.Contains("lexical", packet.Content, StringComparison.Ordinal);
        Assert.Equal(RequiredEvidenceStatus.Complete, Assert.Single(packet.RequiredEvidence!).Status);
    }

    [Fact]
    public void Build_RejectsConflictingContentForAnEvidenceId()
    {
        Assert.Throws<InvalidDataException>(() => ContextPacketBuilder.Build([
            new(ContextEvidenceKind.Reference, 1, "first", "one", EvidenceId: "evidence-collision"),
            new(ContextEvidenceKind.Reference, 1, "second", "one", EvidenceId: "evidence-collision")
        ], 512));
    }
}
