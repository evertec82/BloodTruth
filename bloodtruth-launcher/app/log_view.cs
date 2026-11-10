using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace TruthXR.Launcher {

// The Log tab: what the launcher did, and the emulator's own log while the game runs.
class LogPanel : Panel {
    // How much of an existing game log is shown at first; the rest stays in the file.
    const int initial_tail_bytes = 256 * 1024;
    // Most read per second, so a log that grows fast cannot freeze the window.
    const int read_limit_bytes = 1024 * 1024;
    // When a view holds more text than this, its oldest half is dropped.
    const int view_limit_chars = 800 * 1000;

    readonly InstallPaths paths;
    readonly SegmentedControl source_switch;
    readonly FlatButton copy_button;
    readonly TextBox launcher_view;
    readonly TextBox game_view;
    readonly Label empty_label;
    readonly Timer tail_timer = new Timer();
    readonly Timer copied_timer = new Timer();

    string tail_path;
    long tail_offset = -1;

    public LogPanel(InstallPaths paths, int width, int height) {
        this.paths = paths;
        BackColor = Theme.window;
        Size = new Size(width, height);
        int left = Theme.Scaled(28);
        int inner = width - left * 2;

        source_switch = new SegmentedControl();
        source_switch.Options = new string[] {"Launcher", "Game log"};
        source_switch.BackColor = BackColor;
        source_switch.Bounds = new Rectangle(left, Theme.Scaled(8), Theme.Scaled(240),
                                             Theme.Scaled(34));
        source_switch.SelectionChanged += delegate { ShowSource(); };
        Controls.Add(source_switch);

        FlatButton open_button = new FlatButton();
        open_button.Text = "Open log folder";
        open_button.Kind = ButtonKind.Secondary;
        open_button.BackColor = BackColor;
        open_button.Bounds = new Rectangle(left + inner - Theme.Scaled(140), Theme.Scaled(8),
                                           Theme.Scaled(140), Theme.Scaled(34));
        open_button.Click += delegate { OpenLogFolder(this, paths); };
        Controls.Add(open_button);

        copy_button = new FlatButton();
        copy_button.Text = "Copy";
        copy_button.Kind = ButtonKind.Secondary;
        copy_button.BackColor = BackColor;
        copy_button.Bounds = new Rectangle(open_button.Left - Theme.Scaled(98), Theme.Scaled(8),
                                           Theme.Scaled(90), Theme.Scaled(34));
        copy_button.Click += delegate { CopyCurrent(); };
        Controls.Add(copy_button);

        Card frame = new Card();
        frame.BackColor = Theme.log_background;
        frame.Bounds = new Rectangle(left, Theme.Scaled(54), inner,
                                     height - Theme.Scaled(54) - Theme.Scaled(16));
        Controls.Add(frame);

        Rectangle text_bounds = new Rectangle(Theme.Scaled(12), Theme.Scaled(10),
                                              frame.Width - Theme.Scaled(16),
                                              frame.Height - Theme.Scaled(20));
        launcher_view = MakeView(text_bounds);
        game_view = MakeView(text_bounds);
        frame.Controls.Add(launcher_view);
        frame.Controls.Add(game_view);

        empty_label = Theme.MakeLabel("There is no game log yet. It appears here once the game " +
                                          "has been started.",
                                      Theme.body_font, Theme.muted, frame.BackColor);
        empty_label.TextAlign = ContentAlignment.MiddleCenter;
        empty_label.Bounds = text_bounds;
        frame.Controls.Add(empty_label);

        tail_timer.Interval = 1000;
        tail_timer.Tick += delegate { ReadGameLog(); };
        tail_timer.Start();

        copied_timer.Interval = 1500;
        copied_timer.Tick += delegate {
            copied_timer.Stop();
            copy_button.Text = "Copy";
        };

        ShowSource();
    }

    static TextBox MakeView(Rectangle bounds) {
        TextBox view = new TextBox();
        view.Multiline = true;
        view.ReadOnly = true;
        view.WordWrap = true;
        view.ScrollBars = ScrollBars.Vertical;
        view.BorderStyle = BorderStyle.None;
        view.BackColor = Theme.log_background;
        view.ForeColor = Color.FromArgb(204, 208, 216);
        view.Font = Theme.log_font;
        view.Bounds = bounds;
        view.HandleCreated += delegate { NativeMethods.UseDarkScrollBars(view); };
        return view;
    }

    protected override void Dispose(bool disposing) {
        if (disposing) {
            tail_timer.Dispose();
            copied_timer.Dispose();
        }
        base.Dispose(disposing);
    }

    // Adds a line to the Launcher view. Call it on the window's thread.
    public void AddLauncherLine(string line) {
        Append(launcher_view, line + "\r\n");
    }

    public void ShowGameLog() {
        source_switch.SelectedIndex = 1;
        ShowSource();
        ReadGameLog();
    }

    void ShowSource() {
        bool game = source_switch.SelectedIndex == 1;
        launcher_view.Visible = !game;
        game_view.Visible = game && tail_path != null;
        empty_label.Visible = game && tail_path == null;
    }

    void CopyCurrent() {
        TextBox view = source_switch.SelectedIndex == 1 ? game_view : launcher_view;
        if (view.TextLength == 0) {
            return;
        }
        try {
            Clipboard.SetText(view.Text);
            copy_button.Text = "Copied";
            copied_timer.Stop();
            copied_timer.Start();
        } catch (Exception) {
        }
    }

    static void Append(TextBox view, string text) {
        if (view.TextLength + text.Length > view_limit_chars) {
            string kept = view.Text;
            kept = kept.Substring(Math.Min(kept.Length, kept.Length / 2));
            int line_start = kept.IndexOf('\n');
            view.Text = line_start >= 0 ? kept.Substring(line_start + 1) : kept;
        }
        view.AppendText(text);
    }

    // The emulator writes shadps4.log while it starts and switches to shad_log.txt once the game
    // is loaded. The newer of the two is the one to show.
    string CurrentGameLog() {
        string starting = Path.Combine(paths.log_folder, "shadps4.log");
        string game = Path.Combine(paths.log_folder, "shad_log.txt");
        bool has_starting = File.Exists(starting);
        bool has_game = File.Exists(game);
        if (has_starting && has_game) {
            return File.GetLastWriteTimeUtc(game) >= File.GetLastWriteTimeUtc(starting) ? game
                                                                                       : starting;
        }
        if (has_game) {
            return game;
        }
        return has_starting ? starting : null;
    }

    // Reads whatever was added to the game log since the last look.
    void ReadGameLog() {
        string path;
        try {
            path = CurrentGameLog();
        } catch (Exception) {
            return;
        }
        if (path == null) {
            if (tail_path != null) {
                tail_path = null;
                ShowSource();
            }
            return;
        }
        if (path != tail_path) {
            tail_path = path;
            tail_offset = -1;
            game_view.Clear();
            ShowSource();
        }

        try {
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                                                      FileShare.ReadWrite | FileShare.Delete)) {
                long length = stream.Length;
                // A file shorter than before was started again: show it from the top.
                if (tail_offset < 0 || length < tail_offset) {
                    game_view.Clear();
                    long start = Math.Max(0, length - initial_tail_bytes);
                    if (start > 0) {
                        Append(game_view, "(Older lines are in " + Path.GetFileName(path) +
                                              " in the log folder.)\r\n");
                    }
                    tail_offset = start;
                }
                if (length <= tail_offset) {
                    return;
                }
                int count = (int)Math.Min(read_limit_bytes, length - tail_offset);
                byte[] buffer = new byte[count];
                stream.Seek(tail_offset, SeekOrigin.Begin);
                int read = stream.Read(buffer, 0, count);
                tail_offset += read;
                string text = Encoding.UTF8.GetString(buffer, 0, read);
                Append(game_view, text.Replace("\r\n", "\n").Replace("\n", "\r\n"));
            }
        } catch (Exception) {
            // The emulator may be writing or replacing the file right now; try again next time.
        }
    }

    public static void OpenLogFolder(IWin32Window owner, InstallPaths paths) {
        try {
            Directory.CreateDirectory(paths.log_folder);
            Process.Start("explorer.exe", "\"" + paths.log_folder + "\"");
        } catch (Exception error) {
            FancyDialog.Ask(owner, "Could not open the log folder", error.Message,
                            DialogKind.Error, "OK");
        }
    }
}

} // namespace TruthXR.Launcher
