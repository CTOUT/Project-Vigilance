<#
.SYNOPSIS
    Prepares, validates, and packages a Project Vigilance release across GitHub and Steam Workshop.

.DESCRIPTION
    Automates version synchronisation across package.json, Geoscape.csproj, and packaging scripts,
    extracts release notes from CHANGELOG.md, validates asset constraints (Preview.png < 1MB),
    builds Release binaries, runs spellcheck, generates distribution ZIP packages, and outputs
    actionable next steps for Git, GitHub Releases, and Steam Workshop copy.

.PARAMETER Version
    The Semantic Version to release (e.g. '0.3.0', '0.4.0'). Mandatory.

.PARAMETER SkipBuild
    Skips C# compilation and packaging.

.PARAMETER SkipSpellcheck
    Skips cspell validation.

.EXAMPLE
    ./scripts/prepare-release.ps1 -Version '0.4.0'
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version,

    [switch]$SkipBuild,
    [switch]$SkipSpellcheck
)

$ErrorActionPreference = 'Stop'

Write-Host "=====================================================================" -ForegroundColor Cyan
Write-Host " Project Vigilance — Release Preparation Pipeline (v$Version)" -ForegroundColor Cyan
Write-Host "=====================================================================" -ForegroundColor Cyan

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$geoscapeDir = Join-Path $repoRoot 'Geoscape'
$csprojPath = Join-Path $geoscapeDir 'Source\Geoscape\Geoscape.csproj'
$packageJsonPath = Join-Path $repoRoot 'package.json'
$changelogPath = Join-Path $repoRoot 'CHANGELOG.md'
$packageReleaseScript = Join-Path $PSScriptRoot 'package-release.ps1'
$distDir = Join-Path $repoRoot 'dist'
$docsDir = Join-Path $repoRoot 'docs'

if (-not (Test-Path $distDir)) {
    New-Item -ItemType Directory -Path $distDir -Force | Out-Null
}

# 1. Synchronize package.json version
Write-Host "--> [1/6] Synchronizing version in package.json..." -ForegroundColor Yellow
if (Test-Path $packageJsonPath) {
    $pkg = Get-Content $packageJsonPath -Raw | ConvertFrom-Json
    if ($pkg.version -ne $Version) {
        $pkg.version = $Version
        $pkgJson = $pkg | ConvertTo-Json -Depth 10
        Set-Content -Path $packageJsonPath -Value "$pkgJson`n" -NoNewline
        Write-Host "    Updated package.json to version $Version" -ForegroundColor Gray
    } else {
        Write-Host "    package.json already set to $Version" -ForegroundColor Gray
    }
}

# 2. Synchronize Geoscape.csproj assembly version tags
Write-Host "--> [2/6] Synchronizing version in Geoscape.csproj..." -ForegroundColor Yellow
if (Test-Path $csprojPath) {
    [xml]$xml = Get-Content $csprojPath
    $propGroup = $xml.Project.PropertyGroup | Select-Object -First 1

    $updatedCsp = $false
    if ($propGroup.Version -ne $Version) {
        $propGroup.Version = $Version
        $propGroup.AssemblyVersion = "$Version.0"
        $propGroup.FileVersion = "$Version.0"
        $xml.Save($csprojPath)
        Write-Host "    Updated Geoscape.csproj <Version> to $Version" -ForegroundColor Gray
    } else {
        Write-Host "    Geoscape.csproj already set to $Version" -ForegroundColor Gray
    }
}

# 3. Synchronize default parameter in package-release.ps1
Write-Host "--> [3/6] Synchronizing default Version in package-release.ps1..." -ForegroundColor Yellow
if (Test-Path $packageReleaseScript) {
    $scriptContent = Get-Content $packageReleaseScript -Raw
    $pattern = "\[string\]\$Version = '\d+\.\d+\.\d+'"
    if ($scriptContent -match $pattern) {
        $scriptContent = $scriptContent -replace $pattern, "[string]`$Version = '$Version'"
        Set-Content -Path $packageReleaseScript -Value $scriptContent -NoNewline
        Write-Host "    Updated package-release.ps1 default parameter to '$Version'" -ForegroundColor Gray
    }
}

# 4. Extract release notes from CHANGELOG.md
Write-Host "--> [4/6] Extracting release notes from CHANGELOG.md..." -ForegroundColor Yellow
$releaseNotesFile = Join-Path $distDir "release-notes-v$Version.md"
if (Test-Path $changelogPath) {
    $changelogContent = Get-Content $changelogPath
    $capturing = $false
    $notesLines = [System.Collections.Generic.List[string]]::new()

    foreach ($line in $changelogContent) {
        if ($line -match "^##\s+\[$([regex]::Escape($Version))\]") {
            $capturing = $true
            $notesLines.Add("# Project Vigilance: Overland Incursions v$Version")
            $notesLines.Add("")
            continue
        }
        if ($capturing -and $line -match "^##\s+\[") {
            # Hit next release block
            break
        }
        if ($capturing) {
            $notesLines.Add($line)
        }
    }

    if ($notesLines.Count -gt 0) {
        # Trim leading/trailing blank lines
        while ($notesLines.Count -gt 0 -and [string]::IsNullOrWhiteSpace($notesLines[0])) { $notesLines.RemoveAt(0) }
        while ($notesLines.Count -gt 0 -and [string]::IsNullOrWhiteSpace($notesLines[$notesLines.Count - 1])) { $notesLines.RemoveAt($notesLines.Count - 1) }

        # Append Installation and Bug reporting footer
        $notesLines.Add("")
        $notesLines.Add("### 📦 Installation")
        $notesLines.Add("1. Download `ProjectVigilance-OverlandIncursions-v$Version.zip` attached below.")
        $notesLines.Add("2. Extract the `ProjectVigilance-OverlandIncursions` folder into your RimWorld `Mods/` directory (or subscribe on the [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3822378226)).")
        $notesLines.Add("3. In RimWorld's mod menu, activate **Harmony** followed by **Project Vigilance - Overland Incursions**.")
        $notesLines.Add("")
        $notesLines.Add("### 💬 Feedback & Discussion")
        $notesLines.Add("Please report issues via [GitHub Issues](https://github.com/CTOUT/Project-Vigilance/issues) or join the discussions in [GitHub Discussions](https://github.com/CTOUT/Project-Vigilance/discussions)!")

        Set-Content -Path $releaseNotesFile -Value ($notesLines -join "`n") -NoNewline
        Write-Host "    Extracted release notes to: $releaseNotesFile" -ForegroundColor Gray
    } else {
        Write-Warning "Could not find '## [$Version]' in CHANGELOG.md! Please add release entries before publishing."
    }
}

