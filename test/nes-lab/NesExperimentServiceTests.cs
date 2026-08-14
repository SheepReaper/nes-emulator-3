using System.Text.Json;
using Sheep.Emulation.Nes.Input;

namespace Sheep.Nes.Lab.Tests;

public sealed class NesExperimentServiceTests
{
    [Fact]
    public async Task Run_IsDeterministicAndCompareUsesCaptureIdentity()
    {
        using var temporary = new TemporaryDirectory();
        var romPath = Path.Combine(temporary.Path, "loop.nes");
        await File.WriteAllBytesAsync(romPath, MinimalLoopRom(), TestContext.Current.CancellationToken);
        var scenarioPath = Path.Combine(temporary.Path, "scenario.json");
        var scenario = new NesExperimentScenario(1, "deterministic-loop", new(Path: romPath), "Ntsc",
            new("ppuDots", 2_000), [new(10, 0, NesControllerButton.A)],
            [new("middle", 1_000, true, false, true, true)], 128, 128);
        await File.WriteAllTextAsync(scenarioPath, JsonSerializer.Serialize(scenario, LabResponseSerializer.Options),
            TestContext.Current.CancellationToken);

        var service = new NesExperimentService(temporary.Path);
        var first = await service.RunAsync(scenarioPath, cancellationToken: TestContext.Current.CancellationToken);
        var second = await service.RunAsync(scenarioPath, cancellationToken: TestContext.Current.CancellationToken);
        var comparison = await service.CompareAsync(first.ResourceUri!, second.ResourceUri!, TestContext.Current.CancellationToken);

        Assert.True(comparison.Equal);
        Assert.Equal(first.Captures.Select(item => item.SnapshotSha256), second.Captures.Select(item => item.SnapshotSha256));
        Assert.Equal(first.Captures.Select(item => item.TraceSha256), second.Captures.Select(item => item.TraceSha256));
        Assert.All(first.Captures.Where(item => item.TraceUri is not null), item =>
            Assert.StartsWith("nes-lab://artifact/trace/sha256/", item.TraceUri));

        var shifted = scenario with { CapturePoints = [new("middle", 1_001, true, false, true, true)] };
        await File.WriteAllTextAsync(scenarioPath, JsonSerializer.Serialize(shifted, LabResponseSerializer.Options),
            TestContext.Current.CancellationToken);
        var changed = await service.RunAsync(scenarioPath, cancellationToken: TestContext.Current.CancellationToken);
        var different = await service.CompareAsync(first.ResourceUri!, changed.ResourceUri!, TestContext.Current.CancellationToken);
        Assert.False(different.Equal);
        var window = Assert.Single(different.Differences, item => item.Category == "capture");
        Assert.Equal("1000", window.Left);
    }

    [Fact]
    public async Task McpSafeRun_RejectsCustomRomPath()
    {
        using var temporary = new TemporaryDirectory();
        var scenarioPath = Path.Combine(temporary.Path, "scenario.json");
        var scenario = new NesExperimentScenario(1, "unsafe", new(Path: "outside.nes"), "Ntsc",
            new("ppuDots", 1), [], []);
        await File.WriteAllTextAsync(scenarioPath, JsonSerializer.Serialize(scenario, LabResponseSerializer.Options),
            TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            new NesExperimentService(temporary.Path).RunAsync(scenarioPath, mcpSafe: true,
                cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task InlineRun_CanonicalizesAndPublishesReproducibleScenario()
    {
        using var temporary = new TemporaryDirectory();
        var romPath = Path.Combine(temporary.Path, "loop.nes");
        await File.WriteAllBytesAsync(romPath, MinimalLoopRom(), TestContext.Current.CancellationToken);
        var scenario = new NesExperimentScenario(1, "inline", new(Path: romPath), "Ntsc",
            new("ppuDots", 20), [], [], 16, 16, 100);
        var json = JsonSerializer.Serialize(scenario, LabResponseSerializer.Options);

        var result = await new NesExperimentService(temporary.Path).RunInlineAsync(json,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.StartsWith("nes-lab://artifact/scenario/sha256/", result.ScenarioUri);
        Assert.Contains("--scenario-uri", result.ReproductionCommand, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InlineRun_RejectsExcessiveCaptureCountBeforeExecution()
    {
        using var temporary = new TemporaryDirectory();
        var scenario = new NesExperimentScenario(1, "too-many", new(Path: "rom.nes"), "Ntsc",
            new("ppuDots", 20), [], Enumerable.Range(0, 33).Select(index =>
                new ExperimentCapturePoint(index.ToString(), (ulong)index)).ToArray());

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            new NesExperimentService(temporary.Path).RunInlineAsync(
                JsonSerializer.Serialize(scenario, LabResponseSerializer.Options),
                cancellationToken: TestContext.Current.CancellationToken));
    }

    private static byte[] MinimalLoopRom()
    {
        var rom = new byte[16 + 0x4000 + 0x2000];
        rom[0] = (byte)'N'; rom[1] = (byte)'E'; rom[2] = (byte)'S'; rom[3] = 0x1A;
        rom[4] = 1; rom[5] = 1;
        rom[16] = 0x4C; rom[17] = 0x00; rom[18] = 0xC0;
        var vector = 16 + 0x3FFC; rom[vector] = 0x00; rom[vector + 1] = 0xC0;
        return rom;
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "nes-lab-experiment-" + Guid.NewGuid().ToString("N"));
        public TemporaryDirectory() => Directory.CreateDirectory(Path);
        public void Dispose() => Directory.Delete(Path, true);
    }
}
