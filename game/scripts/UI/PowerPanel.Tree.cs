using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DatacenterArchitect.Core;
using DatacenterArchitect.Sim;
using Godot;

namespace DatacenterArchitect.UI;

/// <summary>
/// The power tree tab: every main switchboard (MSB) with what it feeds, free outputs, and the load on each UPS,
/// PDU and busbar run below it. Click a device for its card: capacity, rack connections, what feeds it and a tip.
/// </summary>
public partial class PowerPanel
{
    /// <summary>Selected device, by <see cref="Key"/>.</summary>
    string _selected;
    /// <summary>Show only the A or the B side, or both (null).</summary>
    FeedSide? _side;

    /// <summary>Show the tree with this device selected.</summary>
    public void Select(PlacedObject o)
    {
        _tab = TabTree; _selected = o == null ? null : "o" + o.Id; _sig = null;
        Refresh();
    }

    static string Key(PowerNode n) => n.Obj != null ? "o" + n.Obj.Id : n.Kind == PowerNodeKind.Busbar ? $"b{n.Hall?.Id}{n.Side}" : "mech";

    static IEnumerable<PowerNode> All(PowerTree t)
    {
        if (t == null) yield break;
        foreach (var n in t.Msbs) yield return n;
        foreach (var n in t.Ups) yield return n;
        foreach (var n in t.Sources) yield return n;
        if (t.Mech != null) yield return t.Mech;
    }

    static PowerNode Find(InfraReport r, string key) => key == null ? null : All(r?.Tree).FirstOrDefault(n => Key(n) == key);

    /// <summary>Rebuild the tab only when the shape of the tree changes; loads update every half second.</summary>
    string TreeSig(GameState s, InfraReport r)
    {
        var sb = new StringBuilder($"tree:{Loc.Language}:{_selected}:{_side}:{s.GridLevel}:");
        foreach (var n in All(r?.Tree)) sb.Append(Key(n)).Append('>').Append(n.Parent == null ? "-" : Key(n.Parent)).Append(n.Side).Append(n.Obj?.Kind).Append(';');
        return sb.ToString();
    }

    static Texture2D Icon(PowerNodeKind k) => GD.Load<Texture2D>($"res://assets/icons/{k switch { PowerNodeKind.Msb => "msb", PowerNodeKind.Ups => "ups", PowerNodeKind.Busbar => "busbar", PowerNodeKind.Mech => "fan", _ => "pdu" }}.svg");

    public static string NodeName(PowerNode n) => n.Kind switch
    {
        PowerNodeKind.Mech => Loc.T("ptree_mech"),
        PowerNodeKind.Busbar => Loc.T("ptree_busbar", n.Side),
        _ => n.Name,
    };

    /// <summary>What the device is, with its key numbers, for the line under its name.</summary>
    public static string NodeSub(PowerNode n, GameState s)
    {
        string text = n.Kind switch
        {
            PowerNodeKind.Msb => Loc.T("ptree_sub_msb", Kw(n.CapacityKw), n.UsedOutputs, n.Outputs),
            PowerNodeKind.Ups => Loc.T("ptree_sub_ups", Kw(n.CapacityKw), n.UsedOutputs, n.Outputs, n.Racks),
            PowerNodeKind.Pdu => Loc.T("ptree_sub_pdu", n.UsedOutlets, n.Outlets) + (n.Full ? " · " + Loc.T("ptree_full") : ""),
            PowerNodeKind.Busbar => Loc.T("ptree_sub_busbar", n.Racks),
            _ => Loc.T("ptree_sub_mech"),
        };
        if (n.Direct) text += " · " + Loc.T("ptree_direct");
        if (n.Kind != PowerNodeKind.Mech && n.DualRacks > 0 && n.FailoverKw > n.LoadKw + 0.5)
            text += " · " + Loc.T("ptree_failover", Kw(n.FailoverKw));
        return text;
    }

    static Color BarColor(PowerNode n) => n.Overloaded || n.FailoverShort ? Brand.Critical : n.Full || n.Ratio > 0.85 ? Brand.Warning : n.Kind == PowerNodeKind.Mech ? Brand.Cooling : Brand.Power;

