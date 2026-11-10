using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TruthXR.Launcher {

// Base for the launcher's own controls: drawn by hand, without flicker.
class ThemedControl : Control {
    protected bool hovered;
    protected bool pressed;

    public ThemedControl() {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor,
                 true);
        Font = Theme.body_font;
        ForeColor = Theme.text;
    }

    protected override void OnMouseEnter(EventArgs e) {
        hovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e) {
        hovered = false;
        pressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e) {
        if (e.Button == MouseButtons.Left) {
            pressed = true;
            Focus();
            Invalidate();
        }
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e) {
        pressed = false;
        Invalidate();
        base.OnMouseUp(e);
    }

    protected override void OnGotFocus(EventArgs e) {
        Invalidate();
        base.OnGotFocus(e);
    }

    protected override void OnLostFocus(EventArgs e) {
        Invalidate();
        base.OnLostFocus(e);
    }

    protected override void OnEnabledChanged(EventArgs e) {
        Invalidate();
        base.OnEnabledChanged(e);
    }

    protected override void OnTextChanged(EventArgs e) {
        Invalidate();
        base.OnTextChanged(e);
    }

    // A thin ring around the control while it has keyboard focus.
    protected void DrawFocus(Graphics graphics, int radius) {
        if (!Focused || !ShowFocusCues) {
            return;
        }
        Rectangle ring = new Rectangle(1, 1, Width - 3, Height - 3);
        using (Pen pen = new Pen(Color.FromArgb(160, Theme.text))) {
            pen.DashStyle = DashStyle.Dot;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = Theme.RoundedRectangle(ring, radius)) {
                graphics.DrawPath(pen, path);
            }
        }
    }
}

// A rounded panel that groups related controls, with an optional heading. Its BackColor is the
// card colour, so the controls placed on it pick that up; the corners are painted in the colour
// of whatever the card sits on.
class Card : Panel {
    string heading = "";

    public Card() {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw,
                 true);
        BackColor = Theme.card;
    }

    public string Heading {
        get { return heading; }
        set {
            heading = value;
            Invalidate();
        }
    }

    // Where the content goes, below the heading.
    public int ContentTop {
        get { return string.IsNullOrEmpty(heading) ? Theme.Scaled(16) : Theme.Scaled(44); }
    }

    protected override void OnPaint(PaintEventArgs e) {
        Graphics graphics = e.Graphics;
        graphics.Clear(Parent != null ? Parent.BackColor : Theme.window);
        Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
        Theme.FillRounded(graphics, bounds, Theme.Scaled(4), BackColor);
        Theme.DrawRounded(graphics, bounds, Theme.Scaled(4), Theme.card_border);
        if (!string.IsNullOrEmpty(heading)) {
            // The heading in spaced gold capitals, with a short gold line under it.
            Rectangle heading_bounds = new Rectangle(Theme.Scaled(18), Theme.Scaled(12),
                                                     Width - Theme.Scaled(36), Theme.Scaled(22));
            Theme.DrawText(graphics, Theme.Spaced(heading.ToUpperInvariant()), Theme.label_font,
                           heading_bounds, Theme.accent,
                           TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            using (SolidBrush brush = new SolidBrush(Theme.accent_dark)) {
                graphics.FillRectangle(brush, Theme.Scaled(18), Theme.Scaled(35), Theme.Scaled(28),
                                       Math.Max(1, Theme.Scaled(2)));
            }
        }
    }
}

enum ButtonKind {
    Primary,
    Secondary,
    Quiet,
}

// A flat, rounded button.
class FlatButton : ThemedControl {
    ButtonKind kind = ButtonKind.Secondary;

    public FlatButton() {
        SetStyle(ControlStyles.Selectable | ControlStyles.StandardClick, true);
        SetStyle(ControlStyles.StandardDoubleClick, false);
        TabStop = true;
        Cursor = Cursors.Hand;
        Font = Theme.button_font;
        Height = Theme.Scaled(36);
    }

