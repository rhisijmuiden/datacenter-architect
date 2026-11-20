using System;
using System.Collections.Generic;
using DatacenterArchitect.Core;
using DatacenterArchitect.Sim;
using Godot;

namespace DatacenterArchitect.World;

/// <summary>
/// Draws every rack with GPU instancing: the hall is cut into chunks of 16 × 16 cells and each chunk holds
/// one MultiMesh for rack bodies, fronts, racks waiting to be fitted and the servers inside them.
/// A chunk is only rebuilt when one of its racks changes, so placing a rack in a hall of thousands
/// costs as much as a hall of a few hundred. Chunks are frustum-culled by the engine, and server
/// faceplates fade out at a distance where their detail is smaller than a pixel.
/// </summary>
public partial class RackRenderer : Node3D
{
    public const int ChunkSize = 16;
    /// <summary>Servers are drawn up to this camera distance (metres); further away the plain rack front shows.</summary>
    public const float ServerRange = 95f;

    sealed class Chunk
    {
        public long Signature;
        public Node3D Node;
    }

    readonly Dictionary<(int floor, int cx, int cz), Chunk> _chunks = new();
    readonly Dictionary<(int floor, int cx, int cz), List<(PlacedObject o, float y)>> _scratch = new();

    public int ChunkCount => _chunks.Count;
    public int LastRebuilt { get; private set; }

    static readonly BoxMesh BodyMesh = new() { Size = new Vector3(0.985f, 2.1f, 0.95f) };
    static readonly BoxMesh FrontMesh = new() { Size = new Vector3(0.9f, 1.95f, 0.01f) };
    static readonly BoxMesh GhostMesh = new() { Size = new Vector3(0.95f, 2.1f, 0.92f) };
    /// <summary>Perforated doors front and back (see <see cref="HallLook.Door"/>), with a swing handle on each.</summary>
    static readonly QuadMesh DoorMesh = new() { Size = new Vector2(0.94f, 2.02f) };
    static readonly BoxMesh HandleMesh = new() { Size = new Vector3(0.035f, 0.2f, 0.03f) };
    static readonly BoxMesh CapMesh = new() { Size = new Vector3(0.99f, 0.03f, 0.955f) };

    public void Clear()
    {
        foreach (var c in _chunks.Values) c.Node.QueueFree();
        _chunks.Clear();
    }

    /// <summary>Bring the chunks in line with the racks that are visible now; returns how many chunks were rebuilt.</summary>
    public int Update(IEnumerable<(PlacedObject o, float y)> racks)
    {
        foreach (var l in _scratch.Values) l.Clear();
        foreach (var (o, y) in racks)
        {
            var key = (o.Floor, FloorDiv(o.X, ChunkSize), FloorDiv(o.Z, ChunkSize));
            if (!_scratch.TryGetValue(key, out var list)) _scratch[key] = list = new List<(PlacedObject, float)>();
            list.Add((o, y));
        }
        var rebuilt = 0;
        foreach (var (key, list) in _scratch)
        {
            if (list.Count == 0)
            {
                if (_chunks.Remove(key, out var gone)) gone.Node.QueueFree();
                continue;
            }
            var sig = Signature(list);
            if (_chunks.TryGetValue(key, out var chunk) && chunk.Signature == sig) continue;
            chunk?.Node.QueueFree();
            _chunks[key] = new Chunk { Signature = sig, Node = Build(list) };
            AddChild(_chunks[key].Node);
            rebuilt++;
        }
        LastRebuilt = rebuilt;
        return rebuilt;
    }

    static int FloorDiv(int a, int b) => (int)Math.Floor(a / (double)b);

    static long Signature(List<(PlacedObject o, float y)> list)
    {
        var h = new HashCode();
        h.Add(list.Count);
        foreach (var (o, y) in list) { h.Add(o.Id); h.Add(o.X); h.Add(o.Z); h.Add(o.Rot); h.Add((int)o.Status); h.Add(y); }
        return h.ToHashCode();
    }

