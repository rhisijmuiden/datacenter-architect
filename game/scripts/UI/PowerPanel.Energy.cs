using System;
using System.Collections.Generic;
using System.Linq;
using DatacenterArchitect.Core;
using DatacenterArchitect.Sim;
using Godot;

namespace DatacenterArchitect.UI;

/// <summary>
/// Infrastructure › Own energy: how much of your use comes from your own solar, wind and battery, an hourly chart of
/// the last day, where your own power went, the battery policy and the road to the green datacenter label.
/// </summary>
public partial class PowerPanel
{
    public static readonly Color Solar = new("#FFD866"), Wind = new("#A8E6C4"), Store = new("#8FA8FF"), GridGrey = new("#4E6378");

    static string Mwh(double kwh) => kwh >= 10000 ? N(kwh / 1000, "0.0") + " MWh" : N(kwh) + " kWh";
    static string P(double v) => N(Math.Round(v * 100)) + "%";

    string EnergySig(GameState s)
    {
        var last = s.LastEnergy;
        return $"{last?.Day}:{s.BatteryUse}:{(int)Math.Round(s.BatteryReserve * 100)}:{s.SellSurplus}:{s.SolarPanels}:{s.WindTurbines}:{s.BatteryKwh}:" +
               $"{s.GreenNominatedDay}:{s.GoldNominatedDay}:{string.Join(",", s.Certs.Select(c => $"{c.Kind}{c.Valid}{c.Suspended}{c.AuditEnds}"))}:{Loc.Language}";
    }

    void BuildEnergy(GameState s)
    {
        _content.AddChild(Ui.Heading("own_title", 14, Brand.Ok));
        _content.AddChild(Plain(Loc.T("own_help"), 12, Brand.InkDim));
        var last = s.LastEnergy;

        // ---- key numbers ----
        var tiles = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        tiles.AddThemeConstantOverride("separation", 10);
        _content.AddChild(tiles);
        VBoxContainer Tile(string key, Color accent, bool hero = false)
        {
            var p = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = hero ? 1.3f : 1 };
            p.AddThemeStyleboxOverride("panel", Ui.Box(hero ? new Color(accent, 0.12f) : Brand.PanelSolid, 10, hero ? new Color(accent, 0.6f) : Brand.Line, 1, 12));
            var v = new VBoxContainer();
            v.AddThemeConstantOverride("separation", 2);
            p.AddChild(v);
            v.AddChild(Plain(Loc.T(key).ToUpperInvariant(), 11, hero ? accent : Brand.InkDim, false));
            tiles.AddChild(p);
            return v;
        }
        Label Big(string text, Color c, int size = 30)
        {
            var l = Ui.Number(text, size); l.AddThemeColorOverride("font_color", c); l.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
            return l;
        }
        var share30 = s.OwnShare30;
        var hero = Tile("own_share", Brand.Ok, true);
        hero.AddChild(Big(P(last?.OwnShare ?? share30), Brand.Ok, 40));
        hero.AddChild(Plain(Loc.T("own_share_30", P(share30), P(GameState.GreenThreshold)), 12, Brand.Ink));

        var gen = Tile("own_generation", Solar);
        gen.AddChild(Big(Mwh(last?.Own ?? 0), Brand.Ink));
        gen.AddChild(Plain(Loc.T("own_gen_split", Mwh(last?.Solar ?? 0), Mwh(last?.Wind ?? 0)), 12, Brand.InkDim));

        var grid = Tile("own_from_grid", Brand.InkDim);
        grid.AddChild(Big(Mwh(last?.Grid ?? 0), Brand.Ink));
        grid.AddChild(Plain(Ui.Money((long)Math.Round(last?.GridEur ?? 0)), 12, Brand.InkDim));

        var sold = Tile("own_sold", Brand.Power);
        sold.AddChild(Big(Mwh(last?.Sold ?? 0), Brand.Ink));
        sold.AddChild(Plain("+ " + Ui.Money((long)Math.Round(last?.SoldEur ?? 0)) + (last?.Curtailed > 1 ? "  ·  " + Loc.T("own_curtailed", Mwh(last.Curtailed)) : ""), 12, Brand.InkDim));

