using System;
using System.Collections.Generic;
using System.Linq;
using DatacenterArchitect.Core;
using DatacenterArchitect.Sim;
using Godot;
using static DatacenterArchitect.World.Models;

namespace DatacenterArchitect.World;

/// <summary>
/// Builds the 3D world from the game state: surroundings, paving, fences, buildings by construction stage,
/// rooms, doors, objects, workers and trucks. Layers rebuild only when their part of the state changes.
/// </summary>
public partial class WorldView : Node3D
{
    public const float FloorH = 3.6f, Slab = 0.3f, CutH = 1.5f;
    public static float Base(int floor) => Slab + floor * FloorH;

    Node3D _static, _layout, _objects, _sites, _agentsRoot, _ghost, _pallets;
    readonly List<Node3D> _fans = new();
    readonly Dictionary<int, (Node3D node, ObjKind kind, int width)> _gates = new();
    public Overlay Overlay { get; private set; }
    MeshInstance3D _grid;
    GameState _state;
    Simulation _sim;
    int _layoutRev = -1, _objRev = -1, _stock = -1;
    double _siteTimer;
    readonly Dictionary<int, Node3D> _agentNodes = new();
    readonly List<Node3D> _cranes = new();
    RackRenderer _racks;
    readonly List<Node3D> _markers = new();
    FontFile _font;
    public DirectionalLight3D Sun { get; private set; }
    WorldEnvironment _env;
    /// <summary>Options: blinking rack LEDs and moving parts (crane jibs, fans, markers).</summary>
    public bool BlinkingLights = true, MovingParts = true;

    /// <summary>Apply the visual effect switches from the Options menu.</summary>
    public void ApplyEffects(AppSettings s)
    {
        if (Sun != null) Sun.ShadowEnabled = s.Shadows;
        if (_env?.Environment != null)
        {
            _env.Environment.SsaoEnabled = s.AmbientOcclusion;
            _env.Environment.FogEnabled = s.Fog;
        }
        BlinkingLights = s.BlinkingLights;
        ServerFill.SetBlinking(BlinkingLights);
        MovingParts = s.MovingParts;
        DayNight = s.DayNight; WeatherFx = s.Weather; Seasons = s.Seasons;
        _lastSeason = (Season)(-1);
        if (IsInsideTree()) GetViewport().Msaa3D = s.Msaa switch { 2 => Viewport.Msaa.Msaa2X, 4 => Viewport.Msaa.Msaa4X, 8 => Viewport.Msaa.Msaa8X, _ => Viewport.Msaa.Disabled };
    }

    int _viewFloor;
    bool _roofs = true;
    public int ViewFloor { get => _viewFloor; set { _viewFloor = Math.Clamp(value, 0, Catalog.MaxFloors - 1); Invalidate(); } }
    public bool Roofs { get => _roofs; set { _roofs = value; Invalidate(); } }
    bool _fullWalls;
    /// <summary>First-person view: walls of the open floor go up to the ceiling and a ceiling closes the room.</summary>
    public bool FullWalls { get => _fullWalls; set { if (_fullWalls == value) return; _fullWalls = value; Invalidate(); } }
    public void Invalidate() { _layoutRev = -1; _objRev = -1; }

