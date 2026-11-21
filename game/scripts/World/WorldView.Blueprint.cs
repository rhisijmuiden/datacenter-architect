using System.Collections.Generic;
using System.Linq;
using DatacenterArchitect.Core;
using DatacenterArchitect.Sim;
using Godot;

namespace DatacenterArchitect.World;

/// <summary>
/// The see-through copy of a hall blueprint under the mouse (infra sketch board 8): the floor in cyan grid tiles,
/// low walls, the doors, racks as boxes with their cold aisle, and the other equipment as real models. Red when it
/// does not fit.
/// </summary>
public partial class WorldView
{
    public void ShowGhostBlueprint(Blueprint bp, int x0, int z0, int floor, bool ok, string text, IEnumerable<(int x, int z, int dir, bool glass)> newWalls)
    {
        ClearGhost();
        var y = Base(floor);
        var tint = ok ? Brand.Cooling : Brand.Critical;
        var cells = bp.CellList().ToList();
        var tiles = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = new PlaneMesh { Size = new Vector2(0.92f, 0.92f) }, InstanceCount = cells.Count };
        for (var i = 0; i < cells.Count; i++) tiles.SetInstanceTransform(i, new Transform3D(Basis.Identity, new Vector3(cells[i].x + x0 + 0.5f, y + 0.07f, cells[i].z + z0 + 0.5f)));
        _ghost.AddChild(new MultiMeshInstance3D { Multimesh = tiles, MaterialOverride = GhostMat(tint), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });

        // walls: the rectangle of a walled hall, and every wall edge of the design; new ones are brighter
        var edges = bp.Walls.Select(e => (x: e.X + x0, z: e.Z + z0, dir: e.Dir, glass: e.Glass)).ToList();
        if (bp.Walled) edges.AddRange(GameState.Perimeter(x0, z0, bp.W, bp.D).Select(e => (e.x, e.z, e.dir, false)));
        var fresh = newWalls?.Select(w => (w.x, w.z, w.dir)).ToHashSet() ?? new HashSet<(int, int, int)>();
        var wallMat = new StandardMaterial3D { AlbedoColor = new Color(tint.Lightened(0.35f), 0.8f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
        var oldMat = new StandardMaterial3D { AlbedoColor = new Color(tint, 0.35f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
        foreach (var e in edges.Distinct())
        {
            var hgt = fresh.Contains((e.x, e.z, e.dir)) || bp.Walled ? 0.9f : 0.4f;
            var pos = e.dir == 0 ? new Vector3(e.x, y + hgt / 2, e.z + 0.5f) : new Vector3(e.x + 0.5f, y + hgt / 2, e.z);
            var size = e.dir == 0 ? new Vector3(0.14f, hgt, 1f) : new Vector3(1f, hgt, 0.14f);
            _ghost.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size }, Position = pos, MaterialOverride = hgt > 0.5f ? wallMat : oldMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        }
        foreach (var d in bp.Doors)
        {
            var w = Catalog.Doors[d.Type].Width;
            var pos = d.Dir == 0 ? new Vector3(d.X + x0, y + 1.0f, d.Z + z0 + w / 2f) : new Vector3(d.X + x0 + w / 2f, y + 1.0f, d.Z + z0);
            var size = d.Dir == 0 ? new Vector3(0.3f, 2.0f, w) : new Vector3(w, 2.0f, 0.3f);
            _ghost.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size }, Position = pos, MaterialOverride = GhostMat(ok ? Brand.Ok : Brand.Critical), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        }

        // racks as boxes (a big hall has hundreds), each with its cold-aisle tile in front
        var racks = bp.Objects.Where(o => o.Kind == ObjKind.Rack).ToList();
        if (racks.Count > 0)
        {
            var box = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = new BoxMesh { Size = new Vector3(0.6f, 2.0f, 0.9f) }, InstanceCount = racks.Count };
            var aisle = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = new PlaneMesh { Size = new Vector2(0.9f, 0.9f) }, InstanceCount = racks.Count };
            for (var i = 0; i < racks.Count; i++)
            {
                var o = racks[i];
                var basis = new Basis(Vector3.Up, Mathf.DegToRad(-90 * o.Rot));
                var p = new Vector3(o.X + x0 + 0.5f, y, o.Z + z0 + 0.5f);
                box.SetInstanceTransform(i, new Transform3D(basis, p + new Vector3(0, 1.0f, 0)));
                var (dx, dz) = GameState.FrontDir(o.Rot);
                aisle.SetInstanceTransform(i, new Transform3D(Basis.Identity, p + new Vector3(dx, 0.09f, dz)));
            }
            _ghost.AddChild(new MultiMeshInstance3D { Multimesh = box, MaterialOverride = GhostMat(ok ? new Color("#8FE3F7") : Brand.Critical), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
            _ghost.AddChild(new MultiMeshInstance3D { Multimesh = aisle, MaterialOverride = GhostFlat(Brand.Cooling, 0.35f), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        }
        foreach (var o in bp.Objects.Where(o => o.Kind != ObjKind.Rack))
        {
            var po = new PlacedObject { Kind = o.Kind, X = o.X + x0, Z = o.Z + z0, Rot = o.Rot, Floor = floor };
            var (w, d) = GameState.Footprint(po);
            var n = Models.Object(o.Kind);
            n.Position = new Vector3(po.X + w / 2f, ObjectY(_state, po), po.Z + d / 2f);
            n.RotationDegrees = new Vector3(0, -90 * o.Rot, 0);
            Models.Tint(n, !ok ? Brand.Critical : Catalog.IsPdu(o.Kind) ? Brand.Power : Catalog.IsHallSwitch(o.Kind) ? Brand.Fiber : Brand.Cooling);
            _ghost.AddChild(n);
        }
        if (text != null)
            _ghost.AddChild(new Label3D
            {
                Text = text, Font = _font, FontSize = 64, PixelSize = 0.014f, OutlineSize = 14, OutlineModulate = new Color("#111D2B"),
                Modulate = Brand.Ink, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true,
                Position = new Vector3(x0 + bp.W / 2f, y + 4.0f, z0 + bp.D / 2f),
            });
    }
}
