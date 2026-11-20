using System;
using System.Collections.Generic;
using DatacenterArchitect.Core;
using DatacenterArchitect.Sim;
using Godot;

namespace DatacenterArchitect.World;

/// <summary>Low-poly models built from primitives, in the flat colourful brand style.</summary>
public static class Models
{
    static readonly Dictionary<(Color, float, float, bool), StandardMaterial3D> Mats = new();

    public static StandardMaterial3D Mat(Color c, float rough = 0.8f, float metal = 0f, bool glow = false)
    {
        var key = (c, rough, metal, glow);
        if (Mats.TryGetValue(key, out var m)) return m;
        m = new StandardMaterial3D { AlbedoColor = c, Roughness = rough, Metallic = metal };
        if (c.A < 0.999f) m.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        if (glow) { m.EmissionEnabled = true; m.Emission = c; m.EmissionEnergyMultiplier = 1.2f; }
        Mats[key] = m;
        return m;
    }

    public static MeshInstance3D Box(Node3D p, Vector3 pos, Vector3 size, Color c, float rough = 0.8f, float metal = 0f, bool glow = false)
    {
        var m = new MeshInstance3D { Mesh = new BoxMesh { Size = size }, Position = pos, MaterialOverride = Mat(c, rough, metal, glow) };
        p.AddChild(m); return m;
    }

    public static MeshInstance3D Ball(Node3D p, Vector3 pos, float r, Color c, float squash = 1f)
    {
        var m = new MeshInstance3D { Mesh = new SphereMesh { Radius = r, Height = r * 2, RadialSegments = 14, Rings = 8 }, Position = pos, Scale = new Vector3(1, squash, 1), MaterialOverride = Mat(c, 0.9f) };
        p.AddChild(m); return m;
    }

    public static MeshInstance3D Cyl(Node3D p, Vector3 pos, float r, float h, Color c, float rTop = -1, bool glow = false)
    {
        var m = new MeshInstance3D { Mesh = new CylinderMesh { BottomRadius = r, TopRadius = rTop < 0 ? r : rTop, Height = h, RadialSegments = 12 }, Position = pos, MaterialOverride = Mat(c, 0.7f, 0, glow) };
        p.AddChild(m); return m;
    }

    public static MeshInstance3D Capsule(Node3D p, Vector3 pos, float r, float h, Color c)
    {
        var m = new MeshInstance3D { Mesh = new CapsuleMesh { Radius = r, Height = h, RadialSegments = 12, Rings = 4 }, Position = pos, MaterialOverride = Mat(c, 0.85f) };
        p.AddChild(m); return m;
    }

    public static void Tint(Node n, Color c)
    {
        var mat = Mat(new Color(c, 0.45f), 1f);
        foreach (var child in n.GetChildren())
        {
            if (child is MeshInstance3D mi) { mi.MaterialOverride = mat; mi.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off; }
            if (child is Node cn) Tint(cn, c);
        }
    }

    /// <summary>Outdoor trees and shrubs share these so the seasons can recolour them all at once.</summary>
    public static readonly StandardMaterial3D LeafMat = new() { AlbedoColor = new Color("#5f9a45"), Roughness = 0.9f };
    public static readonly StandardMaterial3D Leaf2Mat = new() { AlbedoColor = new Color("#77b356"), Roughness = 0.9f };
    /// <summary>Lamp heads glow brighter at night; their lights sit in this group and switch on at dusk.</summary>
    public static readonly StandardMaterial3D LampMat = new() { AlbedoColor = new Color("#fff2c4"), EmissionEnabled = true, Emission = new Color("#fff2c4"), EmissionEnergyMultiplier = 1.2f };
    public const string NightLightGroup = "night_light";

    static readonly Color White = new("#eef1f3"), Steel = new("#8f989f"), Dark = new("#2b3036"), Wood = new("#9a7550"), Leaf = new("#5f9a45"), Leaf2 = new("#77b356");

    /// <summary>Model of a placed object, centred on its footprint, facing +z when Rot = 0.</summary>
    public static Node3D Object(ObjKind k) => Catalog.IsShaft(k) ? Shaft(k, false) : Thing(k);

    /// <summary>Stairs and lifts, drawn on the floor in view. On the top floor stairs end in a guarded opening.</summary>
    public static Node3D Shaft(ObjKind k, bool top)
    {
        var n = new Node3D();
        if (k == ObjKind.Stairs) { Stairs(n, top); return n; }
        if (k == ObjKind.PassengerLift) { PassengerLift(n); return n; }
        Box(n, new Vector3(0, 0.02f, 0), new Vector3(1.9f, 0.04f, 1.9f), new Color("#5d6670"));
        foreach (var (x, z) in new[] { (-0.92f, -0.92f), (0.92f, -0.92f), (-0.92f, 0.92f), (0.92f, 0.92f) })
            Box(n, new Vector3(x, 1.6f, z), new Vector3(0.08f, 3.2f, 0.08f), Brand.Power);
        Box(n, new Vector3(0, 0.25f, 0.95f), new Vector3(1.9f, 0.08f, 0.04f), Brand.Power);
        return n;
    }

    /// <summary>Straight flight of 18 steps over a 2 x 5 m footprint, bottom at the front (+z), with steel handrails.</summary>
    static void Stairs(Node3D n, bool top)
    {
        const int steps = 18;
        const float width = 1.3f, rise = 3.6f / steps, run = 4.4f / steps, z0 = 2.5f;
        var stone = new Color("#c9ccc8"); var nosing = new Color("#3d4247"); var rail = new Color("#9aa3aa"); var glass = new Color(0.72f, 0.85f, 0.92f, 0.3f);
        if (top)
        {
            // the opening the flight comes up through, guarded by glass on three sides
            Box(n, new Vector3(0, 0.01f, 0.3f), new Vector3(width + 0.1f, 0.02f, 4.4f), new Color("#20262c"));
            Box(n, new Vector3(0, 0.012f, -2.2f), new Vector3(width + 0.1f, 0.02f, 0.6f), stone);
            foreach (var x in new[] { -0.75f, 0.75f })
            {
                Box(n, new Vector3(x, 0.5f, 0.3f), new Vector3(0.02f, 1.0f, 4.4f), glass, 0.1f);
                Box(n, new Vector3(x, 1.02f, 0.3f), new Vector3(0.06f, 0.05f, 4.4f), rail, 0.3f, 0.7f);
            }
            Box(n, new Vector3(0, 0.5f, 2.5f), new Vector3(1.5f, 1.0f, 0.02f), glass, 0.1f);
            Box(n, new Vector3(0, 1.02f, 2.5f), new Vector3(1.5f, 0.05f, 0.06f), rail, 0.3f, 0.7f);
            return;
        }
        for (var i = 0; i < steps; i++)
        {
            var h = (i + 1) * rise; var z = z0 - (i + 0.5f) * run;
            Box(n, new Vector3(0, h / 2, z), new Vector3(width, h, run), stone, 0.85f);
            Box(n, new Vector3(0, h - 0.008f, z + run / 2 - 0.02f), new Vector3(width, 0.018f, 0.04f), nosing);
        }
        // top landing that meets the floor above
        Box(n, new Vector3(0, 1.8f, -2.2f), new Vector3(width, 3.6f, 0.6f), stone, 0.85f);
        // stringers and handrails following the pitch, with posts every few steps
        var len = Mathf.Sqrt(4.4f * 4.4f + 3.6f * 3.6f);
        var pitch = Mathf.Atan2(3.6f, 4.4f);
        foreach (var x in new[] { -width / 2 - 0.04f, width / 2 + 0.04f })
        {
            var stringer = Box(n, new Vector3(x, 1.8f - 0.12f, z0 - 2.2f), new Vector3(0.06f, 0.3f, len), new Color("#4b535b"), 0.6f, 0.4f);
            stringer.Rotation = new Vector3(pitch, 0, 0);
            var hand = Box(n, new Vector3(x, 1.8f + 0.9f, z0 - 2.2f), new Vector3(0.05f, 0.05f, len), rail, 0.3f, 0.7f);
            hand.Rotation = new Vector3(pitch, 0, 0);
            for (var i = 1; i < steps; i += 4)
                Box(n, new Vector3(x, i * rise + 0.45f + rise, z0 - (i + 0.5f) * run), new Vector3(0.035f, 0.9f, 0.035f), rail, 0.3f, 0.7f);
        }
    }

    /// <summary>Enclosed passenger lift: panelled shaft, brushed steel doors, call buttons and a floor indicator.</summary>
    static void PassengerLift(Node3D n)
    {
        var panel = new Color("#dfe3e6"); var steel = new Color("#b9c1c7"); var frame = new Color("#59626b");
        Box(n, new Vector3(0, 1.6f, -0.92f), new Vector3(1.9f, 3.2f, 0.1f), panel, 0.6f);
        foreach (var x in new[] { -0.92f, 0.92f }) Box(n, new Vector3(x, 1.6f, 0), new Vector3(0.1f, 3.2f, 1.9f), panel, 0.6f);
        // front wall with the door opening
        foreach (var x in new[] { -0.75f, 0.75f }) Box(n, new Vector3(x, 1.6f, 0.92f), new Vector3(0.4f, 3.2f, 0.1f), panel, 0.6f);
        Box(n, new Vector3(0, 2.65f, 0.92f), new Vector3(1.1f, 1.1f, 0.1f), panel, 0.6f);
        // steel frame and two sliding door leaves
        Box(n, new Vector3(0, 2.13f, 0.975f), new Vector3(1.16f, 0.07f, 0.03f), frame, 0.4f, 0.6f);
        foreach (var x in new[] { -0.565f, 0.565f }) Box(n, new Vector3(x, 1.05f, 0.975f), new Vector3(0.07f, 2.1f, 0.03f), frame, 0.4f, 0.6f);
        foreach (var x in new[] { -0.265f, 0.265f }) Box(n, new Vector3(x, 1.05f, 0.95f), new Vector3(0.52f, 2.08f, 0.03f), steel, 0.25f, 0.85f);
        Box(n, new Vector3(0, 1.05f, 0.968f), new Vector3(0.012f, 2.08f, 0.01f), frame);
        // call buttons and the floor indicator above the door
        Box(n, new Vector3(0.75f, 1.15f, 0.98f), new Vector3(0.1f, 0.22f, 0.02f), frame, 0.4f, 0.6f);
        foreach (var y in new[] { 1.2f, 1.1f }) Box(n, new Vector3(0.75f, y, 0.992f), new Vector3(0.04f, 0.04f, 0.01f), Brand.Cooling, glow: true);
        Box(n, new Vector3(0, 2.3f, 0.975f), new Vector3(0.3f, 0.12f, 0.02f), new Color("#14181c"));
        Box(n, new Vector3(0, 2.3f, 0.988f), new Vector3(0.12f, 0.07f, 0.01f), Brand.Power, glow: true);
        // a floor tile in front so the landing reads at a glance
        Box(n, new Vector3(0, 0.01f, 0), new Vector3(1.7f, 0.02f, 1.7f), new Color("#8e979e"), 0.5f);
    }

