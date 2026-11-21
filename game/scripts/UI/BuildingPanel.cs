using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DatacenterArchitect.Core;
using DatacenterArchitect.Sim;
using Godot;

namespace DatacenterArchitect.UI;

/// <summary>
/// Building management (infra sketch board 9): every data hall in one table with power, distribution, network,
/// bandwidth, cooling, containment, PUE and customer, plus totals on top. Tick halls and bring them level with the
/// same upgrades in one go; what a hall already has is skipped.
/// </summary>
public partial class BuildingPanel : Modal
{
    public enum Filter { All, NoN1, NoBusbar, Hot, NotOnline }

    readonly Func<GameState> _state;
    readonly Action<string> _fail;
    readonly Action<Room> _show;
    readonly Func<LevelUpPlan, LevelUpResult> _apply;
    readonly VBoxContainer _content;
    string _sig;
    double _timer;

    /// <summary>Kept while the screen rebuilds and between openings.</summary>
    static readonly HashSet<int> Selected = new();
    static readonly HashSet<LevelUpStep> Steps = new() { LevelUpStep.Busbar, LevelUpStep.Containment, LevelUpStep.CoolN1 };
    static Filter _filter;

    /// <summary>Tick these halls (screenshots, or a jump from elsewhere).</summary>
    public static void Select(IEnumerable<int> ids) { foreach (var id in ids) Selected.Add(id); }

    static readonly LevelUpStep[] StepOrder = { LevelUpStep.Busbar, LevelUpStep.Containment, LevelUpStep.PowerAB, LevelUpStep.NetN1, LevelUpStep.CoolN1 };
    const float W = 1160;
    static readonly float[] Cols = { 28, 150, 70, 160, 120, 150, 150, 110, 50, 120 };

    static CultureInfo Culture => Loc.Language == "nl" ? new CultureInfo("nl-NL") : new CultureInfo("en-GB");
    static string N(double v, string f = "N0") => v.ToString(f, Culture);
    static string Kw(double v) => v >= 10000 ? N(v / 1000, "0.0") + " MW" : N(v) + " kW";
    static string Pue(double v) => v <= 0 ? "-" : N(v, "0.00");

