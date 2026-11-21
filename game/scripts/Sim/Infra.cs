using System;
using System.Collections.Generic;
using System.Linq;

namespace DatacenterArchitect.Sim;

/// <summary>One link of the power chain with its capacity, what it carries and how redundant it is.</summary>
public class LinkInfo
{
    public PowerLink Link;
    public double CapacityKw;
    public double LoadKw;
    public int Units;
    public string Redundancy = "-";
    public double Ratio => CapacityKw <= 0 ? (LoadKw > 0 ? 9 : 0) : LoadKw / CapacityKw;
}

/// <summary>Power and cooling for one data hall.</summary>
public class HallInfo
{
    public Room Room;
    public int LiveRacks, RunningRacks;
    public double ItKw;
    /// <summary>Capacity of the connected PDUs and busbar runs in the hall.</summary>
    public double DistributionKw;
    /// <summary>Connected PDUs and busbar runs, and on how many sides (A, B) they are.</summary>
    public int Feeds, Sides;
    /// <summary>Racks with at least one feed, and racks fed from both A and B.</summary>
    public int FedRacks, DualRacks;
    /// <summary>Free rack connections left on the hall's PDUs (busbar: unlimited).</summary>
    public int FreeOutlets;
    /// <summary>Every rack is fed from A and B: racks keep running when one side fails.</summary>
    public bool AB;
    /// <summary>"2N" (A and B from separate switchboards), "A+B" (both sides, shared switchboard), "partial", "N" or "-".</summary>
    public string Redundancy = "-";
    /// <summary>CRAH capacity in the hall with the containment bonus.</summary>
    public double CrahKw;
    /// <summary>What the hall can really cool: its CRAH units, limited by its share of heat rejection outside.</summary>
    public double CoolKw;
    public double CoolRatio => CoolKw <= 0 ? (ItKw > 0 ? 9 : 0) : ItKw / CoolKw;
    public double Pue;
    /// <summary>Air temperature the hall moves towards.</summary>
    public double Target;
    public bool PowerLimited;
    /// <summary>The gas extinguishing system went off after a fire alarm.</summary>
    public bool Suppressed;
}

/// <summary>Snapshot of the power chain, backup power and cooling of the whole site.</summary>
public class InfraReport
{
    public readonly List<LinkInfo> Links = new();
    public LinkInfo Bottleneck;
    /// <summary>IT load the chain can carry, after PUE on the grid side.</summary>
    public double UsableItKw;
    public double ItDemandKw, ItRunningKw, OtherKw;
    public double FacilityKw;
    public int LiveRacks, RunningRacks;
    public int DownForPower, DownForHeat;
    public readonly HashSet<int> Down = new();
    /// <summary>Why each rack in <see cref="Down"/> is off.</summary>
    public readonly Dictionary<int, DownReason> Why = new();
    /// <summary>The power distribution tree: switchboards, UPS units, PDUs and busbars with their loads.</summary>
    public PowerTree Tree;
    public DownReason Reason(int rackId) => Why.TryGetValue(rackId, out var w) ? w : DownReason.None;
    public double Pue;
    /// <summary>During a city-wide power cut: running on generators, or completely dark.</summary>
    public bool OnGenerators, Blackout;

    public int Generators;
    public double GenKw, BackupKw;
    public string GenRedundancy = "-";
    public bool Ats;
    public double FuelLitres, FuelHours;
    public double UpsKw, BatteryKwh, UpsMinutes;

    public double HeatKw, CrahKw, RejectKw, CoolingKw;
    public double CoolRatio => CoolingKw <= 0 ? (HeatKw > 0 ? 9 : 0) : HeatKw / CoolingKw;
    public readonly List<HallInfo> Halls = new();
    public LinkInfo Link(PowerLink l) => Links.First(x => x.Link == l);
}

/// <summary>
/// Works out the power chain (grid → transformers → switchgear → UPS → PDUs/busbar → racks),
/// generator backup with fuel, UPS autonomy, cooling (CRAH units and heat rejection outside) and PUE.
/// Racks that the chain cannot power, or that sit in a hall that overheated, are shut down.
/// </summary>
public static class Infra
{
    public const double ShutdownTemp = 32;

    static bool Live(PlacedObject o) => o.Status == ObjStatus.Live && !o.Broken;

