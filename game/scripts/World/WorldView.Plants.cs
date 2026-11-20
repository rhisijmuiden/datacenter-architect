using System;
using System.Collections.Generic;
using System.Linq;
using DatacenterArchitect.Sim;
using Godot;

namespace DatacenterArchitect.World;

/// <summary>
/// Trees, shrubs, flowers and grass tufts from the CC0 Kenney Nature Kit, drawn with MultiMesh instancing (one draw
/// per model) and swaying in the wind in the shader (<see cref="Foliage"/>). Grass tufts and flowers only draw near the
/// camera (visibility range), so a big site stays light.
/// </summary>
public partial class WorldView
{
    /// <summary>A tree model, the material mapping for its leaves and its scale range for a mature tree.</summary>
    public record TreeKind(string File, Func<string, Material, Material> Mats, float Scale);

    public static readonly TreeKind[] Broadleaf =
    {
        new("made/tree_oak.glb", Foliage.ByName, 1f),
        new("made/tree_lime.glb", Foliage.ByName, 1f),
        new("made/tree_maple.glb", Foliage.ByName, 1f),
        new("made/tree_birch.glb", Foliage.ByName, 1f),
        new("made/tree_poplar.glb", Foliage.ByName, 0.95f),
        new("made/tree_beech.glb", Foliage.ByName, 1f),
        new("made/tree_chestnut.glb", Foliage.ByName, 1f),
        new("made/tree_alder.glb", Foliage.ByName, 1f),
        new("made/tree_cherry.glb", Foliage.ByName, 1f),
    };
    public static readonly TreeKind[] Conifer =
    {
        new("made/tree_spruce.glb", Foliage.ByName, 1f),
        new("made/tree_fir.glb", Foliage.ByName, 1f),
        new("made/tree_pine.glb", Foliage.ByName, 1f),
        new("made/tree_larch.glb", Foliage.ByName, 1f),
    };
    static readonly (string file, Func<string, Material, Material> mats)[] Tufts =
    {
        ("made/grass_tuft.glb", Foliage.ByName), ("made/grass_tall.glb", Foliage.ByName), ("made/grass_short.glb", Foliage.ByName),
    };
    static readonly (string file, Func<string, Material, Material> mats)[] Flowers =
    {
        ("made/flower_red.glb", Foliage.ByName), ("made/flower_yellow.glb", Foliage.ByName),
        ("made/flower_purple.glb", Foliage.ByName), ("made/flower_white.glb", Foliage.ByName),
    };
    public static readonly (string file, Func<string, Material, Material> mats)[] Shrubs =
    {
        ("made/bush_round.glb", Foliage.ByName), ("made/bush_wide.glb", Foliage.ByName), ("made/bush_small.glb", Foliage.ByName),
    };

    /// <summary>A slightly different shade per tree so a forest does not look copy-pasted.</summary>
    static Color Shade(Random rng)
    {
        var v = 0.88f + (float)rng.NextDouble() * 0.24f;
        var warm = (float)rng.NextDouble() * 0.08f;
        return new Color(v + warm, v, v - warm * 0.5f);
    }

    /// <summary>Draws trees at the given spots (x, z, size 0.7..1.6), mixing broadleaf and conifers.</summary>
    static void Forest(Node3D root, List<(float x, float z, float size)> spots, Random rng, float conifers = 0.3f)
    {
        var kinds = new List<TreeKind>(Broadleaf); kinds.AddRange(Conifer);
        // one MultiMesh per model and 48 m tile, so far tiles can use the simpler level-of-detail mesh
        var groups = new Dictionary<(int k, int cx, int cz), (List<Transform3D> xf, List<Color> col)>();
        foreach (var (x, z, size) in spots)
        {
            var k = rng.NextDouble() < conifers ? Broadleaf.Length + rng.Next(Conifer.Length) : rng.Next(Broadleaf.Length);
            var sc = kinds[k].Scale * size;
            var basis = new Basis(Vector3.Up, (float)rng.NextDouble() * Mathf.Tau).Scaled(new Vector3(sc, sc * (0.9f + (float)rng.NextDouble() * 0.25f), sc));
            var key = (k, (int)Mathf.Floor(x / 48), (int)Mathf.Floor(z / 48));
            if (!groups.TryGetValue(key, out var g)) groups[key] = g = (new(), new());
            g.xf.Add(new Transform3D(basis, new Vector3(x, 0, z)));
            g.col.Add(Shade(rng));
        }
        foreach (var (key, g) in groups)
            Assets.Many(root, Assets.Mesh(kinds[key.k].File, kinds[key.k].Mats), g.xf, g.col);
    }

