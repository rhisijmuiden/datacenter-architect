using Godot;

namespace DatacenterArchitect.World;

/// <summary>
/// Tilted top-down orbit camera. Right-drag rotates, middle-drag or WASD pans,
/// wheel zooms, Q/E rotate by 90 degrees. Values ease towards their targets.
/// </summary>
public partial class CameraRig : Node3D
{
    public Camera3D Camera { get; private set; }
    public float Yaw = 0.6f, Pitch = 0.9f, Distance = 60f;
    float _yawT, _pitchT, _distT;
    Vector3 _targetT;
    bool _rotating, _panning;
    public Rect2 Bounds = new(-20, -20, 104, 104);
    public bool InputEnabled = true;
    /// <summary>Ease towards the target (Options: smooth camera); off = follow instantly.</summary>
    public bool Smooth = true;

    public override void _Ready()
    {
        Camera = new Camera3D { Fov = 38, Near = 0.3f, Far = 1500 };
        AddChild(Camera);
        _yawT = Yaw; _pitchT = Pitch; _distT = Distance; _targetT = Position;
        Apply();
    }

    /// <summary>Closest zoom: near enough to see the servers and their LEDs in a rack.</summary>
    public const float MinDistance = 6f;

    public void SetView(float yaw, float pitch, float dist, Vector3 target)
    {
        Yaw = _yawT = yaw; Pitch = _pitchT = pitch; Distance = _distT = dist; Position = _targetT = target; Apply();
    }

    public Vector3 TargetPoint => _targetT;

    /// <summary>Glide to a point and zoom in to at most this distance (warnings that show a spot).</summary>
    public void FlyTo(Vector3 target, float dist)
    {
        _targetT = target;
        _distT = Mathf.Clamp(Mathf.Min(_distT, dist), MinDistance, 160);
        if (!Smooth) { Position = _targetT; Distance = _distT; Apply(); }
    }

    public void RotateStep(int dir) => _yawT += dir * Mathf.Pi / 2;

    public override void _UnhandledInput(InputEvent e)
    {
        if (!InputEnabled) return;
        if (e is InputEventMouseButton mb)
        {
            if (mb.ButtonIndex == MouseButton.Right) _rotating = mb.Pressed;
            if (mb.ButtonIndex == MouseButton.Middle) _panning = mb.Pressed;
            if (mb.Pressed && mb.ButtonIndex == MouseButton.WheelUp) _distT = Mathf.Clamp(_distT * 0.9f, MinDistance, 160);
            if (mb.Pressed && mb.ButtonIndex == MouseButton.WheelDown) _distT = Mathf.Clamp(_distT * 1.1f, MinDistance, 160);
        }
        else if (e is InputEventMouseMotion mm)
        {
            if (_rotating)
            {
                _yawT -= mm.Relative.X * 0.006f;
                _pitchT = Mathf.Clamp(_pitchT + mm.Relative.Y * 0.004f, 0.35f, 1.45f);
            }
            else if (_panning) Pan(-mm.Relative.X, -mm.Relative.Y, Distance * 0.0016f);
        }
        else if (e.IsActionPressed("cam_rotate_left")) RotateStep(1);
        else if (e.IsActionPressed("cam_rotate_right")) RotateStep(-1);
    }

    void Pan(float dx, float dy, float scale)
    {
        var right = new Vector3(Mathf.Cos(Yaw), 0, -Mathf.Sin(Yaw));
        var fwd = new Vector3(-Mathf.Sin(Yaw), 0, -Mathf.Cos(Yaw));
        _targetT += (right * dx - fwd * dy) * scale;
        _targetT.X = Mathf.Clamp(_targetT.X, Bounds.Position.X, Bounds.End.X);
        _targetT.Z = Mathf.Clamp(_targetT.Z, Bounds.Position.Y, Bounds.End.Y);
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        if (InputEnabled && !(GetViewport().GuiGetFocusOwner() is LineEdit))
        {
            var k = new Vector2(
                (Input.IsKeyPressed(Key.D) ? 1 : 0) - (Input.IsKeyPressed(Key.A) ? 1 : 0),
                (Input.IsKeyPressed(Key.S) ? 1 : 0) - (Input.IsKeyPressed(Key.W) ? 1 : 0));
            if (k != Vector2.Zero) Pan(k.X, k.Y, Distance * dt * 0.9f);
        }
        float E(float rate) => Smooth ? Mathf.Min(1, dt * rate) : 1;
        Yaw = Mathf.Lerp(Yaw, _yawT, E(7));
        Pitch = Mathf.Lerp(Pitch, _pitchT, E(7));
        Distance = Mathf.Lerp(Distance, _distT, E(6));
        Position = Position.Lerp(_targetT, E(7));
        Apply();
    }

    void Apply()
    {
        if (Camera == null) return;
        var off = new Vector3(Mathf.Cos(Pitch) * Mathf.Sin(Yaw), Mathf.Sin(Pitch), Mathf.Cos(Pitch) * Mathf.Cos(Yaw)) * Distance;
        Camera.Position = off;
        Camera.LookAt(GlobalPosition, Vector3.Up);
    }

    /// <summary>Point on the horizontal plane at height y under a screen position, or null.</summary>
    public Vector3? GroundAt(Vector2 screen, float y = 0)
    {
        var from = Camera.ProjectRayOrigin(screen);
        var dir = Camera.ProjectRayNormal(screen);
        if (Mathf.Abs(dir.Y) < 1e-4f) return null;
        var t = (y - from.Y) / dir.Y;
        return t > 0 ? from + dir * t : null;
    }
}
