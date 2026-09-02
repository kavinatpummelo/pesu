# Pesu Windows Native Replica

This repository now contains the first implementation pass of a Windows-native replica for the original macOS Pēsu app.

## Stack

- C# / .NET 8
- WinUI 3 (Windows App SDK)
- SQLite for local storage
- Service interfaces prepared for WASAPI capture, local transcription, and local summarization

## Solution layout

- `src/Pesu.Windows`: WinUI app shell and screens
- `src/Pesu.Core`: app models, service contracts, and view model
- `src/Pesu.Infrastructure`: SQLite persistence, dependency wiring, and temporary service stubs

## Current milestone

- Implemented native app shell with left navigation and flows matching mac screens:
  - Present
  - Past
  - Future
  - Stats
  - Settings
  - Recording
  - Summary
- Added local SQLite repository scaffold with schema and JSON persistence.
- Added DI wiring and seed data to boot the app with realistic sample meetings.

## Build (on Windows)

1. Install Visual Studio 2022 (17.10+) with:
   - .NET desktop development
   - Windows App SDK / WinUI development tools
2. Open `Pesu.Windows.sln`.
3. Set `Pesu.Windows` as startup project.
4. Run with `x64` target.

Or use PowerShell from repo root:

```powershell
./scripts/check-windows-prereqs.ps1
./scripts/run-windows.ps1
```

One-command bootstrap (installs missing prereqs with winget, then builds and runs):

```powershell
./scripts/bootstrap-windows.ps1
```

Useful options:

```powershell
./scripts/bootstrap-windows.ps1 -Configuration Release
./scripts/bootstrap-windows.ps1 -SkipRun
./scripts/bootstrap-windows.ps1 -SkipVisualStudioInstall
```

## Build a single installer EXE (Option 1)

Generate one distributable installer (`Setup.exe`) for the Windows-native app:

```powershell
./scripts/build-installer.ps1 -InstallInno
```

Output:

- `dist\PesuSetup-0.1.0.exe`

Optional flags:

```powershell
./scripts/build-installer.ps1 -Configuration Release -Version 0.1.1 -InstallInno
./scripts/build-installer.ps1 -Configuration Debug
```

Notes:

- This produces a single installer executable, not a single portable app binary.
- Installer target is x64 Windows.

## Next implementation phases

1. Replace stub services with production adapters:
   - WASAPI loopback + microphone capture
   - local STT engine
   - local summarizer
   - Windows credential store
2. Add calendar provider integration and duplicate resolution flow.
3. Implement settings persistence, export actions, and update/distribution pipeline.

## Linux host?

If your main machine is Linux, use a Windows VM and follow `docs/WINDOWS_VM_SETUP.md`.

Quick VM checklist from Linux terminal:

```bash
bash scripts/bootstrap-vm-notes.sh
```
