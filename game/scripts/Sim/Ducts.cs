using System;
using System.Collections.Generic;
using System.Linq;

namespace DatacenterArchitect.Sim;

/// <summary>
/// A run of your own main fiber duct on the plot (infra sketch board 5): straight legs along x or z with 90° corners,
/// from a cable entry on the plot border to the building with the meet-me room. A 4-, 7- or 12-way duct holds that many
/// subducts; every carrier port, IX port and dark fiber takes one along its whole route. The game puts a handhole at
/// both ends and at every corner.
/// </summary>
public class FiberDuct
{
    public int Id { get; set; }
    public int Ways { get; set; } = 4;
    /// <summary>Corner points of the run as cells: x0, z0, x1, z1, ... Each leg is straight along x or along z.</summary>
    public List<int> Points { get; set; } = new();

    public int Corners => Math.Max(0, Points.Count / 2 - 2);

    /// <summary>A duct from a to b with one 90° corner: first along x, then along z (or the other way round).</summary>
    public static FiberDuct L(int ax, int az, int bx, int bz, bool zFirst, int ways)
    {
        var d = new FiberDuct { Ways = ways };
        d.Points.AddRange(new[] { ax, az });
        if (ax != bx && az != bz) d.Points.AddRange(zFirst ? new[] { ax, bz } : new[] { bx, az });
        if (ax != bx || az != bz) d.Points.AddRange(new[] { bx, bz });
        return d;
    }

    public IEnumerable<(int x, int z)> Corner()
    {
        for (var i = 0; i + 1 < Points.Count; i += 2) yield return (Points[i], Points[i + 1]);
    }

    /// <summary>Every cell the duct runs through, from start to end, without doubles at the corners.</summary>
    public List<(int x, int z)> Cells()
    {
        var list = new List<(int x, int z)>();
        var pts = Corner().ToList();
        if (pts.Count == 0) return list;
        list.Add(pts[0]);
        for (var i = 1; i < pts.Count; i++)
        {
            var (x, z) = pts[i - 1];
            var (tx, tz) = pts[i];
            var sx = Math.Sign(tx - x); var sz = Math.Sign(tz - z);
            while (x != tx || z != tz)
            {
                if (x != tx) x += sx; else z += sz;
                list.Add((x, z));
            }
        }
        return list;
    }

    /// <summary>Metres of duct: one per cell.</summary>
    public int Length => Cells().Count;

    /// <summary>Handholes: both ends and every corner.</summary>
    public List<(int x, int z)> Handholes() => Corner().Distinct().ToList();

    public void Shift(int d) { for (var i = 0; i < Points.Count; i++) Points[i] += d; }
}

/// <summary>A cable entry on the plot border: where the carriers' cables come onto your land.</summary>
public class EntryInfo
{
    public PlacedObject Entry;
    /// <summary>Side of the plot: 0 north, 1 east, 2 south, 3 west.</summary>
    public int Side;
    /// <summary>A duct route (ignoring how full it is) reaches a building with a meet-me room.</summary>
    public bool Connected;
    /// <summary>Subducts along the narrowest duct of that route.</summary>
    public int Ways;
    public int Used;
    /// <summary>Ducts the connections of this entry run through.</summary>
    public readonly HashSet<int> Ducts = new();
}

public class DuctInfo
{
    public FiberDuct Duct;
    public int Used;
    /// <summary>The duct ends at (or runs along) a building with a meet-me room.</summary>
    public bool ToMmr;
    /// <summary>A cable entry is on the duct, or on a duct joined to it.</summary>
    public bool FromEntry;
    public Building Mmr;
}

/// <summary>
/// The ducts as a graph: two ducts are joined when they share a cell or lie side by side; an entry is on a duct when the
/// duct runs through or next to it; a duct reaches a meet-me room when it touches the building that holds a finished MMR.
/// Connections are routed one by one over ducts that still have a free subduct (breadth first, fewest ducts).
/// </summary>
public class DuctGraph
{
    public readonly List<DuctInfo> Ducts = new();
    public readonly List<EntryInfo> Entries = new();
    readonly List<List<int>> _adj = new();
    readonly List<List<int>> _start = new();

