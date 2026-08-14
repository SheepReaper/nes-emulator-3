namespace Sheep.Nes.Lab.Tests;

public sealed class GatewayManifestTests
{
    [Fact]
    public async Task Validate_DistinguishesCurrentStaleAndMissingArtifacts()
    {
        var root = FindRepositoryRoot();
        var directory = Directory.CreateTempSubdirectory("nes-lab-manifest-");
        try
        {
            var assembly = Path.Combine(directory.FullName, "gateway.dll");
            await File.WriteAllTextAsync(assembly, "assembly", TestContext.Current.CancellationToken);
            await GatewayManifestService.WriteAsync(root, assembly, "dotnet", [assembly, "mcp"],
                TestContext.Current.CancellationToken);
            Assert.Equal(GatewayHealthState.Healthy,
                GatewayManifestService.Validate(root, assembly, out _, out _));
            await File.AppendAllTextAsync(assembly, "changed", TestContext.Current.CancellationToken);
            Assert.Equal(GatewayHealthState.Stale,
                GatewayManifestService.Validate(root, assembly, out _, out _));
            File.Delete(assembly);
            Assert.Equal(GatewayHealthState.Missing,
                GatewayManifestService.Validate(root, assembly, out _, out _));
        }
        finally { Directory.Delete(directory.FullName, recursive: true); }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "nes-emulator-3.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
