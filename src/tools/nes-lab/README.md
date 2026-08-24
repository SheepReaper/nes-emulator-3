# NES Lab gateway

NES Lab runs focused verification and returns compact, provenance-bearing evidence while retaining complete local artifacts under `.artifacts/nes-lab`.

## Common commands

The commands below use the source project and always run the current checkout. This is the
developer and CI mode. Restore the published MCP gateway with:

```powershell
.\tools\Restore-NesLab.ps1
```

If an active MCP client is holding the published gateway assembly open, use
`.\tools\Restore-NesLab.ps1 -Force` (PowerShell also accepts `--force`). The force flow terminates
only `dotnet` processes whose command line names this checkout's exact published gateway DLL,
republishes it, and launches a fresh MCP health probe. MCP clients that supervise stdio servers
normally reconnect automatically after the old process exits.

Long-lived MCP registrations use the generated Release gateway instead:

```powershell
dotnet .artifacts/nes-lab/gateway/Sheep.Nes.Lab.dll mcp
```

Do not replace source-mode `dotnet run` commands with the published gateway when developing NES
Lab itself; the published output can be stale until the restore script is run.

```powershell
dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj -- verify --scope cpu
dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj -- verify --changed --baseline-aware-exit-code
dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj -- baseline update
dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj -- baseline update --apply
dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj -- trace capture --case "Implicit DMA Abort"
dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj -- diagnose --case "Delta Modulation Channel" --budget 16000
dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj -- diagnose --run latest --budget 16000
dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj -- code index
dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj -- context build --id <stable-symbol-id> --budget 16000 --budget-tokens 4000
dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj -- context build --changed --base origin/main
dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj -- context build --subsystem ppu
dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj -- context build --task "fix sprite priority"
dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj -- context build --task "fix sprite priority" --synthesize local
dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj -- context build --run latest
dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj -- gateway benchmark --corpus 3
dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj -- gateway benchmark --corpus 3 --agent local
dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj -- references sync
dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj -- references search dma
dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj -- experiment run --scenario tasks/nes-lab/experiments/controller-dma-repeat.json
dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj -- experiment compare --left <experiment-uri> --right <experiment-uri>
dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj -- experiment run --inline '<scenario-json>'
dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj -- media frame compare --left <frame-uri> --right <frame-uri> --heatmap
dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj -- media audio analyze --uri <audio-uri>
dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj -- investigate --task "diagnose DMC timing" --agent local --budget 16000 --max-steps 8
dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj -- session close --task "DMC investigation" --run latest --packet <context-uri>
dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj -- session close --task "DMC investigation" --telemetry '<host-reported-json>'
dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj -- context build --handoff <handoff-uri> --budget 16000
dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj -- history failures latest
dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj -- build diagnose --run latest
dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj -- host diagnostics import --path host-diagnostics.json
Get-Content usage.json | dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj -- telemetry normalize
```

`diagnose --run` is read-only and returns an in-memory packet. Add `--persist` to publish and pin it. Add `--rank local` to use Ollama after deterministic evidence collection; unavailable or invalid model output leaves the deterministic packet unchanged. Use `--budget-tokens` when you need a stricter context cap than the raw byte budget, and keep the deterministic packet authoritative even when a local model suggests different evidence.

`verify` options select and execute tests; it has no `--budget` option. Evidence budgets apply to
`context build` and `diagnose`, for example `diagnose --case "Implicit DMA Abort" --budget 16000`.
Verification responses always separate `executionPassed`, `matchesAcceptedBaseline`, `hasRegressions`,
`hasResolvedBaselineCases`, `verificationStatus`, and `exitPolicy`. The default strict policy preserves
the underlying command failure. Opt-in `--baseline-aware-exit-code` returns zero for the accepted
conformance baseline or an improved baseline, but never for a new failure, changed diagnostic,
unexpected pass/skip count, cancellation, or infrastructure failure.

When a complete conformance run resolves accepted failures, `baseline update` previews the exact
count and known-failure changes. `--apply` is deliberately separate and atomically adopts only that
improvement. Focused or stale runs, incomplete summaries, new failures, and changed known-failure
diagnostics are rejected, so baseline management cannot silently bless a regression. The same
workflow is available through the MCP `baseline.show` and state-changing `baseline.update` operations.

The repository-level enforcement remains simple: start with `context build` or `diagnose --run latest` before a broad code read, and use `gateway benchmark` to measure whether the evidence-first flow still preserves required evidence under tight budgets.

## Immutable MCP resources

The stdio server exposes three tools plus MCP resource listing, a resource template, and a retrieval handler. Artifact URIs are content-addressed:

```text
nes-lab://artifact/log/sha256/{digest}
nes-lab://artifact/trace/sha256/{digest}
nes-lab://artifact/context/sha256/{digest}
nes-lab://artifact/run/sha256/{digest}
nes-lab://artifact/memory/sha256/{digest}
nes-lab://artifact/reference/sha256/{digest}
nes-lab://artifact/experiment/sha256/{digest}
nes-lab://artifact/snapshot/sha256/{digest}
nes-lab://artifact/frame/sha256/{digest}
nes-lab://artifact/audio/sha256/{digest}
nes-lab://artifact/scenario/sha256/{digest}
nes-lab://artifact/frame-diff/sha256/{digest}
nes-lab://artifact/investigation/sha256/{digest}
nes-lab://artifact/handoff/sha256/{digest}
nes-lab://artifact/host-diagnostics/sha256/{digest}
```

Retrieval recomputes the digest and rejects artifacts larger than 16 MiB. MCP never inlines a large artifact: resources above the response threshold return verified metadata, a bounded preview, and follow-up query commands. Trace deserialization additionally limits records and nested bus accesses. Mutable queries such as `runs latest` return the resolved immutable URI.

