# Builds TruthXR.exe, the launcher, from bloodtruth-launcher/app with the C# compiler that ships
# with Windows (.NET Framework 4). Nothing has to be installed for it.
#
# With -Emulator, the exe is standalone: the emulator, its helpers, PkgTool and the licences are
# packed inside it, and it unpacks them into %LOCALAPPDATA%\TruthXR on its first start (and when
# a new version is started). This is the one to give to players:
#   powershell -ExecutionPolicy Bypass -File tools\make-launcher.ps1 -Emulator Build\x64-Clang-Release\shadps4.exe
#
# Without -Emulator, it is a small launcher for development that uses a pc-vr folder next to it
# (or the pc-vr folder it is put in).
#
# The exe goes to Build\launcher\TruthXR.exe unless -OutPath says otherwise.
param(
    [string]$Version = '0.2.0',
    [string]$Emulator,
    [string]$OutPath
)
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path $PSScriptRoot -Parent
$source = Join-Path $repoRoot 'bloodtruth-launcher/app'
if (-not $OutPath) { $OutPath = Join-Path $repoRoot 'Build/launcher/TruthXR.exe' }
$OutPath = [System.IO.Path]::GetFullPath($OutPath)

# The 64-bit compiler first; every Windows 10 and 11 PC has at least one of the two.
$csc = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if (-not (Test-Path -LiteralPath $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET/Framework/v4.0.30319/csc.exe' }
if (-not (Test-Path -LiteralPath $csc)) { throw 'The C# compiler that comes with Windows (.NET Framework 4) was not found.' }

$work = Join-Path ([System.IO.Path]::GetTempPath()) ('truthxr-build-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work -Force | Out-Null
try {
    # The version shown in the launcher comes from here, so it always matches the release.
    $buildInfo = Join-Path $work 'build_info.cs'
    $buildInfoText = "namespace TruthXR.Launcher {`r`nstatic class BuildInfo {`r`n    public const string version = `"$Version`";`r`n}`r`n}`r`n"
    [System.IO.File]::WriteAllText($buildInfo, $buildInfoText, [System.Text.Encoding]::ASCII)

    $resources = @()
    if ($Emulator) {
        if (-not (Test-Path -LiteralPath $Emulator)) { throw "The emulator was not found: $Emulator" }

        # The files the player's install folder gets, laid out as they will be unpacked.
        $stage = Join-Path $work 'payload'
        $gameFolder = Join-Path $stage 'pc-vr'
        New-Item -ItemType Directory -Path $gameFolder -Force | Out-Null
        Copy-Item -LiteralPath $Emulator -Destination (Join-Path $gameFolder 'shadps4.exe')
        $launcher = Join-Path $repoRoot 'bloodtruth-launcher'
        foreach ($file in @('set-native-resolution.ps1', 'config-default.json', 'users-default.json',
                            'launch-bloodtruth.ps1', 'expand-package.ps1')) {
            Copy-Item -LiteralPath (Join-Path $launcher $file) -Destination $gameFolder
        }
        Copy-Item -LiteralPath (Join-Path $repoRoot 'tools/pkgtool') -Destination (Join-Path $gameFolder 'pkgtool') -Recurse
        Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSE') -Destination $stage
        Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSES') -Destination $stage -Recurse
        Copy-Item -LiteralPath (Join-Path $repoRoot 'externals/openxr-sdk/LICENSE') -Destination (Join-Path $stage 'OPENXR-LICENSE')
        Copy-Item -LiteralPath (Join-Path $repoRoot 'README.md') -Destination $stage

        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $zip = Join-Path $work 'payload.zip'
        [System.IO.Compression.ZipFile]::CreateFromDirectory($stage, $zip, [System.IO.Compression.CompressionLevel]::Optimal, $false)

        # The stamp tells an install apart from the next one: version plus the ZIP's hash.
        $hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.Substring(0, 16).ToLower()
        $stamp = Join-Path $work 'payload.stamp'
        [System.IO.File]::WriteAllText($stamp, "$Version-$hash", [System.Text.Encoding]::ASCII)

        $resources = @("/resource:$zip,TruthXR.payload.zip", "/resource:$stamp,TruthXR.payload.stamp")
        Write-Host ("Packed the emulator into the launcher ({0:N1} MB)." -f ((Get-Item -LiteralPath $zip).Length / 1MB))
    } else {
        Write-Warning 'No -Emulator given: this TruthXR.exe needs a pc-vr folder next to it (development only).'
    }

    New-Item -ItemType Directory -Path (Split-Path $OutPath -Parent) -Force | Out-Null
    $files = @(Get-ChildItem -LiteralPath $source -Filter '*.cs' | ForEach-Object { $_.FullName }) + $buildInfo
    $arguments = @(
        '/nologo', '/target:winexe', '/optimize+', '/platform:anycpu', '/codepage:65001',
        "/out:$OutPath",
        '/reference:System.dll', '/reference:System.Drawing.dll', '/reference:System.Windows.Forms.dll',
        '/reference:System.IO.Compression.dll'
    ) + $resources + $files

    & $csc @arguments
    $code = $LASTEXITCODE
    if ($code -ne 0) { throw "The launcher did not compile (csc exit code $code). The errors are listed above." }
    Write-Host "Launcher built: $OutPath"
} finally {
    Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
}
