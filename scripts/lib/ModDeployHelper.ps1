<#
.SYNOPSIS
    Shared helper functions for Project Vigilance deployment and packaging automation.

.DESCRIPTION
    Provides reusable routines for file-lock detection, assembly rotation, and environment validation.
    Idempotent and safe to run across Windows host and CI environments.
#>

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

function Invoke-AssemblyLockRotation {
    <#
    .SYNOPSIS
        Detects if an assembly DLL is locked by a running process (e.g. RimWorld) and rotates it to .old.
    .PARAMETER AssemblyPath
        The full path to the target assembly DLL to check and rotate if locked.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$AssemblyPath
    )

    $oldDll = "$AssemblyPath.old"
    if (Test-Path -LiteralPath $oldDll) {
        Remove-Item -LiteralPath $oldDll -Force -ErrorAction SilentlyContinue
    }

    if (Test-Path -LiteralPath $AssemblyPath) {
        try {
            $fileStream = [System.IO.File]::Open($AssemblyPath, [System.IO.FileMode]::Open, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)
            $fileStream.Close()
            $fileStream.Dispose()
        } catch {
            $leafName = Split-Path $AssemblyPath -Leaf
            Write-Host "--> Active file lock detected on $leafName (RimWorld running). Rotating locked assembly..." -ForegroundColor Yellow
            Move-Item -LiteralPath $AssemblyPath -Destination $oldDll -Force
        }
    }
}
