using System;
using System.Collections.Generic;
using System.Linq;

namespace DatacenterArchitect.Sim;

/// <summary>Redundancy level of one system of a data hall, worst first.</summary>
public enum HallLevel { None, Short, N, Partial, NPlus1, AB, TwoN }

/// <summary>One side (A or B) of the hall's power distribution: PDUs and busbar runs on that side.</summary>
public class HallSide
{
    public FeedSide Side;
    public int Units;
    public double CapacityKw, LoadKw, FailoverKw;
    /// <summary>Rack connections on this side; <see cref="int.MaxValue"/> with a busbar run.</summary>
    public int Outlets, UsedOutlets;
    public bool Busbar;
    public bool Unlimited => Outlets == int.MaxValue;
}

/// <summary>Something the hall can still get: a hall upgrade or a device to place.</summary>
public enum HallUpgradeKind { Containment, Busbar, Pdu, EdgeSwitch, Crah }

public class HallUpgrade
{
    public HallUpgradeKind Kind;
    /// <summary>The device to place (PDU, edge switch, CRAH), or none for a hall upgrade.</summary>
    public ObjKind Obj;
    /// <summary>How many of that device it takes to reach the goal.</summary>
    public int Count = 1;
    public long Cost;
    public double Hours;
    /// <summary>Reason the upgrade cannot start now (message key), null when it can.</summary>
    public string Blocked;
    /// <summary>Most useful first: fixes that bring racks online before redundancy and efficiency.</summary>
    public int Priority;
}

/// <summary>
/// Everything the hall inspector shows about one data hall: what its racks need (power, network ports, cooling)
/// against what is installed, the redundancy level of each system, what the hall already has and what it can still get.
/// Demand counts every rack in the hall, also the ones still on their way, so the player sees whether the whole hall
/// will come online once it is full.
/// </summary>
public class HallReport
{
    public Room Room;
    public HallInfo Power;
    public HallNet Net;
    /// <summary>All racks in the hall (any status), live racks, racks that run and racks that are online.</summary>
    public int Racks, LiveRacks, Running, Online;
    public double NeedKw;

    // ---- power ----
    public readonly HallSide A = new() { Side = FeedSide.A }, B = new() { Side = FeedSide.B };
    public HallLevel PowerLevel;
    /// <summary>The A and the B side hang on different switchboards.</summary>
    public bool SeparateMsb;
    /// <summary>PDUs that found no free output upstream.</summary>
    public int UnfedSources;

    // ---- network ----
    public int Switches, Linked, Ports, Dual;
    public double Gbps, UplinkGbps;
    public HallLevel NetLevel;

    // ---- cooling ----
    public int Crahs, BrokenCrahs;
    public double CrahKw, CoolKw, UnitKw;
    /// <summary>The hall gets less than its CRAH units could do: heat rejection outside (chillers, towers) is short.</summary>
    public bool RejectLimited;
    public HallLevel CoolLevel;
    public double Pue, Temp;

    /// <summary>How many more racks fit before power, network or cooling runs out (keeping one feed per rack), and which runs out first.</summary>
    public int Headroom;
    public string HeadroomBy;

    public readonly List<HallUpgrade> Upgrades = new();

    /// <summary>Live racks that are off, per reason, and running racks without a network path (from the infra and network reports).</summary>
    public readonly Dictionary<DownReason, int> Down = new();
    public int Offline;
    /// <summary>The most common reason racks in this hall are off, or None.</summary>
    public DownReason MainReason => Down.Count == 0 ? DownReason.None : Down.OrderByDescending(x => x.Value).First().Key;

    public HallSide Side(FeedSide s) => s == FeedSide.A ? A : B;
    public bool AllOnline => Racks > 0 && Online >= Racks;

    static bool Live(PlacedObject o) => o.Status == ObjStatus.Live && !o.Broken;

