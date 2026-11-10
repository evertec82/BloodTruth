# Unpacks a Blood & Truth .pkg into games/<serial>/files/uroot, where the launcher expects it.
# The unpacking itself is done by PkgTool (LibOrbisPkg, LGPL-3.0), run as a separate program.
# It only reads packages made from a dump of the game, which are not encrypted.

# A dumped package uses an all-zero passcode.
$zeroPasscode = '0' * 32

# Unpacking happens in this folder first, so a half-finished unpack is never mistaken for the game.
# It sits at the root of the drive with a short name on purpose: PkgTool cannot open a file whose
# whole path is 260 characters or longer, and some files inside the game have long paths.
$unpackFolder = 'tt-unpack'

function Format-Gigabytes([double]$bytes) {
    return ('{0:N1} GB' -f ($bytes / 1GB))
}

# Reads the content ID from the package header (e.g. "UP9000-CUSA11108_00-..."). Returns $null
# when the file is not a PlayStation 4 package.
function Read-PackageId([string]$path) {
    try {
        $head = New-Object byte[] 128
        $stream = [System.IO.File]::OpenRead($path)
        try { $read = $stream.Read($head, 0, $head.Length) } finally { $stream.Dispose() }
        if ($read -lt $head.Length) { return $null }
        # A PS4 package starts with the bytes 7F 43 4E 54 ("\x7fCNT").
        if ($head[0] -ne 0x7F -or $head[1] -ne 0x43 -or $head[2] -ne 0x4E -or $head[3] -ne 0x54) {
            return $null
        }
        # The content ID is a 36 character text at offset 0x40.
        return [System.Text.Encoding]::ASCII.GetString($head, 0x40, 36).Trim([char]0)
    } catch {
        return $null
    }
}

# Looks for a .pkg up to three folders deep and returns the largest one. A game's package is
# larger than its updates'. Leftovers of an unfinished unpack are skipped.
function Find-GamePackage([string]$folder) {
    if (-not (Test-Path -LiteralPath $folder)) { return $null }
    return Get-ChildItem -LiteralPath $folder -Filter '*.pkg' -File -Recurse -Depth 2 -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -notlike "*\$unpackFolder\*" } |
        Sort-Object Length -Descending |
        Select-Object -First 1
}

# PkgTool is looked for next to the launcher first, then in tools/pkgtool of the repository.
function Find-PkgTool([string]$launcherFolder, [string]$root) {
    foreach ($candidate in @((Join-Path $launcherFolder 'pkgtool/PkgTool.exe'),
                             (Join-Path $root 'tools/pkgtool/PkgTool.exe'))) {
        if (Test-Path -LiteralPath $candidate) { return $candidate }
    }
    return $null
}

# Free bytes on the drive that holds the path, or -1 when that cannot be found out (for example
# on a network path).
function Get-FreeSpace([string]$path) {
    try {
        $driveRoot = [System.IO.Path]::GetPathRoot([System.IO.Path]::GetFullPath($path))
        return (New-Object System.IO.DriveInfo($driveRoot)).AvailableFreeSpace
    } catch {
        return -1
    }
}

