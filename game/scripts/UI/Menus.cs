using System;
using DatacenterArchitect.Core;
using DatacenterArchitect.Sim;
using Godot;

namespace DatacenterArchitect.UI;

/// <summary>Title screen: a panel on the left over the slowly turning world.</summary>
public partial class MainMenu : Control
{
    public Action Continue, NewGame, LoadGame, Options, Info, ToggleLanguage, Quit;
    Button _continue, _lang;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        var panel = new PanelContainer();
        panel.SetAnchorsPreset(LayoutPreset.LeftWide);
        panel.CustomMinimumSize = new Vector2(380, 0);
        panel.Position = new Vector2(24, 24);
        panel.OffsetLeft = 24; panel.OffsetTop = 24; panel.OffsetBottom = -24; panel.OffsetRight = 404;
        panel.AddThemeStyleboxOverride("panel", Ui.Box(Brand.Panel, 12, Brand.Line, 1, 28, true));
        AddChild(panel);
        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 8);
        panel.AddChild(v);
        v.AddChild(new TextureRect { Texture = GD.Load<Texture2D>("res://icon.svg"), CustomMinimumSize = new Vector2(64, 64), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspect, SizeFlagsHorizontal = SizeFlags.ShrinkBegin });
        var title = new Label { Text = "Datacenter\nArchitect", Uppercase = true };
        title.AddThemeFontOverride("font", Ui.DisplayBold); title.AddThemeFontSizeOverride("font_size", 46);
        title.AddThemeConstantOverride("line_spacing", -8);
        v.AddChild(title);
        v.AddChild(Ui.Text("menu_tagline", 16, Brand.InkDim));
        v.AddChild(new Control { CustomMinimumSize = new Vector2(0, 18) });
        _continue = Ui.Primary("menu_continue", () => Continue?.Invoke());
        v.AddChild(_continue);
        v.AddChild(Big("menu_new_game", () => NewGame?.Invoke()));
        v.AddChild(Big("menu_load", () => LoadGame?.Invoke()));
        v.AddChild(Big("menu_options", () => Options?.Invoke()));
        v.AddChild(Big("menu_info", () => Info?.Invoke()));
        _lang = Big("", () => ToggleLanguage?.Invoke());
        _lang.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        v.AddChild(_lang);
        v.AddChild(Big("menu_quit", () => Quit?.Invoke()));
        v.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
        var ver = Ui.Text(Loc.T("info_version", GameVersion.Text, Loc.T("channel_" + GameVersion.BuildChannel)), 13, Brand.InkDim);
        ver.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        v.AddChild(ver);
        Refresh();
    }

    static Button Big(string key, Action a)
    {
        var b = new Button { Text = key, Alignment = HorizontalAlignment.Left, CustomMinimumSize = new Vector2(0, 44) };
        b.AddThemeFontOverride("font", Ui.DisplaySemi); b.AddThemeFontSizeOverride("font_size", 22);
        b.Pressed += a;
        return b;
    }

    public void Refresh()
    {
        _continue.Visible = SaveSystem.List().Count > 0;
        _lang.Text = Loc.T("menu_language") + ": " + (Loc.Language == "en" ? "English" : "Nederlands");
    }
}

/// <summary>New game settings: company, difficulty, AI competitors and creative mode with its switches.</summary>
public partial class NewGameDialog : Modal
{
    public NewGameDialog(Action<NewGameOptions> start) : base("new_title", 560)
    {
        var o = new NewGameOptions();
        Body.AddChild(Ui.Heading("new_company"));
        var name = new LineEdit { Text = Loc.T("new_company_default"), MaxLength = 40 };
        Body.AddChild(name);

        Body.AddChild(Ui.Heading("new_mode"));
        var modes = new HBoxContainer(); modes.AddThemeConstantOverride("separation", 8);
        var career = new Button { Text = "new_mode_career", ToggleMode = true, ButtonPressed = true, SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 40) };
        var creative = new Button { Text = "new_mode_creative", ToggleMode = true, SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 40) };
        var group = new ButtonGroup(); career.ButtonGroup = group; creative.ButtonGroup = group;
        modes.AddChild(career); modes.AddChild(creative);
        Body.AddChild(modes);

        var careerBox = new VBoxContainer(); careerBox.AddThemeConstantOverride("separation", 8);
        careerBox.AddChild(Ui.Heading("new_difficulty"));
        var diff = new OptionButton();
        diff.AddItem(Loc.T("new_easy")); diff.AddItem(Loc.T("new_normal")); diff.AddItem(Loc.T("new_hard"));
        diff.Selected = 1;
        careerBox.AddChild(diff);
        careerBox.AddChild(Ui.Text("new_career_help", 13, Brand.InkDim));
        Body.AddChild(careerBox);

        var creativeBox = new VBoxContainer { Visible = false }; creativeBox.AddThemeConstantOverride("separation", 2);
        creativeBox.AddChild(Ui.Text("new_creative_help", 13, Brand.InkDim));
        var c = CreativeOptions.All();
        var toggles = new (string key, Func<bool> get, Action<bool> set)[]
        {
            ("cr_unlimited_money", () => c.UnlimitedMoney, v => c.UnlimitedMoney = v),
            ("cr_no_power", () => c.NoPowerLimit, v => c.NoPowerLimit = v),
            ("cr_no_cooling", () => c.NoCoolingLimit, v => c.NoCoolingLimit = v),
            ("cr_no_network", () => c.NoNetworkLimit, v => c.NoNetworkLimit = v),
            ("cr_no_breakdowns", () => c.NoBreakdowns, v => c.NoBreakdowns = v),
            ("cr_instant_build", () => c.InstantBuild, v => c.InstantBuild = v),
            ("cr_all_unlocks", () => c.AllUnlocks, v => c.AllUnlocks = v),
        };
        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 12);
        foreach (var (key, get, set) in toggles)
        {
            var cb = new CheckBox { Text = key, ButtonPressed = get(), SizeFlagsHorizontal = SizeFlags.ExpandFill };
            var setter = set;
            cb.Toggled += v => setter(v);
            grid.AddChild(cb);
        }
        creativeBox.AddChild(grid);
        Body.AddChild(creativeBox);
        career.Toggled += on => { careerBox.Visible = on; creativeBox.Visible = !on; };

        var ai = new CheckButton { Text = "new_ai", ButtonPressed = true };
        Body.AddChild(ai);
        Body.AddChild(Ui.Text("new_ai_help", 13, Brand.InkDim));
        Body.AddChild(new HSeparator());
        Body.AddChild(Ui.Primary("new_start", () =>
        {
            o.Company = string.IsNullOrWhiteSpace(name.Text) ? Loc.T("new_company_default") : name.Text.Trim();
            o.Difficulty = diff.Selected; o.AiCompetitors = ai.ButtonPressed;
            o.Creative = creative.ButtonPressed;
            o.CreativeOptions = o.Creative ? c : new CreativeOptions();
            if (o.Creative && c.UnlimitedMoney) o.Difficulty = 0;
            Close(); start(o);
        }));
    }
}