    public override void _Ready()
    {
        _env = new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = Brand.Sky,
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color("#dfeefa"),
                AmbientLightEnergy = 0.34f,
                TonemapMode = Godot.Environment.ToneMapper.Linear,
                FogEnabled = true,
                FogLightColor = new Color("#bfe0f4"),
                FogDensity = 0.0016f,
                SsaoEnabled = true,
            }
        };
        AddChild(_env);
        Sun = new DirectionalLight3D { ShadowEnabled = true, LightEnergy = 0.95f, LightColor = new Color("#fff0d8"), RotationDegrees = new Vector3(-52, -38, 0) };
        Sun.DirectionalShadowMaxDistance = 160;
        AddChild(Sun);
        _font = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-SemiBold.ttf");
        Overlay = new Overlay { Name = "Overlay", Visible = false };
        AddChild(Overlay);
        foreach (var name in new[] { "Layout", "Objects", "Sites", "Agents", "Ghost", "Pallets" })
        {
            var n = new Node3D { Name = name };
            AddChild(n);
            switch (name)
            {
                case "Layout": _layout = n; break;
                case "Objects": _objects = n; break;
                case "Sites": _sites = n; break;
                case "Agents": _agentsRoot = n; break;
                case "Ghost": _ghost = n; break;
                case "Pallets": _pallets = n; break;
            }
        }
        _racks = new RackRenderer { Name = "Racks" };
        AddChild(_racks);
        SetupSky();
    }

    public void Build(GameState state, Simulation sim)
    {
        _state = state; _sim = sim;
        _static?.QueueFree();
        foreach (var n in _agentNodes.Values) n.QueueFree();
        _agentNodes.Clear();
        _racks.Clear();
        _static = new Node3D { Name = "Static" };
        AddChild(_static);
        Surroundings(_static, state.PlotSize);
        BuildGrid(_static, state.PlotSize);
        Invalidate(); _stock = -1;
        Sync(0);
    }

    // ---------------- surroundings ----------------
    void Surroundings(Node3D root, int s)
    {
        var rng = new Random(42);
        var grass = new StandardMaterial3D { AlbedoColor = Brand.Grass, Roughness = 1f };
        var plotGrass = new StandardMaterial3D { AlbedoColor = Brand.PlotGrass, Roughness = 1f };
        Plane(root, new Vector3(s / 2f, -0.03f, s / 2f), new Vector2(900, 900), Brand.Grass).MaterialOverride = grass;
        Plane(root, new Vector3(s / 2f, -0.02f, s / 2f), new Vector2(s, s), Brand.PlotGrass).MaterialOverride = plotGrass;
        // public road with markings and pavement
        Plane(root, new Vector3(s / 2f, -0.015f, s + 4), new Vector2(900, 6), Brand.Asphalt);
        for (var x = -420; x < 480; x += 6) Plane(root, new Vector3(s / 2f + x, -0.01f, s + 4), new Vector2(3, 0.18f), new Color("#ece6cc"));
        Plane(root, new Vector3(s / 2f, -0.012f, s + 7.8f), new Vector2(900, 1.6f), new Color("#c9c1b2"));
        // side road heading north along the west of the plot
        Plane(root, new Vector3(-9, -0.014f, s / 2f - 60), new Vector2(6, 2 * s + 120), Brand.Asphalt);
        // fibre pits of carriers along the road (orange = fiber)
        foreach (var x in Catalog.StreetPits.Select((_, i) => _state.StreetPit(i))) Plane(root, new Vector3(x + 0.5f, -0.006f, s + 1.6f), new Vector2(0.9f, 0.9f), Brand.Fiber); // flush covers, no bump in a driveway
        // plot corner posts
        foreach (var c in new[] { new Vector3(0, 0, 0), new Vector3(s, 0, 0), new Vector3(0, 0, s), new Vector3(s, 0, s) })
            Box(root, c + new Vector3(0, 0.6f, 0), new Vector3(0.25f, 1.2f, 0.25f), Brand.Power);

        // forest to the north and east, as multimeshes so it stays light
        var trunks = new List<Transform3D>(); var crowns = new List<Transform3D>(); var crowns2 = new List<Transform3D>();
        void Tree(float x, float z)
        {
            var sc = 0.8f + (float)rng.NextDouble() * 0.9f;
            trunks.Add(new Transform3D(Basis.Identity.Scaled(new Vector3(sc, sc, sc)), new Vector3(x, 0.8f * sc, z)));
            var list = rng.Next(3) == 0 ? crowns2 : crowns;
            list.Add(new Transform3D(Basis.Identity.Scaled(new Vector3(sc, sc * 1.1f, sc)), new Vector3(x, 2.4f * sc, z)));
        }
        for (var i = 0; i < 1400; i++)
        {
            var x = -120 + (float)rng.NextDouble() * (s + 240);
            var z = -110 + (float)rng.NextDouble() * 100;
            if (z > -6) continue;
            Tree(x, z);
        }
        for (var i = 0; i < 700; i++)
        {
            var x = s + 8 + (float)rng.NextDouble() * 90;
            var z = -10 + (float)rng.NextDouble() * (s + 8);
            Tree(x, z);
        }
        for (var i = 0; i < 400; i++)
        {
            var x = -110 + (float)rng.NextDouble() * 96;
            var z = -10 + (float)rng.NextDouble() * (s + 8);
            if (x > -14) continue;
            Tree(x, z);
        }
        Multi(root, new CylinderMesh { TopRadius = 0.12f, BottomRadius = 0.18f, Height = 1.6f, RadialSegments = 6 }, trunks, new Color("#7a5a3c"));
        var crownMat = new StandardMaterial3D { AlbedoColor = new Color("#5a9142"), Roughness = 0.9f };
        var crown2Mat = new StandardMaterial3D { AlbedoColor = new Color("#6fa94e"), Roughness = 0.9f };
        var c1 = Multi(root, new SphereMesh { Radius = 1.3f, Height = 2.6f, RadialSegments = 8, Rings = 5 }, crowns, new Color("#5a9142"));
        var c2 = Multi(root, new SphereMesh { Radius = 1.3f, Height = 2.6f, RadialSegments = 8, Rings = 5 }, crowns2, new Color("#6fa94e"));
        if (c1 != null) c1.MaterialOverride = crownMat;
        if (c2 != null) c2.MaterialOverride = crown2Mat;
        var fieldMat = new StandardMaterial3D { AlbedoColor = new Color("#b5a35a"), Roughness = 1f };
        // farmland south of the road
        var fields = new[] { new Color("#c9b45e"), new Color("#8fb15a"), new Color("#b8995a"), new Color("#9cbd63") };
        for (var i = 0; i < 8; i++)
        {
            var w = 40 + rng.Next(30);
            var f = Plane(root, new Vector3(-90 + i * 52, -0.025f, s + 40 + (i % 2) * 6), new Vector2(w, 50), fields[i % fields.Length]);
            if (i % 2 == 0) f.MaterialOverride = fieldMat;
        }
        SeasonMaterials(grass, plotGrass, crownMat, crown2Mat, fieldMat);
        _lastSeason = (Season)(-1);
        // a few neighbours: a farm, a small office block, and a distant data centre
        Neighbour(root, new Vector3(-40, 0, s + 20), new Vector3(10, 5, 7), new Color("#c96b4e"), new Color("#7d3f32"));
        Neighbour(root, new Vector3(-30, 0, s + 18), new Vector3(6, 7, 6), new Color("#e3dccf"), new Color("#6f7b85"));
        Neighbour(root, new Vector3(s + 30, 0, s + 22), new Vector3(16, 9, 12), new Color("#d9dee2"), Brand.Fiber);
        Neighbour(root, new Vector3(s + 60, 0, s + 18), new Vector3(28, 8, 18), new Color("#cfd5da"), new Color("#55D2F2"));
        // hills on the horizon
        var hill = new Color("#7fa267");
        for (var i = 0; i < 9; i++)
        {
            var m = Ball(root, new Vector3(-200 + i * 70, -18, -170 - (i % 3) * 25), 45 + (i % 4) * 10, hill, 0.6f);
            m.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        }
    }

    void Neighbour(Node3D root, Vector3 pos, Vector3 size, Color wall, Color roof)
    {
        Box(root, pos + new Vector3(0, size.Y / 2, 0), size, wall);
        Box(root, pos + new Vector3(0, size.Y + 0.15f, 0), new Vector3(size.X + 0.3f, 0.3f, size.Z + 0.3f), roof);
        for (var y = 1.5f; y < size.Y - 1; y += 2.5f)
            Box(root, pos + new Vector3(0, y, size.Z / 2 + 0.01f), new Vector3(size.X * 0.85f, 0.7f, 0.02f), new Color("#3c5a75"), 0.2f).MaterialOverride = WindowMat;
    }

    static MultiMeshInstance3D Multi(Node3D root, Mesh mesh, List<Transform3D> xf, Color c, bool shadow = true)
    {
        if (xf.Count == 0) return null;
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = mesh, InstanceCount = xf.Count };
        for (var i = 0; i < xf.Count; i++) mm.SetInstanceTransform(i, xf[i]);
        var mi = new MultiMeshInstance3D { Multimesh = mm, MaterialOverride = Mat(c, 0.9f), CastShadow = shadow ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off };
        root.AddChild(mi);
        return mi;
    }

    static MeshInstance3D Plane(Node3D parent, Vector3 pos, Vector2 size, Color c)
    {
        var m = new MeshInstance3D { Mesh = new PlaneMesh { Size = size }, Position = pos, MaterialOverride = Mat(c, 1f), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        parent.AddChild(m);
        return m;
    }

    void BuildGrid(Node3D root, int s)
    {
        var im = new ImmediateMesh();
        im.SurfaceBegin(Mesh.PrimitiveType.Lines);
        for (var i = 0; i <= s; i++)
        {
            im.SurfaceAddVertex(new Vector3(i, 0.03f, 0)); im.SurfaceAddVertex(new Vector3(i, 0.03f, s));
            im.SurfaceAddVertex(new Vector3(0, 0.03f, i)); im.SurfaceAddVertex(new Vector3(s, 0.03f, i));
        }
        im.SurfaceEnd();
        _grid = new MeshInstance3D { Mesh = im, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        _grid.MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(1, 1, 1, 0.18f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
        root.AddChild(_grid);
    }

    public bool GridVisible { get => _grid?.Visible ?? false; set { if (_grid != null) _grid.Visible = value; } }

    /// <summary>Grid height follows the floor being viewed.</summary>
    void PlaceGrid() { if (_grid != null) _grid.Position = new Vector3(0, _viewFloor == 0 ? 0 : Base(_viewFloor) + 0.02f, 0); }

    // ---------------- per-frame sync ----------------
    public void Sync(double delta)
    {
        if (_state == null) return;
        if (_layoutRev != _state.LayoutRevision) { _layoutRev = _state.LayoutRevision; RebuildLayout(); _siteTimer = 0; _lampEnergy = -1; }
        if (_objRev != _state.Revision) { _objRev = _state.Revision; RebuildObjects(); _lampEnergy = -1; }
        _siteTimer -= delta;
        if (_siteTimer <= 0) { _siteTimer = 0.35; RebuildSites(); }
        if (_stock != _state.MaterialStock) { _stock = _state.MaterialStock; RebuildPallets(); }
        SyncAgents(delta);
        Animate(delta);
        UpdateSky(delta);
        Overlay.Sync(_state, delta);
    }

    // ---------------- buildings ----------------
    /// <summary>Interior view for a building: roofs off and it is tall enough for the viewed floor.</summary>
    bool Open(Building b) => !_roofs && b.Floors > _viewFloor;

    public static Color WallColor(Building b) => b.Facade switch
    {
        Facade.Brick => new Color("#b5654a"),
        Facade.Glass => new Color("#7fa9c6"),
        Facade.Cladding => new Color("#b8c2c9"),
        _ => b.Kind == BuildingKind.Office ? new Color("#cfcac2") : new Color("#cfd4d8"),
    };

    /// <summary>Facade detail on one floor: windows for offices, panel joints for a concrete datacenter, ribs for cladding.</summary>
    void FacadeDetail(Node3D root, Building b, int f)
    {
        var y = Base(f);
        var faces = new[]
        {
            (new Vector3(b.X + b.W / 2f, 0, b.Z + b.D + 0.135f), (float)b.W, 0f),
            (new Vector3(b.X + b.W / 2f, 0, b.Z - 0.135f), (float)b.W, 0f),
            (new Vector3(b.X + b.W + 0.135f, 0, b.Z + b.D / 2f), (float)b.D, 90f),
            (new Vector3(b.X - 0.135f, 0, b.Z + b.D / 2f), (float)b.D, 90f),
        };
        var glass = new Color("#34536e");
        foreach (var (c, len, rot) in faces)
        {
            var n = new Node3D { Position = c, RotationDegrees = new Vector3(0, rot, 0) };
            root.AddChild(n);
            switch (b.Facade)
            {
                case Facade.Brick:
                    for (var yy = 0.45f; yy < FloorH; yy += 0.45f) Box(n, new Vector3(0, y + yy, 0), new Vector3(len, 0.03f, 0.01f), new Color("#8f4a36"));
                    for (var x = -len / 2 + 1.5f; x < len / 2 - 1; x += 2.5f)
                    {
                        Box(n, new Vector3(x, y + 1.7f, 0.005f), new Vector3(1.5f, 1.5f, 0.02f), glass, 0.2f).MaterialOverride = WindowMat;
                        Box(n, new Vector3(x, y + 0.92f, 0.03f), new Vector3(1.7f, 0.1f, 0.06f), new Color("#e9e3d8"));
                    }
                    break;
                case Facade.Glass:
                    for (var x = -len / 2 + 1; x < len / 2; x += 1.5f) Box(n, new Vector3(x, y + FloorH / 2, 0.005f), new Vector3(0.06f, FloorH, 0.03f), new Color("#3a4652"));
                    Box(n, new Vector3(0, y + 0.02f, 0.005f), new Vector3(len, 0.1f, 0.03f), new Color("#3a4652"));
                    break;
                case Facade.Concrete when b.Kind == BuildingKind.Office:
                    for (var x = -len / 2 + 1.5f; x < len / 2 - 1; x += 2.2f)
                        Box(n, new Vector3(x, y + 1.7f, 0.005f), new Vector3(1.6f, 1.7f, 0.02f), glass, 0.2f).MaterialOverride = WindowMat;
                    Box(n, new Vector3(0, y + 0.05f, 0.005f), new Vector3(len, 0.04f, 0.01f), new Color("#b3ada4"));
                    break;
                case Facade.Concrete:
                    for (var x = -len / 2 + 3; x < len / 2; x += 3) Box(n, new Vector3(x, y + FloorH / 2, 0.005f), new Vector3(0.04f, FloorH, 0.01f), new Color("#b4babf"));
                    if (f == 0) FacadeLights(n, len, y);
                    Box(n, new Vector3(0, y + FloorH - 0.05f, 0.005f), new Vector3(len, 0.04f, 0.01f), new Color("#b4babf"));
                    break;
                case Facade.Cladding:
                    for (var x = -len / 2 + 0.5f; x < len / 2; x += 0.5f) Box(n, new Vector3(x, y + FloorH / 2, 0.01f), new Vector3(0.05f, FloorH, 0.03f), new Color("#a3adb4"));
                    break;
            }
        }
    }

    /// <summary>Lighting is placed automatically: strips above the rack rows in halls, panels in other rooms.</summary>
    void RoomLights(Node3D root, Room r, float y)
    {
        var xf = new List<Transform3D>();
        if (r.Type == RoomType.DataHall)
        {
            for (var z = r.Z + 1; z < r.Z + r.D - 1; z += 3)
                xf.Add(new Transform3D(Basis.Identity.Scaled(new Vector3(r.W - 1.2f, 1, 1)), new Vector3(r.X + r.W / 2f, y + 2.9f, z + 0.5f)));
        }
        else
        {
            for (var x = r.X + 1; x < r.X + r.W; x += 3)
                for (var z = r.Z + 1; z < r.Z + r.D; z += 3)
                    xf.Add(new Transform3D(Basis.Identity, new Vector3(x + 0.5f, y + 2.9f, z + 0.5f)));
        }
        if (xf.Count == 0) return;
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = new BoxMesh { Size = new Vector3(1, 0.03f, 0.07f) }, InstanceCount = xf.Count };
        if (r.Type != RoomType.DataHall) mm.Mesh = new CylinderMesh { TopRadius = 0.22f, BottomRadius = 0.28f, Height = 0.06f, RadialSegments = 10 };
        for (var i = 0; i < xf.Count; i++) mm.SetInstanceTransform(i, xf[i]);
        root.AddChild(new MultiMeshInstance3D { Multimesh = mm, MaterialOverride = Mat(new Color("#fff3d6"), 1f, 0, false), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }

    void RebuildLayout()
    {
        foreach (var c in _layout.GetChildren()) c.QueueFree();
        PlaceGrid();
        Surfaces(_layout);
        FencesLayer(_layout);
        foreach (var b in _state.Buildings.Where(b => b.Stage == Stage.Done)) DoneBuilding(_layout, b);
    }

    /// <summary>Paint colour per surface; also used for the build menu thumbnails.</summary>
    public static readonly Dictionary<Surface, Color> SurfaceColors = new()
    {
        [Surface.Asphalt] = Brand.Asphalt, [Surface.Parking] = new Color("#575d66"), [Surface.Footpath] = new Color("#d8ccb4"),
        [Surface.BikePath] = new Color("#b4544a"), [Surface.Yard] = new Color("#b9aa8c"),
    };

    /// <summary>Post colour, panel colour and panel thickness per fence type.</summary>
    public static (Color post, Color panel, float thick) FenceStyle(FenceType type) => type switch
    {
        FenceType.Mesh => (new Color("#8d969c"), new Color("#a9b2b8", 0.55f), 0.03f),
        FenceType.Welded => (new Color("#2f5a3b"), new Color("#3d6b4a", 0.7f), 0.04f),
        FenceType.Palisade => (new Color("#262c31"), new Color("#2c3338", 0.88f), 0.06f),
        FenceType.Wall => (new Color("#b8b0a2"), new Color("#c4bcae"), 0.3f),
        _ => (new Color("#3a4046"), new Color(Brand.Amber, 0.35f), 0.02f),
    };

    void Surfaces(Node3D root)
    {
        var s = _state.PlotSize;
        var cols = SurfaceColors;
        _curveCells = _state.Curves.SelectMany(c => c.Cells).ToHashSet();
        var stripes = new List<Transform3D>();
        var tiles = new List<Transform3D>();
        for (var z = 0; z < s; z++)
            for (var x = 0; x < s; x++)
            {
                var t = _state.SurfaceAt(x, z);
                if (t == Surface.Grass || _curveCells.Contains(z * s + x)) continue;
                if (t == Surface.Footpath) tiles.Add(new Transform3D(Basis.Identity, new Vector3(x + 0.5f, 0.016f, z + 0.5f)));
            }
        foreach (var k in cols.Keys) SmoothSurface(root, k, cols[k]);
        foreach (var c in _state.Curves) CurveStrip(root, c, cols[c.Surface]);
        Driveways(root, s);
        stripes.AddRange(ParkingStripes(ParkingLayout.Lines(s, (x, z) => _state.SurfaceAt(x, z) == Surface.Parking && !_curveCells.Contains(z * s + x),
            (x, z) => _state.SurfaceAt(x, z) == Surface.Asphalt || _curveCells.Contains(z * s + x)), 0.02f));
        Multi(root, new PlaneMesh { Size = new Vector2(1, 1) }, stripes, new Color("#f2f2ee"), false);
        Multi(root, new PlaneMesh { Size = new Vector2(0.46f, 0.46f) }, tiles, new Color("#e4dac5"), false);
    }

    /// <summary>
    /// Draws one surface type as a single mesh with rounded edges: a cell corner that sticks out into grass
    /// becomes a quarter circle, and a grass corner tucked between two cells of the surface gets a fillet.
    /// Painted squares, L-turns and diagonal steps so read as flowing paths and roads.
    /// </summary>
    void SmoothSurface(Node3D root, Surface t, Color c)
    {
        var n = _state.PlotSize;
        const int seg = 6; const float r = 0.5f, y = 0.012f;
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        st.SetNormal(Vector3.Up);
        var any = false;
        void Tri(Vector2 a, Vector2 b, Vector2 d)
        {
            // clockwise seen from above is Godot's front face
            if ((b - a).Cross(d - a) < 0) (b, d) = (d, b);
            st.AddVertex(new Vector3(a.X, y, a.Y)); st.AddVertex(new Vector3(b.X, y, b.Y)); st.AddVertex(new Vector3(d.X, y, d.Y));
            any = true;
        }
        Vector2 Arc(Vector2 ctr, int sx, int sz, int i)
        {
            var ang = Mathf.Pi / 2 * i / seg;
            return ctr + new Vector2(sx * r * Mathf.Cos(ang), sz * r * Mathf.Sin(ang));
        }
        for (var z = 0; z < n; z++)
            for (var x = 0; x < n; x++)
            {
                var ctr = new Vector2(x + 0.5f, z + 0.5f);
                // cells paved by a curve are drawn by its smooth strip instead
                var mine = _state.SurfaceAt(x, z) == t && !_curveCells.Contains(z * n + x);
                if (!mine && !Open(x, z)) continue;
                foreach (var (sx, sz) in new[] { (1, 1), (-1, 1), (1, -1), (-1, -1) })
                {
                    var corner = ctr + new Vector2(sx * 0.5f, sz * 0.5f);
                    if (mine)
                    {
                        // round only a corner that sticks out into open grass on both sides and diagonally
                        if (Open(x + sx, z) && Open(x, z + sz) && Open(x + sx, z + sz))
                            for (var i = 0; i < seg; i++) Tri(ctr, Arc(ctr, sx, sz, i), Arc(ctr, sx, sz, i + 1));
                        else
                        {
                            var ex = ctr + new Vector2(sx * 0.5f, 0); var ez = ctr + new Vector2(0, sz * 0.5f);
                            Tri(ctr, ex, corner); Tri(ctr, corner, ez);
                        }
                    }
                    else if (Is(x + sx, z) && Is(x, z + sz))
                        // fillet in a grass corner between two cells of this surface
                        for (var i = 0; i < seg; i++) Tri(corner, Arc(ctr, sx, sz, i), Arc(ctr, sx, sz, i + 1));
                }
            }
        if (!any) return;
        root.AddChild(new MeshInstance3D { Mesh = st.Commit(), MaterialOverride = Mat(c, 0.9f), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });

        bool Is(int x, int z) => _state.InPlot(x, z) && _state.SurfaceAt(x, z) == t && !_curveCells.Contains(z * n + x);
        // grass with nothing on it; outside the plot counts as closed so edges stay straight
        bool Open(int x, int z) => _state.InPlot(x, z) && _state.SurfaceAt(x, z) == Surface.Grass && _state.BuildingAt(x, z) == null && !_state.InStation(x, z);
    }

    HashSet<int> _curveCells = new();

    /// <summary>A curve drawn as a smooth strip with round ends, only where its cells are still paved.</summary>
    void CurveStrip(Node3D root, CurvePath c, Color col, float y = 0.013f)
    {
        var n = _state.PlotSize;
        var mesh = StripMesh(c, y, t =>
        {
            var (px, pz) = c.At(t);
            int x = (int)Math.Floor(px), z = (int)Math.Floor(pz);
            // keep the strip where this curve, or other paving of the same kind, still lies
            return _state.InPlot(x, z) && _state.SurfaceAt(x, z) == c.Surface;
        });
        if (mesh == null) return;
        root.AddChild(new MeshInstance3D { Mesh = mesh, MaterialOverride = Mat(col, 0.9f), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        if (c.Surface == Surface.Asphalt && c.Width >= 3)
        {
            // dashed centre line on wider roads
            var dash = new CurvePath { Ax = c.Ax, Az = c.Az, Cx = c.Cx, Cz = c.Cz, Bx = c.Bx, Bz = c.Bz, Width = 0.14 };
            var len = c.Length;
            var line = StripMesh(dash, y + 0.004f, t => (t * len / 1.5) % 2 < 1 && t > 0.04 && t < 0.96, caps: false);
            if (line != null) root.AddChild(new MeshInstance3D { Mesh = line, MaterialOverride = Mat(new Color("#ece6cc"), 0.9f), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        }
    }

    /// <summary>Triangle strip along a curve, with half-disc caps at both ends.</summary>
    static ArrayMesh StripMesh(CurvePath c, float y, Func<double, bool> keep, bool caps = true)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        st.SetNormal(Vector3.Up);
        var any = false;
        void Tri(Vector2 a, Vector2 b, Vector2 d)
        {
            if ((b - a).Cross(d - a) < 0) (b, d) = (d, b);
            st.AddVertex(new Vector3(a.X, y, a.Y)); st.AddVertex(new Vector3(b.X, y, b.Y)); st.AddVertex(new Vector3(d.X, y, d.Y));
            any = true;
        }
        var hw = (float)c.Width / 2;
        (Vector2 l, Vector2 r, Vector2 p, Vector2 dir) Edge(double t)
        {
            var (px, pz) = c.At(t); var (dx, dz) = c.Tangent(t);
            var p = new Vector2((float)px, (float)pz); var d = new Vector2((float)dx, (float)dz);
            var nrm = new Vector2(-d.Y, d.X) * hw;
            return (p + nrm, p - nrm, p, d);
        }
        var ts = c.Steps();
        for (var i = 0; i + 1 < ts.Count; i++)
        {
            if (!keep((ts[i] + ts[i + 1]) / 2)) continue;
            var a = Edge(ts[i]); var b = Edge(ts[i + 1]);
            Tri(a.l, a.r, b.r); Tri(a.l, b.r, b.l);
        }
        if (caps)
            foreach (var (t, sign) in new[] { (0.0, -1f), (1.0, 1f) })
            {
                if (!keep(t)) continue;
                var e = Edge(t);
                var nrm = new Vector2(-e.dir.Y, e.dir.X);
                for (var k = 0; k < 8; k++)
                {
                    float a0 = Mathf.Pi * k / 8, a1 = Mathf.Pi * (k + 1) / 8;
                    var v0 = e.p + (nrm * Mathf.Cos(a0) + e.dir * sign * Mathf.Sin(a0)) * hw;
                    var v1 = e.p + (nrm * Mathf.Cos(a1) + e.dir * sign * Mathf.Sin(a1)) * hw;
                    Tri(e.p, v0, v1);
                }
            }
        return any ? st.Commit() : null;
    }

    /// <summary>Preview of a curve being drawn: a see-through strip, its control handle, and a label.</summary>
    public void ShowGhostCurve(CurvePath c, bool ok, string text, Vector2? handle)
    {
        ClearGhost();
        var mesh = StripMesh(c, 0.07f, _ => true);
        if (mesh != null) _ghost.AddChild(new MeshInstance3D { Mesh = mesh, MaterialOverride = GhostMat(ok ? Brand.Ok : Brand.Critical) });
        foreach (var p in new[] { new Vector2((float)c.Ax, (float)c.Az), new Vector2((float)c.Bx, (float)c.Bz) })
            _ghost.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.25f, BottomRadius = 0.25f, Height = 0.3f }, Position = new Vector3(p.X, 0.15f, p.Y), MaterialOverride = GhostMat(Brand.Amber) });
        if (handle is { } h)
            _ghost.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.3f, Height = 0.6f }, Position = new Vector3(h.X, 0.4f, h.Y), MaterialOverride = GhostMat(Brand.Amber) });
        if (text != null)
        {
            var (mx, mz) = c.At(0.5);
            _ghost.AddChild(new Label3D
            {
                Text = text, Font = _font, FontSize = 64, PixelSize = 0.014f, OutlineSize = 14, OutlineModulate = new Color("#111D2B"),
                Modulate = Brand.Ink, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true,
                Position = new Vector3((float)mx, 2.2f, (float)mz),
            });
        }
    }

    /// <summary>Vehicle surface that gets a driveway onto the public road.</summary>
    static bool Drivable(Surface t) => t is Surface.Asphalt or Surface.Parking;

    /// <summary>
    /// The kerb along the public road, broken where asphalt or parking reaches the plot edge. There the
    /// strip between plot and road gets an asphalt apron with flared sides and a dropped kerb, so a
    /// driveway runs flush onto the road. The same goes for asphalt on the west edge towards the side road.
    /// </summary>
    void Driveways(Node3D root, int s)
    {
        var kerb = new Color("#b9b2a6");
        const float flare = 0.8f;
        // south edge: runs of drivable cells on the last row
        var runs = Runs(s, x => Drivable(_state.SurfaceAt(x, s - 1)));
        var from = s / 2f - 450f;
        foreach (var (a, b) in runs)
        {
            var x0 = a - flare; var x1 = b + 1 + flare;
            KerbPiece(from, x0);
            from = x1;
            // apron: full width over the verge, plus two flared wedges at the road side
            Plane(root, new Vector3((a + b + 1) / 2f, 0.004f, s + 0.55f), new Vector2(b + 1 - a, 1.1f), Brand.Asphalt);
            Plane(root, new Vector3((a + b + 1) / 2f, 0.002f, s + 0.85f), new Vector2(x1 - x0, 0.5f), Brand.Asphalt);
            Plane(root, new Vector3((a + b + 1) / 2f, 0.001f, s + 0.95f), new Vector2(x1 - x0 + 0.6f, 0.3f), Brand.Asphalt);
            // dropped kerb: a flush strip in kerb colour on either side
            Plane(root, new Vector3(x0 - 0.15f, 0.006f, s + 0.9f), new Vector2(0.3f, 0.25f), kerb);
            Plane(root, new Vector3(x1 + 0.15f, 0.006f, s + 0.9f), new Vector2(0.3f, 0.25f), kerb);
        }
        KerbPiece(from, s / 2f + 450f);

        // west edge: asphalt on the first column connects to the side road
        foreach (var (a, b) in Runs(s, z => Drivable(_state.SurfaceAt(0, z))))
        {
            Plane(root, new Vector3(-3.05f, 0.004f, (a + b + 1) / 2f), new Vector2(6.1f, b + 1 - a), Brand.Asphalt);
            Plane(root, new Vector3(-5.6f, 0.002f, (a + b + 1) / 2f), new Vector2(0.9f, b + 1 - a + 2 * flare), Brand.Asphalt);
        }

        void KerbPiece(float x0, float x1)
        {
            if (x1 - x0 < 0.05f) return;
            Box(root, new Vector3((x0 + x1) / 2f, 0.06f, s + 0.9f), new Vector3(x1 - x0, 0.12f, 0.25f), kerb);
        }
    }

    static List<(int a, int b)> Runs(int n, Func<int, bool> on)
    {
        var r = new List<(int, int)>();
        for (var i = 0; i < n; i++)
        {
            if (!on(i)) continue;
            var j = i;
            while (j + 1 < n && on(j + 1)) j++;
            r.Add((i, j));
            i = j;
        }
        return r;
    }

    void FencesLayer(Node3D root)
    {
        var s = _state.PlotSize;
        foreach (var (type, def) in Catalog.Fences)
        {
            var posts = new List<Transform3D>(); var panels = new List<Transform3D>();
            for (var z = 0; z < s; z++)
                for (var x = 0; x < s; x++)
                {
                    if (_state.FenceAt(x, z) != type) continue;
                    var gate = _state.ObjectAt(x, z);
                    if (gate != null && Catalog.IsGate(gate.Kind)) continue;
                    posts.Add(new Transform3D(Basis.Identity, new Vector3(x + 0.5f, def.Height / 2, z + 0.5f)));
                    if (Linked(x + 1, z)) panels.Add(new Transform3D(Basis.Identity, new Vector3(x + 1f, def.Height / 2, z + 0.5f)));
                    if (Linked(x, z + 1)) panels.Add(new Transform3D(Basis.Identity.Rotated(Vector3.Up, Mathf.Pi / 2), new Vector3(x + 0.5f, def.Height / 2, z + 1f)));
                    if (Linked(x - 1, z) && Gate(x - 1, z)) panels.Add(new Transform3D(Basis.Identity, new Vector3(x, def.Height / 2, z + 0.5f)));
                    if (Linked(x, z - 1) && Gate(x, z - 1)) panels.Add(new Transform3D(Basis.Identity.Rotated(Vector3.Up, Mathf.Pi / 2), new Vector3(x + 0.5f, def.Height / 2, z)));
                }
            var (postCol, panelCol, thick) = FenceStyle(type);
            Multi(root, new BoxMesh { Size = new Vector3(0.1f, def.Height, 0.1f) }, posts, postCol);
            Multi(root, new BoxMesh { Size = new Vector3(1f, def.Height * 0.92f, thick) }, panels, panelCol);
        }
        bool Linked(int x, int z) => _state.FenceAt(x, z) != FenceType.None;
        bool Gate(int x, int z) { var g = _state.ObjectAt(x, z); return g != null && Catalog.IsGate(g.Kind); }
    }

    void DoneBuilding(Node3D root, Building b)
    {
        var col = WallColor(b);
        var open = Open(b);
        Box(root, new Vector3(b.X + b.W / 2f, Slab / 2, b.Z + b.D / 2f), new Vector3(b.W + 0.2f, Slab, b.D + 0.2f), Brand.Concrete, 0.95f);
        var top = open ? _viewFloor : b.Floors - 1;
        for (var f = 0; f <= top; f++)
        {
            var cut = open && f == _viewFloor;
            var h = cut && !_fullWalls ? CutH : FloorH;
            if (cut && _fullWalls) Box(root, new Vector3(b.X + b.W / 2f, Base(f) + FloorH - 0.06f, b.Z + b.D / 2f), new Vector3(b.W, 0.12f, b.D), new Color("#dfe3e6"), 0.95f);
            if (f > 0) Box(root, new Vector3(b.X + b.W / 2f, Base(f) - 0.1f, b.Z + b.D / 2f), new Vector3(b.W, 0.2f, b.D), Brand.Concrete, 0.95f);
            if (cut) Box(root, new Vector3(b.X + b.W / 2f, Base(f) + 0.005f, b.Z + b.D / 2f), new Vector3(b.W - 0.1f, 0.01f, b.D - 0.1f), new Color("#c4c9cd"), 0.95f);
            var edges = GameState.Perimeter(b.X, b.Z, b.W, b.D).ToHashSet();
            if (cut)
                foreach (var r in _state.Rooms.Where(r => r.BuildingId == b.Id && r.Floor == f && r.Stage == Stage.Done))
                    foreach (var e in GameState.Perimeter(r.X, r.Z, r.W, r.D)) edges.Add(e);
            var doors = _state.Doors.Where(d => d.Floor == f).ToList();
            var doorEdges = doors.SelectMany(d => d.Edges()).ToHashSet();
            Walls(root, edges.Where(e => !doorEdges.Contains(e)), Base(f), h, col, cut ? new Color("#e4e8eb") : col, b);
            foreach (var d in doors.Where(d => d.Edges().Any(edges.Contains))) DoorModel(root, d, Base(f), h);
            if (!cut) FacadeDetail(root, b, f);
            if (cut)
                foreach (var r in _state.Rooms.Where(r => r.BuildingId == b.Id && r.Floor == f))
                {
                    RoomFloor(root, r, Base(f));
                    if (r.Stage == Stage.Done) RoomLights(root, r, Base(f));
                }
        }
        if (!open)
        {
            var roofY = Base(b.Floors - 1) + FloorH;
            Box(root, new Vector3(b.X + b.W / 2f, roofY + 0.12f, b.Z + b.D / 2f), new Vector3(b.W + 0.3f, 0.24f, b.D + 0.3f), new Color("#6f7a83")).MaterialOverride = RoofMat;
            Box(root, new Vector3(b.X + b.W / 2f, roofY + 0.27f, b.Z + b.D / 2f), new Vector3(b.W - 0.6f, 0.08f, b.D - 0.6f), new Color("#8a949c")).MaterialOverride = RoofTopMat;
            if (b.Kind == BuildingKind.Datacenter) Box(root, new Vector3(b.X + b.W / 2f, roofY - 0.25f, b.Z + b.D + 0.16f), new Vector3(b.W, 0.25f, 0.06f), Brand.Power);
            // a few rooftop units so the roof is not empty, unless the player put plant there
            var roofPlant = _state.Objects.Any(o => o.Floor == GameState.RoofFloor && b.Contains(o.X, o.Z));
            for (var i = 0; i < (roofPlant ? 0 : Math.Min(4, b.W / 6)); i++)
                Box(root, new Vector3(b.X + 3 + i * 5, roofY + 0.75f, b.Z + 2.5f), new Vector3(2.5f, 1f, 1.6f), new Color("#e3e7ea"));
        }
    }

    /// <summary>Wall lamps along a closed datacenter facade: they glow at night and every other one lights the wall and ground.</summary>
    static void FacadeLights(Node3D n, float len, float y)
    {
        var i = 0;
        for (var x = -len / 2 + 4.5f; x < len / 2 - 2; x += 9, i++)
        {
            Box(n, new Vector3(x, y + 3.1f, 0.08f), new Vector3(0.36f, 0.12f, 0.16f), new Color("#3a4652"));
            Box(n, new Vector3(x, y + 3.03f, 0.1f), new Vector3(0.3f, 0.02f, 0.12f), new Color("#fff2c4"), glow: true).MaterialOverride = LampMat;
            if (i % 2 != 0) continue;
            var l = new OmniLight3D { Position = new Vector3(x, y + 2.8f, 0.6f), OmniRange = 6, LightColor = new Color("#ffd9a0"), LightEnergy = 0, ShadowEnabled = false, Visible = false };
            l.AddToGroup(NightLightGroup);
            n.AddChild(l);
        }
    }

    /// <summary>Draw wall edges as merged runs so a long wall is one mesh.</summary>
    void Walls(Node3D root, IEnumerable<(int x, int z, int dir)> edges, float y, float h, Color outer, Color inner, Building b)
    {
        var list = edges.ToList();
        foreach (var dir in new[] { 0, 1 })
        {
            var groups = list.Where(e => e.dir == dir).GroupBy(e => dir == 0 ? e.x : e.z);
            foreach (var g in groups)
            {
                var along = g.Select(e => dir == 0 ? e.z : e.x).OrderBy(v => v).ToList();
                var start = along[0]; var prev = start;
                for (var i = 1; i <= along.Count; i++)
                {
                    if (i < along.Count && along[i] == prev + 1) { prev = along[i]; continue; }
                    var len = prev - start + 1;
                    var isOuter = b != null && (dir == 0 ? g.Key == b.X || g.Key == b.X + b.W : g.Key == b.Z || g.Key == b.Z + b.D);
                    var c = isOuter ? outer : inner;
                    var th = isOuter ? 0.25f : 0.14f;
                    if (dir == 0) Box(root, new Vector3(g.Key, y + h / 2, start + len / 2f), new Vector3(th, h, len + (isOuter ? 0.25f : 0)), c);
                    else Box(root, new Vector3(start + len / 2f, y + h / 2, g.Key), new Vector3(len + (isOuter ? 0.25f : 0), h, th), c);
                    if (i < along.Count) { start = along[i]; prev = start; }
                }
            }
        }
    }

    public static void DoorModel(Node3D root, Door d, float y, float wallH)
    {
        var def = Catalog.Doors[d.Type];
        var w = def.Width;
        var center = d.Dir == 0 ? new Vector3(d.X, y, d.Z + w / 2f) : new Vector3(d.X + w / 2f, y, d.Z);
        var n = new Node3D { Position = center, RotationDegrees = new Vector3(0, d.Dir == 0 ? 90 : 0, 0) };
        root.AddChild(n);
        var h = Math.Min(2.3f, wallH);
        var leaf = d.Type switch
        {
            DoorType.Fire => new Color("#c0392b"),
            DoorType.Badge => new Color("#3a4652"),
            DoorType.RollUp => new Color("#9aa4ab"),
            DoorType.Mantrap => new Color("#9fd3ee", 0.45f),
            _ => new Color("#8a6b4f"),
        };
        Box(n, new Vector3(0, 0.01f, 0), new Vector3(w, 0.02f, 0.3f), Brand.Power);
        if (wallH > 2.3f) Box(n, new Vector3(0, 2.3f + (wallH - 2.3f) / 2, 0), new Vector3(w, wallH - 2.3f, 0.25f), new Color("#c9d0d6"));
        if (d.Type == DoorType.RollUp)
        {
            Box(n, new Vector3(0, h * 0.85f, 0), new Vector3(w - 0.1f, h * 0.3f, 0.08f), leaf, 0.5f, 0.4f);
        }
        else
        {
            var leaves = w;
            for (var i = 0; i < leaves; i++)
            {
                var hinge = new Node3D { Position = new Vector3(-w / 2f + i * 1f + (i % 2 == 0 ? 0.05f : 0.95f), 0, 0), RotationDegrees = new Vector3(0, i % 2 == 0 ? -70 : 70, 0) };
                n.AddChild(hinge);
                Box(hinge, new Vector3(i % 2 == 0 ? 0.45f : -0.45f, h / 2, 0), new Vector3(0.88f, h - 0.05f, 0.05f), leaf);
            }
            if (d.Type is DoorType.Badge or DoorType.Mantrap) Box(n, new Vector3(w / 2f + 0.15f, 1.2f, 0.16f), new Vector3(0.12f, 0.18f, 0.04f), Brand.Ok, glow: true);
            if (d.Type == DoorType.Mantrap)
            {
                // a round glass booth with two sliding doors: one person at a time
                Cyl(n, new Vector3(0, h / 2, 0), 0.62f, h, new Color("#9fd3ee", 0.3f));
                Cyl(n, new Vector3(0, h + 0.04f, 0), 0.66f, 0.08f, new Color("#3a4652"));
                Box(n, new Vector3(0, h + 0.09f, 0.3f), new Vector3(0.2f, 0.04f, 0.04f), Brand.Ok, glow: true);
            }
        }
    }

    void RoomFloor(Node3D root, Room r, float y)
    {
        var col = new Color(Catalog.Rooms[r.Type].Color);
        if (r.Stage != Stage.Done) col = new Color(col, 0.55f);
        Box(root, new Vector3(r.X + r.W / 2f, y + 0.015f, r.Z + r.D / 2f), new Vector3(r.W - 0.12f, 0.01f, r.D - 0.12f), col, 0.95f);
        if (r.Type == RoomType.DataHall)
        {
            // raised-floor tile lines
            var lines = new List<Transform3D>();
            for (var x = r.X + 1; x < r.X + r.W; x++) lines.Add(new Transform3D(Basis.Identity, new Vector3(x, y + 0.022f, r.Z + r.D / 2f)));
            Multi(root, new BoxMesh { Size = new Vector3(0.03f, 0.005f, r.D - 0.2f) }, lines, new Color("#8a949c"), false);
        }
        var label = new Label3D
        {
            Text = Loc.T(Catalog.Rooms[r.Type].NameKey).ToUpperInvariant(),
            Font = _font, FontSize = 64, PixelSize = 0.012f, OutlineSize = 0,
            Modulate = new Color("#1d2a36", 0.55f),
            Position = new Vector3(r.X + r.W / 2f, y + 0.04f, r.Z + r.D / 2f),
            RotationDegrees = new Vector3(-90, 0, 0),
            Billboard = BaseMaterial3D.BillboardModeEnum.Disabled,
            DoubleSided = false,
        };
        root.AddChild(label);
    }

    // ---------------- construction sites ----------------
    void RebuildSites()
    {
        foreach (var c in _sites.GetChildren()) c.QueueFree();
        _cranes.Clear();
        if (_state == null) return;
        if (_state.GridRequest >= 0)
        {
            var g = _state.GridStation;
            _sites.AddChild(new Label3D
            {
                Text = $"{Catalog.GridLevels[_state.GridRequest].Mva:0} MVA · {Loc.T("grid_days_left", Math.Ceiling(_state.GridDaysLeft))}",
                Font = _font, FontSize = 56, PixelSize = 0.02f, OutlineSize = 12, OutlineModulate = new Color("#111D2B"),
                Modulate = Brand.Power, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true,
                Position = new Vector3(g.x + g.w / 2f, 6f, g.z + g.d / 2f),
            });
            foreach (var (x, z) in new[] { (g.x - 0.3f, g.z - 0.3f), (g.x + g.w + 0.3f, g.z - 0.3f), (g.x - 0.3f, g.z + g.d + 0.3f), (g.x + g.w + 0.3f, g.z + g.d + 0.3f) })
                Cyl(_sites, new Vector3(x, 0.5f, z), 0.12f, 1f, Brand.Amber, 0.04f);
        }
        foreach (var r in _state.Rooms.Where(r => r.Upgrade != 0 && r.Floor == _viewFloor))
        {
            var b = _state.BuildingOf(r);
            if (b == null || !Open(b)) continue;
            var tape = new Color(Brand.Cooling, 0.8f);
            var y = Base(r.Floor);
            Box(_sites, new Vector3(r.X + r.W / 2f, y + 0.04f, r.Z + 0.15f), new Vector3(r.W - 0.3f, 0.02f, 0.1f), tape);
            Box(_sites, new Vector3(r.X + r.W / 2f, y + 0.04f, r.Z + r.D - 0.15f), new Vector3(r.W - 0.3f, 0.02f, 0.1f), tape);
        }
        foreach (var b in _state.Buildings.Where(b => b.Stage != Stage.Done)) SiteBuilding(_sites, b);
        // rooms being built inside finished buildings
        foreach (var r in _state.Rooms.Where(r => r.Stage != Stage.Done))
        {
            var b = _state.BuildingOf(r);
            if (b == null || b.Stage != Stage.Done || !Open(b) || r.Floor != _viewFloor) continue;
            var p = GameState.StageProgress(r);
            var y = Base(r.Floor);
            Walls(_sites, GameState.Perimeter(r.X, r.Z, r.W, r.D).Where(e => !_state.IsOuterEdge(e)), y, Math.Max(0.12f, CutH * (float)p), Brand.Wall, new Color("#d5dade"), null);
            var tape = new Color(Brand.Amber, 0.8f);
            Box(_sites, new Vector3(r.X + r.W / 2f, y + 0.03f, r.Z + 0.1f), new Vector3(r.W, 0.02f, 0.08f), tape);
            Box(_sites, new Vector3(r.X + r.W / 2f, y + 0.03f, r.Z + r.D - 0.1f), new Vector3(r.W, 0.02f, 0.08f), tape);
        }
    }

    void SiteBuilding(Node3D root, Building b)
    {
        var p = (float)GameState.StageProgress(b);
        var col = WallColor(b);
        // site boundary tape and dug-out ground
        Box(root, new Vector3(b.X + b.W / 2f, 0.01f, b.Z + b.D / 2f), new Vector3(b.W + 1.2f, 0.02f, b.D + 1.2f), new Color("#8b6f4e"), 1f);
        foreach (var (x, z) in new[] { (b.X - 0.6f, b.Z - 0.6f), (b.X + b.W + 0.6f, b.Z - 0.6f), (b.X - 0.6f, b.Z + b.D + 0.6f), (b.X + b.W + 0.6f, b.Z + b.D + 0.6f) })
            Cyl(root, new Vector3(x, 0.5f, z), 0.12f, 1f, Brand.Amber, 0.04f);
        if (b.Stage == Stage.Foundation)
        {
            var rows = (int)Math.Ceiling(b.D * p);
            if (rows > 0) Box(root, new Vector3(b.X + b.W / 2f, Slab / 2, b.Z + rows / 2f), new Vector3(b.W, Slab, rows), Brand.Concrete, 0.95f);
            var ex = Excavator();
            ex.Position = new Vector3(b.X + b.W * 0.3f, 0, b.Z + Math.Min(b.D - 2, rows + 2));
            ex.RotationDegrees = new Vector3(0, 200, 0);
            root.AddChild(ex);
        }
        else
        {
            Box(root, new Vector3(b.X + b.W / 2f, Slab / 2, b.Z + b.D / 2f), new Vector3(b.W + 0.2f, Slab, b.D + 0.2f), Brand.Concrete, 0.95f);
            var total = b.Floors * FloorH;
            var h = b.Stage == Stage.Walls ? Math.Max(0.2f, total * p) : total;
            var edges = GameState.Perimeter(b.X, b.Z, b.W, b.D).ToList();
            var doorEdges = _state.Doors.Where(d => d.Floor == 0).SelectMany(d => d.Edges()).ToHashSet();
            Walls(root, edges.Where(e => !doorEdges.Contains(e)), Slab, Math.Min(h, FloorH), col, col, b);
            for (var f = 1; f < b.Floors; f++)
            {
                var fh = h - f * FloorH;
                if (fh <= 0) break;
                Box(root, new Vector3(b.X + b.W / 2f, Base(f) - 0.1f, b.Z + b.D / 2f), new Vector3(b.W, 0.2f, b.D), Brand.Concrete, 0.95f);
                Walls(root, edges, Base(f), Math.Min(fh, FloorH), col, col, b);
            }
            foreach (var d in _state.Doors.Where(d => d.Floor == 0 && d.Edges().Any(e => edges.Contains(e)))) DoorModel(root, d, Slab, Math.Min(h, FloorH));
            // scaffolding along the front
            var scaf = new List<Transform3D>();
            for (var x = b.X; x <= b.X + b.W; x += 2)
            {
                scaf.Add(new Transform3D(Basis.Identity.Scaled(new Vector3(1, h + 1, 1)), new Vector3(x, (h + 1) / 2, b.Z + b.D + 0.7f)));
                scaf.Add(new Transform3D(Basis.Identity.Scaled(new Vector3(1, h + 1, 1)), new Vector3(x, (h + 1) / 2, b.Z - 0.7f)));
            }
            Multi(root, new BoxMesh { Size = new Vector3(0.06f, 1, 0.06f) }, scaf, new Color("#9aa4ab"), false);
            if (b.Stage == Stage.Roof)
            {
                var rows = (int)Math.Ceiling(b.D * p);
                if (rows > 0) Box(root, new Vector3(b.X + b.W / 2f, Slab + total + 0.12f, b.Z + rows / 2f), new Vector3(b.W + 0.3f, 0.24f, rows), new Color("#6f7a83")).MaterialOverride = RoofMat;
            }
        }
        foreach (var (cx, cz, reach) in CranesFor(b, _state.PlotSize))
        {
            var crane = Crane(Math.Max(12, b.Floors * FloorH + 8), reach);
            crane.Position = new Vector3(cx, 0, cz);
            crane.RotationDegrees = new Vector3(0, (cx * 37 + cz * 11) % 360, 0);
            root.AddChild(crane);
            _cranes.Add(crane);
        }
        // progress label over the site
        var label = new Label3D
        {
            Text = $"{Loc.T("stage_" + b.Stage.ToString().ToLowerInvariant())} {Mathf.RoundToInt(p * 100)}%",
            Font = _font, FontSize = 56, PixelSize = 0.02f, OutlineSize = 12, OutlineModulate = new Color("#111D2B"),
            Modulate = Brand.Amber, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true,
            Position = new Vector3(b.X + b.W / 2f, b.Floors * FloorH + 3f, b.Z + b.D / 2f),
        };
        root.AddChild(label);
    }

    /// <summary>
    /// Tower cranes for a building site: a small site gets one crane at its corner; a big one gets a grid of cranes inside the footprint,
    /// each covering its own part with a normal jib (at most 30 m), like a real large build. Always on the plot.
    /// </summary>
    public static List<(float x, float z, float reach)> CranesFor(Building b, int plot)
    {
        const float MaxReach = 30f, Cover = 28f;
        var list = new List<(float, float, float)>();
        if (Math.Max(b.W, b.D) <= 24)
        {
            var reach = Math.Min(MaxReach, Mathf.Sqrt(b.W * b.W + b.D * b.D) * 0.85f);
            float x = b.X - 2f, z = b.Z - 2f;
            // keep it on the plot: put it inside the corner when the site touches the edge
            if (x < 1) x = b.X + 1.5f;
            if (z < 1) z = b.Z + 1.5f;
            list.Add((x, z, Math.Max(8, reach)));
            return list;
        }
        int nx = Math.Max(1, (int)Math.Ceiling(b.W / Cover)), nz = Math.Max(1, (int)Math.Ceiling(b.D / Cover));
        float cw = b.W / (float)nx, cd = b.D / (float)nz;
        var r = Math.Min(MaxReach, Mathf.Sqrt(cw * cw + cd * cd) / 2 + 3);
        for (var i = 0; i < nx; i++)
            for (var j = 0; j < nz; j++)
                list.Add((Math.Clamp(b.X + cw * (i + 0.5f), 1, plot - 1), Math.Clamp(b.Z + cd * (j + 0.5f), 1, plot - 1), r));
        return list;
    }

    void RebuildPallets()
    {
        foreach (var c in _pallets.GetChildren()) c.QueueFree();
        if (_sim == null) return;
        var cells = _sim.YardCells().ToList();
        var n = Math.Min(cells.Count * 2, (_state.MaterialStock + 4) / 5);
        for (var i = 0; i < n; i++)
        {
            var (x, z) = cells[i / 2 % cells.Count];
            var p = Pallet(i);
            p.Position = new Vector3(x + 0.5f, (i % 2) * 0.72f, z + 0.5f);
            _pallets.AddChild(p);
        }
    }

    // ---------------- objects ----------------
    /// <summary>Height an object stands at: the ground, its floor, or the top of the roof.</summary>
    public static float ObjectY(GameState s, PlacedObject o)
    {
        var b = s.BuildingAt(o.X, o.Z);
        if (o.Floor == GameState.RoofFloor) return b == null ? 0 : Base(b.Floors) + 0.32f;
        return o.Floor > 0 || b != null ? Base(o.Floor) : 0f;
    }

    bool ObjectVisible(PlacedObject o)
    {
        var b = _state.BuildingAt(o.X, o.Z);
        if (b == null) return true;
        if (b.Stage != Stage.Done) return false;
        if (o.Floor == GameState.RoofFloor) return !Open(b);
        if (o.Kind == ObjKind.Lift) return Open(b);
        return Open(b) && o.Floor == _viewFloor;
    }

    void RebuildObjects()
    {
        foreach (var c in _objects.GetChildren()) c.QueueFree();
        _fans.Clear(); _gates.Clear(); _markers.Clear();
        if (_state == null) { _racks.Clear(); return; }
        GridStationModel();
        HallDecor();
        var racks = new List<(PlacedObject, float)>();
        foreach (var o in _state.Objects)
        {
            if (!ObjectVisible(o)) continue;
            var (w, d) = GameState.Footprint(o);
            var y = ObjectY(_state, o);
            var pos = new Vector3(o.X + w / 2f, y, o.Z + d / 2f);
            var basis = Basis.Identity.Rotated(Vector3.Up, -Mathf.Pi / 2 * o.Rot);
            if (o.Kind == ObjKind.Rack)
            {
                // racks and their servers are instanced per chunk by the rack renderer
                racks.Add((o, y));
                if (o.Status > ObjStatus.Delivered && o.Status != ObjStatus.Live) AddMarker(o, pos);
                continue;
            }
            var n = Models.Object(o.Kind);
            n.Position = pos;
            n.Basis = basis;
            foreach (var f in n.GetChildren().OfType<Node3D>().Where(c => c.Name.ToString().StartsWith("Fan"))) _fans.Add(f);
            if (Catalog.IsVehicleStop(o.Kind)) _gates[o.Id] = (n, o.Kind, o.Def.W);
            if (o.Def.Chain && o.Status <= ObjStatus.Delivered) Tint(n, Brand.Ink);
            else if (o.Def.Chain && o.Status != ObjStatus.Live) AddMarker(o, pos);
            _objects.AddChild(n);
        }
        _racks.Update(racks);
    }

    void AddMarker(PlacedObject o, Vector3 pos)
    {
        var c = o.Status == ObjStatus.Placed ? Brand.Power : Brand.Fiber;
        var m = Marker(c);
        m.Position = pos + new Vector3(0, 2.7f, 0);
        m.Name = "Marker";
        _objects.AddChild(m);
        _markers.Add(m);
    }

    // ---------------- agents ----------------
    bool AgentVisible(Agent a)
    {
        var b = _state.BuildingAt((int)Math.Floor(a.X), (int)Math.Floor(a.Z));
        if (b == null || b.Stage != Stage.Done) return a.Floor == 0;
        return Open(b) && a.Floor == _viewFloor;
    }

    void SyncAgents(double delta)
    {
        if (_sim == null) return;
        var alive = new HashSet<int>();
        var t = (float)Time.GetTicksMsec() / 1000f;
        foreach (var a in _sim.Agents)
        {
            alive.Add(a.Id);
            if (!_agentNodes.TryGetValue(a.Id, out var n))
            {
                n = a.Role == Role.Truck ? Models.Truck(a.Load) : Person(a.Role, a.Role == Role.Staff ? a.EmpId : a.Id, a.Staff);
                _agentsRoot.AddChild(n);
                _agentNodes[a.Id] = n;
                n.Position = new Vector3((float)a.X, 0, (float)a.Z);
            }
            n.Visible = AgentVisible(a);
            var b = _state.BuildingAt((int)Math.Floor(a.X), (int)Math.Floor(a.Z));
            var y = a.Role == Role.Truck ? 0f : b != null && b.Stage != Stage.Foundation ? Base(a.Floor) : 0f;
            var target = new Vector3((float)a.X, y, (float)a.Z);
            n.Position = n.Position.DistanceTo(target) > 3 ? target : n.Position.Lerp(target, Mathf.Min(1, (float)delta * 12));
            var yaw = (float)a.Heading;
            n.Rotation = new Vector3(0, Mathf.LerpAngle(n.Rotation.Y, yaw, Mathf.Min(1, (float)delta * 10)), 0);
            if (a.Role == Role.Truck)
            {
                var cargo = n.GetNodeOrNull<Node3D>("Cargo");
                if (cargo != null) cargo.Visible = a.Carry > 0;
                continue;
            }
            var fig = n.GetNode<Node3D>("Fig");
            var walking = a.Moving;
            var ph = t * 9 + a.Id;
            fig.Position = new Vector3(0, walking ? Mathf.Abs(Mathf.Sin(ph)) * 0.06f : 0, 0);
            fig.GetNode<Node3D>("L1").RotationDegrees = new Vector3(walking ? Mathf.Sin(ph) * 28 : 0, 0, 0);
            fig.GetNode<Node3D>("L2").RotationDegrees = new Vector3(walking ? -Mathf.Sin(ph) * 28 : 0, 0, 0);
            var arm = a.Working ? Mathf.Sin(t * 14 + a.Id) * 50 - 40 : walking ? -Mathf.Sin(ph) * 22 : 0;
            fig.GetNode<Node3D>("A1").RotationDegrees = new Vector3(arm, 0, 0);
            fig.GetNode<Node3D>("A2").RotationDegrees = new Vector3(a.Working ? -arm - 80 : -arm, 0, 0);
            n.GetNode<Node3D>("Crate").Visible = a.Role == Role.Builder && a.Loaded && a.Carry > 0 && a.Phase == 1;
            // movers of a customer migration push a trolley of servers: in to the racks, or out to the truck
            var cart = n.GetNode<Node3D>("Cart");
            cart.Visible = a.Role == Role.Visitor && a.Carry > 0;
            if (cart.Visible) fig.GetNode<Node3D>("A1").RotationDegrees = fig.GetNode<Node3D>("A2").RotationDegrees = new Vector3(-62, 0, 0);
            n.GetNode<Node3D>("Rack").Visible = a.Role == Role.Fitter && a.CarryObj != 0 && a.Phase == 2;
            AnimateReels(n, fig, a, t);
        }
        foreach (var id in _agentNodes.Keys.Where(id => !alive.Contains(id)).ToList())
        {
            _agentNodes[id].QueueFree(); _agentNodes.Remove(id);
        }
    }

    /// <summary>
    /// Engineers walk with a reel of amber power cable or orange fiber in both hands; at the job the reel
    /// goes on the floor beside them and a cable run leads to the rack while the drum slowly unrolls.
    /// Electricians and network engineers on a repair carry one too.
    /// </summary>
    static void AnimateReels(Node3D n, Node3D fig, Agent a, float t)
    {
        var p = n.GetNodeOrNull<Node3D>("ReelP");
        var f = n.GetNodeOrNull<Node3D>("ReelF");
        if (p == null && f == null) return;
        var kind = a.Role == Role.Engineer ? a.Cable
            : a.Target == 0 ? CableKind.None
            : a.Staff == StaffRole.NetworkEng ? CableKind.Fiber : CableKind.Power;
        if (a.Role == Role.Engineer && a.Phase == 0) kind = CableKind.None;
        var tool = fig.GetNodeOrNull<Node3D>("Tool");
        if (tool != null) tool.Visible = kind == CableKind.None;
        foreach (var (reel, k) in new[] { (p, CableKind.Power), (f, CableKind.Fiber) })
        {
            if (reel == null) continue;
            reel.Visible = kind == k;
            if (!reel.Visible) continue;
            var down = a.Working;
            reel.Position = down ? new Vector3(0.42f, 0.16f, 0.22f) : new Vector3(0, 0.76f, 0.4f);
            reel.GetNode<Node3D>("Lead").Visible = down;
            var drum = reel.GetNode<Node3D>("Drum");
            drum.RotationDegrees = new Vector3(down ? t * 40 % 360 : a.Moving ? t * 90 % 360 : drum.RotationDegrees.X, 0, 0);
            if (!down)
                fig.GetNode<Node3D>("A1").RotationDegrees = fig.GetNode<Node3D>("A2").RotationDegrees = new Vector3(-48, 0, 0);
        }
    }

    /// <summary>Sliding gates roll their leaf aside and barriers lift their arm, as far as the simulation has them open.</summary>
    void AnimateGates()
    {
        foreach (var (id, (node, kind, width)) in _gates)
        {
            if (!IsInstanceValid(node)) continue;
            var open = _sim != null && _sim.GateOpen.TryGetValue(id, out var v) ? (float)v : 0f;
            var eased = open * open * (3 - 2 * open);
            if (kind == ObjKind.Barrier)
            {
                var arm = node.GetNodeOrNull<Node3D>("Arm");
                if (arm != null) arm.RotationDegrees = new Vector3(0, 0, 82 * eased);
            }
            else
            {
                var leaf = node.GetNodeOrNull<Node3D>("Leaf");
                if (leaf != null) leaf.Position = new Vector3((width - 0.1f) * eased, 0, 0);
            }
        }
    }

    void Animate(double delta)
    {
        if (MovingParts)
        foreach (var c in _cranes)
        {
            var jib = c.GetNodeOrNull<Node3D>("Jib");
            if (jib != null) jib.RotateY((float)delta * 0.15f);
        }
        if (MovingParts)
        {
            foreach (var m in _markers) m.RotateY((float)delta * 2f);
            foreach (var f in _fans) if (IsInstanceValid(f)) f.RotateY((float)delta * 9f);
        }
        // gates are gameplay (vehicles wait for them), so they always move
        AnimateGates();
        // server LEDs blink in their shader (ServerFill), nothing to do per frame here
    }

    // ---------------- power and cooling ----------------
    void GridStationModel()
    {
        var g = _state.GridStation;
        var m = Models.GridStation(_state.GridLevel, g.w, g.d);
        m.Position = new Vector3(g.x + g.w / 2f, 0, g.z + g.d / 2f);
        _objects.AddChild(m);
    }

    /// <summary>Cold aisle tiles and the glass corridor roof for upgraded halls, and the overhead busbar.</summary>
    void HallDecor()
    {
        var tiles = new List<Transform3D>(); var glass = new List<Transform3D>(); var frames = new List<Transform3D>();
        var busA = new List<Transform3D>(); var busB = new List<Transform3D>();
        foreach (var r in _state.Rooms.Where(r => r.Type == RoomType.DataHall && r.Stage == Stage.Done && r.Floor == _viewFloor))
        {
            var b = _state.BuildingOf(r);
            if (b == null || b.Stage != Stage.Done || !Open(b)) continue;
            var y = Base(r.Floor);
            var racks = _state.Objects.Where(o => o.Kind == ObjKind.Rack && o.Floor == r.Floor && r.Contains(o.X, o.Z) && o.Status >= ObjStatus.Placed).ToList();
            if (r.Containment >= Containment.HotCold)
            {
                var aisle = new HashSet<(int, int)>();
                foreach (var o in racks)
                {
                    var (dx, dz) = FrontOffset(o.Rot);
                    var c = (o.X + dx, o.Z + dz);
                    if (r.Contains(c.Item1, c.Item2) && _state.ObjectAt(c.Item1, c.Item2, r.Floor) == null) aisle.Add(c);
                }
                foreach (var (x, z) in aisle)
                {
                    tiles.Add(new Transform3D(Basis.Identity, new Vector3(x + 0.5f, y + 0.03f, z + 0.5f)));
                    if (r.Containment >= Containment.ColdCorridor) glass.Add(new Transform3D(Basis.Identity, new Vector3(x + 0.5f, y + 2.16f, z + 0.5f)));
                }
                if (r.Containment >= Containment.ColdCorridor)
                    foreach (var (x, z) in aisle)
                        foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                        {
                            // sliding doors at the open ends of the corridor
                            var n = (x + dx, z + dz);
                            if (aisle.Contains(n) || _state.ObjectAt(n.Item1, n.Item2, r.Floor) != null || !r.Contains(n.Item1, n.Item2)) continue;
                            var basis = dx != 0 ? Basis.Identity.Rotated(Vector3.Up, Mathf.Pi / 2) : Basis.Identity;
                            frames.Add(new Transform3D(basis, new Vector3(x + 0.5f + dx * 0.5f, y + 1.08f, z + 0.5f + dz * 0.5f)));
                        }
            }
            if (r.Busbar)
                foreach (var o in racks)
                {
                    var basis = o.Rot % 2 == 0 ? Basis.Identity : Basis.Identity.Rotated(Vector3.Up, Mathf.Pi / 2);
                    var (fx, fz) = FrontOffset(o.Rot);
                    var c = new Vector3(o.X + 0.5f, y + 2.5f, o.Z + 0.5f);
                    busA.Add(new Transform3D(basis, c + new Vector3(fx, 0, fz) * -0.22f));
                    busB.Add(new Transform3D(basis, c + new Vector3(fx, 0, fz) * -0.38f));
                }
        }
        Multi(_objects, new BoxMesh { Size = new Vector3(0.94f, 0.012f, 0.94f) }, tiles, new Color("#7fd9ee"), false);
        Multi(_objects, new BoxMesh { Size = new Vector3(1f, 0.03f, 1f) }, glass, new Color(Brand.Cooling, 0.28f), false);
        Multi(_objects, new BoxMesh { Size = new Vector3(0.96f, 2.1f, 0.04f) }, frames, new Color(Brand.Cooling, 0.3f), false);
        Multi(_objects, new BoxMesh { Size = new Vector3(1.0f, 0.1f, 0.1f) }, busA, Brand.Power);
        Multi(_objects, new BoxMesh { Size = new Vector3(1.0f, 0.1f, 0.1f) }, busB, new Color("#d8dde1"));
    }

    /// <summary>Cell offset in front of an object (the cold aisle side of a rack).</summary>
    public static (int dx, int dz) FrontOffset(int rot) => (rot % 4) switch { 1 => (-1, 0), 2 => (0, -1), 3 => (1, 0), _ => (0, 1) };

    /// <summary>Parking bay lines as stretched unit planes, 10 cm wide.</summary>
    static IEnumerable<Transform3D> ParkingStripes(IEnumerable<ParkingLayout.Line> lines, float y)
    {
        foreach (var l in lines)
        {
            var len = (float)l.Length;
            if (len < 0.1f) continue;
            var alongX = Math.Abs(l.Bx - l.Ax) > Math.Abs(l.Bz - l.Az);
            var basis = Basis.FromScale(alongX ? new Vector3(len, 1, 0.1f) : new Vector3(0.1f, 1, len));
            yield return new Transform3D(basis, new Vector3((float)(l.Ax + l.Bx) / 2, y, (float)(l.Az + l.Bz) / 2));
        }
    }

    /// <summary>While dragging parking: the bays as they will be painted, including the parking next to it that joins up.</summary>
    public void ShowGhostParking(Rect2I r)
    {
        var n = _state.PlotSize;
        bool In(int x, int z) => x >= r.Position.X && x < r.End.X && z >= r.Position.Y && z < r.End.Y;
        var lines = ParkingLayout.Lines(n, (x, z) => In(x, z) || (_state.SurfaceAt(x, z) == Surface.Parking && !_curveCells.Contains(z * n + x)),
            (x, z) => !In(x, z) && (_state.SurfaceAt(x, z) == Surface.Asphalt || _curveCells.Contains(z * n + x)),
            (r.Position.X, r.Position.Y, r.End.X - 1, r.End.Y - 1));
        var mat = new StandardMaterial3D { AlbedoColor = new Color("#f2f2ee"), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
        var mesh = new PlaneMesh { Size = new Vector2(1, 1) };
        foreach (var xf in ParkingStripes(lines, 0.08f))
            _ghost.AddChild(new MeshInstance3D { Mesh = mesh, Transform = xf, MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }

    // ---------------- ghosts ----------------
    public void ClearGhost() { foreach (var c in _ghost.GetChildren()) c.QueueFree(); }

    StandardMaterial3D GhostMat(Color c) => new() { AlbedoColor = new Color(c, 0.45f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, NoDepthTest = false };

    public void ShowGhostRect(Rect2I r, Color c, float y, string text = null, bool fenceOutline = false, bool outline = false)
    {
        ClearGhost();
        if (fenceOutline)
        {
            foreach (var (x, z) in GameState.FenceCells(r.Position.X, r.Position.Y, r.Size.X, r.Size.Y))
                _ghost.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.9f, 1.6f, 0.9f) }, Position = new Vector3(x + 0.5f, y + 0.8f, z + 0.5f), MaterialOverride = GhostMat(c) });
        }
        else
        {
            _ghost.AddChild(new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(r.Size.X, r.Size.Y) }, Position = new Vector3(r.Position.X + r.Size.X / 2f, y + 0.06f, r.Position.Y + r.Size.Y / 2f), MaterialOverride = GhostMat(c) });
            if (outline)
            {
                // low walls along the edges show exactly where the room will go, also on a busy floor
                var wall = new StandardMaterial3D { AlbedoColor = new Color(c.Lightened(0.25f), 0.85f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
                float x0 = r.Position.X, z0 = r.Position.Y, w = r.Size.X, d = r.Size.Y, hgt = 0.5f, t = 0.12f;
                foreach (var (pos, size) in new[]
                {
                    (new Vector3(x0 + w / 2, y + hgt / 2, z0 + t / 2), new Vector3(w, hgt, t)),
                    (new Vector3(x0 + w / 2, y + hgt / 2, z0 + d - t / 2), new Vector3(w, hgt, t)),
                    (new Vector3(x0 + t / 2, y + hgt / 2, z0 + d / 2), new Vector3(t, hgt, d)),
                    (new Vector3(x0 + w - t / 2, y + hgt / 2, z0 + d / 2), new Vector3(t, hgt, d)),
                })
                    _ghost.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size }, Position = pos, MaterialOverride = wall, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
            }
        }
        if (text != null)
            _ghost.AddChild(new Label3D
            {
                Text = text, Font = _font, FontSize = 64, PixelSize = 0.014f, OutlineSize = 14, OutlineModulate = new Color("#111D2B"),
                Modulate = Brand.Ink, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true,
                Position = new Vector3(r.Position.X + r.Size.X / 2f, y + 2.2f, r.Position.Y + r.Size.Y / 2f),
            });
    }

    public void ShowGhostObjects(IEnumerable<(PlacedObject o, bool ok)> objs, string text = null)
    {
        ClearGhost();
        Vector3 last = default;
        foreach (var (o, ok) in objs)
        {
            var (w, d) = GameState.Footprint(o);
            var y = ObjectY(_state, o);
            var n = Models.Object(o.Kind);
            n.Position = last = new Vector3(o.X + w / 2f, y, o.Z + d / 2f);
            n.RotationDegrees = new Vector3(0, -90 * o.Rot, 0);
            Tint(n, ok ? Brand.Ok : Brand.Critical);
            // arrow showing the front
            var arrow = Box(n, new Vector3(0, 0.05f, d / 2f + 0.25f), new Vector3(0.3f, 0.02f, 0.3f), ok ? Brand.Ok : Brand.Critical);
            arrow.RotationDegrees = new Vector3(0, 45, 0);
            _ghost.AddChild(n);
        }
        if (text != null)
            _ghost.AddChild(new Label3D
            {
                Text = text, Font = _font, FontSize = 64, PixelSize = 0.014f, OutlineSize = 14, OutlineModulate = new Color("#111D2B"),
                Modulate = Brand.Ink, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true, Position = last + new Vector3(0, 3f, 0),
            });
    }

    public void ShowGhostDoor(Door d, bool ok)
    {
        ClearGhost();
        var w = Catalog.Doors[d.Type].Width;
        var y = Base(d.Floor);
        var pos = d.Dir == 0 ? new Vector3(d.X, y + 1.1f, d.Z + w / 2f) : new Vector3(d.X + w / 2f, y + 1.1f, d.Z);
        var size = d.Dir == 0 ? new Vector3(0.4f, 2.2f, w) : new Vector3(w, 2.2f, 0.4f);
        _ghost.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size }, Position = pos, MaterialOverride = GhostMat(ok ? Brand.Ok : Brand.Critical) });
    }
}