    public DuctGraph(GameState s)
    {
        var mmrBuildings = s.Rooms.Where(r => r.Type == RoomType.MeetMe && r.Stage == Stage.Done)
            .Select(r => s.Buildings.FirstOrDefault(b => b.Id == r.BuildingId)).Where(b => b != null).Distinct().ToList();
        var cells = new List<HashSet<(int, int)>>();
        foreach (var d in s.Ducts)
        {
            var set = d.Cells().ToHashSet();
            cells.Add(set);
            var mmr = mmrBuildings.FirstOrDefault(b => set.Any(c => c.Item1 >= b.X - 1 && c.Item1 <= b.X + b.W && c.Item2 >= b.Z - 1 && c.Item2 <= b.Z + b.D));
            Ducts.Add(new DuctInfo { Duct = d, ToMmr = mmr != null, Mmr = mmr });
            _adj.Add(new List<int>());
        }
        static bool Near(HashSet<(int, int)> a, (int x, int z) c) =>
            a.Contains(c) || a.Contains((c.x + 1, c.z)) || a.Contains((c.x - 1, c.z)) || a.Contains((c.x, c.z + 1)) || a.Contains((c.x, c.z - 1));
        for (var i = 0; i < Ducts.Count; i++)
            for (var j = i + 1; j < Ducts.Count; j++)
            {
                var (small, big) = cells[i].Count <= cells[j].Count ? (cells[i], cells[j]) : (cells[j], cells[i]);
                if (small.Any(c => Near(big, c))) { _adj[i].Add(j); _adj[j].Add(i); }
            }
        foreach (var e in s.Objects.Where(o => o.Kind == ObjKind.FiberPit).OrderBy(o => o.Id))
        {
            var info = new EntryInfo { Entry = e, Side = s.BorderSide(e.X, e.Z) };
            var start = new List<int>();
            for (var i = 0; i < Ducts.Count; i++) if (Near(cells[i], (e.X, e.Z))) start.Add(i);
            Entries.Add(info);
            _start.Add(start);
            var path = Path(Entries.Count - 1, ignoreLoad: true);
            if (path != null)
            {
                info.Connected = true;
                info.Ways = path.Min(i => Ducts[i].Duct.Ways);
                foreach (var i in path) Ducts[i].FromEntry = true;
            }
        }
        // ducts joined to a duct that carries an entry count as fed too
        var queue = new Queue<int>(Enumerable.Range(0, Ducts.Count).Where(i => _start.Any(st => st.Contains(i))));
        var seen = queue.ToHashSet();
        while (queue.Count > 0)
        {
            var i = queue.Dequeue(); Ducts[i].FromEntry = true;
            foreach (var j in _adj[i]) if (seen.Add(j)) queue.Enqueue(j);
        }
    }

    static bool Usable(EntryInfo e) => e.Entry.Status == ObjStatus.Live && !e.Entry.Broken;

    /// <summary>Fewest ducts from an entry to a meet-me room, over ducts with a free subduct (or any, with ignoreLoad).</summary>
    List<int> Path(int entry, bool ignoreLoad)
    {
        bool Free(int i) => ignoreLoad || Ducts[i].Used < Ducts[i].Duct.Ways;
        var prev = new Dictionary<int, int>();
        var queue = new Queue<int>();
        foreach (var i in _start[entry].Where(Free)) { prev[i] = -1; queue.Enqueue(i); }
        while (queue.Count > 0)
        {
            var i = queue.Dequeue();
            if (Ducts[i].ToMmr)
            {
                var path = new List<int>();
                for (var k = i; k >= 0; k = prev[k]) path.Add(k);
                path.Reverse();
                return path;
            }
            foreach (var j in _adj[i]) if (!prev.ContainsKey(j) && Free(j)) { prev[j] = i; queue.Enqueue(j); }
        }
        return null;
    }

    /// <summary>Routes one more connection: over the entry with the fewest connections that still has a free route.</summary>
    public bool Take()
    {
        foreach (var k in Enumerable.Range(0, Entries.Count).Where(k => Usable(Entries[k])).OrderBy(k => Entries[k].Used).ThenBy(k => Entries[k].Entry.Id))
        {
            var path = Path(k, ignoreLoad: false);
            if (path == null) continue;
            Entries[k].Used++;
            foreach (var i in path) { Ducts[i].Used++; Entries[k].Ducts.Add(Ducts[i].Duct.Id); }
            return true;
        }
        return false;
    }

