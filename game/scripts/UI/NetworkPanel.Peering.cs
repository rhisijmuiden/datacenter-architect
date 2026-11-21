using System;
using System.Linq;
using DatacenterArchitect.Core;
using DatacenterArchitect.Sim;
using Godot;

namespace DatacenterArchitect.UI;

/// <summary>
/// Carriers and internet exchanges in the network overview (transit on a commit, IX ports with free traffic, a carrier rack
/// each in the MMR), and private peering with rival datacenters: ask for a port and a capacity, the rival answers with what it allows.
/// </summary>
public partial class NetworkPanel
{
    void CarrierBlock(VBoxContainer left, GameState s, CarrierDef def)
    {
        var d = def;
        var head = d.Ix ? $"{d.Name} · {Loc.T("net_pit", Catalog.PitName(d.Pit))} · {Loc.T("net_ix_reach", N(Network.IxReach(s, d) * 100, "0"))}"
                        : $"{d.Name} · {Loc.T("net_pit", Catalog.PitName(d.Pit))} · € {N(d.PricePerMbit, "0.00")}/Mbit";
        left.AddChild(Plain(head, 15, Brand.Ink, false));
        left.AddChild(Plain(Loc.T(d.DescKey), 12, Brand.InkDim));
        var k = s.ContractOf(d.Id);
        if (k == null)
        {
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 6);
            foreach (var gbps in d.Ix ? new[] { 10, 100, 400 } : new[] { 1, 10, 100 })
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
                var ports = d.Ix ? $"{Math.Max(1, kk.Ports)} × {Port(kk.PortGbps)}" : Port(kk.PortGbps);
                if (!kk.Active) { l.Text = Loc.T("net_delivering", ports, Math.Ceiling(kk.DaysLeft)); l.AddThemeColorOverride("font_color", Brand.InkDim); return; }
                if (info != null && !info.Racked) { l.Text = "⚠ " + Loc.T("net_no_carrier_rack"); l.AddThemeColorOverride("font_color", Brand.Critical); return; }
                if (info != null && !info.Routed) { l.Text = "⚠ " + Loc.T("net_no_route"); l.AddThemeColorOverride("font_color", Brand.Critical); return; }
                var now = info?.Gbps ?? 0;
                l.Text = d.Ix ? Loc.T("net_ix_ports_line", ports, G(kk.TotalGbps), G(now))
                              : Loc.T("net_contract_line", ports, G(kk.Commit), G(now), G(Network.P95(kk.Samples)));
                if (kk.UpgradeTo != 0) l.Text += "\n" + Loc.T("net_upgrading", Port(kk.UpgradeTo), Math.Ceiling(kk.UpgradeDaysLeft));
                l.AddThemeColorOverride("font_color", Brand.Ink);
            }, 14));
            left.AddChild(Bar((_, r) =>
            {
                var info = r.Carriers.FirstOrDefault(x => x.Contract == kk);
                return info == null || !info.Up ? 0 : (info.Gbps + (d.Ix ? 0 : r.Overflow * kk.PortGbps / Math.Max(1, r.TransitCapacity))) / kk.TotalGbps;
            }));
            left.AddChild(Dyn((l, st, r) =>
            {
                var month = Network.MonthlyCost(kk);
                l.Text = d.Ix ? Loc.T("net_cost_ix_ports", Ui.Money((long)month)) : Loc.T("net_cost", Ui.Money((long)month), Ui.Money((long)Network.BurstCost(kk, r.Noc)));
            }, 12, Brand.InkDim));
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 6);
            if (!d.Ix)
            {
                row.AddChild(Small(Loc.T("net_commit_down"), () => _state().StepCommit(kk, -1)));
                row.AddChild(Small(Loc.T("net_commit_up"), () => _state().StepCommit(kk, 1)));
            }
            else
            {
                if (kk.Ports < Catalog.MaxIxPorts)
                    row.AddChild(Small(Loc.T("net_ix_add_port", Port(kk.PortGbps), Ui.Money(Catalog.Port(kk.PortGbps).SetupFee)), () => Do(st => st.AddIxPort(kk)), s.CanAddIxPort(kk) == null));
                if (kk.Ports > 1) row.AddChild(Small(Loc.T("net_ix_remove_port"), () => _state().RemoveIxPort(kk)));
            }
            var next = GameState.NextPort(kk.PortGbps);
            if (next != 0) row.AddChild(Small(Loc.T("net_port_up", Port(next), Ui.Money(Catalog.Port(next).SetupFee)), () => Do(st => st.UpgradePort(kk)), s.CanUpgradePort(kk) == null));
            row.AddChild(Small(Loc.T("net_cancel"), () => _state().CancelCarrier(kk)));
            left.AddChild(row);
        }
        left.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4) });
    }

    static string Cap(double gbps) => N(gbps, gbps >= 1 ? "0" : "0.0") + " Gbit/s";

    void PeerBlock(VBoxContainer right, GameState s, RivalDef rv)
    {
        var rival = rv;
        var p = s.PeerOf(rv.Id);
        right.AddChild(Plain(rv.Name, 15, Brand.Ink, false));
        var terms = rv.Monthly >= 0 ? Loc.T("net_peer_terms_rack2", Ui.Money(rv.SetupCost), Ui.Money(rv.Monthly), N(rv.Share * 100, "0"))
                                    : Loc.T("net_peer_terms_pay2", Ui.Money(rv.SetupCost), Ui.Money(-rv.Monthly), N(rv.Share * 100, "0"));
        right.AddChild(Plain(terms, 12, Brand.InkDim));
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 6);
        switch (p?.State)
        {
            case PeerState.Requested:
                row.AddChild(Plain(Loc.T("net_peer_requested_cap", Cap(p.Capacity), Port(p.Port)), 14, Brand.Fiber, false));
                row.AddChild(Small(Loc.T("net_peer_accept"), () => Do(st => st.StartPeering(rival.Id))));
                row.AddChild(Small(Loc.T("net_peer_decline"), () => _state().DeclinePeering(rival.Id)));
                break;
            case PeerState.Asking:
                row.AddChild(Dyn((l, st, _) => { var x = st.PeerOf(rival.Id); l.Text = Loc.T("net_peer_asking", Cap(x?.Asked ?? 0), Port(x?.AskPort ?? 10), Math.Max(1, Math.Ceiling(x?.AnswerDays ?? 0))); }, 14, Brand.InkDim, false));
                row.AddChild(Small(Loc.T("net_peer_withdraw"), () => _state().DeclinePeering(rival.Id)));
                break;
            case PeerState.Offered:
                row.AddChild(Plain(Loc.T("net_peer_offered", Cap(p.Capacity), Cap(p.Asked), Port(p.Port)), 14, Brand.Fiber, false));
                row.AddChild(Small(Loc.T("net_peer_accept_cost", Ui.Money(GameState.PeerSetupCost(rival.Id, p.Port))), () => Do(st => st.StartPeering(rival.Id))));
                row.AddChild(Small(Loc.T("net_peer_decline"), () => _state().DeclinePeering(rival.Id)));
                break;
            case PeerState.Building:
                row.AddChild(Dyn((l, st, _) => { var x = st.PeerOf(rival.Id); l.Text = Loc.T("net_peer_building_cap", Cap(x?.Capacity ?? 0), Math.Ceiling(x?.DaysLeft ?? 0)); }, 14, Brand.InkDim, false));
                break;
            case PeerState.Active:
                row.AddChild(Dyn((l, st, r) =>
                {
                    var x = st.PeerOf(rival.Id);
                    if (x == null) return;
                    var f = r.PeerFlows.TryGetValue(rival.Id, out var v) ? v : 0;
                    l.Text = Loc.T("net_peer_active_cap", G(f), Cap(x.Capacity), Port(x.Port)) + (x.Upgrading ? "\n" + Loc.T("net_peer_more_waiting", Cap(x.Asked)) : "");
                    l.AddThemeColorOverride("font_color", f >= x.Capacity * 0.95 ? Brand.Warning : Brand.Ok);
                }, 14, Brand.Ok, false));
                if (!p.Upgrading) row.AddChild(Small(Loc.T("net_peer_more"), () => OpenPeerDialog(rival.Id, true)));
                row.AddChild(Small(Loc.T("net_peer_end"), () => _state().DeclinePeering(rival.Id)));
                break;
            default:
                if (p?.State == PeerState.Declined && p.DaysLeft > 0)
                    row.AddChild(Dyn((l, st, _) => l.Text = Loc.T("net_peer_retry", Math.Ceiling(st.PeerOf(rival.Id)?.DaysLeft ?? 0)), 14, Brand.InkDim, false));
                else row.AddChild(Small(Loc.T("net_peer_request"), () => OpenPeerDialog(rival.Id, false), s.CanPeer(rival.Id, false) == null));
                break;
        }
        right.AddChild(row);
    }

    void OpenPeerDialog(int rival, bool more)
    {
        var host = GetParent() ?? this;
        host.AddChild(new PeeringDialog(_state, rival, more, _fail) { OnClosed = () => { _sig = null; Refresh(); } });
    }
}