    /// <summary>Capacity of one CRAH in this hall, with its upgrade level, the containment bonus and research.</summary>
    public static double CrahUnit(GameState s, Room hall, int level = 1, Containment? cont = null)
        => Catalog.Objects[ObjKind.Crah].CoolingKw * Catalog.LevelFactor(level) * (1 + Catalog.Containments[cont ?? hall.Containment].CoolingBonus) * s.CrahFactor;

    /// <summary>The level for units of a system that carry a load: N-, N, N+1 or 2N.</summary>
    static HallLevel Level(IReadOnlyCollection<double> caps, double load) => Infra.Redundancy(caps, load) switch
    {
        "N-" => HallLevel.Short, "N" => HallLevel.N, "N+1" => HallLevel.NPlus1, "2N" => HallLevel.TwoN, _ => HallLevel.None,
    };

    public static HallReport Build(GameState s, Room hall)
    {
        var infra = s.Infra;
        var net = s.Net;
        var r = new HallReport { Room = hall, Temp = hall.Temp };
        r.Power = infra?.Halls.FirstOrDefault(h => h.Room == hall);
        r.Net = net?.Halls.FirstOrDefault(h => h.Room == hall);
        var inHall = s.Objects.Where(o => o.Floor == hall.Floor && hall.Contains(o.X, o.Z)).ToList();
        var racks = inHall.Where(o => o.Kind == ObjKind.Rack).ToList();
        r.Racks = racks.Count;
        r.LiveRacks = racks.Count(o => o.Status == ObjStatus.Live);
        r.Running = r.Power?.RunningRacks ?? 0;
        r.Online = r.Net?.Online ?? 0;
        r.NeedKw = racks.Sum(o => o.Def.PowerKw);
        if (infra != null)
            foreach (var o in racks)
                if (o.Status == ObjStatus.Live && infra.Down.Contains(o.Id)) { var why = infra.Reason(o.Id); r.Down[why] = r.Down.GetValueOrDefault(why) + 1; }
        if (net != null) r.Offline = racks.Count(o => net.Offline.Contains(o.Id));
        var rackKw = Catalog.Objects[ObjKind.Rack].PowerKw;

        // ---- power: both sides of the distribution ----
        var sources = infra?.Tree?.SourcesIn(hall).ToList() ?? new List<PowerNode>();
        r.UnfedSources = sources.Count(n => !n.Connected);
        foreach (var n in sources.Where(n => n.Connected))
        {
            var side = r.Side(n.Side);
            side.Units++;
            side.CapacityKw += n.CapacityKw;
            side.LoadKw += n.LoadKw; side.FailoverKw += n.FailoverKw;
            side.UsedOutlets += n.UsedOutlets;
            if (n.Kind == PowerNodeKind.Busbar) side.Busbar = true;
            side.Outlets = n.Unlimited || side.Unlimited ? int.MaxValue : side.Outlets + n.Outlets;
        }
        bool Carries(HallSide x) => x.Units > 0 && x.CapacityKw + 1e-6 >= r.NeedKw && (x.Unlimited || x.Outlets >= r.Racks);
        var totalKw = r.A.CapacityKw + r.B.CapacityKw;
        var totalOutlets = r.A.Unlimited || r.B.Unlimited ? int.MaxValue : r.A.Outlets + r.B.Outlets;
        var roots = sources.Where(n => n.Connected).GroupBy(n => n.Side).Select(g => g.Select(n => n.Root).ToHashSet()).ToList();
        r.SeparateMsb = roots.Count == 2 && !roots[0].Overlaps(roots[1]);
        if (r.Racks == 0) r.PowerLevel = HallLevel.None;
        else if (totalKw + 1e-6 < r.NeedKw || (totalOutlets != int.MaxValue && totalOutlets < r.Racks)) r.PowerLevel = HallLevel.Short;
        else if (Carries(r.A) && Carries(r.B)) r.PowerLevel = r.SeparateMsb ? HallLevel.TwoN : HallLevel.AB;
        else
        {
            // one PDU may fail and the others still carry every rack
            var connected = sources.Where(n => n.Connected).ToList();
            var biggest = connected.Count == 0 ? null : connected.OrderByDescending(n => n.CapacityKw).First();
            var spareKw = totalKw - (biggest?.CapacityKw ?? 0);
            var spareOutlets = totalOutlets == int.MaxValue ? int.MaxValue : totalOutlets - (biggest?.Outlets ?? 0);
            r.PowerLevel = connected.Count >= 2 && spareKw + 1e-6 >= r.NeedKw && spareOutlets >= r.Racks ? HallLevel.NPlus1
                : r.A.Units > 0 && r.B.Units > 0 ? HallLevel.Partial : HallLevel.N;
        }

        // ---- network: ports on the hall switches that have an uplink ----
        var switches = inHall.Where(o => Catalog.IsHallSwitch(o.Kind) && Live(o)).ToList();
        r.Switches = switches.Count;
        r.Linked = r.Net?.Spines ?? 0;
        r.Ports = r.Net?.Ports ?? 0;
        r.Dual = r.Net?.Dual ?? 0;
        r.Gbps = r.Net?.Gbps ?? 0;
        r.UplinkGbps = r.Net?.UplinkGbps ?? 0;
        var linkedPorts = r.Net?.Switches.Where(o => net.Uplink.TryGetValue(o.Id, out var up) && up >= 0).Select(o => o.Def.Ports).ToList() ?? new List<int>();
        if (r.Racks == 0) r.NetLevel = HallLevel.None;
        else if (r.Ports < r.Racks) r.NetLevel = HallLevel.Short;
        else
        {
            var spare = r.Ports - (linkedPorts.Count > 0 ? linkedPorts.Max() : 0);
            r.NetLevel = linkedPorts.Count >= 2 && spare >= r.Racks ? (r.Ports >= 2 * r.Racks ? HallLevel.TwoN : HallLevel.NPlus1) : HallLevel.N;
        }

        // ---- cooling: CRAH units in the hall, limited by heat rejection outside ----
        var cont = Catalog.Containments[hall.Containment];
        var crahs = inHall.Where(o => o.Kind == ObjKind.Crah && o.Status == ObjStatus.Live).ToList();
        r.BrokenCrahs = crahs.Count(o => o.Broken);
        var caps = crahs.Where(o => !o.Broken).Select(o => o.Def.CoolingKw * Catalog.LevelFactor(o.Level) * (1 + cont.CoolingBonus) * s.CrahFactor).ToList();
        r.Crahs = caps.Count;
        r.CrahKw = caps.Sum();
        r.UnitKw = CrahUnit(s, hall);
        r.CoolKw = r.Power?.CoolKw ?? r.CrahKw;
        if (s.Creative.NoCoolingLimit) r.CoolKw = Math.Max(r.CoolKw, r.CrahKw);
        r.RejectLimited = r.NeedKw > 0 && r.CoolKw + 1e-6 < Math.Min(r.CrahKw, r.NeedKw);
        r.CoolLevel = r.NeedKw <= 0 ? HallLevel.None : caps.Count == 0 || (r.RejectLimited && r.CoolKw + 1e-6 < r.NeedKw) ? HallLevel.Short : Level(caps, r.NeedKw);
        r.Pue = Math.Max(1.08, cont.Pue - s.PueSaving);

        // ---- headroom: racks that still fit with one feed, one port and their cooling ----
        var freeKw = totalKw - r.NeedKw;
        var byPower = totalOutlets == int.MaxValue ? (int)Math.Floor(freeKw / rackKw + 1e-9) : Math.Min(totalOutlets - r.Racks, (int)Math.Floor(freeKw / rackKw + 1e-9));
        var byNet = r.Ports - r.Racks;
        var byCool = (int)Math.Floor((r.CoolKw - r.NeedKw) / rackKw + 1e-9);
        var limits = new[] { (byPower, "power"), (byNet, "network"), (byCool, "cooling") };
        var min = limits.OrderBy(x => x.Item1).First();
        r.Headroom = Math.Max(0, min.Item1);
        r.HeadroomBy = min.Item2;

        r.FindUpgrades(s);
        return r;
    }