    public BuildingPanel(Func<GameState> state, Action<string> fail, Action<Room> show, Func<LevelUpPlan, LevelUpResult> apply) : base("bms_title", W)
    {
        _state = state; _fail = fail; _show = show; _apply = apply;
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 640), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _content.AddThemeConstantOverride("separation", 12);
        scroll.AddChild(_content);
        Body.AddChild(scroll);
        FillHeight(scroll);
        Refresh();
    }

    public override void _Process(double delta)
    {
        _timer -= delta;
        if (_timer > 0) return;
        _timer = 1;
        Refresh();
    }

    void Refresh()
    {
        var s = _state();
        if (s == null) return;
        var halls = s.Rooms.Where(r => r.Type == RoomType.DataHall).ToList();
        var reports = halls.Where(h => h.Stage == Stage.Done).ToDictionary(h => h, h => HallReport.Build(s, h));
        var sig = string.Join("|", halls.Select(h => h.Id + ":" + h.Stage + ":" + (reports.TryGetValue(h, out var r) ? r.Sig() + ":" + string.Join("", h.UpgradeQueue) : Math.Round(h.Work))))
            + Loc.Language + _filter + string.Join(",", Selected.OrderBy(x => x)) + string.Join(",", Steps.OrderBy(x => x)) + s.Money / 100000;
        if (sig == _sig) return;
        _sig = sig;
        Build(s, halls, reports);
    }

    void Rebuild() { _sig = null; Refresh(); }

    static bool Matches(Filter f, Room h, HallReport r) => f switch
    {
        Filter.NoN1 => r != null && r.Racks > 0 && (r.PowerLevel < HallLevel.NPlus1 || r.NetLevel < HallLevel.NPlus1 || r.CoolLevel < HallLevel.NPlus1),
        Filter.NoBusbar => r != null && !h.Busbar,
        Filter.Hot => r != null && (h.Hot || r.CoolLevel == HallLevel.Short),
        Filter.NotOnline => r != null && r.Racks > 0 && !r.AllOnline,
        _ => true,
    };

    void Build(GameState s, List<Room> halls, Dictionary<Room, HallReport> reports)
    {
        foreach (var c in _content.GetChildren()) c.QueueFree();
        Selected.RemoveWhere(id => !halls.Any(h => h.Id == id && h.Stage == Stage.Done));
        _content.AddChild(Dim(Loc.T("bms_intro"), 13));
        if (halls.Count == 0) { _content.AddChild(Dim(Loc.T("halls_none"), 14)); return; }

        // ---- filters ----
        var filters = new HFlowContainer();
        filters.AddThemeConstantOverride("h_separation", 6);
        foreach (var f in Enum.GetValues<Filter>())
        {
            var n = halls.Count(h => Matches(f, h, reports.GetValueOrDefault(h)));
            var ff = f;
            filters.AddChild(Pill(Loc.T("bms_filter_" + f.ToString().ToLowerInvariant(), n), _filter == f, () => { _filter = ff; Rebuild(); }));
        }
        _content.AddChild(filters);

        // ---- totals ----
        var live = reports.Values.Where(r => r.Racks > 0).ToList();
        var tiles = new HBoxContainer(); tiles.AddThemeConstantOverride("separation", 10);
        var it = reports.Values.Sum(r => r.Power?.ItKw ?? 0);
        tiles.AddChild(Tile("bms_tile_it", Kw(it), Loc.T("bms_tile_it_sub", Kw(reports.Values.Sum(r => r.NeedKw))), Brand.Power));
        tiles.AddChild(Tile("bms_tile_ab", $"{live.Count(r => r.PowerLevel >= HallLevel.AB)} / {live.Count}", Loc.T("bms_tile_ab_sub"), Brand.Power));
        tiles.AddChild(Tile("bms_tile_net", $"{live.Count(r => r.NetLevel >= HallLevel.NPlus1)} / {live.Count}",
            Loc.T("bms_tile_net_sub", N(reports.Values.Sum(r => r.Gbps), "N0"), N(reports.Values.Sum(r => r.UplinkGbps), "N0")), Brand.Fiber));
        var hot = reports.Values.Where(r => r.Room.Hot || r.CoolLevel == HallLevel.Short).ToList();
        tiles.AddChild(Tile("bms_tile_cool", $"{live.Count(r => r.CoolLevel >= HallLevel.NPlus1)} / {live.Count}",
            hot.Count > 0 ? Loc.T("bms_tile_hot", hot.Count, N(hot.Max(r => r.Temp), "N0")) : Loc.T("bms_tile_cool_sub"), Brand.Cooling, hot.Count > 0));
        var kw = live.Sum(r => r.NeedKw);
        var pue = kw > 0 ? live.Sum(r => r.Pue * r.NeedKw) / kw : 0;
        var best = kw > 0 && s.Done("cool_corridor") ? live.Sum(r => Math.Max(1.08, Catalog.Containments[Containment.ColdCorridor].Pue - s.PueSaving) * r.NeedKw) / kw : 0;
        tiles.AddChild(Tile("bms_tile_pue", Pue(pue), best > 0 && best < pue - 0.005 ? Loc.T("bms_tile_pue_sub", Pue(best)) : Loc.T("bms_tile_pue_none"), Brand.Ok));
        _content.AddChild(tiles);

        // ---- table ----
        var shown = halls.Where(h => Matches(_filter, h, reports.GetValueOrDefault(h))).ToList();
        var table = new PanelContainer();
        table.AddThemeStyleboxOverride("panel", Ui.Box(new Color("#0F1C29"), 10, Brand.Line, 1, 0));
        var rows = new VBoxContainer(); rows.AddThemeConstantOverride("separation", 0);
        table.AddChild(rows);
        _content.AddChild(table);
        var head = Row(rows, false, false);
        var selectable = shown.Where(h => h.Stage == Stage.Done).ToList();
        var allOn = selectable.Count > 0 && selectable.All(h => Selected.Contains(h.Id));
        var all = Tick(null, allOn, () => { foreach (var h in selectable) { if (!allOn) Selected.Add(h.Id); else Selected.Remove(h.Id); } Rebuild(); });
        all.TooltipText = Loc.T("bms_select_all"); all.Disabled = selectable.Count == 0;
        Cell(head, 0).AddChild(all);
        var heads = new[] { "bms_col_hall", "bms_col_racks", "hall_power", "bms_col_dist", "hall_network", "hall_cooling", "bms_col_air", "bms_col_pue", "bms_col_customer" };
        for (var i = 0; i < heads.Length; i++) Cell(head, i + 1).AddChild(Ui.Heading(heads[i], 11));
        if (shown.Count == 0) rows.AddChild(Ui.Pad(Dim(Loc.T("bms_filter_empty"), 13), 16, 12, 16, 12));
        for (var i = 0; i < shown.Count; i++) HallRow(rows, s, shown[i], reports.GetValueOrDefault(shown[i]), i == shown.Count - 1);

        // ---- level up the ticked halls ----
        LevelBar(s, halls.Where(h => Selected.Contains(h.Id)).ToList());
    }

    void HallRow(VBoxContainer rows, GameState s, Room h, HallReport r, bool last)
    {
        var picked = Selected.Contains(h.Id);
        var row = Row(rows, picked, !last);
        var box = Tick(null, picked, () => { if (!Selected.Remove(h.Id)) Selected.Add(h.Id); Rebuild(); });
        box.Disabled = r == null; box.TooltipText = s.HallCode(h);
        Cell(row, 0).AddChild(box);

        // hall: code, name or size, floor; click to fly there
        var name = new Button { Flat = true, Alignment = HorizontalAlignment.Left, TooltipText = Loc.T("bms_show_hall"), FocusMode = FocusModeEnum.None,
            Text = s.HallCode(h), AutoTranslateMode = AutoTranslateModeEnum.Disabled };
        name.AddThemeFontOverride("font", Ui.DisplaySemi); name.AddThemeFontSizeOverride("font_size", 17);
        name.Pressed += () => _show(h);
        var hc = Cell(row, 1);
        var line = new HBoxContainer(); line.AddThemeConstantOverride("separation", 8);
        line.AddChild(new ColorRect { Color = new Color(GameState.HallBand(h)), CustomMinimumSize = new Vector2(5, 26), MouseFilter = MouseFilterEnum.Ignore });
        line.AddChild(name);
        hc.AddChild(line);
        hc.AddChild(Dim((string.IsNullOrWhiteSpace(h.Name) ? $"{h.Area} m²" : h.Name) + (h.Floor != 0 ? " · " + Ui.FloorName(h.Floor) : ""), 12));
        if (h.Upgrade != 0)
            hc.AddChild(Dim("⟳ " + Loc.T("bms_busy", Math.Ceiling(GameState.UpgradeHoursLeft(h)), 1 + h.UpgradeQueue.Count), 12, Brand.Amber));

        if (r == null)
        {
            Cell(row, 2).AddChild(Chip(Loc.T("bms_building"), HallLevel.None));
            Cell(row, 3).AddChild(Dim(Loc.T("bms_building_sub"), 12));
            return;
        }
        // racks online of placed
        var rc = Cell(row, 2);
        rc.AddChild(Big($"{r.Online} / {r.Racks}"));
        if (r.Headroom > 0 && r.Racks > 0) rc.AddChild(Dim(Loc.T("bms_room_for", r.Headroom), 11));

        // power: level, need against what both sides deliver
        var cap = r.A.CapacityKw + r.B.CapacityKw;
        System(Cell(row, 3), r.PowerLevel, r.Racks == 0 ? "-" : $"{N(r.NeedKw)} / {N(cap)} kW", cap > 0 ? r.NeedKw / cap : r.NeedKw > 0 ? 2 : 0, Brand.Power);

        // distribution: PDUs per size or busbar
        var dist = Cell(row, 4);
        dist.AddChild(Chip(Distribution(s, h), HallLevel.None));
        if (r.UnfedSources > 0) dist.AddChild(Dim("⚠ " + Loc.T("bms_unfed", r.UnfedSources), 11, Brand.Warning));

        // network: level and bandwidth now against the uplink
        System(Cell(row, 5), r.NetLevel, r.Racks == 0 ? "-" : $"{N(r.Gbps, "N0")} / {N(r.UplinkGbps, "N0")} G", r.UplinkGbps > 0 ? r.Gbps / r.UplinkGbps : 0, Brand.Fiber,
            r.Racks > 0 ? Loc.T("hall_ports", Math.Min(r.Ports, r.Racks), r.Ports) : null);

        // cooling: level, temperature, load against what the hall can cool
        System(Cell(row, 6), r.CoolLevel, $"{N(r.Temp, "N0")} °C", r.CoolKw > 0 ? r.NeedKw / r.CoolKw : r.NeedKw > 0 ? 2 : 0, Brand.Cooling,
            r.Crahs > 0 ? Loc.T("hall_crahs", r.Crahs, Kw(r.UnitKw)) : null);

        // aisles
        var air = h.Containment == Containment.ColdCorridor ? HallLevel.NPlus1 : h.Containment == Containment.HotCold ? HallLevel.N : HallLevel.Short;
        Cell(row, 7).AddChild(Chip(Loc.T(Catalog.Containments[h.Containment].NameKey), h.Containment == Containment.Open ? HallLevel.None : air));
        Cell(row, 8).AddChild(Big(Pue(r.Pue)));

        // customer: the tenant of a whole hall, else what the hall is sold as and how many customers it has
        var cu = Cell(row, 9);
        var tenant = s.HallTenant(h);
        if (tenant != null) cu.AddChild(Dim(CompanyPanel.DealName(tenant), 12, Brand.Ok));
        else
        {
            cu.AddChild(Dim(Loc.T("zone_" + h.Zone.ToString().ToLowerInvariant()), 12, Brand.Ink));
            var n = s.Deals.Count(d => d.State == DealState.Active && s.DealHalls(d).Contains(h));
            cu.AddChild(Dim(n > 0 ? Loc.T("bms_customers", n) : CompanyPanel.ClientsText(h), 11));
        }
    }

    /// <summary>How the hall is fed: "Busbar A+B", or the PDUs per size ("4 × PDU 150").</summary>
    static string Distribution(GameState s, Room h)
    {
        var parts = new List<string>();
        if (h.Busbar) parts.Add(Loc.T("hall_feat_busbar"));
        foreach (var g in s.Objects.Where(o => Catalog.IsPdu(o.Kind) && o.Floor == h.Floor && h.Contains(o.X, o.Z)).GroupBy(o => o.Kind).OrderBy(g => Catalog.Objects[g.Key].CapacityKw))
            parts.Add($"{g.Count()} × {Loc.T(Catalog.Objects[g.Key].NameKey)}");
        return parts.Count == 0 ? Loc.T("hall_no_feed") : string.Join(" + ", parts);
    }

    void LevelBar(GameState s, List<Room> picked)
    {
        var p = new PanelContainer();
        p.AddThemeStyleboxOverride("panel", Ui.Box(Brand.PanelSolid, 10, picked.Count > 0 ? Brand.Cooling : Brand.Line, picked.Count > 0 ? 2 : 1, 14));
        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 8);
        p.AddChild(v);
        _content.AddChild(p);
        var top = new HBoxContainer(); top.AddThemeConstantOverride("separation", 16);
        v.AddChild(top);
        var title = Ui.Text(picked.Count == 0 ? Loc.T("bms_pick_halls") : Loc.T("bms_picked", picked.Count), 20, Brand.Ink, Ui.DisplaySemi);
        title.AutoTranslateMode = AutoTranslateModeEnum.Disabled; title.AutowrapMode = TextServer.AutowrapMode.Off;
        top.AddChild(title);
        var steps = new HFlowContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        steps.AddThemeConstantOverride("h_separation", 14);
        foreach (var st in StepOrder)
        {
            var step = st;
            steps.AddChild(Tick(Loc.T("bms_step_" + st.ToString().ToLowerInvariant()), Steps.Contains(st), () => { if (!Steps.Remove(step)) Steps.Add(step); Rebuild(); }));
        }
        top.AddChild(steps);
        var plan = s.PlanLevelUp(picked, Steps);
        var sum = new VBoxContainer();
        var cost = Ui.Text(Ui.Money(plan.Cost), 20, s.CanAfford(plan.Cost) ? Brand.Ink : Brand.Critical, Ui.DisplayBold);
        cost.AutoTranslateMode = AutoTranslateModeEnum.Disabled; cost.HorizontalAlignment = HorizontalAlignment.Right;
        sum.AddChild(cost);
        var sub = plan.Hours > 0 ? Loc.T("hall_hours", N(Math.Ceiling(plan.Hours))) : "";
        if (plan.PueAfter > 0 && plan.PueAfter < plan.PueBefore - 0.005) sub += (sub.Length > 0 ? " · " : "") + $"PUE {Pue(plan.PueBefore)} → {Pue(plan.PueAfter)}";
        var sl = Dim(sub, 12); sl.HorizontalAlignment = HorizontalAlignment.Right; sl.CustomMinimumSize = new Vector2(140, 0);
        sum.AddChild(sl);
        top.AddChild(sum);
        var go = Ui.Primary("bms_level_up", () =>
        {
            var st = _state();
            var res = _apply(st.PlanLevelUp(st.Rooms.Where(h => Selected.Contains(h.Id)).ToList(), Steps));
            if (res.Error != null) _fail(res.Error);
            Rebuild();
        });
        go.Disabled = plan.Actions.Count == 0 || !s.CanAfford(plan.Cost);
        if (plan.Actions.Count > 0 && !s.CanAfford(plan.Cost)) go.TooltipText = Loc.T("msg_no_money");
        top.AddChild(go);

        // what happens per hall, and what is skipped
        if (picked.Count == 0) { v.AddChild(Dim(Loc.T("bms_help"), 12)); return; }
        foreach (var h in picked)
        {
            var acts = plan.Actions.Where(a => a.Hall == h).Select(a => ActionText(a)).ToList();
            var skips = plan.Skipped.Where(x => x.Hall == h && x.Why != "bms_skip_has").Select(x => Loc.T(x.Why, Loc.T("bms_step_" + x.Step.ToString().ToLowerInvariant()))).ToList();
            var line = s.HallCode(h) + ": " + (acts.Count == 0 ? Loc.T("bms_nothing") : string.Join(", ", acts));
            if (skips.Count > 0) line += " · " + string.Join(" · ", skips);
            v.AddChild(Dim(line, 12, acts.Count == 0 ? Brand.InkDim : Brand.Ink));
        }
        v.AddChild(Dim(Loc.T("bms_skip_note"), 11));
    }

    static string ActionText(LevelUpAction a) => a.Step switch
    {
        LevelUpStep.Busbar => Loc.T("hall_up_busbar"),
        LevelUpStep.Containment => Loc.T(Catalog.Containments[a.Level].NameKey),
        _ => $"{a.Count} × {(a.Obj == ObjKind.Crah ? "CRAH" : Loc.T(Catalog.Objects[a.Obj].NameKey))}",
    };

    // ---------------- pieces ----------------
    static HBoxContainer Row(VBoxContainer rows, bool picked, bool line)
    {
        var p = new PanelContainer();
        var sb = Ui.Box(picked ? new Color(Brand.Cooling, 0.07f) : new Color(0, 0, 0, 0), 0, null, 0, 0);
        sb.ContentMarginLeft = sb.ContentMarginRight = 14; sb.ContentMarginTop = sb.ContentMarginBottom = 9;
        if (line) { sb.BorderWidthBottom = 1; sb.BorderColor = new Color("#96BEDC", 0.12f); }
        p.AddThemeStyleboxOverride("panel", sb);
        var h = new HBoxContainer(); h.AddThemeConstantOverride("separation", 10);
        for (var i = 0; i < Cols.Length; i++)
        {
            var c = new VBoxContainer { CustomMinimumSize = new Vector2(Cols[i], 0), SizeFlagsVertical = SizeFlags.ShrinkCenter };
            c.AddThemeConstantOverride("separation", 3);
            if (i == Cols.Length - 1) c.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            h.AddChild(c);
        }
        p.AddChild(h);
        rows.AddChild(p);
        return h;
    }

    static VBoxContainer Cell(HBoxContainer row, int i) => (VBoxContainer)row.GetChild(i);

    static void System(VBoxContainer c, HallLevel level, string value, double ratio, Color accent, string sub = null)
    {
        var top = new HBoxContainer(); top.AddThemeConstantOverride("separation", 6);
        top.AddChild(Chip(HallCard.LevelText(level), level));
        var v = Ui.Text(value, 14, Brand.Ink, Ui.DisplaySemi); v.AutoTranslateMode = AutoTranslateModeEnum.Disabled; v.AutowrapMode = TextServer.AutowrapMode.Off;
        top.AddChild(v);
        c.AddChild(top);
        var bar = new ProgressBar { MinValue = 0, MaxValue = 1, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 7), Value = Math.Min(1, ratio), MouseFilter = MouseFilterEnum.Ignore };
        bar.AddThemeStyleboxOverride("background", Ui.Box(new Color("#96BEDC", 0.14f), 4, null, 0, 0));
        bar.AddThemeStyleboxOverride("fill", Ui.Box(ratio > 1 + 1e-9 ? Brand.Critical : accent, 4, null, 0, 0));
        c.AddChild(bar);
        if (sub != null) c.AddChild(Dim(sub, 11));
    }

    static Control Chip(string text, HallLevel level)
    {
        var col = HallCard.LevelColor(level);
        return level == HallLevel.None ? TreeRow.Chip(text, new Color("#1D3247"), Brand.InkDim) : TreeRow.Chip(text, new Color(col, 0.18f), col.Lightened(0.2f));
    }

    static Control Tile(string key, string value, string sub, Color accent, bool warn = false)
    {
        var p = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        p.AddThemeStyleboxOverride("panel", Ui.Box(Brand.PanelSolid, 10, warn ? new Color(Brand.Critical, 0.6f) : Brand.Line, 1, 12));
        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 2);
        v.AddChild(Ui.Heading(key, 11, accent));
        var n = Ui.Text(value, 26, Brand.Ink, Ui.DisplayBold); n.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        v.AddChild(n);
        v.AddChild(Dim(sub, 12, warn ? Brand.Critical.Lightened(0.2f) : null));
        p.AddChild(v);
        return p;
    }

    /// <summary>A tick box that reads well on the dark panel: an outlined square, cyan with a check mark when on.</summary>
    static Button Tick(string text, bool on, Action pressed)
    {
        var b = new Button { Text = text == null ? (on ? "✓" : "") : (on ? "✓  " : "") + text, AutoTranslateMode = AutoTranslateModeEnum.Disabled, FocusMode = FocusModeEnum.None,
            CustomMinimumSize = text == null ? new Vector2(24, 24) : new Vector2(0, 30), SizeFlagsHorizontal = text == null ? SizeFlags.ShrinkBegin : SizeFlags.Fill, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        b.AddThemeFontSizeOverride("font_size", text == null ? 15 : 14);
        var pad = text == null ? 0 : 10;
        var bg = on ? new Color(Brand.Cooling, text == null ? 1f : 0.16f) : new Color(0, 0, 0, 0);
        var edge = on ? Brand.Cooling : new Color("#96BEDC", 0.55f);
        foreach (var st in new[] { "normal", "pressed", "disabled" }) b.AddThemeStyleboxOverride(st, Ui.Box(bg, text == null ? 5 : 6, edge, 2, pad));
        b.AddThemeStyleboxOverride("hover", Ui.Box(on ? bg : Brand.PanelHover, text == null ? 5 : 6, Brand.Cooling, 2, pad));
        var ink = text == null ? new Color("#062029") : on ? Brand.Ink : Brand.InkDim;
        b.AddThemeColorOverride("font_color", ink);
        b.AddThemeColorOverride("font_hover_color", text == null ? ink : Brand.Ink);
        b.AddThemeColorOverride("font_pressed_color", ink);
        b.Pressed += () => pressed();
        return b;
    }

    static Button Pill(string text, bool on, Action pressed)
    {
        var b = new Button { Text = text, AutoTranslateMode = AutoTranslateModeEnum.Disabled, CustomMinimumSize = new Vector2(0, 30), FocusMode = FocusModeEnum.None };
        b.AddThemeFontSizeOverride("font_size", 13);
        var bg = on ? Brand.Cooling : new Color("#1D3247");
        b.AddThemeStyleboxOverride("normal", Ui.Box(bg, 6, null, 0, 10));
        b.AddThemeStyleboxOverride("hover", Ui.Box(on ? bg : Brand.PanelHover, 6, null, 0, 10));
        b.AddThemeStyleboxOverride("pressed", Ui.Box(bg, 6, null, 0, 10));
        b.AddThemeColorOverride("font_color", on ? new Color("#062029") : Brand.InkDim);
        b.AddThemeColorOverride("font_hover_color", on ? new Color("#062029") : Brand.Ink);
        b.Pressed += () => pressed();
        return b;
    }

    static Label Big(string text)
    {
        var l = Ui.Text(text, 16, Brand.Ink, Ui.DisplaySemi);
        l.AutoTranslateMode = AutoTranslateModeEnum.Disabled; l.AutowrapMode = TextServer.AutowrapMode.Off;
        return l;
    }

    static Label Dim(string text, int size, Color? c = null)
    {
        var l = Ui.Text(text, size, c ?? Brand.InkDim);
        l.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        return l;
    }
}
