using System;
using DatacenterArchitect.Core;
using DatacenterArchitect.Sim;
using Godot;

namespace DatacenterArchitect.World;

/// <summary>
/// First-person walking (step 10): look and listen only, nothing to click. WASD or the arrow keys walk,
/// Shift runs, the mouse looks around, Page Up/Down rides a freight lift, Esc or F walks back out.
/// The walker uses the same walls, doors and fences as the staff, so you go through doors, not through walls.
/// </summary>
public partial class FirstPerson : Node3D
{
    public Camera3D Camera { get; private set; }
    public float Yaw, Pitch;
    public int Floor;
    public const float Eye = 1.62f, Radius = 0.22f, WalkSpeed = 2.6f, RunSpeed = 5.5f;
    public Rect2 Bounds = new(-20, -20, 104, 104);
    GameState _state;
    Nav _nav;
    float _bob, _y;
    /// <summary>Called when the walker steps through a lift to another floor or walks in or out of a building.</summary>
    public Action Moved;
    public Action Exit;

    public override void _Ready()
    {
        Camera = new Camera3D { Fov = 70, Near = 0.08f, Far = 900 };
        AddChild(Camera);
    }

    public void Begin(GameState s, Nav nav, Vector3 at, int floor, float yaw)
    {
        _state = s; _nav = nav;
        Floor = floor; Yaw = yaw; Pitch = -0.08f;
        var (x, z) = Nearest((int)Math.Floor(at.X), (int)Math.Floor(at.Z), floor);
        Position = new Vector3(x + 0.5f, 0, z + 0.5f);
        _y = Ground();
        Camera.Current = true;
        Input.MouseMode = Input.MouseModeEnum.Captured;
        Apply(0);
    }

    public void End()
    {
        Camera.Current = false;
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    /// <summary>The closest cell the walker can stand on, searching outwards.</summary>
    (int x, int z) Nearest(int x, int z, int f)
    {
        for (var r = 0; r < 40; r++)
            for (var dz = -r; dz <= r; dz++)
                for (var dx = -r; dx <= r; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != r) continue;
                    if (Stand(x + dx, z + dz, f)) return (x + dx, z + dz);
                }
        Floor = 0;
        return (x, _state.PlotSize + 2);
    }

    bool Stand(int x, int z, int f) => _state.InPlot(x, z) ? _nav.Walkable(x, z, f) : f == 0;

    public Building Inside => _state.BuildingAt((int)Math.Floor(Position.X), (int)Math.Floor(Position.Z)) is { Stage: Stage.Done } b && b.Floors > Floor ? b : null;

    float Ground() => Inside != null || Floor > 0 ? WorldView.Base(Floor) : 0;

    public override void _UnhandledInput(InputEvent e)
    {
        if (_state == null || !Camera.Current) return;
        if (e is InputEventMouseMotion mm && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            var sens = AppSettings.Instance.MouseSensitivity / 100f * 0.0025f;
            Yaw -= mm.Relative.X * sens;
            Pitch = Mathf.Clamp(Pitch - mm.Relative.Y * sens * (AppSettings.Instance.InvertMouse ? -1 : 1), -1.35f, 1.35f);
            GetViewport().SetInputAsHandled();
        }
        else if (e is InputEventMouseButton { Pressed: true } && Input.MouseMode != Input.MouseModeEnum.Captured)
        {
            Input.MouseMode = Input.MouseModeEnum.Captured;
            GetViewport().SetInputAsHandled();
        }
        else if (e is InputEventKey { Pressed: true, Echo: false } k)
        {
            if (k.Keycode is Key.Escape or Key.F) { Exit?.Invoke(); GetViewport().SetInputAsHandled(); }
            else if (k.Keycode is Key.Pageup or Key.Pagedown) { Lift(k.Keycode == Key.Pageup ? 1 : -1); GetViewport().SetInputAsHandled(); }
        }
    }

    /// <summary>Take a lift or the stairs one floor up or down when standing in or next to one.</summary>
    public bool Lift(int dir)
    {
        int x = (int)Math.Floor(Position.X), z = (int)Math.Floor(Position.Z);
        var o = _state.ObjectAt(x, z, Floor) ?? _state.ObjectAt(x, z);
        var near = o != null && Catalog.IsShaft(o.Kind);
        // the lift itself blocks walking, so also accept standing right next to one
        if (!near)
            foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                if (_state.ObjectAt(x + dx, z + dz) is { } n && Catalog.IsShaft(n.Kind)) { near = true; break; }
        var b = _state.BuildingAt(x, z);
        var to = Floor + dir;
        if (!near || b == null || to < 0 || to >= b.Floors || !_nav.Walkable(x, z, to)) return false;
        Floor = to;
        Moved?.Invoke();
        return true;
    }

    public override void _Process(double delta)
    {
        if (_state == null || !Camera.Current) return;
        var dt = (float)Math.Min(delta, 0.1);
        var k = new Vector2(
            (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right) ? 1 : 0) - (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left) ? 1 : 0),
            (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down) ? 1 : 0) - (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up) ? 1 : 0));
        var moving = k != Vector2.Zero;
        if (moving)
        {
            k = k.Normalized() * (Input.IsKeyPressed(Key.Shift) ? RunSpeed : WalkSpeed) * dt;
            var right = new Vector3(Mathf.Cos(Yaw), 0, -Mathf.Sin(Yaw));
            var fwd = new Vector3(-Mathf.Sin(Yaw), 0, -Mathf.Cos(Yaw));
            var step = right * k.X - fwd * k.Y;
            var wasInside = Inside;
            Step(step.X, 0); Step(0, step.Z);
            if (Inside != wasInside) Moved?.Invoke();
            _bob += dt * (Input.IsKeyPressed(Key.Shift) ? 14 : 9);
        }
        Apply(dt);
    }

    /// <summary>Slide along one axis, stopping a body radius short of walls and blocked cells.</summary>
    public void Step(float dx, float dz)
    {
        var p = Position;
        var np = p + new Vector3(dx, 0, dz);
        np.X = Mathf.Clamp(np.X, Bounds.Position.X, Bounds.End.X);
        np.Z = Mathf.Clamp(np.Z, Bounds.Position.Y, Bounds.End.Y);
        int cx = (int)Math.Floor(p.X), cz = (int)Math.Floor(p.Z);
        // check the cells the body's edge would touch
        foreach (var ox in new[] { -Radius, Radius })
            foreach (var oz in new[] { -Radius, Radius })
            {
                int tx = (int)Math.Floor(np.X + ox), tz = (int)Math.Floor(np.Z + oz);
                if (tx == cx && tz == cz) continue;
                if (!_nav.CanStep(cx, cz, tx, tz, Floor)) return;
            }
        Position = np;
    }

    void Apply(float dt)
    {
        _y = dt <= 0 ? Ground() : Mathf.Lerp(_y, Ground(), Mathf.Min(1, dt * 10));
        var bob = AppSettings.Instance.MovingParts ? Mathf.Sin(_bob) * 0.035f : 0;
        Camera.Position = new Vector3(0, _y + Eye + bob, 0);
        Camera.Rotation = new Vector3(Pitch, Yaw, 0);
    }

    /// <summary>The point the walker stands on, for sound and the weather.</summary>
    public Vector3 Feet => new(Position.X, _y, Position.Z);
}
