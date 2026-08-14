# Phase 5 MCP and Local Inference Report

## Result

`Sheep.Nes.Lab` now exposes the existing deterministic application services over stdio MCP and can optionally use a locally hosted Ollama model to rank and summarize already-retrieved evidence. Ollama is never required by verification, trace, ROM, code, memory, context-packet, CLI, CI, or MCP operations.

## MCP surface

The server uses the official C# MCP SDK (`ModelContextProtocol` 2.2.0) and starts with:

```powershell
dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj --no-restore -- mcp
```

It deliberately advertises only two tools:

- `nes_lab_discover`: lists compact capability groups or describes one group's operation and argument contracts.
- `nes_lab_execute`: dispatches one discovered operation through a strict allowlist to the same CLI application services.

The six progressively disclosed groups are `code`, `context`, `memory`, `rom`, `trace`, and `verify`. There is no arbitrary command, shell, or filesystem tool. An SDK-level integration test starts the real subprocess, lists exactly these two tools, discovers the context contract, executes a 2,048-byte context request, and verifies the CLI-equivalent result.

References: [official C# MCP SDK getting started](https://github.com/modelcontextprotocol/csharp-sdk/blob/main/docs/concepts/getting-started.md), [transport guidance](https://github.com/modelcontextprotocol/csharp-sdk/blob/main/docs/concepts/transports/transports.md), and [ModelContextProtocol 2.2.0](https://www.nuget.org/packages/ModelContextProtocol/2.2.0).

## Local model profile

Hardware observed before loading the model was 24,564 MiB total VRAM, 4,772 MiB in use by the interactive Windows session, and 19,367 MiB free. The repository-owned [`Modelfile`](../src/tools/nes-lab/ollama/Modelfile) was created before downloading the base model and defines the reproducible local alias `nes-lab:devstral-24b` from `devstral-small-2:24b`.

The profile uses a 16,384-token context, a 512-token output cap, temperature zero, a fixed seed, and a system contract restricted to evidence ranking/summarization with provenance-preserving JSON. It is installed with:

```powershell
ollama create nes-lab:devstral-24b -f src/tools/nes-lab/ollama/Modelfile
```

This selected the 15 GB/24B Devstral variant rather than the 19 GB Qwen3 Coder variant so model weights and KV cache fit beneath the approximately 19.8 GB budget left after reserving 4.7 GB for the desktop. During the final benchmark, `ollama ps` reported a 16 GB allocation, `100% GPU`, and context 16,384. `nvidia-smi` reported 21,583 MiB total GPU use and 2,556 MiB free, with no CPU/shared-memory model split.

References: [Ollama Modelfile](https://docs.ollama.com/modelfile), [context length and VRAM](https://docs.ollama.com/context-length), [chat API](https://docs.ollama.com/api/chat), and [structured outputs](https://docs.ollama.com/capabilities/structured-outputs).

## Fixed benchmark

The checked-in [`benchmark.json`](../src/tools/nes-lab/ollama/benchmark.json) covers classification, ranking, deduplication, and provenance-preserving summarization. Run it with:

```powershell
dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj --no-restore -- model benchmark
```

Final warmed result on 2026-08-21: 4/4 cases passed in 5,637.08 ms. Per-case latency was 1,325.00 ms classification, 1,586.07 ms ranking, 822.72 ms deduplication, and 1,902.68 ms summarization. An earlier fixed-fixture pass exposed ambiguity between a generic run observation and actual failure-localizing trace evidence; prompt version `evidence-ranker-v2` now makes that distinction explicitly without changing expected benchmark answers.

## Optional-operation guardrails

`model rank` reads a JSON array of `EvidenceCandidate` values. It is offline and deterministic by default:

```powershell
dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj --no-restore -- model rank --input evidence.json --max 8
```

Only an explicit `--local true` enables Ollama. Model output can select only supplied IDs; unknown IDs and duplicates are discarded, result count is bounded, and original evidence text and source fields are returned rather than model-rewritten claims. Empty, invalid, unavailable, or failed model results fall back to priority/ID deterministic ordering. The response records whether the model was used, model name, prompt version, latency, error, and whether selection changed.

## Verification

- Lab suite: 105/105 passed.
- MCP subprocess integration confirms progressive discovery and CLI parity.
- Model-disabled ranking is covered by a test that proves no model call occurs.
- Offline transport failure is covered and returns deterministic evidence.
- Live fixed benchmark: 4/4 passed.

