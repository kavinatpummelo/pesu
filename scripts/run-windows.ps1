param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$solutionPath = Join-Path $repoRoot "Pesu.Windows.sln"
$projectPath = Join-Path $repoRoot "src/Pesu.Windows/Pesu.Windows.csproj"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "dotnet SDK is not installed. Install .NET 8 SDK and retry."
}

Write-Host "Restoring solution..."
dotnet restore $solutionPath

Write-Host "Building solution ($Configuration)..."
dotnet build $solutionPath -c $Configuration

Write-Host "Launching Pesu.Windows..."
dotnet run --project $projectPath -c $Configuration -f net8.0-windows10.0.19041.0