    public ButtonKind Kind {
        get { return kind; }
        set {
            kind = value;
            Invalidate();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e) {
        if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) {
            e.Handled = true;
            OnClick(EventArgs.Empty);
        }
        base.OnKeyDown(e);
    }

    protected override bool IsInputKey(Keys key_data) {
        if (key_data == Keys.Enter) {
            return true;
        }
        return base.IsInputKey(key_data);
    }

    protected override void OnPaint(PaintEventArgs e) {
        Graphics graphics = e.Graphics;
        graphics.Clear(BackColor);
        Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
        int radius = Theme.Scaled(4);

        Color fill;
        Color label = Theme.text;
        if (kind == ButtonKind.Primary) {
            fill = pressed ? Theme.accent_pressed : (hovered ? Theme.accent_hover : Theme.accent);
            label = Theme.on_accent;
        } else if (kind == ButtonKind.Secondary) {
            fill = pressed ? Theme.card_border : (hovered ? Theme.input_hover : Theme.input);
        } else {
            fill = hovered || pressed ? Theme.input : BackColor;
            label = hovered ? Theme.text : Theme.muted;
        }
        if (!Enabled) {
            fill = kind == ButtonKind.Quiet ? BackColor : Theme.input;
            label = Theme.faint;
        }

        if (kind == ButtonKind.Primary && Enabled) {
            // The main button glows a little brighter at the top.
            Color top = pressed ? Theme.accent_pressed : (hovered ? Color.FromArgb(245, 204, 120)
                                                                  : Theme.accent_hover);
            Theme.FillRoundedGradient(graphics, bounds, radius, top, fill);
        } else if (fill != BackColor) {
            Theme.FillRounded(graphics, bounds, radius, fill);
        }
        string shown = kind == ButtonKind.Primary ? Theme.Spaced(Text.ToUpperInvariant()) : Text;
        Theme.DrawText(graphics, shown,
                       Font, bounds, label,
                       TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                           TextFormatFlags.EndEllipsis);
        DrawFocus(graphics, radius);
    }
}

// A row of options of which exactly one is chosen, like a set of radio buttons.
class SegmentedControl : ThemedControl {
    string[] options = new string[0];
    int selected_index;
    int hovered_index = -1;
    bool allow_none;

    public event EventHandler SelectionChanged;

    public SegmentedControl() {
        SetStyle(ControlStyles.Selectable, true);
        SetStyle(ControlStyles.StandardDoubleClick, false);
        TabStop = true;
        Cursor = Cursors.Hand;
        Height = Theme.Scaled(36);
    }

    public string[] Options {
        get { return options; }
        set {
            options = value ?? new string[0];
            selected_index = Math.Max(MinimumIndex, Math.Min(selected_index, options.Length - 1));
            Invalidate();
        }
    }

    // When true, no option may be chosen (SelectedIndex -1), as for the presets when the
    // settings were changed by hand.
    public bool AllowNone {
        get { return allow_none; }
        set {
            allow_none = value;
            Invalidate();
        }
    }

    int MinimumIndex {
        get { return allow_none ? -1 : 0; }
    }

    public int SelectedIndex {
        get { return selected_index; }
        set {
            int clamped = Math.Max(MinimumIndex, Math.Min(value, options.Length - 1));
            if (clamped == selected_index) {
                return;
            }
            selected_index = clamped;
            Invalidate();
            if (SelectionChanged != null) {
                SelectionChanged(this, EventArgs.Empty);
            }
        }
    }

    int IndexAt(int x) {
        if (options.Length == 0 || Width <= 0) {
            return -1;
        }
        return Math.Max(0, Math.Min(options.Length - 1, x * options.Length / Width));
    }

    protected override void OnMouseMove(MouseEventArgs e) {
        int index = IndexAt(e.X);
        if (index != hovered_index) {
            hovered_index = index;
            Invalidate();
        }
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e) {
        hovered_index = -1;
        base.OnMouseLeave(e);
    }

    protected override void OnMouseClick(MouseEventArgs e) {
        if (e.Button == MouseButtons.Left) {
            int index = IndexAt(e.X);
            if (index >= 0) {
                SelectedIndex = index;
            }
        }
        base.OnMouseClick(e);
    }

