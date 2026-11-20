using System.Collections.Generic;
using Godot;

namespace DatacenterArchitect.World;

/// <summary>
/// Gives the plain colour materials of our own Blender models (paint, steel, concrete, rubber) a light, hand-finished
/// look like a park-sim: a soft grime and wear mottle in the albedo, small roughness variation and, for concrete, the
/// photo texture used on walls. Projected in object space (triplanar), so it needs no UVs and does not swim on
/// moving vehicles. The model scripts keep naming materials by their palette name (tools/models/blender/kit.py).
/// </summary>
public static class Weathering
{
    static readonly Dictionary<Material, Material> Done = new();
    static ImageTexture _wear;

    /// <summary>Palette names that get the grime; strength = how much darker the dirtiest spots get.</summary>
    static readonly Dictionary<string, float> Strength = new()
    {
        ["paint_white"] = 0.24f, ["paint_light"] = 0.24f, ["paint_grey"] = 0.2f, ["paint_dark"] = 0.14f,
        ["paint_green"] = 0.2f, ["steel"] = 0.2f, ["galvanised"] = 0.27f, ["dark_steel"] = 0.14f,
        ["rubber"] = 0.1f, ["coil"] = 0.2f, ["concrete"] = 0.2f, ["wood"] = 0.24f, ["hazard"] = 0.2f,
        ["accent_power"] = 0.14f, ["accent_cooling"] = 0.14f, ["red_paint"] = 0.17f, ["body"] = 0.14f,
    };

    public static bool Enabled = true;

    public static Material Apply(string name, Material m)
    {
        if (!Enabled || m is not StandardMaterial3D src || !Strength.TryGetValue(name, out var k)) return m;
        if (Done.TryGetValue(m, out var d)) return d;
        var w = (StandardMaterial3D)src.Duplicate();
        if (name == "concrete" && ResourceLoader.Exists("res://assets/textures/ground/concrete_wall_008_albedo.jpg"))
        {
            WorldView.Textured(w, "concrete_wall_008", 2.2f, 12, 0.6f);
            w.Uv1WorldTriplanar = false; w.UV2WorldTriplanar = false;
            return Done[m] = w;
        }
        // grime: low-frequency blotches times a fine speckle, multiplied into the paint colour
        w.DetailEnabled = true;
        w.DetailAlbedo = Grime(k);
        w.DetailBlendMode = BaseMaterial3D.BlendModeEnum.Mul;
        w.DetailUVLayer = BaseMaterial3D.DetailUV.UV2;
        w.UV2Triplanar = true; w.UV2WorldTriplanar = false; w.UV2Scale = Vector3.One / 1.6f;
        w.UV2TriplanarSharpness = 2f;
        // roughness varies a little with the same wear, so painted panels catch the light unevenly
        w.RoughnessTexture = Wear();
        w.RoughnessTextureChannel = BaseMaterial3D.TextureChannel.Red;
        w.Uv1Triplanar = true; w.Uv1WorldTriplanar = false; w.Uv1Scale = Vector3.One / 0.9f;
        w.TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic;
        return Done[m] = w;
    }

    static readonly Dictionary<float, ImageTexture> GrimeBy = new();

    /// <summary>Seamless grey grime map: 1 = clean, down to 1 - strength in the dirtiest spots.</summary>
    static ImageTexture Grime(float strength)
    {
        if (GrimeBy.TryGetValue(strength, out var t)) return t;
        const int S = 256;
        var big = new FastNoiseLite { Seed = 11, NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth, Frequency = 0.012f, FractalOctaves = 3 };
        var streak = new FastNoiseLite { Seed = 37, NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth, Frequency = 0.09f, FractalOctaves = 2 };
        var fine = new FastNoiseLite { Seed = 23, NoiseType = FastNoiseLite.NoiseTypeEnum.Cellular, Frequency = 0.08f, FractalOctaves = 2 };
        var img = Image.CreateEmpty(S, S, true, Image.Format.Rgb8);
        for (var y = 0; y < S; y++)
            for (var x = 0; x < S; x++)
            {
                var b = Tile(big, x, y, S) * 0.5f + 0.5f;               // 0..1 blotches
                var f = Tile(fine, x, y, S) * 0.5f + 0.5f;              // fine speckle
                var r = Mathf.Max(0, TileStretched(streak, x, y, S));         // thin vertical rain streaks on the sides
                var dirt = Mathf.Clamp(Mathf.SmoothStep(0.3f, 0.8f, b) * 0.65f + f * 0.2f + r * r * 0.6f, 0, 1);
                var v = 1f - strength * dirt;
                img.SetPixel(x, y, new Color(v, v * 0.995f, v * 0.985f)); // slightly warm dirt
            }
        img.GenerateMipmaps();
        return GrimeBy[strength] = ImageTexture.CreateFromImage(img);
    }

    /// <summary>Roughness multiplier map around 0.85..1.</summary>
    static ImageTexture Wear()
    {
        if (_wear != null) return _wear;
        const int S = 128;
        var n = new FastNoiseLite { Seed = 5, NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth, Frequency = 0.05f, FractalOctaves = 3 };
        var img = Image.CreateEmpty(S, S, true, Image.Format.Rgb8);
        for (var y = 0; y < S; y++)
            for (var x = 0; x < S; x++)
            {
                var v = 0.85f + 0.15f * (Tile(n, x, y, S) * 0.5f + 0.5f);
                img.SetPixel(x, y, new Color(v, v, v));
            }
        img.GenerateMipmaps();
        return _wear = ImageTexture.CreateFromImage(img);
    }

    /// <summary>Like Tile, but the noise is stretched along the texture's vertical axis.</summary>
    static float TileStretched(FastNoiseLite n, int x, int y, int s)
    {
        float u = x / (float)s, v = y / (float)s;
        const float k = 0.12f;
        float a = n.GetNoise2D(x, y * k), b = n.GetNoise2D(x - s, y * k), c = n.GetNoise2D(x, (y - s) * k), d = n.GetNoise2D(x - s, (y - s) * k);
        return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
    }

    /// <summary>Seamless noise: blends the four wrapped copies so the map tiles without seams.</summary>
    static float Tile(FastNoiseLite n, int x, int y, int s)
    {
        float u = x / (float)s, v = y / (float)s;
        float a = n.GetNoise2D(x, y), b = n.GetNoise2D(x - s, y), c = n.GetNoise2D(x, y - s), d = n.GetNoise2D(x - s, y - s);
        return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
    }
}
