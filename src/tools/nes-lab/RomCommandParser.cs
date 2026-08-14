namespace Sheep.Nes.Lab;

public abstract record RomInvocation(string? ManifestPath, string? AssetRoot);
public sealed record RomListInvocation(
    string? Suite, string? ManifestPath = null, string? AssetRoot = null, int MaximumResults = 32)
    : RomInvocation(ManifestPath, AssetRoot);
public sealed record RomShowInvocation(
    string? Suite, string Name, string? ManifestPath = null, string? AssetRoot = null)
    : RomInvocation(ManifestPath, AssetRoot);
public sealed record RomSourceInvocation(
    string? Suite,
    string Name,
    string? Symbol,
    string? Text,
    int MaximumResults,
    string? ManifestPath = null,
    string? AssetRoot = null) : RomInvocation(ManifestPath, AssetRoot);
public sealed record RomDiagnoseInvocation(
    string? Suite,
    string Name,
    int Code,
    string? ManifestPath = null,
    string? AssetRoot = null) : RomInvocation(ManifestPath, AssetRoot);
public sealed record RomParseResult(RomInvocation? Invocation, LabError? Error);

public static class RomCommandParser
{
    public static RomParseResult Parse(IReadOnlyList<string> arguments)
    {
        if (arguments.Count < 2 || !arguments[0].Equals("rom", StringComparison.OrdinalIgnoreCase))
            return Failure("invalid_command", "Expected 'rom list' or 'rom show'.");
        string? suite = null, name = null, manifest = null, assets = null, symbol = null, text = null;
        int? code = null;
        var maximumResults = 32;
        for (var index = 2; index < arguments.Count; index++)
        {
            var option = arguments[index];
            if (++index >= arguments.Count)
                return Failure("missing_value", $"{option} requires a value.");
            switch (option)
            {
                case "--suite": suite = arguments[index]; break;
                case "--name": name = arguments[index]; break;
                case "--manifest": manifest = arguments[index]; break;
                case "--assets": assets = arguments[index]; break;
                case "--symbol": symbol = arguments[index]; break;
                case "--text": text = arguments[index]; break;
                case "--max" when int.TryParse(arguments[index], out var parsed) && parsed is > 0 and <= 256:
                    maximumResults = parsed; break;
                case "--code" when int.TryParse(arguments[index], out var parsedCode) && parsedCode >= 0:
                    code = parsedCode; break;
                default: return Failure("invalid_option", $"Unknown option '{option}'.");
            }
        }

        return arguments[1].ToLowerInvariant() switch
        {
            "list" => new RomParseResult(new RomListInvocation(suite, manifest, assets, maximumResults), null),
            "show" when !string.IsNullOrWhiteSpace(name) =>
                new RomParseResult(new RomShowInvocation(suite, name, manifest, assets), null),
            "show" => Failure("missing_rom_name", "rom show requires --name."),
            "source" when !string.IsNullOrWhiteSpace(name) &&
                              (!string.IsNullOrWhiteSpace(symbol) || !string.IsNullOrWhiteSpace(text)) =>
                new RomParseResult(new RomSourceInvocation(
                    suite, name, symbol, text, maximumResults, manifest, assets), null),
            "source" => Failure(
                "missing_source_query", "rom source requires --name and either --symbol or --text."),
            "diagnose" when !string.IsNullOrWhiteSpace(name) && code.HasValue =>
                new RomParseResult(new RomDiagnoseInvocation(
                    suite, name, code.Value, manifest, assets), null),
            "diagnose" => Failure(
                "missing_diagnosis_input", "rom diagnose requires --name and --code."),
            _ => Failure("invalid_rom_command", $"Unknown ROM command '{arguments[1]}'.")
        };
    }

    private static RomParseResult Failure(string code, string message) =>
        new(null, new LabError(code, message));
}