    protected override bool IsInputKey(Keys key_data) {
        if (key_data == Keys.Left || key_data == Keys.Right) {
            return true;
        }
        return base.IsInputKey(key_data);
    }

    protected override void OnKeyDown(KeyEventArgs e) {
        if (e.KeyCode == Keys.Left) {
            SelectedIndex = Math.Max(0, selected_index - 1);
            e.Handled = true;
        } else if (e.KeyCode == Keys.Right) {
            SelectedIndex = selected_index + 1;
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e) {
        Graphics graphics = e.Graphics;
        graphics.Clear(BackColor);
        Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
        int radius = Theme.Scaled(4);
        Theme.FillRounded(graphics, bounds, radius, Theme.input);
        if (options.Length == 0) {
            return;
        }

        int inset = Theme.Scaled(3);
        for (int i = 0; i < options.Length; ++i) {
            int left = i * Width / options.Length;
            int right = (i + 1) * Width / options.Length;
            Rectangle segment = new Rectangle(left + inset, inset, right - left - inset * 2,
                                              Height - inset * 2 - 1);
            Color label = Theme.muted;
            if (i == selected_index) {
                Theme.FillRounded(graphics, segment, radius - inset,
                                  Enabled ? Theme.accent : Theme.card_border);
                label = Enabled ? Theme.on_accent : Theme.muted;
            } else if (i == hovered_index && Enabled) {
                Theme.FillRounded(graphics, segment, radius - inset, Theme.input_hover);
                label = Theme.text;
            }
            if (!Enabled && i != selected_index) {
                label = Theme.faint;
            }
            Theme.DrawText(graphics, options[i], i == selected_index ? Theme.button_font : Font,
                           segment, label,
                           TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                               TextFormatFlags.EndEllipsis);
        }
        DrawFocus(graphics, radius);
    }
}

// The colours of the list a Dropdown opens.
class DarkMenuColours : ProfessionalColorTable {
    public override Color ToolStripDropDownBackground {
        get { return Theme.input; }
    }
    public override Color MenuBorder {
        get { return Theme.card_border; }
    }
    public override Color MenuItemBorder {
        get { return Theme.input_hover; }
    }
    public override Color MenuItemSelected {
        get { return Theme.input_hover; }
    }
    public override Color MenuItemSelectedGradientBegin {
        get { return Theme.input_hover; }
    }
    public override Color MenuItemSelectedGradientEnd {
        get { return Theme.input_hover; }
    }
    public override Color MenuItemPressedGradientBegin {
        get { return Theme.card_border; }
    }
    public override Color MenuItemPressedGradientEnd {
        get { return Theme.card_border; }
    }
    public override Color ImageMarginGradientBegin {
        get { return Theme.input; }
    }
    public override Color ImageMarginGradientMiddle {
        get { return Theme.input; }
    }
    public override Color ImageMarginGradientEnd {
        get { return Theme.input; }
    }
}

// A box that shows the chosen item and opens a list of the others when clicked.
class Dropdown : ThemedControl {
    string[] items = new string[0];
    int selected_index = -1;
    ContextMenuStrip open_menu;

    public event EventHandler SelectionChanged;

    public Dropdown() {
        SetStyle(ControlStyles.Selectable | ControlStyles.StandardClick, true);
        SetStyle(ControlStyles.StandardDoubleClick, false);
        TabStop = true;
        Cursor = Cursors.Hand;
        Height = Theme.Scaled(36);
    }

    public string[] Items {
        get { return items; }
        set {
            items = value ?? new string[0];
            selected_index = Math.Min(selected_index, items.Length - 1);
            Invalidate();
        }
    }

    public int SelectedIndex {
        get { return selected_index; }
        set {
            int clamped = Math.Max(-1, Math.Min(value, items.Length - 1));
            if (clamped == selected_index) {
                return;
            }
            selected_index = clamped;
            Invalidate();
            if (SelectionChanged != null) {
                SelectionChanged(this, EventArgs.Empty);
            }
        }
    }

