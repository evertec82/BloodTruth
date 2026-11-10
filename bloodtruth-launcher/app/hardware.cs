using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace TruthXR.Launcher {

// A headset the launcher knows, and which kind of controllers it comes with.
class HeadsetModel {
    public string id;
    public string name;
    public int controller;
    public string hint;
    // How wide its view is across both eyes, in degrees, as its maker advertises it. Used until
    // the emulator has measured the headset itself.
    public double total_degrees;
    // Words in the name the headset's runtime reports, to know it from a saved measurement.
    public string[] keywords;
}

// A headset's view as the emulator measured it the last time (pc-vr/user/vr_headset_fov.json).
class MeasuredView {
    public string system = "";
    // One eye's view as tangents: toward the outside and toward the nose.
    public double tan_out;
    public double tan_in;
}

// A choice of picture settings that go together.
class GraphicsPreset {
    public string name;
    public int resolution_index;
    public int sharpen_index;
}

// Headsets, controllers, graphics presets and the button guides, in one place.
static class Hardware {
    public const int touch_controllers = 0;
    public const int index_controllers = 1;
    public const int other_controllers = 2;

    public static readonly string[] controller_ids = {"touch", "index", "other"};
    public static readonly string[] controller_names = {
        "Touch-style (Quest, Pico, PS VR2 Sense)",
        "Valve Index controllers",
        "Other (set up in SteamVR)",
    };

    public static readonly HeadsetModel[] headsets = {
        new HeadsetModel {
            id = "quest3", name = "Meta Quest 3 / 3S", controller = touch_controllers,
            hint = "Connect with Quest Link, Air Link, Virtual Desktop or Steam Link.",
            total_degrees = 110, keywords = new string[] {"Quest 3"},
        },
        new HeadsetModel {
            id = "quest2", name = "Meta Quest 2", controller = touch_controllers,
            hint = "Connect with Quest Link, Air Link, Virtual Desktop or Steam Link.",
            total_degrees = 97, keywords = new string[] {"Quest 2"},
        },
        new HeadsetModel {
            id = "questpro", name = "Meta Quest Pro", controller = touch_controllers,
            hint = "Connect with Quest Link, Air Link, Virtual Desktop or Steam Link.",
            total_degrees = 106, keywords = new string[] {"Quest Pro"},
        },
        new HeadsetModel {
            id = "pico4", name = "Pico 4 / 4 Ultra", controller = touch_controllers,
            hint = "Connect with Pico Connect, Virtual Desktop or Steam Link.",
            total_degrees = 105, keywords = new string[] {"Pico"},
        },
        new HeadsetModel {
            id = "psvr2", name = "PlayStation VR2 (PC adapter)", controller = touch_controllers,
            hint = "Needs the PC adapter and SteamVR. Not tested with this port yet.",
            total_degrees = 110, keywords = new string[] {"PlayStation VR2", "PS VR2", "PSVR2"},
        },
        new HeadsetModel {
            id = "index", name = "Valve Index", controller = index_controllers,
            hint = "Runs through SteamVR.",
            total_degrees = 108, keywords = new string[] {"Index"},
        },
        new HeadsetModel {
            id = "vive", name = "HTC Vive / Vive Pro", controller = other_controllers,
            hint = "Runs through SteamVR. The wands have no face buttons, so they need " +
                   "bindings set in SteamVR. Not tested.",
            total_degrees = 110, keywords = new string[] {"Vive"},
        },
        new HeadsetModel {
            id = "wmr", name = "Windows Mixed Reality", controller = other_controllers,
            hint = "Its controllers need bindings set in SteamVR. Not tested.",
            total_degrees = 98, keywords = new string[] {"Mixed Reality", "Reverb"},
        },
        new HeadsetModel {
            id = "other", name = "Another headset", controller = touch_controllers,
            hint = "Any headset with an OpenXR runtime (SteamVR, Meta, Virtual Desktop...).",
            total_degrees = 100, keywords = new string[] {},
        },
    };

    // How much the picture is sharpened on its way to the headset (SHADPS4_VR_SHARPEN).
    public static readonly string[] sharpen_names = {"Off", "Light", "Normal", "Strong"};
    public static readonly string[] sharpen_values = {"0", "0.15", "0.3", "0.5"};

    // More pixels need less sharpening, so the presets ease it off as the resolution goes up.
    public static readonly GraphicsPreset[] presets = {
        new GraphicsPreset {name = "Low", resolution_index = 0, sharpen_index = 2},
        new GraphicsPreset {name = "Medium", resolution_index = 1, sharpen_index = 1},
        new GraphicsPreset {name = "High", resolution_index = 2, sharpen_index = 0},
    };

    // The emulator takes the field of view as a percentage of the headset's own view
    // (SHADPS4_VR_FOV_OF=headset with SHADPS4_VR_FOV from 50 to 120, vr_runtime.cpp), so it
    // keeps the headset's shape. The launcher shows it in degrees across both eyes, from the
    // outer edge of one eye's view to the other's: the number headset makers advertise.
    public const double fov_minimum_scale = 0.5;
    public const double fov_maximum_scale = 1.2;

    static double Degrees(double tangent) {
        return Math.Atan(tangent) * 180.0 / Math.PI;
    }

    static double Tangent(double degrees) {
        return Math.Tan(degrees * Math.PI / 180.0);
    }

    // How far one eye's view reaches toward the outside, as a tangent: from the emulator's
    // measurement when it has one for this headset, otherwise from the advertised width.
    public static double OuterTangent(MeasuredView view, int headset_index) {
        if (MeasurementFits(view, headset_index)) {
            return view.tan_out;
        }
        double total = headset_index >= 0 ? headsets[headset_index].total_degrees : 100.0;
        return Tangent(total / 2.0);
    }

    // The width across both eyes at a scale of the headset's own view.
    public static int WidthAt(double outer_tangent, double scale) {
        return (int)Math.Round(2.0 * Degrees(outer_tangent * scale));
    }

    public static int MinimumWidth(double outer_tangent) {
        return (int)Math.Ceiling(2.0 * Degrees(outer_tangent * fov_minimum_scale));
    }

    public static int MaximumWidth(double outer_tangent) {
        return (int)Math.Floor(2.0 * Degrees(outer_tangent * fov_maximum_scale));
    }

    // The percentage of the headset's view (as SHADPS4_VR_FOV wants it) for a width in degrees.
    public static string PercentFor(int width_degrees, double outer_tangent) {
        double scale = Tangent(width_degrees / 2.0) / outer_tangent;
        scale = Math.Max(fov_minimum_scale, Math.Min(fov_maximum_scale, scale));
        return (scale * 100.0).ToString("0.#", CultureInfo.InvariantCulture);
    }

    // Reads the emulator's saved measurement of the last headset, or returns null.
    public static MeasuredView ReadMeasuredView(string user_folder) {
        try {
            string path = Path.Combine(user_folder, "vr_headset_fov.json");
            if (!File.Exists(path)) {
                return null;
            }
            string text = File.ReadAllText(path);
            Match system = Regex.Match(text, "\"system\"\\s*:\\s*\"([^\"]*)\"");
            Match tangents = Regex.Match(text, "\"fov_tan\"\\s*:\\s*\\[([^\\]]*)\\]");
            if (!tangents.Success) {
                return null;
            }
            string[] parts = tangents.Groups[1].Value.Split(',');
            if (parts.Length < 2) {
                return null;
            }
            double tan_out = double.Parse(parts[0].Trim(), CultureInfo.InvariantCulture);
            double tan_in = double.Parse(parts[1].Trim(), CultureInfo.InvariantCulture);
            MeasuredView view = new MeasuredView();
            view.system = system.Success ? system.Groups[1].Value : "";
            view.tan_out = tan_out;
            view.tan_in = tan_in;
            return view;
        } catch (Exception) {
            return null;
        }
    }

    // Whether a saved measurement belongs to this headset model. "Another headset" takes a
    // measurement that none of the listed models claims.
    public static bool MeasurementFits(MeasuredView view, int headset_index) {
        if (view == null || headset_index < 0) {
            return false;
        }
        HeadsetModel model = headsets[headset_index];
        if (model.keywords.Length == 0) {
            foreach (HeadsetModel other in headsets) {
                if (other.keywords.Length > 0 && NameContains(view.system, other.keywords)) {
                    return false;
                }
            }
            return view.system.Length > 0;
        }
        return NameContains(view.system, model.keywords);
    }

    static bool NameContains(string name, string[] keywords) {
        foreach (string keyword in keywords) {
            if (name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0) {
                return true;
            }
        }
        return false;
    }

    public static int HeadsetIndex(string id) {
        for (int i = 0; i < headsets.Length; ++i) {
            if (string.Equals(headsets[i].id, id, StringComparison.OrdinalIgnoreCase)) {
                return i;
            }
        }
        return -1;
    }

    // The preset that matches these settings, or -1 when they were set by hand.
    public static int MatchingPreset(int resolution_index, int sharpen_index) {
        for (int i = 0; i < presets.Length; ++i) {
            if (presets[i].resolution_index == resolution_index &&
                presets[i].sharpen_index == sharpen_index) {
                return i;
            }
        }
        return -1;
    }

    // Each row: the PlayStation Move button, then what to press on these controllers. This is
    // how the emulator maps them (openxr_host.cpp); the launcher only shows it.
    public static string[][] ButtonGuide(int controller) {
        bool index = controller == index_controllers;
        return new string[][] {
            new string[] {"T (trigger)", "Trigger"},
            new string[] {"Move button", "Grip (squeeze)"},
            new string[] {"Cross", index ? "A (lower button)" : "A / X (lower button)"},
            new string[] {"Circle", index ? "B (upper button)" : "B / Y (upper button)"},
            new string[] {"Square", "Thumbstick click"},
            new string[] {"Triangle", "Thumbstick up"},
            new string[] {"Select", "Thumbstick down"},
            new string[] {"Start", index ? "Left trackpad, pressed firmly" : "Left menu button"},
            new string[] {"Re-centre the view", "Both thumbstick clicks together"},
        };
    }
}

} // namespace TruthXR.Launcher