    static Node3D Thing(ObjKind k)
    {
        var n = new Node3D();
        if (Furnished(k, n) || Made(k, n)) return n;
        switch (k)
        {
            case ObjKind.Rack:
                Box(n, new Vector3(0, 1.05f, 0), new Vector3(0.985f, 2.1f, 0.95f), Brand.Rack, 0.5f, 0.4f);
                Box(n, new Vector3(0, 1.05f, 0.476f), new Vector3(0.9f, 1.95f, 0.01f), new Color("#363c44"), 0.4f, 0.3f);
                n.AddChild(ServerFill.Instance(3, true));
                break;
            case ObjKind.Crah:
                Box(n, new Vector3(0, 1.0f, 0), new Vector3(0.95f, 2f, 1.9f), White, 0.5f);
                Box(n, new Vector3(0, 1.85f, 0), new Vector3(0.97f, 0.12f, 1.92f), Brand.Cooling, glow: true);
                for (var i = 0; i < 4; i++) Box(n, new Vector3(0, 0.4f + i * 0.3f, 0.955f), new Vector3(0.8f, 0.05f, 0.01f), Steel);
                break;
            case ObjKind.Generator:
                Box(n, new Vector3(0, 1.45f, 0), new Vector3(2.9f, 2.5f, 1.9f), new Color("#e9edf0"), 0.4f, 0.3f);
                Box(n, new Vector3(0, 2.2f, 0), new Vector3(2.92f, 0.22f, 1.92f), Brand.Power);
                Box(n, new Vector3(0, 0.1f, 0), new Vector3(3f, 0.2f, 2f), new Color("#9ea3a8"));
                Cyl(n, new Vector3(1f, 3.1f, 0), 0.12f, 1.0f, new Color("#5d6b78"));
                break;
            case ObjKind.FiberPit:
                // hand hole with a concrete rim, an orange cover and a marker post
                Box(n, new Vector3(0, 0.04f, 0), new Vector3(0.95f, 0.08f, 0.95f), Brand.Concrete);
                Box(n, new Vector3(0, 0.07f, 0), new Vector3(0.75f, 0.04f, 0.75f), Brand.Fiber);
                Box(n, new Vector3(0.38f, 0.45f, 0.38f), new Vector3(0.08f, 0.9f, 0.08f), Brand.Fiber);
                Box(n, new Vector3(0.38f, 0.85f, 0.38f), new Vector3(0.1f, 0.12f, 0.1f), White);
                break;
            case ObjKind.Desk:
                Box(n, new Vector3(0, 0.74f, 0), new Vector3(1.9f, 0.06f, 0.8f), new Color("#d9c7a8"));
                foreach (var x in new[] { -0.85f, 0.85f }) Box(n, new Vector3(x, 0.37f, 0), new Vector3(0.06f, 0.74f, 0.7f), Steel);
                Box(n, new Vector3(0.3f, 1.02f, -0.15f), new Vector3(0.6f, 0.38f, 0.04f), Dark);
                Box(n, new Vector3(0.3f, 1.02f, -0.125f), new Vector3(0.54f, 0.32f, 0.01f), new Color("#4aa3d8"), glow: true);
                break;
            case ObjKind.Chair:
                Box(n, new Vector3(0, 0.46f, 0), new Vector3(0.5f, 0.08f, 0.5f), new Color("#3b4652"));
                Box(n, new Vector3(0, 0.8f, -0.22f), new Vector3(0.48f, 0.6f, 0.07f), new Color("#3b4652"));
                Cyl(n, new Vector3(0, 0.22f, 0), 0.04f, 0.44f, Steel);
                break;
            case ObjKind.Plant:
                Cyl(n, new Vector3(0, 0.22f, 0), 0.22f, 0.44f, new Color("#c9b9a3"), 0.18f);
                Ball(n, new Vector3(0, 0.75f, 0), 0.34f, Leaf);
                Ball(n, new Vector3(0.1f, 1.0f, 0.05f), 0.22f, Leaf2);
                break;
            case ObjKind.Cabinet:
                Box(n, new Vector3(0, 0.9f, 0), new Vector3(0.9f, 1.8f, 0.5f), new Color("#c8ced3"));
                Box(n, new Vector3(0, 0.9f, 0.255f), new Vector3(0.02f, 1.7f, 0.01f), Steel);
                break;
            case ObjKind.MeetingTable:
                Box(n, new Vector3(0, 0.74f, 0), new Vector3(2.8f, 0.07f, 1.5f), new Color("#b88a5c"));
                Box(n, new Vector3(0, 0.37f, 0), new Vector3(0.3f, 0.74f, 0.9f), Steel);
                foreach (var x in new[] { -0.9f, 0f, 0.9f })
                    foreach (var z in new[] { -0.95f, 0.95f })
                        Box(n, new Vector3(x, 0.46f, z), new Vector3(0.45f, 0.08f, 0.45f), new Color("#3b4652"));
                break;
            case ObjKind.Vending:
                Box(n, new Vector3(0, 0.95f, 0), new Vector3(0.9f, 1.9f, 0.8f), new Color("#d6453d"));
                Box(n, new Vector3(-0.12f, 1.05f, 0.405f), new Vector3(0.55f, 1.3f, 0.01f), new Color("#bfe6ff"), glow: true);
                break;
            case ObjKind.Microwave:
                Box(n, new Vector3(0, 0.45f, 0), new Vector3(0.9f, 0.9f, 0.6f), new Color("#d9c7a8"));
                Box(n, new Vector3(0, 1.05f, 0), new Vector3(0.55f, 0.32f, 0.4f), White);
                Box(n, new Vector3(-0.05f, 1.05f, 0.205f), new Vector3(0.35f, 0.22f, 0.01f), Dark);
                break;
            case ObjKind.Coffee:
                Box(n, new Vector3(0, 0.45f, 0), new Vector3(0.9f, 0.9f, 0.6f), new Color("#d9c7a8"));
                Box(n, new Vector3(0, 1.15f, -0.05f), new Vector3(0.5f, 0.5f, 0.4f), Dark);
                Box(n, new Vector3(0, 1.25f, 0.155f), new Vector3(0.3f, 0.12f, 0.01f), new Color("#ffb23e"), glow: true);
                Cyl(n, new Vector3(0.25f, 0.97f, 0.12f), 0.05f, 0.1f, White);
                Cyl(n, new Vector3(-0.25f, 0.97f, 0.12f), 0.05f, 0.1f, White);
                break;
            case ObjKind.Sofa:
                Box(n, new Vector3(0, 0.3f, 0.05f), new Vector3(1.9f, 0.4f, 0.8f), new Color("#4f7fa8"));
                Box(n, new Vector3(0, 0.65f, -0.3f), new Vector3(1.9f, 0.6f, 0.2f), new Color("#4f7fa8"));
                foreach (var x in new[] { -0.9f, 0.9f }) Box(n, new Vector3(x, 0.5f, 0.05f), new Vector3(0.15f, 0.45f, 0.8f), new Color("#456f94"));
                break;
            case ObjKind.Terminal:
                Box(n, new Vector3(0, 0.74f, 0), new Vector3(0.9f, 0.06f, 0.7f), White);
                Box(n, new Vector3(0, 0.37f, 0), new Vector3(0.8f, 0.74f, 0.05f), Steel);
                Box(n, new Vector3(0, 1.02f, -0.15f), new Vector3(0.6f, 0.38f, 0.04f), Dark);
                Box(n, new Vector3(0, 1.02f, -0.125f), new Vector3(0.54f, 0.32f, 0.01f), new Color("#5fd49b"), glow: true);
                break;
            case ObjKind.CrashCart:
                Box(n, new Vector3(0, 0.8f, 0), new Vector3(0.6f, 0.05f, 0.45f), Steel);
                Box(n, new Vector3(0, 0.4f, 0), new Vector3(0.6f, 0.05f, 0.45f), Steel);
                foreach (var x in new[] { -0.27f, 0.27f }) Box(n, new Vector3(x, 0.55f, 0), new Vector3(0.04f, 0.9f, 0.04f), Steel);
                Box(n, new Vector3(0, 1.05f, 0), new Vector3(0.45f, 0.32f, 0.04f), Dark);
                Box(n, new Vector3(0, 1.05f, 0.025f), new Vector3(0.4f, 0.26f, 0.01f), new Color("#4aa3d8"), glow: true);
                Box(n, new Vector3(0, 0.84f, 0.08f), new Vector3(0.4f, 0.03f, 0.15f), Dark);
                break;
            case ObjKind.Tree:
                Cyl(n, new Vector3(0, 0.8f, 0), 0.1f, 1.6f, new Color("#7a5a3c"), 0.07f);
                Ball(n, new Vector3(0, 2.1f, 0), 0.85f, Leaf, 0.9f).MaterialOverride = LeafMat;
                Ball(n, new Vector3(0.25f, 2.6f, 0.1f), 0.55f, Leaf2).MaterialOverride = Leaf2Mat;
                break;
            case ObjKind.Shrub:
                Ball(n, new Vector3(0, 0.35f, 0), 0.48f, Leaf, 0.75f).MaterialOverride = LeafMat;
                break;
            case ObjKind.LampPost:
                Cyl(n, new Vector3(0, 2.0f, 0), 0.06f, 4f, Dark);
                Box(n, new Vector3(0, 4.0f, 0.25f), new Vector3(0.18f, 0.08f, 0.6f), Dark);
                Box(n, new Vector3(0, 3.95f, 0.45f), new Vector3(0.16f, 0.03f, 0.24f), new Color("#fff2c4"), glow: true).MaterialOverride = LampMat;
                var lamp = new OmniLight3D { Position = new Vector3(0, 3.7f, 0.45f), OmniRange = 9, LightColor = new Color("#ffd9a0"), LightEnergy = 0, ShadowEnabled = false, Visible = false };
                lamp.AddToGroup(NightLightGroup);
                n.AddChild(lamp);
                break;
            case ObjKind.Bench:
                Box(n, new Vector3(0, 0.45f, 0), new Vector3(1.8f, 0.07f, 0.45f), Wood);
                Box(n, new Vector3(0, 0.75f, -0.2f), new Vector3(1.8f, 0.3f, 0.05f), Wood);
                foreach (var x in new[] { -0.75f, 0.75f }) Box(n, new Vector3(x, 0.22f, 0), new Vector3(0.06f, 0.45f, 0.4f), Dark);
                break;
            case ObjKind.BikeRack:
                for (var i = 0; i < 5; i++)
                {
                    var x = -0.8f + i * 0.4f;
                    Box(n, new Vector3(x, 0.45f, -0.2f), new Vector3(0.04f, 0.9f, 0.04f), Steel, 0.4f, 0.7f);
                    Box(n, new Vector3(x, 0.45f, 0.2f), new Vector3(0.04f, 0.9f, 0.04f), Steel, 0.4f, 0.7f);
                    Box(n, new Vector3(x, 0.9f, 0), new Vector3(0.04f, 0.04f, 0.44f), Steel, 0.4f, 0.7f);
                }
                break;
            case ObjKind.GuardBooth:
                Box(n, new Vector3(0, 0.1f, 0), new Vector3(1.95f, 0.2f, 1.95f), Brand.Concrete);
                Box(n, new Vector3(0, 1.3f, 0), new Vector3(1.8f, 2.2f, 1.8f), White);
                Box(n, new Vector3(0, 1.6f, 0), new Vector3(1.84f, 0.8f, 1.84f), new Color("#6fb3d9"), 0.2f);
                Box(n, new Vector3(0, 2.5f, 0), new Vector3(2.1f, 0.2f, 2.1f), new Color("#2d4a6b"));
                Box(n, new Vector3(0, 2.4f, 0), new Vector3(2.12f, 0.06f, 2.12f), Brand.Power);
                break;
            case ObjKind.Intercom:
                Box(n, new Vector3(0, 0.6f, 0), new Vector3(0.12f, 1.2f, 0.12f), Dark);
                Box(n, new Vector3(0, 1.25f, 0), new Vector3(0.3f, 0.4f, 0.2f), new Color("#3b4652"));
                Box(n, new Vector3(0, 1.3f, 0.105f), new Vector3(0.12f, 0.08f, 0.01f), Brand.Ok, glow: true);
                break;
            case ObjKind.Barrier:
            {
                Box(n, new Vector3(-1.3f, 0.5f, 0), new Vector3(0.35f, 1f, 0.35f), White);
                Box(n, new Vector3(-1.3f, 0.95f, 0), new Vector3(0.37f, 0.1f, 0.37f), Brand.Critical);
                // the arm pivots on the post and swings up when a vehicle comes
                var arm = new Node3D { Name = "Arm", Position = new Vector3(-1.3f, 0.9f, 0) }; n.AddChild(arm);
                for (var i = 0; i < 7; i++)
                    Box(arm, new Vector3(0.25f + i * 0.4f, 0, 0), new Vector3(0.4f, 0.08f, 0.08f), i % 2 == 0 ? Brand.Critical : White);
                break;
            }
            case ObjKind.SlidingGate:
            case ObjKind.SlidingGateLarge:
                SlidingGate(n, k == ObjKind.SlidingGate ? 3 : 6);
                break;
            case ObjKind.PedGate:
                Box(n, new Vector3(-0.45f, 1.1f, 0), new Vector3(0.1f, 2.2f, 0.1f), Dark);
                Box(n, new Vector3(0.45f, 1.1f, 0), new Vector3(0.1f, 2.2f, 0.1f), Dark);
                Box(n, new Vector3(0, 2.15f, 0), new Vector3(1f, 0.08f, 0.08f), Dark);
                Box(n, new Vector3(-0.45f, 1.3f, 0.08f), new Vector3(0.12f, 0.18f, 0.06f), Brand.Ok, glow: true);
                break;
            case ObjKind.Transformer:
                // oil transformer with cooling fins and HV bushings
                Box(n, new Vector3(0, 0.1f, 0), new Vector3(1.9f, 0.2f, 1.9f), Brand.Concrete);
                Box(n, new Vector3(0, 0.95f, 0), new Vector3(1.3f, 1.5f, 1.1f), new Color("#7d8a93"), 0.5f, 0.3f);
                for (var i = 0; i < 6; i++)
                {
                    Box(n, new Vector3(-0.55f + i * 0.22f, 0.9f, 0.68f), new Vector3(0.05f, 1.2f, 0.26f), new Color("#6c7881"));
                    Box(n, new Vector3(-0.55f + i * 0.22f, 0.9f, -0.68f), new Vector3(0.05f, 1.2f, 0.26f), new Color("#6c7881"));
                }
                foreach (var x in new[] { -0.4f, 0f, 0.4f }) Cyl(n, new Vector3(x, 1.95f, 0), 0.07f, 0.5f, new Color("#c9b98f"));
                Box(n, new Vector3(0, 1.72f, 0.56f), new Vector3(1.32f, 0.08f, 0.02f), Brand.Power, glow: true);
                break;
            case ObjKind.Switchgear:
                Box(n, new Vector3(0, 1.1f, 0), new Vector3(1.95f, 2.2f, 0.85f), new Color("#c8ced3"), 0.5f);
                for (var i = 0; i < 4; i++)
                {
                    Box(n, new Vector3(-0.73f + i * 0.49f, 1.1f, 0.43f), new Vector3(0.44f, 2.0f, 0.01f), new Color("#b2b9bf"));
                    Box(n, new Vector3(-0.73f + i * 0.49f, 1.6f, 0.44f), new Vector3(0.2f, 0.14f, 0.01f), Dark);
                    Box(n, new Vector3(-0.66f + i * 0.49f, 1.25f, 0.445f), new Vector3(0.05f, 0.05f, 0.01f), i == 0 ? Brand.Ok : Brand.Power, glow: true);
                }
                Box(n, new Vector3(0, 2.15f, 0), new Vector3(1.97f, 0.1f, 0.87f), Brand.Power);
                break;
            case ObjKind.Ups:
                Box(n, new Vector3(0, 1.0f, 0), new Vector3(1.95f, 2.0f, 0.9f), new Color("#2f3740"), 0.5f, 0.2f);
                Box(n, new Vector3(-0.5f, 1.45f, 0.455f), new Vector3(0.45f, 0.3f, 0.01f), new Color("#5fd49b"), glow: true);
                for (var i = 0; i < 6; i++) Box(n, new Vector3(0.35f, 0.5f + i * 0.2f, 0.455f), new Vector3(0.9f, 0.04f, 0.01f), new Color("#4a535c"));
                Box(n, new Vector3(0, 1.98f, 0), new Vector3(1.97f, 0.06f, 0.92f), Brand.Power);
                break;
            case ObjKind.Battery:
                Box(n, new Vector3(0, 1.0f, 0), new Vector3(0.9f, 2.0f, 0.8f), new Color("#dfe4e8"), 0.5f);
                for (var i = 0; i < 5; i++) Box(n, new Vector3(0, 0.3f + i * 0.36f, 0.405f), new Vector3(0.76f, 0.26f, 0.01f), new Color("#3c4650"));
                Box(n, new Vector3(0.3f, 1.9f, 0.41f), new Vector3(0.1f, 0.06f, 0.01f), Brand.Ok, glow: true);
                break;
            case ObjKind.Pdu:
                Box(n, new Vector3(0, 0.95f, 0), new Vector3(0.8f, 1.9f, 0.8f), new Color("#3a4149"), 0.5f, 0.2f);
                Box(n, new Vector3(0, 1.5f, 0.405f), new Vector3(0.4f, 0.22f, 0.01f), new Color("#ffcf7a"), glow: true);
                Box(n, new Vector3(0, 1.88f, 0), new Vector3(0.82f, 0.06f, 0.82f), Brand.Power);
                break;
            case ObjKind.DieselTank:
                // bunded above-ground tank on saddles
                Box(n, new Vector3(0, 0.15f, 0), new Vector3(2.95f, 0.3f, 0.95f), new Color("#9ea3a8"));
                var tank = Cyl(n, new Vector3(0, 0.85f, 0), 0.42f, 2.6f, new Color("#eef1f3"));
                tank.RotationDegrees = new Vector3(0, 0, 90);
                Box(n, new Vector3(0, 0.85f, 0.43f), new Vector3(1.2f, 0.18f, 0.01f), Brand.Power);
                Cyl(n, new Vector3(1.0f, 1.55f, 0), 0.04f, 0.9f, Steel);
                break;
            case ObjKind.UndergroundTank:
                // only the fill point, vent pipe and manholes show above ground
                Box(n, new Vector3(-0.9f, 0.03f, 0), new Vector3(0.7f, 0.06f, 0.7f), new Color("#6b7177"));
                Box(n, new Vector3(0.6f, 0.03f, 0.3f), new Vector3(0.6f, 0.06f, 0.6f), new Color("#6b7177"));
                Box(n, new Vector3(-0.9f, 0.45f, -0.6f), new Vector3(0.4f, 0.8f, 0.3f), Brand.Power);
                Cyl(n, new Vector3(1.2f, 1.4f, -0.7f), 0.05f, 2.8f, Steel);
                break;
            case ObjKind.Chiller:
                Box(n, new Vector3(0, 0.12f, 0), new Vector3(2.95f, 0.24f, 1.95f), new Color("#9ea3a8"));
                Box(n, new Vector3(0, 1.0f, 0), new Vector3(2.8f, 1.5f, 1.8f), White, 0.5f);
                for (var i = 0; i < 6; i++) Box(n, new Vector3(-1.2f + i * 0.48f, 0.95f, 0.905f), new Vector3(0.03f, 1.2f, 0.01f), Steel);
                foreach (var x in new[] { -0.75f, 0.75f })
                {
                    Cyl(n, new Vector3(x, 1.8f, 0), 0.6f, 0.12f, new Color("#3d464f"));
                    Cyl(n, new Vector3(x, 1.87f, 0), 0.12f, 0.06f, Brand.Cooling, glow: true);
                    var blade = Box(n, new Vector3(x, 1.88f, 0), new Vector3(1.0f, 0.02f, 0.14f), new Color("#8f989f")); blade.Name = "Fan";
                }
                Box(n, new Vector3(0, 0.3f, 0.92f), new Vector3(2.8f, 0.08f, 0.02f), Brand.Cooling);
                break;
            case ObjKind.Odf:
                // optical distribution frame: open rack with patch panels and orange fiber
                Box(n, new Vector3(0, 1.05f, 0), new Vector3(0.9f, 2.1f, 0.7f), new Color("#3a414a"), 0.5f, 0.3f);
                for (var i = 0; i < 7; i++)
                {
                    Box(n, new Vector3(0, 0.45f + i * 0.22f, 0.36f), new Vector3(0.8f, 0.12f, 0.02f), new Color("#c8ced3"));
                    Box(n, new Vector3(-0.2f + (i % 3) * 0.2f, 0.4f + i * 0.22f, 0.38f), new Vector3(0.05f, 0.18f, 0.02f), Brand.Fiber);
                }
                Box(n, new Vector3(0, 2.12f, 0), new Vector3(0.92f, 0.06f, 0.72f), Brand.Fiber);
                break;
            case ObjKind.CoreRouter:
                // chassis router: tall dark cabinet with line cards and a status display
                Box(n, new Vector3(0, 1.05f, 0), new Vector3(0.95f, 2.1f, 0.95f), new Color("#22272d"), 0.4f, 0.4f);
                for (var i = 0; i < 8; i++) Box(n, new Vector3(0, 0.5f + i * 0.16f, 0.48f), new Vector3(0.8f, 0.1f, 0.01f), new Color("#3d454e"));
                for (var i = 0; i < 8; i++) Box(n, new Vector3(0.33f, 0.5f + i * 0.16f, 0.486f), new Vector3(0.06f, 0.04f, 0.01f), i % 3 == 0 ? Brand.Fiber : Brand.Ok, glow: true);
                Box(n, new Vector3(0, 1.8f, 0.486f), new Vector3(0.5f, 0.16f, 0.01f), new Color("#4aa3d8"), glow: true);
                Box(n, new Vector3(0, 2.12f, 0), new Vector3(0.97f, 0.06f, 0.97f), Brand.Fiber);
                break;
            case ObjKind.Spine:
                // spine switch rack: a network rack with switches near the top and fiber running up to the tray
                Box(n, new Vector3(0, 1.05f, 0), new Vector3(0.985f, 2.1f, 0.95f), Brand.Rack, 0.5f, 0.4f);
                Box(n, new Vector3(0, 1.05f, 0.476f), new Vector3(0.9f, 1.95f, 0.01f), new Color("#2c3239"), 0.4f, 0.3f);
                for (var i = 0; i < 4; i++)
                {
                    Box(n, new Vector3(0, 1.4f + i * 0.14f, 0.485f), new Vector3(0.82f, 0.09f, 0.01f), new Color("#4b545e"));
                    Box(n, new Vector3(-0.3f, 1.4f + i * 0.14f, 0.49f), new Vector3(0.12f, 0.03f, 0.01f), Brand.Fiber, glow: true);
                }
                Box(n, new Vector3(0, 2.13f, 0), new Vector3(0.6f, 0.06f, 0.3f), Brand.Fiber);
                break;
            case ObjKind.NocWall:
                // video wall of 3 x 2 screens on a stand
                Box(n, new Vector3(0, 0.25f, -0.2f), new Vector3(2.9f, 0.5f, 0.5f), new Color("#3a414a"));
                for (var x = 0; x < 3; x++)
                    for (var y = 0; y < 2; y++)
                    {
                        Box(n, new Vector3(-0.96f + x * 0.96f, 1.0f + y * 0.56f, -0.3f), new Vector3(0.92f, 0.52f, 0.06f), Dark);
                        var col = (x + y) % 3 == 0 ? Brand.Fiber : (x + y) % 3 == 1 ? new Color("#4aa3d8") : Brand.Ok;
                        Box(n, new Vector3(-0.96f + x * 0.96f, 1.0f + y * 0.56f, -0.265f), new Vector3(0.84f, 0.44f, 0.01f), new Color(col, 0.9f), glow: true);
                    }
                break;
            case ObjKind.CameraPole:
            {
                // a camera on a pole with a little red recording light
                Cyl(n, new Vector3(0, 1.8f, 0), 0.07f, 3.6f, Dark);
                Box(n, new Vector3(0, 3.55f, 0.2f), new Vector3(0.08f, 0.08f, 0.4f), Dark);
                var cam = new Node3D { Position = new Vector3(0, 3.45f, 0.42f), RotationDegrees = new Vector3(-20, 0, 0) };
                n.AddChild(cam);
                Box(cam, new Vector3(0, 0, 0), new Vector3(0.22f, 0.2f, 0.45f), White);
                Box(cam, new Vector3(0, 0.12f, 0.02f), new Vector3(0.28f, 0.03f, 0.5f), White);
                Box(cam, new Vector3(0, 0, 0.23f), new Vector3(0.14f, 0.12f, 0.02f), new Color("#1b2026"));
                Box(cam, new Vector3(0.07f, 0.06f, 0.235f), new Vector3(0.03f, 0.03f, 0.01f), Brand.Critical, glow: true);
                break;
            }
            case ObjKind.CameraDome:
                // ceiling camera: a short pole from the cable tray height with a dark dome
                Cyl(n, new Vector3(0, 2.55f, 0), 0.03f, 0.5f, Steel);
                Cyl(n, new Vector3(0, 2.28f, 0), 0.16f, 0.06f, White);
                Ball(n, new Vector3(0, 2.24f, 0), 0.13f, new Color("#1b2026"), 0.7f);
                Box(n, new Vector3(0.1f, 2.27f, 0.1f), new Vector3(0.02f, 0.02f, 0.02f), Brand.Critical, glow: true);
                break;
            case ObjKind.SecurityWall:
                // security video wall: 4 x 2 camera feeds above a desk with two consoles
                Box(n, new Vector3(0, 0.37f, 0.25f), new Vector3(2.8f, 0.06f, 0.6f), White);
                Box(n, new Vector3(0, 0.2f, 0.25f), new Vector3(2.7f, 0.36f, 0.5f), new Color("#3a414a"));
                for (var x = 0; x < 4; x++)
                    for (var y = 0; y < 2; y++)
                    {
                        Box(n, new Vector3(-1.08f + x * 0.72f, 1.1f + y * 0.44f, -0.35f), new Vector3(0.7f, 0.42f, 0.05f), Dark);
                        var feed = (x + y) % 2 == 0 ? new Color("#7fa6b8") : new Color("#9bb59a");
                        Box(n, new Vector3(-1.08f + x * 0.72f, 1.1f + y * 0.44f, -0.32f), new Vector3(0.64f, 0.36f, 0.01f), feed, glow: true);
                    }
                Box(n, new Vector3(-1.08f, 1.1f, -0.315f), new Vector3(0.1f, 0.05f, 0.01f), Brand.Critical, glow: true);
                foreach (var x in new[] { -0.5f, 0.5f })
                {
                    Box(n, new Vector3(x, 0.58f, 0.3f), new Vector3(0.5f, 0.32f, 0.04f), Dark);
                    Box(n, new Vector3(x, 0.58f, 0.325f), new Vector3(0.46f, 0.28f, 0.01f), Brand.Cooling, glow: true);
                }
                break;
            case ObjKind.GlassLodge:
            {
                // glass lodge: a counter with a glass front and a guard's chair behind it
                var glass = new Color("#9fd3ee", 0.35f);
                Box(n, new Vector3(0, 0.55f, 0.85f), new Vector3(2.9f, 1.1f, 0.12f), new Color("#3a4652"));
                Box(n, new Vector3(0, 1.12f, 0.85f), new Vector3(3f, 0.05f, 0.3f), White);
                Box(n, new Vector3(0, 1.75f, 0.88f), new Vector3(2.9f, 1.2f, 0.04f), glass, 0.1f);
                foreach (var x in new[] { -1.45f, 1.45f }) Box(n, new Vector3(x, 1.2f, 0), new Vector3(0.06f, 2.4f, 1.8f), glass, 0.1f);
                Box(n, new Vector3(0, 2.4f, 0), new Vector3(3f, 0.08f, 1.9f), new Color("#2d4a6b"));
                Box(n, new Vector3(0, 2.36f, 0.93f), new Vector3(3f, 0.04f, 0.04f), Brand.Critical);
                Box(n, new Vector3(0.6f, 0.75f, 0.3f), new Vector3(1.1f, 0.05f, 0.6f), White);
                Box(n, new Vector3(0.6f, 0.95f, 0.15f), new Vector3(0.45f, 0.3f, 0.04f), Dark);
                Box(n, new Vector3(0.6f, 0.95f, 0.175f), new Vector3(0.4f, 0.25f, 0.01f), new Color("#7fa6b8"), glow: true);
                Box(n, new Vector3(0.6f, 0.45f, -0.3f), new Vector3(0.5f, 0.08f, 0.5f), Dark);
                Box(n, new Vector3(0.6f, 0.8f, -0.52f), new Vector3(0.5f, 0.6f, 0.06f), Dark);
                break;
            }
            case ObjKind.Bollard:
                Cyl(n, new Vector3(0, 0.45f, 0), 0.14f, 0.9f, new Color("#3b4148"));
                Cyl(n, new Vector3(0, 0.75f, 0), 0.145f, 0.08f, Brand.Power);
                Cyl(n, new Vector3(0, 0.6f, 0), 0.145f, 0.05f, Brand.Power);
                break;
            case ObjKind.LabBench:
                // lab bench with a test rig, a laptop and a small rack of prototypes
                Box(n, new Vector3(0, 0.88f, 0), new Vector3(1.9f, 0.06f, 0.8f), new Color("#e4e9ec"));
                foreach (var x in new[] { -0.88f, 0.88f }) Box(n, new Vector3(x, 0.44f, 0), new Vector3(0.08f, 0.88f, 0.72f), Steel);
                Box(n, new Vector3(-0.5f, 1.15f, -0.15f), new Vector3(0.6f, 0.5f, 0.4f), new Color("#3b4652"));
                for (var i = 0; i < 3; i++) Box(n, new Vector3(-0.5f, 1.0f + i * 0.14f, 0.055f), new Vector3(0.5f, 0.08f, 0.01f), new Color("#55606b"));
                Box(n, new Vector3(-0.68f, 1.3f, 0.06f), new Vector3(0.04f, 0.03f, 0.01f), Brand.Ok, glow: true);
                Box(n, new Vector3(-0.6f, 1.16f, 0.06f), new Vector3(0.04f, 0.03f, 0.01f), Brand.Cooling, glow: true);
                Box(n, new Vector3(0.45f, 0.92f, 0.1f), new Vector3(0.45f, 0.02f, 0.32f), Dark);
                Box(n, new Vector3(0.45f, 1.06f, -0.06f), new Vector3(0.45f, 0.28f, 0.02f), Dark);
                Box(n, new Vector3(0.45f, 1.06f, -0.045f), new Vector3(0.41f, 0.24f, 0.01f), new Color("#5fd49b"), glow: true);
                Cyl(n, new Vector3(0.82f, 0.98f, 0.2f), 0.05f, 0.14f, new Color("#9fd3ee", 0.6f));
                break;
            case ObjKind.SolarPanel:
            {
                // two tilted panels on a low frame
                var dark = new Color("#1d3550");
                foreach (var z in new[] { -0.5f, 0.5f })
                {
                    Box(n, new Vector3(0, 0.25f, z), new Vector3(1.8f, 0.05f, 0.05f), Steel);
                    var p = Box(n, new Vector3(0, 0.5f, z), new Vector3(1.9f, 0.04f, 0.85f), dark, 0.2f, 0.4f);
                    p.RotationDegrees = new Vector3(-22, 0, 0);
                    for (var i = 0; i < 4; i++) { var l = Box(n, new Vector3(-0.71f + i * 0.475f, 0.525f, z), new Vector3(0.015f, 0.01f, 0.83f), new Color("#6f8aa6")); l.RotationDegrees = new Vector3(-22, 0, 0); }
                    foreach (var x in new[] { -0.8f, 0.8f }) Box(n, new Vector3(x, 0.22f, z + 0.2f), new Vector3(0.05f, 0.44f, 0.05f), Steel);
                }
                break;
            }
            case ObjKind.WindTurbine:
            {
                Cyl(n, new Vector3(0, 0.1f, 0), 0.9f, 0.2f, Brand.Concrete);
                Cyl(n, new Vector3(0, 4.2f, 0), 0.16f, 8.2f, White, 0.1f);
                Box(n, new Vector3(0, 8.4f, -0.1f), new Vector3(0.38f, 0.38f, 1.0f), White);
                var hub = new Node3D { Name = "FanRotor", Position = new Vector3(0, 8.4f, 0.45f), RotationDegrees = new Vector3(90, 0, 0) };
                n.AddChild(hub);
                Ball(hub, Vector3.Zero, 0.2f, Brand.Power);
                for (var i = 0; i < 3; i++)
                {
                    var arm = new Node3D { RotationDegrees = new Vector3(0, i * 120, 0) }; hub.AddChild(arm);
                    Box(arm, new Vector3(1.6f, 0, 0), new Vector3(3.1f, 0.06f, 0.26f), White);
                }
                break;
            }
            case ObjKind.BatteryContainer:
                // a shipping container full of battery racks, with doors and a cooling unit
                Box(n, new Vector3(0, 0.08f, 0), new Vector3(2.98f, 0.16f, 0.98f), Brand.Concrete);
                Box(n, new Vector3(0, 0.75f, 0), new Vector3(2.9f, 1.2f, 0.9f), new Color("#f1f3f4"), 0.5f, 0.2f);
                for (var i = 0; i < 9; i++) Box(n, new Vector3(-1.3f + i * 0.325f, 0.75f, 0.455f), new Vector3(0.03f, 1.1f, 0.01f), new Color("#c9cfd4"));
                Box(n, new Vector3(0, 1.38f, 0), new Vector3(2.92f, 0.06f, 0.92f), Brand.Power);
                Box(n, new Vector3(1.0f, 0.85f, 0.465f), new Vector3(0.5f, 0.4f, 0.02f), new Color("#3b4148"));
                Box(n, new Vector3(-1.1f, 1.0f, 0.465f), new Vector3(0.12f, 0.05f, 0.01f), Brand.Ok, glow: true);
                Box(n, new Vector3(-0.95f, 1.0f, 0.465f), new Vector3(0.12f, 0.05f, 0.01f), Brand.Power, glow: true);
                break;
            case ObjKind.DryCooler:
            {
                // a low frame with two big fans on top: rejects heat with air, no water
                Box(n, new Vector3(0, 0.1f, 0), new Vector3(1.95f, 0.2f, 0.95f), new Color("#9ea3a8"));
                Box(n, new Vector3(0, 0.55f, 0), new Vector3(1.85f, 0.7f, 0.85f), new Color("#dfe5e9"), 0.5f);
                for (var i = 0; i < 7; i++) Box(n, new Vector3(-0.78f + i * 0.26f, 0.55f, 0.43f), new Vector3(0.03f, 0.6f, 0.01f), Brand.Cooling);
                foreach (var x in new[] { -0.47f, 0.47f })
                {
                    Cyl(n, new Vector3(x, 0.93f, 0), 0.38f, 0.08f, new Color("#3d464f"));
                    var blade = Box(n, new Vector3(x, 0.98f, 0), new Vector3(0.66f, 0.02f, 0.1f), new Color("#8f989f")); blade.Name = "Fan";
                }
                break;
            }
            case ObjKind.PalletRack:
            {
                // orange uprights, blue beams, boxes on pallets on three levels
                foreach (var x in new[] { -1.45f, 0f, 1.45f }) foreach (var z in new[] { -0.4f, 0.4f })
                    Box(n, new Vector3(x, 1.2f, z), new Vector3(0.07f, 2.4f, 0.07f), Brand.Fiber);
                for (var lvl = 0; lvl < 3; lvl++)
                {
                    var y = 0.1f + lvl * 0.8f;
                    foreach (var z in new[] { -0.4f, 0.4f }) Box(n, new Vector3(0, y, z), new Vector3(2.95f, 0.08f, 0.06f), new Color("#2f5fa0"));
                    foreach (var x in new[] { -0.72f, 0.72f })
                    {
                        Box(n, new Vector3(x, y + 0.08f, 0), new Vector3(1.1f, 0.08f, 0.75f), Wood);
                        if ((lvl + (x > 0 ? 1 : 0)) % 3 != 2) Box(n, new Vector3(x, y + 0.38f, 0), new Vector3(1.0f, 0.5f, 0.68f), new Color("#c79a5b"));
                    }
                }
                break;
            }
            case ObjKind.Workbench:
                // ESD workbench with a server on it being prepared, a monitor and a parts tray
                Box(n, new Vector3(0, 0.85f, 0), new Vector3(1.9f, 0.06f, 0.8f), new Color("#4b6b88"));
                foreach (var x in new[] { -0.88f, 0.88f }) Box(n, new Vector3(x, 0.42f, 0), new Vector3(0.08f, 0.84f, 0.72f), Steel);
                Box(n, new Vector3(-0.2f, 0.95f, 0.05f), new Vector3(0.9f, 0.12f, 0.6f), Brand.Rack, 0.5f, 0.4f);
                Box(n, new Vector3(-0.2f, 0.96f, 0.355f), new Vector3(0.84f, 0.08f, 0.01f), new Color("#55606b"));
                Box(n, new Vector3(0.05f, 0.97f, 0.36f), new Vector3(0.04f, 0.02f, 0.01f), Brand.Ok, glow: true);
                Box(n, new Vector3(0.6f, 1.12f, -0.2f), new Vector3(0.5f, 0.32f, 0.03f), Dark);
                Box(n, new Vector3(0.6f, 1.12f, -0.18f), new Vector3(0.46f, 0.28f, 0.01f), new Color("#7fa6b8"), glow: true);
                Box(n, new Vector3(0.65f, 0.9f, 0.2f), new Vector3(0.4f, 0.04f, 0.3f), new Color("#ffd23e"));
                break;
            case ObjKind.PalletJack:
                // hand pallet truck: forks, the pump and a long handle (no forklifts on this site)
                foreach (var x in new[] { -0.15f, 0.15f }) Box(n, new Vector3(x, 0.05f, 0.05f), new Vector3(0.12f, 0.08f, 0.8f), Brand.Fiber);
                Box(n, new Vector3(0, 0.18f, -0.38f), new Vector3(0.42f, 0.26f, 0.12f), Brand.Fiber);
                var handle = Box(n, new Vector3(0, 0.6f, -0.48f), new Vector3(0.04f, 0.8f, 0.04f), Dark); handle.RotationDegrees = new Vector3(-18, 0, 0);
                Box(n, new Vector3(0, 0.98f, -0.6f), new Vector3(0.28f, 0.05f, 0.05f), Dark);
                break;
            case ObjKind.Shredder:
                // disk shredder: a heavy box with a hopper and a green status light
                Box(n, new Vector3(0, 0.5f, 0), new Vector3(0.8f, 1.0f, 0.8f), new Color("#3b4652"), 0.5f, 0.3f);
                Box(n, new Vector3(0, 1.08f, 0.05f), new Vector3(0.6f, 0.16f, 0.5f), new Color("#55606b"));
                Box(n, new Vector3(0, 0.75f, 0.405f), new Vector3(0.5f, 0.12f, 0.01f), Brand.Power);
                Box(n, new Vector3(0.28f, 0.92f, 0.405f), new Vector3(0.06f, 0.06f, 0.01f), Brand.Ok, glow: true);
                break;
            case ObjKind.SocDesk:
                // analyst desk with three screens of dashboards
                Box(n, new Vector3(0, 0.75f, 0.1f), new Vector3(1.9f, 0.05f, 0.75f), new Color("#2b3036"));
                foreach (var x in new[] { -0.85f, 0.85f }) Box(n, new Vector3(x, 0.37f, 0.1f), new Vector3(0.06f, 0.74f, 0.7f), Dark);
                for (var i = -1; i <= 1; i++)
                {
                    var scr = Box(n, new Vector3(i * 0.58f, 1.08f, -0.18f), new Vector3(0.55f, 0.34f, 0.03f), Dark);
                    scr.RotationDegrees = new Vector3(0, -i * 14, 0);
                    var glow = Box(n, new Vector3(i * 0.58f, 1.08f, -0.16f), new Vector3(0.5f, 0.3f, 0.01f), i == 0 ? Brand.Critical : Brand.Cooling, glow: true);
                    glow.RotationDegrees = new Vector3(0, -i * 14, 0);
                }
                Box(n, new Vector3(0, 0.45f, 0.75f), new Vector3(0.5f, 0.08f, 0.5f), Dark);
                break;
            case ObjKind.CoolingTower:
                Box(n, new Vector3(0, 0.15f, 0), new Vector3(1.95f, 0.3f, 1.95f), new Color("#9ea3a8"));
                Box(n, new Vector3(0, 1.35f, 0), new Vector3(1.8f, 2.1f, 1.8f), new Color("#d6dde2"), 0.6f);
                for (var i = 0; i < 5; i++) Box(n, new Vector3(0, 0.6f + i * 0.3f, 0.905f), new Vector3(1.6f, 0.06f, 0.02f), new Color("#a9b4bc"));
                Cyl(n, new Vector3(0, 2.75f, 0), 0.8f, 0.7f, new Color("#c2ccd3"), 0.7f);
                var fan = Box(n, new Vector3(0, 3.05f, 0), new Vector3(1.3f, 0.03f, 0.16f), new Color("#5d6670")); fan.Name = "Fan";
                Box(n, new Vector3(0, 0.45f, 0.92f), new Vector3(1.8f, 0.1f, 0.02f), Brand.Cooling, glow: true);
                break;
        }
        return n;
    }