    protected override void OnClick(EventArgs e) {
        base.OnClick(e);
        OpenList();
    }

    protected override bool IsInputKey(Keys key_data) {
        if (key_data == Keys.Up || key_data == Keys.Down || key_data == Keys.Enter) {
            return true;
        }
        return base.IsInputKey(key_data);
    }

    protected override void OnKeyDown(KeyEventArgs e) {
        bool open_keys = e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter;
        if (open_keys || (e.KeyCode == Keys.Down && e.Alt)) {
            OpenList();
            e.Handled = true;
        } else if (e.KeyCode == Keys.Up) {
            SelectedIndex = Math.Max(0, selected_index - 1);
            e.Handled = true;
        } else if (e.KeyCode == Keys.Down) {
            SelectedIndex = selected_index + 1;
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }

    void OpenList() {
        if (items.Length == 0 || !Enabled) {
            return;
        }
        if (open_menu != null) {
            open_menu.Dispose();
        }
        ContextMenuStrip menu = new ContextMenuStrip();
        menu.Renderer = new ToolStripProfessionalRenderer(new DarkMenuColours());
        menu.BackColor = Theme.input;
        menu.ForeColor = Theme.text;
        menu.Font = Font;
        menu.ShowImageMargin = false;
        menu.ShowCheckMargin = false;
        menu.MinimumSize = new Size(Width, 0);
        for (int i = 0; i < items.Length; ++i) {
            ToolStripMenuItem item = new ToolStripMenuItem(items[i]);
            item.ForeColor = i == selected_index ? Theme.accent_hover : Theme.text;
            item.Font = i == selected_index ? Theme.button_font : Font;
            item.Padding = new Padding(Theme.Scaled(4), Theme.Scaled(5), Theme.Scaled(4),
                                       Theme.Scaled(5));
            int index = i;
            item.Click += delegate { SelectedIndex = index; };
            menu.Items.Add(item);
        }
        menu.Closed += delegate { Invalidate(); };
        open_menu = menu;
        menu.Show(this, new Point(0, Height));
    }

    protected override void Dispose(bool disposing) {
        if (disposing && open_menu != null) {
            open_menu.Dispose();
        }
        base.Dispose(disposing);
    }

    protected override void OnPaint(PaintEventArgs e) {
        Graphics graphics = e.Graphics;
        graphics.Clear(BackColor);
        Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
        int radius = Theme.Scaled(4);
        Color fill = hovered && Enabled ? Theme.input_hover : Theme.input;
        Theme.FillRounded(graphics, bounds, radius, fill);

        int arrow_area = Theme.Scaled(32);
        Rectangle label = new Rectangle(Theme.Scaled(12), 0, Width - Theme.Scaled(12) - arrow_area,
                                        Height);
        string text = selected_index >= 0 ? items[selected_index] : "Choose\u2026";
        Theme.DrawText(graphics, text, Font, label, Enabled ? Theme.text : Theme.faint,
                       TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                           TextFormatFlags.EndEllipsis);

        // A small arrow pointing down.
        int centre_x = Width - arrow_area / 2 - Theme.Scaled(4);
        int centre_y = Height / 2;
        int size = Theme.Scaled(4);
        Point[] arrow = {
            new Point(centre_x - size, centre_y - size / 2),
            new Point(centre_x + size, centre_y - size / 2),
            new Point(centre_x, centre_y + size / 2 + 1),
        };
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (SolidBrush brush = new SolidBrush(Enabled ? Theme.muted : Theme.faint)) {
            graphics.FillPolygon(brush, arrow);
        }
        DrawFocus(graphics, radius);
    }
}

// A horizontal slider for a whole number, moved in steps by mouse or arrow keys.
class Slider : ThemedControl {
    int minimum;
    int maximum = 100;
    int step = 1;
    int current;
    bool dragging;

    public event EventHandler ValueChanged;

