using System;
using System.Linq;
using DatacenterArchitect.Core;
using DatacenterArchitect.Sim;
using DatacenterArchitect.UI;
using DatacenterArchitect.World;
using Godot;

namespace DatacenterArchitect;

/// <summary>Entry point: sets up the world, camera and UI, and switches between the title screen and a running game.</summary>
public partial class Main : Node3D
{
    WorldView _world;
    CameraRig _cam;
    Control _ui;
    Hud _hud;
    MainMenu _menu;
    GameState _state;
    bool _playing;
    int _speed = 1;
    double _autosaveTimer;
    Tool _tool = Tool.Select;
    BuildKind _kind;
    int _rot;
    Vector2I? _dragStart;
    Vector2I _hoverCell;
    static readonly float[] HoursPerSecond = { 0, 1, 4, 12 };

    public override void _Ready()
    {
        Loc.Load();
        TranslationServer.SetLocale(AppSettings.Instance.Language);
        var args = OS.GetCmdlineUserArgs();
        if (args.Contains("--run-tests")) { CallDeferred(nameof(RunTests)); return; }

        _world = new WorldView(); AddChild(_world);
        _cam = new CameraRig(); AddChild(_cam);
        var layer = new CanvasLayer(); AddChild(layer);
        _ui = new Control { Theme = Ui.BuildTheme(), MouseFilter = Control.MouseFilterEnum.Ignore };
        _ui.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(_ui);
        ShowTitle();
        if (AppSettings.Instance.CheckUpdatesOnStart && GameVersion.BuildChannel != "dev") _ = BackgroundUpdateCheck();
        var shot = Array.IndexOf(args, "--screenshot");
        if (shot >= 0 && shot + 1 < args.Length) _ = Screenshot(args[shot + 1], args.Contains("--in-game"), args.Contains("--info"));
    }

    // ---------------- screens ----------------
    void ShowTitle()
    {
        _playing = false;
        _hud?.QueueFree(); _hud = null;
        _state = Demo();
        _world.Build(_state);
        _world.GridVisible = false;
        _cam.SetView(0.6f, 0.62f, 70, new Vector3(32, 0, 30));
        _cam.InputEnabled = false;
        _menu = new MainMenu
        {
            Continue = () => LoadSlot(SaveSystem.List().First().Slot),
            NewGame = () => _ui.AddChild(new NewGameDialog(StartNew)),
            LoadGame = () => _ui.AddChild(new SaveLoadDialog(false, () => null, LoadSlot)),
            Info = OpenInfo,
            ToggleLanguage = ToggleLanguage,
            Quit = () => GetTree().Quit(),
        };
        _ui.AddChild(_menu);
    }

    /// <summary>A small example site shown behind the title screen.</summary>
    static GameState Demo()
    {
        var s = GameState.New(new NewGameOptions());
        s.Money = long.MaxValue / 4;
        for (var x = 18; x < 44; x++) for (var z = 16; z < 34; z++) s.AddFoundation(x, z);
        for (var z = 18; z < 32; z += 3) for (var x = 22; x < 40; x++) s.Objects.Add(new PlacedObject { Kind = BuildKind.Rack, X = x, Z = z });
        for (var z = 18; z < 32; z += 4) s.Objects.Add(new PlacedObject { Kind = BuildKind.Crah, X = 19, Z = z });
        for (var i = 0; i < 3; i++) s.Objects.Add(new PlacedObject { Kind = BuildKind.Generator, X = 48, Z = 16 + i * 4 });
        s.Objects.Add(new PlacedObject { Kind = BuildKind.FiberPit, X = 30, Z = 40 });
        return s;
    }

    void StartNew(NewGameOptions o) => StartGame(GameState.New(o), true);

    void LoadSlot(string slot)
    {
        var s = SaveSystem.Load(slot);
        if (s == null) { _hud?.Notify(Loc.T("msg_load_failed")); return; }
        StartGame(s, false);
        _hud.Notify(Loc.T("msg_loaded", s.Options.Company), Brand.Ok);
    }

