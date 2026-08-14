using System.Xml.Linq;

namespace Sheep.Nes.Lab.Tests;

public sealed class ContractBoundaryTests
{
    [Fact]
    public void ContractsProject_RemainsDependencyLight()
    {
        var root = FindRepositoryRoot();
        var project = XDocument.Load(Path.Combine(root, "src", "tools", "nes-lab-contracts",
            "Sheep.Nes.Lab.Contracts.csproj"));

        Assert.Empty(project.Descendants("PackageReference"));
        Assert.Empty(project.Descendants("ProjectReference"));
    }

    [Fact]
    public void ConformanceProject_DoesNotReferenceLabExecutable()
    {
        var root = FindRepositoryRoot();
        var project = XDocument.Load(Path.Combine(root, "test", "conformance",
            "Sheep.Emulation.Nes.ConformanceTests.csproj"));
        var references = project.Descendants("ProjectReference")
            .Select(element => (string?)element.Attribute("Include"))
            .Where(value => value is not null)
            .ToArray();

        Assert.Contains(references, value => value!.Contains("nes-lab-contracts", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(references, value => value!.EndsWith("nes-lab\\Sheep.Nes.Lab.csproj", StringComparison.OrdinalIgnoreCase));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "nes-emulator-3.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
