namespace Sheep.Nes.Lab;

public enum SetupAction { Check, Apply, Repair, Remove }
public abstract record SetupInvocation(SetupAction Action);
public sealed record McpSetupInvocation(string Client, SetupAction Action, string? ConfigPath = null)
    : SetupInvocation(Action);
public sealed record ModelSetupInvocation(SetupAction Action, string Model = "nes-lab:devstral-24b",
    string? Modelfile = null) : SetupInvocation(Action);
public sealed record SetupParseResult(SetupInvocation? Invocation, LabError? Error);

public static class SetupCommandParser
{
    public static SetupParseResult Parse(IReadOnlyList<string> args)
    {
        if (args.Count < 3 || !args[0].Equals("setup", StringComparison.OrdinalIgnoreCase))
            return Failure("invalid_setup_command", "Expected setup mcp|model with --check, --apply, or --remove.");
        var action = args.Contains("--repair") ? SetupAction.Repair :
            args.Contains("--apply") ? SetupAction.Apply :
            args.Contains("--remove") ? SetupAction.Remove : SetupAction.Check;
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 2; index < args.Count; index++)
        {
            if (args[index] is "--check" or "--apply" or "--repair" or "--remove") continue;
            var option = args[index];
            if (!option.StartsWith("--", StringComparison.Ordinal) || ++index >= args.Count)
                return Failure("missing_value", $"{option} requires a value.");
            values[option] = args[index];
        }
        if (args[1].Equals("mcp", StringComparison.OrdinalIgnoreCase))
        {
            var client = values.GetValueOrDefault("--client");
            if (client is null || client is not ("codex" or "antigravity" or "copilot"))
                return Failure("invalid_mcp_client", "--client must be codex, antigravity, or copilot.");
            return new(new McpSetupInvocation(client, action, values.GetValueOrDefault("--config")), null);
        }
        if (args[1].Equals("model", StringComparison.OrdinalIgnoreCase) && action != SetupAction.Remove)
            return new(new ModelSetupInvocation(action, values.GetValueOrDefault("--model") ?? "nes-lab:devstral-24b",
                values.GetValueOrDefault("--modelfile")), null);
        return Failure("invalid_setup_command", $"Unknown setup target '{args[1]}'.");
    }

    private static SetupParseResult Failure(string code, string message) => new(null, new LabError(code, message));
}