    /// <summary>The game's own equipment and street models (tools/models/blender/infra.py) per object kind.</summary>
    static readonly Dictionary<ObjKind, string> MadeModels = new()
    {
        [ObjKind.Generator] = "generator", [ObjKind.Transformer] = "transformer", [ObjKind.Switchgear] = "switchgear",
        [ObjKind.Ups] = "ups", [ObjKind.Battery] = "battery", [ObjKind.Pdu] = "pdu", [ObjKind.DieselTank] = "diesel_tank",
        [ObjKind.UndergroundTank] = "underground_tank", [ObjKind.Chiller] = "chiller", [ObjKind.DryCooler] = "dry_cooler",
        [ObjKind.CoolingTower] = "cooling_tower", [ObjKind.Crah] = "crah", [ObjKind.CoreRouter] = "core_router",
        [ObjKind.Spine] = "spine", [ObjKind.Odf] = "odf", [ObjKind.BatteryContainer] = "battery_container",
        [ObjKind.LampPost] = "lamp_post", [ObjKind.Bench] = "bench", [ObjKind.BikeRack] = "bike_rack",
        [ObjKind.Bollard] = "bollard", [ObjKind.CameraPole] = "camera_pole",
        [ObjKind.Tree] = "tree_young", [ObjKind.Shrub] = "bush_round",
        [ObjKind.GuardBooth] = "guard_booth", [ObjKind.Intercom] = "intercom", [ObjKind.PedGate] = "ped_gate", [ObjKind.SolarPanel] = "solar_panel",
        [ObjKind.PalletRack] = "pallet_rack", [ObjKind.Workbench] = "workbench", [ObjKind.PalletJack] = "pallet_jack", [ObjKind.Shredder] = "shredder",
    };