    public Slider() {
        SetStyle(ControlStyles.Selectable, true);
        SetStyle(ControlStyles.StandardDoubleClick, false);
        TabStop = true;
        Cursor = Cursors.Hand;
        Height = Theme.Scaled(30);
    }

    public int Minimum {
        get { return minimum; }
        set {
            minimum = value;
            Value = current;
            Invalidate();
        }
    }

    public int Maximum {
        get { return maximum; }
        set {
            maximum = value;
            Value = current;
            Invalidate();
        }
    }

    public int Step {
        get { return step; }
        set { step = Math.Max(1, value); }
    }

    // Changes both ends at once; the value moves inside the new range if it has to.
    public void SetRange(int new_minimum, int new_maximum) {
        minimum = new_minimum;
        maximum = Math.Max(new_minimum, new_maximum);
        Invalidate();
        Value = current;
    }

    public int Value {
        get { return current; }
        set {
            int snapped = minimum + (int)Math.Round((value - minimum) / (double)step) * step;
            int clamped = Math.Max(minimum, Math.Min(maximum, snapped));
            if (clamped == current) {
                return;
            }
            current = clamped;
            Invalidate();
            if (ValueChanged != null) {
                ValueChanged(this, EventArgs.Empty);
            }
        }
    }

    int ThumbRadius {
        get { return Theme.Scaled(9); }
    }

    int TrackLeft {
        get { return ThumbRadius + Theme.Scaled(2); }
    }

    int TrackRight {
        get { return Width - ThumbRadius - Theme.Scaled(2); }
    }

    int PositionOf(int value) {
        if (maximum <= minimum) {
            return TrackLeft;
        }
        double fraction = (value - minimum) / (double)(maximum - minimum);
        return TrackLeft + (int)Math.Round(fraction * (TrackRight - TrackLeft));
    }

    int ValueAt(int x) {
        if (TrackRight <= TrackLeft) {
            return current;
        }
        double fraction = (x - TrackLeft) / (double)(TrackRight - TrackLeft);
        return minimum + (int)Math.Round(Math.Max(0.0, Math.Min(1.0, fraction)) *
                                         (maximum - minimum));
    }

    protected override void OnMouseDown(MouseEventArgs e) {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left && Enabled) {
            dragging = true;
            Value = ValueAt(e.X);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e) {
        base.OnMouseMove(e);
        if (dragging) {
            Value = ValueAt(e.X);
        }
    }

    protected override void OnMouseUp(MouseEventArgs e) {
        dragging = false;
        base.OnMouseUp(e);
    }

    protected override bool IsInputKey(Keys key_data) {
        if (key_data == Keys.Left || key_data == Keys.Right || key_data == Keys.Home ||
            key_data == Keys.End) {
            return true;
        }
        return base.IsInputKey(key_data);
    }

    protected override void OnKeyDown(KeyEventArgs e) {
        if (e.KeyCode == Keys.Left) {
            Value = current - step;
            e.Handled = true;
        } else if (e.KeyCode == Keys.Right) {
            Value = current + step;
            e.Handled = true;
        } else if (e.KeyCode == Keys.Home) {
            Value = minimum;
            e.Handled = true;
        } else if (e.KeyCode == Keys.End) {
            Value = maximum;
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e) {
        Graphics graphics = e.Graphics;
        graphics.Clear(BackColor);
        int track_height = Theme.Scaled(6);
        int middle = Height / 2;
        Rectangle track = new Rectangle(TrackLeft, middle - track_height / 2,
                                        TrackRight - TrackLeft, track_height);
        Theme.FillRounded(graphics, track, track_height / 2, Theme.input);
        int thumb_x = PositionOf(current);
        if (thumb_x > TrackLeft) {
            Rectangle filled = new Rectangle(TrackLeft, track.Top, thumb_x - TrackLeft,
                                             track_height);
            Theme.FillRounded(graphics, filled, track_height / 2,
                              Enabled ? Theme.accent : Theme.card_border);
        }
        int radius = ThumbRadius;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        // A gold ring with a dark middle.
        Color thumb = Enabled ? (hovered || dragging ? Theme.accent_hover : Theme.accent)
                              : Theme.faint;
        int ring = Math.Max(2, Theme.Scaled(3));
        using (SolidBrush brush = new SolidBrush(thumb))
        using (SolidBrush middle_brush = new SolidBrush(Theme.on_accent)) {
            graphics.FillEllipse(brush, thumb_x - radius, middle - radius, radius * 2,
                                 radius * 2);
            graphics.FillEllipse(middle_brush, thumb_x - radius + ring, middle - radius + ring,
                                 (radius - ring) * 2, (radius - ring) * 2);
        }
        DrawFocus(graphics, Theme.Scaled(6));
    }
}

// An on/off switch with its label to the right.
class ToggleSwitch : ThemedControl {
    bool is_checked;

