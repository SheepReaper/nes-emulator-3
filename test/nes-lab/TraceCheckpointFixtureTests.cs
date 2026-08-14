using System.Security.Cryptography;
using Sheep.Emulation.Nes;

namespace Sheep.Nes.Lab.Tests;

public sealed class TraceCheckpointFixtureTests
{
    [Fact]
    public async Task FixedNopProgram_ProducesDeterministicTraceArtifactWhenRequested()
    {
        var rom = CreateNopRom();
        var nes = new NesSystem();
        nes.LoadRom(rom);
        nes.Debugger.EnableCpuClockTracing(256);

        nes.RunForPpuDots(300);

        var trace = nes.Debugger.GetCpuClockTrace();
        Assert.Equal(100, trace.Count);
        var path = Environment.GetEnvironmentVariable("NES_LAB_CHECKPOINT_TRACE_PATH");
        if (!string.IsNullOrWhiteSpace(path))
        {
            var artifact = TraceArtifactFactory.Create(trace, new TraceArtifactMetadata(
                Convert.ToHexString(SHA256.HashData(rom)),
                "checkpoint-fixture",
                DateTimeOffset.UtcNow,
                new TraceRunMetadata("nop-checkpoint.nes", "NTSC", "nes-lab", "nop-cycle")));
            await new TraceArtifactWriter().WriteAsync(
                artifact, path, TestContext.Current.CancellationToken);
        }
    }

    private static byte[] CreateNopRom()
    {
        var rom = new byte[16 + 16 * 1024 + 8 * 1024];
        rom[0] = (byte)'N'; rom[1] = (byte)'E'; rom[2] = (byte)'S'; rom[3] = 0x1A;
        rom[4] = 1; rom[5] = 1;
        Array.Fill(rom, (byte)0xEA, 16, 16 * 1024);
        var vector = 16 + 0x3FFC;
        rom[vector] = 0x00;
        rom[vector + 1] = 0x80;
        return rom;
    }
}