        var bat = Tile("own_battery", Store);
        bat.AddChild(Dyn((l, st, _) => l.Text = st.BatteryKwh > 0 ? P(st.BatterySoc / st.BatteryKwh) : "-", 28, Brand.Ink, false));
        var bar = new ProgressBar { MinValue = 0, MaxValue = 1, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 6) };
        _updaters.Add((st, _) => bar.Value = st.BatteryKwh > 0 ? st.BatterySoc / st.BatteryKwh : 0);
        bat.AddChild(bar);
        bat.AddChild(Plain(Loc.T("own_battery_size", s.Objects.Count(o => o.Kind == ObjKind.BatteryContainer && o.Status == ObjStatus.Live), N(s.BatteryKwh)), 12, Brand.InkDim));

        if (last == null)
        {
            _content.AddChild(Plain(Loc.T("own_no_data"), 14, Brand.InkDim));
        }
        else
        {
            // ---- hourly chart + flows ----
            var cols = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            cols.AddThemeConstantOverride("separation", 18);
            _content.AddChild(cols);
            var left = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = 1.6f };
            var right = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            right.AddThemeConstantOverride("separation", 4);
            cols.AddChild(left); cols.AddChild(right);
            left.AddChild(Ui.Heading("own_chart", 12));
            var legend = new HFlowContainer();
            legend.AddThemeConstantOverride("h_separation", 14);
            foreach (var (key, c) in new[] { ("own_l_direct", Brand.Ok), ("own_l_battery", Store), ("own_l_grid", GridGrey), ("own_l_gen", Solar), ("own_l_use", Brand.Ink) })
                legend.AddChild(Swatch(Loc.T(key), c));
            left.AddChild(legend);
            left.AddChild(new EnergyChart(s.EnergyHours) { CustomMinimumSize = new Vector2(0, 210), SizeFlagsHorizontal = SizeFlags.ExpandFill });

            var own = Math.Max(1e-9, last.Own);
            right.AddChild(Ui.Heading("own_where_own", 12));
            right.AddChild(new StackBar(new[] { (last.Direct / own, Brand.Ok), (last.Charged / own, Store), (last.Sold / own, Brand.Power), (last.Curtailed / own, Brand.Critical) }) { CustomMinimumSize = new Vector2(0, 18) });
            FlowLine(right, "own_f_direct", last.Direct, Brand.Ok);
            FlowLine(right, "own_f_charged", last.Charged, Store);
            FlowLine(right, "own_f_sold", last.Sold, Brand.Power);
            if (last.Curtailed > 1) FlowLine(right, "own_f_curtailed", last.Curtailed, Brand.Critical);
            if (last.Own > 0) right.AddChild(Plain(Loc.T("own_used_self", P((last.Direct + last.Charged) / own)), 12, Brand.InkDim));
            right.AddChild(new HSeparator());
            var use = Math.Max(1e-9, last.Use);
            right.AddChild(Ui.Heading("own_where_use", 12));
            right.AddChild(new StackBar(new[] { (last.Direct / use, Brand.Ok), (last.Discharged / use, Store), (last.Grid / use, GridGrey) }) { CustomMinimumSize = new Vector2(0, 18) });
            FlowLine(right, "own_u_direct", last.Direct, Brand.Ok);
            FlowLine(right, "own_u_battery", last.Discharged, Store);
            FlowLine(right, "own_u_grid", last.Grid, GridGrey);
            FlowLine(right, "own_u_total", last.Use, Brand.Ink);

            if (s.EnergyLog.Count > 2)
            {
                _content.AddChild(Ui.Heading("own_trend", 12));
                _content.AddChild(new MiniChart(s.EnergyLog.Select(r => r.OwnShare * 100).ToList(), Brand.Ok, true) { CustomMinimumSize = new Vector2(0, 70), SizeFlagsHorizontal = SizeFlags.ExpandFill });
            }
        }

        _content.AddChild(new HSeparator());
        var lower = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        lower.AddThemeConstantOverride("separation", 24);
        _content.AddChild(lower);

        // ---- battery policy ----
        var pol = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        pol.AddThemeConstantOverride("separation", 6);
        lower.AddChild(pol);
        pol.AddChild(Ui.Heading("own_policy", 13, Store));
        foreach (var m in new[] { BatteryMode.Own, BatteryMode.Trade, BatteryMode.Smart })
        {
            var mode = m;
            var b = Ui.Secondary(Loc.T("own_mode_" + m.ToString().ToLowerInvariant()) + "\n" + Loc.T("own_mode_" + m.ToString().ToLowerInvariant() + "_help"), () =>
            {
                _state().BatteryUse = mode; _sig = null; Refresh();
            });
            b.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
            b.Alignment = HorizontalAlignment.Left;
            if (s.BatteryUse == m) { var st = Ui.Box(new Color(Brand.Ok, 0.14f), 8, Brand.Ok, 2, 8); b.AddThemeStyleboxOverride("normal", st); b.AddThemeStyleboxOverride("hover", st); }
            pol.AddChild(b);
        }
        var rr = new HBoxContainer();
        rr.AddChild(Plain(Loc.T("own_reserve"), 14, Brand.Ink, false));
        var slider = new HSlider { MinValue = 0, MaxValue = 50, Step = 5, Value = Math.Round(s.BatteryReserve * 100), SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(160, 0) };
        var rv = Plain(P(s.BatteryReserve), 14, Brand.Ink, false);
        slider.ValueChanged += v => { _state().BatteryReserve = v / 100; rv.Text = N(v) + "%"; };
        slider.DragEnded += _ => _sig = null;
        rr.AddChild(slider); rr.AddChild(rv);
        pol.AddChild(rr);
        pol.AddChild(Plain(Loc.T("own_reserve_help"), 12, Brand.InkDim));
        var sell = new CheckButton { Text = Loc.T("own_sell"), ButtonPressed = s.SellSurplus, AutoTranslateMode = AutoTranslateModeEnum.Disabled };
        sell.Toggled += on => { _state().SellSurplus = on; _sig = null; };
        pol.AddChild(sell);
        pol.AddChild(Plain(Loc.T("own_sell_help", N(Catalog.GridLevels[s.GridLevel].Mva)), 12, Brand.InkDim));
        pol.AddChild(Plain(Loc.T("own_objects", s.SolarPanels, N(s.SolarKwpTotal), s.WindTurbines, N(s.WindKwTotal)), 12, Brand.InkDim));

        // ---- green label ----
        var lab = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        lab.AddThemeConstantOverride("separation", 6);
        lower.AddChild(lab);
        lab.AddChild(Ui.Heading("own_label", 13, Brand.Ok));
        LabelCard(lab, s, CertKind.Green, GameState.GreenThreshold, s.GreenNominatedDay, s.GreenBelowDays, Brand.Ok);
        LabelCard(lab, s, CertKind.GreenGold, GameState.GoldThreshold, s.GoldNominatedDay, s.GoldBelowDays, Solar);
        lab.AddChild(Plain(Loc.T("own_label_help"), 12, Brand.InkDim));
    }

    void LabelCard(VBoxContainer parent, GameState s, CertKind kind, double threshold, int nominated, int below, Color accent)
    {
        var c = s.Cert(kind);
        var has = s.HasCert(kind);
        var p = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        p.AddThemeStyleboxOverride("panel", Ui.Box(has ? new Color(accent, 0.1f) : Brand.PanelSolid, 8, has ? new Color(accent, 0.55f) : Brand.Line, 1, 10));
        var v = new VBoxContainer();
        p.AddChild(v);
        var head = new HBoxContainer();
        var name = Plain(Loc.T(CertCatalog.Defs[kind].Key), 16, Brand.Ink, false); name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        head.AddChild(name);
        string status; Color sc;
        if (c?.Suspended == true) { status = Loc.T("own_st_suspended"); sc = Brand.Critical; }
        else if (has) { status = Loc.T("own_st_has"); sc = accent; }
        else if (c?.AuditEnds > 0) { status = Loc.T("own_st_audit"); sc = Brand.Amber; }
        else if (nominated >= 0) { status = Loc.T("own_st_nominated"); sc = Brand.Amber; }
        else { status = Loc.T("own_st_none"); sc = Brand.InkDim; }
        head.AddChild(Plain(status, 13, sc, false));
        v.AddChild(head);
        var share = s.OwnShare30;
        var line = new HBoxContainer();
        line.AddChild(Plain(Loc.T("own_label_need", P(threshold)), 13, Brand.InkDim, false));
        var pb = new ProgressBar { MinValue = 0, MaxValue = 1, ShowPercentage = false, Value = Math.Min(1, share / threshold), CustomMinimumSize = new Vector2(0, 8), SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        line.AddChild(pb);
        line.AddChild(Plain(P(share), 13, share >= threshold ? accent : Brand.Ink, false));
        v.AddChild(line);
        if (has && below > 0) v.AddChild(Plain(Loc.T("own_grace", GameState.GreenGraceDays - below), 12, Brand.Warning));
        else if (!has && c?.AuditEnds is not > 0)
            v.AddChild(Plain(Loc.T(nominated >= 0 ? "own_go_audit" : "own_get_nominated", P(threshold)), 12, Brand.InkDim));
        parent.AddChild(p);
    }

    static Control Swatch(string text, Color c)
    {
        var h = new HBoxContainer();
        h.AddThemeConstantOverride("separation", 5);
        var r = new ColorRect { Color = c, CustomMinimumSize = new Vector2(10, 10), SizeFlagsVertical = SizeFlags.ShrinkCenter };
        h.AddChild(r);
        h.AddChild(Plain(text, 12, Brand.InkDim, false));
        return h;
    }

    static void FlowLine(VBoxContainer parent, string key, double kwh, Color c)
    {
        var h = new HBoxContainer();
        h.AddThemeConstantOverride("separation", 6);
        h.AddChild(new ColorRect { Color = c, CustomMinimumSize = new Vector2(10, 10), SizeFlagsVertical = SizeFlags.ShrinkCenter });
        var l = Plain(Loc.T(key), 13, Brand.Ink, false); l.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        h.AddChild(l);
        h.AddChild(Plain(Mwh(kwh), 13, Brand.Ink, false));
        parent.AddChild(h);
    }
}

