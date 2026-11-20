using Godot;

namespace DatacenterArchitect.World;

/// <summary>Colour grade for the world camera: slightly cool shadows, neutral mid-tones and warm, soft highlights,
/// the sunny "theme park sim" look. Used as the Environment's colour-correction curve (a 1D gradient per channel).</summary>
public static class Look
{
    static GradientTexture1D _grade;

    public static GradientTexture1D Grade()
    {
        if (_grade != null) return _grade;
        var g = new Gradient();
        g.SetColor(0, new Color(0.012f, 0.02f, 0.04f));
        g.SetOffset(0, 0);
        g.SetColor(1, new Color(1f, 0.975f, 0.93f));
        g.SetOffset(1, 1);
        g.AddPoint(0.25f, new Color(0.235f, 0.25f, 0.265f));
        g.AddPoint(0.5f, new Color(0.5f, 0.5f, 0.49f));
        g.AddPoint(0.78f, new Color(0.8f, 0.785f, 0.75f));
        return _grade = new GradientTexture1D { Gradient = g, Width = 256, UseHdr = false };
    }
}
