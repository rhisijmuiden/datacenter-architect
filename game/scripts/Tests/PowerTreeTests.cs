using System.Linq;
using DatacenterArchitect.Core;
using DatacenterArchitect.Sim;

namespace DatacenterArchitect.Tests;

/// <summary>Infra sketch boards 1 and 2: PDU sizes with rack connections, A/B sides, MSB outputs and the power tree.</summary>
public static partial class TestRunner
{
    static void PowerTreeTests()
    {
        Check(Catalog.Objects[ObjKind.Pdu75].Outlets == 12 && Catalog.Objects[ObjKind.Pdu].Outlets == 24 && Catalog.Objects[ObjKind.Pdu300].Outlets == 42, "PDU 75/150/300 have 12/24/42 rack connections");
        Check(Catalog.Objects[ObjKind.Pdu300].CapacityKw == 300 && Catalog.Objects[ObjKind.Switchgear].Outputs == 8 && Catalog.Objects[ObjKind.Switchgear].CapacityKw == 4000, "PDU 300 is 300 kW, an MSB 4000 kW with 8 outputs");

        // ---- outlets limit a hall ----
        var (s, hall, _, _) = PowerSite();
        var msb = Live(s, ObjKind.Switchgear, 24, 4);
        var ups = Live(s, ObjKind.Ups, 29, 4);
        var a = Live(s, ObjKind.Pdu75, 22, 20);
        Check(a.Feed == FeedSide.A, "the first PDU in a hall goes on the A side");
        var racks = Enumerable.Range(0, 14).Select(i => Live(s, ObjKind.Rack, 6 + i % 10, 6 + i / 10 * 3)).ToList();
        var r = Infra.Analyze(s);
        Check(r.RunningRacks == 12 && r.DownForPower == 2, $"a PDU 75 feeds 12 racks ({r.RunningRacks})");
        Check(r.Reason(racks[^1].Id) == DownReason.PduFull && r.Reason(racks[0].Id) == DownReason.None, "the 13th and 14th rack are off because the PDU is full");
        var node = r.Tree.Node(a);
        Check(node.Full && node.UsedOutlets == 12 && node.Parent?.Obj == ups && node.Parent.Parent?.Obj == msb, "the PDU hangs on the UPS, the UPS on the MSB");
        Check(UI.PowerPanel.Tips(s, r, node).Count > 0 && PowerPanelAdvice(s).Contains("advice_pdu_full"), "a full PDU gives a tip");
        var b = Live(s, ObjKind.Pdu75, 22, 18);
        Check(b.Feed == FeedSide.B, "the second PDU goes on the B side by itself");
        r = Infra.Analyze(s);
        var hi = r.Halls[0];
        Check(r.RunningRacks == 14 && hi.FedRacks == 14 && hi.DualRacks == 10 && hi.Redundancy == "partial" && !hi.AB, $"two PDU 75s: all 14 run, 10 of them A+B ({hi.DualRacks})");
        Check(r.Tree.Node(a).FailoverKw == 12 * 6 && System.Math.Abs(r.Tree.Node(a).LoadKw - (2 * 6 + 10 * 3)) < 1e-6, "a rack on two PDUs draws half from each; each side is sized for the full load");
        var (fa, fb) = r.Tree.Feeds(racks[0].Id);
        Check(fa != null && fb != null && fa.Side != fb.Side, "a dual-fed rack has one A and one B feed");
        s.Objects.Remove(racks[^1]); s.Objects.Remove(racks[^2]); s.Objects.Remove(racks[^3]); s.Objects.Remove(racks[^4]); s.Touch();
        r = Infra.Analyze(s);
        Check(r.Halls[0].AB && r.Halls[0].Redundancy == "A+B", "10 racks on two PDU 75s: every rack A+B, but both sides on one MSB");
        // a second switchboard for the B side gives real 2N
        var msbB = Live(s, ObjKind.Switchgear, 26, 4);
        var upsB = Live(s, ObjKind.Ups, 31, 4);
        Check(msbB.Feed == FeedSide.B && upsB.Feed == FeedSide.B, "the second MSB and UPS go on the B side");
        r = Infra.Analyze(s);
        Check(r.Halls[0].Redundancy == "2N" && r.Tree.Node(b).Root.Obj == msbB && r.Tree.Node(a).Root.Obj == msb, "A side via MSB-A, B side via MSB-B: 2N");
        Check(r.Tree.Node(msb).Name == "MSB-A1" && r.Tree.Node(msbB).Name == "MSB-B1" && r.Tree.Node(b).Name == "PDU-B1", "devices are named by side");
        Check(s.SwitchFeed(b) == null && b.Feed == FeedSide.A && !Infra.Analyze(s).Halls[0].AB, "moving the B PDU to side A loses A+B");
        s.SwitchFeed(b);

        // ---- a PDU feeds only its own hall ----
        var hall2 = s.AddRoom(RoomType.DataHall, 24, 12, 10, 12, 0); hall2.Stage = Stage.Done;
        Live(s, ObjKind.Pdu300, 22, 16);
        var other = Live(s, ObjKind.Rack, 27, 16);
        r = Infra.Analyze(s);
        Check(other != null && r.Down.Contains(other.Id) && r.Reason(other.Id) == DownReason.NoPdu, "a rack in the next hall gets nothing from this hall's PDUs");
        Check(s.HallCode(hall) == "DH-01" && s.HallCode(hall2) == "DH-02", "halls have a code");

        // ---- switchboard outputs ----
        var (t, _, _, _) = PowerSite();
        LiveRaw(t, ObjKind.Switchgear, 24, 4);
        for (var i = 0; i < 9; i++) LiveRaw(t, ObjKind.Ups, 29 + i % 2 * 2, 4 + i / 2);
        r = Infra.Analyze(t);
        Check(r.Tree.Msbs[0].UsedOutputs == 8 && r.Tree.Unconnected.Count == 1 && r.Link(PowerLink.Ups).CapacityKw == 8 * 500, "an MSB has 8 outputs: the 9th UPS is not connected");
        Check(PowerPanelAdvice(t).Contains("advice_msb_outputs"), "advice: place another MSB");
        // a UPS has 6 outputs; more PDUs fall back to the switchboard directly (no UPS behind them)
        var (u, _, _, _) = PowerSite();
        LiveRaw(u, ObjKind.Switchgear, 24, 4); LiveRaw(u, ObjKind.Ups, 29, 4);
        for (var i = 0; i < 7; i++) LiveRaw(u, ObjKind.Pdu, 6 + i * 2, 20);
        r = Infra.Analyze(u);
        Check(r.Tree.Ups[0].UsedOutputs == 6 && r.Tree.Sources.Count(n => n.Direct) == 1 && PowerPanelAdvice(u).Contains("advice_pdu_direct"), "the 7th PDU hangs on the MSB without a UPS");

        // ---- older saves ----
        var (o, _, _, _) = PowerSite();
        foreach (var x in new[] { 6, 8, 10 }) LiveRaw(o, ObjKind.Pdu, x, 20);
        LiveRaw(o, ObjKind.Ups, 29, 4); LiveRaw(o, ObjKind.Ups, 31, 4); LiveRaw(o, ObjKind.Switchgear, 24, 4);
        foreach (var x in o.Objects) x.Feed = FeedSide.A;
        o.SaveFormat = 9; o.Notes.Clear();
        o.Migrate();
        var pd = o.Objects.Where(x => x.Kind == ObjKind.Pdu).OrderBy(x => x.Id).Select(x => x.Feed).ToList();
        var us = o.Objects.Where(x => x.Kind == ObjKind.Ups).OrderBy(x => x.Id).Select(x => x.Feed).ToList();
        Check(pd.SequenceEqual(new[] { FeedSide.A, FeedSide.B, FeedSide.A }) && us.SequenceEqual(new[] { FeedSide.A, FeedSide.B }) && o.Objects.First(x => x.Kind == ObjKind.Switchgear).Feed == FeedSide.A, "old saves: PDUs and UPS units alternate A, B");
        Check(o.SaveFormat == GameState.CurrentFormat && o.Notes.Any(n => n.key == "msg_power_tree_new"), "old saves are told about the power tree");
        // the side survives a save
        o.Objects.First(x => x.Kind == ObjKind.Switchgear).Feed = FeedSide.B;
        SaveSystem.Save(o, "unit-test-power");
        var l = SaveSystem.Load("unit-test-power");
        Check(l != null && l.Objects.First(x => x.Kind == ObjKind.Switchgear).Feed == FeedSide.B && l.Objects.Count(x => x.Kind == ObjKind.Pdu && x.Feed == FeedSide.B) == 1, "feed sides are saved");
        SaveSystem.Delete("unit-test-power");
    }
}
