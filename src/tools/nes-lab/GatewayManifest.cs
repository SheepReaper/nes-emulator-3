using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Client;

namespace Sheep.Nes.Lab;

public enum GatewayHealthState { Healthy, Stale, Missing, RegistrationMismatch, StartupFailure, ProtocolFailure }
public sealed record GatewayManifest(string LabVersion, int ContractSchema, string SourceFingerprint,
    string AssemblyDigest, DateTimeOffset PublishedAt, string Command, IReadOnlyList<string> Arguments);

public static class GatewayManifestService
{
    public const string FileName = "gateway-manifest.json";

    public static async Task<GatewayManifest> WriteAsync(string root, string assembly, string command,
        IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var manifest = new GatewayManifest(Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0",
            TraceArtifact.CurrentSchemaVersion, ComputeSourceFingerprint(root), HashFile(assembly),
            DateTimeOffset.UtcNow, command, arguments);
        await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(assembly)!, FileName),
            JsonSerializer.Serialize(manifest, LabResponseSerializer.Options), cancellationToken);
        return manifest;
    }

    public static GatewayHealthState Validate(string root, string assembly, out GatewayManifest? manifest,
        out string detail)
    {
        manifest = null;
        var path = Path.Combine(Path.GetDirectoryName(assembly)!, FileName);
        if (!File.Exists(assembly) || !File.Exists(path)) { detail = "Published assembly or gateway manifest is missing."; return GatewayHealthState.Missing; }
        try { manifest = JsonSerializer.Deserialize<GatewayManifest>(File.ReadAllText(path), LabResponseSerializer.Options); }
        catch (Exception exception) { detail = exception.Message; return GatewayHealthState.ProtocolFailure; }
        if (manifest is null || !manifest.AssemblyDigest.Equals(HashFile(assembly), StringComparison.OrdinalIgnoreCase) ||
            !manifest.SourceFingerprint.Equals(ComputeSourceFingerprint(root), StringComparison.OrdinalIgnoreCase))
        { detail = "Published gateway does not match the current dependency closure."; return GatewayHealthState.Stale; }
        detail = "Published gateway manifest and assembly are current.";
        return GatewayHealthState.Healthy;
    }

    public static async Task<(GatewayHealthState State, string Detail)> ProbeAsync(string root, string assembly,
        CancellationToken cancellationToken)
    {
        var initialized = false;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            var transport = new StdioClientTransport(new StdioClientTransportOptions
            { Name = "nes-lab-health", Command = "dotnet", Arguments = [assembly, "mcp"], WorkingDirectory = root,
                InheritEnvironmentVariables = true });
            await using var client = await McpClient.CreateAsync(transport, cancellationToken: timeout.Token);
            initialized = true;
            var tools = await client.ListToolsAsync(cancellationToken: timeout.Token);
            var names = tools.Select(tool => tool.Name).Order().ToArray();
            if (!names.SequenceEqual(new[] { "nes_lab_discover", "nes_lab_inspect", "nes_lab_run" }))
                return (GatewayHealthState.ProtocolFailure, "Gateway tool surface does not match the contract.");
            var templates = await client.ListResourceTemplatesAsync(cancellationToken: timeout.Token);
            if (!templates.Any(template => template.UriTemplate == "nes-lab://artifact/{kind}/sha256/{digest}"))
                return (GatewayHealthState.ProtocolFailure, "Immutable artifact resource template is missing.");
            _ = await client.CallToolAsync("nes_lab_discover", new Dictionary<string, object?>(), cancellationToken: timeout.Token);
            return (GatewayHealthState.Healthy, "MCP initialization, discovery, tools, and resources are healthy.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { return (GatewayHealthState.StartupFailure, "Gateway startup timed out."); }
        catch (Exception exception)
        { return (initialized ? GatewayHealthState.ProtocolFailure : GatewayHealthState.StartupFailure, exception.Message); }
    }

    public static string ComputeSourceFingerprint(string root)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var directory in new[] { "src/tools/nes-lab", "src/tools/nes-lab-contracts" })
        foreach (var path in Directory.EnumerateFiles(Path.Combine(root, directory), "*", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") &&
                !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")).Order())
        { hash.AppendData(Encoding.UTF8.GetBytes(Path.GetRelativePath(root, path).Replace('\\', '/'))); hash.AppendData(File.ReadAllBytes(path)); }
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
    private static string HashFile(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
}
