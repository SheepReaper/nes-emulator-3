using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Sheep.Nes.Lab;

[McpServerPromptType]
public static class NesLabMcpPrompts
{
    [McpServerPrompt(Name = "diagnose-timing-failure")]
    [Description("Prepare a source-backed CPU, DMA, PPU, or APU timing diagnosis.")]
    public static string DiagnoseTimingFailure(
        [System.ComponentModel.Description("Optional verification run ID or 'latest'.")] string runId = "latest") =>
        $"Use nes_lab_inspect with capability diagnose and operation inspect for runId '{runId}'. " +
        "Treat the returned failure, ROM source, trace window, and references as authoritative evidence. " +
        "Use nes_lab_run only for a focused verification after forming a hypothesis.";

    [McpServerPrompt(Name = "review-mapper-change")]
    [Description("Review a mapper change with focused source, tests, and verification evidence.")]
    public static string ReviewMapperChange() =>
        "Use nes_lab_inspect context build with subsystem 'mapper', then inspect code references and tests. " +
        "Run nes_lab_run verify only for the smallest affected scope and preserve ROM provenance.";

    [McpServerPrompt(Name = "review-apu-dma-change")]
    [Description("Review an APU or DMA change with bounded timing evidence.")]
    public static string ReviewApuDmaChange() =>
        "Use nes_lab_inspect context build with task describing the APU/DMA behavior, then inspect trace " +
        "queries and the relevant conformance ROM. Do not infer hardware timing from an unaligned trace.";

    [McpServerPrompt(Name = "prepare-conformance-fix")]
    [Description("Prepare a minimal, source-backed conformance-fix workflow.")]
    public static string PrepareConformanceFix() =>
        "Use nes_lab_inspect diagnose inspect for the latest failed run, inspect its ROM and trace resources, " +
        "then use nes_lab_run verify for a named regression case. Keep the final change narrowly scoped.";
}