using System;
using System.Linq;

namespace DatacenterArchitect.Sim;

/// <summary>Network actions and the network's clock: carrier ports, commits and burst billing, peering and cross-connects.</summary>
public partial class GameState
{
    bool _warnOffline, _warnCongested, _warnNoc;

    public CarrierContract ContractOf(int carrier) => Contracts.FirstOrDefault(k => k.Carrier == carrier);

    /// <summary>Free patch ports on the ODFs in the meet-me room.</summary>
    public int FreeOdfPorts() { var n = Net; return n.OdfPorts - n.OdfUsed; }

    /// <summary>
    /// New games (and older saves from now on) need a carrier rack in the MMR for every carrier and every internet exchange.
    /// Contracts ordered before the rule keep working without one.
    /// </summary>
    public bool CarrierRackRule { get; set; }

    /// <summary>Carrier racks standing (or on their way) in a meet-me room that no carrier or IX uses yet.</summary>
    public int FreeCarrierRacks()
    {
        var racks = Objects.Count(o => o.Kind == ObjKind.CarrierRack && Rooms.Any(r => r.Type == RoomType.MeetMe && r.Floor == o.Floor && r.Contains(o.X, o.Z)));
        return racks - Contracts.Count(k => !k.NoRackNeeded);
    }

    public string CanOrderCarrier(int carrier, int portGbps)
    {
        if (carrier < 0 || carrier >= Catalog.Carriers.Length) return "msg_nothing_changed";
        if (ContractOf(carrier) != null) return "msg_carrier_exists";
        if (!Has(Exec.Network)) return "msg_needs_netmgr";
        if (!Network.MmrReady(this)) return "msg_needs_mmr";
        var def = Catalog.Carriers[carrier];
        if (!Network.FreeSubduct(this, def.Pit)) return Net.Pits.Any(p => p.Street == def.Pit) ? "msg_duct_full" : "msg_needs_pit";
        if (FreeOdfPorts() <= 0) return "msg_odf_full";
        if (CarrierRackRule && FreeCarrierRacks() <= 0) return "msg_needs_carrier_rack";
        if (!CanAfford(Catalog.Port(portGbps).SetupFee)) return "msg_no_money";
        return null;
    }

    /// <summary>An extra port of the same size at an internet exchange (one more fiber pair; traffic over it is free).</summary>
    public string CanAddIxPort(CarrierContract k)
    {
        if (k == null || !Catalog.Carriers[k.Carrier].Ix) return "msg_nothing_changed";
        if (!k.Active || k.UpgradeTo != 0) return "msg_upgrade_busy";
        if (k.Ports >= Catalog.MaxIxPorts) return "msg_max_level";
        if (FreeOdfPorts() <= 0) return "msg_odf_full";
        if (!CanAfford(Catalog.Port(k.PortGbps).SetupFee)) return "msg_no_money";
        return null;
    }

    public string AddIxPort(CarrierContract k)
    {
        var why = CanAddIxPort(k);
        if (why != null) return why;
        Spend(Catalog.Port(k.PortGbps).SetupFee);
        k.Ports = Math.Max(1, k.Ports) + 1;
        Notes.Add(("msg_ix_port_added", $"{Catalog.Carriers[k.Carrier].Name}¦{k.Ports}"));
        Touch(false);
        return null;
    }

    /// <summary>Hand back one port at an internet exchange (the last one cancels the contract).</summary>
    public void RemoveIxPort(CarrierContract k)
    {
        if (k == null || !Catalog.Carriers[k.Carrier].Ix) return;
        if (k.Ports <= 1) { CancelCarrier(k); return; }
        k.Ports--;
        Touch(false);
    }

