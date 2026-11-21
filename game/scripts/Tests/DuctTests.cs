using System.Linq;
using DatacenterArchitect.Core;
using DatacenterArchitect.Sim;
using DatacenterArchitect.UI;

namespace DatacenterArchitect.Tests;

/// <summary>Own main fiber duct (infra sketch board 5): entries on the border, ducts with corners and handholes, routing, old saves.</summary>
public static partial class TestRunner
{
    static void DuctTests()
    {
        // geometry: an L has one corner; cells run without doubles
        var l = FiberDuct.L(2, 2, 6, 5, false, 7);
        Check(l.Points.SequenceEqual(new[] { 2, 2, 6, 2, 6, 5 }) && l.Length == 8 && l.Cells().Distinct().Count() == 8, "L duct: x first, then z");
        Check(FiberDuct.L(2, 2, 2, 9, true, 4).Corners == 0 && FiberDuct.L(2, 2, 2, 9, true, 4).Handholes().Count == 2, "a straight duct: no corners, two handholes");

        // routing over shared ducts: a trunk fills up
        var (s, _, _) = NetSite();
        Live(s, ObjKind.Odf, 25, 10); Live(s, ObjKind.CoreRouter, 27, 10);
        var e1 = Live(s, ObjKind.FiberPit, 30, 62);
        var e2 = Live(s, ObjKind.FiberPit, 40, 63);
        // both entries feed one 4-way trunk to the building
        Check(s.AddDuct(FiberDuct.L(30, 61, 40, 61, false, 7)) == null, "a 7-way collector along the road");
        Check(s.AddDuct(FiberDuct.L(40, 62, 40, 62, false, 7)) == null, "a one-metre duct from the second entry");
        var trunk = FiberDuct.L(33, 60, 33, 24, false, 4);
        Check(s.AddDuct(trunk) == null, "a 4-way trunk to the datacenter");
        var n = s.Net;
        Check(n.Entries.All(e => e.Connected) && n.Link(NetLink.Ducts).Capacity == 4, $"four subducts through the trunk ({n.Link(NetLink.Ducts).Capacity})");
        for (var c = 0; c < 4; c++) Check(s.OrderCarrier(c, 10) == null, $"carrier {c} gets a subduct");
        Check(s.CanOrderCarrier(4, 10) == "msg_duct_full", "the fifth finds the trunk full");
        n = s.Net;
        Check(n.Entries[0].Used == 2 && n.Entries[1].Used == 2 && n.Routes == 1 && n.Link(NetLink.Ducts).Redundancy == "N", $"connections spread over both entries, but over one trunk ({n.Entries[0].Used}/{n.Entries[1].Used}, routes {n.Routes}, {string.Join(",", n.Spofs)})");
        Check(NetworkPanel.Advice(s, n).Contains("net_advice_ducts"), "advice: ducts are full");
        Check(s.UpgradeDuct(trunk) == null && s.CanOrderCarrier(4, 10) == null, "a 7-way trunk takes more");
        // a cut cable entry drops its connections onto the other entry while room lasts
        e1.Broken = true; s.Touch(false);
        n = s.Net;
        Check(n.Entries[1].Used == 4 && n.Carriers.All(x => x.Routed), $"a cut entry: the other takes over ({n.Entries[1].Used}, {n.Carriers.Count(x => x.Routed)}/{n.Carriers.Count})");
        e1.Broken = false; s.Touch(false);

        // undo and redo of a duct
        var h = new UndoHistory();
        var before = UndoHistory.Take(s);
        var extra = FiberDuct.L(2, 40, 2, 30, false, 4);
        s.AddDuct(extra);
        Check(h.Commit(s, before, "duct_4way") && h.Undo(s) != null && !s.Ducts.Contains(extra) && h.Redo(s) != null && s.Ducts.Contains(extra), "undo and redo a duct");

        // saves keep ducts; land bought later shifts them along
        SaveSystem.Save(s, "unit-test-ducts");
        var ld = SaveSystem.Load("unit-test-ducts");
        Check(ld != null && ld.Ducts.Count == s.Ducts.Count && ld.Ducts.Select(d => d.Length).SequenceEqual(s.Ducts.Select(d => d.Length)) && ld.Net.Entries.All(e => e.Connected), "save roundtrip ducts");
        var len = ld.Ducts.Sum(d => d.Length);
        ld.Grow(ld.PlotSize + 8);
        Check(ld.Net.Entries.All(e => e.Connected) && ld.Ducts.Sum(d => d.Length) == len, "growing the plot moves the ducts along");

        // old saves (format 12): street pits are gone, fiber pits become entries with a free duct to the MMR
        var (o, _, _) = NetSite();
        Live(o, ObjKind.Odf, 25, 10);
        var old1 = Live(o, ObjKind.FiberPit, 30, 62); old1.Level = 2;
        Live(o, ObjKind.FiberPit, 52, 62);
        o.Contracts.Add(new CarrierContract { Carrier = 1, PortGbps = 10 });
        o.Ducts.Clear(); o.SaveFormat = 12; o.Notes.Clear();
        o.Migrate();
        var on = o.Net;
        Check(o.Ducts.Count == 2 && o.Ducts.Select(d => d.Ways).OrderBy(w => w).SequenceEqual(new[] { 4, 12 }) && on.Entries.All(e => e.Connected), "old pits get ducts as wide as their subducts");
        Check(on.Carriers.All(x => x.Routed) && o.Notes.Any(x => x.key == "msg_ducts_new") && o.SaveFormat == GameState.CurrentFormat, "nothing that ran stops, with a note");
        Check(o.Ducts.All(d => d.Cells().All(c => o.BuildingAt(c.x, c.z) == null)), "migrated ducts go around buildings");

        // the demo site: two entries on two sides, two separate routes
        var demo = Demo.Build();
        var dn = demo.Net;
        Check(dn.Entries.Count == 2 && dn.Entries.All(e => e.Connected) && dn.Carriers.All(x => x.Routed) && dn.Routes == 2, $"demo: two entries, two routes ({dn.Routes})");
    }
}
