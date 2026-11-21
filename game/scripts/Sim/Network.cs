using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace DatacenterArchitect.Sim;

/// <summary>Links of the network chain, from the carriers outside to the racks.</summary>
public enum NetLink { Upstream, Ducts, Mmr, Core, Distribution, Spine }

/// <summary>A carrier contract: a port of a given size with a commit, ordered at a carrier at one of the street pits.</summary>
public class CarrierContract
{
    public int Carrier { get; set; }
    public int PortGbps { get; set; } = 10;
    /// <summary>Number of ports of this size; more than one only at an internet exchange.</summary>
    public int Ports { get; set; } = 1;
    public double Commit { get; set; } = 1;
    /// <summary>Ordered before carrier racks existed: it works without its own rack in the MMR.</summary>
    public bool NoRackNeeded { get; set; }
    /// <summary>Days until the new port is delivered (0 = in service).</summary>
    public double DaysLeft { get; set; }
    /// <summary>A port upgrade on its way (0 = none) and the days it still takes.</summary>
    public int UpgradeTo { get; set; }
    public double UpgradeDaysLeft { get; set; }
    /// <summary>Hourly traffic samples (Gbit/s) of this billing month, for the 95th percentile.</summary>
    public List<float> Samples { get; set; } = new();
    public bool Active => DaysLeft <= 0;
    /// <summary>Total port capacity in Gbit/s.</summary>
    [JsonIgnore] public int TotalGbps => PortGbps * Math.Max(1, Ports);
}

/// <summary>Requested = the rival asks you; Asking = you asked and wait for the answer; Offered = the rival offers less than you asked.</summary>
public enum PeerState { Requested, Building, Active, Declined, Asking, Offered }

/// <summary>Private peering with a rival datacenter over dark fiber between the two meet-me rooms, on a port with an agreed capacity.</summary>
public class PeeringDeal
{
    public int Rival { get; set; }
    public PeerState State { get; set; }
    /// <summary>Days until the fiber is built (Building), or until you may ask again (Declined).</summary>
    public double DaysLeft { get; set; }
    /// <summary>Port size and the capacity the rival allows (0 = older save: a 10G port at 10 Gbit/s).</summary>
    public int PortGbps { get; set; }
    public double Granted { get; set; }
    /// <summary>A request waiting for an answer: the capacity and port you asked for, and the days until the rival answers.</summary>
    public double Asked { get; set; }
    public int AskPort { get; set; }
    public double AnswerDays { get; set; }
    [JsonIgnore] public int Port => PortGbps > 0 ? PortGbps : 10;
    [JsonIgnore] public double Capacity => Math.Min(Port, Granted > 0 ? Granted : 10);
    /// <summary>A request for more capacity on a running peering.</summary>
    [JsonIgnore] public bool Upgrading => State == PeerState.Active && AnswerDays > 0 && Asked > 0;
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
    /// <summary>In service, with a subduct to its street pit and its own rack in the MMR.</summary>
    public bool Up;
    public bool Routed;
    /// <summary>The carrier rack in the MMR it uses (null = none free).</summary>
    public PlacedObject Rack;
    public bool Racked;
    public double Gbps;
}

/// <summary>
/// One data hall in the network tree: its own edge switches (they never serve another hall), their ports and the racks on them.
/// With two or more switches a rack gets an A and a B port on different switches (dual-homed) while ports last.
/// </summary>
public class HallNet
{
    public Room Room;
    /// <summary>Hall switches with an uplink (Spines), all hall switches (Switches) and their ports, racks and online racks.</summary>
    public int Spines, Ports, Racks, Online;
    /// <summary>Racks with an A and a B port; ports in use (online + dual).</summary>
    public int Dual, UsedPorts;
    public double Gbps, UplinkGbps;
    public readonly List<PlacedObject> Switches = new();
    /// <summary>"-" without racks, "N-" when racks are left without a port, "N" all online, "2N" all racks on two switches (A/B).</summary>
    public string Redundancy = "-";
    public int Unlinked => Switches.Count - Spines;
    public bool AB => Racks > 0 && Dual >= Racks;
}

