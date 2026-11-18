<#
.SYNOPSIS
    Deploys the Geoscape mod to the local RimWorld Mods directory using an NTFS junction or symlink.

.DESCRIPTION
    Builds Geoscape.csproj targeting .NET 10 (net10.0) — which runs StageCleanDistribution to
    populate dist/ProjectVigilance-OverlandIncursions — and creates or updates an NTFS junction
    (Windows) or symbolic link (Linux/macOS) in RimWorld's Mods directory pointing to the clean
    staging folder (~2.3 MB, excluding Source/ and build caches). Pass -Dev to link directly to
    the raw Geoscape/ folder instead.

.PARAMETER RimWorldModsPath
    The target directory where RimWorld mods are located.
    Defaults to 'D:\Games\Steam\steamapps\common\RimWorld\Mods' on Windows,
    and '~/.local/share/Steam/steamapps/common/RimWorld/Mods' on Linux/macOS.

.PARAMETER Dev
    When set, points the RimWorld Mods link directly at raw Geoscape/ instead of clean dist/ staging.
#>

[CmdletBinding()]
param(
    [string]$RimWorldModsPath,
    [switch]$Dev
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

# 1. Resolve repository root and build targets
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$geoscapeDir = Join-Path $repoRoot 'Geoscape'
$stagingDir = Join-Path $repoRoot 'dist\ProjectVigilance-OverlandIncursions'
$projectFile = Join-Path $geoscapeDir (Join-Path 'Source' (Join-Path 'Geoscape' 'Geoscape.csproj'))
$deploySourceDir = if ($Dev) { $geoscapeDir } else { $stagingDir }

if (-not (Test-Path -LiteralPath $projectFile)) {
    throw "Project file not found at: $projectFile"
}

# 2. Handle active DLL locks if RimWorld is open
. (Join-Path $PSScriptRoot (Join-Path 'lib' 'ModDeployHelper.ps1'))
$targetDll = Join-Path $geoscapeDir (Join-Path '1.7' (Join-Path 'Assemblies' 'Geoscape.dll'))
Invoke-AssemblyLockRotation -AssemblyPath $targetDll

# 3. Build .NET 10 binary (automatically triggers StageCleanDistribution into dist/)
Write-Host "--> Compiling Geoscape.csproj targeting .NET 10 (Release)..." -ForegroundColor Yellow
& dotnet build $projectFile --configuration Release --nologo -v quiet
if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE."
}

# Remove any stray in-source bin/obj directories and unlocked .old backups
$sourceRoot = Join-Path $geoscapeDir 'Source'
Get-ChildItem -LiteralPath $sourceRoot -Directory -Recurse -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -in @('bin', 'obj') } |
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
Get-ChildItem -LiteralPath (Join-Path $geoscapeDir '1.7\Assemblies') -Filter '*.old' -File -ErrorAction SilentlyContinue |
    Remove-Item -Force -ErrorAction SilentlyContinue
if (Test-Path -LiteralPath (Join-Path $stagingDir '1.7\Assemblies')) {
    Get-ChildItem -LiteralPath (Join-Path $stagingDir '1.7\Assemblies') -Filter '*.old' -File -ErrorAction SilentlyContinue |
        Remove-Item -Force -ErrorAction SilentlyContinue
}

if (-not (Test-Path -LiteralPath $deploySourceDir)) {
    throw "Deployment source directory not found at: $deploySourceDir"
}

# 4. Validate target Mods folder
if (-not (Test-Path -LiteralPath $RimWorldModsPath)) {
    Write-Host "--> Target Mods directory not found. Creating: $RimWorldModsPath" -ForegroundColor Yellow
    New-Item -ItemType Directory -Path $RimWorldModsPath -Force | Out-Null
}

# 5. Create or update NTFS Junction / SymbolicLink to desired target
$linkType = if ($isUnix) { 'SymbolicLink' } else { 'Junction' }
$targetLink = Join-Path $RimWorldModsPath 'ProjectVigilance-Geoscape'
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
        Write-Host "--> Link already exists and targets desired directory: $targetLink -> $deploySourceDir" -ForegroundColor Green
    } elseif ($isReparse) {
        Write-Host "--> Updating $linkType target from '$currentTargetRaw' to '$deploySourceDir'..." -ForegroundColor Yellow
        $existing.Delete()
        New-Item -ItemType $linkType -Path $targetLink -Target $deploySourceDir | Out-Null
        Write-Host "--> Updated $linkType`: $targetLink -> $deploySourceDir" -ForegroundColor Green
    } else {
        Write-Warning "Target exists and is not a $linkType. Removing and recreating..."
        Remove-Item -LiteralPath $targetLink -Recurse -Force
        New-Item -ItemType $linkType -Path $targetLink -Target $deploySourceDir | Out-Null
        Write-Host "--> Created $linkType`: $targetLink -> $deploySourceDir" -ForegroundColor Green
    }
} else {
    New-Item -ItemType $linkType -Path $targetLink -Target $deploySourceDir | Out-Null
    Write-Host "--> Created $linkType`: $targetLink -> $deploySourceDir" -ForegroundColor Green
}

$totalBytes = (Get-ChildItem -LiteralPath $deploySourceDir -Recurse -File -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum
$sizeMb = if ($null -ne $totalBytes) { [math]::Round($totalBytes / 1MB, 2) } else { 0.0 }
$modeLabel = if ($Dev) { 'Dev (raw Geoscape)' } else { 'Clean staging (Workshop-ready)' }

Write-Host "==> [Project Vigilance] Deployment complete [$modeLabel]" -ForegroundColor Cyan
Write-Host "    Active Link: $targetLink -> $deploySourceDir" -ForegroundColor Gray
Write-Host "    Mod Size:    $sizeMb MB" -ForegroundColor Gray
