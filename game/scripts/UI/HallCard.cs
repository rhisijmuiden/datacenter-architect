using System;
using System.Linq;
using DatacenterArchitect.Core;
using DatacenterArchitect.Sim;
using Godot;

namespace DatacenterArchitect.UI;

/// <summary>
/// The data hall card in the inspector (infra sketch board 6): what the racks need against what power, network and
/// cooling can deliver, the redundancy level of each, what the hall has and which upgrades are left.
/// Two tabs (Capacity, Upgrades) keep it short; buttons jump to the power and network trees.
/// </summary>
public static class HallCard
{
    public const int TabCapacity = 0, TabUpgrades = 1;
    /// <summary>The open tab, kept while the card rebuilds and between halls.</summary>
    public static int Tab;
    const float Width = 360;

    public static string LevelText(HallLevel l) => l switch
    {
        HallLevel.Short => Loc.T("hall_lvl_short"), HallLevel.N => "N", HallLevel.Partial => Loc.T("hall_lvl_partial"),
        HallLevel.NPlus1 => "N+1", HallLevel.AB => "A+B", HallLevel.TwoN => "2N", _ => "-",
    };

    public static Color LevelColor(HallLevel l) => l switch
    {
        HallLevel.Short => Brand.Critical, HallLevel.N or HallLevel.Partial => Brand.Warning, HallLevel.None => Brand.InkDim, _ => Brand.Ok,
    };

    static string Kw(double v) => $"{v:N0} kW";
    static string Outlets(HallSide s) => s.Unlimited ? "∞" : s.Outlets.ToString();

    /// <summary>Title of an upgrade row and its line underneath.</summary>
    public static (string title, string sub) UpgradeText(GameState s, HallReport r, HallUpgrade u)
    {
        var room = r.Room;
        switch (u.Kind)
        {
            case HallUpgradeKind.Containment:
            {
                var next = Catalog.Containments[room.Containment + 1];
                var now = Catalog.Containments[room.Containment];
                var pueNow = Math.Max(1.08, now.Pue - s.PueSaving); var pueNext = Math.Max(1.08, next.Pue - s.PueSaving);
                return (Loc.T(next.NameKey), Loc.T("hall_up_containment_sub", Math.Round(next.CoolingBonus * 100), Pue(pueNow), Pue(pueNext)));
            }
            case HallUpgradeKind.Busbar:
                return (Loc.T("hall_up_busbar"), Loc.T("hall_up_busbar_sub", Kw(Catalog.BusbarKw)));
            case HallUpgradeKind.Pdu:
            {
                var d = Catalog.Objects[u.Obj];
                var key = r.PowerLevel == HallLevel.Short ? "hall_up_pdu_short" : "hall_up_pdu_ab";
                return (Loc.T("hall_up_place", u.Count, Loc.T(d.NameKey)), Loc.T(key, Kw(d.CapacityKw), d.Outlets));
            }
            case HallUpgradeKind.EdgeSwitch:
            {
                var d = Catalog.Objects[u.Obj];
                return (Loc.T("hall_up_place", u.Count, Loc.T(d.NameKey)), Loc.T(r.NetLevel == HallLevel.Short ? "hall_up_edge_short" : "hall_up_edge_n1", d.Ports));
            }
            default:
                return (Loc.T("hall_up_place", u.Count, "CRAH"), Loc.T(r.CoolLevel == HallLevel.Short ? "hall_up_crah_short" : "hall_up_crah_n1", Kw(r.UnitKw)));
        }
    }

