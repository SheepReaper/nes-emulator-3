# Local Agent Offload Task Ledger

Status values: `[ ]` pending, `[~]` in progress, `[x]` complete.

## Phase 1: Context and verification foundation

- [x] 1. Split persistent guidance by subsystem.
- [x] 2. Create the tested `Sheep.Nes.Lab` CLI and versioned contracts.
- [x] 3. Execute verification and reduce structured results.
- [x] 4. Add changed-file scope selection and successful-run caching.
- [x] Checkpoint 1: Execute every verification surface against the recorded emulator baseline and measure at least 90% output reduction.

## Phase 2: Cycle-trace diagnostics

- [x] 5. Define a stable bounded trace artifact.
- [x] 6. Implement trace queries.
- [x] 7. Implement semantic trace diffs.
- [x] 8. Integrate named conformance and AccuracyCoin trace runs.
- [x] Checkpoint 2: Localize a known regression to its first divergent clock.

## Phase 3: ROM and source knowledge

- [x] 9. Index ROM manifests, checksums, protocols, skips, and gaps.
- [x] 10. Index assembly sources, symbols, and result encodings.
- [x] 11. Add provenance-first engineering memory.
- [x] Checkpoint 3: Resolve a terse ROM failure locally with cited evidence.

## Phase 4: Code intelligence and context packing

- [x] 12. Add Roslyn symbol and reference indexing.
- [x] 13. Build deterministic budgeted context packets.
- [x] 14. Benchmark Serena against local retrieval.
- [x] Checkpoint 4: Validate retrieval precision, size, and latency.

## Phase 5: MCP and optional local inference

- [x] 15. Add the thin C# stdio MCP adapter.
- [x] 16. Add progressive capability discovery.
- [x] 17. Benchmark one local Ollama model on fixed repository tasks.
- [x] 18. Add optional evidence ranking and summarization.
- [x] Checkpoint 5: Verify CLI/MCP parity and model-optional operation.

## Measurements

- [x] Record baseline raw output sizes for main verification commands.
- [x] Record reduced JSON sizes and reduction ratios.
- [x] Record cache hit rate and avoided full-suite runs.
- [x] Record average trace records before and after reduction.
- [x] Record cloud context packet sizes by subsystem.

## Phase 7: Trustworthy autonomous diagnosis

- [x] 26. Extract shared trace and ROM contracts from the Lab executable dependency graph.
- [x] 27. Add checkpoint-aware trace schema v3 and suite-aware AccuracyCoin source/result resolution.
- [x] 28. Prefer structured test reports and record complete repository/tool/schema provenance.
- [x] 29. Balance context categories and add read-only-safe persistent Roslyn indexing.
- [x] 30. Add stale/superseded engineering-memory lifecycle and optional downstream local ranking.
- [x] 31. Publish content-addressed MCP resources with verified retrieval, retention, and tombstones.
- [x] Checkpoint 7: Validate live AccuracyCoin and ordinary manifest diagnoses plus all repository gates.

## Phase 6: Production-ready gateway

- [x] 19. Make ROM protocols canonical and verification cache inputs fixture/toolchain aware.
- [x] 20. Align trace diffs by CPU clock and reject incompatible capture windows.
- [x] 21. Constrain MCP paths, split inspect/run tools, parse envelopes, and kill cancelled process trees.
- [x] 22. Index runs, failures, artifacts, cache usage, and reduction metrics.
- [x] 23. Capture bounded terminal traces for ordinary manifest and AccuracyCoin cases.
- [x] 24. Add filtered Roslyn queries and actual declaration/caller/test source excerpts.
- [x] 25. Add deterministic composite diagnosis by named case or stable run ID.
- [x] Checkpoint 6: Validate both known AccuracyCoin diagnoses and all repository gates.

## Phase 8: Context-safe general development gateway

- [x] 32. Bound MCP artifact and tool responses and add immutable trace/text projections.
- [x] 33. Add stable-ID and filtered context selection with structured ambiguity results.
- [x] 34. Align discovery with allowlisted mapping and bound ROM list output.
- [x] 35. Add changed-file, merge-base, subsystem, task, and run context modes.
- [x] 36. Add common MCP registration with required Codex, Antigravity, and Copilot adapters plus Ollama readiness.
- [x] 37. Add the fixed eight-workflow gateway benchmark.
- [x] Checkpoint 8: Validate live setup/context/MCP behavior and all repository gates.

