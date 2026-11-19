using System;
using System.Collections.Generic;
using System.Linq;

namespace DatacenterArchitect.Sim;

/// <summary>Links of the network chain, from the carriers outside to the racks.</summary>
public enum NetLink { Upstream, Ducts, Mmr, Core, Spine }

/// <summary>A carrier contract: a port of a given size with a commit, ordered at a carrier at one of the street pits.</summary>
public class CarrierContract
{
    public int Carrier { get; set; }
    public int PortGbps { get; set; } = 10;
    public double Commit { get; set; } = 1;
    /// <summary>Days until the new port is delivered (0 = in service).</summary>
    public double DaysLeft { get; set; }
    /// <summary>A port upgrade on its way (0 = none) and the days it still takes.</summary>
    public int UpgradeTo { get; set; }
    public double UpgradeDaysLeft { get; set; }
    /// <summary>Hourly traffic samples (Gbit/s) of this billing month, for the 95th percentile.</summary>
    public List<float> Samples { get; set; } = new();
    public bool Active => DaysLeft <= 0;
}

public enum PeerState { Requested, Building, Active, Declined }

/// <summary>Private peering with a rival datacenter over dark fiber between the two meet-me rooms.</summary>
public class PeeringDeal
{
    public int Rival { get; set; }
    public PeerState State { get; set; }
    public double DaysLeft { get; set; }
}

public class NetLinkInfo
{
    public NetLink Link;
    public double Capacity, Load;
    public int Units;
    public string Redundancy = "-";
    public double Ratio => Capacity <= 0 ? (Load > 0 ? 9 : 0) : Load / Capacity;
}

/// <summary>Your own fiber pit with its duct to the nearest carrier pit in the street.</summary>
public class PitInfo
{
    public PlacedObject Pit;
    public int Street;
    public int Subducts, Used;
}

public class CarrierInfo
{
    public CarrierContract Contract;
    public CarrierDef Def;
    /// <summary>In service and with a subduct to its street pit.</summary>
    public bool Up;
    public bool Routed;
    public double Gbps;
}

public class HallNet
{
    public Room Room;
    public int Spines, Ports, Racks, Online;
    public double Gbps;
}

/// <summary>Snapshot of the network: routes, carriers, peering, MMR, core and spine switches, traffic and the racks that are online.</summary>
public class NetReport
{
    public readonly List<NetLinkInfo> Links = new();
    public NetLinkInfo Bottleneck;
    public readonly List<PitInfo> Pits = new();
    public readonly List<CarrierInfo> Carriers = new();
    public readonly List<HallNet> Halls = new();
    public readonly HashSet<int> Offline = new();
    public readonly List<string> Spofs = new();
    public int Routes;
    public bool Upstream, Mmr, Noc;
    public int MmrRooms;
    public int OdfPorts, OdfUsed, External;
    public int Cores, CorePorts;
    public double CoreGbps;
    public int OnlineRacks, OfflineRacks;
    /// <summary>Traffic right now, and how it is split over the internet exchange, private peering and transit.</summary>
    public double DemandGbps, IxGbps, PeerGbps, TransitGbps, TransitCapacity, Overflow;
    public double IxPort;
    public readonly Dictionary<int, double> PeerFlows = new();
    /// <summary>Share of the traffic that gets through (1 = no congestion).</summary>
    public double Served = 1;
    public double CoreLimitGbps = double.MaxValue;
    public double TransitRatio => TransitCapacity <= 0 ? (TransitGbps > 0 ? 9 : 0) : (TransitGbps + Overflow) / TransitCapacity;
    public NetLinkInfo Link(NetLink l) => Links.First(x => x.Link == l);
}

/// <summary>
/// Works out the network: own fiber pits and their ducts to the carrier pits in the street, carrier ports, the internet
/// exchange and private peering, patch ports (ODFs) in the meet-me room, core routers and the spine switches in each hall.
/// Racks without a path to the internet are offline; when the traffic does not fit, part of it is dropped (congestion).
/// </summary>
public static class Network
{
    /// <summary>Daily traffic curve: quiet in the morning, peak in the evening around 20:00, average 1.</summary>
    public static double DayCurve(double hours)
    {
        var h = hours % 24;
        return 1 + 0.35 * Math.Sin(2 * Math.PI * (h - 14) / 24);
    }

    static bool Live(PlacedObject o) => o.Status == ObjStatus.Live && !o.Broken;

    public static int NearestStreetPit(GameState s, PlacedObject pit)
    {
        var best = 0; var bd = double.MaxValue;
        for (var i = 0; i < Catalog.StreetPits.Length; i++)
        {
            double dx = s.StreetPit(i) - pit.X, dz = s.PlotSize + 1.6 - pit.Z;
            var d = dx * dx + dz * dz;
            if (d < bd) { bd = d; best = i; }
        }
        return best;
    }

