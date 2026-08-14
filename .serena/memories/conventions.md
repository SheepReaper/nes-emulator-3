# Conventions

- Preserve caller-driven deterministic emulation; host pacing must not alter simulated timing.
- Keep implementation devices internal and expose portable contracts through `Nes`.
- Ground timing fixes in primary documentation and focused tests; compare first divergent clock rather than terminal ROM output alone.
- Preserve unrelated dirty-worktree changes and remove temporary tracing/mutations.
- Add focused tests for changed behavior; use `apply_patch` for source edits.
