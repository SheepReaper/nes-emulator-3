namespace Sheep.Nes.Lab.Tests;

public sealed class GatewayLearningStoreTests
{
    [Fact]
    public void Feedback_IsDeduplicatedAndBounded()
    {
        using var temporary = new TestDirectory();
        using var store = new GatewayLearningStore(Path.Combine(temporary.Path, "index.sqlite"));
        var packet = "packet-" + new string('a', 64); var evidence = "evidence-" + new string('b', 64);
        var first = store.Record(packet, [evidence], []); var second = store.Record(packet, [evidence], []);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(1, store.EvidenceWeights()[evidence]);
    }

    [Fact]
    public void Proposal_RequiresExplicitAcceptanceBeforeMemoryExists()
    {
        using var temporary = new TestDirectory();
        using var learning = new GatewayLearningStore(Path.Combine(temporary.Path, "index.sqlite"));
        using var memory = new EngineeringMemoryStore(Path.Combine(temporary.Path, "knowledge.sqlite"));
        var provenance = new[] { new EngineeringProvenance("test", "a.cs", new string('c', 64), null, null) };
        var id = learning.Propose(EngineeringMemoryKind.Fix, "Fix", "Body", provenance, "packet-" + new string('a', 64));
        Assert.Equal(MemoryProposalStatus.Pending, learning.GetProposal(id).Status);
        var accepted = learning.Accept(id, memory);
        Assert.Equal(MemoryProposalStatus.Accepted, accepted.Status);
        Assert.NotNull(accepted.AcceptedMemoryId);
    }

    private sealed class TestDirectory : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory("nes-lab-learning-").FullName;
        public void Dispose()
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(Path, recursive: true);
        }
    }
}
