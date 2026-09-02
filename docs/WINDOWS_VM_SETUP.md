# Running Pesu Windows from a Linux host (VM path)

This guide is the fastest way to run the WinUI app when your host machine is Linux.

## Recommended setup

- Host: Linux
- Guest: Windows 11 (or Windows 10 22H2+)
- VM platform: VirtualBox or VMware Workstation Player
- In guest: Visual Studio 2022 Community + .NET 8 SDK + winget

## 1) Create a Windows VM

Minimum recommended VM resources:

- 4 vCPU
- 8 GB RAM (12 GB preferred)
- 80 GB disk
- 3D acceleration enabled

Install Windows and complete initial updates.

## 2) Share your repo into the VM

Choose one:

- Git clone directly in the VM.
- Shared folder (VirtualBox/VMware tools installed).

Recommended in-VM path:

`C:\dev\pesu`

## 3) Open PowerShell in the VM and bootstrap

From repo root:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
./scripts/bootstrap-windows.ps1
```

What this does:

- installs .NET 8 SDK if missing
- installs Visual Studio 2022 Community if missing
- validates prerequisites
- restores, builds, and runs the WinUI app

## 4) If you already have Visual Studio

Use:

```powershell
./scripts/bootstrap-windows.ps1 -SkipVisualStudioInstall
```

## 5) Run without launching (CI-like sanity check)

```powershell
./scripts/bootstrap-windows.ps1 -SkipRun
```

## Common issues

- `winget not available`
  - Install **App Installer** from Microsoft Store.
- XAML/WinUI build tools missing
  - Re-run bootstrap without `-SkipVisualStudioInstall`, or add the desktop workload in Visual Studio Installer.
- Slow UI in VM
  - Increase RAM/CPU and enable 3D acceleration.

## Native host recommendation

For best reliability and performance (especially audio and graphics), run on a real Windows machine when implementing capture/transcription features.
