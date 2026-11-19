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

    static readonly Color White = new("#eef1f3"), Steel = new("#8f989f"), Dark = new("#2b3036"), Wood = new("#9a7550"), Leaf = new("#5f9a45"), Leaf2 = new("#77b356");

    /// <summary>Model of a placed object, centred on its footprint, facing +z when Rot = 0.</summary>
    public static Node3D Object(ObjKind k)
    {
        var n = new Node3D();
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
            case ObjKind.Lift:
                Box(n, new Vector3(0, 0.02f, 0), new Vector3(1.9f, 0.04f, 1.9f), new Color("#5d6670"));
                foreach (var (x, z) in new[] { (-0.92f, -0.92f), (0.92f, -0.92f), (-0.92f, 0.92f), (0.92f, 0.92f) })
                    Box(n, new Vector3(x, 1.6f, z), new Vector3(0.08f, 3.2f, 0.08f), Brand.Power);
                Box(n, new Vector3(0, 0.25f, 0.95f), new Vector3(1.9f, 0.08f, 0.04f), Brand.Power);
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
                Ball(n, new Vector3(0, 2.1f, 0), 0.85f, Leaf, 0.9f);
                Ball(n, new Vector3(0.25f, 2.6f, 0.1f), 0.55f, Leaf2);
                break;
            case ObjKind.Shrub:
                Ball(n, new Vector3(0, 0.35f, 0), 0.48f, Leaf, 0.75f);
                break;
            case ObjKind.LampPost:
                Cyl(n, new Vector3(0, 2.0f, 0), 0.06f, 4f, Dark);
                Box(n, new Vector3(0, 4.0f, 0.25f), new Vector3(0.18f, 0.08f, 0.6f), Dark);
                Box(n, new Vector3(0, 3.95f, 0.45f), new Vector3(0.16f, 0.03f, 0.24f), new Color("#fff2c4"), glow: true);
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
    /// <summary>Customers wear their own clothes; a cyan visitor badge marks them.</summary>
    static readonly Color[] Casual = { new("#6c5aa8"), new("#3f8f6b"), new("#b0473f"), new("#5d6b78"), new("#c48a3a"), new("#2f6f9a") };
    static readonly Color[] Skins = { new("#f1c7a5"), new("#d9a27c"), new("#a8714d"), new("#7a4e32"), new("#e8b48f") };

    /// <summary>A round, friendly worker figure. Colour and hat show the role.</summary>
    public static Node3D Person(Role role, int id, StaffRole staff = StaffRole.Support)
    {
        var n = new Node3D();
        var body = role switch
        {
            Role.Staff when id < 0 => new Color("#8a96a3"),
            Role.Staff => new Color(Staffing.Roles[staff].Color),
            Role.Builder => new Color("#ff8a2a"),
            Role.Fitter => new Color("#3d6fb0"),
            Role.Engineer => new Color("#ffd23e"),
            Role.Visitor => Casual[id % Casual.Length],
            _ => White,
        };
        var legs = role == Role.Builder ? new Color("#3f4a57") : new Color("#2f3a46");
        var fig = new Node3D { Name = "Fig" }; n.AddChild(fig);
        var l1 = Capsule(fig, new Vector3(-0.1f, 0.28f, 0), 0.09f, 0.56f, legs); l1.Name = "L1";
        var l2 = Capsule(fig, new Vector3(0.1f, 0.28f, 0), 0.09f, 0.56f, legs); l2.Name = "L2";
        Capsule(fig, new Vector3(0, 0.82f, 0), 0.24f, 0.75f, body);
        if (role == Role.Builder) Box(fig, new Vector3(0, 0.88f, 0.2f), new Vector3(0.3f, 0.06f, 0.06f), new Color("#e8f0f4"), glow: false);
        if (role == Role.Engineer) Box(fig, new Vector3(0, 0.95f, 0.22f), new Vector3(0.36f, 0.05f, 0.05f), Brand.Power);
        if (role == Role.Visitor) Box(fig, new Vector3(0.1f, 0.98f, 0.23f), new Vector3(0.1f, 0.13f, 0.02f), Brand.Cooling);
        if (role == Role.Staff && id > 0)
        {
            // a coloured band shows the trade: amber power, cyan cooling, orange fiber (the brand colours)
            Color? band = staff switch
            {
                StaffRole.Electrical or StaffRole.GeneratorTech => Brand.Power, StaffRole.Cooling => Brand.Cooling, StaffRole.NetworkEng => Brand.Fiber,
                StaffRole.Noc => Brand.Cooling, StaffRole.Guard => new Color("#c9d3dc"), StaffRole.Canteen => new Color("#2f3a46"), StaffRole.RemoteHands => Brand.Power,
                _ => null,
            };
            if (band is Color b) Box(fig, new Vector3(0, 0.95f, 0.22f), new Vector3(0.36f, 0.05f, 0.05f), b);
            switch (staff)
            {
                case StaffRole.Guard: Ball(fig, new Vector3(0, 1.53f, 0), 0.2f, new Color("#1a2a44"), 0.45f); break;
                case StaffRole.Gardener: Ball(fig, new Vector3(0, 1.52f, 0), 0.25f, new Color("#c9a85a"), 0.35f); break;
                case StaffRole.FireSafety: Ball(fig, new Vector3(0, 1.5f, 0), 0.235f, new Color("#d23c32"), 0.62f); break;
                case StaffRole.Electrical or StaffRole.Cooling or StaffRole.GeneratorTech or StaffRole.NetworkEng: Ball(fig, new Vector3(0, 1.5f, 0), 0.235f, White, 0.62f); break;
                case StaffRole.Canteen: Ball(fig, new Vector3(0, 1.56f, 0), 0.17f, White, 1.1f); break;
            }
        }
        if (role == Role.Staff && id < 0) Ball(fig, new Vector3(0, 1.5f, 0), 0.235f, new Color("#ff8a2a"), 0.62f);
        var arm1 = Capsule(fig, new Vector3(-0.29f, 0.85f, 0), 0.07f, 0.5f, body); arm1.Name = "A1";
        var arm2 = Capsule(fig, new Vector3(0.29f, 0.85f, 0), 0.07f, 0.5f, body); arm2.Name = "A2";
        Ball(fig, new Vector3(0, 1.38f, 0), 0.22f, Skins[Math.Abs(id) % Skins.Length]);
        Ball(fig, new Vector3(-0.07f, 1.41f, 0.2f), 0.025f, Dark);
        Ball(fig, new Vector3(0.07f, 1.41f, 0.2f), 0.025f, Dark);
        switch (role)
        {
            case Role.Builder:
                Ball(fig, new Vector3(0, 1.5f, 0), 0.235f, new Color("#ffd23e"), 0.62f);
                Box(fig, new Vector3(0, 1.46f, 0.18f), new Vector3(0.3f, 0.03f, 0.12f), new Color("#ffd23e"));
                break;
            case Role.Engineer:
                Ball(fig, new Vector3(0, 1.5f, 0), 0.235f, White, 0.62f);
                break;
            case Role.Fitter:
                Ball(fig, new Vector3(0, 1.52f, -0.02f), 0.2f, new Color("#24466f"), 0.5f);
                break;
        }
        var crate = Box(n, new Vector3(0, 0.95f, 0.32f), new Vector3(0.4f, 0.3f, 0.3f), new Color("#c79a5b")); crate.Name = "Crate"; crate.Visible = false;
        var rack = Box(n, new Vector3(0, 1.05f, 0.62f), new Vector3(0.9f, 2.1f, 0.85f), Brand.Rack, 0.5f, 0.4f); rack.Name = "Rack"; rack.Visible = false;
        var tool = Box(fig, new Vector3(0.3f, 0.62f, 0.12f), new Vector3(0.12f, 0.2f, 0.06f), role == Role.Engineer ? Brand.Fiber : role == Role.Visitor ? Dark : Steel); tool.Name = "Tool";
        return n;
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

    public static Node3D Truck(TruckLoad load)
    {
        var n = new Node3D();
        var cab = load == TruckLoad.Material ? new Color("#f2a33a") : new Color("#e9edf0");
        Box(n, new Vector3(0, 1.15f, 2.4f), new Vector3(2.2f, 1.9f, 1.6f), cab, 0.5f);
        Box(n, new Vector3(0, 1.55f, 3.21f), new Vector3(1.9f, 0.8f, 0.02f), new Color("#2c4359"), 0.2f);
        Box(n, new Vector3(0, 0.55f, -0.4f), new Vector3(2.2f, 0.3f, 4.6f), Dark);
        foreach (var z in new[] { 2.3f, -1.2f, -2.2f })
            foreach (var x in new[] { -1.05f, 1.05f })
            {
                var w = Cyl(n, new Vector3(x, 0.45f, z), 0.45f, 0.35f, new Color("#1e2226"));
                w.RotationDegrees = new Vector3(0, 0, 90);
            }
        if (load == TruckLoad.Equipment)
        {
            Box(n, new Vector3(0, 1.85f, -0.5f), new Vector3(2.3f, 2.3f, 4.4f), White, 0.6f);
            Box(n, new Vector3(0, 2.6f, -0.5f), new Vector3(2.32f, 0.3f, 4.42f), Brand.Power);
        }
        else
        {
            var cargo = new Node3D { Name = "Cargo" }; n.AddChild(cargo);
            for (var i = 0; i < 4; i++)
            {
                Box(cargo, new Vector3(0, 0.82f, -2.0f + i * 1.1f), new Vector3(1.9f, 0.14f, 0.95f), Wood);
                Box(cargo, new Vector3(0, 1.2f, -2.0f + i * 1.1f), new Vector3(1.7f, 0.6f, 0.85f), i % 2 == 0 ? new Color("#c9c3b8") : new Color("#b5785a"));
            }
        }
        return n;
    }

    /// <summary>Stack of building material on a pallet.</summary>
    public static Node3D Pallet(int i)
    {
        var n = new Node3D();
        Box(n, new Vector3(0, 0.07f, 0), new Vector3(0.9f, 0.14f, 0.9f), Wood);
        Box(n, new Vector3(0, 0.42f, 0), new Vector3(0.82f, 0.56f, 0.82f), i % 3 == 0 ? new Color("#b5785a") : i % 3 == 1 ? new Color("#c9c3b8") : new Color("#9aa4ab"));
        return n;
    }

    /// <summary>Tower crane: Mast stays, "Jib" rotates.</summary>
    public static Node3D Crane(float height, float reach)
    {
        var n = new Node3D();
        Box(n, new Vector3(0, 0.2f, 0), new Vector3(2f, 0.4f, 2f), Brand.Concrete);
        for (var i = 0; i < 4; i++)
        {
            var x = i % 2 == 0 ? -0.45f : 0.45f; var z = i < 2 ? -0.45f : 0.45f;
            Box(n, new Vector3(x, height / 2, z), new Vector3(0.1f, height, 0.1f), Brand.Power);
        }
        for (var y = 1f; y < height; y += 1.5f) Box(n, new Vector3(0, y, 0), new Vector3(1f, 0.08f, 1f), Brand.Power);
        var jib = new Node3D { Name = "Jib", Position = new Vector3(0, height, 0) }; n.AddChild(jib);
        Box(jib, new Vector3(0, 0.6f, 0), new Vector3(1.2f, 1.2f, 1.2f), new Color("#f4f6f8"));
        Box(jib, new Vector3(0, 1.4f, reach / 2 - 2), new Vector3(0.5f, 0.5f, reach + 4), Brand.Power);
        Box(jib, new Vector3(0, 1.0f, -4.5f), new Vector3(1.2f, 1.2f, 1.6f), Brand.Concrete);
        var hook = Box(jib, new Vector3(0, -height * 0.4f, reach * 0.7f), new Vector3(0.05f, height * 0.8f, 0.05f), Dark); hook.Name = "Cable";
        Box(jib, new Vector3(0, -height * 0.8f, reach * 0.7f), new Vector3(0.9f, 0.5f, 0.9f), new Color("#b5785a"));
        return n;
    }

    public static Node3D Excavator()
    {
        var n = new Node3D();
        Box(n, new Vector3(0, 0.35f, 0), new Vector3(1.6f, 0.6f, 2.6f), Dark);
        var top = new Node3D { Name = "Top", Position = new Vector3(0, 0.7f, 0) }; n.AddChild(top);
        Box(top, new Vector3(0, 0.6f, -0.2f), new Vector3(1.7f, 1.1f, 1.9f), new Color("#ffc02e"));
        Box(top, new Vector3(-0.4f, 1.0f, 0.45f), new Vector3(0.7f, 0.8f, 0.7f), new Color("#2c4359"), 0.2f);
        var arm = Box(top, new Vector3(0.35f, 1.2f, 1.6f), new Vector3(0.25f, 0.25f, 2.4f), new Color("#ffc02e"));
        arm.RotationDegrees = new Vector3(20, 0, 0);
        Box(top, new Vector3(0.35f, 0.5f, 2.8f), new Vector3(0.6f, 0.5f, 0.5f), Dark);
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