    public string OrderCarrier(int carrier, int portGbps)
    {
        var why = CanOrderCarrier(carrier, portGbps);
        if (why != null) return why;
        Spend(Catalog.Port(portGbps).SetupFee);
        var k = new CarrierContract
        {
            Carrier = carrier, PortGbps = Catalog.Port(portGbps).Gbps, Ports = 1,
            Commit = Catalog.Carriers[carrier].Ix ? 0 : Math.Min(1, portGbps),
            DaysLeft = Creative.InstantBuild ? 0 : Catalog.NewPortDays,
        };
        Contracts.Add(k);
        if (k.Active) Notes.Add(("msg_carrier_live", Catalog.Carriers[carrier].Name));
        Touch(false);
        return null;
    }

    public static int NextPort(int gbps)
    {
        var i = Array.FindIndex(Catalog.Ports, p => p.Gbps == gbps);
        return i >= 0 && i + 1 < Catalog.Ports.Length ? Catalog.Ports[i + 1].Gbps : 0;
    }

    public string CanUpgradePort(CarrierContract k)
    {
        if (k == null) return "msg_nothing_changed";
        if (k.UpgradeTo != 0 || !k.Active) return "msg_upgrade_busy";
        var next = NextPort(k.PortGbps);
        if (next == 0) return "msg_max_level";
        if (!CanAfford(Catalog.Port(next).SetupFee)) return "msg_no_money";
        return null;
    }

    public string UpgradePort(CarrierContract k)
    {
        var why = CanUpgradePort(k);
        if (why != null) return why;
        var next = NextPort(k.PortGbps);
        Spend(Catalog.Port(next).SetupFee);
        if (Creative.InstantBuild) k.PortGbps = next;
        else { k.UpgradeTo = next; k.UpgradeDaysLeft = Catalog.PortUpgradeDays; }
        Touch(false);
        return null;
    }

    /// <summary>Raise or lower the commit one step; never above the port size.</summary>
    public void StepCommit(CarrierContract k, int dir)
    {
        if (k == null || Catalog.Carriers[k.Carrier].Ix) return;
        var steps = Catalog.Commits;
        var i = Array.FindIndex(steps, x => x >= k.Commit - 1e-9);
        if (i < 0) i = steps.Length - 1;
        i = Math.Clamp(i + Math.Sign(dir), 0, steps.Length - 1);
        if (steps[i] > k.PortGbps) return;
        k.Commit = steps[i];
        Touch(false);
    }

    public void CancelCarrier(CarrierContract k)
    {
        if (k == null) return;
        Contracts.Remove(k);
        Touch(false);
    }

    // ---------------- peering ----------------
    public PeeringDeal PeerOf(int rival) => Peers.FirstOrDefault(p => p.Rival == rival);

    /// <summary>Is this rival in the game (AI competitors on, and not bought or bankrupt)?</summary>
    public bool RivalOn(int rival)
    {
        if (!Options.AiCompetitors || rival < 0 || rival >= Catalog.Rivals.Length) return false;
        return Competitors.Count == 0 ? rival < 3 : Competitors.Any(c => c.Id == rival && !c.Gone);
    }

    /// <summary>One-off cost of a peering: the rival's setup cost plus the port (a 10G port is included).</summary>
    public static long PeerSetupCost(int rival, int port) => Catalog.Rivals[rival].SetupCost + (port > 10 ? Catalog.Port(port).SetupFee : 0);

    /// <summary>Monthly money of a peering: a smaller rival rents a rack in your MMR (plus), a bigger one charges per 10 Gbit/s (minus).</summary>
    public static double PeerMonthly(int rival, double capacity)
    {
        var m = Catalog.Rivals[rival].Monthly;
        return m >= 0 ? m : m * Math.Max(1, capacity / 10);
    }

    public string CanPeer(int rival, bool proposal)
    {
        if (!Options.AiCompetitors) return "msg_no_rivals";
        if (!Has(Exec.Network)) return "msg_needs_netmgr";
        if (!RivalOn(rival)) return "msg_nothing_changed";
        var p = PeerOf(rival);
        if (p != null && p.State is PeerState.Building or PeerState.Active or PeerState.Asking) return "msg_nothing_changed";
        if (p != null && p.State == PeerState.Declined && p.DaysLeft > 0) return "msg_peer_wait";
        if (!Network.MmrReady(this)) return "msg_needs_mmr";
        if (!Network.FreeSubduct(this, -1)) return Net.Pits.Count == 0 ? "msg_needs_pit" : "msg_duct_full";
        if (FreeOdfPorts() <= 0) return "msg_odf_full";
        if (proposal && Network.PeakGbps(Net) < Catalog.Rivals[rival].MinGbps * 0.5) return "msg_peer_too_small";
        return null;
    }

