using System;
using System.Collections.Generic;
using System.Linq;
using DatacenterArchitect.Core;
using Godot;

namespace DatacenterArchitect.UI;

/// <summary>
/// Options: display (window mode, resolution, monitor, interface size, V-Sync, frame cap),
/// graphics and visual effects, audio volumes and sound effects, and game settings.
/// Every change applies at once and is saved to user://settings.cfg. A display change that could
/// leave the screen unusable asks to be kept and reverts by itself after 15 seconds.
/// </summary>
public partial class OptionsDialog : Modal
{
    static readonly string[] Tabs = { "opt_tab_display", "opt_tab_graphics", "opt_tab_audio", "opt_tab_game" };
    static readonly string[] TabIcons = { "grid", "settings", "play", "menu" };
    static readonly int[] FpsCaps = { 0, 30, 60, 120, 144, 165, 240 };
    static readonly int[] AutosaveSteps = { 1, 5, 10, 15, 30 };
    const double ConfirmSeconds = 15;

    readonly Action _applyDisplay, _applyOther;
    readonly Action<string> _setLanguage;
    readonly List<Button> _tabBtns = new();
    readonly VBoxContainer _content;
    readonly PanelContainer _confirm;
    readonly Label _confirmText;
    int _tab;
    AppSettings _before;
    double _confirmLeft;

    static AppSettings S => AppSettings.Instance;

    /// <param name="applyDisplay">applies window mode, resolution, monitor, V-Sync and interface size</param>
    /// <param name="applyOther">applies graphics, effects and audio</param>
    /// <param name="setLanguage">switches the game language ("en" or "nl")</param>
    public OptionsDialog(Action applyDisplay, Action applyOther, Action<string> setLanguage, int tab = 0) : base("opt_title", 880)
    {
        _applyDisplay = applyDisplay; _applyOther = applyOther; _setLanguage = setLanguage;
        _tab = Math.Clamp(tab, 0, Tabs.Length - 1);

        var split = new HBoxContainer(); split.AddThemeConstantOverride("separation", 16);
        Body.AddChild(split);

        // tabs on the left
        var tabs = new VBoxContainer { CustomMinimumSize = new Vector2(190, 0) }; tabs.AddThemeConstantOverride("separation", 4);
        for (var i = 0; i < Tabs.Length; i++)
        {
            var idx = i;
            var b = new Button
            {
                Text = Tabs[i], Alignment = HorizontalAlignment.Left, CustomMinimumSize = new Vector2(0, 42),
                Icon = GD.Load<Texture2D>($"res://assets/icons/{TabIcons[i]}.svg"), ExpandIcon = false,
            };
            b.AddThemeConstantOverride("icon_max_width", 18);
            b.AddThemeFontOverride("font", Ui.DisplaySemi); b.AddThemeFontSizeOverride("font_size", 19);
            b.Pressed += () => { _tab = idx; Rebuild(); };
            _tabBtns.Add(b);
            tabs.AddChild(b);
        }
        split.AddChild(tabs);
        split.AddChild(new VSeparator());

        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 480), SizeFlagsHorizontal = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _content.AddThemeConstantOverride("separation", 10);
        var pad = Ui.Pad(_content, 0, 0, 14, 0);
        pad.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(pad);
        split.AddChild(scroll);

        // keep-or-revert bar for display changes
        _confirm = new PanelContainer { Visible = false };
        _confirm.AddThemeStyleboxOverride("panel", Ui.Box(new Color(Brand.Amber, 0.12f), 8, Brand.Amber, 1, 10));
        var ch = new HBoxContainer(); ch.AddThemeConstantOverride("separation", 8);
        _confirmText = Ui.Text("", 15, Brand.Ink, Ui.BodySemi);
        _confirmText.SizeFlagsHorizontal = SizeFlags.ExpandFill; _confirmText.VerticalAlignment = VerticalAlignment.Center;
        _confirmText.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        ch.AddChild(_confirmText);
        ch.AddChild(Ui.Secondary("opt_revert", Revert));
        ch.AddChild(Ui.Primary("opt_keep", Keep));
        _confirm.AddChild(ch);
        Body.AddChild(_confirm);

