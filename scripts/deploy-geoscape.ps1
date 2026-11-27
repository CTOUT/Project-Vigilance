<#
.SYNOPSIS
    Deploys all 5 Project Vigilance modular packages to the local RimWorld Mods directory.

.DESCRIPTION
    Builds Geoscape.slnx targeting .NET 10 (net10.0) across all 5 modules (Core, Geoscape,
    Orbital, Aquatics, Thermal) — which runs StageCleanDistribution via Directory.Build.targets
    to populate dist/ProjectVigilance-<Module>/ — and creates or updates NTFS junctions (Windows)
    or symbolic links (Linux/macOS) in RimWorld's Mods directory pointing to the clean staging
    folders by default (~2.3 MB combined, excluding Source/ and caches). Pass -Dev to link
    directly to raw <Module>/ directories instead.

.PARAMETER RimWorldModsPath
    The target directory where RimWorld mods are located.
    Defaults to 'D:\Games\Steam\steamapps\common\RimWorld\Mods' on Windows,
    and '~/.local/share/Steam/steamapps/common/RimWorld/Mods' on Linux/macOS.

.PARAMETER Dev
    When set, points RimWorld Mods links directly at raw <Module>/ folders instead of clean dist/ staging.
#>

[CmdletBinding()]
param(
    [string]$RimWorldModsPath,
    [switch]$Dev,
    [switch]$ForceReplace,
    [switch]$SkipRimWorldCheck
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
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$solutionFile = Join-Path $repoRoot 'Geoscape\Source\Geoscape.slnx'

if (-not (Test-Path -LiteralPath $solutionFile)) {
    throw "Solution file not found at: $solutionFile"
}

$modules = @(
    @{ Source = 'Core';     PackageName = 'ProjectVigilance-Core';     Assembly = 'ProjectVigilance.Core.dll' }
    @{ Source = 'Geoscape'; PackageName = 'ProjectVigilance-Geoscape'; Assembly = 'Geoscape.dll' }
    @{ Source = 'Orbital';  PackageName = 'ProjectVigilance-Orbital';  Assembly = 'ProjectVigilance.Orbital.dll' }
    @{ Source = 'Aquatics'; PackageName = 'ProjectVigilance-Aquatics'; Assembly = 'ProjectVigilance.Aquatics.dll' }
    @{ Source = 'Thermal';  PackageName = 'ProjectVigilance-Thermal';  Assembly = 'ProjectVigilance.Thermal.dll' }
)

# 2. Handle active DLL locks in both source trees and clean staging if RimWorld is open
. (Join-Path $PSScriptRoot (Join-Path 'lib' 'ModDeployHelper.ps1'))
foreach ($mod in $modules) {
    $sourceDll = Join-Path $repoRoot (Join-Path $mod.Source (Join-Path '1.7' (Join-Path 'Assemblies' $mod.Assembly)))
    Invoke-AssemblyLockRotation -AssemblyPath $sourceDll

    $stagedDll = Join-Path $repoRoot (Join-Path 'dist' (Join-Path $mod.PackageName (Join-Path '1.7' (Join-Path 'Assemblies' $mod.Assembly))))
    Invoke-AssemblyLockRotation -AssemblyPath $stagedDll
}
$embeddedCoreStagedDll = Join-Path $repoRoot 'dist\ProjectVigilance-Geoscape\1.7\EmbeddedCore\Assemblies\ProjectVigilance.Core.dll'
Invoke-AssemblyLockRotation -AssemblyPath $embeddedCoreStagedDll

# 3. Build .NET 10 suite solution (automatically runs StageCleanDistribution via Directory.Build.targets)
Write-Host "--> Compiling Geoscape.slnx targeting .NET 10 (Release)..." -ForegroundColor Yellow
& dotnet build $solutionFile --configuration Release --nologo -v quiet
if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE."
}

# Clean any stray in-source bin/obj directories and unlocked .old backups
foreach ($mod in $modules) {
    $modRoot = Join-Path $repoRoot $mod.Source
    $sourceRoot = Join-Path $modRoot 'Source'
    if (Test-Path -LiteralPath $sourceRoot) {
        Get-ChildItem -LiteralPath $sourceRoot -Directory -Recurse -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -in @('bin', 'obj') } |
            Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
    }

    foreach ($asmDir in @(
        (Join-Path $modRoot '1.7\Assemblies'),
        (Join-Path $repoRoot "dist\$($mod.PackageName)\1.7\Assemblies")
    )) {
        if (Test-Path -LiteralPath $asmDir) {
            Get-ChildItem -LiteralPath $asmDir -Filter '*.old' -File -ErrorAction SilentlyContinue |
                Remove-Item -Force -ErrorAction SilentlyContinue
        }
    }
}
$embeddedCoreAsmDir = Join-Path $repoRoot 'dist\ProjectVigilance-Geoscape\1.7\EmbeddedCore\Assemblies'
if (Test-Path -LiteralPath $embeddedCoreAsmDir) {
    Get-ChildItem -LiteralPath $embeddedCoreAsmDir -Filter '*.old' -File -ErrorAction SilentlyContinue |
        Remove-Item -Force -ErrorAction SilentlyContinue
}

