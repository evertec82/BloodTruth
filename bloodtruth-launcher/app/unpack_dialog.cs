using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace TruthXR.Launcher {

// The window shown while a .pkg is unpacked: what is happening, a progress bar and Cancel.
class UnpackDialog : ThemedForm {
    readonly PackageUnpacker unpacker;
    readonly long package_size;
    readonly Label phase_label;
    readonly Label detail_label;
    readonly SlimProgressBar progress_bar;
    readonly FlatButton cancel_button;
    readonly Stopwatch clock = new Stopwatch();
    bool running;
    bool finished;

    public string eboot_path;
    public string error_message;
    public bool was_cancelled;

    public UnpackDialog(PackageUnpacker unpacker) {
        this.unpacker = unpacker;
        package_size = unpacker.PackageSize;
        Text = "Blood & Truth PC VR";
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        ControlBox = false;
        int width = Theme.Scaled(540);
        int inner = width - Theme.Scaled(56);

        Label title = Theme.MakeWrappedLabel("Unpacking Blood & Truth", Theme.dialog_title_font,
                                             Theme.text, BackColor, inner);
        title.Location = new Point(Theme.Scaled(28), Theme.Scaled(26));
        Controls.Add(title);

        string about = "This happens once. It takes a few minutes and about " +
                       PackageUnpacker.Gigabytes(unpacker.SpaceNeeded) + " of space in\n" +
                       unpacker.TargetFolder;
        Label about_label = Theme.MakeWrappedLabel(about, Theme.body_font, Theme.muted, BackColor,
                                                   inner);
        about_label.Location = new Point(Theme.Scaled(28), title.Bottom + Theme.Scaled(6));
        Controls.Add(about_label);

        phase_label = Theme.MakeLabel("Getting ready\u2026", Theme.heading_font, Theme.text,
                                      BackColor);
        phase_label.Bounds = new Rectangle(Theme.Scaled(28), about_label.Bottom + Theme.Scaled(22),
                                           inner, Theme.Scaled(22));
        Controls.Add(phase_label);

        progress_bar = new SlimProgressBar();
        progress_bar.BackColor = BackColor;
        progress_bar.Bounds = new Rectangle(Theme.Scaled(28), phase_label.Bottom + Theme.Scaled(8),
                                            inner, Theme.Scaled(8));
        Controls.Add(progress_bar);

        detail_label = Theme.MakeLabel("", Theme.small_font, Theme.muted, BackColor);
        detail_label.Bounds = new Rectangle(Theme.Scaled(28),
                                            progress_bar.Bottom + Theme.Scaled(8), inner,
                                            Theme.Scaled(20));
        Controls.Add(detail_label);

        cancel_button = new FlatButton();
        cancel_button.Text = "Cancel";
        cancel_button.Kind = ButtonKind.Secondary;
        cancel_button.BackColor = BackColor;
        cancel_button.Bounds = new Rectangle(width - Theme.Scaled(28 + 110),
                                             detail_label.Bottom + Theme.Scaled(18),
                                             Theme.Scaled(110), Theme.Scaled(36));
        cancel_button.Click += delegate { RequestCancel(); };
        Controls.Add(cancel_button);

        ClientSize = new Size(width, cancel_button.Bottom + Theme.Scaled(22));
        unpacker.on_progress = OnProgress;
    }

    protected override void OnShown(EventArgs e) {
        base.OnShown(e);
        running = true;
        clock.Start();
        Thread worker = new Thread(Work);
        worker.IsBackground = true;
        worker.Start();
    }

    void Work() {
        try {
            eboot_path = unpacker.Run();
        } catch (OperationCanceledException) {
            was_cancelled = true;
        } catch (LauncherException error) {
            error_message = error.Message;
        } catch (Exception error) {
            error_message = "Unpacking stopped because of an unexpected error:\n" + error.Message;
        }
        RunOnUi(delegate {
            finished = true;
            running = false;
            DialogResult = eboot_path != null ? DialogResult.OK : DialogResult.Cancel;
            Close();
        });
    }

    void OnProgress(string what, long bytes_written) {
        RunOnUi(delegate {
            phase_label.Text = what + "\u2026";
            string elapsed = FormatElapsed(clock.Elapsed);
            if (bytes_written >= 0 && package_size > 0) {
                progress_bar.Fraction = Math.Min(0.98, (double)bytes_written / package_size);
                detail_label.Text = PackageUnpacker.Gigabytes(bytes_written) + " of about " +
                                    PackageUnpacker.Gigabytes(package_size) + "   \u00B7   " +
                                    elapsed;
            } else {
                progress_bar.Fraction = -1;
                detail_label.Text = elapsed;
            }
        });
    }

    static string FormatElapsed(TimeSpan elapsed) {
        return ((int)elapsed.TotalMinutes) + ":" + elapsed.Seconds.ToString("00") + " elapsed";
    }

    void RequestCancel() {
        if (!running) {
            return;
        }
        cancel_button.Enabled = false;
        cancel_button.Text = "Stopping\u2026";
        phase_label.Text = "Stopping and cleaning up\u2026";
        progress_bar.Fraction = -1;
        unpacker.Cancel();
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
        // Closing the window while it works means Cancel; it closes once the cleanup is done.
        if (running && !finished) {
            e.Cancel = true;
            RequestCancel();
        }
        base.OnFormClosing(e);
    }
}

} // namespace TruthXR.Launcher
