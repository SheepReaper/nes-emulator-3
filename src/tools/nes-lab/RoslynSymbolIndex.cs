using System.Security.Cryptography;
using System.Text;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.MSBuild;
using System.Text.Json;

namespace Sheep.Nes.Lab;

public sealed record RoslynSymbolDeclaration(
    string Id,
    string Name,
    string QualifiedName,
    string Kind,
    string ProjectName,
    string FilePath,
    int LineNumber);

public sealed record RoslynSymbolReference(
    string SymbolId,
    string ProjectName,
    string FilePath,
    int LineNumber,
    string ContainingSymbol,
    bool IsInvocation);
public sealed record RoslynSymbolQuery(
    string Name, string? ExactQualifiedName = null, string? Kind = null,
    string? Project = null, string? Namespace = null, string? FilePath = null,
    int MaximumResults = 64);
public sealed record RoslynSourceExcerpt(string SymbolId, string Content, string FilePath, int LineNumber, bool Truncated);

public sealed class RoslynSymbolIndex : IAsyncDisposable
{
    private static readonly object RegistrationGate = new();
    private readonly Workspace? _ownedWorkspace;
    private readonly Solution _solution;
    private readonly IReadOnlyList<RoslynSymbolDeclaration> _declarations;
    private readonly Dictionary<string, ISymbol> _symbols;
    private bool _symbolsLoaded;

    private RoslynSymbolIndex(
        Solution solution,
        IReadOnlyList<RoslynSymbolDeclaration> declarations,
        Dictionary<string, ISymbol> symbols,
        bool symbolsLoaded,
        Workspace? ownedWorkspace)
    {
        _solution = solution;
        _declarations = declarations;
        _symbols = symbols;
        _symbolsLoaded = symbolsLoaded;
        _ownedWorkspace = ownedWorkspace;
    }

    public static async Task<RoslynSymbolIndex> OpenAsync(
        string solutionPath,
        CancellationToken cancellationToken = default,
        bool persistCache = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(solutionPath);
        EnsureMsBuildRegistered();
        var workspace = MSBuildWorkspace.Create();
        try
        {
            var solution = await workspace.OpenSolutionAsync(
                Path.GetFullPath(solutionPath), cancellationToken: cancellationToken).ConfigureAwait(false);
            var cache = await RoslynIndexCache.TryReadAsync(solution, solutionPath, cancellationToken).ConfigureAwait(false);
            var index = cache is null
                ? await CreateCoreAsync(solution, workspace, cancellationToken).ConfigureAwait(false)
                : new RoslynSymbolIndex(solution, cache, new Dictionary<string, ISymbol>(StringComparer.Ordinal), false, workspace);
            if (persistCache && cache is null)
                await RoslynIndexCache.WriteAsync(solution, solutionPath, index._declarations, cancellationToken).ConfigureAwait(false);
            return index;
        }
        catch
        {
            workspace.Dispose();
            throw;
        }
    }

    public static Task<RoslynSymbolIndex> CreateAsync(
        Solution solution,
        CancellationToken cancellationToken = default) =>
        CreateCoreAsync(solution, null, cancellationToken);

    public int DeclarationCount => _declarations.Count;

    public IReadOnlyList<RoslynSymbolDeclaration> FindDeclarations(
        string name,
        int maximumResults = 64)
        => FindDeclarations(new RoslynSymbolQuery(name, MaximumResults: maximumResults));

    public IReadOnlyList<RoslynSymbolDeclaration> FindDeclarations(RoslynSymbolQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        var name = query.Name;
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(query.MaximumResults);
        var filtered = _declarations.Where(item =>
            (query.ExactQualifiedName is null || item.QualifiedName.Equals(query.ExactQualifiedName, StringComparison.Ordinal)) &&
            (query.Kind is null || item.Kind.Equals(query.Kind, StringComparison.OrdinalIgnoreCase)) &&
            (query.Project is null || item.ProjectName.Equals(query.Project, StringComparison.OrdinalIgnoreCase)) &&
            (query.Namespace is null || item.QualifiedName.StartsWith(query.Namespace + ".", StringComparison.Ordinal)) &&
            (query.FilePath is null || item.FilePath.Replace('\\', '/').EndsWith(query.FilePath.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase)));
        var exact = filtered.Where(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).ToArray();
        return (exact.Length > 0 ? exact : filtered.Where(item =>
            item.QualifiedName.Contains(name, StringComparison.OrdinalIgnoreCase)).ToArray())
            .Take(query.MaximumResults).ToArray();
    }

