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
                Box(n, new Vector3(0, 0.05f, 0), new Vector3(0.9f, 0.1f, 0.9f), Brand.Fiber);
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
                Box(n, new Vector3(-1.3f, 0.5f, 0), new Vector3(0.35f, 1f, 0.35f), White);
                Box(n, new Vector3(-1.3f, 0.95f, 0), new Vector3(0.37f, 0.1f, 0.37f), Brand.Critical);
                for (var i = 0; i < 6; i++)
                    Box(n, new Vector3(-1.05f + i * 0.4f, 0.9f, 0), new Vector3(0.4f, 0.08f, 0.08f), i % 2 == 0 ? Brand.Critical : White);
                break;
            case ObjKind.SlidingGate:
                Box(n, new Vector3(-1.45f, 1.2f, 0), new Vector3(0.14f, 2.4f, 0.14f), Dark);
                Box(n, new Vector3(1.45f, 1.2f, 0), new Vector3(0.14f, 2.4f, 0.14f), Dark);
                var leaf = new Node3D { Name = "Leaf" }; n.AddChild(leaf);
                Box(leaf, new Vector3(0, 2.2f, 0), new Vector3(2.8f, 0.08f, 0.06f), Dark);
                Box(leaf, new Vector3(0, 0.15f, 0), new Vector3(2.8f, 0.08f, 0.06f), Dark);
                for (var i = 0; i < 12; i++) Box(leaf, new Vector3(-1.32f + i * 0.24f, 1.2f, 0), new Vector3(0.04f, 2.0f, 0.04f), Dark);
                Box(n, new Vector3(1.45f, 2.5f, 0), new Vector3(0.18f, 0.12f, 0.18f), Brand.Power, glow: true);
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
    static readonly Color[] Skins = { new("#f1c7a5"), new("#d9a27c"), new("#a8714d"), new("#7a4e32"), new("#e8b48f") };

    /// <summary>A round, friendly worker figure. Colour and hat show the role.</summary>
    public static Node3D Person(Role role, int id)
    {
        var n = new Node3D();
        var body = role switch
        {
            Role.Builder => new Color("#ff8a2a"),
            Role.Fitter => new Color("#3d6fb0"),
            Role.Engineer => new Color("#ffd23e"),
            _ => White,
        };
        var legs = role == Role.Builder ? new Color("#3f4a57") : new Color("#2f3a46");
        var fig = new Node3D { Name = "Fig" }; n.AddChild(fig);
        var l1 = Capsule(fig, new Vector3(-0.1f, 0.28f, 0), 0.09f, 0.56f, legs); l1.Name = "L1";
        var l2 = Capsule(fig, new Vector3(0.1f, 0.28f, 0), 0.09f, 0.56f, legs); l2.Name = "L2";
        Capsule(fig, new Vector3(0, 0.82f, 0), 0.24f, 0.75f, body);
        if (role == Role.Builder) Box(fig, new Vector3(0, 0.88f, 0.2f), new Vector3(0.3f, 0.06f, 0.06f), new Color("#e8f0f4"), glow: false);
        if (role == Role.Engineer) Box(fig, new Vector3(0, 0.95f, 0.22f), new Vector3(0.36f, 0.05f, 0.05f), Brand.Power);
        var arm1 = Capsule(fig, new Vector3(-0.29f, 0.85f, 0), 0.07f, 0.5f, body); arm1.Name = "A1";
        var arm2 = Capsule(fig, new Vector3(0.29f, 0.85f, 0), 0.07f, 0.5f, body); arm2.Name = "A2";
        Ball(fig, new Vector3(0, 1.38f, 0), 0.22f, Skins[id % Skins.Length]);
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
        var tool = Box(fig, new Vector3(0.3f, 0.62f, 0.12f), new Vector3(0.12f, 0.2f, 0.06f), role == Role.Engineer ? Brand.Fiber : Steel); tool.Name = "Tool";
        return n;
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