    /// <summary>The PDU that covers this many racks and kW with the fewest units, smallest first on a tie.</summary>
    public static (ObjKind kind, int count) PduFor(double kw, int racks)
    {
        (ObjKind, int) best = (ObjKind.Pdu, 1);
        var bestN = int.MaxValue;
        foreach (var k in Catalog.PduKinds)
        {
            var d = Catalog.Objects[k];
            var n = Math.Max(1, Math.Max((int)Math.Ceiling(kw / d.CapacityKw - 1e-9), (int)Math.Ceiling((double)racks / d.Outlets - 1e-9)));
            if (n < bestN) { best = (k, n); bestN = n; }
        }
        return best;
    }

    void FindUpgrades(GameState s)
    {
        var room = Room;
        // ---- power: an extra PDU on the side that is short, or the busbar ----
        if (Racks > 0 && PowerLevel < HallLevel.AB)
        {
            // the weakest side gets the next PDU (a new PDU goes on the side with fewer PDUs in the hall)
            var weak = A.Units <= B.Units ? A : B;
            var other = weak == A ? B : A;
            var short_ = PowerLevel == HallLevel.Short;
            // short: the hall needs enough feeds at all; otherwise the weak side must carry every rack on its own
            var gapKw = short_ ? NeedKw - A.CapacityKw - B.CapacityKw : NeedKw - weak.CapacityKw;
            var gapRacks = short_
                ? (A.Unlimited || B.Unlimited ? 0 : Racks - A.Outlets - B.Outlets)
                : weak.Unlimited ? 0 : Racks - weak.Outlets;
            if (!short_ && other.Units == 0) { gapKw = NeedKw; gapRacks = Racks; }
            var (kind, count) = PduFor(Math.Max(0, gapKw), Math.Max(0, gapRacks));
            Upgrades.Add(new HallUpgrade { Kind = HallUpgradeKind.Pdu, Obj = kind, Count = count, Cost = Catalog.Objects[kind].Cost * count, Priority = short_ ? 0 : 3,
                Blocked = s.CanAfford(Catalog.Objects[kind].Cost) ? null : "msg_no_money" });
        }
        if (!room.Busbar && !room.UpgradeQueue.Contains(2))
            Upgrades.Add(new HallUpgrade { Kind = HallUpgradeKind.Busbar, Cost = Catalog.BusbarCost, Hours = Catalog.BusbarHours, Priority = PowerLevel < HallLevel.AB && Racks > 0 ? 4 : 7,
                Blocked = room.Upgrade == 2 ? "msg_upgrade_busy" : s.CanUpgradeHall(room, true) });

        // ---- network: ports for every rack, then a second switch so one may fail ----
        if (Racks > 0 && NetLevel < HallLevel.NPlus1)
        {
            // the fewest switches (then the cheapest) that give every rack a port, or keep every rack on a port when the biggest switch fails
            var have = Net?.Switches.Where(o => o.Status == ObjStatus.Live && !o.Broken).Select(o => o.Def.Ports).ToList() ?? new List<int>();
            var goalN1 = NetLevel != HallLevel.Short;
            (ObjKind kind, int count) best = (ObjKind.EdgeSwitch48, 99);
            foreach (var k in new[] { ObjKind.EdgeSwitch24, ObjKind.EdgeSwitch48 })
            {
                var p = Catalog.Objects[k].Ports;
                for (var c = 1; c < 99; c++)
                {
                    var ports = Ports + p * c;
                    var biggest = Math.Max(have.Count > 0 ? have.Max() : 0, p);
                    if (goalN1 ? ports - biggest >= Racks : ports >= Racks)
                    {
                        if (c < best.count || (c == best.count && Catalog.Objects[k].Cost < Catalog.Objects[best.kind].Cost)) best = (k, c);
                        break;
                    }
                }
            }
            var (kind, count) = best;
            Upgrades.Add(new HallUpgrade { Kind = HallUpgradeKind.EdgeSwitch, Obj = kind, Count = count, Cost = Catalog.Objects[kind].Cost * count, Priority = NetLevel == HallLevel.Short ? 1 : 5,
                Blocked = s.CanAfford(Catalog.Objects[kind].Cost) ? null : "msg_no_money" });
        }

        // ---- cooling: CRAH units until one may fail (N+1), then containment ----
        if (NeedKw > 0 && CoolLevel < HallLevel.NPlus1)
        {
            var count = CrahsForNPlus1(s);
            var d = Catalog.Objects[ObjKind.Crah];
            Upgrades.Add(new HallUpgrade { Kind = HallUpgradeKind.Crah, Obj = ObjKind.Crah, Count = count, Cost = d.Cost * count, Priority = CoolLevel == HallLevel.Short ? 2 : 6,
                Blocked = s.CanAfford(d.Cost) ? null : "msg_no_money" });
        }
        if (GameState.PendingContainment(room) < Containment.ColdCorridor || room.Upgrade == 1)
            Upgrades.Add(new HallUpgrade { Kind = HallUpgradeKind.Containment, Cost = GameState.HallUpgradeCost(room), Hours = Catalog.Containments[room.Containment + 1].Hours, Priority = 8,
                Blocked = room.Upgrade == 1 ? "msg_upgrade_busy" : s.CanUpgradeHall(room, false) });
        Upgrades.Sort((x, y) => x.Priority.CompareTo(y.Priority));
    }

