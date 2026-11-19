using System;
using System.Collections.Generic;
using DatacenterArchitect.Core;
using DatacenterArchitect.Sim;
using DatacenterArchitect.UI;
using Godot;
using static DatacenterArchitect.World.Models;

namespace DatacenterArchitect.World;

/// <summary>
/// Small pictures for the build menu cards, rendered from the same 3D models the game uses:
/// a tree looks like a tree, a fence like a short run of that fence, a room like a floor tile in its colour.
/// Each picture is a tiny off-screen viewport that renders once and is then kept.
/// </summary>
public partial class Thumbnails : Node
{
    public static Thumbnails Instance { get; private set; }
    public static readonly Vector2I Size = new(150, 84);
    readonly Dictionary<BuildItem, Texture2D> _cache = new();

    public override void _Ready() => Instance = this;

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>The picture for a build item; null when nothing is drawn for it (select and demolish).</summary>
    public Texture2D Get(BuildItem item)
    {
        if (_cache.TryGetValue(item, out var t)) return t;
        var scene = Scene(item);
        if (scene == null) return null;
        var vp = new SubViewport
        {
            Size = Size * 2, // rendered at twice the card size so it stays sharp on 4K
            TransparentBg = true, OwnWorld3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Once,
            Msaa3D = Viewport.Msaa.Msaa4X,
        };
        vp.AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.ClearColor,
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color("#dfeefa"), AmbientLightEnergy = 0.55f,
                TonemapMode = Godot.Environment.ToneMapper.Linear,
            },
        });
        vp.AddChild(new DirectionalLight3D { LightEnergy = 1.0f, LightColor = new Color("#fff0d8"), RotationDegrees = new Vector3(-52, -38, 0), ShadowEnabled = false });
        vp.AddChild(scene);
        var cam = new Camera3D { Fov = 28 };
        vp.AddChild(cam);
        Frame(cam, scene);
        AddChild(vp);
        t = vp.GetTexture();
        _cache[item] = t;
        return t;
    }

    /// <summary>Points the camera at the model from the game's usual tilted angle so the whole model fits.</summary>
    static void Frame(Camera3D cam, Node3D scene)
    {
        var box = Bounds(scene, Transform3D.Identity);
        var center = box.GetCenter();
        var radius = Math.Max(0.4f, box.Size.Length() / 2);
        var aspect = (float)Size.X / Size.Y;
        var fov = Mathf.DegToRad(cam.Fov) / 2;
        // fit vertically, and horizontally through the aspect ratio
        var dist = radius / Mathf.Sin(Math.Min(fov, Mathf.Atan(Mathf.Tan(fov) * aspect))) * 0.92f;
        const float yaw = 0.62f, pitch = 0.55f;
        var dir = new Vector3(Mathf.Cos(pitch) * Mathf.Sin(yaw), Mathf.Sin(pitch), Mathf.Cos(pitch) * Mathf.Cos(yaw));
        cam.Position = center + dir * dist;
        cam.LookAtFromPosition(cam.Position, center, Vector3.Up);
    }

    static Aabb Bounds(Node n, Transform3D parent)
    {
        Aabb? box = null;
        var xf = n is Node3D n3 ? parent * n3.Transform : parent;
        if (n is MeshInstance3D mi && mi.Mesh != null)
        {
            var b = xf * mi.Mesh.GetAabb();
            box = b;
        }
        foreach (var c in n.GetChildren())
        {
            var cb = Bounds(c, xf);
            if (cb.Size == Vector3.Zero && cb.Position == Vector3.Zero) continue;
            box = box.HasValue ? box.Value.Merge(cb) : cb;
        }
        return box ?? new Aabb();
    }

    /// <summary>A small diorama for each kind of build item, built from the in-game models.</summary>
    public static Node3D Scene(BuildItem item)
    {
        var n = new Node3D();
        switch (item.Mode)
        {
            case Mode.Object:
            {
                var k = (ObjKind)item.Arg;
                var def = Catalog.Objects[k];
                // a hint of floor under small things so they do not float
                if (def.W * def.D <= 2) Tile(n, Vector3.Zero, new Vector2(Math.Max(1.4f, def.W + 0.6f), Math.Max(1.4f, def.D + 0.6f)), Brand.PlotGrass);
                n.AddChild(Models.Object(k));
                break;
            }
            case Mode.Row:
                for (var i = 0; i < 4; i++)
                {
                    var r = Models.Object((ObjKind)item.Arg);
                    r.Position = new Vector3(i - 1.5f, 0, 0);
                    n.AddChild(r);
                }
                Tile(n, new Vector3(0, 0, 1.2f), new Vector2(4.4f, 1.2f), new Color("#c9d3da"));
                break;
            case Mode.Surface:
            case Mode.Curve:
                SurfacePatch(n, (Surface)item.Arg, item.Mode == Mode.Curve);
                break;
            case Mode.Fence:
                FenceRun(n, (FenceType)item.Arg);
                break;
            case Mode.Building:
                BuildingBlock(n, (BuildingKind)item.Arg);
                break;
            case Mode.Room:
                RoomTile(n, (RoomType)item.Arg);
                break;
            case Mode.Door:
            {
                var type = (DoorType)item.Arg;
                var w = Catalog.Doors[type].Width;
                Box(n, new Vector3(-w / 2f - 0.75f, 1.4f, 0), new Vector3(1.5f, 2.8f, 0.25f), Brand.Wall);
                Box(n, new Vector3(w / 2f + 0.75f, 1.4f, 0), new Vector3(1.5f, 2.8f, 0.25f), Brand.Wall);
                Box(n, new Vector3(0, 2.55f, 0), new Vector3(w, 0.5f, 0.25f), Brand.Wall);
                WorldView.DoorModel(n, new Door { X = 0, Z = 0, Dir = 1, Type = type }, 0, 2.8f);
                // DoorModel centres on (X + w/2, Z): shift it back onto the wall opening
                var door = n.GetChild<Node3D>(n.GetChildCount() - 1);
                door.Position -= new Vector3(w / 2f, 0, 0);
                Tile(n, new Vector3(0, 0, 0.8f), new Vector2(w + 3.4f, 1.6f), Brand.Concrete);
                break;
            }
            default:
                n.QueueFree();
                return null;
        }
        return n;
    }

    static void Tile(Node3D n, Vector3 center, Vector2 size, Color c) =>
        Box(n, center + new Vector3(0, -0.03f, 0), new Vector3(size.X, 0.06f, size.Y), c, 0.95f);

    static void SurfacePatch(Node3D n, Surface s, bool curved)
    {
        Tile(n, new Vector3(0, -0.02f, 0), new Vector2(3.6f, 2.6f), Brand.PlotGrass);
        if (s == Surface.Grass)
        {
            Tile(n, Vector3.Zero, new Vector2(2.6f, 1.8f), Brand.Grass);
            for (var i = 0; i < 3; i++) Ball(n, new Vector3(-0.8f + i * 0.8f, 0.15f, 0.2f * (i - 1)), 0.18f, Brand.Grass.Darkened(0.15f));
            return;
        }
        var col = WorldView.SurfaceColors[s];
        if (curved)
        {
            // a quarter bend made of short straight pieces
            for (var i = 0; i < 7; i++)
            {
                var a = i / 6f * Mathf.Pi / 2;
                var seg = new Node3D { Position = new Vector3(-1.4f + Mathf.Sin(a) * 2.2f, 0, 1.0f - Mathf.Cos(a) * 2.2f + 0.2f), Rotation = new Vector3(0, -a, 0) };
                n.AddChild(seg);
                Box(seg, Vector3.Zero, new Vector3(s == Surface.Asphalt ? 1.4f : 0.8f, 0.04f, 0.55f), col, 0.95f);
            }
            return;
        }
        Box(n, Vector3.Zero, new Vector3(2.6f, 0.04f, 1.8f), col, 0.95f);
        if (s == Surface.Parking)
            for (var i = 0; i < 4; i++) Box(n, new Vector3(-0.9f + i * 0.6f, 0.03f, 0.2f), new Vector3(0.05f, 0.01f, 1.2f), new Color("#f2f2ee"));
        if (s == Surface.Footpath)
            for (var x = 0; x < 4; x++) for (var z = 0; z < 3; z++)
                Box(n, new Vector3(-0.95f + x * 0.63f, 0.025f, -0.6f + z * 0.6f), new Vector3(0.55f, 0.01f, 0.52f), new Color("#e4dac5"));
        if (s == Surface.Asphalt)
            for (var i = 0; i < 3; i++) Box(n, new Vector3(-0.9f + i * 0.9f, 0.03f, 0), new Vector3(0.45f, 0.01f, 0.06f), new Color("#f2f2ee"));
        if (s == Surface.Yard)
            for (var i = 0; i < 2; i++) Box(n, new Vector3(-0.5f + i * 1.0f, 0.25f, 0), new Vector3(0.8f, 0.45f, 0.8f), new Color("#a07a4f"));
    }

    static void FenceRun(Node3D n, FenceType type)
    {
        var def = Catalog.Fences[type];
        var (post, panel, thick) = WorldView.FenceStyle(type);
        Tile(n, new Vector3(0, 0, 0.3f), new Vector2(4.2f, 1.6f), Brand.PlotGrass);
        for (var i = 0; i < 4; i++) Box(n, new Vector3(-1.5f + i, def.Height / 2, 0), new Vector3(0.1f, def.Height, 0.1f), post);
        for (var i = 0; i < 3; i++) Box(n, new Vector3(-1f + i, def.Height / 2, 0), new Vector3(1f, def.Height * 0.92f, thick), panel);
        if (type == FenceType.Electric)
            for (var h = 0; h < 4; h++) Box(n, new Vector3(0, 0.4f + h * def.Height / 4.5f, 0.02f), new Vector3(3f, 0.02f, 0.02f), Brand.Amber, glow: true);
    }

    static void BuildingBlock(Node3D n, BuildingKind kind)
    {
        var b = new Building { Kind = kind, W = kind == BuildingKind.Datacenter ? 6 : 4, D = 4, Floors = kind == BuildingKind.Office ? 2 : 1,
            Facade = kind switch { BuildingKind.Office => Facade.Brick, BuildingKind.Warehouse => Facade.Cladding, _ => Facade.Concrete } };
        var h = WorldView.FloorH * b.Floors;
        Tile(n, Vector3.Zero, new Vector2(b.W + 1.2f, b.D + 1.2f), Brand.Concrete);
        Box(n, new Vector3(0, h / 2, 0), new Vector3(b.W, h, b.D), WorldView.WallColor(b));
        Box(n, new Vector3(0, h + 0.12f, 0), new Vector3(b.W + 0.3f, 0.24f, b.D + 0.3f), new Color("#6f7a83"));
        switch (kind)
        {
            case BuildingKind.Datacenter:
                Box(n, new Vector3(0, h - 0.25f, b.D / 2f + 0.02f), new Vector3(b.W, 0.25f, 0.06f), Brand.Power);
                for (var i = 0; i < 2; i++) Box(n, new Vector3(-1.5f + i * 3, h + 0.75f, -0.5f), new Vector3(2f, 1f, 1.4f), new Color("#e3e7ea"));
                break;
            case BuildingKind.Office:
                for (var f = 0; f < b.Floors; f++)
                    for (var i = 0; i < 3; i++)
                    {
                        Box(n, new Vector3(-1.2f + i * 1.2f, f * WorldView.FloorH + 1.9f, b.D / 2f + 0.02f), new Vector3(0.8f, 1.1f, 0.04f), new Color("#2f4a63"));
                        Box(n, new Vector3(b.W / 2f + 0.02f, f * WorldView.FloorH + 1.9f, -1.2f + i * 1.2f), new Vector3(0.04f, 1.1f, 0.8f), new Color("#2f4a63"));
                    }
                break;
            default:
                Box(n, new Vector3(0, 1.4f, b.D / 2f + 0.03f), new Vector3(2.4f, 2.8f, 0.05f), new Color("#9aa4ab"), 0.5f, 0.4f);
                break;
        }
    }

    static void RoomTile(Node3D n, RoomType type)
    {
        var col = new Color(Catalog.Rooms[type].Color);
        Tile(n, Vector3.Zero, new Vector2(4f, 3f), new Color("#c4c9cd"));
        Box(n, new Vector3(0, 0.02f, 0), new Vector3(3.6f, 0.04f, 2.6f), col, 0.9f);
        // two low walls at the back so it reads as a room
        Box(n, new Vector3(0, 0.6f, -1.4f), new Vector3(4f, 1.2f, 0.14f), new Color("#e4e8eb"));
        Box(n, new Vector3(-1.95f, 0.6f, 0), new Vector3(0.14f, 1.2f, 3f), new Color("#e4e8eb"));
        // a typical piece of furniture
        var k = type switch
        {
            RoomType.DataHall => ObjKind.Rack,
            RoomType.Office or RoomType.Executive => ObjKind.Desk,
            RoomType.MeetMe => ObjKind.Odf,
            RoomType.Noc => ObjKind.NocWall,
            _ => (ObjKind?)null,
        } ;
        if (k is { } kind && Catalog.Objects.ContainsKey(kind))
        {
            var m = Models.Object(kind);
            m.Position = new Vector3(0.6f, 0.04f, -0.4f);
            n.AddChild(m);
        }
    }
}
