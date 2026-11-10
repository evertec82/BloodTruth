using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace TruthXR.Launcher {

enum GameStatus {
    // An unpacked game of the supported version: ready to play.
    Ready,
    // A .pkg that has to be unpacked first.
    Package,
    // Something was found, but it cannot be played (wrong version, missing files).
    Problem,
    // Nothing was found.
    Missing,
}

class GameInfo {
    public GameStatus status = GameStatus.Missing;
    public string eboot_path = "";
    public string package_path = "";
    public string title = "";
    public string title_id = "";
    public string version = "";
    public string problem = "";

    public static GameInfo Missing() {
        return new GameInfo();
    }

    // The path to remember for this game.
    public string RememberedPath {
        get { return status == GameStatus.Package ? package_path : eboot_path; }
    }
}

// Finds the player's copy of Blood & Truth and checks that it is the supported version.
static class GameLocator {
    public const string expected_title_id = "CUSA11108";
    public const string expected_version_text = "CUSA11108 version 1.00";
    // SHA-256 of the eboot.bin of version 1.00, the only build the emulator changes are made for.
    const string expected_eboot_sha256 =
        "9312B61DAF0AC023F66203F59BC3563497C26BF6E97199833B9B60CEF14B65F7";
    // Folders the search skips: what is left of an unfinished unpack.
    static readonly string[] skipped_folders = {".unpacking", "tt-unpack"};

    static readonly Dictionary<string, bool> hash_results = new Dictionary<string, bool>();

    // Looks at the remembered path first, then the usual place, then searches the games folder.
    public static GameInfo Find(string remembered_path, string games_folder) {
        if (!string.IsNullOrEmpty(remembered_path) &&
            (File.Exists(remembered_path) || Directory.Exists(remembered_path))) {
            GameInfo remembered = Inspect(remembered_path);
            if (remembered.status != GameStatus.Missing) {
                return remembered;
            }
        }

        string usual = Path.Combine(games_folder, expected_title_id + "\\files\\uroot\\eboot.bin");
        if (File.Exists(usual)) {
            return InspectEboot(usual);
        }

        if (Directory.Exists(games_folder)) {
            return InspectFolder(games_folder);
        }
        return GameInfo.Missing();
    }

    // Works out what a path the player chose is: an eboot.bin, a .pkg, or a folder holding either.
    public static GameInfo Inspect(string path) {
        try {
            if (Directory.Exists(path)) {
                return InspectFolder(path);
            }
            if (!File.Exists(path)) {
                return GameInfo.Missing();
            }
            string extension = Path.GetExtension(path);
            if (string.Equals(extension, ".pkg", StringComparison.OrdinalIgnoreCase)) {
                return InspectPackage(path);
            }
            if (string.Equals(Path.GetFileName(path), "eboot.bin",
                              StringComparison.OrdinalIgnoreCase)) {
                return InspectEboot(path);
            }
            GameInfo other = new GameInfo();
            other.status = GameStatus.Problem;
            other.eboot_path = path;
            other.problem = "This is not the game. Choose its eboot.bin, its folder, or a .pkg.";
            return other;
        } catch (Exception error) {
            GameInfo failed = new GameInfo();
            failed.status = GameStatus.Problem;
            failed.eboot_path = path;
            failed.problem = "The game could not be read: " + error.Message;
            return failed;
        }
    }

    static GameInfo InspectFolder(string folder) {
        string eboot = FindEboot(folder);
        if (eboot != null) {
            return InspectEboot(eboot);
        }
        string package = FindLargestPackage(folder);
        if (package != null) {
            return InspectPackage(package);
        }
        return GameInfo.Missing();
    }

    static GameInfo InspectPackage(string package) {
        GameInfo info = new GameInfo();
        info.package_path = package;
        string content_id = PackageUnpacker.ReadPackageId(package);
        if (content_id == null) {
            info.status = GameStatus.Problem;
            info.problem = "This file is not a PlayStation 4 package.";
            return info;
        }
        info.status = GameStatus.Package;
        info.title = "Blood & Truth";
        int at = content_id.IndexOf("CUSA", StringComparison.Ordinal);
        info.title_id = at >= 0 && content_id.Length >= at + 9 ? content_id.Substring(at, 9) : "";
        if (info.title_id.Length > 0 && info.title_id != expected_title_id) {
            info.status = GameStatus.Problem;
            info.problem = "This package is " + info.title_id + ". The port needs " +
                           expected_version_text + ".";
        }
        return info;
    }

    static GameInfo InspectEboot(string eboot) {
        GameInfo info = new GameInfo();
        info.eboot_path = eboot;
        string sfo = Path.Combine(Path.GetDirectoryName(eboot), "sce_sys\\param.sfo");
        if (!File.Exists(sfo)) {
            info.status = GameStatus.Problem;
            info.problem = "The game's description (sce_sys\\param.sfo) is missing next to " +
                           "eboot.bin. Keep the complete game folder.";
            return info;
        }
        Dictionary<string, string> values = ReadSfo(sfo);
        info.title = Lookup(values, "TITLE", "Blood & Truth");
        info.title_id = Lookup(values, "TITLE_ID", "");
        info.version = Lookup(values, "APP_VER", "");

        if (!HasExpectedHash(eboot)) {
            info.status = GameStatus.Problem;
            info.problem = "This copy is not the supported version. The port needs " +
                           expected_version_text + " (without updates).";
            return info;
        }
        info.status = GameStatus.Ready;
        return info;
    }

    static string Lookup(Dictionary<string, string> values, string key, string fallback) {
        string value;
        return values.TryGetValue(key, out value) && value.Length > 0 ? value : fallback;
    }

    // The hash takes a moment for a 45 MB file, so each file is only checked once while the
    // launcher runs (unless it changes).
    static bool HasExpectedHash(string eboot) {
        FileInfo file = new FileInfo(eboot);
        string key = file.FullName + "|" + file.Length + "|" + file.LastWriteTimeUtc.Ticks;
        lock (hash_results) {
            bool known;
            if (hash_results.TryGetValue(key, out known)) {
                return known;
            }
        }
        bool matches;
        using (SHA256 sha = SHA256.Create())
        using (FileStream stream = new FileStream(eboot, FileMode.Open, FileAccess.Read,
                                                  FileShare.ReadWrite)) {
            byte[] hash = sha.ComputeHash(stream);
            matches = BitConverter.ToString(hash).Replace("-", "") == expected_eboot_sha256;
        }
        lock (hash_results) {
            hash_results[key] = matches;
        }
        return matches;
    }

    // Searches up to four folders deep for eboot.bin, preferring the one that says it is
    // Blood & Truth.
    static string FindEboot(string top) {
        string first = null;
        foreach (string folder in FoldersUnder(top, 4)) {
            string eboot = Path.Combine(folder, "eboot.bin");
            if (!File.Exists(eboot)) {
                continue;
            }
            string sfo = Path.Combine(folder, "sce_sys\\param.sfo");
            if (File.Exists(sfo) && Lookup(ReadSfo(sfo), "TITLE_ID", "") == expected_title_id) {
                return eboot;
            }
            if (first == null) {
                first = eboot;
            }
        }
        return first;
    }

    // The largest .pkg up to three folders deep: a game's package is larger than its updates'.
    static string FindLargestPackage(string top) {
        string largest = null;
        long largest_size = -1;
        foreach (string folder in FoldersUnder(top, 3)) {
            string[] files;
            try {
                files = Directory.GetFiles(folder, "*.pkg");
            } catch (Exception) {
                continue;
            }
            foreach (string file in files) {
                long size = new FileInfo(file).Length;
                if (size > largest_size) {
                    largest = file;
                    largest_size = size;
                }
            }
        }
        return largest;
    }

    // The folder itself and the ones below it, nearest first. Folders that hold an unpacked game
    // are not searched further.
    static List<string> FoldersUnder(string top, int depth) {
        List<string> all = new List<string>();
        List<string> level = new List<string>();
        level.Add(top);
        for (int i = 0; i <= depth && level.Count > 0; ++i) {
            List<string> next = new List<string>();
            foreach (string folder in level) {
                all.Add(folder);
                if (File.Exists(Path.Combine(folder, "eboot.bin"))) {
                    continue;
                }
                try {
                    foreach (string sub in Directory.GetDirectories(folder)) {
                        string name = Path.GetFileName(sub);
                        if (Array.IndexOf(skipped_folders, name) < 0) {
                            next.Add(sub);
                        }
                    }
                } catch (Exception) {
                }
            }
            level = next;
        }
        return all;
    }

    // The text values of a param.sfo (TITLE, TITLE_ID, APP_VER...).
    public static Dictionary<string, string> ReadSfo(string path) {
        Dictionary<string, string> values = new Dictionary<string, string>();
        try {
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length < 20 || BitConverter.ToUInt32(bytes, 0) != 0x46535000) {
                return values;
            }
            int keys = BitConverter.ToInt32(bytes, 8);
            int data = BitConverter.ToInt32(bytes, 12);
            int count = BitConverter.ToInt32(bytes, 16);
            for (int i = 0; i < count; ++i) {
                int entry = 20 + 16 * i;
                int key_at = keys + BitConverter.ToUInt16(bytes, entry);
                int format = BitConverter.ToUInt16(bytes, entry + 2);
                int length = BitConverter.ToInt32(bytes, entry + 4);
                int value_at = data + BitConverter.ToInt32(bytes, entry + 12);
                int end = Array.IndexOf(bytes, (byte)0, key_at);
                if (end < key_at) {
                    break;
                }
                string key = Encoding.ASCII.GetString(bytes, key_at, end - key_at);
                // 0x0204 is a UTF-8 text that ends with a zero byte.
                if (format == 0x0204 && length > 0 && value_at + length <= bytes.Length) {
                    values[key] = Encoding.UTF8.GetString(bytes, value_at, length - 1);
                }
            }
        } catch (Exception) {
        }
        return values;
    }
}

} // namespace TruthXR.Launcher
