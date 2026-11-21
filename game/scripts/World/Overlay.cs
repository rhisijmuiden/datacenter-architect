using System;
using System.Collections.Generic;
using System.Linq;
using DatacenterArchitect.Core;
using DatacenterArchitect.Sim;
using Godot;

namespace DatacenterArchitect.World;

public enum OverlayMode { None, Power, Cooling, Network }

/// <summary>
/// X-ray overlay over the world. Power: amber lines from the grid connection via transformers, switchgear,
/// UPS and PDUs to the rack rows, with dots that run faster the busier a link is; overloaded links and racks
/// without power turn red. Cooling: cold air from the CRAH units to the rows, warm water to the chillers
/// and cooling towers, and racks coloured by the temperature of their hall. Network: orange fiber from the carrier pits
/// in the street through your own pits and ducts to the meet-me room, the core routers, the spine switches and the rows;
/// dots run faster the busier a link is, congested links and offline racks turn red.
/// </summary>
public partial class Overlay : Node3D
{
    record struct Seg(Vector3 A, Vector3 B, float Speed, Color Color, int Dots);

    GameState _s;
    OverlayMode _mode;
    readonly List<Seg> _segs = new();
    MultiMesh _dots;
    double _rebuild;
    float _t;
    Node3D _root;
    static StandardMaterial3D _mat;

    public OverlayMode Mode
    {
        get => _mode;
        set { _mode = value; _rebuild = 0; Visible = value != OverlayMode.None; }
    }

