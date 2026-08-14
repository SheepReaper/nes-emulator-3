# Technical Stack

- C#/.NET 10 solution (`nes-emulator-3.slnx`) with Central Package Management in `Directory.Packages.props`.
- xUnit tests use Microsoft Testing Platform.
- Emulator library is portable .NET; WinUI 3 app uses Windows App SDK.
- `Sheep.Nes.Lab` uses Roslyn Workspaces/MSBuildWorkspace and SQLite FTS5.
- PowerShell is the repository shell on Windows.
