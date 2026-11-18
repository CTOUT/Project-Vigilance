<#
.SYNOPSIS
    Toggles all 5 Project Vigilance RimWorld Mods/ junctions between Dev source trees and clean Workshop staging.

.DESCRIPTION
    Manages directory junctions inside RimWorld's `Mods/` folder for all 5 modular packages
    (`ProjectVigilance-Core`, `ProjectVigilance-Geoscape`, `ProjectVigilance-Orbital`,
    `ProjectVigilance-Aquatics`, `ProjectVigilance-Thermal`):
    - Workshop: Points each module junction to `dist/ProjectVigilance-<Module>` (clean runtime staging,
      excludes `Source/`, `bin/`, `obj/`, `*.old`, and `Assets.zip*`) for clean Steam Workshop uploads.
    - Dev: Copies any updated `PublishedFileId.txt` back to `<Module>/About/` and points each junction
      back to `<Module>/` for live local development edits.
    - Status: Reports active junction targets and individual + combined sizes without making changes.

.PARAMETER Mode
    Operation mode: 'Workshop', 'Dev', or 'Status' (default).

.PARAMETER ModsDir
    Path to the local RimWorld `Mods` directory. Defaults to 'D:\Games\Steam\steamapps\common\RimWorld\Mods'.
#>

[CmdletBinding()]
param(
    [ValidateSet('Workshop', 'Dev', 'Status')]
    [string]$Mode = 'Status',

    [string]$ModsDir = 'D:\Games\Steam\steamapps\common\RimWorld\Mods'
)

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$modules = @(
    @{ Source = 'Core';     PackageName = 'ProjectVigilance-Core' }
    @{ Source = 'Geoscape'; PackageName = 'ProjectVigilance-Geoscape' }
    @{ Source = 'Orbital';  PackageName = 'ProjectVigilance-Orbital' }
    @{ Source = 'Aquatics'; PackageName = 'ProjectVigilance-Aquatics' }
    @{ Source = 'Thermal';  PackageName = 'ProjectVigilance-Thermal' }
)

function Get-DirectorySizeMB {
    <#
    .SYNOPSIS
        Calculates total recursive file size of a directory in megabytes.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return 0.0
    }

    $sum = (Get-ChildItem -LiteralPath $Path -Recurse -File -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum
    if ($null -eq $sum) {
        return 0.0
    }

    return [math]::Round($sum / 1MB, 2)
}

function Set-ModJunction {
    <#
    .SYNOPSIS
        Replaces an existing NTFS directory junction to point at the specified target directory.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Junction,

        [Parameter(Mandatory = $true)]
        [string]$Target
    )

    $parentDir = Split-Path -Path $Junction -Parent
    if (-not (Test-Path -LiteralPath $parentDir)) {
        throw "RimWorld Mods directory not found at: $parentDir"
    }

    $existing = Get-Item -LiteralPath $Junction -Force -ErrorAction SilentlyContinue
    if ($null -ne $existing) {
        if (-not ($existing.Attributes -band [System.IO.FileAttributes]::ReparsePoint)) {
            throw "Refusing to delete non-junction directory at '$Junction'. Remove or move it manually first."
        }
        $existing.Delete()
    }

    New-Item -ItemType Junction -Path $Junction -Target $Target | Out-Null
}

switch ($Mode) {
    'Workshop' {
        $totalMb = 0.0
        Write-Host "==> Switching all 5 module junctions to [Workshop] mode..." -ForegroundColor Green
        foreach ($mod in $modules) {
            $workshopTarget = Join-Path $repoRoot "dist\$($mod.PackageName)"
            $junctionPath = Join-Path $ModsDir $mod.PackageName
            if (-not (Test-Path -LiteralPath $workshopTarget)) {
                throw "Workshop staging directory not found at '$workshopTarget'. Run dotnet build or ./scripts/package-release.ps1 first."
            }

            Set-ModJunction -Junction $junctionPath -Target $workshopTarget
            $sizeMb = Get-DirectorySizeMB -Path $workshopTarget
            $totalMb = [math]::Round($totalMb + $sizeMb, 2)
            Write-Host "    $($mod.PackageName): $workshopTarget ($sizeMb MB)" -ForegroundColor Gray
        }
        Write-Host "    Combined Workshop Size: $totalMb MB (clean staging, excludes Source/ and caches)" -ForegroundColor Cyan
    }

    'Dev' {
        $totalMb = 0.0
        Write-Host "==> Switching all 5 module junctions to [Dev] mode..." -ForegroundColor Green
        foreach ($mod in $modules) {
            $devTarget = Join-Path $repoRoot $mod.Source
            $workshopTarget = Join-Path $repoRoot "dist\$($mod.PackageName)"
            $junctionPath = Join-Path $ModsDir $mod.PackageName

            $stagedPublishedId = Join-Path $workshopTarget 'About\PublishedFileId.txt'
            $devPublishedId = Join-Path $devTarget 'About\PublishedFileId.txt'
            if (Test-Path -LiteralPath $stagedPublishedId) {
                Copy-Item -LiteralPath $stagedPublishedId -Destination $devPublishedId -Force
            }

            if (-not (Test-Path -LiteralPath $devTarget)) {
                throw "Dev mod directory not found at '$devTarget'."
            }

            Set-ModJunction -Junction $junctionPath -Target $devTarget
            $sizeMb = Get-DirectorySizeMB -Path $devTarget
            $totalMb = [math]::Round($totalMb + $sizeMb, 2)
            Write-Host "    $($mod.PackageName): $devTarget ($sizeMb MB)" -ForegroundColor Gray
        }
        Write-Host "    Combined Dev Size: $totalMb MB (live repo trees, excludes bin/ and obj/)" -ForegroundColor Cyan
    }

    'Status' {
        $totalMb = 0.0
        Write-Host "==> Modular RimWorld Mods/ junction status:" -ForegroundColor Cyan
        foreach ($mod in $modules) {
            $devTarget = Join-Path $repoRoot $mod.Source
            $workshopTarget = Join-Path $repoRoot "dist\$($mod.PackageName)"
            $junctionPath = Join-Path $ModsDir $mod.PackageName

            $existing = Get-Item -LiteralPath $junctionPath -Force -ErrorAction SilentlyContinue
            if ($null -eq $existing) {
                Write-Host "    $($mod.PackageName): [Missing] ($junctionPath)" -ForegroundColor Yellow
                continue
            }

            $target = ($existing.Target | Select-Object -First 1)
            $sizeMb = if ($target) { Get-DirectorySizeMB -Path $target } else { 0.0 }
            $totalMb = [math]::Round($totalMb + $sizeMb, 2)
            $activeLabel = if ($target -eq $workshopTarget) { 'Workshop' } elseif ($target -eq $devTarget) { 'Dev' } else { 'Custom' }
            Write-Host "    $($mod.PackageName) [$activeLabel]: $target ($sizeMb MB)" -ForegroundColor Gray
        }
        Write-Host "    Combined Active Size: $totalMb MB" -ForegroundColor Cyan
    }
}
