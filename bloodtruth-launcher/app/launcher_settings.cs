using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace TruthXR.Launcher {

// The choices the launcher remembers between starts, kept in pc-vr/user/launcher.ini as simple
// "name=value" lines. It sits in the user folder, so it moves along when players copy that folder
// to a new installation.
class LauncherSettings {
    public static readonly string[] resolution_names = {"Original", "High", "Ultra8x"};
    public static readonly int[] mirror_rates = {30, 60, 0};

    public string game_path = "";
    // The first start is the Medium preset: High resolution with light sharpening.
    public int resolution_index = 1;
    public int sharpen_index = 1;
    // The field of view in degrees across both eyes; 0 means "whatever fills the headset".
    public int fov_degrees;
    // What is sent to the emulator for it (a percentage of the headset's own view). Worked out
    // by the window from fov_degrees and the headset, and not saved.
    public string fov_percent_value = "100";
    public int mirror_index;
    public int headset_index = Hardware.HeadsetIndex("quest3");
    public int controller_index = Hardware.touch_controllers;
    public bool original_move_order;

    public string ResolutionName {
        get { return resolution_names[resolution_index]; }
    }

    public int MirrorFps {
        get { return mirror_rates[mirror_index]; }
    }

    public string SharpenValue {
        get { return Hardware.sharpen_values[sharpen_index]; }
    }

    public static LauncherSettings Load(string path) {
        LauncherSettings settings = new LauncherSettings();
        if (!File.Exists(path)) {
            return settings;
        }
        try {
            foreach (string line in File.ReadAllLines(path, Encoding.UTF8)) {
                int equals = line.IndexOf('=');
                if (equals <= 0 || line.TrimStart().StartsWith("#")) {
                    continue;
                }
                string name = line.Substring(0, equals).Trim().ToLowerInvariant();
                string value = line.Substring(equals + 1).Trim();
                settings.Apply(name, value);
            }
        } catch (Exception) {
            // A damaged settings file is not worth stopping for: the defaults are used instead.
        }
        return settings;
    }

    static int IndexOf(string[] names, string value) {
        for (int i = 0; i < names.Length; ++i) {
            if (string.Equals(names[i], value, StringComparison.OrdinalIgnoreCase)) {
                return i;
            }
        }
        return -1;
    }

    void Apply(string name, string value) {
        int index;
        if (name == "game") {
            game_path = value;
        } else if (name == "resolution") {
            index = IndexOf(resolution_names, value);
            if (index >= 0) {
                resolution_index = index;
            }
        } else if (name == "sharpen") {
            index = IndexOf(Hardware.sharpen_names, value);
            if (index >= 0) {
                sharpen_index = index;
            }
        } else if (name == "fov_degrees") {
            int degrees;
            if (int.TryParse(value, out degrees) && degrees >= 30 && degrees <= 180) {
                fov_degrees = degrees;
            }
        } else if (name == "mirror_fps") {
            int rate;
            if (int.TryParse(value, out rate)) {
                index = Array.IndexOf(mirror_rates, rate);
                if (index >= 0) {
                    mirror_index = index;
                }
            }
        } else if (name == "headset") {
            index = Hardware.HeadsetIndex(value);
            if (index >= 0) {
                headset_index = index;
            }
        } else if (name == "controllers") {
            index = IndexOf(Hardware.controller_ids, value);
            if (index >= 0) {
                controller_index = index;
            }
        } else if (name == "original_move_order") {
            original_move_order = value == "1" ||
                                  string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        }
    }

    public void Save(string path) {
        try {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            List<string> lines = new List<string>();
            lines.Add("# Blood & Truth PC VR launcher settings. The launcher rewrites this file.");
            lines.Add("game=" + game_path);
            lines.Add("headset=" + (headset_index >= 0 ? Hardware.headsets[headset_index].id : ""));
            lines.Add("controllers=" + Hardware.controller_ids[controller_index]);
            lines.Add("resolution=" + ResolutionName);
            lines.Add("sharpen=" + Hardware.sharpen_names[sharpen_index]);
            lines.Add("fov_degrees=" + fov_degrees);
            lines.Add("mirror_fps=" + MirrorFps);
            lines.Add("original_move_order=" + (original_move_order ? "true" : "false"));
            File.WriteAllLines(path, lines.ToArray(), new UTF8Encoding(false));
        } catch (Exception) {
            // Not being able to save is not a reason to stop the game from starting.
        }
    }
}

} // namespace TruthXR.Launcher