    static StandardMaterial3D Mat()
    {
        if (_mat != null) return _mat;
        _mat = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha, NoDepthTest = true, RenderPriority = 10,
        };
        return _mat;
    }

    public void Sync(GameState s, double delta)
    {
        _s = s;
        if (_mode == OverlayMode.None || s == null) return;
        _rebuild -= delta;
        if (_rebuild <= 0) { _rebuild = 0.5; Rebuild(); }
        _t += (float)delta;
        if (_dots == null) return;
        var i = 0;
        foreach (var seg in _segs)
        {
            for (var k = 0; k < seg.Dots; k++)
            {
                var f = (_t * seg.Speed / Math.Max(0.5f, seg.A.DistanceTo(seg.B)) * 3f + k / (float)seg.Dots) % 1f;
                _dots.SetInstanceTransform(i++, new Transform3D(Basis.Identity, seg.A.Lerp(seg.B, f)));
            }
        }
    }

    // ---------------- geometry helpers ----------------
    static Vector3 Top(GameState s, PlacedObject o, float h)
    {
        var (w, d) = GameState.Footprint(o);
        return new Vector3(o.X + w / 2f, WorldView.ObjectY(s, o) + h, o.Z + d / 2f);
    }

    static T Nearest<T>(IEnumerable<T> list, Vector3 p, Func<T, Vector3> pos) where T : class
        => list.OrderBy(x => pos(x).DistanceSquaredTo(p)).FirstOrDefault();

    void Add(Vector3 a, Vector3 b, float ratio, Color c, bool flow = true)
    {
        var len = a.DistanceTo(b);
        if (len < 0.05f) return;
        var speed = flow ? Mathf.Clamp(0.35f + ratio * 1.6f, 0.35f, 2.4f) : 0.12f;
        var dots = flow ? Mathf.Clamp((int)(len / 2.2f), 1, 10) : Mathf.Clamp((int)(len / 5f), 1, 3);
        _segs.Add(new Seg(a, b, speed, c, dots));
    }

    static Transform3D LineXf(Vector3 a, Vector3 b, float thick)
    {
        var dir = b - a;
        var len = dir.Length();
        var x = dir / len;
        var up = Mathf.Abs(x.Dot(Vector3.Up)) > 0.95f ? Vector3.Forward : Vector3.Up;
        var z = x.Cross(up).Normalized();
        var y = z.Cross(x).Normalized();
        return new Transform3D(new Basis(x * len, y * thick, z * thick), (a + b) / 2);
    }

    /// <summary>Rows of racks: neighbouring racks in a hall on one line facing the same way.</summary>
    public static List<List<PlacedObject>> Rows(IEnumerable<PlacedObject> racks)
    {
        var rows = new List<List<PlacedObject>>();
        foreach (var g in racks.GroupBy(o => (o.Floor, o.Rot % 2, o.Rot % 2 == 0 ? o.Z : o.X)))
        {
            var along = g.OrderBy(o => o.Rot % 2 == 0 ? o.X : o.Z).ToList();
            var cur = new List<PlacedObject>();
            foreach (var o in along)
            {
                if (cur.Count > 0)
                {
                    var p = cur[^1];
                    var gap = (o.Rot % 2 == 0 ? o.X - p.X : o.Z - p.Z);
                    if (gap > 1) { rows.Add(cur); cur = new List<PlacedObject>(); }
                }
                cur.Add(o);
            }
            if (cur.Count > 0) rows.Add(cur);
        }
        return rows;
    }

    // ---------------- build ----------------
    void Rebuild()
    {
        _root?.QueueFree();
        _root = new Node3D(); AddChild(_root);
        _segs.Clear(); _dots = null;
        var s = _s;
        var r = s.Infra;
        var lines = new List<(Transform3D, Color)>();
        var plates = new List<(Transform3D, Color)>();
        var objs = s.Objects.Where(o => o.Status >= ObjStatus.Placed || !o.Def.Chain).ToList();
        var racks = objs.Where(o => o.Kind == ObjKind.Rack).ToList();
        var halls = s.Rooms.Where(x => x.Type == RoomType.DataHall).ToList();
        Room HallOf(PlacedObject o) => halls.FirstOrDefault(h => h.Floor == o.Floor && h.Contains(o.X, o.Z));
        var power = Brand.Power; var cool = Brand.Cooling; var bad = Brand.Critical;
        var standby = new Color(Brand.Power, 0.45f);

        if (_mode == OverlayMode.Power)
        {
            var g = s.GridStation;
            var station = new Vector3(g.x + g.w / 2f, 3.2f, g.z + g.d / 2f);
            var link = r.Links.ToDictionary(l => l.Link);
            Color C(PowerLink l) => link[l].Ratio > 1 ? bad : power;
            float R(PowerLink l) => (float)Math.Min(1.5, link[l].Ratio);
            var trafos = objs.Where(o => o.Kind == ObjKind.Transformer).ToList();
            var switches = objs.Where(o => o.Kind == ObjKind.Switchgear).ToList();
            var ups = objs.Where(o => o.Kind == ObjKind.Ups).ToList();
            var pdus = objs.Where(o => Catalog.IsPdu(o.Kind)).ToList();
            foreach (var t in trafos) Add(station, Top(s, t, 2.4f), R(PowerLink.Grid), C(PowerLink.Grid));
            foreach (var w in switches)
            {
                var p = Top(s, w, 2.4f);
                var src = trafos.Count == 0 ? station : Top(s, Nearest(trafos, p, t => Top(s, t, 2.4f)), 2.4f);
                Add(src, p, R(PowerLink.Transformers), C(PowerLink.Transformers));
            }
            foreach (var gen in objs.Where(o => o.Kind == ObjKind.Generator))
            {
                var p = Top(s, gen, 2.8f);
                var w = Nearest(switches, p, x => Top(s, x, 2.4f));
                if (w != null) Add(p, Top(s, w, 2.4f), 0, standby, false);
            }
            var gens = objs.Where(o => o.Kind == ObjKind.Generator).ToList();
            foreach (var tank in objs.Where(o => o.Kind is ObjKind.DieselTank or ObjKind.UndergroundTank))
            {
                var under = tank.Kind == ObjKind.UndergroundTank;
                var p = under ? Top(s, tank, -1.1f) : Top(s, tank, 1.4f);
                if (under)
                {
                    var (w, d) = GameState.Footprint(tank);
                    plates.Add((new Transform3D(Basis.Identity.Scaled(new Vector3(w - 0.2f, 1.6f, d - 0.2f)), p), new Color(Brand.Power, 0.3f)));
                }
                var gn = Nearest(gens, p, x => Top(s, x, 0.4f));
                if (gn != null) Add(p, Top(s, gn, 0.4f), 0, standby, false);
            }
            // feeders follow the power tree: every UPS to its switchboard, every PDU or busbar run to its UPS (or switchboard)
            var tree = r.Tree;
            Vector3? Upstream(PowerNode n, Vector3 p)
            {
                var up = n?.Parent?.Obj;
                if (up != null) return Top(s, up, 2.4f);
                return null;
            }
            foreach (var u in ups)
            {
                var p = Top(s, u, 2.4f);
                var src = Upstream(tree?.Node(u), p);
                if (src != null) Add(src.Value, p, R(PowerLink.Switchgear), C(PowerLink.Switchgear));
            }
            foreach (var b in objs.Where(o => o.Kind == ObjKind.Battery))
            {
                var p = Top(s, b, 2.2f);
                var u = Nearest(ups, p, x => Top(s, x, 2.4f));
                if (u != null) Add(p, Top(s, u, 2.4f), 0, standby, false);
            }
            foreach (var pd in pdus)
            {
                var p = Top(s, pd, 2.2f);
                var node = tree?.Node(pd);
                var src = Upstream(node, p);
                if (src != null) Add(src.Value, p, (float)Math.Min(1.5, node.FailoverRatio), node.FailoverShort ? bad : power);
            }
            var busbarAt = new Dictionary<Room, Vector3>();
            foreach (var h in halls.Where(h => h.Busbar))
            {
                var p = new Vector3(h.X + h.W / 2f, WorldView.Base(h.Floor) + 2.6f, h.Z + 0.6f);
                busbarAt[h] = p;
                var run = tree?.SourcesIn(h).FirstOrDefault(n => n.Kind == PowerNodeKind.Busbar);
                var src = Upstream(run, p);
                if (src != null) Add(src.Value, p, R(PowerLink.Ups), C(PowerLink.Ups));
            }
            foreach (var row in Rows(racks))
            {
                var hall = HallOf(row[0]);
                var a = Top(s, row[0], 2.45f); var b = Top(s, row[^1], 2.45f);
                var mid = (a + b) / 2;
                var down = row.Any(o => r.Down.Contains(o.Id));
                Vector3? src = null;
                if (hall != null && busbarAt.TryGetValue(hall, out var bb)) src = bb;
                var pdu = hall == null ? null : Nearest(pdus.Where(p => HallOf(p) == hall), mid, x => Top(s, x, 2.2f));
                if (src == null && pdu != null) src = Top(s, pdu, 2.2f);
                var hi = hall == null ? null : r.Halls.FirstOrDefault(x => x.Room == hall);
                var ratio = hi == null || hi.DistributionKw <= 0 ? 1f : (float)(hi.ItKw / hi.DistributionKw);
                if (src != null) Add(src.Value, a, ratio, down ? bad : power);
                if (row.Count > 1) Add(a, b, ratio, down ? bad : power);
            }
            foreach (var o in racks.Where(o => o.Status == ObjStatus.Live))
                plates.Add((new Transform3D(Basis.Identity.Scaled(new Vector3(0.9f, 0.05f, 0.9f)), Top(s, o, 2.14f)), r.Down.Contains(o.Id) ? new Color(bad, 0.85f) : new Color(power, 0.7f)));
        }
        else if (_mode == OverlayMode.Cooling)
        {
            var rejects = objs.Where(o => o.Def.RejectKw > 0).ToList();
            var rowsByHall = Rows(racks).GroupBy(row => HallOf(row[0])).Where(g => g.Key != null).ToDictionary(g => g.Key, g => g.ToList());
            var crahs = objs.Where(o => o.Kind == ObjKind.Crah).ToList();
            HallInfo Info(Room h) => r.Halls.FirstOrDefault(x => x.Room == h);
            // cold air: every row gets it from the nearest CRAH unit in its hall
            foreach (var (hall, rows) in rowsByHall)
            {
                var hi = Info(hall);
                var ratio = hi == null ? 0 : (float)Math.Min(1.5, hi.CoolRatio);
                var col = hi != null && hi.CoolRatio > 1 ? bad : cool;
                foreach (var row in rows)
                {
                    var mid = (Top(s, row[0], 0.3f) + Top(s, row[^1], 0.3f)) / 2;
                    var crah = Nearest(crahs.Where(c => HallOf(c) == hall), mid, x => Top(s, x, 2.3f));
                    if (crah != null) Add(Top(s, crah, 2.3f), mid, ratio, col);
                }
            }
            // warm water: every CRAH unit sends its heat to the nearest chiller or cooling tower
            foreach (var crah in crahs)
            {
                var p = Top(s, crah, 2.3f);
                var rj = Nearest(rejects, p, x => Top(s, x, 2f));
                if (rj != null) Add(p, Top(s, rj, 2f), (float)Math.Min(1.5, r.CoolRatio), r.RejectKw < r.HeatKw ? bad : new Color(Brand.Fiber.Lerp(cool, 0.35f), 0.85f));
            }
            foreach (var o in racks.Where(o => o.Status == ObjStatus.Live))
            {
                var hall = HallOf(o);
                var t = hall?.Temp ?? 20;
                var col = r.Down.Contains(o.Id) && hall?.Hot == true ? bad : t < 25 ? cool : t < 29 ? Brand.Power : bad;
                plates.Add((new Transform3D(Basis.Identity.Scaled(new Vector3(0.9f, 0.05f, 0.9f)), Top(s, o, 2.14f)), new Color(col, 0.75f)));
            }
        }

        else if (_mode == OverlayMode.Network)
        {
            var n = s.Net;
            var fiber = Brand.Fiber;
            var idle = new Color(Brand.Fiber, 0.45f);
            var upRatio = (float)Math.Min(1.5, n.Link(NetLink.Upstream).Ratio);
            var upColor = n.Served < 0.999 ? bad : fiber;
            var mmr = s.Rooms.Where(x => x.Type == RoomType.MeetMe && x.Stage == Stage.Done).ToList();
            bool InMmr(PlacedObject o) => mmr.Any(m => m.Floor == o.Floor && m.Contains(o.X, o.Z));
            var odfs = objs.Where(o => o.Kind == ObjKind.Odf && InMmr(o)).ToList();
            var cores = objs.Where(o => o.Kind == ObjKind.CoreRouter && InMmr(o)).ToList();
            var spines = objs.Where(o => Catalog.IsHallSwitch(o.Kind)).ToList();
            var dists = objs.Where(o => o.Kind == ObjKind.DistSwitch && InMmr(o)).ToList();
            // carrier pits in the street, your pits and the ducts into the building
            foreach (var p in n.Pits)
            {
                var street = new Vector3(s.StreetPit(p.Street) + 0.5f, 0.3f, s.PlotSize + Catalog.StreetPitOffset);
                var pit = Top(s, p.Pit, 0.3f);
                var used = p.Used > 0;
                Add(street, pit, used ? upRatio : 0, used ? upColor : idle, used);
                var odf = Nearest(odfs, pit, x => Top(s, x, 2.3f));
                if (odf != null) Add(pit, Top(s, odf, 2.3f), used ? upRatio : 0, used ? upColor : idle, used);
                plates.Add((new Transform3D(Basis.Identity.Scaled(new Vector3(0.9f, 0.05f, 0.9f)), pit), new Color(used ? fiber : idle, 0.8f)));
            }
            // dark fiber to peers leaves along the road to the west
            var firstPit = n.Pits.FirstOrDefault(p => p.Used > 0);
            if (firstPit != null)
            {
                var a = Top(s, firstPit.Pit, 0.3f);
                var i = 0;
                foreach (var (rival, gbps) in n.PeerFlows)
                    Add(a, new Vector3(-24, 0.3f, s.PlotSize + 3 + i++ * 1.2f), (float)Math.Min(1.5, gbps / 10), fiber);
            }
            foreach (var c in cores)
            {
                var p = Top(s, c, 2.3f);
                var odf = Nearest(odfs, p, x => Top(s, x, 2.3f));
                if (odf != null) Add(Top(s, odf, 2.3f), p, upRatio, upColor);
            }
            var coreRatio = (float)Math.Min(1.5, n.Link(NetLink.Core).Ratio);
            // the tree: distribution switches on the core, hall switches on their distribution switch (or straight on the core)
            foreach (var d in dists)
            {
                var p = Top(s, d, 2.3f);
                var c = Nearest(cores, p, x => Top(s, x, 2.3f));
                if (c != null) Add(Top(s, c, 2.3f), p, coreRatio, n.Link(NetLink.Core).Ratio > 1 ? bad : fiber);
            }
            foreach (var sp in spines)
            {
                var p = Top(s, sp, 2.4f);
                n.Uplink.TryGetValue(sp.Id, out var up);
                var ds = up > 0 ? dists.FirstOrDefault(x => x.Id == up) : null;
                var c = ds ?? Nearest(cores, p, x => Top(s, x, 2.3f));
                if (c != null) Add(Top(s, c, 2.3f), p, coreRatio, up < 0 || n.Link(NetLink.Core).Ratio > 1 ? bad : fiber);
            }
            foreach (var row in Rows(racks))
            {
                var hall = HallOf(row[0]);
                var a = Top(s, row[0], 2.35f); var b = Top(s, row[^1], 2.35f);
                var off = row.Any(o => n.Offline.Contains(o.Id));
                var sp = hall == null ? null : Nearest(spines.Where(x => HallOf(x) == hall), (a + b) / 2, x => Top(s, x, 2.4f));
                var col = off ? bad : fiber;
                if (sp != null) Add(Top(s, sp, 2.4f), a, upRatio, col);
                if (row.Count > 1) Add(a, b, upRatio, col);
            }
            foreach (var o in racks.Where(o => o.Status == ObjStatus.Live))
            {
                var col = n.Offline.Contains(o.Id) ? new Color(bad, 0.85f) : r.Down.Contains(o.Id) ? new Color(Brand.InkDim, 0.6f) : new Color(fiber, 0.7f);
                plates.Add((new Transform3D(Basis.Identity.Scaled(new Vector3(0.9f, 0.05f, 0.9f)), Top(s, o, 2.14f)), col));
            }
        }

        foreach (var seg in _segs) lines.Add((LineXf(seg.A, seg.B, seg.Color.A < 0.6f ? 0.05f : 0.08f), new Color(seg.Color, seg.Color.A * 0.55f)));
        Instances(new BoxMesh { Size = Vector3.One }, lines);
        Instances(new BoxMesh { Size = Vector3.One }, plates);
        var count = _segs.Sum(x => x.Dots);
        if (count > 0)
        {
            _dots = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, Mesh = new SphereMesh { Radius = 0.14f, Height = 0.28f, RadialSegments = 8, Rings = 4 }, InstanceCount = count };
            var i = 0;
            foreach (var seg in _segs)
                for (var k = 0; k < seg.Dots; k++) { _dots.SetInstanceColor(i, seg.Color.A < 0.6f ? new Color(seg.Color, 0.8f) : seg.Color.Lightened(0.25f)); _dots.SetInstanceTransform(i++, new Transform3D(Basis.Identity, seg.A)); }
            _root.AddChild(new MultiMeshInstance3D { Multimesh = _dots, MaterialOverride = Mat(), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        }
    }

    void Instances(Mesh mesh, List<(Transform3D xf, Color c)> list)
    {
        if (list.Count == 0) return;
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, Mesh = mesh, InstanceCount = list.Count };
        for (var i = 0; i < list.Count; i++) { mm.SetInstanceTransform(i, list[i].xf); mm.SetInstanceColor(i, list[i].c); }
        _root.AddChild(new MultiMeshInstance3D { Multimesh = mm, MaterialOverride = Mat(), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }
}
