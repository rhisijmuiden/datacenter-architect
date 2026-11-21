using System;
using DatacenterArchitect.Sim;
using System.Collections.Generic;
using DatacenterArchitect.Core;
using Godot;

namespace DatacenterArchitect.UI;

/// <summary>Theme and small widget helpers so every screen follows brand/BRAND.md.</summary>
public static class Ui
{
    public static FontFile Body, BodyMedium, BodySemi, Display, DisplaySemi, DisplayBold;

    static FontFile F(string name)
    {
        var f = GD.Load<FontFile>($"res://assets/fonts/{name}.ttf");
        return f;
    }

    public static StyleBoxFlat Box(Color bg, int radius = 10, Color? border = null, int borderW = 1, int pad = 10, bool shadow = false)
    {
        var s = new StyleBoxFlat { BgColor = bg };
        s.SetCornerRadiusAll(radius);
        if (border.HasValue) { s.BorderColor = border.Value; s.SetBorderWidthAll(borderW); }
        s.SetContentMarginAll(pad);
        if (shadow) { s.ShadowColor = new Color(0.02f, 0.05f, 0.08f, 0.35f); s.ShadowSize = 12; s.ShadowOffset = new Vector2(0, 4); }
        return s;
    }

    public static Theme BuildTheme()
    {
        Body = F("Barlow-Regular"); BodyMedium = F("Barlow-Medium"); BodySemi = F("Barlow-SemiBold");
        Display = F("BarlowCondensed-Medium"); DisplaySemi = F("BarlowCondensed-SemiBold"); DisplayBold = F("BarlowCondensed-Bold");
        var t = new Theme { DefaultFont = Body, DefaultFontSize = 16 };
        t.SetStylebox("panel", "PanelContainer", Box(Brand.Panel, 10, Brand.Line, 1, 10, true));
        t.SetStylebox("panel", "Panel", Box(Brand.Panel, 10, Brand.Line, 1, 10, true));
        t.SetColor("font_color", "Label", Brand.Ink);

        // buttons: flat on the panel, hover = panel hover colour
        var normal = Box(new Color(0, 0, 0, 0), 7, null, 0, 8);
        var hover = Box(Brand.PanelHover, 7, null, 0, 8);
        var pressed = Box(Brand.PanelHover, 7, null, 0, 8);
        var disabled = Box(new Color(0, 0, 0, 0), 7, null, 0, 8);
        var focus = Box(new Color(0, 0, 0, 0), 7, Brand.Amber, 2, 8);
        foreach (var type in new[] { "Button", "OptionButton", "CheckBox", "CheckButton" })
        {
            t.SetStylebox("normal", type, normal); t.SetStylebox("hover", type, hover); t.SetStylebox("pressed", type, pressed);
            t.SetStylebox("disabled", type, disabled); t.SetStylebox("focus", type, focus); t.SetStylebox("hover_pressed", type, pressed);
            t.SetColor("font_color", type, Brand.InkDim); t.SetColor("font_hover_color", type, Brand.Ink);
            t.SetColor("font_pressed_color", type, Brand.Ink); t.SetColor("font_hover_pressed_color", type, Brand.Ink);
            t.SetColor("font_disabled_color", type, new Color(Brand.InkDim, 0.45f)); t.SetColor("font_focus_color", type, Brand.Ink);
            t.SetColor("icon_normal_color", type, Brand.InkDim); t.SetColor("icon_hover_color", type, Brand.Ink); t.SetColor("icon_pressed_color", type, Brand.Amber);
            t.SetFont("font", type, BodyMedium);
        }
        t.SetStylebox("normal", "LineEdit", Box(new Color(1, 1, 1, 0.06f), 7, Brand.Line, 1, 8));
        t.SetStylebox("focus", "LineEdit", Box(new Color(1, 1, 1, 0.06f), 7, Brand.Amber, 1, 8));
        t.SetColor("font_color", "LineEdit", Brand.Ink); t.SetColor("caret_color", "LineEdit", Brand.Amber);
        t.SetStylebox("background", "ProgressBar", Box(new Color(1, 1, 1, 0.1f), 3, null, 0, 0));
        t.SetStylebox("fill", "ProgressBar", Box(Brand.Amber, 3, null, 0, 0));
        t.SetColor("font_color", "ProgressBar", Brand.Ink);
        t.SetStylebox("panel", "PopupMenu", Box(Brand.PanelSolid, 8, Brand.Line, 1, 6));
        t.SetStylebox("hover", "PopupMenu", Box(Brand.PanelHover, 6, null, 0, 6));
        t.SetColor("font_color", "PopupMenu", Brand.InkDim); t.SetColor("font_hover_color", "PopupMenu", Brand.Ink);
        t.SetStylebox("panel", "TooltipPanel", Box(Brand.PanelSolid, 6, Brand.Line, 1, 8));
        t.SetColor("font_color", "TooltipLabel", Brand.Ink);
        t.SetStylebox("separator", "HSeparator", new StyleBoxLine { Color = Brand.Line, Thickness = 1 });
        t.SetConstant("separation", "HSeparator", 10);
        t.SetStylebox("scroll", "VScrollBar", Box(new Color(0, 0, 0, 0), 3, null, 0, 0));
        t.SetStylebox("grabber", "VScrollBar", Box(new Color(1, 1, 1, 0.15f), 3, null, 0, 0));
        // horizontal bars (build tray) get a real height, so they can be seen and dragged
        var hTrack = Box(new Color(1, 1, 1, 0.06f), 4, null, 0, 0); hTrack.ContentMarginTop = hTrack.ContentMarginBottom = 4;
        t.SetStylebox("scroll", "HScrollBar", hTrack);
        t.SetStylebox("grabber", "HScrollBar", Box(new Color(1, 1, 1, 0.28f), 4, null, 0, 0));
        t.SetStylebox("grabber_highlight", "HScrollBar", Box(new Color(Brand.Amber, 0.7f), 4, null, 0, 0));
        t.SetStylebox("grabber_pressed", "HScrollBar", Box(Brand.Amber, 4, null, 0, 0));
        // sliders: thin track, amber fill, round amber knob
        var track = Box(new Color(1, 1, 1, 0.12f), 3, null, 0, 0); track.ContentMarginTop = track.ContentMarginBottom = 3;
        var fill = Box(Brand.Amber, 3, null, 0, 0); fill.ContentMarginTop = fill.ContentMarginBottom = 3;
        t.SetStylebox("slider", "HSlider", track);
        t.SetStylebox("grabber_area", "HSlider", fill);
        t.SetStylebox("grabber_area_highlight", "HSlider", fill);
        t.SetIcon("grabber", "HSlider", Knob(Brand.Amber));
        t.SetIcon("grabber_highlight", "HSlider", Knob(Brand.Amber.Lightened(0.2f)));
        t.SetStylebox("focus", "HSlider", Box(new Color(0, 0, 0, 0), 4, null, 0, 0));
        // drop-downs look like input fields
        t.SetStylebox("normal", "OptionButton", Box(new Color(1, 1, 1, 0.06f), 7, Brand.Line, 1, 9));
        t.SetStylebox("hover", "OptionButton", Box(Brand.PanelHover, 7, Brand.Line, 1, 9));
        t.SetStylebox("pressed", "OptionButton", Box(Brand.PanelHover, 7, Brand.Amber, 1, 9));
        t.SetStylebox("focus", "OptionButton", Box(new Color(0, 0, 0, 0), 7, Brand.Amber, 1, 9));
        t.SetColor("font_color", "OptionButton", Brand.Ink);
        // on/off switches in brand colours, no button background
        var clear = Box(new Color(0, 0, 0, 0), 7, null, 0, 4);
        foreach (var k in new[] { "normal", "pressed", "hover", "hover_pressed", "disabled" }) t.SetStylebox(k, "CheckButton", clear);
        t.SetIcon("checked", "CheckButton", Switch(true, false)); t.SetIcon("unchecked", "CheckButton", Switch(false, false));
        t.SetIcon("checked_disabled", "CheckButton", Switch(true, true)); t.SetIcon("unchecked_disabled", "CheckButton", Switch(false, true));
        t.SetStylebox("separator", "VSeparator", new StyleBoxLine { Color = Brand.Line, Thickness = 1, Vertical = true });
        return t;
    }

