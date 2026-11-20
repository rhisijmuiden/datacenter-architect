using System;
using DatacenterArchitect.Core;
using DatacenterArchitect.Sim;
using Godot;

namespace DatacenterArchitect.World;

/// <summary>
/// Step 10 looks: day and night, seasons and weather. The sun follows the clock and the time of year,
/// at night the moon takes over and windows, lamp posts and truck lights glow. Rain and snow fall around
/// the camera, storms flash, fog rolls in, the grass and trees change colour per season and snow settles
/// on the ground and on roofs. Everything is a handful of shared materials and two particle systems,
/// so it costs the same in a huge campus as in a small one.
/// </summary>
public partial class WorldView
{
    /// <summary>Options: day/night cycle, weather and seasonal colours. Off = a sunny summer afternoon.</summary>
    public bool DayNight = true, WeatherFx = true, Seasons = true;
    /// <summary>The first-person walker is inside a building: light comes from the ceiling, not the sun.</summary>
    public bool Indoor;
    /// <summary>Where rain and snow fall (camera target or the walker) and how wide the shower is.</summary>
    public Vector3 WeatherCenter;
    public float WeatherRadius = 40;
    /// <summary>The sky as last drawn, for the top bar and sound.</summary>
    public WeatherNow Now { get; private set; }
    /// <summary>A lightning flash happened this frame (the game plays thunder a moment later).</summary>
    public bool Flashed { get; private set; }

    /// <summary>Screenshots: force a weather or snow cover, and jump straight to the target light for some frames.</summary>
    public WeatherKind? ForceWeather;
    public double? ForceSnow;
    public int SnapFrames;

    DirectionalLight3D _moon;
    GpuParticles3D _rain, _snow;
    StandardMaterial3D _grassMat, _plotGrassMat, _crownMat, _crown2Mat, _fieldMat;
    /// <summary>Shared materials that change with the time of day and the season.</summary>
    public static readonly StandardMaterial3D WindowMat = new() { AlbedoColor = new Color("#34536e"), Roughness = 0.2f, EmissionEnabled = true, Emission = new Color("#ffd98a"), EmissionEnergyMultiplier = 0 };
    public static readonly StandardMaterial3D RoofMat = new() { AlbedoColor = new Color("#6f7a83"), Roughness = 0.8f };
    public static readonly StandardMaterial3D RoofTopMat = new() { AlbedoColor = new Color("#8a949c"), Roughness = 0.8f };
    public static readonly StandardMaterial3D HeadlightMat = new() { AlbedoColor = new Color("#fff6d8"), EmissionEnabled = true, Emission = new Color("#fff6d8"), EmissionEnergyMultiplier = 0.4f };
    static readonly Color RoofColor = new("#6f7a83"), RoofTopColor = new("#8a949c"), SnowColor = new("#eef3f7");

    // the light we ease towards, so dawn, dusk and a passing shower blend smoothly
    float _cloud, _wet, _flakes, _fogK, _snowK, _flash;
    double _flashTimer = 6;
    readonly Random _skyRng = new(7);
    bool _skyReady;
    float _lampEnergy = -1;

    void SetupSky()
    {
        _moon = new DirectionalLight3D { ShadowEnabled = false, LightEnergy = 0, LightColor = new Color("#9db4ff"), RotationDegrees = new Vector3(-58, 150, 0) };
        AddChild(_moon);
        _rain = Precipitation(false);
        _snow = Precipitation(true);
        _skyReady = true;
    }

