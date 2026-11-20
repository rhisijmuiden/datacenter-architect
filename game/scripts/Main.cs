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
    FirstPerson _fp;
    bool _walking;
    (int floor, bool roofs, bool grid) _beforeWalk;
    Control _walkHint;
    Ambience _sound;
    UiSounds _uiSounds;
    Timelapse _lapse;
    PhotoMode _photo;
    int _speedBeforePhoto;
    bool _gridBeforePhoto;
    Control _ui;
    Hud _hud;
    MainMenu _menu;
    GameState _state;
    Simulation _sim;
    bool _playing;
    int _speed = 1;
    double _autosaveTimer, _goalTimer;
    TutorialCoach _coach;
    PlanBar _planBar;
    bool _planMode;
    HashSet<int> _planIds;
    BuildItem _item = BuildItem.Select;
    int _rot, _floors = 1;
    Facade _facade = Facade.Brick;
    Vector2I? _dragStart;
    // curve tool: first click sets the start, second the end, the mouse bends it, the third click builds
    Vector2? _curveA, _curveB;
    int _curveWidth = 4;
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
        _fp = new FirstPerson { Name = "Walker" }; AddChild(_fp);
        _fp.Exit = StopWalk; _fp.Moved = WalkView;
        _sound = new Ambience(); AddChild(_sound);
        _uiSounds = new UiSounds(); AddChild(_uiSounds);
        AddChild(new Thumbnails());
        _lapse = new Timelapse { Name = "Timelapse", State = () => _state, Playing = () => _playing && !_walking && _photo == null, World = _world };
        AddChild(_lapse);
        ApplyDisplay(); ApplyOther();
        var layer = new CanvasLayer(); AddChild(layer);
        _ui = new Control { Theme = Ui.BuildTheme(), MouseFilter = Control.MouseFilterEnum.Ignore };
        _ui.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(_ui);
        ShowTitle();
        if (AppSettings.Instance.CheckUpdatesOnStart && GameVersion.BuildChannel != "dev") _ = BackgroundUpdateCheck();
        var bench = Array.IndexOf(args, "--bench");
        if (bench >= 0 && bench + 1 < args.Length) { _ = RunBench(int.Parse(args[bench + 1]), args); return; }
        var shot = Array.IndexOf(args, "--screenshot");
        if (shot >= 0 && shot + 1 < args.Length) _ = Screenshot(args[shot + 1], args);
    }

    // ---------------- screens ----------------
    void ShowTitle()
    {
        StopWalk(); StopPhoto();
        _playing = false;
        _hud?.QueueFree(); _hud = null;
        _coach?.QueueFree(); _coach = null;
        _planBar?.QueueFree(); _planBar = null; _planMode = false;
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
        _landRev = s.LandRevision;
    }

    void StartNew(NewGameOptions o)
    {
        var sc = o.Scenario != null && Scenarios.ById.TryGetValue(o.Scenario, out var d) ? d : null;
        StartGame(sc != null ? Scenarios.Start(sc, o) : GameState.New(o), true);
    }

    void LoadSlot(string slot)
    {
        var s = SaveSystem.Load(slot);
        if (s == null) { _hud?.Notify(Loc.T("msg_load_failed")); return; }
        StartGame(s, false);
        _hud.Notify(Loc.T("msg_loaded", s.Options.Company), Brand.Ok);
    }

    void StartGame(GameState s, bool fresh)
    {
        StopWalk(); StopPhoto();
        Profile.Instance.Merge(s.Achieved, s.ScenarioId, s.ScenarioMedal, s.Date.ToString("yyyy-MM-dd"));
        _menu?.QueueFree(); _menu = null;
        _hud?.QueueFree();
        _coach?.QueueFree(); _coach = null;
        _planBar?.QueueFree(); _planBar = null; _planMode = false;
        // the title screen shows the demo site in the background: swap it for the game that starts or loads now
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
            CyclePriority = CyclePriority,
            RotateLeft = () => _cam.RotateStep(1), RotateRight = () => _cam.RotateStep(-1), Walk = StartWalk, Photo = StartPhoto,
            ToggleGrid = () => _world.GridVisible = !_world.GridVisible,
            ToggleRoofs = () => SetView(_world.ViewFloor, !_world.Roofs),
            FloorUp = () => SetView(_world.ViewFloor + 1, false),
            FloorDown = () => SetView(_world.ViewFloor - 1, _world.ViewFloor - 1 <= 0 && _world.Roofs),
            OpenInfo = OpenInfo, OpenOptions = OpenOptions, OpenMenu = OpenPause, ToggleLanguage = ToggleLanguage,
            OpenSection = ToggleSection,
            Undo = DoUndo, Redo = DoRedo, TogglePlan = TogglePlan,
            OpenPower = OpenPower, OpenNetwork = OpenNetwork, OpenCompany = OpenCompany, OpenStaff = OpenStaff, OpenReputation = OpenReputation, OpenGrowth = OpenGrowth, ToggleOverlay = m => SetOverlay(_world.Overlay.Mode == (OverlayMode)m ? OverlayMode.None : (OverlayMode)m),
        };
        _ui.AddChild(_hud);
        _ui.MoveChild(_hud, 0);
        _coach = new TutorialCoach { State = () => _state, Goals = () => _hud?.Goals ?? Array.Empty<(string, bool)>(), Hud = _hud, OpenSection = (sec, tab) => OpenSectionTab(sec, tab) };
        _ui.AddChild(_coach);
        _ui.MoveChild(_coach, 1);
        _planMode = false;
        _planBar = new PlanBar { State = () => _state, PlanMode = () => _planMode, Build = BuildPlan, Discard = DiscardPlan, Toggle = TogglePlan };
        _ui.AddChild(_planBar);
        _ui.MoveChild(_planBar, 2);
        _playing = true; _speed = 1; _autosaveTimer = 0; _item = BuildItem.Select; _bankruptShown = false;
        _inspectCell = null; SetOverlay(OverlayMode.None);
        _hud.UpdateStats(_state);
        _history.Clear(); RefreshUndo();
        UpdateGoals();
        if (fresh)
        {
            _hud.Notify(Loc.T(s.Options.AiCompetitors ? "msg_welcome_ai" : "msg_welcome", s.Options.Company));
            _hud.Notify(Loc.T("msg_start_hint"), Brand.Cooling);
            if (s.Scenario is ScenarioDef sd) _hud.Notify(Loc.T("msg_scenario_start", Loc.T("sc_" + sd.Id), Loc.T("sc_" + sd.Id + "_goal")), Brand.Amber);
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
        _item = item; _rot = 0; _dragStart = null; _curveA = _curveB = null; _world.ClearGhost();
        if (item.Mode == Mode.Curve) _curveWidth = CurvePath.DefaultWidth[(Surface)item.Arg];
        var inside = item.Mode is Mode.Room or Mode.Door or Mode.Row ||
            (item.Mode == Mode.Object && Catalog.Objects[(ObjKind)item.Arg].Where is Where.Room or Where.Hall or Where.Building);
        if (inside && _world.Roofs) SetView(_world.ViewFloor, false);
        if (item.Mode is Mode.Surface or Mode.Curve or Mode.Fence or Mode.Building && _world.ViewFloor > 0) SetView(0, _world.Roofs);
    }

    void CyclePriority(int id)
    {
        var b = _state.Buildings.FirstOrDefault(x => x.Id == id);
        if (b != null) b.Priority = b.Priority % 3 + 1;
        var r = _state.Rooms.FirstOrDefault(x => x.Id == id);
        if (r != null) r.Priority = r.Priority % 3 + 1;
        UpdateProjects();
    }

    // ---------------- management drawer (GUI v2) ----------------
    Modal _drawer;
    OverlayMode _overlayBeforeDrawer;
    bool _drawerSetOverlay;

    void OpenPower() => OpenPanel(PanelKind.Power, 0);
    void OpenNetwork() => OpenPanel(PanelKind.Network, 0);
    void OpenCompany(int tab) => OpenPanel(PanelKind.Company, tab);
    void OpenReputation(int tab) => OpenPanel(PanelKind.Reputation, tab);
    void OpenGrowth(int tab) => OpenPanel(PanelKind.Growth, tab);
    void OpenStaff(int tab) => OpenPanel(PanelKind.Staff, tab);

    /// <summary>Opens a screen in the drawer, in the section and tab that show it.</summary>
    void OpenPanel(PanelKind kind, int tab)
    {
        var (sec, t) = Sections.Find(kind, tab);
        // a screen can sit under two tabs of a section only once, so open the exact tab when it exists
        OpenSectionTab(sec, t, tab);
    }

    /// <summary>
    /// Opens tab <paramref name="tabIndex"/> of rail section <paramref name="section"/>. Pressing the rail button of the open section closes the drawer.
    /// </summary>
    void OpenSectionTab(int section, int tabIndex) => OpenSectionTab(section, tabIndex, null);

    void OpenSectionTab(int section, int tabIndex, int? screenTab)
    {
        if (!_playing || section < 0 || section >= Sections.All.Length) return;
        var e = Sections.All[section].Tabs[Math.Clamp(tabIndex, 0, Sections.All[section].Tabs.Length - 1)];
        var want = screenTab ?? e.Tab;
        if (_drawer != null && IsInstanceValid(_drawer) && !_drawer.IsQueuedForDeletion())
        {
            if (_drawer.DrawerSection == section && _drawer.Kind == e.Kind) { _drawer.SelectTab(want); ApplyDrawerOverlay(e.Kind); return; }
            CloseDrawer(keepOverlay: true);
        }
        Modal m = e.Kind switch
        {
            PanelKind.Overview => new OverviewPanel(() => _state, () => _hud?.Goals ?? Array.Empty<(string, bool)>(), () => _hud?.History ?? new List<HudMessage>(), OpenPanel, want),
            PanelKind.Company => new CompanyPanel(() => _state, Fail, want),
            PanelKind.Staff => new StaffPanel(() => _state, Fail, want),
            PanelKind.Reputation => new ReputationPanel(() => _state, Fail, want),
            PanelKind.Growth => new GrowthPanel(() => _state, Fail, want),
            PanelKind.Power => new PowerPanel(() => _state, Fail),
            PanelKind.Progress => new ProgressPanel(() => _state, StartPhoto, want),
            _ => new NetworkPanel(() => _state, Fail),
        };
        m.Kind = e.Kind;
        m.Dock(Hud.LeftEdge - 2, section, tabIndex, (s, t) => OpenSectionTab(s, t));
        var self = m;
        m.OnClosed = () =>
        {
            if (_drawer != self) return;
            _drawer = null;
            _hud?.SetActiveSection(-1);
            if (_drawerSetOverlay) { SetOverlay(_overlayBeforeDrawer); _drawerSetOverlay = false; }
        };
        _ui.AddChild(m);
        _drawer = m;
        _hud?.SetActiveSection(section);
        ApplyDrawerOverlay(e.Kind);
    }

    /// <summary>A rail button: opens its section, or closes the drawer when that section is already open.</summary>
    void ToggleSection(int section)
    {
        if (_drawer != null && IsInstanceValid(_drawer) && _drawer.DrawerSection == section) { CloseDrawer(); return; }
        OpenSectionTab(section, 0);
    }

    void CloseDrawer(bool keepOverlay = false)
    {
        if (_drawer == null) return;
        var d = _drawer;
        if (keepOverlay) { _drawer = null; d.QueueFree(); _hud?.SetActiveSection(-1); return; }
        d.Close();
        _drawer = null;
    }

    /// <summary>The power and network screens switch on their overlay, and put back the previous one when the drawer closes.</summary>
    void ApplyDrawerOverlay(PanelKind kind)
    {
        var want = kind switch { PanelKind.Power => OverlayMode.Power, PanelKind.Network => OverlayMode.Network, _ => (OverlayMode?)null };
        if (want == null)
        {
            if (_drawerSetOverlay) { SetOverlay(_overlayBeforeDrawer); _drawerSetOverlay = false; }
            return;
        }
        if (!_drawerSetOverlay) { _overlayBeforeDrawer = _world.Overlay.Mode; _drawerSetOverlay = true; }
        SetOverlay(want.Value);
    }

    /// <summary>Bought land: the plot grew, so the world and the people on it are rebuilt and the camera follows the shift.</summary>
    void CheckLand()
    {
        if (_state.LandRevision == _landRev) return;
        _landRev = _state.LandRevision;
        var d = _state.LastLandShift;
        var t = _cam.TargetPoint;
        _sim = new Simulation(_state);
        _world.Build(_state, _sim);
        _cam.Bounds = new Rect2(-30, -30, _state.PlotSize + 60, _state.PlotSize + 60);
        _cam.SetView(_cam.Yaw, _cam.Pitch, _cam.Distance, new Vector3(t.X + d, t.Y, t.Z + d));
    }
    int _landRev;


    bool _bankruptShown;

    /// <summary>Game over: the bank takes the company. Load a savegame or go back to the main menu.</summary>
    void ShowBankrupt()
    {
        if (_bankruptShown) return;
        _bankruptShown = true;
        _speed = 0; _hud.SetSpeed(0);
        var m = new Modal("bankrupt_title", 560);
        m.Body.AddChild(Ui.Text("bankrupt_text", 15, Brand.Ink));
        var h = new HBoxContainer(); h.AddThemeConstantOverride("separation", 8);
        h.AddChild(Ui.Secondary("menu_load", () => { m.Close(); _ui.AddChild(new SaveLoadDialog(false, () => _state, LoadSlot)); }));
        h.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        h.AddChild(Ui.Primary("bankrupt_menu", () => { m.Close(); ShowTitle(); }));
        m.Body.AddChild(h);
        _ui.AddChild(m);
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
        _swSim.Restart();
        if (hours > 0)
        {
            if (_playing) _state.Advance(hours);
            _sim.Tick(hours);
        }
        _swSim.Stop();
        _swWorld.Restart();
        _world.WeatherCenter = _walking ? _fp.Feet : _cam.TargetPoint;
        _world.WeatherRadius = _walking ? 24 : _cam.Distance * 0.75f;
        _world.Sync(delta);
        _swWorld.Stop();
        _benchSample?.Add(delta, Bench.Ms(_swSim), Bench.Ms(_swWorld));
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
            ProgressNote(key, arg);
            var text = arg != null && arg.Contains('¦') ? Loc.T(key, arg.Split('¦').Select(ArgText).ToArray<object>()) : Loc.T(key, ArgText(arg));
            var accent = key is "msg_signed" or "msg_tender_won" or "msg_tender_delivered" or "msg_hired" or "msg_renewed" or "msg_trained" or "msg_staff_hired" or "msg_staff_trained"
                    or "msg_repaired" or "msg_replaced" or "msg_ev_press_good" or "msg_ev_fire_false" or "msg_ev_outage_gen" or "msg_parts_arrived" ? Brand.Ok
                : key is "msg_bankrupt" or "msg_in_red" or "msg_tender_failed" or "msg_customer_left" or "msg_tender_lost" or "msg_fault" or "msg_ev_outage_dark" or "msg_ev_cable_cut"
                    or "msg_ev_fire_gas" or "msg_staff_quit" or "msg_ev_bankrupt" or "msg_ev_press_bad" ? Brand.Critical : key.Contains("done") || key.Contains("live") || key.Contains("ready") || key.Contains("cool") ? Brand.Ok : key.Contains("need") || key.Contains("unreach") || key.Contains("no_road") || key.Contains("hot") || key.Contains("offline") || key.Contains("congested") ? Brand.Critical : Brand.Amber;
            _hud.Notify(text, accent);
        }
        _sim.Messages.Clear(); _state.Notes.Clear();
        if (_state.Bankrupt) ShowBankrupt();
        CheckLand();
        _hud.UpdateStats(_state);
        _goalTimer -= delta;
        if (_goalTimer <= 0) { _goalTimer = 1; _hud.SetWeather(_world.Now, AppSettings.Instance.Weather, _state); UpdateGoals(); UpdateProjects(); RefreshInspect(); _coach?.Refresh(); _planBar?.Refresh(); }
        _autosaveTimer += delta;
        if (_autosaveTimer > AppSettings.Instance.AutosaveMinutes * 60) { _autosaveTimer = 0; Autosave(); _hud.Notify(Loc.T("msg_autosaved"), Brand.Ok); }
    }

    /// <summary>Server noise grows as the camera zooms in on live racks that are in view.</summary>
    void UpdateSound()
    {
        var t = _walking ? _fp.Feet : _cam.TargetPoint;
        var reach = _walking ? 9 : 14;
        var floor = _walking ? _fp.Floor : _world.Roofs ? -1 : _world.ViewFloor;
        var near = _state.Objects.Count(o => o.Kind == ObjKind.Rack && o.Status == ObjStatus.Live && Math.Abs(o.X - t.X) < reach && Math.Abs(o.Z - t.Z) < reach && (floor < 0 || o.Floor == floor));
        var zoom = _walking ? 1 : Mathf.Clamp((60 - _cam.Distance) / 45f, 0, 1);
        var open = _walking ? (_world.Indoor ? 1f : 0.3f) : _world.Roofs ? 0.35f : 1f;
        _sound.Level = _playing ? Mathf.Clamp(near / (_walking ? 24f : 40f), 0, 1) * zoom * open : 0;
        // rain is loud outside, a soft drum on the roof inside; thunder follows a lightning flash
        var w = _world.Now;
        var wet = WeatherFxOn && w.Kind is WeatherKind.Rain or WeatherKind.Storm ? (float)(0.45 + 0.55 * w.Intensity) : 0f;
        _sound.Rain = (_playing || _walking ? wet : wet * 0.4f) * (_world.Indoor ? 0.25f : _walking ? 0.9f : Mathf.Clamp(1.1f - _cam.Distance / 140f, 0.35f, 1f));
        if (_world.Flashed) _sound.Thunder(_world.Indoor ? 0.5f : 1f);
    }

    static bool WeatherFxOn => AppSettings.Instance.Weather;

    // ---------------- first-person walking ----------------
    /// <summary>Walk around in first person (look and listen only). F or the walker button starts it; Esc or F ends it.</summary>
    void StartWalk()
    {
        if (!_playing || _walking || _state == null) return;
        _walking = true;
        _state.Unlock(Achievements.Walk);
        _beforeWalk = (_world.ViewFloor, _world.Roofs, _world.GridVisible);
        if (_item.Mode != Mode.Select) _hud.Pick(BuildItem.Select);
        _world.ClearGhost();
        _world.GridVisible = false;
        _inspectCell = null;
        _fp.Bounds = _cam.Bounds;
        var floor = _world.Roofs ? 0 : _world.ViewFloor;
        _fp.Begin(_state, _sim.Nav, _cam.TargetPoint, floor, _cam.Yaw);
        _cam.InputEnabled = false;
        _hud.Visible = false;
        _walkHint = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        _walkHint.AnchorLeft = _walkHint.AnchorRight = 0.5f; _walkHint.AnchorTop = _walkHint.AnchorBottom = 1;
        _walkHint.OffsetTop = _walkHint.OffsetBottom = -24;
        _walkHint.GrowHorizontal = Control.GrowDirection.Both; _walkHint.GrowVertical = Control.GrowDirection.Begin;
        var hint = Ui.Text(Loc.T("walk_hint"), 15); hint.AutoTranslateMode = Control.AutoTranslateModeEnum.Disabled; hint.AutowrapMode = TextServer.AutowrapMode.Off;
        _walkHint.AddChild(Ui.Pad(hint, 14, 6, 14, 6));
        _ui.AddChild(_walkHint);
        var dot = new ColorRect { Color = new Color(1, 1, 1, 0.55f), CustomMinimumSize = new Vector2(4, 4), MouseFilter = Control.MouseFilterEnum.Ignore, Name = "Dot" };
        dot.AnchorLeft = dot.AnchorRight = dot.AnchorTop = dot.AnchorBottom = 0.5f;
        dot.OffsetLeft = dot.OffsetTop = -2; dot.OffsetRight = dot.OffsetBottom = 2;
        _walkHint.SetMeta("dot", dot);
        _ui.AddChild(dot);
        WalkView();
    }

    /// <summary>Inside a building the walker sees full walls and a ceiling on their floor; outside the roofs are on.</summary>
    void WalkView()
    {
        if (!_walking) return;
        var inside = _fp.Inside != null || _fp.Floor > 0;
        _world.Indoor = inside;
        _world.FullWalls = inside;
        if (inside)
        {
            if (_world.Roofs) _world.Roofs = false;
            if (_world.ViewFloor != _fp.Floor) _world.ViewFloor = _fp.Floor;
        }
        else
        {
            if (!_world.Roofs) _world.Roofs = true;
            if (_world.ViewFloor != 0) _world.ViewFloor = 0;
        }
    }

    // ---------------- step 11: photo mode, achievements and medals ----------------
    /// <summary>Photo mode: hides the interface, stops the clock and shows the photo bar.</summary>
    void StartPhoto()
    {
        if (!_playing || _photo != null || _state == null) return;
        StopWalk();
        CloseDrawer();
        if (_item.Mode != Mode.Select) _hud.Pick(BuildItem.Select);
        _world.ClearGhost(); _inspectCell = null;
        _speedBeforePhoto = _speed; _speed = 0; _hud.SetSpeed(0);
        _gridBeforePhoto = _world.GridVisible; _world.GridVisible = false;
        _hud.Visible = false;
        var env = _world.GetChildren().OfType<WorldEnvironment>().FirstOrDefault()?.Environment;
        _photo = new PhotoMode(_cam, () => _state, env)
        {
            Exit = StopPhoto,
            Saved = _ => _state?.Unlock(Achievements.Photo),
        };
        _ui.AddChild(_photo);
    }

    void StopPhoto()
    {
        if (_photo == null) return;
        _photo.Restore();
        _photo.QueueFree(); _photo = null;
        if (_hud != null) { _hud.Visible = true; _speed = _speedBeforePhoto; _hud.SetSpeed(_speed); }
        _world.GridVisible = _gridBeforePhoto;
    }

    /// <summary>Achievements and medals also go into the profile that spans all savegames.</summary>
    void ProgressNote(string key, string arg)
    {
        if (_state == null || arg == null) return;
        if (key == "msg_achievement" && arg.StartsWith("ach_")) Profile.Instance.Earn(arg[4..], _state.Date.ToString("yyyy-MM-dd"));
        if (key is "msg_medal" or "msg_scenario_won" or "msg_scenario_over" && _state.ScenarioId != null) Profile.Instance.Medal(_state.ScenarioId, _state.ScenarioMedal);
    }

    void StopWalk()
    {
        if (!_walking) return;
        _walking = false;
        _fp.End();
        _world.Indoor = false;
        _world.FullWalls = false;
        var p = _fp.Feet;
        _cam.SetView(_fp.Yaw, 0.85f, 24, new Vector3(p.X, 0, p.Z));
        _cam.InputEnabled = true;
        var inside = _fp.Floor > 0 || _fp.Inside != null;
        SetView(inside ? _fp.Floor : _beforeWalk.floor, !inside && _beforeWalk.roofs);
        _world.GridVisible = _beforeWalk.grid;
        _hud.Visible = true;
        if (_walkHint != null)
        {
            if (_walkHint.GetMeta("dot").AsGodotObject() is Node d) d.QueueFree();
            _walkHint.QueueFree(); _walkHint = null;
        }
    }

    /// <summary>Message arguments are keys, plain names, or "@key|arg" for translated customer names.</summary>
    public static string ArgText(string arg)
    {
        if (arg == null) return "";
        if (arg.StartsWith('@')) { var p = arg[1..].Split('|'); return Loc.T(p[0], p.Length > 1 ? p[1] : ""); }
        return Loc.T(arg);
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
            ("goal_office", s.Offices > 0),
            ("goal_ceo", s.Has(Exec.Ceo)),
            ("goal_sales", s.Has(Exec.Sales)),
            ("goal_contract", s.Deals.Any(d => d.State == DealState.Active && !d.Legacy)),
            ("goal_switchgear", s.Objects.Any(o => o.Kind == ObjKind.Switchgear && o.Status == ObjStatus.Live)),
            ("goal_ups", s.Objects.Any(o => o.Kind == ObjKind.Ups && o.Status == ObjStatus.Live) && s.Objects.Any(o => o.Kind == ObjKind.Battery && o.Status == ObjStatus.Live)),
            ("goal_pdu", s.Objects.Any(o => o.Kind == ObjKind.Pdu && o.Status == ObjStatus.Live) || s.Rooms.Any(r => r.Busbar)),
            ("goal_cooling", s.Objects.Any(o => o.Def.RejectKw > 0)),
            ("goal_generators", s.Objects.Count(o => o.Kind == ObjKind.Generator) >= 2),
            ("goal_fiber", s.Objects.Any(o => o.Kind == ObjKind.FiberPit) && Sim.Network.MmrReady(s)),
            ("goal_carrier", s.Net.Carriers.Count(x => x.Up && !x.Def.Ix) >= 2),
            ("goal_network", s.Net.OnlineRacks > 0),
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
        if (!_playing || _state == null || _walking || _photo != null) return;
        if (e.IsActionPressed("ui_cancel"))
        {
            if (_curveA != null) { _curveA = _curveB = null; UpdateGhost(); }
            else if (_dragStart != null) { _dragStart = null; UpdateGhost(); }
            else if (_item.Mode != Mode.Select) _hud.Pick(BuildItem.Select);
            else OpenPause();
            GetViewport().SetInputAsHandled(); return;
        }
        if (e is InputEventKey { Pressed: true, Echo: false } k)
        {
            if (k.Keycode == Key.R) { _rot = (_rot + 1) % 4; UpdateGhost(); }
            if (k.Keycode == Key.F) { StartWalk(); GetViewport().SetInputAsHandled(); return; }
            if (k.Keycode == Key.F9) { StartPhoto(); GetViewport().SetInputAsHandled(); return; }
            if (_item.Mode == Mode.Curve && k.Keycode is Key.Bracketleft or Key.Bracketright)
            {
                _curveWidth = Math.Clamp(_curveWidth + (k.Keycode == Key.Bracketright ? 1 : -1), CurvePath.MinWidth, CurvePath.MaxWidth);
                UpdateGhost();
            }
            if (k.Keycode == Key.Space) { var s = _speed == 0 ? 1 : 0; _speed = s; _hud.SetSpeed(s); }
            if (k.Keycode is Key.Key1 or Key.Key2 or Key.Key3) { _speed = (int)(k.Keycode - Key.Key0); _hud.SetSpeed(_speed); }
            if (k.Keycode == Key.S && k.CtrlPressed) { Autosave(); _hud.Notify(Loc.T("msg_autosaved"), Brand.Ok); }
            if (k.CtrlPressed && (k.Keycode == Key.Y || (k.Keycode == Key.Z && k.ShiftPressed))) { DoRedo(); GetViewport().SetInputAsHandled(); return; }
            if (k.CtrlPressed && k.Keycode == Key.Z) { DoUndo(); GetViewport().SetInputAsHandled(); return; }
            var sec = Sections.ForKey(k.Keycode);
            if (sec >= 0) { ToggleSection(sec); GetViewport().SetInputAsHandled(); return; }
            if (k.Keycode == Key.Pageup) SetView(_world.ViewFloor + 1, false);
            if (k.Keycode == Key.Pagedown) SetView(_world.ViewFloor - 1, _world.Roofs);
            if (k.Keycode == Key.T) SetView(_world.ViewFloor, !_world.Roofs);
            if (k.Keycode == Key.B && !k.CtrlPressed) TogglePlan();
            if (k.Keycode == Key.P) SetOverlay(_world.Overlay.Mode == OverlayMode.Power ? OverlayMode.None : OverlayMode.Power);
            if (k.Keycode == Key.H) SetOverlay(_world.Overlay.Mode == OverlayMode.Cooling ? OverlayMode.None : OverlayMode.Cooling);
            if (k.Keycode == Key.N) SetOverlay(_world.Overlay.Mode == OverlayMode.Network ? OverlayMode.None : OverlayMode.Network);
        }
        if (e is InputEventMouseMotion mm)
        {
            var p = PointAt(mm.Position);
            if (p == null) return;
            var cell = RoofItem ? RoofCell(mm.Position) ?? CellOf(p.Value) : CellOf(p.Value);
            var doorMoved = _item.Mode == Mode.Door && DoorAt(p.Value) is var a && DoorAt(_hoverPoint) is var b && (a.X != b.X || a.Z != b.Z || a.Dir != b.Dir);
            _hoverPoint = p.Value;
            if (_item.Mode == Mode.Curve) { _hoverCell = cell; UpdateGhost(); }
            else if (cell != _hoverCell || doorMoved) { _hoverCell = cell; UpdateGhost(); }
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
                    case Mode.Curve: CurveClick(Snap(p.Value)); break;
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
        // in plan mode nothing is paid yet: the ghost does not turn red for money
        _state.Planning = _planMode;
        try { GhostNow(); } finally { _state.Planning = false; }
    }

    void GhostNow()
    {
        var r = RectOf(_dragStart ?? _hoverCell, _hoverCell);
        var y = PickHeight;
        switch (_item.Mode)
        {
            case Mode.Select: _world.ClearGhost(); break;
            case Mode.Erase:
                var gone = _dragStart == null ? 0 : _state.Objects.Count(o => o.Floor == _world.ViewFloor && GameState.Cells(o).Any(c => r.HasPoint(new Vector2I(c.x, c.z))));
                _world.ShowGhostRect(r, Brand.Critical, y, _dragStart == null ? null : Dim(r) + (gone > 0 ? " · " + Loc.T("erase_objects", gone) : ""));
                break;
            case Mode.Curve:
                var mouse = Snap(_hoverPoint);
                if (_curveA == null)
                {
                    _world.ShowGhostCurve(new CurvePath { Ax = mouse.X, Az = mouse.Y, Bx = mouse.X + 0.01, Bz = mouse.Y, Cx = mouse.X, Cz = mouse.Y, Width = _curveWidth }, true, Loc.T("curve_width", _curveWidth), null);
                    break;
                }
                var cv = CurveAt(mouse);
                var ccost = _state.CurveCost(cv);
                _world.ShowGhostCurve(cv, _state.Money >= ccost || _state.Creative.UnlimitedMoney, $"{Loc.T("curve_width", _curveWidth)} · {cv.Length:0} m · {Ui.Money(ccost)}", _curveB == null ? null : mouse);
                break;
            case Mode.Surface:
                var sc = Catalog.Surfaces[(Surface)_item.Arg].Cost * r.Size.X * r.Size.Y;
                var bays = "";
                if ((Surface)_item.Arg == Surface.Parking && _dragStart != null)
                {
                    var spots = ParkingLayout.Bays(ParkingLayout.Lines(_state.PlotSize, (x, z) => x >= r.Position.X && x < r.End.X && z >= r.Position.Y && z < r.End.Y));
                    bays = " · " + Loc.T("parking_bays", spots);
                }
                _world.ShowGhostRect(r, Brand.Ok, 0, _dragStart == null ? null : $"{Dim(r)} · {Ui.Money(sc)}{bays}");
                if ((Surface)_item.Arg == Surface.Parking) _world.ShowGhostParking(r);
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
                // rooms are drawn on the building's floor slab, not on the ground under it
                _world.ShowGhostRect(r, rwhy == null ? new Color(Catalog.Rooms[rt].Color) : Brand.Critical, WorldView.Base(_world.ViewFloor), rtext, outline: true);
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
                var cells = GameState.RowBlock(start.X, start.Y, _hoverCell.X, _hoverCell.Y, kindR, _rot, _state.MaxRowLength);
                var objs = cells.Select(c => new PlacedObject { Kind = kindR, X = c.x, Z = c.z, Rot = c.rot, Floor = _world.ViewFloor }).Select(x => (x, _state.CanPlace(x) == null)).ToList();
                var ok = objs.Count(x => x.Item2);
                var rows = cells.Select(c => Math.Abs(_hoverCell.X - start.X) >= Math.Abs(_hoverCell.Y - start.Y) ? c.z : c.x).Distinct().Count();
                var rowText = rows > 1 ? " · " + Loc.T("row_rows", rows) : "";
                var kw = ok * Catalog.Objects[kindR].PowerKw;
                var powerText = kw > 0 ? $" · {kw:0} kW" : "";
                _world.ShowGhostObjects(objs, _dragStart == null ? null : $"{ok} × {Loc.T(Catalog.Objects[kindR].NameKey)}{rowText} · {Dim(RectOf(start, _hoverCell))}{powerText} · {Ui.Money(ok * Catalog.Objects[kindR].Cost)}");
                break;
        }
    }

    // ---------------- undo / redo ----------------
    readonly UndoHistory _history = new();

    void Record(UndoHistory.Snapshot before, string label) { if (_history.Commit(_state, before, label)) RefreshUndo(); }
    void RefreshUndo() => _hud?.SetUndo(_history.NextUndo, _history.NextRedo);

    void DoUndo()
    {
        if (_state == null || !_playing) return;
        var label = _history.Undo(_state);
        if (label == null) { _hud.Notify(Loc.T("msg_nothing_to_undo"), Brand.InkDim); return; }
        _hud.Notify(Loc.T("msg_undone", Loc.T(label)), Brand.Ok);
        _hud.UpdateStats(_state); UpdateGhost(); RefreshUndo();
    }

    void DoRedo()
    {
        if (_state == null || !_playing) return;
        var label = _history.Redo(_state);
        if (label == null) { _hud.Notify(Loc.T("msg_nothing_to_redo"), Brand.InkDim); return; }
        if (label == "msg_no_money") { Fail(label); return; }
        _hud.Notify(Loc.T("msg_redone", Loc.T(label)), Brand.Ok);
        _hud.UpdateStats(_state); UpdateGhost(); RefreshUndo();
    }

    void Fail(string why) { if (why != null) _hud.Notify(Loc.T(why), Brand.Critical); }

    /// <summary>Curve points snap to half metres.</summary>
    static Vector2 Snap(Vector2 p) => new(Mathf.Round(p.X * 2) / 2, Mathf.Round(p.Y * 2) / 2);

    /// <summary>The curve as it would be built with the mouse at <paramref name="mouse"/>.</summary>
    CurvePath CurveAt(Vector2 mouse)
    {
        var a = _curveA ?? mouse;
        var b = _curveB ?? mouse;
        // before the end is set the line is straight; after it, the mouse is the bend
        var c = _curveB == null ? (a + b) / 2 : mouse;
        return new CurvePath { Surface = (Surface)_item.Arg, Ax = a.X, Az = a.Y, Bx = b.X, Bz = b.Y, Cx = c.X, Cz = c.Y, Width = _curveWidth };
    }

    // ---------------- plan mode (step 11) ----------------
    /// <summary>Before a build action in plan mode: remember what exists and let the action cost nothing.</summary>
    void PlanBegin(bool plannable)
    {
        if (!_planMode || !plannable) return;
        _planIds = _state.LayoutIds();
        _state.Planning = true;
    }

    /// <summary>After the action: whatever it added becomes part of the blueprint.</summary>
    void PlanEnd()
    {
        if (_planIds == null) return;
        _state.Planning = false;
        _state.MarkPlanned(_planIds);
        _planIds = null;
        _planBar?.Refresh();
    }

    void TogglePlan()
    {
        if (!_playing) return;
        _planMode = !_planMode;
        _hud?.SetPlan(_planMode);
        _hud?.Notify(Loc.T(_planMode ? "msg_plan_on" : "msg_plan_off"), Brand.Cooling);
        _planBar?.Refresh();
        UpdateGhost();
    }

    void BuildPlan()
    {
        var count = _state.PlanCount; var cost = _state.PlanCost;
        var why = _state.BuildPlan();
        if (why != null) { Fail(why); return; }
        // the history cannot take back half a plan that is now paid for
        _history.Clear(); RefreshUndo();
        _hud.Notify(Loc.T("msg_plan_built", count, Ui.Money(cost)), Brand.Ok);
        if (_planMode) TogglePlan();
        _planBar?.Refresh();
        _hud.UpdateStats(_state);
    }

    void DiscardPlan()
    {
        var n = _state.DiscardPlan();
        _history.Clear(); RefreshUndo();
        if (n > 0) _hud.Notify(Loc.T("msg_plan_discarded", n), Brand.Amber);
        _planBar?.Refresh();
    }

    void CurveClick(Vector2 p)
    {
        if (_curveA == null) { _curveA = p; return; }
        if (_curveB == null) { if ((p - _curveA.Value).Length() >= 1) _curveB = p; return; }
        var before = UndoHistory.Take(_state);
        var why = _state.AddCurve(CurveAt(p));
        Record(before, _item.NameKey);
        if (why is "msg_no_money") Fail(why);
        _curveA = _curveB = null;
        _hud.UpdateStats(_state);
    }

    void ApplyDrag(Vector2I a, Vector2I b, Rect2I r)
    {
        var before = UndoHistory.Take(_state);
        PlanBegin(_item.Mode is Mode.Building or Mode.Room or Mode.Row);
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
                foreach (var (x, z, rr) in GameState.RowBlock(a.X, a.Y, b.X, b.Y, k, _rot, _state.MaxRowLength))
                {
                    var o = new PlacedObject { Kind = k, X = x, Z = z, Rot = rr, Floor = _world.ViewFloor };
                    last = _state.CanPlace(o);
                    if (last == null && _state.AddObject(k, x, z, rr, _world.ViewFloor) != null) placed++;
                }
                if (placed == 0) Fail(last);
                else _hud.Notify(Loc.T(_planMode ? "msg_racks_planned" : "msg_racks_ordered", placed), _planMode ? Brand.Cooling : Brand.Ok);
                break;
        }
        PlanEnd();
        Record(before, _item.NameKey);
        _hud.UpdateStats(_state);
    }

    void PlaceObject(Vector2I c)
    {
        var k = (ObjKind)_item.Arg;
        var floor = _state.PlaceFloor(k, c.X, c.Y, _world.ViewFloor);
        var o = new PlacedObject { Kind = k, X = c.X, Z = c.Y, Rot = _rot, Floor = floor };
        _state.Planning = _planMode;
        var why = _state.CanPlace(o);
        _state.Planning = false;
        if (why != null) { Fail(why); return; }
        var before = UndoHistory.Take(_state);
        PlanBegin(true);
        _state.AddObject(k, c.X, c.Y, _rot, floor);
        PlanEnd();
        Record(before, _item.NameKey);
        _hud.UpdateStats(_state);
    }

    void PlaceDoor(Door d)
    {
        _state.Planning = _planMode;
        var why = _state.CanDoor(d);
        _state.Planning = false;
        if (why != null) { Fail(why); return; }
        var before = UndoHistory.Take(_state);
        PlanBegin(true);
        _state.AddDoor(d.Type, d.X, d.Z, d.Dir, d.Floor);
        PlanEnd();
        Record(before, Catalog.Doors[d.Type].NameKey);
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
            if (o.Broken)
            {
                lines.Add("⚠ " + Loc.T(o.Noticed ? "inspect_broken" : "inspect_broken_unnoticed"));
                var target = o;
                if (o.RepairBy != -1) actions.Add((Loc.T("st_call_contractor", Ui.Money(Staffing.ContractorCost(o.Kind)), Staffing.ContractorDelay), () => Act(_state.CallContractor(target)), true));
                actions.Add((Loc.T("st_tab_incidents"), () => OpenStaff(StaffPanel.TabIncidents), true));
            }
            else if (Staffing.Breaks(o.Kind) && o.Status == ObjStatus.Live && !_state.Creative.NoBreakdowns)
            {
                lines.Add(Loc.T("inspect_wear", Math.Round(o.Wear)));
                if (_state.Aged(o))
                {
                    var target = o;
                    lines.Add("⚠ " + Loc.T("inspect_aged"));
                    actions.Add((Loc.T("st_replace", Ui.Money(_state.ReplaceCost(o))), () => Act(_state.Replace(target)), _state.CanAfford(_state.ReplaceCost(o))));
                }
            }
            if (o.Kind == ObjKind.Rack && o.Status == ObjStatus.Live)
            {
                var hall = _state.RoomAt(o.X, o.Z, o.Floor);
                lines.Add(!infra.Down.Contains(o.Id) ? Loc.T("inspect_running") : hall?.Hot == true ? "⚠ " + Loc.T("inspect_down_heat") : "⚠ " + Loc.T("inspect_down_power"));
                if (!infra.Down.Contains(o.Id)) lines.Add(_state.Net.Offline.Contains(o.Id) ? "⚠ " + Loc.T("inspect_offline") : Loc.T("inspect_online"));
                var owner = _state.RackOwners().TryGetValue(o.Id, out var dealId) ? _state.DealById(dealId) : null;
                lines.Add(owner != null ? Loc.T("inspect_rented", CompanyPanel.DealName(owner)) : hall?.Zone == HallZone.PerU ? Loc.T("inspect_peru") : Loc.T("inspect_for_sale"));
            }
            if (d.Ports > 0) lines.Add(Loc.T("inspect_ports", d.Ports) + (d.Gbps > 0 ? $" · {d.Gbps:0} Gbit/s" : ""));
            if (o.Kind == ObjKind.FiberPit)
            {
                var pi = _state.Net.Pits.FirstOrDefault(x => x.Pit == o);
                if (pi != null)
                {
                    lines.Add(Loc.T("inspect_pit", Catalog.PitName(pi.Street), string.Join(", ", Catalog.Carriers.Where(x => x.Pit == pi.Street).Select(x => x.Name))));
                    lines.Add(Loc.T("inspect_subducts", pi.Used, pi.Subducts));
                }
                if (o.Level < 3)
                {
                    var target = o;
                    actions.Add((Loc.T("action_duct", Catalog.Subducts(o.Level + 1), Ui.Money(Catalog.UpgradeCost(d, o.Level))), () => Act(_state.UpgradeObject(target)), _state.CanUpgrade(o) == null));
                }
            }
            if (o.Kind is ObjKind.FiberPit or ObjKind.Odf or ObjKind.CoreRouter or ObjKind.Spine or ObjKind.NocWall)
                actions.Add((Loc.T("net_overview"), OpenNetwork, true));
            if (d.PowerKw > 0) lines.Add($"{d.PowerKw:0.#} kW");
            if (d.CoolingKw > 0) lines.Add(Loc.T("inspect_cooling", Math.Round(d.CoolingKw * Catalog.LevelFactor(o.Level))));
            if (d.RejectKw > 0) lines.Add(Loc.T("inspect_reject", Math.Round(d.RejectKw * Catalog.LevelFactor(o.Level))));
            if (d.CapacityKw > 0) lines.Add(Loc.T("inspect_capacity", d.CapacityKw));
            if (d.StorageKwh > 0) lines.Add(Loc.T("inspect_storage", d.StorageKwh));
            if (d.GenKw > 0) lines.Add(Loc.T("inspect_generator", d.GenKw));
            if (d.FuelLitres > 0) lines.Add(Loc.T("inspect_fuel", d.FuelLitres));
            if (o.Floor == GameState.RoofFloor) lines.Add(Loc.T("inspect_on_roof"));
            if (d.MaxLevel > 1 && o.Kind != ObjKind.FiberPit)
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
                lines.Add($"{Loc.T(cont.NameKey)} · PUE {Math.Max(1.08, cont.Pue - _state.PueSaving).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}");
                if (r.Containment == Containment.HotCold && !_state.Done("cool_corridor")) lines.Add(Loc.T("inspect_corridor_research"));
                if (hi != null)
                {
                    lines.Add(Loc.T("inspect_hall_power", Kw(hi.ItKw), Kw(hi.DistributionKw), hi.ItKw <= 0 ? "-" : hi.AB ? "A+B" : Loc.T("hall_ab_no")));
                    lines.Add(Loc.T("inspect_hall_cooling", Kw(hi.CrahKw), $"{r.Temp:0}"));
                    if (hi.RunningRacks < hi.LiveRacks) lines.Add("⚠ " + Loc.T(r.Hot ? "inspect_hall_hot" : "inspect_hall_short", hi.LiveRacks - hi.RunningRacks));
                }
                if (r.Upgrade != 0) lines.Add(Loc.T("inspect_upgrading", Math.Ceiling(r.UpgradeLeft)));
                var hn = _state.Net.Halls.FirstOrDefault(h => h.Room == r);
                if (hn != null && hn.Racks > 0) lines.Add((hn.Online < hn.Racks ? "⚠ " : "") + Loc.T("inspect_hall_net", hn.Online, hn.Racks, hn.Spines));
                var hall = r;
                if (r.Containment < Containment.ColdCorridor)
                {
                    var next = Catalog.Containments[r.Containment + 1];
                    actions.Add((Loc.T("action_containment", Loc.T(next.NameKey), Ui.Money(GameState.HallUpgradeCost(r))), () => Act(_state.UpgradeHall(hall, false)), _state.CanUpgradeHall(r, false) == null));
                }
                if (!r.Busbar) actions.Add((Loc.T("action_busbar", Ui.Money(Catalog.BusbarCost)), () => Act(_state.UpgradeHall(hall, true)), _state.CanUpgradeHall(r, true) == null));
                lines.Add(Loc.T("inspect_zone", Loc.T("zone_" + r.Zone.ToString().ToLowerInvariant())));
                var nextZone = (HallZone)(((int)r.Zone + 1) % Enum.GetValues<HallZone>().Length);
                actions.Add((Loc.T("action_zone", Loc.T("zone_" + nextZone.ToString().ToLowerInvariant())), () => { _state.SetZone(hall, nextZone); Act(null); }, true));
            }
            if (r.Type is RoomType.Executive or RoomType.Office)
            {
                var desks = _state.Objects.Count(x => x.Kind == ObjKind.Desk && x.Floor == r.Floor && r.Contains(x.X, x.Z));
                var chairs = _state.Objects.Count(x => x.Kind == ObjKind.Chair && x.Floor == r.Floor && r.Contains(x.X, x.Z));
                lines.Add(r.Type == RoomType.Executive
                    ? (desks > 0 && chairs > 0 && r.Stage == Stage.Done ? Loc.T("inspect_office_ready") : "⚠ " + Loc.T("inspect_office_needs"))
                    : Loc.T("inspect_workplaces", desks));
                actions.Add((Loc.T("co_tab_org"), () => OpenCompany(CompanyPanel.TabOrg), true));
            }
            if (r.Type is RoomType.Lab)
            {
                lines.Add(Loc.T("inspect_lab", _state.LabBenches, _state.CountOf(StaffRole.Researcher)));
                actions.Add((Loc.T("rp_tab_research"), () => OpenReputation(ReputationPanel.TabResearch), true));
            }
            if (r.Type is RoomType.Security or RoomType.Reception)
                actions.Add((Loc.T("rp_tab_security"), () => OpenReputation(ReputationPanel.TabSecurity), true));
            if (r.Type is RoomType.MeetMe or RoomType.Noc)
            {
                var net = _state.Net;
                lines.Add(r.Type == RoomType.MeetMe ? Loc.T("inspect_mmr", net.OdfPorts, net.OdfUsed, net.Cores, _state.CrossConnects) : net.Noc ? Loc.T("net_noc_on") : Loc.T("inspect_noc_needs_wall"));
                actions.Add((Loc.T("net_overview"), OpenNetwork, true));
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
    readonly System.Diagnostics.Stopwatch _swSim = new(), _swWorld = new();
    Bench.Sample _benchSample;

    /// <summary>--bench N [--bench-seconds S] [--bench-out file.json]: see <see cref="Bench"/>.</summary>
    async System.Threading.Tasks.Task RunBench(int racks, string[] args)
    {
        var si = Array.IndexOf(args, "--bench-seconds");
        var seconds = si >= 0 ? double.Parse(args[si + 1], System.Globalization.CultureInfo.InvariantCulture) : 8;
        var oi = Array.IndexOf(args, "--bench-out");
        Engine.MaxFps = 0;
        DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var s = Bench.Build(racks);
        var buildMs = Bench.Ms(sw);
        StartGame(s, false);
        _speed = 1;
        SetView(0, false);
        for (var i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        // time a full rebuild of the 3D objects, as happens after placing a rack
        // time what placing one more rack costs (state change plus rebuilding the 3D world), as the player feels it
        sw.Restart(); s.AddObject(ObjKind.Rack, 67, 6, 0, 0); _world.Sync(0); var rebuildMs = Bench.Ms(sw);
        sw.Restart(); s.Touch(); _world.Sync(0); var fullMs = Bench.Ms(sw);
        var parts = new List<string>();
        var views = new (string name, float dist, Vector3 target)[]
        {
            ("overview", Math.Max(60, s.PlotSize * 0.9f), new Vector3(s.PlotSize / 2f, 0, s.PlotSize / 2f)),
            ("closeup", 14, new Vector3(20, 0, 12)),
        };
        foreach (var (name, dist, target) in views)
        {
            _cam.SetView(0.6f, 0.85f, dist, target);
            for (var i = 0; i < 30; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            _benchSample = new Bench.Sample();
            var start = Time.GetTicksMsec();
            while (Time.GetTicksMsec() - start < seconds * 1000) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            parts.Add(_benchSample.Report(name));
            _benchSample = null;
        }
        var live = s.Objects.Count(o => o.Kind == ObjKind.Rack);
        var json = $"{{\"racks\": {live}, \"state_build_ms\": {buildMs:0}, \"place_rack_ms\": {rebuildMs:0}, \"full_touch_ms\": {fullMs:0}, \"draw_calls\": {Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame)}, \"objects_in_frame\": {Performance.GetMonitor(Performance.Monitor.RenderTotalObjectsInFrame)}, \"nodes\": {Performance.GetMonitor(Performance.Monitor.ObjectNodeCount)}, {string.Join(", ", parts)}}}";
        GD.Print(json);
        if (oi >= 0) System.IO.File.WriteAllText(args[oi + 1], json);
        GetTree().Quit();
    }
    /// <summary>--screenshot path [--in-game] [--interior] [--new-game] [--info] [--options TAB] [--company TAB] [--category cat_x] [--nl] [--hours N] [--driveway] [--curves] [--curvetool] [--gate] [--barrier] [--cam yaw,pitch,dist,x,z]</summary>
    async System.Threading.Tasks.Task Screenshot(string path, string[] args)
    {
        if (args.Contains("--nl")) Loc.SetLanguage("nl");
        var hi = Array.IndexOf(args, "--hours");
        var hours = hi >= 0 ? double.Parse(args[hi + 1], System.Globalization.CultureInfo.InvariantCulture) : 6;
        if (args.Contains("--showcase"))
        {
            // playtest batch 1: a big building site with several cranes, an excavator, a parking lot with bays and the three trucks
            var g = GameState.New(new NewGameOptions { Company = "Polder Datacenters", Creative = true, CreativeOptions = new CreativeOptions { UnlimitedMoney = true, AllUnlocks = true } });
            g.AddBuilding(BuildingKind.Datacenter, 3, 3, 40, 30, 1);
            g.PaintSurface(46, 10, 5, g.PlotSize - 10, Surface.Asphalt, out _);
            g.PaintSurface(51, 14, 13, 12, Surface.Parking, out _);
            g.PaintSurface(51, 28, 11, 6, Surface.Parking, out _);
            StartGame(g, false);
            var loads = new[] { TruckLoad.Material, TruckLoad.Equipment, TruckLoad.Customer };
            for (var i = 0; i < loads.Length; i++)
            {
                var t = Models.Truck(loads[i]);
                t.Position = new Vector3(48.5f, 0, 26 + i * 14);
                t.RotationDegrees = new Vector3(0, 180, 0);
                _world.AddChild(t);
            }
            // --lineup: one of each kind of person in a row on the parking lot, to judge the figures up close
            var people = new (Role role, int id, StaffRole staff)[]
            {
                (Role.Builder, 1, default), (Role.Fitter, 2, default), (Role.Engineer, 3, default), (Role.Visitor, 4, default), (Role.Visitor, 5, default),
                (Role.Visitor, 6, default), (Role.Staff, 7, StaffRole.Guard), (Role.Staff, 8, StaffRole.Cooling), (Role.Staff, 9, StaffRole.Canteen),
            };
            for (var i = 0; i < people.Length; i++)
            {
                var p = Models.Person(people[i].role, people[i].id, people[i].staff);
                p.Position = new Vector3(54 + i * 1.0f, 0, 20);
                _world.AddChild(p);
                // a mix of poses: standing, walking, carrying a crate, pushing a trolley, holding a cable reel
                var pose = i switch { 0 => PersonPose.Carry, 2 => PersonPose.Reel, 4 => PersonPose.Push, 1 or 5 or 7 => PersonPose.Walk, _ => PersonPose.Idle };
                if (pose == PersonPose.Carry) p.GetNode<Node3D>("Crate").Visible = true;
                if (pose == PersonPose.Push) p.GetNode<Node3D>("Cart").Visible = true;
                if (pose == PersonPose.Reel) p.GetNode<Node3D>("ReelP").Visible = true;
                if (p is PersonFigure f) f.Animate(0.4 + i * 0.13, pose, i);
            }
            _cam.SetView(0.75f, 0.85f, 62, new Vector3(36, 0, 32));
            if (args.Contains("--close")) _cam.SetView(1.1f, 0.5f, 22, new Vector3(48.5f, 0, 42));
            if (args.Contains("--lineup")) _cam.SetView(0.05f, 0.22f, 7.5f, new Vector3(58, 0.9f, 20));
            _speed = 0;
        }
        else if (args.Contains("--in-game") || args.Contains("--interior"))
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
                if (args.Contains("--overlay-network")) SetOverlay(OverlayMode.Network);
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
                if (args.Contains("--curvetool"))
                {
                    // a curved road off the driveway and a winding footpath, plus one being drawn
                    _state.AddCurve(new CurvePath { Surface = Surface.Asphalt, Ax = 28, Az = 52.5, Cx = 11, Cz = 52.5, Bx = 11, Bz = 38, Width = 4 });
                    _state.AddCurve(new CurvePath { Surface = Surface.Footpath, Ax = 14, Az = 35, Cx = 5, Cz = 35, Bx = 6, Bz = 50, Width = 2 });
                    _state.AddCurve(new CurvePath { Surface = Surface.BikePath, Ax = 7, Az = 52, Cx = 8, Cz = 58.5, Bx = 16, Bz = 58.5, Width = 2 });
                    _hud.Pick(new BuildItem(Mode.Curve, (int)Surface.Asphalt));
                    _curveA = new Vector2(31, 38); _curveB = new Vector2(50, 30); _hoverPoint = new Vector2(48, 40);
                    UpdateGhost();
                    _cam.SetView(0.6f, 1.0f, 52, new Vector3(26, 0, 46));
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
        // --gallery a.glb,b.glb: models from assets/models in a row on an empty plot, to judge them in the game's light
        var gal = Array.IndexOf(args, "--gallery");
        if (gal >= 0 && gal + 1 < args.Length)
        {
            if (_state == null || !_playing) StartGame(GameState.New(new NewGameOptions { Company = "Polder Datacenters", Creative = true }), false);
            var x = 6f;
            foreach (var f in args[gal + 1].Split(','))
            {
                var mesh = Assets.Mesh(f, Foliage.ByName);
                if (mesh == null) continue;
                var w = Math.Max(mesh.GetAabb().Size.X, 1f);
                x += w / 2;
                _world.AddChild(new MeshInstance3D { Mesh = mesh, Position = new Vector3(x, 0, 20) });
                x += w / 2 + 1.5f;
            }
            _cam.SetView(0.25f, 0.4f, Math.Max(7, (x - 6) * 0.62f), new Vector3((6 + x) / 2, 1.2f, 20));
        }
        var cami = Array.IndexOf(args, "--cam");
        if (cami >= 0 && cami + 1 < args.Length)
        {
            // --cam yaw,pitch,distance,x,z
            var v = args[cami + 1].Split(',').Select(x => float.Parse(x, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            _cam.SetView(v[0], v[1], v[2], new Vector3(v[3], 0, v[4]));
        }
        // --drag-demo: an empty demo hall with a smart-drag rack block in progress
        if (args.Contains("--drag-demo"))
        {
            _state.Objects.RemoveAll(o => o.Kind == ObjKind.Rack); _state.Touch();
            _hud.Pick(new BuildItem(Mode.Row, (int)ObjKind.Rack));
            _dragStart = new Vector2I(12, 16); _hoverCell = new Vector2I(24, 30);
            UpdateGhost();
        }
        // --floor N: look at floor N with the roofs off (stairs and lifts screenshots)
        var fli = Array.IndexOf(args, "--floor");
        if (fli >= 0 && fli + 1 < args.Length) SetView(int.Parse(args[fli + 1]), false);
        // step 10: --time 2027-12-20T21:00 --weather snow|rain|storm|fog|cloudy|clear --snow 0..1 --walk x,z,floor,yaw,pitch --migration
        var tmi = Array.IndexOf(args, "--time");
        if (tmi >= 0 && tmi + 1 < args.Length) _state.Hours = (DateTime.Parse(args[tmi + 1], System.Globalization.CultureInfo.InvariantCulture) - GameState.Epoch).TotalHours;
        var wxi = Array.IndexOf(args, "--weather");
        if (wxi >= 0 && wxi + 1 < args.Length) _world.ForceWeather = Enum.Parse<WeatherKind>(args[wxi + 1], true);
        var sni = Array.IndexOf(args, "--snow");
        if (sni >= 0 && sni + 1 < args.Length) _world.ForceSnow = double.Parse(args[sni + 1], System.Globalization.CultureInfo.InvariantCulture);
        _world.SnapFrames = 100000;
        if (args.Contains("--migration")) MigrationDemo(args.Contains("--movers"));
        if (args.Contains("--cables")) CableDemo(args.Contains("--at-work"));
        var wki = Array.IndexOf(args, "--walk");
        if (wki >= 0 && wki + 1 < args.Length)
        {
            var v = args[wki + 1].Split(',').Select(x => float.Parse(x, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            SetView((int)v[2], v[2] < 0.5f);
            _cam.SetView(v[3], 0.8f, 20, new Vector3(v[0], 0, v[1]));
            StartWalk();
            _fp.Yaw = v[3]; _fp.Pitch = v.Length > 4 ? v[4] : -0.1f;
        }
        if (args.Contains("--staff-demo"))
        {
            // a few days of automatic hiring, then a broken CRAH with a technician on the way and a city power cut
            _state.Hours = Math.Floor(_state.Hours / 24) * 24 + 33;
                        for (var d = 0; d < 3; d++) _state.Advance(24);
            _state.Support247 = true; _state.Noc247 = true; _state.Advance(24);
            var crah = _state.Objects.First(o => o.Kind == ObjKind.Crah);
            if (!args.Contains("--no-break")) { crah.Wear = 0; _state.Break(crah); var ups = _state.Objects.First(o => o.Kind == ObjKind.Ups); _state.Break(ups); }
            if (!args.Contains("--no-outage")) _state.StartEvent(EventKind.CityOutage, 5);
            foreach (var o in _state.Objects.Where(o => Staffing.Breaks(o.Kind)).Take(12)) o.Wear = 55;
            _state.Advance(0.5);
            for (var i = 0; i < 60; i++) _sim.Tick(0.05);
            if (args.Contains("--interior")) { _cam.SetView(0.35f, 0.75f, 22, new Vector3((float)crah.X, 0, (float)crah.Z + 2)); Inspect(new Vector2I(crah.X, crah.Z)); }
        }
        if (args.Contains("--rep-demo")) RepDemo();
        if (args.Contains("--growth-demo")) GrowthDemo();
        if (args.Contains("--progress-demo")) await ProgressDemo();
        if (args.Contains("--photo")) StartPhoto();
        if (args.Contains("--award-demo")) AwardDemo(args.Contains("--close"));
        if (args.Contains("--plan-demo")) PlanDemo();
        var tui = Array.IndexOf(args, "--tutorial");
        if (tui >= 0)
        {
            StartNew(new NewGameOptions { Tutorial = true, GridQueue = true });
            var skip = tui + 1 < args.Length && int.TryParse(args[tui + 1], out var n) ? n : 0;
            if (skip > 0) _state.TutorialSkipped.Add(Tutorial.Welcome);
            for (var i = 1; i < skip; i++) _state.TutorialSkipped.Add(Tutorial.Steps[i - 1].Goal);
            UpdateGoals(); _coach?.Refresh();
        }
        var gri = Array.IndexOf(args, "--growth");
        if (gri >= 0 && gri + 1 < args.Length) { _playing = true; OpenGrowth(int.Parse(args[gri + 1])); }
        var rpi = Array.IndexOf(args, "--reputation");
        if (rpi >= 0 && rpi + 1 < args.Length) { _playing = true; OpenReputation(int.Parse(args[rpi + 1])); }
        var sti = Array.IndexOf(args, "--staff");
        if (sti >= 0 && sti + 1 < args.Length) { _playing = true; OpenStaff(int.Parse(args[sti + 1])); }
        if (args.Contains("--new-game")) _ui.AddChild(new NewGameDialog(_ => { }));
        if (args.Contains("--start-new")) StartNew(new NewGameOptions());
        var ci = Array.IndexOf(args, "--category");
        if (ci >= 0 && ci + 1 < args.Length) _hud?.ShowCategory(args[ci + 1]);
        if (args.Contains("--info")) OpenInfo();
        var oi = Array.IndexOf(args, "--options");
        if (oi >= 0) OpenOptions(oi + 1 < args.Length && int.TryParse(args[oi + 1], out var tab) ? tab : 0);
        if (args.Contains("--power")) { _playing = true; OpenPower(); }
        if (args.Contains("--network")) { _playing = true; OpenNetwork(); }
        var coi = Array.IndexOf(args, "--company");
        if (coi >= 0 && coi + 1 < args.Length) { _playing = true; OpenCompany(int.Parse(args[coi + 1])); }
        var seci = Array.IndexOf(args, "--section");
        if (seci >= 0 && seci + 1 < args.Length)
        {
            _playing = true;
            var parts = args[seci + 1].Split(',');
            OpenSectionTab(int.Parse(parts[0]), parts.Length > 1 ? int.Parse(parts[1]) : 0);
        }
        if (args.Contains("--goals-open")) _hud?.ToggleGoals();
        if (args.Contains("--overlay-network") && !args.Contains("--interior")) { SetOverlay(OverlayMode.Network); _cam.SetView(0.62f, 0.9f, 70, new Vector3(30, 0, 40)); }
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

    /// <summary>Screenshot setup for step 11: two years of figures, a scenario with a medal, some achievements and a timelapse picture.</summary>
    async System.Threading.Tasks.Task ProgressDemo()
    {
        var s = _state;
        s.Options.Creative = false; s.Options.CreativeOptions = new CreativeOptions();
        s.ScenarioId = "ten_mw"; s.ScenarioStartDay = s.Hours / 24 - 700; s.ScenarioMedal = 1;
        var rng = new Random(7);
        s.Stats.Clear();
        for (var d = 0; d <= 730; d++)
        {
            var g = Math.Min(1, d / 600.0);
            var racks = (int)(8 + 900 * g * g * (3 - 2 * g));
            var wobble = 1 + 0.03 * Math.Sin(d / 9.0) + 0.02 * (rng.NextDouble() - 0.5);
            s.Stats.Add(new StatSample
            {
                Day = d, LiveRacks = racks, ItKw = racks * 3.9 * wobble, FacilityKw = racks * 3.9 * (1.62 - 0.3 * g) * wobble,
                Pue = 1.62 - 0.3 * g + 0.02 * Math.Sin(d / 30.0), Uptime = d % 97 == 13 ? 0.982 : 1,
                Customers = (int)(1 + racks / 18.0), Revenue = racks * 800, Income = (long)(racks * 800 / 30.0 * wobble), Costs = (long)((40_000 + racks * 610) / 30.0 * (2 - wobble)),
                Money = (long)(12_000_000 - 4_000_000 * Math.Sin(Math.Min(d, 400) / 400.0 * Math.PI / 2) + Math.Max(0, d - 380) * 21_000),
                Gbps = racks * 0.35 * wobble, Reputation = 50 + 32 * g, Esg = 30 + 35 * g, Staff = 6 + (int)(64 * g), HostingCustomers = d > 420 ? (d - 420) * 9 : 0,
            });
        }
        foreach (var id in new[] { "first_rack", "racks_100", "mw_1", "n_plus_1", "pue_14", "carriers_3", "first_contract", "clients_25", "profit_month", "first_cert", "year_1", "tourist", "photographer" })
            if (!s.Achieved.Contains(id)) s.Achieved.Add(id);
        s.Notes.Clear();
        _speed = 0;
        for (var i = 0; i < 20; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await _lapse.Capture(s);
    }

    /// <summary>Screenshot setup for step 11 round 2: three years of awards, statuettes on the CEO desk.</summary>
    void AwardDemo(bool close)
    {
        var s = _state;
        s.Awards.Clear();
        s.Awards.Add(new AwardResult { Year = 2027, Place = 3, Score = 61.4, Winner = s.Rivals.FirstOrDefault()?.Name ?? "", WinnerScore = 70.2, Entrants = 4 });
        s.Awards.Add(new AwardResult { Year = 2028, Place = 2, Score = 72.9, Winner = s.Rivals.Skip(1).FirstOrDefault()?.Name ?? "", WinnerScore = 74.0, Entrants = 4 });
        s.Awards.Add(new AwardResult { Year = 2029, Place = 1, Score = 81.3, Winner = s.Options.Company, WinnerScore = 81.3, Entrants = 4 });
        s.Touch();
        if (close && s.TrophyDesk is { } desk)
        {
            SetView(0, false);
            _cam.SetView(0.9f, 0.42f, 3.2f, new Vector3(desk.X + 0.5f, 0.9f, desk.Z + 0.5f));
        }
    }

    /// <summary>Screenshot setup for plan mode: a planned second datacenter with a hall and racks beside the demo campus.</summary>
    void PlanDemo()
    {
        var s = _state;
        s.Options.Creative = false; s.Options.CreativeOptions = new CreativeOptions(); s.Money = 4_000_000;
        _planMode = true; _hud?.SetPlan(true);
        var ids = s.LayoutIds();
        s.Planning = true;
        (int x, int z)? spot = null;
        for (var z = 4; z < s.PlotSize - 14 && spot == null; z += 2)
            for (var x = 4; x < s.PlotSize - 20 && spot == null; x += 2)
                if (s.CanBuild(BuildingKind.Datacenter, x, z, 18, 12, 1) == null) spot = (x, z);
        if (spot is { } p)
        {
            s.AddBuilding(BuildingKind.Datacenter, p.x, p.z, 18, 12, 1);
            s.AddRoom(RoomType.DataHall, p.x + 1, p.z + 1, 12, 10, 0);
            s.AddRoom(RoomType.UpsRoom, p.x + 13, p.z + 1, 4, 10, 0);
            foreach (var (x, z, r) in GameState.RowBlock(p.x + 2, p.z + 2, p.x + 11, p.z + 8, ObjKind.Rack, 0, s.MaxRowLength)) s.AddObject(ObjKind.Rack, x, z, r, 0);
            s.AddObject(ObjKind.Ups, p.x + 14, p.z + 2, 0, 0);
            s.AddObject(ObjKind.Generator, p.x + 2, p.z + 13, 0, 0);
            s.AddObject(ObjKind.Generator, p.x + 7, p.z + 13, 0, 0);
            _cam.SetView(0.7f, 0.85f, 46, new Vector3(p.x + 9, 0, p.z + 7));
        }
        s.Planning = false;
        s.MarkPlanned(ids);
        _planBar?.Refresh();
    }

    /// <summary>Screenshot setup for step 10: a big customer moves in; run until the moving truck is at the dock and movers push trolleys.</summary>
    void MigrationDemo(bool movers)
    {
        var s = _state;
        s.Hours = Math.Floor(s.Hours / 24) * 24 + 10;
        var racks = s.Objects.Where(o => o.Kind == ObjKind.Rack && o.Status == ObjStatus.Live).Take(20).Select(o => o.Id).ToList();
        var d = new Deal { Id = s.NextId++, Product = Product.Rack, State = DealState.Active, Racks = 20, RackIds = racks, Monthly = 20000, StartDay = s.Day, EndDay = s.Day + 900, Months = 30, Name = "Nordwind Cloud" };
        s.Deals.Add(d);
        _sim.TrackMigrations();
        _sim.MoveQueue.RemoveAll(j => j.DealId != d.Id);
        for (var i = 0; i < 3000; i++)
        {
            _sim.Tick(0.01); s.Hours += 0.01;
            if (_sim.Agents.Any(a => a.Role == Role.Truck && a.Load == TruckLoad.Customer && a.Phase == 2) && _sim.Agents.Count(a => a.Role == Role.Visitor && a.Carry > 0 && a.Loaded) >= 2
                && _sim.Agents.Where(a => a.Role == Role.Visitor && a.Loaded).Average(a => a.WayI) > 4) break;
        }
        var t = _sim.Agents.FirstOrDefault(a => a.Role == Role.Truck && a.Load == TruckLoad.Customer);
        SetView(0, false);
        var m = _sim.Agents.FirstOrDefault(a => a.Role == Role.Visitor && a.Carry > 0);
        if (movers && m != null) _cam.SetView((float)m.Heading + 0.5f, 0.45f, 7, new Vector3((float)m.X, 0.8f, (float)m.Z));
        else if (t != null) _cam.SetView(0.3f, 0.95f, 30, new Vector3((float)t.X - 2, 0, (float)t.Z - 1));
    }

    /// <summary>Screenshot: new racks waiting for cabling, engineers walking up with reels (or, with --at-work, unrolling them).</summary>
    void CableDemo(bool atWork)
    {
        var s = _state;
        s.Hours = Math.Floor(s.Hours / 24) * 24 + 11;
        foreach (var o in s.Objects.Where(o => o.Kind == ObjKind.Rack && o.Status == ObjStatus.Live).Take(12)) o.Status = ObjStatus.Placed;
        s.Touch();
        Agent e = null;
        for (var i = 0; i < 4000; i++)
        {
            _sim.Tick(0.01); s.Hours += 0.01;
            e = _sim.Agents.FirstOrDefault(a => a.Role == Role.Engineer && a.Cable != CableKind.None && (atWork ? a.Working : a.Moving && a.WayI > 3));
            if (e != null && (!atWork || _sim.Agents.Count(a => a.Role == Role.Engineer && a.Working) >= 2)) break;
        }
        SetView(0, false);
        if (e == null) return;
        if (!atWork) { _cam.SetView((float)e.Heading + 0.6f, 0.5f, 6, new Vector3((float)e.X, 0.8f, (float)e.Z)); return; }
        // walk up the aisle and look at the engineer from the side
        var h = e.Heading;
        var at = new Vector3((float)(e.X + Math.Cos(h) * 3.6), 0, (float)(e.Z - Math.Sin(h) * 3.6));
        SetView(e.Floor, e.Floor == 0);
        _cam.SetView(0, 0.8f, 20, at);
        StartWalk();
        _fp.Yaw = (float)Math.Atan2(Math.Cos(h), -Math.Sin(h)); _fp.Pitch = -0.32f;
    }

    /// <summary>Screenshots of step 8: managers in place, policies on, a certificate, research running, reviews and a proposal.</summary>
    /// <summary>Screenshot setup for step 9: a second location, a hosting company, a key person and some months of market news.</summary>
    void GrowthDemo()
    {
        var s = _state;
        s.Options.Creative = false; s.Options.Events = false;
        s.Grandfathered = Enum.GetValues<Exec>().ToList();
        s.Money = 400_000_000;
        foreach (var o in s.Objects) if (o.Status != ObjStatus.Live && o.Kind != ObjKind.Rack) o.Status = ObjStatus.Live;
        s.OpenCountry("de");
        s.BuildSite("nl", 1); s.BuildSite("de", 0);
        s.StartHosting();
        var hall = s.Rooms.FirstOrDefault(r => r.Type == RoomType.DataHall);
        if (hall != null) hall.Zone = HallZone.OwnCloud;
        s.OrderServers(40);
        s.KeyPeople.Add(new KeyPerson { Id = s.NextId++, Name = "Sanne Visser", Role = KeyRole.EnergyTrader, Salary = 10200, Candidate = true, ExpiresDay = s.Day + 400 });
        s.HireKey(s.KeyPeople[^1]);
        s.SetInsurance(1);
        for (var d = 0; d < 200; d++) s.Advance(24);
        SetState(s);
    }

    void RepDemo()
    {
        var s = _state;
        s.Options.Creative = false; s.Options.Events = false;
        foreach (var e in new[] { Exec.Security, Exec.Compliance, Exec.Research, Exec.Marketing })
            if (s.Hire(e) == null) s.Execs.Add(new ExecHire { Role = e });
        s.SetPolicy(0, true); s.SetPolicy(1, true);
        s.AddObject(ObjKind.GlassLodge, 42, 23, 0, 0);
        foreach (var x in new[] { 8, 14, 20 }) s.AddObject(ObjKind.CameraPole, x, 50, 0, 0);
        for (var i = 0; i < 4; i++) s.AddObject(ObjKind.Bollard, 34 + i, 52, 0, 0);
        foreach (var o in s.Objects) if (o.Status != ObjStatus.Live && o.Kind != ObjKind.Rack) o.Status = ObjStatus.Live;
        s.Hours = Math.Floor(s.Hours / 24) * 24 + 10;
        for (var d = 0; d < 4; d++) s.Advance(24);
        s.Researched.AddRange(new[] { "cool_corridor", "pwr_lithium", "sec_biometrics", "biz_crm" });
        s.QueueResearch("cool_ecfans"); s.QueueResearch("sec_mantrap"); s.QueueResearch("net_optics");
        s.ResearchProgress = 22;
        s.Certs.Add(new Certificate { Kind = CertKind.Iso27001, Valid = true, IssuedDay = s.Day - 40, ExpiresDay = s.Day + 1055, CheckDay = s.Day + 325 });
        s.RequestAudit(CertKind.TierIII);
        var names = s.Deals.Where(d => d.State == DealState.Active).Select(GameState.DisplayArg).ToList();
        var rng = new Random(4);
        for (var i = 0; i < 8; i++) s.AddReview(s.Day - i * 3, names[i % names.Count], (ReviewTopic)(i % 4), new[] { 5, 4, 3, 5, 2, 4, 5, 4 }[i]);
        s.Reputation = 58;
        s.MakeProposal((int)s.Day, rng, ProposalKind.Router);
        s.MakeProposal((int)s.Day, rng, ProposalKind.CrahFans);
        s.Notes.Clear();
        for (var i = 0; i < 80; i++) _sim.Tick(0.05);
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
