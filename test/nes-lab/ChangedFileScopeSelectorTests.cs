using Sheep.Nes.Lab;

namespace Sheep.Nes.Lab.Tests;

public sealed class ChangedFileScopeSelectorTests
{
    [Fact]
    public void Select_EmulationCore_RequiresCpuConformanceAndLibrary()
    {
        var scopes = ChangedFileScopeSelector.Select(["src/lib/emulation/Cpu.cs"]);

        Assert.Equal(
            [VerificationScope.Cpu, VerificationScope.Conformance, VerificationScope.Library],
            scopes);
    }

    [Fact]
    public void Select_WinUiInterop_RequiresInteropConsumerAndTests()
    {
        var scopes = ChangedFileScopeSelector.Select(["src/lib/interop-winui/AudioFrameBuffer.cs"]);

        Assert.Equal(
            [VerificationScope.WinUiTests, VerificationScope.WinUiInterop, VerificationScope.WinUiApp],
            scopes);
    }

    [Fact]
    public void Select_LabCode_RequiresLabAndConformanceTests()
    {
        var scopes = ChangedFileScopeSelector.Select(["src/tools/nes-lab/Program.cs"]);

        Assert.Equal([VerificationScope.LabTests, VerificationScope.Conformance], scopes);
    }

    [Fact]
    public void Select_LabContracts_RequiresLabAndConformanceTests()
    {
        var scopes = ChangedFileScopeSelector.Select([
            "src/tools/nes-lab-contracts/TraceArtifact.cs"
        ]);

        Assert.Equal([VerificationScope.LabTests, VerificationScope.Conformance], scopes);
    }

    [Fact]
    public void Select_UnknownFile_ConservativelyRequiresAllScopes()
    {
        var scopes = ChangedFileScopeSelector.Select(["build/unknown.targets"]);

        Assert.Equal(VerificationCommandCatalog.AllConcreteScopes, scopes);
    }

    [Fact]
    public void SelectWithExplanation_ReportsOmissionsAndFallbackPolicy()
    {
        var focused = ChangedFileScopeSelector.SelectWithExplanation(["test/cpu/CpuTests.cs"]);
        Assert.Contains(VerificationScope.Conformance, focused.Omitted);
        Assert.False(focused.ConservativeFallback);
        Assert.NotEmpty(focused.Reasons);
        Assert.True(ChangedFileScopeSelector.SelectWithExplanation(["unknown.file"]).ConservativeFallback);
    }

    [Theory]
    [InlineData("tasks/local-agent-offload-todo.md")]
    [InlineData("src/lib/interop-winui/AudioFrameBuffer.cs")]
    [InlineData("src/emulator-winui/MainPage.xaml.cs")]
    public void SelectWithExplanation_EverySelectedPathHasReason(string path)
    {
        var selection = ChangedFileScopeSelector.SelectWithExplanation([path]);

        Assert.NotEmpty(selection.Selected);
        Assert.Contains(selection.Reasons, reason => reason.StartsWith(path, StringComparison.Ordinal));
    }

    [Fact]
    public void Select_MultipleFiles_DeduplicatesInStableOrder()
    {
        var scopes = ChangedFileScopeSelector.Select([
            "test/cpu/CpuTests.cs",
            "src/emulator-winui/MainPage.xaml.cs",
            "test/cpu/AudioTests.cs"
        ]);

        Assert.Equal(
            [VerificationScope.Cpu, VerificationScope.WinUiTests, VerificationScope.WinUiApp],
            scopes);
    }
}
