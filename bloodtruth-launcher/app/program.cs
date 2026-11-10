// The TruthXR launcher for Blood & Truth PC VR.
// Built by tools/make-launcher.ps1 with the C# compiler that ships with Windows (.NET Framework 4),
// so the code only uses C# 5 language features.

using System;
using System.Drawing;
using System.IO;
using System.Runtime.Versioning;
using System.Threading;
using System.Windows.Forms;

// Marks the program as made for .NET Framework 4.8. Without it Windows treats it as a .NET 4.0
// program and switches off support for long paths (\\?\), which the unpacker needs.
[assembly: TargetFramework(".NETFramework,Version=v4.8")]

namespace TruthXR.Launcher {

static class Program {
    [STAThread]
    static void Main() {
        // Anything that goes wrong is written down and shown, instead of the launcher quietly
        // not appearing.
        AppDomain.CurrentDomain.UnhandledException +=
            delegate(object sender, UnhandledExceptionEventArgs e) {
                ReportCrash(e.ExceptionObject as Exception);
            };
        try {
            Run();
        } catch (Exception error) {
            ReportCrash(error);
        }
    }

    static void Run() {
        // Must happen before anything touches System.IO, for the same reason as the attribute
        // above.
        try {
            AppContext.SetSwitch("Switch.System.IO.UseLegacyPathHandling", false);
            AppContext.SetSwitch("Switch.System.IO.BlockLongPaths", false);
        } catch (Exception) {
        }

        try {
            NativeMethods.SetProcessDPIAware();
        } catch (Exception) {
        }

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e) {
            ReportCrash(e.Exception);
            Application.Exit();
        };
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        using (Graphics graphics = Graphics.FromHwnd(IntPtr.Zero)) {
            Theme.scale = Math.Max(1f, graphics.DpiX / 96f);
        }

        // Only one launcher at a time: two of them could start two emulators.
        bool created_new = false;
        using (Mutex single_instance = new Mutex(true, "Local\\TruthXR.Launcher",
                                                 out created_new)) {
            if (!created_new) {
                FancyDialog.Ask(null, "Already open",
                                "The Blood & Truth PC VR launcher is already open.",
                                DialogKind.Info, "OK");
                return;
            }
            InstallPaths paths = FindInstall();
            if (paths == null) {
                return;
            }
            Application.Run(new MainWindow(paths));
        }
    }

    // Where the emulator is. A release TruthXR.exe has it inside and unpacks it into the home
    // folder when needed. One built without it (for development) uses the pc-vr folder next
    // to it, or the pc-vr folder it was put in.
    static InstallPaths FindInstall() {
        if (PayloadInstaller.HasPayload()) {
            string home = PayloadInstaller.HomeFolder();
            if (PayloadInstaller.NeedsInstall(home)) {
                bool first_time = !File.Exists(Path.Combine(home, "pc-vr\\shadps4.exe"));
                if (!CloseRunningForUpdate(home)) {
                    return null;
                }
                using (SetupDialog setup = new SetupDialog(home, first_time)) {
                    setup.ShowDialog();
                    if (setup.error_message != null) {
                        FancyDialog.Ask(null, "Setting up didn't work",
                                        "The emulator could not be unpacked into\n" + home +
                                            "\n\n" + setup.error_message,
                                        DialogKind.Error, "Close");
                        return null;
                    }
                }
            }
            return InstallPaths.FromRoot(home);
        }

        string folder = Path.GetDirectoryName(Path.GetFullPath(Application.ExecutablePath));
        string[] roots = {folder, Path.GetDirectoryName(folder)};
        foreach (string root in roots) {
            if (root != null && File.Exists(Path.Combine(root, "pc-vr\\shadps4.exe"))) {
                return InstallPaths.FromRoot(root);
            }
        }
        FancyDialog.Ask(null, "The emulator isn't inside this TruthXR.exe",
                        "This TruthXR.exe was built without the emulator inside it, so it needs " +
                            "a pc-vr folder with shadps4.exe next to it, and there is none in\n" +
                            folder + "\n\nUse a release TruthXR.exe, or build one with the " +
                            "emulator inside:\ntools\\make-launcher.ps1 -Emulator " +
                            "<path to shadps4.exe>",
                        DialogKind.Error, "Close");
        return null;
    }

