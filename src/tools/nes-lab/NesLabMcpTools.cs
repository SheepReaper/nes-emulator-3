using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace Sheep.Nes.Lab;

[McpServerToolType]
public static class NesLabMcpTools
{
    private static readonly WarmMcpInspectionSession WarmInspection = new(Directory.GetCurrentDirectory());
    [McpServerTool(Name = "nes_lab_discover", ReadOnly = true, Idempotent = true),
     Description("Lists compact nes-lab capability groups, or describes one group with its operations and parameters.")]
    public static object Discover(
        [Description("Optional capability name: verify, trace, rom, code, memory, context, history, diagnose, artifacts, feedback, references, experiment, media, investigate, session, host, or build.")]
        string? capability = null)
    {
        var result = string.IsNullOrWhiteSpace(capability)
            ? (object)LabCapabilityCatalog.List()
            : LabCapabilityCatalog.Describe(capability);
        McpResponseLimits.EnsureResponseIsBounded(JsonSerializer.Serialize(result, LabResponseSerializer.Options),
            McpResponseLimits.MaximumDiscoveryBytes);
        return result;
    }

    [McpServerTool(Name = "nes_lab_inspect", ReadOnly = true),
     Description("Executes one allowlisted read-only nes-lab inspection. Call nes_lab_discover first.")]
    public static Task<JsonElement> InspectAsync(
        [Description("Capability group returned by nes_lab_discover.")] string capability,
        [Description("Operation returned by nes_lab_discover for that capability.")] string operation,
        [Description("JSON object containing only the operation parameters.")] JsonElement arguments,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(capability, operation, arguments, allowRun: false, cancellationToken);

    [McpServerTool(Name = "nes_lab_run", ReadOnly = false, Destructive = false),
     Description("Runs one allowlisted state-changing but non-destructive nes-lab operation.")]
    public static Task<JsonElement> RunAsync(
        [Description("Capability group returned by nes_lab_discover.")] string capability,
        [Description("Operation returned by nes_lab_discover for that capability.")] string operation,
        [Description("JSON object containing only the operation parameters.")] JsonElement arguments,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(capability, operation, arguments, allowRun: true, cancellationToken);

    private static async Task<JsonElement> ExecuteAsync(
        string capability, string operation, JsonElement arguments, bool allowRun,
        CancellationToken cancellationToken)
    {
        var isRun = capability.Equals("verify", StringComparison.OrdinalIgnoreCase) ||
            capability.Equals("diagnose", StringComparison.OrdinalIgnoreCase) &&
            operation.Equals("run", StringComparison.OrdinalIgnoreCase) ||
            capability.Equals("artifacts", StringComparison.OrdinalIgnoreCase) &&
            operation is "pin" or "unpin" or "prune" ||
            capability.Equals("trace", StringComparison.OrdinalIgnoreCase) &&
            operation.Equals("capture", StringComparison.OrdinalIgnoreCase) ||
            capability.Equals("code", StringComparison.OrdinalIgnoreCase) &&
            operation.Equals("index", StringComparison.OrdinalIgnoreCase) ||
            capability.Equals("memory", StringComparison.OrdinalIgnoreCase) &&
            operation.Equals("validate", StringComparison.OrdinalIgnoreCase) ||
            capability.Equals("memory", StringComparison.OrdinalIgnoreCase) &&
            operation is "proposals-accept" or "proposals-reject" ||
            capability.Equals("feedback", StringComparison.OrdinalIgnoreCase) &&
            operation.Equals("record", StringComparison.OrdinalIgnoreCase) ||
            capability.Equals("references", StringComparison.OrdinalIgnoreCase) &&
            operation.Equals("sync", StringComparison.OrdinalIgnoreCase) ||
            capability.Equals("experiment", StringComparison.OrdinalIgnoreCase) &&
            operation is "run" or "run-inline" ||
            capability.Equals("investigate", StringComparison.OrdinalIgnoreCase) ||
            capability.Equals("session", StringComparison.OrdinalIgnoreCase) &&
            operation.Equals("close", StringComparison.OrdinalIgnoreCase) ||
            capability.Equals("host", StringComparison.OrdinalIgnoreCase) &&
            operation.Equals("diagnostics-import", StringComparison.OrdinalIgnoreCase);
        if (isRun != allowRun)
            throw new InvalidOperationException(isRun
                ? "Verification must use nes_lab_run."
                : "Read-only inspection must use nes_lab_inspect.");
        var root = Directory.GetCurrentDirectory();
        if (!allowRun && await WarmInspection.TryExecuteAsync(capability.ToLowerInvariant(),
                operation.ToLowerInvariant(), arguments, cancellationToken).ConfigureAwait(false) is { } warmResult)
        {
            McpResponseLimits.EnsureResponseIsBounded(warmResult.GetRawText(),
                capability is "context" or "diagnose" ? McpResponseLimits.MaximumContextBytes : McpResponseLimits.MaximumInspectionBytes);
            return warmResult;
        }
        var command = LabMcpCommandMapper.Map(capability, operation, arguments, root);
        var result = await new LabCliBridge(root).ExecuteAsync(command, cancellationToken).ConfigureAwait(false);
        var maximum = capability is "context" or "diagnose"
            ? McpResponseLimits.MaximumContextBytes : McpResponseLimits.MaximumInspectionBytes;
        McpResponseLimits.EnsureResponseIsBounded(result.GetRawText(), maximum);
        return result;
    }
}
