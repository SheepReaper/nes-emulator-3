# NES hardware conformance tests

This project runs pinned, hardware-validated test ROMs headlessly through the emulator. It uses the Blargg `$6000` result protocol and deterministic emulated PPU-dot timeouts; it does not inspect rendered text or depend on wall-clock timing.

Install the assets once:

```powershell
./test/conformance/Install-TestRoms.ps1
```

Then run the suite:

```powershell
dotnet test test/conformance/SR.Emulation.Nes.ConformanceTests.csproj
```

Set `NES_TEST_ROMS` to use an existing checkout of `christopherpow/nes-test-roms`. Every selected binary is pinned by upstream commit and SHA-256 in `test-roms.json`. Missing assets skip only the external-ROM cases; parser, runner, reset, timeout, and manifest integrity tests always run.

The selected baseline covers NTSC PPU VBlank/NMI behavior and Mapper 4 IRQ/A12 behavior. A conformance failure is intentionally not converted into an expected result: it remains a failing test until the emulator is corrected or a documented hardware-variant policy is added.