    void BuildTree(GameState s, InfraReport r)
    {
        var t = r?.Tree;
        _content.AddChild(Ui.Heading("ptree_title", 14, Brand.Power));
        _content.AddChild(Ui.Text("ptree_help", 12, Brand.InkDim));

        // upstream: grid › transformers › ATS with the generators › switchboards
        var chain = new HFlowContainer();
        chain.AddThemeConstantOverride("h_separation", 6);
        void Chip(string text, Color c, bool on = false)
        {
            var p = new PanelContainer();
            p.AddThemeStyleboxOverride("panel", Ui.Box(on ? new Color(c, 0.2f) : Brand.PanelHover, 5, null, 0, 6));
            p.AddChild(Plain(text, 13, on ? c : Brand.Ink, false));
            chain.AddChild(p);
        }
        void Arrow() => chain.AddChild(Plain("›", 14, Brand.InkDim, false));
        Chip(Loc.T("ptree_up_grid", N(Catalog.GridLevels[s.GridLevel].Mva)), Brand.Ink); Arrow();
        var trafos = s.Objects.Count(o => o.Kind == ObjKind.Transformer && o.Status == ObjStatus.Live);
        Chip(Loc.T("ptree_up_trafo", trafos + 1), Brand.Ink); Arrow();
        Chip(r.Ats ? Loc.T("ptree_up_ats", r.Generators, r.GenRedundancy) : Loc.T("ptree_up_no_ats"), r.Ats ? Brand.Ink : Brand.Critical, !r.Ats); Arrow();
        Chip(Loc.T("ptree_up_msb", t?.Msbs.Count ?? 0), Brand.Power, true);
        // own generation feeds the switchboards next to the grid
        if (s.SolarPanels + s.WindTurbines > 0 || s.BatteryKwh > 0)
        {
            var hour = s.EnergyHours.Count == 24 ? s.EnergyHours[(int)(s.Hours % 24)] : null;
            Arrow();
            Chip(Loc.T("ptree_up_own", N(s.SolarKwpTotal), N(s.WindKwTotal), hour == null ? "-" : Kw(hour.Own)), Brand.Ok, true);
            if (s.BatteryKwh > 0) Chip(Loc.T("ptree_up_battery", N(s.BatteryKwh), N(Math.Round(100 * s.BatterySoc / s.BatteryKwh))), PowerPanel.Store, true);
        }
        chain.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        foreach (var opt in new FeedSide?[] { null, FeedSide.A, FeedSide.B })
        {
            var o = opt;
            var b = Ui.Secondary(o == null ? Loc.T("ptree_side_both") : Loc.T("ptree_side", o), () => { _side = o; _sig = null; Refresh(); });
            b.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
            b.CustomMinimumSize = new Vector2(0, 30);
            if (_side == o) { var st = Ui.Box(Brand.PanelHover, 6, Brand.Power, 2, 6); b.AddThemeStyleboxOverride("normal", st); b.AddThemeStyleboxOverride("hover", st); }
            chain.AddChild(b);
        }
        _content.AddChild(chain);
        _content.AddChild(RouteCard());

        if (t == null || t.Msbs.Count == 0 && t.Unconnected.Count == 0 && t.Sources.Count == 0)
        {
            _content.AddChild(Ui.Text("ptree_none", 14, Brand.InkDim));
            return;
        }

        var rows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        rows.AddThemeConstantOverride("separation", 5);
        _content.AddChild(rows);
        bool Shown(PowerNode n) => _side == null || n.Kind == PowerNodeKind.Mech && _side == FeedSide.A || n.Side == _side || n.Children.Any(Shown);
        void Branch(PowerNode n, int depth)
        {
            if (!Shown(n)) return;
            rows.AddChild(Row(n, depth));
            foreach (var c in n.Children.OrderBy(c => c.Kind == PowerNodeKind.Mech ? 9 : (int)c.Kind).ThenBy(c => c.Hall?.Id ?? 0).ThenBy(c => c.Obj?.Id ?? 0))
                Branch(c, depth + 1);
            if (n.Kind is PowerNodeKind.Msb or PowerNodeKind.Ups && (_side == null || n.Side == _side)) rows.AddChild(FreeRow(n, depth + 1));
        }
        foreach (var m in t.Msbs) Branch(m, 0);
        if (t.Unconnected.Count > 0)
        {
            rows.AddChild(Ui.Heading("ptree_unconnected", 12, Brand.Critical));
            foreach (var n in t.Unconnected.Where(n => _side == null || n.Side == _side)) rows.AddChild(Row(n, 0));
        }

        // the selected device's card
        var sel = Find(r, _selected);
        _content.AddChild(new HSeparator());
        if (sel == null) _content.AddChild(Ui.Text("ptree_pick", 13, Brand.InkDim));
        else _content.AddChild(Card(s, r, sel));
    }