/// <summary>
/// Ask a rival for private peering: pick a port (10G, 100G or 400G) and the capacity you want. The rival answers in a few days
/// with what it allows; the dialog shows the traffic you would send it and a hint of what it will probably allow.
/// </summary>
public partial class PeeringDialog : Modal
{
    readonly Func<GameState> _state;
    readonly int _rival;
    readonly bool _more;
    readonly Action<string> _fail;
    int _port = 10;
    double _ask = 10;
    readonly Label _summary, _askLabel;
    readonly HBoxContainer _ports;

    public PeeringDialog(Func<GameState> state, int rival, bool more, Action<string> fail) : base(more ? "peer_dlg_more_title" : "peer_dlg_title", 560)
    {
        _state = state; _rival = rival; _more = more; _fail = fail;
        var s = state();
        var rv = Catalog.Rivals[rival];
        var p = s.PeerOf(rival);
        if (more && p != null) { _port = p.Port; _ask = Network.StepDown(Math.Min(p.Port, p.Capacity * 2)); if (_ask <= p.Capacity) _ask = Math.Min(p.Port, p.Capacity * 2); }
        else { var hint = Network.PeerAllowance(s, s.Net, rival, 400, 400); _ask = Math.Max(1, Math.Min(10, hint)); }

        Body.AddChild(Ui.Heading(rv.Name, 16, Brand.Fiber));
        var n = s.Net;
        var toThem = Network.PeakGbps(n) * rv.Share;
        Body.AddChild(Plain(Loc.T("peer_dlg_traffic", Gb(toThem), N(rv.Share * 100), Gb(Network.PeakGbps(n))), 14, Brand.Ink));
        Body.AddChild(Plain(Loc.T("peer_dlg_hint"), 12, Brand.InkDim));

        Body.AddChild(Ui.Heading("peer_dlg_port", 12));
        _ports = new HBoxContainer(); _ports.AddThemeConstantOverride("separation", 6);
        Body.AddChild(_ports);

        Body.AddChild(Ui.Heading("peer_dlg_capacity", 12));
        var step = new HBoxContainer(); step.AddThemeConstantOverride("separation", 8);
        step.AddChild(Btn("−", () => Step(-1)));
        _askLabel = Plain("", 18, Brand.Ink, false);
        _askLabel.CustomMinimumSize = new Vector2(130, 0);
        _askLabel.HorizontalAlignment = HorizontalAlignment.Center;
        step.AddChild(_askLabel);
        step.AddChild(Btn("+", () => Step(1)));
        Body.AddChild(step);

        _summary = Plain("", 13, Brand.InkDim);
        Body.AddChild(_summary);

        var actions = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        actions.AddThemeConstantOverride("separation", 8);
        var cancel = Ui.Secondary("peer_dlg_cancel", Close);
        actions.AddChild(cancel);
        var send = Ui.Primary("peer_dlg_send", Send);
        actions.AddChild(send);
        Body.AddChild(actions);
        Update();
    }

