using System;
using DatacenterArchitect.Core;
using Godot;

namespace DatacenterArchitect.UI;

/// <summary>
/// One row of an infrastructure tree (network tree, later the power tree): icon, name with a sub line, optional chips,
/// a load bar and a value on the right. Click selects it; the screen shows the details of the selected row.
/// </summary>
public partial class TreeRow : PanelContainer
{
    public readonly Label Title, Sub, Value;
    public readonly ProgressBar Bar;
    public readonly HBoxContainer Chips;
    readonly PanelContainer _iconBox;
    readonly TextureRect _icon;
    Color _accent;
    bool _selected, _warn;

    public TreeRow(string icon, Color accent, Action pressed, float barWidth = 130, float valueWidth = 120)
    {
        _accent = accent;
        MouseFilter = MouseFilterEnum.Stop;
        AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        var h = new HBoxContainer();
        h.AddThemeConstantOverride("separation", 12);
        AddChild(h);
        _iconBox = new PanelContainer { CustomMinimumSize = new Vector2(32, 32), SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore };
        _icon = new TextureRect
        {
            Texture = GD.Load<Texture2D>($"res://assets/icons/{icon}.svg"), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new Vector2(18, 18), MouseFilter = MouseFilterEnum.Ignore,
        };
        _iconBox.AddChild(_icon);
        h.AddChild(_iconBox);
        var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(140, 0) };
        text.AddThemeConstantOverride("separation", 0);
        Title = L(15, Brand.Ink); Title.AddThemeFontOverride("font", Ui.DisplaySemi);
        Sub = L(13, Brand.InkDim);
        text.AddChild(Title); text.AddChild(Sub);
        h.AddChild(text);
        Chips = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        Chips.AddThemeConstantOverride("separation", 6);
        h.AddChild(Chips);
        Bar = new ProgressBar
        {
            MinValue = 0, MaxValue = 1, ShowPercentage = false, CustomMinimumSize = new Vector2(barWidth, 8),
            SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore, Visible = barWidth > 0,
        };
        Bar.AddThemeStyleboxOverride("background", Ui.Box(new Color("#96BEDC", 0.14f), 4, null, 0, 0));
        h.AddChild(Bar);
        Value = L(17, Brand.Ink);
        Value.AddThemeFontOverride("font", Ui.DisplaySemi);
        Value.HorizontalAlignment = HorizontalAlignment.Right;
        Value.CustomMinimumSize = new Vector2(valueWidth, 0);
        Value.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        h.AddChild(Value);
        GuiInput += e =>
        {
            if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) { pressed?.Invoke(); AcceptEvent(); }
        };
        Style();
    }

    static Label L(int size, Color c)
    {
        var l = new Label { AutoTranslateMode = AutoTranslateModeEnum.Disabled, MouseFilter = MouseFilterEnum.Ignore, ClipText = true, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis };
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", c);
        return l;
    }

    /// <summary>Load bar 0..1 (above 1 shows as critical); warn turns the row amber.</summary>
    public void SetLoad(double ratio, bool warn)
    {
        Bar.Value = Math.Min(1, ratio);
        var col = ratio > 1 ? Brand.Critical : warn || ratio > 0.85 ? Brand.Warning : _accent;
        Bar.AddThemeStyleboxOverride("fill", Ui.Box(col, 4, null, 0, 0));
        if (warn != _warn) { _warn = warn; Style(); }
    }

    public bool Selected { get => _selected; set { if (_selected == value) return; _selected = value; Style(); } }

    void Style()
    {
        var border = _selected ? _accent : _warn ? new Color(Brand.Warning, 0.6f) : Brand.Line;
        AddThemeStyleboxOverride("panel", Ui.Box(_selected ? new Color("#1A2C40") : Brand.PanelSolid, 8, border, 1, 8));
        var tint = _warn ? Brand.Warning : _accent;
        _iconBox.AddThemeStyleboxOverride("panel", Ui.Box(new Color(tint, 0.16f), 8, null, 0, 7));
        _icon.Modulate = tint == Brand.Ink ? Brand.Ink : tint.Lightened(0.15f);
        Sub.AddThemeColorOverride("font_color", _warn ? Brand.Warning.Lightened(0.2f) : Brand.InkDim);
    }

    /// <summary>A small label chip next to the name, e.g. the hall a switch belongs to.</summary>
    public static PanelContainer Chip(string text, Color? bg = null, Color? ink = null)
    {
        var p = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
        p.AddThemeStyleboxOverride("panel", Ui.Box(bg ?? new Color("#1D3247"), 4, null, 0, 0));
        var l = L(12, ink ?? Brand.Ink);
        l.Text = text;
        l.ClipText = false;
        l.TextOverrunBehavior = TextServer.OverrunBehavior.NoTrimming;
        p.AddChild(Ui.Pad(l, 8, 2, 8, 2));
        return p;
    }
}

/// <summary>Helpers for infrastructure trees: indented branches with a guide line and a grid of port squares.</summary>
public static class TreeRows
{
    /// <summary>An indented branch under a row, with a thin guide line on its left in the system colour.</summary>
    public static VBoxContainer Branch(Container parent, Color line)
    {
        var box = new PanelContainer();
        var st = new StyleBoxFlat { BgColor = Colors.Transparent, BorderColor = new Color(line, 0.4f), BorderWidthLeft = 2 };
        st.ContentMarginLeft = 12; st.ContentMarginTop = 2; st.ContentMarginBottom = 2;
        box.AddThemeStyleboxOverride("panel", st);
        var m = new MarginContainer();
        m.AddThemeConstantOverride("margin_left", 14);
        m.AddChild(box);
        parent.AddChild(m);
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 6);
        box.AddChild(v);
        return v;
    }

    /// <summary>One square per port: used ports filled in the accent colour, A/B second ports in a lighter shade, free ports dim.</summary>
    public static HFlowContainer Ports(int total, int used, int dual, Color accent, int max = 96)
    {
        var flow = new HFlowContainer { CustomMinimumSize = new Vector2(300, 0) };
        flow.AddThemeConstantOverride("h_separation", 4);
        flow.AddThemeConstantOverride("v_separation", 4);
        var shown = Math.Min(total, max);
        for (var i = 0; i < shown; i++)
        {
            var c = i < used - dual ? accent : i < used ? accent.Lightened(0.35f) : new Color("#96BEDC", 0.16f);
            flow.AddChild(new ColorRect { Color = c, CustomMinimumSize = new Vector2(14, 14), MouseFilter = Control.MouseFilterEnum.Ignore });
        }
        return flow;
    }

    /// <summary>A card on the right of a tree with a small heading.</summary>
    public static VBoxContainer Card(Container parent, string headingKey = null)
    {
        var p = new PanelContainer();
        p.AddThemeStyleboxOverride("panel", Ui.Box(Brand.PanelSolid, 10, Brand.Line, 1, 14));
        parent.AddChild(p);
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 8);
        p.AddChild(v);
        if (headingKey != null) v.AddChild(Ui.Heading(headingKey, 12, Brand.InkDim));
        return v;
    }
}