    /// <summary>Glowing brand colours and lamp heads of the made models.</summary>
    public static Material MadeMat(string name, Material m) => name switch
    {
        "glow_power" => Mat(Brand.Power, 0.4f, 0, true),
        "glow_cooling" => Mat(Brand.Cooling, 0.4f, 0, true),
        "glow_fiber" => Mat(Brand.Fiber, 0.4f, 0, true),
        "glow_ok" => Mat(Brand.Ok, 0.4f, 0, true),
        "glow_alert" => Mat(Brand.Critical, 0.4f, 0, true),
        "lamp" => LampMat,
        "window_glass" => World.WorldView.WindowMat,
        _ => Foliage.ByName(name, m),
    };

    static bool Made(ObjKind k, Node3D n)
    {
        if (MadeMoving(k, n)) return true;
        if (!MadeModels.TryGetValue(k, out var file) || !Assets.Mount(n, "made/" + file + ".glb", MadeMat)) return false;
        if (k == ObjKind.LampPost)
        {
            var lamp = new OmniLight3D { Position = new Vector3(0, 4.7f, 0.65f), OmniRange = 11, LightColor = new Color("#ffd9a0"), LightEnergy = 0, ShadowEnabled = false, Visible = false };
            lamp.AddToGroup(NightLightGroup);
            n.AddChild(lamp);
        }
        return true;
    }

