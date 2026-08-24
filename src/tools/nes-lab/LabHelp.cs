namespace Sheep.Nes.Lab;

public static class LabHelp
{
    public static string For(string? command = null) => For(command is null ? [] : [command]);

    public static string For(IReadOnlyList<string> commandPath)
    {
        var key = string.Join(' ', commandPath.Select(item => item.ToLowerInvariant()));
        return key switch
    {
        "verify" => """
            nes-lab verify [--scope <scope>|--changed] [--case <name>]
              [--trace-on-failure|--trace-always] [--plan-only] [--baseline-aware-exit-code]
            Runs focused verification. Use --changed to select affected scopes.
            Strict exit codes remain the default; baseline-aware exits succeed only when no regression exists.
            Verification has no --budget option; budgets apply to context and diagnose.
            """,
        "diagnose" => """
            nes-lab diagnose (--case <name>|--run <id|latest>) [--budget <bytes>]
              [--budget-tokens <count>] [--persist]
            Builds a bounded source-backed failure packet.
            """,
        "context" => """
            nes-lab context build [selector] --budget <bytes>
              Selectors include --task, --symbol, --id, --changed, --subsystem, --run, and --handoff.
            Builds a bounded source context packet.
            """,
        "baseline" or "baseline update" => """
            nes-lab baseline update [--run <id|latest>] [--apply]
            Previews an accepted-conformance-baseline update from a complete indexed run.
            --apply atomically writes the previewed improvement. New failures, changed diagnostics,
            focused runs, stale runs, and incomplete summaries are always rejected.
            nes-lab baseline show
            """,
        "baseline show" => """
            nes-lab baseline show
            Reads the current accepted conformance baseline without changing local state.
            """,
        "investigate" => """
            nes-lab investigate (--task <text>|--run <id|latest>) --agent local
              [--budget <bytes>] [--max-steps <1..16>]
            Runs a bounded read-only local investigation with cited immutable output.
            """,
        "media" => """
            nes-lab media frame compare --left <uri> --right <uri> [--heatmap]
            nes-lab media audio analyze|compare [options]
            Produces compact semantic evidence for immutable RGBA and Float32 artifacts.
            """,
        "session" => """
            nes-lab session close --task <text> [--run latest] [--packet <uri>] [--telemetry <json|path>]
            nes-lab session show --uri <handoff-uri>
            Publishes or reads an immutable development-session handoff.
            """,
        "history" => """
            nes-lab history runs latest [--scope <scope>] [--case <name>]
            nes-lab history failures latest|search [options]
            nes-lab history metrics
            Legacy runs, failures, and metrics aliases remain supported.
            """,
        "host" => """
            nes-lab host diagnostics import (--json <bundle>|--path <file>)
            nes-lab host diagnostics show --uri <uri>
            nes-lab host diagnostics compare --left <uri> --right <uri>
            Imports portable frontend diagnostics without attaching to a live process.
            """,
        "build" => """
            nes-lab build diagnose --run <id|latest>
            Parses indexed MSBuild/compiler failures and identifies the earliest actionable diagnostic.
            """,
        "telemetry" => """
            nes-lab telemetry normalize [--json <envelope>|--path <file>]
            With no option, reads the agent-agnostic telemetry envelope from stdin and writes normalized JSON to stdout.
            """,
        "trace" => """
            nes-lab trace <capture|query|diff> [options]
            Captures, queries, or compares bounded CPU-clock trace artifacts.
            Use `nes-lab trace <subcommand> --help` for exact, copyable syntax.
            """,
        "trace capture" => """
            nes-lab trace capture --case <manifest-case-name> [--timeout-seconds <1..300>]
              Runs the named conformance case with trace-always enabled and returns its trace artifact.
              Capture is bounded to 2,048 CPU-clock records and retains checkpoint/terminal provenance.
              The default execution timeout is 30 seconds; timeout terminates the complete child process tree.
            Example: nes-lab trace capture --case "Explicit DMA Abort"
            """,
        "trace query" => """
            nes-lab trace query (--artifact <path>|--artifact-uri <uri>)
              [--actor <name>] [--address <decimal|0xhex|$hex>] [--end <decimal|0xhex|$hex>]
              [--interrupt-edges] [--dma-overlap] [--instruction-boundaries] [--max <count>]
            Reads a bounded projection from an existing trace without rerunning emulation.
            Example: nes-lab trace query --artifact-uri <trace-uri> --dma-overlap --max 64
            """,
        "trace diff" => """
            nes-lab trace diff --expected <trace-path> --actual <trace-path> [--context <records>]
            Aligns compatible traces by CPU clock and reports semantic differences or window mismatch.
            """,
        _ => """
            nes-lab <command> [options]

            Commands:
              verify      Run focused repository verification.
              baseline    Show or safely update the accepted conformance baseline.
              diagnose    Build a source-backed failure packet.
              context     Build bounded source context.
              trace       Capture, query, or diff traces.
              rom         Inspect ROM metadata and source.
              code        Query Roslyn declarations and references.
              history     Query runs, failures, and metrics.
              memory      Search engineering knowledge.
              references  Inspect the pinned hardware reference corpus.
              experiment  Run or compare deterministic scenarios.
              media       Analyze immutable frame and audio artifacts.
              investigate Run a bounded read-only local investigation.
              session     Publish or reopen an immutable session handoff.
              host        Import and compare portable frontend diagnostics.
              build       Diagnose structured build failures.
              telemetry   Normalize host-reported usage over stdin/stdout.
              artifacts   Inspect immutable evidence artifacts.
              setup       Publish and configure the MCP gateway.
              gateway     Run gateway benchmarks.

            Use `nes-lab <command> --help` for command-specific usage.
            """
    };
    }
}