    /// <summary>
    /// A Dutch ditch between the road-side buildings and the fields: a row of pollard willows (knotwilgen) with now and
    /// then a weeping willow, so the open land south of the road gets its own kind of trees.
    /// </summary>
    static void Willows(Node3D root, int s)
    {
        var rng = new Random(4242);
        var pollard = new List<Transform3D>(); var pc = new List<Color>();
        var weeping = new List<Transform3D>(); var wc = new List<Color>();
        for (var x = -140f; x < s + 150; x += 6.5f + (float)rng.NextDouble() * 2.5f)
        {
            if (x > -16 && x < -2) continue;                                      // the side road
            var z = s + 66 + (float)rng.NextDouble() * 1.2f;
            var sc = 0.85f + (float)rng.NextDouble() * 0.3f;
            var xf = new Transform3D(new Basis(Vector3.Up, (float)rng.NextDouble() * Mathf.Tau).Scaled(Vector3.One * sc), new Vector3(x, 0, z));
            if (rng.NextDouble() < 0.12) { var w = xf; w.Origin = new Vector3(x, 0, z + 3); weeping.Add(w); wc.Add(Shade(rng)); }
            else { pollard.Add(xf); pc.Add(Shade(rng)); }
        }
        Assets.Many(root, Assets.Mesh("made/tree_pollard.glb", Foliage.ByName), pollard, pc);
        Assets.Many(root, Assets.Mesh("made/tree_willow.glb", Foliage.ByName), weeping, wc);
    }

    /// <summary>Grass tufts and the odd flower on open land, only drawn within <paramref name="range"/> metres of the camera.</summary>
    static void Meadow(Node3D root, List<Vector2> spots, Random rng, float flowerShare = 0.08f, float range = 110, bool plot = false)
    {
        var tx = new List<Transform3D>[Tufts.Length]; var tc = new List<Color>[Tufts.Length];
        var fx = new List<Transform3D>[Flowers.Length];
        for (var i = 0; i < Tufts.Length; i++) { tx[i] = new(); tc[i] = new(); }
        for (var i = 0; i < Flowers.Length; i++) fx[i] = new();
        foreach (var p in spots)
        {
            var yaw = new Basis(Vector3.Up, (float)rng.NextDouble() * Mathf.Tau);
            if (rng.NextDouble() < flowerShare)
            {
                var f = rng.Next(Flowers.Length); var sc = 0.8f + (float)rng.NextDouble() * 0.5f;
                fx[f].Add(new Transform3D(yaw.Scaled(Vector3.One * sc), new Vector3(p.X, 0, p.Y)));
            }
            else
            {
                var t = plot ? (rng.Next(2) == 0 ? 0 : 2) : rng.Next(Tufts.Length); var sc = 0.7f + (float)rng.NextDouble() * 0.5f;
                tx[t].Add(new Transform3D(yaw.Scaled(new Vector3(sc, sc * (0.8f + (float)rng.NextDouble() * 0.6f), sc)), new Vector3(p.X, 0, p.Y)));
                tc[t].Add(Shade(rng));
            }
        }
        void Add(MultiMeshInstance3D mi) { if (mi == null) return; mi.AddToGroup(MeadowGroup); mi.Visible = GrassOn; }
        for (var i = 0; i < Tufts.Length; i++) Add(Assets.Many(root, Assets.Mesh(Tufts[i].file, Tufts[i].mats), tx[i], tc[i], false, range));
        for (var i = 0; i < Flowers.Length; i++) Add(Assets.Many(root, Assets.Mesh(Flowers[i].file, Flowers[i].mats), fx[i], null, false, range));
    }

