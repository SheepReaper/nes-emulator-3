# Local Agent Offload Implementation Plan

## Objective

Reduce cloud-model context and repeated reasoning by converting large local evidence—test output, cycle traces, ROM sources, code relationships, and prior diagnoses—into small, typed, provenance-preserving results before an agent sees them.

The reusable implementation is a local `.NET` CLI/library named `Sheep.Nes.Lab`. Humans and CI invoke the CLI directly. A later stdio MCP server exposes the same application services without duplicating their implementation. Local language models may rank or summarize evidence, but deterministic code and cited hardware/test sources remain authoritative.

## Success metrics

- Record raw bytes produced locally and reduced bytes returned by each Lab command.
- Reduce routine verification output delivered to an agent by at least 90%.
- Return no more than 2,048 CPU clocks by default and normally no more than 64 clocks around a detected boundary.
- Cache successful verification by command, repository state, and relevant inputs.
- Identify the first divergent trace clock and its nearest instruction/source boundary without model assistance.
- Generate task context packets within an explicit byte/token budget.
- Preserve a source path, source hash, test/ROM checksum, or run artifact for every diagnostic claim.

## Architecture decisions

- Build a CLI/library first; MCP is an adapter, not the implementation.
- Store full artifacts under `.artifacts/nes-lab/`, excluded from source control.
- Return versioned JSON contracts from every automation-facing command.
- Use Microsoft Testing Platform structured reports where practical; retain console logs only as artifacts.
- Use Roslyn for C# symbol relationships, `rg` for text/assembly, and SQLite FTS5 for durable indexed knowledge.
- Keep hypotheses separate from confirmed facts, rejected hypotheses, design decisions, and known gaps.
- Keep local-model use optional and downstream of deterministic evidence collection.
- Expose typed MCP tools rather than arbitrary shell or filesystem access.

## Dependency graph

```text
Scoped repository guidance
        |
Lab command host + versioned JSON contracts
        |
        +-- verification runner + artifact store + cache
        +-- trace serialization/query/diff
        +-- ROM/source/protocol index
        +-- Roslyn symbol index
        +-- engineering knowledge store
                    |
              context packet builder
                    |
              stdio MCP adapter
                    |
          optional Ollama ranking/summaries
```

## Phase 1: Context and verification foundation

### Task 1: Scope persistent guidance

Move subsystem-specific rules from the root `AGENTS.md` into the nearest emulation, conformance, WinUI, and interop directories.

Acceptance criteria:

- Root guidance contains only repository-wide rules and verification entry points.
- Timing, ROM-harness, and AudioFrame rules remain available under their relevant directories.
- No rule is silently discarded.

Verification:

- Inspect the effective guidance hierarchy for each subsystem.
- `git diff --check` passes.

### Task 2: Establish the Lab CLI and contracts

Create a dependency-light `Sheep.Nes.Lab` command-line project and a small test project. Implement argument parsing, scope selection, versioned JSON output, exit codes, and artifact-directory selection.

Acceptance criteria:

- `nes-lab verify --scope cpu --format json` resolves to the documented CPU test command.
- Invalid commands and scopes return structured errors and nonzero exit codes.
- Argument and command planning logic has focused unit tests.

### Task 3: Run and reduce verification

Execute selected verification surfaces, write full stdout/stderr locally, and return a compact result containing status, duration, failed tests, skipped counts, artifact paths, and raw/reduced byte counts.

Acceptance criteria:

- Full logs never need to be returned in JSON.
- Multiple scopes run in deterministic order and stop/continue according to an explicit option.
- A failed test is represented independently from an infrastructure/build failure.

### Task 4: Add changed-file selection and cache

Map changed paths to verification scopes and cache successful runs by repository/input fingerprint.

Acceptance criteria:

- `verify --changed` selects focused scopes conservatively.
- Shared-clock or bus changes include CPU and conformance.
- Cache hits are reported and never hide changed inputs.

### Checkpoint 1

- Every existing verification surface executes successfully as tooling, and its outcome matches the recorded emulator baseline; known emulator test failures do not block Lab development.
- Lab tests pass.
- A sample verification returns at least 90% fewer bytes than its raw log.

