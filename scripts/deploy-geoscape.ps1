<#
.SYNOPSIS
    Deploys the Geoscape mod to the local RimWorld Mods directory using an NTFS junction.

.DESCRIPTION
    Builds the Geoscape project targeting .NET 10 (net10.0) and creates or updates
    an NTFS junction in RimWorld's Mods directory pointing to Project-Vigilance\Geoscape.
    Idempotent and safe to run repeatedly.

.PARAMETER RimWorldModsPath
    The target directory where RimWorld mods are located.
    Defaults to 'D:\Games\Steam\steamapps\common\RimWorld\Mods'.
#>

[CmdletBinding()]
param(
    [string]$RimWorldModsPath = 'D:\Games\Steam\steamapps\common\RimWorld\Mods'
)

$ErrorActionPreference = 'Stop'

Write-Host "==> [Project Vigilance] Starting Geoscape deployment pipeline..." -ForegroundColor Cyan

# 1. Resolve repository root
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$geoscapeDir = Join-Path $repoRoot 'Geoscape'
$solutionFile = Join-Path $geoscapeDir 'Source\Geoscape.slnx'

if (-not (Test-Path $solutionFile)) {
    throw "Solution file not found at: $solutionFile"
}

# 2. Handle active DLL locks if RimWorld is open
$targetDll = Join-Path $geoscapeDir '1.7\Assemblies\Geoscape.dll'
$oldDll = Join-Path $geoscapeDir '1.7\Assemblies\Geoscape.dll.old'
if (Test-Path $oldDll) {
    Remove-Item $oldDll -Force -ErrorAction SilentlyContinue
}
if (Test-Path $targetDll) {
    try {
        $fileStream = [System.IO.File]::Open($targetDll, [System.IO.FileMode]::Open, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)
        $fileStream.Close()
        $fileStream.Dispose()
    } catch {
        Write-Host "--> Active file lock detected on Geoscape.dll (RimWorld running). Rotating locked assembly..." -ForegroundColor Yellow
        Move-Item -Path $targetDll -Destination $oldDll -Force
    }
}

# 3. Build .NET 10 binary
Write-Host "--> Compiling Geoscape.slnx targeting .NET 10..." -ForegroundColor Yellow
& dotnet build $solutionFile --configuration Release
if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE."
}

# 3. Validate target Mods folder
if (-not (Test-Path $RimWorldModsPath)) {
    throw "RimWorld Mods directory not found at: $RimWorldModsPath"
}

# 4. Create or verify NTFS Junction
$targetJunction = Join-Path $RimWorldModsPath 'ProjectVigilance-Geoscape'

if (Test-Path $targetJunction) {
    $item = Get-Item $targetJunction
    if ($item.LinkType -eq 'Junction') {
        Write-Host "--> Junction already exists and is healthy: $targetJunction -> $($item.Target)" -ForegroundColor Green
    } else {
        Write-Warning "Target exists but is not a Junction. Removing and recreating..."
        Remove-Item -Path $targetJunction -Recurse -Force
        New-Item -ItemType Junction -Path $targetJunction -Target $geoscapeDir | Out-Null
        Write-Host "--> Created junction: $targetJunction -> $geoscapeDir" -ForegroundColor Green
    }
} else {
    New-Item -ItemType Junction -Path $targetJunction -Target $geoscapeDir | Out-Null
    Write-Host "--> Created junction: $targetJunction -> $geoscapeDir" -ForegroundColor Green
}

Write-Host "==> [Project Vigilance] Deployment complete. Mod is active in: $targetJunction" -ForegroundColor Cyan