    /// <summary>Own models with a moving part on the node the world view animates: barrier "Arm", gate "Leaf", turbine "FanRotor".</summary>
    static bool MadeMoving(ObjKind k, Node3D n)
    {
        switch (k)
        {
            case ObjKind.Barrier:
                if (!Assets.Has("made/barrier_arm.glb")) return false;
                Assets.Place(n, "made/barrier_post.glb", Vector3.Zero, 1, 0, MadeMat);
                var arm = new Node3D { Name = "Arm", Position = new Vector3(-1.3f, 0.9f, 0) }; n.AddChild(arm);
                Assets.Place(arm, "made/barrier_arm.glb", Vector3.Zero, 1, 0, MadeMat);
                return true;
            case ObjKind.SlidingGate:
            case ObjKind.SlidingGateLarge:
                var cells = k == ObjKind.SlidingGate ? 3 : 6;
                if (!Assets.Has($"made/gate_leaf_{cells}.glb")) return false;
                Assets.Place(n, $"made/gate_posts_{cells}.glb", Vector3.Zero, 1, 0, MadeMat);
                var leaf = new Node3D { Name = "Leaf" }; n.AddChild(leaf);
                Assets.Place(leaf, $"made/gate_leaf_{cells}.glb", Vector3.Zero, 1, 0, MadeMat);
                return true;
            case ObjKind.WindTurbine:
                if (!Assets.Has("made/wind_rotor.glb")) return false;
                Assets.Place(n, "made/wind_tower.glb", Vector3.Zero, 1, 0, MadeMat);
                var hub = new Node3D { Name = "FanRotor", Position = new Vector3(0, 8.45f, 0.5f), RotationDegrees = new Vector3(90, 0, 0) }; n.AddChild(hub);
                Assets.Place(hub, "made/wind_rotor.glb", Vector3.Zero, 1, 0, MadeMat);
                return true;
        }
        return false;
    }

    const float Kf = 0.2f; // Kenney Furniture Kit units to metres
    static MeshInstance3D F(Node3D n, string name, Vector3 pos, float yaw = 0) => Assets.Centered(n, "furniture/" + name + ".obj", pos, Kf, yaw);
    static float FTop(string name) => Assets.Top("furniture/" + name + ".obj", Kf);

    /// <summary>Height of a desk top, for things that stand on it.</summary>
    public static float DeskTop => Assets.Has("furniture/desk.obj") ? FTop("desk") : 0.77f;

    /// <summary>
    /// Step 11: the "Datacenter of the year" statuette. A small rack-shaped column on a dark plinth, with a glowing orb on top:
    /// gold for the winner, silver and bronze for second and third place.
    /// </summary>
    public static Node3D Statuette(int place)
    {
        var n = new Node3D { Name = "Statuette" };
        var metal = place switch { 1 => new Color("#e8b84a"), 2 => new Color("#c9d0d6"), _ => new Color("#b9774a") };
        Box(n, new Vector3(0, 0.02f, 0), new Vector3(0.11f, 0.04f, 0.11f), Dark, 0.4f);
        Box(n, new Vector3(0, 0.045f, 0), new Vector3(0.09f, 0.01f, 0.09f), metal, 0.25f, 0.9f);
        Box(n, new Vector3(0, 0.12f, 0), new Vector3(0.045f, 0.15f, 0.045f), metal, 0.22f, 0.95f);
        for (var i = 0; i < 3; i++) Box(n, new Vector3(0, 0.07f + i * 0.045f, 0.0235f), new Vector3(0.035f, 0.006f, 0.002f), Dark, 0.5f);
        Ball(n, new Vector3(0, 0.215f, 0), 0.028f, metal);
        Box(n, new Vector3(0, 0.215f, 0), new Vector3(0.012f, 0.012f, 0.012f), new Color("#fff4d6"), glow: true);
        return n;
    }

    /// <summary>Office, canteen and lounge furniture from the CC0 Kenney Furniture Kit, when the model files are there.</summary>
    static bool Furnished(ObjKind k, Node3D n)
    {
        if (!Assets.Has("furniture/desk.obj")) return false;
        switch (k)
        {
            case ObjKind.Desk:
                F(n, "desk", Vector3.Zero, 180);
                var top = FTop("desk");
                F(n, "computerScreen", new Vector3(0.3f, top, -0.15f), 180);
                F(n, "computerKeyboard", new Vector3(0.3f, top, 0.12f), 180);
                if (Assets.Has("furniture/laptop.obj")) F(n, "laptop", new Vector3(-0.5f, top, 0.05f), 160);
                return true;
            case ObjKind.Chair: F(n, "chairDesk", Vector3.Zero, 180); return true;
            case ObjKind.Plant: F(n, "pottedPlant", Vector3.Zero); return true;
            case ObjKind.Cabinet: F(n, "bookcaseClosed", Vector3.Zero, 180); return true;
            case ObjKind.MeetingTable:
                F(n, "table", Vector3.Zero);
                foreach (var x in new[] { -0.8f, 0f, 0.8f })
                {
                    F(n, "chair", new Vector3(x, 0, -0.85f), 0);
                    F(n, "chair", new Vector3(x, 0, 0.85f), 180);
                }
                return true;
            case ObjKind.Microwave:
                F(n, "kitchenCabinet", Vector3.Zero, 180);
                F(n, "kitchenMicrowave", new Vector3(0, FTop("kitchenCabinet"), -0.05f), 180);
                return true;
            case ObjKind.Coffee:
                F(n, "kitchenCabinet", Vector3.Zero, 180);
                F(n, "kitchenCoffeeMachine", new Vector3(0.1f, FTop("kitchenCabinet"), -0.05f), 180);
                return true;
            case ObjKind.Sofa: F(n, "loungeSofa", Vector3.Zero, 180); return true;
        }
        return false;
    }

    /// <summary>The grid operator's connection on the site, growing with the contracted capacity (2, 10, 20 or 50 MVA).</summary>
    public static Node3D GridStation(int level, float w, float d)
    {
        var n = new Node3D();
        var wall = new Color("#d6dce0");
        switch (level)
        {
            case 0:
                Box(n, new Vector3(0, 1.4f, 0), new Vector3(w - 0.2f, 2.8f, d - 0.2f), wall);
                Box(n, new Vector3(0, 2.9f, 0), new Vector3(w, 0.25f, d), Brand.Power);
                break;
            case 1:
                Box(n, new Vector3(0, 1.7f, 0), new Vector3(w - 0.1f, 3.4f, d - 0.1f), new Color("#b5654a"));
                Box(n, new Vector3(0, 3.5f, 0), new Vector3(w + 0.2f, 0.25f, d + 0.2f), new Color("#4b535b"));
                Box(n, new Vector3(0, 3.1f, d / 2f), new Vector3(w, 0.15f, 0.02f), Brand.Power, glow: true);
                for (var i = 0; i < 3; i++) Box(n, new Vector3(-w / 2f + 0.7f + i * 1.2f, 2.6f, d / 2f + 0.01f), new Vector3(0.7f, 0.4f, 0.02f), new Color("#8f989f"));
                break;
            default:
                // switch house with an outdoor transformer yard behind a fence, portals for the incoming lines
                Box(n, new Vector3(0, 0.05f, 0), new Vector3(w, 0.1f, d), new Color("#b9b2a6"));
                Box(n, new Vector3(-w / 4f, 1.6f, 0), new Vector3(w / 2f - 0.1f, 3.2f, d - 0.2f), wall);
                Box(n, new Vector3(-w / 4f, 3.3f, 0), new Vector3(w / 2f + 0.1f, 0.2f, d), Brand.Power);
                var t = Object(ObjKind.Transformer);
                t.Position = new Vector3(w / 4f, 0, 0); n.AddChild(t);
                var poles = level >= 3 ? 9f : 6.5f;
                foreach (var z in new[] { -d / 2f + 0.2f, d / 2f - 0.2f })
                    Box(n, new Vector3(w / 4f + 0.9f, poles / 2, z), new Vector3(0.18f, poles, 0.18f), new Color("#7c858d"), 0.5f, 0.6f);
                Box(n, new Vector3(w / 4f + 0.9f, poles, 0), new Vector3(0.2f, 0.2f, d - 0.2f), new Color("#7c858d"), 0.5f, 0.6f);
                if (level >= 3)
                    foreach (var z in new[] { -d / 2f + 0.2f, d / 2f - 0.2f })
                        Box(n, new Vector3(-w / 2f + 0.3f, poles * 0.7f / 2, z), new Vector3(0.18f, poles * 0.7f, 0.18f), new Color("#7c858d"), 0.5f, 0.6f);
                // fence around the transformer yard
                Box(n, new Vector3(w / 4f, 1.0f, d / 2f), new Vector3(w / 2f, 2f, 0.04f), new Color("#a9b2b8", 0.55f));
                Box(n, new Vector3(w / 2f, 1.0f, 0), new Vector3(0.04f, 2f, d), new Color("#a9b2b8", 0.55f));
                break;
        }
        return n;
    }

    // ---------------- people and vehicles ----------------
    /// <summary>A person: a rigged CC0 model dressed for the role (see <see cref="People"/>).</summary>
    public static Node3D Person(Role role, int id, StaffRole staff = StaffRole.Support) => People.Create(role, id, staff);

