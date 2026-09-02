param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    [switch]$SkipRun,
    [switch]$SkipVisualStudioInstall
)

$ErrorActionPreference = "Stop"

function Test-RunningOnWindows {
    return [System.Environment]::OSVersion.Platform -eq [System.PlatformID]::Win32NT
}

function Test-DotNet8Sdk {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        return $false
    }

    $version = (& dotnet --version)
    if ([string]::IsNullOrWhiteSpace($version)) {
        return $false
    }

    $major = [int]($version.Split('.')[0])
    return $major -ge 8
}

function Test-VisualStudioInstalled {
    $vsWhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    if (-not (Test-Path $vsWhere)) {
        return $false
    }

    $installPath = & $vsWhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
    return -not [string]::IsNullOrWhiteSpace($installPath)
}

function Ensure-Winget {
    if (-not (Get-Command winget -ErrorAction SilentlyContinue)) {
        throw "winget is not available. Install App Installer from Microsoft Store and run this script again."
    }
}

function Install-WithWinget {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Id,
        [string]$Name,
        [string]$Override
    )

    $display = if ([string]::IsNullOrWhiteSpace($Name)) { $Id } else { $Name }
    Write-Host "Installing $display..."

    $args = @(
        "install",
        "--id", $Id,
        "--exact",
        "--silent",
        "--accept-package-agreements",
        "--accept-source-agreements"
    )

    if (-not [string]::IsNullOrWhiteSpace($Override)) {
        $args += @("--override", $Override)
    }

    & winget @args
    if ($LASTEXITCODE -ne 0) {
        throw "winget install failed for $Id (exit code: $LASTEXITCODE)."
    }
}

if (-not (Test-RunningOnWindows)) {
    throw "bootstrap-windows.ps1 must be run on Windows."
}

Ensure-Winget

if (-not (Test-DotNet8Sdk)) {
    Install-WithWinget -Id "Microsoft.DotNet.SDK.8" -Name ".NET 8 SDK"
} else {
    Write-Host ".NET 8 SDK already available."
}

if (-not (Test-VisualStudioInstalled)) {
    if ($SkipVisualStudioInstall) {
        Write-Warning "Visual Studio was not detected and -SkipVisualStudioInstall was passed. Build may fail."
    } else {
        Install-WithWinget -Id "Microsoft.VisualStudio.2022.Community" -Name "Visual Studio 2022 Community" -Override "--wait --quiet --norestart --add Microsoft.VisualStudio.Workload.ManagedDesktop --add Microsoft.VisualStudio.Component.Windows10SDK.19041"
    }
} else {
    Write-Host "Visual Studio installation detected."
}

$checkScript = Join-Path $PSScriptRoot "check-windows-prereqs.ps1"
$runScript = Join-Path $PSScriptRoot "run-windows.ps1"

Write-Host "Running prerequisite validation..."
& $checkScript

if (-not $SkipRun) {
    Write-Host "Starting application run flow..."
    & $runScript -Configuration $Configuration
} else {
    Write-Host "Bootstrap complete. Skipped app launch because -SkipRun was provided."
}