    /// <summary>How many more connections fit, routed the same way (without keeping them).</summary>
    public int Spare(int max = 64)
    {
        var entries = Entries.Select(e => (e.Used, e.Ducts.ToList())).ToList();
        var ducts = Ducts.Select(d => d.Used).ToList();
        var n = 0;
        while (n < max && Take()) n++;
        for (var i = 0; i < Entries.Count; i++) { Entries[i].Used = entries[i].Item1; Entries[i].Ducts.Clear(); Entries[i].Ducts.UnionWith(entries[i].Item2); }
        for (var i = 0; i < Ducts.Count; i++) Ducts[i].Used = ducts[i];
        return n;
    }

    /// <summary>Two entries in use whose connections share no duct: two separate routes.</summary>
    public bool Diverse()
    {
        var used = Entries.Where(e => e.Used > 0).ToList();
        for (var i = 0; i < used.Count; i++)
            for (var j = i + 1; j < used.Count; j++)
                if (!used[i].Ducts.Overlaps(used[j].Ducts)) return true;
        return false;
    }
}

public partial class GameState
{
    /// <summary>Your own main fiber ducts on the plot.</summary>
    public List<FiberDuct> Ducts { get; set; } = new();

    /// <summary>A cable entry stands on the plot border: within this many metres of the edge.</summary>
    public const int EntryBorder = 2;

    public bool OnBorder(int x, int z) => InPlot(x, z) && (x < EntryBorder || z < EntryBorder || x >= PlotSize - EntryBorder || z >= PlotSize - EntryBorder);

    /// <summary>The nearest plot edge: 0 north (z = 0), 1 east, 2 south (the public road), 3 west.</summary>
    public int BorderSide(int x, int z)
    {
        var d = new[] { z, PlotSize - 1 - x, PlotSize - 1 - z, x };
        return Array.IndexOf(d, d.Min());
    }

    /// <summary>A building with a meet-me room that this cell touches (inside or right next to it), or null.</summary>
    public Building MmrBuildingNear(int x, int z) =>
        Rooms.Where(r => r.Type == RoomType.MeetMe).Select(r => Buildings.FirstOrDefault(b => b.Id == r.BuildingId))
            .FirstOrDefault(b => b != null && x >= b.X - 1 && x <= b.X + b.W && z >= b.Z - 1 && z <= b.Z + b.D);

    public FiberDuct DuctAt(int x, int z) => Ducts.LastOrDefault(d => d.Cells().Contains((x, z)));

    public long DuctCost(FiberDuct d) => d.Length * Catalog.DuctCostPerM(d.Ways);

    public string CanDuct(FiberDuct d)
    {
        if (!Catalog.DuctWays.Contains(d.Ways) || d.Points.Count < 2) return "msg_nothing_changed";
        foreach (var (x, z) in d.Cells())
        {
            if (!InPlot(x, z)) return "msg_outside_plot";
            if (InStation(x, z)) return "msg_occupied";
            if (BuildingAt(x, z) != null) return "msg_duct_building";
        }
        if (!CanAfford(DuctCost(d))) return "msg_no_money";
        return null;
    }

    public string AddDuct(FiberDuct d)
    {
        var why = CanDuct(d);
        if (why != null) return why;
        Spend(DuctCost(d));
        d.Id = NextId++;
        Ducts.Add(d);
        Touch();
        return null;
    }

    public int NextWays(FiberDuct d) => Catalog.DuctWays.FirstOrDefault(w => w > d.Ways);

    /// <summary>Pull a bigger duct through the same route: you pay the difference per metre.</summary>
    public long DuctUpgradeCost(FiberDuct d) { var n = NextWays(d); return n == 0 ? 0 : d.Length * (Catalog.DuctCostPerM(n) - Catalog.DuctCostPerM(d.Ways)); }

    public string CanUpgradeDuct(FiberDuct d)
    {
        if (NextWays(d) == 0) return "msg_nothing_changed";
        return CanAfford(DuctUpgradeCost(d)) ? null : "msg_no_money";
    }

    public string UpgradeDuct(FiberDuct d)
    {
        var why = CanUpgradeDuct(d);
        if (why != null) return why;
        Spend(DuctUpgradeCost(d));
        d.Ways = NextWays(d);
        Touch(false);
        return null;
    }

