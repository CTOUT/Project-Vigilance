<#
.SYNOPSIS
    Deploys all Project Vigilance modular packages to the local RimWorld Mods directory using NTFS junctions or symlinks.

.DESCRIPTION
    Builds Geoscape.slnx targeting .NET 10 (net10.0) across all 5 modules (Core, Geoscape,
    Orbital, Aquatics, Thermal) and creates or updates NTFS junctions (Windows) or symbolic links
    (Linux/macOS) in RimWorld's Mods directory pointing to each module directory.
    Idempotent and safe to run repeatedly across Windows and Linux (Zorin OS, Ubuntu, CachyOS).

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

Write-Host "==> [Project Vigilance] Starting modular suite deployment pipeline..." -ForegroundColor Cyan

# 1. Resolve repository root & module layout
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$solutionFile = Join-Path $repoRoot 'Geoscape\Source\Geoscape.slnx'

if (-not (Test-Path $solutionFile)) {
    throw "Solution file not found at: $solutionFile"
}

$modules = @(
    @{ Source = 'Core';     LinkName = 'ProjectVigilance-Core';     Assembly = 'ProjectVigilance.Core.dll' }
    @{ Source = 'Geoscape'; LinkName = 'ProjectVigilance-Geoscape'; Assembly = 'Geoscape.dll' }
    @{ Source = 'Orbital';  LinkName = 'ProjectVigilance-Orbital';  Assembly = 'ProjectVigilance.Orbital.dll' }
    @{ Source = 'Aquatics'; LinkName = 'ProjectVigilance-Aquatics'; Assembly = 'ProjectVigilance.Aquatics.dll' }
    @{ Source = 'Thermal';  LinkName = 'ProjectVigilance-Thermal';  Assembly = 'ProjectVigilance.Thermal.dll' }
)

# 2. Handle active DLL locks if RimWorld is open
. (Join-Path $PSScriptRoot (Join-Path 'lib' 'ModDeployHelper.ps1'))
foreach ($mod in $modules) {
    $targetDll = Join-Path $repoRoot (Join-Path $mod.Source (Join-Path '1.7' (Join-Path 'Assemblies' $mod.Assembly)))
    Invoke-AssemblyLockRotation -AssemblyPath $targetDll
}

# 3. Build .NET 10 suite solution
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

# 5. Create or verify NTFS Junctions / SymbolicLinks for all modules
$linkType = if ($isUnix) { 'SymbolicLink' } else { 'Junction' }

foreach ($mod in $modules) {
    $sourceDir = Join-Path $repoRoot $mod.Source
    $targetLink = Join-Path $RimWorldModsPath $mod.LinkName

    if (Test-Path $targetLink) {
        $item = Get-Item $targetLink
        $isHealthy = if ($isUnix) {
            $item.LinkType -eq 'SymbolicLink' -or $item.Attributes.HasFlag([System.IO.FileAttributes]::ReparsePoint)
        } else {
            $item.LinkType -eq 'Junction'
        }

        if ($isHealthy) {
            Write-Host "--> Link healthy: $targetLink -> $($item.Target)" -ForegroundColor Green
        } else {
            Write-Warning "Target exists but is not a $linkType. Removing and recreating..."
            Remove-Item -Path $targetLink -Recurse -Force
            New-Item -ItemType $linkType -Path $targetLink -Target $sourceDir | Out-Null
            Write-Host "--> Created $linkType`: $targetLink -> $sourceDir" -ForegroundColor Green
        }
    } else {
        New-Item -ItemType $linkType -Path $targetLink -Target $sourceDir | Out-Null
        Write-Host "--> Created $linkType`: $targetLink -> $sourceDir" -ForegroundColor Green
    }
}

Write-Host "==> [Project Vigilance] Deployment complete for all 5 modules in: $RimWorldModsPath" -ForegroundColor Cyan
