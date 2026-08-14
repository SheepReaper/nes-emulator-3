---
applyTo: '**'
description: 'Use NES Lab as the primary local evidence gateway for this repository.'
---

# NES Lab Gateway

For emulator, conformance, timing, mapper, PPU, APU, DMA, WinUI, and tooling work, use the local `nes-lab` CLI or MCP server as the first verification and evidence surface.

This repository runs on Windows PowerShell. Use `rg` for repository search; do not use `grep`.
Prefer PowerShell-native commands for filesystem and process operations, and use `Select-String`
for targeted fallback checks only when `rg` is unavailable.
Python is not required for normal emulator or NES Lab work. Do not launch Python unless the task
explicitly requires a Python script; when it does, use `py -3.14` rather than bare `python`.
Avoid interactive commands and REPLs; use bounded, non-interactive commands so the agent does not
wait indefinitely for manual input.

Start with a bounded context packet and changed-file verification before broad source reads:

```powershell
dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj --no-restore -- context build --task "<issue or change>" --budget 16000
dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj --no-restore -- verify --changed
```

These commands intentionally run the current source checkout. The registered MCP server uses the
published Release binary at `.artifacts/nes-lab/gateway/Sheep.Nes.Lab.dll`; rebuild it with
`.\tools\Restore-NesLab.ps1` after changing NES Lab source or restoring a fresh checkout.

For failures, inspect `failures latest`, `runs latest`, or `diagnose --run latest`. Use trace queries, ROM lookup, and Roslyn code queries to gather focused evidence. Preserve full logs and traces as local artifacts and pass bounded packets or immutable artifact URIs to the model.

Verification does not accept a context budget. Use `verify --scope ...` (or `verify --changed`)
for execution, and use `diagnose --budget <bytes>` or `context build --budget <bytes>` to limit
agent-facing evidence. If `diagnose --budget` is rejected, refresh the published gateway with
`.\tools\Restore-NesLab.ps1` and verify that the command is running from this checkout.

Use typed NES Lab operations through MCP. Do not replace this gateway with generic shell or filesystem MCP access. Keep hardware conclusions source-backed and distinguish known baseline failures from new regressions.

This repository uses Microsoft.Testing.Platform through `global.json`. For direct filtered test
runs, forward `--filter-class`, `--filter-method`, or `--filter-display-name` after the `--`
separator; do not translate a class selection to the VSTest form
`--filter "FullyQualifiedName~..."`, which can select zero tests under the xUnit MTP runner.
Keep VSTest-only options such as `--logger "console;verbosity=minimal"` out of the forwarded
arguments. `nes-lab` already emits the native filter options.