        Body.AddChild(new HSeparator());
        var bottom = new HBoxContainer(); bottom.AddThemeConstantOverride("separation", 8);
        bottom.AddChild(Ui.Secondary("opt_reset", ResetAll));
        bottom.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        var done = Ui.Primary("opt_done", () => { Keep(); Close(); });
        done.CustomMinimumSize = new Vector2(160, 40);
        bottom.AddChild(done);
        Body.AddChild(bottom);

        TreeExiting += () => { if (_before != null) Revert(); };
        Rebuild();
    }

    // ---------------- layout ----------------
    void Rebuild()
    {
        for (var i = 0; i < _tabBtns.Count; i++) Mark(_tabBtns[i], i == _tab);
        foreach (var c in _content.GetChildren()) c.QueueFree();
        switch (_tab)
        {
            case 0: DisplayTab(); break;
            case 1: GraphicsTab(); break;
            case 2: AudioTab(); break;
            default: GameTab(); break;
        }
    }

    static void Mark(Button b, bool on)
    {
        if (on)
        {
            var s = Ui.Box(Brand.PanelHover, 7, null, 0, 8);
            s.BorderColor = Brand.Amber; s.BorderWidthLeft = 3;
            b.AddThemeStyleboxOverride("normal", s); b.AddThemeStyleboxOverride("hover", s);
            b.AddThemeColorOverride("font_color", Brand.Ink);
            b.AddThemeColorOverride("icon_normal_color", Brand.Amber);
        }
        else
        {
            b.RemoveThemeStyleboxOverride("normal"); b.RemoveThemeStyleboxOverride("hover");
            b.RemoveThemeColorOverride("font_color"); b.RemoveThemeColorOverride("icon_normal_color");
        }
    }

    void Section(string key)
    {
        if (_content.GetChildCount() > 0) _content.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4) });
        _content.AddChild(Ui.Heading(key, 14, Brand.Amber));
    }

    /// <summary>A setting row: name and optional help text on the left, the control on the right.</summary>
    void Row(string key, Control control, string helpKey = null)
    {
        var h = new HBoxContainer(); h.AddThemeConstantOverride("separation", 16);
        var left = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(240, 0) }; left.AddThemeConstantOverride("separation", 0);
        left.AddChild(Ui.Text(key, 16, Brand.Ink, Ui.BodyMedium));
        if (helpKey != null) left.AddChild(Ui.Text(helpKey, 13, Brand.InkDim));
        h.AddChild(left);
        control.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        if (control is CheckButton)
        {
            // a switch sits at the right edge of the same 280 px column as the other controls
            var box = new HBoxContainer { CustomMinimumSize = new Vector2(280, 36), Alignment = BoxContainer.AlignmentMode.End };
            box.AddChild(control);
            control = box;
        }
        else control.CustomMinimumSize = new Vector2(Math.Max(control.CustomMinimumSize.X, 280), Math.Max(control.CustomMinimumSize.Y, 36));
        h.AddChild(control);
        _content.AddChild(h);
    }

    static OptionButton Choice(IEnumerable<string> items, int selected, Action<int> changed)
    {
        var o = new OptionButton { AutoTranslateMode = AutoTranslateModeEnum.Disabled, FitToLongestItem = false };
        foreach (var t in items) o.AddItem(t);
        o.Selected = Math.Clamp(selected, 0, Math.Max(0, o.ItemCount - 1));
        o.ItemSelected += i => changed((int)i);
        return o;
    }

    static CheckButton Toggle(bool value, Action<bool> changed)
    {
        var c = new CheckButton { ButtonPressed = value, Alignment = HorizontalAlignment.Right };
        c.Toggled += on => changed(on);
        return c;
    }

    /// <summary>Slider with a value label; <paramref name="live"/> fires while dragging, <paramref name="commit"/> on release.</summary>
    static HBoxContainer Slider(double min, double max, double step, double value, Func<double, string> format, Action<double> live, Action<double> commit = null)
    {
        var h = new HBoxContainer(); h.AddThemeConstantOverride("separation", 10);
        var s = new HSlider { MinValue = min, MaxValue = max, Step = step, Value = value, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        var l = Ui.Number(format(value), 17); l.CustomMinimumSize = new Vector2(52, 0); l.HorizontalAlignment = HorizontalAlignment.Right;
        l.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        var dragging = false;
        s.DragStarted += () => dragging = true;
        s.ValueChanged += v => { l.Text = format(v); live?.Invoke(v); if (!dragging) commit?.Invoke(v); };
        s.DragEnded += _ => { dragging = false; commit?.Invoke(s.Value); };
        h.AddChild(s); h.AddChild(l);
        return h;
    }

    static string Pct(double v) => $"{v:0}%";

    // ---------------- tabs ----------------
    void DisplayTab()
    {
        Section("opt_section_screen");
        Row("opt_window_mode", Choice(new[] { Loc.T("opt_mode_windowed"), Loc.T("opt_mode_borderless"), Loc.T("opt_mode_fullscreen") }, (int)S.WindowMode,
            i => ChangeDisplay(() => S.WindowMode = (WindowModeSetting)i)), "opt_window_mode_help");

        var screens = DisplaySettings.Headless ? 1 : DisplayServer.GetScreenCount();
        var screen = DisplaySettings.Headless ? 0 : DisplaySettings.CurrentScreen(S);
        if (screens > 1)
        {
            var names = Enumerable.Range(0, screens).Select(i => { var z = DisplayServer.ScreenGetSize(i); return Loc.T("opt_monitor_n", i + 1, z.X, z.Y); });
            Row("opt_monitor", Choice(names, screen, i => ChangeDisplay(() => { S.Monitor = i; S.Resolution = Vector2I.Zero; })));
        }

        var native = DisplaySettings.Headless ? new Vector2I(1920, 1080) : DisplaySettings.ScreenSize(screen);
        var list = DisplaySettings.ResolutionsFor(native);
        var items = new List<string> { Loc.T("opt_res_native", native.X, native.Y, DisplaySettings.AspectName(native)) };
        items.AddRange(list.Select(r => $"{r.X} × {r.Y}   ({DisplaySettings.AspectName(r)})"));
        var sel = S.Resolution == Vector2I.Zero ? 0 : list.IndexOf(S.Resolution) + 1;
        Row("opt_resolution", Choice(items, Math.Max(0, sel), i => ChangeDisplay(() => S.Resolution = i == 0 ? Vector2I.Zero : list[i - 1])),
            S.WindowMode == WindowModeSetting.Windowed ? "opt_resolution_help_window" : "opt_resolution_help_full");

        Row("opt_ui_scale", Slider(75, 150, 5, S.UiScale * 100, Pct, null, v => { S.UiScale = (float)(v / 100); Save(); _applyDisplay?.Invoke(); }), "opt_ui_scale_help");

        Section("opt_section_performance");
        Row("opt_vsync", Toggle(S.VSync, on => { S.VSync = on; Save(); _applyDisplay?.Invoke(); }), "opt_vsync_help");
        var caps = FpsCaps.Select(f => f == 0 ? Loc.T("opt_fps_unlimited") : Loc.T("opt_fps_n", f));
        Row("opt_max_fps", Choice(caps, Math.Max(0, Array.IndexOf(FpsCaps, S.MaxFps)), i => { S.MaxFps = FpsCaps[i]; Save(); _applyDisplay?.Invoke(); }));
        _content.AddChild(Ui.Text("opt_fullscreen_hotkey", 13, Brand.InkDim));
    }

    void GraphicsTab()
    {
        Section("opt_section_quality");
        var presets = new HBoxContainer(); presets.AddThemeConstantOverride("separation", 8);
        foreach (var (key, level) in new[] { ("opt_preset_low", 0), ("opt_preset_medium", 1), ("opt_preset_high", 2) })
        {
            var lv = level;
            var b = Ui.Secondary(key, () => { ApplyPreset(S, lv); SaveOther(); Rebuild(); });
            b.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            presets.AddChild(b);
        }
        _content.AddChild(presets);
        _content.AddChild(Ui.Text("opt_preset_help", 13, Brand.InkDim));

        Section("opt_section_graphics");
        Row("opt_shadows", Toggle(S.Shadows, on => { S.Shadows = on; SaveOther(); }), "opt_shadows_help");
        Row("opt_ao", Toggle(S.AmbientOcclusion, on => { S.AmbientOcclusion = on; SaveOther(); }), "opt_ao_help");
        Row("opt_fog", Toggle(S.Fog, on => { S.Fog = on; SaveOther(); }), "opt_fog_help");
        int[] aa = { 0, 2, 4, 8 };
        Row("opt_msaa", Choice(new[] { Loc.T("opt_off"), "2×", "4×", "8×" }, Math.Max(0, Array.IndexOf(aa, S.Msaa)), i => { S.Msaa = aa[i]; SaveOther(); }), "opt_msaa_help");

        Section("opt_section_effects");
        Row("opt_blinking", Toggle(S.BlinkingLights, on => { S.BlinkingLights = on; SaveOther(); }), "opt_blinking_help");
        Row("opt_moving", Toggle(S.MovingParts, on => { S.MovingParts = on; SaveOther(); }), "opt_moving_help");
        Row("opt_smooth_camera", Toggle(S.SmoothCamera, on => { S.SmoothCamera = on; SaveOther(); }), "opt_smooth_camera_help");

        Section("opt_section_world");
        Row("opt_day_night", Toggle(S.DayNight, on => { S.DayNight = on; SaveOther(); }), "opt_day_night_help");
        Row("opt_weather", Toggle(S.Weather, on => { S.Weather = on; SaveOther(); }), "opt_weather_help");
        Row("opt_seasons", Toggle(S.Seasons, on => { S.Seasons = on; SaveOther(); }), "opt_seasons_help");
    }

    /// <summary>0 = performance, 1 = balanced, 2 = quality.</summary>
    public static void ApplyPreset(AppSettings s, int level)
    {
        s.Shadows = level >= 1;
        s.AmbientOcclusion = level >= 2;
        s.Fog = level >= 1;
        s.Msaa = level switch { 0 => 0, 1 => 2, _ => 4 };
    }

    void AudioTab()
    {
        Section("opt_section_volume");
        Row("opt_vol_master", Slider(0, 100, 1, S.MasterVolume, Pct, v => { S.MasterVolume = (int)v; AudioMixer.Apply(S); }, _ => Save()));
        Row("opt_vol_ambience", Slider(0, 100, 1, S.AmbienceVolume, Pct, v => { S.AmbienceVolume = (int)v; AudioMixer.Apply(S); }, _ => Save()), "opt_vol_ambience_help");
        Row("opt_vol_interface", Slider(0, 100, 1, S.InterfaceVolume, Pct, v => { S.InterfaceVolume = (int)v; AudioMixer.Apply(S); }, _ => Save()), "opt_vol_interface_help");

        Section("opt_section_sounds");
        Row("opt_snd_ambience", Toggle(S.AmbienceSound, on => { S.AmbienceSound = on; SaveOther(); }), "opt_snd_ambience_help");
        Row("opt_snd_clicks", Toggle(S.ClickSounds, on => { S.ClickSounds = on; SaveOther(); }));
        Row("opt_snd_notifications", Toggle(S.NotificationSounds, on => { S.NotificationSounds = on; SaveOther(); World.UiSounds.Instance?.Chime(); }));
        Row("opt_snd_background", Toggle(S.MuteInBackground, on => { S.MuteInBackground = on; SaveOther(); }), "opt_snd_background_help");
    }

    void GameTab()
    {
        Section("opt_section_language");
        var langs = new HBoxContainer(); langs.AddThemeConstantOverride("separation", 8);
        foreach (var (code, name) in new[] { ("en", "English"), ("nl", "Nederlands") })
        {
            var c = code;
            var b = Ui.Secondary(name, () => { if (Loc.Language != c) { _setLanguage?.Invoke(c); Rebuild(); } });
            b.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
            b.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            if (Loc.Language == code)
            {
                b.AddThemeStyleboxOverride("normal", Ui.Box(new Color(Brand.Amber, 0.16f), 8, Brand.Amber, 1, 10));
                b.AddThemeColorOverride("font_color", Brand.Amber);
            }
            langs.AddChild(b);
        }
        _content.AddChild(langs);

        Section("opt_section_saves");
        var steps = AutosaveSteps.Select(m => Loc.T("opt_minutes_n", m));
        var idx = Array.IndexOf(AutosaveSteps, S.AutosaveMinutes);
        Row("opt_autosave", Choice(steps, idx < 0 ? 1 : idx, i => { S.AutosaveMinutes = AutosaveSteps[i]; Save(); }), "opt_autosave_help");

        Section("opt_section_walk");
        int[] sens = { 50, 75, 100, 150, 200 };
        var si = Array.IndexOf(sens, S.MouseSensitivity);
        Row("opt_mouse_sensitivity", Choice(sens.Select(v => v + "%").ToArray(), si < 0 ? 2 : si, i => { S.MouseSensitivity = sens[i]; SaveOther(); }), "opt_mouse_sensitivity_help");
        Row("opt_invert_mouse", Toggle(S.InvertMouse, on => { S.InvertMouse = on; SaveOther(); }), "opt_invert_mouse_help");
    }

    // ---------------- applying ----------------
    static void Save() => S.Save();

    void SaveOther()
    {
        S.Save();
        _applyOther?.Invoke();
    }

    /// <summary>Change a display setting, apply it, and ask to keep it.</summary>
    void ChangeDisplay(Action change)
    {
        _before ??= S.Clone();
        change();
        S.Save();
        _applyDisplay?.Invoke();
        _confirmLeft = ConfirmSeconds;
        _confirm.Visible = true;
        UpdateConfirm();
        // labels such as the resolution help depend on the mode; rebuild after the popup has closed
        Callable.From(Rebuild).CallDeferred();
    }

    void Keep()
    {
        _before = null;
        _confirm.Visible = false;
    }

    void Revert()
    {
        if (_before == null) return;
        var b = _before; _before = null;
        S.WindowMode = b.WindowMode; S.Resolution = b.Resolution; S.Monitor = b.Monitor;
        S.Save();
        _applyDisplay?.Invoke();
        if (IsInstanceValid(_confirm)) _confirm.Visible = false;
        if (IsInsideTree() && !IsQueuedForDeletion()) Rebuild();
    }

    void ResetAll()
    {
        var lang = S.Language;
        S.ResetOptions();
        S.Language = lang;
        S.Save();
        _applyDisplay?.Invoke();
        _applyOther?.Invoke();
        Keep();
        Rebuild();
    }

    void UpdateConfirm() => _confirmText.Text = Loc.T("opt_confirm", Math.Ceiling(_confirmLeft));

    public override void _Process(double delta)
    {
        if (_before == null) return;
        _confirmLeft -= delta;
        if (_confirmLeft <= 0) Revert();
        else UpdateConfirm();
    }
}