    public event EventHandler CheckedChanged;

    public ToggleSwitch() {
        SetStyle(ControlStyles.Selectable | ControlStyles.StandardClick, true);
        SetStyle(ControlStyles.StandardDoubleClick, false);
        TabStop = true;
        Cursor = Cursors.Hand;
        Height = Theme.Scaled(28);
    }

    public bool Checked {
        get { return is_checked; }
        set {
            if (value == is_checked) {
                return;
            }
            is_checked = value;
            Invalidate();
            if (CheckedChanged != null) {
                CheckedChanged(this, EventArgs.Empty);
            }
        }
    }

    protected override void OnClick(EventArgs e) {
        Checked = !Checked;
        base.OnClick(e);
    }

    protected override void OnKeyDown(KeyEventArgs e) {
        if (e.KeyCode == Keys.Space) {
            Checked = !Checked;
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e) {
        Graphics graphics = e.Graphics;
        graphics.Clear(BackColor);
        int track_height = Theme.Scaled(20);
        int track_width = Theme.Scaled(38);
        int top = (Height - track_height) / 2;
        Rectangle track = new Rectangle(0, top, track_width, track_height);
        Color track_colour = is_checked ? Theme.accent
                                        : (hovered ? Theme.input_hover : Theme.input);
        if (!Enabled) {
            track_colour = Theme.input;
        }
        Theme.FillRounded(graphics, track, track_height / 2, track_colour);

        int knob_size = track_height - Theme.Scaled(6);
        int knob_left = is_checked ? track.Right - knob_size - Theme.Scaled(3) : Theme.Scaled(3);
        Rectangle knob = new Rectangle(knob_left, top + Theme.Scaled(3), knob_size, knob_size);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Color knob_colour = !Enabled ? Theme.faint : (is_checked ? Theme.on_accent : Theme.text);
        using (SolidBrush brush = new SolidBrush(knob_colour)) {
            graphics.FillEllipse(brush, knob);
        }

        Rectangle label = new Rectangle(track_width + Theme.Scaled(12), 0,
                                        Width - track_width - Theme.Scaled(12), Height);
        Theme.DrawText(graphics, Text, Font, label, Enabled ? Theme.text : Theme.faint,
                       TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                           TextFormatFlags.EndEllipsis);
        DrawFocus(graphics, Theme.Scaled(6));
    }
}

// The pages listed down the sidebar: one per row, the chosen one lit with a gold bar on its
// left.
class SideNav : ThemedControl {
    string[] items = new string[0];
    int selected_index;
    int hovered_index = -1;

    public event EventHandler SelectionChanged;

    public SideNav() {
        SetStyle(ControlStyles.Selectable, true);
        SetStyle(ControlStyles.StandardDoubleClick, false);
        TabStop = true;
        Cursor = Cursors.Hand;
        Font = Theme.heading_font;
    }

    public string[] Items {
        get { return items; }
        set {
            items = value ?? new string[0];
            Invalidate();
        }
    }

    public int SelectedIndex {
        get { return selected_index; }
        set {
            int clamped = Math.Max(0, Math.Min(value, items.Length - 1));
            if (clamped == selected_index) {
                return;
            }
            selected_index = clamped;
            Invalidate();
            if (SelectionChanged != null) {
                SelectionChanged(this, EventArgs.Empty);
            }
        }
    }

