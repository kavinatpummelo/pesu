param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string]$Version = "0.1.0",
    [switch]$InstallInno
)

$ErrorActionPreference = "Stop"

if (-not $IsWindows) {
    throw "build-installer.ps1 must be run on Windows."
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "dotnet SDK is not installed. Install .NET 8 SDK and retry."
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$solutionPath = Join-Path $repoRoot "Pesu.Windows.sln"
$projectPath = Join-Path $repoRoot "src/Pesu.Windows/Pesu.Windows.csproj"
$publishDir = Join-Path $repoRoot "artifacts/publish/Pesu.Windows"
$distDir = Join-Path $repoRoot "dist"
$issPath = Join-Path $repoRoot "installer/PesuSetup.iss"

function Resolve-IsccPath {
    if (Get-Command iscc -ErrorAction SilentlyContinue) {
        return (Get-Command iscc).Source
    }

    $commonPaths = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
    )
    foreach ($candidate in $commonPaths) {
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    return $null
}

function Install-InnoSetup {
    if (-not (Get-Command winget -ErrorAction SilentlyContinue)) {
        throw "winget is required to auto-install Inno Setup. Install App Installer and retry."
    }

    Write-Host "Installing Inno Setup..."
    & winget install --id JRSoftware.InnoSetup --exact --silent --accept-package-agreements --accept-source-agreements
    if ($LASTEXITCODE -ne 0) {
        throw "winget failed while installing Inno Setup (exit code: $LASTEXITCODE)."
    }
}

$isccPath = Resolve-IsccPath
if (-not $isccPath) {
    if (-not $InstallInno) {
        throw "Inno Setup compiler (ISCC.exe) was not found. Re-run with -InstallInno to auto-install it."
    }

    Install-InnoSetup
    $isccPath = Resolve-IsccPath
    if (-not $isccPath) {
        throw "Inno Setup was installed but ISCC.exe is still not discoverable. Open a new PowerShell session and retry."
    }
}

Write-Host "Restoring solution..."
dotnet restore $solutionPath

Write-Host "Publishing WinUI app ($Configuration)..."
dotnet publish $projectPath `
    -c $Configuration `
    -r win-x64 `
    --self-contained false `
    -p:WindowsAppSDKSelfContained=true `
    -p:PublishSingleFile=false `
    -o $publishDir

if (-not (Test-Path (Join-Path $publishDir "Pesu.Windows.exe"))) {
    throw "Publish output did not include Pesu.Windows.exe."
}

if (-not (Test-Path $distDir)) {
    New-Item -ItemType Directory -Path $distDir | Out-Null
}

Write-Host "Building Setup.exe with Inno Setup..."
& $isccPath "/DMyAppVersion=$Version" "/DSourceDir=\"$publishDir\"" "/DOutputDir=\"$distDir\"" $issPath
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup compilation failed (exit code: $LASTEXITCODE)."
}

$installerPath = Join-Path $distDir "PesuSetup-$Version.exe"
if (-not (Test-Path $installerPath)) {
    throw "Installer was not generated at expected path: $installerPath"
}

Write-Host "Installer ready: $installerPath"
