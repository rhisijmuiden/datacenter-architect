using System.Collections.Generic;
using DatacenterArchitect.Core;
using DatacenterArchitect.Sim;
using Godot;

namespace DatacenterArchitect.World;

/// <summary>Builds the 3D world: terrain, plot, public road, grid overlay, and the meshes for placed objects.</summary>
public partial class WorldView : Node3D
{
    public const float Cell = 1f;
    Node3D _objects, _foundation, _ghost;
    MeshInstance3D _grid;
    GameState _state;
    readonly Dictionary<Color, StandardMaterial3D> _mats = new();
    public DirectionalLight3D Sun { get; private set; }

    public StandardMaterial3D Mat(Color c, float rough = 0.8f, float metal = 0f)
    {
        if (_mats.TryGetValue(c, out var m)) return m;
        m = new StandardMaterial3D { AlbedoColor = c, Roughness = rough, Metallic = metal };
        _mats[c] = m;
        return m;
    }

    public override void _Ready()
    {
        var env = new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = Brand.Sky,
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color("#dfeefa"),
                AmbientLightEnergy = 0.32f,
                TonemapMode = Godot.Environment.ToneMapper.Linear,
                FogEnabled = true,
                FogLightColor = new Color("#bfe0f4"),
                FogDensity = 0.0007f,
                SsaoEnabled = true,
            }
        };
        AddChild(env);
        Sun = new DirectionalLight3D { ShadowEnabled = true, LightEnergy = 0.95f, LightColor = new Color("#fff0d8"), RotationDegrees = new Vector3(-52, -38, 0) };
        Sun.DirectionalShadowMaxDistance = 220;
        AddChild(Sun);

        AddChild(_foundation = new Node3D { Name = "Foundation" });
        AddChild(_objects = new Node3D { Name = "Objects" });
        AddChild(_ghost = new Node3D { Name = "Ghost" });
    }

    public void Build(GameState state)
    {
        _state = state;
        foreach (var n in GetChildren()) if (n.Name.ToString().StartsWith("Static")) n.QueueFree();
        var s = state.PlotSize;
        var root = new Node3D { Name = "Static" };
        AddChild(root);
        // surrounding land, plot and public road
        Plane(root, new Vector3(s / 2f, -0.03f, s / 2f), new Vector2(800, 800), Brand.Grass);
        Plane(root, new Vector3(s / 2f, -0.02f, s / 2f), new Vector2(s, s), Brand.PlotGrass);
        Plane(root, new Vector3(s / 2f, -0.015f, s + 4), new Vector2(600, 6), Brand.Asphalt);
        for (var x = -280; x < 300; x += 6) Plane(root, new Vector3(s / 2f + x, -0.01f, s + 4), new Vector2(3, 0.18f), new Color("#ece6cc"));
        Box(root, new Vector3(s / 2f, 0.06f, s + 0.9f), new Vector3(600, 0.12f, 0.25f), new Color("#b9b2a6"));
        // fibre pits of carriers along the road (orange = fiber)
        foreach (var x in new[] { 8, 30, 52 }) Box(root, new Vector3(x + 0.5f, 0.05f, s + 1.6f), new Vector3(0.9f, 0.1f, 0.9f), Brand.Fiber);
        // grid operator connection point, upgradeable later
        Box(root, new Vector3(s - 4, 1.4f, s - 3), new Vector3(4, 2.8f, 3), new Color("#d6dce0"));
        Box(root, new Vector3(s - 4, 2.9f, s - 3), new Vector3(4.2f, 0.25f, 3.2f), Brand.Power);
        // plot corner posts
        foreach (var c in new[] { new Vector3(0, 0, 0), new Vector3(s, 0, 0), new Vector3(0, 0, s), new Vector3(s, 0, s) })
            Box(root, c + new Vector3(0, 0.6f, 0), new Vector3(0.25f, 1.2f, 0.25f), Brand.Power);
        BuildGrid(root, s);
        Refresh();
    }

    void BuildGrid(Node3D root, int s)
    {
        var im = new ImmediateMesh();
        im.SurfaceBegin(Mesh.PrimitiveType.Lines);
        for (var i = 0; i <= s; i++)
        {
            im.SurfaceAddVertex(new Vector3(i, 0.02f, 0)); im.SurfaceAddVertex(new Vector3(i, 0.02f, s));
            im.SurfaceAddVertex(new Vector3(0, 0.02f, i)); im.SurfaceAddVertex(new Vector3(s, 0.02f, i));
        }
        im.SurfaceEnd();
        _grid = new MeshInstance3D { Mesh = im, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        _grid.MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(1, 1, 1, 0.22f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
        root.AddChild(_grid);
    }

    public bool GridVisible { get => _grid?.Visible ?? false; set { if (_grid != null) _grid.Visible = value; } }

    public void Refresh()
    {
        foreach (var c in _foundation.GetChildren()) c.QueueFree();
        foreach (var c in _objects.GetChildren()) c.QueueFree();
        if (_state == null) return;
        // foundation slabs as one multimesh
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = new BoxMesh { Size = new Vector3(1, 0.3f, 1) }, InstanceCount = _state.Foundation.Count };
        for (var i = 0; i < _state.Foundation.Count; i++)
            mm.SetInstanceTransform(i, new Transform3D(Basis.Identity, new Vector3(_state.Foundation[i][0] + 0.5f, 0.15f, _state.Foundation[i][1] + 0.5f)));
        _foundation.AddChild(new MultiMeshInstance3D { Multimesh = mm, MaterialOverride = Mat(Brand.Concrete, 0.95f) });
        foreach (var o in _state.Objects) _objects.AddChild(ObjectModel(o, false));
    }

    public Node3D ObjectModel(PlacedObject o, bool ghost, bool valid = true)
    {
        var (w, d) = GameState.Footprint(o);
        var n = new Node3D { Position = new Vector3(o.X + w / 2f, _state.HasFoundation(o.X, o.Z) ? 0.3f : 0f, o.Z + d / 2f) };
        n.RotationDegrees = new Vector3(0, -90 * o.Rot, 0);
        var def = Catalog.Defs[o.Kind];
        switch (o.Kind)
        {
            case BuildKind.Rack:
                Box(n, new Vector3(0, 1.05f, 0), new Vector3(0.96f, 2.1f, 0.95f), Brand.Rack, 0.5f, 0.4f);
                Box(n, new Vector3(0, 2.25f, -0.2f), new Vector3(1f, 0.06f, 0.3f), new Color("#FFC533"));
                for (var k = 0; k < 3; k++) Box(n, new Vector3(-0.3f + k * 0.12f, 0.6f + k * 0.5f, 0.48f), new Vector3(0.08f, 0.05f, 0.02f), Brand.Ok, emissive: true);
                break;
            case BuildKind.Crah:
                Box(n, new Vector3(0, 1.0f, 0), new Vector3(0.95f, 2f, 1.9f), new Color("#eef1f3"), 0.5f);
                Box(n, new Vector3(0, 1.85f, 0), new Vector3(0.97f, 0.12f, 1.92f), Brand.Cooling, emissive: true);
                break;
            case BuildKind.Generator:
                Box(n, new Vector3(0, 1.45f, 0), new Vector3(2.9f, 2.5f, 1.9f), new Color("#e9edf0"), 0.4f, 0.3f);
                Box(n, new Vector3(0, 2.2f, 0), new Vector3(2.92f, 0.22f, 1.92f), Brand.Power);
                Box(n, new Vector3(0, 0.1f, 0), new Vector3(3f, 0.2f, 2f), new Color("#9ea3a8"));
                Box(n, new Vector3(1f, 3.1f, 0), new Vector3(0.25f, 1.0f, 0.25f), new Color("#5d6b78"));
                break;
            case BuildKind.FiberPit:
                Box(n, new Vector3(0, 0.05f, 0), new Vector3(0.9f, 0.1f, 0.9f), Brand.Fiber);
                break;
            case BuildKind.Foundation:
                Box(n, new Vector3(0, 0.15f, 0), new Vector3(1, 0.3f, 1), Brand.Concrete);
                break;
        }
        if (ghost) Tint(n, valid ? Brand.Ok : Brand.Critical);
        return n;
    }

    static void Tint(Node n, Color c)
    {
        foreach (var child in n.GetChildren())
            if (child is MeshInstance3D mi)
                mi.MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(c, 0.55f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
    }

    public void ShowGhost(IEnumerable<PlacedObject> objs, bool valid)
    {
        ClearGhost();
        foreach (var o in objs) _ghost.AddChild(ObjectModel(o, true, valid));
    }

    public void ShowGhostRect(Rect2I r, Color c)
    {
        ClearGhost();
        var m = new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(r.Size.X, r.Size.Y) }, Position = new Vector3(r.Position.X + r.Size.X / 2f, 0.35f, r.Position.Y + r.Size.Y / 2f) };
        m.MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(c, 0.5f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
        _ghost.AddChild(m);
    }

    public void ClearGhost() { foreach (var c in _ghost.GetChildren()) c.QueueFree(); }

    MeshInstance3D Box(Node3D parent, Vector3 pos, Vector3 size, Color c, float rough = 0.8f, float metal = 0f, bool emissive = false)
    {
        var m = new MeshInstance3D { Mesh = new BoxMesh { Size = size }, Position = pos };
        if (emissive) m.MaterialOverride = new StandardMaterial3D { AlbedoColor = c, EmissionEnabled = true, Emission = c, EmissionEnergyMultiplier = 0.8f };
        else m.MaterialOverride = Mat(c, rough, metal);
        parent.AddChild(m);
        return m;
    }

    void Plane(Node3D parent, Vector3 pos, Vector2 size, Color c)
    {
        var m = new MeshInstance3D { Mesh = new PlaneMesh { Size = size }, Position = pos, MaterialOverride = Mat(c, 1f) };
        parent.AddChild(m);
    }
}
