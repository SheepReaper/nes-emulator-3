# NES Emulator

- Emulator core: `src/lib/emulation`; portable host API is `Nes`.
- Local diagnostics/offload CLI: `src/tools/nes-lab`; tests: `test/nes-lab`.
- Conformance harness and pinned ROM manifest: `test/conformance`.
- WinUI host: `src/emulator-winui`; portable WinUI interop: `src/lib/interop-winui`.
- Read the nearest nested `AGENTS.md` before changing a subsystem.
- Timing/conformance evidence belongs in nes-lab artifacts or its provenance-first SQLite memory, not uncited prose.
- Build/tool details: `mem:tech_stack`; completion gates: `mem:task_completion`.
