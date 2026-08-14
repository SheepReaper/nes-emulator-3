using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Sheep.Nes.Lab;

public enum AssemblySymbolKind { Label, Procedure, Constant, Define }

public sealed record AssemblySourceDocument(
    string SourcePath,
    string RelativePath,
    string Sha256,
    int LineCount);

public sealed record AssemblySymbol(
    string Name,
    AssemblySymbolKind Kind,
    string? Value,
    string SourcePath,
    string SourceSha256,
    int LineNumber,
    string Excerpt);

public sealed record AssemblySourceMatch(
    string SourcePath,
    string SourceSha256,
    int LineNumber,
    string Excerpt);

public sealed record AssemblyResultEncoding(
    int Code,
    string? Message,
    string SourcePath,
    string SourceSha256,
    int LineNumber,
    string Excerpt);

public sealed class AssemblySourceIndex
{
    private static readonly Regex LabelPattern = new(
        @"^\s*([A-Za-z_@.][A-Za-z0-9_@.]*)\s*:\s*(?:;.*)?$", RegexOptions.Compiled);
    private static readonly Regex ProcedurePattern = new(
        @"^\s*\.proc\s+([A-Za-z_@.][A-Za-z0-9_@.]*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex DefinePattern = new(
        @"^\s*\.define\s+([A-Za-z_@.][A-Za-z0-9_@.]*)\s+([^;]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ConstantPattern = new(
        @"^\s*([A-Za-z_@.][A-Za-z0-9_@.]*)\s*(?::=|=)\s*([^;]+)", RegexOptions.Compiled);
    private static readonly Regex SetTestPattern = new(
        "^\\s*set_test\\s+([0-9]+)(?:\\s*,\\s*\"([^\"]*)\")?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly HashSet<string> Extensions =
        new(StringComparer.OrdinalIgnoreCase) { ".s", ".asm", ".inc", ".a65" };

    private readonly IReadOnlyDictionary<string, string[]> _linesByPath;

    private AssemblySourceIndex(
        RomCatalogEntry entry,
        string sourceRoot,
        IReadOnlyList<AssemblySourceDocument> documents,
        IReadOnlyList<AssemblySymbol> symbols,
        IReadOnlyList<AssemblyResultEncoding> resultEncodings,
        IReadOnlyDictionary<string, string[]> linesByPath)
    {
        Entry = entry;
        SourceRoot = sourceRoot;
        Documents = documents;
        Symbols = symbols;
        ResultEncodings = resultEncodings;
        _linesByPath = linesByPath;
    }

    public RomCatalogEntry Entry { get; }
    public string SourceRoot { get; }
    public IReadOnlyList<AssemblySourceDocument> Documents { get; }
    public IReadOnlyList<AssemblySymbol> Symbols { get; }
    public IReadOnlyList<AssemblyResultEncoding> ResultEncodings { get; }

    public static AssemblySourceIndex Build(RomCatalogEntry entry, string assetRoot)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentException.ThrowIfNullOrWhiteSpace(assetRoot);
        var firstSeparator = entry.RelativePath.IndexOf('/');
        var directoryName = firstSeparator < 0
            ? Path.GetDirectoryName(entry.RelativePath) ?? ""
            : entry.RelativePath[..firstSeparator];
        var sourceRoot = Path.GetFullPath(Path.Combine(assetRoot, directoryName));
        var files = Directory.Exists(sourceRoot)
            ? Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories)
                .Where(path => Extensions.Contains(Path.GetExtension(path)))
                .OrderBy(path => Path.GetRelativePath(sourceRoot, path), StringComparer.Ordinal)
                .ToArray()
            : [];

        List<AssemblySourceDocument> documents = [];
        List<AssemblySymbol> symbols = [];
        List<AssemblyResultEncoding> resultEncodings = [];
        Dictionary<string, string[]> linesByPath = new(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            var bytes = File.ReadAllBytes(file);
            var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
            var lines = File.ReadAllLines(file);
            linesByPath[file] = lines;
            documents.Add(new AssemblySourceDocument(
                file, Path.GetRelativePath(sourceRoot, file).Replace('\\', '/'), hash, lines.Length));
            for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                var parsed = ParseSymbol(lines[lineIndex]);
                if (parsed is null) continue;
                symbols.Add(new AssemblySymbol(
                    parsed.Value.Name, parsed.Value.Kind, parsed.Value.Value,
                    file, hash, lineIndex + 1, lines[lineIndex].Trim()));
            }
            for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                var match = SetTestPattern.Match(lines[lineIndex]);
                if (!match.Success) continue;
                resultEncodings.Add(new AssemblyResultEncoding(
                    int.Parse(match.Groups[1].Value),
                    match.Groups[2].Success ? match.Groups[2].Value : null,
                    file, hash, lineIndex + 1, lines[lineIndex].Trim()));
            }
        }

        return new AssemblySourceIndex(
            entry, sourceRoot, documents, symbols, resultEncodings, linesByPath);
    }

    public IReadOnlyList<AssemblySymbol> FindSymbol(string name, int maximumResults = 64)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumResults);
        return Symbols.Where(symbol => symbol.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            .Take(maximumResults).ToArray();
    }

    public IReadOnlyList<AssemblySourceMatch> SearchText(string text, int maximumResults = 64)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumResults);
        List<AssemblySourceMatch> matches = [];
        var hashes = Documents.ToDictionary(document => document.SourcePath, document => document.Sha256,
            StringComparer.OrdinalIgnoreCase);
        foreach (var document in Documents)
        {
            var lines = _linesByPath[document.SourcePath];
            for (var index = 0; index < lines.Length && matches.Count < maximumResults; index++)
            {
                if (!lines[index].Contains(text, StringComparison.OrdinalIgnoreCase)) continue;
                matches.Add(new AssemblySourceMatch(
                    document.SourcePath, hashes[document.SourcePath], index + 1, lines[index].Trim()));
            }
            if (matches.Count == maximumResults) break;
        }
        return matches;
    }

    private static (string Name, AssemblySymbolKind Kind, string? Value)? ParseSymbol(string line)
    {
        var match = ProcedurePattern.Match(line);
        if (match.Success) return (match.Groups[1].Value, AssemblySymbolKind.Procedure, null);
        match = DefinePattern.Match(line);
        if (match.Success) return (match.Groups[1].Value, AssemblySymbolKind.Define, match.Groups[2].Value.Trim());
        match = ConstantPattern.Match(line);
        if (match.Success) return (match.Groups[1].Value, AssemblySymbolKind.Constant, match.Groups[2].Value.Trim());
        match = LabelPattern.Match(line);
        return match.Success ? (match.Groups[1].Value, AssemblySymbolKind.Label, null) : null;
    }
}
