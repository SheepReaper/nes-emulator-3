# Phase 10 — Reference-grounded NES Lab diagnosis

Phase 10 makes repository retrieval an honest gate, grounds timing explanations in a pinned offline reference corpus, adds trace-v4 phase evidence, and introduces deterministic headless experiments. It intentionally does not change emulator fidelity or claim a semantic divergence without a paired reference/assertion boundary.

## Delivered checkpoints

- Checkpoint A: corpus v2 uses structured evidence identities and gates candidate/packet recall, precision, MRR, distractors, profile coverage, payload size, and latency at 2 KiB and 16 KiB.
- Checkpoint B: explicit reference sync/status/search/show verifies NESdev, AccuracyCoin, mapper, and Microsoft source digests; diagnosis can attach cited v4 hardware observations.
- Checkpoint C: schema-v1 NTSC experiments support deterministic emulated stops, controller schedules, bounded captures, immutable results, and capture-aligned comparison.
- Checkpoint D: MCP read-only code/context/reference/experiment comparison uses a warmed session workspace; feedback stores only explicitly supplied outcome telemetry.

## Trust boundaries

- Curated claims navigate to upstream material; the verified cached source is the proof.
- Network access occurs only through `references sync`.
- Hardware evaluators report cited observations, never inferred causality or an unsupported first divergence.
- Trusted CLI may use custom ROM paths. MCP experiments may use only manifest cases or immutable artifacts.
- Unknown cloud/host telemetry is reported as `Unavailable`; NES Lab does not estimate it.