    /// <summary>Extra CRAH units (level 1) the hall needs so it still cools every rack when one unit fails.</summary>
    public int CrahsForNPlus1(GameState s, Containment? level = null)
    {
        var cont = Catalog.Containments[level ?? Room.Containment];
        var caps = s.Objects.Where(o => o.Kind == ObjKind.Crah && o.Status == ObjStatus.Live && !o.Broken && o.Floor == Room.Floor && Room.Contains(o.X, o.Z))
            .Select(o => o.Def.CoolingKw * Catalog.LevelFactor(o.Level) * (1 + cont.CoolingBonus) * s.CrahFactor).ToList();
        var unit = CrahUnit(s, Room, 1, level);
        if (caps.Count >= 2 && caps.Sum() - caps.Max() + 1e-6 >= NeedKw) return 0;
        for (var n = 1; n < 200; n++)
        {
            caps.Add(unit);
            if (caps.Sum() - caps.Max() + 1e-6 >= NeedKw) return n;
        }
        return 200;
    }

    /// <summary>What the hall already has, for the green chips (localisation keys).</summary>
    public List<string> Features()
    {
        var list = new List<string>();
        if (Room.Containment != Containment.Open) list.Add(Catalog.Containments[Room.Containment].NameKey);
        if (Room.Busbar) list.Add("hall_feat_busbar");
        if (PowerLevel == HallLevel.TwoN) list.Add("hall_feat_2n");
        else if (PowerLevel == HallLevel.AB) list.Add("hall_feat_ab");
        if (NetLevel >= HallLevel.NPlus1) list.Add(NetLevel == HallLevel.TwoN ? "hall_feat_net_ab" : "hall_feat_net_n1");
        if (CoolLevel >= HallLevel.NPlus1) list.Add(CoolLevel == HallLevel.TwoN ? "hall_feat_cool_2n" : "hall_feat_cool_n1");
        return list;
    }

    /// <summary>Changes when anything the card shows changes, so the inspector only rebuilds then.</summary>
    public string Sig() => string.Join(",", Racks, LiveRacks, Running, Online, NeedKw, A.Units, A.CapacityKw, A.UsedOutlets, A.Outlets, B.Units, B.CapacityKw, B.UsedOutlets, B.Outlets,
        PowerLevel, NetLevel, CoolLevel, Ports, Switches, Linked, Math.Round(Gbps, 1), Crahs, BrokenCrahs, Math.Round(CoolKw), Math.Round(Temp), Room.Containment, Room.Busbar, Room.Upgrade,
        Math.Ceiling(Room.UpgradeLeft), Headroom, UnfedSources, string.Join(";", Upgrades.Select(u => $"{u.Kind}{u.Obj}{u.Count}{u.Cost}{u.Blocked}")));
}