    public RoslynSymbolDeclaration FindDeclarationById(string symbolId) =>
        _declarations.FirstOrDefault(item => item.Id.Equals(symbolId, StringComparison.Ordinal)) ??
        throw new KeyNotFoundException($"No declaration has stable symbol ID '{symbolId}'.");

    public IReadOnlyList<RoslynSymbolDeclaration> FindDeclarationsByPaths(
        IEnumerable<string> pathFragments, int maximumResults = 32)
    {
        var fragments = pathFragments.Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Replace('\\', '/')).ToArray();
        return _declarations.Where(item => fragments.Any(fragment =>
                item.FilePath.Replace('\\', '/').Contains(fragment, StringComparison.OrdinalIgnoreCase)))
            .Take(maximumResults).ToArray();
    }

    public async Task<RoslynSourceExcerpt> GetDeclarationSourceAsync(
        string symbolId, int maximumCharacters = 4096, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumCharacters);
        var symbol = await GetSymbolAsync(symbolId, cancellationToken).ConfigureAwait(false);
        var syntaxReference = symbol.DeclaringSyntaxReferences.FirstOrDefault() ??
            throw new InvalidOperationException($"Symbol '{symbolId}' has no source declaration.");
        var syntax = await syntaxReference.GetSyntaxAsync(cancellationToken).ConfigureAwait(false);
        var text = syntax.ToFullString();
        var truncated = text.Length > maximumCharacters;
        if (truncated) text = text[..maximumCharacters] + "\n…[truncated]";
        var location = syntax.GetLocation().GetLineSpan();
        return new RoslynSourceExcerpt(symbolId, text, location.Path,
            location.StartLinePosition.Line + 1, truncated);
    }

    public async Task<RoslynSourceExcerpt> GetContainingSourceAsync(
        RoslynSymbolReference reference, int maximumCharacters = 4096,
        CancellationToken cancellationToken = default)
    {
        var document = _solution.Projects.SelectMany(project => project.Documents).FirstOrDefault(item =>
            string.Equals(item.FilePath, reference.FilePath, StringComparison.OrdinalIgnoreCase)) ??
            throw new KeyNotFoundException($"Document '{reference.FilePath}' is not indexed.");
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false) ??
            throw new InvalidOperationException($"Document '{reference.FilePath}' has no syntax root.");
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var line = text.Lines[Math.Clamp(reference.LineNumber - 1, 0, text.Lines.Count - 1)];
        var node = root.FindToken(line.Start).Parent?.AncestorsAndSelf().FirstOrDefault(candidate =>
            candidate is BaseTypeDeclarationSyntax or MethodDeclarationSyntax or ConstructorDeclarationSyntax or
                PropertyDeclarationSyntax) ?? root;
        var content = node.ToFullString();
        var truncated = content.Length > maximumCharacters;
        if (truncated) content = content[..maximumCharacters] + "\n…[truncated]";
        return new RoslynSourceExcerpt(reference.SymbolId, content,
            document.FilePath ?? document.Name, reference.LineNumber, truncated);
    }

    public async Task<IReadOnlyList<RoslynSymbolReference>> FindReferencesAsync(
        string symbolId,
        CancellationToken cancellationToken = default)
    {
        var symbol = await GetSymbolAsync(symbolId, cancellationToken).ConfigureAwait(false);
        var referenced = await SymbolFinder.FindReferencesAsync(
            symbol, _solution, cancellationToken).ConfigureAwait(false);
        List<RoslynSymbolReference> results = [];
        foreach (var location in referenced.SelectMany(item => item.Locations))
        {
            var document = _solution.GetDocument(location.Document.Id);
            if (document is null) continue;
            var model = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            if (model is null || root is null) continue;
            var position = location.Location.SourceSpan.Start;
            var enclosing = model.GetEnclosingSymbol(position, cancellationToken);
            var node = root.FindNode(location.Location.SourceSpan);
            var line = location.Location.GetLineSpan().StartLinePosition.Line + 1;
            results.Add(new RoslynSymbolReference(
                symbolId,
                document.Project.Name,
                document.FilePath ?? document.Name,
                line,
                enclosing?.ToDisplayString() ?? "<global>",
                node.AncestorsAndSelf().OfType<InvocationExpressionSyntax>().Any()));
        }
        return results.OrderBy(item => item.FilePath, StringComparer.Ordinal)
            .ThenBy(item => item.LineNumber).ToArray();
    }

    public async Task<IReadOnlyList<RoslynSymbolReference>> FindCallersAsync(
        string symbolId,
        CancellationToken cancellationToken = default) =>
        (await FindReferencesAsync(symbolId, cancellationToken).ConfigureAwait(false))
        .Where(item => item.IsInvocation).ToArray();

    public async Task<IReadOnlyList<RoslynSymbolReference>> FindAffectedTestsAsync(
        string symbolId,
        CancellationToken cancellationToken = default) =>
        (await FindReferencesAsync(symbolId, cancellationToken).ConfigureAwait(false))
        .Where(item => IsTestProject(item.ProjectName) || IsTestPath(item.FilePath)).ToArray();

    public ValueTask DisposeAsync()
    {
        _ownedWorkspace?.Dispose();
        return ValueTask.CompletedTask;
    }

    private static async Task<RoslynSymbolIndex> CreateCoreAsync(
        Solution solution,
        Workspace? ownedWorkspace,
        CancellationToken cancellationToken)
    {
        List<RoslynSymbolDeclaration> declarations = [];
        Dictionary<string, ISymbol> symbols = new(StringComparer.Ordinal);
        foreach (var project in solution.Projects.Where(project => project.Language == LanguageNames.CSharp))
        {
            foreach (var document in project.Documents)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
                var model = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
                if (root is null || model is null) continue;
                foreach (var node in DeclarationNodes(root))
                {
                    var symbol = model.GetDeclaredSymbol(node, cancellationToken);
                    if (symbol is null || symbol.IsImplicitlyDeclared) continue;
                    var line = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    var id = CreateId(project.Name, document.FilePath ?? document.Name, node.SpanStart, symbol);
                    declarations.Add(new RoslynSymbolDeclaration(
                        id, symbol.Name, symbol.ToDisplayString(), symbol.Kind.ToString(),
                        project.Name, document.FilePath ?? document.Name, line));
                    symbols[id] = symbol;
                }
            }
        }
        return new RoslynSymbolIndex(solution, declarations, symbols, true, ownedWorkspace);
    }

    private async Task<ISymbol> GetSymbolAsync(string id, CancellationToken cancellationToken)
    {
        if (!_symbolsLoaded)
        {
            var loaded = await CreateCoreAsync(_solution, null, cancellationToken).ConfigureAwait(false);
            foreach (var pair in loaded._symbols) _symbols[pair.Key] = pair.Value;
            _symbolsLoaded = true;
        }
        return _symbols.TryGetValue(id, out var symbol) ? symbol :
            throw new KeyNotFoundException($"No indexed symbol has ID '{id}'.");
    }

    private static IEnumerable<SyntaxNode> DeclarationNodes(SyntaxNode root) =>
        root.DescendantNodes().Where(node => node is
            BaseTypeDeclarationSyntax or DelegateDeclarationSyntax or MethodDeclarationSyntax or
            ConstructorDeclarationSyntax or PropertyDeclarationSyntax or IndexerDeclarationSyntax or
            EventDeclarationSyntax or EnumMemberDeclarationSyntax ||
            node is VariableDeclaratorSyntax { Parent.Parent: FieldDeclarationSyntax or EventFieldDeclarationSyntax });

    private static string CreateId(string project, string file, int position, ISymbol symbol)
    {
        var input = $"{project}\0{file}\0{position}\0{symbol.ToDisplayString()}";
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(input)))[..24];
    }

    private static bool IsTestProject(string name) =>
        name.Contains("Test", StringComparison.OrdinalIgnoreCase);

    private static bool IsTestPath(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.Contains("/test/", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("test/", StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsureMsBuildRegistered()
    {
        lock (RegistrationGate)
        {
            if (!MSBuildLocator.IsRegistered)
                MSBuildLocator.RegisterDefaults();
        }
    }
}
