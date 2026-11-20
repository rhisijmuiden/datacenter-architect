using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DatacenterArchitect.Core;
using DatacenterArchitect.Sim;
using Godot;

namespace DatacenterArchitect.UI;

/// <summary>
/// Power and cooling overview: grid connection and upgrades, energy supplier, the power chain with its
/// bottleneck, backup power (generators, fuel, UPS batteries), cooling, PUE and every data hall.
/// </summary>
public partial class PowerPanel : Modal
{
    readonly Func<GameState> _state;
    readonly Action<string> _fail;
    readonly List<Action<GameState, InfraReport>> _updaters = new();
    readonly VBoxContainer _content;
    string _sig;
    double _timer;

    static string N(double v, string f = "N0") => v.ToString(f, Loc.Language == "nl" ? new CultureInfo("nl-NL") : new CultureInfo("en-GB"));
    static string Kw(double v) => v >= 10000 ? N(v / 1000, "0.0") + " MW" : N(v) + " kW";

    public PowerPanel(Func<GameState> state, Action<string> fail) : base("power_title", 900)
    {
        _state = state; _fail = fail;
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 640), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _content.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(_content);
        Body.AddChild(scroll);
        FillHeight(scroll);
        Refresh();
    }

    public override void _Process(double delta)
    {
        _timer -= delta;
        if (_timer > 0) return;
        _timer = 0.5;
        Refresh();
    }

    void Refresh()
    {
        var s = _state();
        if (s == null) return;
        var r = s.Infra;
        var sig = $"{s.GridLevel}:{s.GridRequest}:{s.Supplier}:{s.Rooms.Count(x => x.Type == RoomType.DataHall)}:{Loc.Language}:{s.Rooms.Sum(x => (int)x.Containment + (x.Busbar ? 10 : 0) + x.Upgrade * 100)}";
        if (sig != _sig) { _sig = sig; Build(s); }
        foreach (var u in _updaters) u(s, r);
    }

    Label Dyn(Action<Label, GameState, InfraReport> update, int size = 15, Color? color = null, bool wrap = true)
    {
        var l = Ui.Text("", size, color ?? Brand.Ink);
        if (!wrap) l.AutowrapMode = TextServer.AutowrapMode.Off;
        l.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        _updaters.Add((s, r) => update(l, s, r));
        return l;
    }

    static Label Plain(string text, int size = 15, Color? color = null, bool wrap = true)
    {
        var l = Ui.Text(text, size, color ?? Brand.Ink);
        if (!wrap) l.AutowrapMode = TextServer.AutowrapMode.Off;
        l.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        return l;
    }

    void Build(GameState s)
    {
        foreach (var c in _content.GetChildren()) c.QueueFree();
        _updaters.Clear();
        var cols = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        cols.AddThemeConstantOverride("separation", 28);
        _content.AddChild(cols);
        var left = new VBoxContainer { CustomMinimumSize = new Vector2(360, 0), SizeFlagsStretchRatio = 0.8f, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        left.AddThemeConstantOverride("separation", 6);
        var right = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        right.AddThemeConstantOverride("separation", 6);
        cols.AddChild(left); cols.AddChild(right);

        // ---- grid connection ----
        left.AddChild(Ui.Heading("power_grid", 14, Brand.Power));
        var cur = Catalog.GridLevels[s.GridLevel];
        left.AddChild(Plain($"{N(cur.Mva)} MVA · {Loc.T(cur.NameKey)}", 17));
        if (s.GridRequest >= 0)
        {
            var req = Catalog.GridLevels[s.GridRequest];
            left.AddChild(Dyn((l, st, _) => l.Text = Loc.T("grid_request_running", N(req.Mva), Math.Ceiling(st.GridDaysLeft)), 14, Brand.InkDim));
            var bar = new ProgressBar { MinValue = 0, MaxValue = 1, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 6) };
            _updaters.Add((st, _) => bar.Value = 1 - st.GridDaysLeft / Math.Max(1, req.LeadDays));
            left.AddChild(bar);
        }
        else
        {
            foreach (var lv in Catalog.GridLevels.Where(x => x.Level > s.GridLevel))
            {
                var level = lv.Level;
                var b = Ui.Secondary(Loc.T("grid_request", N(lv.Mva), Ui.Money(lv.Cost), lv.LeadDays), () =>
                {
                    var why = _state().RequestGrid(level);
                    if (why != null) _fail(why);
                    _sig = null; Refresh();
                });
                b.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
                b.Alignment = HorizontalAlignment.Left;
                var locked = s.GridUnlock(level);
                if (locked != null) { b.Disabled = true; b.TooltipText = Loc.T(locked); b.Text += "  ·  " + Loc.T(locked); }
                left.AddChild(b);
            }
            if (s.GridLevel >= Catalog.GridLevels.Length - 1) left.AddChild(Plain(Loc.T("grid_max"), 14, Brand.InkDim));
        }
        left.AddChild(Ui.Text("grid_help", 12, Brand.InkDim));
        left.AddChild(new HSeparator());

        // ---- supplier ----
        left.AddChild(Ui.Heading("power_supplier", 14, Brand.Power));
        foreach (var sup in Catalog.Suppliers)
        {
            var id = sup.Id;
            var text = $"{sup.Name} · € {N(sup.PricePerKwh, "0.000")}/kWh\n{Loc.T(sup.DescKey, sup.Years)}";
            var fee = s.SwitchFee(id);
            if (fee > 0) text += "\n" + Loc.T("supplier_fee", Ui.Money(fee));
            var b = Ui.Secondary(text, () => { var why = _state().SetSupplier(id); if (why != null && why != "msg_nothing_changed") _fail(why); _sig = null; Refresh(); });
            b.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
            b.Alignment = HorizontalAlignment.Left;
            b.CustomMinimumSize = new Vector2(0, 52);
            if (id == s.Supplier)
            {
                var st = Ui.Box(Brand.PanelHover, 8, Brand.Power, 2, 10);
                b.AddThemeStyleboxOverride("normal", st); b.AddThemeStyleboxOverride("hover", st);
            }
            if (sup.Green) b.AddThemeColorOverride("font_color", Brand.Ok);
            left.AddChild(b);
        }
        left.AddChild(Dyn((l, st, _) => l.Text = Loc.T("supplier_today", N(st.EnergyPrice(), "0.000")), 14, Brand.InkDim));
        left.AddChild(Dyn((l, st, _) =>
        {
            var cur = Catalog.Suppliers[st.Supplier];
            var left2 = cur.Years * 365 - (st.Day - st.EnergyStartDay);
            l.Text = cur.Variable ? Loc.T("supplier_term_free") : left2 > 0 ? Loc.T("supplier_term_left", N(Math.Ceiling(left2))) : Loc.T("supplier_term_done");
            if (st.EnergyDiscount > 0) l.Text += "  ·  " + Loc.T("supplier_discount", N(st.EnergyDiscount * 100, "0.#"));
        }, 13, Brand.InkDim));
        left.AddChild(new HSeparator());

        // ---- backup ----
        left.AddChild(Ui.Heading("power_backup", 14, Brand.Power));
        left.AddChild(Dyn((l, _, r) => l.Text = Loc.T("backup_generators", r.Generators, Kw(r.GenKw), r.GenRedundancy, Kw(r.BackupKw))));
        left.AddChild(Dyn((l, _, r) => { l.Text = r.Ats ? Loc.T("backup_ats_yes") : Loc.T("backup_ats_no"); l.AddThemeColorOverride("font_color", r.Ats || r.Generators == 0 ? Brand.Ink : Brand.Critical); }));
        left.AddChild(Dyn((l, _, r) => l.Text = Loc.T("backup_fuel", N(r.FuelLitres), r.FuelHours > 0 ? N(r.FuelHours, "0") : "-")));
        left.AddChild(Dyn((l, _, r) =>
        {
            l.Text = Loc.T("backup_ups", Kw(r.UpsKw), N(r.BatteryKwh), r.UpsMinutes > 0 ? N(r.UpsMinutes, "0.0") : "-");
            l.AddThemeColorOverride("font_color", r.UpsKw > 0 && r.ItRunningKw > 0 && r.UpsMinutes < 5 ? Brand.Warning : Brand.Ink);
        }));

        // ---- power chain ----
        right.AddChild(Ui.Heading("power_chain", 14, Brand.Power));
        var grid = new GridContainer { Columns = 5 };
        grid.AddThemeConstantOverride("h_separation", 14); grid.AddThemeConstantOverride("v_separation", 4);
        foreach (var h in new[] { "chain_link", "chain_capacity", "chain_load", "chain_redundancy", "" })
            grid.AddChild(h == "" ? new Control() : Ui.Heading(h, 11));
        foreach (var link in Enum.GetValues<PowerLink>())
        {
            var lk = link;
            grid.AddChild(Dyn((l, _, r) => { l.Text = Loc.T("link_" + lk.ToString().ToLowerInvariant()) + (r.Bottleneck?.Link == lk && r.ItDemandKw > 0 ? "  ◀" : ""); l.AddThemeColorOverride("font_color", r.Bottleneck?.Link == lk && r.ItDemandKw > 0 ? Brand.Power : Brand.Ink); }, 14, null, false));
            grid.AddChild(Dyn((l, _, r) => l.Text = Kw(r.Link(lk).CapacityKw), 14, null, false));
            grid.AddChild(Dyn((l, _, r) => l.Text = Kw(r.Link(lk).LoadKw), 14, null, false));
            grid.AddChild(Dyn((l, _, r) => { var x = r.Link(lk); l.Text = x.Redundancy; l.AddThemeColorOverride("font_color", x.Redundancy == "N-" ? Brand.Critical : x.Redundancy is "N+1" or "2N" ? Brand.Ok : Brand.Ink); }, 14, null, false));
            var bar = new ProgressBar { MinValue = 0, MaxValue = 1, ShowPercentage = false, CustomMinimumSize = new Vector2(110, 6), SizeFlagsVertical = SizeFlags.ShrinkCenter };
            _updaters.Add((_, r) =>
            {
                var ratio = r.Link(lk).Ratio;
                bar.Value = Math.Min(1, ratio);
                bar.AddThemeStyleboxOverride("fill", Ui.Box(ratio > 1 ? Brand.Critical : ratio > 0.85 ? Brand.Warning : Brand.Ok, 3, null, 0, 0));
            });
            grid.AddChild(bar);
        }
        right.AddChild(grid);
        right.AddChild(Dyn((l, st, r) =>
        {
            l.Text = st.Creative.NoPowerLimit ? Loc.T("chain_unlimited") : Loc.T("chain_usable", Kw(r.UsableItKw), r.RunningRacks, r.LiveRacks);
            l.AddThemeColorOverride("font_color", r.DownForPower > 0 ? Brand.Critical : Brand.Ink);
        }));
        right.AddChild(Ui.Text("chain_help", 12, Brand.InkDim));
        right.AddChild(new HSeparator());

        // ---- cooling ----
        right.AddChild(Ui.Heading("cool_title", 14, Brand.Cooling));
        right.AddChild(Dyn((l, _, r) => l.Text = Loc.T("cool_summary", Kw(r.HeatKw), Kw(r.CrahKw), Kw(r.RejectKw))));
        right.AddChild(Dyn((l, _, r) => l.Text = Loc.T("cool_pue", r.Pue > 0 ? r.Pue.ToString("0.00", CultureInfo.InvariantCulture) : "-")));
        right.AddChild(Ui.Text("cool_help", 12, Brand.InkDim));
        right.AddChild(new HSeparator());

        // ---- halls ----
        right.AddChild(Ui.Heading("halls_title", 14));
        var halls = s.Rooms.Where(x => x.Type == RoomType.DataHall).ToList();
        if (halls.Count == 0) right.AddChild(Plain(Loc.T("halls_none"), 14, Brand.InkDim));
        var hg = new GridContainer { Columns = 6 };
        hg.AddThemeConstantOverride("h_separation", 12); hg.AddThemeConstantOverride("v_separation", 3);
        if (halls.Count > 0)
            foreach (var h in new[] { "hall_col_hall", "hall_col_racks", "hall_col_power", "hall_col_ab", "hall_col_air", "hall_col_temp" }) hg.AddChild(Ui.Heading(h, 11));
        var i = 0;
        foreach (var hall in halls)
        {
            i++;
            var room = hall;
            hg.AddChild(Plain($"{i} · {hall.W}×{hall.D} m" + (hall.Floor > 0 ? $" · {Loc.T("hud_floor_n", hall.Floor)}" : ""), 14, null, false));
            HallInfo Info(InfraReport r) => r.Halls.FirstOrDefault(x => x.Room == room);
            hg.AddChild(Dyn((l, _, r) => { var x = Info(r); l.Text = x == null ? "-" : $"{x.RunningRacks}/{x.LiveRacks}"; l.AddThemeColorOverride("font_color", x != null && x.RunningRacks < x.LiveRacks ? Brand.Critical : Brand.Ink); }, 14, null, false));
            hg.AddChild(Dyn((l, _, r) => { var x = Info(r); l.Text = x == null ? "-" : $"{N(x.ItKw)}/{N(x.DistributionKw)} kW"; l.AddThemeColorOverride("font_color", x != null && x.PowerLimited ? Brand.Critical : Brand.Ink); }, 14, null, false));
            hg.AddChild(Dyn((l, _, r) => { var x = Info(r); l.Text = x == null || x.ItKw <= 0 ? "-" : x.AB ? "A+B" : Loc.T("hall_ab_no"); l.AddThemeColorOverride("font_color", x != null && x.AB ? Brand.Ok : Brand.InkDim); }, 14, null, false));
            hg.AddChild(Plain(Loc.T(Catalog.Containments[room.Containment].NameKey) + (room.Busbar ? " · busbar" : "") + (room.Upgrade != 0 ? " ⟳" : ""), 14, null, false));
            hg.AddChild(Dyn((l, _, r) => { l.Text = $"{room.Temp:0} °C"; l.AddThemeColorOverride("font_color", room.Hot ? Brand.Critical : room.Temp >= 27 ? Brand.Warning : Brand.Cooling); }, 14, null, false));
        }
        right.AddChild(hg);

        // ---- advice ----
        _content.AddChild(new HSeparator());
        _content.AddChild(Ui.Heading("advice_title", 14));
        _content.AddChild(Dyn((l, st, r) =>
        {
            var tips = Advice(st, r);
            l.Text = tips.Count == 0 ? Loc.T("advice_ok") : string.Join("\n", tips.Select(t => "• " + Loc.T(t)));
            l.AddThemeColorOverride("font_color", tips.Count == 0 ? Brand.Ok : Brand.Ink);
        }, 14));
    }

    /// <summary>Short hints for what the power chain or the cooling is missing, most urgent first.</summary>
    public static List<string> Advice(GameState s, InfraReport r)
    {
        var list = new List<string>();
        var c = s.Creative;
        if (!c.NoPowerLimit)
        {
            if (r.Link(PowerLink.Switchgear).CapacityKw <= 0) list.Add("advice_switchgear");
            if (r.Link(PowerLink.Ups).CapacityKw <= 0) list.Add("advice_ups");
            if (r.Halls.Any(h => h.LiveRacks > 0 && h.DistributionKw <= 0)) list.Add("advice_pdu");
            if (r.DownForPower > 0) list.Add("advice_power_short");
            if (r.UpsKw > 0 && r.BatteryKwh <= 0) list.Add("advice_battery");
            if (r.Generators < 2) list.Add("advice_generators");
            else if (!r.Ats) list.Add("advice_ats");
            if (r.Link(PowerLink.Grid).Ratio > 0.85) list.Add("advice_grid");
        }
        if (!c.NoCoolingLimit)
        {
            if (r.HeatKw > 0 && r.RejectKw <= 0) list.Add("advice_reject");
            if (r.Halls.Any(h => h.LiveRacks > 0 && h.CrahKw <= 0)) list.Add("advice_crah");
            if (r.Halls.Any(h => h.Room.Hot)) list.Add("advice_hot");
            else if (r.CoolRatio > 0.9) list.Add("advice_cooling_tight");
        }
        if (r.Halls.Any(h => h.LiveRacks > 0 && h.Room.Containment == Containment.Open)) list.Add("advice_containment");
        return list;
    }
}