## Phase 2: Cycle-trace diagnostics

### Task 5: Define a stable trace artifact

Serialize bounded `NesCpuClockTrace` records with schema version, ROM hash, run metadata, and source commit.

### Task 6: Query trace boundaries

Support tail, address/range, bus-owner, interrupt-edge, DMA-overlap, and instruction-boundary queries.

### Task 7: Diff traces

Find the first semantic divergence and emit a compact before/divergence/after window.

### Task 8: Integrate focused conformance runs

Run a named ROM/AccuracyCoin case with opt-in trace capture and attach the smallest relevant trace result on failure.

### Checkpoint 2

- A known timing regression is localized to its first divergent CPU clock.
- Default trace responses remain bounded while full traces remain available by artifact path.

## Phase 3: ROM and source knowledge

### Task 9: Index ROM manifests and protocols

Index suite/case identity, checksum, terminal protocol, maximum clocks, skips, and known gaps.

### Task 10: Index assembly sources and symbols

Associate ROM cases and result bytes with exact source locations and symbol definitions.

### Task 11: Add provenance-first engineering memory

Store confirmed facts, observations, hypotheses, rejected hypotheses, fixes, regression tests, sources, and commits as distinct record types.

### Checkpoint 3

- A terse ROM failure can be resolved to protocol meaning and source lines without loading the whole source tree.
- Every returned rule or diagnosis includes provenance.

## Phase 4: Code intelligence and context packing

### Task 12: Add Roslyn symbol indexing

Provide declarations, references, callers, project ownership, and affected-test queries for C# symbols.

### Task 13: Build deterministic context packets

Combine scoped guidance, changed symbols, focused tests, reduced failures, trace windows, ROM excerpts, and applicable rules under a requested budget.

### Task 14: Evaluate Serena

Benchmark Serena against the Lab/Roslyn index on representative repository tasks. Retain it only if measured context and latency improve.

### Checkpoint 4

- Context packets cite real files and fit their requested budget.
- Retrieval benchmarks record precision, payload bytes, and latency.

## Phase 5: MCP and optional local inference

### Task 15: Add a thin stdio MCP server

Expose typed verification, trace, ROM, symbol, knowledge, and context-packet tools/resources using the official C# MCP SDK.

### Task 16: Add capability discovery without schema flooding

Keep the default tool surface small and use resources/prompts for progressive disclosure.

### Task 17: Benchmark an Ollama model

Install one small tool-capable local model and evaluate classification, ranking, deduplication, and evidence summarization against a fixed repository benchmark.

### Task 18: Add optional local ranking

Allow local ranking only after deterministic retrieval. Record model, prompt version, latency, and whether its output changed the selected evidence.

### Checkpoint 5

- CLI, CI, MCP, and local-model clients share the same application services.
- Disabling Ollama leaves every deterministic command functional.

## Risks and mitigations

| Risk | Impact | Mitigation |
|---|---:|---|
| Tool output becomes another large context source | High | Version small response contracts; put complete data in artifact files. |
| Cached results become stale | High | Hash command, project inputs, Git state, ROMs, and tool schema version. |
| Historical hypotheses are treated as facts | High | Separate record kinds and require provenance/status. |
| MCP tool schemas consume more context than they save | Medium | Small typed surface and progressive resources; measure schema bytes. |
| Local model introduces confident but incorrect hardware claims | High | Restrict it to ranking/summarization of deterministic evidence. |
| Trace format couples to internal implementation | Medium | Version the schema and map internal records into public artifact DTOs. |
| Full conformance runs remain expensive | Medium | Focused selection, successful-run cache, and explicit escalation policy. |

## Definition of complete

- All tasks and checkpoints in `tasks/local-agent-offload-todo.md` are complete.
- Main repository verification passes.
- The context-reduction benchmark demonstrates measured savings on representative CPU, DMA, PPU/mapper, APU, and WinUI tasks.
- CLI and MCP documentation include examples, schemas, artifact retention, and trust/provenance behavior.

## Phase 6: Production-ready gateway