    Control Row(PowerNode n, int depth)
    {
        var key = Key(n);
        var outer = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        outer.AddThemeConstantOverride("separation", 0);
        if (depth > 0)
        {
            outer.AddChild(new Control { CustomMinimumSize = new Vector2(22 * (depth - 1) + 10, 0) });
            var line = new ColorRect { Color = new Color(Brand.Power, 0.35f), CustomMinimumSize = new Vector2(2, 0) };
            outer.AddChild(line);
            outer.AddChild(new Control { CustomMinimumSize = new Vector2(10, 0) });
        }
        var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Stop, MouseDefaultCursorShape = CursorShape.PointingHand };
        var selected = key == _selected;
        StyleBoxFlat Style(PowerNode x)
        {
            var bad = x.Overloaded || x.FailoverShort || !x.Connected;
            return Ui.Box(bad ? new Color("#2A1E24") : selected ? Brand.PanelHover : Brand.PanelSolid, 8, bad ? new Color(Brand.Critical, 0.7f) : selected ? Brand.Power : Brand.Line, selected ? 2 : 1, 8);
        }
        panel.AddThemeStyleboxOverride("panel", Style(n));
        panel.GuiInput += e =>
        {
            if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                _selected = _selected == key ? null : key; _sig = null;
                Callable.From(Refresh).CallDeferred();
            }
        };
        var h = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        h.AddThemeConstantOverride("separation", 10);
        panel.AddChild(h);
        var ico = new TextureRect { Texture = Icon(n.Kind), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new Vector2(n.Kind == PowerNodeKind.Msb ? 26 : 20, 0), MouseFilter = MouseFilterEnum.Ignore };
        ico.Modulate = n.Kind == PowerNodeKind.Mech ? Brand.Cooling : n.Kind is PowerNodeKind.Msb or PowerNodeKind.Ups ? Brand.Power : Brand.Ink;
        h.AddChild(ico);
        var names = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        names.AddThemeConstantOverride("separation", 0);
        var title = Plain(NodeName(n) + (n.Obj != null && n.Kind == PowerNodeKind.Pdu ? " · " + Loc.T(n.Obj.Def.NameKey) : ""), n.Kind == PowerNodeKind.Msb ? 17 : 15, Brand.Ink, false);
        title.AddThemeFontOverride("font", Ui.BodySemi);
        names.AddChild(title);
        var sub = Plain("", 12, Brand.InkDim, false);
        sub.ClipText = true; sub.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        names.AddChild(sub);
        h.AddChild(names);
        if (n.Kind != PowerNodeKind.Mech)
        {
            var side = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore, SizeFlagsVertical = SizeFlags.ShrinkCenter };
            side.AddThemeStyleboxOverride("panel", n.Side == FeedSide.A ? Ui.Box(Brand.Power, 4, null, 0, 4) : Ui.Box(Brand.PanelHover, 4, Brand.Power, 1, 4));
            side.AddChild(Plain(n.Side.ToString(), 12, n.Side == FeedSide.A ? new Color("#1A1206") : Brand.Power, false));
            h.AddChild(side);
        }
        if (n.Hall != null)
        {
            var chip = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore, SizeFlagsVertical = SizeFlags.ShrinkCenter };
            chip.AddThemeStyleboxOverride("panel", Ui.Box(Brand.PanelHover, 4, null, 0, 5));
            chip.AddChild(Plain(_state()?.HallCode(n.Hall) ?? "DH", 12, Brand.Ink, false));
            h.AddChild(chip);
        }
        var bar = new ProgressBar { MinValue = 0, MaxValue = 1, ShowPercentage = false, CustomMinimumSize = new Vector2(110, 7), SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore };
        bar.Visible = n.Kind != PowerNodeKind.Mech;
        h.AddChild(bar);
        var val = Plain("", 15, Brand.Ink, false);
        val.CustomMinimumSize = new Vector2(124, 0); val.HorizontalAlignment = HorizontalAlignment.Right;
        h.AddChild(val);
        _updaters.Add((s, r) =>
        {
            var x = Find(r, key) ?? r?.Tree?.Unconnected.FirstOrDefault(u => Key(u) == key);
            if (x == null) return;
            sub.Text = x.Connected ? NodeSub(x, s) : Loc.T("ptree_not_connected");
            bar.Value = Math.Min(1, x.Ratio);
            bar.AddThemeStyleboxOverride("fill", Ui.Box(BarColor(x), 3, null, 0, 0));
            val.Text = x.Kind == PowerNodeKind.Mech ? Kw(x.LoadKw) : $"{N(x.LoadKw)} / {Kw(x.CapacityKw)}";
            val.AddThemeColorOverride("font_color", x.Overloaded ? Brand.Critical : Brand.Ink);
            panel.AddThemeStyleboxOverride("panel", Style(x));
        });
        outer.AddChild(panel);
        return outer;
    }

    /// <summary>The dashed line under a switchboard or UPS: how many outputs and kW are still free.</summary>
    Control FreeRow(PowerNode n, int depth)
    {
        var key = Key(n);
        var outer = new HBoxContainer();
        outer.AddChild(new Control { CustomMinimumSize = new Vector2(22 * (depth - 1) + 22, 0) });
        var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var st = Ui.Box(new Color(0, 0, 0, 0), 8, new Color(Brand.InkDim, 0.4f), 1, 6);
        panel.AddThemeStyleboxOverride("panel", st);
        var l = Plain("", 13, Brand.InkDim);
        panel.AddChild(l);
        outer.AddChild(panel);
        _updaters.Add((s, r) =>
        {
            var x = Find(r, key);
            if (x == null) return;
            var left = Math.Max(0, x.CapacityKw - x.LoadKw);
            l.Text = x.FreeOutputs <= 0 ? Loc.T("ptree_no_outputs", Kw(left)) : Loc.T("ptree_free_outputs", x.FreeOutputs, Kw(left), (int)(left / 150));
            l.AddThemeColorOverride("font_color", x.FreeOutputs <= 0 ? Brand.Warning : Brand.InkDim);
        });
        return outer;
    }

    /// <summary>Card of the selected device: key numbers, connections, what feeds it, a tip and actions.</summary>
    Control Card(GameState s, InfraReport r, PowerNode n)
    {
        var card = new PanelContainer();
        card.AddThemeStyleboxOverride("panel", Ui.Box(Brand.PanelSolid, 10, Brand.Line, 1, 14));
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 8);
        card.AddChild(v);
        v.AddChild(Ui.Heading("ptree_selected", 11));
        var title = Plain(NodeName(n) + (n.Obj != null ? " · " + Loc.T(n.Obj.Def.NameKey) : ""), 22, Brand.Ink, false);
        title.AddThemeFontOverride("font", Ui.DisplaySemi);
        v.AddChild(title);
        var g = new GridContainer { Columns = 3 };
        g.AddThemeConstantOverride("h_separation", 28);
        void Fact(string label, string value)
        {
            var c = new VBoxContainer(); c.AddThemeConstantOverride("separation", 0);
            c.AddChild(Ui.Heading(label, 11));
            var l = Plain(value, 19, Brand.Ink, false); l.AddThemeFontOverride("font", Ui.DisplaySemi);
            c.AddChild(l);
            g.AddChild(c);
        }
        if (n.Kind != PowerNodeKind.Mech) Fact("ptree_max", Kw(n.CapacityKw));
        if (n.Kind == PowerNodeKind.Pdu) Fact("ptree_outlets", Loc.T("ptree_outlets_n", n.Outlets));
        if (n.Kind == PowerNodeKind.Busbar) Fact("ptree_outlets", Loc.T("ptree_tapoff"));
        if (n.Kind is PowerNodeKind.Msb or PowerNodeKind.Ups) Fact("ptree_outputs", n.Outputs.ToString());
        if (n.Hall != null) Fact("ptree_serves", s.HallCode(n.Hall));
        else if (n.Kind != PowerNodeKind.Mech) Fact("ptree_side_label", n.Side.ToString());
        v.AddChild(g);
        // one square per outlet (PDU) or output (MSB, UPS): used ones lit amber
        var slots = n.Kind == PowerNodeKind.Pdu ? (n.Outlets, n.UsedOutlets) : n.Kind is PowerNodeKind.Msb or PowerNodeKind.Ups ? (n.Outputs, n.UsedOutputs) : (0, 0);
        if (slots.Item1 > 0)
        {
            v.AddChild(Plain(Loc.T(n.Kind == PowerNodeKind.Pdu ? "ptree_outlets_used" : "ptree_outputs_used", slots.Item2, slots.Item1), 12, Brand.InkDim));
            var f = new HFlowContainer(); f.AddThemeConstantOverride("h_separation", 4); f.AddThemeConstantOverride("v_separation", 4);
            for (var i = 0; i < slots.Item1; i++)
                f.AddChild(new ColorRect { Color = i < slots.Item2 ? Brand.Power : new Color(Brand.InkDim, 0.25f), CustomMinimumSize = new Vector2(16, 16) });
            v.AddChild(f);
        }
        v.AddChild(Plain(Loc.T("ptree_load", Kw(n.LoadKw), Kw(n.FailoverKw)), 13, n.FailoverShort ? Brand.Critical : Brand.Ink));
        var path = new List<string>();
        for (var p = n.Parent; p != null; p = p.Parent) path.Insert(0, NodeName(p));
        if (path.Count > 0) v.AddChild(Plain(Loc.T("ptree_fed_by", string.Join(" › ", path)) + (n.Obj?.Def.FeederAmps > 0 ? " · " + Loc.T("ptree_feeder", n.Obj.Def.FeederAmps) : n.Kind == PowerNodeKind.Busbar ? " · " + Loc.T("ptree_feeder", Catalog.BusbarAmps) : ""), 13, Brand.InkDim));
        foreach (var tip in Tips(s, r, n))
        {
            var box = new PanelContainer();
            box.AddThemeStyleboxOverride("panel", Ui.Box(new Color(Brand.Power, 0.1f), 8, new Color(Brand.Power, 0.35f), 1, 10));
            box.AddChild(Plain(Loc.T("ptree_tip_prefix") + " " + tip, 14, Brand.Ink));
            v.AddChild(box);
        }
        if (n.Obj != null && Catalog.HasFeed(n.Obj.Kind))
        {
            var o = n.Obj;
            var other = n.Side == FeedSide.A ? FeedSide.B : FeedSide.A;
            var b = Ui.Secondary(Loc.T("action_switch_feed", other), () => { var why = _state().SwitchFeed(o); if (why != null) _fail(why); _sig = null; Refresh(); });
            b.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
            b.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
            v.AddChild(b);
        }
        return card;
    }

    /// <summary>
    /// The route to N+1 and 2N (playtest): per link of the chain (generators, transformers, switchboards, UPS) and per
    /// hall the level now and the next concrete step. Updates with the load, the rest of the tree only on a new shape.
    /// </summary>
    Control RouteCard()
    {
        var box = new PanelContainer();
        box.AddThemeStyleboxOverride("panel", Ui.Box(new Color(Brand.Power, 0.06f), 8, new Color(Brand.Power, 0.3f), 1, 12));
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 4);
        box.AddChild(v);
        var head = new HBoxContainer();
        var title = Ui.Heading("ptree_route_title", 13, Brand.Power);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        head.AddChild(title);
        var help = Ui.Secondary(Loc.T("help_open"), () => GetParent()?.AddChild(new HelpPanel(topic: "redundancy")));
        help.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        help.CustomMinimumSize = new Vector2(0, 28);
        head.AddChild(help);
        v.AddChild(head);
        var lines = new VBoxContainer();
        lines.AddThemeConstantOverride("separation", 3);
        v.AddChild(lines);
        string last = null;
        _updaters.Add((s, r) =>
        {
            var route = Route(s, r);
            var sig = string.Join("|", route.Select(x => x.level + x.text));
            if (sig == last) return;
            last = sig;
            foreach (var c in lines.GetChildren()) { lines.RemoveChild(c); c.QueueFree(); }
            if (route.Count == 0) { lines.AddChild(Plain(Loc.T("ptree_route_none"), 13, Brand.InkDim)); return; }
            foreach (var (level, text) in route)
            {
                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 8);
                var chip = new PanelContainer { CustomMinimumSize = new Vector2(52, 0) };
                var col = LevelColor(level);
                chip.AddThemeStyleboxOverride("panel", Ui.Box(new Color(col, 0.2f), 5, null, 0, 4));
                var lv = Plain(level, 12, col, false);
                lv.HorizontalAlignment = HorizontalAlignment.Center;
                chip.AddChild(lv);
                row.AddChild(chip);
                var l = Plain(text, 13, Brand.Ink);
                l.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                row.AddChild(l);
                lines.AddChild(row);
            }
        });
        return box;
    }

    static Color LevelColor(string level) => level switch
    {
        "2N" => Brand.Ok,
        "N+1" or "A+B" => Brand.Power,
        "N-" => Brand.Critical,
        _ => Brand.Warning,
    };

    /// <summary>Per link and per hall with racks: the redundancy level now and what to add for the next level.</summary>
    public static List<(string level, string text)> Route(GameState s, InfraReport r)
    {
        var list = new List<(string, string)>();
        if (r == null || r.ItDemandKw <= 0) return list;
        void Link(string unitKey, string level, IReadOnlyCollection<double> caps, double load)
        {
            if (level == "-" || caps.Count == 0 || load <= 0) return;
            var name = Loc.T(unitKey);
            var sum = caps.Sum();
            var text = level switch
            {
                "N-" => Loc.T("ptree_route_short", name, Kw(load - sum)),
                "N" => Loc.T("ptree_route_n", name),
                "N+1" => Loc.T("ptree_route_n1", name, Kw(Math.Max(1, 2 * load - sum))),
                _ => Loc.T("ptree_route_2n", name),
            };
            list.Add((level, text));
        }
        var gens = s.Objects.Where(o => o.Kind == ObjKind.Generator).Select(o => o.Def.GenKw).ToList();
        if (!r.Ats) list.Add(("N-", Loc.T("ptree_route_no_ats")));
        else Link("ptree_route_gen", r.GenRedundancy, gens, r.ItDemandKw * r.Pue + r.OtherKw);
        foreach (var (link, key) in new[] { (PowerLink.Transformers, "ptree_route_trafo"), (PowerLink.Switchgear, "ptree_route_msb"), (PowerLink.Ups, "ptree_route_ups") })
        {
            var l = r.Links.FirstOrDefault(x => x.Link == link);
            if (l == null || l.Units == 0) continue;
            // the units of a link are all one size in this game: split the capacity evenly
            var caps = Enumerable.Repeat(l.CapacityKw / l.Units, l.Units).ToList();
            Link(key, l.Redundancy, caps, l.LoadKw);
        }
        foreach (var h in r.Halls.Where(h => h.LiveRacks > 0))
        {
            var code = s.HallCode(h.Room);
            var text = h.Redundancy switch
            {
                "2N" => Loc.T("ptree_route_hall_2n", code),
                "A+B" => Loc.T("ptree_route_hall_ab", code),
                "partial" => Loc.T("ptree_route_hall_partial", code, h.DualRacks, h.LiveRacks),
                _ => Loc.T("ptree_route_hall_n", code),
            };
            list.Add((h.Redundancy == "partial" ? "N" : h.Redundancy, text));
        }
        return list;
    }

    /// <summary>What to do about this device, in plain words. Empty when it is fine.</summary>
    public static List<string> Tips(GameState s, InfraReport r, PowerNode n)
    {
        var list = new List<string>();
        var hall = n.Hall != null ? s.HallCode(n.Hall) : "";
        if (!n.Connected)
            list.Add(Loc.T(n.Kind == PowerNodeKind.Ups ? "ptree_tip_ups_unconnected" : "ptree_tip_unconnected"));
        if (n.Kind == PowerNodeKind.Pdu && n.Full)
        {
            var info = r.Halls.FirstOrDefault(h => h.Room == n.Hall);
            list.Add(info != null && info.FedRacks < info.LiveRacks ? Loc.T("ptree_tip_pdu_full_down", hall, info.LiveRacks - info.FedRacks, n.Side) : Loc.T("ptree_tip_pdu_full", hall, n.Side));
        }
        if (n.Direct) list.Add(Loc.T("ptree_tip_direct"));
        if (n.FailoverShort) list.Add(Loc.T("ptree_tip_failover", Kw(n.FailoverKw), Kw(n.CapacityKw)));
        if (n.Overloaded) list.Add(Loc.T("ptree_tip_overload"));
        if (n.Kind is PowerNodeKind.Msb or PowerNodeKind.Ups && n.FreeOutputs <= 0)
            list.Add(Loc.T(n.Kind == PowerNodeKind.Msb ? "ptree_tip_msb_full" : "ptree_tip_ups_full"));
        if (n.Kind is PowerNodeKind.Pdu or PowerNodeKind.Busbar && n.Hall != null)
        {
            var info = r.Halls.FirstOrDefault(h => h.Room == n.Hall);
            if (info != null && info.LiveRacks > 0 && info.Sides < 2) list.Add(Loc.T("ptree_tip_add_b", hall, n.Side == FeedSide.A ? "B" : "A"));
        }
        if (n.Kind == PowerNodeKind.Msb && r.Tree.Msbs.Count == 1 && r.Tree.Sources.Any(x => x.Racks > 0))
            list.Add(Loc.T("ptree_tip_one_msb"));
        return list;
    }
}
