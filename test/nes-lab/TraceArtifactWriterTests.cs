using System.Text.Json;

namespace Sheep.Nes.Lab.Tests;

public sealed class TraceArtifactWriterTests
{
    [Fact]
    public async Task WriteAsync_CreatesJsonArtifactAndReturnsItsAbsolutePath()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"nes-lab-trace-{Guid.NewGuid():N}");
        try
        {
            var artifact = TraceArtifactFactory.Create([], new TraceArtifactMetadata(
                "hash", "commit", DateTimeOffset.UnixEpoch,
                new TraceRunMetadata("rom.nes", "NTSC", null, null)));

            var path = await new TraceArtifactWriter().WriteAsync(
                artifact, Path.Combine(directory, "trace.json"),
                TestContext.Current.CancellationToken);

            Assert.True(Path.IsPathFullyQualified(path));
            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(
                path, TestContext.Current.CancellationToken));
            Assert.Equal(TraceArtifact.CurrentSchemaVersion,
                document.RootElement.GetProperty("schemaVersion").GetInt32());
            Assert.Equal("nes-cpu-clock-trace",
                document.RootElement.GetProperty("artifactKind").GetString());
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
}
