# Pokémon Roguelite Gameplay Core

This is a presentation-independent C# prototype. It is kept at the repository root and is not wired into Unity's `Assets` or player builds.

The source is organized into the initial deterministic battle prototype, the persistent Pokémon/catalog/save domain, and the expanding HGSS battle engine. Generation IV HGSS remains the default combat ruleset; newer-generation effects are versioned and must be implemented explicitly. This branch is development work and is not a production-ready or cartridge-conformant release.

The public repository includes source, tests, architecture notes, and only the small machine-readable fixtures required by tests. Emulator executables, ROMs, save states, game saves, machine-specific logs, test result dumps, and temporary research evidence are excluded. The local research workspace remains separate from Unity integration.

Build and test from this directory with the .NET SDK version in `global.json`:

```powershell
dotnet build Core/GameplayCore.csproj -c Release
dotnet test Tests/Tests.csproj -c Release
dotnet test Phase2/Tests/Tests.csproj -c Release
dotnet test Phase3/Tests/Tests.csproj -c Release
```
