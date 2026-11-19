using System;
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
        return t;
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

    public static string Money(long v) => (v < 0 ? "-€ " : "€ ") + Math.Abs(v).ToString("N0", Loc.Language == "nl" ? new System.Globalization.CultureInfo("nl-NL") : new System.Globalization.CultureInfo("en-GB"));
}

/// <summary>A dimmed full-screen layer with a centred panel. Close with Esc or the close button.</summary>
public partial class Modal : Control
{
    public VBoxContainer Body { get; private set; }
    public Action OnClosed;
    readonly string _titleKey;
    readonly float _width;

    public Modal(string titleKey, float width = 520)
    {
        _titleKey = titleKey; _width = width;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        var dim = new ColorRect { Color = new Color(0.03f, 0.06f, 0.1f, 0.55f) };
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(dim);
        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(_width, 0) };
        panel.AddThemeStyleboxOverride("panel", Ui.Box(Brand.PanelSolid, 12, Brand.Line, 1, 22, true));
        center.AddChild(panel);
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 12);
        panel.AddChild(v);
        var head = new HBoxContainer();
        var title = new Label { Text = _titleKey, Uppercase = true, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        title.AddThemeFontOverride("font", Ui.DisplayBold);
        title.AddThemeFontSizeOverride("font_size", 24);
        head.AddChild(title);
        var close = new Button { Text = "×", TooltipText = "ui_close" };
        close.Pressed += Close;
        head.AddChild(close);
        v.AddChild(head);
        Body = new VBoxContainer();
        Body.AddThemeConstantOverride("separation", 10);
        v.AddChild(Body);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e.IsActionPressed("ui_cancel")) { Close(); GetViewport().SetInputAsHandled(); }
    }

    public void Close()
    {
        OnClosed?.Invoke();
        QueueFree();
    }
}
