using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DatacenterArchitect.Core;
using DatacenterArchitect.Sim;
using Godot;

namespace DatacenterArchitect.UI;

/// <summary>
/// Network overview: fiber routes and ducts, carrier ports with commit and burst, the internet exchange, private peering
/// with rival datacenters, the network chain from the street to the racks, traffic, the NOC, cross-connects and advice.
/// </summary>
public partial class NetworkPanel : Modal
{
    public const int TabOverview = 0, TabTree = 1;
    readonly Func<GameState> _state;
    readonly Action<string> _fail;
    readonly Action<PlacedObject> _show;
    readonly Action<BuildItem> _pick;
    readonly ScrollContainer _scroll;
    int _tab;
    /// <summary>Selected row of the network tree: "in", "core:id", "dist:id", "hall:roomId", "nohall:roomId".</summary>
    string _sel;
    readonly List<Action<GameState, NetReport>> _updaters = new();
    readonly VBoxContainer _content;
    string _sig;
    double _timer;

    static CultureInfo Culture => Loc.Language == "nl" ? new CultureInfo("nl-NL") : new CultureInfo("en-GB");
    static string N(double v, string f = "N0") => v.ToString(f, Culture);
    static string G(double v) => N(v, v >= 100 ? "N0" : "0.0") + " Gbit/s";
    static string Port(int gbps) => gbps + "G";