    public static int RowHeight {
        get { return Theme.Scaled(44); }
    }

    int IndexAt(int y) {
        int index = y / RowHeight;
        return index >= 0 && index < items.Length ? index : -1;
    }

    protected override void OnMouseMove(MouseEventArgs e) {
        int index = IndexAt(e.Y);
        if (index != hovered_index) {
            hovered_index = index;
            Invalidate();
        }
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e) {
        hovered_index = -1;
        base.OnMouseLeave(e);
    }

    protected override void OnMouseClick(MouseEventArgs e) {
        int index = IndexAt(e.Y);
        if (index >= 0) {
            SelectedIndex = index;
        }
        base.OnMouseClick(e);
    }

    protected override bool IsInputKey(Keys key_data) {
        if (key_data == Keys.Up || key_data == Keys.Down) {
            return true;
        }
        return base.IsInputKey(key_data);
    }

    protected override void OnKeyDown(KeyEventArgs e) {
        if (e.KeyCode == Keys.Up) {
            SelectedIndex = selected_index - 1;
            e.Handled = true;
        } else if (e.KeyCode == Keys.Down) {
            SelectedIndex = selected_index + 1;
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e) {
        Graphics graphics = e.Graphics;
        graphics.Clear(BackColor);
        for (int i = 0; i < items.Length; ++i) {
            Rectangle row = new Rectangle(0, i * RowHeight, Width, RowHeight);
            Color label = Theme.muted;
            if (i == selected_index) {
                using (SolidBrush brush = new SolidBrush(Theme.sidebar_selected)) {
                    graphics.FillRectangle(brush, row);
                }
                using (SolidBrush brush = new SolidBrush(Theme.accent)) {
                    graphics.FillRectangle(brush, row.Left, row.Top, Math.Max(2, Theme.Scaled(3)),
                                           row.Height);
                }
                label = Theme.text;
            } else if (i == hovered_index) {
                label = Theme.text;
            }
            Rectangle text_box = new Rectangle(row.Left + Theme.Scaled(22), row.Top,
                                               row.Width - Theme.Scaled(30), row.Height);
            Theme.DrawText(graphics, items[i], Font, text_box, label,
                           TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        }
        DrawFocus(graphics, Theme.Scaled(2));
    }
}

// The column on the left of the main window. It shows the name stacked in large condensed
// capitals, a gold tag line and the version, all drawn as text. The navigation and the Play
// button are placed on it as ordinary controls.
class Sidebar : Panel {
    string title_top = "";
    string title_bottom = "";
    string tag = "";
    string version_line = "";
    string note = "";

    public Sidebar() {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw,
                 true);
        BackColor = Theme.sidebar;
    }

    public void SetText(string new_top, string new_bottom, string new_tag, string new_version,
                        string new_note) {
        title_top = new_top;
        title_bottom = new_bottom;
        tag = new_tag;
        version_line = new_version;
        note = new_note;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e) {
        Graphics graphics = e.Graphics;
        Rectangle bounds = ClientRectangle;
        if (bounds.Width <= 0 || bounds.Height <= 0) {
            return;
        }
        graphics.Clear(BackColor);

        // A soft light at the top that fades into the sidebar colour before the navigation.
        Rectangle glow = new Rectangle(0, 0, bounds.Width, Theme.Scaled(200));
        using (LinearGradientBrush brush = new LinearGradientBrush(
                   new Rectangle(0, -1, glow.Width, glow.Height + 2), Theme.sidebar_top,
                   BackColor, LinearGradientMode.Vertical)) {
            graphics.FillRectangle(brush, glow);
        }

        int left = Theme.Scaled(24);
        int width = bounds.Width - left * 2;
        TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.NoPadding;
        Theme.DrawText(graphics, title_top, Theme.sidebar_title_font,
                       new Rectangle(left, Theme.Scaled(24), width, Theme.Scaled(44)), Theme.text,
                       flags);
        Theme.DrawText(graphics, title_bottom, Theme.sidebar_title_font,
                       new Rectangle(left, Theme.Scaled(62), width, Theme.Scaled(44)), Theme.text,
                       flags);

        using (SolidBrush brush = new SolidBrush(Theme.accent)) {
            graphics.FillRectangle(brush, left + Theme.Scaled(2), Theme.Scaled(112),
                                   Theme.Scaled(36), Math.Max(2, Theme.Scaled(2)));
        }
        Theme.DrawText(graphics, Theme.Spaced(tag), Theme.label_font,
                       new Rectangle(left + Theme.Scaled(1), Theme.Scaled(122), width,
                                     Theme.Scaled(18)),
                       Theme.accent, TextFormatFlags.Left);
        Theme.DrawText(graphics, version_line, Theme.small_font,
                       new Rectangle(left, Theme.Scaled(142), width, Theme.Scaled(18)),
                       Theme.muted, TextFormatFlags.Left);
        Theme.DrawText(graphics, note, Theme.small_font,
                       new Rectangle(left, Theme.Scaled(160), width, Theme.Scaled(34)),
                       Theme.faint, TextFormatFlags.Left | TextFormatFlags.WordBreak);

        // A thin gold edge between the sidebar and the pages.
        using (SolidBrush brush = new SolidBrush(Color.FromArgb(90, Theme.accent))) {
            graphics.FillRectangle(brush, bounds.Width - 1, 0, 1, bounds.Height);
        }
    }
}

// A small coloured circle that shows whether the game is ready.
class StatusDot : Control {
    Color colour = Theme.faint;

