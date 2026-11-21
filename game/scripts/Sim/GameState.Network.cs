using System;
using System.Linq;

namespace DatacenterArchitect.Sim;

/// <summary>Network actions and the network's clock: carrier ports, commits and burst billing, peering and cross-connects.</summary>
public partial class GameState
{
    bool _warnCongested, _warnNoc;

    public CarrierContract ContractOf(int carrier) => Contracts.FirstOrDefault(k => k.Carrier == carrier);

    /// <summary>Free patch ports on the ODFs in the meet-me room.</summary>
    public int FreeOdfPorts() { var n = Net; return n.OdfPorts - n.OdfUsed; }

    public string CanOrderCarrier(int carrier, int portGbps)
    {
        if (carrier < 0 || carrier >= Catalog.Carriers.Length) return "msg_nothing_changed";
        if (ContractOf(carrier) != null) return "msg_carrier_exists";
        if (!Has(Exec.Network)) return "msg_needs_netmgr";
        if (!Network.MmrReady(this)) return "msg_needs_mmr";
        var def = Catalog.Carriers[carrier];
        if (!Network.FreeSubduct(this, def.Pit)) return Net.Pits.Any(p => p.Street == def.Pit) ? "msg_duct_full" : "msg_needs_pit";
        if (FreeOdfPorts() <= 0) return "msg_odf_full";
        if (!CanAfford(Catalog.Port(portGbps).SetupFee)) return "msg_no_money";
        return null;
    }

    public string OrderCarrier(int carrier, int portGbps)
    {
        var why = CanOrderCarrier(carrier, portGbps);
        if (why != null) return why;
        Spend(Catalog.Port(portGbps).SetupFee);
        var k = new CarrierContract
        {
            Carrier = carrier, PortGbps = Catalog.Port(portGbps).Gbps,
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

    public string CanPeer(int rival, bool proposal)
    {
        if (!Options.AiCompetitors) return "msg_no_rivals";
        if (!Has(Exec.Network)) return "msg_needs_netmgr";
        if (rival < 0 || rival >= Catalog.Rivals.Length) return "msg_nothing_changed";
        var p = PeerOf(rival);
        if (p != null && p.State is PeerState.Building or PeerState.Active) return "msg_nothing_changed";
        if (!Network.MmrReady(this)) return "msg_needs_mmr";
        if (!Network.FreeSubduct(this, -1)) return Net.Pits.Count == 0 ? "msg_needs_pit" : "msg_duct_full";
        if (FreeOdfPorts() <= 0) return "msg_odf_full";
        if (proposal && Net.OnlineRacks * Catalog.RackGbps * 1.35 < Catalog.Rivals[rival].MinGbps) return "msg_peer_too_small";
        if (!CanAfford(Catalog.Rivals[rival].SetupCost)) return "msg_no_money";
        return null;
    }

    /// <summary>Accept a request or make a proposal: dark fiber between the two MMRs, built in a few days.</summary>
    public string StartPeering(int rival)
    {
        var p = PeerOf(rival);
        var why = CanPeer(rival, p == null || p.State != PeerState.Requested);
        if (why != null) return why;
        Spend(Catalog.Rivals[rival].SetupCost);
        if (p == null) { p = new PeeringDeal { Rival = rival }; Peers.Add(p); }
        p.State = Creative.InstantBuild ? PeerState.Active : PeerState.Building;
        p.DaysLeft = Creative.InstantBuild ? 0 : Catalog.PeeringDays;
        if (p.State == PeerState.Active) Notes.Add(("msg_peer_live", Catalog.Rivals[rival].Name));
        Touch(false);
        return null;
    }

    public void DeclinePeering(int rival)
    {
        var p = PeerOf(rival);
        if (p == null) { p = new PeeringDeal { Rival = rival }; Peers.Add(p); }
        p.State = PeerState.Declined; p.DaysLeft = 0;
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
        // offline racks are told per hall with their cause and a fix (Outages, Main.CheckOutages)
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
                if (day >= rv.RequestDay && PeerOf(rv.Id) == null && Network.MmrReady(this))
                {
                    Peers.Add(new PeeringDeal { Rival = rv.Id, State = PeerState.Requested });
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
        foreach (var p in Peers.Where(p => p.State == PeerState.Active)) sum += Catalog.Rivals[p.Rival].Monthly / 30.0;
        sum += CrossConnects * Catalog.XcMonthlyFee / 30.0;
        return sum;
    }
}
