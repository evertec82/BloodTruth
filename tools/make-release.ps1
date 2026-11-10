# Builds the TruthXR release: one standalone TruthXR-<version>.exe that carries the emulator,
# its helpers, PkgTool and the licences, and installs them into %LOCALAPPDATA%\TruthXR when a
# player starts it. Players then point it at their own copy of the game.
#
# Example:
#   powershell -ExecutionPolicy Bypass -File tools\make-release.ps1 -ExePath Build\x64-Clang-Release\shadps4.exe
#
# The result is written next to the repository (not inside it) so it is never committed by accident.
param(
    [Parameter(Mandatory = $true)][string]$ExePath,
    [string]$Version = '0.1.5',
    [string]$OutDir
)
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path $PSScriptRoot -Parent
if (-not $OutDir) { $OutDir = Join-Path (Split-Path $repoRoot -Parent) 'TruthXR-release' }
if (-not (Test-Path -LiteralPath $ExePath)) { throw "The emulator was not found: $ExePath" }

# A release should be made from committed files, so the commit ID written down is true.
$dirty = git -C $repoRoot status --porcelain
if ($dirty) {
    Write-Warning 'There are uncommitted changes. Commit them first, or BUILD.txt will name a commit that does not match the files.'
}
$commit = (git -C $repoRoot rev-parse HEAD).Trim()

New-Item -ItemType Directory -Path $OutDir -Force | Out-Null
$exe = Join-Path $OutDir "TruthXR-$Version.exe"
if (Test-Path -LiteralPath $exe) { Remove-Item -LiteralPath $exe -Force }

& (Join-Path $PSScriptRoot 'make-launcher.ps1') -Version $Version -Emulator $ExePath -OutPath $exe

# BUILD.txt records exactly what this release was made from; it goes next to the exe for the
# release page, not inside it.
$emulatorHash = (Get-FileHash -LiteralPath $ExePath -Algorithm SHA256).Hash.ToLower()
$launcherHash = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash.ToLower()
$buildText = @(
    "TruthXR $Version",
    "Source commit: $commit",
    "Upstream base: e9f2c54c207ad6d789d898b3a691aa4b34a59c96",
    "Emulator (shadps4.exe) SHA256: $emulatorHash",
    "TruthXR-$Version.exe SHA256: $launcherHash"
) -join "`r`n"
[System.IO.File]::WriteAllText((Join-Path $OutDir "BUILD-$Version.txt"), $buildText + "`r`n", [System.Text.Encoding]::ASCII)

Write-Host "Release: $exe"
Write-Host "SHA256:  $launcherHash"
