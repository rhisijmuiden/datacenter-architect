using System;
using System.Collections.Generic;
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
    Ambience _sound;
    UiSounds _uiSounds;
    Control _ui;
    Hud _hud;
    MainMenu _menu;
    GameState _state;
    Simulation _sim;
    bool _playing;
    int _speed = 1;
    double _autosaveTimer, _goalTimer;
    BuildItem _item = BuildItem.Select;
    int _rot, _floors = 1;
    Facade _facade = Facade.Brick;
    Vector2I? _dragStart;
    Vector2I _hoverCell;
    Vector2 _hoverPoint;
    /// <summary>Game hours per real second for pause, normal, fast and fastest.</summary>
    static readonly double[] HoursPerSecond = { 0, 0.25, 1, 3 };

    public override void _Ready()
    {
        Loc.Load();
        TranslationServer.SetLocale(AppSettings.Instance.Language);
        var args = OS.GetCmdlineUserArgs();
        if (args.Contains("--run-tests")) { CallDeferred(nameof(RunTests)); return; }

        _world = new WorldView(); AddChild(_world);
        _cam = new CameraRig(); AddChild(_cam);
        _sound = new Ambience(); AddChild(_sound);
        _uiSounds = new UiSounds(); AddChild(_uiSounds);
        ApplyDisplay(); ApplyOther();
        var layer = new CanvasLayer(); AddChild(layer);
        _ui = new Control { Theme = Ui.BuildTheme(), MouseFilter = Control.MouseFilterEnum.Ignore };
        _ui.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(_ui);
        ShowTitle();
        if (AppSettings.Instance.CheckUpdatesOnStart && GameVersion.BuildChannel != "dev") _ = BackgroundUpdateCheck();
        var shot = Array.IndexOf(args, "--screenshot");
        if (shot >= 0 && shot + 1 < args.Length) _ = Screenshot(args[shot + 1], args);
    }

    // ---------------- screens ----------------
    void ShowTitle()
    {
        _playing = false;
        _hud?.QueueFree(); _hud = null;
        SetState(Demo.Build());
        _world.GridVisible = false;
        _world.Roofs = true; _world.ViewFloor = 0;
        _cam.SetView(0.6f, 0.62f, 80, new Vector3(30, 0, 34));
        _cam.InputEnabled = false;
        _menu = new MainMenu
        {
            Continue = () => LoadSlot(SaveSystem.List().First().Slot),
            NewGame = () => _ui.AddChild(new NewGameDialog(StartNew)),
            LoadGame = () => _ui.AddChild(new SaveLoadDialog(false, () => null, LoadSlot)),
            Options = OpenOptions,
            Info = OpenInfo,
            ToggleLanguage = ToggleLanguage,
            Quit = () => GetTree().Quit(),
        };
        _ui.AddChild(_menu);
    }

    void SetState(GameState s)
    {
        _state = s;
        _sim = new Simulation(s);
        _world.Build(s, _sim);
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
        SetState(s);
        _world.GridVisible = true;
        _world.Roofs = true; _world.ViewFloor = 0;
        _cam.InputEnabled = true;
        _cam.SetView(s.CamYaw, s.CamPitch, s.CamDistance, new Vector3(s.CamTarget[0], s.CamTarget[1], s.CamTarget[2]));
        if (fresh) _cam.SetView(0.6f, 0.9f, 70, new Vector3(s.PlotSize / 2f, 0, s.PlotSize / 2f + 4));
        _cam.Bounds = new Rect2(-30, -30, s.PlotSize + 60, s.PlotSize + 60);
        _hud = new Hud
        {
            State = () => _state,
            ToolChanged = OnTool,
            SpeedChanged = sp => _speed = sp,
            FloorsChanged = f => { _floors = f; UpdateGhost(); },
            FacadeChanged = f => _facade = f,
            CrewChanged = ChangeCrew,
            CyclePriority = CyclePriority,
            RotateLeft = () => _cam.RotateStep(1), RotateRight = () => _cam.RotateStep(-1),
            ToggleGrid = () => _world.GridVisible = !_world.GridVisible,
            ToggleRoofs = () => SetView(_world.ViewFloor, !_world.Roofs),
            FloorUp = () => SetView(_world.ViewFloor + 1, false),
            FloorDown = () => SetView(_world.ViewFloor - 1, _world.ViewFloor - 1 <= 0 && _world.Roofs),
            OpenInfo = OpenInfo, OpenOptions = OpenOptions, OpenMenu = OpenPause, ToggleLanguage = ToggleLanguage,
            OpenPower = OpenPower, ToggleOverlay = m => SetOverlay(_world.Overlay.Mode == (OverlayMode)m ? OverlayMode.None : (OverlayMode)m),
        };
        _ui.AddChild(_hud);
        _ui.MoveChild(_hud, 0);
        _playing = true; _speed = 1; _autosaveTimer = 0; _item = BuildItem.Select;
        _inspectCell = null; SetOverlay(OverlayMode.None);
        _hud.UpdateStats(_state);
        UpdateGoals();
        if (fresh)
        {
            _hud.Notify(Loc.T(s.Options.AiCompetitors ? "msg_welcome_ai" : "msg_welcome", s.Options.Company));
            _hud.Notify(Loc.T("msg_start_hint"), Brand.Cooling);
        }
    }

    void SetView(int floor, bool roofs)
    {
        floor = Math.Clamp(floor, 0, Catalog.MaxFloors - 1);
        _world.ViewFloor = floor;
        _world.Roofs = roofs && floor == 0;
        _hud?.SetView(_world.ViewFloor, _world.Roofs);
        UpdateGhost();
    }

    void OnTool(BuildItem item)
    {
        _item = item; _rot = 0; _dragStart = null; _world.ClearGhost();
        var inside = item.Mode is Mode.Room or Mode.Door or Mode.Row ||
            (item.Mode == Mode.Object && Catalog.Objects[(ObjKind)item.Arg].Where is Where.Room or Where.Hall or Where.Building);
        if (inside && _world.Roofs) SetView(_world.ViewFloor, false);
        if (item.Mode is Mode.Surface or Mode.Fence or Mode.Building && _world.ViewFloor > 0) SetView(0, _world.Roofs);
    }

    void ChangeCrew(Role role, int delta)
    {
        switch (role)
        {
            case Role.Builder: _state.Builders = Math.Clamp(_state.Builders + delta, 0, 40); break;
            case Role.Fitter: _state.Fitters = Math.Clamp(_state.Fitters + delta, 0, 20); break;
            case Role.Engineer: _state.Engineers = Math.Clamp(_state.Engineers + delta, 0, 20); break;
        }
        _hud.UpdateStats(_state);
    }

    void CyclePriority(int id)
    {
        var b = _state.Buildings.FirstOrDefault(x => x.Id == id);
        if (b != null) b.Priority = b.Priority % 3 + 1;
        var r = _state.Rooms.FirstOrDefault(x => x.Id == id);
        if (r != null) r.Priority = r.Priority % 3 + 1;
        UpdateProjects();
    }

    void OpenPower()
    {
        if (!_playing || _ui.GetChildren().OfType<PowerPanel>().Any()) return;
        _ui.AddChild(new PowerPanel(() => _state, Fail));
    }

    void SetOverlay(OverlayMode m)
    {
        _world.Overlay.Mode = m;
        _hud?.SetOverlay((int)m);
    }

    void OpenOptions(int tab)
    {
        if (_ui.GetChildren().OfType<OptionsDialog>().Any()) return;
        _ui.AddChild(new OptionsDialog(ApplyDisplay, ApplyOther, SetLanguage, tab));
    }

    void OpenOptions() => OpenOptions(0);

    /// <summary>Window mode, resolution, monitor, V-Sync, frame cap and interface size.</summary>
    void ApplyDisplay() => DisplaySettings.Apply(AppSettings.Instance, GetTree().Root);

    /// <summary>Graphics, visual effects, camera and audio.</summary>
    void ApplyOther()
    {
        var s = AppSettings.Instance;
        _world?.ApplyEffects(s);
        if (_cam != null) _cam.Smooth = s.SmoothCamera;
        AudioMixer.Apply(s);
    }

    void OpenInfo() => _ui.AddChild(new InfoDialog(() => { if (_playing) Autosave(); }));

    void OpenPause()
    {
        if (!_playing) return;
        var prev = _speed; _speed = 0;
        var m = new PauseMenu(
            save: () => _ui.AddChild(new SaveLoadDialog(true, () => { SyncCamera(); return _state; }, LoadSlot)),
            load: () => _ui.AddChild(new SaveLoadDialog(false, () => _state, LoadSlot)),
            options: OpenOptions,
            info: OpenInfo,
            toMenu: () => { Autosave(); ShowTitle(); },
            quit: () => { Autosave(); GetTree().Quit(); });
        m.OnClosed = () => { _speed = prev; _hud?.SetSpeed(prev); };
        _ui.AddChild(m);
    }

    void ToggleLanguage() => SetLanguage(Loc.Language == "en" ? "nl" : "en");

    void SetLanguage(string lang)
    {
        Loc.SetLanguage(lang);
        _hud?.RefreshLanguage();
        _menu?.Refresh();
        _world.Invalidate();
        if (_state != null && _hud != null) { _hud.UpdateStats(_state); _hud.SetView(_world.ViewFloor, _world.Roofs); UpdateGoals(); }
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
        if (what == NotificationApplicationFocusOut) AudioMixer.SetInBackground(true, AppSettings.Instance);
        if (what == NotificationApplicationFocusIn) AudioMixer.SetInBackground(false, AppSettings.Instance);
    }

    // ---------------- loop ----------------
    public override void _Process(double delta)
    {
        if (_cam == null || _state == null) return;
        var hours = delta * HoursPerSecond[_playing ? _speed : 1];
        if (hours > 0)
        {
            if (_playing) _state.Advance(hours);
            _sim.Tick(hours);
        }
        _world.Sync(delta);
        UpdateSound();
        if (!_playing)
        {
            _cam.Yaw += (float)delta * 0.04f; // slow spin behind the title screen
            _cam.SetView(_cam.Yaw, _cam.Pitch, _cam.Distance, _cam.Position);
            _sim.Messages.Clear(); _state.Notes.Clear();
            return;
        }
        foreach (var (key, arg) in _sim.Messages.Concat(_state.Notes))
        {
            var text = Loc.T(key, arg == null ? "" : Loc.T(arg));
            var accent = key.Contains("done") || key.Contains("live") || key.Contains("ready") || key.Contains("cool") ? Brand.Ok : key.Contains("need") || key.Contains("unreach") || key.Contains("no_road") || key.Contains("hot") ? Brand.Critical : Brand.Amber;
            _hud.Notify(text, accent);
        }
        _sim.Messages.Clear(); _state.Notes.Clear();
        _hud.UpdateStats(_state);
        _goalTimer -= delta;
        if (_goalTimer <= 0) { _goalTimer = 1; UpdateGoals(); UpdateProjects(); RefreshInspect(); }
        _autosaveTimer += delta;
        if (_autosaveTimer > AppSettings.Instance.AutosaveMinutes * 60) { _autosaveTimer = 0; Autosave(); _hud.Notify(Loc.T("msg_autosaved"), Brand.Ok); }
    }

    /// <summary>Server noise grows as the camera zooms in on live racks that are in view.</summary>
    void UpdateSound()
    {
        var t = _cam.TargetPoint;
        var near = _state.Objects.Count(o => o.Kind == ObjKind.Rack && o.Status == ObjStatus.Live && Math.Abs(o.X - t.X) < 14 && Math.Abs(o.Z - t.Z) < 14);
        var zoom = Mathf.Clamp((60 - _cam.Distance) / 45f, 0, 1);
        var open = _world.Roofs ? 0.35f : 1f;
        _sound.Level = _playing ? Mathf.Clamp(near / 40f, 0, 1) * zoom * open : 0;
    }

    void UpdateGoals()
    {
        if (_hud == null) return;
        var s = _state;
        _hud.UpdateGoals(new[]
        {
            ("goal_driveway", _sim.Entries().Count > 0),
            ("goal_yard", _sim.YardCells().Any()),
            ("goal_building", s.Buildings.Any(b => b.Kind == BuildingKind.Datacenter && b.Stage == Stage.Done)),
            ("goal_hall", s.Rooms.Any(r => r.Type == RoomType.DataHall && r.Stage == Stage.Done)),
            ("goal_dock", _sim.Docks().Count > 0),
            ("goal_rack", s.LiveRacks > 0),
            ("goal_office", s.Rooms.Any(r => r.Type == RoomType.Executive && r.Stage == Stage.Done && s.Objects.Any(o => o.Kind == ObjKind.Desk && r.Contains(o.X, o.Z) && o.Floor == r.Floor))),
            ("goal_switchgear", s.Objects.Any(o => o.Kind == ObjKind.Switchgear && o.Status == ObjStatus.Live)),
            ("goal_ups", s.Objects.Any(o => o.Kind == ObjKind.Ups && o.Status == ObjStatus.Live) && s.Objects.Any(o => o.Kind == ObjKind.Battery && o.Status == ObjStatus.Live)),
            ("goal_pdu", s.Objects.Any(o => o.Kind == ObjKind.Pdu && o.Status == ObjStatus.Live) || s.Rooms.Any(r => r.Busbar)),
            ("goal_cooling", s.Objects.Any(o => o.Def.RejectKw > 0)),
            ("goal_generators", s.Objects.Count(o => o.Kind == ObjKind.Generator) >= 2),
        });
    }

    void UpdateProjects()
    {
        if (_hud == null) return;
        var list = new List<(int, string, double, int, bool)>();
        foreach (var b in _state.Buildings.Where(b => b.Stage != Stage.Done))
            list.Add((b.Id, Catalog.Buildings[b.Kind].NameKey, GameState.Progress(b), b.Priority, false));
        foreach (var r in _state.Rooms.Where(r => r.Stage != Stage.Done))
            list.Add((r.Id, Catalog.Rooms[r.Type].NameKey, GameState.StageProgress(r), r.Priority, _state.BuildingOf(r)?.Stage != Stage.Done));
        _hud.UpdateProjects(list.OrderByDescending(p => p.Item4).ThenBy(p => p.Item1).ToList());
    }

    // ---------------- building input ----------------
    float PickHeight => _world.ViewFloor == 0 ? 0.15f : WorldView.Base(_world.ViewFloor);

    Vector2? PointAt(Vector2 screen)
    {
        var p = _cam.GroundAt(screen, PickHeight);
        return p == null ? null : new Vector2(p.Value.X, p.Value.Z);
    }

    static Vector2I CellOf(Vector2 p) => new(Mathf.FloorToInt(p.X), Mathf.FloorToInt(p.Y));

    /// <summary>Alt+Enter and F11 switch between windowed and fullscreen, on any screen.</summary>
    public override void _Input(InputEvent e)
    {
        if (_ui == null) return;
        if (e is InputEventKey { Pressed: true, Echo: false } k && (k.Keycode == Key.F11 || (k.Keycode == Key.Enter && k.AltPressed)))
        {
            DisplaySettings.ToggleFullscreen(AppSettings.Instance, GetTree().Root);
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (!_playing || _state == null) return;
        if (e.IsActionPressed("ui_cancel"))
        {
            if (_dragStart != null) { _dragStart = null; UpdateGhost(); }
            else if (_item.Mode != Mode.Select) _hud.Pick(BuildItem.Select);
            else OpenPause();
            GetViewport().SetInputAsHandled(); return;
        }
        if (e is InputEventKey { Pressed: true, Echo: false } k)
        {
            if (k.Keycode == Key.R) { _rot = (_rot + 1) % 4; UpdateGhost(); }
            if (k.Keycode == Key.Space) { var s = _speed == 0 ? 1 : 0; _speed = s; _hud.SetSpeed(s); }
            if (k.Keycode is Key.Key1 or Key.Key2 or Key.Key3) { _speed = (int)(k.Keycode - Key.Key0); _hud.SetSpeed(_speed); }
            if (k.Keycode == Key.F5) { Autosave(); _hud.Notify(Loc.T("msg_autosaved"), Brand.Ok); }
            if (k.Keycode == Key.Pageup) SetView(_world.ViewFloor + 1, false);
            if (k.Keycode == Key.Pagedown) SetView(_world.ViewFloor - 1, _world.Roofs);
            if (k.Keycode == Key.T) SetView(_world.ViewFloor, !_world.Roofs);
            if (k.Keycode == Key.P) SetOverlay(_world.Overlay.Mode == OverlayMode.Power ? OverlayMode.None : OverlayMode.Power);
            if (k.Keycode == Key.H) SetOverlay(_world.Overlay.Mode == OverlayMode.Cooling ? OverlayMode.None : OverlayMode.Cooling);
        }
        if (e is InputEventMouseMotion mm)
        {
            var p = PointAt(mm.Position);
            if (p == null) return;
            var cell = RoofItem ? RoofCell(mm.Position) ?? CellOf(p.Value) : CellOf(p.Value);
            var doorMoved = _item.Mode == Mode.Door && DoorAt(p.Value) is var a && DoorAt(_hoverPoint) is var b && (a.X != b.X || a.Z != b.Z || a.Dir != b.Dir);
            _hoverPoint = p.Value;
            if (cell != _hoverCell || doorMoved) { _hoverCell = cell; UpdateGhost(); }
        }
        if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left } mb)
        {
            var p = PointAt(mb.Position);
            if (p == null) return;
            var c = CellOf(p.Value);
            if (mb.Pressed)
            {
                switch (_item.Mode)
                {
                    case Mode.Select:
                        var roof = _world.Roofs ? RoofCell(mb.Position) : null;
                        Inspect(roof ?? c, roof != null);
                        break;
                    case Mode.Object: PlaceObject(RoofItem ? RoofCell(mb.Position) ?? c : c); break;
                    case Mode.Door: PlaceDoor(DoorAt(p.Value)); break;
                    default: _dragStart = c; break;
                }
                UpdateGhost();
            }
            else if (_dragStart != null)
            {
                var r = RectOf(_dragStart.Value, c);
                ApplyDrag(_dragStart.Value, c, r);
                _dragStart = null; UpdateGhost();
            }
        }
    }

    static Rect2I RectOf(Vector2I a, Vector2I b)
    {
        var x0 = Math.Min(a.X, b.X); var z0 = Math.Min(a.Y, b.Y);
        return new Rect2I(x0, z0, Math.Abs(a.X - b.X) + 1, Math.Abs(a.Y - b.Y) + 1);
    }

    /// <summary>The wall edge closest to the mouse, as a door of the chosen type centred on the cursor.</summary>
    Door DoorAt(Vector2 p)
    {
        var type = _item.Mode == Mode.Door ? (DoorType)_item.Arg : DoorType.Single;
        var w = Catalog.Doors[type].Width;
        int cx = Mathf.FloorToInt(p.X), cz = Mathf.FloorToInt(p.Y);
        float fx = p.X - cx, fz = p.Y - cz;
        var d = new[] { fx, 1 - fx, fz, 1 - fz };
        var i = Array.IndexOf(d, d.Min());
        return i switch
        {
            0 => new Door { Type = type, Dir = 0, X = cx, Z = cz - (w - 1) / 2, Floor = _world.ViewFloor },
            1 => new Door { Type = type, Dir = 0, X = cx + 1, Z = cz - (w - 1) / 2, Floor = _world.ViewFloor },
            2 => new Door { Type = type, Dir = 1, X = cx - (w - 1) / 2, Z = cz, Floor = _world.ViewFloor },
            _ => new Door { Type = type, Dir = 1, X = cx - (w - 1) / 2, Z = cz + 1, Floor = _world.ViewFloor },
        };
    }

    string Dim(Rect2I r) => $"{r.Size.X} × {r.Size.Y} m";

    void UpdateGhost()
    {
        if (_world == null || _state == null) return;
        var r = RectOf(_dragStart ?? _hoverCell, _hoverCell);
        var y = PickHeight;
        switch (_item.Mode)
        {
            case Mode.Select: _world.ClearGhost(); break;
            case Mode.Erase: _world.ShowGhostRect(r, Brand.Critical, y); break;
            case Mode.Surface:
                var sc = Catalog.Surfaces[(Surface)_item.Arg].Cost * r.Size.X * r.Size.Y;
                _world.ShowGhostRect(r, Brand.Ok, 0, _dragStart == null ? null : $"{Dim(r)} · {Ui.Money(sc)}");
                break;
            case Mode.Fence:
                var n = GameState.FenceCells(r.Position.X, r.Position.Y, r.Size.X, r.Size.Y).Count();
                _world.ShowGhostRect(r, Brand.Ok, 0, _dragStart == null ? null : $"{n} m · {Ui.Money(n * Catalog.Fences[(FenceType)_item.Arg].Cost)}", true);
                break;
            case Mode.Building:
                var kind = (BuildingKind)_item.Arg;
                var why = _dragStart == null ? null : _state.CanBuild(kind, r.Position.X, r.Position.Y, r.Size.X, r.Size.Y, _floors);
                var cost = GameState.BuildingCost(kind, r.Size.X, r.Size.Y, _floors);
                var text = _dragStart == null ? null : $"{Dim(r)} · {Loc.T("hud_floors_n", _floors)} · {Ui.Money(cost)}" + (why != null ? "\n" + Loc.T(why) : "");
                _world.ShowGhostRect(r, why == null ? Brand.Ok : Brand.Critical, 0, text);
                break;
            case Mode.Room:
                var rt = (RoomType)_item.Arg;
                var rwhy = _dragStart == null ? null : _state.CanRoom(rt, r.Position.X, r.Position.Y, r.Size.X, r.Size.Y, _world.ViewFloor);
                var rtext = _dragStart == null ? null : $"{Loc.T(Catalog.Rooms[rt].NameKey)} · {Dim(r)} · {Ui.Money(Catalog.RoomCost(r.Size.X, r.Size.Y))}" + (rwhy != null ? "\n" + Loc.T(rwhy) : "");
                _world.ShowGhostRect(r, rwhy == null ? new Color(Catalog.Rooms[rt].Color) : Brand.Critical, y, rtext);
                break;
            case Mode.Door:
                var door = DoorAt(_hoverPoint);
                _world.ShowGhostDoor(door, _state.CanDoor(door) == null);
                break;
            case Mode.Object:
                var gk = (ObjKind)_item.Arg;
                var o = new PlacedObject { Kind = gk, X = _hoverCell.X, Z = _hoverCell.Y, Rot = _rot, Floor = _state.PlaceFloor(gk, _hoverCell.X, _hoverCell.Y, _world.ViewFloor) };
                var gwhy = _state.CanPlace(o);
                _world.ShowGhostObjects(new[] { (o, gwhy == null) }, gwhy == null ? null : Loc.T(gwhy));
                break;
            case Mode.Row:
                var start = _dragStart ?? _hoverCell;
                var kindR = (ObjKind)_item.Arg;
                var cells = GameState.RowCells(start.X, start.Y, _hoverCell.X, _hoverCell.Y, kindR, _rot);
                var objs = cells.Select(c => new PlacedObject { Kind = kindR, X = c.x, Z = c.z, Rot = _rot, Floor = _world.ViewFloor }).Select(x => (x, _state.CanPlace(x) == null)).ToList();
                var ok = objs.Count(x => x.Item2);
                _world.ShowGhostObjects(objs, _dragStart == null ? null : $"{ok} × {Loc.T(Catalog.Objects[kindR].NameKey)} · {Ui.Money(ok * Catalog.Objects[kindR].Cost)}");
                break;
        }
    }

    void Fail(string why) { if (why != null) _hud.Notify(Loc.T(why), Brand.Critical); }

    void ApplyDrag(Vector2I a, Vector2I b, Rect2I r)
    {
        switch (_item.Mode)
        {
            case Mode.Erase:
                if (_state.Erase(r.Position.X, r.Position.Y, r.Size.X, r.Size.Y, _world.ViewFloor, _world.Roofs && _world.ViewFloor == 0)) _hud.UpdateStats(_state);
                break;
            case Mode.Surface:
                var why = _state.PaintSurface(r.Position.X, r.Position.Y, r.Size.X, r.Size.Y, (Surface)_item.Arg, out _);
                if (why == "msg_no_money") Fail(why);
                break;
            case Mode.Fence:
                var fw = _state.PlaceFence(r.Position.X, r.Position.Y, r.Size.X, r.Size.Y, (FenceType)_item.Arg, out _);
                if (fw is "msg_no_money" or "msg_needs_research") Fail(fw);
                break;
            case Mode.Building:
                var kind = (BuildingKind)_item.Arg;
                var bw = _state.CanBuild(kind, r.Position.X, r.Position.Y, r.Size.X, r.Size.Y, _floors);
                if (bw != null) { Fail(bw); break; }
                _state.AddBuilding(kind, r.Position.X, r.Position.Y, r.Size.X, r.Size.Y, _floors, _facade);
                if (!_sim.YardCells().Any()) _hud.Notify(Loc.T("msg_need_yard"), Brand.Amber);
                break;
            case Mode.Room:
                var rt = (RoomType)_item.Arg;
                var rw = _state.CanRoom(rt, r.Position.X, r.Position.Y, r.Size.X, r.Size.Y, _world.ViewFloor);
                if (rw != null) { Fail(rw); break; }
                _state.AddRoom(rt, r.Position.X, r.Position.Y, r.Size.X, r.Size.Y, _world.ViewFloor);
                break;
            case Mode.Row:
                var k = (ObjKind)_item.Arg;
                var placed = 0; string last = null;
                foreach (var (x, z) in GameState.RowCells(a.X, a.Y, b.X, b.Y, k, _rot))
                {
                    var o = new PlacedObject { Kind = k, X = x, Z = z, Rot = _rot, Floor = _world.ViewFloor };
                    last = _state.CanPlace(o);
                    if (last == null && _state.AddObject(k, x, z, _rot, _world.ViewFloor) != null) placed++;
                }
                if (placed == 0) Fail(last);
                else _hud.Notify(Loc.T("msg_racks_ordered", placed), Brand.Ok);
                break;
        }
        _hud.UpdateStats(_state);
    }

    void PlaceObject(Vector2I c)
    {
        var k = (ObjKind)_item.Arg;
        var floor = _state.PlaceFloor(k, c.X, c.Y, _world.ViewFloor);
        var o = new PlacedObject { Kind = k, X = c.X, Z = c.Y, Rot = _rot, Floor = floor };
        var why = _state.CanPlace(o);
        if (why != null) { Fail(why); return; }
        _state.AddObject(k, c.X, c.Y, _rot, floor);
        _hud.UpdateStats(_state);
    }

    void PlaceDoor(Door d)
    {
        var why = _state.CanDoor(d);
        if (why != null) { Fail(why); return; }
        _state.AddDoor(d.Type, d.X, d.Z, d.Dir, d.Floor);
        _hud.UpdateStats(_state);
    }

    bool RoofItem => _item.Mode == Mode.Object && Catalog.Objects[(ObjKind)_item.Arg].Where == Where.OutsideOrRoof;

    /// <summary>The roof cell under the mouse, when a finished building's roof is shown there.</summary>
    Vector2I? RoofCell(Vector2 screen)
    {
        if (_state == null) return null;
        foreach (var b in _state.Buildings.Where(b => b.Stage == Stage.Done).OrderByDescending(b => b.Floors))
        {
            if (!_world.Roofs && b.Floors > _world.ViewFloor) continue;
            var p = _cam.GroundAt(screen, WorldView.Base(b.Floors) + 0.3f);
            if (p == null) continue;
            var c = new Vector2I(Mathf.FloorToInt(p.Value.X), Mathf.FloorToInt(p.Value.Z));
            if (b.Contains(c.X, c.Y)) return c;
        }
        return null;
    }

    Vector2I? _inspectCell;
    bool _inspectRoof;

    void RefreshInspect()
    {
        if (_inspectCell != null) Inspect(_inspectCell.Value, _inspectRoof);
    }

    static string Kw(double v) => $"{v:0} kW";

    void Act(string why) { if (why != null) Fail(why); _hud.UpdateStats(_state); RefreshInspect(); }

    void Inspect(Vector2I c, bool roof = false)
    {
        _inspectCell = c; _inspectRoof = roof;
        var f = _world.ViewFloor;
        var infra = _state.Infra;
        var o = roof ? _state.ObjectAt(c.X, c.Y, GameState.RoofFloor) : _state.ObjectAt(c.X, c.Y, f);
        if (o == null && roof && !_world.Roofs) o = _state.ObjectAt(c.X, c.Y, f);
        if (o != null)
        {
            var d = o.Def;
            var lines = new List<string>();
            var actions = new List<(string, Action, bool)>();
            if (d.Chain) lines.Add(Loc.T("status_" + o.Status.ToString().ToLowerInvariant()));
            if (o.Kind == ObjKind.Rack && o.Status == ObjStatus.Live)
            {
                var hall = _state.RoomAt(o.X, o.Z, o.Floor);
                lines.Add(!infra.Down.Contains(o.Id) ? Loc.T("inspect_running") : hall?.Hot == true ? "⚠ " + Loc.T("inspect_down_heat") : "⚠ " + Loc.T("inspect_down_power"));
            }
            if (d.PowerKw > 0) lines.Add($"{d.PowerKw:0.#} kW");
            if (d.CoolingKw > 0) lines.Add(Loc.T("inspect_cooling", Math.Round(d.CoolingKw * Catalog.LevelFactor(o.Level))));
            if (d.RejectKw > 0) lines.Add(Loc.T("inspect_reject", Math.Round(d.RejectKw * Catalog.LevelFactor(o.Level))));
            if (d.CapacityKw > 0) lines.Add(Loc.T("inspect_capacity", d.CapacityKw));
            if (d.StorageKwh > 0) lines.Add(Loc.T("inspect_storage", d.StorageKwh));
            if (d.GenKw > 0) lines.Add(Loc.T("inspect_generator", d.GenKw));
            if (d.FuelLitres > 0) lines.Add(Loc.T("inspect_fuel", d.FuelLitres));
            if (o.Floor == GameState.RoofFloor) lines.Add(Loc.T("inspect_on_roof"));
            if (d.MaxLevel > 1)
            {
                lines.Add(Loc.T("inspect_level", o.Level, d.MaxLevel));
                if (o.Level < d.MaxLevel)
                {
                    var target = o;
                    actions.Add((Loc.T("action_upgrade", o.Level + 1, Ui.Money(Catalog.UpgradeCost(d, o.Level))), () => Act(_state.UpgradeObject(target)), _state.CanUpgrade(o) == null));
                }
            }
            if (d.Rooms != null && o.Kind is ObjKind.Transformer or ObjKind.Switchgear or ObjKind.Ups or ObjKind.Battery)
                actions.Add((Loc.T("power_overview"), OpenPower, true));
            _hud.Inspect(Loc.T(d.NameKey), lines, actions);
            return;
        }
        if (!roof && _state.InStation(c.X, c.Y))
        {
            var lv = Catalog.GridLevels[_state.GridLevel];
            var lines = new List<string> { $"{lv.Mva:0} MVA · {Loc.T(lv.NameKey)}" };
            if (_state.GridRequest >= 0) lines.Add(Loc.T("grid_request_running", Catalog.GridLevels[_state.GridRequest].Mva, Math.Ceiling(_state.GridDaysLeft)));
            _hud.Inspect(Loc.T("grid_station"), lines, new List<(string, Action, bool)> { (Loc.T("power_overview"), OpenPower, true) });
            return;
        }
        var r = roof ? null : _state.RoomAt(c.X, c.Y, f);
        var b = _state.BuildingAt(c.X, c.Y);
        if (r != null && !_world.Roofs)
        {
            var lines = new List<string> { $"{r.W} × {r.D} m · {(f == 0 ? Loc.T("hud_floor_ground") : Loc.T("hud_floor_n", f))}" };
            var actions = new List<(string, Action, bool)>();
            lines.Add(r.Stage == Stage.Done ? Loc.T("inspect_ready") : Loc.T("inspect_building_walls", Mathf.RoundToInt(GameState.StageProgress(r) * 100)));
            if (r.Type == RoomType.DataHall)
            {
                var hi = infra.Halls.FirstOrDefault(h => h.Room == r);
                lines.Add(Loc.T("inspect_racks", _state.Objects.Count(x => x.Kind == ObjKind.Rack && x.Floor == r.Floor && r.Contains(x.X, x.Z) && x.Status == ObjStatus.Live),
                    _state.Objects.Count(x => x.Kind == ObjKind.Rack && x.Floor == r.Floor && r.Contains(x.X, x.Z))));
                var cont = Catalog.Containments[r.Containment];
                lines.Add($"{Loc.T(cont.NameKey)} · PUE {cont.Pue.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}");
                if (hi != null)
                {
                    lines.Add(Loc.T("inspect_hall_power", Kw(hi.ItKw), Kw(hi.DistributionKw), hi.ItKw <= 0 ? "-" : hi.AB ? "A+B" : Loc.T("hall_ab_no")));
                    lines.Add(Loc.T("inspect_hall_cooling", Kw(hi.CrahKw), $"{r.Temp:0}"));
                    if (hi.RunningRacks < hi.LiveRacks) lines.Add("⚠ " + Loc.T(r.Hot ? "inspect_hall_hot" : "inspect_hall_short", hi.LiveRacks - hi.RunningRacks));
                }
                if (r.Upgrade != 0) lines.Add(Loc.T("inspect_upgrading", Math.Ceiling(r.UpgradeLeft)));
                var hall = r;
                if (r.Containment < Containment.ColdCorridor)
                {
                    var next = Catalog.Containments[r.Containment + 1];
                    actions.Add((Loc.T("action_containment", Loc.T(next.NameKey), Ui.Money(GameState.HallUpgradeCost(r))), () => Act(_state.UpgradeHall(hall, false)), _state.CanUpgradeHall(r, false) == null));
                }
                if (!r.Busbar) actions.Add((Loc.T("action_busbar", Ui.Money(Catalog.BusbarCost)), () => Act(_state.UpgradeHall(hall, true)), _state.CanUpgradeHall(r, true) == null));
            }
            _hud.Inspect(Loc.T(Catalog.Rooms[r.Type].NameKey), lines, actions);
            return;
        }
        if (b != null)
        {
            var lines = new List<string>
            {
                $"{b.W} × {b.D} m · {Loc.T("hud_floors_n", b.Floors)} · {Loc.T("facade_" + b.Facade.ToString().ToLowerInvariant())}",
                b.Stage == Stage.Done ? Loc.T("inspect_ready") : $"{Loc.T("stage_" + b.Stage.ToString().ToLowerInvariant())} · {Mathf.RoundToInt(GameState.Progress(b) * 100)}%",
            };
            if (b.Stage != Stage.Done) lines.Add(Loc.T("inspect_material", b.Material, GameState.MaterialNeed(b)));
            _hud.Inspect(Loc.T(Catalog.Buildings[b.Kind].NameKey), lines);
            return;
        }
        _inspectCell = null;
        _hud.Inspect(null, null);
    }

    // ---------------- dev helpers ----------------
    /// <summary>--screenshot path [--in-game] [--interior] [--new-game] [--info] [--options TAB] [--nl] [--hours N] [--driveway] [--curves] [--gate] [--barrier]</summary>
    async System.Threading.Tasks.Task Screenshot(string path, string[] args)
    {
        if (args.Contains("--nl")) Loc.SetLanguage("nl");
        var hi = Array.IndexOf(args, "--hours");
        var hours = hi >= 0 ? double.Parse(args[hi + 1], System.Globalization.CultureInfo.InvariantCulture) : 6;
        if (args.Contains("--in-game") || args.Contains("--interior"))
        {
            StartGame(Demo.Build(), false);
            for (var i = 0; i < hours * 20; i++) _sim.Tick(0.05);
            if (args.Contains("--interior"))
            {
                SetView(0, false);
                _cam.SetView(0.35f, 0.8f, 30, new Vector3(22, 0, 28));
                Inspect(new Vector2I(16, 22));
                if (args.Contains("--corridor")) { foreach (var hr in _state.Rooms) hr.Containment = Containment.ColdCorridor; _state.Touch(); }
                if (args.Contains("--overlay-power")) SetOverlay(OverlayMode.Power);
                if (args.Contains("--overlay-cooling")) SetOverlay(OverlayMode.Cooling);
            }
            else
            {
                _cam.SetView(0.62f, 0.78f, 78, new Vector3(30, 0, 34));
                Inspect(new Vector2I(44, 34));
                if (args.Contains("--driveway")) _cam.SetView(0.5f, 0.75f, 16, new Vector3(29.5f, 0, 64));
                if (args.Contains("--gate") || args.Contains("--barrier"))
                {
                    // run until a truck is at the gate (or barrier) and it is half open
                    var kind = args.Contains("--gate") ? ObjKind.SlidingGate : ObjKind.Barrier;
                    var g = _state.Objects.First(o => o.Kind == kind);
                    for (var i = 0; i < 4000; i++)
                    {
                        _sim.Tick(0.005);
                        if (_sim.GateOpen.TryGetValue(g.Id, out var v) && v > 0.45 && v < 0.75 && _sim.Agents.Any(a => a.Role == Role.Truck && a.Phase == 1)) break;
                    }
                    _cam.SetView(0.9f, 0.7f, 22, new Vector3(g.X + 1.5f, 0, g.Z + 1));
                }
                if (args.Contains("--curves"))
                {
                    // a winding footpath and an L-shaped road to show the rounded edges
                    for (var i = 0; i < 8; i++) _state.PaintSurface(46 + i, 52 + i / 2, 2, 2, Surface.Footpath, out _);
                    _state.PaintSurface(40, 56, 3, 8, Surface.Asphalt, out _);
                    _state.PaintSurface(40, 53, 12, 3, Surface.Asphalt, out _);
                    _cam.SetView(0.6f, 0.85f, 26, new Vector3(46, 0, 57));
                }
            }
            _hud.Notify(Loc.T("msg_building_done", Loc.T("bld_office")), Brand.Ok);
            _hud.Notify(Loc.T("msg_first_rack_live"), Brand.Ok);
            UpdateGoals(); UpdateProjects();
            _speed = 0;
        }
        if (args.Contains("--new-game")) _ui.AddChild(new NewGameDialog(_ => { }));
        if (args.Contains("--info")) OpenInfo();
        var oi = Array.IndexOf(args, "--options");
        if (oi >= 0) OpenOptions(oi + 1 < args.Length && int.TryParse(args[oi + 1], out var tab) ? tab : 0);
        if (args.Contains("--power")) { _playing = true; OpenPower(); }
        for (var i = 0; i < 50; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (args.Contains("--new-game"))
        {
            // show the creative switches
            var dlg = _ui.GetChildren().OfType<NewGameDialog>().FirstOrDefault();
            var btn = FindButton(dlg, "new_mode_creative");
            if (btn != null) { btn.ButtonPressed = true; btn.EmitSignal(BaseButton.SignalName.Toggled, true); }
            for (var i = 0; i < 10; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        GetViewport().GetTexture().GetImage().SavePng(path);
        GetTree().Quit();
    }

    static Button FindButton(Node n, string text)
    {
        if (n == null) return null;
        foreach (var c in n.GetChildren())
        {
            if (c is Button b && b.Text == text) return b;
            var f = FindButton(c, text);
            if (f != null) return f;
        }
        return null;
    }

    void RunTests()
    {
        var failures = Tests.TestRunner.RunAll();
        GetTree().Quit(failures == 0 ? 0 : 1);
    }
}
