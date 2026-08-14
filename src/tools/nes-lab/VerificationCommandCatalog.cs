namespace Sheep.Nes.Lab;

public static class VerificationCommandCatalog
{
    public static readonly IReadOnlyList<VerificationScope> AllConcreteScopes =
    [
        VerificationScope.LabTests,
        VerificationScope.Cpu,
        VerificationScope.Conformance,
        VerificationScope.WinUiTests,
        VerificationScope.Library,
        VerificationScope.WinUiInterop,
        VerificationScope.WinUiApp
    ];

    public static IReadOnlyList<VerificationCommand> Create(VerificationScope scope, bool noRestore)
    {
        if (scope == VerificationScope.All)
            return Create(AllConcreteScopes, noRestore);

        return [CreateSingle(scope, noRestore)];
    }

    public static IReadOnlyList<VerificationCommand> Create(
        IEnumerable<VerificationScope> scopes,
        bool noRestore) => scopes.Select(scope => CreateSingle(scope, noRestore)).ToArray();

    public static VerificationCommand CreateNamedConformance(
        string caseName,
        bool noRestore,
        IReadOnlyDictionary<string, string>? environment = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caseName);
        var command = CreateSingle(VerificationScope.Conformance, noRestore);
        var arguments = command.Arguments.ToList();
        arguments.Add("--filter-display-name");
        arguments.Add($"*{caseName}*");
        return command with { Arguments = arguments, Environment = environment };
    }

    private static VerificationCommand CreateSingle(VerificationScope scope, bool noRestore)
    {
        if (LogicalFilter(scope) is { } logical)
        {
            var logicalArguments = new List<string> { "test", logical.Project };
            if (noRestore) logicalArguments.Add("--no-restore");
            logicalArguments.AddRange(["--output", "Detailed", "--filter-class", logical.Filter]);
            return new VerificationCommand(scope, "dotnet", logicalArguments);
        }
        var (verb, project) = scope switch
        {
            VerificationScope.LabTests => ("test", "test/nes-lab/Sheep.Nes.Lab.Tests.csproj"),
            VerificationScope.Cpu => ("test", "test/cpu/Sheep.Emulation.Nes.Tests.csproj"),
            VerificationScope.Conformance => ("test", "test/conformance/Sheep.Emulation.Nes.ConformanceTests.csproj"),
            VerificationScope.WinUiTests => ("test", "test/emulator-winui/EmuSheep.Tests.csproj"),
            VerificationScope.Library => ("build", "src/lib/emulation/Sheep.Emulation.Nes.csproj"),
            VerificationScope.WinUiInterop => ("build", "src/lib/interop-winui/Sheep.WinUI.Interop.csproj"),
            VerificationScope.WinUiApp => ("build", "src/emulator-winui/EmuSheep.csproj"),
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "A concrete verification scope is required.")
        };

        List<string> arguments = [verb, project];
        if (noRestore)
            arguments.Add("--no-restore");
        if (verb == "test")
        {
            arguments.Add("--output");
            arguments.Add("Detailed");
        }

        return new VerificationCommand(scope, "dotnet", arguments);
    }

    private static (string Project, string Filter)? LogicalFilter(VerificationScope scope) => scope switch
    {
        VerificationScope.Ppu => ("test/cpu/Sheep.Emulation.Nes.Tests.csproj", "*Ppu*"),
        VerificationScope.Apu => ("test/cpu/Sheep.Emulation.Nes.Tests.csproj", "*Apu*"),
        VerificationScope.Dma => ("test/cpu/Sheep.Emulation.Nes.Tests.csproj", "*Dma*"),
        VerificationScope.Bus => ("test/cpu/Sheep.Emulation.Nes.Tests.csproj", "*Bus*"),
        VerificationScope.Mapper => ("test/cpu/Sheep.Emulation.Nes.Tests.csproj", "*Mapper*"),
        VerificationScope.Cartridge => ("test/cpu/Sheep.Emulation.Nes.Tests.csproj", "*Cartridge*"),
        VerificationScope.Debugger => ("test/cpu/Sheep.Emulation.Nes.Tests.csproj", "*Debugger*"),
        VerificationScope.WinUiVideo => ("test/emulator-winui/EmuSheep.Tests.csproj", "*Frame*"),
        VerificationScope.WinUiAudio => ("test/emulator-winui/EmuSheep.Tests.csproj", "*Audio*"),
        _ => null
    };
}