    // An update cannot replace an emulator that is running, so it has to close first.
    static bool CloseRunningForUpdate(string home) {
        System.Diagnostics.Process running = PayloadInstaller.RunningFrom(home);
        if (running == null) {
            return true;
        }
        int choice = FancyDialog.Ask(null, "The game is still running",
                                     "This TruthXR.exe is a new version and has to update the " +
                                         "emulator, but the game is still open from before. " +
                                         "Close it and update?",
                                     DialogKind.Warning, "Close it and update", "Cancel");
        if (choice != 0) {
            return false;
        }
        try {
            running.Kill();
            running.WaitForExit(10000);
        } catch (Exception) {
        }
        return true;
    }

    static bool crash_reported;

    // Writes what went wrong to TruthXR-crash.txt (next to TruthXR.exe, or in the temporary
    // folder if that is not possible) and shows it in a plain Windows message box, which still
    // works when the launcher's own windows do not.
    static void ReportCrash(Exception error) {
        if (crash_reported) {
            return;
        }
        crash_reported = true;
        string details = error != null ? error.ToString() : "No details were given.";
        string text = "The Blood & Truth PC VR launcher stopped because of an error.\r\n" +
                      DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\r\n\r\n" + details +
                      "\r\n";
        string written = null;
        string[] places = {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TruthXR-crash.txt"),
            Path.Combine(Path.GetTempPath(), "TruthXR-crash.txt"),
        };
        foreach (string place in places) {
            try {
                File.WriteAllText(place, text);
                written = place;
                break;
            } catch (Exception) {
            }
        }
        string shown = details.Length > 1500 ? details.Substring(0, 1500) + "\u2026" : details;
        try {
            MessageBox.Show("The launcher stopped because of an error." +
                                (written != null ? "\n\nThe details are saved in:\n" + written
                                                 : "") +
                                "\n\n" + shown,
                            "Blood & Truth PC VR", MessageBoxButtons.OK, MessageBoxIcon.Error);
        } catch (Exception) {
        }
    }
}

// Where everything lives: root holds pc-vr (the emulator and its helpers) and pc-vr\\user (the
// player's settings, saves and logs).
class InstallPaths {
    public string root;
    public string game_folder;
    public string user_folder;
    public string log_folder;
    public string games_folder;
    public string emulator;
    public string resolution_helper;
    public string pkgtool;
    public string settings_file;
    public string launcher_log;

    public static InstallPaths FromRoot(string root) {
        InstallPaths paths = new InstallPaths();
        paths.root = System.IO.Path.GetFullPath(root);
        paths.game_folder = System.IO.Path.Combine(paths.root, "pc-vr");
        paths.user_folder = System.IO.Path.Combine(paths.game_folder, "user");
        paths.log_folder = System.IO.Path.Combine(paths.user_folder, "log");
        paths.games_folder = System.IO.Path.Combine(paths.root, "games");
        paths.emulator = System.IO.Path.Combine(paths.game_folder, "shadps4.exe");
        paths.resolution_helper = System.IO.Path.Combine(paths.game_folder,
                                                         "set-native-resolution.ps1");
        paths.settings_file = System.IO.Path.Combine(paths.user_folder, "launcher.ini");
        paths.launcher_log = System.IO.Path.Combine(paths.log_folder, "launcher.log");

        // PkgTool is next to the emulator in a release, and in tools/pkgtool in the repository.
        string[] pkgtool_places = {
            System.IO.Path.Combine(paths.game_folder, "pkgtool\\PkgTool.exe"),
            System.IO.Path.Combine(paths.root, "tools\\pkgtool\\PkgTool.exe"),
        };
        foreach (string place in pkgtool_places) {
            if (System.IO.File.Exists(place)) {
                paths.pkgtool = place;
                break;
            }
        }
        return paths;
    }
}

} // namespace TruthXR.Launcher
