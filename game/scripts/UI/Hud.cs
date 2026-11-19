using System;
using System.Collections.Generic;
using DatacenterArchitect.Core;
using DatacenterArchitect.Sim;
using Godot;

namespace DatacenterArchitect.UI;

public enum Tool { Select, Erase, Build }

/// <summary>In-game HUD: build menu left, resource bars top, camera/info right, notifications and time bottom.</summary>
public partial class Hud : Control
{
    public Action<Tool, BuildKind> ToolChanged;
    public Action<int> SpeedChanged;
    public Action RotateLeft, RotateRight, ToggleGrid, OpenInfo, OpenMenu, ToggleLanguage;

    readonly Dictionary<string, (Label value, ProgressBar bar)> _bars = new();
    Label _money, _date, _company, _toolHint;
    Button _langBtn;
    VBoxContainer _notes;
    readonly List<Button> _speedBtns = new();
    readonly Dictionary<string, Button> _toolBtns = new();
    VBoxContainer _sub;
    string _category = "cat_buildings";

    // Category -> tools. Categories follow the brand guide; later steps fill the empty ones.
    static readonly (string cat, BuildKind[] kinds)[] Categories =
    {
        ("cat_land", Array.Empty<BuildKind>()),
        ("cat_buildings", new[] { BuildKind.Foundation }),
        ("cat_power", new[] { BuildKind.Generator }),
        ("cat_cooling", new[] { BuildKind.Crah }),
        ("cat_network", new[] { BuildKind.FiberPit }),
        ("cat_halls", new[] { BuildKind.Rack }),
        ("cat_security", Array.Empty<BuildKind>()),
        ("cat_staff", Array.Empty<BuildKind>()),
        ("cat_customers", Array.Empty<BuildKind>()),
    };

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        BuildTop(); BuildLeft(); BuildRight(); BuildBottom();
    }

    // ---------------- top ----------------
    void BuildTop()
    {
        var top = new HBoxContainer { Position = new Vector2(12, 12) };
        top.AddThemeConstantOverride("separation", 10);
        AddChild(top);
        var brand = new PanelContainer();
        var bh = new HBoxContainer(); bh.AddThemeConstantOverride("separation", 10);
        bh.AddChild(new TextureRect { Texture = GD.Load<Texture2D>("res://icon.svg"), CustomMinimumSize = new Vector2(34, 34), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered });
        var bv = new VBoxContainer(); bv.AddThemeConstantOverride("separation", 0);
        var title = new Label { Text = "Datacenter Architect", Uppercase = true };
        title.AddThemeFontOverride("font", Ui.DisplayBold); title.AddThemeFontSizeOverride("font_size", 19);
        bv.AddChild(title);
        _company = Ui.Text("", 12, Brand.InkDim); _company.AutowrapMode = TextServer.AutowrapMode.Off;
        bv.AddChild(_company);
        bh.AddChild(bv);
        brand.AddChild(bh);
        top.AddChild(brand);

        var stats = new PanelContainer();
        var sh = new HBoxContainer(); sh.AddThemeConstantOverride("separation", 18);
        stats.AddChild(sh);
        AddBar(sh, "res_power", Brand.Power);
        AddBar(sh, "res_backup", Brand.Power);
        AddBar(sh, "res_cooling", Brand.Cooling);
        AddBar(sh, "res_racks", Brand.Ok, false);
        AddBar(sh, "res_fiber", Brand.Fiber, false);
        var mv = new VBoxContainer(); mv.AddThemeConstantOverride("separation", 2);
        mv.AddChild(Ui.Heading("res_money", 12));
        _money = Ui.Number("€ 0", 20); _money.AddThemeColorOverride("font_color", Brand.Ok);
        mv.AddChild(_money);
        sh.AddChild(mv);
        top.AddChild(stats);
    }

    void AddBar(HBoxContainer parent, string key, Color c, bool bar = true)
    {
        var v = new VBoxContainer { CustomMinimumSize = new Vector2(bar ? 130 : 70, 0) };
        v.AddThemeConstantOverride("separation", 2);
        v.AddChild(Ui.Heading(key, 12));
        var value = Ui.Number("0", 18);
        value.AutowrapMode = TextServer.AutowrapMode.Off;
        v.AddChild(value);
        ProgressBar pb = null;
        if (bar)
        {
            pb = new ProgressBar { MinValue = 0, MaxValue = 1, Step = 0.001, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 5) };
            pb.AddThemeStyleboxOverride("fill", Ui.Box(c, 3, null, 0, 0));
            v.AddChild(pb);
        }
        parent.AddChild(v);
        _bars[key] = (value, pb);
    }

    static void SetBar((Label value, ProgressBar bar) b, string text, double ratio, Color normal)
    {
        b.value.Text = text;
        if (b.bar == null) return;
        b.bar.Value = Math.Min(1, ratio);
        var c = ratio > 1 ? Brand.Critical : ratio > 0.85 ? Brand.Warning : normal;
        b.bar.AddThemeStyleboxOverride("fill", Ui.Box(c, 3, null, 0, 0));
    }

    public void UpdateStats(GameState s)
    {
        _company.Text = s.Options.Company;
        SetBar(_bars["res_power"], $"{s.PowerDemandKw:0} / {s.GridCapacityKw:0} kW", s.PowerDemandKw / s.GridCapacityKw, Brand.Power);
        var backupRatio = s.BackupCapacityKw <= 0 ? (s.PowerDemandKw > 0 ? 2 : 0) : s.PowerDemandKw / s.BackupCapacityKw;
        SetBar(_bars["res_backup"], $"{s.BackupCapacityKw:0} kW N+1", backupRatio, Brand.Power);
        var coolRatio = s.CoolingCapacityKw <= 0 ? (s.CoolingDemandKw > 0 ? 2 : 0) : s.CoolingDemandKw / s.CoolingCapacityKw;
        SetBar(_bars["res_cooling"], $"{s.CoolingDemandKw:0} / {s.CoolingCapacityKw:0} kW", coolRatio, Brand.Cooling);
        SetBar(_bars["res_racks"], s.Racks.ToString(), 0, Brand.Ok);
        SetBar(_bars["res_fiber"], s.FiberPits.ToString(), 0, Brand.Fiber);
        _money.Text = Ui.Money(s.Money);
        _money.AddThemeColorOverride("font_color", s.Money < 0 ? Brand.Critical : Brand.Ok);
        var culture = new System.Globalization.CultureInfo(Loc.Language == "nl" ? "nl-NL" : "en-GB");
        _date.Text = s.Date.ToString("ddd d MMM yyyy · HH:00", culture);
    }

    // ---------------- left: build menu ----------------
    void BuildLeft()
    {
        var panel = new PanelContainer { Position = new Vector2(12, 96), CustomMinimumSize = new Vector2(230, 0) };
        AddChild(panel);
        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 4);
        panel.AddChild(v);
        v.AddChild(Ui.Heading("hud_build"));
        var tools = new HBoxContainer(); tools.AddThemeConstantOverride("separation", 4);
        tools.AddChild(ToolButton("tool_select", "select", () => Pick(Tool.Select, default)));
        tools.AddChild(ToolButton("tool_erase", "erase", () => Pick(Tool.Erase, default)));
        v.AddChild(tools);
        v.AddChild(new HSeparator());
        foreach (var (cat, kinds) in Categories)
        {
            var b = new Button { Text = cat, Alignment = HorizontalAlignment.Left, CustomMinimumSize = new Vector2(0, 34) };
            b.AddThemeFontOverride("font", Ui.DisplaySemi); b.AddThemeFontSizeOverride("font_size", 17);
            if (kinds.Length == 0) { b.Disabled = true; b.TooltipText = "hud_coming_soon"; }
            var c = cat;
            b.Pressed += () => { _category = c; RefreshCategory(); };
            _toolBtns[cat] = b;
            v.AddChild(b);
        }
        v.AddChild(new HSeparator());
        _sub = new VBoxContainer(); _sub.AddThemeConstantOverride("separation", 2);
        v.AddChild(_sub);
        _toolHint = Ui.Text("", 13, Brand.InkDim);
        _toolHint.CustomMinimumSize = new Vector2(200, 0);
        v.AddChild(_toolHint);
        RefreshCategory();
    }

    Button ToolButton(string key, string icon, Action a)
    {
        var b = new Button { Text = key, Icon = GD.Load<Texture2D>($"res://assets/icons/{icon}.svg"), ExpandIcon = false, SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = HorizontalAlignment.Left };
        b.AddThemeConstantOverride("icon_max_width", 18);
        b.Pressed += a;
        _toolBtns[key] = b;
        return b;
    }

    Tool _tool = Tool.Select; BuildKind _kind;

    void RefreshCategory()
    {
        foreach (var c in _sub.GetChildren()) c.QueueFree();
        foreach (var (cat, kinds) in Categories)
        {
            Active(_toolBtns[cat], cat == _category);
            if (cat != _category) continue;
            foreach (var k in kinds)
            {
                var def = Catalog.Defs[k];
                var b = new Button { Alignment = HorizontalAlignment.Left, CustomMinimumSize = new Vector2(0, 32) };
                b.Text = $"{Loc.T(def.NameKey)}   {Ui.Money(def.Cost)}";
                b.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
                var kind = k;
                b.Pressed += () => Pick(Tool.Build, kind);
                Active(b, _tool == Tool.Build && _kind == k);
                _sub.AddChild(b);
            }
        }
        Active(_toolBtns["tool_select"], _tool == Tool.Select);
        Active(_toolBtns["tool_erase"], _tool == Tool.Erase);
        _toolHint.Text = Loc.T(_tool switch { Tool.Erase => "hint_erase", Tool.Build when _kind == BuildKind.Foundation => "hint_foundation", Tool.Build => "hint_place", _ => "hint_select" });
    }

    static void Active(Button b, bool on)
    {
        if (on)
        {
            var s = Ui.Box(Brand.PanelHover, 7, null, 0, 8);
            s.BorderColor = Brand.Amber; s.BorderWidthLeft = 3;
            b.AddThemeStyleboxOverride("normal", s);
            b.AddThemeColorOverride("font_color", Brand.Ink);
        }
        else { b.RemoveThemeStyleboxOverride("normal"); b.RemoveThemeColorOverride("font_color"); }
    }

    public void Pick(Tool tool, BuildKind kind)
    {
        _tool = tool; _kind = kind;
        RefreshCategory();
        ToolChanged?.Invoke(tool, kind);
    }

    // ---------------- right ----------------
    void BuildRight()
    {
        var panel = new PanelContainer();
        panel.AnchorLeft = panel.AnchorRight = 1;
        panel.OffsetLeft = panel.OffsetRight = -12; panel.OffsetTop = 12;
        panel.GrowHorizontal = GrowDirection.Begin;
        AddChild(panel);
        var h = new HBoxContainer(); h.AddThemeConstantOverride("separation", 2);
        panel.AddChild(h);
        h.AddChild(Ui.IconButton("rotate_left", "hud_rotate_left", () => RotateLeft?.Invoke()));
        h.AddChild(Ui.IconButton("rotate_right", "hud_rotate_right", () => RotateRight?.Invoke()));
        h.AddChild(Ui.IconButton("grid", "hud_grid", () => ToggleGrid?.Invoke()));
        _langBtn = new Button { CustomMinimumSize = new Vector2(44, 38), TooltipText = "hud_language", AutoTranslateMode = AutoTranslateModeEnum.Disabled };
        _langBtn.AddThemeFontOverride("font", Ui.DisplayBold);
        _langBtn.Pressed += () => ToggleLanguage?.Invoke();
        h.AddChild(_langBtn);
        h.AddChild(Ui.IconButton("info", "hud_info", () => OpenInfo?.Invoke()));
        h.AddChild(Ui.IconButton("menu", "hud_menu", () => OpenMenu?.Invoke()));
        RefreshLanguage();
    }

    public void RefreshLanguage()
    {
        _langBtn.Text = Loc.Language == "en" ? "NL" : "EN";
        if (_sub != null) RefreshCategory();
    }

    // ---------------- bottom ----------------
    void BuildBottom()
    {
        _notes = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        _notes.AnchorTop = _notes.AnchorBottom = 1;
        _notes.OffsetTop = _notes.OffsetBottom = -12; _notes.OffsetLeft = 258;
        _notes.GrowVertical = GrowDirection.Begin;
        _notes.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_notes);

        var time = new PanelContainer();
        time.AnchorLeft = time.AnchorRight = time.AnchorTop = time.AnchorBottom = 1;
        time.OffsetLeft = time.OffsetRight = -12; time.OffsetTop = time.OffsetBottom = -12;
        time.GrowHorizontal = GrowDirection.Begin; time.GrowVertical = GrowDirection.Begin;
        AddChild(time);
        var h = new HBoxContainer(); h.AddThemeConstantOverride("separation", 4);
        time.AddChild(h);
        _date = Ui.Number("", 17); _date.CustomMinimumSize = new Vector2(190, 0); _date.VerticalAlignment = VerticalAlignment.Center;
        h.AddChild(_date);
        string[] icons = { "pause", "play", "fast", "faster" };
        for (var i = 0; i < 4; i++)
        {
            var speed = i;
            var b = Ui.IconButton(icons[i], $"hud_speed_{i}", () => { SetSpeed(speed); SpeedChanged?.Invoke(speed); });
            _speedBtns.Add(b); h.AddChild(b);
        }
        SetSpeed(1);
    }

    public void SetSpeed(int s)
    {
        for (var i = 0; i < _speedBtns.Count; i++) Active(_speedBtns[i], i == s);
    }

    public void Notify(string text, Color? accent = null)
    {
        var p = new PanelContainer();
        var s = Ui.Box(Brand.Panel, 8, null, 0, 10);
        s.BorderColor = accent ?? Brand.Amber; s.BorderWidthLeft = 3;
        p.AddThemeStyleboxOverride("panel", s);
        var l = Ui.Text(text, 14); l.CustomMinimumSize = new Vector2(320, 0);
        l.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        p.AddChild(l);
        p.MouseFilter = MouseFilterEnum.Ignore;
        _notes.AddChild(p);
        while (_notes.GetChildCount() > 4) _notes.GetChild(0).QueueFree();
        var tw = CreateTween();
        tw.TweenInterval(6);
        tw.TweenProperty(p, "modulate:a", 0f, 0.6f);
        tw.TweenCallback(Callable.From(p.QueueFree));
    }
}
