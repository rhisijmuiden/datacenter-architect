using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DatacenterArchitect.Core;

/// <summary>
/// Applies the display part of <see cref="AppSettings"/>: window mode, resolution, monitor,
/// V-Sync, frame cap and interface size. Works the same on 4:3, 16:9, 16:10, ultrawide and 4K screens.
/// </summary>
public static class DisplaySettings
{
    /// <summary>Common resolutions per aspect ratio; only the ones that fit the monitor are offered.</summary>
    static readonly Vector2I[] Common =
    {
        new(1024, 768), new(1280, 960), new(1400, 1050), new(1600, 1200), new(2048, 1536),       // 4:3
        new(1280, 1024),                                                                          // 5:4
        new(1280, 720), new(1366, 768), new(1600, 900), new(1920, 1080), new(2560, 1440), new(3200, 1800), new(3840, 2160), // 16:9
        new(1280, 800), new(1440, 900), new(1680, 1050), new(1920, 1200), new(2560, 1600), new(3840, 2400),                 // 16:10
        new(2560, 1080), new(3440, 1440), new(3840, 1600), new(5120, 2160),                       // 21:9 ultrawide
        new(3840, 1080), new(5120, 1440),                                                         // 32:9 super ultrawide
    };

    /// <summary>Short aspect name such as 16:9, 21:9 or 4:3.</summary>
    public static string AspectName(Vector2I r)
    {
        if (r.X <= 0 || r.Y <= 0) return "";
        var a = (double)r.X / r.Y;
        (double ratio, string name)[] known = { (4 / 3.0, "4:3"), (5 / 4.0, "5:4"), (16 / 10.0, "16:10"), (16 / 9.0, "16:9"), (2.37, "21:9"), (32 / 9.0, "32:9") };
        var best = known.OrderBy(k => Math.Abs(k.ratio - a)).First();
        if (Math.Abs(best.ratio - a) < 0.06) return best.name;
        int Gcd(int x, int y) => y == 0 ? x : Gcd(y, x % y);
        var g = Gcd(r.X, r.Y);
        return $"{r.X / g}:{r.Y / g}";
    }

    /// <summary>Resolutions that fit on a monitor of the given size, largest first; the monitor's own size is always included.</summary>
    public static List<Vector2I> ResolutionsFor(Vector2I screen)
    {
        var list = Common.Where(r => r.X <= screen.X && r.Y <= screen.Y).ToList();
        if (screen.X > 0 && screen.Y > 0 && !list.Contains(screen)) list.Add(screen);
        return list.Distinct().OrderByDescending(r => r.X * r.Y).ThenByDescending(r => r.X).ToList();
    }

    /// <summary>
    /// In fullscreen the monitor keeps its own resolution (no flicker, sharp text) and the 3D world is
    /// rendered at the chosen resolution instead; this is the matching 3D render scale.
    /// </summary>
    public static float RenderScaleFor(Vector2I chosen, Vector2I screen)
    {
        if (chosen.X <= 0 || chosen.Y <= 0 || screen.X <= 0 || screen.Y <= 0) return 1f;
        return Mathf.Clamp(Math.Min((float)chosen.X / screen.X, (float)chosen.Y / screen.Y), 0.25f, 1f);
    }

    public static bool Headless => DisplayServer.GetName() == "headless";

    public static int CurrentScreen(AppSettings s) =>
        s.Monitor >= 0 && s.Monitor < DisplayServer.GetScreenCount() ? s.Monitor : DisplayServer.WindowGetCurrentScreen();

    public static Vector2I ScreenSize(int screen) => DisplayServer.ScreenGetSize(screen);

    public static void Apply(AppSettings s, Window root)
    {
        Engine.MaxFps = s.MaxFps;
        root.ContentScaleFactor = Mathf.Clamp(s.UiScale, 0.75f, 1.5f);
        if (Headless) return;
        DisplayServer.WindowSetVsyncMode(s.VSync ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled);

        var screen = CurrentScreen(s);
        var screenSize = ScreenSize(screen);
        if (DisplayServer.WindowGetCurrentScreen() != screen)
        {
            // leave fullscreen first, otherwise the window cannot move to another monitor
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
            DisplayServer.WindowSetCurrentScreen(screen);
        }

        var vp = root;
        switch (s.WindowMode)
        {
            case WindowModeSetting.Windowed:
            {
                if (DisplayServer.WindowGetMode() != DisplayServer.WindowMode.Windowed)
                    DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
                DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, false);
                var usable = DisplayServer.ScreenGetUsableRect(screen);
                var size = s.Resolution.X > 0 ? s.Resolution : new Vector2I(usable.Size.X * 3 / 4, usable.Size.Y * 3 / 4);
                size = new Vector2I(Math.Min(size.X, usable.Size.X), Math.Min(size.Y, usable.Size.Y));
                DisplayServer.WindowSetSize(size);
                DisplayServer.WindowSetPosition(usable.Position + (usable.Size - size) / 2);
                vp.Scaling3DScale = 1f;
                break;
            }
            case WindowModeSetting.Borderless:
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
                vp.Scaling3DScale = RenderScaleFor(s.Resolution, screenSize);
                break;
            case WindowModeSetting.Fullscreen:
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.ExclusiveFullscreen);
                vp.Scaling3DScale = RenderScaleFor(s.Resolution, screenSize);
                break;
        }
        // FSR keeps a lowered 3D resolution crisp; at full size plain bilinear is free
        vp.Scaling3DMode = vp.Scaling3DScale < 0.99f ? Viewport.Scaling3DModeEnum.Fsr : Viewport.Scaling3DModeEnum.Bilinear;
    }

    /// <summary>Switch between windowed and borderless fullscreen (Alt+Enter / F11).</summary>
    public static void ToggleFullscreen(AppSettings s, Window root)
    {
        s.WindowMode = s.WindowMode == WindowModeSetting.Windowed ? WindowModeSetting.Borderless : WindowModeSetting.Windowed;
        s.Save();
        Apply(s, root);
    }
}
