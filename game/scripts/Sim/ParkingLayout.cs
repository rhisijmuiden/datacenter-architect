using System;
using System.Collections.Generic;
using System.Linq;

namespace DatacenterArchitect.Sim;

/// <summary>
/// Paints parking bays on parking surface: per connected patch, rows of 2.5 m wide and 5 m deep bays along the long side.
/// A patch of 10 m or more deep gets two rows facing each other with the aisle between; 5 to 9 m gets one row on the side
/// away from the road it is reached from; a narrow strip gets one row across its full depth.
/// Used both for the painted lines in the world and for the preview while dragging.
/// </summary>
public static class ParkingLayout
{
    public const double BayWidth = 2.5, BayDepth = 5;

    /// <summary>A painted line from (Ax, Az) to (Bx, Bz) in world metres (one cell is one metre).</summary>
    public readonly record struct Line(double Ax, double Az, double Bx, double Bz)
    {
        public double Length => Math.Sqrt((Bx - Ax) * (Bx - Ax) + (Bz - Az) * (Bz - Az));
    }

    /// <summary>
    /// All bay lines on an <paramref name="n"/>×<paramref name="n"/> grid where <paramref name="parking"/> says which cells
    /// are parking. <paramref name="road"/> tells which other cells are driven on (asphalt), to put the aisle next to them.
    /// <paramref name="only"/> limits the result to patches that touch these cells (for the drag preview).
    /// </summary>
    public static List<Line> Lines(int n, Func<int, int, bool> parking, Func<int, int, bool> road = null, (int x0, int z0, int x1, int z1)? only = null)
    {
        var lines = new List<Line>();
        var seen = new bool[n * n];
        var stack = new Stack<(int x, int z)>();
        for (var z0 = 0; z0 < n; z0++)
            for (var x0 = 0; x0 < n; x0++)
            {
                if (seen[z0 * n + x0] || !parking(x0, z0)) continue;
                // flood the patch and take its bounding box
                int minX = x0, maxX = x0, minZ = z0, maxZ = z0;
                var touches = only == null;
                stack.Push((x0, z0)); seen[z0 * n + x0] = true;
                while (stack.Count > 0)
                {
                    var (x, z) = stack.Pop();
                    minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); minZ = Math.Min(minZ, z); maxZ = Math.Max(maxZ, z);
                    if (only is { } o && x >= o.x0 && x <= o.x1 && z >= o.z0 && z <= o.z1) touches = true;
                    foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                    {
                        int nx = x + dx, nz = z + dz;
                        if (nx < 0 || nz < 0 || nx >= n || nz >= n || seen[nz * n + nx] || !parking(nx, nz)) continue;
                        seen[nz * n + nx] = true; stack.Push((nx, nz));
                    }
                }
                if (touches) Patch(lines, parking, road, minX, minZ, maxX, maxZ, n);
            }
        return lines;
    }

    /// <summary>Number of bays: lines that share a row span form one row, and a row of k lines holds k - 1 bays.</summary>
    public static int Bays(IEnumerable<Line> lines)
    {
        var rows = new Dictionary<(bool, double, double, double), int>();
        foreach (var l in lines)
        {
            var vertical = Math.Abs(l.Ax - l.Bx) < 1e-6; // a divider running along z
            var key = vertical ? (true, Math.Round(l.Az, 2), Math.Round(l.Bz, 2), 0.0) : (false, Math.Round(l.Ax, 2), Math.Round(l.Bx, 2), 0.0);
            rows[key] = rows.GetValueOrDefault(key) + 1;
        }
        var n = 0;
        foreach (var c in rows.Values) n += Math.Max(0, c - 1);
        return n;
    }

    static void Patch(List<Line> lines, Func<int, int, bool> parking, Func<int, int, bool> road, int minX, int minZ, int maxX, int maxZ, int n)
    {
        var w = maxX - minX + 1; var h = maxZ - minZ + 1;
        var alongX = w >= h; // bays line up along the long side
        int L = alongX ? w : h, S = alongX ? h : w;
        if (L < 2) return;
        // (along, across) -> cell
        (int x, int z) Cell(int a, int c) => alongX ? (minX + a, minZ + c) : (minX + c, minZ + a);
        bool P(int a, int c) { var (x, z) = Cell(a, c); return x >= 0 && z >= 0 && x < n && z < n && parking(x, z); }
        bool R(int a, int c) { var (x, z) = Cell(a, c); return road != null && x >= 0 && z >= 0 && x < n && z < n && road(x, z); }

        var rows = new List<(double d0, double d1)>();
        if (S >= 2 * BayDepth) { rows.Add((0, BayDepth)); rows.Add((S - BayDepth, S)); }
        else if (S >= BayDepth)
        {
            // the aisle goes next to the road the patch is reached from, so the bays sit on the far side
            int nearStart = 0, nearEnd = 0;
            for (var a = 0; a < L; a++) { if (R(a, -1)) nearStart++; if (R(a, S)) nearEnd++; }
            rows.Add(nearStart > nearEnd ? (S - BayDepth, S) : (0, BayDepth));
        }
        else rows.Add((0, S));

        foreach (var (d0, d1) in rows)
        {
            var bays = (int)Math.Floor(L / BayWidth + 1e-6);
            if (bays < 1) continue;
            var start = (L - bays * BayWidth) / 2; // centre the bays, so the spare bit is split over both ends
            for (var k = 0; k <= bays; k++)
            {
                var p = start + k * BayWidth;
                // only where the whole bay edge lies on parking (odd shapes, buildings in the way)
                var cellA = Math.Clamp((int)Math.Floor(p - (k == bays ? 0.01 : -0.01)), 0, L - 1);
                var ok = true;
                for (var c = (int)d0; c < (int)Math.Ceiling(d1) && ok; c++) ok = P(cellA, c);
                if (!ok) continue;
                // leave a little gap at the aisle side, like real bay markings
                var a0 = d0 + (d0 > 0 ? 0.35 : 0.15); var a1 = d1 - (d1 < S ? 0.35 : 0.15);
                lines.Add(alongX
                    ? new Line(minX + p, minZ + a0, minX + p, minZ + a1)
                    : new Line(minX + a0, minZ + p, minX + a1, minZ + p));
            }
        }
    }

    /// <summary>
    /// One parking bay: its centre, the yaw of its long axis (0 = along z, π/2 = along x), its size, and whether it is an
    /// EV bay next to a charging point. <see cref="Hash"/> is fixed per position, for car models and colours.
    /// </summary>
    public readonly record struct Bay(double X, double Z, double Yaw, double Width, double Depth, bool Ev, uint Hash);

    /// <summary>Bays between neighbouring dividers of the same row; bays within 3.2 m of a charging point are EV bays.</summary>
    public static List<Bay> BayList(List<Line> lines, IEnumerable<(double x, double z)> chargers = null)
    {
        var ch = chargers?.ToList() ?? new List<(double x, double z)>();
        var rows = new Dictionary<(bool, double, double), List<Line>>();
        foreach (var l in lines)
        {
            var vertical = Math.Abs(l.Ax - l.Bx) < 1e-6;
            var key = vertical ? (true, Math.Round(l.Az, 2), Math.Round(l.Bz, 2)) : (false, Math.Round(l.Ax, 2), Math.Round(l.Bx, 2));
            if (!rows.TryGetValue(key, out var list)) rows[key] = list = new();
            list.Add(l);
        }
        var bays = new List<Bay>();
        foreach (var (key, row) in rows.OrderBy(r => r.Key.Item1).ThenBy(r => r.Key.Item2).ThenBy(r => r.Key.Item3))
        {
            // dividers along z sit side by side in x, and the other way round
            var sorted = key.Item1 ? row.OrderBy(l => l.Ax).ToList() : row.OrderBy(l => l.Az).ToList();
            for (var i = 0; i + 1 < sorted.Count; i++)
            {
                var a = sorted[i]; var b = sorted[i + 1];
                var cx = (a.Ax + a.Bx + b.Ax + b.Bx) / 4; var cz = (a.Az + a.Bz + b.Az + b.Bz) / 4;
                var h = (uint)((int)Math.Round(cx * 10) * 73856093) ^ (uint)((int)Math.Round(cz * 10) * 19349663);
                var bw = key.Item1 ? Math.Abs(b.Ax - a.Ax) : Math.Abs(b.Az - a.Az);
                var bd = key.Item1 ? Math.Abs(a.Bz - a.Az) : Math.Abs(a.Bx - a.Ax);
                var ev = ch.Any(c => Math.Sqrt((c.x - cx) * (c.x - cx) + (c.z - cz) * (c.z - cz)) < 3.2);
                bays.Add(new Bay(cx, cz, key.Item1 ? 0 : Math.PI / 2, bw, bd, ev, h));
            }
        }
        return bays;
    }

    /// <summary>The bays of a site, exactly as they are painted (curved roads cut through parking).</summary>
    public static List<Bay> SiteBays(GameState s)
    {
        var n = s.PlotSize;
        var curve = s.Curves.SelectMany(c => c.Cells).ToHashSet();
        var lines = Lines(n, (x, z) => s.SurfaceAt(x, z) == Surface.Parking && !curve.Contains(z * n + x),
            (x, z) => s.SurfaceAt(x, z) == Surface.Asphalt || curve.Contains(z * n + x));
        return BayList(lines, Commute.Chargers(s).Select(o => (o.X + 0.5, o.Z + 0.5)));
    }
}