## Latest verification

- 2026-08-23: First fresh-session usability feedback exposed non-discoverable nested trace help. `trace capture --help` and `help trace capture` now show the exact `--case` syntax, the 2,048-record bound, provenance behavior, and a copyable AccuracyCoin example; query and diff help enumerate every parser-supported option. Lab passed 249/249 and the schema-v4 gateway was force-republished after safely terminating only this checkout's locked MCP processes.

- 2026-08-23: Phase 13 completed. Verification now separates strict execution, accepted-baseline, regression, resolved-case, and infrastructure semantics; the opt-in baseline-aware policy returned exit code 0 for the unchanged 239 passed, 1 skipped, 3 known-failure baseline while retaining `success: false`. Context evidence is deduplicated before budgeting with explicit input/unique/duplicate counts, and corpus v3 remained 26/26 at 2 KiB and 16 KiB. Lab passed 246/246, CPU 716/716, WinUI 29/29, all consumer builds passed, and Codex, Antigravity, and Copilot reported healthy schema-v4 published gateways with clean structured diagnostics.

- 2026-08-23: Phase 12 completed. Corpus v3 passed 26/26 at 2 KiB and 16 KiB across CPU, PPU, DMA/APU, conformance, MMC1/MMC3, cartridge/iNES, WinUI video/audio, debugger/public API, and build/namespace workflows. A free-form 2 KiB audit packet retained complete or excerpted implementation, test, contract, and verification spine evidence. Lab passed 235/235, CPU 716/716, WinUI 29/29, all consumer builds passed, and conformance matched the accepted 239 passed, 1 skipped, 3 classified-failure baseline. Codex, Antigravity, and Copilot live MCP health probes all passed after publication.

- 2026-08-23: Phase 11 and Checkpoint 11 completed. Corpus v2 passed 12/12 at both 2 KiB and 16 KiB with complete required-evidence accounting. Lab passed 207/207, CPU 716/716, WinUI 28/28, all consumer builds passed, and conformance matched the versioned accepted baseline at 239 passed, 1 skipped, and 3 classified AccuracyCoin failures. All 28 pinned references were available with matching digests. The republished Codex, Antigravity, and Copilot MCP gateways passed registration, manifest/fingerprint, initialization, three-tool discovery, immutable-resource-template, compact-discovery, and process-cleanup health checks.

- 2026-08-22: Phase 8 and Checkpoint 8 completed. MCP resources inline only small content and project large artifacts into verified metadata, bounded previews, line windows, or trace queries. Stable-ID/filtered, changed-file/merge-base, subsystem, task, and run context modes are live. Discovery parity and bounded ROM listing are guarded by tests. Codex, Antigravity, and Copilot registrations all resolve to the published silent gateway under `.artifacts/nes-lab/gateway`; the customized Ollama model validates with a 16,384-token runtime context. The eight-workflow gateway benchmark passed 8/8 at 99.4–99.5% reduction. Lab passed 159/159, CPU 713/713, WinUI 28/28, all consumer builds passed, and conformance retained the accepted 240 passed, 1 skipped, 2 known AccuracyCoin failure baseline.

- 2026-08-22: Phase 7 and Checkpoint 7 completed. The dependency-light Contracts project is guarded against package/project dependency regressions, conformance no longer references the Lab executable, and mutable engineering-memory additions/supersessions publish pinned immutable snapshots. Live ordinary-manifest and both known AccuracyCoin diagnoses select relevant schema-v3 checkpoints and suite-resolved routines without unsupported divergence claims. Lab passed 136/136, CPU 713/713, WinUI 28/28, all consumer builds passed, and conformance retained the accepted 240 passed, 1 skipped, 2 known AccuracyCoin failure baseline. `git diff --check` passed.

