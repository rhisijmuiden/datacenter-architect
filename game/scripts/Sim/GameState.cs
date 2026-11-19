using System;
using System.Collections.Generic;
using System.Linq;

namespace DatacenterArchitect.Sim;

/// <summary>Placeable things on the build grid. Real numbers come later; these drive the first resource bars.</summary>
public enum BuildKind { Foundation, Rack, Crah, Generator, FiberPit }

public record BuildDef(BuildKind Kind, string NameKey, int W, int D, long Cost, double PowerKw, double CoolingKw, double GenKw, int Fiber, bool NeedsFoundation);

public static class Catalog
{
    public static readonly Dictionary<BuildKind, BuildDef> Defs = new()
    {
        [BuildKind.Foundation] = new(BuildKind.Foundation, "build_foundation", 1, 1, 150, 0, 0, 0, 0, false),
        [BuildKind.Rack] = new(BuildKind.Rack, "build_rack", 1, 1, 4200, 6, 0, 0, 0, true),
        [BuildKind.Crah] = new(BuildKind.Crah, "build_crah", 1, 2, 38000, 4, 120, 0, 0, true),
        [BuildKind.Generator] = new(BuildKind.Generator, "build_generator", 3, 2, 410000, 0, 0, 1000, 0, false),
        [BuildKind.FiberPit] = new(BuildKind.FiberPit, "build_fiberpit", 1, 1, 12000, 0, 0, 0, 1, false),
    };
}

public class PlacedObject
{
    public BuildKind Kind { get; set; }
    public int X { get; set; }
    public int Z { get; set; }
    public int Rot { get; set; }
}

public class NewGameOptions
{
    public string Company { get; set; } = "My Datacenter";
    public bool AiCompetitors { get; set; } = true;
    /// <summary>0 = easy, 1 = normal, 2 = hard. Sets the starting balance.</summary>
    public int Difficulty { get; set; } = 1;
    public long StartMoney => Difficulty switch { 0 => 25_000_000, 2 => 6_000_000, _ => 12_000_000 };
}

/// <summary>All state that goes into a savegame.</summary>
public class GameState
{
    public int SaveFormat { get; set; } = 1;
    public string GameVersion { get; set; } = "";
    public NewGameOptions Options { get; set; } = new();
    public long Money { get; set; }
    /// <summary>In-game time in hours since 1 January 2027 00:00.</summary>
    public double Hours { get; set; } = 8;
    public int PlotSize { get; set; } = 64;
    public List<int[]> Foundation { get; set; } = new();
    public List<PlacedObject> Objects { get; set; } = new();
    public float CamYaw { get; set; } = 0.6f;
    public float CamPitch { get; set; } = 0.9f;
    public float CamDistance { get; set; } = 60f;
    public float[] CamTarget { get; set; } = { 0, 0, 0 };

    public static readonly DateTime Epoch = new(2027, 1, 1);
    public DateTime Date => Epoch.AddHours(Hours);

    public static GameState New(NewGameOptions o) => new() { Options = o, Money = o.StartMoney };

    // ---- foundation grid ----
    HashSet<(int, int)> _found;
    HashSet<(int, int)> FoundSet => _found ??= Foundation.Select(f => (f[0], f[1])).ToHashSet();
    public bool HasFoundation(int x, int z) => FoundSet.Contains((x, z));
    public void AddFoundation(int x, int z) { if (FoundSet.Add((x, z))) Foundation.Add(new[] { x, z }); }
    public void RemoveFoundation(int x, int z) { if (FoundSet.Remove((x, z))) Foundation.RemoveAll(f => f[0] == x && f[1] == z); }

    public static (int w, int d) Footprint(PlacedObject o)
    {
        var d = Catalog.Defs[o.Kind];
        return o.Rot % 2 == 0 ? (d.W, d.D) : (d.D, d.W);
    }

    public PlacedObject ObjectAt(int x, int z) => Objects.FirstOrDefault(o =>
    {
        var (w, d) = Footprint(o);
        return x >= o.X && x < o.X + w && z >= o.Z && z < o.Z + d;
    });

    public bool InPlot(int x, int z) => x >= 0 && z >= 0 && x < PlotSize && z < PlotSize;

    public string CanPlace(PlacedObject o)
    {
        var def = Catalog.Defs[o.Kind];
        var (w, d) = Footprint(o);
        for (var x = o.X; x < o.X + w; x++)
            for (var z = o.Z; z < o.Z + d; z++)
            {
                if (!InPlot(x, z)) return "msg_outside_plot";
                if (ObjectAt(x, z) != null) return "msg_occupied";
                if (def.NeedsFoundation && !HasFoundation(x, z)) return "msg_needs_foundation";
                if (!def.NeedsFoundation && o.Kind != BuildKind.Foundation && HasFoundation(x, z)) return "msg_outside_only";
            }
        if (Money < def.Cost) return "msg_no_money";
        return null;
    }

    // ---- derived resources ----
    public double PowerDemandKw => Objects.Sum(o => Catalog.Defs[o.Kind].PowerKw);
    public double CoolingDemandKw => Objects.Where(o => o.Kind == BuildKind.Rack).Sum(o => Catalog.Defs[o.Kind].PowerKw);
    public double CoolingCapacityKw => Objects.Sum(o => Catalog.Defs[o.Kind].CoolingKw);
    /// <summary>Backup capacity with N+1: the largest generator is kept in reserve.</summary>
    public double BackupCapacityKw
    {
        get
        {
            var gens = Objects.Where(o => o.Kind == BuildKind.Generator).Select(o => Catalog.Defs[o.Kind].GenKw).OrderByDescending(k => k).ToList();
            return gens.Count <= 1 ? 0 : gens.Skip(1).Sum();
        }
    }
    public double GridCapacityKw => 2000; // 2 MVA starting connection from the grid operator
    public int Racks => Objects.Count(o => o.Kind == BuildKind.Rack);
    public int FiberPits => Objects.Count(o => o.Kind == BuildKind.FiberPit);

    /// <summary>Advance time; costs and income are a placeholder until the economy step.</summary>
    public void Advance(double hours)
    {
        var before = (int)(Hours / 24);
        Hours += hours;
        var days = (int)(Hours / 24) - before;
        for (var i = 0; i < days; i++)
        {
            var energy = PowerDemandKw * 24 * 0.18;       // €0.18 per kWh
            var rent = Racks * 900.0 / 30;                // €900 per rack per month
            Money += (long)Math.Round(rent - energy - 400); // site upkeep
        }
    }
}