- Canonical manifest-owned ROM protocols and fixture-aware verification cache fingerprints.
- Provenance-compatible, CPU-clock-aligned trace comparison and generic conformance tracing.
- Repository-confined structured MCP inspection/run boundaries with process-tree cancellation.
- Indexed run/failure history and reduction metrics under `.artifacts/nes-lab/index.sqlite`.
- Disambiguated Roslyn queries and source-body context packets.
- One-command deterministic diagnosis for a named case or indexed run.

## Phase 7: Trustworthy autonomous diagnosis

- Keep conformance dependent only on the dependency-light Lab Contracts assembly.
- Capture trace schema v3 with named entry, hardware, assertion/result, and terminal windows; never label an unreferenced checkpoint a divergence.
- Resolve AccuracyCoin menu entries to their real routines and decode encoded result values.
- Prefer structured MTP reports, record complete Git/tool/schema provenance, and preserve cache entries across unrelated commits.
- Balance context budgets across failure/trace, implementation, tests, guidance, and supporting evidence.
- Validate and supersede engineering-memory revisions; exclude stale facts by default.
- Publish logs, traces, contexts, runs, and memory snapshots through content-addressed immutable MCP resources.
- Retain pinned evidence indefinitely and replace expired unpinned bytes with reproducible tombstones.

## Phase 8: Context-safe general development gateway

- Prevent large immutable artifacts from entering MCP context; expose verified metadata, bounded previews, line windows, and trace queries instead.
- Resolve context by stable symbol ID or disambiguation filters and keep discovery mechanically aligned with mapping.
- Bound ROM catalog output and report total/returned/truncated counts.
- Build deterministic context packets from changed files, merge-base changes, subsystems, natural-language tasks, and passing or failing runs.
- Register NES Lab through a common MCP definition with the smallest necessary Codex, Antigravity, and Copilot adapters.
- Validate or build the optional Ollama model without making it a deterministic-gateway dependency.
- Benchmark eight representative emulator workflows under explicit response, reduction, and evidence-retention gates.

## Phase 11: Complete investigation gateway

- Guarantee fixture-required evidence before secondary context and report completeness and density under tight budgets.
- Classify natural-language tasks by subsystem with explicit score reasons and negative weighting for unrelated evidence.
- Analyze immutable RGBA frames and Float32 audio as compact pixel, scanline, region, level, health, and spectral evidence.
- Accept bounded inline experiments, canonicalize them as immutable scenario artifacts, and preserve exact reproduction commands.
- Let the local Ollama model conduct a bounded read-only, citation-validated investigation over typed Lab inspections.
- Publish immutable session handoffs containing repository state, verification, memory, Git hunks, artifacts, and the next command.
- Pin complete certified-subsystem references and classify conformance changes against a versioned accepted baseline.

## Phase 12: Repository-wide certification and adoption

- Certify retrieval across CPU, PPU, DMA/APU, conformance, MMC1/MMC3, cartridge formats, debugger/public APIs, WinUI video/audio, and build/namespace workflows through corpus v3.
- Give unfamiliar task packets a deterministic implementation, focused-test, contract/reference, and smallest-verification evidence spine with explicit unavailable and budget-excluded states.
- Add logical verification scopes backed by native MTP filters and require a reason for every selected changed-file scope.
- Parse build failures into structured compiler/MSBuild diagnostics and identify the earliest actionable error without hiding complete logs.
- Normalize semantic history aliases across CLI and MCP while retaining legacy commands.
- Accept explicit host-reported usage telemetry without estimating unavailable provider data.
- Diagnose frontend behavior through portable immutable host bundles instead of arbitrary live-process attachment.

## Phase 13: Operational contract hardening

- Separate strict execution success from accepted-baseline, regression, improvement, and infrastructure semantics; retain strict exits by default and offer an explicit baseline-aware policy.
- Deduplicate immutable evidence before packet allocation and reject evidence-ID integrity collisions.
- Report registration, publish, protocol, session exposure, restart, stderr, and unexpected-output setup health independently.
- Certify machine-clean JSON output and document the intentional boundaries around host telemetry, non-C# indexing, and live-process attachment.