- 2026-08-22: Phase 6 and Checkpoint 6 completed. Manifest-driven `4015_cleared` produced an indexed generic trace. Both known AccuracyCoin failures produced deterministic 16 KB diagnosis packets containing reduced failure, bounded trace with real dropped-record accounting, AccuracyCoin assembly, reproduction command, `CpuBus`/`CpuClockDriver` bodies, and affected test source. Lab passed 118/118, CPU 713/713, WinUI 28/28, and all consumer builds passed. Full conformance retained the accepted 240 passed, 1 skipped, 2 known AccuracyCoin failure baseline.
- 2026-08-21: Phase 5 and Checkpoint 5 completed. The official C# MCP SDK subprocess exposes only `nes_lab_discover` and `nes_lab_execute`; its integration test proves progressive context discovery and CLI-equivalent execution. A repository Modelfile created `nes-lab:devstral-24b` with a 16,384-token context; Ollama reported 16 GB and 100% GPU on the RTX 4090. The fixed classification/ranking/deduplication/summarization benchmark passed 4/4 in 5.64 s. Optional ranking is deterministic and model-free by default, validates selected IDs, preserves provenance, records model/prompt/latency/change metadata, and falls back safely when Ollama is unavailable. Lab passes 105/105.
- 2026-08-21: Phase 4 and Checkpoint 4 completed. The real solution Roslyn index resolved `CpuClockDriver` and its exact caller across separate CLI processes. In a five-iteration persistent benchmark, Roslyn averaged 0.24 ms for lookup and 1.09 ms for references with a 545-byte payload; Serena averaged 567.86 ms and 720.36 ms with 741 bytes. Serena initialization (2.81 s), mandatory instructions (2.05 s), and warmup (3.82 s) were recorded separately and excluded. A 4,096-byte context request produced a cited 4,049-byte packet with explicit omission accounting. Lab passes 90/90.
- 2026-08-21: Phase 3 and Checkpoint 3 completed. `rom diagnose --suite instr_timing --name 1-instr_timing --code 3` resolved the terse result to “NOPs and alternate SBC timing is wrong” at upstream `source/1-instr_timing.s:42`, including source/manifest hashes, ROM checksum, `$6000` protocol, and pinned commit. The mapping was persisted and retrieved as a `ConfirmedFact` with mandatory provenance in the SQLite FTS5 knowledge store. Lab passes 81/81.
- 2026-08-21: Phase 3 Task 9 completed: `rom list`/`rom show` index all pinned manifest cases with upstream/manifest provenance, expected and installed checksums, region, timeout, optional skip/gap fields, and ordered terminal protocols matching the conformance harness. Lab passes 69/69; live `dmc_tests/latency` lookup verified its installed ROM digest and `$E162` success-PC protocol.
- 2026-08-21: Checkpoint 2 passed with a deterministic NROM micro-fixture. A temporary `$EA` NOP regression from 2 to 3 cycles produced two 100-clock traces; semantic diff localized the first divergence to trace index/CPU clock 7 at opcode `$EA`, PC `$8001`, with only `cpu.cyclesRemaining` differing (1 expected, 2 actual). The response retained a 7-clock context window instead of loading 200 input records. The deliberate mutation was restored immediately after capture; Lab passed 62/62 and CPU passed 713/713 afterward.
- 2026-08-21: Named AccuracyCoin failure tracing passed end-to-end with `Implicit DMA Abort`: the expected failing test produced a versioned 2,048-clock artifact, returned its path in compact JSON, and retained the distinguishing `$0478=$0A` result write at the capture boundary. Conformance and Lab projects build; Lab tests pass 60/60.
- 2026-08-21: Checkpoint 1 passed as a tooling gate: every surface executed and matched the recorded baseline; Lab passed 56/56. Detailed local evidence totaled 298,436 bytes and the agent-facing JSON was 3,575 bytes (98.8% reduction). An identical follow-up reused 6 successful scopes from cache and reran only failing conformance; failures are intentionally never cached.
- 2026-08-21: Checkpoint 1 attempted through `nes-lab verify --scope all --continue-on-failure`: Lab 55/55, CPU 713/713, WinUI 28/28, and all three builds passed. Conformance remained at 240 passed, 1 skipped, and 2 failed (`Delta Modulation Channel`, `Implicit DMA Abort`). Raw output was 7,163 bytes and the JSON response was 4,764 bytes (33.5% reduction, 0 cache hits); both checkpoint gates therefore remain open.
- 2026-08-21: Lab tests passed 55/55 after adding verification reduction/cache, bounded trace artifacts, CLI trace queries/diffs, and named conformance selection.
- 2026-08-21: Lab tests passed 8/8; CPU passed 713/713; WinUI passed 28/28; library, WinUI interop, and WinUI app builds succeeded.
- 2026-08-21: Full conformance ran 243 cases with 240 passed, 1 skipped, and 2 existing focused AccuracyCoin failures (`Delta Modulation Channel` and `Implicit DMA Abort`). Checkpoint 1 remains open.
# Phase 9