# 4. Validate target Mods folder
if (-not (Test-Path -LiteralPath $RimWorldModsPath)) {
    if (-not $SkipRimWorldCheck) {
        $rimWorldRoot = Split-Path -Path $RimWorldModsPath -Parent
        $hasGameExe = $false
        if (-not [string]::IsNullOrEmpty($rimWorldRoot) -and (Test-Path -LiteralPath $rimWorldRoot)) {
            $hasGameExe = $null -ne (Get-ChildItem -LiteralPath $rimWorldRoot -Filter 'RimWorld*' -File -ErrorAction SilentlyContinue | Select-Object -First 1)
        }
        if (-not $hasGameExe) {
            throw "Refusing to create '$RimWorldModsPath' because no RimWorld installation was detected in parent '$rimWorldRoot'. Pass -RimWorldModsPath or -SkipRimWorldCheck to override."
        }
    }
    Write-Host "--> Target Mods directory not found. Creating: $RimWorldModsPath" -ForegroundColor Yellow
    New-Item -ItemType Directory -Path $RimWorldModsPath -Force | Out-Null
}

# 5. Create or update NTFS Junctions / SymbolicLinks for all modules
$linkType = if ($isUnix) { 'SymbolicLink' } else { 'Junction' }
$totalMb = 0.0

foreach ($mod in $modules) {
    $deploySourceDir = if ($Dev) {
        Join-Path $repoRoot $mod.Source
    } else {
        Join-Path $repoRoot "dist\$($mod.PackageName)"
    }

    if (-not (Test-Path -LiteralPath $deploySourceDir)) {
        throw "Deployment source directory not found for '$($mod.PackageName)' at: $deploySourceDir"
    }

    $targetLink = Join-Path $RimWorldModsPath $mod.PackageName
    $desiredNormalized = [System.IO.Path]::GetFullPath($deploySourceDir).TrimEnd('\', '/')

    $existing = Get-Item -LiteralPath $targetLink -Force -ErrorAction SilentlyContinue
    if ($null -ne $existing) {
        $isReparse = $existing.Attributes.HasFlag([System.IO.FileAttributes]::ReparsePoint)
        $isExpectedKind = if ($isUnix) {
            $existing.LinkType -eq 'SymbolicLink' -or $isReparse
        } else {
            $existing.LinkType -eq 'Junction' -or $isReparse
        }

        $currentTargetRaw = ($existing.Target | Select-Object -First 1)
        $currentNormalized = if ($currentTargetRaw) {
            [System.IO.Path]::GetFullPath($currentTargetRaw).TrimEnd('\', '/')
        } else {
            ''
        }
        $targetMatches = if ($isUnix) {
            [string]::Equals($currentNormalized, $desiredNormalized, [System.StringComparison]::Ordinal)
        } else {
            [string]::Equals($currentNormalized, $desiredNormalized, [System.StringComparison]::OrdinalIgnoreCase)
        }

        if ($isExpectedKind -and $targetMatches) {
            Write-Host "--> Link current: $targetLink -> $deploySourceDir" -ForegroundColor Green
        } elseif ($isReparse) {
            Write-Host "--> Updating $linkType target: $targetLink -> $deploySourceDir" -ForegroundColor Yellow
            $existing.Delete()
            New-Item -ItemType $linkType -Path $targetLink -Target $deploySourceDir | Out-Null
            Write-Host "--> Updated $linkType`: $targetLink -> $deploySourceDir" -ForegroundColor Green
        } else {
            if (-not $ForceReplace) {
                throw "Refusing to delete non-$linkType directory at '$targetLink'. Remove/move it manually or pass -ForceReplace."
            }
            Write-Warning "Target exists and is not a $linkType (-ForceReplace specified). Removing and recreating..."
            Remove-Item -LiteralPath $targetLink -Recurse -Force
            New-Item -ItemType $linkType -Path $targetLink -Target $deploySourceDir | Out-Null
            Write-Host "--> Created $linkType`: $targetLink -> $deploySourceDir" -ForegroundColor Green
        }
    } else {
        New-Item -ItemType $linkType -Path $targetLink -Target $deploySourceDir | Out-Null
        Write-Host "--> Created $linkType`: $targetLink -> $deploySourceDir" -ForegroundColor Green
    }

    $modBytes = (Get-ChildItem -LiteralPath $deploySourceDir -Recurse -File -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum
    $modMb = if ($null -ne $modBytes) { [math]::Round($modBytes / 1MB, 2) } else { 0.0 }
    $totalMb = [math]::Round($totalMb + $modMb, 2)
    Write-Host "    $($mod.PackageName): $modMb MB" -ForegroundColor Gray
}

$modeLabel = if ($Dev) { 'Dev (raw module trees)' } else { 'Clean staging (Workshop-ready)' }
Write-Host "==> [Project Vigilance] Deployment complete [$modeLabel] in: $RimWorldModsPath" -ForegroundColor Cyan
Write-Host "    Combined Mod Size: $totalMb MB" -ForegroundColor Gray
