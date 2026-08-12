# NES hardware conformance tests

This project runs pinned, hardware-validated test ROMs headlessly through the emulator. It uses the Blargg `$6000` result protocol and deterministic emulated PPU-dot timeouts. The harness drives `NesSystem` directly, so WinUI clock pacing and audio-buffer synchronization are not involved and emulation runs as fast as the host permits.

The project uses Microsoft Testing Platform with xUnit. Independent ROM cases may run concurrently, capped at half the host's logical processors to avoid oversubscribing these CPU-bound tests. Long-running diagnostics identify a currently executing case without replacing the deterministic PPU-dot ceilings that decide pass, failure, or timeout.

Install the assets once:

```powershell
./test/conformance/Install-TestRoms.ps1
```

Then run the suite:

```powershell
dotnet test test/conformance/Sheep.Emulation.Nes.ConformanceTests.csproj
```

Set `NES_TEST_ROMS` to use an existing checkout of `christopherpow/nes-test-roms`,
and `NES_HOLY_MAPPEREL_ROMS` to use an existing Holy Mapperel `testroms`
directory.
Every selected binary is pinned by upstream commit and SHA-256, either in
`test-roms.json` or beside its legacy-protocol test definition. Missing assets
skip only the external-ROM cases; parser, runner, reset, timeout, and manifest
integrity tests always run.

The pinned checkout also supplies `cpu_timing_test6`, whose official-instruction
path is run by this project, the three ordered `branch_timing_tests` ROMs, and
the `instr_test-v5` official-opcode aggregate. The optional controller input in
`cpu_timing_test6` is left unpressed, selecting official opcodes only.

The selected baseline covers official CPU instructions and timing boundaries,
reset RAM, IRQ/DMA interaction, APU and DMC behavior, NTSC PPU VBlank/NMI
behavior, Mapper 4 IRQ/A12 behavior, and discrete mapper PRG/CHR banking through
Holy Mapperel. A conformance failure is intentionally
not converted into an expected result: it remains a failing test until the
emulator is corrected or a documented hardware-variant policy is added.