    public NetworkPanel(Func<GameState> state, Action<string> fail, int tab = TabOverview, Action<BuildItem> pick = null, Action<PlacedObject> show = null) : base("net_title", 1100)
    {
        _state = state; _fail = fail; _pick = pick; _show = show; _tab = Math.Clamp(tab, TabOverview, TabTree);
        var scroll = _scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 640), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _content.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(_content);
        Body.AddChild(scroll);
        FillHeight(scroll);
        Refresh();
    }

    public override void SelectTab(int tab)
    {
        _tab = Math.Clamp(tab, TabOverview, TabTree); _sig = null;
        _scroll.ScrollVertical = 0;
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
        var n = s.Net;
        var sig = string.Join(",", s.Contracts.Select(k => $"{k.Carrier}:{k.PortGbps}:{k.UpgradeTo}:{k.Active}")) + "|" +
                  string.Join(",", s.Contracts.Select(k => k.Ports)) + "|" +
                  string.Join(",", s.Peers.Select(p => $"{p.Rival}:{p.State}:{p.Upgrading}")) + $"|{n.Pits.Count}|{Loc.Language}|{s.Options.AiCompetitors}|{_tab}" +
                  (_tab == TabTree ? "|" + TreeSig(n) : "");
        if (sig != _sig) { _sig = sig; Build(s); }
        foreach (var u in _updaters) u(s, n);
    }

    Label Dyn(Action<Label, GameState, NetReport> update, int size = 15, Color? color = null, bool wrap = true)
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

    Button Small(string text, Action act, bool enabled = true)
    {
        var b = Ui.Secondary(text, () => { act(); _sig = null; Refresh(); });
        b.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        b.AddThemeFontSizeOverride("font_size", 13);
        b.Disabled = !enabled;
        return b;
    }

    ProgressBar Bar(Func<GameState, NetReport, double> ratio, float width = 0)
    {
        var bar = new ProgressBar { MinValue = 0, MaxValue = 1, ShowPercentage = false, CustomMinimumSize = new Vector2(width, 6), SizeFlagsVertical = SizeFlags.ShrinkCenter };
        _updaters.Add((s, r) =>
        {
            var x = ratio(s, r);
            bar.Value = Math.Min(1, x);
            bar.AddThemeStyleboxOverride("fill", Ui.Box(x > 1 ? Brand.Critical : x > 0.85 ? Brand.Warning : Brand.Fiber, 3, null, 0, 0));
        });
        return bar;
    }

    void Do(Func<GameState, string> act) { var why = act(_state()); if (why != null) _fail(why); }

    void Build(GameState s)
    {
        foreach (var c in _content.GetChildren()) c.QueueFree();
        _updaters.Clear();
        if (_tab == TabTree) { BuildTree(s); return; }
        var n = s.Net;
        var cols = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        cols.AddThemeConstantOverride("separation", 28);
        _content.AddChild(cols);
        var left = new VBoxContainer { CustomMinimumSize = new Vector2(440, 0), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        left.AddThemeConstantOverride("separation", 6);
        var right = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        right.AddThemeConstantOverride("separation", 6);
        cols.AddChild(left); cols.AddChild(right);

        // ---- routes: own pits and ducts ----
        left.AddChild(Ui.Heading("net_routes", 14, Brand.Fiber));
        if (n.Pits.Count == 0) left.AddChild(Plain(Loc.T("net_no_pits"), 14, Brand.Warning));
        for (var i = 0; i < n.Pits.Count; i++)
        {
            var idx = i;
            // each pit line is a link: click it and the camera goes to that pit and selects it
            var link = new Button { Flat = true, Alignment = HorizontalAlignment.Left, MouseDefaultCursorShape = CursorShape.PointingHand,
                TooltipText = Loc.T("net_pit_show"), AutoTranslateMode = AutoTranslateModeEnum.Disabled, ClipText = true, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            link.AddThemeFontSizeOverride("font_size", 14);
            link.Pressed += () => { var r = _state()?.Net; if (r != null && idx < r.Pits.Count) _show?.Invoke(r.Pits[idx].Pit); };
            _updaters.Add((_, r) =>
            {
                if (idx >= r.Pits.Count) return;
                var p = r.Pits[idx];
                link.Text = "› " + Loc.T("net_pit_line", idx + 1, Catalog.PitName(p.Street), p.Used, p.Subducts, p.Subducts * Catalog.FibersPerCable);
                var c = p.Used >= p.Subducts ? Brand.Warning : Brand.Ink;
                foreach (var k in new[] { "font_color", "font_focus_color", "font_pressed_color" }) link.AddThemeColorOverride(k, c);
                link.AddThemeColorOverride("font_hover_color", Brand.Fiber);
            });
            left.AddChild(link);
        }
        left.AddChild(Dyn((l, _, r) => { l.Text = Loc.T("net_routes_count", r.Routes); l.AddThemeColorOverride("font_color", r.Routes >= 2 ? Brand.Ok : Brand.InkDim); }, 14));
        left.AddChild(Ui.Text("net_routes_help", 12, Brand.InkDim));
        left.AddChild(new HSeparator());

        // ---- carriers (transit on a commit) and the internet exchanges (ports only) ----
        left.AddChild(Ui.Heading("net_carriers", 14, Brand.Fiber));
        foreach (var def in Catalog.Carriers.Where(c => !c.Ix)) CarrierBlock(left, s, def);
        left.AddChild(Ui.Text("net_carriers_help", 12, Brand.InkDim));
        left.AddChild(new HSeparator());
        left.AddChild(Ui.Heading("net_ixes", 14, Brand.Fiber));
        foreach (var def in Catalog.Carriers.Where(c => c.Ix)) CarrierBlock(left, s, def);
        left.AddChild(Ui.Text("net_ixes_help", 12, Brand.InkDim));
        left.AddChild(Dyn((l, st, r) =>
        {
            var need = st.Contracts.Count(k => !k.NoRackNeeded);
            l.Text = Loc.T("net_carrier_racks", r.CarrierRacks, need);
            l.AddThemeColorOverride("font_color", r.CarrierRacks < need ? Brand.Warning : Brand.InkDim);
        }, 13));
        var placeRow = new HBoxContainer();
        placeRow.AddChild(PlaceButton(ObjKind.CarrierRack, "ntree_place_mmr", Loc.T("obj_carrier_rack")));
        left.AddChild(placeRow);

        // ---- the chain ----
        right.AddChild(Ui.Heading("net_chain", 14, Brand.Fiber));
        var grid = new GridContainer { Columns = 5 };
        grid.AddThemeConstantOverride("h_separation", 14); grid.AddThemeConstantOverride("v_separation", 4);
        foreach (var h in new[] { "chain_link", "chain_capacity", "chain_load", "chain_redundancy", "" })
            grid.AddChild(h == "" ? new Control() : Ui.Heading(h, 11));
        foreach (var link in Enum.GetValues<NetLink>())
        {
            var lk = link;
            string Fmt(double v) => lk is NetLink.Upstream or NetLink.Core ? G(v) : lk == NetLink.Ducts ? Loc.T("net_unit_subducts", N(v)) : Loc.T("net_unit_ports", N(v));
            grid.AddChild(Dyn((l, _, r) => { var on = r.Bottleneck?.Link == lk && r.Bottleneck.Ratio > 0.85; l.Text = Loc.T("netlink_" + lk.ToString().ToLowerInvariant()) + (on ? "  ◀" : ""); l.AddThemeColorOverride("font_color", on ? Brand.Fiber : Brand.Ink); }, 14, null, false));
            grid.AddChild(Dyn((l, _, r) => l.Text = Fmt(r.Link(lk).Capacity), 14, null, false));
            grid.AddChild(Dyn((l, _, r) => l.Text = Fmt(r.Link(lk).Load), 14, null, false));
            grid.AddChild(Dyn((l, _, r) => { var x = r.Link(lk); l.Text = x.Redundancy; l.AddThemeColorOverride("font_color", x.Redundancy == "N-" ? Brand.Critical : x.Redundancy is "N+1" or "2N" ? Brand.Ok : Brand.Ink); }, 14, null, false));
            grid.AddChild(Bar((_, r) => r.Link(lk).Ratio, 110));
        }
        right.AddChild(grid);
        right.AddChild(Dyn((l, st, r) =>
        {
            var running = r.OnlineRacks + r.OfflineRacks;
            l.Text = st.Creative.NoNetworkLimit ? Loc.T("net_unlimited") : Loc.T("net_online", r.OnlineRacks, running, N(r.Served * 100, "0"));
            l.AddThemeColorOverride("font_color", r.OfflineRacks > 0 || r.Served < 0.999 ? Brand.Critical : Brand.Ink);
        }));
        right.AddChild(Ui.Text("net_chain_help", 12, Brand.InkDim));
        right.AddChild(new HSeparator());

        // ---- traffic ----
        right.AddChild(Ui.Heading("net_traffic", 14, Brand.Fiber));
        right.AddChild(Dyn((l, _, r) => l.Text = Loc.T("net_traffic_now", G(r.DemandGbps), G(r.IxGbps), G(r.PeerGbps), G(r.TransitGbps))));
        right.AddChild(Dyn((l, _, r) => { l.Text = r.Overflow > 0.001 ? "⚠ " + Loc.T("net_dropped", G(r.Overflow)) : ""; l.Visible = r.Overflow > 0.001; }, 14, Brand.Critical));
        var hallNo = 0;
        foreach (var h in n.Halls)
        {
            hallNo++;
            var room = h.Room; var no = hallNo;
            right.AddChild(Dyn((l, _, r) =>
            {
                var x = r.Halls.FirstOrDefault(y => y.Room == room);
                if (x == null) return;
                l.Text = Loc.T("net_hall_line", no, x.Online, x.Racks, x.Spines, G(x.Gbps));
                l.AddThemeColorOverride("font_color", x.Online < x.Racks ? Brand.Critical : Brand.Ink);
            }, 14));
        }
        right.AddChild(new HSeparator());

        // ---- peering ----
        right.AddChild(Ui.Heading("net_peering", 14, Brand.Fiber));
        var rivals = Catalog.Rivals.Where(rv => s.RivalOn(rv.Id)).ToList();
        if (!s.Options.AiCompetitors || rivals.Count == 0) right.AddChild(Plain(Loc.T("net_no_rivals"), 14, Brand.InkDim));
        else
            foreach (var rv in rivals) PeerBlock(right, s, rv);
        right.AddChild(Ui.Text("net_peering_help", 12, Brand.InkDim));
        right.AddChild(new HSeparator());

        // ---- NOC and cross-connects ----
        right.AddChild(Ui.Heading("net_noc", 14, Brand.Fiber));
        right.AddChild(Dyn((l, _, r) => { l.Text = r.Noc ? Loc.T("net_noc_on") : Loc.T("net_noc_off"); l.AddThemeColorOverride("font_color", r.Noc ? Brand.Ok : Brand.InkDim); }, 14));
        right.AddChild(Dyn((l, st, r) => l.Text = Loc.T("net_xc", st.CrossConnects, st.XcPending, Ui.Money(st.CrossConnects * Catalog.XcMonthlyFee), Math.Max(0, r.OdfPorts - r.OdfUsed)), 14));

        // ---- advice ----
        _content.AddChild(new HSeparator());
        _content.AddChild(Ui.Heading("advice_title", 14));
        _content.AddChild(Dyn((l, st, r) =>
        {
            var tips = Advice(st, r);
            l.Text = tips.Count == 0 ? Loc.T("net_advice_ok") : string.Join("\n", tips.Select(t => "• " + Loc.T(t)));
            l.AddThemeColorOverride("font_color", tips.Count == 0 ? Brand.Ok : Brand.Ink);
        }, 14));
        // a full pit: one click takes you to it
        var go = Small(Loc.T("net_pit_goto_full"), () =>
        {
            var full = _state()?.Net.Pits.FirstOrDefault(p => p.Used >= p.Subducts);
            if (full != null) _show?.Invoke(full.Pit);
        });
        go.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        _updaters.Add((_, r) => go.Visible = _show != null && r.Pits.Any(p => p.Used >= p.Subducts));
        _content.AddChild(go);
    }

    /// <summary>What the network is missing, most urgent first, followed by its single points of failure.</summary>
    public static List<string> Advice(GameState s, NetReport r)
    {
        var list = new List<string>();
        if (!s.Creative.NoNetworkLimit)
        {
            if (r.Pits.Count == 0) list.Add("net_advice_pit");
            if (!r.Mmr) list.Add("net_advice_mmr");
            if (r.Cores == 0) list.Add("net_advice_core");
            if (s.Contracts.Count == 0) list.Add("net_advice_carrier");
            if (r.Halls.Any(h => h.Racks > 0 && h.Spines == 0)) list.Add("net_advice_spine");
            else if (r.Halls.Any(h => h.Online < h.Racks)) list.Add("net_advice_spine_ports");
            if (r.Uplink.Values.Any(u => u < 0)) list.Add("ntree_advice_uplink");
            else if (r.Dists.Count == 0 && r.DirectEdges.Count >= 6) list.Add("ntree_advice_dist");
            if (r.Served < 0.999) list.Add("net_advice_congested");
            else if (r.TransitCapacity > 0 && r.TransitRatio > 0.85) list.Add("net_advice_transit_tight");
            if (r.Pits.Count > 0 && r.Pits.All(p => p.Used >= p.Subducts)) list.Add("net_advice_ducts");
            if (r.Mmr && r.OdfUsed >= r.OdfPorts) list.Add("net_advice_odf");
            if (!r.Noc && s.Contracts.Count > 0) list.Add("net_advice_noc");
            if (r.IxPort <= 0 && s.Contracts.Count > 0) list.Add("net_advice_ix");
            if (r.Carriers.Any(c => !c.Racked)) list.Add("net_advice_carrier_rack");
            else if (r.IxPort > 0 && r.IxFlows.Any(f => { var k = s.ContractOf(f.Key); return k != null && f.Value >= k.TotalGbps * 0.95; })) list.Add("net_advice_ix_full");
        }
        list.AddRange(r.Spofs);
        return list;
    }
}
