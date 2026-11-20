using System;
using System.Collections.Generic;
using System.Linq;
using DatacenterArchitect.Core;
using DatacenterArchitect.Sim;
using Godot;

namespace DatacenterArchitect.World;

/// <summary>
/// Office interiors (playtest): each finished office room gets the look of its style. A painted feature wall on the
/// back wall, a floor finish, framed art (standard and formal) or wall screens with live charts (support/tech),
/// screens on the desks, and a rug under the director's desk. Rebuilt with the objects, only on the floor in view.
/// </summary>
public partial class WorldView
{
    /// <summary>Feature wall, floor and accent colour per style.</summary>
    public static (Color wall, Color floor, Color accent) StyleColors(InteriorStyle s) => s switch
    {
        InteriorStyle.Tech => (new Color("#26323f"), new Color("#4b525a"), Brand.Cooling),
        InteriorStyle.Formal => (new Color("#1f3346"), new Color("#6b4a32"), new Color("#c9a45c")),
        _ => (new Color("#8fae96"), new Color("#c9a77c"), Brand.Power),
    };

    static readonly Color[] ArtColors =
    {
        new("#d9493b"), new("#f2c14e"), new("#3f7fc2"), new("#4f9d5a"), new("#e47a3a"), new("#8a5bb5"), new("#e9e4da"), new("#2b3a4a"),
    };

