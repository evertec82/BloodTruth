using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace TruthXR.Launcher {

// Explains what the launcher needs and lets the player point it at their copy of the game.
// Shown when the game was not found, when it cannot be used, and from the Change button.
class NotFoundDialog : ThemedForm {
    readonly string games_folder;

    // The file or folder the player chose, or null.
    public string chosen_path;
    // True when the player asked the launcher to search again.
    public bool look_again;

    public NotFoundDialog(string games_folder, bool game_missing, string problem) {
        this.games_folder = games_folder;
        Text = "Blood & Truth PC VR";
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        int width = Theme.Scaled(560);
        int left = Theme.Scaled(28);
        int inner = width - left * 2;

        DialogBadge badge = new DialogBadge(game_missing ? DialogKind.Warning
                                                         : DialogKind.Question);
        badge.BackColor = BackColor;
        badge.Location = new Point(left, Theme.Scaled(26));
        Controls.Add(badge);

        int text_left = left + Theme.Scaled(52);
        int text_width = width - text_left - left;
        string heading = game_missing ? "Blood & Truth wasn't found"
                                      : "Choose your copy of Blood & Truth";
        Label title = Theme.MakeWrappedLabel(heading, Theme.dialog_title_font, Theme.text,
                                             BackColor, text_width);
        title.Location = new Point(text_left, Theme.Scaled(26));
        Controls.Add(title);

        string intro = "The launcher doesn't include the game. It plays your own copy, so show " +
                       "it where that is, or put the game in the games folder.";
        Label intro_label = Theme.MakeWrappedLabel(intro, Theme.body_font, Theme.muted, BackColor,
                                                   text_width);
        intro_label.Location = new Point(text_left, title.Bottom + Theme.Scaled(6));
        Controls.Add(intro_label);

        int top = Math.Max(intro_label.Bottom, badge.Bottom) + Theme.Scaled(18);
        if (!string.IsNullOrEmpty(problem)) {
            Label problem_label = Theme.MakeWrappedLabel(problem, Theme.body_font, Theme.warning,
                                                         BackColor, inner);
            problem_label.Location = new Point(left, top);
            Controls.Add(problem_label);
            top = problem_label.Bottom + Theme.Scaled(14);
        }

        // What can be used, on a card of its own.
        Card needs = new Card();
        needs.Heading = "What the launcher can use";
        needs.Location = new Point(left, top);
        needs.Width = inner;
        Controls.Add(needs);
        int needs_top = needs.ContentTop;
        needs_top = AddPoint(needs, needs_top, "The unpacked game: the folder that has eboot.bin " +
                                                   "in it.");
        needs_top = AddPoint(needs, needs_top, "Or a .pkg made from a dump of the game. The " +
                                                   "launcher unpacks it for you, once.");
        Label version_label = Theme.MakeWrappedLabel(
            "It must be " + GameLocator.expected_version_text + ", without updates.",
            Theme.small_font, Theme.faint, needs.BackColor, inner - Theme.Scaled(40));
        version_label.Location = new Point(Theme.Scaled(18), needs_top + Theme.Scaled(4));
        needs.Controls.Add(version_label);
        needs.Height = version_label.Bottom + Theme.Scaled(16);
        top = needs.Bottom + Theme.Scaled(18);

        int half = (inner - Theme.Scaled(10)) / 2;
        FlatButton choose_file = MakeButton("Choose eboot.bin or .pkg\u2026", ButtonKind.Secondary,
                                            new Rectangle(left, top, half, Theme.Scaled(40)));
        choose_file.Click += delegate { ChooseFile(); };
        FlatButton choose_folder = MakeButton("Choose a folder\u2026", ButtonKind.Secondary,
                                              new Rectangle(left + half + Theme.Scaled(10), top,
                                                            half, Theme.Scaled(40)));
        choose_folder.Click += delegate { ChooseFolder(); };
        top = choose_file.Bottom + Theme.Scaled(10);

        FlatButton open_games = MakeButton("Open the games folder", ButtonKind.Quiet,
                                           new Rectangle(left, top, half, Theme.Scaled(36)));
        open_games.Click += delegate { OpenGamesFolder(); };
        top = open_games.Bottom + Theme.Scaled(22);

        FlatButton cancel = MakeButton("Cancel", ButtonKind.Secondary,
                                       new Rectangle(width - left - Theme.Scaled(100), top,
                                                     Theme.Scaled(100), Theme.Scaled(36)));
        cancel.Click += delegate {
            DialogResult = DialogResult.Cancel;
            Close();
        };
        FlatButton search = MakeButton("Look again", ButtonKind.Primary,
                                       new Rectangle(cancel.Left - Theme.Scaled(118), top,
                                                     Theme.Scaled(110), Theme.Scaled(36)));
        search.Click += delegate {
            look_again = true;
            DialogResult = DialogResult.OK;
            Close();
        };

        ClientSize = new Size(width, cancel.Bottom + Theme.Scaled(22));
        ActiveControl = choose_file;
    }

    static int AddPoint(Card card, int top, string text) {
        int left = Theme.Scaled(18);
        StatusDot dot = new StatusDot();
        dot.Colour = Theme.accent;
        dot.Size = new Size(Theme.Scaled(8), Theme.Scaled(8));
        dot.Location = new Point(left + Theme.Scaled(2), top + Theme.Scaled(7));
        card.Controls.Add(dot);
        Label label = Theme.MakeWrappedLabel(text, Theme.body_font, Theme.text, card.BackColor,
                                             card.Width - left * 2 - Theme.Scaled(20));
        label.Location = new Point(left + Theme.Scaled(20), top);
        card.Controls.Add(label);
        return label.Bottom + Theme.Scaled(6);
    }

    FlatButton MakeButton(string text, ButtonKind kind, Rectangle bounds) {
        FlatButton button = new FlatButton();
        button.Text = text;
        button.Kind = kind;
        button.BackColor = BackColor;
        button.Bounds = bounds;
        Controls.Add(button);
        return button;
    }

    protected override void OnKeyDown(KeyEventArgs e) {
        if (e.KeyCode == Keys.Escape) {
            DialogResult = DialogResult.Cancel;
            Close();
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }

    void ChooseFile() {
        using (OpenFileDialog dialog = new OpenFileDialog()) {
            dialog.Title = "Choose Blood & Truth: its eboot.bin, or its .pkg";
            dialog.Filter = "Blood & Truth (eboot.bin, *.pkg)|eboot.bin;*.pkg|All files (*.*)|*.*";
            dialog.CheckFileExists = true;
            if (Directory.Exists(games_folder)) {
                dialog.InitialDirectory = games_folder;
            }
            if (dialog.ShowDialog(this) == DialogResult.OK) {
                chosen_path = dialog.FileName;
                DialogResult = DialogResult.OK;
                Close();
            }
        }
    }

    void ChooseFolder() {
        using (FolderBrowserDialog dialog = new FolderBrowserDialog()) {
            dialog.Description = "Choose the folder that holds Blood & Truth (its eboot.bin or " +
                                 "its .pkg). Folders inside it are searched too.";
            dialog.ShowNewFolderButton = false;
            if (Directory.Exists(games_folder)) {
                dialog.SelectedPath = games_folder;
            }
            if (dialog.ShowDialog(this) == DialogResult.OK) {
                chosen_path = dialog.SelectedPath;
                DialogResult = DialogResult.OK;
                Close();
            }
        }
    }

    void OpenGamesFolder() {
        try {
            Directory.CreateDirectory(games_folder);
            Process.Start("explorer.exe", "\"" + games_folder + "\"");
        } catch (Exception error) {
            FancyDialog.Ask(this, "Could not open the folder", error.Message, DialogKind.Error,
                            "OK");
            return;
        }
        FancyDialog.Ask(this, "Put the game in this folder",
                        "Copy the unpacked game folder, or the .pkg, into\n" + games_folder +
                            "\n\nThen choose Look again.",
                        DialogKind.Info, "OK");
    }
}

} // namespace TruthXR.Launcher
