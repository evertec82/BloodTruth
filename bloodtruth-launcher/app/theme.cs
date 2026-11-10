using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TruthXR.Launcher {

// Colours, fonts and drawing helpers shared by every window, so they all look the same.
// The look is deep navy, brass gold for anything that matters, cool off-white text and
// condensed capitals for titles.
static class Theme {
    public static readonly Color window = Color.FromArgb(11, 16, 24);
    public static readonly Color card = Color.FromArgb(18, 27, 41);
    public static readonly Color card_border = Color.FromArgb(34, 50, 74);
    public static readonly Color input = Color.FromArgb(26, 38, 58);
    public static readonly Color input_hover = Color.FromArgb(35, 50, 75);
    public static readonly Color text = Color.FromArgb(238, 242, 247);
    public static readonly Color muted = Color.FromArgb(154, 168, 188);
    public static readonly Color faint = Color.FromArgb(95, 110, 132);
    public static readonly Color accent = Color.FromArgb(217, 164, 65);
    public static readonly Color accent_hover = Color.FromArgb(235, 185, 87);
    public static readonly Color accent_pressed = Color.FromArgb(181, 133, 44);
    public static readonly Color accent_dark = Color.FromArgb(90, 67, 24);
    // Text drawn on top of the gold.
    public static readonly Color on_accent = Color.FromArgb(14, 20, 30);
    public static readonly Color good = Color.FromArgb(76, 195, 138);
    public static readonly Color warning = Color.FromArgb(240, 138, 75);
    public static readonly Color bad = Color.FromArgb(229, 83, 75);
    public static readonly Color log_background = Color.FromArgb(9, 13, 20);
    // The sidebar on the left of the main window.
    public static readonly Color sidebar = Color.FromArgb(14, 22, 36);
    public static readonly Color sidebar_top = Color.FromArgb(28, 44, 70);
    public static readonly Color sidebar_selected = Color.FromArgb(24, 36, 56);

    // Bahnschrift comes with Windows 10 and 11; older systems get Segoe UI instead.
    public static readonly Font title_font = PickFont(
        new string[] {"Bahnschrift SemiBold SemiConden", "Bahnschrift", "Segoe UI Semibold"}, 24f);
    public static readonly Font sidebar_title_font = PickFont(
        new string[] {"Bahnschrift SemiBold SemiConden", "Bahnschrift", "Segoe UI Semibold"}, 30f);
    public static readonly Font label_font = PickFont(
        new string[] {"Bahnschrift SemiBold", "Bahnschrift", "Segoe UI Semibold"}, 9f);
    public static readonly Font heading_font = new Font("Segoe UI Semibold", 10.5f);
    public static readonly Font dialog_title_font = new Font("Segoe UI Semibold", 12f);
    public static readonly Font body_font = new Font("Segoe UI", 9.75f);
    public static readonly Font small_font = new Font("Segoe UI", 8.75f);
    public static readonly Font button_font = new Font("Segoe UI Semibold", 9.75f);
    public static readonly Font play_font = PickFont(
        new string[] {"Bahnschrift SemiBold", "Bahnschrift", "Segoe UI Semibold"}, 13f);
    public static readonly Font log_font = new Font("Consolas", 9f);

    // The first of these fonts that is installed. A font that is missing would otherwise be
    // replaced quietly by Windows' plainest one.
    static Font PickFont(string[] families, float size) {
        foreach (string family in families) {
            try {
                using (FontFamily found = new FontFamily(family)) {
                    if (found.IsStyleAvailable(FontStyle.Regular)) {
                        return new Font(family, size);
                    }
                }
            } catch (ArgumentException) {
            }
        }
        return new Font(FontFamily.GenericSansSerif, size);
    }

    // Spreads capitals out a little, with thin spaces between the letters.
    public static string Spaced(string value) {
        if (string.IsNullOrEmpty(value)) {
            return "";
        }
        System.Text.StringBuilder spaced = new System.Text.StringBuilder();
        for (int i = 0; i < value.Length; ++i) {
            if (i > 0) {
                spaced.Append('\u2009');
            }
            spaced.Append(value[i]);
        }
        return spaced.ToString();
    }

    // Layout numbers are written for a 100% display; this scales them to the real one.
    public static float scale = 1f;

    public static int Scaled(int value) {
        return (int)Math.Round(value * scale);
    }

    public static Rectangle Scaled(int x, int y, int width, int height) {
        return new Rectangle(Scaled(x), Scaled(y), Scaled(width), Scaled(height));
    }

    public static GraphicsPath RoundedRectangle(Rectangle bounds, int radius) {
        GraphicsPath path = new GraphicsPath();
        int diameter = Math.Max(1, Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height)));
        Rectangle arc = new Rectangle(bounds.Location, new Size(diameter, diameter));

        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static void FillRounded(Graphics graphics, Rectangle bounds, int radius, Color fill) {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (GraphicsPath path = RoundedRectangle(bounds, radius))
        using (SolidBrush brush = new SolidBrush(fill)) {
            graphics.FillPath(brush, path);
        }
    }

    // A rounded shape that fades from one colour at the top to another at the bottom.
    public static void FillRoundedGradient(Graphics graphics, Rectangle bounds, int radius,
                                           Color top, Color bottom) {
        if (bounds.Width <= 0 || bounds.Height <= 0) {
            return;
        }
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (GraphicsPath path = RoundedRectangle(bounds, radius))
        using (LinearGradientBrush brush = new LinearGradientBrush(
                   new Rectangle(bounds.X, bounds.Y - 1, bounds.Width, bounds.Height + 2), top,
                   bottom, LinearGradientMode.Vertical)) {
            graphics.FillPath(brush, path);
        }
    }

    public static void DrawRounded(Graphics graphics, Rectangle bounds, int radius, Color line) {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (GraphicsPath path = RoundedRectangle(bounds, radius))
        using (Pen pen = new Pen(line)) {
            graphics.DrawPath(pen, path);
        }
    }

    public static void DrawText(Graphics graphics, string value, Font font, Rectangle bounds,
                                Color colour, TextFormatFlags flags) {
        TextRenderer.DrawText(graphics, value, font, bounds, colour,
                              flags | TextFormatFlags.NoPrefix);
    }

    // A label that wraps its text at the given width and is exactly as tall as the text needs.
    public static Label MakeWrappedLabel(string value, Font font, Color colour, Color back,
                                         int width) {
        Label label = MakeLabel(value, font, colour, back);
        label.AutoEllipsis = false;
        label.AutoSize = false;
        Size needed = TextRenderer.MeasureText(value, font, new Size(width, int.MaxValue),
                                               TextFormatFlags.WordBreak |
                                                   TextFormatFlags.NoPrefix);
        label.Size = new Size(width, needed.Height + Scaled(2));
        return label;
    }

    // A plain label in the launcher's colours.
    public static Label MakeLabel(string value, Font font, Color colour, Color back) {
        Label label = new Label();
        label.Text = value;
        label.Font = font;
        label.ForeColor = colour;
        label.BackColor = back;
        label.UseMnemonic = false;
        label.AutoEllipsis = true;
        return label;
    }
}

} // namespace TruthXR.Launcher
