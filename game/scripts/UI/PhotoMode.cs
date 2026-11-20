using System;
using System.Linq;
using DatacenterArchitect.Core;
using DatacenterArchitect.Sim;
using DatacenterArchitect.World;
using Godot;

namespace DatacenterArchitect.UI;

/// <summary>
/// Photo mode (step 11): the interface goes away, time stands still and a small bar at the bottom offers the time of day,
/// a colour filter, depth of field, a cinematic camera that slowly glides around the campus, and a button to save a photo.
/// Keys: F12 or Enter takes a photo, H hides the bar, C toggles the cinematic camera, Esc leaves.
/// </summary>
public partial class PhotoMode : Control
{
    public static readonly string[] Filters = { "ph_filter_none", "ph_filter_warm", "ph_filter_cool", "ph_filter_vivid", "ph_filter_soft", "ph_filter_bw", "ph_filter_vintage" };

    readonly CameraRig _cam;
    readonly Func<GameState> _state;
    readonly Godot.Environment _env;
    public Action Exit;
    /// <summary>A photo was saved: its full path.</summary>
    public Action<string> Saved;

    readonly PanelContainer _bar;
    readonly Label _clock, _status;
    readonly CheckButton _cine;
    readonly double _origHours;
    readonly (bool on, float sat, float con, float bri, Texture _cc) _origAdjust;
    double _t;
    bool _shooting;
    float _cineYaw, _cinePitch, _cineDist;
    Vector3 _cineTarget;

    public bool Cinematic => _cine.ButtonPressed;