/// <summary>A distribution switch in the MMR: hall switches hang on its ports, it takes one port on the core.</summary>
public class DistNet
{
    public PlacedObject Obj;
    public int Ports, Used;
    public double Capacity, Load;
    public bool Linked;
    public readonly List<PlacedObject> Edges = new();
}

/// <summary>A core router with its share of the core ports and of the traffic.</summary>
public class CoreNet
{
    public PlacedObject Obj;
    public int Ports, Used;
    public double Capacity, Load;
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
    public int Cores, CorePorts, CorePortsUsed;
    public double CoreGbps;
    /// <summary>The tree: cores, distribution switches, hall switches straight on the core (no distribution switch free) and
    /// each hall switch's uplink (distribution switch id, 0 = straight on the core, -1 = no free port anywhere).</summary>
    public readonly List<CoreNet> CoreList = new();
    public readonly List<DistNet> Dists = new();
    public readonly List<PlacedObject> DirectEdges = new();
    public readonly Dictionary<int, int> Uplink = new();
    /// <summary>Incoming fiber pairs: one per carrier port, IX port and peering link.</summary>
    public int Pairs => External;
    public HallNet HallOf(Room room) => Halls.FirstOrDefault(h => h.Room == room);
    public int OnlineRacks, OfflineRacks;
    /// <summary>Traffic right now, and how it is split over the internet exchange, private peering and transit.</summary>
    public double DemandGbps, IxGbps, PeerGbps, TransitGbps, TransitCapacity, Overflow;
    /// <summary>Port capacity over all internet exchanges, and the traffic per exchange.</summary>
    public double IxPort;
    public readonly Dictionary<int, double> IxFlows = new();
    /// <summary>Carrier racks in the MMR, and how many a carrier or IX uses.</summary>
    public int CarrierRacks, CarrierRacksUsed;
    public readonly Dictionary<int, double> PeerFlows = new();
    /// <summary>Share of the traffic that gets through (1 = no congestion).</summary>
    public double Served = 1;
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
            double dx = s.StreetPit(i) - pit.X, dz = s.PlotSize + Catalog.StreetPitOffset - pit.Z;
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
        var mmrRooms = s.Rooms.Where(x => x.Type == RoomType.MeetMe && x.Stage == Stage.Done).ToList();
        bool InMmr(PlacedObject o) => mmrRooms.Any(m => m.Floor == o.Floor && m.Contains(o.X, o.Z));
        var racks = new Queue<PlacedObject>(live.Where(o => o.Kind == ObjKind.CarrierRack && InMmr(o)).OrderBy(o => o.Id));
        r.CarrierRacks = racks.Count;
        foreach (var k in s.Contracts)
        {
            var def = Catalog.Carriers[k.Carrier];
            var routed = Take(pits, def.Pit);
            var info = new CarrierInfo { Contract = k, Def = def, Routed = routed };
            if (k.NoRackNeeded || !s.CarrierRackRule) info.Racked = true;
            else if (racks.Count > 0) { info.Rack = racks.Dequeue(); info.Racked = true; r.CarrierRacksUsed++; }
            info.Up = routed && info.Racked && k.Active;
            r.Carriers.Add(info);
        }
        var peers = new List<PeeringDeal>();
        foreach (var p in s.Peers.Where(p => p.State is PeerState.Building or PeerState.Active))
            if (Take(pits, -1) && p.State == PeerState.Active) peers.Add(p);
        var up = r.Carriers.Where(x => x.Up).ToList();
        r.Routes = r.Carriers.Where(x => x.Up).Select(x => x.Def.Pit).Distinct().Count();
        // one fiber pair per port: an IX with two ports takes two
        r.External = up.Sum(x => Math.Max(1, x.Contract.Ports)) + peers.Count;

        // ---- meet-me room, core and spines ----
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

