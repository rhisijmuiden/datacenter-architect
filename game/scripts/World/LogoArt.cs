using System;
using System.Collections.Generic;
using DatacenterArchitect.Sim;
using DatacenterArchitect.UI;
using Godot;

namespace DatacenterArchitect.World;

/// <summary>Draws a company logo (shape, border, initials) in a control: used for the editor preview and for the texture.</summary>
public partial class LogoView : Control
{
    CompanyLogo _logo;
    string _letters = "";

    public void Set(CompanyLogo logo, string letters) { _logo = logo; _letters = letters ?? ""; QueueRedraw(); }

    public override void _Draw()
    {
        if (_logo == null) return;
        var s = Mathf.Min(Size.X, Size.Y);
        var o = (Size - new Vector2(s, s)) / 2;
        var c = o + new Vector2(s, s) / 2;
        var p = new Color(_logo.Primary); var q = new Color(_logo.Secondary);
        var border = s * 0.055f;
        switch (_logo.Shape)
        {
            case 0:
                DrawCircle(c, s * 0.48f, q); DrawCircle(c, s * 0.48f - border, p); break;
            case 1:
            {
                var outer = new StyleBoxFlat { BgColor = q }; outer.SetCornerRadiusAll((int)(s * 0.2f)); outer.AntiAliasing = true;
                var inner = new StyleBoxFlat { BgColor = p }; inner.SetCornerRadiusAll((int)(s * 0.2f - border)); inner.AntiAliasing = true;
                DrawStyleBox(outer, new Rect2(o + new Vector2(s * 0.03f, s * 0.03f), new Vector2(s * 0.94f, s * 0.94f)));
                DrawStyleBox(inner, new Rect2(o + new Vector2(s * 0.03f + border, s * 0.03f + border), new Vector2(s * 0.94f - 2 * border, s * 0.94f - 2 * border)));
                break;
            }
            case 2: Poly(Regular(c, s * 0.49f, 6, Mathf.Pi / 6), Regular(c, s * 0.49f - border * 1.15f, 6, Mathf.Pi / 6), p, q); break;
            case 3: Poly(Shield(c, s * 0.47f), Shield(c, s * 0.47f - border), p, q); break;
            case 4: Poly(Regular(c, s * 0.5f, 4, 0), Regular(c, s * 0.5f - border * 1.4f, 4, 0), p, q); break;
            default:
                DrawCircle(c, s * 0.48f, p); DrawCircle(c, s * 0.36f, q); DrawCircle(c, s * 0.36f - border, p); break;
        }
        var font = Ui.DisplayBold ?? ThemeDB.FallbackFont;
        var size = (int)(s * (_letters.Length >= 3 ? 0.3f : 0.4f));
        var w = font.GetStringSize(_letters, HorizontalAlignment.Left, -1, size);
        var asc = font.GetAscent(size) - font.GetDescent(size);
        DrawString(font, new Vector2(c.X - w.X / 2, c.Y + asc * 0.42f), _letters, HorizontalAlignment.Left, -1, size, q);
    }

    void Poly(Vector2[] outer, Vector2[] inner, Color p, Color q) { DrawColoredPolygon(outer, q); DrawColoredPolygon(inner, p); }

    static Vector2[] Regular(Vector2 c, float r, int n, float rot)
    {
        var pts = new Vector2[n];
        for (var i = 0; i < n; i++) { var a = rot + i * Mathf.Tau / n - Mathf.Pi / 2; pts[i] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r; }
        return pts;
    }

    static Vector2[] Shield(Vector2 c, float r)
    {
        var pts = new List<Vector2> { c + new Vector2(-r * 0.85f, -r * 0.9f), c + new Vector2(r * 0.85f, -r * 0.9f), c + new Vector2(r * 0.85f, r * 0.05f) };
        for (var i = 1; i < 8; i++) { var t = i / 8f; pts.Add(c + new Vector2(r * 0.85f * (1 - t), r * (0.05f + 0.95f * Mathf.Sin(t * Mathf.Pi / 2)))); }
        pts.Add(c + new Vector2(0, r));
        for (var i = 7; i >= 1; i--) { var t = i / 8f; pts.Add(c + new Vector2(-r * 0.85f * (1 - t), r * (0.05f + 0.95f * Mathf.Sin(t * Mathf.Pi / 2)))); }
        pts.Add(c + new Vector2(-r * 0.85f, r * 0.05f));
        return pts.ToArray();
    }
}

/// <summary>
/// Step 11 round 3: the logo as a texture for the 3D world. One small SubViewport draws it once whenever it changes;
/// facades, trucks and rack doors share <see cref="Material"/>.
/// </summary>
public partial class LogoArt : Node
{
    public static LogoArt Instance { get; private set; }
    public const int Size = 256;

    SubViewport _vp;
    LogoView _view;
    int _version = int.MinValue;
    string _letters;

    /// <summary>Lit, alpha-scissored logo material (the area outside the shape is cut away).</summary>
    public StandardMaterial3D Material { get; private set; }
    public Texture2D Texture => _vp?.GetTexture();

    public override void _Ready()
    {
        Instance = this;
        _vp = new SubViewport { Size = new Vector2I(Size, Size), TransparentBg = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Once, Disable3D = true };
        _view = new LogoView { Size = new Vector2(Size, Size) };
        _vp.AddChild(_view);
        AddChild(_vp);
        Material = new StandardMaterial3D
        {
            AlbedoTexture = _vp.GetTexture(), Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor, AlphaScissorThreshold = 0.5f,
            Roughness = 0.55f, TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic, CullMode = BaseMaterial3D.CullModeEnum.Back,
        };
    }

    public override void _ExitTree() { if (Instance == this) Instance = null; }

    /// <summary>Redraws the texture when the logo or the company name changed; returns true when it did.</summary>
    public bool Sync(CompanyLogo logo, string company)
    {
        if (logo == null || _view == null) return false;
        var letters = logo.Letters(company);
        if (logo.Version == _version && letters == _letters) return false;
        _version = logo.Version; _letters = letters;
        _view.Set(logo, letters);
        _vp.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
        return true;
    }

    /// <summary>A flat logo of <paramref name="size"/> metres facing +Z of its parent.</summary>
    public static MeshInstance3D Quad(Node3D parent, Vector3 pos, float size, float yawDeg = 0)
    {
        if (Instance?.Material == null) return null;
        var m = new MeshInstance3D
        {
            Mesh = new QuadMesh { Size = new Vector2(size, size) }, MaterialOverride = Instance.Material, Position = pos,
            RotationDegrees = new Vector3(0, yawDeg, 0), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Name = "Logo",
        };
        parent.AddChild(m);
        return m;
    }
}