The MCP server also exposes stable semantic resources at `nes://architecture/timing`,
`nes://architecture/bus-map`, and `nes://roms/{suite}/{case}`. Prompt workflows are available
as `diagnose-timing-failure`, `review-mapper-change`, `review-apu-dma-change`, and
`prepare-conformance-fix`; they guide agents toward typed inspection and verification calls.

```powershell
dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj -- artifacts list
dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj -- artifacts pin --uri <uri>
dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj -- artifacts unpin --uri <uri>
dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj -- artifacts prune --older-than 30d
dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj -- artifacts describe --uri <uri>
dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj -- artifacts text --uri <uri> --start-line 1 --max-lines 200
dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj -- trace query --artifact-uri <trace-uri> --instruction-boundaries --max 64
```

Pruning deletes only unpinned bytes. The metadata tombstone remains and retrieval returns `status: "gone"` with the original provenance and reproduction command.

## Trust model

- Terminal trace tails are fallback evidence, not automatically a cause.
- A semantic divergence requires a compatible reference trace, paired run, or explicit assertion boundary.
- AccuracyCoin result bytes are decoded according to its source protocol and linked to the actual test routine.
- Stale engineering-memory entries are excluded from normal search and diagnosis.
- Read-only MCP inspection does not create databases, caches, packets, or artifacts.
- Local-model ranking is optional and advisory; deterministic evidence and provenance remain authoritative.
- Local synthesis must cite immutable evidence IDs; invalid or unavailable output is discarded.
- Feedback adjusts future deterministic ranking only within a bounded weight and never overrides benchmark-required evidence.
- Reference synchronization is explicit. Diagnosis and inspection use only checksum-verified local content and never access the network.
- Trace schema v4 labels pre/post clock state, bus phase, DMA transitions, and interrupt polling; older traces are phase-incomplete.
- Experiments are unpaced, schema-validated, and deterministic. MCP rejects custom ROM paths.
- Inline experiments are canonicalized and published before execution; inspection never runs them implicitly.
- The local investigator can only issue typed read-only inspections and every retained claim cites an evidence ID.
- Conformance results are compared with a versioned accepted baseline so new, resolved, and changed failures remain visible.
- Proposed facts and fixes remain non-authoritative until explicitly accepted.
- Every free-form task packet reports an implementation/test/contract/verification evidence spine; unavailable and budget-excluded categories are explicit.
- Logical verification scopes (`ppu`, `apu`, `dma`, `bus`, `mapper`, `cartridge`, `debugger`, `winui-video`, and `winui-audio`) resolve to native MTP filters while preserving project-level scopes.
- Host usage is recorded only when supplied by the caller; missing provider token data remains `Unavailable` and is never estimated.
- Frontend diagnostics are explicit immutable bundles. NES Lab does not attach to arbitrary live processes or expose ETW/process control.
- `WinUiHostDiagnosticsBuilder` emits the portable schema without introducing a WinUI-to-NES-Lab executable dependency.

```powershell
dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj -- feedback record --packet <packet-id> --useful <evidence-id,...> --not-useful <evidence-id,...> --outcome fixed --provider openai --cloud-input-tokens 12000 --cloud-output-tokens 800 --elapsed-ms 45000 --verification passed
dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj -- feedback metrics
dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj -- memory proposals list
dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj -- memory proposals accept <id>
```

## Agent and model setup

Setup is read-only unless `--repair`/`--apply` or `--remove` is supplied. Repair publishes, registers, validates source and assembly freshness, and performs a live MCP protocol probe. Existing configuration is backed up before JSON or Codex registration changes.
Setup results report registration, published-manifest, protocol, current-session exposure, restart,
stderr, and unexpected-output diagnostics separately. Client registration changes may require a new
agent session even when the independent live protocol probe is healthy.

```powershell
dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj -- setup mcp --client codex --check
dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj -- setup mcp --client codex --repair
dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj -- setup mcp --client antigravity --apply
dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj -- setup mcp --client copilot --apply
dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj -- setup model --check
dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj -- setup model --apply
```

Antigravity uses the repository-level, agent-oriented `.agents/mcp_config.json`. Copilot requires its user-level `~/.copilot/mcp-config.json` shape. Codex registration uses `codex mcp` because Codex does not consume the shared JSON configuration.

Run `gateway benchmark` to verify the fixed CPU, PPU, DMA/APU, mapper, WinUI, conformance-harness, feature-task, and branch-review context-reduction fixtures.

`verify --changed` runs the selected focused scopes first. It escalates to the omitted scopes only
when every focused scope passes; a focused failure returns immediately with its reduced evidence.

## Production-readiness checklist

- Restore the published gateway after NES Lab source changes and confirm its source fingerprint and assembly digest.
- Run corpus v3 at 2 KiB and 16 KiB; required evidence must be complete or excerpted with no duplicate evidence IDs.
- Confirm verification reports the accepted `239 passed / 1 skipped / 3 known failures` baseline with `hasRegressions: false`.
- Check Codex, Antigravity, and Copilot registration plus initialization, three-tool discovery, and immutable-resource retrieval.
- Confirm CLI stdout contains one JSON envelope; environmental warnings must be reported through stderr diagnostics.
- Restart an already-running client session when setup reports `restartRequired: true`.
- Treat host token totals as caller-supplied telemetry. NES Lab never scrapes private client session storage or invents estimates.
- Treat richer XAML/PowerShell/assembly semantic indexing and live-process attachment as optional future work, not readiness requirements.