    public PhotoMode(CameraRig cam, Func<GameState> state, Godot.Environment env)
    {
        _cam = cam; _state = state; _env = env;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        var s = state();
        _origHours = s?.Hours ?? 0;
        if (env != null) _origAdjust = (env.AdjustmentEnabled, env.AdjustmentSaturation, env.AdjustmentContrast, env.AdjustmentBrightness, env.AdjustmentColorCorrection);

        _bar = new PanelContainer { MouseFilter = MouseFilterEnum.Stop };
        _bar.AddThemeStyleboxOverride("panel", Ui.Box(Brand.Panel, 12, Brand.Line, 1, 10, true));
        _bar.AnchorLeft = _bar.AnchorRight = 0.5f; _bar.AnchorTop = _bar.AnchorBottom = 1;
        _bar.OffsetTop = _bar.OffsetBottom = -18;
        _bar.GrowHorizontal = GrowDirection.Both; _bar.GrowVertical = GrowDirection.Begin;
        AddChild(_bar);
        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 6);
        _bar.AddChild(v);
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 12);
        v.AddChild(row);
        var title = Ui.Text("ph_title", 18, Brand.Amber, Ui.DisplayBold);
        title.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(title);
        row.AddChild(new VSeparator());

        // time of day: the same day, another hour
        row.AddChild(Ui.Text("ph_time", 14, Brand.InkDim));
        var day = Math.Floor(_origHours / 24) * 24;
        var hour = new HSlider { MinValue = 0, MaxValue = 23.75, Step = 0.25, Value = _origHours - day, CustomMinimumSize = new Vector2(170, 0), SizeFlagsVertical = SizeFlags.ShrinkCenter, TooltipText = "ph_time_help" };
        _clock = Ui.Text("", 15, Brand.Ink); _clock.AutoTranslateMode = AutoTranslateModeEnum.Disabled; _clock.CustomMinimumSize = new Vector2(48, 0);
        hour.ValueChanged += h => { var st = _state(); if (st != null) st.Hours = day + h; UpdateClock(); };
        row.AddChild(hour); row.AddChild(_clock);

        row.AddChild(Ui.Text("ph_filter", 14, Brand.InkDim));
        var filter = new OptionButton();
        foreach (var f in Filters) filter.AddItem(Loc.T(f));
        filter.ItemSelected += i => ApplyFilter((int)i);
        row.AddChild(filter);

        var dof = new CheckButton { Text = "ph_dof", TooltipText = "ph_dof_help" };
        dof.Toggled += on => ApplyDof(on);
        row.AddChild(dof);
        _cine = new CheckButton { Text = "ph_cinematic", TooltipText = "ph_cinematic_help" };
        _cine.Toggled += on => StartCinematic(on);
        row.AddChild(_cine);
        row.AddChild(new VSeparator());
        var shot = Ui.Primary("ph_take", () => _ = TakePhoto());
        shot.TooltipText = "ph_take_help";
        row.AddChild(shot);
        row.AddChild(Ui.Secondary("ph_folder", () => OS.ShellOpen(PhotoDir())));
        row.AddChild(Ui.Secondary("ph_exit", () => Exit?.Invoke()));
        var hint = Ui.Text("ph_hint", 12, Brand.InkDim);
        hint.HorizontalAlignment = HorizontalAlignment.Center;
        v.AddChild(hint);
        foreach (var c in row.GetChildren().OfType<Control>())
        {
            c.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            if (c is Label l) l.AutowrapMode = TextServer.AutowrapMode.Off;
        }
        _status = Ui.Text("", 13, Brand.Ok); _status.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        _status.HorizontalAlignment = HorizontalAlignment.Center; _status.Visible = false;
        v.AddChild(_status);
        UpdateClock();
    }

    void UpdateClock()
    {
        var s = _state();
        if (s != null) _clock.Text = s.Date.ToString("HH:mm");
    }

    /// <summary>Folder for photos: the Pictures folder of Windows, or the game's own folder when there is none.</summary>
    public static string PhotoDir()
    {
        var pics = OS.GetSystemDir(OS.SystemDir.Pictures);
        var dir = string.IsNullOrEmpty(pics) ? ProjectSettings.GlobalizePath("user://photos") : pics.TrimEnd('/', '\\') + "/Datacenter Architect";
        DirAccess.MakeDirRecursiveAbsolute(dir);
        return dir;
    }

    /// <summary>Saturation, contrast and brightness of a filter, and an optional tint curve.</summary>
    public static (float sat, float con, float bri, Color? mid) FilterValues(int i) => i switch
    {
        1 => (1.2f, 1.1f, 1.03f, new Color(0.56f, 0.5f, 0.42f)),
        2 => (1.05f, 1.12f, 1.0f, new Color(0.44f, 0.5f, 0.58f)),
        3 => (1.5f, 1.22f, 1.02f, null),
        4 => (0.95f, 0.92f, 1.06f, null),
        5 => (0f, 1.2f, 1.02f, null),
        6 => (0.7f, 0.95f, 1.04f, new Color(0.58f, 0.5f, 0.38f)),
        _ => (1.15f, 1.12f, 1f, null),
    };

    void ApplyFilter(int i)
    {
        if (_env == null) return;
        var (sat, con, bri, mid) = FilterValues(i);
        _env.AdjustmentEnabled = true;
        _env.AdjustmentSaturation = sat; _env.AdjustmentContrast = con; _env.AdjustmentBrightness = bri;
        if (mid is Color m)
        {
            var g = new Gradient();
            g.SetColor(0, Colors.Black); g.SetColor(1, new Color(1, 1, 1).Lerp(m * 1.8f, 0.12f));
            g.AddPoint(0.5f, m);
            _env.AdjustmentColorCorrection = new GradientTexture1D { Gradient = g, Width = 256 };
        }
        else _env.AdjustmentColorCorrection = _origAdjust._cc;
    }

    void ApplyDof(bool on)
    {
        if (_cam?.Camera == null) return;
        if (!on) { _cam.Camera.Attributes = null; return; }
        var d = _cam.Distance;
        _cam.Camera.Attributes = new CameraAttributesPractical
        {
            DofBlurFarEnabled = true, DofBlurFarDistance = d * 1.15f, DofBlurFarTransition = d * 0.9f,
            DofBlurNearEnabled = true, DofBlurNearDistance = d * 0.55f, DofBlurNearTransition = d * 0.4f,
            DofBlurAmount = 0.08f,
        };
    }

    void StartCinematic(bool on)
    {
        _cam.InputEnabled = !on;
        if (!on) return;
        var s = _state();
        _cineYaw = _cam.Yaw; _cinePitch = _cam.Pitch; _cineDist = _cam.Distance;
        var n = s?.PlotSize ?? 64;
        _cineTarget = new Vector3(n / 2f, 0, n / 2f);
        _t = 0;
    }

    public override void _Process(double delta)
    {
        if (!Cinematic) return;
        // a slow glide: orbit around the campus while height and distance breathe
        _t += delta;
        var n = _state()?.PlotSize ?? 64;
        var yaw = _cineYaw + (float)_t * 0.045f;
        var pitch = Mathf.Clamp(_cinePitch * 0.4f + 0.42f + 0.12f * Mathf.Sin((float)_t * 0.07f), 0.38f, 1.2f);
        var dist = Mathf.Clamp(n * 0.9f + 18 + n * 0.25f * Mathf.Sin((float)_t * 0.05f + 1), 25, 260);
        var target = _cam.TargetPoint.Lerp(_cineTarget, (float)Math.Min(1, delta * 0.3));
        _cam.SetView(yaw, pitch, dist, target);
        if (_cam.Camera.Attributes is CameraAttributesPractical a) { a.DofBlurFarDistance = dist * 1.15f; a.DofBlurNearDistance = dist * 0.55f; }
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is not InputEventKey { Pressed: true, Echo: false } k) return;
        if (k.Keycode == Key.Escape) Exit?.Invoke();
        else if (k.Keycode is Key.F12 or Key.Enter or Key.KpEnter) _ = TakePhoto();
        else if (k.Keycode == Key.H) _bar.Visible = !_bar.Visible;
        else if (k.Keycode == Key.C) _cine.ButtonPressed = !_cine.ButtonPressed;
        else return;
        GetViewport().SetInputAsHandled();
    }

    async System.Threading.Tasks.Task TakePhoto()
    {
        if (_shooting) return;
        _shooting = true;
        var bar = _bar.Visible;
        _bar.Visible = false;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var img = GetViewport().GetTexture().GetImage();
        _bar.Visible = bar;
        _shooting = false;
        if (img == null || img.IsEmpty()) return;
        var path = $"{PhotoDir()}/{SaveSystem.Sanitize(_state()?.Options.Company ?? "Datacenter")} {DateTime.Now:yyyy-MM-dd HH-mm-ss}.png";
        if (img.SavePng(path) != Error.Ok) return;
        _status.Text = Loc.T("ph_saved", path); _status.Visible = true;
        UiSounds.Instance?.Chime();
        Saved?.Invoke(path);
    }

    /// <summary>Puts back the clock, the colours and the camera as they were.</summary>
    public void Restore()
    {
        var s = _state();
        if (s != null) s.Hours = _origHours;
        if (_env != null)
        {
            _env.AdjustmentEnabled = _origAdjust.on; _env.AdjustmentSaturation = _origAdjust.sat; _env.AdjustmentContrast = _origAdjust.con;
            _env.AdjustmentBrightness = _origAdjust.bri; _env.AdjustmentColorCorrection = _origAdjust._cc;
        }
        if (_cam?.Camera != null) _cam.Camera.Attributes = null;
        if (_cam != null) _cam.InputEnabled = true;
    }
}
