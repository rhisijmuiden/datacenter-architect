using System;
using static DatacenterArchitect.World.Models;
using DatacenterArchitect.Core;
using DatacenterArchitect.Sim;
using Godot;

namespace DatacenterArchitect.World;

/// <summary>Step 11 round 3: the company logo on facades, delivery trucks, staff clothes and rack doors.</summary>
public partial class WorldView
{
    int _logoKey = int.MinValue;

    /// <summary>When the logo or the name changes: redraw the texture, rebuild facades, racks and people.</summary>
    void SyncLogo()
    {
        var logo = _state.Logo;
        var key = HashCode.Combine(logo.Version, _state.Options.Company, _state.GreenLabel, _state.GoldLabel, Loc.Language);
        LogoArt.Instance?.Sync(logo, _state.Options.Company);
        if (key == _logoKey) return;
        var first = _logoKey == int.MinValue;
        _logoKey = key;
        People.CompanyColor = logo.OnWorkwear ? new Color(logo.Primary) : null;
        RackRenderer.LogoKey = logo.OnRacks ? key : 0;
        if (first) return;
        _layoutRev = -1; _objRev = -1;
        _racks.Clear();
        foreach (var n in _agentNodes.Values) n.QueueFree();
        _agentNodes.Clear();
    }

    /// <summary>A sign high on the front (+Z) facade: a plate in the secondary colour with the logo and the company name on it.</summary>
    void BuildingSign(Node3D root, Building b, float roofY)
    {
        if (b.W < 6) return;
        // the wall's outer face is 0.1 m out from the edge; the plate sits on it and the logo just in front of the plate
        var z = b.Z + b.D + 0.1f;
        var y = roofY - 1.15f;
        var name = b.W >= 12 ? _state.Options.Company : "";
        var textW = string.IsNullOrEmpty(name) ? 0 : _font.GetStringSize(name, HorizontalAlignment.Left, -1, 96).X * 0.0075f;
        var plateW = Math.Min(b.W - 1.2f, 2.0f + (textW > 0 ? textW + 0.5f : 0));
        var plate = new Color(_state.Logo.Secondary);
        Box(root, new Vector3(b.X + 0.6f + plateW / 2f, y, z + 0.03f), new Vector3(plateW, 1.9f, 0.06f), plate, 0.6f);
        LogoArt.Quad(root, new Vector3(b.X + 1.6f, y, z + 0.065f), 1.6f);
        GreenBadge(root, b.X + 0.6f + plateW + 0.2f, b.X + b.W - 0.4f, y, z);
        if (textW <= 0) return;
        root.AddChild(new Label3D
        {
            Text = name, Font = _font, FontSize = 96, PixelSize = 0.0075f, OutlineSize = 0,
            Modulate = plate.Luminance > 0.5f ? new Color("#141c26") : new Color("#f4f1ea"), HorizontalAlignment = HorizontalAlignment.Left,
            Position = new Vector3(b.X + 2.65f, y, z + 0.065f), Shaded = true, DoubleSided = false,
        });
    }

    /// <summary>Green datacenter label: a green (gold rim for Green Gold) badge next to the company sign.</summary>
    void GreenBadge(Node3D root, float left, float right, float y, float z)
    {
        if (!_state.GreenLabel || right - left < 2.2f) return;
        var gold = _state.GoldLabel;
        var cx = left + 1.05f;
        if (gold) Box(root, new Vector3(cx, y, z + 0.025f), new Vector3(2.1f, 1.7f, 0.05f), new Color("#FFD866"), 0.4f);
        Box(root, new Vector3(cx, y, z + 0.04f), new Vector3(1.96f, 1.56f, 0.06f), new Color("#2F8A5B"), 0.6f);
        root.AddChild(new Label3D
        {
            Text = Wrap2(Loc.T(gold ? "sign_green_gold" : "sign_green")), Font = _font, FontSize = 40, PixelSize = 0.0075f, OutlineSize = 0,
            Modulate = new Color("#f4f1ea"), HorizontalAlignment = HorizontalAlignment.Center, Position = new Vector3(cx, y, z + 0.075f), Shaded = true, DoubleSided = false,
        });
    }

    static string Wrap2(string t) { var i = t.LastIndexOf(' '); return i < 0 ? t : t[..i] + "\n" + t[(i + 1)..]; }

    /// <summary>Equipment trucks run in company colours: the logo and the name on both sides of the box trailer.</summary>
    internal Node3D Livery(Node3D truck, TruckLoad load)
    {
        if (load != TruckLoad.Equipment || !_state.Logo.OnVehicles || truck.GetNodeOrNull<Node3D>("Trailer") is not { } tr) return truck;
        foreach (var side in new[] { 1, -1 })
        {
            var x = side * 1.272f;
            LogoArt.Quad(tr, new Vector3(x, 2.7f, -1.7f), 1.9f, side * 90);
            tr.AddChild(new Label3D
            {
                Text = _state.Options.Company, Font = _font, FontSize = 96, PixelSize = 0.0085f, OutlineSize = 0,
                Modulate = new Color(_state.Logo.Primary).Lightened(0.05f), Position = new Vector3(x, 2.7f, -2.9f), RotationDegrees = new Vector3(0, side * 90, 0),
                HorizontalAlignment = side > 0 ? HorizontalAlignment.Left : HorizontalAlignment.Right, Shaded = true, DoubleSided = false,
            });
        }
        return truck;
    }
}
