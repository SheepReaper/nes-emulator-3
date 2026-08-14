using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Sheep.Nes.Lab;

public enum McpClientKind { Codex, Antigravity, Copilot }
public sealed record GatewayProbeDiagnostics(bool RegistrationMatches, bool PublishedGatewayHealthy,
    bool ProtocolHealthy, bool? CurrentSessionExposesNesLab, bool RestartRequired,
    string? StandardError = null, string? UnexpectedStandardOutput = null);
public sealed record SetupResult(string Target, SetupAction Action, bool Configured,
    bool Changed, string? ConfigPath, string Command, IReadOnlyList<string> Arguments,
    string? BackupPath = null, string? Detail = null, GatewayHealthState? Health = null,
    GatewayManifest? Manifest = null, GatewayProbeDiagnostics? Diagnostics = null);

public static class McpRegistration
{
    public static JsonNode MergeJson(JsonNode document, McpClientKind client,
        string command, IReadOnlyList<string> arguments, string repositoryRoot)
    {
        var root = document as JsonObject ?? throw new InvalidDataException("MCP configuration root must be an object.");
        var servers = root["mcpServers"] as JsonObject ?? new JsonObject();
        root["mcpServers"] = servers;
        var args = new JsonArray();
        foreach (var argument in arguments) args.Add(argument);
        var server = new JsonObject
        {
            ["command"] = command,
            ["args"] = args,
            ["cwd"] = repositoryRoot
        };
        if (client == McpClientKind.Copilot)
        {
            server["type"] = "local";
            server["env"] = new JsonObject();
            server["tools"] = new JsonArray("*");
        }
        servers["nes-lab"] = server;
        return root;
    }

    public static JsonNode RemoveJson(JsonNode document)
    {
        if (document["mcpServers"] is JsonObject servers) servers.Remove("nes-lab");
        return document;
    }

    public static async Task<SetupResult> ExecuteAsync(McpSetupInvocation invocation,
        string repositoryRoot, CancellationToken cancellationToken = default)
    {
        var project = Path.Combine(repositoryRoot, "src", "tools", "nes-lab", "Sheep.Nes.Lab.csproj");
        var publishDirectory = Path.Combine(repositoryRoot, ".artifacts", "nes-lab", "gateway");
        var publishedAssembly = Path.Combine(publishDirectory, "Sheep.Nes.Lab.dll");
        if (invocation.Action is SetupAction.Apply or SetupAction.Repair)
        {
            Directory.CreateDirectory(publishDirectory);
            var publish = await new ProcessCommandExecutor().ExecuteAsync(
                new VerificationCommand(VerificationScope.LabTests, "dotnet",
                    ["publish", project, "--no-restore", "--output", publishDirectory]),
                repositoryRoot, cancellationToken).ConfigureAwait(false);
            if (publish.ExitCode != 0) throw new InvalidOperationException(
                $"NES Lab gateway publish failed: {publish.StandardError}\n{publish.StandardOutput}");
            await GatewayManifestService.WriteAsync(repositoryRoot, publishedAssembly, "dotnet",
                [publishedAssembly, "mcp"], cancellationToken);
        }
        var command = "dotnet";
        string[] arguments = [publishedAssembly, "mcp"];
        var kind = Enum.Parse<McpClientKind>(invocation.Client, true);
        if (kind == McpClientKind.Codex)
        {
            var result = await ExecuteCodexAsync(invocation.Action, command, arguments, cancellationToken);
            if (result.Health == GatewayHealthState.RegistrationMismatch) return result;
            return await WithHealthAsync(result, repositoryRoot, publishedAssembly, cancellationToken);
        }

        var path = invocation.ConfigPath ?? kind switch
        {
            McpClientKind.Antigravity => Path.Combine(repositoryRoot, ".agents", "mcp_config.json"),
            McpClientKind.Copilot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".copilot", "mcp-config.json"),
            _ => throw new UnreachableException()
        };
        var exists = File.Exists(path);
        var document = exists ? JsonNode.Parse(await File.ReadAllTextAsync(path, cancellationToken)) ?? new JsonObject()
            : new JsonObject();
        var configured = document["mcpServers"]?["nes-lab"] is not null;
        if (invocation.Action == SetupAction.Check)
        {
            var server = document["mcpServers"]?["nes-lab"];
            var registeredCommand = server?["command"]?.GetValue<string>();
            var registeredArguments = server?["args"]?.AsArray().Select(node => node?.GetValue<string>() ?? "").ToArray() ?? [];
            if (!configured) return new(kind.ToString(), invocation.Action, false, false, path, command, arguments,
                Detail: "NES Lab is not registered.", Health: GatewayHealthState.Missing,
                Diagnostics: new(false, false, false, null, false));
            if (registeredCommand != command || !registeredArguments.SequenceEqual(arguments))
                return new(kind.ToString(), invocation.Action, true, false, path, command, arguments,
                    Detail: "Registered launch command differs from the published gateway.", Health: GatewayHealthState.RegistrationMismatch,
                    Diagnostics: new(false, false, false, null, false));
            return await WithHealthAsync(new(kind.ToString(), invocation.Action, true, false, path, command, arguments),
                repositoryRoot, publishedAssembly, cancellationToken);
        }