    /// <summary>Office blocks, houses and sheds along the public road and on the horizon, instead of plain boxes.</summary>
    static void Neighbours(Node3D root, int s, Random rng)
    {
        // across the road, facing it: an office park on the left, houses in the middle, sheds on the right
        var row = new (string file, float scale)[]
        {
            ("city/building-a.glb", 11), ("city/building-f.glb", 10), ("city/building-e.glb", 10), ("city/building-g.glb", 10),
            ("city/building-b.glb", 11), ("suburb/building-type-b.glb", 7), ("suburb/building-type-d.glb", 7), ("suburb/building-type-a.glb", 7),
            ("suburb/building-type-e.glb", 7), ("industry/building-a.glb", 10), ("industry/building-c.glb", 9), ("industry/building-e.glb", 10),
            ("industry/building-b.glb", 10), ("city/building-h.glb", 11), ("city/building-d.glb", 11),
        };
        var x = -110f;
        foreach (var (file, scale) in row)
        {
            var mesh = Assets.Mesh(file);
            if (mesh == null) continue;
            var w = mesh.GetAabb().Size.X * scale;
            var d = mesh.GetAabb().Size.Z * scale;
            x += w / 2 + 4;
            // stay clear of the side road west of the plot
            if (x + w / 2 > -16 && x - w / 2 < -2) x = -2 + w / 2 + 4;
            Assets.Centered(root, file, new Vector3(x, 0, s + 12 + d / 2), scale, 180);
            x += w / 2 + 4 + (float)rng.NextDouble() * 6;
        }
        // a few taller blocks of a town in the distance, behind the fields
        var far = new[] { "city/building-skyscraper-a.glb", "city/building-skyscraper-b.glb", "city/building-c.glb", "city/building-f.glb", "city/building-g.glb" };
        for (var i = 0; i < 12; i++)
            Assets.Centered(root, far[i % far.Length], new Vector3(-150 + i * 26 + (float)rng.NextDouble() * 10, 0, s + 135 + (float)rng.NextDouble() * 25), 11, rng.Next(4) * 90);
    }

    static readonly string[] Cars = { "sedan", "sedan-sports", "suv", "suv-luxury", "hatchback-sports", "van", "sedan", "suv" };

    /// <summary>
    /// Staff and customer cars in the parking bays (CC0 Kenney Car Kit). About two thirds of the bays are taken,
    /// always the same bays for the same layout so cars do not jump around when something else is built.
    /// </summary>
    static void ParkedCars(Node3D root, List<ParkingLayout.Line> lines)
    {
        var rows = new Dictionary<(bool, double, double), List<ParkingLayout.Line>>();
        foreach (var l in lines)
        {
            var vertical = Math.Abs(l.Ax - l.Bx) < 1e-6;
            var key = vertical ? (true, Math.Round(l.Az, 2), Math.Round(l.Bz, 2)) : (false, Math.Round(l.Ax, 2), Math.Round(l.Bx, 2));
            if (!rows.TryGetValue(key, out var list)) rows[key] = list = new();
            list.Add(l);
        }
        var xf = new Dictionary<string, List<Transform3D>>();
        foreach (var (key, row) in rows)
        {
            // dividers along z sit side by side in x, and the other way round
            var sorted = key.Item1 ? row.OrderBy(l => l.Ax).ToList() : row.OrderBy(l => l.Az).ToList();
            for (var i = 0; i + 1 < sorted.Count; i++)
            {
                var a = sorted[i]; var b = sorted[i + 1];
                var cx = (float)(a.Ax + a.Bx + b.Ax + b.Bx) / 4; var cz = (float)(a.Az + a.Bz + b.Az + b.Bz) / 4;
                var h = (uint)(Mathf.RoundToInt(cx * 10) * 73856093) ^ (uint)(Mathf.RoundToInt(cz * 10) * 19349663);
                if (h % 3 == 0) continue;
                var car = Cars[h / 3 % (uint)Cars.Length];
                var yaw = (key.Item1 ? 0 : Mathf.Pi / 2) + (h / 7 % 2 == 0 ? 0 : Mathf.Pi);
                if (!xf.TryGetValue(car, out var list)) xf[car] = list = new();
                list.Add(new Transform3D(new Basis(Vector3.Up, yaw).Scaled(Vector3.One * 1.3f), new Vector3(cx, 0, cz)));
            }
        }
        foreach (var (car, list) in xf) Assets.Many(root, Assets.Mesh("vehicles/" + car + ".glb"), list);
    }

    /// <summary>Grass tufts and flowers on the plot's open grass, rebuilt with the layout so paving and buildings clear them.</summary>
    void PlotMeadow(Node3D root)
    {
        var s = _state.PlotSize;
        var rng = new Random(1234);
        var spots = new List<Vector2>();
        for (var z = 0; z < s; z++)
            for (var x = 0; x < s; x++)
            {
                // hash per cell so the same cell always gets the same tufts
                var h = (uint)(x * 73856093) ^ (uint)(z * 19349663);
                var n = h % 5 == 0 ? 1 : 0;
                if (_state.SurfaceAt(x, z) != Surface.Grass || _state.BuildingAt(x, z) != null || _state.InStation(x, z) || _state.ObjectAt(x, z, 0) != null) continue;
                for (var i = 0; i < n; i++) spots.Add(new Vector2(x + 0.1f + (float)rng.NextDouble() * 0.8f, z + 0.1f + (float)rng.NextDouble() * 0.8f));
            }
        Meadow(root, spots, rng, 0.12f, 90, plot: true);
    }
}
