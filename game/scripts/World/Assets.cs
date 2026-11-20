using System;
using System.Collections.Generic;
using Godot;

namespace DatacenterArchitect.World;

/// <summary>
/// Loads the CC0 3D models in res://assets/models (see CREDITS.md there) and hands them out as one merged mesh per
/// model: every mesh node of a .glb (or a whole .obj) is baked into a single ArrayMesh with one surface per material.
/// One mesh per model keeps draw calls low and lets the same mesh feed MultiMesh instancing for forests, grass and
/// parked cars. Meshes are cached, so a model is only read and merged once.
/// </summary>
public static class Assets
{
    const string Root = "res://assets/models/";
    static readonly Dictionary<string, ArrayMesh> Meshes = new();

    public static bool Has(string path) => ResourceLoader.Exists(Root + path);

    /// <summary>The model as one mesh, or null when the file is missing. <paramref name="remap"/> may swap materials by their name.</summary>
    public static ArrayMesh Mesh(string path, Func<string, Material, Material> remap = null)
    {
        var key = remap == null ? path : path + "#" + remap.Method.Name;
        if (Meshes.TryGetValue(key, out var cached)) return cached;
        ArrayMesh result = null;
        if (Has(path))
        {
            var res = ResourceLoader.Load(Root + path);
            var parts = new List<(Mesh mesh, Transform3D xf, Material[] mats)>();
            if (res is Mesh direct) parts.Add((direct, Transform3D.Identity, null));
            else if (res is PackedScene ps)
            {
                var inst = ps.Instantiate<Node>();
                Collect(inst, Transform3D.Identity, parts);
                inst.Free();
            }
            // a single mesh keeps the level-of-detail versions Godot made on import; several are merged into one
            result = parts.Count == 1 && parts[0].xf.IsEqualApprox(Transform3D.Identity) && parts[0].mesh is ArrayMesh am
                ? Remapped(am, parts[0].mats, remap) : Merge(parts, remap);
        }
        else GD.PushWarning($"model missing: {path}");
        Meshes[key] = result;
        return result;
    }

    static ArrayMesh Remapped(ArrayMesh src, Material[] mats, Func<string, Material, Material> remap)
    {
        var m = (ArrayMesh)src.Duplicate();
        for (var i = 0; i < m.GetSurfaceCount(); i++)
        {
            var mat = mats?[i] ?? m.SurfaceGetMaterial(i);
            if (mat != null && remap != null) m.SurfaceSetMaterial(i, remap(mat.ResourceName ?? "", mat));
        }
        return m;
    }

    static void Collect(Node n, Transform3D parent, List<(Mesh, Transform3D, Material[])> parts)
    {
        var xf = n is Node3D n3 ? parent * n3.Transform : parent;
        if (n is MeshInstance3D mi && mi.Mesh != null && mi.Visible)
        {
            var mats = new Material[mi.Mesh.GetSurfaceCount()];
            for (var i = 0; i < mats.Length; i++) mats[i] = mi.GetSurfaceOverrideMaterial(i) ?? mi.MaterialOverride ?? mi.Mesh.SurfaceGetMaterial(i);
            parts.Add((mi.Mesh, xf, mats));
        }
        foreach (var c in n.GetChildren()) Collect(c, xf, parts);
    }

    static ArrayMesh Merge(List<(Mesh mesh, Transform3D xf, Material[] mats)> parts, Func<string, Material, Material> remap)
    {
        // group surfaces by material so a model with many nodes still ends up with a few surfaces
        var groups = new Dictionary<Material, SurfaceTool>();
        var order = new List<Material>();
        var none = new StandardMaterial3D();
        foreach (var (mesh, xf, mats) in parts)
            for (var i = 0; i < mesh.GetSurfaceCount(); i++)
            {
                if (mesh is ArrayMesh am && am.SurfaceGetPrimitiveType(i) != Godot.Mesh.PrimitiveType.Triangles) continue;
                var mat = mats?[i] ?? mesh.SurfaceGetMaterial(i) ?? none;
                if (!groups.TryGetValue(mat, out var st))
                {
                    st = new SurfaceTool();
                    st.Begin(Godot.Mesh.PrimitiveType.Triangles);
                    groups[mat] = st; order.Add(mat);
                }
                st.AppendFrom(mesh, i, xf);
            }
        if (order.Count == 0) return null;
        var result = new ArrayMesh();
        foreach (var mat in order)
        {
            var st = groups[mat];
            st.Index();
            st.Commit(result);
            var name = mat.ResourceName ?? "";
            result.SurfaceSetMaterial(result.GetSurfaceCount() - 1, remap?.Invoke(name, mat) ?? mat);
        }
        return result;
    }

    static readonly Dictionary<string, (ArrayMesh body, List<(ArrayMesh mesh, Transform3D xf)> fans)> Mounts = new();