    static string N(double v) => v.ToString("0", Loc.Language == "nl" ? new System.Globalization.CultureInfo("nl-NL") : new System.Globalization.CultureInfo("en-GB"));
    static string Gb(double v) => v.ToString(v >= 10 ? "0" : "0.0", Loc.Language == "nl" ? new System.Globalization.CultureInfo("nl-NL") : new System.Globalization.CultureInfo("en-GB")) + " Gbit/s";

    static Label Plain(string text, int size, Color color, bool wrap = true)
    {
        var l = Ui.Text(text, size, color);
        if (!wrap) l.AutowrapMode = TextServer.AutowrapMode.Off;
        l.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        return l;
    }

    static Button Btn(string text, Action act)
    {
        var b = Ui.Secondary(text, act);
        b.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        b.CustomMinimumSize = new Vector2(44, 0);
        return b;
    }

    void Step(int dir)
    {
        var steps = Catalog.PeerSteps.Where(x => x <= _port).ToArray();
        var i = Array.FindIndex(steps, x => x >= _ask - 1e-9);
        if (i < 0) i = steps.Length - 1;
        _ask = steps[Math.Clamp(i + dir, 0, steps.Length - 1)];
        Update();
    }

    void Update()
    {
        var s = _state();
        if (s == null) return;
        var p = s.PeerOf(_rival);
        foreach (var c in _ports.GetChildren()) c.QueueFree();
        foreach (var port in Catalog.PeerPorts)
        {
            var pt = port;
            var small = _more && p != null && pt < p.Port;
            var b = pt == _port ? Ui.Primary($"{pt}G", () => { }) : Ui.Secondary($"{pt}G", () => { _port = pt; _ask = Math.Min(_ask, pt); Update(); });
            b.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
            b.Disabled = small;
            _ports.AddChild(b);
        }
        _askLabel.Text = Gb(_ask);
        var setup = _more ? (p != null && _port > p.Port ? Catalog.Port(_port).SetupFee : 0) : GameState.PeerSetupCost(_rival, _port);
        var monthly = GameState.PeerMonthly(_rival, _ask);
        var money = monthly >= 0 ? Loc.T("peer_dlg_income", Ui.Money((long)monthly)) : Loc.T("peer_dlg_cost", Ui.Money((long)-monthly));
        _summary.Text = Loc.T("peer_dlg_summary", Ui.Money(setup), money, Catalog.PeerAnswerDays);
    }

    void Send()
    {
        var s = _state();
        var why = _more ? s.AskMorePeering(_rival, _ask, _port) : s.AskPeering(_rival, _ask, _port);
        if (why != null) { _fail?.Invoke(why); return; }
        Close();
    }
}
