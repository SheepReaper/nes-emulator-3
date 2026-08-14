using Sheep.Nes.Lab;

namespace Sheep.Nes.Lab.Tests;

public sealed class VerificationCommandCatalogTests
{
    [Fact]
    public void CreateNamedConformance_AddsDisplayNameFilter()
    {
        var command = VerificationCommandCatalog.CreateNamedConformance(
            "Delta Modulation Channel", noRestore: true);

        Assert.Equal(VerificationScope.Conformance, command.Scope);
        Assert.Contains("--filter-display-name", command.Arguments);
        Assert.Contains("*Delta Modulation Channel*", command.Arguments);
    }

    [Fact]
    public void CreateNamedConformance_PreservesTraceEnvironment()
    {
        IReadOnlyDictionary<string, string> environment = new Dictionary<string, string>
        {
            ["NES_LAB_TRACE_PATH"] = "trace.json"
        };

        var command = VerificationCommandCatalog.CreateNamedConformance(
            "case", noRestore: true, environment);

        Assert.Equal("trace.json", command.Environment!["NES_LAB_TRACE_PATH"]);
    }
    [Fact]
    public void Create_CpuScope_UsesRepositoryCpuTestProject()
    {
        var commands = VerificationCommandCatalog.Create(VerificationScope.Cpu, noRestore: true);

        var command = Assert.Single(commands);
        Assert.Equal("dotnet", command.FileName);
        Assert.Equal(
            ["test", "test/cpu/Sheep.Emulation.Nes.Tests.csproj", "--no-restore", "--output", "Detailed"],
            command.Arguments);
    }

    [Theory]
    [InlineData(VerificationScope.Ppu, "*Ppu*")]
    [InlineData(VerificationScope.Apu, "*Apu*")]
    [InlineData(VerificationScope.Dma, "*Dma*")]
    [InlineData(VerificationScope.Mapper, "*Mapper*")]
    public void Create_LogicalScope_UsesMtpClassFilter(VerificationScope scope, string filter)
    {
        var command = Assert.Single(VerificationCommandCatalog.Create(scope, noRestore: true));

        Assert.Equal("test/cpu/Sheep.Emulation.Nes.Tests.csproj", command.Arguments[1]);
        Assert.Contains("--filter-class", command.Arguments);
        Assert.Contains(filter, command.Arguments);
    }

    [Fact]
    public void Create_AllScope_UsesStableRepositoryOrder()
    {
        var commands = VerificationCommandCatalog.Create(VerificationScope.All, noRestore: false);

        Assert.Collection(
            commands,
            command => Assert.Equal(VerificationScope.LabTests, command.Scope),
            command => Assert.Equal(VerificationScope.Cpu, command.Scope),
            command => Assert.Equal(VerificationScope.Conformance, command.Scope),
            command => Assert.Equal(VerificationScope.WinUiTests, command.Scope),
            command => Assert.Equal(VerificationScope.Library, command.Scope),
            command => Assert.Equal(VerificationScope.WinUiInterop, command.Scope),
            command => Assert.Equal(VerificationScope.WinUiApp, command.Scope));
        Assert.All(commands, command => Assert.DoesNotContain("--no-restore", command.Arguments));
    }
}