        // ---- the tree: distribution switches take a core port, hall switches a distribution port (else a core port) ----
        var halls = s.Rooms.Where(x => x.Type == RoomType.DataHall).ToList();
        var running = s.Objects.Where(o => o.Kind == ObjKind.Rack && Live(o) && !infra.Down.Contains(o.Id)).OrderBy(o => o.Id).ToList();
        var edgesAll = live.Where(o => Catalog.IsHallSwitch(o.Kind)).OrderBy(o => o.Id).ToList();
        var corePortsLeft = r.CorePorts - r.External;
        foreach (var d in live.Where(o => o.Kind == ObjKind.DistSwitch && InMmr(o)).OrderBy(o => o.Id))
            r.Dists.Add(new DistNet { Obj = d, Ports = d.Def.Ports, Capacity = d.Def.Gbps, Linked = corePortsLeft-- > 0 });
        var linked = new HashSet<int>();
        foreach (var e in edgesAll)
        {
            var dn = r.Dists.FirstOrDefault(x => x.Linked && x.Used < x.Ports);
            if (dn != null) { dn.Used++; dn.Edges.Add(e); r.Uplink[e.Id] = dn.Obj.Id; linked.Add(e.Id); }
            else if (corePortsLeft-- > 0) { r.Uplink[e.Id] = 0; r.DirectEdges.Add(e); linked.Add(e.Id); }
            else r.Uplink[e.Id] = -1;
        }
        r.CorePortsUsed = Math.Min(r.CorePorts, r.External + r.Dists.Count(d => d.Linked) + r.DirectEdges.Count);
        // the cores share the connections evenly
        var ci = 0;
        foreach (var co in cores.OrderBy(o => o.Id))
        {
            var used = Math.Min(co.Def.Ports, r.CorePortsUsed / cores.Count + (ci++ < r.CorePortsUsed % cores.Count ? 1 : 0));
            r.CoreList.Add(new CoreNet { Obj = co, Ports = co.Def.Ports, Used = used, Capacity = co.Def.Gbps });
        }

