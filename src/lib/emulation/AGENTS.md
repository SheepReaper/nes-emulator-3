# Emulation Core Guidance

## Shared timing model

- `Nes` owns the shared clock. NTSC uses three PPU dots per CPU clock; mapper M2 filtering receives that CPU-clock cadence.
- APU and CPU share clock timestamps. Advance APU clock state at the end of the CPU cycle so writes to `$4000`–`$4017` see CPU/DMA parity for that cycle.
- CPU-visible absolute I/O and RMW final writes occur on the instruction's final cycle. Clear `RmwDummyReadAddress` as soon as its dummy read executes.
- NMI rules, in precedence order:
	1. NMI is edge-sensitive.
	2. Capture inter-clock edges.
	3. Reject pulses shortened by pre-render clear before the CPU latch; this takes priority over rule 2.
	4. If the edge first becomes visible at an opcode boundary, it missed the preceding poll and defers until after the next instruction completes.
- IRQ is level-sensitive and polled on the penultimate instruction cycle. In this functional CPU, `_cycles == 2` is the polling point, including two-cycle instructions.

## Trace workflow

- For CPU, DMA, or interrupt timing, locate the distinguishing ROM instruction, enable a bounded trace (normally 256–2,048 CPU clocks), and inspect `GetCpuClockTrace()`.
- Trace records describe state after one CPU clock. `BusAccesses` is authoritative when DMC and OAM DMA overlap.
- At runtime, when DMC and OAM DMA overlap, OAM DMA yields to DMC reads per cycle. Document the expected stall count so it can be verified against `BusAccesses` in the trace.
- Tracing is observational, opt-in, and bounded. Reproduce setup through execution; do not manufacture private component state.

## PPU and mapper phases

- NTSC odd frames jump after pre-render dot 339; rendering eligibility is sampled at dot 338.
- Present PPU fetch addresses before reading data. Mapper A12 transitions occur on address presentation.
- For 8x8 sprites at `$1000`, the qualified MMC3 A12 transition is associated with sprite fetch dot 260.
- For backgrounds at `$1000`, present the pattern address at dot 324 of the preceding scanline.
- MMC3 A12 filtering uses falling CPU M2 edges. Direct mapper tests must issue exactly four falling CPU M2 edge notifications to accumulate three complete consecutive low samples; each notification corresponds to one falling M2 edge.
- Debugger peeks and framebuffer palette lookup must not create mapper-visible address transitions.

Add a focused boundary test and run full conformance when changing these phases.
