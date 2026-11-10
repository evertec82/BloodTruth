using System;
using System.Drawing;
using System.Windows.Forms;

namespace TruthXR.Launcher {

// Shows which button on the player's controllers stands for which PlayStation Move button.
class ButtonGuideDialog : ThemedForm {
    public ButtonGuideDialog(int controller) {
        Text = "Blood & Truth PC VR";
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        int width = Theme.Scaled(520);
        int left = Theme.Scaled(28);
        int inner = width - left * 2;

        Label title = Theme.MakeWrappedLabel("Buttons", Theme.dialog_title_font, Theme.text,
                                             BackColor, inner);
        title.Location = new Point(left, Theme.Scaled(24));
        Controls.Add(title);

        Label subtitle = Theme.MakeWrappedLabel(
            Hardware.controller_names[controller] + ". The game was made for two PlayStation " +
                "Move controllers; each of your controllers is one of them.",
            Theme.body_font, Theme.muted, BackColor, inner);
        subtitle.Location = new Point(left, title.Bottom + Theme.Scaled(4));
        Controls.Add(subtitle);

        Card table = new Card();
        table.Location = new Point(left, subtitle.Bottom + Theme.Scaled(16));
        table.Width = inner;
        Controls.Add(table);

        int row_top = Theme.Scaled(12);
        int first_column = Theme.Scaled(160);
        string[][] rows = Hardware.ButtonGuide(controller);
        for (int i = 0; i < rows.Length; ++i) {
            Label move = Theme.MakeLabel(rows[i][0], Theme.body_font, Theme.muted,
                                         table.BackColor);
            move.Bounds = new Rectangle(Theme.Scaled(18), row_top,
                                        first_column - Theme.Scaled(18), Theme.Scaled(24));
            table.Controls.Add(move);
            Label yours = Theme.MakeLabel(rows[i][1], Theme.heading_font, Theme.text,
                                          table.BackColor);
            yours.Bounds = new Rectangle(first_column, row_top,
                                         inner - first_column - Theme.Scaled(18), Theme.Scaled(24));
            table.Controls.Add(yours);
            row_top += Theme.Scaled(28);
        }
        table.Height = row_top + Theme.Scaled(8);
        int top = table.Bottom + Theme.Scaled(12);

        string note = "The emulator recognises your controllers by itself; this guide is for " +
                      "reference.";
        if (controller == Hardware.other_controllers) {
            note = "The emulator offers Touch and Index bindings. For other controllers, open " +
                   "SteamVR's Controller Bindings for this game and map yours onto those. Not " +
                   "tested.";
        }
        Label note_label = Theme.MakeWrappedLabel(note, Theme.small_font, Theme.faint, BackColor,
                                                  inner);
        note_label.Location = new Point(left, top);
        Controls.Add(note_label);
        top = note_label.Bottom + Theme.Scaled(18);

        FlatButton close = new FlatButton();
        close.Text = "Close";
        close.Kind = ButtonKind.Primary;
        close.BackColor = BackColor;
        close.Bounds = new Rectangle(width - left - Theme.Scaled(100), top, Theme.Scaled(100),
                                     Theme.Scaled(36));
        close.Click += delegate { Close(); };
        Controls.Add(close);

        ClientSize = new Size(width, close.Bottom + Theme.Scaled(22));
        ActiveControl = close;
    }

    protected override void OnKeyDown(KeyEventArgs e) {
        if (e.KeyCode == Keys.Escape) {
            Close();
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }
}

} // namespace TruthXR.Launcher