# Deletes a folder, including one with paths too long for the usual delete. The \\?\ prefix lifts
# the length limit; if the folder is still there, robocopy (which has no such limit) empties it.
function Remove-Folder([string]$path) {
    if (-not (Test-Path -LiteralPath $path)) { return }
    $full = [System.IO.Path]::GetFullPath($path)
    $long = $full
    if ($full -match '^[A-Za-z]:\\') { $long = '\\?\' + $full }
    Remove-Item -LiteralPath $long -Recurse -Force -ErrorAction SilentlyContinue
    if (Test-Path -LiteralPath $full) {
        $empty = Join-Path ([System.IO.Path]::GetTempPath()) 'truthxr-empty'
        New-Item -ItemType Directory -Path $empty -Force | Out-Null
        robocopy $empty $full /MIR /NFL /NDL /NJH /NJS /NC /NS /NP | Out-Null
        Remove-Item -LiteralPath $full -Recurse -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $empty -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# Unpacks the package and returns the path of the game's eboot.bin. Throws with a readable
# message when anything goes wrong.
function Expand-GamePackage {
    param([string]$PackagePath, [string]$GamesFolder, [string]$LauncherFolder, [string]$Root)

    $package = Get-Item -LiteralPath $PackagePath
    $contentId = Read-PackageId $package.FullName
    if ($null -eq $contentId) { throw "$($package.FullName) is not a PlayStation 4 package." }

    $tool = Find-PkgTool $LauncherFolder $Root
    if ($null -eq $tool) { throw 'PkgTool.exe is missing. It belongs in tools/pkgtool.' }

    # The game's folder is named after its serial (CUSA11108), taken from the content ID.
    $serial = 'game'
    if ($contentId -match '[A-Z]{4}[0-9]{5}') { $serial = $Matches[0] }
    $target = Join-Path $GamesFolder $serial

    # An unpacked game is larger than its package; twice the size is a safe guess.
    $needed = $package.Length * 2
    $freeBefore = Get-FreeSpace $target
    if ($freeBefore -ge 0 -and $freeBefore -lt $needed) {
        throw "Unpacking needs about $(Format-Gigabytes $needed) free. Only $(Format-Gigabytes $freeBefore) is available."
    }

    # The work folder is on the same drive as the game, so moving the result into place is a quick
    # rename and not a copy.
    $driveRoot = [System.IO.Path]::GetPathRoot([System.IO.Path]::GetFullPath($target))
    $work = Join-Path $driveRoot $unpackFolder
    # Leftovers of earlier attempts, including the old work folder inside the game's folder.
    Remove-Folder $work
    Remove-Folder (Join-Path $target '.unpacking')
    New-Item -ItemType Directory -Path $work -Force | Out-Null

    try {
        $errorFile = Join-Path $work 'pkgtool-errors.txt'
        $outputFile = Join-Path $work 'pkgtool-output.txt'
        $arguments = 'pkg_extract --passcode ' + $zeroPasscode + ' "' + $package.FullName + '" "' + (Join-Path $work 'files') + '"'
        $process = Start-Process -FilePath $tool -ArgumentList $arguments -PassThru -WindowStyle Hidden `
            -RedirectStandardOutput $outputFile -RedirectStandardError $errorFile
        # Reading the handle keeps the exit code available after the process ends.
        $null = $process.Handle

        try {
            # PkgTool reports no progress, so the amount written is worked out from the free space
            # lost on the drive.
            while (-not $process.HasExited) {
                Start-Sleep -Milliseconds 500
                if ($freeBefore -ge 0) {
                    $written = [math]::Max([long]0, [long]$freeBefore - [long](Get-FreeSpace $target))
                    Write-Progress -Activity 'Unpacking Blood & Truth' -Status ((Format-Gigabytes $written) + ' unpacked')
                }
            }
        } finally {
            # Reached early if the launcher is stopped (Ctrl+C): do not leave PkgTool running.
            if (-not $process.HasExited) { $process.Kill() }
            Write-Progress -Activity 'Unpacking Blood & Truth' -Completed
        }

        $unpacked = Join-Path $work 'files/uroot'
        if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath (Join-Path $unpacked 'eboot.bin'))) {
            $reason = ''
            $allErrors = ''
            if (Test-Path -LiteralPath $errorFile) {
                $reason = @(Get-Content -LiteralPath $errorFile | Where-Object { $_.Trim() -ne '' })[0]
                $allErrors = Get-Content -LiteralPath $errorFile -Raw
            }
            if ($allErrors -match 'PathTooLong') {
                throw "A file inside the package has a path that is too long. Move the TruthXR folder to a shorter path such as C:\TruthXR and try again. ($reason)"
            }
            throw "The package could not be unpacked. Only a package made from a dump of the game works; a package from the PlayStation Store is encrypted. $reason"
        }

        # Move the finished game into place, replacing anything an earlier attempt left there.
        $destination = Join-Path $target 'files/uroot'
        New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force | Out-Null
        if (Test-Path -LiteralPath $destination) { Remove-Item -LiteralPath $destination -Recurse -Force }
        Move-Item -LiteralPath $unpacked -Destination $destination

        # The package keeps some of the game's data (param.sfo, icon, trophies) outside its file
        # system. The emulator needs it in sce_sys, so each entry is pulled out by its index.
        # These entries are package bookkeeping and are not needed.
        $leftOut = @('DIGESTS', 'ENTRY_KEYS', 'IMAGE_KEY', 'GENERAL_DIGESTS', 'METAS',
                     'ENTRY_NAMES', 'LICENSE_DAT', 'LICENSE_INFO')
        foreach ($line in (& $tool pkg_listentries $package.FullName 2>$null)) {
            # A line of the listing: offset, size, flags, index, (id), name.
            if ($line -notmatch '^0x[0-9A-Fa-f]+\s+0x[0-9A-Fa-f]+\s+[0-9A-Fa-f]+\s+(\d+)\s+(?:\d+\s+)?([A-Z0-9_]+)\s*$') { continue }
            $index = $Matches[1]
            $name = $Matches[2]
            if ($leftOut -contains $name -or $name.EndsWith('_DDS')) { continue }
            # Entry names stand for paths: "__" is a folder separator and the last "_" is the
            # dot of the extension (ICON0_PNG becomes icon0.png). Playgo files use a dash.
            $file = $name.ToLower().Replace('__', '\')
            $at = $file.LastIndexOf('_')
            if ($at -gt 0) { $file = $file.Substring(0, $at) + '.' + $file.Substring($at + 1) }
            if ($file.StartsWith('playgo_')) { $file = 'playgo-' + $file.Substring(7) }
            $file = Join-Path (Join-Path $destination 'sce_sys') $file
            New-Item -ItemType Directory -Path (Split-Path $file -Parent) -Force | Out-Null
            & $tool pkg_extractentry --passcode $zeroPasscode $package.FullName $index $file 2>$null | Out-Null
        }

        # Without param.sfo the emulator cannot tell what game this is.
        if (-not (Test-Path -LiteralPath (Join-Path $destination 'sce_sys/param.sfo'))) {
            throw "The package was unpacked, but its game description (param.sfo) could not be read from it."
        }
        return (Join-Path $destination 'eboot.bin')
    } finally {
        # Runs on success and on failure: the temporary folder is never left behind.
        Remove-Folder $work
    }
}
