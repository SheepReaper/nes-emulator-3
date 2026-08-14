namespace Sheep.Nes.Lab.Tests;

public sealed class EngineeringMemoryStoreTests
{
    [Fact]
    public void AddAndSearch_PersistsTypedProvenanceBearingRecords()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nes-lab-memory-{Guid.NewGuid():N}.db");
        try
        {
            using (var store = new EngineeringMemoryStore(path))
            {
                store.Add(new EngineeringMemoryEntry(
                    0, EngineeringMemoryKind.ConfirmedFact,
                    "DMC fetch priority", "DMC fetches preempt OAM DMA get cycles.",
                    [new EngineeringProvenance("source", "apu_ref.md", "abc", 42, "commit")],
                    DateTimeOffset.UnixEpoch));
                store.Add(new EngineeringMemoryEntry(
                    0, EngineeringMemoryKind.Hypothesis,
                    "DMC timing idea", "Maybe DMC fetches occur one clock earlier.",
                    [new EngineeringProvenance("trace", "trace.json", "def", null, "commit")],
                    DateTimeOffset.UnixEpoch));
            }

            using var reopened = new EngineeringMemoryStore(path);
            var facts = reopened.Search("DMC fetch", EngineeringMemoryKind.ConfirmedFact);

            var fact = Assert.Single(facts);
            Assert.True(fact.Id > 0);
            Assert.Equal(EngineeringMemoryKind.ConfirmedFact, fact.Kind);
            Assert.Equal("apu_ref.md", Assert.Single(fact.Provenance).Source);
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + "-shm");
            File.Delete(path + "-wal");
        }
    }

    [Fact]
    public void Add_RejectsRecordsWithoutProvenance()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nes-lab-memory-{Guid.NewGuid():N}.db");
        try
        {
            using var store = new EngineeringMemoryStore(path);
            var entry = new EngineeringMemoryEntry(
                0, EngineeringMemoryKind.Observation, "title", "body", [], DateTimeOffset.UtcNow);

            Assert.Throws<ArgumentException>(() => store.Add(entry));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Search_IsBoundedAndSeparatesRejectedHypotheses()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nes-lab-memory-{Guid.NewGuid():N}.db");
        try
        {
            using var store = new EngineeringMemoryStore(path);
            for (var index = 0; index < 3; index++)
                store.Add(Entry(EngineeringMemoryKind.Observation, $"clock observation {index}"));
            store.Add(Entry(EngineeringMemoryKind.RejectedHypothesis, "clock hypothesis rejected"));

            var results = store.Search("clock", maximumResults: 2);

            Assert.Equal(2, results.Count);
            Assert.DoesNotContain(results, item => item.Kind == EngineeringMemoryKind.RejectedHypothesis);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static EngineeringMemoryEntry Entry(EngineeringMemoryKind kind, string title) => new(
        0, kind, title, "clock evidence", [new EngineeringProvenance(
        "test", "fixture", "hash", 1, "commit")], DateTimeOffset.UnixEpoch);

    [Fact]
    public void Supersede_CreatesImmutableRevisionLink()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nes-lab-memory-{Guid.NewGuid():N}.db");
        try
        {
            using var store = new EngineeringMemoryStore(path);
            var original = store.Add(Entry(EngineeringMemoryKind.Hypothesis, "old theory"));
            var replacement = store.Supersede(original,
                Entry(EngineeringMemoryKind.RejectedHypothesis, "old theory rejected"));

            Assert.Equal(original, store.Get(replacement).SupersedesId);
            Assert.Equal("old theory", store.Get(original).Title);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Validate_LabelsChangedSourcesStaleAndSearchExcludesThem()
    {
        var root = Path.Combine(Path.GetTempPath(), $"nes-lab-memory-root-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var source = Path.Combine(root, "source.txt");
        File.WriteAllText(source, "current");
        var path = Path.Combine(root, "memory.db");
        try
        {
            using var store = new EngineeringMemoryStore(path);
            store.Add(new EngineeringMemoryEntry(0, EngineeringMemoryKind.ConfirmedFact,
                "clock fact", "clock evidence", [new EngineeringProvenance(
                    "source", "source.txt", new string('0', 64), null, null)], DateTimeOffset.UnixEpoch));

            var validation = Assert.Single(store.Validate(root));

            Assert.True(validation.IsStale);
            Assert.Empty(store.Search("clock"));
            Assert.Single(store.Stale());
        }
        finally { Directory.Delete(root, true); }
    }
}