    /// <summary>Switch icon drawn in code: amber track with the knob right when on, grey with the knob left when off.</summary>
    static ImageTexture Switch(bool on, bool disabled)
    {
        const int w = 44, h = 24, r = h / 2;
        var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
        var track = on ? Brand.Amber : new Color(1, 1, 1, 0.16f);
        var knobC = on ? Brand.PanelSolid : Brand.Ink;
        var kx = on ? w - r : r;
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var px = new Vector2(x + 0.5f, y + 0.5f);
                var cx = Mathf.Clamp(px.X, r, w - r);
                var dTrack = px.DistanceTo(new Vector2(cx, r));
                var aTrack = Mathf.Clamp(r - dTrack, 0, 1);
                var dKnob = px.DistanceTo(new Vector2(kx, r));
                var aKnob = Mathf.Clamp(r - 3 - dKnob, 0, 1);
                var c = new Color(track, track.A * aTrack).Blend(new Color(knobC, aKnob));
                if (disabled) c.A *= 0.45f;
                img.SetPixel(x, y, c);
            }
        return ImageTexture.CreateFromImage(img);
    }

    /// <summary>Round slider knob drawn in code.</summary>
    static ImageTexture Knob(Color c)
    {
        const int n = 18;
        var img = Image.CreateEmpty(n, n, false, Image.Format.Rgba8);
        for (var y = 0; y < n; y++)
            for (var x = 0; x < n; x++)
            {
                var d = new Vector2(x + 0.5f - n / 2f, y + 0.5f - n / 2f).Length();
                var a = Mathf.Clamp(n / 2f - d, 0, 1);
                var inner = d < n / 2f - 3 ? c : Brand.PanelSolid.Lerp(c, 0.25f);
                img.SetPixel(x, y, new Color(inner, a));
            }
        return ImageTexture.CreateFromImage(img);
    }

    /// <summary>Uppercase condensed label used for headings (0.08em tracking approximated by font spacing).</summary>
    public static Label Heading(string key, int size = 13, Color? color = null)
    {
        var l = new Label { Text = key, Uppercase = true };
        l.AddThemeFontOverride("font", DisplaySemi);
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", color ?? Brand.InkDim);
        l.AddThemeConstantOverride("outline_size", 0);
        return l;
    }

    public static Label Text(string text, int size = 15, Color? color = null, FontFile font = null)
    {
        var l = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        l.AddThemeFontSizeOverride("font_size", size);
        if (color.HasValue) l.AddThemeColorOverride("font_color", color.Value);
        if (font != null) l.AddThemeFontOverride("font", font);
        return l;
    }

    public static Label Number(string text, int size = 22)
    {
        var l = new Label { Text = text };
        l.AddThemeFontOverride("font", DisplaySemi);
        l.AddThemeFontSizeOverride("font_size", size);
        return l;
    }

    public static Button Primary(string key, Action onPressed)
    {
        var b = new Button { Text = key, CustomMinimumSize = new Vector2(0, 40) };
        b.AddThemeStyleboxOverride("normal", Box(Brand.Amber, 8, null, 0, 10));
        b.AddThemeStyleboxOverride("hover", Box(Brand.Amber.Lightened(0.12f), 8, null, 0, 10));
        b.AddThemeStyleboxOverride("pressed", Box(Brand.Amber.Darkened(0.1f), 8, null, 0, 10));
        b.AddThemeStyleboxOverride("disabled", Box(new Color(Brand.Amber, 0.4f), 8, null, 0, 10));
        foreach (var k in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color", "font_disabled_color", "font_hover_pressed_color" })
            b.AddThemeColorOverride(k, Brand.PanelSolid);
        b.AddThemeFontOverride("font", DisplayBold);
        b.AddThemeFontSizeOverride("font_size", 18);
        b.Pressed += () => onPressed?.Invoke();
        return b;
    }

    public static Button Secondary(string key, Action onPressed)
    {
        var b = new Button { Text = key, CustomMinimumSize = new Vector2(0, 38) };
        b.AddThemeStyleboxOverride("normal", Box(new Color(1, 1, 1, 0.05f), 8, Brand.Line, 1, 10));
        b.AddThemeStyleboxOverride("hover", Box(Brand.PanelHover, 8, Brand.Line, 1, 10));
        b.AddThemeColorOverride("font_color", Brand.Ink);
        b.Pressed += () => onPressed?.Invoke();
        return b;
    }

    public static Button IconButton(string icon, string tooltipKey, Action onPressed)
    {
        var b = new Button { Icon = GD.Load<Texture2D>($"res://assets/icons/{icon}.svg"), TooltipText = tooltipKey, ExpandIcon = true, CustomMinimumSize = new Vector2(40, 38) };
        b.AddThemeConstantOverride("icon_max_width", 20);
        b.IconAlignment = HorizontalAlignment.Center;
        b.Pressed += () => onPressed?.Invoke();
        return b;
    }

    public static MarginContainer Pad(Control c, int l, int t, int r, int b)
    {
        var m = new MarginContainer();
        m.AddThemeConstantOverride("margin_left", l); m.AddThemeConstantOverride("margin_top", t);
        m.AddThemeConstantOverride("margin_right", r); m.AddThemeConstantOverride("margin_bottom", b);
        m.AddChild(c);
        return m;
    }

    /// <summary>Player-facing name of a floor: basement, ground or floor N.</summary>
    public static string FloorName(int f) => f == GameState.BasementFloor ? Loc.T("hud_floor_basement") : f == 0 ? Loc.T("hud_floor_ground") : Loc.T("hud_floor_n", f);
    public static string Money(long v) => (v < 0 ? "-€ " : "€ ") + Math.Abs(v).ToString("N0", Loc.Language == "nl" ? new System.Globalization.CultureInfo("nl-NL") : new System.Globalization.CultureInfo("en-GB"));
}

