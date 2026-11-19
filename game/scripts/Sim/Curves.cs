using System;
using System.Collections.Generic;
using System.Linq;

namespace DatacenterArchitect.Sim;

/// <summary>
/// A curved road, footpath or bike path drawn with the curve tool: a quadratic Bézier from A through
/// control point C to B, with a width in metres. The cells it paves are stored, so the view can draw the
/// smooth strip there instead of square tiles, and erasing them removes the curve.
/// </summary>
public class CurvePath
{
    public Surface Surface { get; set; }
    public double Ax { get; set; }
    public double Az { get; set; }
    public double Cx { get; set; }
    public double Cz { get; set; }
    public double Bx { get; set; }
    public double Bz { get; set; }
    public double Width { get; set; } = 3;
    /// <summary>Cells (z * plot size + x) that this curve paved.</summary>
    public List<int> Cells { get; set; } = new();

    public (double x, double z) At(double t)
    {
        var u = 1 - t;
        return (u * u * Ax + 2 * u * t * Cx + t * t * Bx, u * u * Az + 2 * u * t * Cz + t * t * Bz);
    }

    /// <summary>Unit direction of travel at t.</summary>
    public (double x, double z) Tangent(double t)
    {
        var dx = 2 * (1 - t) * (Cx - Ax) + 2 * t * (Bx - Cx);
        var dz = 2 * (1 - t) * (Cz - Az) + 2 * t * (Bz - Cz);
        var l = Math.Sqrt(dx * dx + dz * dz);
        if (l < 1e-9) { dx = Bx - Ax; dz = Bz - Az; l = Math.Max(1e-9, Math.Sqrt(dx * dx + dz * dz)); }
        return (dx / l, dz / l);
    }

    public double Length
    {
        get
        {
            double len = 0; var p = At(0);
            for (var i = 1; i <= 64; i++) { var q = At(i / 64.0); len += Math.Sqrt((q.x - p.x) * (q.x - p.x) + (q.z - p.z) * (q.z - p.z)); p = q; }
            return len;
        }
    }

    /// <summary>Evenly spaced points along the curve, about every <paramref name="step"/> metres.</summary>
    public List<double> Steps(double step = 0.33)
    {
        var n = Math.Max(2, (int)Math.Ceiling(Length / step));
        return Enumerable.Range(0, n + 1).Select(i => i / (double)n).ToList();
    }

    /// <summary>Cells whose centre lies within half the width (plus a little) of the curve.</summary>
    public IEnumerable<(int x, int z)> Covered(int plot)
    {
        var pts = Steps(0.25).Select(At).ToList();
        var reach = Width / 2 + 0.2;
        var minX = (int)Math.Floor(pts.Min(p => p.x) - reach); var maxX = (int)Math.Ceiling(pts.Max(p => p.x) + reach);
        var minZ = (int)Math.Floor(pts.Min(p => p.z) - reach); var maxZ = (int)Math.Ceiling(pts.Max(p => p.z) + reach);
        for (var z = Math.Max(0, minZ); z <= Math.Min(plot - 1, maxZ); z++)
            for (var x = Math.Max(0, minX); x <= Math.Min(plot - 1, maxX); x++)
            {
                double cx = x + 0.5, cz = z + 0.5, best = double.MaxValue;
                for (var i = 0; i + 1 < pts.Count; i++) best = Math.Min(best, SegDist(cx, cz, pts[i], pts[i + 1]));
                if (best <= reach) yield return (x, z);
            }
    }

    static double SegDist(double px, double pz, (double x, double z) a, (double x, double z) b)
    {
        double dx = b.x - a.x, dz = b.z - a.z, l2 = dx * dx + dz * dz;
        var t = l2 < 1e-12 ? 0 : Math.Clamp(((px - a.x) * dx + (pz - a.z) * dz) / l2, 0, 1);
        double qx = a.x + t * dx - px, qz = a.z + t * dz - pz;
        return Math.Sqrt(qx * qx + qz * qz);
    }

    /// <summary>Surfaces the curve tool can draw, with their default width in metres.</summary>
    public static readonly IReadOnlyDictionary<Surface, int> DefaultWidth = new Dictionary<Surface, int>
    {
        [Surface.Asphalt] = 4, [Surface.Footpath] = 2, [Surface.BikePath] = 2,
    };
    public const int MinWidth = 1, MaxWidth = 8;
}
