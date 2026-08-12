using Sheep.Emulation.Nes.Debugging;
using Sheep.Nes.Lab;

namespace Sheep.Emulation.Nes.ConformanceTests;

/// <summary>
/// Harness for tracing and executing single AccuracyCoin bus/timing test cases.
/// </summary>
internal static class AccuracyCoinHarness
{
    internal static AccuracyCoinSingleResult RunFocused(
        NesSystem nes,
        int suiteIndex,
        int testIndex,
        ushort resultAddress,
        string name,
        out string diagnosticMessage)
    {
        var syncExitClock = 0UL;
        var nextDmaClock = 0UL;
        var originalRequestDmcDma = nes.CpuBus.RequestDmcDma;
        var captured50 = new byte[32];
        var writeLog = new List<string>();
        var dmcDmaEvents = new List<ulong>();
        var oamDmaEvents = new List<ulong>();
        var testStarted = false;
        var traceCapture = ConformanceTraceCapture.FromEnvironment(
            nes, "AccuracyCoin.nes", AccuracyCoinLoader.RomSha256, "Ntsc", "AccuracyCoin", name);
        var traceBoundaryPending = false;
        var apuStatusBoundaryPending = false;
        var apuStatusCheckpointCount = 0;

        nes.CpuBus.DebugAccessed = (kind, addr, val) =>
        {
            if (!testStarted)
            {
                return;
            }

            if (traceBoundaryPending)
            {
                traceCapture?.MarkCheckpoint("result-write", "assertion", "AccuracyCoin result address write");
                traceBoundaryPending = false;
            }

            if (apuStatusBoundaryPending)
            {
                traceCapture?.MarkCheckpoint($"apu-status-access-{apuStatusCheckpointCount}",
                    "hardware", "$4015 access", maximumRecords: 32);
                apuStatusBoundaryPending = false;
            }

            if (kind == NesDebugBreakKind.CpuWrite && addr == 0x4014)
            {
                if (oamDmaEvents.Count == 0)
                    traceCapture?.MarkCheckpoint("first-oam-dma-request", "hardware", "$4014 write");
                if (oamDmaEvents.Count < 40)
                {
                    oamDmaEvents.Add(nes.CpuClockCounter);
                }
            }

            if (kind == NesDebugBreakKind.CpuWrite && addr >= 0x0050 && addr <= 0x006F)
            {
                captured50[addr - 0x0050] = val;
                if (writeLog.Count < 100)
                {
                    writeLog.Add($"${addr:X2}=${val:X2}@{nes.CpuClockCounter} PC={nes.Debugger.ProgramCounter:X4}");
                }
            }

            if (kind == NesDebugBreakKind.CpuWrite && addr == resultAddress)
                traceBoundaryPending = true;

            if (addr == 0x4015 && apuStatusCheckpointCount < 32)
            {
                apuStatusCheckpointCount++;
                apuStatusBoundaryPending = true;
            }

        };

        nes.Apu.ConnectDmcDma((addr, cb) =>
        {
            if (testStarted && dmcDmaEvents.Count < 60)
            {
                if (dmcDmaEvents.Count == 0)
                    traceCapture?.MarkCheckpoint("first-dmc-dma-request", "hardware", "APU DMC request");
                dmcDmaEvents.Add(nes.CpuClockCounter);
            }
            originalRequestDmcDma(addr, cb);
        }, () => nes.CpuBus.AbortDmcDma());

        var maxDots = name is "DMC DMA + OAM DMA" or "Implied Dummy Reads" or "DMC DMA Bus Conflicts" or "Explicit DMA Abort" or "Implicit DMA Abort"
            ? 250_000_000L
            : name is "PPU Register Open Bus"
                ? 25_000_000L
                : 50_000_000L;
        var result = new AccuracyCoinRunner(new NesTestMachine(nes), chunkSize: 25_000).RunSingle(
            suiteIndex, testIndex, resultAddress, maximumPpuDots: maxDots,
            beforeTest: () =>
            {
                testStarted = true;
                traceCapture?.Start();
                traceCapture?.MarkCheckpoint("test-entry", "entry", "AccuracyCoin focused runner");
            });

        traceCapture?.Complete(result.Outcome == AccuracyCoinOutcome.Passed, 0);

        diagnosticMessage =
            $"AccuracyCoin {name}: {result.Outcome}, value ${result.Value:X2}, " +
            $"after {result.ElapsedPpuDots:N0} PPU dots; PC=${nes.Debugger.ProgramCounter:X4}, " +
            $"syncExit={syncExitClock}, nextDma={nextDmaClock}, diff={(nextDmaClock > syncExitClock ? nextDmaClock - syncExitClock : 0)}, " +
            $"suite={nes.Debugger.PeekCpuMemory(0x14)}, cursor={nes.Debugger.PeekCpuMemory(0x16):X2}, " +
            $"ready={nes.Debugger.PeekCpuMemory(0xEC):X2}, " +
            $"error={nes.Debugger.PeekCpuMemory(0x10):X2}, " +
            $"writeLog=[{string.Join("; ", writeLog)}], " +
            $"oamDma=[{string.Join(", ", oamDmaEvents)}], " +
            $"dmcDma=[{string.Join(", ", dmcDmaEvents.Take(20))}], " +
            $"zp50={string.Join(' ', Enumerable.Range(0, 32).Select(index => $"{nes.Debugger.PeekCpuMemory((ushort)(0x50 + index)):X2}"))}, " +
            $"ram500={string.Join(' ', Enumerable.Range(0, 32).Select(index => $"{nes.Debugger.PeekCpuMemory((ushort)(0x500 + index)):X2}"))}, " +
            $"ram520={string.Join(' ', Enumerable.Range(0, 32).Select(index => $"{nes.Debugger.PeekCpuMemory((ushort)(0x520 + index)):X2}"))}, " +
            $"ram540={string.Join(' ', Enumerable.Range(0, 32).Select(index => $"{nes.Debugger.PeekCpuMemory((ushort)(0x540 + index)):X2}"))}, " +
            $"SP={nes.Debugger.CaptureSnapshot().Cpu?.StackPointer:X2}, " +
            $"stack={string.Join(' ', Enumerable.Range(0xF0, 16).Select(index => $"{nes.Debugger.PeekCpuMemory((ushort)(0x100 + index)):X2}"))}, " +
            $"ram600={string.Join(' ', Enumerable.Range(0, 8).Select(index => $"{nes.Debugger.PeekCpuMemory((ushort)(0x600 + index)):X2}"))}.";

        return result;
    }

}
