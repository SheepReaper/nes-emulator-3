---
name: nes-lab-gateway
description: Use the repository's NES Lab CLI and MCP server as the primary evidence gateway for emulator changes, failures, verification, traces, ROMs, and context packets.
---

# NES Lab Gateway

## Use When

Use this workflow for NES emulator, conformance, timing, mapper, APU, DMA, PPU, WinUI integration, and NES Lab tooling work.

The repository runs on Windows PowerShell. Use `rg` for repository search; do not use `grep`.
Prefer PowerShell-native filesystem and process commands, using `Select-String` only as a targeted
fallback when `rg` is unavailable.
Python is not part of the normal NES Lab workflow. Do not launch it unless the task explicitly
requires Python; use `py -3.14` instead of bare `python` when it is required. Avoid interactive
commands and REPLs; use bounded, non-interactive commands.

## Process

1. Start with `context build --task` for the issue or requested change.
2. Run `verify --changed` to select the smallest affected verification scopes. If it reports no
   affected scopes, confirm the change is non-functional (documentation, config only) and skip
   verification; otherwise escalate to `verify --all`.
3. For a failure, use `runs latest`, `failures latest`, or `diagnose --run latest` before broad source exploration.
4. Use `trace query`, ROM lookup, and Roslyn code queries for focused evidence.
5. Keep complete logs and traces local; pass the bounded packet and immutable artifact URIs to the agent.
6. Before changing emulator timing or bus behavior, run `verify --changed` to identify the minimal
   affected test scope, then execute only those tests via `dotnet test` with the relevant filter.

**Budget flags:** `verify` has no `--budget` option; it controls execution scope only. Apply
byte/token budgets to `context build` or `diagnose` instead.

**Rejected parser options:** if a current source parser option is rejected, refresh the published
gateway with `.\tools\Restore-NesLab.ps1` before treating it as an implementation gap.

## Harness Integration

The repository's `.agents/mcp_config.json` is the canonical project-local MCP registration. Codex, Copilot, and Antigravity should invoke the published `Sheep.Nes.Lab.dll mcp` gateway for MCP integrations. Keep client-specific configuration adapters outside this skill.

For direct CLI use (not MCP), run source-mode commands with
`dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj -- <command>` so changes to NES Lab
source are reflected immediately. After a fresh checkout or NES Lab source change, run
`.\tools\Restore-NesLab.ps1` to publish the Release gateway and refresh its manifest; MCP clients
always use this published binary, so keep it current whenever NES Lab source changes.
If an active stdio MCP process locks the published DLL, rerun the script with `-Force` or
`--force`. This targets only processes launched with this checkout's exact gateway assembly,
then republishes and validates the replacement with a fresh MCP handshake.

## Verification

```powershell
dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj --no-restore -- context build --task "<issue>" --budget 16000
dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj --no-restore -- verify --changed
dotnet test test/nes-lab/Sheep.Nes.Lab.Tests.csproj --no-restore --output Normal
```
