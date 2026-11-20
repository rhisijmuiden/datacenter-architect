using System.Collections.Generic;
using DatacenterArchitect.Core;
using Godot;

namespace DatacenterArchitect.World;

/// <summary>
/// Ground materials made from CC0 photo textures (Poly Haven, see assets/models/CREDITS.md): grass, asphalt,
/// pavers, red bike-path bricks, gravel and dirt. The textures were pre-processed (tools/textures/prep_ground.py) to
/// keep their detail but little of their own colour, so the brand and season colours still tint them. A large,
/// soft mottle on top breaks up the repeat of the photo texture at a distance.
/// </summary>
public partial class WorldView
{
    const string GroundDir = "res://assets/textures/ground/";
    static readonly Dictionary<string, (Texture2D albedo, Texture2D normal)> GroundTextures = new();

    static (Texture2D, Texture2D) GroundTexture(string name)
    {
        if (GroundTextures.TryGetValue(name, out var t)) return t;
        var a = ResourceLoader.Exists(GroundDir + name + "_albedo.jpg") ? GD.Load<Texture2D>(GroundDir + name + "_albedo.jpg") : null;
        var n = ResourceLoader.Exists(GroundDir + name + "_normal.jpg") ? GD.Load<Texture2D>(GroundDir + name + "_normal.jpg") : null;
        return GroundTextures[name] = (a, n);
    }

    /// <summary>Photo texture <paramref name="tex"/> repeated every <paramref name="metres"/>, tinted by the material's AlbedoColor, with the mottle every <paramref name="grain"/> metres.</summary>
    public static StandardMaterial3D Textured(StandardMaterial3D m, string tex, float metres, float grain = 40, float normal = 0.9f)
    {
        var (a, n) = GroundTexture(tex);
        if (a == null) return Grained(m, grain);
        m.AlbedoTexture = a;
        if (n != null) { m.NormalEnabled = true; m.NormalTexture = n; m.NormalScale = normal; }
        m.Uv1Triplanar = true; m.Uv1WorldTriplanar = true; m.Uv1Scale = Vector3.One / metres;
        m.DetailEnabled = true; m.DetailAlbedo = Grain; m.DetailBlendMode = BaseMaterial3D.BlendModeEnum.Mul;
        m.DetailUVLayer = BaseMaterial3D.DetailUV.UV2;
        m.UV2Triplanar = true; m.UV2WorldTriplanar = true; m.UV2Scale = Vector3.One / grain;
        m.TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic;
        return m;
    }

    /// <summary>Which photo texture a paint colour uses, and how many metres one repeat covers.</summary>
    static readonly Dictionary<Color, (string tex, float metres)> SurfaceTextures = new()
    {
        [Brand.Asphalt] = ("asphalt_02", 4.5f),
        [new Color("#575d66")] = ("herringbone_pavement", 1.8f),
        [new Color("#d8ccb4")] = ("square_concrete_pavers", 2.4f),
        [new Color("#b4544a")] = ("red_brick_pavers", 2.2f),
        [new Color("#b9aa8c")] = ("gravel_floor", 2.5f),
        [new Color("#8b6f4e")] = ("raked_dirt", 3f),
        [new Color("#c9c1b2")] = ("square_concrete_pavers", 2.4f),
        [Brand.Concrete] = ("concrete_floor_02", 4f),
    };

    /// <summary>Tint so the pre-processed textures (average brightness 0.85) come out at the paint colour.</summary>
    static Color Lift(Color c) => new(Mathf.Min(c.R / 0.85f, 1.2f), Mathf.Min(c.G / 0.85f, 1.2f), Mathf.Min(c.B / 0.85f, 1.2f), c.A);

    /// <summary>The ground material for a paint colour: a photo texture when one belongs to it, otherwise the plain mottle.</summary>
    static StandardMaterial3D SurfaceMat(Color c, float rough = 0.9f)
    {
        if (!SurfaceTextures.TryGetValue(c, out var t)) return GrainMat(c, 14, rough);
        if (GrainMats.TryGetValue((c, -t.metres), out var m)) return m;
        return GrainMats[(c, -t.metres)] = Textured(new StandardMaterial3D { AlbedoColor = Lift(c), Roughness = rough }, t.tex, t.metres, 14);
    }

    static readonly Dictionary<Color, (string tex, float metres)> WallTextures = new()
    {
        [new Color("#b5654a")] = ("red_brick", 2.2f),
        [new Color("#b8c2c9")] = ("corrugated_iron_02", 1.6f),
        [new Color("#cfcac2")] = ("concrete_wall_008", 4f),
        [new Color("#cfd4d8")] = ("concrete_wall_008", 4f),
    };

    /// <summary>Outer wall material for a facade colour: brick, ribbed cladding or precast concrete.</summary>
    static Material WallMat(Color c)
    {
        if (!WallTextures.TryGetValue(c, out var t)) return null;
        if (GrainMats.TryGetValue((c, -100 - t.metres), out var m)) return m;
        return GrainMats[(c, -100 - t.metres)] = Textured(new StandardMaterial3D { AlbedoColor = Lift(c), Roughness = 0.85f }, t.tex, t.metres, 30, 1f);
    }

    /// <summary>Gravel ballast on the flat roofs (the colour is set by the seasons, snow included).</summary>
    static void RoofTextures()
    {
        if (RoofTopMat.AlbedoTexture != null) return;
        Textured(RoofTopMat, "gravel_floor", 2.5f, 20, 0.6f);
        Textured(RoofMat, "concrete_wall_008", 4f, 20, 0.6f);
    }

    /// <summary>Grass for the plot and the land around it.</summary>
    static StandardMaterial3D GrassMat(Color c, float grain) => Textured(new StandardMaterial3D { AlbedoColor = Lift(c), Roughness = 1f }, "leafy_grass", 3.2f, grain, 0.7f);
}