    /// <summary>
    /// A small cable reel held in both hands: wooden flanges, the wound cable in its brand colour and a loose end.
    /// "Drum" turns while the cable unrolls and "Lead" is the cable run to the job, shown while working.
    /// </summary>
    internal static void CableReel(Node3D person, string name, Color cable, float size)
    {
        var reel = new Node3D { Name = name, Position = new Vector3(0, 0.76f, 0.4f), Visible = false }; person.AddChild(reel);
        var drum = new Node3D { Name = "Drum" }; reel.AddChild(drum);
        var w = size * 0.75f;
        foreach (var x in new[] { -w / 2, w / 2 })
            Cyl(drum, new Vector3(x, 0, 0), size / 2, 0.025f, new Color("#c79a5b")).RotationDegrees = new Vector3(0, 0, 90);
        Cyl(drum, Vector3.Zero, size * 0.4f, w, cable).RotationDegrees = new Vector3(0, 0, 90);
        Cyl(drum, Vector3.Zero, size * 0.12f, w + 0.06f, Dark).RotationDegrees = new Vector3(0, 0, 90);
        var lead = Box(reel, new Vector3(0, -0.02f, 0.32f), new Vector3(0.022f, 0.022f, 0.6f), cable);
        lead.Name = "Lead"; lead.RotationDegrees = new Vector3(-22, 0, 0); lead.Visible = false;
    }

    /// <summary>
    /// A cantilever sliding gate as wide as its footprint: posts at both ends, a motor box, and a leaf
    /// named "Leaf" that slides sideways along the fence to open the full width of the road.
    /// </summary>
    static void SlidingGate(Node3D n, int cells)
    {
        var half = cells / 2f;
        var span = cells - 0.12f;
        var h = cells > 3 ? 2.6f : 2.4f;
        Box(n, new Vector3(-half + 0.07f, h / 2, 0), new Vector3(0.16f, h, 0.16f), Dark);
        Box(n, new Vector3(half - 0.07f, h / 2, 0), new Vector3(0.16f, h, 0.16f), Dark);
        // guide posts and motor next to the opening, on the side the leaf slides to
        Box(n, new Vector3(half + 0.25f, 0.35f, 0.25f), new Vector3(0.35f, 0.7f, 0.3f), new Color("#3b4652"));
        Box(n, new Vector3(half - 0.07f, h + 0.1f, 0), new Vector3(0.22f, 0.14f, 0.22f), Brand.Power, glow: true);
        var leaf = new Node3D { Name = "Leaf" }; n.AddChild(leaf);
        Box(leaf, new Vector3(0, h - 0.2f, 0.12f), new Vector3(span, 0.1f, 0.08f), Dark);
        Box(leaf, new Vector3(0, 0.18f, 0.12f), new Vector3(span, 0.16f, 0.1f), Dark);
        var bars = (int)(span / 0.22f);
        for (var i = 0; i <= bars; i++) Box(leaf, new Vector3(-span / 2 + i * span / bars, (h - 0.1f) / 2 + 0.1f, 0.12f), new Vector3(0.045f, h - 0.4f, 0.045f), Dark);
        // diagonal brace and amber warning strip so the gate reads clearly from above
        var brace = Box(leaf, new Vector3(0, (h - 0.1f) / 2 + 0.1f, 0.12f), new Vector3(Mathf.Sqrt(span * span + h * h) * 0.92f, 0.07f, 0.05f), Dark);
        brace.RotationDegrees = new Vector3(0, 0, Mathf.RadToDeg(Mathf.Atan2(h - 0.4f, span)));
        Box(leaf, new Vector3(0, h - 0.1f, 0.12f), new Vector3(span, 0.06f, 0.09f), Brand.Power);
    }

    /// <summary>Length of the trailer behind the kingpin, in metres (the truck's own position is the kingpin over the drive axle).</summary>
    public const float TrailerLength = (float)Sim.Simulation.TruckRear;

    /// <summary>
    /// An articulated lorry: a cab-over tractor with a high roof, black grille, side skirts and a fifth wheel, pulling a
    /// semi-trailer on three axles. The node "Trailer" pivots at the kingpin (the origin) so the trailer follows the
    /// tractor through bends. Material: flatbed with pallets; equipment: white box; customer: removal box with open doors.
    /// </summary>
    public static Node3D Truck(TruckLoad load)
    {
        if (MadeTruck(load) is { } made) return made;
        var n = new Node3D();
        var cab = load == TruckLoad.Material ? new Color("#f2a33a") : load == TruckLoad.Customer ? new Color("#3b4f66") : new Color("#e9edf0");
        var glass = new Color("#2c4359");
        var trim = new Color("#23272c");
        var tyre = new Color("#1e2226");
        void Wheel(Node3D p, float x, float z, float r = 0.5f)
        {
            var w = Cyl(p, new Vector3(x, r, z), r, 0.36f, tyre); w.RotationDegrees = new Vector3(0, 0, 90);
            var hub = Cyl(p, new Vector3(x * 1.02f, r, z), r * 0.45f, 0.38f, new Color("#b9c2c8")); hub.RotationDegrees = new Vector3(0, 0, 90);
        }

        // ---- tractor ----
        Box(n, new Vector3(0, 0.62f, 0.9f), new Vector3(1.1f, 0.24f, 4.6f), trim);                       // chassis rails
        Cyl(n, new Vector3(0, 0.8f, 0), 0.55f, 0.1f, trim);                                               // fifth wheel
        Box(n, new Vector3(0, 1.55f, 2.6f), new Vector3(2.4f, 1.9f, 1.5f), cab, 0.4f);                   // cab
        Box(n, new Vector3(0, 2.75f, 2.5f), new Vector3(2.36f, 0.5f, 1.7f), cab, 0.4f);                  // high roof
        var spoiler = Box(n, new Vector3(0, 3.2f, 2.0f), new Vector3(2.3f, 0.5f, 0.9f), cab, 0.4f);      // roof spoiler towards the trailer
        spoiler.RotationDegrees = new Vector3(14, 0, 0);
        Box(n, new Vector3(0, 2.9f, 3.36f), new Vector3(2.3f, 0.16f, 0.12f), cab.Darkened(0.1f), 0.4f);  // sun visor
        Box(n, new Vector3(0, 2.2f, 3.36f), new Vector3(2.1f, 0.85f, 0.03f), glass, 0.15f, 0.2f);        // windscreen
        Box(n, new Vector3(0, 1.2f, 3.36f), new Vector3(1.7f, 1.0f, 0.04f), trim, 0.5f, 0.3f);           // black grille
        for (var i = 0; i < 4; i++) Box(n, new Vector3(0, 0.85f + i * 0.2f, 3.385f), new Vector3(1.55f, 0.05f, 0.02f), new Color("#4a525a"), 0.4f, 0.5f);
        Box(n, new Vector3(0, 0.55f, 3.33f), new Vector3(2.42f, 0.42f, 0.16f), cab, 0.45f);              // bumper
        foreach (var x in new[] { -1.21f, 1.21f })
        {
            Box(n, new Vector3(x, 2.1f, 2.75f), new Vector3(0.02f, 0.75f, 0.85f), glass, 0.15f, 0.2f);   // side window
            Box(n, new Vector3(x * 1.12f, 2.05f, 3.15f), new Vector3(0.07f, 0.5f, 0.14f), trim);         // mirror
            Box(n, new Vector3(x * 0.93f, 1.08f, 2.5f), new Vector3(0.4f, 0.14f, 1.2f), trim);           // front mudguard
            Box(n, new Vector3(x * 0.96f, 0.82f, 0.95f), new Vector3(0.12f, 0.6f, 1.5f), cab, 0.45f);    // side skirt over the tanks
            Box(n, new Vector3(x * 0.93f, 1.08f, 0f), new Vector3(0.4f, 0.1f, 1.1f), trim);              // rear mudguard
            Box(n, new Vector3(x * 0.98f, 0.62f, 1.85f), new Vector3(0.14f, 0.5f, 0.3f), trim);          // step
            Box(n, new Vector3(x * 0.82f, 0.62f, 3.42f), new Vector3(0.36f, 0.14f, 0.03f), new Color("#fff6d8")).MaterialOverride = World.WorldView.HeadlightMat;
            Wheel(n, x * 0.88f, 2.45f);
            Wheel(n, x * 0.88f, 0f);
        }
        Box(n, new Vector3(0, 2.62f, 3.38f), new Vector3(1.2f, 0.06f, 0.02f), Brand.Cooling, glow: true); // light strip above the screen

        // ---- semi-trailer, pivoting at the kingpin ----
        var tr = new Node3D { Name = "Trailer" }; n.AddChild(tr);
        float front = 0.9f, rear = -TrailerLength, len = front - rear, mid = (front + rear) / 2;
        Box(tr, new Vector3(0, 1.08f, mid), new Vector3(1.0f, 0.22f, len), trim);                         // trailer frame
        foreach (var z in new[] { rear + 2.1f, rear + 1.3f, rear + 0.5f + 0.0f })
            foreach (var x in new[] { -0.92f, 0.92f }) Wheel(tr, x, z + 0.0f);
        foreach (var x in new[] { -1.05f, 1.05f })
        {
            Box(tr, new Vector3(x, 0.72f, rear + 4.2f), new Vector3(0.05f, 0.4f, 3.0f), new Color("#9aa4ab"), 0.5f, 0.5f); // side guard
            Box(tr, new Vector3(x, 1.0f, rear + 1.3f), new Vector3(0.36f, 0.1f, 2.5f), trim);            // mudguards over the axles
            Box(tr, new Vector3(x * 0.75f, 0.45f, rear + 5.6f), new Vector3(0.08f, 0.8f, 0.08f), new Color("#9aa4ab")); // landing legs
            Box(tr, new Vector3(x * 1.1f, 0.88f, rear + 0.05f), new Vector3(0.22f, 0.14f, 0.03f), Brand.Critical, glow: true);
        }
        Box(tr, new Vector3(0, 0.62f, rear - 0.02f), new Vector3(2.2f, 0.12f, 0.08f), trim);              // rear under-run bar

        if (load == TruckLoad.Material)
        {
            // flatbed with a headboard and pallets of building material
            Box(tr, new Vector3(0, 1.28f, mid), new Vector3(2.5f, 0.18f, len), new Color("#59626b"), 0.6f, 0.3f);
            foreach (var x in new[] { -1.24f, 1.24f }) Box(tr, new Vector3(x, 1.5f, mid), new Vector3(0.05f, 0.3f, len), cab.Darkened(0.2f));
            Box(tr, new Vector3(0, 1.95f, front - 0.06f), new Vector3(2.5f, 1.2f, 0.1f), new Color("#59626b"), 0.6f, 0.3f);
            var cargo = new Node3D { Name = "Cargo" }; tr.AddChild(cargo);
            for (var i = 0; i < 6; i++)
            {
                var z = rear + 0.9f + i * 1.4f;
                Box(cargo, new Vector3(0, 1.43f, z), new Vector3(2.1f, 0.14f, 1.15f), Wood);
                Box(cargo, new Vector3(0, 1.85f, z), new Vector3(1.9f, 0.7f, 1.05f), i % 2 == 0 ? new Color("#c9c3b8") : new Color("#b5785a"));
            }
        }
        else
        {
            // a long box: white for equipment with an amber line, a removal box with open rear doors for customers
            var white = new Color("#f2f4f6");
            Box(tr, new Vector3(0, 2.6f, mid), new Vector3(2.5f, 2.8f, len), white, 0.55f);
            Box(tr, new Vector3(0, 1.22f, mid), new Vector3(2.52f, 0.08f, len), new Color("#8d969d"), 0.4f, 0.5f); // bottom rail
            Box(tr, new Vector3(0, 4.0f, mid), new Vector3(2.52f, 0.06f, len), new Color("#d8dde1"));               // top rail
            for (var z = rear + 1.2f; z < front - 0.5f; z += 1.2f)
                foreach (var x in new[] { -1.26f, 1.26f }) Box(tr, new Vector3(x, 2.6f, z), new Vector3(0.015f, 2.7f, 0.04f), new Color("#dfe3e6"));
            if (load == TruckLoad.Equipment)
                foreach (var x in new[] { -1.262f, 1.262f }) Box(tr, new Vector3(x, 1.5f, mid), new Vector3(0.01f, 0.18f, len - 0.4f), Brand.Power);
            else
            {
                foreach (var x in new[] { -1.262f, 1.262f })
                {
                    Box(tr, new Vector3(x, 1.7f, mid), new Vector3(0.01f, 0.35f, len - 0.4f), Brand.Cooling);
                    Box(tr, new Vector3(x, 3.6f, mid), new Vector3(0.01f, 0.2f, len - 0.4f), Brand.Fiber);
                }
                Box(tr, new Vector3(0, 2.6f, rear - 0.01f), new Vector3(2.3f, 2.6f, 0.02f), new Color("#20262c"));
                foreach (var x in new[] { -1.25f, 1.25f })
                {
                    var door = new Node3D { Position = new Vector3(x, 0, rear - 0.02f), RotationDegrees = new Vector3(0, x < 0 ? 100 : -100, 0) }; tr.AddChild(door);
                    Box(door, new Vector3(x < 0 ? 0.62f : -0.62f, 2.6f, 0), new Vector3(1.24f, 2.7f, 0.04f), new Color("#e3e7ea"));
                }
                var cargo = new Node3D { Name = "Cargo" }; tr.AddChild(cargo);
                for (var i = 0; i < 3; i++)
                    foreach (var x in new[] { -0.55f, 0.55f })
                    {
                        Box(cargo, new Vector3(x, 2.25f, rear + 0.8f + i * 1.0f), new Vector3(0.8f, 1.9f, 0.8f), Brand.Rack, 0.5f, 0.3f);
                        for (var u = 0; u < 5; u++) Box(cargo, new Vector3(x + 0.2f, 1.5f + u * 0.35f, rear + 0.39f + i * 1.0f), new Vector3(0.06f, 0.025f, 0.01f), u % 2 == 0 ? Brand.Ok : Brand.Cooling, glow: true);
                    }
            }
        }
        return n;
    }