/// <summary>Save and load local savegames.</summary>
public partial class SaveLoadDialog : Modal
{
    readonly VBoxContainer _list;
    readonly Func<GameState> _current;
    readonly Action<string> _load;

    public SaveLoadDialog(bool allowSave, Func<GameState> current, Action<string> load) : base(allowSave ? "save_title" : "load_title", 560)
    {
        _current = current; _load = load;
        if (allowSave)
        {
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 8);
            var name = new LineEdit { PlaceholderText = Loc.T("save_name"), SizeFlagsHorizontal = SizeFlags.ExpandFill, Text = current()?.Options.Company ?? "" };
            row.AddChild(name);
            row.AddChild(Ui.Primary("save_button", () =>
            {
                SaveSystem.Save(_current(), name.Text);
                Fill();
            }));
            Body.AddChild(row);
        }
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 300), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _list.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(_list);
        Body.AddChild(scroll);
        Fill();
    }

    void Fill()
    {
        foreach (var c in _list.GetChildren()) c.QueueFree();
        var saves = SaveSystem.List();
        if (saves.Count == 0) { _list.AddChild(Ui.Text("save_none", 14, Brand.InkDim)); return; }
        var culture = new System.Globalization.CultureInfo(Loc.Language == "nl" ? "nl-NL" : "en-GB");
        foreach (var s in saves)
        {
            var p = new PanelContainer();
            p.AddThemeStyleboxOverride("panel", Ui.Box(new Color(1, 1, 1, 0.03f), 8, Brand.Line, 1, 10));
            var h = new HBoxContainer(); h.AddThemeConstantOverride("separation", 8);
            var info = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            var title = Ui.Text(s.IsAutosave ? $"{Loc.T("save_autosave")} · {s.Company}" : s.Slot, 15, null, Ui.BodySemi);
            title.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
            info.AddChild(title);
            var sub = Ui.Text($"{s.Company} · {s.GameDate.ToString("d MMM yyyy", culture)} · {Loc.T("save_saved")} {s.SavedAt.ToString("g", culture)}", 12, Brand.InkDim);
            sub.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
            info.AddChild(sub);
            h.AddChild(info);
            var slot = s.Slot;
            h.AddChild(Ui.Secondary("save_load", () => { Close(); _load(slot); }));
            h.AddChild(Ui.Secondary("save_delete", () => { SaveSystem.Delete(slot); Fill(); }));
            p.AddChild(h);
            _list.AddChild(p);
        }
    }
}

/// <summary>Pause menu (Esc).</summary>
public partial class PauseMenu : Modal
{
    public PauseMenu(Action save, Action load, Action options, Action info, Action toMenu, Action quit) : base("pause_title", 380)
    {
        Body.AddChild(Ui.Primary("pause_resume", Close));
        Body.AddChild(Ui.Secondary("pause_save", () => { Close(); save(); }));
        Body.AddChild(Ui.Secondary("menu_load", () => { Close(); load(); }));
        Body.AddChild(Ui.Secondary("menu_options", () => { Close(); options(); }));
        Body.AddChild(Ui.Secondary("menu_info", () => { Close(); info(); }));
        Body.AddChild(Ui.Secondary("pause_main_menu", () => { Close(); toMenu(); }));
        Body.AddChild(Ui.Secondary("menu_quit", () => { Close(); quit(); }));
    }
}
