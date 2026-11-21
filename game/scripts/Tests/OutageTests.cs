using System.Linq;
using DatacenterArchitect.Core;
using DatacenterArchitect.Sim;
using DatacenterArchitect.UI;

namespace DatacenterArchitect.Tests;

/// <summary>Infra sketch board 4: every rack that is off or offline has a cause per hall and a fix.</summary>
public static partial class TestRunner
{
    static void OutageTests()
    {
        Check(Outages.PduFor(5) == ObjKind.Pdu75 && Outages.PduFor(20) == ObjKind.Pdu && Outages.PduFor(30) == ObjKind.Pdu300 && Outages.PduFor(99) == ObjKind.Pdu300, "the fix picks the smallest PDU with enough connections");

        // ---- power: no PDU, PDU without a switchboard, full PDU ----
        var (s, hall, swr, _) = PowerSite();
        var racks = Enumerable.Range(0, 14).Select(i => Live(s, ObjKind.Rack, 6 + i % 10, 6 + i / 10 * 3)).ToList();
        var list = Outages.For(s, Infra.Analyze(s), null);
        var o = list.FirstOrDefault(x => x.Hall == hall);
        Check(o != null && o.Cause == OutageCause.NoPdu && o.Racks == 14 && o.Fix == FixKind.Place && o.Place == ObjKind.Pdu, $"racks without a PDU: cause NoPdu, fix places a PDU 150 for 14 racks ({o?.Cause})");
        var text = OutageUi.Text(o); var fix = OutageUi.FixText(o);
        Check(text.Contains("DH-01") && text.Contains("14") && fix.Contains("DH-01") && fix.Contains("150") && !fix.StartsWith("fix_"), $"the warning names the hall and the fix: {text} / {fix}");

        var pdu = Live(s, ObjKind.Pdu75, 22, 20);
        o = Outages.For(s, Infra.Analyze(s), null).First(x => x.Hall == hall);
        Check(o.Cause == OutageCause.PduUnfed && o.Place == ObjKind.Switchgear && o.FixRoom == swr, $"a PDU without a switchboard: place an MSB in the switchgear room ({o.Cause})");

        Live(s, ObjKind.Switchgear, 24, 4); Live(s, ObjKind.Ups, 29, 4);
        var r = Infra.Analyze(s);
        o = Outages.For(s, r, null).First(x => x.Hall == hall);
        Check(o.Cause == OutageCause.PduFull && o.Racks == 2 && o.Place == ObjKind.Pdu75 && OutageUi.Text(o).Contains("12"), $"a full PDU 75: 2 racks off, fix places an extra PDU ({o.Cause}, {o.Racks}: {OutageUi.Text(o)})");
        Live(s, ObjKind.Pdu75, 22, 18);
        Check(!Outages.For(s, Infra.Analyze(s), null).Any(x => !x.Offline), "a second PDU: no power outage left");

        // ---- racks outside a hall ----
        var stray = LiveRaw(s, ObjKind.Rack, 26, 16);
        s.Touch();
        o = Outages.For(s, Infra.Analyze(s), null).FirstOrDefault(x => x.Cause == OutageCause.NoHall);
        Check(o != null && o.At == stray && o.Hall == null && o.Racks == 1, "a rack outside a hall points at the rack");
        s.Objects.Remove(stray); s.Touch();

        // ---- network: cause per hall ----
        var (n, nh, mmr) = NetSite();
        for (var i = 0; i < 10; i++) Live(n, ObjKind.Rack, 6 + i, 6);
        o = Outages.For(n).FirstOrDefault(x => x.Hall == nh);
        Check(o != null && o.Offline && o.Cause == OutageCause.NoCarrier && o.Fix == FixKind.Network, $"no carrier: the racks are offline, fix opens the network screen ({o?.Cause})");
        var odf = Live(n, ObjKind.Odf, 25, 10);
        Live(n, ObjKind.FiberPit, 30, 62); n.OrderCarrier(1, 10);
        n.Objects.Remove(odf); n.Touch();
        o = Outages.For(n).First(x => x.Hall == nh);
        Check(o.Cause == OutageCause.NoMmr && o.Place == ObjKind.Odf && o.FixRoom == mmr, $"no ODF: place one in the meet-me room ({o.Cause})");
        n.Objects.Add(odf); n.Touch();
        o = Outages.For(n).First(x => x.Hall == nh);
        Check(o.Cause == OutageCause.NoCore && o.Place == ObjKind.CoreRouter, $"no core router ({o.Cause})");
        Live(n, ObjKind.CoreRouter, 27, 10);
        o = Outages.For(n).First(x => x.Hall == nh);
        Check(o.Cause == OutageCause.NoEdge && o.Place == ObjKind.EdgeSwitch24 && o.Racks == 10 && OutageUi.FixText(o).Contains("DH-01"), $"no edge switch: place one in DH-01 ({o.Cause})");
        Live(n, ObjKind.EdgeSwitch24, 5, 20);
        Check(!Outages.For(n).Any(x => x.Hall == nh), "an edge switch: every rack online");
        for (var i = 0; i < 20; i++) Live(n, ObjKind.Rack, 6 + i % 10, 10 + i / 10 * 3);
        o = Outages.For(n).FirstOrDefault(x => x.Hall == nh);
        Check(o != null && o.Cause == OutageCause.EdgeFull && o.Racks == 6 && OutageUi.Text(o).Contains("24"), $"30 racks on 24 ports: 6 offline, place an extra edge switch ({o?.Cause}, {o?.Racks})");

        // every cause has an English and Dutch text and fix
        foreach (var key in new[] { "outage_nohall", "outage_nopdu", "outage_pdu_unfed", "outage_pdu_unfed_full", "outage_pdufull", "outage_chain", "outage_blackout", "outage_heat_crah", "outage_heat_reject", "outage_suppressed",
                     "outage_nocarrier", "outage_nommr", "outage_nocore", "outage_noedge", "outage_nouplink", "outage_edgefull" })
            Check(Loc.T(key) != key, "text for " + key);
    }
}
