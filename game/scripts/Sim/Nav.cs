using System;
using System.Collections.Generic;
using System.Linq;

namespace DatacenterArchitect.Sim;

public enum NavMode { Walk, Carry, Truck }

public readonly record struct Pt(int X, int Z, int F);

/// <summary>
/// Grid pathfinding on the site. Walls sit on cell edges and can only be crossed through a door;
/// carrying a rack needs a wide door. Trucks stay on asphalt, parking and the yard.
/// Floors are joined by freight lifts.
/// </summary>
public class Nav
{
    readonly GameState _s;
    int _rev = -1, _n;
    bool[][] _walk;
    bool[] _truck;
    float[] _cost;
    HashSet<(int, int, int)>[] _walls;
    Dictionary<(int, int, int), bool>[] _doors;

    public Nav(GameState s) { _s = s; }

    void Rebuild()
    {
        if (_rev == _s.Revision && _walk != null) return;
        _rev = _s.Revision;
        _n = _s.PlotSize;
        var floors = Catalog.MaxFloors;
        _walk = new bool[floors][];
        _walls = new HashSet<(int, int, int)>[floors];
        _doors = new Dictionary<(int, int, int), bool>[floors];
        _truck = new bool[_n * _n];
        _cost = new float[_n * _n];
        for (var f = 0; f < floors; f++)
        {
            var w = new bool[_n * _n];
            for (var z = 0; z < _n; z++)
                for (var x = 0; x < _n; x++)
                {
                    bool ok;
                    if (f == 0) ok = !_s.InStation(x, z);
                    else { var b = _s.BuildingAt(x, z); ok = b != null && b.Stage == Stage.Done && b.Floors > f; }
                    if (ok && f == 0 && _s.FenceAt(x, z) != FenceType.None)
                    {
                        var g = _s.ObjectAt(x, z);
                        ok = g != null && g.Kind is ObjKind.SlidingGate or ObjKind.PedGate;
                    }
                    w[z * _n + x] = ok;
                }
            _walls[f] = _s.WallEdges(f, true);
            _doors[f] = new();
            foreach (var d in _s.Doors.Where(d => d.Floor == f))
                foreach (var e in d.Edges()) _doors[f][e] = Catalog.Doors[d.Type].Wide;
            _walk[f] = w;
        }
        foreach (var o in _s.Objects)
        {
            if (!o.Def.Blocks || (o.Def.Chain && o.Status < ObjStatus.Placed)) continue;
            foreach (var (x, z) in GameState.Cells(o))
                if (_s.InPlot(x, z)) _walk[o.Floor][z * _n + x] = false;
        }
        for (var z = 0; z < _n; z++)
            for (var x = 0; x < _n; x++)
            {
                var i = z * _n + x;
                var surf = _s.SurfaceAt(x, z);
                _cost[i] = surf == Surface.Grass ? 1f : 0.8f;
                var t = _walk[0][i] && Catalog.Surfaces[surf].Trucks && _s.BuildingAt(x, z) == null;
                if (t && _s.FenceAt(x, z) != FenceType.None) t = _s.ObjectAt(x, z)?.Kind == ObjKind.SlidingGate;
                _truck[i] = t;
            }
    }

    public bool Walkable(int x, int z, int f)
    {
        Rebuild();
        return _s.InPlot(x, z) && f >= 0 && f < _walk.Length && _walk[f][z * _n + x];
    }

    public bool TruckCell(int x, int z) { Rebuild(); return _s.InPlot(x, z) && _truck[z * _n + x]; }

    bool Pass(int x, int z, int f, NavMode m) => m == NavMode.Truck ? _truck[z * _n + x] : _walk[f][z * _n + x];

    bool Cross(int f, (int, int, int) edge, NavMode m)
    {
        if (m == NavMode.Truck || !_walls[f].Contains(edge)) return true;
        return _doors[f].TryGetValue(edge, out var wide) && (m == NavMode.Walk || wide);
    }

    static readonly (int dx, int dz)[] Dirs = { (1, 0), (-1, 0), (0, 1), (0, -1) };

    static (int, int, int) EdgeBetween(int x, int z, int dx, int dz) =>
        dx == 1 ? (x + 1, z, 0) : dx == -1 ? (x, z, 0) : dz == 1 ? (x, z + 1, 1) : (x, z, 1);

    /// <summary>Shortest path on one floor from any start to any cell matching goal. Returns cells after the start, or null.</summary>
    public List<Pt> FindOnFloor(IEnumerable<(int x, int z)> starts, int f, Func<int, int, bool> goal, NavMode m)
    {
        Rebuild();
        var dist = new float[_n * _n];
        Array.Fill(dist, float.MaxValue);
        var prev = new int[_n * _n];
        Array.Fill(prev, -1);
        var q = new PriorityQueue<int, float>();
        foreach (var (x, z) in starts)
        {
            if (!_s.InPlot(x, z)) continue;
            var i = z * _n + x;
            dist[i] = 0; q.Enqueue(i, 0);
        }
        while (q.TryDequeue(out var cur, out var d))
        {
            if (d > dist[cur]) continue;
            int cx = cur % _n, cz = cur / _n;
            if (goal(cx, cz))
            {
                var path = new List<Pt>();
                for (var i = cur; prev[i] != -1; i = prev[i]) path.Add(new Pt(i % _n, i / _n, f));
                path.Reverse();
                return path;
            }
            foreach (var (dx, dz) in Dirs)
            {
                int nx = cx + dx, nz = cz + dz;
                if (!_s.InPlot(nx, nz)) continue;
                if (!Pass(nx, nz, f, m) && !goal(nx, nz)) continue;
                if (!Cross(f, EdgeBetween(cx, cz, dx, dz), m)) continue;
                var ni = nz * _n + nx;
                var nd = d + (m == NavMode.Truck ? 1f : _cost[ni]);
                if (nd >= dist[ni]) continue;
                dist[ni] = nd; prev[ni] = cur; q.Enqueue(ni, nd);
            }
        }
        return null;
    }

    /// <summary>Path that may change floors through a freight lift.</summary>
    public List<Pt> Find(Pt start, int goalFloor, Func<int, int, bool> goal, NavMode m)
    {
        var starts = new[] { (start.X, start.Z) };
        if (start.F == goalFloor) return FindOnFloor(starts, start.F, goal, m);
        foreach (var lift in _s.Objects.Where(o => o.Kind == ObjKind.Lift))
        {
            var b = _s.BuildingAt(lift.X, lift.Z);
            if (b == null || b.Stage != Stage.Done || b.Floors <= Math.Max(start.F, goalFloor)) continue;
            var cells = GameState.Cells(lift).ToHashSet();
            var a = FindOnFloor(starts, start.F, (x, z) => cells.Contains((x, z)), m);
            if (a == null) continue;
            var at = a.Count > 0 ? a[^1] : start;
            var c = FindOnFloor(new[] { (at.X, at.Z) }, goalFloor, goal, m);
            if (c == null) continue;
            a.Add(new Pt(at.X, at.Z, goalFloor));
            a.AddRange(c);
            return a;
        }
        return null;
    }
}
