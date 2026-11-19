using Godot;

namespace DatacenterArchitect.Core;

/// <summary>Brand colours from brand/tokens.json. Power = amber, cooling = cyan, fiber = orange, everywhere.</summary>
public static class Brand
{
    public static readonly Color Panel = new("#111D2B", 0.88f);
    public static readonly Color PanelSolid = new("#142334");
    public static readonly Color PanelHover = new("#1D3247");
    public static readonly Color Line = new("#96BEDC", 0.18f);
    public static readonly Color Ink = new("#EAF2F8");
    public static readonly Color InkDim = new("#93A9BC");
    public static readonly Color Amber = new("#FFB23E");
    public static readonly Color Power = Amber;
    public static readonly Color Cooling = new("#55D2F2");
    public static readonly Color Fiber = new("#FF7A2F");
    public static readonly Color Ok = new("#5FD49B");
    public static readonly Color Warning = Amber;
    public static readonly Color Critical = new("#FF6B5E");

    public static readonly Color Grass = new("#6A9A4C");
    public static readonly Color PlotGrass = new("#78A855");
    public static readonly Color Asphalt = new("#4A4F57");
    public static readonly Color Concrete = new("#A9ADB1");
    public static readonly Color Wall = new("#C9D0D6");
    public static readonly Color HallFloor = new("#9AA5AE");
    public static readonly Color Rack = new("#23282F");
    public static readonly Color Sky = new("#A8D6F2");
}
