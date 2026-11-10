using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace TruthXR.Launcher {

// The launcher window. The Play tab has the game, headset and controllers on the left, the
// picture settings on the right and Play at the bottom; the Log tab shows what happened.
class MainWindow : ThemedForm {
    static readonly string[] resolution_hints = {
        "The game chooses its own resolution. The safest choice.",
        "1440 \u00D7 1632 per eye (1.5\u00D7 in each direction). Experimental.",
        "8\u00D7 the original pixel count (2.83\u00D7 in each direction). Experimental and very " +
            "demanding.",
    };
    const string ready_hint = "Before you press Play: connect the headset, wake both " +
                              "controllers and start your OpenXR runtime.";
    const string running_hint = "The game is running: put on the headset. Closing this window " +
                                "does not stop the game.";

    readonly InstallPaths paths;
    readonly LauncherSettings settings;
    readonly EmulatorRunner runner;
    readonly ToolTip tips = new ToolTip();

    readonly SideNav tabs;
    readonly Panel play_page;
    readonly LogPanel log_page;

    StatusDot status_dot;
    Label status_title;
    Label status_detail;
    Label path_label;
    FlatButton change_button;
    FlatButton folder_button;
    Dropdown headset_dropdown;
    Label headset_hint;
    Dropdown controller_dropdown;
    FlatButton guide_button;
    ToggleSwitch move_toggle;
    SegmentedControl preset_switch;
    SegmentedControl resolution_switch;
    Label resolution_hint;
    SegmentedControl sharpen_switch;
    Slider fov_slider;
    FlatButton fov_match_button;
    Label fov_hint;
    SegmentedControl mirror_switch;
    Label hint_label;
    FlatButton play_button;

    GameInfo current_game = GameInfo.Missing();
    // The emulator's measurement of the last headset it ran with, or null.
    MeasuredView measured_view;
    bool busy;
    bool game_running;
    bool stop_requested;
    // True while the saved settings are put into the controls, so that doing so does not count
    // as the player changing them (a headset shown at the start must not reset the controllers).
    bool loading;
    // True while a preset fills in the settings it stands for.
    bool applying_preset;

    public MainWindow(InstallPaths paths) {
        this.paths = paths;
        settings = LauncherSettings.Load(paths.settings_file);
        measured_view = Hardware.ReadMeasuredView(paths.user_folder);
        runner = new EmulatorRunner(paths);
        runner.on_log = AddLog;
        runner.on_exited = delegate(int code) { RunOnUi(delegate { OnGameExited(code); }); };
        runner.on_resolution_failed = delegate(string reason) {
            RunOnUi(delegate { OnResolutionFailed(reason); });
        };
        StartLauncherLog();

        Text = "Blood & Truth PC VR v" + BuildInfo.version;
        StartPosition = FormStartPosition.CenterScreen;
        MinimizeBox = true;
        int sidebar_width = Theme.Scaled(250);
        int page_width = Theme.Scaled(670);
        int height = Theme.Scaled(626);
        ClientSize = new Size(sidebar_width + page_width, height);

        // ---- The sidebar: name, pages and the Play button ------------------------------------
        Sidebar sidebar = new Sidebar();
        sidebar.SetText("BLOOD &", "TRUTH", "PC VR",
                        "v" + BuildInfo.version + "   \u00B7   TruthXR, an OpenXR port",
                        "Not affiliated with Sony or London Studio.");
        sidebar.Bounds = new Rectangle(0, 0, sidebar_width, height);
        Controls.Add(sidebar);

        int side_left = Theme.Scaled(24);
        int side_inner = sidebar_width - side_left * 2;
        tabs = new SideNav();
        tabs.Items = new string[] {"Play", "Log"};
        tabs.Bounds = new Rectangle(0, Theme.Scaled(212), sidebar_width - 1,
                                    SideNav.RowHeight * 2);
        tabs.SelectionChanged += delegate { ShowPage(); };
        sidebar.Controls.Add(tabs);

        hint_label = Theme.MakeLabel(ready_hint, Theme.small_font, Theme.muted, Theme.sidebar);
        hint_label.AutoEllipsis = false;
        hint_label.Bounds = new Rectangle(side_left, Theme.Scaled(474), side_inner,
                                          Theme.Scaled(66));
        sidebar.Controls.Add(hint_label);

        play_button = new FlatButton();
        play_button.Text = "Play";
        play_button.Kind = ButtonKind.Primary;
        play_button.Font = Theme.play_font;
        play_button.Bounds = new Rectangle(side_left, height - Theme.Scaled(76), side_inner,
                                           Theme.Scaled(52));
        play_button.Click += delegate { OnPlay(); };
        sidebar.Controls.Add(play_button);

        // ---- The pages, to the right of the sidebar ------------------------------------------
        play_page = new Panel();
        play_page.BackColor = Theme.window;
        play_page.Bounds = new Rectangle(sidebar_width, 0, page_width, height);
        BuildPlayPage(play_page);
        Controls.Add(play_page);

        log_page = new LogPanel(paths, page_width, height - Theme.Scaled(16));
        log_page.Location = new Point(sidebar_width, Theme.Scaled(16));
        log_page.Visible = false;
        Controls.Add(log_page);

        loading = true;
        headset_dropdown.SelectedIndex = settings.headset_index;
        controller_dropdown.SelectedIndex = settings.controller_index;
        move_toggle.Checked = settings.original_move_order;
        resolution_switch.SelectedIndex = settings.resolution_index;
        sharpen_switch.SelectedIndex = settings.sharpen_index;
        UpdateFovRange();
        fov_slider.Value = settings.fov_degrees > 0 ? settings.fov_degrees : MatchingWidth();
        mirror_switch.SelectedIndex = settings.mirror_index;
        loading = false;
        UpdateHeadsetHint();
        UpdatePictureHints();
        UpdateFovHint();
        UpdatePreset();
        ShowGame(current_game);
        ActiveControl = play_button;
    }

    // A setting's name above its control. Left, top and width are in display pixels.
    Label AddRowLabel(Control parent, string text, int left, int top, int width) {
        Label label = Theme.MakeLabel(text, Theme.body_font, Theme.text, parent.BackColor);
        label.Bounds = new Rectangle(left, top, width, Theme.Scaled(20));
        parent.Controls.Add(label);
        return label;
    }

    SegmentedControl AddSegments(Control parent, string[] options, int left, int top,
                                 int width) {
        SegmentedControl segments = new SegmentedControl();
        segments.Options = options;
        segments.Bounds = new Rectangle(left, top, width, Theme.Scaled(36));
        parent.Controls.Add(segments);
        return segments;
    }

    Label AddHint(Control parent, int left, int top, int width, int height) {
        Label hint = Theme.MakeLabel("", Theme.small_font, Theme.muted, parent.BackColor);
        hint.AutoEllipsis = false;
        hint.Bounds = new Rectangle(left, top, width, height);
        parent.Controls.Add(hint);
        return hint;
    }

    // The Play page: three cards stacked down the page, each split into two halves.
    void BuildPlayPage(Panel page) {
        int left = Theme.Scaled(24);
        int inner = page.Width - left * 2;
        int pad = Theme.Scaled(18);
        int card_inner = inner - pad * 2;
        int gap = Theme.Scaled(24);
        int half = (card_inner - gap) / 2;
        int second = pad + half + gap;
        int buttons_width = Theme.Scaled(120);

        // ---- The game ------------------------------------------------------------------------
        Card game_card = new Card();
        game_card.Heading = "Game";
        game_card.Bounds = new Rectangle(left, Theme.Scaled(20), inner, Theme.Scaled(120));
        page.Controls.Add(game_card);

        int text_width = card_inner - buttons_width - Theme.Scaled(12);
        status_dot = new StatusDot();
        status_dot.Location = new Point(pad, Theme.Scaled(51));
        game_card.Controls.Add(status_dot);

        status_title = Theme.MakeLabel("", Theme.heading_font, Theme.text, game_card.BackColor);
        status_title.Bounds = new Rectangle(pad + Theme.Scaled(20), Theme.Scaled(44),
                                            text_width - Theme.Scaled(20), Theme.Scaled(24));
        game_card.Controls.Add(status_title);

        status_detail = Theme.MakeLabel("", Theme.body_font, Theme.muted, game_card.BackColor);
        status_detail.Bounds = new Rectangle(pad + Theme.Scaled(20), Theme.Scaled(68),
                                             text_width - Theme.Scaled(20), Theme.Scaled(20));
        game_card.Controls.Add(status_detail);

        path_label = Theme.MakeLabel("", Theme.small_font, Theme.faint, game_card.BackColor);
        path_label.Bounds = new Rectangle(pad, Theme.Scaled(94), text_width, Theme.Scaled(18));
        game_card.Controls.Add(path_label);

        int buttons_left = pad + card_inner - buttons_width;
        change_button = new FlatButton();
        change_button.Text = "Change\u2026";
        change_button.Bounds = new Rectangle(buttons_left, Theme.Scaled(40), buttons_width,
                                             Theme.Scaled(34));
        change_button.Click += delegate {
            ChooseGame(current_game.status == GameStatus.Missing, null);
        };
        game_card.Controls.Add(change_button);

        folder_button = new FlatButton();
        folder_button.Text = "Show folder";
        folder_button.Kind = ButtonKind.Quiet;
        folder_button.Bounds = new Rectangle(buttons_left, Theme.Scaled(78), buttons_width,
                                             Theme.Scaled(30));
        folder_button.Click += delegate { ShowGameFolder(); };
        game_card.Controls.Add(folder_button);

        // ---- Headset (left half) and controllers (right half) --------------------------------
        Card headset_card = new Card();
        headset_card.Heading = "Headset and controllers";
        headset_card.Bounds = new Rectangle(left, game_card.Bottom + Theme.Scaled(14), inner,
                                            Theme.Scaled(156));
        page.Controls.Add(headset_card);

        AddRowLabel(headset_card, "Your headset", pad, Theme.Scaled(46), half);
        headset_dropdown = new Dropdown();
        string[] headset_names = new string[Hardware.headsets.Length];
        for (int i = 0; i < headset_names.Length; ++i) {
            headset_names[i] = Hardware.headsets[i].name;
        }
        headset_dropdown.Items = headset_names;
        headset_dropdown.Bounds = new Rectangle(pad, Theme.Scaled(68), half, Theme.Scaled(36));
        headset_dropdown.SelectionChanged += delegate { OnHeadsetChanged(); };
        headset_card.Controls.Add(headset_dropdown);

        headset_hint = AddHint(headset_card, pad, Theme.Scaled(108), half, Theme.Scaled(44));

        int guide_width = Theme.Scaled(100);
        AddRowLabel(headset_card, "Controllers", second, Theme.Scaled(46), half);
        controller_dropdown = new Dropdown();
        controller_dropdown.Items = Hardware.controller_names;
        controller_dropdown.Bounds = new Rectangle(second, Theme.Scaled(68),
                                                   half - guide_width - Theme.Scaled(8),
                                                   Theme.Scaled(36));
        controller_dropdown.SelectionChanged += delegate {
            if (controller_dropdown.SelectedIndex >= 0) {
                settings.controller_index = controller_dropdown.SelectedIndex;
                if (!loading) {
                    settings.Save(paths.settings_file);
                }
            }
        };
        tips.SetToolTip(controller_dropdown, "Picking a headset chooses its controllers. Change " +
                                                 "it if you use other ones.");
        headset_card.Controls.Add(controller_dropdown);

        guide_button = new FlatButton();
        guide_button.Text = "Buttons\u2026";
        guide_button.Bounds = new Rectangle(second + half - guide_width, Theme.Scaled(68),
                                            guide_width, Theme.Scaled(36));
        guide_button.Click += delegate { ShowButtonGuide(); };
        tips.SetToolTip(guide_button, "Which button does what.");
        headset_card.Controls.Add(guide_button);

        move_toggle = new ToggleSwitch();
        move_toggle.Text = "Original Move hand order";
        move_toggle.Bounds = new Rectangle(second, Theme.Scaled(114), half, Theme.Scaled(28));
        move_toggle.CheckedChanged += delegate {
            settings.original_move_order = move_toggle.Checked;
            if (!loading) {
                settings.Save(paths.settings_file);
            }
        };
        tips.SetToolTip(move_toggle, "Only for comparing. Off is the corrected hand order, " +
                                         "which is what you want for playing.");
        headset_card.Controls.Add(move_toggle);

        // ---- The picture ---------------------------------------------------------------------
        Card picture_card = new Card();
        picture_card.Heading = "Picture";
        picture_card.Bounds = new Rectangle(left, headset_card.Bottom + Theme.Scaled(14), inner,
                                            page.Height - headset_card.Bottom -
                                                Theme.Scaled(14 + 18));
        page.Controls.Add(picture_card);

        preset_switch = new SegmentedControl();
        preset_switch.AllowNone = true;
        preset_switch.Options = new string[] {"Low", "Medium", "High"};
        preset_switch.SelectedIndex = -1;
        int preset_width = Theme.Scaled(240);
        preset_switch.Bounds = new Rectangle(pad + card_inner - preset_width, Theme.Scaled(10),
                                             preset_width, Theme.Scaled(32));
        preset_switch.SelectionChanged += delegate { ApplyPreset(); };
        tips.SetToolTip(preset_switch, "Low is the lightest on your PC, High the sharpest. None " +
                                           "is lit when you set things by hand.");
        picture_card.Controls.Add(preset_switch);

        // Resolution on the left, sharpening and the desktop mirror on the right.
        AddRowLabel(picture_card, "Resolution", pad, Theme.Scaled(52), half);
        resolution_switch = AddSegments(picture_card, new string[] {"Original", "High", "Ultra"},
                                        pad, Theme.Scaled(74), half);
        resolution_switch.SelectionChanged += delegate {
            settings.resolution_index = resolution_switch.SelectedIndex;
            OnPictureChanged();
        };
        resolution_hint = AddHint(picture_card, pad, Theme.Scaled(114), half, Theme.Scaled(44));

        AddRowLabel(picture_card, "Sharpening", second, Theme.Scaled(52), half);
        sharpen_switch = AddSegments(picture_card, Hardware.sharpen_names, second,
                                     Theme.Scaled(74), half);
        sharpen_switch.SelectionChanged += delegate {
            settings.sharpen_index = sharpen_switch.SelectedIndex;
            OnPictureChanged();
        };
        tips.SetToolTip(sharpen_switch, "Sharpens the picture on its way to the headset. It " +
                                            "costs very little.");

        Label mirror_label = AddRowLabel(picture_card, "Desktop mirror", second,
                                         Theme.Scaled(116), half);
        tips.SetToolTip(mirror_label, "How often the picture on your monitor is updated.");
        mirror_switch = AddSegments(picture_card, new string[] {"30 FPS", "60 FPS", "Uncapped"},
                                    second, Theme.Scaled(138), half);
        mirror_switch.SelectionChanged += delegate {
            settings.mirror_index = mirror_switch.SelectedIndex;
            if (!loading) {
                settings.Save(paths.settings_file);
            }
        };
        tips.SetToolTip(mirror_switch, "How often the picture on your monitor is updated.");

        // The field of view gets the whole width, below both halves.
        int fov_top = Theme.Scaled(186);
        Panel rule = new Panel();
        rule.BackColor = Theme.card_border;
        rule.Bounds = new Rectangle(pad, fov_top - Theme.Scaled(8), card_inner,
                                    Math.Max(1, Theme.Scaled(1)));
        picture_card.Controls.Add(rule);

        AddRowLabel(picture_card, "Field of view", pad, fov_top, card_inner - Theme.Scaled(150));
        fov_match_button = new FlatButton();
        fov_match_button.Text = "Match headset";
        fov_match_button.Kind = ButtonKind.Quiet;
        fov_match_button.Font = Theme.small_font;
        fov_match_button.Bounds = new Rectangle(pad + card_inner - Theme.Scaled(130),
                                                fov_top - Theme.Scaled(4), Theme.Scaled(130),
                                                Theme.Scaled(28));
        fov_match_button.Click += delegate { fov_slider.Value = MatchingWidth(); };
        tips.SetToolTip(fov_match_button, "Sets the field of view to what your headset shows.");
        picture_card.Controls.Add(fov_match_button);

        fov_slider = new Slider();
        fov_slider.SetRange(60, 140);
        fov_slider.Value = 100;
        fov_slider.Bounds = new Rectangle(pad, fov_top + Theme.Scaled(26), card_inner,
                                          Theme.Scaled(30));
        fov_slider.ValueChanged += delegate {
            settings.fov_degrees = fov_slider.Value;
            UpdateFovHint();
            if (!loading) {
                settings.Save(paths.settings_file);
            }
        };
        tips.SetToolTip(fov_slider, "Narrower is sharper, with a dark border around the " +
                                        "picture. Wider fills more of your view but is softer.");
        picture_card.Controls.Add(fov_slider);

        fov_hint = AddHint(picture_card, pad, fov_top + Theme.Scaled(58), card_inner,
                           Theme.Scaled(34));
    }

    protected override void OnShown(EventArgs e) {
        base.OnShown(e);
        AddLog("Launcher " + BuildInfo.version + " started from " + paths.root);
        Locate(true);
    }

    protected override void OnFormClosing(FormClosingEventArgs e) {
        settings.Save(paths.settings_file);
        base.OnFormClosing(e);
    }

    void ShowPage() {
        bool log = tabs.SelectedIndex == 1;
        play_page.Visible = !log;
        log_page.Visible = log;
    }

    // ---- Headset, controllers and picture ---------------------------------------------------

    // Picking a headset picks the controllers it comes with; they can still be changed after.
    void OnHeadsetChanged() {
        int index = headset_dropdown.SelectedIndex;
        if (index < 0) {
            return;
        }
        settings.headset_index = index;
        UpdateHeadsetHint();
        if (loading) {
            return;
        }
        controller_dropdown.SelectedIndex = Hardware.headsets[index].controller;
        UpdateFovRange();
        fov_slider.Value = MatchingWidth();
        UpdateFovHint();
        settings.Save(paths.settings_file);
    }

    double OuterTangent() {
        return Hardware.OuterTangent(measured_view, headset_dropdown.SelectedIndex);
    }

    // The width that fills the chosen headset's whole view.
    int MatchingWidth() {
        return Hardware.WidthAt(OuterTangent(), 1.0);
    }

    // The slider reaches as far as the emulator allows for this headset: 50% to 120% of its view.
    void UpdateFovRange() {
        double tangent = OuterTangent();
        fov_slider.SetRange(Hardware.MinimumWidth(tangent), Hardware.MaximumWidth(tangent));
    }

    void UpdateFovHint() {
        int width = fov_slider.Value;
        int match = MatchingWidth();
        int index = headset_dropdown.SelectedIndex;
        string text = width + "\u00B0 across both eyes. ";
        if (index < 0) {
            text += "Choose your headset to match it.";
        } else if (width == match) {
            string name = Hardware.headsets[index].name;
            text += Hardware.MeasurementFits(measured_view, index)
                        ? "Fills your " + name + "'s whole view, as the emulator measured it."
                        : "Fills a " + name + "'s view (its advertised width, until the game " +
                              "has run on it once).";
        } else if (width < match) {
            text += "Narrower than your headset (" + match + "\u00B0): sharper, with a dark " +
                    "border.";
        } else {
            text += "Wider than your headset (" + match + "\u00B0): the edges are drawn but " +
                    "not seen, so it is softer.";
        }
        fov_hint.Text = text;
        fov_hint.ForeColor = width == match || index < 0 ? Theme.muted : Theme.warning;
    }

    void UpdateHeadsetHint() {
        int index = headset_dropdown.SelectedIndex;
        headset_hint.Text = index >= 0 ? Hardware.headsets[index].hint : "";
    }

    void ShowButtonGuide() {
        int controller = Math.Max(0, controller_dropdown.SelectedIndex);
        using (ButtonGuideDialog dialog = new ButtonGuideDialog(controller)) {
            dialog.ShowDialog(this);
        }
    }

    // A preset fills in the resolution and sharpening it stands for.
    void ApplyPreset() {
        int index = preset_switch.SelectedIndex;
        if (index < 0 || loading || applying_preset) {
            return;
        }
        applying_preset = true;
        resolution_switch.SelectedIndex = Hardware.presets[index].resolution_index;
        sharpen_switch.SelectedIndex = Hardware.presets[index].sharpen_index;
        applying_preset = false;
        OnPictureChanged();
    }

    void OnPictureChanged() {
        UpdatePictureHints();
        if (loading || applying_preset) {
            return;
        }
        UpdatePreset();
        settings.Save(paths.settings_file);
    }

    // Lights the preset that matches the current settings, or none when they were set by hand.
    void UpdatePreset() {
        applying_preset = true;
        preset_switch.SelectedIndex = Hardware.MatchingPreset(settings.resolution_index,
                                                              settings.sharpen_index);
        applying_preset = false;
    }

    void UpdatePictureHints() {
        resolution_hint.Text = resolution_hints[resolution_switch.SelectedIndex];
        resolution_hint.ForeColor = resolution_switch.SelectedIndex == 0 ? Theme.muted
                                                                          : Theme.warning;
    }

    // ---- Finding the game --------------------------------------------------------------------

    // Looks for the game on another thread (checking the version takes a moment) and shows the
    // result. With ask_if_missing, the "not found" window opens when nothing is found.
    void Locate(bool ask_if_missing) {
        SetBusy(true, "Looking for the game\u2026");
        string remembered = settings.game_path;
        RunInBackground(delegate { return GameLocator.Find(remembered, paths.games_folder); },
                        delegate(GameInfo info) {
                            SetBusy(false, null);
                            ShowGame(info);
                            if (info.status == GameStatus.Missing && ask_if_missing) {
                                ChooseGame(true, null);
                            }
                        });
    }

    // Checks a file or folder the player chose.
    void InspectChoice(string chosen) {
        SetBusy(true, "Checking your choice\u2026");
        RunInBackground(delegate { return GameLocator.Inspect(chosen); },
                        delegate(GameInfo info) {
                            SetBusy(false, null);
                            if (info.status == GameStatus.Missing) {
                                ShowGame(current_game);
                                int choice = FancyDialog.Ask(
                                    this, "Nothing found there",
                                    "There is no eboot.bin or .pkg in\n" + chosen,
                                    DialogKind.Warning, "Choose again", "Cancel");
                                if (choice == 0) {
                                    ChooseGame(false, null);
                                }
                                return;
                            }
                            ShowGame(info);
                            if (info.status == GameStatus.Problem) {
                                ChooseGame(false, info.problem);
                            }
                        });
    }

    void ChooseGame(bool missing, string problem) {
        using (NotFoundDialog dialog = new NotFoundDialog(paths.games_folder, missing, problem)) {
            if (dialog.ShowDialog(this) != DialogResult.OK) {
                return;
            }
            if (dialog.look_again) {
                Locate(false);
            } else if (dialog.chosen_path != null) {
                AddLog("Game chosen: " + dialog.chosen_path);
                InspectChoice(dialog.chosen_path);
            }
        }
    }

    void ShowGame(GameInfo info) {
        current_game = info;
        string path = info.status == GameStatus.Package ? info.package_path : info.eboot_path;
        switch (info.status) {
        case GameStatus.Ready:
            status_dot.Colour = Theme.good;
            status_title.Text = "Ready to play";
            status_detail.Text = info.title + (info.title_id.Length > 0 ? "   \u00B7   " +
                                                   info.title_id : "") +
                                 (info.version.Length > 0 ? "   \u00B7   version " + info.version
                                                          : "");
            break;
        case GameStatus.Package:
            status_dot.Colour = Theme.warning;
            status_title.Text = "Package found";
            status_detail.Text = "It is unpacked once when you press Play.";
            break;
        case GameStatus.Problem:
            status_dot.Colour = Theme.bad;
            status_title.Text = "This copy can't be used";
            status_detail.Text = info.problem;
            break;
        default:
            status_dot.Colour = Theme.bad;
            status_title.Text = "Blood & Truth wasn't found";
            status_detail.Text = "Show the launcher where your copy is.";
            break;
        }
        path_label.Text = path;
        tips.SetToolTip(path_label, path);
        tips.SetToolTip(status_detail, status_detail.Text);
        change_button.Text = info.status == GameStatus.Missing ? "Find game\u2026" : "Change\u2026";
        folder_button.Enabled = path.Length > 0;

        // Only something playable (or unpackable) is worth remembering.
        if (info.status == GameStatus.Ready || info.status == GameStatus.Package) {
            if (settings.game_path != info.RememberedPath) {
                settings.game_path = info.RememberedPath;
                settings.Save(paths.settings_file);
            }
            AddLog(info.status == GameStatus.Ready ? "Game ready: " + info.eboot_path
                                                   : "Package found: " + info.package_path);
        } else if (info.status == GameStatus.Problem) {
            AddLog("The game can't be used: " + info.problem + " (" + path + ")");
        }
    }

    void ShowGameFolder() {
        string path = current_game.status == GameStatus.Package ? current_game.package_path
                                                                : current_game.eboot_path;
        if (path.Length == 0) {
            return;
        }
        try {
            System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + path + "\"");
        } catch (Exception error) {
            FancyDialog.Ask(this, "Could not open the folder", error.Message, DialogKind.Error,
                            "OK");
        }
    }

    // ---- Playing -----------------------------------------------------------------------------

    void OnPlay() {
        if (game_running) {
            AskToStop();
            return;
        }
        if (busy) {
            return;
        }
        if (!CloseRunningEmulators()) {
            return;
        }

        switch (current_game.status) {
        case GameStatus.Ready:
            Launch(current_game.eboot_path);
            break;
        case GameStatus.Package:
            string eboot = Unpack(current_game.package_path);
            if (eboot != null) {
                // Check the unpacked game, then start it if it is the right one.
                SetBusy(true, "Checking the unpacked game\u2026");
                RunInBackground(delegate { return GameLocator.Inspect(eboot); },
                                delegate(GameInfo info) {
                                    SetBusy(false, null);
                                    ShowGame(info);
                                    if (info.status == GameStatus.Ready) {
                                        Launch(info.eboot_path);
                                    } else {
                                        ChooseGame(false, info.problem);
                                    }
                                });
            }
            break;
        case GameStatus.Problem:
            int choice = FancyDialog.Ask(this, "This copy of Blood & Truth can't be used",
                                         current_game.problem + "\n\n" + current_game.eboot_path,
                                         DialogKind.Error, "Choose another copy\u2026", "Cancel");
            if (choice == 0) {
                ChooseGame(false, current_game.problem);
            }
            break;
        default:
            ChooseGame(true, null);
            break;
        }
    }

    // Two emulators cannot share the headset, so any shadPS4 that is already running has to
    // close first. Returns false when the player would rather not, or it would not close.
    bool CloseRunningEmulators() {
        foreach (RunningEmulator running in runner.FindRunning()) {
            string where = running.path.Length > 0 ? running.path : "(Windows didn't say where)";
            string title;
            string message;
            if (running.is_ours) {
                title = "The game is already running";
                message = "The emulator from this launcher is still open, perhaps from an " +
                          "earlier start that didn't close properly.\n\nClose it and start again?";
            } else {
                title = "Another emulator is running";
                message = "Another copy of shadPS4 is open:\n" + where + "\n\nTwo emulators " +
                          "can't use the headset at the same time. Close it? Anything unsaved " +
                          "in that game is lost.";
            }
            if (FancyDialog.Ask(this, title, message, DialogKind.Warning, "Close it",
                                "Cancel") != 0) {
                return false;
            }
            if (!runner.Close(running)) {
                FancyDialog.Ask(this, "It didn't close",
                                "Close it yourself (for example in Task Manager), then press " +
                                    "Play again.",
                                DialogKind.Error, "OK");
                return false;
            }
            AddLog("Closed a running emulator: " + where);
        }
        return true;
    }

    // Unpacks a .pkg. Returns the path of the unpacked eboot.bin, or null if it was not done.
    string Unpack(string package) {
        if (paths.pkgtool == null) {
            FancyDialog.Ask(this, "PkgTool is missing",
                            "PkgTool, which unpacks packages, should be in pc-vr\\pkgtool. " +
                                "Extract the whole release ZIP again.",
                            DialogKind.Error, "OK");
            return null;
        }
        // The game is unpacked next to its package, so it goes wherever the player keeps it.
        PackageUnpacker unpacker = new PackageUnpacker(package, Path.GetDirectoryName(package),
                                                       paths.pkgtool);
        int answer = FancyDialog.Ask(
            this, "Unpack the game first?",
            "Your copy is a package:\n" + Path.GetFileName(package) + "  (" +
                PackageUnpacker.Gigabytes(unpacker.PackageSize) + ")\n\nIt has to be unpacked " +
                "once before it can be played, into\n" + unpacker.TargetFolder + "\n\nThat " +
                "takes a few minutes and about " +
                PackageUnpacker.Gigabytes(unpacker.SpaceNeeded) + " of free space.",
            DialogKind.Question, "Unpack now", "Cancel");
        if (answer != 0) {
            return null;
        }

        AddLog("Unpacking " + package);
        string eboot;
        using (UnpackDialog dialog = new UnpackDialog(unpacker)) {
            dialog.ShowDialog(this);
            if (dialog.was_cancelled) {
                AddLog("Unpacking was cancelled.");
                return null;
            }
            if (dialog.error_message != null || dialog.eboot_path == null) {
                string reason = dialog.error_message ?? "It stopped without saying why.";
                AddLog("Unpacking failed: " + reason);
                FancyDialog.Ask(this, "Unpacking didn't work", reason, DialogKind.Error, "OK");
                return null;
            }
            eboot = dialog.eboot_path;
        }
        AddLog("Unpacked into " + Path.GetDirectoryName(eboot));
        settings.game_path = eboot;
        settings.Save(paths.settings_file);

        int delete = FancyDialog.Ask(
            this, "Delete the package?",
            "The game is unpacked and ready. The package isn't needed any more:\n" + package +
                "\n\nDelete it to free " + PackageUnpacker.Gigabytes(unpacker.PackageSize) +
                "? Keep it if it is your only copy of the game.",
            DialogKind.Question, "Keep it", "Delete it");
        if (delete == 1) {
            try {
                File.Delete(package);
                AddLog("Deleted the package " + package);
            } catch (Exception error) {
                FancyDialog.Ask(this, "Could not delete the package", error.Message,
                                DialogKind.Error, "OK");
            }
        }
        return eboot;
    }

    void Launch(string eboot) {
        settings.fov_degrees = fov_slider.Value;
        settings.fov_percent_value = fov_slider.Value == MatchingWidth()
                                         ? "100"
                                         : Hardware.PercentFor(fov_slider.Value, OuterTangent());
        settings.Save(paths.settings_file);
        try {
            runner.Start(eboot, settings);
        } catch (Exception error) {
            AddLog("The game didn't start: " + error.Message);
            FancyDialog.Ask(this, "The game didn't start", error.Message, DialogKind.Error, "OK");
            return;
        }
        SetRunning(true);
    }

    void AskToStop() {
        int choice = FancyDialog.Ask(this, "Stop the game?",
                                     "Anything since your last save in the game is lost.",
                                     DialogKind.Question, "Stop the game", "Keep playing");
        if (choice == 0) {
            stop_requested = true;
            runner.Stop();
        }
    }

    void OnGameExited(int code) {
        SetRunning(false);
        // The emulator may have measured the headset during the game: if the slider was on the
        // old match, it moves to the new one.
        int old_match = MatchingWidth();
        measured_view = Hardware.ReadMeasuredView(paths.user_folder);
        UpdateFovRange();
        if (fov_slider.Value == old_match) {
            fov_slider.Value = MatchingWidth();
        }
        UpdateFovHint();
        AddLog("The game ended (exit code " + FormatExitCode(code) + ").");
        bool was_stopped = stop_requested;
        stop_requested = false;
        if (code == 0 || was_stopped) {
            return;
        }
        int choice = FancyDialog.Ask(
            this, "Blood & Truth closed with an error",
            "The emulator stopped with exit code " + FormatExitCode(code) + ". The game log " +
                "usually shows what happened just before.",
            DialogKind.Error, "Show the log", "Open log folder", "Close");
        if (choice == 0) {
            tabs.SelectedIndex = 1;
            log_page.ShowGameLog();
        } else if (choice == 1) {
            LogPanel.OpenLogFolder(this, paths);
        }
    }

    void OnResolutionFailed(string reason) {
        if (!game_running) {
            return;
        }
        hint_label.ForeColor = Theme.warning;
        hint_label.Text = "The " + settings.ResolutionName + " resolution couldn't be applied, " +
                          "so the game uses its own. Details are in the Log tab.";
    }

    // Windows crash codes are large numbers that read better in hexadecimal (0xC0000005).
    static string FormatExitCode(int code) {
        if (code < 0 || code > 0xFFFF) {
            return "0x" + unchecked((uint)code).ToString("X8");
        }
        return code.ToString();
    }

    // ---- Window state ------------------------------------------------------------------------

    void SetRunning(bool running) {
        game_running = running;
        play_button.Text = running ? "Stop game" : "Play";
        play_button.Kind = running ? ButtonKind.Secondary : ButtonKind.Primary;
        hint_label.ForeColor = Theme.muted;
        hint_label.Text = running ? running_hint : ready_hint;
        // The settings only count when the game starts, so they wait while it runs.
        change_button.Enabled = !running;
        headset_dropdown.Enabled = !running;
        controller_dropdown.Enabled = !running;
        move_toggle.Enabled = !running;
        preset_switch.Enabled = !running;
        resolution_switch.Enabled = !running;
        sharpen_switch.Enabled = !running;
        fov_slider.Enabled = !running;
        fov_match_button.Enabled = !running;
        mirror_switch.Enabled = !running;
    }

    void SetBusy(bool is_busy, string message) {
        busy = is_busy;
        play_button.Enabled = !is_busy;
        change_button.Enabled = !is_busy && !game_running;
        if (is_busy) {
            status_dot.Colour = Theme.faint;
            status_title.Text = message;
            status_detail.Text = "";
            path_label.Text = "";
        }
    }

    // ---- Helpers -----------------------------------------------------------------------------

    void StartLauncherLog() {
        try {
            Directory.CreateDirectory(paths.log_folder);
            File.WriteAllText(paths.launcher_log, "");
        } catch (Exception) {
        }
    }

    // Adds a line to the Log tab and to pc-vr/user/log/launcher.log. Safe from any thread.
    void AddLog(string message) {
        string line = DateTime.Now.ToString("HH:mm:ss") + "  " + message;
        try {
            File.AppendAllText(paths.launcher_log, line + Environment.NewLine);
        } catch (Exception) {
        }
        RunOnUi(delegate { log_page.AddLauncherLine(line); });
    }

    void RunOnUi(MethodInvoker action) {
        if (IsDisposed) {
            return;
        }
        if (!InvokeRequired && log_page != null) {
            action();
            return;
        }
        try {
            if (IsHandleCreated) {
                BeginInvoke(action);
            }
        } catch (Exception) {
        }
    }

    // Runs work on another thread and hands its result to done on the window's thread.
    void RunInBackground(Func<GameInfo> work, Action<GameInfo> done) {
        Thread worker = new Thread(delegate() {
            GameInfo result;
            try {
                result = work();
            } catch (Exception error) {
                result = new GameInfo();
                result.status = GameStatus.Problem;
                result.problem = "The game could not be checked: " + error.Message;
            }
            RunOnUi(delegate { done(result); });
        });
        worker.IsBackground = true;
        worker.Start();
    }
}

} // namespace TruthXR.Launcher
