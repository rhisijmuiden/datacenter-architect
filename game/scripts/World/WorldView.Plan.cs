using System.Linq;
using DatacenterArchitect.Core;
using DatacenterArchitect.Sim;
using Godot;
using static DatacenterArchitect.World.Models;

namespace DatacenterArchitect.World;

/// <summary>
/// Step 11 round 2: plan mode blueprints. Planned buildings are a light cyan glass volume with their floor plates and
/// outline; planned rooms are a cyan plate with an outline on their floor; planned objects are tinted cyan (see RebuildObjects).
/// </summary>
public partial class WorldView
{
    public static readonly Color PlanColor = new("#4fd6ff");

    void PlanBuilding(Node3D root, Building b)
    {
        var h = b.Floors * FloorH;
        var glass = new Color(PlanColor, 0.10f);
        Box(root, new Vector3(b.X + b.W / 2f, Slab + h / 2f, b.Z + b.D / 2f), new Vector3(b.W, h, b.D), glass, 1f).CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        Box(root, new Vector3(b.X + b.W / 2f, 0.03f, b.Z + b.D / 2f), new Vector3(b.W, 0.03f, b.D), new Color(PlanColor, 0.22f), 1f).CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        for (var f = 0; f <= b.Floors; f++) Outline(root, b.X, b.Z, b.W, b.D, f == 0 ? 0.06f : Base(f), 0.08f, new Color(PlanColor, 0.9f));
        foreach (var (x, z) in new[] { (b.X, b.Z), (b.X + b.W, b.Z), (b.X, b.Z + b.D), (b.X + b.W, b.Z + b.D) })
            Box(root, new Vector3(x, Slab + h / 2f, z), new Vector3(0.08f, h, 0.08f), new Color(PlanColor, 0.9f), 1f).CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        PlanLabel(root, Loc.T(Catalog.Buildings[b.Kind].NameKey), new Vector3(b.X + b.W / 2f, Slab + h + 1.2f, b.Z + b.D / 2f));
    }

    void PlanRoom(Node3D root, Room r)
    {
        var y = r.Floor == 0 ? 0.08f : Base(r.Floor) + 0.04f;
        Box(root, new Vector3(r.X + r.W / 2f, y, r.Z + r.D / 2f), new Vector3(r.W - 0.1f, 0.02f, r.D - 0.1f), new Color(PlanColor, 0.2f), 1f).CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        Outline(root, r.X + 0.1f, r.Z + 0.1f, r.W - 0.2f, r.D - 0.2f, y + 0.02f, 0.07f, new Color(PlanColor, 0.85f));
        PlanLabel(root, Loc.T(Catalog.Rooms[r.Type].NameKey), new Vector3(r.X + r.W / 2f, y + 0.9f, r.Z + r.D / 2f), 40);
    }

    static void Outline(Node3D root, float x, float z, float w, float d, float y, float t, Color c)
    {
        foreach (var (p, s) in new[]
        {
            (new Vector3(x + w / 2f, y, z), new Vector3(w, t, t)), (new Vector3(x + w / 2f, y, z + d), new Vector3(w, t, t)),
            (new Vector3(x, y, z + d / 2f), new Vector3(t, t, d)), (new Vector3(x + w, y, z + d / 2f), new Vector3(t, t, d)),
        })
            Box(root, p, s, c, 1f).CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
    }

    void PlanLabel(Node3D root, string text, Vector3 pos, int size = 52)
    {
        root.AddChild(new Label3D
        {
            Text = text, Font = _font, FontSize = size, PixelSize = 0.02f, OutlineSize = 10, OutlineModulate = new Color("#111D2B"),
            Modulate = PlanColor, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true, Position = pos,
        });
    }
}
