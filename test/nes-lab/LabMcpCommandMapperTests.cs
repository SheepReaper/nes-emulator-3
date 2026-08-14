using System.Text.Json;

namespace Sheep.Nes.Lab.Tests;

public sealed class LabMcpCommandMapperTests
{
    [Fact]
    public void Map_ContextBuild_ProducesExistingCliArguments()
    {
        using var document = JsonDocument.Parse("""
            { "symbol": "CpuClockDriver", "budgetBytes": 4096 }
            """);

        var arguments = LabMcpCommandMapper.Map("context", "build", document.RootElement);

        Assert.Equal(["context", "build", "--symbol", "CpuClockDriver", "--budget", "4096"], arguments);
    }

    [Fact]
    public void Map_ContextBuild_AcceptsStableSymbolId()
    {
        using var document = JsonDocument.Parse("""{ "symbolId": "stable-id", "budgetBytes": 2048 }""");

        var arguments = LabMcpCommandMapper.Map("context", "build", document.RootElement);

        Assert.Equal(["context", "build", "--id", "stable-id", "--budget", "2048"], arguments);
    }

    [Fact]
    public void Map_RejectsUnknownOperationsInsteadOfPassingThemToShell()
    {
        using var document = JsonDocument.Parse("{}");

        Assert.Throws<KeyNotFoundException>(() =>
            LabMcpCommandMapper.Map("verify", "delete-everything", document.RootElement));
    }

    [Fact]
    public void Map_TraceDiff_RequiresArtifactPaths()
    {
        using var document = JsonDocument.Parse("{ \"expectedArtifactPath\": \"a.json\" }");

        var exception = Assert.Throws<ArgumentException>(() =>
            LabMcpCommandMapper.Map("trace", "diff", document.RootElement));
        Assert.Contains("actualArtifactPath", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Map_TraceQuery_RejectsPathOutsideRepositoryForMcp()
    {
        var root = Path.Combine(Path.GetTempPath(), $"nes-lab-root-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            using var document = JsonDocument.Parse("{ \"artifactPath\": \"../outside.json\" }");
            Assert.Throws<UnauthorizedAccessException>(() =>
                LabMcpCommandMapper.Map("trace", "query", document.RootElement, root));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void Map_InlineExperiment_PassesJsonWithoutCreatingAFilePath()
    {
        using var document = JsonDocument.Parse("""{"scenario":{"schemaVersion":1,"name":"safe"}}""");

        var arguments = LabMcpCommandMapper.Map("experiment", "run-inline", document.RootElement);

        Assert.Equal("--inline", arguments[2]);
        Assert.Equal("--mcp-safe", arguments[^1]);
    }

    [Fact]
    public void Map_Verify_ExposesBaselineAwareExitPolicy()
    {
        using var document = JsonDocument.Parse("""{"scope":"conformance","baselineAwareExitCode":true}""");

        var arguments = LabMcpCommandMapper.Map("verify", "run", document.RootElement);

        Assert.Contains("--baseline-aware-exit-code", arguments);
    }
}
