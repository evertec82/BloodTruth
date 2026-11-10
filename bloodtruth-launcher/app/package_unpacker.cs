using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace TruthXR.Launcher {

// An error with a message meant for the player.
class LauncherException : Exception {
    public LauncherException(string message) : base(message) {
    }
}

// Unpacks a Blood & Truth .pkg into games/<serial>/files/uroot, where the launcher expects it.
// The work is done by PkgTool (LibOrbisPkg, LGPL-3.0), run as a separate program. It only reads
// packages made from a dump of the game, which are not encrypted.
class PackageUnpacker {
    // A dumped package uses an all-zero passcode.
    static readonly string zero_passcode = new string('0', 32);
    // Unpacking happens in this folder at the root of the drive first. It is short on purpose:
    // PkgTool cannot open a file whose whole path is 260 characters or longer.
    const string work_folder_name = "tt-unpack";
    // Package entries that are bookkeeping of the package itself, not part of the game.
    static readonly string[] left_out_entries = {
        "DIGESTS", "ENTRY_KEYS", "IMAGE_KEY", "GENERAL_DIGESTS",
        "METAS",   "ENTRY_NAMES", "LICENSE_DAT", "LICENSE_INFO",
    };
    // One line of "pkg_listentries": offset, size, flags, index, (id), name.
    static readonly Regex entry_line = new Regex(
        @"^0x[0-9A-Fa-f]+\s+0x[0-9A-Fa-f]+\s+[0-9A-Fa-f]+\s+(\d+)\s+(?:\d+\s+)?([A-Z0-9_]+)\s*$",
        RegexOptions.IgnoreCase);

    readonly string package_path;
    readonly string games_folder;
    readonly string pkgtool;
    volatile bool cancelled;
    Process current_process;
    readonly object process_lock = new object();

    public PackageUnpacker(string package_path, string games_folder, string pkgtool) {
        this.package_path = package_path;
        this.games_folder = games_folder;
        this.pkgtool = pkgtool;
    }

    // Called with (what is happening, bytes written so far or -1 when unknown).
    public Action<string, long> on_progress;

    public long PackageSize {
        get { return new FileInfo(package_path).Length; }
    }

    // Unpacking takes roughly the package's size; twice that leaves room for safety.
    public long SpaceNeeded {
        get { return PackageSize * 2; }
    }

    public string TargetFolder {
        get { return Path.Combine(games_folder, SerialOf(ReadPackageId(package_path))); }
    }

    public void Cancel() {
        cancelled = true;
        lock (process_lock) {
            if (current_process != null) {
                try {
                    current_process.Kill();
                } catch (Exception) {
                }
            }
        }
    }

    // Reads the content ID from the package header (e.g. "UP9000-CUSA11108_00-..."). Returns
    // null when the file is not a PlayStation 4 package.
    public static string ReadPackageId(string path) {
        try {
            byte[] head = new byte[128];
            int read;
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                                                      FileShare.ReadWrite)) {
                read = stream.Read(head, 0, head.Length);
            }
            if (read < head.Length) {
                return null;
            }
            // A PS4 package starts with the bytes 7F 43 4E 54 ("\x7fCNT").
            if (head[0] != 0x7F || head[1] != 0x43 || head[2] != 0x4E || head[3] != 0x54) {
                return null;
            }
            // The content ID is a 36 character text at offset 0x40.
            return Encoding.ASCII.GetString(head, 0x40, 36).TrimEnd('\0');
        } catch (Exception) {
            return null;
        }
    }

    static string SerialOf(string content_id) {
        if (content_id != null) {
            Match match = Regex.Match(content_id, "[A-Z]{4}[0-9]{5}");
            if (match.Success) {
                return match.Value;
            }
        }
        return "game";
    }

    static long FreeSpace(string path) {
        try {
            string root = Path.GetPathRoot(Path.GetFullPath(path));
            return new DriveInfo(root).AvailableFreeSpace;
        } catch (Exception) {
            return -1;
        }
    }

    void Report(string what, long bytes) {
        Action<string, long> handler = on_progress;
        if (handler != null) {
            handler(what, bytes);
        }
    }

    // Unpacks the package and returns the path of the game's eboot.bin. Throws a
    // LauncherException with a message for the player when something goes wrong, and an
    // OperationCanceledException when Cancel() was called.
    public string Run() {
        if (pkgtool == null || !File.Exists(pkgtool)) {
            throw new LauncherException("PkgTool, which unpacks packages, is missing from " +
                                        "pc-vr\\pkgtool. Extract the whole release ZIP again.");
        }
        string content_id = ReadPackageId(package_path);
        if (content_id == null) {
            throw new LauncherException("This file is not a PlayStation 4 package:\n" +
                                        package_path);
        }

        string target = Path.Combine(games_folder, SerialOf(content_id));
        long free_before = FreeSpace(target);
        if (free_before >= 0 && free_before < SpaceNeeded) {
            throw new LauncherException("Unpacking needs about " + Gigabytes(SpaceNeeded) +
                                        " of free space, and the drive has " +
                                        Gigabytes(free_before) + ". Make room, or move the " +
                                        "whole launcher folder to a drive with more space.");
        }

        string drive_root = Path.GetPathRoot(Path.GetFullPath(target));
        string work = Path.Combine(drive_root, work_folder_name);
        Report("Getting ready", -1);
        DeleteFolder(work);
        DeleteFolder(Path.Combine(target, ".unpacking"));
        Directory.CreateDirectory(work);

        try {
            StringBuilder errors = new StringBuilder();
            string files_folder = Path.Combine(work, "files");
            int exit_code = RunPkgTool("pkg_extract --passcode " + zero_passcode + " " +
                                           Quote(package_path) + " " + Quote(files_folder),
                                       null, errors, free_before, target);
            CheckCancelled();

            string unpacked = Path.Combine(files_folder, "uroot");
            if (exit_code != 0 || !File.Exists(Path.Combine(unpacked, "eboot.bin"))) {
                string all_errors = errors.ToString();
                string first_error = FirstLine(all_errors);
                if (all_errors.IndexOf("PathTooLong", StringComparison.OrdinalIgnoreCase) >= 0) {
                    throw new LauncherException(
                        "A file inside the package has a path that is too long for PkgTool. " +
                        "Move the launcher folder to a short path such as C:\\TruthXR and " +
                        "try again.\n\n(" + first_error + ")");
                }
                throw new LauncherException(
                    "The package could not be unpacked. Only a package made from a dump of " +
                    "the game works; a package from the PlayStation Store is encrypted." +
                    (first_error.Length > 0 ? "\n\n(PkgTool: " + first_error + ")" : ""));
            }

            // Move the finished game into place, replacing anything an earlier attempt left.
            Report("Moving the game into place", -1);
            string destination = Path.Combine(target, "files\\uroot");
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            DeleteFolder(destination);
            Directory.Move(unpacked, destination);

            // The package keeps some of the game's data (param.sfo, icon, trophies) outside its
            // file system. The emulator needs it in sce_sys, so each entry is pulled out by its
            // index.
            Report("Copying the game's description", -1);
            ExtractSystemEntries(Path.Combine(destination, "sce_sys"));

            // Without param.sfo the emulator cannot tell what game this is.
            if (!File.Exists(Path.Combine(destination, "sce_sys\\param.sfo"))) {
                throw new LauncherException("The package was unpacked, but its game " +
                                            "description (param.sfo) could not be read from it.");
            }
            return Path.Combine(destination, "eboot.bin");
        } finally {
            // On success and on failure: the work folder is never left behind.
            DeleteFolder(work);
        }
    }

    void ExtractSystemEntries(string sce_sys) {
        List<string> listing = new List<string>();
        RunPkgTool("pkg_listentries " + Quote(package_path), listing, new StringBuilder(), -1,
                   null);
        foreach (string line in listing) {
            CheckCancelled();
            Match match = entry_line.Match(line);
            if (!match.Success) {
                continue;
            }
            string index = match.Groups[1].Value;
            string name = match.Groups[2].Value.ToUpperInvariant();
            if (Array.IndexOf(left_out_entries, name) >= 0 || name.EndsWith("_DDS")) {
                continue;
            }
            // Entry names stand for paths: "__" is a folder separator and the last "_" is the
            // dot of the extension (ICON0_PNG becomes icon0.png). Playgo files use a dash.
            string file = name.ToLowerInvariant().Replace("__", "\\");
            int at = file.LastIndexOf('_');
            if (at > 0) {
                file = file.Substring(0, at) + "." + file.Substring(at + 1);
            }
            if (file.StartsWith("playgo_")) {
                file = "playgo-" + file.Substring(7);
            }
            string output = Path.Combine(sce_sys, file);
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            RunPkgTool("pkg_extractentry --passcode " + zero_passcode + " " +
                           Quote(package_path) + " " + index + " " + Quote(output),
                       null, new StringBuilder(), -1, null);
        }
    }

    void CheckCancelled() {
        if (cancelled) {
            throw new OperationCanceledException();
        }
    }

    // Runs PkgTool hidden and waits for it. While it runs, the progress is worked out from the
    // free space lost on the drive, because PkgTool itself reports none.
    int RunPkgTool(string arguments, List<string> output, StringBuilder errors, long free_before,
                   string measured_path) {
        CheckCancelled();
        ProcessStartInfo start = new ProcessStartInfo(pkgtool, arguments);
        start.UseShellExecute = false;
        start.CreateNoWindow = true;
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        start.WorkingDirectory = Path.GetDirectoryName(pkgtool);

        object lines_lock = new object();
        using (Process process = new Process()) {
            process.StartInfo = start;
            process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e) {
                if (e.Data != null && output != null) {
                    lock (lines_lock) {
                        output.Add(e.Data);
                    }
                }
            };
            process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e) {
                if (e.Data != null) {
                    lock (lines_lock) {
                        errors.AppendLine(e.Data);
                    }
                }
            };
            process.Start();
            lock (process_lock) {
                current_process = process;
            }
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            try {
                while (!process.WaitForExit(500)) {
                    if (cancelled) {
                        try {
                            process.Kill();
                        } catch (Exception) {
                        }
                    } else if (measured_path != null) {
                        long written = -1;
                        if (free_before >= 0) {
                            long free_now = FreeSpace(measured_path);
                            written = free_now >= 0 ? Math.Max(0L, free_before - free_now) : -1;
                        }
                        Report("Unpacking the game files", written);
                    }
                }
                // Lets the output readers finish.
                process.WaitForExit();
            } finally {
                lock (process_lock) {
                    current_process = null;
                }
            }
            return process.ExitCode;
        }
    }

    static string Quote(string value) {
        return "\"" + value + "\"";
    }

    static string FirstLine(string text) {
        foreach (string line in text.Split('\n')) {
            if (line.Trim().Length > 0) {
                return line.Trim();
            }
        }
        return "";
    }

    public static string Gigabytes(long bytes) {
        return (bytes / 1073741824.0).ToString("0.0") + " GB";
    }

    // Deletes a folder, including one with paths too long for the usual delete: the \\?\ form of
    // a path lifts the 260 character limit. If something is still left, robocopy (which has no
    // such limit) empties the folder first.
    public static void DeleteFolder(string path) {
        if (!Directory.Exists(path)) {
            return;
        }
        string full = Path.GetFullPath(path);
        try {
            DeleteTree(full.StartsWith("\\\\") ? full : "\\\\?\\" + full);
        } catch (Exception) {
        }
        if (!Directory.Exists(full)) {
            return;
        }
        string empty = Path.Combine(Path.GetTempPath(),
                                    "truthxr-empty-" + Guid.NewGuid().ToString("N"));
        try {
            Directory.CreateDirectory(empty);
            ProcessStartInfo start = new ProcessStartInfo(
                "robocopy.exe",
                Quote(empty) + " " + Quote(full) + " /MIR /NFL /NDL /NJH /NJS /NC /NS /NP");
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            using (Process robocopy = Process.Start(start)) {
                robocopy.WaitForExit();
            }
            Directory.Delete(full, true);
        } catch (Exception) {
        } finally {
            try {
                Directory.Delete(empty, true);
            } catch (Exception) {
            }
        }
    }

    static void DeleteTree(string folder) {
        foreach (string file in Directory.GetFiles(folder)) {
            File.SetAttributes(file, FileAttributes.Normal);
            File.Delete(file);
        }
        foreach (string sub in Directory.GetDirectories(folder)) {
            DeleteTree(sub);
        }
        Directory.Delete(folder, false);
    }
}

} // namespace TruthXR.Launcher
