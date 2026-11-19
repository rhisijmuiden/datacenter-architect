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
    public double DistributionKw;
    public int Feeds;
    /// <summary>Racks keep running when one of the A/B feeds fails.</summary>
    public bool AB;
    /// <summary>CRAH capacity in the hall with the containment bonus.</summary>
    public double CrahKw;
    /// <summary>What the hall can really cool: its CRAH units, limited by its share of heat rejection outside.</summary>
    public double CoolKw;
    public double CoolRatio => CoolKw <= 0 ? (ItKw > 0 ? 9 : 0) : ItKw / CoolKw;
    public double Pue;
    /// <summary>Air temperature the hall moves towards.</summary>
    public double Target;
    public bool PowerLimited;
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
    public double Pue;

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

    static bool Live(PlacedObject o) => o.Status == ObjStatus.Live;

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
        var racks = live.Where(o => o.Kind == ObjKind.Rack).OrderBy(o => o.Id).ToList();
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
        r.RejectKw = rejectUnits.Sum();
        r.HeatKw = racks.Sum(o => o.Def.PowerKw);
        foreach (var h in halls)
        {
            var info = new HallInfo { Room = h, LiveRacks = byHall[h].Count, ItKw = byHall[h].Sum(o => o.Def.PowerKw) };
            var inHall = live.Where(o => o.Floor == h.Floor && h.Contains(o.X, o.Z)).ToList();
            var feeds = inHall.Where(o => o.Kind == ObjKind.Pdu).Select(o => o.Def.CapacityKw).ToList();
            if (h.Busbar) { feeds.Add(Catalog.BusbarKw); feeds.Add(Catalog.BusbarKw); }
            info.Feeds = feeds.Count;
            info.DistributionKw = feeds.Sum();
            info.AB = feeds.Count >= 2 && info.ItKw > 0 && feeds.Sum() - feeds.Max() >= info.ItKw;
            var cont = Catalog.Containments[h.Containment];
            info.CrahKw = inHall.Where(o => o.Kind == ObjKind.Crah).Sum(o => o.Def.CoolingKw * Catalog.LevelFactor(o.Level)) * (1 + cont.CoolingBonus);
            info.Pue = cont.Pue;
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
        var pue = r.Pue > 0 ? r.Pue : Catalog.Containments[Containment.Open].Pue;

        // ---- the power chain ----
        var trafos = live.Where(o => o.Kind == ObjKind.Transformer).Select(o => o.Def.CapacityKw).ToList();
        var switches = live.Where(o => o.Kind == ObjKind.Switchgear).Select(o => o.Def.CapacityKw).ToList();
        var ups = live.Where(o => o.Kind == ObjKind.Ups).Select(o => o.Def.CapacityKw).ToList();
        var dist = r.Halls.Select(h => h.DistributionKw).Where(k => k > 0).ToList();
        var gridT = new List<double> { Catalog.GridTransformerKw }; gridT.AddRange(trafos);
        var links = new[]
        {
            (PowerLink.Grid, new List<double> { s.GridCapacityKw }, true),
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
            var allowed = list.Count;
            if (!c.NoPowerLimit)
            {
                var byDist = (int)Math.Floor(h.DistributionKw / Catalog.RackKw + 1e-9);
                if (byDist < allowed) { allowed = byDist; h.PowerLimited = true; }
            }
            var heatLimit = allowed;
            if (h.Room.Hot && !c.NoCoolingLimit) heatLimit = Math.Min(allowed, (int)Math.Floor(h.CoolKw / Catalog.RackKw + 1e-9));
            r.DownForPower += list.Count - allowed;
            r.DownForHeat += allowed - heatLimit;
            running.AddRange(list.Take(heatLimit));
            h.RunningRacks = heatLimit;
        }
        running.AddRange(orphan.Where(_ => c.NoPowerLimit));
        r.DownForPower += orphan.Count(_ => !c.NoPowerLimit);
        if (!c.NoPowerLimit)
        {
            var max = (int)Math.Floor(usable / Catalog.RackKw + 1e-9);
            if (running.Count > max)
            {
                // the newest racks are switched off first
                var cut = running.OrderByDescending(o => o.Id).Take(running.Count - max).ToHashSet();
                r.DownForPower += cut.Count;
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
        r.BatteryKwh = live.Where(o => o.Kind == ObjKind.Battery).Sum(o => o.Def.StorageKwh);
        r.UpsMinutes = r.UpsKw > 0 && r.ItRunningKw > 0 ? r.BatteryKwh / r.ItRunningKw * 60 : 0;
        return r;
    }
}
