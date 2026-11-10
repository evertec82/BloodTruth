using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace TruthXR.Launcher {

// Another shadPS4 that is already running.
class RunningEmulator {
    public Process process;
    // Its full path, or "" when Windows would not say.
    public string path = "";
    // True when it is this installation's emulator.
    public bool is_ours;
}

// Starts the emulator with the settings the port needs, and the helper that raises the
// game's own resolution.
class EmulatorRunner {
    readonly InstallPaths paths;
    Process game;
    Process resolution_helper;

    // Messages for the launcher log; called from other threads.
    public Action<string> on_log;
    // The game ended, with its exit code; called from another thread.
    public Action<int> on_exited;
    // The resolution helper stopped with an error; called from another thread.
    public Action<string> on_resolution_failed;

    public EmulatorRunner(InstallPaths paths) {
        this.paths = paths;
    }

    public bool IsRunning {
        get {
            try {
                return game != null && !game.HasExited;
            } catch (Exception) {
                return false;
            }
        }
    }

    void Log(string message) {
        Action<string> handler = on_log;
        if (handler != null) {
            handler(message);
        }
    }

    // Every shadps4.exe that is running, with whether it is this installation's.
    public List<RunningEmulator> FindRunning() {
        List<RunningEmulator> found = new List<RunningEmulator>();
        string ours = Path.GetFullPath(paths.emulator);
        foreach (Process process in Process.GetProcessesByName("shadps4")) {
            RunningEmulator running = new RunningEmulator();
            running.process = process;
            try {
                running.path = process.MainModule.FileName;
                running.is_ours = string.Equals(Path.GetFullPath(running.path), ours,
                                                StringComparison.OrdinalIgnoreCase);
            } catch (Win32Exception) {
            } catch (InvalidOperationException) {
            }
            found.Add(running);
        }
        return found;
    }

    // Closes an emulator that is already running. Returns false if it would not close.
    public bool Close(RunningEmulator running) {
        try {
            running.process.Kill();
            return running.process.WaitForExit(10000);
        } catch (Exception) {
            return false;
        }
    }

    // Stops the game this launcher started.
    public void Stop() {
        try {
            if (game != null && !game.HasExited) {
                Log("Stopping the game.");
                game.Kill();
            }
        } catch (Exception error) {
            Log("Could not stop the game: " + error.Message);
        }
    }

    // The user folder needs the emulator's settings before the first start.
    void PrepareUserFolder() {
        Directory.CreateDirectory(paths.log_folder);
        string[] names = {"config", "users"};
        foreach (string name in names) {
            string destination = Path.Combine(paths.user_folder, name + ".json");
            string source = Path.Combine(paths.game_folder, name + "-default.json");
            if (!File.Exists(destination) && File.Exists(source)) {
                File.Copy(source, destination);
            }
        }
    }

    public void Start(string eboot_path, LauncherSettings settings) {
        PrepareUserFolder();

        ProcessStartInfo start = new ProcessStartInfo(paths.emulator,
                                                      "-g \"" + eboot_path + "\"");
        start.UseShellExecute = false;
        // Hides only the emulator's console window. The desktop mirror window still shows.
        start.CreateNoWindow = true;
        start.WorkingDirectory = paths.game_folder;

        // The settings the original PowerShell launcher used, without the four it set that this
        // emulator never reads (SHADPS4_TITLE_RESOLUTION, SHADPS4_TITLE_EYE_WIDTH,
        // SHADPS4_VR_FPS_CAP and SHADPS4_VR_PACE).
        Dictionary<string, string> environment = new Dictionary<string, string>();
        environment["SHADPS4_OPENXR"] = "1";
        environment["SHADPS4_VR"] = "1";
        environment["SHADPS4_XR_WAIT"] = "0";
        environment["SHADPS4_XR_PAUSE"] = "0";
        environment["SHADPS4_XR_GAMEPAD_LAYOUT"] = "1";
        environment["SHADPS4_MOVE_SWAP_HANDS"] = settings.original_move_order ? "0" : "1";
        environment["SHADPS4_VR_WINDOW_FPS"] = settings.MirrorFps.ToString();
        // Sharpening of the picture on its way to the headset (vk_presenter.cpp).
        environment["SHADPS4_VR_SHARPEN"] = settings.SharpenValue;
        // The field of view the game is told, as a percentage of what the headset itself shows
        // (vr_runtime.cpp). Fewer degrees for the same pixels is sharper, with a border.
        environment["SHADPS4_VR_FOV_OF"] = "headset";
        environment["SHADPS4_VR_FOV"] = settings.fov_percent_value;
        environment["SHADPS4_INPUT_SCRIPT"] = "";
        environment["SHADPS4_LIVE_INPUT"] = "";
        environment["SHADPS4_XR_TEST_PRESS"] = "";
        environment["SHADPS4_XR_TEST_TURN"] = "";
        // Kept if the player set it themselves, as before.
        string alias = Environment.GetEnvironmentVariable("SHADPS4_BLOODTRUTH_DMEM_ALIAS");
        if (string.IsNullOrEmpty(alias)) {
            environment["SHADPS4_BLOODTRUTH_DMEM_ALIAS"] = "1";
        }
        foreach (KeyValuePair<string, string> pair in environment) {
            if (pair.Value.Length == 0) {
                start.EnvironmentVariables.Remove(pair.Key);
            } else {
                start.EnvironmentVariables[pair.Key] = pair.Value;
            }
        }

        Process started = new Process();
        started.StartInfo = start;
        started.EnableRaisingEvents = true;
        started.Exited += delegate {
            int code = -1;
            try {
                code = started.ExitCode;
            } catch (Exception) {
            }
            Action<int> handler = on_exited;
            if (handler != null) {
                handler(code);
            }
        };
        Log("Starting the emulator: " + paths.emulator);
        Log("Game: " + eboot_path);
        string headset = settings.headset_index >= 0
                             ? Hardware.headsets[settings.headset_index].name : "not chosen";
        Log("Headset: " + headset + ", controllers: " +
            Hardware.controller_names[settings.controller_index] + ", Move hand order: " +
            (settings.original_move_order ? "original" : "corrected"));
        Log("Resolution: " + settings.ResolutionName + ", sharpening: " +
            Hardware.sharpen_names[settings.sharpen_index] + ", field of view: " +
            settings.fov_degrees + "\u00B0 across (" + settings.fov_percent_value +
            "% of the headset's own view)" +
            ", desktop mirror: " +
            (settings.MirrorFps == 0 ? "uncapped" : settings.MirrorFps + " FPS"));
        started.Start();
        game = started;
        Log("The emulator is running (process " + game.Id + ").");

        if (settings.ResolutionName != "Original") {
            StartResolutionHelper(settings.ResolutionName);
        }
    }

    // set-native-resolution.ps1 waits for the game to load and then writes the chosen
    // resolution into the game's own settings. It stops by itself when the game ends.
    void StartResolutionHelper(string resolution) {
        if (!File.Exists(paths.resolution_helper)) {
            Log("The resolution helper is missing, so the game uses its own resolution.");
            Action<string> missing = on_resolution_failed;
            if (missing != null) {
                missing("set-native-resolution.ps1 is missing from the pc-vr folder.");
            }
            return;
        }
        string powershell = Path.Combine(Environment.SystemDirectory,
                                         "WindowsPowerShell\\v1.0\\powershell.exe");
        if (!File.Exists(powershell)) {
            powershell = "powershell.exe";
        }
        string arguments = "-NoProfile -ExecutionPolicy Bypass -File \"" +
                           paths.resolution_helper + "\" -GameProcessId " + game.Id +
                           " -Resolution " + resolution + " -InstallPath \"" + paths.game_folder +
                           "\"";
        ProcessStartInfo start = new ProcessStartInfo(powershell, arguments);
        start.UseShellExecute = false;
        start.CreateNoWindow = true;
        start.WorkingDirectory = paths.game_folder;
        try {
            resolution_helper = new Process();
            resolution_helper.StartInfo = start;
            resolution_helper.EnableRaisingEvents = true;
            Process helper = resolution_helper;
            helper.Exited += delegate {
                int code = 0;
                try {
                    code = helper.ExitCode;
                } catch (Exception) {
                }
                if (code != 0) {
                    string reason = LastResolutionMessage();
                    Log("The resolution helper stopped: " + reason);
                    Action<string> handler = on_resolution_failed;
                    if (handler != null) {
                        handler(reason);
                    }
                }
            };
            helper.Start();
            Log("Waiting for the game to load to apply the " + resolution + " resolution.");
        } catch (Exception error) {
            Log("The resolution helper could not start: " + error.Message);
        }
    }

    // The last line the resolution helper wrote to its log.
    string LastResolutionMessage() {
        try {
            string report = Path.Combine(paths.log_folder, "native-resolution.log");
            if (File.Exists(report)) {
                string[] lines = File.ReadAllLines(report);
                for (int i = lines.Length - 1; i >= 0; --i) {
                    if (lines[i].Trim().Length > 0) {
                        return lines[i].Trim();
                    }
                }
            }
        } catch (Exception) {
        }
        return "no reason was written down.";
    }
}

} // namespace TruthXR.Launcher
