# NES Emulator Agent Guidance

Use the hardware-validated ROMs in `test/conformance` as integration tests, but read the corresponding source under `test-roms/nes-test-roms` before interpreting a terse failure label. Locate the byte or instruction that distinguishes the two sides of a timing boundary and inspect it directly when needed. Do not infer direction solely from names such as “sooner” or “later.”

Run the main verification surfaces with:

```powershell
dotnet test test/cpu/SR.Emulation.Nes.Tests.csproj --no-restore --output Normal
dotnet test test/conformance/SR.Emulation.Nes.ConformanceTests.csproj --no-restore --output Normal
dotnet test test/emulator-winui/EmuSheep.Tests.csproj --no-restore --output Normal
dotnet build src/lib/emulation/SR.Emulation.Nes.csproj --no-restore
git diff --check
```

The successful paths for `ppu_vbl_nmi/10-even_odd_timing` and `mmc3_test_2/4-scanline_timing` need substantially more than ten million PPU dots. Do not reduce their manifest ceilings based on an earlier failing path.

## Shared timing model

- `Nes` owns the shared clock. NTSC uses three PPU dots per CPU clock; mapper M2 filtering must receive the same CPU-clock cadence rather than deriving it from elapsed PPU dots.
- CPU-visible absolute I/O reads and writes occur on the instruction's final cycle. Memory read-modify-write results used as timing discriminators must likewise become visible on their final write cycle.
- NMI is edge-sensitive. Capture a PPU NMI edge even if the line rises and falls between CPU clocks, but reject a pulse shortened by the pre-render clear before it reaches the CPU latch.
- An NMI first observed at an opcode boundary has missed the preceding poll and waits through the next instruction.
- IRQ is level-sensitive but is polled on the instruction's penultimate cycle. Service only the latched poll result at the next boundary. In this functional CPU, `_cycles == 2` is that polling point, including the opcode-fetch cycle of a two-cycle instruction.

## PPU and mapper bus phases

- NTSC odd frames omit dot 340 by jumping after pre-render dot 339. Rendering eligibility for this skip is sampled one dot earlier, at dot 338; a later PPUMASK change must not retroactively change the decision.
- A PPU fetch presents its address before the data read. Mapper-visible A12 transitions belong to the address-presentation dot, while renderer latches may still load data on the following phase.
- For 8x8 sprites with sprites at `$1000`, the MMC3-qualified A12 transition is associated with sprite fetch dot 260 in this model.
- With backgrounds at `$1000`, present the background pattern address at dot 324 of the preceding scanline. Do not clock MMC3 only when the pattern byte is consumed.
- MMC3 A12 filtering is governed by falling CPU M2 edges, not a fixed eight-PPU-dot duration. `Mmc3Cart` counts completed low M2 samples; its internal threshold includes the transition interval, so direct mapper tests use four clock notifications to represent three complete low samples.
- Debugger peeks and framebuffer palette lookup must not create mapper-visible PPU address transitions.

When changing any one of these phases, add a focused unit test for the exact boundary and run the complete conformance suite. Remove temporary console tracing and diagnostic assertion fields before handing off.