        // ---- racks: a running rack needs a port on a switch in its own hall and a path up ----
        var chainOk = c.NoNetworkLimit || (r.Upstream && r.Mmr && r.Cores > 0);
        var online = new List<PlacedObject>();
        foreach (var h in halls)
        {
            var inHall = running.Where(o => o.Floor == h.Floor && h.Contains(o.X, o.Z)).ToList();
            var hn = new HallNet { Room = h, Racks = inHall.Count };
            hn.Switches.AddRange(edgesAll.Where(o => o.Floor == h.Floor && h.Contains(o.X, o.Z)));
            var sp = hn.Switches.Where(o => linked.Contains(o.Id)).ToList();
            hn.Spines = sp.Count;
            hn.Ports = sp.Sum(o => o.Def.Ports);
            hn.UplinkGbps = sp.Sum(o => o.Def.Gbps);
            var ok = c.NoNetworkLimit ? inHall : chainOk ? inHall.Take(hn.Ports).ToList() : new List<PlacedObject>();
            hn.Online = ok.Count;
            // a B port on another switch for as many racks as the spare ports allow
            hn.Dual = sp.Count >= 2 ? Math.Max(0, Math.Min(hn.Online, Math.Min(hn.Ports - hn.Online, hn.Ports - sp.Max(o => o.Def.Ports)))) : 0;
            hn.UsedPorts = Math.Min(hn.Ports, hn.Online + hn.Dual);
            hn.Redundancy = hn.Racks == 0 ? "-" : hn.Online < hn.Racks ? "N-" : hn.Dual >= hn.Racks ? "2N" : "N";
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
        foreach (var dn in r.Dists) dn.Load = dn.Edges.Sum(e => EdgeLoad(r, e));
        foreach (var cn in r.CoreList) cn.Load = r.Cores == 0 ? 0 : r.DemandGbps / r.Cores;

        // ---- the chain with redundancy ----
        var transitCaps = up.Select(x => (double)x.Contract.TotalGbps).ToList();
        var subducts = pits.Sum(p => p.Subducts);
        var usedSub = pits.Sum(p => p.Used);
        var worstSpine = "-";
        foreach (var hn in r.Halls.Where(h => h.Racks > 0)) worstSpine = Worse(worstSpine, hn.Redundancy);
        var viaDist = r.Dists.Sum(d => d.Load);
        r.Links.Add(new NetLinkInfo { Link = NetLink.Upstream, Capacity = r.TransitCapacity + r.IxPort + peers.Sum(p => p.Capacity), Load = r.DemandGbps, Units = up.Count + peers.Count,
            Redundancy = r.DemandGbps <= 0 ? "-" : Infra.Redundancy(transitCaps, r.TransitGbps + r.Overflow + r.IxGbps) });
        r.Links.Add(new NetLinkInfo { Link = NetLink.Ducts, Capacity = subducts, Load = usedSub, Units = pits.Count,
            Redundancy = usedSub <= 0 ? "-" : pits.Where(p => p.Used > 0).Select(p => p.Street).Distinct().Count() >= 2 ? "N+1" : "N" });
        r.Links.Add(new NetLinkInfo { Link = NetLink.Mmr, Capacity = r.OdfPorts, Load = r.OdfUsed, Units = odfs.Count,
            Redundancy = r.OdfUsed <= 0 ? "-" : r.MmrRooms >= 2 ? "2N" : r.OdfUsed > r.OdfPorts ? "N-" : "N" });
        r.Links.Add(new NetLinkInfo { Link = NetLink.Core, Capacity = r.CoreGbps, Load = r.DemandGbps, Units = r.Cores,
            Redundancy = Infra.Redundancy(cores.Select(o => o.Def.Gbps).ToList(), r.DemandGbps) });
        var liveDists = r.Dists.Where(d => d.Linked).ToList();
        r.Links.Add(new NetLinkInfo { Link = NetLink.Distribution, Capacity = liveDists.Sum(d => d.Capacity), Load = viaDist, Units = r.Dists.Count,
            Redundancy = viaDist <= 0 ? "-" : Infra.Redundancy(liveDists.Select(d => d.Capacity).ToList(), viaDist) });
        r.Links.Add(new NetLinkInfo { Link = NetLink.Spine, Capacity = r.Halls.Sum(h => h.Ports), Load = running.Count, Units = edgesAll.Count, Redundancy = worstSpine });
        r.Bottleneck = r.Links.Where(l => l.Load > 0).OrderByDescending(l => l.Ratio).FirstOrDefault();

        // ---- single points of failure ----
        if (r.OnlineRacks > 0 || running.Count > 0)
        {
            if (up.Count(x => !x.Def.Ix) <= 1) r.Spofs.Add("spof_carrier");
            if (pits.Count(p => p.Used > 0) <= 1 || pits.Where(p => p.Used > 0).Select(p => p.Street).Distinct().Count() <= 1) r.Spofs.Add("spof_route");
            if (r.MmrRooms <= 1) r.Spofs.Add("spof_mmr");
            if (r.Cores <= 1) r.Spofs.Add("spof_core");
            if (r.Dists.Count(d => d.Linked) == 1 && r.Dists.Any(d => d.Edges.Count > 0)) r.Spofs.Add("spof_dist");
            if (r.Halls.Any(h => h.Racks > 0 && h.Spines <= 1)) r.Spofs.Add("spof_spine");
        }
        return r;
    }

    /// <summary>Traffic over one hall switch: its hall's traffic shared by port count.</summary>
    public static double EdgeLoad(NetReport r, PlacedObject e)
    {
        var hn = r.Halls.FirstOrDefault(h => h.Switches.Contains(e));
        if (hn == null || hn.Ports <= 0 || !r.Uplink.TryGetValue(e.Id, out var up) || up < 0) return 0;
        return hn.Gbps * e.Def.Ports / hn.Ports;
    }

    /// <summary>Ports in use on one hall switch: the hall's used ports shared by port count (rounded so they add up).</summary>
    public static int EdgePortsUsed(NetReport r, PlacedObject e)
    {
        var hn = r.Halls.FirstOrDefault(h => h.Switches.Contains(e));
        if (hn == null || !r.Uplink.TryGetValue(e.Id, out var up) || up < 0) return 0;
        var left = hn.UsedPorts;
        foreach (var x in hn.Switches.Where(x => r.Uplink.TryGetValue(x.Id, out var u) && u >= 0))
        {
            var take = Math.Min(x.Def.Ports, (int)Math.Ceiling((double)hn.UsedPorts * x.Def.Ports / Math.Max(1, hn.Ports)));
            take = Math.Min(take, left); left -= take;
            if (x == e) return take;
        }
        return 0;
    }

