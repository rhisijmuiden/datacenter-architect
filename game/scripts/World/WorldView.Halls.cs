using System;
using System.Linq;
using DatacenterArchitect.Sim;
using DatacenterArchitect.UI;
using Godot;
using static DatacenterArchitect.World.Models;

namespace DatacenterArchitect.World;

/// <summary>
/// Infra sketch board 7: data halls carry their code and name on the wall next to each door, a colour band along the floor
/// edge and the code on the floor in front of the door. A customer who rents the whole hall gets its logo plate on the wall.
/// </summary>
public partial class WorldView
{
    int _tenantKey;

    /// <summary>Whole-hall tenants change without a layout change (signing, leaving): redraw the signs then.</summary>
    void SyncHallTenants()
    {
        var key = 17;
        foreach (var d in _state.Deals)
            if (d.Product == Product.Hall && d.State is DealState.Active or DealState.Building) key = HashCode.Combine(key, d.Id, d.HallId, d.State);
        if (key == _tenantKey) return;
        _tenantKey = key;
        _layoutRev = -1;
    }

    /// <summary>Code and name painted on the hall floor (instead of the generic room name), plus the colour band at the walls.</summary>
    void HallFloorMark(Node3D root, Room r, float y)
    {
        var band = new Color(GameState.HallBand(r));
        var code = _state.HallCode(r);
        var px = Math.Clamp(Math.Min(r.W, r.D) * 0.0016f, 0.008f, 0.03f);
        var cx = r.X + r.W / 2f; var cz = r.Z + r.D / 2f;
        root.AddChild(new Label3D
        {
            Text = code, Font = _font, FontSize = 128, PixelSize = px, OutlineSize = 0, Modulate = new Color(band, 0.75f),
            Position = new Vector3(cx, y + 0.04f, cz), RotationDegrees = new Vector3(-90, 0, 0), DoubleSided = false,
        });
        if (!string.IsNullOrWhiteSpace(r.Name))
            root.AddChild(new Label3D
            {
                Text = r.Name.ToUpperInvariant(), Font = _font, FontSize = 64, PixelSize = px * 0.9f, OutlineSize = 0, Modulate = new Color("#1d2a36", 0.6f),
                Position = new Vector3(cx, y + 0.04f, cz + 128 * px * 0.62f), RotationDegrees = new Vector3(-90, 0, 0), DoubleSided = false,
            });
        if (r.Stage != Stage.Done) return;
        // colour band: a thin painted line just inside the walls
        const float w = 0.12f, inset = 0.14f;
        Box(root, new Vector3(cx, y + 0.026f, r.Z + inset), new Vector3(r.W - 2 * inset, 0.004f, w), band, 0.7f);
        Box(root, new Vector3(cx, y + 0.026f, r.Z + r.D - inset), new Vector3(r.W - 2 * inset, 0.004f, w), band, 0.7f);
        Box(root, new Vector3(r.X + inset, y + 0.026f, cz), new Vector3(w, 0.004f, r.D - 2 * inset), band, 0.7f);
        Box(root, new Vector3(r.X + r.W - inset, y + 0.026f, cz), new Vector3(w, 0.004f, r.D - 2 * inset), band, 0.7f);
    }

    /// <summary>Signs on the wall beside each door of a finished hall (outside face), or on the inside of the back wall when it has none.</summary>
    void HallSigns(Node3D root, Room r, float y, float wallH)
    {
        if (r.Stage != Stage.Done) return;
        var edges = GameState.Perimeter(r.X, r.Z, r.W, r.D).ToHashSet();
        var floorDoors = _state.Doors.Where(d => d.Floor == r.Floor).ToList();
        var doorEdges = floorDoors.SelectMany(d => d.Edges()).ToHashSet();
        var doors = floorDoors.Where(d => d.Edges().Any(edges.Contains)).ToList();
        var tenant = r.ShowLogo ? _state.HallTenant(r) : null;
        if (doors.Count == 0)
        {
            // no door yet: the sign hangs inside on the -Z wall, facing into the hall
            var half = r.W / 2f - 0.3f;
            HallSign(root, r, new Vector3(r.X + r.W / 2f, y, r.Z), new Vector3(0, 0, 1), 0.07f, wallH, tenant, -Math.Min(1.3f, half), half, half);
            return;
        }
        foreach (var d in doors.Take(3))
        {
            var dw = Catalog.Doors[d.Type].Width;
            Vector3 p, along, n;
            if (d.Dir == 0) { p = new Vector3(d.X, y, d.Z + dw / 2f); along = new Vector3(0, 0, 1); n = new Vector3(r.Contains(d.X, d.Z) ? -1 : 1, 0, 0); }
            else { p = new Vector3(d.X + dw / 2f, y, d.Z); along = new Vector3(1, 0, 0); n = new Vector3(0, 0, r.Contains(d.X, d.Z) ? -1 : 1); }
            var outer = _state.IsOuterEdge(d.Edges().First());
            // free wall on both sides of the door, up to a corner, another door or the end of the corridor in front of it
            var plus = WallRun(r, d, dw, n, 1, edges, doorEdges);
            var minus = WallRun(r, d, dw, n, -1, edges, doorEdges);
            // sign node: local +X is the viewer's right
            var rightWorld = new Vector3(n.Z, 0, -n.X);
            var plusIsRight = rightWorld.Dot(along) > 0;
            float right = plusIsRight ? plus : minus, left = plusIsRight ? minus : plus;
            HallSign(root, r, p, n, outer ? 0.14f : 0.075f, wallH, tenant, dw / 2f + 0.15f, dw / 2f + right, dw / 2f + left);
            FloorArrow(root, r, p + n * 1.1f, n, y);
        }
    }