/// <summary>One bar split into coloured parts (fractions of the whole).</summary>
public partial class StackBar : Control
{
    readonly (double part, Color c)[] _parts;
    public StackBar((double, Color)[] parts) { _parts = parts; }

    public override void _Draw()
    {
        DrawStyleBox(Ui.Box(new Color(Brand.Line, 0.5f), 5, null, 0, 0), new Rect2(Vector2.Zero, Size));
        float x = 0;
        foreach (var (part, c) in _parts)
        {
            var w = (float)Math.Clamp(part, 0, 1) * Size.X;
            if (w <= 0.5f) continue;
            DrawRect(new Rect2(x, 0, Math.Min(w, Size.X - x), Size.Y), c);
            x += w;
        }
    }
}

/// <summary>The last day hour by hour: stacked own power, battery and grid under the use line, generation dashed.</summary>
public partial class EnergyChart : Control
{
    readonly List<EnergyHour> _h;
    public EnergyChart(List<EnergyHour> hours) { _h = hours?.ToList() ?? new(); }

    public override void _Draw()
    {
        DrawStyleBox(Ui.Box(new Color(1, 1, 1, 0.03f), 8, Brand.Line, 1, 0), new Rect2(Vector2.Zero, Size));
        if (_h.Count == 0) return;
        var max = Math.Max(1, _h.Max(x => Math.Max(x.Use, x.Own))) * 1.1;
        float padL = 44, padR = 10, padT = 10, padB = 22;
        float w = Size.X - padL - padR, hh = Size.Y - padT - padB;
        var font = ThemeDB.FallbackFont;
        float X(double i) => padL + (float)(w * i / _h.Count);
        float Y(double v) => padT + hh - (float)(v / max * hh);
        for (var g = 0; g <= 4; g++)
        {
            var v = max / 1.1 * g / 4;
            DrawLine(new Vector2(padL, Y(v)), new Vector2(padL + w, Y(v)), new Color(Brand.Line, 0.6f));
            DrawString(font, new Vector2(2, Y(v) + 4), v >= 10000 ? $"{v / 1000:0.#} MW" : $"{v:0} kW", HorizontalAlignment.Left, padL - 4, 10, Brand.InkDim);
        }
        for (var i = 0; i < _h.Count; i++)
        {
            var x = _h[i];
            float x0 = X(i) + 1, x1 = X(i + 1) - 1;
            var a = x.Direct; var b = a + x.Discharged; var c = b + x.Grid;
            DrawRect(new Rect2(x0, Y(a), x1 - x0, Y(0) - Y(a)), Brand.Ok);
            DrawRect(new Rect2(x0, Y(b), x1 - x0, Y(a) - Y(b)), PowerPanel.Store);
            DrawRect(new Rect2(x0, Y(c), x1 - x0, Y(b) - Y(c)), PowerPanel.GridGrey);
            if (x.Own > x.Use) DrawRect(new Rect2(x0, Y(x.Own), x1 - x0, Y(x.Use) - Y(x.Own)), new Color(PowerPanel.Solar, 0.3f));
            if (i % 6 == 0) DrawString(font, new Vector2(X(i), Size.Y - 6), $"{i:00}:00", HorizontalAlignment.Left, 60, 10, Brand.InkDim);
        }
        var use = _h.Select((x, i) => new Vector2(X(i + 0.5), Y(x.Use))).ToArray();
        var gen = _h.Select((x, i) => new Vector2(X(i + 0.5), Y(x.Own))).ToArray();
        if (use.Length > 1) DrawPolyline(use, Brand.Ink, 2, true);
        if (gen.Length > 1) for (var i = 0; i + 1 < gen.Length; i++) DrawDashedLine(gen[i], gen[i + 1], PowerPanel.Solar, 2, 5);
    }
}
