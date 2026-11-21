using System.Collections.Generic;
using System.Linq;
using DatacenterArchitect.Core;
using DatacenterArchitect.Sim;
using Godot;

namespace DatacenterArchitect.World;

/// <summary>
/// Your own main fiber ducts (infra sketch board 5). The duct itself lies underground; on the surface you see the
/// handhole covers at both ends and every corner (concrete rim, steel lid, an orange "FIBER" strip) and a row of small
/// orange marker posts along the route. The network view (N) draws the ducts themselves.
/// </summary>
public partial class WorldView
{
    void DuctCovers(Node3D root)
    {
        if (_state.Ducts.Count == 0) return;
        var rims = new List<Transform3D>(); var lids = new List<Transform3D>(); var strips = new List<Transform3D>(); var posts = new List<Transform3D>();
        foreach (var h in _state.Ducts.SelectMany(d => d.Handholes()).Distinct())
        {
            var p = new Vector3(h.x + 0.5f, 0, h.z + 0.5f);
            rims.Add(new Transform3D(Basis.Identity.Scaled(new Vector3(0.92f, 0.05f, 0.92f)), p + new Vector3(0, 0.0f, 0)));
            lids.Add(new Transform3D(Basis.Identity.Scaled(new Vector3(0.72f, 0.02f, 0.72f)), p + new Vector3(0, 0.026f, 0)));
            strips.Add(new Transform3D(Basis.Identity.Scaled(new Vector3(0.34f, 0.01f, 0.07f)), p + new Vector3(0, 0.04f, 0)));
        }
        // marker posts every 12 m along the route, so you can see where the duct runs
        foreach (var d in _state.Ducts)
        {
            var cells = d.Cells();
            for (var i = 6; i < cells.Count - 3; i += 12)
                posts.Add(new Transform3D(Basis.Identity.Scaled(new Vector3(0.07f, 0.7f, 0.07f)), new Vector3(cells[i].x + 0.85f, 0.35f, cells[i].z + 0.85f)));
        }
        DuctMany(root, rims, Brand.Concrete);
        DuctMany(root, lids, new Color("#4a4f55"));
        DuctMany(root, strips, Brand.Fiber);
        DuctMany(root, posts, Brand.Fiber);
    }

    void DuctMany(Node3D root, List<Transform3D> xfs, Color c)
    {
        if (xfs.Count == 0) return;
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = new BoxMesh { Size = Vector3.One }, InstanceCount = xfs.Count };
        for (var i = 0; i < xfs.Count; i++) mm.SetInstanceTransform(i, xfs[i]);
        root.AddChild(new MultiMeshInstance3D { Multimesh = mm, MaterialOverride = new StandardMaterial3D { AlbedoColor = c, Roughness = 0.8f } });
    }

    /// <summary>Ghost of a duct while you drag it: the legs as orange strips, handholes as squares, and a label.</summary>
    public void ShowGhostDuct(FiberDuct d, bool ok, string text)
    {
        ClearGhost();
        var col = ok ? Brand.Fiber : Brand.Critical;
        // drawn on top of grass and paving, so the whole route reads while you drag
        var mat = new StandardMaterial3D { AlbedoColor = new Color(col, 0.85f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, NoDepthTest = true, RenderPriority = 5 };
        var hole = new StandardMaterial3D { AlbedoColor = new Color(ok ? Brand.Ink : Brand.Critical, 0.9f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, NoDepthTest = true, RenderPriority = 6 };
        var pts = d.Corner().ToList();
        for (var i = 1; i < pts.Count; i++)
        {
            var a = new Vector3(pts[i - 1].x + 0.5f, 0.08f, pts[i - 1].z + 0.5f);
            var b = new Vector3(pts[i].x + 0.5f, 0.08f, pts[i].z + 0.5f);
            var size = new Vector3(Mathf.Abs(b.X - a.X) + 0.6f, 0.08f, Mathf.Abs(b.Z - a.Z) + 0.6f);
            _ghost.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size }, Position = (a + b) / 2, MaterialOverride = mat });
        }
        foreach (var h in d.Handholes())
            _ghost.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.9f, 0.12f, 0.9f) }, Position = new Vector3(h.x + 0.5f, 0.12f, h.z + 0.5f), MaterialOverride = hole });
        if (text == null || pts.Count == 0) return;
        var mid = pts[pts.Count / 2];
        _ghost.AddChild(new Label3D
        {
            Text = text, Font = _font, FontSize = 64, PixelSize = 0.014f, OutlineSize = 14, OutlineModulate = new Color("#111D2B"),
            Modulate = Brand.Ink, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true,
            Position = new Vector3(mid.x + 0.5f, 2.2f, mid.z + 0.5f),
        });
    }
}