    /// <summary>Ask a rival for private peering on a port, with the capacity you want. It answers in a few days with what it allows.</summary>
    public string AskPeering(int rival, double asked, int port)
    {
        var why = CanPeer(rival, true);
        if (why != null) return why;
        if (Array.IndexOf(Catalog.PeerPorts, port) < 0 || asked <= 0) return "msg_nothing_changed";
        var p = PeerOf(rival);
        if (p == null) { p = new PeeringDeal { Rival = rival }; Peers.Add(p); }
        p.State = PeerState.Asking; p.DaysLeft = 0;
        p.Asked = Math.Min(asked, port); p.AskPort = port;
        p.AnswerDays = Creative.InstantBuild ? 0 : Catalog.PeerAnswerDays;
        if (p.AnswerDays <= 0) AnswerPeering(p);
        Touch(false);
        return null;
    }

    /// <summary>Ask for more capacity (or a bigger port) on a running peering.</summary>
    public string AskMorePeering(int rival, double asked, int port)
    {
        var p = PeerOf(rival);
        if (p == null || p.State != PeerState.Active) return "msg_nothing_changed";
        if (p.Upgrading) return "msg_upgrade_busy";
        port = Math.Max(port, p.Port);
        if (asked <= p.Capacity && port <= p.Port) return "msg_nothing_changed";
        p.Asked = Math.Min(asked, port); p.AskPort = port;
        p.AnswerDays = Creative.InstantBuild ? 0 : Catalog.PeerAnswerDays;
        if (p.AnswerDays <= 0) AnswerPeering(p);
        Touch(false);
        return null;
    }

    /// <summary>The rival answers: all you asked (the fiber is built straight away when you can pay), less (an offer you accept or not), or no.</summary>
    void AnswerPeering(PeeringDeal p)
    {
        var rv = Catalog.Rivals[p.Rival];
        var allow = Network.PeerAllowance(this, Net, p.Rival, p.Asked, p.AskPort);
        p.AnswerDays = 0;
        if (p.State == PeerState.Active)
        {
            // more capacity on a running peering: it simply grows when the rival allows more
            if (allow > p.Capacity || p.AskPort > p.Port)
            {
                var extra = p.AskPort > p.Port ? Catalog.Port(p.AskPort).SetupFee : 0;
                if (!CanAfford(extra)) { Notes.Add(("msg_no_money", null)); p.Asked = 0; return; }
                Spend(extra);
                p.PortGbps = Math.Max(p.Port, p.AskPort);
                p.Granted = Math.Max(p.Capacity, allow);
                Notes.Add(("msg_peer_more", $"{rv.Name}¦{p.Granted:0}"));
            }
            else Notes.Add(("msg_peer_more_no", rv.Name));
            p.Asked = 0;
            Touch(false);
            return;
        }
        if (allow <= 0)
        {
            p.State = PeerState.Declined; p.DaysLeft = Catalog.PeerRetryDays; p.Granted = 0;
            Notes.Add(("msg_peer_refused", rv.Name));
        }
        else
        {
            p.Granted = allow; p.PortGbps = p.AskPort;
            p.State = PeerState.Offered;
            if (allow >= p.Asked - 1e-9 && CanAfford(PeerSetupCost(p.Rival, p.Port)) && Build(p) == null) { }
            else Notes.Add(("msg_peer_offer", $"{rv.Name}¦{allow:0}¦{p.Asked:0}"));
        }
        Touch(false);
    }

