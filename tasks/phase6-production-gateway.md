# Phase 6 Production Gateway

## Commands

```powershell
# Run and prepare one deterministic failure packet.
dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj --no-restore -- diagnose --case "Implicit DMA Abort" --budget 16000

# Rebuild from local evidence without rerunning emulation.
dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj --no-restore -- diagnose --run latest --budget 16000

dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj --no-restore -- runs latest
dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj --no-restore -- failures search --query "DMA"
dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj --no-restore -- metrics
```

## Trust boundary

The stdio server advertises `nes_lab_discover`, read-only `nes_lab_inspect`, and state-changing but non-destructive `nes_lab_run`. MCP paths are confined to the repository and `.artifacts/nes-lab`; custom manifests, ROM roots, and knowledge databases remain trusted-CLI-only options. Cancellation kills the CLI process tree. CLI envelopes are parsed so argument/infrastructure failures become MCP errors while expected test failures remain typed results.

## Evidence and storage

- Verification cache v2 records command, environment, configuration, runtime/architecture, SDK information, repository revision, relevant source hashes, manifest hash, and selected installed ROM checksums. Missing or mismatched conformance assets are not cacheable.
- Trace schema v2 records capture/boundary identity and actual dropped clocks. Diffing validates provenance and aligns overlapping records by CPU clock.
- `.artifacts/nes-lab/index.sqlite` indexes run IDs and metadata; logs, traces, and context packets remain separate immutable artifacts.
- Diagnosis packets prioritize reduced failure, bounded trace, canonical ROM protocol/source, reproduction command, relevant declarations/tests, memory, and affected Git diffs under the requested byte ceiling.

Ollama remains optional and downstream of deterministic packet construction.

## Final checkpoint

- `Implicit DMA Abort`: packet `1e782be88f665f6ab8b1d755` included failure, trace, reproduction, ROM assembly, emulator declarations, and affected tests.
- `Delta Modulation Channel`: packet `3b49091d395be60bf7b67ed0` included the same required evidence classes.
- Lab 118/118, CPU 713/713, WinUI 28/28, and all three consumer builds passed.
- Conformance remained at 240 passed, 1 intentional skip, and the two accepted AccuracyCoin failures.
