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

    public NetworkPanel(Func<GameState> state, Action<string> fail, int tab = TabOverview, Action<BuildItem> pick = null) : base("net_title", 1100)
    {
        _state = state; _fail = fail; _pick = pick; _tab = Math.Clamp(tab, TabOverview, TabTree);
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
                  string.Join(",", s.Peers.Select(p => $"{p.Rival}:{p.State}")) + $"|{n.Entries.Count}:{n.Ducts.Count}|{Loc.Language}|{s.Options.AiCompetitors}|{_tab}" +
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

        // ---- routes: cable entries on the plot border and your own main ducts to the MMR ----
        left.AddChild(Ui.Heading("net_routes", 14, Brand.Fiber));
        if (n.Entries.Count == 0) left.AddChild(Plain(Loc.T("net_no_entries"), 14, Brand.Warning));
        for (var i = 0; i < n.Entries.Count; i++)
        {
            var idx = i;
            left.AddChild(Dyn((l, _, r) =>
            {
                if (idx >= r.Entries.Count) return;
                var e = r.Entries[idx];
                l.Text = EntryLine(idx, e);
                l.AddThemeColorOverride("font_color", !e.Connected || e.Entry.Broken ? Brand.Warning : Brand.Ink);
            }, 14));
        }
        for (var i = 0; i < n.Ducts.Count; i++)
        {
            var idx = i;
            left.AddChild(Dyn((l, _, r) =>
            {
                if (idx >= r.Ducts.Count) return;
                var d = r.Ducts[idx];
                l.Text = DuctLine(idx, d);
                l.AddThemeColorOverride("font_color", d.Used >= d.Duct.Ways || !d.ToMmr ? Brand.Warning : Brand.InkDim);
            }, 13));
        }
        var tools = new HBoxContainer(); tools.AddThemeConstantOverride("separation", 6);
        tools.AddChild(Small(Loc.T("net_place_entry"), () => _pick?.Invoke(new BuildItem(Mode.Object, (int)ObjKind.FiberPit)), _pick != null));
        tools.AddChild(Small(Loc.T("net_draw_duct"), () => _pick?.Invoke(new BuildItem(Mode.Duct, 12)), _pick != null));
        left.AddChild(tools);
        left.AddChild(Dyn((l, _, r) => { l.Text = Loc.T("net_routes_count", r.Routes); l.AddThemeColorOverride("font_color", r.Routes >= 2 ? Brand.Ok : Brand.InkDim); }, 14));
        left.AddChild(Ui.Text("net_routes_help", 12, Brand.InkDim));
        left.AddChild(new HSeparator());

        // ---- carriers and the IX ----
        left.AddChild(Ui.Heading("net_carriers", 14, Brand.Fiber));
        foreach (var def in Catalog.Carriers)
        {
            var d = def;
            var head = d.Ix ? $"{d.Name} · {Loc.T("net_ix")}"
                            : $"{d.Name} · € {N(d.PricePerMbit, "0.00")}/Mbit";
            left.AddChild(Plain(head, 15, Brand.Ink, false));
            left.AddChild(Plain(Loc.T(d.DescKey), 12, Brand.InkDim));
            var k = s.ContractOf(d.Id);
            if (k == null)
            {
                var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 6);
                foreach (var gbps in new[] { 1, 10, 100 })
                {
                    var g = gbps;
                    var pd = Catalog.Port(g);
                    row.AddChild(Small(Loc.T("net_order", Port(g), Ui.Money(pd.SetupFee)), () => Do(st => st.OrderCarrier(d.Id, g)), s.CanAfford(pd.SetupFee)));
                }
                left.AddChild(row);
            }
            else
            {
                var kk = k;
                left.AddChild(Dyn((l, st, r) =>
                {
                    var info = r.Carriers.FirstOrDefault(x => x.Contract == kk);
                    if (!kk.Active) { l.Text = Loc.T("net_delivering", Port(kk.PortGbps), Math.Ceiling(kk.DaysLeft)); l.AddThemeColorOverride("font_color", Brand.InkDim); return; }
                    if (info != null && !info.Routed) { l.Text = "⚠ " + Loc.T("net_no_route"); l.AddThemeColorOverride("font_color", Brand.Critical); return; }
                    var now = info?.Gbps ?? 0;
                    l.Text = d.Ix ? Loc.T("net_ix_line", Port(kk.PortGbps), G(now))
                                  : Loc.T("net_contract_line", Port(kk.PortGbps), G(kk.Commit), G(now), G(Network.P95(kk.Samples)));
                    if (kk.UpgradeTo != 0) l.Text += "\n" + Loc.T("net_upgrading", Port(kk.UpgradeTo), Math.Ceiling(kk.UpgradeDaysLeft));
                    l.AddThemeColorOverride("font_color", Brand.Ink);
                }, 14));
                left.AddChild(Bar((_, r) => { var info = r.Carriers.FirstOrDefault(x => x.Contract == kk); return info == null || !info.Up ? 0 : (info.Gbps + (d.Ix ? 0 : r.Overflow * kk.PortGbps / Math.Max(1, r.TransitCapacity))) / kk.PortGbps; }));
                left.AddChild(Dyn((l, st, r) =>
                {
                    var month = Network.MonthlyCost(kk);
                    var burst = Network.BurstCost(kk, r.Noc);
                    l.Text = d.Ix ? Loc.T("net_cost_ix", Ui.Money((long)month)) : Loc.T("net_cost", Ui.Money((long)month), Ui.Money((long)burst));
                }, 12, Brand.InkDim));
                var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 6);
                if (!d.Ix)
                {
                    row.AddChild(Small(Loc.T("net_commit_down"), () => _state().StepCommit(kk, -1)));
                    row.AddChild(Small(Loc.T("net_commit_up"), () => _state().StepCommit(kk, 1)));
                }
                var next = GameState.NextPort(kk.PortGbps);
                if (next != 0) row.AddChild(Small(Loc.T("net_port_up", Port(next), Ui.Money(Catalog.Port(next).SetupFee)), () => Do(st => st.UpgradePort(kk)), s.CanUpgradePort(kk) == null));
                row.AddChild(Small(Loc.T("net_cancel"), () => _state().CancelCarrier(kk)));
                left.AddChild(row);
            }
            left.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4) });
        }
        left.AddChild(Ui.Text("net_carriers_help", 12, Brand.InkDim));

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
        if (!s.Options.AiCompetitors) right.AddChild(Plain(Loc.T("net_no_rivals"), 14, Brand.InkDim));
        else
            foreach (var rv in Catalog.Rivals)
            {
                var rival = rv;
                var p = s.PeerOf(rv.Id);
                right.AddChild(Plain(rv.Name, 15, Brand.Ink, false));
                var terms = rv.Monthly >= 0 ? Loc.T("net_peer_terms_rack", Ui.Money(rv.SetupCost), Ui.Money(rv.Monthly), N(rv.Share * 100, "0"))
                                            : Loc.T("net_peer_terms_pay", Ui.Money(rv.SetupCost), Ui.Money(-rv.Monthly), N(rv.Share * 100, "0"));
                right.AddChild(Plain(terms, 12, Brand.InkDim));
                var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 6);
                switch (p?.State)
                {
                    case PeerState.Requested:
                        row.AddChild(Plain(Loc.T("net_peer_requested"), 14, Brand.Fiber, false));
                        row.AddChild(Small(Loc.T("net_peer_accept"), () => Do(st => st.StartPeering(rival.Id))));
                        row.AddChild(Small(Loc.T("net_peer_decline"), () => _state().DeclinePeering(rival.Id)));
                        break;
                    case PeerState.Building:
                        row.AddChild(Dyn((l, st, _) => l.Text = Loc.T("net_peer_building", Math.Ceiling(st.PeerOf(rival.Id)?.DaysLeft ?? 0)), 14, Brand.InkDim, false));
                        break;
                    case PeerState.Active:
                        row.AddChild(Dyn((l, _, r) => l.Text = Loc.T("net_peer_active", G(r.PeerFlows.TryGetValue(rival.Id, out var f) ? f : 0)), 14, Brand.Ok, false));
                        row.AddChild(Small(Loc.T("net_peer_end"), () => _state().DeclinePeering(rival.Id)));
                        break;
                    default:
                        row.AddChild(Small(Loc.T("net_peer_propose"), () => Do(st => st.StartPeering(rival.Id))));
                        break;
                }
                right.AddChild(row);
            }
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
    }

    public static string SideName(int side) => Loc.T("side_" + side);

    public static string EntryLine(int idx, EntryInfo e) =>
        Loc.T("net_entry_line", idx + 1, SideName(e.Side), e.Used)
        + " · " + (e.Entry.Broken ? Loc.T("net_entry_cut") : e.Connected ? Loc.T("net_entry_ok", e.Ways) : Loc.T("net_entry_no_duct"));

    public static string DuctLine(int idx, DuctInfo d) =>
        Loc.T("net_duct_line", idx + 1, d.Duct.Ways, d.Duct.Length, d.Used, d.Duct.Handholes().Count)
        + (d.ToMmr ? "" : " · " + Loc.T("net_duct_open"));

    /// <summary>What the network is missing, most urgent first, followed by its single points of failure.</summary>
    public static List<string> Advice(GameState s, NetReport r)
    {
        var list = new List<string>();
        if (!s.Creative.NoNetworkLimit)
        {
            if (r.Entries.Count == 0) list.Add("net_advice_entry");
            else if (!r.Entries.Any(e => e.Connected)) list.Add("net_advice_duct");
            if (!r.Mmr) list.Add("net_advice_mmr");
            if (r.Cores == 0) list.Add("net_advice_core");
            if (s.Contracts.Count == 0) list.Add("net_advice_carrier");
            if (r.Halls.Any(h => h.Racks > 0 && h.Spines == 0)) list.Add("net_advice_spine");
            else if (r.Halls.Any(h => h.Online < h.Racks)) list.Add("net_advice_spine_ports");
            if (r.Uplink.Values.Any(u => u < 0)) list.Add("ntree_advice_uplink");
            else if (r.Dists.Count == 0 && r.DirectEdges.Count >= 6) list.Add("ntree_advice_dist");
            if (r.Served < 0.999) list.Add("net_advice_congested");
            else if (r.TransitCapacity > 0 && r.TransitRatio > 0.85) list.Add("net_advice_transit_tight");
            if (r.Entries.Any(e => e.Connected) && r.Link(NetLink.Ducts) is { Load: > 0 } dl && dl.Load >= dl.Capacity) list.Add("net_advice_ducts");
            if (r.Mmr && r.OdfUsed >= r.OdfPorts) list.Add("net_advice_odf");
            if (!r.Noc && s.Contracts.Count > 0) list.Add("net_advice_noc");
            if (r.IxPort <= 0 && s.Contracts.Count > 0) list.Add("net_advice_ix");
        }
        list.AddRange(r.Spofs);
        return list;
    }
}