    static string Pue(double v) => v.ToString(Loc.Language == "nl" ? "0.00" : "0.00", Loc.Language == "nl" ? new System.Globalization.CultureInfo("nl-NL") : System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Build the card. <paramref name="apply"/> runs an upgrade (or picks the tool for a device), <paramref name="refresh"/> redraws after a tab change.</summary>
    public static Control Build(GameState s, HallReport r, Action<HallUpgrade> apply, Action openPower, Action openNet, Action refresh)
    {
        var v = new VBoxContainer { CustomMinimumSize = new Vector2(Width, 0) };
        v.AddThemeConstantOverride("separation", 8);

        // ---- demand and the verdict ----
        v.AddChild(Dim(Loc.T("hall_need", r.Racks, Kw(r.NeedKw)), 14, Brand.Ink));
        if (r.Racks > 0)
        {
            var ok = r.AllOnline;
            var verdict = ok ? Loc.T("hall_all_online", r.Racks) : Loc.T("hall_not_online", r.Online, r.Racks);
            v.AddChild(Banner(verdict, ok ? Brand.Ok : Brand.Critical));
        }
        if (r.Room.Upgrade != 0)
            v.AddChild(Dim("⟳ " + Loc.T(r.Room.Upgrade == 2 ? "hall_busy_busbar" : "hall_busy_containment", Math.Ceiling(r.Room.UpgradeLeft)), 13, Brand.Amber));

        // ---- tabs ----
        var tabs = new HBoxContainer();
        tabs.AddThemeConstantOverride("separation", 6);
        var count = r.Upgrades.Count;
        tabs.AddChild(TabButton(Loc.T("hall_tab_capacity"), Tab == TabCapacity, () => { Tab = TabCapacity; refresh(); }));
        tabs.AddChild(TabButton(Loc.T("hall_tab_upgrades") + (count > 0 ? $" ({count})" : ""), Tab == TabUpgrades, () => { Tab = TabUpgrades; refresh(); }));
        tabs.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        tabs.AddChild(LinkButton("pdu", Loc.T("nav_tab_power_tree"), openPower));
        tabs.AddChild(LinkButton("edge_switch", Loc.T("ntree_open"), openNet));
        v.AddChild(tabs);

        if (Tab == TabCapacity) Capacity(v, s, r);
        else Upgrades(v, s, r, apply);
        return v;
    }

    static void Capacity(VBoxContainer v, GameState s, HallReport r)
    {
        // ---- power ----
        var p = Card(v, "hall_power", Brand.Power, r.PowerLevel, "hall_goal_ab");
        foreach (var side in new[] { r.A, r.B })
        {
            var what = side.Units == 0 ? Loc.T("hall_side_none")
                : side.Busbar ? Loc.T("hall_side_busbar", side.Units) : Loc.T("hall_side_pdus", side.Units);
            var cap = side.Units == 0 ? Loc.T("hall_no_feed") : $"{Kw(side.CapacityKw)} / {Kw(r.NeedKw)}";
            var covers = side.Units > 0 && side.CapacityKw + 1e-6 >= r.NeedKw && (side.Unlimited || side.Outlets >= r.Racks);
            Row(p, Loc.T("hall_side", side.Side) + " · " + what, cap, side.Units == 0 || !covers ? Brand.Critical : null);
            Cover(p, r.NeedKw <= 0 ? (side.Units > 0 ? 1 : 0) : side.CapacityKw / r.NeedKw, covers || r.Racks == 0, Brand.Power);
            p.AddChild(Dim(Loc.T("hall_outlets", side.UsedOutlets, Outlets(side)) + (side.Units > 0 ? " · " + Loc.T("hall_side_need", Kw(r.NeedKw), r.Racks) : ""), 12));
        }
        if (r.UnfedSources > 0) p.AddChild(Dim("⚠ " + Loc.T("hall_unfed", r.UnfedSources), 12, Brand.Critical));
        if (r.PowerLevel == HallLevel.AB) p.AddChild(Dim(Loc.T("hall_tip_2n"), 12));

        // ---- network ----
        var n = Card(v, "hall_network", Brand.Fiber, r.NetLevel, "hall_goal_n1");
        Row(n, r.Switches == 0 ? Loc.T("hall_no_switch") : Loc.T("hall_switches", r.Switches), Loc.T("hall_ports", Math.Min(r.Racks, r.Ports), r.Ports), r.Switches == 0 ? Brand.Critical : null);
        Bar(n, r.Ports <= 0 ? (r.Racks > 0 ? 1.01 : 0) : (double)r.Racks / r.Ports, Brand.Fiber);
        n.AddChild(Dim(Loc.T("hall_gbps", $"{r.Gbps:0.#}", $"{r.UplinkGbps:0}") + (r.Dual > 0 ? " · " + Loc.T("hall_dual", r.Dual) : ""), 12));
        if (r.Switches > r.Linked) n.AddChild(Dim("⚠ " + Loc.T("hall_unlinked", r.Switches - r.Linked), 12, Brand.Critical));

        // ---- cooling ----
        var c = Card(v, "hall_cooling", Brand.Cooling, r.CoolLevel, "hall_goal_n1");
        var cont = Catalog.Containments[r.Room.Containment];
        var units = r.Crahs == 0 ? Loc.T("hall_no_crah") : Loc.T("hall_crahs", r.Crahs, Kw(r.UnitKw)) + (cont.CoolingBonus > 0 ? $" · +{cont.CoolingBonus * 100:0}%" : "");
        var cooled = r.CoolKw + 1e-6 >= r.NeedKw;
        Row(c, units, $"{Kw(r.CoolKw)} / {Kw(r.NeedKw)}", cooled ? null : Brand.Critical);
        Cover(c, r.NeedKw <= 0 ? (r.CoolKw > 0 ? 1 : 0) : r.CoolKw / r.NeedKw, cooled, Brand.Cooling);
        c.AddChild(Dim(Loc.T("hall_temp", $"{r.Temp:0}", Loc.T(cont.NameKey), Pue(r.Pue)), 12, r.Room.Hot ? Brand.Critical : null));
        if (r.BrokenCrahs > 0) c.AddChild(Dim("⚠ " + Loc.T("hall_crah_broken", r.BrokenCrahs), 12, Brand.Critical));
        if (r.RejectLimited) c.AddChild(Dim("⚠ " + Loc.T("hall_reject_short", Kw(r.CrahKw), Kw(r.CoolKw)), 12, Brand.Critical));
        if (r.NeedKw > 0 && r.CoolLevel < HallLevel.NPlus1) c.AddChild(Dim(Loc.T("hall_crah_n1", r.CrahsForNPlus1(s)), 12));

        // ---- room left and what the hall has ----
        if (r.Racks > 0 || r.Headroom > 0)
            v.AddChild(Dim(Loc.T("hall_headroom", r.Headroom, Loc.T("hall_by_" + r.HeadroomBy)), 13, r.Headroom == 0 ? Brand.Warning : Brand.InkDim));
        var feats = r.Features();
        if (feats.Count > 0)
        {
            v.AddChild(Ui.Heading("hall_has", 12, Brand.InkDim));
            var flow = new HFlowContainer();
            flow.AddThemeConstantOverride("h_separation", 6); flow.AddThemeConstantOverride("v_separation", 6);
            foreach (var f in feats) flow.AddChild(TreeRow.Chip(Loc.T(f), new Color(Brand.Ok, 0.16f), Brand.Ok));
            v.AddChild(flow);
        }
    }

    static void Upgrades(VBoxContainer v, GameState s, HallReport r, Action<HallUpgrade> apply)
    {
        if (r.Upgrades.Count == 0) { v.AddChild(Dim(Loc.T("hall_up_none"), 13)); return; }
        foreach (var u in r.Upgrades)
        {
            var (title, sub) = UpgradeText(s, r, u);
            var box = new PanelContainer();
            var urgent = u.Priority <= 2;
            box.AddThemeStyleboxOverride("panel", Ui.Box(Brand.PanelSolid, 8, urgent ? new Color(Brand.Critical, 0.5f) : Brand.Line, 1, 10));
            var h = new HBoxContainer(); h.AddThemeConstantOverride("separation", 10);
            box.AddChild(h);
            var text = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            text.AddThemeConstantOverride("separation", 0);
            var t = Ui.Text(title, 14, Brand.Ink, Ui.BodySemi); t.AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled;
            t.AutowrapMode = TextServer.AutowrapMode.WordSmart; t.CustomMinimumSize = new Vector2(230, 0);
            text.AddChild(t);
            text.AddChild(Dim(sub, 12, null, 230));
            var cost = Ui.Money(u.Cost) + (u.Hours > 0 ? " · " + Loc.T("hall_hours", u.Hours) : "");
            text.AddChild(Dim(cost, 12, Brand.Ink, 230));
            if (u.Blocked != null && u.Blocked != "msg_no_money") text.AddChild(Dim(Loc.T(u.Blocked), 12, Brand.Warning, 230));
            h.AddChild(text);
            var place = u.Obj != default && u.Kind is HallUpgradeKind.Pdu or HallUpgradeKind.EdgeSwitch or HallUpgradeKind.Crah;
            var target = u;
            var b = place ? Ui.Secondary(Loc.T("hall_btn_place"), () => apply(target)) : Ui.Primary(Loc.T("hall_btn_upgrade"), () => apply(target));
            b.AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled;
            b.AddThemeFontSizeOverride("font_size", 13);
            b.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            b.Disabled = u.Blocked != null;
            h.AddChild(b);
            v.AddChild(box);
        }
    }

    // ---------------- pieces ----------------
    static Label Dim(string text, int size, Color? c = null, float width = Width - 40)
    {
        var l = Ui.Text(text, size, c ?? Brand.InkDim);
        l.AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled;
        l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        l.CustomMinimumSize = new Vector2(width, 0);
        return l;
    }

    static Control Banner(string text, Color c)
    {
        var p = new PanelContainer();
        p.AddThemeStyleboxOverride("panel", Ui.Box(new Color(c, 0.14f), 8, new Color(c, 0.45f), 1, 8));
        var l = Dim((c == Brand.Ok ? "✓ " : "⚠ ") + text, 13, c.Lightened(0.15f));
        l.CustomMinimumSize = new Vector2(Width - 24, 0);
        p.AddChild(l);
        return p;
    }

    static Button TabButton(string text, bool on, Action pressed)
    {
        var b = new Button { Text = text, AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled, CustomMinimumSize = new Vector2(0, 30), FocusMode = Control.FocusModeEnum.None };
        b.AddThemeFontSizeOverride("font_size", 13);
        var bg = on ? Brand.Cooling : new Color("#1D3247");
        b.AddThemeStyleboxOverride("normal", Ui.Box(bg, 6, null, 0, 8));
        b.AddThemeStyleboxOverride("hover", Ui.Box(on ? bg : Brand.PanelHover, 6, null, 0, 8));
        b.AddThemeStyleboxOverride("pressed", Ui.Box(bg, 6, null, 0, 8));
        b.AddThemeColorOverride("font_color", on ? new Color("#062029") : Brand.InkDim);
        b.AddThemeColorOverride("font_hover_color", on ? new Color("#062029") : Brand.Ink);
        b.Pressed += () => pressed();
        return b;
    }

    static Button LinkButton(string icon, string tip, Action pressed)
    {
        var b = new Button
        {
            Icon = GD.Load<Texture2D>($"res://assets/icons/{icon}.svg"), ExpandIcon = true, TooltipText = tip, AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled,
            CustomMinimumSize = new Vector2(30, 30), FocusMode = Control.FocusModeEnum.None,
        };
        b.AddThemeStyleboxOverride("normal", Ui.Box(new Color("#1D3247"), 6, null, 0, 6));
        b.AddThemeStyleboxOverride("hover", Ui.Box(Brand.PanelHover, 6, Brand.Line, 1, 6));
        b.Pressed += () => pressed();
        return b;
    }

    /// <summary>A system card: coloured label, redundancy chip and the goal next to it.</summary>
    static VBoxContainer Card(VBoxContainer parent, string key, Color accent, HallLevel level, string goalKey)
    {
        var box = TreeRows.Card(parent);
        var head = new HBoxContainer(); head.AddThemeConstantOverride("separation", 8);
        head.AddChild(Ui.Heading(key, 12, accent));
        head.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        var col = LevelColor(level);
        head.AddChild(TreeRow.Chip(LevelText(level), new Color(col, 0.2f), col.Lightened(0.2f)));
        var goal = Ui.Text(Loc.T(goalKey), 12, Brand.InkDim); goal.AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled; goal.AutowrapMode = TextServer.AutowrapMode.Off;
        head.AddChild(goal);
        box.AddChild(head);
        return box;
    }

    static void Row(VBoxContainer parent, string left, string right, Color? rightColor = null)
    {
        var h = new HBoxContainer(); h.AddThemeConstantOverride("separation", 8);
        var l = Ui.Text(left, 13, Brand.Ink); l.AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled;
        l.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; l.AutowrapMode = TextServer.AutowrapMode.Off; l.ClipText = true; l.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        h.AddChild(l);
        var rt = Ui.Text(right, 14, rightColor ?? Brand.Ink, Ui.DisplaySemi); rt.AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled; rt.AutowrapMode = TextServer.AutowrapMode.Off;
        h.AddChild(rt);
        parent.AddChild(h);
    }

    /// <summary>How much of the need a system covers: full in the system colour, short in red.</summary>
    static void Cover(VBoxContainer parent, double ratio, bool ok, Color accent)
    {
        var bar = new ProgressBar { MinValue = 0, MaxValue = 1, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 8), Value = Math.Min(1, ratio), MouseFilter = Control.MouseFilterEnum.Ignore };
        bar.AddThemeStyleboxOverride("background", Ui.Box(new Color("#96BEDC", 0.14f), 4, null, 0, 0));
        bar.AddThemeStyleboxOverride("fill", Ui.Box(ok ? accent : Brand.Critical, 4, null, 0, 0));
        parent.AddChild(bar);
    }

    static void Bar(VBoxContainer parent, double ratio, Color accent)
    {
        var bar = new ProgressBar { MinValue = 0, MaxValue = 1, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 8), Value = Math.Min(1, ratio), MouseFilter = Control.MouseFilterEnum.Ignore };
        bar.AddThemeStyleboxOverride("background", Ui.Box(new Color("#96BEDC", 0.14f), 4, null, 0, 0));
        var col = ratio > 1 + 1e-9 ? Brand.Critical : ratio > 0.85 ? Brand.Warning : accent;
        bar.AddThemeStyleboxOverride("fill", Ui.Box(col, 4, null, 0, 0));
        parent.AddChild(bar);
    }
}
