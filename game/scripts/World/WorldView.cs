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
    public static float Base(int floor) => Slab + GameState.Level(floor) * FloorH;

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
    /// <summary>The sky dome; its colours follow the time of day and the weather (UpdateSky).</summary>
    readonly ProceduralSkyMaterial _skyMat = new()
    {
        SkyTopColor = new Color("#4f8fd6"), SkyHorizonColor = new Color("#bfdcf2"), SkyCurve = 0.12f,
        GroundHorizonColor = new Color("#bfdcf2"), GroundBottomColor = new Color("#4d6b3c"), GroundCurve = 0.05f,
        SunAngleMax = 18, SunCurve = 0.12f,
    };
    public const string MeadowGroup = "meadow";
    public static bool GrassOn = true;
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
            // lighting quality: bounce light from walls and ground (SSIL), and full global illumination (SDFGI) on Ultra
            _env.Environment.SsilEnabled = s.Lighting >= 1;
            _env.Environment.SdfgiEnabled = s.Lighting >= 2;
        }
        GrassOn = s.Grass;
        if (IsInsideTree()) foreach (var n in GetTree().GetNodesInGroup(MeadowGroup)) if (n is Node3D m) m.Visible = GrassOn;
        BlinkingLights = s.BlinkingLights;
        ServerFill.SetBlinking(BlinkingLights);
        MovingParts = s.MovingParts;
        DayNight = s.DayNight; WeatherFx = s.Weather; Seasons = s.Seasons;
        _lastSeason = (Season)(-1);
        if (IsInsideTree()) GetViewport().Msaa3D = s.Msaa switch { 2 => Viewport.Msaa.Msaa2X, 4 => Viewport.Msaa.Msaa4X, 8 => Viewport.Msaa.Msaa8X, _ => Viewport.Msaa.Disabled };
    }

    int _viewFloor;
    bool _roofs = true;
    public int ViewFloor { get => _viewFloor; set { _viewFloor = value == GameState.BasementFloor ? value : Math.Clamp(value, 0, Catalog.MaxFloors - 1); Invalidate(); } }
    /// <summary>The basement level is in view: the ground, the outside and buildings without a basement are hidden.</summary>
    public bool Underground => _viewFloor == GameState.BasementFloor && !_roofs;
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
                BackgroundMode = Godot.Environment.BGMode.Sky,
                BackgroundColor = Brand.Sky,
                Sky = new Sky { SkyMaterial = _skyMat, RadianceSize = Sky.RadianceSizeEnum.Size256 },
                ReflectedLightSource = Godot.Environment.ReflectionSource.Sky,
                SsilEnabled = true,
                SsilIntensity = 0.9f,
                SsilRadius = 4f,
                SdfgiUseOcclusion = true,
                SdfgiCascades = 4,
                SdfgiMinCellSize = 0.4f,
                SdfgiEnergy = 0.8f,
                FogSunScatter = 0.12f,
                FogAerialPerspective = 0.3f,
                // ambient light is part sky (soft blue fill from above, varies over the day) and part flat colour
                AmbientLightSource = Godot.Environment.AmbientSource.Sky,
                AmbientLightSkyContribution = 0.35f,
                AmbientLightColor = new Color("#dfeefa"),
                AmbientLightEnergy = 0.34f,
                // AgX tonemapping: natural highlight roll-off (sunlit roofs and grass no longer clip), then a gentle
                // warm grade (Look.Grade) and a little extra saturation for the friendly park-sim look
                TonemapMode = Godot.Environment.ToneMapper.Agx,
                TonemapExposure = 1.06f,
                AdjustmentEnabled = true,
                AdjustmentSaturation = 1.22f,
                AdjustmentContrast = 1.12f,
                AdjustmentColorCorrection = Look.Grade(),
                GlowEnabled = true,
                GlowIntensity = 0.45f,
                GlowBloom = 0.04f,
                GlowHdrThreshold = 1.0f,
                GlowBlendMode = Godot.Environment.GlowBlendModeEnum.Softlight,
                // contact shadows: ambient occlusion also darkens a little in direct sun, so objects sit on the ground
                SsaoIntensity = 2.2f,
                SsaoRadius = 1.2f,
                SsaoPower = 1.6f,
                SsaoDetail = 0.7f,
                SsaoLightAffect = 0.22f,
                FogEnabled = true,
                FogLightColor = new Color("#bfe0f4"),
                FogDensity = 0.0016f,
                FogSkyAffect = 0.2f,
                SsaoEnabled = true,
            }
        };
        AddChild(_env);
        Sun = new DirectionalLight3D { ShadowEnabled = true, LightEnergy = 0.95f, LightColor = new Color("#fff0d8"), RotationDegrees = new Vector3(-52, -38, 0), LightAngularDistance = 0.8f };
        Sun.DirectionalShadowMaxDistance = 160;
        Sun.DirectionalShadowBlendSplits = true;
        Sun.ShadowBlur = 1.5f;
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
        RoofTextures();
        Surroundings(_static, state.PlotSize);
        BuildGrid(_static, state.PlotSize);
        Invalidate(); _stock = -1;
        Sync(0);
    }

    // ---------------- surroundings ----------------
    void Surroundings(Node3D root, int s)
    {
        var rng = new Random(42);
        var grass = GrassMat(Brand.Grass, 40);
        var plotGrass = GrassMat(Brand.PlotGrass, 24);
        Plane(root, new Vector3(s / 2f, -0.03f, s / 2f), new Vector2(900, 900), Brand.Grass).MaterialOverride = grass;
        Plane(root, new Vector3(s / 2f, -0.02f, s / 2f), new Vector2(s, s), Brand.PlotGrass).MaterialOverride = plotGrass;
        // public road with markings and pavement
        Plane(root, new Vector3(s / 2f, -0.015f, s + 4), new Vector2(900, 6), Brand.Asphalt).MaterialOverride = SurfaceMat(Brand.Asphalt);
        for (var x = -420; x < 480; x += 6) Plane(root, new Vector3(s / 2f + x, -0.01f, s + 4), new Vector2(3, 0.18f), new Color("#ece6cc"));
        Plane(root, new Vector3(s / 2f, -0.012f, s + 7.8f), new Vector2(900, 1.6f), new Color("#c9c1b2")).MaterialOverride = SurfaceMat(new Color("#c9c1b2"));
        // side road heading north along the west of the plot
        Plane(root, new Vector3(-9, -0.014f, s / 2f - 60), new Vector2(6, 2 * s + 120), Brand.Asphalt).MaterialOverride = SurfaceMat(Brand.Asphalt);
        // fibre pits of carriers along the road (orange = fiber)
        foreach (var x in Catalog.StreetPits.Select((_, i) => _state.StreetPit(i)))
        {
            // flush cover in the pavement with a grey frame, like a real handhole
            Plane(root, new Vector3(x + 0.5f, -0.009f, s + Catalog.StreetPitOffset), new Vector2(1.1f, 1.1f), new Color("#8d8a84"));
            Plane(root, new Vector3(x + 0.5f, -0.006f, s + Catalog.StreetPitOffset), new Vector2(0.9f, 0.9f), Brand.Fiber);
        }
        // plot corner posts
        foreach (var c in new[] { new Vector3(0, 0, 0), new Vector3(s, 0, 0), new Vector3(0, 0, s), new Vector3(s, 0, s) })
            Box(root, c + new Vector3(0, 0.6f, 0), new Vector3(0.25f, 1.2f, 0.25f), Brand.Power);

        // forest to the north and east: CC0 tree models, instanced per model so it stays light
        var spots = new List<(float x, float z, float size)>();
        void Tree(float x, float z) => spots.Add((x, z, 0.75f + (float)rng.NextDouble() * 0.45f));
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
        Forest(root, spots, rng, 0.35f);
        Willows(root, s);
        // undergrowth and grass along the forest edges and the verges outside the plot
        var verge = new List<Vector2>();
        for (var i = 0; i < 9000; i++)
        {
            var x = -40 + (float)rng.NextDouble() * (s + 80);
            var z = -40 + (float)rng.NextDouble() * (s + 80);
            if (x >= -0.5f && x <= s + 0.5f && z >= -0.5f && z <= s + 0.5f) continue;
            if (z > s + 0.5f && z < s + 9) continue; // the public road and its pavement
            if (x > -12.5f && x < -5.5f && z > -70 && z < s + 9) continue; // the side road
            verge.Add(new Vector2(x, z));
        }
        Meadow(root, verge, rng, 0.07f, 140);
        var bushXf = new List<Transform3D>[Shrubs.Length]; var bushCol = new List<Color>[Shrubs.Length];
        for (var i = 0; i < Shrubs.Length; i++) { bushXf[i] = new(); bushCol[i] = new(); }
        for (var i = 0; i < 500; i++)
        {
            // bushes along the forest edge on the north and east
            var north = rng.Next(3) > 0;
            var x = north ? -60 + (float)rng.NextDouble() * (s + 120) : s + 6 + (float)rng.NextDouble() * 6;
            var z = north ? -8 - (float)rng.NextDouble() * 6 : -8 + (float)rng.NextDouble() * (s + 8);
            var k = rng.Next(Shrubs.Length); var sc = 0.8f + (float)rng.NextDouble() * 0.6f;
            bushXf[k].Add(new Transform3D(new Basis(Vector3.Up, (float)rng.NextDouble() * Mathf.Tau).Scaled(Vector3.One * sc), new Vector3(x, 0, z)));
            bushCol[k].Add(Shade(rng));
        }
        for (var i = 0; i < Shrubs.Length; i++) Assets.Many(root, Assets.Mesh(Shrubs[i].file, Shrubs[i].mats), bushXf[i], bushCol[i]);
        var fieldMat = Textured(new StandardMaterial3D { AlbedoColor = new Color("#b5a35a"), Roughness = 1f }, "raked_dirt", 3.5f, 30);
        // farmland south of the road
        var fields = new[] { new Color("#c9b45e"), new Color("#8fb15a"), new Color("#b8995a"), new Color("#9cbd63") };
        for (var i = 0; i < 8; i++)
        {
            var w = 40 + rng.Next(30);
            var f = Plane(root, new Vector3(-90 + i * 52, -0.025f, s + 72 + (i % 2) * 6), new Vector2(w, 50), fields[i % fields.Length]);
            if (i % 2 == 0) f.MaterialOverride = fieldMat;
        }
        SeasonMaterials(grass, plotGrass, null, null, fieldMat);
        _lastSeason = (Season)(-1);
        // neighbours along the road: an office park, a few houses and an industrial estate (CC0 Kenney city kits)
        Neighbours(root, s, rng);
        // hills on the horizon
        var hill = new Color("#7fa267");
        for (var i = 0; i < 9; i++)
        {
            var m = Ball(root, new Vector3(-200 + i * 70, -18, -170 - (i % 3) * 25), 45 + (i % 4) * 10, hill, 0.6f);
            m.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        }
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

    static MultiMeshInstance3D MultiMat(Node3D root, Mesh mesh, List<Transform3D> xf, Material mat)
    {
        if (xf.Count == 0) return null;
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = mesh, InstanceCount = xf.Count };
        for (var i = 0; i < xf.Count; i++) mm.SetInstanceTransform(i, xf[i]);
        var mi = new MultiMeshInstance3D { Multimesh = mm, MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        root.AddChild(mi);
        return mi;
    }

    static NoiseTexture2D _grain;
    static readonly Dictionary<(Color, float), StandardMaterial3D> GrainMats = new();

    /// <summary>
    /// A soft, seamless mottle (light and dark patches) so big flat areas such as grass and asphalt are not one flat colour.
    /// Projected in world space, so the pattern has the same size everywhere. The colour still comes from AlbedoColor (seasons tint it).
    /// </summary>
    public static NoiseTexture2D Grain => _grain ??= new NoiseTexture2D
    {
        Width = 256, Height = 256, Seamless = true, GenerateMipmaps = true,
        Noise = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth, Frequency = 0.012f, FractalOctaves = 4, Seed = 7 },
        ColorRamp = new Gradient { Colors = new[] { new Color(0.72f, 0.75f, 0.7f), new Color(1.1f, 1.1f, 1.05f) }, Offsets = new[] { 0f, 1f } },
    };

    /// <summary>Apply the mottle to a material: one pattern repeat every <paramref name="metres"/>.</summary>
    public static StandardMaterial3D Grained(StandardMaterial3D m, float metres)
    {
        m.AlbedoTexture = Grain;
        m.Uv1Triplanar = true; m.Uv1WorldTriplanar = true;
        m.Uv1Scale = Vector3.One / metres;
        return m;
    }

    static StandardMaterial3D GrainMat(Color c, float metres, float rough = 0.95f)
    {
        if (GrainMats.TryGetValue((c, metres), out var m)) return m;
        return GrainMats[(c, metres)] = Grained(new StandardMaterial3D { AlbedoColor = c, Roughness = rough }, metres);
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
        SyncLogo();
        SyncHallTenants();
        if (_layoutRev != _state.LayoutRevision) { _layoutRev = _state.LayoutRevision; RebuildLayout(); _siteTimer = 0; _lampEnergy = -1; }
        if (_objRev != _state.Revision) { _objRev = _state.Revision; RebuildObjects(); _lampEnergy = -1; }
        _siteTimer -= delta;
        if (_siteTimer <= 0) { _siteTimer = 0.35; RebuildSites(); }
        if (_stock != _state.MaterialStock) { _stock = _state.MaterialStock; RebuildPallets(); }
        SyncAgents(delta);
        SyncBikes();
        Animate(delta);
        UpdateSky(delta);
        Overlay.Sync(_state, delta);
    }

    // ---------------- buildings ----------------
    /// <summary>Interior view for a building: roofs off and it is tall enough for the viewed floor.</summary>
    bool Open(Building b) => !_roofs && b.HasFloor(_viewFloor);

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
            (new Vector3(b.X + b.W / 2f, 0, b.Z - 0.135f), (float)b.W, 180f),
            (new Vector3(b.X + b.W + 0.135f, 0, b.Z + b.D / 2f), (float)b.D, 90f),
            (new Vector3(b.X - 0.135f, 0, b.Z + b.D / 2f), (float)b.D, -90f),
        };
        var glass = new Color("#34536e");
        var brickWin = Assets.Mesh("made/window_brick.glb", Models.MadeMat);
        var officeWin = Assets.Mesh("made/window_office.glb", Models.MadeMat);
        foreach (var (c, len, rot) in faces)
        {
            var n = new Node3D { Position = c, RotationDegrees = new Vector3(0, rot, 0) };
            root.AddChild(n);
            var wins = new List<Transform3D>();
            switch (b.Facade)
            {
                case Facade.Brick:
                    for (var yy = 0.45f; yy < FloorH; yy += 0.45f) Box(n, new Vector3(0, y + yy, 0), new Vector3(len, 0.03f, 0.01f), new Color("#8f4a36"));
                    for (var x = -len / 2 + 1.5f; x < len / 2 - 1; x += 2.5f)
                    {
                        if (brickWin != null) { wins.Add(new Transform3D(Basis.Identity, new Vector3(x, y + 1.7f, 0))); continue; }
                        Box(n, new Vector3(x, y + 1.7f, 0.005f), new Vector3(1.5f, 1.5f, 0.02f), glass, 0.2f).MaterialOverride = WindowMat;
                        Box(n, new Vector3(x, y + 0.92f, 0.03f), new Vector3(1.7f, 0.1f, 0.06f), new Color("#e9e3d8"));
                    }
                    Assets.Many(n, brickWin, wins);
                    break;
                case Facade.Glass:
                    for (var x = -len / 2 + 1; x < len / 2; x += 1.5f) Box(n, new Vector3(x, y + FloorH / 2, 0.005f), new Vector3(0.06f, FloorH, 0.03f), new Color("#3a4652"));
                    Box(n, new Vector3(0, y + 0.02f, 0.005f), new Vector3(len, 0.1f, 0.03f), new Color("#3a4652"));
                    break;
                case Facade.Concrete when b.Kind == BuildingKind.Office:
                    for (var x = -len / 2 + 1.5f; x < len / 2 - 1; x += 2.2f)
                        if (officeWin != null) wins.Add(new Transform3D(Basis.Identity, new Vector3(x, y + 1.7f, 0)));
                        else Box(n, new Vector3(x, y + 1.7f, 0.005f), new Vector3(1.6f, 1.7f, 0.02f), glass, 0.2f).MaterialOverride = WindowMat;
                    Assets.Many(n, officeWin, wins);
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
        _doorLeaves.Clear();
        PlaceGrid();
        var under = Underground;
        foreach (var c in _static.GetChildren().OfType<Node3D>()) if (c != _grid) c.Visible = !under;
        if (_sites != null) _sites.Visible = !under;
        if (_pallets != null) _pallets.Visible = !under;
        if (_bikes != null) _bikes.Visible = !under;
        if (under)
        {
            Underworld(_layout);
            foreach (var b in _state.Buildings.Where(b => b.Stage == Stage.Done && b.Basement)) DoneBuilding(_layout, b);
            return;
        }
        Surfaces(_layout);
        DockBays(_layout);
        PlotMeadow(_layout);
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

    /// <summary>
    /// Loading bays: every roll-up door of a loading dock is one bay where a truck backs up. The apron gets yellow bay
    /// lines and a painted bay number, the wall black rubber bumpers and a steel dock leveller, so several bays read clearly.
    /// </summary>
    void DockBays(Node3D root)
    {
        if (_sim == null) return;
        var lines = new List<Transform3D>(); var bumpers = new List<Transform3D>(); var plates = new List<Transform3D>();
        var n = 0;
        foreach (var (_, apron, inside) in _sim.Docks().OrderBy(d => d.apron.x).ThenBy(d => d.apron.z))
        {
            n++;
            float ox = apron.x - inside.x, oz = apron.z - inside.z;
            var door = new Vector3((apron.x + inside.x) / 2f + 0.5f, 0, (apron.z + inside.z) / 2f + 0.5f);
            var side = new Vector3(-oz, 0, ox);
            var along = new Vector3(ox, 0, oz);
            var yaw = Mathf.Atan2(ox, oz);
            const float len = 13f;
            foreach (var sgn in new[] { -1f, 1f })
            {
                // bay lines start a little out from the wall and run as far as a truck is long
                var c = door + side * (sgn * 1.5f) + along * (0.4f + len / 2);
                lines.Add(new Transform3D(new Basis(Vector3.Up, yaw) * Basis.FromScale(new Vector3(0.12f, 1, len)), c + new Vector3(0, 0.025f, 0)));
                bumpers.Add(new Transform3D(new Basis(Vector3.Up, yaw), door + side * (sgn * 1.05f) + along * 0.22f + new Vector3(0, 0.95f, 0)));
            }
            plates.Add(new Transform3D(new Basis(Vector3.Up, yaw), door + along * 0.35f + new Vector3(0, 0.03f, 0)));
            root.AddChild(new Label3D
            {
                Text = n.ToString(), Font = _font, FontSize = 160, PixelSize = 0.01f, Modulate = new Color("#f2c230"), OutlineSize = 0,
                Position = door + along * 3.2f + new Vector3(0, 0.035f, 0), Rotation = new Vector3(-Mathf.Pi / 2, yaw + Mathf.Pi, 0),
                DoubleSided = true, Shaded = true,
            });
        }
        if (lines.Count == 0) return;
        Multi(root, new PlaneMesh { Size = new Vector2(1, 1) }, lines, new Color("#f2c230"), false);
        Multi(root, new BoxMesh { Size = new Vector3(0.28f, 0.45f, 0.14f) }, bumpers, new Color("#1d2126"));
        Multi(root, new BoxMesh { Size = new Vector3(2.0f, 0.04f, 0.5f) }, plates, new Color("#8a949c"));
    }

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
        stripes.AddRange(RoadCentreLines(s, 0.02f));
        Multi(root, new PlaneMesh { Size = new Vector2(1, 1) }, stripes, new Color("#f2f2ee"), false);
        ParkedCars(root, ParkingLayout.Lines(s, (x, z) => _state.SurfaceAt(x, z) == Surface.Parking && !_curveCells.Contains(z * s + x),
            (x, z) => _state.SurfaceAt(x, z) == Surface.Asphalt || _curveCells.Contains(z * s + x)),
            Commute.Chargers(_state).Select(o => new Vector3(o.X + 0.5f, 0, o.Z + 0.5f)).ToList());
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
        root.AddChild(new MeshInstance3D { Mesh = st.Commit(), MaterialOverride = SurfaceMat(c), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });

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
        root.AddChild(new MeshInstance3D { Mesh = mesh, MaterialOverride = SurfaceMat(col), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        if (c.Surface == Surface.Asphalt && c.Width >= Nav.TwoLaneWidth)
        {
            // dashed centre line on two-lane roads (traffic keeps right)
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
            // modelled fence panels and posts (tools/models/blender/infra.py), standing on the ground
            var file = "made/fence_" + type.ToString().ToLowerInvariant();
            var panelMesh = Assets.Mesh(file + "_panel.glb", Models.MadeMat);
            var postMesh = Assets.Mesh(file + "_post.glb", Models.MadeMat);
            if (panelMesh != null && postMesh != null)
            {
                Transform3D Ground(Transform3D t) => new(t.Basis, new Vector3(t.Origin.X, 0, t.Origin.Z));
                Assets.Many(root, postMesh, posts.Select(Ground).ToList());
                Assets.Many(root, panelMesh, panels.Select(Ground).ToList());
                continue;
            }
            Multi(root, new BoxMesh { Size = new Vector3(0.1f, def.Height, 0.1f) }, posts, postCol);
            Multi(root, new BoxMesh { Size = new Vector3(1f, def.Height * 0.92f, thick) }, panels, panelCol);
        }
        bool Linked(int x, int z) => _state.FenceAt(x, z) != FenceType.None;
        bool Gate(int x, int z) { var g = _state.ObjectAt(x, z); return g != null && Catalog.IsGate(g.Kind); }
    }

    static readonly Color BasementWall = new("#9a9d9f");

    /// <summary>
    /// The basement level: a soil cut-away at basement depth with the concrete pits of the basements, and a faint
    /// outline of every building above so the player knows where things are on the surface.
    /// </summary>
    void Underworld(Node3D root)
    {
        var s = _state.PlotSize;
        var y = Base(GameState.BasementFloor);
        Box(root, new Vector3(s / 2f, y - 0.3f, s / 2f), new Vector3(s + 40, 0.5f, s + 40), new Color("#4a3b2c"), 1f).MaterialOverride = SurfaceMat(new Color("#4a3b2c"), 1f);
        foreach (var b in _state.Buildings.Where(b => b.Stage == Stage.Done))
        {
            Outline(root, b.X, b.Z, b.W, b.D, y + 0.03f, 0.12f, new Color(b.Basement ? Brand.Concrete : new Color("#c9b79a"), 0.8f));
            if (b.Basement) Box(root, new Vector3(b.X + b.W / 2f, y - 0.04f, b.Z + b.D / 2f), new Vector3(b.W + 0.4f, 0.08f, b.D + 0.4f), Brand.Concrete, 0.95f);
        }
    }

    void DoneBuilding(Node3D root, Building b)
    {
        var col = WallColor(b);
        var open = Open(b);
        var under = Underground;
        if (!under) Box(root, new Vector3(b.X + b.W / 2f, Slab / 2, b.Z + b.D / 2f), new Vector3(b.W + 0.2f, Slab, b.D + 0.2f), Brand.Concrete, 0.95f).MaterialOverride = SurfaceMat(Brand.Concrete, 0.95f);
        var top = open ? _viewFloor : b.Floors - 1;
        var levels = under ? new[] { GameState.BasementFloor } : Enumerable.Range(0, top + 1).ToArray();
        foreach (var f in levels)
        {
            var cut = open && f == _viewFloor;
            var h = cut && !_fullWalls ? CutH : FloorH;
            if (cut && _fullWalls) Box(root, new Vector3(b.X + b.W / 2f, Base(f) + FloorH - 0.06f, b.Z + b.D / 2f), new Vector3(b.W, 0.12f, b.D), new Color("#dfe3e6"), 0.95f);
            if (f != 0) Box(root, new Vector3(b.X + b.W / 2f, Base(f) - 0.1f, b.Z + b.D / 2f), new Vector3(b.W, 0.2f, b.D), Brand.Concrete, 0.95f);
            if (cut) Box(root, new Vector3(b.X + b.W / 2f, Base(f) + 0.005f, b.Z + b.D / 2f), new Vector3(b.W - 0.1f, 0.01f, b.D - 0.1f), new Color("#c4c9cd"), 0.95f);
            var edges = GameState.Perimeter(b.X, b.Z, b.W, b.D).ToHashSet();
            if (cut)
                foreach (var r in _state.Rooms.Where(r => r.BuildingId == b.Id && r.Floor == f && r.Stage == Stage.Done))
                    foreach (var e in GameState.Perimeter(r.X, r.Z, r.W, r.D)) edges.Add(e);
            var doors = _state.Doors.Where(d => d.Floor == f).ToList();
            var doorEdges = doors.SelectMany(d => d.Edges()).ToHashSet();
            Walls(root, edges.Where(e => !doorEdges.Contains(e)), Base(f), h, f == GameState.BasementFloor ? BasementWall : col, cut ? new Color("#e4e8eb") : col, b);
            foreach (var d in doors.Where(d => d.Edges().Any(edges.Contains))) DoorModel(root, d, Base(f), h, _doorLeaves);
            if (!cut) FacadeDetail(root, b, f);
            if (cut)
                foreach (var r in _state.Rooms.Where(r => r.BuildingId == b.Id && r.Floor == f))
                {
                    RoomFloor(root, r, Base(f));
                    if (r.Stage == Stage.Done) RoomLights(root, r, Base(f));
                    if (r.Type == RoomType.DataHall) HallSigns(root, r, Base(f), h);
                }
        }
        if (!open)
        {
            var roofY = Base(b.Floors - 1) + FloorH;
            Box(root, new Vector3(b.X + b.W / 2f, roofY + 0.12f, b.Z + b.D / 2f), new Vector3(b.W + 0.3f, 0.24f, b.D + 0.3f), new Color("#6f7a83")).MaterialOverride = RoofMat;
            Box(root, new Vector3(b.X + b.W / 2f, roofY + 0.27f, b.Z + b.D / 2f), new Vector3(b.W - 0.6f, 0.08f, b.D - 0.6f), new Color("#8a949c")).MaterialOverride = RoofTopMat;
            if (b.Kind == BuildingKind.Datacenter) Box(root, new Vector3(b.X + b.W / 2f, roofY - 0.25f, b.Z + b.D + 0.16f), new Vector3(b.W, 0.25f, 0.06f), Brand.Power);
            if (_state.Logo.OnBuildings) BuildingSign(root, b, roofY);
            // a few rooftop units so the roof is not empty, unless the player put plant there
            var roofPlant = _state.Objects.Any(o => o.Floor == GameState.RoofFloor && b.Contains(o.X, o.Z));
            for (var i = 0; i < (roofPlant || b.Raising ? 0 : Math.Min(4, b.W / 6)); i++)
                if (Assets.Place(root, "made/roof_unit.glb", new Vector3(b.X + 3 + i * 5, roofY + 0.25f, b.Z + 2.5f), 1, 0, Models.MadeMat) == null)
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
                    var wall = dir == 0 ? Box(root, new Vector3(g.Key, y + h / 2, start + len / 2f), new Vector3(th, h, len + (isOuter ? 0.25f : 0)), c)
                        : Box(root, new Vector3(start + len / 2f, y + h / 2, g.Key), new Vector3(len + (isOuter ? 0.25f : 0), h, th), c);
                    if (isOuter && WallMat(c) is { } wm) wall.MaterialOverride = wm;
                    if (i < along.Count) { start = along[i]; prev = start; }
                }
            }
        }
    }

    /// <summary>A door leaf that swings (hinge, +1/-1 for its direction) or a roll-up curtain, and how far it is open (0..1).</summary>
    sealed class DoorLeaf
    {
        public Vector3 At; public int Floor; public bool Roll; public float Height;
        public readonly List<(Node3D hinge, float sign)> Hinges = new();
        public MeshInstance3D Curtain; public float Open;
    }
    readonly List<DoorLeaf> _doorLeaves = new();

    /// <summary>Draw a door, closed. With <paramref name="leaves"/> its moving parts are registered so it opens for people passing.</summary>
    public static void DoorModel(Node3D root, Door d, float y, float wallH) => DoorModel(root, d, y, wallH, null);

    static void DoorModel(Node3D root, Door d, float y, float wallH, List<DoorLeaf> leaves)
    {
        var reg = leaves == null ? null : new DoorLeaf { Floor = d.Floor, Roll = d.Type == DoorType.RollUp };
        var def = Catalog.Doors[d.Type];
        var w = def.Width;
        var center = d.Dir == 0 ? new Vector3(d.X, y, d.Z + w / 2f) : new Vector3(d.X + w / 2f, y, d.Z);
        var n = new Node3D { Position = center, RotationDegrees = new Vector3(0, d.Dir == 0 ? 90 : 0, 0) };
        root.AddChild(n);
        var h = Math.Min(2.3f, wallH);
        if (reg != null) { reg.At = center; reg.Height = h; leaves.Add(reg); }
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
            // closed: the curtain fills the opening; it rolls up into the drum at the top when something passes
            var curtain = Box(n, new Vector3(0, h / 2, 0), new Vector3(w - 0.1f, 1, 0.08f), leaf, 0.5f, 0.4f);
            curtain.Scale = new Vector3(1, h, 1);
            Box(n, new Vector3(0, h + 0.12f, 0.04f), new Vector3(w, 0.24f, 0.24f), new Color("#7d878e"), 0.5f, 0.5f);
            if (reg != null) reg.Curtain = curtain;
        }
        else
        {
            var leafCount = w;
            for (var i = 0; i < leafCount; i++)
            {
                // closed by default; the leaves swing open for someone passing
                var hinge = new Node3D { Position = new Vector3(-w / 2f + i * 1f + (i % 2 == 0 ? 0.05f : 0.95f), 0, 0) };
                n.AddChild(hinge);
                reg?.Hinges.Add((hinge, i % 2 == 0 ? -1f : 1f));
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
        // finished offices get the floor of their interior style (oak, dark carpet, walnut)
        if (r.Stage == Stage.Done && GameState.Furnishable(r.Type)) col = StyleColors(_state.StyleOf(r)).floor;
        if (r.Stage != Stage.Done) col = new Color(col, 0.55f);
        var floor = Box(root, new Vector3(r.X + r.W / 2f, y + 0.015f, r.Z + r.D / 2f), new Vector3(r.W - 0.12f, 0.01f, r.D - 0.12f), col, 0.95f);
        // a finished data hall gets a light raised floor of 600 mm tiles
        if (r.Type == RoomType.DataHall && r.Stage == Stage.Done) floor.MaterialOverride = HallLook.Floor;
        if (r.Type == RoomType.DataHall) { HallFloorMark(root, r, y); return; }
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
                Text = $"{Catalog.GridLevels[_state.GridRequest].Mva:0} MVA · {(_state.InGridQueue ? Loc.T("grid_queue_place", _state.GridQueuePlace) : Loc.T("grid_days_left", Math.Ceiling(_state.GridDaysLeft)))}",
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
        foreach (var b in _state.Buildings.Where(b => b.Stage != Stage.Done))
        {
            if (_state.IsPlanned(b)) PlanBuilding(_sites, b);
            else SiteBuilding(_sites, b);
        }
        if (!Underground)
            foreach (var b in _state.Buildings.Where(b => b.Stage == Stage.Done && b.Raising)) RaiseSite(_sites, b);
        // planned rooms: on the floor in view (inside a planned building, or an open finished one)
        foreach (var r in _state.Rooms.Where(r => _state.IsPlanned(r)))
        {
            var b = _state.BuildingOf(r);
            if (b == null || r.Floor != _viewFloor && !(r.Floor == 0 && _state.IsPlanned(b))) continue;
            if (b.Stage == Stage.Done && !Open(b)) continue;
            PlanRoom(_sites, r);
        }
        // rooms being built inside finished buildings
        foreach (var r in _state.Rooms.Where(r => r.Stage != Stage.Done && !_state.IsPlanned(r)))
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

    /// <summary>A storey going up on a finished building: new walls rising on the roof, scaffolding, a crane and the progress.</summary>
    void RaiseSite(Node3D root, Building b)
    {
        var p = (float)GameState.RaiseProgress(b);
        var col = WallColor(b);
        var y = Base(b.Floors) + 0.3f;
        var h = Math.Max(0.25f, FloorH * p);
        var edges = GameState.Perimeter(b.X, b.Z, b.W, b.D).ToList();
        Walls(root, edges, y, h, col, col, b);
        var top = y + h;
        var scaf = new List<Transform3D>();
        for (var x = b.X; x <= b.X + b.W; x += 2)
            foreach (var z in new[] { b.Z - 0.7f, b.Z + b.D + 0.7f })
                scaf.Add(new Transform3D(Basis.Identity.Scaled(new Vector3(1, top + 1, 1)), new Vector3(x, (top + 1) / 2, z)));
        for (var z = b.Z; z <= b.Z + b.D; z += 2)
            foreach (var x in new[] { b.X - 0.7f, b.X + b.W + 0.7f })
                scaf.Add(new Transform3D(Basis.Identity.Scaled(new Vector3(1, top + 1, 1)), new Vector3(x, (top + 1) / 2, z)));
        Multi(root, new BoxMesh { Size = new Vector3(0.06f, 1, 0.06f) }, scaf, new Color("#9aa4ab"), false);
        // walkway boards round the top of the scaffold
        foreach (var (c, size) in new[]
        {
            (new Vector3(b.X + b.W / 2f, top, b.Z - 0.7f), new Vector3(b.W + 1.4f, 0.05f, 0.6f)),
            (new Vector3(b.X + b.W / 2f, top, b.Z + b.D + 0.7f), new Vector3(b.W + 1.4f, 0.05f, 0.6f)),
            (new Vector3(b.X - 0.7f, top, b.Z + b.D / 2f), new Vector3(0.6f, 0.05f, b.D + 1.4f)),
            (new Vector3(b.X + b.W + 0.7f, top, b.Z + b.D / 2f), new Vector3(0.6f, 0.05f, b.D + 1.4f)),
        })
            Box(root, c, size, new Color("#b48a52"));
        foreach (var (cx, cz, reach) in CranesFor(b, _state.PlotSize))
        {
            var crane = Crane(Math.Max(12, (b.Floors + 1) * FloorH + 8), reach);
            crane.Position = new Vector3(cx, 0, cz);
            crane.RotationDegrees = new Vector3(0, (cx * 37 + cz * 11) % 360, 0);
            root.AddChild(crane);
            _cranes.Add(crane);
        }
        root.AddChild(new Label3D
        {
            Text = $"{Loc.T("site_raising")} {Mathf.RoundToInt(p * 100)}%",
            Font = _font, FontSize = 56, PixelSize = 0.02f, OutlineSize = 12, OutlineModulate = new Color("#111D2B"),
            Modulate = Brand.Amber, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true,
            Position = new Vector3(b.X + b.W / 2f, top + 3f, b.Z + b.D / 2f),
        });
    }

    void SiteBuilding(Node3D root, Building b)
    {
        var p = (float)GameState.StageProgress(b);
        var col = WallColor(b);
        // site boundary tape and dug-out ground
        Box(root, new Vector3(b.X + b.W / 2f, 0.01f, b.Z + b.D / 2f), new Vector3(b.W + 1.2f, 0.02f, b.D + 1.2f), new Color("#8b6f4e"), 1f).MaterialOverride = SurfaceMat(new Color("#8b6f4e"), 1f);
        foreach (var (x, z) in new[] { (b.X - 0.6f, b.Z - 0.6f), (b.X + b.W + 0.6f, b.Z - 0.6f), (b.X - 0.6f, b.Z + b.D + 0.6f), (b.X + b.W + 0.6f, b.Z + b.D + 0.6f) })
            Cyl(root, new Vector3(x, 0.5f, z), 0.12f, 1f, Brand.Amber, 0.04f);
        if (b.Stage == Stage.Foundation)
        {
            var rows = (int)Math.Ceiling(b.D * p);
            if (rows > 0) Box(root, new Vector3(b.X + b.W / 2f, Slab / 2, b.Z + rows / 2f), new Vector3(b.W, Slab, rows), Brand.Concrete, 0.95f).MaterialOverride = SurfaceMat(Brand.Concrete, 0.95f);
            var ex = Excavator();
            ex.Position = new Vector3(b.X + b.W * 0.3f, 0, b.Z + Math.Min(b.D - 2, rows + 2));
            ex.RotationDegrees = new Vector3(0, 200, 0);
            root.AddChild(ex);
        }
        else
        {
            Box(root, new Vector3(b.X + b.W / 2f, Slab / 2, b.Z + b.D / 2f), new Vector3(b.W + 0.2f, Slab, b.D + 0.2f), Brand.Concrete, 0.95f).MaterialOverride = SurfaceMat(Brand.Concrete, 0.95f);
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
        return o.Floor != 0 || b != null ? Base(o.Floor) : 0f;
    }

    bool ObjectVisible(PlacedObject o)
    {
        var b = _state.BuildingAt(o.X, o.Z);
        if (b == null) return !Underground;
        if (b.Stage != Stage.Done) return _state.IsPlanned(b) && o.Floor == System.Math.Min(_viewFloor, b.Floors - 1);
        if (o.Floor == GameState.RoofFloor) return !Open(b);
        if (Catalog.IsShaft(o.Kind)) return Open(b);
        return Open(b) && o.Floor == _viewFloor;
    }

    void RebuildObjects()
    {
        foreach (var c in _objects.GetChildren()) c.QueueFree();
        _fans.Clear(); _gates.Clear(); _markers.Clear();
        if (_state == null) { _racks.Clear(); return; }
        if (!Underground) GridStationModel();
        HallDecor();
        OfficeDecor();
        var racks = new List<(PlacedObject, float)>();
        var trophyDesk = _state.Statuettes.Any() ? _state.TrophyDesk?.Id ?? -1 : -1;
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
            Node3D n;
            if (Catalog.IsShaft(o.Kind) && _state.BuildingAt(o.X, o.Z) is { } sb)
            {
                // stairs and lifts serve every floor: draw them on the floor in view
                var f = _viewFloor == GameState.BasementFloor && sb.Basement ? _viewFloor : Math.Clamp(_viewFloor, 0, sb.Floors - 1);
                n = Models.Shaft(o.Kind, f == sb.Floors - 1 && sb.Levels > 1);
                pos.Y = Base(f);
            }
            else n = Models.Object(o.Kind);
            n.Position = pos;
            n.Basis = basis;
            foreach (var f in n.GetChildren().OfType<Node3D>().Where(c => c.Name.ToString().StartsWith("Fan"))) _fans.Add(f);
            if (Catalog.IsVehicleStop(o.Kind)) _gates[o.Id] = (n, o.Kind, o.Def.W);
            if (o.Id == trophyDesk) AddStatuettes(n);
            if (_state.IsPlanned(o)) { Tint(n, PlanColor); _objects.AddChild(n); continue; }
            if (o.Def.Chain && o.Status <= ObjStatus.Delivered) Tint(n, Brand.Ink);
            else if (o.Def.Chain && o.Status != ObjStatus.Live) AddMarker(o, pos);
            _objects.AddChild(n);
        }
        _racks.Update(racks);
    }

    /// <summary>Step 11: the award statuettes in a row on the left of the CEO's desk (at most seven, the newest).</summary>
    void AddStatuettes(Node3D desk)
    {
        var list = _state.Statuettes.TakeLast(7).ToList();
        for (var i = 0; i < list.Count; i++)
        {
            var st = Models.Statuette(list[i].Place);
            st.Position = new Vector3(-0.86f + i * 0.15f, Models.DeskTop, -0.33f);
            st.Scale = Vector3.One * 1.4f;
            st.RotationDegrees = new Vector3(0, 180, 0);
            desk.AddChild(st);
        }
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
        if (b == null || b.Stage != Stage.Done) return a.Floor == 0 && !Underground;
        return Open(b) && a.Floor == _viewFloor;
    }

    int _animTick;
    Vector3 _camPos;

    void SyncAgents(double delta)
    {
        if (_sim == null) return;
        _animTick++;
        _camPos = GetViewport()?.GetCamera3D()?.GlobalPosition ?? Vector3.Zero;
        var alive = new HashSet<int>();
        var t = (float)Time.GetTicksMsec() / 1000f;
        foreach (var a in _sim.Agents)
        {
            alive.Add(a.Id);
            if (!_agentNodes.TryGetValue(a.Id, out var n))
            {
                n = a.Role == Role.Truck ? Livery(Models.Truck(a.Load), a.Load) : Person(a.Role, a.Role == Role.Staff ? a.EmpId : a.Id, a.Staff);
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
                var cargo = n.GetNodeOrNull<Node3D>("Trailer/Cargo");
                if (cargo != null) cargo.Visible = a.Carry > 0;
                FollowTrailer(n, a, delta);
                continue;
            }
            var fig = (PersonFigure)n;
            n.GetNode<Node3D>("Crate").Visible = a.Role == Role.Builder && a.Loaded && a.Carry > 0 && a.Phase == 1;
            // movers of a customer migration push a trolley of servers: in to the racks, or out to the truck
            var cart = n.GetNode<Node3D>("Cart");
            cart.Visible = a.Role == Role.Visitor && a.Carry > 0;
            n.GetNode<Node3D>("Rack").Visible = a.Role == Role.Fitter && a.CarryObj != 0 && a.Phase == 2;
            var bike = n.GetNodeOrNull<Node3D>("Bike");
            if (a.Ride && bike == null)
            {
                bike = new Node3D { Name = "Bike" };
                Assets.Place(bike, "made/" + BikeModels[Math.Abs(a.EmpId) % BikeModels.Length] + ".glb", Vector3.Zero, 1, 0, Models.MadeMat);
                n.AddChild(bike);
            }
            if (bike != null) bike.Visible = a.Ride;
            var reel = AnimateReels(n, a, t);
            var pose = a.Ride ? PersonPose.Ride
                : a.Working && !reel ? PersonPose.Work
                : n.GetNode<Node3D>("Crate").Visible ? PersonPose.Carry
                : cart.Visible ? PersonPose.Push
                : reel && !a.Working ? PersonPose.Reel
                : a.Moving ? PersonPose.Walk : PersonPose.Idle;
            if (reel && a.Working) pose = PersonPose.Work;
            // only figures on screen are animated; far away ones every other frame
            if (n.Visible && (pose != PersonPose.Idle || _animTick % 3 == a.Id % 3))
            {
                var far = _camPos.DistanceSquaredTo(n.Position) > 90 * 90;
                if (!far || (_animTick + a.Id) % 2 == 0) fig.Animate(far ? delta * 2 : delta, pose, a.Id);
            }
        }
        foreach (var id in _agentNodes.Keys.Where(id => !alive.Contains(id)).ToList())
        {
            _agentNodes[id].QueueFree(); _agentNodes.Remove(id); _trails.Remove(id);
        }
    }

    /// <summary>Where each truck has been (newest last), so its trailer can follow the same line through bends.</summary>
    readonly Dictionary<int, List<Vector2>> _trails = new();

    /// <summary>
    /// The semi-trailer pivots at the kingpin and points at the spot on the truck's own track one axle-group length back,
    /// so it swings wide of nothing and cuts the bend a little, like a real one. Backing up to a dock it straightens out.
    /// </summary>
    void FollowTrailer(Node3D n, Agent a, double delta)
    {
        var tr = n.GetNodeOrNull<Node3D>("Trailer");
        if (tr == null) return;
        if (!_trails.TryGetValue(a.Id, out var trail)) _trails[a.Id] = trail = new List<Vector2>();
        var kp = new Vector2(n.Position.X, n.Position.Z);
        var reversing = a.ReverseFrom >= 0 && a.WayI >= a.ReverseFrom;
        var rel = tr.Rotation.Y;
        if (reversing || !a.Moving && trail.Count < 2)
        {
            trail.Clear();
            tr.Rotation = new Vector3(0, Mathf.LerpAngle(rel, 0, Mathf.Min(1, (float)delta * 2)), 0);
            return;
        }
        if (trail.Count == 0 || trail[^1].DistanceTo(kp) > 0.3f) trail.Add(kp);
        if (trail.Count > 80) trail.RemoveRange(0, trail.Count - 80);
        // walk back along the track to the point one axle-group length behind the kingpin
        const float reach = Models.TrailerLength - 1.3f;
        float left = reach; var from = kp; Vector2? hit = null;
        for (var i = trail.Count - 1; i >= 0; i--)
        {
            var d = from.DistanceTo(trail[i]);
            if (d >= left) { hit = from + (trail[i] - from) * (left / d); break; }
            left -= d; from = trail[i];
        }
        var want = 0f;
        if (hit is Vector2 h)
        {
            var dir = kp - h;
            var yaw = Mathf.Atan2(dir.X, dir.Y);
            want = Mathf.Wrap(yaw - n.Rotation.Y, -Mathf.Pi, Mathf.Pi);
            // turning round on the spot (start of the way back): start straight again
            if (Mathf.Abs(want) > Mathf.DegToRad(100)) { trail.Clear(); trail.Add(kp); want = 0; }
            want = Mathf.Clamp(want, -1.2f, 1.2f);
        }
        tr.Rotation = new Vector3(0, Mathf.LerpAngle(rel, want, Mathf.Min(1, (float)delta * 8)), 0);
    }

    /// <summary>
    /// Engineers walk with a reel of amber power cable or orange fiber in both hands; at the job the reel
    /// goes on the floor beside them and a cable run leads to the rack while the drum slowly unrolls.
    /// Electricians and network engineers on a repair carry one too.
    /// </summary>
    static bool AnimateReels(Node3D n, Agent a, float t)
    {
        var p = n.GetNodeOrNull<Node3D>("ReelP");
        var f = n.GetNodeOrNull<Node3D>("ReelF");
        if (p == null && f == null) return false;
        var kind = a.Role == Role.Engineer ? a.Cable
            : a.Target == 0 ? CableKind.None
            : a.Staff == StaffRole.NetworkEng ? CableKind.Fiber : CableKind.Power;
        if (a.Role == Role.Engineer && a.Phase == 0) kind = CableKind.None;
        var tool = (n as PersonFigure)?.Tool;
        if (tool != null) tool.Visible = kind == CableKind.None;
        foreach (var (reel, k) in new[] { (p, CableKind.Power), (f, CableKind.Fiber) })
        {
            if (reel == null) continue;
            reel.Visible = kind == k;
            if (!reel.Visible) continue;
            var down = a.Working;
            reel.Position = down ? new Vector3(0.42f, 0.16f, 0.22f) : new Vector3(0, 0.72f, 0.38f);
            reel.GetNode<Node3D>("Lead").Visible = down;
            var drum = reel.GetNode<Node3D>("Drum");
            drum.RotationDegrees = new Vector3(down ? t * 40 % 360 : a.Moving ? t * 90 % 360 : drum.RotationDegrees.X, 0, 0);
        }
        return kind != CableKind.None;
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

    /// <summary>Doors open when a person, cart or vehicle comes close on the same floor and close again behind them.</summary>
    void AnimateDoors(double delta)
    {
        if (_doorLeaves.Count == 0 || _sim == null) return;
        var step = (float)Math.Min(delta, 0.1) * 3.5f;
        foreach (var dl in _doorLeaves)
        {
            var near = false;
            var reach = dl.Roll ? 4.5f : 1.7f;
            foreach (var a in _sim.Agents)
            {
                if (a.Floor != dl.Floor) continue;
                float dx = (float)a.X - dl.At.X, dz = (float)a.Z - dl.At.Z;
                if (dx * dx + dz * dz < reach * reach) { near = true; break; }
            }
            var target = near ? 1f : 0f;
            if (Mathf.IsEqualApprox(dl.Open, target)) continue;
            dl.Open = Mathf.MoveToward(dl.Open, target, step * (dl.Roll ? 0.5f : 1f));
            var e = dl.Open * dl.Open * (3 - 2 * dl.Open);
            foreach (var (hinge, sign) in dl.Hinges) hinge.RotationDegrees = new Vector3(0, sign * 88 * e, 0);
            if (dl.Curtain != null)
            {
                var hh = Mathf.Max(0.15f, dl.Height * (1 - 0.92f * e));
                dl.Curtain.Scale = new Vector3(1, hh, 1);
                dl.Curtain.Position = new Vector3(0, dl.Height - hh / 2, 0);
            }
        }
    }

    void Animate(double delta)
    {
        AnimateDoors(delta);
        if (MovingParts)
        foreach (var c in _cranes)
        {
            var jib = c.GetNodeOrNull<Node3D>("Jib");
            if (jib != null) jib.RotateY((float)delta * 0.15f);
        }
        if (MovingParts)
        {
            foreach (var m in _markers) m.RotateY((float)delta * 2f);
            // spin round their own axis (a tilted wind turbine hub too), turbines slower than fans
            foreach (var f in _fans) if (IsInstanceValid(f)) f.RotateObjectLocal(Vector3.Up, (float)delta * (f.Name == "FanRotor" ? 1.6f : 9f));
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
        var fiberTray = new List<Transform3D>(); var powerLadder = new List<Transform3D>(); var tapOff = new List<Transform3D>(); var hangers = new List<Transform3D>();
        var trayLip = new List<Transform3D>(); var fiberBundle = new List<Transform3D>(); var waterfall = new List<Transform3D>();
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
            // overhead: an orange fiber tray over the front of every rack row and a grey power cable ladder over the back,
            // on drop rods from the ceiling. Trays are only laid where racks stand (placed or live), so they grow with the hall.
            // Each tray is a U-channel with a bundle of fiber in it and a "waterfall" spout that drops the fiber into the rack.
            foreach (var o in racks)
            {
                var basis = o.Rot % 2 == 0 ? Basis.Identity : Basis.Identity.Rotated(Vector3.Up, Mathf.Pi / 2);
                var (fx, fz) = FrontOffset(o.Rot);
                var (sx, sz) = (fx, fz); // across the tray (the tray runs along the row)
                var c = new Vector3(o.X + 0.5f, y, o.Z + 0.5f);
                var tc = c + new Vector3(fx * 0.2f, 2.42f, fz * 0.2f);
                fiberTray.Add(new Transform3D(basis, tc));
                trayLip.Add(new Transform3D(basis, tc + new Vector3(sx * 0.17f, 0.06f, sz * 0.17f)));
                trayLip.Add(new Transform3D(basis, tc + new Vector3(sx * -0.17f, 0.06f, sz * -0.17f)));
                fiberBundle.Add(new Transform3D(basis, tc + new Vector3(0, 0.05f, 0)));
                if (o.Status == ObjStatus.Live)
                    waterfall.Add(new Transform3D(Basis.Identity, c + new Vector3(fx * 0.32f, 2.27f, fz * 0.32f)));
                if (!r.Busbar) powerLadder.Add(new Transform3D(basis, c + new Vector3(fx * -0.3f, 2.52f, fz * -0.3f)));
                if ((o.X + o.Z) % 2 == 0) hangers.Add(new Transform3D(basis, c + new Vector3(fx * 0.2f, 2.9f, fz * 0.2f)));
                if (r.Busbar) tapOff.Add(new Transform3D(basis, c + new Vector3(fx * -0.22f, 2.33f, fz * -0.22f)));
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
        if (tiles.Count > 0) MultiMat(_objects, new QuadMesh { Size = new Vector2(0.6f, 0.6f), Orientation = PlaneMesh.OrientationEnum.Y }, tiles, HallLook.Grate);
        Multi(_objects, new BoxMesh { Size = new Vector3(1f, 0.03f, 1f) }, glass, new Color(Brand.Cooling, 0.28f), false);
        Multi(_objects, new BoxMesh { Size = new Vector3(0.96f, 2.1f, 0.04f) }, frames, new Color(Brand.Cooling, 0.3f), false);
        Multi(_objects, new BoxMesh { Size = new Vector3(1.0f, 0.1f, 0.1f) }, busA, Brand.Power);
        Multi(_objects, new BoxMesh { Size = new Vector3(1.0f, 0.1f, 0.1f) }, busB, new Color("#d8dde1"));
        if (fiberTray.Count > 0)
        {
            var trayMat = new StandardMaterial3D { AlbedoColor = Brand.Fiber, Roughness = 0.45f, EmissionEnabled = true, Emission = Brand.Fiber, EmissionEnergyMultiplier = 0.35f };
            MultiMat(_objects, new BoxMesh { Size = new Vector3(1.0f, 0.03f, 0.36f) }, fiberTray, trayMat);
            MultiMat(_objects, new BoxMesh { Size = new Vector3(1.0f, 0.12f, 0.025f) }, trayLip, trayMat);
            Multi(_objects, new BoxMesh { Size = new Vector3(1.0f, 0.06f, 0.22f) }, fiberBundle, new Color("#f2c230"));
            Multi(_objects, new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0.05f, Height = 0.3f, RadialSegments = 8 }, waterfall, Brand.Fiber);
        }
        Multi(_objects, new BoxMesh { Size = new Vector3(1.0f, 0.05f, 0.34f) }, powerLadder, new Color("#9aa4ab"));
        Multi(_objects, new BoxMesh { Size = new Vector3(0.025f, 0.9f, 0.025f) }, hangers, new Color("#8a949c"));
        Multi(_objects, new BoxMesh { Size = new Vector3(0.24f, 0.14f, 0.18f) }, tapOff, new Color("#c8322b"));
    }

    /// <summary>Cell offset in front of an object (the cold aisle side of a rack).</summary>
    public static (int dx, int dz) FrontOffset(int rot) => (rot % 4) switch { 1 => (-1, 0), 2 => (0, -1), 3 => (1, 0), _ => (0, 1) };

    /// <summary>Parking bay lines as stretched unit planes, 10 cm wide.</summary>
    /// <summary>
    /// Dashed centre lines (3 m line, 3 m gap) on straight asphalt roads of <see cref="Nav.TwoLaneWidth"/> to 12 m
    /// wide: a row or column of asphalt that wide, whose cells all continue at least 8 m along the road. Junctions
    /// and squares are wider than that, so the line stops there.
    /// </summary>
    IEnumerable<Transform3D> RoadCentreLines(int n, float y)
    {
        const int maxWidth = 12, minRun = 8;
        bool Road(int x, int z) => _state.InPlot(x, z) && _state.SurfaceAt(x, z) == Surface.Asphalt && !_curveCells.Contains(z * n + x);
        // length of the asphalt run through each cell, along x and along z
        var runX = new int[n * n]; var runZ = new int[n * n];
        for (var z = 0; z < n; z++)
            for (var x = 0; x < n;)
            {
                if (!Road(x, z)) { x++; continue; }
                var e = x; while (e < n && Road(e, z)) e++;
                for (var i = x; i < e; i++) runX[z * n + i] = e - x;
                x = e;
            }
        for (var x = 0; x < n; x++)
            for (var z = 0; z < n;)
            {
                if (!Road(x, z)) { z++; continue; }
                var e = z; while (e < n && Road(x, e)) e++;
                for (var i = z; i < e; i++) runZ[i * n + x] = e - z;
                z = e;
            }
        var dash = Basis.FromScale(new Vector3(0.14f, 1, 1));
        var dashX = Basis.FromScale(new Vector3(1, 1, 0.14f));
        // roads running along z: a row run of road width, every cell of it long along z
        for (var z = 0; z < n; z++)
            for (var x = 0; x < n;)
            {
                var w = runX[z * n + x];
                if (w == 0) { x++; continue; }
                var straight = w >= Nav.TwoLaneWidth && w <= maxWidth;
                for (var i = x; i < x + w && straight; i++) straight = runZ[z * n + i] >= Math.Max(minRun, w + 2);
                if (straight && z % 6 < 3) yield return new Transform3D(dash, new Vector3(x + w / 2f, y, z + 0.5f));
                x += w;
            }
        // roads running along x
        for (var x = 0; x < n; x++)
            for (var z = 0; z < n;)
            {
                var w = runZ[z * n + x];
                if (w == 0) { z++; continue; }
                var straight = w >= Nav.TwoLaneWidth && w <= maxWidth;
                for (var i = z; i < z + w && straight; i++) straight = runX[i * n + x] >= Math.Max(minRun, w + 2);
                if (straight && x % 6 < 3) yield return new Transform3D(dashX, new Vector3(x + 0.5f, y, z + w / 2f));
                z += w;
            }
    }

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
            _ghost.AddChild(n);
            // a clear arrow on the floor pointing out of the front
            var (fw, fd) = (o.Def.W, o.Def.D);
            var arrow = new MeshInstance3D { Mesh = FrontArrow, MaterialOverride = GhostFlat(ok ? Brand.Amber : Brand.Critical, 0.95f), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            arrow.Position = new Vector3(0, 0.05f, fd / 2f + 0.08f);
            arrow.Scale = Vector3.One * Math.Clamp(fw * 0.9f, 0.8f, 2.2f);
            n.AddChild(arrow);
            if (o.Kind == ObjKind.Rack)
            {
                // racks are worked on from both sides: show the cold aisle in front and the hot aisle behind
                var front = new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(0.96f, 0.96f) }, MaterialOverride = GhostFlat(Brand.Cooling, 0.5f), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Position = new Vector3(0, 0.03f, 1) };
                var back = new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(0.96f, 0.96f) }, MaterialOverride = GhostFlat(Brand.Fiber, 0.5f), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Position = new Vector3(0, 0.03f, -1) };
                n.AddChild(front); n.AddChild(back);
            }
        }
        if (text != null)
            _ghost.AddChild(new Label3D
            {
                Text = text, Font = _font, FontSize = 64, PixelSize = 0.014f, OutlineSize = 14, OutlineModulate = new Color("#111D2B"),
                Modulate = Brand.Ink, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true, Position = last + new Vector3(0, 3f, 0),
            });
    }

    static ArrayMesh _frontArrow;
    /// <summary>Flat arrow on the floor, pointing along +z from the origin (shaft and a wide head).</summary>
    static ArrayMesh FrontArrow
    {
        get
        {
            if (_frontArrow != null) return _frontArrow;
            var st = new SurfaceTool();
            st.Begin(Mesh.PrimitiveType.Triangles);
            st.SetNormal(Vector3.Up);
            void Tri(Vector2 a, Vector2 b, Vector2 c)
            {
                foreach (var p in new[] { a, c, b }) st.AddVertex(new Vector3(p.X, 0, p.Y));
            }
            Vector2 V(float x, float z) => new(x, z);
            // shaft
            Tri(V(-0.08f, 0), V(0.08f, 0), V(0.08f, 0.3f)); Tri(V(-0.08f, 0), V(0.08f, 0.3f), V(-0.08f, 0.3f));
            // head
            Tri(V(-0.26f, 0.3f), V(0.26f, 0.3f), V(0, 0.62f));
            _frontArrow = st.Commit();
            return _frontArrow;
        }
    }

    static StandardMaterial3D GhostFlat(Color c, float alpha) => new()
    {
        AlbedoColor = new Color(c, alpha), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = alpha < 1 ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled, NoDepthTest = false,
    };

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