/// <summary>
/// A dimmed full-screen layer with a centred panel. Close with Esc or the close button.
/// Management screens can instead be docked as a drawer next to the rail on the left (<see cref="Dock"/>):
/// no dimming, full height, the world stays visible and clickable, and the section's tabs sit on the left.
/// </summary>
public partial class Modal : Control
{
    public VBoxContainer Body { get; private set; }
    public Action OnClosed;
    readonly string _titleKey;
    readonly float _width;
    readonly ColorRect _dim;
    readonly CenterContainer _center;
    readonly PanelContainer _panel;
    readonly VBoxContainer _frame;
    readonly Label _title;
    readonly List<Control> _ownTabs = new();
    readonly List<Button> _sectionBtns = new();

    /// <summary>The section this drawer shows, or -1 when it is a normal centred dialog.</summary>
    public int DrawerSection { get; private set; } = -1;
    /// <summary>The tab of <see cref="DrawerSection"/> that is open.</summary>
    public int SectionTab { get; private set; }
    /// <summary>Which screen this is; set by the drawer, used to switch tabs without reopening.</summary>
    public PanelKind? Kind { get; set; }

    public Modal(string titleKey, float width = 520)
    {
        _titleKey = titleKey; _width = width;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        _dim = new ColorRect { Color = new Color(0.03f, 0.06f, 0.1f, 0.55f) };
        _dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_dim);
        _center = new CenterContainer();
        _center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_center);
        _panel = new PanelContainer { CustomMinimumSize = new Vector2(_width, 0) };
        _panel.AddThemeStyleboxOverride("panel", Ui.Box(Brand.PanelSolid, 12, Brand.Line, 1, 22, true));
        _center.AddChild(_panel);
        var v = _frame = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 12);
        _panel.AddChild(v);
        var head = new HBoxContainer();
        _title = new Label { Text = _titleKey, Uppercase = true, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _title.AddThemeFontOverride("font", Ui.DisplayBold);
        _title.AddThemeFontSizeOverride("font_size", 24);
        head.AddChild(_title);
        var close = new Button { Text = "×", TooltipText = "ui_close" };
        close.Pressed += Close;
        head.AddChild(close);
        v.AddChild(head);
        Body = new VBoxContainer();
        Body.AddThemeConstantOverride("separation", 10);
        v.AddChild(Body);
    }

    /// <summary>Marks the screen's own tab column (and its divider); a docked drawer hides it and shows the section's tabs instead.</summary>
    protected void OwnTabs(params Control[] controls) => _ownTabs.AddRange(controls);

    /// <summary>Show one of this screen's own tabs. Screens with tabs override this.</summary>
    public virtual void SelectTab(int tab) { }

    /// <summary>Lets a scroll area fill the drawer's height when docked.</summary>
    protected static void FillHeight(params Control[] controls)
    {
        foreach (var c in controls) c.SizeFlagsVertical |= SizeFlags.ExpandFill;
    }

    /// <summary>
    /// Turns this dialog into the management drawer: docked at <paramref name="left"/> px from the left edge, full height,
    /// without dimming the world. The section's tabs replace the screen's own tabs; <paramref name="openTab"/> is called
    /// for a tab that belongs to another screen.
    /// </summary>
    public void Dock(float left, int section, int tab, Action<int, int> openTab)
    {
        DrawerSection = section; SectionTab = tab;
        var sec = Sections.All[section];
        _dim.Visible = false;
        MouseFilter = MouseFilterEnum.Ignore;
        _center.RemoveChild(_panel);
        _center.QueueFree();
        AddChild(_panel);
        _panel.AnchorTop = 0; _panel.AnchorBottom = 1; _panel.AnchorLeft = _panel.AnchorRight = 0;
        _panel.OffsetLeft = left; _panel.OffsetRight = left + _width; _panel.OffsetTop = 12; _panel.OffsetBottom = -12;
        _panel.AddThemeStyleboxOverride("panel", Ui.Box(Brand.PanelSolid, 12, Brand.Line, 1, 18, true));
        _title.Text = sec.Key;
        _title.AddThemeColorOverride("font_color", sec.Accent);
        foreach (var c in _ownTabs) c.Visible = false;

        _frame.RemoveChild(Body);
        var split = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        split.AddThemeConstantOverride("separation", 16);
        var col = new VBoxContainer { CustomMinimumSize = new Vector2(196, 0) };
        col.AddThemeConstantOverride("separation", 4);
        for (var i = 0; i < sec.Tabs.Length; i++)
        {
            var e = sec.Tabs[i];
            var idx = i;
            var b = new Button
            {
                Text = e.Key, Alignment = HorizontalAlignment.Left, CustomMinimumSize = new Vector2(0, 42),
                Icon = GD.Load<Texture2D>($"res://assets/icons/{e.Icon}.svg"), ExpandIcon = false,
            };
            b.AddThemeConstantOverride("icon_max_width", 18);
            b.AddThemeFontOverride("font", Ui.DisplaySemi); b.AddThemeFontSizeOverride("font_size", 19);
            b.Pressed += () =>
            {
                if (e.Kind == Kind) { SectionTab = idx; MarkSectionTabs(); SelectTab(e.Tab); }
                else openTab?.Invoke(section, idx);
            };
            _sectionBtns.Add(b);
            col.AddChild(b);
        }
        col.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
        var keys = Ui.Text(Loc.T("drawer_keys"), 12, Brand.InkDim);
        keys.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        col.AddChild(keys);
        split.AddChild(col);
        split.AddChild(new VSeparator());
        Body.SizeFlagsHorizontal = SizeFlags.ExpandFill; Body.SizeFlagsVertical = SizeFlags.ExpandFill;
        split.AddChild(Body);
        _frame.AddChild(split);
        // the drawer is as tall as the window: lists take the height there is, so their last rows never fall off the
        // bottom of the screen (a big UI scale or a small window) where the wheel cannot reach them
        LowerListHeights(Body);
        MarkSectionTabs();
    }

    static void LowerListHeights(Node n)
    {
        foreach (var c in n.GetChildren())
        {
            if (c is ScrollContainer sc && sc.CustomMinimumSize.Y > 200)
            {
                sc.CustomMinimumSize = new Vector2(sc.CustomMinimumSize.X, 200);
                sc.SizeFlagsVertical |= SizeFlags.ExpandFill;
            }
            LowerListHeights(c);
        }
    }

    void MarkSectionTabs()
    {
        var accent = DrawerSection >= 0 ? Sections.All[DrawerSection].Accent : Brand.Amber;
        for (var i = 0; i < _sectionBtns.Count; i++)
        {
            var b = _sectionBtns[i];
            if (i == SectionTab)
            {
                var st = Ui.Box(Brand.PanelHover, 7, null, 0, 8);
                st.BorderColor = accent; st.BorderWidthLeft = 3;
                b.AddThemeStyleboxOverride("normal", st);
                b.AddThemeColorOverride("font_color", Brand.Ink);
            }
            else { b.RemoveThemeStyleboxOverride("normal"); b.RemoveThemeColorOverride("font_color"); }
        }
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e.IsActionPressed("ui_cancel")) { Close(); GetViewport().SetInputAsHandled(); }
    }

    public void Close()
    {
        if (IsQueuedForDeletion()) return;
        OnClosed?.Invoke();
        QueueFree();
    }
}

