namespace Sheep.Nes.Lab.Tests;

public sealed class DiagnosisServiceTests
{
    [Fact]
    public void SelectWindow_PrefersDmcCheckpointOverTerminalTail()
    {
        var dmc = Window("first-dmc-dma-request", 10);
        var terminal = Window("terminal-state", 99);
        var trace = new TraceArtifact(3, "nes-cpu-clock-trace", "hash", "commit",
            DateTimeOffset.UnixEpoch, new TraceRunMetadata("rom", "NTSC", "suite", "case"),
            0, 0, false, [], Windows: [dmc, terminal]);

        Assert.Same(dmc, DiagnosisService.SelectWindow(trace, "DMC timing failed"));
    }

    private static TraceCheckpointWindow Window(string name, ulong clock) =>
        new(name, "hardware", "test", clock, clock, clock, 0, 0, 0, []);
}
