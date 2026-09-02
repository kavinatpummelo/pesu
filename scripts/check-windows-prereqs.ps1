$ErrorActionPreference = "Stop"

$issues = @()

function Test-RunningOnWindows {
    return [System.Environment]::OSVersion.Platform -eq [System.PlatformID]::Win32NT
}

if (-not (Test-RunningOnWindows)) {
    $issues += "This script must run on Windows."
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    $issues += ".NET SDK is missing. Install .NET 8 SDK."
} else {
    $sdkVersion = (& dotnet --version)
    Write-Host "dotnet SDK: $sdkVersion"
}

$vsWhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
if (-not (Test-Path $vsWhere)) {
    $issues += "Visual Studio 2022 not found. Install Visual Studio 2022 with WinUI/Windows App SDK tools."
} else {
    $vsInstall = & $vsWhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
    if ([string]::IsNullOrWhiteSpace($vsInstall)) {
        $issues += "Visual Studio installation was not detected by vswhere."
    } else {
        Write-Host "Visual Studio: $vsInstall"
    }
}

if ($issues.Count -gt 0) {
    Write-Host "Prerequisite check failed:" -ForegroundColor Red
    foreach ($issue in $issues) {
        Write-Host " - $issue" -ForegroundColor Red
    }
    exit 1
}

Write-Host "All required prerequisites look available." -ForegroundColor Green
