<#
.SYNOPSIS
    Packages all 5 Project Vigilance modular RimWorld packages for public release and distribution.

.DESCRIPTION
    Builds the Release configuration of Geoscape.slnx targeting .NET 10 (net10.0), maintains
    clean staging folders (dist/ProjectVigilance-<Module>/) free of Source/ and build caches,
    generates individual module ZIP archives and a consolidated Suite ZIP in dist/, and
    automatically switches the local RimWorld Mods/ junctions on Windows to clean Workshop staging.

.PARAMETER Version
    The release version tag (defaults to '0.4.1').

.PARAMETER OutputDir
    The output directory where staging and zip packages will be generated.
    Defaults to 'dist' in repository root.

.PARAMETER StageWorkshopJunction
    Optional backward-compatibility switch (Workshop junction staging is automatic on Windows
    when RimWorld's Mods directory exists unless -SkipJunctionUpdate is set).

.PARAMETER SkipJunctionUpdate
    Skips automatically switching the local RimWorld Mods/ junctions to clean Workshop
    staging after packaging.
#>

[CmdletBinding()]
param(
    [string]$Version = '0.4.1',
    [string]$OutputDir = 'dist',
    [switch]$StageWorkshopJunction,
    [switch]$SkipJunctionUpdate
)

$ErrorActionPreference = 'Stop'

Write-Host "==> [Project Vigilance] Starting modular suite packaging pipeline for v$Version..." -ForegroundColor Cyan

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$solutionFile = Join-Path $repoRoot 'Geoscape\Source\Geoscape.slnx'
$distPath = Join-Path $repoRoot $OutputDir
$suiteStagingRoot = Join-Path $distPath "ProjectVigilance-Suite-v$Version"
$suiteZipFile = Join-Path $distPath "ProjectVigilance-Suite-v$Version.zip"

if (-not (Test-Path -LiteralPath $solutionFile)) {
    throw "Solution file not found at: $solutionFile"
}

$modules = @(
    @{ Source = 'Core';     PackageName = 'ProjectVigilance-Core';     AssemblyBase = 'ProjectVigilance.Core' }
    @{ Source = 'Geoscape'; PackageName = 'ProjectVigilance-Geoscape'; AssemblyBase = 'Geoscape' }
    @{ Source = 'Orbital';  PackageName = 'ProjectVigilance-Orbital';  AssemblyBase = 'ProjectVigilance.Orbital' }
    @{ Source = 'Aquatics'; PackageName = 'ProjectVigilance-Aquatics'; AssemblyBase = 'ProjectVigilance.Aquatics' }
    @{ Source = 'Thermal';  PackageName = 'ProjectVigilance-Thermal';  AssemblyBase = 'ProjectVigilance.Thermal' }
)

# 1. Handle active DLL locks in both source trees and Workshop staging if RimWorld is open
. (Join-Path $PSScriptRoot 'lib\ModDeployHelper.ps1')
foreach ($mod in $modules) {
    $sourceDll = Join-Path $repoRoot "$($mod.Source)\1.7\Assemblies\$($mod.AssemblyBase).dll"
    Invoke-AssemblyLockRotation -AssemblyPath $sourceDll

    $stagedDll = Join-Path $distPath "$($mod.PackageName)\1.7\Assemblies\$($mod.AssemblyBase).dll"
    Invoke-AssemblyLockRotation -AssemblyPath $stagedDll
}

# 2. Build Release suite solution (automatically runs StageCleanDistribution via Directory.Build.targets)
Write-Host "--> Building Geoscape.slnx (Release configuration)..." -ForegroundColor Yellow
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
        (Join-Path $distPath "$($mod.PackageName)\1.7\Assemblies")
    )) {
        if (Test-Path -LiteralPath $asmDir) {
            Get-ChildItem -LiteralPath $asmDir -Filter '*.old' -File -ErrorAction SilentlyContinue |
                Remove-Item -Force -ErrorAction SilentlyContinue
        }
    }
}

# 3. Reset suite staging root
if (Test-Path -LiteralPath $suiteStagingRoot) {
    Remove-Item -LiteralPath $suiteStagingRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $suiteStagingRoot -Force | Out-Null

function Copy-OptionalTree {
    <#
    .SYNOPSIS
        Recursively copies an optional subdirectory tree if present.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$From,

        [Parameter(Mandatory = $true)]
        [string]$To
    )

    if (Test-Path -LiteralPath $From) {
        New-Item -ItemType Directory -Path $To -Force | Out-Null
        Copy-Item -Path "$From\*" -Destination $To -Recurse -Force
    }
}