    /// <summary>
    /// Adds a model with moving parts to <paramref name="n"/>: the static body as one mesh, and every node named Fan*
    /// as its own child node "Fan" pivoting at its hub, so the world view can spin it.
    /// </summary>
    public static bool Mount(Node3D n, string path, Func<string, Material, Material> remap)
    {
        if (!Mounts.TryGetValue(path, out var m))
        {
            if (!Has(path)) { Mounts[path] = m = (null, null); }
            else
            {
                var inst = GD.Load<PackedScene>(Root + path).Instantiate<Node>();
                var body = new List<(Mesh, Transform3D, Material[])>();
                var fans = new List<(Mesh, Transform3D, Material[])>();
                void Walk(Node node, Transform3D parent, bool fan)
                {
                    var xf = node is Node3D n3 ? parent * n3.Transform : parent;
                    fan |= node.Name.ToString().StartsWith("Fan");
                    if (node is MeshInstance3D mi && mi.Mesh != null)
                    {
                        var mats = new Material[mi.Mesh.GetSurfaceCount()];
                        for (var i = 0; i < mats.Length; i++) mats[i] = mi.GetSurfaceOverrideMaterial(i) ?? mi.Mesh.SurfaceGetMaterial(i);
                        (fan ? fans : body).Add((mi.Mesh, xf, mats));
                    }
                    foreach (var c in node.GetChildren()) Walk(c, xf, fan);
                }
                Walk(inst, Transform3D.Identity, false);
                inst.Free();
                var fanMeshes = new List<(ArrayMesh, Transform3D)>();
                foreach (var f in fans)
                {
                    // the rotor mesh around its own hub; the node goes where the hub is
                    var local = Merge(new List<(Mesh, Transform3D, Material[])> { (f.Item1, Transform3D.Identity, f.Item3) }, remap);
                    if (local != null) fanMeshes.Add((local, f.Item2));
                }
                Mounts[path] = m = (Merge(body, remap), fanMeshes);
            }
        }
        if (m.body == null) return false;
        n.AddChild(new MeshInstance3D { Mesh = m.body });
        foreach (var (mesh, xf) in m.fans)
        {
            var pivot = new Node3D { Name = "Fan", Transform = xf };
            pivot.AddChild(new MeshInstance3D { Mesh = mesh });
            n.AddChild(pivot);
        }
        return true;
    }

    /// <summary>A node showing the model, standing on the ground at its local origin.</summary>
    public static MeshInstance3D Place(Node3D parent, string path, Vector3 pos, float scale = 1, float yawDeg = 0, Func<string, Material, Material> remap = null)
    {
        var mesh = Mesh(path, remap);
        if (mesh == null) return null;
        var mi = new MeshInstance3D { Mesh = mesh, Position = pos, Scale = Vector3.One * scale, RotationDegrees = new Vector3(0, yawDeg, 0) };
        parent.AddChild(mi);
        return mi;
    }

    /// <summary>Places a model at a fixed scale with its footprint centred on <paramref name="pos"/> and its base on pos.Y.</summary>
    public static MeshInstance3D Centered(Node3D parent, string path, Vector3 pos, float scale, float yawDeg = 0, Func<string, Material, Material> remap = null)
    {
        var mesh = Mesh(path, remap);
        if (mesh == null) return null;
        var box = mesh.GetAabb();
        var c = box.GetCenter();
        var mi = new MeshInstance3D { Mesh = mesh, Scale = Vector3.One * scale, RotationDegrees = new Vector3(0, yawDeg, 0) };
        mi.Position = pos - mi.Basis * new Vector3(c.X, box.Position.Y, c.Z);
        parent.AddChild(mi);
        return mi;
    }

    /// <summary>Height of the top of a model at the given scale.</summary>
    public static float Top(string path, float scale) => Mesh(path) is { } m ? m.GetAabb().End.Y * scale : 0;

    /// <summary>One MultiMesh drawing many copies of a model; per-instance colours tint the foliage shader.</summary>
    public static MultiMeshInstance3D Many(Node3D parent, Mesh mesh, IReadOnlyList<Transform3D> xf, IReadOnlyList<Color> colors = null, bool shadow = true, float visibleTo = 0)
    {
        if (mesh == null || xf.Count == 0) return null;
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = colors != null, Mesh = mesh, InstanceCount = xf.Count };
        for (var i = 0; i < xf.Count; i++)
        {
            mm.SetInstanceTransform(i, xf[i]);
            if (colors != null) mm.SetInstanceColor(i, colors[i]);
        }
        var mi = new MultiMeshInstance3D { Multimesh = mm, CastShadow = shadow ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off };
        if (visibleTo > 0) { mi.VisibilityRangeEnd = visibleTo; mi.VisibilityRangeEndMargin = visibleTo * 0.1f; mi.VisibilityRangeFadeMode = GeometryInstance3D.VisibilityRangeFadeModeEnum.Self; }
        parent.AddChild(mi);
        return mi;
    }
}

