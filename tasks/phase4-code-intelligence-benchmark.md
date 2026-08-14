# Phase 4 Code-Intelligence Benchmark

Date: 2026-08-21

## Workload

Representative target: `Sheep.Emulation.Nes.Cpu.CpuClockDriver`.

- Resolve the unique declaration.
- Resolve its invocation from `Cpu.Clock(ulong)`.
- Run five warmed iterations in one initialized session.
- Measure UTF-8 response bytes.
- Record initialization, mandatory instructions, and warmup independently of steady-state queries.

## Results

| Engine | Initialization | Mandatory instructions | Warmup | Mean symbol lookup | Mean references | Payload | Precision |
|---|---:|---:|---:|---:|---:|---:|---|
| nes-lab Roslyn 5.9 | 6,304 ms | n/a | one unscored reference query | 0.24 ms | 1.09 ms | 545 B | unique target and exact caller |
| Serena 1.7.1.dev0 (`7fcbca7e`) | 2,814 ms | 2,048 ms | 3,821 ms | 567.86 ms | 720.36 ms | 741 B | target and caller both present |

Serena setup, instruction reading, activation/index warmup, and onboarding are explicitly excluded from its steady-state figures. The persistent Roslyn benchmark likewise reports workspace initialization separately.

## Serena capability/configuration audit

- Serena reported 52 installed tools. Its Codex default exposed 23.
- The repository profile exposes 12 read-only tools: symbol search, declaration, references, implementations, symbol/file diagnostics, symbol overview, pattern fallback, activation/configuration, initial instructions, and on-demand Serena information.
- C# is the only enabled language server. The auto-detected PowerShell server was intentionally omitted.
- Project indexing cached 394 C# documents. Serena's health check completed successfully; its final checkmark initially hit a Windows CP1252 display error, and the same check passed under UTF-8.
- Serena onboarding was completed according to its own instructions, including reading `memory_maintenance`, writing the five required memories, and passing `serena memories check`.

The audit used Serena's [official tool catalog](https://oraios.github.io/serena/01-about/035_tools.html), [configuration model](https://oraios.github.io/serena/02-usage/050_configuration.html), and [project workflow](https://oraios.github.io/serena/02-usage/040_workflow.html). The version and commit are pinned in the benchmark artifact at `.artifacts/nes-lab/phase4-serena-benchmark.json`.

## Decision

Serena is accurate for this workload but does not improve warmed latency or payload size over the in-process Lab/Roslyn index. It is therefore not selected as nes-lab's primary retrieval engine or added as a runtime dependency. The narrow Serena profile remains as a reproducible, opt-in comparison and a useful external semantic fallback.

## Context-packet checkpoint

`context build --symbol CpuClockDriver --budget 4096` produced a deterministic 4,049-byte packet. It cited root and emulation guidance plus the exact declaration, omitted one lower-priority reference to honor the ceiling, and reported the omission. Lab tests passed 90/90.
