<#
.SYNOPSIS
    Generates public API reference assemblies (stubs) for RimWorld 1.7 Waylith core assemblies.

.DESCRIPTION
    Uses JetBrains Refasmer to strip private members and implementation bytecode from
    RimWorld core assemblies (Assembly-CSharp.dll, RimWorld.dll, UnityStubs.dll, Waylith.UnityCompat.dll)
    and places them into lib/ref along with 0Harmony.dll and CommunityToolkit.HighPerformance.dll.
    These reference assemblies allow building and CI verification without distributing proprietary game code.

.PARAMETER RimWorldPath
    Path to the RimWorld game directory containing Assembly-CSharp.dll.
    Defaults to Steam install location.

.PARAMETER HarmonyPath
    Path to 0Harmony.dll. Defaults to Steam Workshop location.

.PARAMETER OutputDir
    Destination folder for reference assemblies. Defaults to 'lib/ref'.
#>

[CmdletBinding()]
param(
    [string]$RimWorldPath,
    [string]$HarmonyPath,
    [string]$OutputDir
)

$ErrorActionPreference = 'Stop'

$isUnix = $IsLinux -or $IsMacOS
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')

if (-not $RimWorldPath) {
    if ($isUnix) {
        $flatpakPath = Join-Path $HOME '.var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/common/RimWorld'
        $standardPath = Join-Path $HOME '.local/share/Steam/steamapps/common/RimWorld'
        $RimWorldPath = if (Test-Path $flatpakPath) { $flatpakPath } else { $standardPath }
    } else {
        $RimWorldPath = 'D:\Games\Steam\steamapps\common\RimWorld'
    }
}

if (-not $HarmonyPath) {
    if ($isUnix) {
        $flatpakHarmony = Join-Path $HOME '.var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/workshop/content/294100/2009463077/1.7/Assemblies/0Harmony.dll'
        $standardHarmony = Join-Path $HOME '.local/share/Steam/steamapps/workshop/content/294100/2009463077/1.7/Assemblies/0Harmony.dll'
        $HarmonyPath = if (Test-Path $flatpakHarmony) { $flatpakHarmony } else { $standardHarmony }
    } else {
        $HarmonyPath = 'D:\Games\Steam\steamapps\workshop\content\294100\2009463077\1.7\Assemblies\0Harmony.dll'
    }
}

if (-not $OutputDir) {
    $OutputDir = Join-Path (Join-Path $repoRoot 'lib') 'ref'
}

Write-Host "==> [Project Vigilance] Generating reference assemblies..." -ForegroundColor Cyan
Write-Host "    RimWorld Path : $RimWorldPath"
Write-Host "    Harmony Path  : $HarmonyPath"
Write-Host "    Output Dir    : $OutputDir"

if (-not (Test-Path $RimWorldPath)) {
    throw "RimWorld installation path not found: $RimWorldPath"
}

$requiredDlls = @(
    'Assembly-CSharp.dll',
    'RimWorld.dll',
    'UnityStubs.dll',
    'Waylith.UnityCompat.dll'
)

foreach ($dll in $requiredDlls) {
    $path = Join-Path $RimWorldPath $dll
    if (-not (Test-Path $path)) {
        throw "Required game assembly missing: $path"
    }
}

# Ensure refasmer tool is installed
if (-not (Get-Command 'refasmer' -ErrorAction SilentlyContinue)) {
    Write-Host "--> 'refasmer' tool not found. Installing jetbrains.refasmer.clitool globally..." -ForegroundColor Yellow
    & dotnet tool install --global jetbrains.refasmer.clitool
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to install refasmer dotnet tool."
    }
}

if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

$targetAssemblies = $requiredDlls | ForEach-Object { Join-Path $RimWorldPath $_ }

Write-Host "--> Stripping public API metadata with refasmer..." -ForegroundColor Yellow
& refasmer -v -O "$OutputDir" --omit-non-api-members=true $targetAssemblies
if ($LASTEXITCODE -ne 0) {
    throw "refasmer generation failed with exit code $LASTEXITCODE."
}

# Copy auxiliary dependencies
$auxAssemblies = @(
    @{ Source = Join-Path $RimWorldPath 'CommunityToolkit.HighPerformance.dll'; Name = 'CommunityToolkit.HighPerformance.dll' }
)

if (Test-Path $HarmonyPath) {
    $auxAssemblies += @{ Source = $HarmonyPath; Name = '0Harmony.dll' }
} else {
    Write-Warning "0Harmony.dll not found at '$HarmonyPath'. If needed, install Harmony workshop mod or copy manually."
}

foreach ($aux in $auxAssemblies) {
    if (Test-Path $aux.Source) {
        $dest = Join-Path $OutputDir $aux.Name
        Copy-Item -Path $aux.Source -Destination $dest -Force
        Write-Host "    Copied: $($aux.Name)" -ForegroundColor Green
    }
}

Write-Host "==> [Project Vigilance] Reference assemblies successfully generated in: $OutputDir" -ForegroundColor Green
