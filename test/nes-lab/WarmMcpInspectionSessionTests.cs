using System.Text.Json;

namespace Sheep.Nes.Lab.Tests;

public sealed class WarmMcpInspectionSessionTests
{
    [Fact]
    public async Task RepeatedCodeInspection_ReusesOneWorkspace()
    {
        var root = FindRepositoryRoot();
        await using var session = new WarmMcpInspectionSession(root);
        var arguments = JsonSerializer.SerializeToElement(new { symbol = "CpuDmaController", maximumResults = 4 });

        var first = await session.TryExecuteAsync("code", "find", arguments, TestContext.Current.CancellationToken);
        var second = await session.TryExecuteAsync("code", "find", arguments, TestContext.Current.CancellationToken);

        Assert.NotNull(first); Assert.NotNull(second);
        Assert.Equal(1, session.WorkspaceLoads);
        Assert.True(first.Value.GetProperty("success").GetBoolean());
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "nes-emulator-3.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