    /// <summary>Your own pits with their subducts, in placement order.</summary>
    public static List<PitInfo> Pits(GameState s) => s.Objects.Where(o => o.Kind == ObjKind.FiberPit).OrderBy(o => o.Id)
        .Select(o => new PitInfo { Pit = o, Street = NearestStreetPit(s, o), Subducts = Catalog.Subducts(o.Level) }).ToList();

    /// <summary>Takes a free subduct towards a street pit (any street pit when street is -1). Returns false when none is free.</summary>
    static bool Take(List<PitInfo> pits, int street)
    {
        var p = pits.FirstOrDefault(x => (street < 0 || x.Street == street) && x.Used < x.Subducts);
        if (p == null) return false;
        p.Used++;
        return true;
    }

    /// <summary>Is there a free subduct for one more connection at this street pit (any when -1)?</summary>
    public static bool FreeSubduct(GameState s, int street)
    {
        var pits = Pits(s);
        foreach (var c in s.Contracts) Take(pits, Catalog.Carriers[c.Carrier].Pit);
        foreach (var p in s.Peers.Where(p => p.State is PeerState.Building or PeerState.Active)) Take(pits, -1);
        return pits.Any(x => (street < 0 || x.Street == street) && x.Used < x.Subducts);
    }

    public static bool MmrReady(GameState s) =>
        s.Objects.Any(o => o.Kind == ObjKind.Odf && Live(o) && s.Rooms.Any(r => r.Type == RoomType.MeetMe && r.Stage == Stage.Done && r.Floor == o.Floor && r.Contains(o.X, o.Z)));

    public static bool NocReady(GameState s) =>
        s.Objects.Any(o => o.Kind == ObjKind.NocWall && s.Rooms.Any(r => r.Type == RoomType.Noc && r.Stage == Stage.Done && r.Floor == o.Floor && r.Contains(o.X, o.Z)));