    /// <summary>Pay the setup and build the dark fiber between the two MMRs.</summary>
    string Build(PeeringDeal p)
    {
        var why = CanPeer(p.Rival, false);
        if (why != null && why != "msg_nothing_changed") return why;
        var cost = PeerSetupCost(p.Rival, p.Port);
        if (!CanAfford(cost)) return "msg_no_money";
        Spend(cost);
        p.State = Creative.InstantBuild ? PeerState.Active : PeerState.Building;
        p.DaysLeft = Creative.InstantBuild ? 0 : Catalog.PeeringDays;
        p.Asked = 0;
        if (p.State == PeerState.Active) Notes.Add(("msg_peer_live", Catalog.Rivals[p.Rival].Name));
        else Notes.Add(("msg_peer_agreed", $"{Catalog.Rivals[p.Rival].Name}¦{p.Capacity:0}"));
        Touch(false);
        return null;
    }

    /// <summary>Accept a rival's request or its offer: the fiber is built at the capacity it allows.</summary>
    public string StartPeering(int rival)
    {
        var p = PeerOf(rival);
        if (p == null || p.State is not (PeerState.Requested or PeerState.Offered))
        {
            // an old-style proposal: ask for 10 Gbit/s on a 10G port
            var why = AskPeering(rival, 10, 10);
            if (why != null) return why;
            p = PeerOf(rival);
            return p.State == PeerState.Offered ? StartPeering(rival) : null;
        }
        if (p.Granted <= 0) { p.Granted = 10; p.PortGbps = 10; }
        return Build(p);
    }

    public void DeclinePeering(int rival)
    {
        var p = PeerOf(rival);
        if (p == null) { p = new PeeringDeal { Rival = rival }; Peers.Add(p); }
        p.State = PeerState.Declined; p.DaysLeft = 0; p.Asked = 0; p.AnswerDays = 0;
        Touch(false);
    }

    // ---------------- cross-connects ----------------
    /// <summary>An engineer finished patching a cross-connect in the MMR.</summary>
    public void CompleteCrossConnect()
    {
        if (XcPending <= 0) return;
        XcPending--; CrossConnects++;
        if (!Creative.UnlimitedMoney) Money += Catalog.XcSetupFee;
        if (CrossConnects == 1) Notes.Add(("msg_first_xc", null));
        Touch(false);
    }

    /// <summary>Networks in the MMR that customers can cross-connect to: carriers, the IX and peers.</summary>
    public int NetworksInMmr() => Net.Carriers.Count(x => x.Up) + Peers.Count(p => p.State == PeerState.Active);

    // ---------------- the clock ----------------
    /// <summary>Port deliveries, hourly traffic samples for the 95th percentile, and warnings when racks go offline or traffic does not fit.</summary>
    public void UpdateNetwork(double hours, int hourBefore)
    {
        foreach (var k in Contracts)
        {
            if (k.DaysLeft > 0)
            {
                k.DaysLeft -= hours / 24;
                if (k.DaysLeft <= 0 || Creative.InstantBuild) { k.DaysLeft = 0; Notes.Add(("msg_carrier_live", Catalog.Carriers[k.Carrier].Name)); Touch(false); }
            }
            if (k.UpgradeTo != 0)
            {
                k.UpgradeDaysLeft -= hours / 24;
                if (k.UpgradeDaysLeft <= 0 || Creative.InstantBuild)
                {
                    k.PortGbps = k.UpgradeTo; k.UpgradeTo = 0; k.UpgradeDaysLeft = 0;
                    Notes.Add(("msg_port_upgraded", Catalog.Carriers[k.Carrier].Name)); Touch(false);
                }
            }
        }
        foreach (var p in Peers.Where(p => p.State == PeerState.Declined && p.DaysLeft > 0)) p.DaysLeft = Math.Max(0, p.DaysLeft - hours / 24);
        foreach (var p in Peers.Where(p => p.AnswerDays > 0 && p.State is PeerState.Asking or PeerState.Active).ToList())
        {
            p.AnswerDays -= hours / 24;
            if (p.AnswerDays <= 0) AnswerPeering(p);
        }
        foreach (var p in Peers.Where(p => p.State == PeerState.Building))
        {
            p.DaysLeft -= hours / 24;
            if (p.DaysLeft <= 0 || Creative.InstantBuild) { p.State = PeerState.Active; p.DaysLeft = 0; Notes.Add(("msg_peer_live", Catalog.Rivals[p.Rival].Name)); Touch(false); }
        }
        var net = Net;
        // one sample per game hour for every contract in service
        for (var h = hourBefore + 1; h <= (int)Hours; h++)
        {
            var flows = Network.FlowsAt(this, net, h);
            foreach (var (k, g) in flows) k.Samples.Add((float)g);
        }
        if (!_warnOffline && net.OfflineRacks > 0 && !Creative.NoNetworkLimit) { _warnOffline = true; Notes.Add(("msg_net_offline", null)); }
        else if (_warnOffline && net.OfflineRacks == 0) _warnOffline = false;
        if (!_warnCongested && net.Served < 0.999 && net.OnlineRacks > 0) { _warnCongested = true; Notes.Add(("msg_net_congested", null)); }
        else if (_warnCongested && net.Served >= 0.999) _warnCongested = false;
        // the NOC sees the transit filling up before it overflows
        if (net.Noc && !_warnNoc && net.TransitCapacity > 0 && net.TransitRatio > 0.85 && net.Served >= 0.999) { _warnNoc = true; Notes.Add(("msg_noc_warning", null)); }
        else if (_warnNoc && net.TransitRatio < 0.75) _warnNoc = false;
    }

