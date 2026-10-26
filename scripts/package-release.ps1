<#
.SYNOPSIS
    Packages the Project Vigilance: Overland Incursions mod for public release and distribution.

.DESCRIPTION
    Builds the Release configuration of Geoscape targeting .NET 10 (net10.0), creates
    a clean staging directory free of source code and build artifacts, and produces
    a versioned distribution ZIP archive in the dist/ directory for GitHub Releases
    or manual RimWorld mod installation.

.PARAMETER Version
    The release version tag (defaults to '0.3.0').

.PARAMETER OutputDir
    The output directory where staging and zip packages will be generated.
    Defaults to 'dist' in repository root.
#>

[CmdletBinding()]
param(
    [string]$Version = '0.3.0',
    [string]$OutputDir = 'dist'
)

$ErrorActionPreference = 'Stop'

Write-Host "==> [Project Vigilance] Starting release packaging pipeline for v$Version..." -ForegroundColor Cyan

# 1. Resolve paths
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$geoscapeDir = Join-Path $repoRoot 'Geoscape'
$solutionFile = Join-Path $geoscapeDir 'Source\Geoscape.slnx'
$distPath = Join-Path $repoRoot $OutputDir
$modPackageName = "ProjectVigilance-OverlandIncursions"
$stagingPath = Join-Path $distPath $modPackageName
$zipFile = Join-Path $distPath "$modPackageName-v$Version.zip"

if (-not (Test-Path $solutionFile)) {
    throw "Solution file not found at: $solutionFile"
}

# 2. Handle active DLL locks if RimWorld is open
. (Join-Path $PSScriptRoot 'lib\ModDeployHelper.ps1')
$targetDll = Join-Path $geoscapeDir '1.7\Assemblies\Geoscape.dll'
Invoke-AssemblyLockRotation -AssemblyPath $targetDll

# 3. Build Release configuration
Write-Host "--> Building Geoscape (Release configuration)..." -ForegroundColor Yellow
& dotnet build $solutionFile --configuration Release --nologo -v quiet
if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE."
}

# 4. Clean and prepare staging directory
Write-Host "--> Preparing clean staging directory at: $stagingPath" -ForegroundColor Yellow
if (Test-Path $stagingPath) {
    Remove-Item -Path $stagingPath -Recurse -Force
}
New-Item -ItemType Directory -Path $stagingPath -Force | Out-Null

# 5. Copy About/ metadata and preview image
$stagingAbout = Join-Path $stagingPath 'About'
New-Item -ItemType Directory -Path $stagingAbout -Force | Out-Null
Copy-Item (Join-Path $geoscapeDir 'About\About.xml') -Destination (Join-Path $stagingAbout 'About.xml') -Force
$previewPng = Join-Path $geoscapeDir 'About\Preview.png'
if (Test-Path $previewPng) {
    Copy-Item $previewPng -Destination (Join-Path $stagingAbout 'Preview.png') -Force
} else {
    Write-Warning "Preview.png not found in Geoscape/About/! Steam Workshop requires a preview thumbnail."
}

$publishedFileId = Join-Path $geoscapeDir 'About\PublishedFileId.txt'
if (Test-Path $publishedFileId) {
    Copy-Item $publishedFileId -Destination (Join-Path $stagingAbout 'PublishedFileId.txt') -Force
}

# 6. Copy 1.7 distribution files (Assemblies and Defs)
$staging17 = Join-Path $stagingPath '1.7'
$stagingAssemblies = Join-Path $staging17 'Assemblies'
$stagingDefs = Join-Path $staging17 'Defs'
$stagingPatches = Join-Path $staging17 'Patches'
New-Item -ItemType Directory -Path $stagingAssemblies -Force | Out-Null
New-Item -ItemType Directory -Path $stagingDefs -Force | Out-Null
New-Item -ItemType Directory -Path $stagingPatches -Force | Out-Null

Copy-Item (Join-Path $geoscapeDir '1.7\Assemblies\Geoscape.dll') -Destination (Join-Path $stagingAssemblies 'Geoscape.dll') -Force
$pdbPath = Join-Path $geoscapeDir '1.7\Assemblies\Geoscape.pdb'
if (Test-Path $pdbPath) {
    Copy-Item $pdbPath -Destination (Join-Path $stagingAssemblies 'Geoscape.pdb') -Force
}

$sourceDefs = Join-Path $geoscapeDir '1.7\Defs'
if (Test-Path $sourceDefs) {
    Copy-Item -Path "$sourceDefs\*" -Destination $stagingDefs -Recurse -Force
}

$sourcePatches = Join-Path $geoscapeDir '1.7\Patches'
if (Test-Path $sourcePatches) {
    Copy-Item -Path "$sourcePatches\*" -Destination $stagingPatches -Recurse -Force
}

# 7. Copy License and Mod README
Copy-Item (Join-Path $repoRoot 'LICENSE') -Destination (Join-Path $stagingPath 'LICENSE') -Force
Copy-Item (Join-Path $geoscapeDir 'README.md') -Destination (Join-Path $stagingPath 'README.md') -Force

# 8. Create distribution ZIP archive
Write-Host "--> Compressing distribution package: $zipFile" -ForegroundColor Yellow
if (Test-Path $zipFile) {
    Remove-Item $zipFile -Force
}

Compress-Archive -Path $stagingPath -DestinationPath $zipFile -CompressionLevel Optimal

$zipItem = Get-Item $zipFile
$zipSizeKb = [math]::Round($zipItem.Length / 1KB, 1)

Write-Host "==> [Project Vigilance] Packaging complete!" -ForegroundColor Green
Write-Host "    Staging Directory: $stagingPath" -ForegroundColor Gray
Write-Host "    Distribution ZIP:  $zipFile ($zipSizeKb KB)" -ForegroundColor Gray
