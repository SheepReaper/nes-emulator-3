namespace Sheep.Emulation.Nes.ConformanceTests;

/// <summary>Automates AccuracyCoin through its controller and zero-page result protocol.</summary>
internal sealed class AccuracyCoinRunner(INesTestMachine machine, int chunkSize = 25_000)
{
    private const ushort RunningAllTestsAddress = 0x0035;
    private const ushort TotalAddress = 0x0037;
    private const ushort CompletionTrampolineAddress = 0x0700;

    internal AccuracyCoinRunResult Run(long maximumPpuDots)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumPpuDots);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunkSize);

        long elapsed = 0;
        var started = StartAllTests(maximumPpuDots, ref elapsed);
        var lastProgress = machine.PeekCpuMemory(TotalAddress);
        var lastProgressAt = elapsed;

        while (elapsed < maximumPpuDots)
        {
            var running = machine.PeekCpuMemory(RunningAllTestsAddress) != 0;
            var total = machine.PeekCpuMemory(TotalAddress);
            if (started && !running && total != 0 && machine.PeekCpuMemory(CompletionTrampolineAddress) == 0x4C)
            {
                return AccuracyCoinSnapshotBuilder.Complete(machine, total, elapsed);
            }

            if (total != lastProgress)
            {
                lastProgress = total;
                lastProgressAt = elapsed;
            }
            else if (started && elapsed - lastProgressAt >= 100_000_000)
            {
                return AccuracyCoinSnapshotBuilder.Snapshot(machine, AccuracyCoinOutcome.TimedOut, total, elapsed);
            }

            AccuracyCoinMenuNavigator.RunChunk(machine, chunkSize, NesControllerButton.None, maximumPpuDots, ref elapsed, false);
        }

        return new AccuracyCoinRunResult(AccuracyCoinOutcome.TimedOut, 0, 0, 0, elapsed, []);
    }

    internal AccuracyCoinSingleResult RunSingle(
        int suiteIndex, int testIndex, ushort resultAddress, long maximumPpuDots,
        Action? beforeTest = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(suiteIndex);
        ArgumentOutOfRangeException.ThrowIfNegative(testIndex);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumPpuDots);

        long elapsed = 0;
        if (!AccuracyCoinMenuNavigator.WaitForMenuReady(machine, chunkSize, maximumPpuDots, ref elapsed))
        {
            return new AccuracyCoinSingleResult(AccuracyCoinOutcome.TimedOut, 0, elapsed);
        }

        var (_, routineAddr) = AccuracyCoinCatalog.GetTestEntry(machine, suiteIndex, testIndex);

        beforeTest?.Invoke();

        // 1. Initialize zero-page variables expected by AccuracyCoin tests
        machine.WriteCpuMemory(0x0010, 0x01); // ErrorCode = 1
        machine.WriteCpuMemory(0x0011, 0x01); // initialSubTest = 1
        machine.WriteCpuMemory(0x0012, 0x01); // result_DMCDMASync_PreTest = 1
        machine.WriteCpuMemory(0x003A, 0x01); // result_VblankSync_PreTest = 1
        machine.WriteCpuMemory(0x0027, 0x80); // Test_UnOp_SP = $80
        machine.WriteCpuMemory(0x001E, (byte)(resultAddress & 0xFF)); // TestResultPointer Lo
        machine.WriteCpuMemory(0x001F, (byte)(resultAddress >> 8));   // TestResultPointer Hi
        machine.SetControllerState(0, NesControllerButton.None);

        // 2. Clear Page 2 (OAM scratch with $FF) and Page 5 (test scratch RAM with 0)
        for (ushort addr = 0x0200; addr <= 0x02FF; addr++)
        {
            machine.WriteCpuMemory(addr, 0xFF);
        }
        for (ushort addr = 0x0500; addr <= 0x05FF; addr++)
        {
            machine.WriteCpuMemory(addr, 0x00);
        }

        // 3. Populate JSRFromRAM at $001A (JSR routineAddr, RTS)
        machine.WriteCpuMemory(0x001A, 0x20); // JSR
        machine.WriteCpuMemory(0x001B, (byte)(routineAddr & 0xFF));
        machine.WriteCpuMemory(0x001C, (byte)(routineAddr >> 8));
        machine.WriteCpuMemory(0x001D, 0x60); // RTS

        machine.WriteCpuMemory(0x003C, 0x00); // IncorrectReturnAddressOffset = 0

        // 4. Disable NMI from PPUCTRL while preserving pattern table configuration
        var ppuctrl = (byte)(machine.PeekCpuMemory(0x00F0) & 0x7F);
        machine.WriteCpuMemory(0x00F0, ppuctrl);
        var ppumask = machine.PeekCpuMemory(0x00F1);

        // 5. Inject trampoline at $0380 and completion flag at $03DF (avoiding $0700 which is used by Implied Dummy Reads backup)
        const ushort trampolineAddress = 0x0380;
        const ushort completionFlagAddress = 0x03DF;

        var trampoline = new byte[]
        {
            0x78,                                                           // 0380: SEI (Disable IRQ)
            0xA9, ppuctrl,                                                  // 0381: LDA #ppuctrl
            0x8D, 0x00, 0x20,                                               // 0383: STA $2000 (Disable NMI)
            0xA9, ppumask,                                                  // 0386: LDA #ppumask
            0x8D, 0x01, 0x20,                                               // 0388: STA $2001
            0xA2, 0x08,                                                     // 038B: LDX #8
            0xAD, 0x16, 0x40,                                               // 038D: LDA $4016 (Read controller 1)
            0xCA,                                                           // 0390: DEX
            0xD0, 0xFA,                                                     // 0391: BNE 038D
            0xAD, 0x02, 0x20,                                               // 0393: LDA $2002 (Wait for VBlank start)
            0x10, 0xFB,                                                     // 0396: BPL 0393
            0xA9, 0x00,                                                     // 0398: LDA #0
            0xAA,                                                           // 039A: TAX
            0xA8,                                                           // 039B: TAY
            0x38,                                                           // 039C: SEC (Set carry flag)
            0x20, 0x1A, 0x00,                                               // 039D: JSR $001A (Call JSRFromRAM)
            0x8D, (byte)(resultAddress & 0xFF), (byte)(resultAddress >> 8), // 03A0: STA resultAddress
            0xA9, 0x00,                                                     // 03A3: LDA #0
            0x8D, 0x15, 0x40,                                               // 03A5: STA $4015 (Silence APU)
            0xA9, 0x55,                                                     // 03A8: LDA #$55
            0x8D, (byte)(completionFlagAddress & 0xFF), (byte)(completionFlagAddress >> 8), // 03AA: STA $03DF
            0x4C, (byte)((trampolineAddress + 45) & 0xFF), (byte)(trampolineAddress >> 8)  // 03AD: JMP self
        };

        for (var i = 0; i < trampoline.Length; i++)
        {
            machine.WriteCpuMemory((ushort)(trampolineAddress + i), trampoline[i]);
        }

        machine.WriteCpuMemory(completionFlagAddress, 0x00);
        machine.SetCpuRegisters(new Sheep.Emulation.Nes.Debugging.CpuRegisterValues(
            accumulator: 0,
            x: 0,
            y: 0,
            stackPointer: 0xFD,
            programCounter: trampolineAddress,
            status: 0x24));

        while (elapsed < maximumPpuDots)
        {
            if (machine.PeekCpuMemory(completionFlagAddress) == 0x55)
            {
                var value = machine.PeekCpuMemory(resultAddress);
                var outcome = (value & 1) != 0 ? AccuracyCoinOutcome.Passed : AccuracyCoinOutcome.Failed;
                return new AccuracyCoinSingleResult(outcome, value, elapsed);
            }
            AccuracyCoinMenuNavigator.RunChunk(machine, chunkSize, NesControllerButton.None, maximumPpuDots, ref elapsed, false);
        }
        return new AccuracyCoinSingleResult(AccuracyCoinOutcome.TimedOut, 0, elapsed);
    }

    private bool StartAllTests(long maximumPpuDots, ref long elapsed)
    {
        var started = false;
        var controllerReleased = false;
        while (elapsed < maximumPpuDots && !started)
        {
            AccuracyCoinMenuNavigator.RunChunk(machine, chunkSize, NesControllerButton.Start, maximumPpuDots, ref elapsed);
            controllerReleased = false;
            started = machine.PeekCpuMemory(RunningAllTestsAddress) != 0;
            if (started || elapsed >= maximumPpuDots)
            {
                break;
            }

            AccuracyCoinMenuNavigator.RunChunk(machine, chunkSize, NesControllerButton.None, maximumPpuDots, ref elapsed);
            controllerReleased = true;
            started = machine.PeekCpuMemory(RunningAllTestsAddress) != 0;
        }

        if (!controllerReleased)
        {
            machine.SetControllerState(0, NesControllerButton.None);
        }
        return started;
    }
}
