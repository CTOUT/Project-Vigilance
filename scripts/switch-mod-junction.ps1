<#
.SYNOPSIS
    Toggles the RimWorld Mods/ NTFS junction between the Dev source tree and clean Workshop staging.

.DESCRIPTION
    Manages the `ProjectVigilance-Geoscape` directory junction inside RimWorld's `Mods/` folder:
    - Workshop: Points to `dist/ProjectVigilance-OverlandIncursions` (~2.3 MB, excludes `Source/` and caches)
      for clean Steam Workshop uploads.
    - Dev: Copies any updated `PublishedFileId.txt` back to `Geoscape/About/` and points back to `Geoscape/`
      (~3.8 MB, excludes `bin/`/`obj/`) for live local development edits.
    - Status: Reports the active junction target and directory size without making changes.

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
$devTarget = Join-Path $repoRoot 'Geoscape'
$workshopTarget = Join-Path $repoRoot 'dist\ProjectVigilance-OverlandIncursions'
$junctionPath = Join-Path $ModsDir 'ProjectVigilance-Geoscape'

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
        if (-not (Test-Path -LiteralPath $workshopTarget)) {
            throw "Workshop staging directory not found at '$workshopTarget'. Run ./scripts/package-release.ps1 first."
        }

        Set-ModJunction -Junction $junctionPath -Target $workshopTarget
        $sizeMb = Get-DirectorySizeMB -Path $workshopTarget
        Write-Host "==> Mod junction switched to [Workshop] mode" -ForegroundColor Green
        Write-Host "    Junction: $junctionPath" -ForegroundColor Gray
        Write-Host "    Target:   $workshopTarget" -ForegroundColor Gray
        Write-Host "    Size:     $sizeMb MB (clean staging, excludes Source/ and caches)" -ForegroundColor Gray
    }

    'Dev' {
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
        Write-Host "==> Mod junction switched to [Dev] mode" -ForegroundColor Green
        Write-Host "    Junction: $junctionPath" -ForegroundColor Gray
        Write-Host "    Target:   $devTarget" -ForegroundColor Gray
        Write-Host "    Size:     $sizeMb MB (live repo tree, excludes bin/ and obj/)" -ForegroundColor Gray
    }

    'Status' {
        $existing = Get-Item -LiteralPath $junctionPath -Force -ErrorAction SilentlyContinue
        if ($null -eq $existing) {
            Write-Host "==> Mod junction not found at: $junctionPath" -ForegroundColor Yellow
            return
        }

        $target = ($existing.Target | Select-Object -First 1)
        $sizeMb = if ($target) { Get-DirectorySizeMB -Path $target } else { 0.0 }
        $activeLabel = if ($target -eq $workshopTarget) { 'Workshop (clean staging)' } elseif ($target -eq $devTarget) { 'Dev (live repo tree)' } else { 'Custom' }

        Write-Host "==> Mod junction status: [$activeLabel]" -ForegroundColor Cyan
        Write-Host "    Junction: $junctionPath" -ForegroundColor Gray
        Write-Host "    Target:   $target" -ForegroundColor Gray
        Write-Host "    Size:     $sizeMb MB" -ForegroundColor Gray
    }
}
