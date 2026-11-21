using System;
using System.Linq;
using DatacenterArchitect.Core;
using DatacenterArchitect.Sim;
using DatacenterArchitect.UI;

namespace DatacenterArchitect.Tests;

/// <summary>Several internet exchanges with ports (free traffic), a carrier rack per carrier/IX, and private peering on request with a capacity.</summary>
public static partial class TestRunner
{
    static void PeeringIxTests()
    {
        // ---- internet exchanges: ports, free traffic, several exchanges, a carrier rack each ----
        var (s, _, _) = NetSite();
        for (var i = 0; i < 50; i++) Live(s, ObjKind.Rack, 6 + i % 16, 6 + i / 16 * 3);
        Live(s, ObjKind.Odf, 25, 10); Live(s, ObjKind.CoreRouter, 27, 10);
        Live(s, ObjKind.EdgeSwitch48, 5, 20); Live(s, ObjKind.EdgeSwitch48, 6, 20);
        Live(s, ObjKind.FiberPit, 30, 62); Live(s, ObjKind.FiberPit, 52, 62);
        Check(Catalog.Carriers.Count(c => c.Ix) == 3, "three internet exchanges in the region");
        Check(s.OrderCarrier(1, 10) == null && s.OrderCarrier(4, 10) == null, "transit and a port at Polder-IX");
        var n = s.Net;
        var one = n.IxGbps;
        Check(one > 0 && n.IxFlows.ContainsKey(4) && Network.MonthlyCost(s.ContractOf(4)) == Catalog.Port(10).MonthlyFee, "the IX carries traffic and charges only the port");
        s.StepCommit(s.ContractOf(4), 1);
        Check(s.ContractOf(4).Commit == 0, "an IX has no commit");
        var pairs = n.External;
        Check(s.AddIxPort(s.ContractOf(4)) == null && s.ContractOf(4).Ports == 2 && s.ContractOf(4).TotalGbps == 20, "a second port at the same IX");
        n = s.Net;
        Check(n.External == pairs + 1 && n.IxPort == 20, "a second IX port is one more fiber pair and doubles the bandwidth");
        Check(Network.MonthlyCost(s.ContractOf(4)) == 2 * Catalog.Port(10).MonthlyFee, "two ports, two port fees");
        Check(s.OrderCarrier(6, 10) == null, "a port at a second exchange (Hanze-IX)");
        n = s.Net;
        Check(n.IxFlows.Count == 2 && n.IxGbps > one, $"two exchanges reach more of the traffic ({n.IxGbps:0.00} > {one:0.00})");
        Check(n.IxGbps < n.DemandGbps * (Network.IxReach(s, Catalog.Carriers[4]) + Network.IxReach(s, Catalog.Carriers[6])), "their members overlap: less than the plain sum");
        s.ContractOf(6).PortGbps = 1; s.Touch(false);
        n = s.Net;
        Check(n.IxFlows[6] <= 1 + 1e-9, "an IX never carries more than its ports");
        s.RemoveIxPort(s.ContractOf(4));
        Check(s.ContractOf(4).Ports == 1, "hand back a port");
        // carrier racks: one per carrier and IX
        Check(n.CarrierRacks == 4 && n.CarrierRacksUsed == 3 && s.FreeCarrierRacks() == 1, "three of the four carrier racks are in use");
        foreach (var o in s.Objects.Where(o => o.Kind == ObjKind.CarrierRack).Take(3).ToList()) s.Objects.Remove(o);
        s.Touch();
        n = s.Net;
        Check(n.Carriers.Count(c => !c.Racked) == 2 && n.Carriers.Count(c => c.Up) == 1, "without its rack a carrier carries nothing");
        Check(NetworkPanel.Advice(s, n).Contains("net_advice_carrier_rack"), "advice: place a carrier rack");
        Check(s.CanOrderCarrier(5, 10) is "msg_needs_carrier_rack" or "msg_needs_pit", "no free rack, no new carrier");

        // old saves: what is ordered keeps working without a rack
        var old = new GameState { Options = new NewGameOptions { AiCompetitors = false } };
        old.Contracts.Add(new CarrierContract { Carrier = 4, PortGbps = 10, Commit = 0 });
        old.Migrate();
        Check(old.CarrierRackRule && old.Contracts[0].NoRackNeeded && old.Contracts[0].Ports == 1 && old.Notes.Any(x => x.key == "msg_carrier_racks_new"), "older saves keep their carriers without racks");

        // ---- private peering on request: port, capacity, and what the rival allows ----
        var (a, _, _) = NetSite(ai: true, instant: false);
        a.Creative.NoNetworkLimit = true;
        Live(a, ObjKind.Odf, 25, 10); Live(a, ObjKind.FiberPit, 30, 62);
        Check(a.CanPeer(0, true) == "msg_peer_too_small", "without traffic a rival is not interested");
        for (var i = 0; i < 96; i++) Live(a, ObjKind.Rack, 6 + i % 16, 6 + i / 16 * 3);
        n = a.Net;
        Check(n.OnlineRacks == 96, "96 racks online");
        Check(a.CanPeer(0, true) == null, "now you can ask");
        Check(a.AskPeering(0, 50, 100) == null && a.PeerOf(0).State == PeerState.Asking && a.PeerOf(0).AnswerDays == Catalog.PeerAnswerDays, "ask Zuidas Colo for 50 Gbit/s on a 100G port");
        var allow = Network.PeerAllowance(a, a.Net, 0, 50, 100);
        Check(allow == 10, $"they allow about four times the traffic between you ({allow})");
        a.Advance(24 * (Catalog.PeerAnswerDays + 0.5));
        var p = a.PeerOf(0);
        Check(p.State == PeerState.Offered && p.Granted == 10 && p.Asked == 50 && a.Notes.Any(x => x.key == "msg_peer_offer"), "an offer of 10 of the 50 Gbit/s asked");
        var money = a.Money;
        Check(a.StartPeering(0) == null && p.State == PeerState.Building && p.DaysLeft == Catalog.PeeringDays, "accepting lays the dark fiber");
        a.Advance(24 * (Catalog.PeeringDays + 1));
        n = a.Net;
        Check(p.State == PeerState.Active && p.Port == 100 && p.Capacity == 10, "peering live on a 100G port at 10 Gbit/s");
        Check(n.PeerFlows.TryGetValue(0, out var f0) && f0 > 0 && f0 <= 10 + 1e-9 && n.Link(NetLink.Upstream).Capacity >= 10, "traffic stays within the capacity allowed");
        // asking just what they allow: agreed straight away
        Check(a.AskPeering(2, 5, 10) == null, "ask Noordpoort Data for 5 Gbit/s");
        a.Advance(24 * (Catalog.PeerAnswerDays + 0.5));
        Check(a.PeerOf(2).State is PeerState.Building or PeerState.Active && a.PeerOf(2).Capacity == 5 && a.Notes.Any(x => x.key == "msg_peer_agreed"), "all you asked: agreed and built");
        // more capacity later, when the network grows and the reputation is good
        Check(a.AskMorePeering(0, 40, 100) == null && a.PeerOf(0).Upgrading, "ask for more capacity");
        a.Advance(24 * (Catalog.PeerAnswerDays + 0.5));
        Check(a.PeerOf(0).Capacity == 10 && a.Notes.Any(x => x.key == "msg_peer_more_no"), "not yet: same traffic, same capacity");
        var b2 = a.AddBuilding(BuildingKind.Datacenter, 4, 30, 20, 16, 1); b2.Stage = Stage.Done;
        a.AddRoom(RoomType.DataHall, 4, 30, 20, 16, 0).Stage = Stage.Done;
        for (var i = 0; i < 80; i++) Live(a, ObjKind.Rack, 6 + i % 16, 32 + i / 16 * 3);
        a.Reputation = 70;
        Check(a.AskMorePeering(0, 40, 100) == null, "ask again with a bigger network");
        a.Advance(24 * (Catalog.PeerAnswerDays + 0.5));
        Check(a.PeerOf(0).Capacity == 20 && a.Notes.Any(x => x.key == "msg_peer_more"), $"they raise it to 20 Gbit/s ({a.PeerOf(0).Capacity})");
        // a refusal waits before you can ask again
        var (t, _, _) = NetSite(ai: true);
        t.Creative.NoNetworkLimit = true;
        Live(t, ObjKind.Odf, 25, 10); Live(t, ObjKind.FiberPit, 30, 62);
        for (var i = 0; i < 10; i++) Live(t, ObjKind.Rack, 6 + i, 6);
        t.Peers.Add(new PeeringDeal { Rival = 1, State = PeerState.Asking, Asked = 10, AskPort = 10, AnswerDays = 0.01 });
        t.UpdateNetwork(1, (int)t.Hours);
        Check(t.PeerOf(1).State == PeerState.Declined && t.PeerOf(1).DaysLeft == Catalog.PeerRetryDays && t.CanPeer(1, true) == "msg_peer_wait", "Rivierhaven says no to a small network, ask again in 30 days");
        // a rival's own request comes with a capacity that fits
        t.NetworkDay(Catalog.Rivals[0].RequestDay);
        Check(t.PeerOf(0)?.State == PeerState.Requested && t.PeerOf(0).Capacity >= 1 && t.PeerOf(0).Capacity <= 10, "a rival's request names its capacity");
        Check(t.StartPeering(0) == null && t.PeerOf(0).State == PeerState.Active, "accept it (instant build)");
        // money: a bigger rival charges per 10 Gbit/s
        Check(GameState.PeerMonthly(4, 20) == 2 * Catalog.Rivals[4].Monthly && GameState.PeerMonthly(0, 20) == Catalog.Rivals[0].Monthly, "bigger rivals charge per 10 Gbit/s, smaller ones rent a rack");
        Check(GameState.PeerSetupCost(0, 100) == Catalog.Rivals[0].SetupCost + Catalog.Port(100).SetupFee, "a bigger port costs extra");
        // savegame
        SaveSystem.Save(a, "unit-test-peering");
        var l = SaveSystem.Load("unit-test-peering");
        Check(l != null && l.PeerOf(0).Capacity == 20 && l.PeerOf(0).Port == 100 && l.PeerOf(2).Capacity == 5 && l.CarrierRackRule, "save roundtrip peering");
        // older peering deals: a 10G port at 10 Gbit/s
        Check(new PeeringDeal { State = PeerState.Active }.Capacity == 10, "older deals keep 10 Gbit/s");
        _ = money;
    }
}
