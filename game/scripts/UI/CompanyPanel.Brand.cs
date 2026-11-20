using System;
using DatacenterArchitect.Core;
using DatacenterArchitect.Sim;
using DatacenterArchitect.World;
using Godot;

namespace DatacenterArchitect.UI;

/// <summary>Step 11 round 3: the company's name and logo (shape, colours, initials) and where the logo shows.</summary>
public partial class CompanyPanel
{
    void BrandTab(GameState s)
    {
        var logo = s.Logo;
        _content.AddChild(Ui.Text("br_help", 14, Brand.InkDim));
        var top = new HBoxContainer(); top.AddThemeConstantOverride("separation", 20);
        _content.AddChild(top);
        // big preview, with the name as a wordmark
        var prevBox = new VBoxContainer(); prevBox.AddThemeConstantOverride("separation", 6);
        var preview = new LogoView { CustomMinimumSize = new Vector2(180, 180) };
        preview.Set(logo, logo.Letters(s.Options.Company));
        prevBox.AddChild(preview);
        var word = Plain(s.Options.Company, 20, new Color(logo.Primary)); word.AddThemeFontOverride("font", Ui.DisplayBold);
        word.HorizontalAlignment = HorizontalAlignment.Center; word.CustomMinimumSize = new Vector2(180, 0);
        prevBox.AddChild(word);
        top.AddChild(prevBox);

        var right = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; right.AddThemeConstantOverride("separation", 8);
        top.AddChild(right);
        // company name
        right.AddChild(Ui.Heading("br_name"));
        var nameRow = new HBoxContainer(); nameRow.AddThemeConstantOverride("separation", 8);
        var name = new LineEdit { Text = s.Options.Company, MaxLength = 40, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        nameRow.AddChild(name);
        void Rename()
        {
            var why = _state().Rename(name.Text);
            if (why != null) _fail(why);
            _sig = null; Refresh(true);
        }
        name.TextSubmitted += _ => Rename();
        nameRow.AddChild(Ui.Secondary("br_rename", Rename));
        right.AddChild(nameRow);
        // initials
        right.AddChild(Ui.Heading("br_initials"));
        var initRow = new HBoxContainer(); initRow.AddThemeConstantOverride("separation", 8);
        var init = new LineEdit { Text = logo.Initials, MaxLength = 3, PlaceholderText = CompanyLogo.InitialsOf(s.Options.Company), CustomMinimumSize = new Vector2(90, 0) };
        init.TextSubmitted += t => Change(l => l.Initials = t.Trim());
        init.FocusExited += () => { if (init.Text.Trim() != _state().Logo.Initials) Change(l => l.Initials = init.Text.Trim()); };
        initRow.AddChild(init);
        var initHelp = Plain(Loc.T("br_initials_help"), 13, Brand.InkDim); initHelp.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        initRow.AddChild(initHelp);
        right.AddChild(initRow);

        // shapes, each drawn in the current colours
        right.AddChild(Ui.Heading("br_shape"));
        var shapes = new HBoxContainer(); shapes.AddThemeConstantOverride("separation", 6);
        for (var i = 0; i < CompanyLogo.Shapes; i++)
        {
            var shape = i;
            var b = new Button { CustomMinimumSize = new Vector2(64, 64), ToggleMode = true, ButtonPressed = logo.Shape == i, TooltipText = "br_shape_" + i };
            var lv = new LogoView { MouseFilter = MouseFilterEnum.Ignore };
            lv.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect, LayoutPresetMode.KeepSize, 6);
            lv.Set(new CompanyLogo { Shape = i, Primary = logo.Primary, Secondary = logo.Secondary }, logo.Letters(s.Options.Company));
            b.AddChild(lv);
            if (logo.Shape == i) b.AddThemeStyleboxOverride("normal", Ui.Box(Brand.PanelHover, 8, Brand.Amber, 2, 4));
            b.Pressed += () => Change(l => l.Shape = shape);
            shapes.AddChild(b);
        }
        right.AddChild(shapes);
        right.AddChild(Ui.Heading("br_primary"));
        right.AddChild(Swatches(logo.Primary, c => Change(l => l.Primary = c)));
        right.AddChild(Ui.Heading("br_secondary"));
        right.AddChild(Swatches(logo.Secondary, c => Change(l => l.Secondary = c)));

        // where it shows
        _content.AddChild(new HSeparator());
        _content.AddChild(Ui.Heading("br_where"));
        var grid = new GridContainer { Columns = 2 }; grid.AddThemeConstantOverride("h_separation", 16);
        foreach (var (key, get, set) in new (string, Func<CompanyLogo, bool>, Action<CompanyLogo, bool>)[]
        {
            ("br_on_buildings", l => l.OnBuildings, (l, v) => l.OnBuildings = v),
            ("br_on_vehicles", l => l.OnVehicles, (l, v) => l.OnVehicles = v),
            ("br_on_workwear", l => l.OnWorkwear, (l, v) => l.OnWorkwear = v),
            ("br_on_racks", l => l.OnRacks, (l, v) => l.OnRacks = v),
        })
        {
            var cb = new CheckBox { Text = key, ButtonPressed = get(logo), SizeFlagsHorizontal = SizeFlags.ExpandFill };
            var setter = set;
            cb.Toggled += v => Change(l => setter(l, v));
            grid.AddChild(cb);
        }
        _content.AddChild(grid);
        _content.AddChild(Ui.Text("br_where_help", 13, Brand.InkDim));
        _content.AddChild(Ui.Secondary("br_random", () => Change(l =>
        {
            var r = new Random();
            l.Shape = r.Next(CompanyLogo.Shapes);
            l.Primary = CompanyLogo.Palette[r.Next(8)];
            l.Secondary = r.Next(2) == 0 ? "#111d2b" : "#f4f1ea";
        })));
    }

    HFlowContainer Swatches(string current, Action<string> pick)
    {
        var row = new HFlowContainer(); row.AddThemeConstantOverride("h_separation", 6);
        foreach (var hex in CompanyLogo.Palette)
        {
            var h = hex;
            var on = string.Equals(hex, current, StringComparison.OrdinalIgnoreCase);
            var b = new Button { CustomMinimumSize = new Vector2(34, 34), TooltipText = hex };
            var box = Ui.Box(new Color(hex), 17, on ? Brand.Ink : Brand.Line, on ? 3 : 1, 0);
            b.AddThemeStyleboxOverride("normal", box); b.AddThemeStyleboxOverride("hover", box); b.AddThemeStyleboxOverride("pressed", box);
            b.Pressed += () => pick(h);
            row.AddChild(b);
        }
        return row;
    }

    void Change(Action<CompanyLogo> edit)
    {
        var s = _state();
        if (s == null) return;
        edit(s.Logo);
        s.Touch(false);
        _sig = null; Refresh(true);
    }
}
