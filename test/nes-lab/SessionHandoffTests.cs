namespace Sheep.Nes.Lab.Tests;

public sealed class SessionHandoffTests
{
    [Fact]
    public void Build_SnapshotsCurrentAndRejectedMemoryWithoutStaleClaims()
    {
        var current = Entry(1, EngineeringMemoryKind.ConfirmedFact, stale: false);
        var stale = Entry(2, EngineeringMemoryKind.Fix, stale: true);
        var rejected = Entry(3, EngineeringMemoryKind.RejectedHypothesis, stale: false);

        var handoff = SessionHandoffBuilder.Build("task", new RepositoryProvenance("head", true,
            "diff", "lab", 4), null, [], [current, stale, rejected], [], null, "verify --changed");

        Assert.Single(handoff.AcceptedMemory);
        Assert.Single(handoff.RejectedHypotheses);
        Assert.Single(handoff.StaleMemory);
        Assert.Equal("verify --changed", handoff.RecommendedNextCommand);
    }

    private static EngineeringMemoryEntry Entry(long id, EngineeringMemoryKind kind, bool stale) =>
        new(id, kind, "title", "body", [new("source", "file", "hash", 1, "head")],
            DateTimeOffset.UnixEpoch, IsStale: stale);
}