    GpuParticles3D Precipitation(bool snow)
    {
        var pm = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(40, 0.5f, 40),
            Direction = new Vector3(snow ? 0.15f : 0.08f, -1, 0),
            Spread = snow ? 18 : 3,
            InitialVelocityMin = snow ? 2.2f : 24, InitialVelocityMax = snow ? 3.4f : 28,
            Gravity = new Vector3(0, snow ? -0.3f : -9.8f, 0),
        };
        if (snow) { pm.TurbulenceEnabled = true; pm.TurbulenceNoiseStrength = 1.2f; pm.TurbulenceNoiseScale = 6; pm.TurbulenceInfluenceMin = 0.05f; pm.TurbulenceInfluenceMax = 0.12f; }
        var mat = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoColor = snow ? new Color(1, 1, 1, 0.95f) : new Color("#cfe3f2", 0.42f),
            BillboardMode = snow ? BaseMaterial3D.BillboardModeEnum.Particles : BaseMaterial3D.BillboardModeEnum.FixedY,
            BillboardKeepScale = true,
        };
        var mesh = new QuadMesh { Size = snow ? new Vector2(0.09f, 0.09f) : new Vector2(0.025f, 0.7f), Material = mat };
        var p = new GpuParticles3D
        {
            Amount = snow ? 5000 : 7000,
            Lifetime = snow ? 9 : 1.35f,
            Preprocess = snow ? 9 : 1.35f,
            ProcessMaterial = pm,
            DrawPass1 = mesh,
            Emitting = false,
            VisibilityAabb = new Aabb(new Vector3(-100, -60, -100), new Vector3(200, 80, 200)),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            AmountRatio = 0,
        };
        AddChild(p);
        return p;
    }

    /// <summary>Surroundings hand over their grass and tree materials so the seasons can recolour them.</summary>
    void SeasonMaterials(StandardMaterial3D grass, StandardMaterial3D plot, StandardMaterial3D crown, StandardMaterial3D crown2, StandardMaterial3D fields)
    {
        _grassMat = grass; _plotGrassMat = plot; _crownMat = crown; _crown2Mat = crown2; _fieldMat = fields;
    }

    static Color Mul(Color c, Color m) => new(c.R * m.R, c.G * m.G, c.B * m.B, c.A);

    /// <summary>Seasonal leaf colours: blossom in spring, deep green in summer, orange and gold in autumn, bare in winter.</summary>
    public static (Color leaf, Color leaf2, Color grassTint) SeasonColors(Season s) => s switch
    {
        Season.Spring => (new Color("#6fb04c"), new Color("#f0b4cb"), new Color(0.97f, 1.06f, 0.88f)),
        Season.Autumn => (new Color("#c8742c"), new Color("#ddb23a"), new Color(1.12f, 1.0f, 0.72f)),
        Season.Winter => (new Color("#8a7d6c"), new Color("#7d8a6e"), new Color(0.92f, 0.9f, 0.84f)),
        _ => (new Color("#5a9142"), new Color("#6fa94e"), new Color(1, 1, 1)),
    };

    Season _lastSeason = (Season)(-1);
    float _lastSnow = -1;

    /// <summary>Called every frame from Sync: eases the light, sky, weather and season colours to the game clock.</summary>
    void UpdateSky(double delta)
    {
        if (!_skyReady || _state == null) return;
        var dt = (float)Math.Min(delta, 0.1);
        var w = _state.Weather;
        if (ForceWeather is WeatherKind fw) w = w with { Kind = fw, Intensity = 0.85 };
        if (ForceSnow is double fs) w = w with { SnowCover = fs };
        if (!WeatherFx) w = w with { Kind = WeatherKind.Clear, Intensity = 0, SnowCover = 0 };
        if (!Seasons) w = w with { Season = Season.Summer, SnowCover = 0, Kind = w.Kind == WeatherKind.Snow ? WeatherKind.Cloudy : w.Kind };
        var date = _state.Date;
        var elev = (float)w.SunElevation;
        var day = (float)w.Daylight;
        if (!DayNight) { elev = 50; day = 1; date = new DateTime(date.Year, date.Month, date.Day, 14, 0, 0); }
        Now = w with { SunElevation = elev, Daylight = day };

        var k = (float)w.Intensity;
        var cloudT = w.Kind switch { WeatherKind.Cloudy => 0.55f, WeatherKind.Rain => 0.75f + 0.15f * k, WeatherKind.Storm => 1f, WeatherKind.Snow => 0.7f, WeatherKind.Fog => 0.6f, _ => 0f };
        var wetT = w.Kind switch { WeatherKind.Rain => 0.4f + 0.6f * k, WeatherKind.Storm => 1f, _ => 0f };
        var flakesT = w.Kind == WeatherKind.Snow ? 0.35f + 0.65f * k : 0f;
        var fogT = w.Kind switch { WeatherKind.Fog => 0.5f + 0.5f * k, WeatherKind.Rain => 0.18f, WeatherKind.Storm => 0.3f, WeatherKind.Snow => 0.3f, _ => 0f };
        // a whole weather change takes a few real seconds, a time-of-day change follows the clock
        var ease = SnapFrames > 0 ? 1 : Mathf.Min(1, dt * 0.6f);
        _cloud = Mathf.Lerp(_cloud, cloudT, ease);
        _wet = Mathf.Lerp(_wet, wetT, ease);
        _flakes = Mathf.Lerp(_flakes, flakesT, ease);
        _fogK = Mathf.Lerp(_fogK, fogT, ease);
        _snowK = Mathf.Lerp(_snowK, (float)w.SnowCover, SnapFrames > 0 ? 1 : Mathf.Min(1, dt * 0.25f));
        if (SnapFrames > 0) SnapFrames--;

        // --- sun and moon ---
        var hour = (float)date.TimeOfDay.TotalHours;
        var yaw = -38 - (hour - 13) * 12;
        var lightElev = Mathf.Max(elev, 10);
        if (Sun != null)
        {
            Sun.RotationDegrees = new Vector3(-lightElev, yaw, 0);
            var low = Mathf.Clamp(elev / 25f, 0, 1); // golden light near the horizon
            Sun.LightColor = new Color("#ffb36b").Lerp(new Color("#ffe9c4"), low); // warm sunlight
            Sun.LightEnergy = 1.45f * day * (1 - 0.5f * _cloud) * (Indoor ? 0.25f : 1);
            Sun.Visible = Sun.LightEnergy > 0.01f;
        }
        var night = 1 - day;
        _moon.LightEnergy = 0.38f * night * (1 - 0.5f * _cloud); // a bright moon keeps nights playable
        _moon.Visible = _moon.LightEnergy > 0.01f;

        // --- lightning ---
        Flashed = false;
        _flash = Mathf.Max(0, _flash - dt * 6);
        if (w.Kind == WeatherKind.Storm && WeatherFx && MovingParts)
        {
            _flashTimer -= dt;
            if (_flashTimer <= 0) { _flash = 1; Flashed = true; _flashTimer = 4 + _skyRng.NextDouble() * 10; }
        }

        // --- sky, ambient light and fog ---
        var env = _env?.Environment;
        if (env != null)
        {
            var dusk = Mathf.Clamp(1 - Mathf.Abs(elev - 2) / 10f, 0, 1);
            var sky = new Color("#0d1626").Lerp(Brand.Sky, day);
            sky = sky.Lerp(new Color("#f2a66b"), dusk * 0.55f);
            var grey = new Color("#9aa7b2").Lerp(new Color("#1a222c"), night);
            sky = sky.Lerp(grey, _cloud * 0.75f);
            sky = sky.Lerp(new Color("#e8eeff"), _flash * 0.8f);
            env.BackgroundColor = sky;
            _skyMat.SkyTopColor = sky.Darkened(0.28f).Lerp(new Color("#2f6fc0"), 0.35f * day * (1 - _cloud));
            _skyMat.SkyHorizonColor = sky.Lightened(0.22f);
            _skyMat.GroundHorizonColor = sky.Lightened(0.22f);
            _skyMat.GroundBottomColor = new Color("#4d6b3c").Lerp(new Color("#0b1016"), night);
            var amb = new Color("#3a4f7a").Lerp(new Color("#b9d6f2"), day); // cool sky fill against the warm sun
            env.AmbientLightColor = Indoor ? new Color("#f2f6ff") : amb;
            env.AmbientLightSkyContribution = Indoor ? 0f : 0.35f * day;
            env.AmbientLightEnergy = Indoor ? 0.85f : 0.22f + 0.04f * day + 0.24f * _cloud * day + _flash * 0.9f;
            env.FogLightColor = new Color("#1b2636").Lerp(new Color("#bfe0f4"), day).Lerp(grey, _cloud * 0.5f);
            // fog thins the far view but never hides the site under the camera
            env.FogDensity = Indoor ? 0.0006f : 0.0007f + _fogK * 0.0075f + night * 0.0008f;
        }

        // --- precipitation follows the camera ---
        var r = Mathf.Clamp(WeatherRadius, 18, 90);
        foreach (var (p, amount) in new[] { (_rain, _wet), (_snow, _flakes) })
        {
            var on = amount > 0.02f && !Indoor;
            p.Emitting = on;
            p.AmountRatio = Mathf.Clamp(amount, 0, 1);
            p.Position = WeatherCenter + new Vector3(0, p == _snow ? 26 : 30, 0);
            if (p.ProcessMaterial is ParticleProcessMaterial pm) pm.EmissionBoxExtents = new Vector3(r, 0.5f, r);
        }

        // --- the build grid fades at night so it does not glare ---
        if (_grid?.MaterialOverride is StandardMaterial3D gm) gm.AlbedoColor = new Color(1, 1, 1, 0.18f * (0.3f + 0.7f * day));

        // --- night lights: windows, lamp posts, facade lights, headlights ---
        WindowMat.EmissionEnergyMultiplier = 1.6f * Mathf.Clamp(night * 1.3f - 0.15f, 0, 1);
        Models.LampMat.EmissionEnergyMultiplier = 1.2f + 3.5f * night;
        HeadlightMat.EmissionEnergyMultiplier = 0.4f + 2.6f * Mathf.Max(night, _fogK);
        var lamp = Mathf.Clamp(night * 1.4f - 0.2f, 0, 1) * 1.6f;
        if (Mathf.Abs(lamp - _lampEnergy) > 0.02f)
        {
            _lampEnergy = lamp;
            foreach (var n in GetTree().GetNodesInGroup(Models.NightLightGroup))
                if (n is Light3D l) { l.LightEnergy = lamp; l.Visible = lamp > 0.02f; }
        }

        // --- seasons and snow ---
        if (_lastSeason != w.Season || Mathf.Abs(_snowK - _lastSnow) > 0.01f)
        {
            _lastSeason = w.Season; _lastSnow = _snowK;
            var (leaf, leaf2, tint) = SeasonColors(w.Season);
            var snowy = _snowK * 0.85f;
            if (_grassMat != null) _grassMat.AlbedoColor = Lift(Mul(Brand.Grass, tint).Lerp(SnowColor, snowy));
            if (_plotGrassMat != null) _plotGrassMat.AlbedoColor = Lift(Mul(Brand.PlotGrass, tint).Lerp(SnowColor, snowy));
            if (_crownMat != null) _crownMat.AlbedoColor = leaf.Lerp(SnowColor, snowy * 0.5f);
            if (_crown2Mat != null) _crown2Mat.AlbedoColor = leaf2.Lerp(SnowColor, snowy * 0.5f);
            if (_fieldMat != null) _fieldMat.AlbedoColor = Mul(new Color("#b5a35a"), tint).Lerp(SnowColor, snowy);
            Foliage.SetColor(Foliage.Leaf, (w.Season == Season.Summer ? Foliage.LeafSummer : leaf).Lerp(SnowColor, snowy * 0.5f));
            Foliage.SetColor(Foliage.Leaf2, (w.Season == Season.Summer ? Foliage.Leaf2Summer : leaf2).Lerp(SnowColor, snowy * 0.5f));
            Foliage.SetColor(Foliage.Needle, Foliage.NeedleSummer.Lerp(SnowColor, snowy * 0.45f));
            Foliage.SetColor(Foliage.Bush, Mul(new Color("#4f8a37"), tint).Lerp(SnowColor, snowy * 0.5f));
            Foliage.SetColor(Foliage.Grass, Mul(Foliage.GrassSummer, tint).Lerp(SnowColor, snowy * 0.8f));
            RoofMat.AlbedoColor = RoofColor.Lerp(SnowColor, _snowK);
            RoofTopMat.AlbedoColor = RoofTopColor.Lerp(SnowColor, _snowK);
        }
    }
}
