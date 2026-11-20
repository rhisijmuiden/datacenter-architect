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
        _areas.Clear();
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
                        ok = g != null && Catalog.IsGate(g.Kind);
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
            if (!o.Def.Blocks || o.Floor < 0 || o.Floor >= floors || (o.Def.Chain && o.Status < ObjStatus.Placed)) continue;
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
                if (t && _s.FenceAt(x, z) != FenceType.None) t = _s.ObjectAt(x, z) is { } gate && Catalog.IsVehicleGate(gate.Kind);
                _truck[i] = t;
            }
        // trucks prefer the middle of a road: cells next to the edge cost a bit more
        _truckCost = new float[_n * _n];
        for (var z = 0; z < _n; z++)
            for (var x = 0; x < _n; x++)
            {
                var i = z * _n + x;
                if (!_truck[i]) continue;
                var edge = 3;
                for (var r = 1; r <= 2 && edge == 3; r++)
                    foreach (var (dx, dz) in Dirs)
                        if (!_s.InPlot(x + dx * r, z + dz * r) ? false : !_truck[(z + dz * r) * _n + x + dx * r]) { edge = r; break; }
                _truckCost[i] = 1f + (3 - edge) * 0.35f;
            }
    }

    float[] _truckCost;

    /// <summary>
    /// Moves the waypoints of a truck route to the middle of the paved strip it drives on, so it no longer scrapes
    /// along a fence or kerb. Measures across the direction of travel on the same surface, up to 8 m each way.
    /// </summary>
    public List<(double x, double z)> CenterLane(IReadOnlyList<Pt> path)
    {
        Rebuild();
        var res = new List<(double x, double z)>();
        for (var i = 0; i < path.Count; i++)
        {
            var p = path[i];
            var a = i > 0 ? path[i - 1] : p; var b = i < path.Count - 1 ? path[i + 1] : p;
            int dx = Math.Sign(b.X - a.X), dz = Math.Sign(b.Z - a.Z);
            double ox = 0, oz = 0;
            // straight on (not at a corner): measure the width across
            if ((dx == 0) != (dz == 0))
            {
                int px = dz, pz = dx; // perpendicular
                var surf = _s.SurfaceAt(p.X, p.Z);
                bool Same(int x, int z) => _s.InPlot(x, z) && _truck[z * _n + x] && _s.SurfaceAt(x, z) == surf;
                int left = 0, right = 0;
                while (left < 8 && Same(p.X - px * (left + 1), p.Z - pz * (left + 1))) left++;
                while (right < 8 && Same(p.X + px * (right + 1), p.Z + pz * (right + 1))) right++;
                var shift = (right - left) / 2.0;
                ox = px * shift; oz = pz * shift;
            }
            res.Add((p.X + 0.5 + ox, p.Z + 0.5 + oz));
        }
        // corners: take the lane offset of the straight parts on both sides, so the truck turns in its lane
        for (var i = 1; i < res.Count - 1; i++)
        {
            var p = path[i];
            var a = path[i - 1]; var b = path[i + 1];
            if ((Math.Sign(b.X - a.X) == 0) == (Math.Sign(b.Z - a.Z) == 0))
            {
                // the corner sits on the line of the incoming lane and on the line of the outgoing lane
                var inc = res[i - 1]; var outg = res[i + 1];
                var horizontalIn = a.Z == p.Z;
                res[i] = horizontalIn ? (outg.x, inc.z) : (inc.x, outg.z);
            }
        }
        // drop points on a straight line, so small jitter does not show as wobbling
        var simple = new List<(double x, double z)>();
        foreach (var q in res)
        {
            if (simple.Count >= 2)
            {
                var (ax, az) = simple[^2]; var (bx, bz) = simple[^1];
                var cross = (bx - ax) * (q.z - az) - (bz - az) * (q.x - ax);
                if (Math.Abs(cross) < 1e-6 && (bx - ax) * (q.x - bx) + (bz - az) * (q.z - bz) >= 0) { simple[^1] = q; continue; }
            }
            simple.Add(q);
        }
        return simple;
    }

    public bool Walkable(int x, int z, int f)
    {
        Rebuild();
        return _s.InPlot(x, z) && f >= 0 && f < _walk.Length && _walk[f][z * _n + x];
    }

    /// <summary>
    /// One step for the first-person walker from a cell to a neighbouring one (also diagonal): the target must be walkable
    /// and a wall may only be crossed through a door. Outside the plot the walker may roam the grass on the ground floor.
    /// </summary>
    public bool CanStep(int x, int z, int nx, int nz, int f)
    {
        Rebuild();
        if (x == nx && z == nz) return true;
        bool Open(int cx, int cz) => _s.InPlot(cx, cz) ? _walk[f][cz * _n + cx] : f == 0;
        bool Edge(int ax, int az, int dx, int dz) => Cross(f, EdgeBetween(ax, az, dx, dz), NavMode.Walk);
        if (f < 0 || f >= _walk.Length || !Open(nx, nz)) return false;
        int sx = Math.Sign(nx - x), sz = Math.Sign(nz - z);
        if (Math.Abs(nx - x) > 1 || Math.Abs(nz - z) > 1) return false;
        if (sx != 0 && sz != 0)
            // diagonal: one of the two L-shaped routes must be open
            return Open(x + sx, z) && Edge(x, z, sx, 0) && Edge(x + sx, z, 0, sz) || Open(x, z + sz) && Edge(x, z, 0, sz) && Edge(x, z + sz, sx, 0);
        return Edge(x, z, sx, sz);
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

    // connected areas per floor and mode: cells with the same label can reach each other
    readonly Dictionary<(int f, NavMode m), int[]> _areas = new();

    int[] Areas(int f, NavMode m)
    {
        if (_areas.TryGetValue((f, m), out var label)) return label;
        label = new int[_n * _n];
        var next = 0;
        var stack = new Stack<int>();
        for (var i = 0; i < label.Length; i++)
        {
            if (label[i] != 0 || !Pass(i % _n, i / _n, f, m)) continue;
            label[i] = ++next; stack.Push(i);
            while (stack.Count > 0)
            {
                var cur = stack.Pop();
                int cx = cur % _n, cz = cur / _n;
                foreach (var (dx, dz) in Dirs)
                {
                    int nx = cx + dx, nz = cz + dz;
                    if (!_s.InPlot(nx, nz)) continue;
                    var ni = nz * _n + nx;
                    if (label[ni] != 0 || !Pass(nx, nz, f, m) || !Cross(f, EdgeBetween(cx, cz, dx, dz), m)) continue;
                    label[ni] = next; stack.Push(ni);
                }
            }
        }
        _areas[(f, m)] = label;
        return label;
    }

    /// <summary>
    /// False when no path can join the two cells on this floor (for example a spot boxed in between racks, or a hall
    /// without a door). Answers from a labelling of connected areas that is made once per layout change, so walkers
    /// do not search the whole site again and again for a place they cannot reach.
    /// </summary>
    public bool Connected(int ax, int az, int bx, int bz, int f, NavMode m)
    {
        Rebuild();
        if (!_s.InPlot(ax, az) || !_s.InPlot(bx, bz) || f < 0 || f >= _walk.Length) return false;
        var label = Areas(f, m);
        var a = label[az * _n + ax]; var b = label[bz * _n + bx];
        // a start or target that is not itself passable is left to the full search, which has its own rules for it
        return a == 0 || b == 0 || a == b;
    }

    // search buffers, reused between searches: a cell counts as unvisited unless its stamp is the current one
    float[] _dist = Array.Empty<float>();
    int[] _prev = Array.Empty<int>(), _stampOf = Array.Empty<int>();
    int _stamp;
    readonly PriorityQueue<int, float> _queue = new();

    /// <summary>
    /// Shortest path on one floor from any start to any cell matching goal. Returns cells after the start, or null.
    /// With <paramref name="target"/> (the one cell the goal accepts) the search is aimed at it (A*), which finds a
    /// path of the same length while looking at far fewer cells in big halls.
    /// </summary>
    public List<Pt> FindOnFloor(IEnumerable<(int x, int z)> starts, int f, Func<int, int, bool> goal, NavMode m, (int x, int z)? target = null)
    {
        Rebuild();
        if (target is { } tg && starts is (int, int)[] { Length: 1 } one && !Connected(one[0].Item1, one[0].Item2, tg.x, tg.z, f, m)) return null;
        var size = _n * _n;
        if (_dist.Length != size) { _dist = new float[size]; _prev = new int[size]; _stampOf = new int[size]; _stamp = 0; }
        if (++_stamp == int.MaxValue) { Array.Clear(_stampOf); _stamp = 1; }
        var dist = _dist; var prev = _prev; var seen = _stampOf; var stamp = _stamp;
        // cheapest cell cost, so the estimate never overshoots and the path stays the shortest
        var minCost = m == NavMode.Truck ? 1f : 0.8f;
        float H(int x, int z) => target is { } t ? (Math.Abs(t.x - x) + Math.Abs(t.z - z)) * minCost : 0f;
        var q = _queue; q.Clear();
        foreach (var (x, z) in starts)
        {
            if (!_s.InPlot(x, z)) continue;
            var i = z * _n + x;
            dist[i] = 0; prev[i] = -1; seen[i] = stamp; q.Enqueue(i, H(x, z));
        }
        while (q.TryDequeue(out var cur, out var pri))
        {
            int cx = cur % _n, cz = cur / _n;
            var d = dist[cur];
            if (pri > d + H(cx, cz)) continue; // a stale entry: this cell was reached cheaper since
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
                var nd = d + (m == NavMode.Truck ? _truckCost[ni] : _cost[ni]);
                if (seen[ni] == stamp && nd >= dist[ni]) continue;
                seen[ni] = stamp; dist[ni] = nd; prev[ni] = cur; q.Enqueue(ni, nd + H(nx, nz));
            }
        }
        return null;
    }

    /// <summary>Path that may change floors through stairs or a lift. Walkers take whichever link gives the shortest
    /// trip (stairs win for a floor or two, a lift for more); carried loads need the freight lift.</summary>
    public List<Pt> Find(Pt start, int goalFloor, Func<int, int, bool> goal, NavMode m, (int x, int z)? target = null)
    {
        var starts = new[] { (start.X, start.Z) };
        if (start.F == goalFloor) return FindOnFloor(starts, start.F, goal, m, target);
        List<Pt> best = null; var bestCost = double.MaxValue;
        var floors = Math.Abs(goalFloor - start.F);
        foreach (var link in _s.Objects.Where(o => Catalog.ShaftAllows(o.Kind, m)))
        {
            var b = _s.BuildingAt(link.X, link.Z);
            if (b == null || b.Stage != Stage.Done || b.Floors <= Math.Max(start.F, goalFloor)) continue;
            var cells = GameState.Cells(link).ToHashSet();
            var a = FindOnFloor(starts, start.F, (x, z) => cells.Contains((x, z)), m);
            if (a == null) continue;
            var at = a.Count > 0 ? a[^1] : start;
            var c = FindOnFloor(new[] { (at.X, at.Z) }, goalFloor, goal, m, target);
            if (c == null) continue;
            var cost = a.Count + c.Count + LinkCost(link.Kind, floors);
            if (cost >= bestCost) continue;
            a.Add(new Pt(at.X, at.Z, goalFloor));
            a.AddRange(c);
            best = a; bestCost = cost;
        }
        return best;
    }

    /// <summary>Extra steps a floor link is worth: waiting for a lift, or climbing stairs floor by floor.</summary>
    public static double LinkCost(ObjKind k, int floors) => k == ObjKind.Stairs ? 7 * floors : 12 + 2 * floors;
}