    /// <summary>Once a day: rivals ask to peer, customers order cross-connects, and every 30 days the burst above the commit is billed.</summary>
    public void NetworkDay(int day)
    {
        if (Options.AiCompetitors)
            foreach (var rv in Catalog.Rivals)
                if (day >= rv.RequestDay && PeerOf(rv.Id) == null && RivalOn(rv.Id) && Network.MmrReady(this))
                {
                    // the rival proposes a capacity that fits the traffic between you, on a 10G port
                    var offer = Math.Max(1, Network.StepDown(Math.Min(10, Network.PeakGbps(Net) * rv.Share * 2)));
                    Peers.Add(new PeeringDeal { Rival = rv.Id, State = PeerState.Requested, PortGbps = 10, Granted = offer });
                    Notes.Add(("msg_peer_request", rv.Name));
                    Touch(false);
                }
        var net = Net;
        var room = Math.Max(0, net.OdfPorts - net.External);
        if (CrossConnects > room) { CrossConnects = room; Touch(false); }
        // customers want cross-connects to the networks in your MMR
        var target = (int)Math.Floor(net.OnlineRacks * 0.12 * Math.Min(2.0, NetworksInMmr() / 2.0));
        var want = target - CrossConnects - XcPending;
        if (want > 0) XcPending += Math.Min(want, 3);
        if (day % 30 == 0)
        {
            foreach (var k in Contracts)
            {
                var bill = Network.BurstCost(k, net.Noc);
                if (bill >= 1 && !Creative.UnlimitedMoney)
                {
                    Money -= (long)Math.Round(bill); Book("cost_network", -(long)Math.Round(bill));
                    Notes.Add(("msg_burst_bill", Catalog.Carriers[k.Carrier].Name));
                }
                k.Samples.Clear();
            }
        }
    }

    /// <summary>Daily network money: ports and commits (minus), peering deals (either way) and cross-connect fees (plus).</summary>
    public double NetworkPerDay()
    {
        var sum = 0.0;
        var discount = (Has(Exec.Procurement) ? 0.9 - 0.025 * Training(Exec.Procurement) : 1) * NetworkCostFactor;
        foreach (var k in Contracts.Where(k => k.Active)) sum -= Network.MonthlyCost(k) * discount / 30;
        foreach (var p in Peers.Where(p => p.State == PeerState.Active)) sum += PeerMonthly(p.Rival, p.Capacity) / 30.0;
        sum += CrossConnects * Catalog.XcMonthlyFee / 30.0;
        return sum;
    }
}
