---
name: nes-accuracy-coin-debugging
description: "Debug CPU-cycle, DMA, NMI, IRQ, open-bus, and mapper timing failures in this NES emulator, especially AccuracyCoin conformance failures. Use when an AccuracyCoin test fails, DMA timing is uncertain, or a cycle-level trace is needed."
---

# NES AccuracyCoin Timing Debugging

## Goal

Turn a conformance failure into one small, observable timing boundary. Do not infer a DMA or interrupt schedule from the final result byte alone.

## Workflow

1. Read `AGENTS.md`, then locate the failing test in `test/conformance/AccuracyCoinConformanceTests.cs` and the corresponding AccuracyCoin assembly source.
2. Identify the instruction, read/write, or branch whose timing distinguishes pass from fail. Establish its expected side of the boundary from the assembly, not from a label such as "early" or "late".
3. In a focused CPU or conformance test, enable the bounded trace before running the real setup:

```csharp
nes.Debugger.EnableCpuClockTracing(512);
// Run the real ROM/test setup to the suspect boundary.
var trace = nes.Debugger.GetCpuClockTrace();
```

4. Inspect the final records around the event. Each `NesCpuClockTrace` is a post-clock record containing:
   - CPU clock; CPU and PPU state
   - selected `Cpu`, `OamDma`, or `DmcDma` scheduler actor
   - next pending CPU-bus address; NMI and IRQ line levels
   - actual CPU-bus reads/writes for that clock
   - open/internal bus latches and OAM/DMC DMA state
5. For DMC/OAM overlap, trust `BusAccesses` over the actor name alone: a `DmcDma` scheduling slot can include an OAM transfer while DMC timing is counted.
6. Write one focused boundary test that asserts the relevant trace and emulated-memory outcome. Only then change the owning component: `NesSystem` for clock order, `CpuBus` for DMA arbitration/bus values, `ApuDmc` for DMC request timing, or `Cpu` for instruction-cycle visibility.
7. Disable tracing for long runs with `nes.Debugger.DisableCpuClockTracing()`. Do not leave temporary logging or trace-driven diagnostic strings in conformance assertions.

## Limits

The trace does not capture or restore a complete emulator checkpoint. It is not a license to synthesize hidden component state with debugger memory writes. Use a real ROM execution to obtain a valid initial state.

## Validation

Run the focused test first, then:

```powershell
dotnet test test/cpu/Sheep.Emulation.Nes.Tests.csproj --no-restore --output Normal
dotnet test test/conformance/Sheep.Emulation.Nes.ConformanceTests.csproj --no-restore --output Normal
git diff --check
```