function Copy-CleanModuleLayout {
    <#
    .SYNOPSIS
        Populates a clean distribution staging folder free of Source/, bin/, obj/, and caches.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [hashtable]$Module,

        [Parameter(Mandatory = $true)]
        [string]$DestinationRoot
    )

    $srcDir = Join-Path $repoRoot $Module.Source
    if (-not (Test-Path -LiteralPath $DestinationRoot)) {
        New-Item -ItemType Directory -Path $DestinationRoot -Force | Out-Null
    }

    # Preserve PublishedFileId.txt back to source tree if updated in live Workshop staging
    $destPublishedId = Join-Path $DestinationRoot 'About\PublishedFileId.txt'
    $srcPublishedId = Join-Path $srcDir 'About\PublishedFileId.txt'
    if ((Test-Path -LiteralPath $destPublishedId) -and -not (Test-Path -LiteralPath $srcPublishedId)) {
        Copy-Item -LiteralPath $destPublishedId -Destination $srcPublishedId -Force
    }

    # About/ metadata & artwork
    $stagingAbout = Join-Path $DestinationRoot 'About'
    New-Item -ItemType Directory -Path $stagingAbout -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $srcDir 'About\About.xml') -Destination (Join-Path $stagingAbout 'About.xml') -Force

    foreach ($asset in @('Preview.png', 'Icon.png', 'ModIcon.png', 'PublishedFileId.txt')) {
        $assetPath = Join-Path $srcDir "About\$asset"
        if (Test-Path -LiteralPath $assetPath) {
            Copy-Item -LiteralPath $assetPath -Destination (Join-Path $stagingAbout $asset) -Force
        }
    }

    # 1.7/Assemblies
    $staging17 = Join-Path $DestinationRoot '1.7'
    $stagingAssemblies = Join-Path $staging17 'Assemblies'
    New-Item -ItemType Directory -Path $stagingAssemblies -Force | Out-Null

    $dllSrc = Join-Path $srcDir "1.7\Assemblies\$($Module.AssemblyBase).dll"
    Copy-Item -LiteralPath $dllSrc -Destination (Join-Path $stagingAssemblies "$($Module.AssemblyBase).dll") -Force

    $pdbSrc = Join-Path $srcDir "1.7\Assemblies\$($Module.AssemblyBase).pdb"
    if (Test-Path -LiteralPath $pdbSrc) {
        Copy-Item -LiteralPath $pdbSrc -Destination (Join-Path $stagingAssemblies "$($Module.AssemblyBase).pdb") -Force
    }

    # 1.7 content trees
    foreach ($sub in @('Defs', 'Patches', 'Textures', 'Languages', 'Sounds')) {
        Copy-OptionalTree -From (Join-Path $srcDir "1.7\$sub") -To (Join-Path $staging17 $sub)
    }

    # License & README
    Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSE') -Destination (Join-Path $DestinationRoot 'LICENSE') -Force
    $modReadme = Join-Path $srcDir 'README.md'
    if (Test-Path -LiteralPath $modReadme) {
        Copy-Item -LiteralPath $modReadme -Destination (Join-Path $DestinationRoot 'README.md') -Force
    }
}

# 4. Stage each module (both live dist/<PackageName> and suite staging) and emit individual ZIP archives
foreach ($mod in $modules) {
    $liveStagingPath = Join-Path $distPath $mod.PackageName
    $suiteModPath = Join-Path $suiteStagingRoot $mod.PackageName
    $zipFile = Join-Path $distPath "$($mod.PackageName)-v$Version.zip"

    Copy-CleanModuleLayout -Module $mod -DestinationRoot $liveStagingPath
    Copy-CleanModuleLayout -Module $mod -DestinationRoot $suiteModPath

    if (Test-Path -LiteralPath $zipFile) {
        Remove-Item -LiteralPath $zipFile -Force
    }
    Compress-Archive -Path $suiteModPath -DestinationPath $zipFile -CompressionLevel Optimal

    $zipSizeKb = [math]::Round((Get-Item -LiteralPath $zipFile).Length / 1KB, 1)
    Write-Host "    Staged & zipped $($mod.PackageName): $zipFile ($zipSizeKb KB)" -ForegroundColor Gray
}

# 5. Create consolidated Suite ZIP archive containing all 5 mod folders
if (Test-Path -LiteralPath $suiteZipFile) {
    Remove-Item -LiteralPath $suiteZipFile -Force
}
Compress-Archive -Path "$suiteStagingRoot\*" -DestinationPath $suiteZipFile -CompressionLevel Optimal
$suiteZipSizeKb = [math]::Round((Get-Item -LiteralPath $suiteZipFile).Length / 1KB, 1)

Write-Host "==> [Project Vigilance] Suite packaging complete!" -ForegroundColor Green
Write-Host "    Staging Directory: $suiteStagingRoot" -ForegroundColor Gray
Write-Host "    Consolidated ZIP:  $suiteZipFile ($suiteZipSizeKb KB)" -ForegroundColor Gray

$defaultModsDir = 'D:\Games\Steam\steamapps\common\RimWorld\Mods'
if ($StageWorkshopJunction -or (-not $SkipJunctionUpdate -and (Test-Path -LiteralPath $defaultModsDir))) {
    Write-Host "--> Switching RimWorld Mods/ junctions to clean Workshop staging..." -ForegroundColor Yellow
    $switchJunctionScript = Join-Path $PSScriptRoot 'switch-mod-junction.ps1'
    & $switchJunctionScript -Mode Workshop
}
