namespace Sheep.Nes.Lab;

public static class ChangedFileScopeSelector
{
    public sealed record Selection(IReadOnlyList<VerificationScope> Selected,
        IReadOnlyList<VerificationScope> Omitted, IReadOnlyList<string> Reasons, bool ConservativeFallback);

    public static IReadOnlyList<VerificationScope> Select(IEnumerable<string> changedFiles) =>
        SelectWithExplanation(changedFiles).Selected;

    public static Selection SelectWithExplanation(IEnumerable<string> changedFiles)
    {
        HashSet<VerificationScope> selected = [];
        List<string> reasons = [];
        var fallback = false;
        foreach (var changedFile in changedFiles)
        {
            var path = changedFile.Replace('\\', '/').TrimStart('/').ToLowerInvariant();
            if (path.StartsWith("src/lib/emulation/", StringComparison.Ordinal))
            {
                selected.UnionWith([
                    VerificationScope.Cpu,
                    VerificationScope.Conformance,
                    VerificationScope.Library
                ]);
                reasons.Add($"{changedFile}: emulation library dependency closure reaches CPU, conformance, and library builds.");
            }
            else if (path.StartsWith("test/cpu/", StringComparison.Ordinal))
            {
                selected.Add(VerificationScope.Cpu);
                reasons.Add($"{changedFile}: owned by the CPU test project.");
            }
            else if (path.StartsWith("test/conformance/", StringComparison.Ordinal))
            {
                selected.Add(VerificationScope.Conformance);
                reasons.Add($"{changedFile}: owned by the conformance project.");
            }
            else if (path.StartsWith("src/lib/interop-winui/", StringComparison.Ordinal))
            {
                selected.UnionWith([
                    VerificationScope.WinUiTests,
                    VerificationScope.WinUiInterop,
                    VerificationScope.WinUiApp
                ]);
                reasons.Add($"{changedFile}: WinUI interop dependency closure reaches adapter tests, interop, and app builds.");
            }
            else if (path.StartsWith("src/emulator-winui/", StringComparison.Ordinal))
            {
                selected.UnionWith([VerificationScope.WinUiTests, VerificationScope.WinUiApp]);
                reasons.Add($"{changedFile}: owned by the WinUI application and its tests.");
            }
            else if (path.StartsWith("test/emulator-winui/", StringComparison.Ordinal))
            {
                selected.Add(VerificationScope.WinUiTests);
                reasons.Add($"{changedFile}: owned by the WinUI test project.");
            }
            else if (path.StartsWith("src/tools/nes-lab-contracts/", StringComparison.Ordinal))
            {
                selected.UnionWith([VerificationScope.LabTests, VerificationScope.Conformance]);
                reasons.Add($"{changedFile}: shared Lab contracts are consumed by Lab and conformance tests.");
            }
            else if (path.StartsWith("src/tools/nes-lab/", StringComparison.Ordinal) ||
                     path.StartsWith("test/nes-lab/", StringComparison.Ordinal) ||
                     path.StartsWith("tasks/", StringComparison.Ordinal) ||
                     path.EndsWith("agents.md", StringComparison.Ordinal) ||
                     path == ".gitignore")
            {
                selected.UnionWith([VerificationScope.LabTests, VerificationScope.Conformance]);
                reasons.Add($"{changedFile}: NES Lab tooling, guidance, or planning affects Lab behavior and conformance orchestration.");
            }
            else
            {
                selected.UnionWith(VerificationCommandCatalog.AllConcreteScopes);
                reasons.Add($"{changedFile}: ownership is ambiguous; selected every scope conservatively.");
                fallback = true;
            }
        }

        var ordered = VerificationCommandCatalog.AllConcreteScopes
            .Where(selected.Contains)
            .ToArray();
        return new(ordered, VerificationCommandCatalog.AllConcreteScopes.Except(ordered).ToArray(), reasons, fallback);
    }
}