# 5. Validate Steam Workshop assets and BBCode copy files
Write-Host "--> [5/6] Validating Steam Workshop assets and documentation..." -ForegroundColor Yellow
$previewPng = Join-Path $geoscapeDir 'About\Preview.png'
if (Test-Path $previewPng) {
    $previewItem = Get-Item $previewPng
    $previewKb = [math]::Round($previewItem.Length / 1KB, 1)
    if ($previewItem.Length -gt 1MB) {
        Write-Error "Steam Workshop Preview.png exceeds 1.0 MB ceiling! (Size: $previewKb KB). Compress image before uploading."
    } else {
        Write-Host "    Preview.png size OK: $previewKb KB (Max 1024 KB)" -ForegroundColor Gray
    }
} else {
    Write-Warning "Preview.png not found at $previewPng"
}

$changenotesFile = Join-Path $docsDir "workshop_changenotes_v$Version.txt"
if (-not (Test-Path $changenotesFile)) {
    Write-Host "    Generating BBCode changenotes template: $changenotesFile" -ForegroundColor Gray
    $bbcodeTemplate = @"
[h2]v$Version — Milestone Update[/h2]
[list]
[*] [b]Key Highlights:[/b]
    [list]
    [*] Feature 1 description
    [*] Feature 2 description
    [/list]
[*] [b]Fixes & Optimizations:[/b]
    [list]
    [*] Performance and bug fix details
    [/list]
[/list]
"@
    Set-Content -Path $changenotesFile -Value $bbcodeTemplate -NoNewline
} else {
    Write-Host "    Found existing changenotes file: $changenotesFile" -ForegroundColor Gray
}

# 6. Build, test and package
Write-Host "--> [6/6] Executing packaging and quality gates..." -ForegroundColor Yellow

if (-not $SkipSpellcheck) {
    Write-Host "    Running spellcheck..." -ForegroundColor Gray
    & npm run spellcheck
    if ($LASTEXITCODE -ne 0) {
        throw "Spellcheck failed! Fix spelling errors before publishing release."
    }
}

if (-not $SkipBuild) {
    Write-Host "    Running release packaging script..." -ForegroundColor Gray
    & $packageReleaseScript -Version $Version -OutputDir 'dist'
    if ($LASTEXITCODE -ne 0) {
        throw "Packaging script failed with exit code $LASTEXITCODE."
    }
}

Write-Host "`n=====================================================================" -ForegroundColor Green
Write-Host " Release Preparation Successful for v$Version!" -ForegroundColor Green
Write-Host "=====================================================================" -ForegroundColor Green

Write-Host "`nNext Steps to Complete the Release:`n" -ForegroundColor Cyan

Write-Host "1. Review and Commit on 'main':" -ForegroundColor Yellow
Write-Host "   git add -A" -ForegroundColor Gray
Write-Host "   git commit -m `"chore(release): prepare v$Version`"" -ForegroundColor Gray
Write-Host "   git push origin main" -ForegroundColor Gray

Write-Host "`n2. Create and Push Tag:" -ForegroundColor Yellow
Write-Host "   git tag -a v$Version -m `"Release v$Version`"" -ForegroundColor Gray
Write-Host "   git push origin v$Version" -ForegroundColor Gray

Write-Host "`n3. Publish GitHub Release via GitHub CLI:" -ForegroundColor Yellow
Write-Host "   gh release create v$Version dist/ProjectVigilance-OverlandIncursions-v$Version.zip --title `"Project Vigilance: Overland Incursions v$Version`" --notes-file $releaseNotesFile" -ForegroundColor Gray

Write-Host "`n4. Steam Workshop Update:" -ForegroundColor Yellow
Write-Host "   a) Launch RimWorld -> Mods -> Project Vigilance -> Click 'Upload to Steam Workshop'" -ForegroundColor Gray
Write-Host "   b) Update Workshop Description from: docs/workshop_description.bbcode" -ForegroundColor Gray
Write-Host "   c) Update Workshop Change Notes from: docs/workshop_changenotes_v$Version.txt" -ForegroundColor Gray
Write-Host "   d) Update Collection Description from: docs/workshop_collection_description.bbcode" -ForegroundColor Gray
Write-Host ""