/// <summary>
/// A vertical scroll list that stays where the player put it. Screens rebuild their rows every half second or so when the
/// game changes; fresh rows only get their real height a frame or two later, so a plain ScrollContainer gets clamped back
/// up and the list seems to ignore the mouse wheel. This one remembers the position the player chose (wheel, scroll bar,
/// keyboard) and puts it back once the rows are tall enough again.
/// </summary>
public partial class StableScroll : ScrollContainer
{
    int _want;
    ulong _userFrame;

    /// <summary>Sets the position as if the player chose it (0 = back to the top, on a tab switch).</summary>
    public new int ScrollVertical
    {
        get => base.ScrollVertical;
        set { base.ScrollVertical = value; _want = value; }
    }

    public override void _Ready()
    {
        HorizontalScrollMode = ScrollMode.Disabled;
        var bar = GetVScrollBar();
        bar.Scrolling += Touched;
        bar.GuiInput += _ => Touched();
    }

    void Touched() => _userFrame = Engine.GetProcessFrames();

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton or InputEventPanGesture or InputEventScreenDrag or InputEventKey) Touched();
    }

    public override void _Process(double delta)
    {
        var now = Engine.GetProcessFrames();
        var cur = base.ScrollVertical;
        if (now - _userFrame <= 4) { _want = cur; return; }
        // not the player: the rows were rebuilt and the list got clamped; go back to where they were once it fits again
        if (cur != _want) base.ScrollVertical = _want;
    }
}
