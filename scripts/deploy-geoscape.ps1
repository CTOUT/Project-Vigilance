<#
.SYNOPSIS
    Deploys the Geoscape mod to the local RimWorld Mods directory using an NTFS junction or symlink.

.DESCRIPTION
    Builds the Geoscape project targeting .NET 10 (net10.0) and creates or updates
    an NTFS junction (Windows) or symbolic link (Linux/macOS) in RimWorld's Mods directory
    pointing to Project-Vigilance/Geoscape.
    Idempotent and safe to run repeatedly across Windows and Linux (CachyOS).

.PARAMETER RimWorldModsPath
    The target directory where RimWorld mods are located.
    Defaults to 'D:\Games\Steam\steamapps\common\RimWorld\Mods' on Windows,
    and '~/.local/share/Steam/steamapps/common/RimWorld/Mods' on Linux/macOS.
#>

[CmdletBinding()]
param(
    [string]$RimWorldModsPath
)

$ErrorActionPreference = 'Stop'

$isUnix = $IsLinux -or $IsMacOS

if (-not $RimWorldModsPath) {
    if ($isUnix) {
        $RimWorldModsPath = [System.IO.Path]::Combine($HOME, '.local', 'share', 'Steam', 'steamapps', 'common', 'RimWorld', 'Mods')
    } else {
        $RimWorldModsPath = 'D:\Games\Steam\steamapps\common\RimWorld\Mods'
    }
}

Write-Host "==> [Project Vigilance] Starting Geoscape deployment pipeline..." -ForegroundColor Cyan

# 1. Resolve repository root
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$geoscapeDir = Join-Path $repoRoot 'Geoscape'
$solutionFile = Join-Path $geoscapeDir (Join-Path 'Source' 'Geoscape.slnx')

if (-not (Test-Path $solutionFile)) {
    throw "Solution file not found at: $solutionFile"
}

# 2. Handle active DLL locks if RimWorld is open
. (Join-Path $PSScriptRoot (Join-Path 'lib' 'ModDeployHelper.ps1'))
$targetDll = Join-Path $geoscapeDir (Join-Path '1.7' (Join-Path 'Assemblies' 'Geoscape.dll'))
Invoke-AssemblyLockRotation -AssemblyPath $targetDll

# 3. Build .NET 10 binary
Write-Host "--> Compiling Geoscape.slnx targeting .NET 10..." -ForegroundColor Yellow
& dotnet build $solutionFile --configuration Release
if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE."
}

# 4. Validate target Mods folder
if (-not (Test-Path $RimWorldModsPath)) {
    Write-Host "--> Target Mods directory not found. Creating: $RimWorldModsPath" -ForegroundColor Yellow
    New-Item -ItemType Directory -Path $RimWorldModsPath -Force | Out-Null
}

# 5. Create or verify NTFS Junction / SymbolicLink
$linkType = if ($isUnix) { 'SymbolicLink' } else { 'Junction' }
$targetLink = Join-Path $RimWorldModsPath 'ProjectVigilance-Geoscape'

if (Test-Path $targetLink) {
    $item = Get-Item $targetLink
    $isHealthy = if ($isUnix) {
        $item.LinkType -eq 'SymbolicLink' -or $item.Attributes.HasFlag([System.IO.FileAttributes]::ReparsePoint)
    } else {
        $item.LinkType -eq 'Junction'
    }

    if ($isHealthy) {
        Write-Host "--> Link already exists and is healthy: $targetLink -> $($item.Target)" -ForegroundColor Green
    } else {
        Write-Warning "Target exists but is not a $linkType. Removing and recreating..."
        Remove-Item -Path $targetLink -Recurse -Force
        New-Item -ItemType $linkType -Path $targetLink -Target $geoscapeDir | Out-Null
        Write-Host "--> Created $linkType`: $targetLink -> $geoscapeDir" -ForegroundColor Green
    }
} else {
    New-Item -ItemType $linkType -Path $targetLink -Target $geoscapeDir | Out-Null
    Write-Host "--> Created $linkType`: $targetLink -> $geoscapeDir" -ForegroundColor Green
}

Write-Host "==> [Project Vigilance] Deployment complete. Mod is active in: $targetLink" -ForegroundColor Cyan
