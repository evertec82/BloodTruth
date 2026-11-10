using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TruthXR.Launcher {

// A window in the launcher's dark style. Every window and dialog derives from it.
class ThemedForm : Form {
    public ThemedForm() {
        BackColor = Theme.window;
        ForeColor = Theme.text;
        Font = Theme.body_font;
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = false;
        KeyPreview = true;
        try {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        } catch (Exception) {
        }
    }

    protected override void OnHandleCreated(EventArgs e) {
        base.OnHandleCreated(e);
        NativeMethods.UseDarkTitleBar(this);
    }
}

enum DialogKind {
    Info,
    Question,
    Warning,
    Error,
}

// The round symbol at the left of a dialog: i, ?, or !.
class DialogBadge : Control {
    readonly DialogKind kind;

    public DialogBadge(DialogKind kind) {
        this.kind = kind;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer,
                 true);
        Size = new Size(Theme.Scaled(36), Theme.Scaled(36));
    }

    public static Color ColourFor(DialogKind kind) {
        switch (kind) {
        case DialogKind.Error:
            return Theme.bad;
        case DialogKind.Warning:
            return Theme.warning;
        case DialogKind.Question:
            return Theme.accent;
        default:
            return Theme.good;
        }
    }

    protected override void OnPaint(PaintEventArgs e) {
        Graphics graphics = e.Graphics;
        graphics.Clear(BackColor);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Color colour = ColourFor(kind);
        using (SolidBrush halo = new SolidBrush(Color.FromArgb(48, colour))) {
            graphics.FillEllipse(halo, 0, 0, Width - 1, Height - 1);
        }
        string symbol = kind == DialogKind.Question ? "?" : (kind == DialogKind.Info ? "i" : "!");
        using (Font font = new Font("Segoe UI Semibold", 14f)) {
            Theme.DrawText(graphics, symbol, font, new Rectangle(0, 0, Width, Height), colour,
                           TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }
}

// A message box in the launcher's style. The first button is the main one.
class FancyDialog : ThemedForm {
    int chosen = -1;

    FancyDialog(string title, string message, DialogKind kind, string[] buttons) {
        Text = "Blood & Truth PC VR";
        int width = Theme.Scaled(500);
        int text_left = Theme.Scaled(80);
        int text_width = width - text_left - Theme.Scaled(28);

        DialogBadge badge = new DialogBadge(kind);
        badge.Location = new Point(Theme.Scaled(26), Theme.Scaled(26));
        badge.BackColor = BackColor;
        Controls.Add(badge);

        Label title_label = Theme.MakeWrappedLabel(title, Theme.dialog_title_font, Theme.text,
                                                   BackColor, text_width);
        title_label.Location = new Point(text_left, Theme.Scaled(26));
        Controls.Add(title_label);

        Label message_label = Theme.MakeWrappedLabel(message, Theme.body_font, Theme.muted,
                                                     BackColor, text_width);
        message_label.Location = new Point(text_left, title_label.Bottom + Theme.Scaled(6));
        Controls.Add(message_label);

        int buttons_top = Math.Max(message_label.Bottom, badge.Bottom) + Theme.Scaled(26);
        int right = width - Theme.Scaled(24);
        FlatButton first_button = null;
        for (int i = buttons.Length - 1; i >= 0; --i) {
            FlatButton button = new FlatButton();
            button.Text = buttons[i];
            button.Kind = i == 0 ? ButtonKind.Primary : ButtonKind.Secondary;
            int text_width_needed = TextRenderer.MeasureText(buttons[i], Theme.button_font).Width;
            button.Width = Math.Max(Theme.Scaled(96), text_width_needed + Theme.Scaled(36));
            button.Location = new Point(right - button.Width, buttons_top);
            button.BackColor = BackColor;
            button.TabIndex = i;
            int index = i;
            button.Click += delegate {
                chosen = index;
                Close();
            };
            Controls.Add(button);
            right = button.Left - Theme.Scaled(8);
            if (i == 0) {
                first_button = button;
            }
        }

        ClientSize = new Size(width, buttons_top + Theme.Scaled(36) + Theme.Scaled(22));
        if (first_button != null) {
            ActiveControl = first_button;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e) {
        if (e.KeyCode == Keys.Escape) {
            chosen = -1;
            Close();
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }

    // Shows the dialog and returns the index of the button pressed, or -1 if it was closed.
    public static int Ask(IWin32Window owner, string title, string message, DialogKind kind,
                          params string[] buttons) {
        if (buttons == null || buttons.Length == 0) {
            buttons = new string[] {"OK"};
        }
        using (FancyDialog dialog = new FancyDialog(title, message, kind, buttons)) {
            if (owner == null) {
                dialog.StartPosition = FormStartPosition.CenterScreen;
                dialog.ShowInTaskbar = true;
                dialog.ShowDialog();
            } else {
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.ShowInTaskbar = false;
                dialog.ShowDialog(owner);
            }
            return dialog.chosen;
        }
    }
}

} // namespace TruthXR.Launcher