    void OfficeDecor()
    {
        var wall = new Dictionary<Color, List<Transform3D>>();
        var frames = new List<Transform3D>(); var gold = new List<Transform3D>(); var canvases = new List<(Transform3D, Color)>();
        var screens = new List<Transform3D>(); var glow = new List<(Transform3D, Color)>(); var rugs = new List<Transform3D>(); var strips = new List<(Transform3D, Color)>();
        var h = _fullWalls ? FloorH - 0.05f : CutH;
        foreach (var r in _state.Rooms.Where(r => GameState.Furnishable(r.Type) && r.Stage == Stage.Done && r.Floor == _viewFloor))
        {
            var b = _state.BuildingOf(r);
            if (b == null || b.Stage != Stage.Done || !Open(b)) continue;
            var y = Base(r.Floor);
            var style = _state.StyleOf(r);
            var (wc, _, accent) = StyleColors(style);
            if (!wall.TryGetValue(wc, out var wl)) wall[wc] = wl = new();
            // the feature wall is the back wall (north side) of the room, skipping door openings
            var doorEdges = _state.DoorsOf(r).SelectMany(d => d.Edges()).ToHashSet();
            var free = new List<int>();
            for (var x = r.X; x < r.X + r.W; x++)
            {
                if (doorEdges.Contains((x, r.Z, 1))) continue;
                free.Add(x);
                wl.Add(new Transform3D(Basis.FromScale(new Vector3(1.0f, h - 0.02f, 0.03f)), new Vector3(x + 0.5f, y + (h - 0.02f) / 2, r.Z + 0.1f)));
                if (style == InteriorStyle.Tech) strips.Add((new Transform3D(Basis.FromScale(new Vector3(1.0f, 0.025f, 0.02f)), new Vector3(x + 0.5f, y + 0.08f, r.Z + 0.12f)), accent));
                if (style == InteriorStyle.Formal) strips.Add((new Transform3D(Basis.FromScale(new Vector3(1.0f, 0.03f, 0.02f)), new Vector3(x + 0.5f, y + 0.9f, r.Z + 0.12f)), accent));
            }
            // art or screens every few metres along the feature wall
            var artY = y + Math.Min(1.55f, h - 0.45f);
            for (var i = 1; i + 1 < free.Count; i += 3)
            {
                if (free[i + 1] != free[i] + 1) continue;
                var c = new Vector3(free[i] + 1f, artY, r.Z + 0.14f);
                var hsh = (uint)(r.Id * 7919 + free[i] * 104729);
                if (style == InteriorStyle.Tech)
                {
                    screens.Add(new Transform3D(Basis.FromScale(new Vector3(1.5f, 0.85f, 0.05f)), c));
                    // a dashboard: bars in brand colours
                    for (var k = 0; k < 6; k++)
                    {
                        var bh = 0.15f + (hsh >> (k * 3) & 7) * 0.07f;
                        var col = k % 3 == 0 ? Brand.Power : k % 3 == 1 ? Brand.Cooling : Brand.Ok;
                        glow.Add((new Transform3D(Basis.FromScale(new Vector3(0.14f, bh, 0.01f)), c + new Vector3(-0.55f + k * 0.22f, -0.32f + bh / 2, 0.03f)), col));
                    }
                }
                else
                {
                    var size = style == InteriorStyle.Formal ? new Vector2(1.0f, 0.75f) : new Vector2(1.2f, 0.8f);
                    (style == InteriorStyle.Formal ? gold : frames).Add(new Transform3D(Basis.FromScale(new Vector3(size.X, size.Y, 0.04f)), c));
                    canvases.Add((new Transform3D(Basis.FromScale(new Vector3(size.X - 0.1f, size.Y - 0.1f, 0.01f)), c + new Vector3(0, 0, 0.022f)), style == InteriorStyle.Formal ? new Color("#3b4a3a") : new Color("#efe9dc")));
                    // abstract blocks of colour (standard) or a landscape horizon (formal)
                    for (var k = 0; k < 3; k++)
                    {
                        var col = ArtColors[(hsh >> (k * 4)) % (uint)ArtColors.Length];
                        if (style == InteriorStyle.Formal) col = k == 0 ? new Color("#7d9cb5") : k == 1 ? new Color("#5d7a4c") : new Color("#c9a45c");
                        var bw = style == InteriorStyle.Formal ? size.X - 0.12f : 0.25f + (hsh >> (k * 5) & 3) * 0.08f;
                        var bhh = style == InteriorStyle.Formal ? (size.Y - 0.12f) / 3 : 0.2f + (hsh >> (k * 6) & 3) * 0.06f;
                        var off = style == InteriorStyle.Formal ? new Vector3(0, (size.Y - 0.12f) / 3 * (1 - k), 0) : new Vector3(-0.3f + k * 0.3f, -0.1f + k * 0.08f, 0);
                        canvases.Add((new Transform3D(Basis.FromScale(new Vector3(bw, bhh, 0.008f)), c + new Vector3(0, 0, 0.03f) + off), col));
                    }
                }
            }
            // screens on the desks: two per desk in a support office, one elsewhere; a rug under the director's desk
            foreach (var o in _state.Objects.Where(o => o.Kind == ObjKind.Desk && o.Floor == r.Floor && r.Contains(o.X, o.Z)))
            {
                var basis = new Basis(Vector3.Up, -Mathf.Pi / 2 * o.Rot);
                var centre = new Vector3(o.X + (o.Rot % 2 == 0 ? 1f : 0.5f), y, o.Z + (o.Rot % 2 == 0 ? 0.5f : 1f));
                var n = style == InteriorStyle.Tech ? 2 : style == InteriorStyle.Formal ? 0 : 1;
                for (var k = 0; k < n; k++)
                {
                    var p = centre + basis * new Vector3(n == 1 ? 0 : -0.3f + k * 0.6f, 1.02f, -0.22f);
                    screens.Add(new Transform3D(basis * Basis.FromScale(new Vector3(0.55f, 0.34f, 0.03f)), p));
                    glow.Add((new Transform3D(basis * Basis.FromScale(new Vector3(0.5f, 0.29f, 0.005f)), p + basis * new Vector3(0, 0, 0.018f)), style == InteriorStyle.Tech ? new Color("#5ad1f2") : new Color("#cfe4f2")));
                }
                if (style == InteriorStyle.Formal && r.Type == RoomType.Executive)
                    rugs.Add(new Transform3D(basis * Basis.FromScale(new Vector3(3.2f, 1, 2.4f)), centre + basis * new Vector3(0, 0.022f, 0.4f)));
            }
        }
        foreach (var (c, list) in wall) Multi(_objects, new BoxMesh { Size = Vector3.One }, list, c, false);
        Multi(_objects, new BoxMesh { Size = Vector3.One }, frames, new Color("#2b3036"), false);
        Multi(_objects, new BoxMesh { Size = Vector3.One }, gold, new Color("#b8954f"), false);
        Multi(_objects, new BoxMesh { Size = Vector3.One }, screens, new Color("#15191e"), false);
        Multi(_objects, new PlaneMesh { Size = Vector2.One }, rugs, new Color("#6e2a2a"), false);
        foreach (var grp in canvases.GroupBy(c => c.Item2)) Multi(_objects, new BoxMesh { Size = Vector3.One }, grp.Select(g => g.Item1).ToList(), grp.Key, false);
        foreach (var grp in glow.Concat(strips).GroupBy(c => c.Item2))
        {
            var mat = new StandardMaterial3D { AlbedoColor = grp.Key, EmissionEnabled = true, Emission = grp.Key, EmissionEnergyMultiplier = 1.2f };
            MultiMat(_objects, new BoxMesh { Size = Vector3.One }, grp.Select(g => g.Item1).ToList(), mat);
        }
    }
}
