<#
.SYNOPSIS
    Packages all 5 Project Vigilance modular RimWorld packages for public release and distribution.

.DESCRIPTION
    Builds the Release configuration of Geoscape.slnx targeting .NET 10 (net10.0), stages each
    module cleanly (Core, Geoscape, Orbital, Aquatics, Thermal) free of source code and build
    artifacts, and generates both individual module ZIP archives and a consolidated Suite ZIP
    in the dist/ directory for GitHub Releases or manual installation.

.PARAMETER Version
    The release version tag (defaults to '0.4.0').

.PARAMETER OutputDir
    The output directory where staging and zip packages will be generated.
    Defaults to 'dist' in repository root.
#>

[CmdletBinding()]
param(
    [string]$Version = '0.4.0',
    [string]$OutputDir = 'dist'
)

$ErrorActionPreference = 'Stop'

Write-Host "==> [Project Vigilance] Starting modular suite packaging pipeline for v$Version..." -ForegroundColor Cyan

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$solutionFile = Join-Path $repoRoot 'Geoscape\Source\Geoscape.slnx'
$distPath = Join-Path $repoRoot $OutputDir
$suiteStagingRoot = Join-Path $distPath "ProjectVigilance-Suite-v$Version"
$suiteZipFile = Join-Path $distPath "ProjectVigilance-Suite-v$Version.zip"

if (-not (Test-Path $solutionFile)) {
    throw "Solution file not found at: $solutionFile"
}

$modules = @(
    @{ Source = 'Core';     PackageName = 'ProjectVigilance-Core';     AssemblyBase = 'ProjectVigilance.Core' }
    @{ Source = 'Geoscape'; PackageName = 'ProjectVigilance-Geoscape'; AssemblyBase = 'Geoscape' }
    @{ Source = 'Orbital';  PackageName = 'ProjectVigilance-Orbital';  AssemblyBase = 'ProjectVigilance.Orbital' }
    @{ Source = 'Aquatics'; PackageName = 'ProjectVigilance-Aquatics'; AssemblyBase = 'ProjectVigilance.Aquatics' }
    @{ Source = 'Thermal';  PackageName = 'ProjectVigilance-Thermal';  AssemblyBase = 'ProjectVigilance.Thermal' }
)

# 1. Handle active DLL locks if RimWorld is open
. (Join-Path $PSScriptRoot 'lib\ModDeployHelper.ps1')
foreach ($mod in $modules) {
    $targetDll = Join-Path $repoRoot "$($mod.Source)\1.7\Assemblies\$($mod.AssemblyBase).dll"
    Invoke-AssemblyLockRotation -AssemblyPath $targetDll
}

# 2. Build Release suite solution
Write-Host "--> Building Geoscape.slnx (Release configuration)..." -ForegroundColor Yellow
& dotnet build $solutionFile --configuration Release --nologo -v quiet
if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE."
}

# 3. Reset suite staging root
if (Test-Path $suiteStagingRoot) {
    Remove-Item -Path $suiteStagingRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $suiteStagingRoot -Force | Out-Null

function Copy-OptionalTree {
    param([string]$From, [string]$To)
    if (Test-Path $From) {
        New-Item -ItemType Directory -Path $To -Force | Out-Null
        Copy-Item -Path "$From\*" -Destination $To -Recurse -Force
    }
}

# 4. Stage each module and emit individual ZIP archives
foreach ($mod in $modules) {
    $srcDir = Join-Path $repoRoot $mod.Source
    $stagingPath = Join-Path $suiteStagingRoot $mod.PackageName
    $zipFile = Join-Path $distPath "$($mod.PackageName)-v$Version.zip"

    New-Item -ItemType Directory -Path $stagingPath -Force | Out-Null

    # About/ metadata & artwork
    $stagingAbout = Join-Path $stagingPath 'About'
    New-Item -ItemType Directory -Path $stagingAbout -Force | Out-Null
    Copy-Item (Join-Path $srcDir 'About\About.xml') -Destination (Join-Path $stagingAbout 'About.xml') -Force

    foreach ($asset in @('Preview.png', 'Icon.png', 'ModIcon.png', 'PublishedFileId.txt')) {
        $assetPath = Join-Path $srcDir "About\$asset"
        if (Test-Path $assetPath) {
            Copy-Item $assetPath -Destination (Join-Path $stagingAbout $asset) -Force
        }
    }

    # 1.7/Assemblies
    $staging17 = Join-Path $stagingPath '1.7'
    $stagingAssemblies = Join-Path $staging17 'Assemblies'
    New-Item -ItemType Directory -Path $stagingAssemblies -Force | Out-Null

    $dllSrc = Join-Path $srcDir "1.7\Assemblies\$($mod.AssemblyBase).dll"
    Copy-Item $dllSrc -Destination (Join-Path $stagingAssemblies "$($mod.AssemblyBase).dll") -Force

    $pdbSrc = Join-Path $srcDir "1.7\Assemblies\$($mod.AssemblyBase).pdb"
    if (Test-Path $pdbSrc) {
        Copy-Item $pdbSrc -Destination (Join-Path $stagingAssemblies "$($mod.AssemblyBase).pdb") -Force
    }

    # 1.7 content trees
    foreach ($sub in @('Defs', 'Patches', 'Textures', 'Languages', 'Sounds')) {
        Copy-OptionalTree -From (Join-Path $srcDir "1.7\$sub") -To (Join-Path $staging17 $sub)
    }

    # License & README
    Copy-Item (Join-Path $repoRoot 'LICENSE') -Destination (Join-Path $stagingPath 'LICENSE') -Force
    $modReadme = Join-Path $srcDir 'README.md'
    if (Test-Path $modReadme) {
        Copy-Item $modReadme -Destination (Join-Path $stagingPath 'README.md') -Force
    }

    if (Test-Path $zipFile) {
        Remove-Item $zipFile -Force
    }
    Compress-Archive -Path $stagingPath -DestinationPath $zipFile -CompressionLevel Optimal

    $zipSizeKb = [math]::Round((Get-Item $zipFile).Length / 1KB, 1)
    Write-Host "    Staged & zipped $($mod.PackageName): $zipFile ($zipSizeKb KB)" -ForegroundColor Gray
}

# 5. Create consolidated Suite ZIP archive containing all 5 mod folders
if (Test-Path $suiteZipFile) {
    Remove-Item $suiteZipFile -Force
}
Compress-Archive -Path "$suiteStagingRoot\*" -DestinationPath $suiteZipFile -CompressionLevel Optimal
$suiteZipSizeKb = [math]::Round((Get-Item $suiteZipFile).Length / 1KB, 1)

Write-Host "==> [Project Vigilance] Suite packaging complete!" -ForegroundColor Green
Write-Host "    Staging Directory: $suiteStagingRoot" -ForegroundColor Gray
Write-Host "    Consolidated ZIP:  $suiteZipFile ($suiteZipSizeKb KB)" -ForegroundColor Gray