- [x] Replace synthetic gateway benchmark with versioned repository-task corpus and deterministic relevance/byte/latency metrics.
- [x] Add hybrid terminology/profile/Roslyn/text/test/memory retrieval with score explanations and source diversity.
- [x] Replace changed-file prefixes with parsed Git hunk evidence.
- [x] Add gateway manifests, typed health, `--repair`, live MCP probes, and operation-specific budgets.
- [x] Add immutable packet/evidence IDs, feedback, bounded ranking influence, and explicit memory proposal approval.
- [x] Add dependency-scope explanations and conservative fallback reporting.
- [x] Complete all repository verification gates and live installed-client probes. Satisfied by the Phase 11–13 checkpoints and the latest 246 Lab / 716 CPU / 239 passed, 1 skipped, 3 accepted-failure conformance verification record.
# Phase 10 checkpoint ledger

- [x] Corpus-v2 packet recall/precision/MRR gate and FTS5 retrieval index
- [x] Scoped root/nested guidance within the guidance reservation
- [x] Pinned offline authoritative reference corpus and immutable resources
- [x] Trace-v4 phase, DMA, and interrupt sampling evidence
- [x] Cited DMA/controller and DMC/OAM observation rules
- [x] Deterministic headless scenario runner and immutable comparison
- [x] Warm MCP read-only inspection session
- [x] Explicit outcome telemetry with unavailable-host semantics
- [x] Accepted conformance baseline and all consumer gates reverified at handoff

# Phase 11 checkpoint ledger

- [x] Guarantee required evidence and pass corpus v2 12/12 at 2 KiB and 16 KiB.
- [x] Add explicit subsystem classification, negative routing weights, and packet density accounting.
- [x] Add semantic frame/audio analysis and immutable heatmaps.
- [x] Add bounded canonical inline experiments with CLI/MCP parity.
- [x] Add a bounded read-only local investigator with citation validation and deterministic fallback.
- [x] Add immutable session handoff and handoff-driven context reconstruction.
- [x] Expand and synchronize the authoritative APU, controller, PPU, ROM-format, mapper, and test-ROM references.
- [x] Diagnose the third AccuracyCoin failure and add versioned conformance-baseline classification.
- [x] Checkpoint 11: Complete all repository verification gates, republish the MCP gateway, and run live health probes.

# Phase 12 checkpoint ledger

- [x] Add a deterministic unfamiliar-task evidence spine and exact tight-budget accounting.
- [x] Add explainable logical verification scopes using native MTP filters.
- [x] Add structured build diagnostics and semantic history CLI aliases.
- [x] Expand corpus v3 across mapper, cartridge, debugger, WinUI, and build workflows.
- [x] Add host-supplied telemetry to immutable session handoffs.
- [x] Add portable immutable host-diagnostic bundles with typed MCP operations.
- [x] Add an agent-agnostic stdin/stdout telemetry normalizer and WinUI diagnostic-bundle builder.
- [x] Checkpoint 12: Run all repository gates, republish the MCP gateway, and verify every installed client.

# Phase 13 checkpoint ledger

- [x] Add baseline-aware verification fields and opt-in exit policy while preserving strict defaults.
- [x] Persist semantic verification status in run history, diagnoses, and session handoffs.
- [x] Deduplicate evidence before allocation and add unique/duplicate packet accounting.
- [x] Add structured gateway registration/publish/protocol/session/output diagnostics.
- [x] Document production readiness and intentional future boundaries.
- [x] Checkpoint 13: Run all repository gates, corpus v3, restore the published gateway, and verify installed clients.
