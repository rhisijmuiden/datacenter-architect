using System;
using System.Collections.Generic;
using System.Linq;
using DatacenterArchitect.Sim;
using Godot;

namespace DatacenterArchitect.World;

/// <summary>
/// Timelapse of the campus (step 11). Once a game week, at noon, a second camera high above the site takes a picture of the
/// whole campus from the same angle. The pictures go to user://timelapse/{game id}/ as small JPGs; the Progress screen plays them back.
/// It renders one extra frame a week, so a huge campus stays smooth.
/// </summary>
public partial class Timelapse : Node
{
    public const string Root = "user://timelapse";
    public const int Width = 960, Height = 540;
    /// <summary>Pictures are taken every this many game days, at this hour.</summary>
    public const int EveryDays = 7, AtHour = 12;

    public Func<GameState> State;
    public Func<bool> Playing;
    public WorldView World;
    /// <summary>Called after a picture was saved (day).</summary>
    public Action<int> Captured;

    SubViewport _vp;
    Camera3D _cam;
    double _lastHours = double.NaN;
    string _lastGame;
    bool _busy;

    public override void _Ready()
    {
        _vp = new SubViewport
        {
            Size = new Vector2I(Width, Height), RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled,
            Msaa3D = Viewport.Msaa.Msaa2X, ScreenSpaceAA = Viewport.ScreenSpaceAAEnum.Disabled,
        };
        _cam = new Camera3D { Fov = 34, Near = 1, Far = 2500, Current = true };
        _vp.AddChild(_cam);
        AddChild(_vp);
    }

    public static string DirOf(string gameId) => $"{Root}/{(GameState.IsSafeId(gameId) ? gameId : "unknown")}";

    /// <summary>The saved pictures of a game, oldest first: (game day, file path).</summary>
    public static List<(int day, string path)> Frames(string gameId)
    {
        var list = new List<(int, string)>();
        if (string.IsNullOrEmpty(gameId)) return list;
        using var d = DirAccess.Open(DirOf(gameId));
        if (d == null) return list;
        foreach (var f in d.GetFiles())
            if (f.StartsWith('d') && f.EndsWith(".jpg") && int.TryParse(f[1..^4], out var day)) list.Add((day, $"{DirOf(gameId)}/{f}"));
        return list.OrderBy(x => x.Item1).ToList();
    }

    /// <summary>Whether the clock passed a picture moment between two times (in game hours).</summary>
    public static bool Due(double before, double now)
    {
        if (double.IsNaN(before) || now <= before) return false;
        // picture moments: day 0, 7, 14 ... at noon
        var period = EveryDays * 24.0;
        var k = Math.Floor((now - AtHour) / period);
        var moment = k * period + AtHour;
        return moment > before && moment <= now;
    }

    /// <summary>Where the timelapse camera looks from: high, at a fixed corner, far enough to see the whole plot.</summary>
    public static (Vector3 eye, Vector3 target) Framing(int plotSize)
    {
        var target = new Vector3(plotSize / 2f, 0, plotSize / 2f + 2);
        float yaw = 0.62f, pitch = 0.78f;
        var dist = plotSize * 1.32f + 26;
        var off = new Vector3(Mathf.Cos(pitch) * Mathf.Sin(yaw), Mathf.Sin(pitch), Mathf.Cos(pitch) * Mathf.Cos(yaw)) * dist;
        return (target + off, target);
    }

    public override void _Process(double delta)
    {
        var s = State?.Invoke();
        if (s == null || Playing?.Invoke() != true) { _lastHours = double.NaN; return; }
        if (_lastGame != s.GameId) { _lastGame = s.GameId; _lastHours = s.Hours; if (Frames(s.GameId).Count == 0) _ = Capture(s); return; }
        var before = _lastHours;
        _lastHours = s.Hours;
        if (Due(before, s.Hours)) _ = Capture(s);
    }

    /// <summary>Takes one picture now (also used for the first picture of a new game).</summary>
    public async System.Threading.Tasks.Task Capture(GameState s)
    {
        if (_busy || _vp == null) return;
        _busy = true;
        try
        {
            var (eye, target) = Framing(s.PlotSize);
            _cam.Position = eye;
            _cam.LookAt(target, Vector3.Up);
            var grid = World?.GridVisible ?? false;
            if (World != null) World.GridVisible = false;
            _vp.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            if (World != null) World.GridVisible = grid;
            var img = _vp.GetTexture().GetImage();
            if (img == null || img.IsEmpty()) return;
            var dir = DirOf(s.GameId);
            DirAccess.MakeDirRecursiveAbsolute(dir);
            var day = (int)(s.Hours / 24);
            img.SaveJpg($"{dir}/d{day:00000}.jpg", 0.82f);
            Captured?.Invoke(day);
        }
        finally { _busy = false; }
    }
}
