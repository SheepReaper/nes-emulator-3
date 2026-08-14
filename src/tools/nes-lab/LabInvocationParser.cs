namespace Sheep.Nes.Lab;

public static class LabInvocationParser
{
    public static LabParseResult Parse(IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
            return LabParseResult.Failure("missing_command", "Expected the 'verify' command.");
        if (!string.Equals(arguments[0], "verify", StringComparison.OrdinalIgnoreCase))
            return LabParseResult.Failure("invalid_command", $"Unknown command '{arguments[0]}'.");

        var scope = VerificationScope.All;
        var planOnly = false;
        var noRestore = true;
        var continueOnFailure = false;
        var changed = false;
        var scopeSpecified = false;
        string? caseName = null;
        var traceOnFailure = false;
        var traceAlways = false;
        var exitPolicy = VerificationExitPolicy.Strict;

        for (var index = 1; index < arguments.Count; index++)
        {
            switch (arguments[index])
            {
                case "--scope":
                    scopeSpecified = true;
                    if (++index >= arguments.Count)
                        return LabParseResult.Failure("missing_scope", "The --scope option requires a value.");
                    if (!TryParseScope(arguments[index], out scope))
                        return LabParseResult.Failure("invalid_scope", $"Unknown verification scope '{arguments[index]}'.");
                    break;
                case "--plan-only":
                    planOnly = true;
                    break;
                case "--restore":
                    noRestore = false;
                    break;
                case "--continue-on-failure":
                    continueOnFailure = true;
                    break;
                case "--changed":
                    changed = true;
                    break;
                case "--case":
                    if (++index >= arguments.Count)
                        return LabParseResult.Failure("missing_case", "The --case option requires a value.");
                    caseName = arguments[index];
                    break;
                case "--trace-on-failure":
                    traceOnFailure = true;
                    break;
                case "--trace-always":
                    traceAlways = true;
                    break;
                case "--baseline-aware-exit-code":
                    exitPolicy = VerificationExitPolicy.BaselineAware;
                    break;
                case "--format":
                    if (++index >= arguments.Count)
                        return LabParseResult.Failure("missing_format", "The --format option requires a value.");
                    if (!string.Equals(arguments[index], "json", StringComparison.OrdinalIgnoreCase))
                        return LabParseResult.Failure("invalid_format", "Only JSON output is currently supported.");
                    break;
                default:
                    return LabParseResult.Failure("invalid_option", $"Unknown option '{arguments[index]}'.");
            }
        }

        if (changed && scopeSpecified)
            return LabParseResult.Failure(
                "conflicting_selection",
                "Use either --changed or --scope, not both.");
        if (caseName is not null && (changed || scope != VerificationScope.Conformance))
            return LabParseResult.Failure(
                "invalid_case_scope",
                "--case is supported only with --scope conformance.");
        if ((traceOnFailure || traceAlways) && caseName is null)
            return LabParseResult.Failure(
                "trace_requires_case",
                "Trace capture requires a named conformance --case.");
        if (traceOnFailure && traceAlways)
            return LabParseResult.Failure(
                "conflicting_trace_mode",
                "Use either --trace-on-failure or --trace-always, not both.");

        return LabParseResult.Success(new LabInvocation(
            scope,
            planOnly,
            noRestore,
            continueOnFailure,
            changed,
            LabOutputFormat.Json,
            caseName,
            traceOnFailure,
            traceAlways,
            exitPolicy));
    }

    private static bool TryParseScope(string value, out VerificationScope scope)
    {
        scope = value.ToLowerInvariant() switch
        {
            "cpu" => VerificationScope.Cpu,
            "lab-tests" => VerificationScope.LabTests,
            "conformance" => VerificationScope.Conformance,
            "winui-tests" => VerificationScope.WinUiTests,
            "library" => VerificationScope.Library,
            "winui-interop" => VerificationScope.WinUiInterop,
            "winui-app" => VerificationScope.WinUiApp,
            "ppu" => VerificationScope.Ppu,
            "apu" => VerificationScope.Apu,
            "dma" => VerificationScope.Dma,
            "bus" => VerificationScope.Bus,
            "mapper" => VerificationScope.Mapper,
            "cartridge" => VerificationScope.Cartridge,
            "debugger" => VerificationScope.Debugger,
            "winui-video" => VerificationScope.WinUiVideo,
            "winui-audio" => VerificationScope.WinUiAudio,
            "all" => VerificationScope.All,
            _ => default
        };
        return value.ToLowerInvariant() is
            "lab-tests" or "cpu" or "conformance" or "winui-tests" or "library" or "winui-interop" or "winui-app" or
            "ppu" or "apu" or "dma" or "bus" or "mapper" or "cartridge" or "debugger" or "winui-video" or "winui-audio" or "all";
    }
}