    /// <summary>Metres of free wall from the edge of a door along the hall, on the side <paramref name="dir"/> (+1 or -1 along the wall).</summary>
    int WallRun(Room r, Door d, int dw, Vector3 n, int dir, System.Collections.Generic.HashSet<(int, int, int)> edges, System.Collections.Generic.HashSet<(int, int, int)> doorEdges)
    {
        // the cell just outside the hall at a wall edge
        (int, int) Outside(int x, int z) => d.Dir == 0 ? (n.X < 0 ? x - 1 : x, z) : (x, n.Z < 0 ? z - 1 : z);
        var o0 = Outside(d.X, d.Z);
        var refRoom = _state.RoomAt(o0.Item1, o0.Item2, r.Floor);
        var count = 0;
        for (var i = 0; i < 40; i++)
        {
            var off = dir > 0 ? dw + i : -1 - i;
            var e = d.Dir == 0 ? (d.X, d.Z + off, 0) : (d.X + off, d.Z, 1);
            if (!edges.Contains(e) || doorEdges.Contains(e)) break;
            var o = Outside(e.Item1, e.Item2);
            if (_state.RoomAt(o.Item1, o.Item2, r.Floor) != refRoom) break;
            count++;
        }
        return count;
    }

    /// <summary>
    /// One sign: a dark plate with a colour stripe, the code big and the name small; the customer's logo plate next to it.
    /// It goes on the side of the door with the most free wall (right/left: metres of wall from the door centre) and shrinks to fit.
    /// </summary>
    void HallSign(Node3D root, Room r, Vector3 at, Vector3 n, float face, float wallH, Deal tenant, float gap, float right, float left)
    {
        var band = new Color(GameState.HallBand(r));
        var cut = wallH < 2.5f;
        var plateH = cut ? 0.62f : 0.9f;
        var named = !string.IsNullOrWhiteSpace(r.Name);
        var side = right >= left ? 1 : -1;
        var room = (side > 0 ? right : left) - gap - 0.1f;
        var plateW = Math.Min(named ? (cut ? 1.9f : 2.6f) : (cut ? 1.1f : 1.5f), room);
        if (plateW < 0.7f) return;
        if (plateW < (cut ? 1.5f : 2.0f)) named = false;
        var cy = cut ? wallH - plateH / 2 - 0.08f : 2.05f;
        var yaw = Mathf.RadToDeg(Mathf.Atan2(n.X, n.Z));
        var sign = new Node3D { Position = at + n * face + Vector3.Up * cy, RotationDegrees = new Vector3(0, yaw, 0), Name = "HallSign" };
        root.AddChild(sign);
        var px = side * (gap + plateW / 2f);
        Box(sign, new Vector3(px, 0, 0.01f), new Vector3(plateW, plateH, 0.02f), new Color("#142334"), 0.6f);
        Box(sign, new Vector3(px - plateW / 2f + 0.05f, 0, 0.022f), new Vector3(0.06f, plateH - 0.08f, 0.004f), band, 0.5f);
        var codeSize = plateH * (named ? 0.42f : 0.55f);
        sign.AddChild(new Label3D
        {
            Text = _state.HallCode(r), Font = _font, FontSize = 128, PixelSize = codeSize / 128 * 1.25f, OutlineSize = 0, Modulate = band,
            HorizontalAlignment = HorizontalAlignment.Left, Position = new Vector3(px - plateW / 2f + 0.16f, named ? plateH * 0.14f : 0, 0.026f),
            Shaded = true, DoubleSided = false,
        });
        if (named)
        {
            var text = r.Name.ToUpperInvariant();
            var fit = (plateW - 0.26f) / Math.Max(1, _font.GetStringSize(text, HorizontalAlignment.Left, -1, 64).X);
            sign.AddChild(new Label3D
            {
                Text = text, Font = _font, FontSize = 64, PixelSize = Math.Min(plateH * 0.2f / 64 * 1.25f, fit), OutlineSize = 0, Modulate = new Color("#eaf2f8"),
                HorizontalAlignment = HorizontalAlignment.Left, Position = new Vector3(px - plateW / 2f + 0.16f, -plateH * 0.25f, 0.026f), Shaded = true, DoubleSided = false,
            });
        }
        if (tenant == null) return;
        // the customer's logo on a white plate beside the hall sign, or on the other side of the door when there is no room
        var lw = cut ? 1.5f : 2.1f;
        var rest = room - plateW - 0.15f;
        float lx;
        var other = (side > 0 ? left : right) - gap - 0.1f;
        if (rest >= lw) lx = side * (gap + plateW + 0.15f + lw / 2f);
        else if (other >= lw) lx = -side * (gap + lw / 2f);
        else if (Math.Max(rest, other) >= 0.9f) { lw = Math.Max(rest, other); lx = rest >= other ? side * (gap + plateW + 0.15f + lw / 2f) : -side * (gap + lw / 2f); }
        else return;
        var name = CompanyPanel.DealName(tenant);
        var logo = new Node3D { Position = new Vector3(lx, 0, 0) };
        sign.AddChild(logo);
        Box(logo, new Vector3(0, 0, 0.01f), new Vector3(lw, plateH, 0.02f), new Color("#f6f7f8"), 0.5f);
        var mark = Math.Min(plateH * 0.8f, lw - 0.1f);
        var mat = LogoArt.Instance?.MaterialFor(CompanyLogo.ForCustomer(tenant), CompanyLogo.InitialsOf(name));
        if (mat != null)
            logo.AddChild(new MeshInstance3D
            {
                Mesh = new QuadMesh { Size = new Vector2(mark, mark) }, MaterialOverride = mat, Position = new Vector3(-lw / 2f + 0.08f + mark / 2f, 0, 0.024f),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Name = "CustomerLogo",
            });
        var textW = lw - mark - 0.28f;
        if (textW < 0.3f) return;
        var color = new Color(CompanyLogo.ForCustomer(tenant).Primary);
        if (color.Luminance > 0.62f) color = new Color("#1f2b38");
        var fitName = textW / Math.Max(1, _font.GetStringSize(name, HorizontalAlignment.Left, -1, 64).X);
        logo.AddChild(new Label3D
        {
            Text = name, Font = _font, FontSize = 64, PixelSize = Math.Min(plateH * 0.3f / 64 * 1.25f, fitName),
            OutlineSize = 0, Modulate = color, HorizontalAlignment = HorizontalAlignment.Left, Position = new Vector3(-lw / 2f + mark + 0.18f, 0, 0.026f), Shaded = true, DoubleSided = false,
        });
    }

    /// <summary>The hall code painted on the corridor floor in front of the door, pointing into the hall.</summary>
    void FloorArrow(Node3D root, Room r, Vector3 at, Vector3 n, float y)
    {
        var b = _state.BuildingOf(r);
        var cx = Mathf.FloorToInt(at.X); var cz = Mathf.FloorToInt(at.Z);
        if (b == null || !b.Contains(cx, cz) || _state.RoomAt(cx, cz, r.Floor) is { Type: RoomType.DataHall }) return;
        if (_state.ObjectAt(cx, cz, r.Floor) != null) return;
        var band = new Color(GameState.HallBand(r));
        // the text reads from the corridor, its top towards the door
        var yaw = Mathf.RadToDeg(Mathf.Atan2(-n.X, -n.Z));
        root.AddChild(new Label3D
        {
            Text = _state.HallCode(r), Font = _font, FontSize = 96, PixelSize = 0.006f, OutlineSize = 0, Modulate = new Color(band, 0.85f),
            Position = new Vector3(at.X, y + 0.035f, at.Z), RotationDegrees = new Vector3(-90, yaw + 180, 0), DoubleSided = false,
        });
    }
}
