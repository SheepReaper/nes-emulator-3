# NES Emulator Agent Guidance

Read the nearest nested `AGENTS.md` before changing a subsystem. Detailed timing, conformance-ROM, and WinUI interop rules are scoped to their relevant directories.

This repository runs on Windows PowerShell. Use `rg` for repository search; do not use `grep`.
Prefer PowerShell-native commands for filesystem and process operations, and use `Select-String`
for targeted fallback checks only when `rg` is unavailable.
Python is not required for normal emulator or NES Lab work. Do not launch Python unless the task
explicitly requires a Python script; when it does, use `py -3.14` rather than bare `python`.
Avoid interactive commands and REPLs; use bounded, non-interactive commands so the agent does not
wait indefinitely for manual input.

## Primary evidence gateway

Use `nes-lab` as the repository's primary verification and evidence gateway for all emulator and tooling work. Follow this workflow in order:

1. Run only the two commands below; do not read additional source files or run additional commands until their output is reviewed.

```powershell
dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj --no-restore -- context build --task "<issue or change>" --budget 16000
dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj --no-restore -- verify --changed
```

2. If `verify --changed` reports failures, inspect them with `diagnose --run latest` or a named `diagnose --case` command before running any test project directly.
3. Only if the gateway output does not resolve the issue, run the specific `dotnet test`/`dotnet build` project for the affected subsystem from the reference commands below. Do not run the full block of reference commands unless every subsystem is in scope.

These `dotnet run` commands are source-mode CLI commands and always include `--no-restore`. MCP clients use the published Release
gateway at `.artifacts/nes-lab/gateway/Sheep.Nes.Lab.dll`. After cloning or changing NES Lab
source, run `.\tools\Restore-NesLab.ps1` to rebuild the ignored gateway output and refresh its
manifest. If `.\tools\Restore-NesLab.ps1` exits with a non-zero code, stop and report the script output to the user before proceeding with any `nes-lab` commands.
When an active MCP host locks the published assembly, rerun it with `-Force` (or `--force`). The
force flow terminates only the process launched with this checkout's published gateway DLL,
republishes it, and validates the replacement through a fresh MCP handshake.

Use the typed MCP server, repository-local `.agents/mcp_config.json`, and published gateway under `.artifacts/nes-lab/gateway` for agent integrations. Do not expose generic shell or filesystem access as a substitute for typed `nes-lab` operations.

Verification does not accept a context budget. Use `verify --scope ...` (or `verify --changed`)
for execution, and use `diagnose --budget <bytes>` or `context build --budget <bytes>` to limit
agent-facing evidence. If `diagnose --budget` is rejected, refresh the published gateway with
`.\tools\Restore-NesLab.ps1` and verify that the command is running from this checkout.

### Reference: full verification surfaces

Use these only per step 3 above, scoped to the affected project(s); do not run the whole block as a first step:

```powershell
dotnet test test/nes-lab/Sheep.Nes.Lab.Tests.csproj --no-restore --output Normal
dotnet test test/cpu/Sheep.Emulation.Nes.Tests.csproj --no-restore --output Normal
dotnet test test/conformance/Sheep.Emulation.Nes.ConformanceTests.csproj --no-restore --output Normal
dotnet test test/emulator-winui/EmuSheep.Tests.csproj --no-restore --output Normal
dotnet build src/lib/emulation/Sheep.Emulation.Nes.csproj --no-restore
dotnet build src/lib/interop-winui/Sheep.WinUI.Interop.csproj --no-restore
dotnet build src/emulator-winui/EmuSheep.csproj --no-restore
git diff --check
```

## Test filtering

This repository uses Microsoft.Testing.Platform through `global.json`. Do not use the usual
VSTest filter form (`--filter "FullyQualifiedName~..."`) with these test projects; it can
select zero tests and return an MTP non-success exit code. Use the native MTP filters instead:

```powershell
dotnet test <project> -- --filter-class "*TestClass*"
dotnet test <project> -- --filter-method "*TestClass.TestMethod*"
dotnet test <project> -- --filter-display-name "*case name*"
```

When translating an agent-generated `FullyQualifiedName~ClassName` filter, use
`-- --filter-class "*ClassName*"`. `nes-lab` already emits MTP-native filters for named cases.
Keep VSTest-only options such as `--logger "console;verbosity=minimal"` out of the forwarded
arguments; use the default console output or MTP-native reporting options instead.

For NES timing, conformance, and emulator-diagnosis work, follow the same primary evidence gateway workflow above instead of broad repository reads:

```powershell
dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj --no-restore -- context build --task "<issue or symptom>" --budget 16000
dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj --no-restore -- verify --scope cpu
dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj --no-restore -- diagnose --run latest --budget 16000
```

Use the focused packet and reduced verification output before escalating to broader suite or code reads. Preserve unrelated user changes in a dirty worktree. Add a focused test for changed behavior and remove temporary tracing before handing off.
