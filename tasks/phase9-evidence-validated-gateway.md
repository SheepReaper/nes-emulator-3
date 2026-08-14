# Phase 9: Evidence-Validated NES Lab Gateway

Status: implemented, verification in progress (2026-08-22).

NES Lab now measures retrieval against the versioned `gateway-corpus.v1.json` repository-task corpus. Hybrid retrieval combines Roslyn declarations, bounded source/test/assembly text, subsystem profiles, engineering memory, and capped explicit feedback. Every candidate reports its score contributions. The deterministic benchmark records recall, precision, reciprocal rank, omissions, false positives, actual packet/CLI/MCP bytes, cache use, and cold/warm latency at 2 KiB and 16 KiB budgets. `--agent local` is a separate non-gating citation evaluation.

Changed-context collection uses real committed/staged/unstaged/untracked Git hunks with rename/deletion metadata and content hashes. Gateway publishing writes a source/assembly manifest; check/repair distinguish missing, stale, registration mismatch, startup, and protocol failures and perform a live MCP handshake. MCP response ceilings are 32 KiB discovery, 64 KiB ordinary inspection/resources, and 128 KiB context/diagnosis.

Packets and evidence carry stable immutable IDs. Feedback is append-only and changes deterministic ranking by at most ±10. Memory candidates remain proposals until an explicit accept operation writes authoritative engineering memory. Optional local synthesis is citation-constrained and falls back without changing deterministic evidence.

Remaining verification: run the accepted emulator/conformance/WinUI gates and live registration probes; record any environmental failures in the task ledger rather than weakening the corpus.
