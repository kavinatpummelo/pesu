#!/usr/bin/env bash
set -euo pipefail

cat <<'EOF'
Pesu Windows VM Quickstart (Linux host -> Windows guest)
=========================================================

1) VM recommendation
   - OS: Windows 11 (or Windows 10 22H2+)
   - CPU: 4 vCPU minimum
   - RAM: 8 GB minimum (12 GB preferred)
   - Disk: 80 GB minimum
   - Enable 3D acceleration in VM settings

2) Bring repo into VM
   - Preferred path: C:\dev\pesu
   - Either git clone in VM or use shared folder

3) In Windows PowerShell (inside VM), from repo root:
   Set-ExecutionPolicy -Scope Process Bypass
   ./scripts/bootstrap-windows.ps1

4) Useful variants
   ./scripts/bootstrap-windows.ps1 -SkipRun
   ./scripts/bootstrap-windows.ps1 -Configuration Release
   ./scripts/bootstrap-windows.ps1 -SkipVisualStudioInstall

5) Troubleshooting
   - winget missing: install App Installer from Microsoft Store
   - Build tools missing: rerun bootstrap without -SkipVisualStudioInstall
   - Slow rendering: increase VM RAM/CPU and ensure 3D acceleration is on

For full guidance, see:
   docs/WINDOWS_VM_SETUP.md

EOF