    public static NetReport Analyze(GameState s, InfraReport infra)
    {
        var r = new NetReport();
        var c = s.Creative;
        var live = s.Objects.Where(Live).ToList();
        r.Noc = NocReady(s);

        // ---- outside: pits, ducts and who gets a subduct ----
        var pits = Pits(s);
        r.Pits.AddRange(pits);
        foreach (var k in s.Contracts)
        {
            var def = Catalog.Carriers[k.Carrier];
            var routed = Take(pits, def.Pit);
            r.Carriers.Add(new CarrierInfo { Contract = k, Def = def, Routed = routed, Up = routed && k.Active });
        }
        var peers = new List<PeeringDeal>();
        foreach (var p in s.Peers.Where(p => p.State is PeerState.Building or PeerState.Active))
            if (Take(pits, -1) && p.State == PeerState.Active) peers.Add(p);
        var up = r.Carriers.Where(x => x.Up).ToList();
        r.Routes = r.Carriers.Where(x => x.Up).Select(x => x.Def.Pit).Distinct().Count();
        r.External = up.Count + peers.Count;

        // ---- meet-me room, core and spines ----
        var mmrRooms = s.Rooms.Where(x => x.Type == RoomType.MeetMe && x.Stage == Stage.Done).ToList();
        bool InMmr(PlacedObject o) => mmrRooms.Any(m => m.Floor == o.Floor && m.Contains(o.X, o.Z));
        var odfs = live.Where(o => o.Kind == ObjKind.Odf && InMmr(o)).ToList();
        r.MmrRooms = mmrRooms.Count(m => odfs.Any(o => o.Floor == m.Floor && m.Contains(o.X, o.Z)));
        r.Mmr = odfs.Count > 0;
        r.OdfPorts = odfs.Sum(o => o.Def.Ports);
        r.OdfUsed = r.External + s.CrossConnects;
        var cores = live.Where(o => o.Kind == ObjKind.CoreRouter && InMmr(o)).ToList();
        r.Cores = cores.Count;
        r.CorePorts = cores.Sum(o => o.Def.Ports);
        r.CoreGbps = cores.Sum(o => o.Def.Gbps);
        r.Upstream = up.Count > 0 || peers.Count > 0;

        // ---- racks: running racks need a spine port in their hall and a path up ----
        var halls = s.Rooms.Where(x => x.Type == RoomType.DataHall).ToList();
        var running = s.Objects.Where(o => o.Kind == ObjKind.Rack && Live(o) && !infra.Down.Contains(o.Id)).OrderBy(o => o.Id).ToList();
        var spinesAll = live.Where(o => o.Kind == ObjKind.Spine).OrderBy(o => o.Id).ToList();
        // every spine and every outside connection takes a core port
        var corePortsLeft = r.CorePorts - r.External;
        var linked = new HashSet<int>();
        foreach (var sp in spinesAll) if (corePortsLeft-- > 0) linked.Add(sp.Id);
        var chainOk = c.NoNetworkLimit || (r.Upstream && r.Mmr && r.Cores > 0);
        var online = new List<PlacedObject>();
        foreach (var h in halls)
        {
            var inHall = running.Where(o => o.Floor == h.Floor && h.Contains(o.X, o.Z)).ToList();
            var sp = spinesAll.Where(o => o.Floor == h.Floor && h.Contains(o.X, o.Z) && linked.Contains(o.Id)).ToList();
            var hn = new HallNet { Room = h, Spines = sp.Count, Ports = sp.Sum(o => o.Def.Ports), Racks = inHall.Count };
            var ok = c.NoNetworkLimit ? inHall : chainOk ? inHall.Take(hn.Ports).ToList() : new List<PlacedObject>();
            hn.Online = ok.Count;
            online.AddRange(ok);
            r.Halls.Add(hn);
        }
        var onlineIds = online.Select(o => o.Id).ToHashSet();
        foreach (var o in running) if (!onlineIds.Contains(o.Id)) r.Offline.Add(o.Id);
        r.OnlineRacks = online.Count;
        r.OfflineRacks = r.Offline.Count;

        // ---- traffic ----
        var demand = r.OnlineRacks * Catalog.RackGbps * DayCurve(s.Hours);
        Split(s, r, up, peers, demand);
        foreach (var hn in r.Halls) hn.Gbps = hn.Online * Catalog.RackGbps * DayCurve(s.Hours);

        // ---- the chain with redundancy ----
        var transitCaps = up.Select(x => (double)x.Contract.PortGbps).ToList();
        if (r.IxPort > 0) transitCaps.Add(r.IxPort);
        var subducts = pits.Sum(p => p.Subducts);
        var usedSub = pits.Sum(p => p.Used);
        var worstSpine = "-";
        foreach (var hn in r.Halls.Where(h => h.Racks > 0))
        {
            var red = Infra.Redundancy(Enumerable.Repeat((double)Catalog.Objects[ObjKind.Spine].Ports, hn.Spines).ToList(), hn.Racks);
            worstSpine = Worse(worstSpine, red);
        }
        r.Links.Add(new NetLinkInfo { Link = NetLink.Upstream, Capacity = r.TransitCapacity + r.IxPort + peers.Count * 10, Load = r.DemandGbps, Units = up.Count + peers.Count,
            Redundancy = r.DemandGbps <= 0 ? "-" : Infra.Redundancy(transitCaps, r.TransitGbps + r.Overflow + r.IxGbps) });
        r.Links.Add(new NetLinkInfo { Link = NetLink.Ducts, Capacity = subducts, Load = usedSub, Units = pits.Count,
            Redundancy = usedSub <= 0 ? "-" : pits.Where(p => p.Used > 0).Select(p => p.Street).Distinct().Count() >= 2 ? "N+1" : "N" });
        r.Links.Add(new NetLinkInfo { Link = NetLink.Mmr, Capacity = r.OdfPorts, Load = r.OdfUsed, Units = odfs.Count,
            Redundancy = r.OdfUsed <= 0 ? "-" : r.MmrRooms >= 2 ? "2N" : r.OdfUsed > r.OdfPorts ? "N-" : "N" });
        r.Links.Add(new NetLinkInfo { Link = NetLink.Core, Capacity = r.CoreGbps, Load = r.DemandGbps, Units = r.Cores,
            Redundancy = Infra.Redundancy(cores.Select(o => o.Def.Gbps).ToList(), r.DemandGbps) });
        r.Links.Add(new NetLinkInfo { Link = NetLink.Spine, Capacity = r.Halls.Sum(h => h.Ports), Load = running.Count, Units = spinesAll.Count, Redundancy = worstSpine });
        r.Bottleneck = r.Links.Where(l => l.Load > 0).OrderByDescending(l => l.Ratio).FirstOrDefault();

        // ---- single points of failure ----
        if (r.OnlineRacks > 0 || running.Count > 0)
        {
            if (up.Count(x => !x.Def.Ix) <= 1) r.Spofs.Add("spof_carrier");
            if (pits.Count(p => p.Used > 0) <= 1 || pits.Where(p => p.Used > 0).Select(p => p.Street).Distinct().Count() <= 1) r.Spofs.Add("spof_route");
            if (r.MmrRooms <= 1) r.Spofs.Add("spof_mmr");
            if (r.Cores <= 1) r.Spofs.Add("spof_core");
            if (r.Halls.Any(h => h.Racks > 0 && h.Spines <= 1)) r.Spofs.Add("spof_spine");
        }
        return r;
    }