    static Node3D Build(List<(PlacedObject o, float y)> list)
    {
        var node = new Node3D();
        var bodies = new List<Transform3D>(); var fronts = new List<Transform3D>(); var ghosts = new List<Transform3D>();
        var doors = new List<Transform3D>(); var handles = new List<Transform3D>(); var caps = new List<Transform3D>();
        var plates = new List<float>();
        var plateCount = 0;
        foreach (var (o, y) in list)
        {
            var pos = new Vector3(o.X + 0.5f, y, o.Z + 0.5f);
            var basis = Basis.Identity.Rotated(Vector3.Up, -Mathf.Pi / 2 * o.Rot);
            if (o.Status <= ObjStatus.Delivered) { ghosts.Add(new Transform3D(basis, pos + new Vector3(0, 1.05f, 0))); continue; }
            bodies.Add(new Transform3D(basis, pos + new Vector3(0, 1.05f, 0)));
            fronts.Add(new Transform3D(basis, pos + basis * new Vector3(0, 1.05f, 0.476f)));
            var back = basis.Rotated(Vector3.Up, Mathf.Pi);
            doors.Add(new Transform3D(basis, pos + basis * new Vector3(0, 1.05f, 0.494f)));
            doors.Add(new Transform3D(back, pos + basis * new Vector3(0, 1.05f, -0.477f)));
            handles.Add(new Transform3D(basis, pos + basis * new Vector3(0.39f, 1.05f, 0.51f)));
            handles.Add(new Transform3D(back, pos + basis * new Vector3(-0.39f, 1.05f, -0.493f)));
            caps.Add(new Transform3D(basis, pos + new Vector3(0, 2.115f, 0)));
            // servers go in once a fitter has installed the rack; their LEDs light up when it is live
            if (o.Status < ObjStatus.Placed) continue;
            var live = o.Status == ObjStatus.Live;
            var rack = new Transform3D(basis, pos);
            foreach (var u in ServerFill.For(o.Id))
            {
                Append(plates, rack * ServerFill.Plate(u));
                plates.Add(u.Color.R); plates.Add(u.Color.G); plates.Add(u.Color.B); plates.Add(1);
                var c = ServerFill.Custom(u, o.Id, live);
                plates.Add(c.R); plates.Add(c.G); plates.Add(c.B); plates.Add(c.A);
                plateCount++;
            }
        }
        Add(node, BodyMesh, bodies, Models.Mat(new Color("#1a1e23"), 0.6f, 0.25f), true);
        Add(node, FrontMesh, fronts, Models.Mat(new Color("#3a414a"), 0.9f), false);
        Add(node, GhostMesh, ghosts, Models.Mat(new Color(1, 1, 1, 0.22f), 1f), false);
        Add(node, DoorMesh, doors, HallLook.Door, false);
        Add(node, HandleMesh, handles, Models.Mat(new Color("#14171a"), 0.4f, 0.6f), false);
        Add(node, CapMesh, caps, Models.Mat(new Color("#15181c"), 0.6f, 0.3f), false);
        if (plateCount > 0)
        {
            var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, UseCustomData = true, Mesh = ServerFill.PlateMesh };
            mm.InstanceCount = plateCount;
            mm.Buffer = plates.ToArray();
            node.AddChild(new MultiMeshInstance3D
            {
                Name = "Servers", Multimesh = mm, MaterialOverride = ServerFill.Material,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                VisibilityRangeEnd = ServerRange, VisibilityRangeEndMargin = 12, VisibilityRangeFadeMode = GeometryInstance3D.VisibilityRangeFadeModeEnum.Disabled,
            });
        }
        return node;
    }

    static void Add(Node3D root, Mesh mesh, List<Transform3D> xf, Material mat, bool shadow)
    {
        if (xf.Count == 0) return;
        var buf = new List<float>(xf.Count * 12);
        foreach (var t in xf) Append(buf, t);
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = mesh };
        mm.InstanceCount = xf.Count;
        mm.Buffer = buf.ToArray();
        root.AddChild(new MultiMeshInstance3D { Multimesh = mm, MaterialOverride = mat, CastShadow = shadow ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off });
    }

    /// <summary>MultiMesh buffer layout for a Transform3D: the basis rows with the origin at the end of each.</summary>
    static void Append(List<float> b, Transform3D t)
    {
        b.Add(t.Basis.X.X); b.Add(t.Basis.Y.X); b.Add(t.Basis.Z.X); b.Add(t.Origin.X);
        b.Add(t.Basis.X.Y); b.Add(t.Basis.Y.Y); b.Add(t.Basis.Z.Y); b.Add(t.Origin.Y);
        b.Add(t.Basis.X.Z); b.Add(t.Basis.Y.Z); b.Add(t.Basis.Z.Z); b.Add(t.Origin.Z);
    }
}