    void StartGame(GameState s, bool fresh)
    {
        _menu?.QueueFree(); _menu = null;
        _hud?.QueueFree();
        _state = s;
        _world.Build(s);
        _world.GridVisible = true;
        _cam.InputEnabled = true;
        _cam.SetView(s.CamYaw, s.CamPitch, s.CamDistance, new Vector3(s.CamTarget[0], s.CamTarget[1], s.CamTarget[2]));
        if (fresh) _cam.SetView(0.6f, 0.9f, 70, new Vector3(s.PlotSize / 2f, 0, s.PlotSize / 2f));
        _cam.Bounds = new Rect2(-20, -20, s.PlotSize + 40, s.PlotSize + 40);
        _hud = new Hud
        {
            ToolChanged = (t, k) => { _tool = t; _kind = k; _rot = 0; _dragStart = null; _world.ClearGhost(); },
            SpeedChanged = sp => _speed = sp,
            RotateLeft = () => _cam.RotateStep(1), RotateRight = () => _cam.RotateStep(-1),
            ToggleGrid = () => _world.GridVisible = !_world.GridVisible,
            OpenInfo = OpenInfo, OpenMenu = OpenPause, ToggleLanguage = ToggleLanguage,
        };
        _ui.AddChild(_hud);
        _ui.MoveChild(_hud, 0);
        _playing = true; _speed = 1; _autosaveTimer = 0; _tool = Tool.Select;
        _hud.UpdateStats(_state);
        if (fresh) _hud.Notify(Loc.T(s.Options.AiCompetitors ? "msg_welcome_ai" : "msg_welcome", s.Options.Company));
    }

    void OpenInfo() => _ui.AddChild(new InfoDialog(() => { if (_playing) Autosave(); }));

    void OpenPause()
    {
        if (!_playing) return;
        var prev = _speed; _speed = 0;
        var m = new PauseMenu(
            save: () => _ui.AddChild(new SaveLoadDialog(true, () => { SyncCamera(); return _state; }, LoadSlot)),
            load: () => _ui.AddChild(new SaveLoadDialog(false, () => _state, LoadSlot)),
            info: OpenInfo,
            toMenu: () => { Autosave(); ShowTitle(); },
            quit: () => { Autosave(); GetTree().Quit(); });
        m.OnClosed = () => { _speed = prev; _hud?.SetSpeed(prev); };
        _ui.AddChild(m);
    }

    void ToggleLanguage()
    {
        Loc.SetLanguage(Loc.Language == "en" ? "nl" : "en");
        _hud?.RefreshLanguage();
        _menu?.Refresh();
        if (_state != null && _hud != null) _hud.UpdateStats(_state);
    }

    void SyncCamera()
    {
        _state.CamYaw = _cam.Yaw; _state.CamPitch = _cam.Pitch; _state.CamDistance = _cam.Distance;
        var t = _cam.TargetPoint; _state.CamTarget = new[] { t.X, t.Y, t.Z };
    }

    void Autosave()
    {
        if (!_playing || _state == null) return;
        SyncCamera();
        SaveSystem.Autosave(_state);
    }

    async System.Threading.Tasks.Task BackgroundUpdateCheck()
    {
        var r = await UpdateService.CheckAsync(AppSettings.Instance.UpdateChannel);
        if (r.Verdict == UpdateVerdict.NewerAvailable)
        {
            var msg = Loc.T("msg_update_available", r.Release.Version.ToString());
            if (_hud != null) _hud.Notify(msg, Brand.Amber); else GD.Print(msg);
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest) Autosave();
    }

    // ---------------- loop ----------------
    public override void _Process(double delta)
    {
        if (_cam == null) return;
        if (!_playing)
        {
            _cam.Yaw += (float)delta * 0.05f; // slow spin behind the title screen
            _cam.SetView(_cam.Yaw, _cam.Pitch, _cam.Distance, _cam.Position);
            return;
        }
        if (_speed > 0)
        {
            _state.Advance(delta * HoursPerSecond[_speed]);
            _hud.UpdateStats(_state);
        }
        _autosaveTimer += delta;
        if (_autosaveTimer > AppSettings.Instance.AutosaveMinutes * 60) { _autosaveTimer = 0; Autosave(); _hud.Notify(Loc.T("msg_autosaved"), Brand.Ok); }
    }