    static string Worse(string a, string b)
    {
        int Rank(string x) => x switch { "N-" => 0, "N" => 1, "N+1" => 2, "2N" => 3, _ => 4 };
        return Rank(b) < Rank(a) ? b : a;
    }

    /// <summary>
    /// The internet exchanges take their members' share of the traffic (several exchanges overlap: together they reach
    /// 1 - (1 - a)(1 - b)...), each capped by its ports; every private peer takes its own share up to the capacity it allows;
    /// the rest goes over transit.
    /// </summary>
    static void Split(GameState s, NetReport r, List<CarrierInfo> up, List<PeeringDeal> peers, double demand)
    {
        r.DemandGbps = demand;
        var noc = r.Noc ? 1.25 : 1.0;
        var ixs = up.Where(x => x.Def.Ix).ToList();
        r.IxPort = ixs.Sum(x => x.Contract.TotalGbps);
        r.IxGbps = 0; r.IxFlows.Clear();
        if (ixs.Count > 0)
        {
            var reach = ixs.Select(x => IxReach(s, x.Def)).ToList();
            var together = 1 - reach.Aggregate(1.0, (a, b) => a * (1 - b));
            var sum = reach.Sum();
            for (var i = 0; i < ixs.Count; i++)
            {
                var f = Math.Min(ixs[i].Contract.TotalGbps, demand * together * noc * reach[i] / sum);
                ixs[i].Gbps = f;
                r.IxFlows[ixs[i].Def.Id] = f;
                r.IxGbps += f;
            }
        }
        r.PeerGbps = 0; r.PeerFlows.Clear();
        foreach (var p in peers)
        {
            var f = Math.Min(p.Capacity, demand * Catalog.Rivals[p.Rival].Share * noc);
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

    /// <summary>Share of your traffic one internet exchange's members take: more members when rival datacenters are in the game.</summary>
    public static double IxReach(GameState s, CarrierDef ix) => Math.Min(0.6, ix.Reach * (s.Options.AiCompetitors ? 1 : 0.72) + s.IxBonus);

    /// <summary>Share of the traffic the first internet exchange (Polder-IX) can carry on its own.</summary>
    public static double IxShare(GameState s) => IxReach(s, Catalog.Carriers[4]);

    /// <summary>Your traffic at the evening peak with the racks online now.</summary>
    public static double PeakGbps(NetReport r) => r.OnlineRacks * Catalog.RackGbps * 1.35;

    /// <summary>
    /// How much capacity a rival allows on a private peering. It looks at the traffic you would send it at the evening peak
    /// (your peak × its share): it allows about four times that, at least its own minimum, never more than its own network can
    /// take (about 10 Gbit/s per MW it runs) or than the port. A good reputation gets a quarter more. 0 = it says no: your
    /// network is less than half its minimum.
    /// </summary>
    public static double PeerAllowance(GameState s, NetReport r, int rival, double asked, int port)
    {
        var rv = Catalog.Rivals[rival];
        var peak = PeakGbps(r);
        if (peak < rv.MinGbps * 0.5) return 0;
        var willing = Math.Max(rv.MinGbps, peak * rv.Share * 4) * (s.Reputation >= 60 ? 1.25 : 1);
        var comp = s.Competitors.FirstOrDefault(c => c.Id == rival);
        var own = comp == null ? 100 : Math.Max(10, comp.CapacityMw * 10);
        return StepDown(Math.Min(Math.Min(asked, port), Math.Min(willing, own)));
    }

    /// <summary>Rounds a capacity down to one of the peering steps (at least the smallest).</summary>
    public static double StepDown(double gbps)
    {
        var best = Catalog.PeerSteps[0];
        foreach (var x in Catalog.PeerSteps) if (x <= gbps + 1e-9) best = x;
        return best;
    }

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

    /// <summary>Monthly cost of a contract without burst: port fee plus commit times the price per Mbit (an IX only charges its ports).</summary>
    public static double MonthlyCost(CarrierContract k)
    {
        var def = Catalog.Carriers[k.Carrier];
        var port = Catalog.Port(k.PortGbps).MonthlyFee;
        return def.Ix ? port * Math.Max(1, k.Ports) : port + k.Commit * 1000 * Catalog.PricePerMbit(def, k.Commit);
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
