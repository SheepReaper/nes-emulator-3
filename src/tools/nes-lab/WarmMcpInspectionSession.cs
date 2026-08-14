using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Sheep.Nes.Lab;

/// <summary>Session-scoped, read-only application services for MCP inspection.</summary>
public sealed class WarmMcpInspectionSession : IAsyncDisposable
{
    private readonly string root;
    private readonly SemaphoreSlim gate = new(1, 1);
    private RoslynSymbolIndex? index;
    private string? fingerprint;
    private int workspaceLoads;

    public WarmMcpInspectionSession(string repositoryRoot) => root = Path.GetFullPath(repositoryRoot);
    public int WorkspaceLoads => workspaceLoads;

    public async Task<JsonElement?> TryExecuteAsync(string capability, string operation,
        JsonElement arguments, CancellationToken cancellationToken)
    {
        if (capability.Equals("references", StringComparison.OrdinalIgnoreCase) && operation != "sync")
        {
            var store = new ReferenceCorpusStore(Path.Combine(root, "src", "tools", "nes-lab", "reference-corpus.v1.json"),
                Path.Combine(root, ".artifacts", "nes-lab", "references"));
            object result = operation switch
            {
                "status" => store.Status(),
                "show" => store.Show(Required(arguments, "id")),
                "search" => store.Search(Required(arguments, "query"), 16),
                _ => throw new KeyNotFoundException($"Unknown references operation '{operation}'.")
            };
            return Envelope("references-" + operation, result);
        }
        if (capability.Equals("experiment", StringComparison.OrdinalIgnoreCase) && operation == "compare")
            return Envelope("experiment-compare", await new NesExperimentService(root).CompareAsync(
                Required(arguments, "leftUri"), Required(arguments, "rightUri"), cancellationToken));
        if (capability is not ("code" or "context") || capability == "code" && operation == "index") return null;

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var warmIndex = await GetIndexAsync(cancellationToken).ConfigureAwait(false);
            var command = LabMcpCommandMapper.Map(capability, operation, arguments, root).ToArray();
            if (capability == "code")
            {
                var parsed = CodeCommandParser.Parse(command);
                if (parsed.Invocation is null) throw new ArgumentException(parsed.Error?.Message);
                object result = parsed.Invocation switch
                {
                    CodeFindInvocation find => warmIndex.FindDeclarations(new RoslynSymbolQuery(find.Symbol,
                        find.ExactQualifiedName, find.Kind, find.Project, find.Namespace, find.FilePath, find.MaximumResults)),
                    CodeRelationsInvocation { Operation: "refs" } relation => await warmIndex.FindReferencesAsync(relation.SymbolId, cancellationToken),
                    CodeRelationsInvocation { Operation: "callers" } relation => await warmIndex.FindCallersAsync(relation.SymbolId, cancellationToken),
                    CodeRelationsInvocation { Operation: "tests" } relation => await warmIndex.FindAffectedTestsAsync(relation.SymbolId, cancellationToken),
                    _ => throw new InvalidOperationException("Unsupported warm code operation.")
                };
                return Envelope("code-" + operation, result, new { workspaceLoads });
            }

            var context = ContextCommandParser.Parse(command).Invocation ??
                throw new ArgumentException("Invalid context operation.");
            if (context.RankLocal || context.SynthesizeLocal) return null;
            var evidence = context.SymbolId is not null
                ? await SymbolContextPacketBuilder.CollectByIdAsync(warmIndex, root, context.SymbolId, cancellationToken)
                : context.Symbol is not null
                    ? await SymbolContextPacketBuilder.CollectAsync(warmIndex, root, new RoslynSymbolQuery(context.Symbol,
                        context.ExactQualifiedName, context.Kind, context.Project, context.Namespace, context.FilePath, 16), cancellationToken)
                    : await GeneralContextEvidenceCollector.CollectAsync(warmIndex, root, context, cancellationToken);
            var packet = ContextPacketBuilder.Build(evidence, context.BudgetBytes);
            using var document = JsonDocument.Parse(packet.Content);
            return Envelope("context-build", new { packet.PacketId, packet.BudgetBytes, packet.UsedBytes,
                packet.IncludedEvidenceCount, packet.OmittedEvidenceCount, packet.Truncated, packet.Categories,
                packet.RequiredEvidence, packet.Density, packet.EvidenceSpine,
                packet = document.RootElement.Clone(), workspaceLoads });
        }
        finally { gate.Release(); }
    }

    private async Task<RoslynSymbolIndex> GetIndexAsync(CancellationToken cancellationToken)
    {
        var current = SourceFingerprint(root);
        if (index is not null && current == fingerprint) return index;
        if (index is not null) await index.DisposeAsync().ConfigureAwait(false);
        index = await RoslynSymbolIndex.OpenAsync(Path.Combine(root, "nes-emulator-3.slnx"), cancellationToken)
            .ConfigureAwait(false);
        fingerprint = current; workspaceLoads++;
        return index;
    }

    private static JsonElement Envelope(string operation, object result, object? diagnostics = null) =>
        JsonSerializer.SerializeToElement(new { schemaVersion = 1, success = true, operation, result, diagnostics },
            LabResponseSerializer.Options);
    private static string Required(JsonElement arguments, string name) =>
        arguments.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()! : throw new ArgumentException($"'{name}' is required.");
    private static string SourceFingerprint(string root)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => Path.GetExtension(path) is ".cs" or ".csproj" or ".slnx" &&
                !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") &&
                !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                !path.Contains($"{Path.DirectorySeparatorChar}.artifacts{Path.DirectorySeparatorChar}"))
            .Order(StringComparer.OrdinalIgnoreCase))
        { hash.AppendData(Encoding.UTF8.GetBytes(Path.GetRelativePath(root, path))); hash.AppendData(File.ReadAllBytes(path)); }
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
    public async ValueTask DisposeAsync()
    { if (index is not null) await index.DisposeAsync().ConfigureAwait(false); gate.Dispose(); }
}
