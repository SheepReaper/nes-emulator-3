namespace Sheep.Nes.Lab.Tests;

public sealed class TraceArtifactReaderTests
{
    [Fact]
    public async Task ReadAsync_RoundTripsWriterOutput()
    {
        var path = Path.Combine(Path.GetTempPath(), $"trace-{Guid.NewGuid():N}.json");
        try
        {
            var artifact = new TraceArtifact(1, "nes-cpu-clock-trace", "hash", "commit",
                DateTimeOffset.UnixEpoch, new TraceRunMetadata("rom", "NTSC", null, null),
                0, 0, false, []);
            await new TraceArtifactWriter().WriteAsync(
                artifact, path, TestContext.Current.CancellationToken);

            var loaded = await new TraceArtifactReader().ReadAsync(
                path, TestContext.Current.CancellationToken);

            Assert.Equal(artifact.SchemaVersion, loaded.SchemaVersion);
            Assert.Equal(artifact.RomSha256, loaded.RomSha256);
            Assert.Equal(artifact.Run, loaded.Run);
            Assert.Empty(loaded.Records);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ReadAsync_RejectsExcessiveNestedBusAccesses()
    {
        var path = Path.Combine(Path.GetTempPath(), $"trace-{Guid.NewGuid():N}.json");
        try
        {
            var accesses = Enumerable.Range(0, TraceArtifactReader.MaximumBusAccessesPerRecord + 1)
                .Select(index => new TraceBusAccess("cpuRead", (ushort)index, 0)).ToArray();
            var record = new TraceClockRecord(1, 0, 0,
                new TraceCpuState(0, 0, 0, 0, 0, 0, 0, 0, 1, true),
                new TracePpuState(0, 0, 0, 0, 0, 0, 0, false, 0, 0, 0, 0, false, 0),
                "cpu", 0, false, false,
                new TraceCpuBusState(0, 0, 0, false, false, false, 0, false, false, 0, 0), accesses);
            await new TraceArtifactWriter().WriteAsync(new TraceArtifact(
                3, "nes-cpu-clock-trace", "hash", "commit", DateTimeOffset.UnixEpoch,
                new TraceRunMetadata("rom", "NTSC", null, null), 1, 0, false, [record]), path,
                TestContext.Current.CancellationToken);

            await Assert.ThrowsAsync<InvalidDataException>(() =>
                new TraceArtifactReader().ReadAsync(path, TestContext.Current.CancellationToken));
        }
        finally { File.Delete(path); }
    }
}