    /// <summary>
    /// Lays a duct from a cable entry to the nearest building with a meet-me room (or any datacenter when there is none):
    /// the shortest route around buildings with as few corners as possible. Returns null when no route exists.
    /// </summary>
    public FiberDuct AutoDuct(PlacedObject entry, int ways, bool pay = true)
    {
        var mmr = Rooms.Where(r => r.Type == RoomType.MeetMe).Select(r => Buildings.FirstOrDefault(b => b.Id == r.BuildingId)).Where(b => b != null).ToList();
        if (mmr.Count == 0) mmr = Buildings.Where(b => b.Kind == BuildingKind.Datacenter).ToList();
        if (mmr.Count == 0) return null;
        bool Goal(int x, int z) => mmr.Any(b => x >= b.X - 1 && x <= b.X + b.W && z >= b.Z - 1 && z <= b.Z + b.D);
        bool Open(int x, int z) => InPlot(x, z) && !InStation(x, z) && BuildingAt(x, z) == null;
        // Dijkstra over (cell, direction): a step costs 1, a corner 6 more
        var n = PlotSize;
        var dist = new Dictionary<(int, int, int), int>();
        var prev = new Dictionary<(int, int, int), (int, int, int)>();
        var pq = new PriorityQueue<(int x, int z, int dir), int>();
        int[] dx = { 1, 0, -1, 0 }, dz = { 0, 1, 0, -1 };
        if (!Open(entry.X, entry.Z) && ObjectAt(entry.X, entry.Z, 0) != entry) return null;
        for (var k = 0; k < 4; k++) { dist[(entry.X, entry.Z, k)] = 0; pq.Enqueue((entry.X, entry.Z, k), 0); }
        (int, int, int)? end = null;
        while (pq.TryDequeue(out var cur, out var cost))
        {
            if (dist.TryGetValue((cur.x, cur.z, cur.dir), out var best) && best < cost) continue;
            if (Goal(cur.x, cur.z)) { end = (cur.x, cur.z, cur.dir); break; }
            for (var k = 0; k < 4; k++)
            {
                if ((k + 2) % 4 == cur.dir && cost > 0) continue;
                int nx = cur.x + dx[k], nz = cur.z + dz[k];
                if (nx < 0 || nz < 0 || nx >= n || nz >= n || !Open(nx, nz)) continue;
                var c2 = cost + 1 + (k != cur.dir && cost > 0 ? 6 : 0);
                var key = (nx, nz, k);
                if (dist.TryGetValue(key, out var old) && old <= c2) continue;
                dist[key] = c2; prev[key] = (cur.x, cur.z, cur.dir);
                pq.Enqueue((nx, nz, k), c2);
            }
        }
        if (end == null) return null;
        var cells = new List<(int x, int z)>();
        for (var k = end.Value; ; k = prev[k])
        {
            cells.Add((k.Item1, k.Item2));
            if (!prev.ContainsKey(k)) break;
        }
        cells.Reverse();
        // the duct starts next to the entry, not under it
        if (cells.Count > 1) cells.RemoveAt(0);
        var d = new FiberDuct { Ways = ways };
        for (var i = 0; i < cells.Count; i++)
        {
            var turn = i == 0 || i == cells.Count - 1 ||
                       (cells[i - 1].x - cells[i].x != cells[i].x - cells[i + 1].x) || (cells[i - 1].z - cells[i].z != cells[i].z - cells[i + 1].z);
            if (turn) d.Points.AddRange(new[] { cells[i].x, cells[i].z });
        }
        if (d.Points.Count == 2) d.Points.AddRange(new[] { cells[0].x, cells[0].z });
        if (pay)
        {
            if (AddDuct(d) != null) return null;
        }
        else { d.Id = NextId++; Ducts.Add(d); Touch(); }
        return d;
    }

    /// <summary>
    /// Save format 13: street fiber pits are gone. Your old fiber pits become cable entries and get a duct to the nearest
    /// meet-me room for free, as wide as their old subducts (4 stays 4, 8 and 12 become 12), so nothing that ran stops.
    /// </summary>
    void MigrateDucts()
    {
        Ducts ??= new();
        var pits = Objects.Where(o => o.Kind == ObjKind.FiberPit).OrderBy(o => o.Id).ToList();
        var missed = 0;
        foreach (var p in pits)
        {
            var ways = p.Level >= 2 ? 12 : 4;
            p.Level = 1;
            if (AutoDuct(p, ways, pay: false) == null) missed++;
        }
        if (pits.Count > 0) Notes.Add((missed > 0 ? "msg_ducts_new_missing" : "msg_ducts_new", null));
    }
}
