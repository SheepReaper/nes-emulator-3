# Suggested Commands

- Focused Lab: `dotnet test test/nes-lab/Sheep.Nes.Lab.Tests.csproj --no-restore --output Normal`
- CPU: `dotnet test test/cpu/Sheep.Emulation.Nes.Tests.csproj --no-restore --output Normal`
- Conformance: `dotnet test test/conformance/Sheep.Emulation.Nes.ConformanceTests.csproj --no-restore --output Normal`
- WinUI tests: `dotnet test test/emulator-winui/EmuSheep.Tests.csproj --no-restore --output Normal`
- Lab CLI: `dotnet run --project src/tools/nes-lab/Sheep.Nes.Lab.csproj -- <command>`
- Search with `rg`; changed files with `git status --short`; whitespace gate with `git diff --check`.