    public StatusDot() {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw,
                 true);
        Size = new Size(Theme.Scaled(12), Theme.Scaled(12));
    }

    public Color Colour {
        get { return colour; }
        set {
            colour = value;
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e) {
        e.Graphics.Clear(BackColor);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (SolidBrush halo = new SolidBrush(Color.FromArgb(60, colour)))
        using (SolidBrush brush = new SolidBrush(colour)) {
            e.Graphics.FillEllipse(halo, 0, 0, Width - 1, Height - 1);
            int inset = Math.Max(2, Width / 5);
            e.Graphics.FillEllipse(brush, inset, inset, Width - 1 - inset * 2,
                                   Height - 1 - inset * 2);
        }
    }
}

// A thin progress bar. When the total is not known it shows a moving stripe instead.
class SlimProgressBar : Control {
    double fraction = -1;
    int animation_step;
    readonly Timer animation = new Timer();

    public SlimProgressBar() {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw,
                 true);
        Height = Theme.Scaled(8);
        animation.Interval = 30;
        animation.Tick += delegate {
            animation_step = (animation_step + 2) % 200;
            Invalidate();
        };
        animation.Start();
    }

    // A value from 0 to 1, or below 0 for "not known".
    public double Fraction {
        get { return fraction; }
        set {
            fraction = value;
            Invalidate();
        }
    }

    protected override void Dispose(bool disposing) {
        if (disposing) {
            animation.Dispose();
        }
        base.Dispose(disposing);
    }

    protected override void OnPaint(PaintEventArgs e) {
        Graphics graphics = e.Graphics;
        graphics.Clear(BackColor);
        Rectangle track = new Rectangle(0, 0, Width - 1, Height - 1);
        Theme.FillRounded(graphics, track, Height / 2, Theme.input);
        if (fraction >= 0) {
            int filled = (int)(track.Width * Math.Min(1.0, fraction));
            if (filled > Height) {
                Theme.FillRounded(graphics, new Rectangle(0, 0, filled, Height - 1), Height / 2,
                                  Theme.accent);
            }
            return;
        }
        int stripe_width = Width / 3;
        int left = (animation_step * (Width + stripe_width) / 200) - stripe_width;
        Rectangle stripe = new Rectangle(left, 0, stripe_width, Height - 1);
        stripe.Intersect(track);
        if (stripe.Width > Height) {
            Theme.FillRounded(graphics, stripe, Height / 2, Theme.accent);
        }
    }
}

} // namespace TruthXR.Launcher
