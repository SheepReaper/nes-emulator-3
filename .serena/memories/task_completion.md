# Task Completion

Run applicable focused tests first, then the root `AGENTS.md` verification surfaces: Lab, CPU, conformance, WinUI tests; emulation, WinUI interop, and WinUI app builds; finally `git diff --check`. Existing conformance baseline failures are not nes-lab failures, but new regressions must be identified against the recorded baseline.