    /// <summary>Stack of building material on a pallet.</summary>
    public static Node3D Pallet(int i)
    {
        var n = new Node3D();
        if (Assets.Place(n, "made/pallet.glb", Vector3.Zero, 1, i * 37 % 4 * 90 + i % 3 * 7, i % 2 == 0 ? MadeMat : BlockPallet) != null) return n;
        Box(n, new Vector3(0, 0.07f, 0), new Vector3(0.9f, 0.14f, 0.9f), Wood);
        Box(n, new Vector3(0, 0.42f, 0), new Vector3(0.82f, 0.56f, 0.82f), i % 3 == 0 ? new Color("#b5785a") : i % 3 == 1 ? new Color("#c9c3b8") : new Color("#9aa4ab"));
        return n;
    }

    /// <summary>Tower crane: Mast stays, "Jib" rotates.</summary>
    public static Node3D Crane(float height, float reach)
    {
        if (MadeCrane(height, reach) is { } made) return made;
        // a mobile self-erecting tower crane: a three-axle carrier on outriggers, a lattice mast with the cab halfway up,
        // and a lattice jib held by tie bars from the mast top, with a trolley and hook. "Jib" (mast and jib) slews.
        var n = new Node3D();
        var yellow = new Color("#f5c400");
        var black = new Color("#24282d");
        // ---- carrier ----
        Box(n, new Vector3(0, 1.05f, 0), new Vector3(2.5f, 0.6f, 9.0f), yellow, 0.5f);
        Box(n, new Vector3(0, 0.8f, 0), new Vector3(1.2f, 0.3f, 9.0f), black);
        Box(n, new Vector3(0, 1.75f, 3.9f), new Vector3(2.5f, 1.3f, 1.4f), yellow, 0.5f);               // driver's cab
        Box(n, new Vector3(0, 1.95f, 4.61f), new Vector3(2.2f, 0.7f, 0.02f), new Color("#2c4359"), 0.15f, 0.2f);
        for (var i = 0; i < 6; i++)                                                                    // hazard stripes on the bumpers
            foreach (var z in new[] { -4.52f, 4.62f })
                Box(n, new Vector3(-1.05f + i * 0.42f, 0.75f, z), new Vector3(0.2f, 0.3f, 0.02f), i % 2 == 0 ? black : yellow).RotationDegrees = new Vector3(0, 0, 35);
        foreach (var z in new[] { 3.4f, -1.6f, -3.2f })
            foreach (var x in new[] { -1.1f, 1.1f })
            {
                var wh = Cyl(n, new Vector3(x, 0.62f, z), 0.62f, 0.5f, new Color("#1e2226")); wh.RotationDegrees = new Vector3(0, 0, 90);
                Cyl(n, new Vector3(x * 1.03f, 0.62f, z), 0.28f, 0.52f, new Color("#9aa4ab")).RotationDegrees = new Vector3(0, 0, 90);
            }
        // outriggers swung out to pads on the ground
        foreach (var (x, z) in new[] { (-1f, 1f), (1f, 1f), (-1f, -1f), (1f, -1f) })
        {
            Box(n, new Vector3(x * 2.0f, 1.0f, z * 3.0f), new Vector3(2.0f, 0.22f, 0.25f), yellow, 0.5f);
            Box(n, new Vector3(x * 2.9f, 0.55f, z * 3.0f), new Vector3(0.18f, 0.9f, 0.18f), black);
            Box(n, new Vector3(x * 2.9f, 0.06f, z * 3.0f), new Vector3(0.8f, 0.12f, 0.8f), new Color("#c79a5b"));
        }
        // ---- slewing part: turntable, counterweight, mast, cab, jib ----
        var up = new Node3D { Name = "Jib", Position = new Vector3(0, 1.4f, -2.2f) }; n.AddChild(up);
        Cyl(up, new Vector3(0, 0.1f, 0), 1.1f, 0.2f, black);
        Box(up, new Vector3(0, 0.6f, -1.2f), new Vector3(2.2f, 0.9f, 1.4f), new Color("#59626b"), 0.7f); // ballast blocks
        Box(up, new Vector3(0, 0.35f, 0.3f), new Vector3(1.6f, 0.4f, 1.6f), yellow, 0.5f);
        const float w = 0.9f;                     // mast width
        var mh = height;
        foreach (var (x, z) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) })
            Box(up, new Vector3(x * w / 2, mh / 2 + 0.5f, z * w / 2), new Vector3(0.09f, mh, 0.09f), yellow);
        var seg = 1.5f; var diag = Mathf.Sqrt(seg * seg + w * w); var ang = Mathf.RadToDeg(Mathf.Atan2(seg, w));
        for (var y = 0.5f; y < mh + 0.4f; y += seg)
        {
            foreach (var z in new[] { -w / 2, w / 2 }) Box(up, new Vector3(0, y, z), new Vector3(w, 0.05f, 0.05f), yellow);
            foreach (var x in new[] { -w / 2, w / 2 }) Box(up, new Vector3(x, y, 0), new Vector3(0.05f, 0.05f, w), yellow);
            if (y + seg > mh + 0.5f) break;
            var k = (int)(y / seg) % 2 == 0 ? 1 : -1;
            foreach (var z in new[] { -w / 2, w / 2 }) Box(up, new Vector3(0, y + seg / 2, z), new Vector3(diag, 0.04f, 0.04f), yellow).RotationDegrees = new Vector3(0, 0, k * ang);
            foreach (var x in new[] { -w / 2, w / 2 }) Box(up, new Vector3(x, y + seg / 2, 0), new Vector3(0.04f, 0.04f, diag), yellow).RotationDegrees = new Vector3(-k * ang, 0, 0);
        }
        // the operator's cab hangs on the side of the mast, about halfway
        var cy = Mathf.Max(5f, mh * 0.55f);
        Box(up, new Vector3(0.95f, cy, 0.2f), new Vector3(1.0f, 1.3f, 1.1f), yellow, 0.5f);
        Box(up, new Vector3(0.95f, cy + 0.1f, 0.76f), new Vector3(0.85f, 0.85f, 0.02f), new Color("#2c4359"), 0.15f, 0.2f);
        Box(up, new Vector3(1.46f, cy + 0.1f, 0.2f), new Vector3(0.02f, 0.85f, 0.9f), new Color("#2c4359"), 0.15f, 0.2f);
        // ---- jib: a triangular lattice running out from the mast top ----
        var top = mh + 0.5f;
        var jl = reach;
        foreach (var x in new[] { -0.3f, 0.3f }) Box(up, new Vector3(x, top, jl / 2), new Vector3(0.07f, 0.07f, jl + 0.6f), yellow);
        Box(up, new Vector3(0, top + 0.55f, jl / 2), new Vector3(0.07f, 0.07f, jl + 0.6f), yellow);
        var jseg = 1.2f; var jd = Mathf.Sqrt(jseg * jseg / 4 + 0.55f * 0.55f + 0.09f); var ja = Mathf.RadToDeg(Mathf.Atan2(0.55f, jseg / 2));
        for (var z = 0f; z < jl; z += jseg)
        {
            Box(up, new Vector3(0, top, z), new Vector3(0.6f, 0.05f, 0.05f), yellow);
            foreach (var (x, s2) in new[] { (-0.15f, 1), (0.15f, -1) })
            {
                Box(up, new Vector3(x, top + 0.27f, z + jseg / 4), new Vector3(0.04f, 0.04f, jd), yellow).RotationDegrees = new Vector3(-ja, 0, s2 * 15);
                Box(up, new Vector3(x, top + 0.27f, z + jseg * 3 / 4), new Vector3(0.04f, 0.04f, jd), yellow).RotationDegrees = new Vector3(ja, 0, s2 * 15);
            }
        }
        // mast-top A-frame and tie bars down to the jib
        var apex = new Vector3(0, top + 3.2f, -0.6f);
        void Tie(Node3D p, Vector3 from, Vector3 to, float t)
        {
            var mid = (from + to) / 2; var len = from.DistanceTo(to);
            var bar = Box(p, mid, new Vector3(t, t, len), yellow);
            bar.Transform = new Transform3D(Basis.LookingAt(to - from, Vector3.Up), mid);
        }
        Tie(up, new Vector3(-0.4f, top, 0), apex, 0.08f);
        Tie(up, new Vector3(0.4f, top, 0), apex, 0.08f);
        Tie(up, new Vector3(0, top, -1.2f), apex, 0.08f);
        Tie(up, apex, new Vector3(0, top + 0.6f, jl * 0.45f), 0.05f);
        Tie(up, apex, new Vector3(0, top + 0.6f, jl * 0.85f), 0.05f);
        Tie(up, apex, new Vector3(0, 0.9f, -1.6f), 0.04f);                                            // back stay to the ballast
        // trolley, hoist rope and hook block with a load
        var tz = jl * 0.6f;
        Box(up, new Vector3(0, top - 0.15f, tz), new Vector3(0.7f, 0.25f, 0.6f), black);
        var drop = Mathf.Max(3f, top - 4f);
        var cable = Box(up, new Vector3(0, top - 0.3f - drop / 2, tz), new Vector3(0.04f, drop, 0.04f), black); cable.Name = "Cable";
        Box(up, new Vector3(0, top - 0.5f - drop, tz), new Vector3(0.4f, 0.5f, 0.3f), yellow);
        Box(up, new Vector3(0, top - 0.5f - drop, tz + 0.16f), new Vector3(0.4f, 0.1f, 0.02f), black);
        Box(up, new Vector3(0, top - 1.4f - drop, tz), new Vector3(1.4f, 0.9f, 1.0f), new Color("#b5785a"));
        return n;
    }

    /// <summary>Tracked excavator: undercarriage with tracks, a turning upper body ("Top") with cab and counterweight, and a two-part arm with a bucket.</summary>
    public static Node3D Excavator()
    {
        if (MadeExcavator() is { } made) return made;
        var n = new Node3D();
        var paint = new Color("#f08a24");          // construction orange
        var black = new Color("#24282d");
        var steel = new Color("#c9ced3");
        var shoe = new Color("#2f3439");
        // ---- undercarriage: track frames with segmented shoes, sprocket, idler and rollers ----
        foreach (var x in new[] { -0.95f, 0.95f })
        {
            Box(n, new Vector3(x, 0.42f, 0), new Vector3(0.36f, 0.42f, 3.0f), black, 0.8f);            // track frame
            for (var i = 0; i < 18; i++)
            {
                var z = -1.6f + i * 0.19f;
                Box(n, new Vector3(x, 0.07f, z), new Vector3(0.6f, 0.1f, 0.15f), shoe, 0.9f);         // bottom shoes
                if (i > 1 && i < 16) Box(n, new Vector3(x, 0.79f, z), new Vector3(0.6f, 0.08f, 0.15f), shoe, 0.9f); // top run
            }
            foreach (var z in new[] { -1.62f, 1.62f })
            {
                var end = Cyl(n, new Vector3(x, 0.43f, z), 0.38f, 0.6f, shoe); end.RotationDegrees = new Vector3(0, 0, 90);
                var hub = Cyl(n, new Vector3(x, 0.43f, z), 0.27f, 0.64f, black); hub.RotationDegrees = new Vector3(0, 0, 90);
            }
            for (var i = 0; i < 5; i++)
            {
                var r = Cyl(n, new Vector3(x * 1.17f, 0.24f, -1.1f + i * 0.55f), 0.12f, 0.05f, new Color("#59626b")); r.RotationDegrees = new Vector3(0, 0, 90);
            }
        }
        Box(n, new Vector3(0, 0.55f, 0), new Vector3(1.6f, 0.3f, 1.2f), black);                          // car body between the tracks

        // ---- upper structure, slews on the turntable ----
        var top = new Node3D { Name = "Top", Position = new Vector3(0, 0.88f, 0) }; n.AddChild(top);
        Cyl(top, new Vector3(0, 0.06f, 0), 0.8f, 0.14f, black);
        Box(top, new Vector3(0, 0.22f, -0.35f), new Vector3(2.4f, 0.16f, 2.9f), black);                  // deck
        Box(top, new Vector3(0.35f, 0.62f, -0.75f), new Vector3(1.7f, 0.66f, 1.9f), paint, 0.45f);       // engine housing
        var cw = Cyl(top, new Vector3(0, 0.6f, -1.75f), 1.2f, 0.75f, paint);                              // rounded counterweight
        cw.Scale = new Vector3(1f, 1f, 0.42f);
        Box(top, new Vector3(0, 0.6f, -1.42f), new Vector3(2.38f, 0.75f, 0.5f), paint, 0.45f);
        Box(top, new Vector3(0, 0.27f, -1.8f), new Vector3(2.3f, 0.06f, 0.5f), black);                  // black band under the counterweight
        for (var i = 0; i < 6; i++) Box(top, new Vector3(1.21f, 0.75f, -0.2f - i * 0.12f), new Vector3(0.02f, 0.05f, 0.08f), black); // side grille
        Box(top, new Vector3(0.35f, 1.06f, -1.05f), new Vector3(1.6f, 0.22f, 1.1f), black);              // black hood box on top
        Cyl(top, new Vector3(0.85f, 1.35f, -0.55f), 0.06f, 0.5f, black);                                 // exhaust
        // hand rails on the deck
        foreach (var z in new[] { -0.2f, -1.4f })
            Box(top, new Vector3(1.15f, 1.25f, z), new Vector3(0.04f, 0.6f, 0.04f), black);
        Box(top, new Vector3(1.15f, 1.55f, -0.8f), new Vector3(0.04f, 0.04f, 1.25f), black);
        Box(top, new Vector3(-0.15f, 1.25f, -0.2f), new Vector3(0.04f, 0.6f, 0.04f), black);
        // cab front-left with tinted glass, a roof and a front guard
        Box(top, new Vector3(-0.68f, 0.95f, 0.65f), new Vector3(0.95f, 1.3f, 1.2f), paint, 0.45f);
        Box(top, new Vector3(-0.68f, 1.05f, 1.26f), new Vector3(0.82f, 0.95f, 0.02f), new Color("#2c4359"), 0.15f, 0.2f);
        Box(top, new Vector3(-1.16f, 1.1f, 0.65f), new Vector3(0.02f, 0.85f, 1.0f), new Color("#2c4359"), 0.15f, 0.2f);
        Box(top, new Vector3(-0.68f, 1.62f, 0.62f), new Vector3(1.0f, 0.06f, 1.3f), black);
        Box(top, new Vector3(-0.68f, 0.38f, 1.22f), new Vector3(0.9f, 0.2f, 0.06f), black);
        // ---- boom with a bend, stick and bucket, each with its hydraulic cylinder ----
        var boom = new Node3D { Name = "Boom", Position = new Vector3(0.2f, 0.55f, 0.95f), RotationDegrees = new Vector3(-48, 0, 0) }; top.AddChild(boom);
        Box(boom, new Vector3(0, 0, 1.2f), new Vector3(0.42f, 0.5f, 2.5f), paint, 0.45f);
        var knee = new Node3D { Position = new Vector3(0, 0, 2.4f), RotationDegrees = new Vector3(38, 0, 0) }; boom.AddChild(knee);
        Box(knee, new Vector3(0, 0, 1.0f), new Vector3(0.38f, 0.44f, 2.1f), paint, 0.45f);
        Box(knee, new Vector3(0.215f, 0, 0.9f), new Vector3(0.01f, 0.22f, 1.0f), black);                  // panel on the side of the boom
        // boom cylinders from the deck to the underside of the boom, stick cylinder along the top
        var bc = Cyl(top, new Vector3(0.2f, 0.75f, 1.55f), 0.08f, 1.5f, black); bc.RotationDegrees = new Vector3(62, 0, 0);
        Cyl(top, new Vector3(0.2f, 1.08f, 1.95f), 0.05f, 0.9f, steel).RotationDegrees = new Vector3(62, 0, 0);
        Cyl(knee, new Vector3(0, 0.36f, 0.6f), 0.07f, 1.4f, black).RotationDegrees = new Vector3(90, 0, 0);
        Cyl(knee, new Vector3(0, 0.36f, 1.5f), 0.045f, 0.7f, steel).RotationDegrees = new Vector3(90, 0, 0);
        var stick = new Node3D { Name = "Stick", Position = new Vector3(0, 0, 2.1f), RotationDegrees = new Vector3(78, 0, 0) }; knee.AddChild(stick);
        Box(stick, new Vector3(0, 0, 1.0f), new Vector3(0.32f, 0.38f, 2.1f), paint, 0.45f);
        Cyl(stick, new Vector3(0, 0.3f, 0.9f), 0.06f, 1.2f, black).RotationDegrees = new Vector3(90, 0, 0); // bucket cylinder
        var bucket = new Node3D { Name = "Bucket", Position = new Vector3(0, 0, 2.05f), RotationDegrees = new Vector3(35, 0, 0) }; stick.AddChild(bucket);
        Box(bucket, new Vector3(0, -0.05f, 0.05f), new Vector3(0.36f, 0.3f, 0.3f), black);                // quick coupler
        Box(bucket, new Vector3(0, -0.25f, 0.4f), new Vector3(0.95f, 0.55f, 0.6f), new Color("#8f8476"), 0.8f, 0.2f);
        Box(bucket, new Vector3(0, -0.02f, 0.45f), new Vector3(0.9f, 0.08f, 0.52f), new Color("#6b5a44"), 1f); // soil in the bucket
        for (var i = 0; i < 5; i++) Box(bucket, new Vector3(-0.36f + i * 0.18f, -0.55f, 0.72f), new Vector3(0.09f, 0.1f, 0.16f), new Color("#9aa4ab"), 0.5f, 0.6f);
        return n;
    }

    // ---- own Blender models for the site vehicles (tools/models/blender/vehicles.py); the primitive versions above stay as fallback ----

    static readonly StandardMaterial3D CabWhiteMat = Mat(new Color("#e9edf0"), 0.35f, 0.1f), CabOrangeMat = Mat(new Color("#f2a33a"), 0.35f, 0.1f), CabBlueMat = Mat(new Color("#3b4f66"), 0.35f, 0.1f);
    static Material Vehicle(string name, Material m) => name == "headlight" ? World.WorldView.HeadlightMat : MadeMat(name, m);
    static Material CabWhite(string name, Material m) => name == "body" ? CabWhiteMat : Vehicle(name, m);
    static Material CabOrange(string name, Material m) => name == "body" ? CabOrangeMat : Vehicle(name, m);
    static Material CabBlue(string name, Material m) => name == "body" ? CabBlueMat : Vehicle(name, m);
    static readonly StandardMaterial3D BlockMat = Mat(new Color("#c9c3b8"), 0.9f);
    static Material BlockPallet(string name, Material m) => name == "brick_stack" ? BlockMat : MadeMat(name, m);

    /// <summary>The articulated lorry from the own models: tractor, then a trailer pivoting at the kingpin (node "Trailer") with its "Cargo".</summary>
    static Node3D MadeTruck(TruckLoad load)
    {
        if (!Assets.Has("made/truck_tractor.glb")) return null;
        var n = new Node3D();
        Func<string, Material, Material> cab = load == TruckLoad.Material ? CabOrange : load == TruckLoad.Customer ? CabBlue : CabWhite;
        Assets.Place(n, "made/truck_tractor.glb", Vector3.Zero, 1, 0, cab);
        var tr = new Node3D { Name = "Trailer" }; n.AddChild(tr);
        Assets.Place(tr, load == TruckLoad.Material ? "made/trailer_flat.glb" : load == TruckLoad.Customer ? "made/trailer_removal.glb" : "made/trailer_box.glb", Vector3.Zero, 1, 0, Vehicle);
        if (load != TruckLoad.Equipment)
        {
            var cargo = new Node3D { Name = "Cargo" }; tr.AddChild(cargo);
            Assets.Place(cargo, load == TruckLoad.Material ? "made/cargo_pallets.glb" : "made/cargo_racks.glb", Vector3.Zero, 1, 0, Vehicle);
        }
        return n;
    }

    /// <summary>Tracked excavator from its joint parts, posed digging (same pivots as the primitive one).</summary>
    static Node3D MadeExcavator()
    {
        if (!Assets.Has("made/exc_base.glb")) return null;
        var n = new Node3D();
        Assets.Place(n, "made/exc_base.glb", Vector3.Zero, 1, 0, Vehicle);
        var top = new Node3D { Name = "Top", Position = new Vector3(0, 0.88f, 0) }; n.AddChild(top);
        Assets.Place(top, "made/exc_top.glb", Vector3.Zero, 1, 0, Vehicle);
        var boom = new Node3D { Name = "Boom", Position = new Vector3(0.2f, 0.55f, 0.95f), RotationDegrees = new Vector3(-48, 0, 0) }; top.AddChild(boom);
        Assets.Place(boom, "made/exc_boom.glb", Vector3.Zero, 1, 0, Vehicle);
        var knee = new Node3D { Position = new Vector3(0, 0, 2.4f), RotationDegrees = new Vector3(38, 0, 0) }; boom.AddChild(knee);
        Assets.Place(knee, "made/exc_knee.glb", Vector3.Zero, 1, 0, Vehicle);
        // boom cylinder from the deck to the underside of the boom
        var bc = Cyl(top, new Vector3(0.2f, 0.75f, 1.55f), 0.08f, 1.5f, new Color("#24282d")); bc.RotationDegrees = new Vector3(62, 0, 0);
        Cyl(top, new Vector3(0.2f, 1.08f, 1.95f), 0.05f, 0.9f, new Color("#c9ced3")).RotationDegrees = new Vector3(62, 0, 0);
        var stick = new Node3D { Name = "Stick", Position = new Vector3(0, 0, 2.1f), RotationDegrees = new Vector3(78, 0, 0) }; knee.AddChild(stick);
        Assets.Place(stick, "made/exc_stick.glb", Vector3.Zero, 1, 0, Vehicle);
        var bucket = new Node3D { Name = "Bucket", Position = new Vector3(0, 0, 2.05f), RotationDegrees = new Vector3(35, 0, 0) }; stick.AddChild(bucket);
        Assets.Place(bucket, "made/exc_bucket.glb", Vector3.Zero, 1, 0, Vehicle);
        return n;
    }

    /// <summary>Mobile self-erecting crane: carrier, and the slewing "Jib" with a stacked lattice mast, cab, jib, ties, trolley and hook.</summary>
    static Node3D MadeCrane(float height, float reach)
    {
        if (!Assets.Has("made/crane_carrier.glb")) return null;
        var n = new Node3D();
        var yellow = new Color("#f5c400");
        Assets.Place(n, "made/crane_carrier.glb", Vector3.Zero, 1, 0, Vehicle);
        var up = new Node3D { Name = "Jib", Position = new Vector3(0, 1.4f, -2.2f) }; n.AddChild(up);
        Assets.Place(up, "made/crane_turret.glb", Vector3.Zero, 1, 0, Vehicle);
        var sections = Mathf.Max(1, Mathf.RoundToInt(height / 1.5f));
        var mh = sections * 1.5f;
        var mast = new List<Transform3D>();
        for (var i = 0; i < sections; i++) mast.Add(new Transform3D(i % 2 == 0 ? Basis.Identity : new Basis(Vector3.Up, Mathf.Pi / 2), new Vector3(0, 0.5f + i * 1.5f, 0)));
        Assets.Many(up, Assets.Mesh("made/crane_mast.glb", Vehicle), mast);
        Box(up, new Vector3(0, mh + 0.5f, 0), new Vector3(0.95f, 0.06f, 0.95f), yellow);
        var cy = Mathf.Max(5f, mh * 0.55f);
        Assets.Place(up, "made/crane_cab.glb", new Vector3(0.95f, cy, 0.2f), 1, 0, Vehicle);
        var top = mh + 0.5f;
        var jsec = Mathf.Max(4, Mathf.RoundToInt(reach / 1.2f));
        var jl = jsec * 1.2f;
        var jib = new List<Transform3D>();
        for (var i = 0; i < jsec; i++) jib.Add(new Transform3D(Basis.Identity, new Vector3(0, top, i * 1.2f)));
        Assets.Many(up, Assets.Mesh("made/crane_jib.glb", Vehicle), jib);
        var apex = new Vector3(0, top + 3.2f, -0.6f);
        void Tie(Vector3 from, Vector3 to, float t)
        {
            var mid = (from + to) / 2;
            var bar = Box(up, mid, new Vector3(t, t, from.DistanceTo(to)), yellow);
            bar.Transform = new Transform3D(Basis.LookingAt(to - from, Vector3.Up), mid);
        }
        Tie(new Vector3(-0.4f, top, 0), apex, 0.08f);
        Tie(new Vector3(0.4f, top, 0), apex, 0.08f);
        Tie(new Vector3(0, top, -1.2f), apex, 0.08f);
        Tie(apex, new Vector3(0, top + 0.55f, jl * 0.45f), 0.05f);
        Tie(apex, new Vector3(0, top + 0.55f, jl * 0.85f), 0.05f);
        Tie(apex, new Vector3(0, 0.9f, -1.6f), 0.04f);
        var tz = jl * 0.6f;
        Assets.Place(up, "made/crane_trolley.glb", new Vector3(0, top - 0.15f, tz), 1, 0, Vehicle);
        var drop = Mathf.Max(3f, top - 4f);
        var cable = Box(up, new Vector3(0, top - 0.3f - drop / 2, tz), new Vector3(0.03f, drop, 0.03f), new Color("#24282d")); cable.Name = "Cable";
        Assets.Place(up, "made/crane_hook.glb", new Vector3(0, top - 0.3f - drop, tz), 1, 0, Vehicle);
        return n;
    }

    public static Node3D Marker(Color c)
    {
        var n = new Node3D();
        var m = Box(n, Vector3.Zero, new Vector3(0.32f, 0.32f, 0.32f), c, glow: true);
        m.RotationDegrees = new Vector3(45, 0, 45);
        m.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        return n;
    }
}
