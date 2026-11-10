using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace TruthXR.Launcher {

// A release TruthXR.exe carries the emulator and everything it needs as a ZIP inside itself
// (tools/make-launcher.ps1 -Emulator puts it there). On the first start, and whenever the exe
// is a different version, it is unpacked into %LOCALAPPDATA%\TruthXR (or the folder named by
// TRUTHXR_HOME). The player's own files in pc-vr\user (settings, saves, logs) are never touched.
static class PayloadInstaller {
    const string payload_resource = "TruthXR.payload.zip";
    const string stamp_resource = "TruthXR.payload.stamp";
    const string stamp_file = "payload.stamp";

    static Stream OpenResource(string name) {
        return Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
    }

    public static bool HasPayload() {
        using (Stream stream = OpenResource(payload_resource)) {
            return stream != null;
        }
    }

    // A short text that is different for every payload: the version and the ZIP's hash.
    public static string EmbeddedStamp() {
        using (Stream stream = OpenResource(stamp_resource)) {
            if (stream == null) {
                return "";
            }
            using (StreamReader reader = new StreamReader(stream, Encoding.ASCII)) {
                return reader.ReadToEnd().Trim();
            }
        }
    }

    public static string HomeFolder() {
        string custom = Environment.GetEnvironmentVariable("TRUTHXR_HOME");
        if (!string.IsNullOrEmpty(custom)) {
            return Path.GetFullPath(custom);
        }
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(local, "TruthXR");
    }

    public static bool NeedsInstall(string home) {
        string emulator = Path.Combine(home, "pc-vr\\shadps4.exe");
        string stamp_path = Path.Combine(home, stamp_file);
        if (!File.Exists(emulator) || !File.Exists(stamp_path)) {
            return true;
        }
        try {
            return File.ReadAllText(stamp_path).Trim() != EmbeddedStamp();
        } catch (Exception) {
            return true;
        }
    }

    // Unpacks the payload into home. Calls progress with (entries done, entries in all).
    public static void Install(string home, Action<int, int> progress) {
        string home_full = Path.GetFullPath(home).TrimEnd('\\') + "\\";
        Directory.CreateDirectory(home_full);
        using (Stream stream = OpenResource(payload_resource))
        using (ZipArchive zip = new ZipArchive(stream, ZipArchiveMode.Read)) {
            int total = zip.Entries.Count;
            int done = 0;
            foreach (ZipArchiveEntry entry in zip.Entries) {
                ++done;
                // ZIPs made by older .NET versions use \ between folders; both are accepted.
                string name = entry.FullName.Replace('\\', '/');
                if (name.StartsWith("pc-vr/user/", StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }
                string target = Path.GetFullPath(Path.Combine(home_full, name.Replace('/', '\\')));
                // Never write outside the home folder, whatever a name in the ZIP says.
                if (!target.StartsWith(home_full, StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }
                if (name.EndsWith("/")) {
                    Directory.CreateDirectory(target);
                } else {
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    // Written next to the old file first, so a failure never leaves half a file.
                    string fresh = target + ".new";
                    using (Stream source = entry.Open())
                    using (FileStream output = new FileStream(fresh, FileMode.Create,
                                                              FileAccess.Write)) {
                        source.CopyTo(output);
                    }
                    if (File.Exists(target)) {
                        File.SetAttributes(target, FileAttributes.Normal);
                        File.Delete(target);
                    }
                    File.Move(fresh, target);
                }
                if (progress != null) {
                    progress(done, total);
                }
            }
        }
        File.WriteAllText(Path.Combine(home_full, stamp_file), EmbeddedStamp());
    }

    // The emulator in the home folder, if it is running (an update cannot replace it then).
    public static Process RunningFrom(string home) {
        string emulator = Path.GetFullPath(Path.Combine(home, "pc-vr\\shadps4.exe"));
        foreach (Process process in Process.GetProcessesByName("shadps4")) {
            try {
                if (string.Equals(Path.GetFullPath(process.MainModule.FileName), emulator,
                                  StringComparison.OrdinalIgnoreCase)) {
                    return process;
                }
            } catch (Exception) {
            }
        }
        return null;
    }
}

// The small window shown while the payload is unpacked.
class SetupDialog : ThemedForm {
    readonly string home;
    readonly SlimProgressBar progress_bar;
    readonly Label detail_label;
    bool finished;

    public string error_message;

    public SetupDialog(string home, bool first_time) {
        this.home = home;
        Text = "Blood & Truth PC VR";
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = true;
        ControlBox = false;
        int width = Theme.Scaled(500);
        int left = Theme.Scaled(28);
        int inner = width - left * 2;

        Label title = Theme.MakeWrappedLabel(first_time ? "Setting up Blood & Truth PC VR"
                                                        : "Updating Blood & Truth PC VR",
                                             Theme.dialog_title_font, Theme.text, BackColor,
                                             inner);
        title.Location = new Point(left, Theme.Scaled(26));
        Controls.Add(title);

        Label about = Theme.MakeWrappedLabel(
            "Unpacking the emulator into\n" + home + "\nThis happens once for each version. " +
                "Your settings and saves are kept.",
            Theme.body_font, Theme.muted, BackColor, inner);
        about.Location = new Point(left, title.Bottom + Theme.Scaled(6));
        Controls.Add(about);

        progress_bar = new SlimProgressBar();
        progress_bar.BackColor = BackColor;
        progress_bar.Bounds = new Rectangle(left, about.Bottom + Theme.Scaled(20), inner,
                                            Theme.Scaled(8));
        Controls.Add(progress_bar);

        detail_label = Theme.MakeLabel("", Theme.small_font, Theme.muted, BackColor);
        detail_label.Bounds = new Rectangle(left, progress_bar.Bottom + Theme.Scaled(8), inner,
                                            Theme.Scaled(20));
        Controls.Add(detail_label);

        ClientSize = new Size(width, detail_label.Bottom + Theme.Scaled(24));
    }

    protected override void OnShown(EventArgs e) {
        base.OnShown(e);
        Thread worker = new Thread(Work);
        worker.IsBackground = true;
        worker.Start();
    }

    void Work() {
        try {
            PayloadInstaller.Install(home, delegate(int done, int total) {
                RunOnUi(delegate {
                    progress_bar.Fraction = total > 0 ? (double)done / total : -1;
                    detail_label.Text = done + " of " + total + " files";
                });
            });
        } catch (Exception error) {
            error_message = error.Message;
        }
        RunOnUi(delegate {
            finished = true;
            DialogResult = error_message == null ? DialogResult.OK : DialogResult.Abort;
            Close();
        });
    }

    void RunOnUi(MethodInvoker action) {
        try {
            if (IsHandleCreated && !IsDisposed) {
                BeginInvoke(action);
            }
        } catch (Exception) {
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e) {
        // Stopping half-way would leave a broken install; the window closes when it is done.
        if (!finished) {
            e.Cancel = true;
        }
        base.OnFormClosing(e);
    }
}

} // namespace TruthXR.Launcher