    static string Worse(string a, string b)
    {
        int Rank(string x) => x switch { "N-" => 0, "N" => 1, "N+1" => 2, "2N" => 3, _ => 4 };
        return Rank(b) < Rank(a) ? b : a;
    }

    /// <summary>The internet exchange takes a share of the traffic, every private peer its own share; the rest goes over transit.</summary>
    static void Split(GameState s, NetReport r, List<CarrierInfo> up, List<PeeringDeal> peers, double demand)
    {
        r.DemandGbps = demand;
        var noc = r.Noc ? 1.25 : 1.0;
        var ix = up.FirstOrDefault(x => x.Def.Ix);
        r.IxPort = ix?.Contract.PortGbps ?? 0;
        r.IxGbps = ix == null ? 0 : Math.Min(r.IxPort, demand * IxShare(s) * noc);
        if (ix != null) ix.Gbps = r.IxGbps;
        r.PeerGbps = 0; r.PeerFlows.Clear();
        foreach (var p in peers)
        {
            var f = Math.Min(10, demand * Catalog.Rivals[p.Rival].Share * noc);
            r.PeerFlows[p.Rival] = f;
            r.PeerGbps += f;
        }
        var rest = Math.Max(0, demand - r.IxGbps - r.PeerGbps);
        var transit = up.Where(x => !x.Def.Ix).ToList();
        r.TransitCapacity = transit.Sum(x => x.Contract.PortGbps);
        r.TransitGbps = Math.Min(rest, r.TransitCapacity);
        r.Overflow = rest - r.TransitGbps;
        foreach (var t in transit) t.Gbps = r.TransitCapacity <= 0 ? 0 : r.TransitGbps * t.Contract.PortGbps / r.TransitCapacity;
        var through = demand - r.Overflow;
        if (!s.Creative.NoNetworkLimit && r.CoreGbps > 0) through = Math.Min(through, r.CoreGbps);
        r.Served = demand <= 0 || s.Creative.NoNetworkLimit ? 1 : Math.Clamp(through / demand, 0, 1);
    }

    /// <summary>Share of the traffic the internet exchange can carry: more members when rival datacenters are in the game.</summary>
    public static double IxShare(GameState s) => (s.Options.AiCompetitors ? 0.25 : 0.18) + s.IxBonus;

    /// <summary>Traffic per contract at a moment with the given demand, for the hourly samples.</summary>
    public static Dictionary<CarrierContract, double> FlowsAt(GameState s, NetReport r, double hours)
    {
        var up = r.Carriers.Where(x => x.Up).Select(x => new CarrierInfo { Contract = x.Contract, Def = x.Def, Up = true, Routed = true }).ToList();
        var peers = s.Peers.Where(p => p.State == PeerState.Active && r.PeerFlows.ContainsKey(p.Rival)).ToList();
        var tmp = new NetReport { Noc = r.Noc, CoreGbps = r.CoreGbps };
        Split(s, tmp, up, peers, r.OnlineRacks * Catalog.RackGbps * DayCurve(hours));
        return up.ToDictionary(x => x.Contract, x => x.Gbps);
    }

    /// <summary>95th percentile of the samples: the top 5% of the hours is not billed.</summary>
    public static double P95(IReadOnlyList<float> samples)
    {
        if (samples == null || samples.Count == 0) return 0;
        var sorted = samples.OrderBy(x => x).ToList();
        var i = (int)Math.Ceiling(sorted.Count * 0.95) - 1;
        return sorted[Math.Clamp(i, 0, sorted.Count - 1)];
    }

    /// <summary>Monthly cost of a contract without burst: port fee plus commit times the price per Mbit (the IX only charges the port).</summary>
    public static double MonthlyCost(CarrierContract k)
    {
        var def = Catalog.Carriers[k.Carrier];
        var port = Catalog.Port(k.PortGbps).MonthlyFee;
        return def.Ix ? port : port + k.Commit * 1000 * Catalog.PricePerMbit(def, k.Commit);
    }

    /// <summary>Burst bill for a month: the 95th percentile above the commit, at the burst price.</summary>
    public static double BurstCost(CarrierContract k, bool noc)
    {
        var def = Catalog.Carriers[k.Carrier];
        if (def.Ix) return 0;
        var over = Math.Max(0, P95(k.Samples) - k.Commit);
        return over * 1000 * Catalog.PricePerMbit(def, k.Commit) * (noc ? Catalog.BurstFactorNoc : Catalog.BurstFactor);
    }
}