    /// <summary>N, N+1 or 2N for a set of equal-purpose units carrying a load.</summary>
    public static string Redundancy(IReadOnlyCollection<double> caps, double load)
    {
        if (load <= 0 || caps.Count == 0) return "-";
        var sum = caps.Sum();
        if (sum < load) return "N-";
        if (caps.Count >= 2 && sum >= 2 * load) return "2N";
        if (sum - caps.Max() >= load) return "N+1";
        return "N";
    }

    public static InfraReport Analyze(GameState s)
    {
        var r = new InfraReport();
        var c = s.Creative;
        var live = s.Objects.Where(Live).ToList();
        var racks = live.Where(o => o.Kind == ObjKind.Rack).ToList();
        // objects are nearly always kept in id order already; only sort when they are not (same order either way)
        var sorted = true;
        for (var i = 1; i < racks.Count && sorted; i++) sorted = racks[i - 1].Id <= racks[i].Id;
        if (!sorted) racks = racks.OrderBy(o => o.Id).ToList();
        r.LiveRacks = racks.Count;
        r.OtherKw = live.Where(o => o.Kind != ObjKind.Rack).Sum(o => o.Def.PowerKw);

        // ---- halls: distribution, cooling and PUE ----
        var halls = s.Rooms.Where(x => x.Type == RoomType.DataHall).ToList();
        var byHall = new Dictionary<Room, List<PlacedObject>>();
        foreach (var h in halls) byHall[h] = new List<PlacedObject>();
        var orphan = new List<PlacedObject>();
        foreach (var o in racks)
        {
            var h = halls.FirstOrDefault(x => x.Floor == o.Floor && x.Contains(o.X, o.Z));
            if (h != null) byHall[h].Add(o); else orphan.Add(o);
        }
        var rejectUnits = live.Where(o => o.Def.RejectKw > 0).Select(o => o.Def.RejectKw * Catalog.LevelFactor(o.Level)).ToList();
        r.RejectKw = rejectUnits.Sum() * s.RejectFactor;
        r.HeatKw = racks.Sum(o => o.Def.PowerKw);
        var tree = PowerTree.Build(s, live, halls, byHall);
        r.Tree = tree;
        foreach (var h in halls)
        {
            var info = new HallInfo { Room = h, LiveRacks = byHall[h].Count, ItKw = byHall[h].Sum(o => o.Def.PowerKw) };
            var inHall = live.Where(o => o.Floor == h.Floor && h.Contains(o.X, o.Z)).ToList();
            var sources = tree.SourcesIn(h).Where(n => n.Connected).ToList();
            info.Feeds = sources.Count;
            info.Sides = sources.Select(n => n.Side).Distinct().Count();
            info.DistributionKw = sources.Sum(n => n.CapacityKw);
            info.FreeOutlets = sources.Any(n => n.Unlimited) ? int.MaxValue : sources.Sum(n => n.FreeOutlets);
            foreach (var o in byHall[h])
                if (tree.RackFeeds.TryGetValue(o.Id, out var f)) { info.FedRacks++; if (f.b != null) info.DualRacks++; }
            info.AB = info.LiveRacks > 0 && info.DualRacks == info.LiveRacks;
            if (info.LiveRacks == 0) info.Redundancy = "-";
            else if (info.AB)
            {
                // real 2N: the A side and the B side come from different switchboards
                var roots = sources.Where(n => n.Racks > 0).GroupBy(n => n.Side).Select(g => g.Select(n => n.Root).ToHashSet()).ToList();
                info.Redundancy = roots.Count == 2 && !roots[0].Overlaps(roots[1]) ? "2N" : "A+B";
            }
            else info.Redundancy = info.DualRacks > 0 ? "partial" : "N";
            var cont = Catalog.Containments[h.Containment];
            info.CrahKw = inHall.Where(o => o.Kind == ObjKind.Crah).Sum(o => o.Def.CoolingKw * Catalog.LevelFactor(o.Level)) * (1 + cont.CoolingBonus) * s.CrahFactor;
            info.Pue = Math.Max(1.08, cont.Pue - s.PueSaving);
            r.Halls.Add(info);
        }
        r.CrahKw = r.Halls.Sum(h => h.CrahKw);
        r.CoolingKw = Math.Min(r.CrahKw, r.RejectKw);
        foreach (var h in r.Halls)
        {
            // each hall gets a share of the heat rejection outside in proportion to its heat
            var share = r.HeatKw > 0 ? r.RejectKw * h.ItKw / r.HeatKw : r.RejectKw;
            h.CoolKw = c.NoCoolingLimit ? Math.Max(h.ItKw, h.CrahKw) : Math.Min(h.CrahKw, share);
            var ratio = h.CoolRatio;
            h.Target = h.ItKw <= 0 ? 20 : ratio <= 1 ? 20 + 5 * ratio : 25 + 25 * (ratio - 1);
            if (c.NoCoolingLimit) h.Target = 20 + 5 * Math.Min(1, ratio);
        }
        var weight = r.Halls.Sum(h => h.ItKw);
        r.Pue = weight > 0 ? r.Halls.Sum(h => h.Pue * h.ItKw) / weight : 0;
        // step 10: free cooling on cold days, harder work on hot ones
        if (r.Pue > 0 && s.WeatherMatters) r.Pue = 1 + (r.Pue - 1) * s.CoolingWeatherFactor;
        var pue = r.Pue > 0 ? r.Pue : Catalog.Containments[Containment.Open].Pue;

        // ---- the power chain ----
        var trafos = live.Where(o => o.Kind == ObjKind.Transformer).Select(o => o.Def.CapacityKw).ToList();
        var switches = live.Where(o => o.Kind == ObjKind.Switchgear).Select(o => o.Def.CapacityKw).ToList();
        // a UPS that found no free switchboard output carries nothing
        var ups = tree.Ups.Where(n => n.Connected).Select(n => n.CapacityKw).ToList();
        var dist = r.Halls.Select(h => h.DistributionKw).Where(k => k > 0).ToList();
        var gridT = new List<double> { Catalog.GridTransformerKw }; gridT.AddRange(trafos);
        // a city-wide power cut: the generators take over behind the ATS, for as long as the diesel lasts
        var gridKw = s.GridCapacityKw;
        if (s.GridDown && !c.NoPowerLimit)
        {
            var genList = s.Objects.Where(o => o.Kind == ObjKind.Generator && !o.Broken).Select(o => o.Def.GenKw).ToList();
            var demand = r.HeatKw * pue + r.OtherKw;
            var fuel = s.Objects.Where(o => !o.Broken).Sum(o => o.Def.FuelLitres);
            var load = Math.Min(demand, genList.Sum());
            var hoursOfFuel = load > 0 ? fuel / (load * Catalog.FuelPerKwh) : double.MaxValue;
            var ats = switches.Count > 0;
            // without N+1 all generators run; a single generator still helps in an outage
            var backup = genList.Count <= 1 ? genList.Sum() : Math.Max(s.BackupCapacityKw, 0);
            if (genList.Count >= 2 && backup < demand) backup = genList.Sum();
            gridKw = ats && !s.GeneratorsDry(hoursOfFuel) ? backup : 0;
            r.OnGenerators = gridKw > 0;
            r.Blackout = gridKw <= 0;
        }
        var links = new[]
        {
            (PowerLink.Grid, new List<double> { gridKw }, true),
            (PowerLink.Transformers, gridT, true),
            (PowerLink.Switchgear, switches, true),
            (PowerLink.Ups, ups, false),
            (PowerLink.Distribution, dist, false),
        };
        // the grid side also carries cooling and losses (PUE), the UPS and the hall distribution only the IT load
        var usable = double.MaxValue;
        foreach (var (link, caps, gridSide) in links)
        {
            var cap = caps.Sum();
            var it = gridSide ? cap / pue : cap;
            if (it < usable) usable = it;
            r.Links.Add(new LinkInfo { Link = link, CapacityKw = cap, Units = caps.Count });
        }
        r.UsableItKw = usable;

        // ---- which racks run ----
        r.ItDemandKw = r.HeatKw;
        var running = new List<PlacedObject>();
        foreach (var h in r.Halls)
        {
            var list = byHall[h.Room];
            var on = list;
            if (!c.NoPowerLimit)
            {
                // a rack runs when it has a PDU outlet (or busbar tap-off) in its own hall
                on = list.Where(o => tree.RackFeeds.ContainsKey(o.Id)).ToList();
                if (on.Count < list.Count)
                {
                    h.PowerLimited = true;
                    var why = h.Feeds == 0 ? DownReason.NoPdu : DownReason.PduFull;
                    foreach (var o in list) if (!tree.RackFeeds.ContainsKey(o.Id)) r.Why[o.Id] = why;
                }
            }
            var allowed = on.Count;
            var heatLimit = allowed;
            if (h.Room.Hot && !c.NoCoolingLimit) heatLimit = Math.Min(allowed, (int)Math.Floor(h.CoolKw / Catalog.RackKw + 1e-9));
            var suppressed = s.Suppressed(h.Room);
            if (suppressed) { heatLimit = 0; h.Suppressed = true; }
            r.DownForPower += list.Count - allowed;
            r.DownForHeat += allowed - heatLimit;
            for (var i = heatLimit; i < on.Count; i++) r.Why[on[i].Id] = suppressed ? DownReason.Suppressed : DownReason.Heat;
            running.AddRange(on.Take(heatLimit));
            h.RunningRacks = heatLimit;
        }
        running.AddRange(orphan.Where(_ => c.NoPowerLimit));
        r.DownForPower += orphan.Count(_ => !c.NoPowerLimit);
        if (!c.NoPowerLimit) foreach (var o in orphan) r.Why[o.Id] = DownReason.NoHall;
        if (!c.NoPowerLimit)
        {
            var max = (int)Math.Floor(usable / Catalog.RackKw + 1e-9);
            if (running.Count > max)
            {
                // the newest racks are switched off first
                var cut = running.OrderByDescending(o => o.Id).Take(running.Count - max).ToHashSet();
                r.DownForPower += cut.Count;
                foreach (var o in cut) r.Why[o.Id] = DownReason.Chain;
                running = running.Where(o => !cut.Contains(o)).ToList();
                // a set, not the list: with thousands of racks a list lookup per rack takes far too long
                var on = running.ToHashSet();
                foreach (var h in r.Halls) { var before = h.RunningRacks; h.RunningRacks = byHall[h.Room].Count(on.Contains); if (h.RunningRacks < before) h.PowerLimited = true; }
            }
        }
        var runSet = running.ToHashSet();
        foreach (var o in racks) if (!runSet.Contains(o)) r.Down.Add(o.Id);
        r.RunningRacks = running.Count;
        r.ItRunningKw = running.Sum(o => o.Def.PowerKw);
        r.FacilityKw = r.ItRunningKw * pue + r.OtherKw;
        // cooling, offices and losses hang on the first switchboard, on a field of their own (no feeder output)
        if (tree.Msbs.Count > 0)
        {
            var mech = new PowerNode { Kind = PowerNodeKind.Mech, Name = "mech", LoadKw = Math.Max(0, r.FacilityKw - r.ItRunningKw), Parent = tree.Msbs[0] };
            mech.FailoverKw = mech.LoadKw;
            tree.Mech = mech;
            tree.Msbs[0].Children.Add(mech);
            tree.Msbs[0].LoadKw += mech.LoadKw; tree.Msbs[0].FailoverKw += mech.LoadKw;
        }

        foreach (var l in r.Links)
        {
            var gridSide = l.Link is PowerLink.Grid or PowerLink.Transformers or PowerLink.Switchgear;
            l.LoadKw = gridSide ? r.ItDemandKw * pue : r.ItDemandKw;
            var caps = links.First(x => x.Item1 == l.Link).Item2;
            l.Redundancy = l.Link == PowerLink.Grid ? (l.LoadKw > 0 ? (l.CapacityKw >= l.LoadKw ? "N" : "N-") : "-") : Redundancy(caps, l.LoadKw);
        }
        r.Bottleneck = r.Links.OrderBy(l => l.Link is PowerLink.Grid or PowerLink.Transformers or PowerLink.Switchgear ? l.CapacityKw / pue : l.CapacityKw).First();

        // ---- backup: generators behind the ATS, fuel and UPS batteries ----
        var gens = s.Objects.Where(o => o.Kind == ObjKind.Generator).Select(o => o.Def.GenKw).ToList();
        r.Generators = gens.Count;
        r.GenKw = gens.Sum();
        r.Ats = switches.Count > 0;
        r.BackupKw = r.Ats ? s.BackupCapacityKw : 0;
        var facilityDemand = r.ItDemandKw * pue + r.OtherKw;
        r.GenRedundancy = r.Ats ? Redundancy(gens, facilityDemand) : "-";
        r.FuelLitres = s.Objects.Sum(o => o.Def.FuelLitres);
        var genLoad = Math.Min(facilityDemand, r.GenKw);
        r.FuelHours = genLoad > 0 && r.GenKw > 0 ? r.FuelLitres / (genLoad * Catalog.FuelPerKwh) : 0;
        r.UpsKw = ups.Sum();
        r.BatteryKwh = live.Where(o => o.Kind == ObjKind.Battery).Sum(o => o.Def.StorageKwh) * s.BatteryFactor;
        r.UpsMinutes = r.UpsKw > 0 && r.ItRunningKw > 0 ? r.BatteryKwh / r.ItRunningKw * 60 : 0;
        return r;
    }
}