/// <summary>
/// Materials for plants: leaves, needles, grass and bark sway in the wind in the vertex shader (more the higher a
/// vertex sits), so whole forests move without any per-frame C# work. Per-instance colours from a MultiMesh vary the
/// shade of each tree. The seasons recolour <see cref="Leaf"/>, <see cref="Leaf2"/>, <see cref="Needle"/> and <see cref="Grass"/>.
/// </summary>
public static class Foliage
{
    const string Code = @"
shader_type spatial;
render_mode cull_disabled;
uniform vec4 albedo : source_color = vec4(0.37, 0.6, 0.27, 1.0);
uniform float sway = 0.05;
uniform float flutter = 0.012;
uniform float wind = 1.0;
uniform float roughness = 0.88;
uniform float translucency = 0.25;
varying float v_shade;
void vertex() {
    vec3 pos = MODEL_MATRIX[3].xyz;
    float h = max(VERTEX.y, 0.0);
    float t = TIME * 1.3 + pos.x * 0.21 + pos.z * 0.17;
    float gust = 0.65 + 0.35 * sin(TIME * 0.37 + pos.x * 0.013 + pos.z * 0.011);
    vec2 bend = vec2(sin(t), 0.6 * cos(t * 0.83 + 1.7)) * sway * wind * gust * h * h;
    VERTEX.xz += bend;
    VERTEX += NORMAL * sin(TIME * 7.0 + VERTEX.y * 9.0 + pos.x * 3.1) * flutter * wind * h;
    v_shade = fract(sin(dot(pos.xz, vec2(12.9898, 78.233))) * 43758.5453);
}
void fragment() {
    vec3 c = albedo.rgb * COLOR.rgb * mix(0.86, 1.12, v_shade);
    // a little light from below the crown so leaves do not go black in shadow
    ALBEDO = c;
    ROUGHNESS = roughness;
    SPECULAR = 0.25;
    BACKLIGHT = c * translucency;
}";
    static Shader _shader;
    static Shader Shader => _shader ??= new Shader { Code = Code };
    static readonly List<ShaderMaterial> All = new();

    static ShaderMaterial Make(Color c, float sway, float flutter, float rough = 0.88f)
    {
        var m = new ShaderMaterial { Shader = Shader };
        m.SetShaderParameter("albedo", c);
        m.SetShaderParameter("sway", sway);
        m.SetShaderParameter("flutter", flutter);
        m.SetShaderParameter("roughness", rough);
        All.Add(m);
        return m;
    }

    public static readonly Color LeafSummer = new("#5c9a3c"), Leaf2Summer = new("#7cb34a"), NeedleSummer = new("#3f7a45"), GrassSummer = new("#6aa443");
    public static readonly ShaderMaterial Leaf = Make(LeafSummer, 0.006f, 0.01f);
    public static readonly ShaderMaterial Leaf2 = Make(Leaf2Summer, 0.006f, 0.01f);
    public static readonly ShaderMaterial Needle = Make(NeedleSummer, 0.004f, 0.004f);
    public static readonly ShaderMaterial Bark = Make(new Color("#7b5a3f"), 0.003f, 0f, 0.95f);
    public static readonly ShaderMaterial BirchBark = Make(new Color("#e9e4da"), 0.003f, 0f, 0.9f);
    public static readonly ShaderMaterial Grass = Make(GrassSummer, 0.35f, 0.01f);
    public static readonly ShaderMaterial Bush = Make(new Color("#4f8a37"), 0.02f, 0.01f);
    public static readonly ShaderMaterial FlowerRed = Make(new Color("#e2493b"), 0.3f, 0.01f);
    public static readonly ShaderMaterial FlowerYellow = Make(new Color("#f2c53d"), 0.3f, 0.01f);
    public static readonly ShaderMaterial FlowerPurple = Make(new Color("#9a6ad6"), 0.3f, 0.01f);
    public static readonly ShaderMaterial FlowerWhite = Make(new Color("#f4f1e8"), 0.3f, 0.01f);

    public static void SetColor(ShaderMaterial m, Color c) => m.SetShaderParameter("albedo", c);

    /// <summary>Materials of the game's own plant models (tools/models/blender/plants.py) by name.</summary>
    public static Material ByName(string name, Material m) => name switch
    {
        "leaves" => Leaf, "leaves2" => Leaf2, "needles" => Needle, "bark" => Bark, "bark_birch" => BirchBark,
        "bush" => Bush, "grass" => Grass, "petal_red" => FlowerRed, "petal_yellow" => FlowerYellow,
        "petal_purple" => FlowerPurple, "petal_white" => FlowerWhite, _ => m,
    };
}
