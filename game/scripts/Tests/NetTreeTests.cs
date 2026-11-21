using System.Linq;
using DatacenterArchitect.Sim;
using DatacenterArchitect.UI;

namespace DatacenterArchitect.Tests;

/// <summary>Network tree: edge switches per hall (ports = racks, own hall only, A/B), distribution switches and core ports.</summary>
public static partial class TestRunner
{
    static void NetTreeTests()
    {
        var (s, hall, _) = NetSite();
        var b2 = s.AddBuilding(BuildingKind.Datacenter, 4, 30, 20, 16, 1); b2.Stage = Stage.Done;
        var hall2 = s.AddRoom(RoomType.DataHall, 4, 30, 20, 16, 0); hall2.Stage = Stage.Done;
        Live(s, ObjKind.Odf, 25, 10); Entry(s, 30, 62);
        s.OrderCarrier(1, 10);
        var core = Live(s, ObjKind.CoreRouter, 27, 10);
        for (var i = 0; i < 10; i++) Live(s, ObjKind.Rack, 6 + i, 6);
        for (var i = 0; i < 30; i++) Live(s, ObjKind.Rack, 6 + i % 15, 32 + i / 15 * 3);

        // an edge switch only serves the racks of its own hall
        var e1 = Live(s, ObjKind.EdgeSwitch48, 5, 20);
        var n = s.Net;
        Check(n.HallOf(hall).Online == 10 && n.HallOf(hall2).Online == 0, "an edge switch in hall 1 leaves hall 2 offline");
        Check(n.Uplink[e1.Id] == 0 && n.DirectEdges.Contains(e1) && n.CorePortsUsed == 2, "without a distribution switch it hangs straight on the core");
        Check(n.Spofs.Contains("spof_spine") && n.HallOf(hall).Redundancy == "N" && !n.HallOf(hall).AB, "one switch: no B side");

        // ports are the racks you can connect
        Live(s, ObjKind.EdgeSwitch24, 5, 44);
        n = s.Net;
        Check(n.HallOf(hall2).Online == 24 && n.HallOf(hall2).Ports == 24 && n.HallOf(hall2).Redundancy == "N-", "an Edge 24 connects 24 of the 30 racks");
        Check(NetworkPanel.Advice(s, n).Contains("net_advice_spine_ports"), "advice: the hall needs more ports");

        // a second switch in the hall gives every rack an A and a B port
        Live(s, ObjKind.EdgeSwitch48, 6, 20);
        n = s.Net;
        var h1 = n.HallOf(hall);
        Check(h1.Dual == 10 && h1.AB && h1.Redundancy == "2N" && h1.UsedPorts == 20, $"A/B: 10 racks on two switches ({h1.Dual})");
        Check(h1.Switches.Sum(e => Network.EdgePortsUsed(n, e)) == h1.UsedPorts, "ports per switch add up");

        // the distribution switch stands in the MMR and takes one core port for all hall switches
        Check(s.CanPlace(new PlacedObject { Kind = ObjKind.DistSwitch, X = 6, Z = 10 }) != null, "a distribution switch does not go in a hall");
        var ds = Live(s, ObjKind.DistSwitch, 26, 12);
        n = s.Net;
        Check(ds != null && n.Dists.Count == 1 && n.Dists[0].Linked && n.Dists[0].Edges.Count == 3, "all three hall switches hang on the distribution switch");
        Check(n.Uplink.Values.All(u => u == ds.Id) && n.DirectEdges.Count == 0 && n.CorePortsUsed == 2, "one core port for the carrier, one for the distribution switch");
        Check(n.Spofs.Contains("spof_dist") && n.Link(NetLink.Distribution).Units == 1, "a single distribution switch is flagged");
        Check(n.Dists[0].Load > 0 && n.CoreList.Count == 1 && n.CoreList[0].Load > 0, "traffic shows on the distribution switch and the core");

        // core ports run out without a distribution switch: switches beyond the free core ports get no uplink
        var (c, ch, _) = NetSite();
        Live(c, ObjKind.Odf, 25, 10); Entry(c, 30, 62); c.OrderCarrier(1, 10);
        Live(c, ObjKind.CoreRouter, 27, 10);
        for (var i = 0; i < 40; i++) LiveRaw(c, ObjKind.EdgeSwitch24, 5 + i % 18, 6 + i / 18 * 2);
        n = c.Net;
        Check(n.DirectEdges.Count == 31 && n.Uplink.Values.Count(u => u < 0) == 9 && n.CorePortsUsed == 32, "32 core ports: 1 carrier and 31 edge switches");
        Check(NetworkPanel.Advice(c, n).Contains("ntree_advice_uplink"), "advice: add a distribution switch");
        Live(c, ObjKind.DistSwitch, 26, 12);
        n = c.Net;
        Check(n.Uplink.Values.All(u => u > 0) && n.CorePortsUsed == 2 && n.HallOf(ch).Spines == 40, "a distribution switch connects all 40");

        // the tree's text for the inspector
        Check(NetworkPanel.InspectLines(s, e1).Count == 2 && NetworkPanel.InspectLines(s, ds).Count == 1 && NetworkPanel.InspectLines(s, core).Count == 1, "inspector lines for switches and routers");
        Check(Catalog.Objects[ObjKind.Spine].Ports == 96 && Catalog.IsHallSwitch(ObjKind.Spine) && !Catalog.IsHallSwitch(ObjKind.DistSwitch), "the spine is the big hall switch");

        // old saves: spines stay, a note points to the tree
        var old = Quiet();
        old.Objects.Add(new PlacedObject { Id = old.NextId++, Kind = ObjKind.Spine, X = 1, Z = 1 });
        old.SaveFormat = 9; old.Notes.Clear(); old.Migrate();
        Check(old.SaveFormat == GameState.CurrentFormat && old.Notes.Any(x => x.key == "msg_nettree_new") && old.Objects.Any(o => o.Kind == ObjKind.Spine), "old saves keep their spines and learn about the tree");
        // the new kinds survive a save roundtrip
        var json = System.Text.Json.JsonSerializer.Serialize(s);
        var back = System.Text.Json.JsonSerializer.Deserialize<GameState>(json);
        Check(back.Objects.Count(o => o.Kind == ObjKind.EdgeSwitch48) == 2 && back.Objects.Any(o => o.Kind == ObjKind.DistSwitch), "edge and distribution switches are saved");
    }
}