        string? backup = null;
        if (exists)
        {
            backup = path.StartsWith(repositoryRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                ? Path.Combine(repositoryRoot, ".artifacts", "nes-lab", "setup-backups",
                    Path.GetFileName(path) + ".bak-" + DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss"))
                : path + ".bak-" + DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");
            Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
            File.Copy(path, backup, overwrite: false);
        }
        document = invocation.Action == SetupAction.Remove
            ? RemoveJson(document) : MergeJson(document, kind, command, arguments, repositoryRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, document.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
        var configuredResult = new SetupResult(kind.ToString(), invocation.Action, invocation.Action != SetupAction.Remove,
            true, path, command, arguments, backup, Diagnostics: new(true, false, false, null,
                invocation.Action is SetupAction.Apply or SetupAction.Repair));
        return invocation.Action == SetupAction.Remove ? configuredResult :
            await WithHealthAsync(configuredResult, repositoryRoot, publishedAssembly, cancellationToken);
    }

    private static async Task<SetupResult> ExecuteCodexAsync(SetupAction action, string command,
        IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var check = await RunAsync("codex", ["mcp", "get", "nes-lab", "--json"], cancellationToken);
        var configured = check.ExitCode == 0;
        if (action == SetupAction.Check)
        {
            if (!configured) return new("Codex", action, false, false, null, command, arguments,
                Detail: "NES Lab is not registered.", Health: GatewayHealthState.Missing,
                Diagnostics: new(false, false, false, null, false,
                    EmptyToNull(check.StandardError), UnexpectedOutput(check.StandardOutput)));
            using var json = JsonDocument.Parse(ExtractJsonEnvelope(check.StandardOutput));
            var transport = json.RootElement.GetProperty("transport");
            var registeredCommand = transport.GetProperty("command").GetString();
            var registeredArguments = transport.GetProperty("args").EnumerateArray().Select(item => item.GetString() ?? "").ToArray();
            if (registeredCommand != command || !registeredArguments.SequenceEqual(arguments))
                return new("Codex", action, true, false, null, command, arguments,
                    Detail: "Registered launch command differs from the published gateway.", Health: GatewayHealthState.RegistrationMismatch,
                    Diagnostics: new(false, false, false, null, false,
                        EmptyToNull(check.StandardError), UnexpectedOutput(check.StandardOutput)));
            return new("Codex", action, true, false, null, command, arguments,
                Diagnostics: new(true, false, false, null, false,
                    EmptyToNull(check.StandardError), UnexpectedOutput(check.StandardOutput)));
        }
        string? backup = null;
        var config = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".codex", "config.toml");
        if (File.Exists(config))
        {
            backup = config + ".bak-" + DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");
            File.Copy(config, backup, overwrite: false);
        }
        if (configured)
            _ = await RunRequiredAsync("codex", ["mcp", "remove", "nes-lab"], cancellationToken);
        if (action is SetupAction.Apply or SetupAction.Repair)
        {
            List<string> add = ["mcp", "add", "nes-lab", "--", command];
            add.AddRange(arguments);
            _ = await RunRequiredAsync("codex", add, cancellationToken);
        }
        return new("Codex", action, action is SetupAction.Apply or SetupAction.Repair, true, config, command, arguments, backup,
            Diagnostics: new(true, false, false, null, action is SetupAction.Apply or SetupAction.Repair,
                EmptyToNull(check.StandardError), UnexpectedOutput(check.StandardOutput)));
    }

    private static async Task<SetupResult> WithHealthAsync(SetupResult result, string root, string assembly,
        CancellationToken cancellationToken)
    {
        var state = GatewayManifestService.Validate(root, assembly, out var manifest, out var detail);
        var publishedHealthy = state == GatewayHealthState.Healthy;
        if (state == GatewayHealthState.Healthy)
        {
            var probe = await GatewayManifestService.ProbeAsync(root, assembly, cancellationToken);
            state = probe.State; detail = probe.Detail;
        }
        var prior = result.Diagnostics;
        var exposedInCurrentSession = Environment.GetEnvironmentVariable("NES_LAB_CURRENT_MCP_SESSION") == "1"
            ? true : (bool?)null;
        return result with
        {
            Health = state,
            Manifest = manifest,
            Detail = detail,
            Diagnostics = new(prior?.RegistrationMatches ?? true,
                publishedHealthy,
                state == GatewayHealthState.Healthy, exposedInCurrentSession, prior?.RestartRequired ?? false,
                prior?.StandardError, prior?.UnexpectedStandardOutput)
        };
    }

    private static async Task<CommandExecution> RunRequiredAsync(string file, IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var result = await RunAsync(file, arguments, cancellationToken);
        if (result.ExitCode != 0) throw new InvalidOperationException(result.StandardError);
        return result;
    }

    private static Task<CommandExecution> RunAsync(string file, IReadOnlyList<string> arguments,
        CancellationToken cancellationToken) => new ProcessCommandExecutor().ExecuteAsync(
            new VerificationCommand(VerificationScope.LabTests, file, arguments),
            Directory.GetCurrentDirectory(), cancellationToken);

    public static string ExtractJsonEnvelope(string output)
    {
        var trimmed = output.Trim();
        try { using var _ = JsonDocument.Parse(trimmed); return trimmed; }
        catch (JsonException) { }
        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');
        if (start < 0 || end < start) throw new InvalidDataException("Command did not return a JSON envelope.");
        var candidate = trimmed[start..(end + 1)];
        using var document = JsonDocument.Parse(candidate);
        return document.RootElement.GetRawText();
    }

    public static string? UnexpectedOutput(string output)
    {
        var envelope = ExtractJsonEnvelope(output);
        var trimmed = output.Trim();
        return trimmed == envelope ? null : trimmed.Replace(envelope, "", StringComparison.Ordinal).Trim() is { Length: > 0 } extra
            ? extra : null;
    }

    private static string? EmptyToNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