    // ---------------- building ----------------
    Vector2I? CellAt(Vector2 screen)
    {
        var p = _cam.GroundAt(screen);
        if (p == null) return null;
        return new Vector2I(Mathf.FloorToInt(p.Value.X), Mathf.FloorToInt(p.Value.Z));
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (!_playing || _state == null) return;
        if (e.IsActionPressed("ui_cancel"))
        {
            if (_tool != Tool.Select) _hud.Pick(Tool.Select, default); else OpenPause();
            GetViewport().SetInputAsHandled(); return;
        }
        if (e is InputEventKey { Pressed: true, Echo: false } k)
        {
            if (k.Keycode == Key.R) { _rot = (_rot + 1) % 4; UpdateGhost(); }
            if (k.Keycode == Key.Space) { var s = _speed == 0 ? 1 : 0; _speed = s; _hud.SetSpeed(s); }
            if (k.Keycode is Key.Key1 or Key.Key2 or Key.Key3) { _speed = (int)(k.Keycode - Key.Key0); _hud.SetSpeed(_speed); }
            if (k.Keycode == Key.F5 && k.CtrlPressed == false) { Autosave(); _hud.Notify(Loc.T("msg_autosaved"), Brand.Ok); }
        }
        if (e is InputEventMouseMotion mm)
        {
            var c = CellAt(mm.Position);
            if (c != null && c.Value != _hoverCell) { _hoverCell = c.Value; UpdateGhost(); }
        }
        if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left } mb)
        {
            var c = CellAt(mb.Position);
            if (c == null) return;
            if (mb.Pressed)
            {
                if (_tool == Tool.Build && _kind == BuildKind.Foundation) _dragStart = c;
                else if (_tool == Tool.Build) Place(c.Value);
                else if (_tool == Tool.Erase) _dragStart = c;
            }
            else if (_dragStart != null)
            {
                var r = RectOf(_dragStart.Value, c.Value);
                if (_tool == Tool.Erase) EraseRect(r); else PlaceFoundation(r);
                _dragStart = null; UpdateGhost();
            }
        }
    }

    static Rect2I RectOf(Vector2I a, Vector2I b)
    {
        var x0 = Math.Min(a.X, b.X); var z0 = Math.Min(a.Y, b.Y);
        return new Rect2I(x0, z0, Math.Abs(a.X - b.X) + 1, Math.Abs(a.Y - b.Y) + 1);
    }

    void UpdateGhost()
    {
        if (_tool == Tool.Select) { _world.ClearGhost(); return; }
        if (_tool == Tool.Erase || _kind == BuildKind.Foundation)
        {
            var r = RectOf(_dragStart ?? _hoverCell, _hoverCell);
            _world.ShowGhostRect(r, _tool == Tool.Erase ? Brand.Critical : Brand.Ok);
            return;
        }
        var o = new PlacedObject { Kind = _kind, X = _hoverCell.X, Z = _hoverCell.Y, Rot = _rot };
        _world.ShowGhost(new[] { o }, _state.CanPlace(o) == null);
    }

    void Place(Vector2I c)
    {
        var o = new PlacedObject { Kind = _kind, X = c.X, Z = c.Y, Rot = _rot };
        var why = _state.CanPlace(o);
        if (why != null) { _hud.Notify(Loc.T(why), Brand.Critical); return; }
        _state.Objects.Add(o);
        _state.Money -= Catalog.Defs[_kind].Cost;
        _world.Refresh(); _hud.UpdateStats(_state); UpdateGhost();
        if (_kind == BuildKind.Rack && _state.CoolingDemandKw > _state.CoolingCapacityKw)
            _hud.Notify(Loc.T("msg_cooling_short"), Brand.Cooling);
    }

    void PlaceFoundation(Rect2I r)
    {
        var cost = Catalog.Defs[BuildKind.Foundation].Cost; var n = 0;
        for (var x = r.Position.X; x < r.End.X; x++)
            for (var z = r.Position.Y; z < r.End.Y; z++)
            {
                if (!_state.InPlot(x, z) || _state.HasFoundation(x, z) || _state.ObjectAt(x, z) != null) continue;
                if (_state.Money < cost) { _hud.Notify(Loc.T("msg_no_money"), Brand.Critical); goto done; }
                _state.AddFoundation(x, z); _state.Money -= cost; n++;
            }
        done:
        if (n > 0) { _world.Refresh(); _hud.UpdateStats(_state); }
    }

    void EraseRect(Rect2I r)
    {
        var changed = false;
        for (var x = r.Position.X; x < r.End.X; x++)
            for (var z = r.Position.Y; z < r.End.Y; z++)
            {
                var o = _state.ObjectAt(x, z);
                if (o != null) { _state.Objects.Remove(o); _state.Money += Catalog.Defs[o.Kind].Cost / 2; changed = true; }
                else if (_state.HasFoundation(x, z) && !_state.Objects.Any(p => p.X == x && p.Z == z)) { _state.RemoveFoundation(x, z); changed = true; }
            }
        if (changed) { _world.Refresh(); _hud.UpdateStats(_state); }
    }

    // ---------------- dev helpers ----------------
    async System.Threading.Tasks.Task Screenshot(string path, bool inGame, bool info)
    {
        if (inGame)
        {
            StartGame(Demo(), true);
            _state.Money = 12_000_000;
            _hud.UpdateStats(_state);
            _hud.Notify(Loc.T("msg_cooling_short"), Brand.Cooling);
        }
        if (info) OpenInfo();
        for (var i = 0; i < 40; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GetViewport().GetTexture().GetImage().SavePng(path);
        GetTree().Quit();
    }

    void RunTests()
    {
        var failures = Tests.TestRunner.RunAll();
        GetTree().Quit(failures == 0 ? 0 : 1);
    }
}
