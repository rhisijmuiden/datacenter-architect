using System;
using System.Collections.Generic;
using System.Linq;
using DatacenterArchitect.Core;
using DatacenterArchitect.Sim;
using Godot;

namespace DatacenterArchitect.UI;

public enum Mode { Select, Erase, Surface, Fence, Building, Room, Door, Object, Row, Curve }

/// <summary>What the player is about to build: a mode plus the surface, fence, building, room, door or object kind.</summary>
public readonly record struct BuildItem(Mode Mode, int Arg = 0)
{
    public static readonly BuildItem Select = new(Mode.Select);
    public string NameKey => Mode switch
    {
        Mode.Surface => Catalog.Surfaces[(Surface)Arg].NameKey,
        Mode.Fence => Catalog.Fences[(FenceType)Arg].NameKey,
        Mode.Building => Catalog.Buildings[(BuildingKind)Arg].NameKey,
        Mode.Room => Catalog.Rooms[(RoomType)Arg].NameKey,
        Mode.Door => Catalog.Doors[(DoorType)Arg].NameKey,
        Mode.Object or Mode.Row => Catalog.Objects[(ObjKind)Arg].NameKey,
        Mode.Curve => (Surface)Arg switch { Surface.Footpath => "curve_footpath", Surface.BikePath => "curve_bikepath", _ => "curve_asphalt" },
        Mode.Erase => "tool_erase",
        _ => "tool_select",
    };
}

/// <summary>In-game HUD: build bar along the bottom with the bulldozer bottom-right, resource bars top, camera/info and site checklist right, notifications and time bottom.</summary>
public partial class Hud : Control
{
    public Action<BuildItem> ToolChanged;
    public Action<int> SpeedChanged, FloorsChanged;
    public Action<Facade> FacadeChanged;
    public Action<int> CyclePriority;
    public Action Walk;
    public Action Photo;
    /// <summary>Undo and redo the last building action (Ctrl+Z / Ctrl+Y), from the buttons above the bulldozer.</summary>
    public Action Undo, Redo;
    /// <summary>Step 11: plan mode on/off (button above the bulldozer, key B).</summary>
    public Action TogglePlan;
    public Action RotateLeft, RotateRight, ToggleGrid, ToggleRoofs, FloorUp, FloorDown, OpenInfo, OpenOptions, OpenMenu, ToggleLanguage, OpenPower, OpenNetwork;
    public Action<int> ToggleOverlay, OpenCompany, OpenStaff, OpenReputation, OpenGrowth;
    public Func<GameState> State;
    /// <summary>Opens a section of the management rail (GUI v2); the rail sits on the left, F1 to F7.</summary>
    public Action<int> OpenSection;

    /// <summary>Width of the management rail; the build bar and the top bar start to the right of it.</summary>
    public const float RailWidth = 72, LeftEdge = 12 + RailWidth + 8;
    readonly List<Button> _railBtns = new();
    readonly List<Label> _railBadges = new();
    int _activeSection = -1;
    /// <summary>Every message shown this session, newest first (Overview › Messages).</summary>
    public readonly List<HudMessage> History = new();
    List<(string key, bool done)> _lastGoals = new();
    public IReadOnlyList<(string key, bool done)> Goals => _lastGoals;
    bool _goalsOpen;

    readonly Dictionary<string, (Label value, ProgressBar bar)> _bars = new();
    Label _weather;
    Label _money, _date, _company, _toolHint, _floorLabel, _creative;
    Label _staffStat, _repStat;
    bool _alert;
    Button _langBtn, _roofBtn, _powerBtn, _coolBtn, _netBtn;
    Label _pue;
    VBoxContainer _notes, _goals, _inspect;
    HBoxContainer _sub;
    PanelContainer _inspectPanel;
    readonly List<Button> _speedBtns = new();
    readonly Dictionary<string, Button> _catBtns = new();
    Button _selectBtn, _eraseBtn, _undoBtn, _redoBtn, _planBtn;
    HBoxContainer _floorsRow, _facadeRow;
    SpinBox _floors;
    OptionButton _facade;
    PanelContainer _projectsPanel;
    HBoxContainer _topRow;
    VBoxContainer _rightCol;
    VBoxContainer _projects;
    static readonly Facade[] OfficeFacades = { Facade.Brick, Facade.Concrete, Facade.Glass };
    string _category = "cat_land";
    BuildItem _item = BuildItem.Select;

    static BuildItem O(ObjKind k) => new(Mode.Object, (int)k);

    // Categories follow the brand guide; the build bar sits along the bottom of the screen.
    static readonly (string cat, BuildItem[] items)[] Categories =
    {
        ("cat_land", new[]
        {
            new BuildItem(Mode.Surface, (int)Surface.Asphalt), new BuildItem(Mode.Surface, (int)Surface.Parking),
            new BuildItem(Mode.Surface, (int)Surface.Footpath), new BuildItem(Mode.Surface, (int)Surface.BikePath),
            new BuildItem(Mode.Curve, (int)Surface.Asphalt), new BuildItem(Mode.Curve, (int)Surface.Footpath), new BuildItem(Mode.Curve, (int)Surface.BikePath),
            new BuildItem(Mode.Surface, (int)Surface.Yard), new BuildItem(Mode.Surface, (int)Surface.Grass),
            O(ObjKind.Tree), O(ObjKind.Shrub), O(ObjKind.LampPost), O(ObjKind.Bench), O(ObjKind.BikeRack),
        }),
        ("cat_security", new[]
        {
            new BuildItem(Mode.Fence, (int)FenceType.Mesh), new BuildItem(Mode.Fence, (int)FenceType.Welded),
            new BuildItem(Mode.Fence, (int)FenceType.Palisade), new BuildItem(Mode.Fence, (int)FenceType.Wall),
            new BuildItem(Mode.Fence, (int)FenceType.Electric),
            O(ObjKind.SlidingGate), O(ObjKind.SlidingGateLarge), O(ObjKind.PedGate), O(ObjKind.Barrier), O(ObjKind.GuardBooth), O(ObjKind.Intercom),
            O(ObjKind.CameraPole), O(ObjKind.Bollard), O(ObjKind.CameraDome), O(ObjKind.SecurityWall), O(ObjKind.GlassLodge), new BuildItem(Mode.Door, (int)DoorType.Mantrap),
        }),
        ("cat_buildings", new[]
        {
            new BuildItem(Mode.Building, (int)BuildingKind.Datacenter), new BuildItem(Mode.Building, (int)BuildingKind.Office),
            new BuildItem(Mode.Building, (int)BuildingKind.Warehouse), O(ObjKind.Stairs), O(ObjKind.PassengerLift), O(ObjKind.Lift),
            new BuildItem(Mode.Door, (int)DoorType.Single), new BuildItem(Mode.Door, (int)DoorType.Double),
            new BuildItem(Mode.Door, (int)DoorType.Fire), new BuildItem(Mode.Door, (int)DoorType.Badge),
            new BuildItem(Mode.Door, (int)DoorType.RollUp),
        }),
        ("cat_rooms", Enum.GetValues<RoomType>().Select(t => new BuildItem(Mode.Room, (int)t)).ToArray()),
        ("cat_interior", new[]
        {
            O(ObjKind.Desk), O(ObjKind.Chair), O(ObjKind.Cabinet), O(ObjKind.Plant), O(ObjKind.MeetingTable),
            O(ObjKind.Sofa), O(ObjKind.Coffee), O(ObjKind.Vending), O(ObjKind.Microwave), O(ObjKind.Terminal), O(ObjKind.LabBench), O(ObjKind.SocDesk),
            O(ObjKind.PalletRack), O(ObjKind.Workbench), O(ObjKind.PalletJack), O(ObjKind.Shredder),
        }),
        ("cat_halls", new[] { new BuildItem(Mode.Row, (int)ObjKind.Rack), O(ObjKind.Crah), O(ObjKind.Pdu), O(ObjKind.CrashCart), O(ObjKind.CameraDome) }),
        ("cat_power", new[]
        {
            O(ObjKind.Transformer), O(ObjKind.Switchgear), O(ObjKind.Ups), O(ObjKind.Battery), O(ObjKind.Pdu),
            O(ObjKind.Generator), O(ObjKind.DieselTank), O(ObjKind.UndergroundTank),
            O(ObjKind.SolarPanel), O(ObjKind.WindTurbine), O(ObjKind.BatteryContainer),
        }),
        ("cat_cooling", new[] { O(ObjKind.Crah), O(ObjKind.Chiller), O(ObjKind.CoolingTower), O(ObjKind.DryCooler) }),
        ("cat_network", new[] { O(ObjKind.FiberPit), O(ObjKind.Odf), O(ObjKind.CoreRouter), O(ObjKind.Spine), O(ObjKind.NocWall) }),
    };

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        BuildRail(); BuildTop(); BuildLeft(); BuildRight(); BuildBottom();
    }

    // ---------------- management rail (left) ----------------
    void BuildRail()
    {
        var rail = new PanelContainer();
        rail.AnchorTop = 0; rail.AnchorBottom = 1;
        rail.OffsetLeft = 12; rail.OffsetRight = 12 + RailWidth; rail.OffsetTop = 12; rail.OffsetBottom = -12;
        rail.AddThemeStyleboxOverride("panel", Ui.Box(Brand.Panel, 12, Brand.Line, 1, 4, true));
        AddChild(rail);
        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 4);
        rail.AddChild(v);
        var logo = new TextureRect { Texture = GD.Load<Texture2D>("res://icon.svg"), CustomMinimumSize = new Vector2(0, 44), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered };
        v.AddChild(logo);
        v.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });
        for (var i = 0; i < Sections.All.Length; i++)
        {
            var sec = Sections.All[i];
            var idx = i;
            var b = new Button
            {
                Text = sec.ShortKey, Icon = GD.Load<Texture2D>($"res://assets/icons/{sec.Icon}.svg"), ExpandIcon = true,
                IconAlignment = HorizontalAlignment.Center, VerticalIconAlignment = VerticalAlignment.Top,
                CustomMinimumSize = new Vector2(RailWidth - 8, 58), ClipText = true,
                TooltipText = Loc.T(sec.Key) + " (" + sec.Hotkey + ")",
            };
            b.AddThemeConstantOverride("icon_max_width", 22);
            b.AddThemeFontOverride("font", Ui.DisplaySemi); b.AddThemeFontSizeOverride("font_size", 13);
            b.AddThemeColorOverride("icon_normal_color", sec.Accent);
            b.AddThemeColorOverride("icon_hover_color", sec.Accent);
            b.Pressed += () => OpenSection?.Invoke(idx);
            var badge = new Label { Visible = false, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore, AutoTranslateMode = AutoTranslateModeEnum.Disabled };
            badge.AddThemeFontOverride("font", Ui.BodySemi); badge.AddThemeFontSizeOverride("font_size", 11);
            badge.AddThemeColorOverride("font_color", Brand.PanelSolid);
            var bs = Ui.Box(Brand.Critical, 8, null, 0, 0); bs.ContentMarginLeft = bs.ContentMarginRight = 4;
            badge.AddThemeStyleboxOverride("normal", bs);
            badge.AnchorLeft = badge.AnchorRight = 1; badge.OffsetLeft = -24; badge.OffsetRight = -4; badge.OffsetTop = 3; badge.OffsetBottom = 19;
            badge.GrowHorizontal = GrowDirection.Begin;
            b.AddChild(badge);
            _railBtns.Add(b); _railBadges.Add(badge);
            v.AddChild(b);
        }
        v.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
        v.AddChild(new HSeparator());
        foreach (var (icon, tip, act) in new (string, string, Action)[] { ("settings", "hud_options", () => OpenOptions?.Invoke()), ("info", "hud_info", () => OpenInfo?.Invoke()), ("menu", "hud_menu", () => OpenMenu?.Invoke()) })
        {
            var b = Ui.IconButton(icon, tip, act);
            b.CustomMinimumSize = new Vector2(RailWidth - 8, 40);
            v.AddChild(b);
        }
        SetActiveSection(-1);
    }

    /// <summary>Highlights the open section on the rail (-1 when the drawer is closed).</summary>
    public void SetActiveSection(int section)
    {
        _activeSection = section;
        for (var i = 0; i < _railBtns.Count; i++)
        {
            var b = _railBtns[i];
            if (i == section)
            {
                var st = Ui.Box(Brand.PanelHover, 8, null, 0, 4);
                st.BorderColor = Sections.All[i].Accent; st.BorderWidthLeft = 3;
                b.AddThemeStyleboxOverride("normal", st); b.AddThemeStyleboxOverride("hover", st);
                b.AddThemeColorOverride("font_color", Brand.Ink);
            }
            else
            {
                b.AddThemeStyleboxOverride("normal", Ui.Box(new Color(0, 0, 0, 0), 8, null, 0, 4));
                b.AddThemeStyleboxOverride("hover", Ui.Box(Brand.PanelHover, 8, null, 0, 4));
                b.AddThemeColorOverride("font_color", Brand.InkDim);
            }
        }
    }

    /// <summary>Red counters on the rail: new leads, open faults, infrastructure problems and open proposals.</summary>
    void UpdateBadges(GameState s)
    {
        if (_railBadges.Count == 0) return;
        void Set(int i, int n, string text = null)
        {
            _railBadges[i].Visible = n > 0;
            _railBadges[i].Text = text ?? (n > 9 ? "9+" : n.ToString());
        }
        var c = s.Creative; var inf = s.Infra;
        Set(Sections.Customers, s.Deals.Count(d => d.State == DealState.Lead));
        Set(Sections.Staff, s.Objects.Count(o => o.Broken && o.Noticed));
        var infraProblem = s.GridDown || (!c.NoPowerLimit && inf.UsableItKw > 0 && inf.ItDemandKw > inf.UsableItKw)
            || (!c.NoCoolingLimit && inf.CoolRatio > 1) || (!c.NoNetworkLimit && s.Net.OfflineRacks > 0 && s.Net.OnlineRacks + s.Net.OfflineRacks > 0);
        Set(Sections.Infra, infraProblem ? 1 : 0, "!");
        Set(Sections.Reputation, s.OpenProposals.Count);
    }

    // ---------------- top ----------------
    void BuildTop()
    {
        var top = _topRow = new HBoxContainer { Position = new Vector2(LeftEdge, 12) };
        top.AddThemeConstantOverride("separation", 10);
        AddChild(top);
        var brand = new PanelContainer();
        var bh = new HBoxContainer(); bh.AddThemeConstantOverride("separation", 10);
        var bv = new VBoxContainer(); bv.AddThemeConstantOverride("separation", 0);
        var title = new Label { Text = "Datacenter Architect", Uppercase = true };
        title.AddThemeFontOverride("font", Ui.DisplayBold); title.AddThemeFontSizeOverride("font_size", 19);
        bv.AddChild(title);
        var ch = new HBoxContainer(); ch.AddThemeConstantOverride("separation", 6);
        _company = Ui.Text("", 12, Brand.InkDim); _company.AutowrapMode = TextServer.AutowrapMode.Off;
        _company.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        ch.AddChild(_company);
        _creative = Ui.Heading("hud_creative", 11, Brand.Cooling);
        ch.AddChild(_creative);
        bv.AddChild(ch);
        bh.AddChild(bv);
        brand.AddChild(bh);
        top.AddChild(brand);

        var stats = new PanelContainer();
        var sh = new HBoxContainer(); sh.AddThemeConstantOverride("separation", 14);
        stats.AddChild(sh);
        AddBar(sh, "res_power", Brand.Power, true, () => OpenPower?.Invoke());
        AddBar(sh, "res_backup", Brand.Power, true, () => OpenPower?.Invoke());
        AddBar(sh, "res_cooling", Brand.Cooling, true, () => OpenPower?.Invoke());
        AddBar(sh, "res_network", Brand.Fiber, true, () => OpenNetwork?.Invoke());
        AddBar(sh, "res_racks", Brand.Ok, false, () => OpenCompany?.Invoke(CompanyPanel.TabContracts));
        AddBar(sh, "res_material", Brand.Amber, false);
        var pv = new VBoxContainer { CustomMinimumSize = new Vector2(56, 0) }; pv.AddThemeConstantOverride("separation", 2);
        pv.AddChild(Ui.Heading("res_pue", 12));
        _pue = Ui.Number("-", 18); _pue.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        pv.AddChild(_pue);
        sh.AddChild(pv);
        var fv = new VBoxContainer { MouseFilter = MouseFilterEnum.Stop, TooltipText = "hud_staff_tip", MouseDefaultCursorShape = CursorShape.PointingHand, CustomMinimumSize = new Vector2(78, 0) };
        fv.AddThemeConstantOverride("separation", 2);
        fv.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) OpenStaff?.Invoke(_alert ? StaffPanel.TabIncidents : StaffPanel.TabTeam); };
        fv.AddChild(Ui.Heading("res_staff", 12));
        _staffStat = Ui.Number("0", 18); _staffStat.AutoTranslateMode = AutoTranslateModeEnum.Disabled; _staffStat.AutowrapMode = TextServer.AutowrapMode.Off;
        fv.AddChild(_staffStat);
        sh.AddChild(fv);
        var rv = new VBoxContainer { MouseFilter = MouseFilterEnum.Stop, TooltipText = "hud_reputation_tip", MouseDefaultCursorShape = CursorShape.PointingHand, CustomMinimumSize = new Vector2(70, 0) };
        rv.AddThemeConstantOverride("separation", 2);
        rv.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) OpenReputation?.Invoke(ReputationPanel.TabReputation); };
        rv.AddChild(Ui.Heading("res_reputation", 12));
        _repStat = Ui.Number("30", 18); _repStat.AutoTranslateMode = AutoTranslateModeEnum.Disabled; _repStat.AutowrapMode = TextServer.AutowrapMode.Off;
        rv.AddChild(_repStat);
        sh.AddChild(rv);
        var mv = new VBoxContainer { MouseFilter = MouseFilterEnum.Stop, TooltipText = "hud_money_tip", MouseDefaultCursorShape = CursorShape.PointingHand }; mv.AddThemeConstantOverride("separation", 2);
        mv.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) OpenCompany?.Invoke(CompanyPanel.TabFinance); };
        mv.AddChild(Ui.Heading("res_money", 12));
        _money = Ui.Number("€ 0", 20); _money.AddThemeColorOverride("font_color", Brand.Ok);
        _money.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        mv.AddChild(_money);
        sh.AddChild(mv);
        top.AddChild(stats);
        BuildTicker();
    }

    // ---------------- news ticker (step 9) ----------------
    PanelContainer _ticker;
    Control _tickerClip;
    Label _tickerText;
    string _tickerSig;
    float _tickerX;

    void BuildTicker()
    {
        _ticker = new PanelContainer { Position = new Vector2(LeftEdge, 0), MouseFilter = MouseFilterEnum.Stop, TooltipText = "hud_news_tip", MouseDefaultCursorShape = CursorShape.PointingHand, Visible = false };
        _ticker.AddThemeStyleboxOverride("panel", Ui.Box(Brand.Panel, 8, Brand.Line, 1, 6));
        _ticker.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) OpenGrowth?.Invoke(GrowthPanel.TabMarket); };
        var h = new HBoxContainer(); h.AddThemeConstantOverride("separation", 8);
        var tag = Ui.Heading("hud_news", 12, Brand.Amber); tag.MouseFilter = MouseFilterEnum.Ignore;
        h.AddChild(tag);
        _tickerClip = new Control { ClipContents = true, CustomMinimumSize = new Vector2(520, 20), MouseFilter = MouseFilterEnum.Ignore };
        _tickerText = Ui.Text("", 13, Brand.Ink); _tickerText.AutowrapMode = TextServer.AutowrapMode.Off; _tickerText.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        _tickerText.MouseFilter = MouseFilterEnum.Ignore;
        _tickerClip.AddChild(_tickerText);
        h.AddChild(_tickerClip);
        _ticker.AddChild(h);
        AddChild(_ticker);
    }

    /// <summary>The mouse wheel over the build tray scrolls it sideways instead of zooming the camera.</summary>
    public override void _Input(InputEvent e)
    {
        if (e is not InputEventMouseButton { Pressed: true } mb || !TrayScroll(mb.Position, mb.ButtonIndex, mb.Factor)) return;
        GetViewport().SetInputAsHandled();
    }

    /// <summary>One wheel notch moves the tray about one card.</summary>
    public const int TrayScrollStep = 170;

    /// <summary>Scrolls the build tray when <paramref name="at"/> is over it; false when the wheel is not for the tray.</summary>
    public bool TrayScroll(Vector2 at, MouseButton button, float factor = 1f)
    {
        if (_tray == null || _trayScroll == null || !_tray.IsVisibleInTree() || !_tray.GetGlobalRect().HasPoint(at)) return false;
        var dir = button switch
        {
            MouseButton.WheelUp or MouseButton.WheelLeft => -1,
            MouseButton.WheelDown or MouseButton.WheelRight => 1,
            _ => 0,
        };
        if (dir == 0) return false;
        _trayScroll.ScrollHorizontal += (int)(dir * TrayScrollStep * (factor > 0 ? factor : 1f));
        return true;
    }

    public override void _Process(double delta)
    {
        if (_ticker == null || !_ticker.Visible) return;
        _tickerX -= (float)delta * 60;
        var w = _tickerText.GetMinimumSize().X;
        if (_tickerX < -w) _tickerX = _tickerClip.Size.X;
        _tickerText.Position = new Vector2(_tickerX, 0);
    }

    void UpdateTicker(GameState s)
    {
        if (_ticker == null) return;
        var on = s.Options.News && s.News.Count > 0;
        _ticker.Visible = on;
        if (!on) return;
        _ticker.Position = new Vector2(LeftEdge, _topRow.Position.Y + _topRow.Size.Y + 6);
        _tickerClip.CustomMinimumSize = new Vector2(Math.Max(320, _topRow.Size.X - 90), 20);
        var sig = Loc.Language + s.News.Count + s.News[0].Day + s.News[0].Key;
        if (sig == _tickerSig) return;
        _tickerSig = sig;
        _tickerText.Text = string.Join("     ◆     ", s.News.Take(6).Select(GrowthPanel.NewsText));
    }

    void AddBar(HBoxContainer parent, string key, Color c, bool bar = true, Action click = null)
    {
        var v = new VBoxContainer { CustomMinimumSize = new Vector2(bar ? 118 : 72, 0) };
        if (click != null)
        {
            v.MouseFilter = MouseFilterEnum.Stop; v.MouseDefaultCursorShape = CursorShape.PointingHand;
            v.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) click(); };
        }
        v.AddThemeConstantOverride("separation", 2);
        v.AddChild(Ui.Heading(key, 12));
        var value = Ui.Number("0", 18);
        value.AutowrapMode = TextServer.AutowrapMode.Off;
        value.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        v.AddChild(value);
        ProgressBar pb = null;
        if (bar)
        {
            pb = new ProgressBar { MinValue = 0, MaxValue = 1, Step = 0.001, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 5) };
            pb.AddThemeStyleboxOverride("fill", Ui.Box(c, 3, null, 0, 0));
            v.AddChild(pb);
        }
        parent.AddChild(v);
        _bars[key] = (value, pb);
    }

    static void SetBar((Label value, ProgressBar bar) b, string text, double ratio, Color normal)
    {
        b.value.Text = text;
        if (b.bar == null) return;
        b.bar.Value = Math.Min(1, ratio);
        var c = ratio > 1 ? Brand.Critical : ratio > 0.85 ? Brand.Warning : normal;
        b.bar.AddThemeStyleboxOverride("fill", Ui.Box(c, 3, null, 0, 0));
    }

    public void UpdateStats(GameState s)
    {
        var c = s.Creative;
        _company.Text = s.Options.Company;
        _creative.Visible = s.Options.Creative;
        var inf = s.Infra;
        if (c.NoPowerLimit) SetBar(_bars["res_power"], $"{inf.ItDemandKw:0} kW · " + Loc.T("hud_unlimited"), 0, Brand.Power);
        else SetBar(_bars["res_power"], $"{inf.ItDemandKw:0} / {Math.Max(0, inf.UsableItKw):0} kW", inf.UsableItKw <= 0 ? (inf.ItDemandKw > 0 ? 2 : 0) : inf.ItDemandKw / inf.UsableItKw, Brand.Power);
        var facility = inf.ItDemandKw * (inf.Pue > 0 ? inf.Pue : 1) + inf.OtherKw;
        var backupRatio = inf.BackupKw <= 0 ? (facility > 0 ? 2 : 0) : facility / inf.BackupKw;
        var fuel = inf.FuelHours > 0 ? $" · {inf.FuelHours:0} h" : "";
        SetBar(_bars["res_backup"], $"{inf.BackupKw:0} kW {inf.GenRedundancy}{fuel}", c.NoPowerLimit ? 0 : backupRatio, Brand.Power);
        if (c.NoCoolingLimit) SetBar(_bars["res_cooling"], $"{inf.HeatKw:0} kW · " + Loc.T("hud_unlimited"), 0, Brand.Cooling);
        else SetBar(_bars["res_cooling"], $"{inf.HeatKw:0} / {inf.CoolingKw:0} kW", inf.CoolRatio, Brand.Cooling);
        var net = s.Net;
        var gb = System.Globalization.CultureInfo.InvariantCulture;
        if (c.NoNetworkLimit) SetBar(_bars["res_network"], net.DemandGbps.ToString("0.0", gb) + " Gbit/s · " + Loc.T("hud_unlimited"), 0, Brand.Fiber);
        else
        {
            var cap = net.Link(NetLink.Upstream).Capacity;
            SetBar(_bars["res_network"], $"{net.DemandGbps.ToString("0.0", gb)} / {cap.ToString("0", gb)} Gbit/s", net.OfflineRacks > 0 && net.OnlineRacks == 0 ? 2 : net.Served < 0.999 ? 1.2 : net.Link(NetLink.Upstream).Ratio, Brand.Fiber);
        }
        var online = c.NoNetworkLimit ? inf.RunningRacks : net.OnlineRacks;
        var down = inf.LiveRacks - online;
        SetBar(_bars["res_racks"], down > 0 ? $"{online} / {s.Racks} ⚠" : $"{s.LiveRacks} / {s.Racks}", 0, Brand.Ok);
        _bars["res_racks"].value.AddThemeColorOverride("font_color", down > 0 ? Brand.Critical : Brand.Ink);
        _pue.Text = inf.Pue > 0 ? inf.Pue.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) : "-";
        SetBar(_bars["res_material"], s.MaterialStock.ToString(), 0, Brand.Amber);
        var faults = s.Objects.Count(o => o.Broken && o.Noticed);
        var events = s.ActiveEvents.Count(e => s.Hours < e.EndHour);
        _alert = faults + events > 0;
        _staffStat.Text = s.Staff.Count + (faults > 0 ? $" · ⚠ {faults}" : "") + (s.GridDown ? " · ⚡" : events > 0 ? " · !" : "");
        _staffStat.AddThemeColorOverride("font_color", faults > 0 || s.GridDown ? Brand.Critical : events > 0 ? Brand.Warning : Brand.Ink);
        _repStat.Text = "★ " + Math.Round(s.Reputation).ToString("0") + (s.OpenProposals.Count > 0 ? " · !" : "");
        _repStat.AddThemeColorOverride("font_color", s.Reputation >= 65 ? Brand.Ok : s.Reputation >= 40 ? Brand.Amber : Brand.Critical);
        _money.Text = c.UnlimitedMoney ? "∞" : Ui.Money(s.Money);
        _money.AddThemeColorOverride("font_color", s.Money < 0 && !c.UnlimitedMoney ? Brand.Critical : Brand.Ok);
        var culture = new System.Globalization.CultureInfo(Loc.Language == "nl" ? "nl-NL" : "en-GB");
        _date.Text = s.Date.ToString("ddd d MMM yyyy · HH:mm", culture);
        UpdateBadges(s);
        UpdateTicker(s);
        AvoidOverlap();
    }

    /// <summary>On narrower screens the resource bars reach the camera toolbar: then the right column moves down below them.</summary>
    void AvoidOverlap()
    {
        if (_topRow == null || _rightCol == null) return;
        var clash = _topRow.Position.X + _topRow.Size.X + 8 > _rightCol.Position.X;
        var top = clash ? _topRow.Position.Y + _topRow.Size.Y + 8 : 12;
        if (!Mathf.IsEqualApprox(_rightCol.OffsetTop, top)) _rightCol.OffsetTop = top;
    }

    // ---------------- bottom: build bar ----------------
    /// <summary>Category icon and accent: power = amber, cooling = cyan, fiber = orange, as everywhere.</summary>
    static (string icon, Color accent) CatStyle(string cat) => cat switch
    {
        "cat_land" => ("cat_land", Brand.Ok),
        "cat_security" => ("cat_security", Brand.Critical),
        "cat_buildings" => ("cat_buildings", Brand.Wall),
        "cat_rooms" => ("cat_rooms", Brand.Wall),
        "cat_interior" => ("cat_interior", Brand.Wall),
        "cat_halls" => ("cat_halls", Brand.Ok),
        "cat_power" => ("bolt", Brand.Power),
        "cat_cooling" => ("snow", Brand.Cooling),
        "cat_network" => ("cat_network", Brand.Fiber),
        "cat_staff" => ("crew", Brand.Wall),
        "cat_growth" => ("globe", Brand.Amber),
        _ => ("cat_customers", Brand.Wall),
    };

    const float BarHeight = 66, TrayHeight = 176;
    PanelContainer _tray;
    ScrollContainer _trayScroll;
    Label _trayTitle;

    void BuildLeft()
    {
        // category bar along the bottom edge; the select tool is the first entry
        var bar = new PanelContainer();
        bar.AnchorTop = bar.AnchorBottom = 1;
        bar.OffsetLeft = LeftEdge; bar.OffsetBottom = -12; bar.OffsetTop = -12 - BarHeight;
        bar.GrowVertical = GrowDirection.Begin;
        bar.AddThemeStyleboxOverride("panel", Ui.Box(Brand.Panel, 10, Brand.Line, 1, 6, true));
        AddChild(bar);
        var h = new HBoxContainer(); h.AddThemeConstantOverride("separation", 2);
        bar.AddChild(h);
        _selectBtn = CatButton("tool_select", "select", Brand.Amber, () => Pick(BuildItem.Select));
        h.AddChild(_selectBtn);
        h.AddChild(new VSeparator());
        foreach (var (cat, items) in Categories)
        {
            var (icon, accent) = CatStyle(cat);
            var c = cat;
            var b = CatButton(cat, icon, accent, () => { _category = _category == c ? null : c; RefreshCategory(); });
            if (items.Length == 0) { b.Disabled = true; b.TooltipText = "hud_coming_soon"; }
            _catBtns[cat] = b;
            h.AddChild(b);
        }

        // tray with the items of the open category, just above the bar
        _tray = new PanelContainer();
        _tray.AnchorTop = _tray.AnchorBottom = 1;
        _tray.OffsetLeft = LeftEdge; _tray.OffsetBottom = -12 - BarHeight - 6; _tray.OffsetTop = _tray.OffsetBottom - TrayHeight;
        _tray.OffsetRight = LeftEdge + 1000;
        _tray.GrowVertical = GrowDirection.Begin;
        AddChild(_tray);
        var tv = new VBoxContainer(); tv.AddThemeConstantOverride("separation", 6);
        _tray.AddChild(tv);
        var head = new HBoxContainer(); head.AddThemeConstantOverride("separation", 10);
        _trayTitle = Ui.Heading("", 14, Brand.Ink);
        _trayTitle.VerticalAlignment = VerticalAlignment.Center;
        head.AddChild(_trayTitle);
        _toolHint = Ui.Text("", 13, Brand.InkDim);
        _toolHint.AutowrapMode = TextServer.AutowrapMode.Off; _toolHint.ClipText = true;
        _toolHint.SizeFlagsHorizontal = SizeFlags.ExpandFill; _toolHint.VerticalAlignment = VerticalAlignment.Center;
        _toolHint.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        head.AddChild(_toolHint);
        _floorsRow = new HBoxContainer(); _floorsRow.AddThemeConstantOverride("separation", 6);
        var fl = Ui.Heading("hud_floors", 13); fl.VerticalAlignment = VerticalAlignment.Center;
        _floorsRow.AddChild(fl);
        _floors = new SpinBox { MinValue = 1, MaxValue = Catalog.MaxFloors, Value = 1, CustomMinimumSize = new Vector2(90, 0) };
        _floors.ValueChanged += val => FloorsChanged?.Invoke((int)val);
        _floorsRow.AddChild(_floors);
        head.AddChild(_floorsRow);
        _facadeRow = new HBoxContainer(); _facadeRow.AddThemeConstantOverride("separation", 6);
        var fa = Ui.Heading("hud_facade", 13); fa.VerticalAlignment = VerticalAlignment.Center;
        _facadeRow.AddChild(fa);
        _facade = new OptionButton { CustomMinimumSize = new Vector2(120, 0) };
        _facade.ItemSelected += i => FacadeChanged?.Invoke(OfficeFacades[i]);
        _facadeRow.AddChild(_facade);
        head.AddChild(_facadeRow);
        tv.AddChild(head);
        _trayScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, VerticalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _sub = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; _sub.AddThemeConstantOverride("separation", 6);
        _trayScroll.AddChild(_sub);
        tv.AddChild(_trayScroll);

        // demolish: a big bulldozer button in the bottom-right corner
        _eraseBtn = new Button { Icon = GD.Load<Texture2D>("res://assets/icons/bulldozer.svg"), TooltipText = "tool_erase", ExpandIcon = true, CustomMinimumSize = new Vector2(BarHeight + 12, BarHeight), IconAlignment = HorizontalAlignment.Center };
        _eraseBtn.AnchorLeft = _eraseBtn.AnchorRight = _eraseBtn.AnchorTop = _eraseBtn.AnchorBottom = 1;
        _eraseBtn.OffsetLeft = _eraseBtn.OffsetRight = -12; _eraseBtn.OffsetTop = _eraseBtn.OffsetBottom = -12;
        _eraseBtn.GrowHorizontal = GrowDirection.Begin; _eraseBtn.GrowVertical = GrowDirection.Begin;
        _eraseBtn.AddThemeConstantOverride("icon_max_width", 52);
        _eraseBtn.AddThemeStyleboxOverride("normal", Ui.Box(Brand.Panel, 12, Brand.Line, 1, 10, true));
        _eraseBtn.AddThemeStyleboxOverride("hover", Ui.Box(Brand.PanelHover, 12, Brand.Critical, 1, 10, true));
        _eraseBtn.AddThemeStyleboxOverride("pressed", Ui.Box(new Color(Brand.Critical, 0.35f), 12, Brand.Critical, 2, 10, true));
        _eraseBtn.Pressed += () => Pick(_item.Mode == Mode.Erase ? BuildItem.Select : new BuildItem(Mode.Erase));
        AddChild(_eraseBtn);
        // undo and redo sit just above the bulldozer
        _undoBtn = SmallCorner("undo", "hud_undo", 2, () => Undo?.Invoke());
        _redoBtn = SmallCorner("redo", "hud_redo", 1, () => Redo?.Invoke());
        _planBtn = SmallCorner("blueprint", "hud_plan", 3, () => TogglePlan?.Invoke());
        _planBtn.ToggleMode = true;
        RefreshCategory();
    }

    Button SmallCorner(string icon, string tip, int slot, Action a)
    {
        const float size = 38;
        var b = new Button { Icon = GD.Load<Texture2D>($"res://assets/icons/{icon}.svg"), TooltipText = tip, ExpandIcon = true, CustomMinimumSize = new Vector2(size, size), IconAlignment = HorizontalAlignment.Center };
        b.AnchorLeft = b.AnchorRight = b.AnchorTop = b.AnchorBottom = 1;
        b.OffsetRight = -12 - (slot - 1) * (size + 6); b.OffsetLeft = b.OffsetRight - size;
        b.OffsetBottom = -12 - BarHeight - 8; b.OffsetTop = b.OffsetBottom - size;
        b.AddThemeConstantOverride("icon_max_width", 20);
        b.AddThemeStyleboxOverride("normal", Ui.Box(Brand.Panel, 10, Brand.Line, 1, 6, true));
        b.AddThemeStyleboxOverride("hover", Ui.Box(Brand.PanelHover, 10, Brand.Amber, 1, 6, true));
        b.AddThemeStyleboxOverride("pressed", Ui.Box(new Color(Brand.Amber, 0.3f), 10, Brand.Amber, 1, 6, true));
        b.AddThemeStyleboxOverride("disabled", Ui.Box(new Color(Brand.Panel, 0.6f), 10, Brand.Line, 1, 6, true));
        b.Pressed += a;
        AddChild(b);
        return b;
    }

    /// <summary>Shows whether plan mode is on (the blueprint button lights up in cyan).</summary>
    public void SetPlan(bool on)
    {
        if (_planBtn == null) return;
        _planBtn.SetPressedNoSignal(on);
        _planBtn.AddThemeStyleboxOverride("pressed", Ui.Box(new Color(Brand.Cooling, 0.35f), 10, Brand.Cooling, 2, 6, true));
        _planBtn.AddThemeColorOverride("icon_pressed_color", Brand.Cooling);
    }

    /// <summary>Grey out undo and redo when there is nothing to take back or do again; tooltips name the action.</summary>
    public void SetUndo(string undo, string redo)
    {
        if (_undoBtn == null) return;
        _undoBtn.Disabled = undo == null; _redoBtn.Disabled = redo == null;
        _undoBtn.TooltipText = undo == null ? Loc.T("hud_undo") : Loc.T("hud_undo_what", Loc.T(undo));
        _redoBtn.TooltipText = redo == null ? Loc.T("hud_redo") : Loc.T("hud_redo_what", Loc.T(redo));
    }

    /// <summary>Step 11 tutorial: the build-bar button of a category (to point at it), and opening a category from outside.</summary>
    public Button CategoryButton(string cat) => cat != null && _catBtns.TryGetValue(cat, out var b) ? b : null;
    public void OpenCategory(string cat) { if (cat == null || !_catBtns.ContainsKey(cat)) return; _category = cat; RefreshCategory(); }

    Button CatButton(string key, string icon, Color accent, Action a)
    {
        var b = new Button
        {
            Text = key, Icon = GD.Load<Texture2D>($"res://assets/icons/{icon}.svg"), ExpandIcon = true,
            IconAlignment = HorizontalAlignment.Center, VerticalIconAlignment = VerticalAlignment.Top,
            CustomMinimumSize = new Vector2(80, BarHeight - 12), ClipText = true,
        };
        b.AddThemeConstantOverride("icon_max_width", 22);
        b.AddThemeFontOverride("font", Ui.DisplaySemi); b.AddThemeFontSizeOverride("font_size", 14);
        b.AddThemeColorOverride("icon_normal_color", new Color(accent, 0.85f));
        b.AddThemeColorOverride("icon_hover_color", accent);
        b.AddThemeColorOverride("icon_pressed_color", accent);
        b.SetMeta("accent", accent);
        b.Pressed += a;
        return b;
    }

    /// <summary>Highlight for a category tab: a coloured line along the top in the category's accent.</summary>
    static void ActiveTab(Button b, bool on)
    {
        if (on)
        {
            var accent = b.HasMeta("accent") ? b.GetMeta("accent").AsColor() : Brand.Amber;
            var s = Ui.Box(Brand.PanelHover, 7, null, 0, 6);
            s.BorderColor = accent; s.BorderWidthTop = 3;
            b.AddThemeStyleboxOverride("normal", s);
            b.AddThemeStyleboxOverride("hover", s);
            b.AddThemeColorOverride("font_color", Brand.Ink);
            b.AddThemeColorOverride("icon_normal_color", accent);
        }
        else
        {
            b.RemoveThemeStyleboxOverride("normal"); b.RemoveThemeStyleboxOverride("hover");
            b.RemoveThemeColorOverride("font_color");
            if (b.HasMeta("accent")) b.AddThemeColorOverride("icon_normal_color", new Color(b.GetMeta("accent").AsColor(), 0.85f));
        }
    }

    /// <summary>A card in the tray: name on top, price below, colour swatch for rooms.</summary>
    /// <summary>A card in the tray: a small 3D picture of the item, its name and its price.</summary>
    Button ItemCard(BuildItem it, bool locked)
    {
        var b = new Button { CustomMinimumSize = new Vector2(164, 0), SizeFlagsVertical = SizeFlags.ExpandFill, AutoTranslateMode = AutoTranslateModeEnum.Disabled, ClipText = true, TooltipText = Loc.T(it.NameKey) };
        b.AddThemeStyleboxOverride("normal", Ui.Box(new Color(1, 1, 1, 0.04f), 8, Brand.Line, 1, 8));
        var v = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        v.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        v.OffsetLeft = 6; v.OffsetRight = -6; v.OffsetTop = 4; v.OffsetBottom = -5;
        v.AddThemeConstantOverride("separation", 0);
        // picture on a soft sky tint, so dark models (racks, palisade) stand out
        var pic = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore, SizeFlagsVertical = SizeFlags.ExpandFill };
        pic.AddThemeStyleboxOverride("panel", Ui.Box(new Color(Brand.Sky, 0.14f), 6, null, 0, 0));
        var tex = World.Thumbnails.Instance?.Get(it);
        if (tex != null)
            pic.AddChild(new TextureRect { Texture = tex, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(0, 70) });
        v.AddChild(pic);
        var top = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore }; top.AddThemeConstantOverride("separation", 6);
        if (it.Mode == Mode.Room)
            top.AddChild(new TextureRect { Texture = Swatch(new Color(Catalog.Rooms[(RoomType)it.Arg].Color)), StretchMode = TextureRect.StretchModeEnum.KeepCentered, MouseFilter = MouseFilterEnum.Ignore });
        var name = Ui.Text(Loc.T(it.NameKey), 14, Brand.Ink, Ui.BodySemi);
        name.AutowrapMode = TextServer.AutowrapMode.Off; name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis; name.ClipText = true;
        name.AutoTranslateMode = AutoTranslateModeEnum.Disabled; name.MouseFilter = MouseFilterEnum.Ignore;
        name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        top.AddChild(name);
        v.AddChild(top);
        var cost = CostText(it);
        var price = Ui.Number(cost == "" ? " " : cost, 14);
        price.AddThemeColorOverride("font_color", Brand.Ok);
        price.AutoTranslateMode = AutoTranslateModeEnum.Disabled; price.MouseFilter = MouseFilterEnum.Ignore;
        v.AddChild(price);
        b.AddChild(v);
        if (((it.Mode == Mode.Fence && Catalog.Fences[(FenceType)it.Arg].NeedsResearch && State?.Invoke()?.Done("sec_electric") != true)
             || (it.Mode == Mode.Door && it.Arg == (int)DoorType.Mantrap && State?.Invoke()?.Done("sec_mantrap") != true)) && locked) { b.Disabled = true; b.TooltipText = Loc.T("hud_needs_research"); b.Modulate = new Color(1, 1, 1, 0.5f); }
        var item = it;
        b.Pressed += () => Pick(item);
        if (_item == it)
        {
            var s = Ui.Box(Brand.PanelHover, 8, Brand.Amber, 2, 8);
            b.AddThemeStyleboxOverride("normal", s); b.AddThemeStyleboxOverride("hover", s);
        }
        return b;
    }

    string CostText(BuildItem i) => i.Mode switch
    {
        Mode.Surface or Mode.Curve => Ui.Money(Catalog.Surfaces[(Surface)i.Arg].Cost) + " /m²",
        Mode.Fence => Ui.Money(Catalog.Fences[(FenceType)i.Arg].Cost) + " /m",
        Mode.Building => Ui.Money(Catalog.Buildings[(BuildingKind)i.Arg].CostPerCellPerFloor) + " /m²",
        Mode.Room => "",
        Mode.Door => Ui.Money(Catalog.Doors[(DoorType)i.Arg].Cost),
        Mode.Object or Mode.Row => Ui.Money(Catalog.Objects[(ObjKind)i.Arg].Cost),
        _ => "",
    };

    void RefreshCategory()
    {
        foreach (var c in _sub.GetChildren()) c.QueueFree();
        var locked = State?.Invoke()?.Creative.AllUnlocks != true;
        _tray.Visible = _category != null;
        foreach (var (cat, items) in Categories)
        {
            ActiveTab(_catBtns[cat], cat == _category);
            if (cat != _category) continue;
            _trayTitle.Text = Loc.T(cat);
            _trayTitle.AddThemeColorOverride("font_color", CatStyle(cat).accent);
            if (cat == "cat_network")
            {
                var nv = Ui.Secondary("net_overview", () => OpenNetwork?.Invoke());
                nv.Icon = GD.Load<Texture2D>("res://assets/icons/cat_network.svg");
                nv.AddThemeConstantOverride("icon_max_width", 18);
                nv.CustomMinimumSize = new Vector2(150, 0); nv.SizeFlagsVertical = SizeFlags.ExpandFill;
                _sub.AddChild(nv);
            }
            if (cat == "cat_security")
            {
                var sv = Ui.Secondary("rp_tab_security", () => OpenReputation?.Invoke(ReputationPanel.TabSecurity));
                sv.Icon = GD.Load<Texture2D>("res://assets/icons/shield.svg");
                sv.AddThemeConstantOverride("icon_max_width", 18);
                sv.CustomMinimumSize = new Vector2(150, 0); sv.SizeFlagsVertical = SizeFlags.ExpandFill;
                _sub.AddChild(sv);
            }
            if (cat is "cat_power" or "cat_cooling")
            {
                var ov = Ui.Secondary("power_overview", () => OpenPower?.Invoke());
                ov.Icon = GD.Load<Texture2D>(cat == "cat_power" ? "res://assets/icons/bolt.svg" : "res://assets/icons/snow.svg");
                ov.AddThemeConstantOverride("icon_max_width", 18);
                ov.CustomMinimumSize = new Vector2(150, 0); ov.SizeFlagsVertical = SizeFlags.ExpandFill;
                _sub.AddChild(ov);
            }
            foreach (var it in items) _sub.AddChild(ItemCard(it, locked));
        }
        ActiveTab(_selectBtn, _item.Mode == Mode.Select);
        var erasing = _item.Mode == Mode.Erase;
        _eraseBtn.AddThemeStyleboxOverride("normal", erasing ? Ui.Box(new Color(Brand.Critical, 0.35f), 12, Brand.Critical, 2, 10, true) : Ui.Box(Brand.Panel, 12, Brand.Line, 1, 10, true));
        _eraseBtn.AddThemeColorOverride("icon_normal_color", erasing ? Brand.Ink : Brand.InkDim);
        _floorsRow.Visible = _item.Mode == Mode.Building;
        _facadeRow.Visible = _item.Mode == Mode.Building && _item.Arg == (int)BuildingKind.Office;
        var sel = Math.Max(0, _facade.Selected);
        _facade.Clear();
        foreach (var f in OfficeFacades) _facade.AddItem(Loc.T("facade_" + f.ToString().ToLowerInvariant()));
        _facade.Selected = sel;
        _toolHint.Text = Loc.T(_item.Mode switch
        {
            Mode.Erase => "hint_erase", Mode.Surface => "hint_surface", Mode.Fence => "hint_fence", Mode.Building => "hint_building",
            Mode.Curve => "hint_curve", Mode.Room => "hint_room", Mode.Door => "hint_door", Mode.Row => "hint_row", Mode.Object => "hint_place", _ => "hint_select",
        });
        FitTray();
    }

    /// <summary>The tray spans from the left edge to just before the bulldozer, never wider than its content needs.</summary>
    void FitTray()
    {
        if (_tray == null) return;
        var avail = Size.X - 24;
        _tray.OffsetRight = 12 + Math.Min(avail, 1400);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized) FitTray();
    }

    static ImageTexture Swatch(Color c)
    {
        var img = Image.CreateEmpty(14, 14, false, Image.Format.Rgba8);
        img.Fill(c);
        return ImageTexture.CreateFromImage(img);
    }

    static void Active(Button b, bool on)
    {
        if (on)
        {
            var s = Ui.Box(Brand.PanelHover, 7, null, 0, 8);
            s.BorderColor = Brand.Amber; s.BorderWidthLeft = 3;
            b.AddThemeStyleboxOverride("normal", s);
            b.AddThemeColorOverride("font_color", Brand.Ink);
        }
        else { b.RemoveThemeStyleboxOverride("normal"); b.RemoveThemeColorOverride("font_color"); }
    }

    public void Pick(BuildItem item)
    {
        _item = item;
        if (item.Mode is Mode.Room or Mode.Door) ToggleRoofsIfNeeded();
        RefreshCategory();
        ToolChanged?.Invoke(item);
    }

    void ToggleRoofsIfNeeded() { }

    public void ShowCategory(string cat) { _category = cat; RefreshCategory(); }

    // ---------------- right ----------------
    void BuildRight()
    {
        var col = _rightCol = new VBoxContainer();
        col.AnchorLeft = col.AnchorRight = 1;
        col.OffsetLeft = col.OffsetRight = -12; col.OffsetTop = 12;
        col.GrowHorizontal = GrowDirection.Begin;
        col.AddThemeConstantOverride("separation", 8);
        col.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(col);
        var panel = new PanelContainer();
        col.AddChild(panel);
        var h = new HBoxContainer(); h.AddThemeConstantOverride("separation", 2);
        panel.AddChild(h);
        h.AddChild(Ui.IconButton("rotate_left", "hud_rotate_left", () => RotateLeft?.Invoke()));
        h.AddChild(Ui.IconButton("rotate_right", "hud_rotate_right", () => RotateRight?.Invoke()));
        h.AddChild(Ui.IconButton("grid", "hud_grid", () => ToggleGrid?.Invoke()));
        h.AddChild(Ui.IconButton("walk", "hud_walk", () => Walk?.Invoke()));
        h.AddChild(Ui.IconButton("camera", "hud_photo", () => Photo?.Invoke()));
        _roofBtn = Ui.IconButton("roof", "hud_roofs", () => ToggleRoofs?.Invoke());
        h.AddChild(_roofBtn);
        h.AddChild(Ui.IconButton("floor_down", "hud_floor_down", () => FloorDown?.Invoke()));
        _floorLabel = Ui.Number("", 16); _floorLabel.CustomMinimumSize = new Vector2(44, 0);
        _floorLabel.HorizontalAlignment = HorizontalAlignment.Center; _floorLabel.VerticalAlignment = VerticalAlignment.Center;
        _floorLabel.AutoTranslateMode = AutoTranslateModeEnum.Disabled; _floorLabel.TooltipText = "hud_floor"; _floorLabel.MouseFilter = MouseFilterEnum.Stop;
        h.AddChild(_floorLabel);
        h.AddChild(Ui.IconButton("floor_up", "hud_floor_up", () => FloorUp?.Invoke()));
        _powerBtn = Ui.IconButton("bolt", "hud_overlay_power", () => ToggleOverlay?.Invoke(1));
        _coolBtn = Ui.IconButton("snow", "hud_overlay_cooling", () => ToggleOverlay?.Invoke(2));
        _netBtn = Ui.IconButton("cat_network", "hud_overlay_network", () => ToggleOverlay?.Invoke(3));
        h.AddChild(new VSeparator());
        h.AddChild(_powerBtn); h.AddChild(_coolBtn); h.AddChild(_netBtn);

        // messages sit right under the view controls, newest at the bottom, at most three
        _notes = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, SizeFlagsHorizontal = SizeFlags.ShrinkEnd };
        _notes.AddThemeConstantOverride("separation", 6);
        col.AddChild(_notes);

        // the checklist folds to one line; click to open
        var gp = new PanelContainer { CustomMinimumSize = new Vector2(300, 0), SizeFlagsHorizontal = SizeFlags.ShrinkEnd };
        var gv = new VBoxContainer(); gv.AddThemeConstantOverride("separation", 3);
        _goalsHead = new Button { Alignment = HorizontalAlignment.Left, AutoTranslateMode = AutoTranslateModeEnum.Disabled, CustomMinimumSize = new Vector2(0, 30), TooltipText = "hud_goals_tip" };
        _goalsHead.AddThemeFontOverride("font", Ui.DisplaySemi); _goalsHead.AddThemeFontSizeOverride("font_size", 15);
        _goalsHead.AddThemeStyleboxOverride("normal", Ui.Box(new Color(0, 0, 0, 0), 6, null, 0, 4));
        _goalsHead.Pressed += () => { _goalsOpen = !_goalsOpen; _goalsSig = null; UpdateGoals(_lastGoals); };
        gv.AddChild(_goalsHead);
        _goals = new VBoxContainer(); _goals.AddThemeConstantOverride("separation", 3);
        gv.AddChild(_goals);
        gp.AddChild(gv);
        _goalsPanel = gp;
        col.AddChild(gp);

        _projectsPanel = new PanelContainer { CustomMinimumSize = new Vector2(300, 0), SizeFlagsHorizontal = SizeFlags.ShrinkEnd, Visible = false };
        _projects = new VBoxContainer(); _projects.AddThemeConstantOverride("separation", 3);
        _projectsPanel.AddChild(_projects);
        col.AddChild(_projectsPanel);

        _inspectPanel = new PanelContainer { CustomMinimumSize = new Vector2(300, 0), SizeFlagsHorizontal = SizeFlags.ShrinkEnd, Visible = false };
        _inspect = new VBoxContainer(); _inspect.AddThemeConstantOverride("separation", 3);
        _inspectPanel.AddChild(_inspect);
        col.AddChild(_inspectPanel);
        RefreshLanguage();
        SetView(0, true);
    }

    public void SetView(int floor, bool roofs)
    {
        _floorLabel.Text = floor == 0 ? Loc.T("hud_floor_ground") : Loc.T("hud_floor_n", floor);
        Active(_roofBtn, !roofs);
    }

    public void SetOverlay(int mode)
    {
        Active(_powerBtn, mode == 1);
        Active(_coolBtn, mode == 2);
        Active(_netBtn, mode == 3);
    }

    string _goalsSig;

    /// <summary>Opens or folds the checklist on the right.</summary>
    public void ToggleGoals() { _goalsOpen = !_goalsOpen; _goalsSig = null; UpdateGoals(_lastGoals); }
    Button _goalsHead;
    PanelContainer _goalsPanel;

    public void UpdateGoals(IEnumerable<(string key, bool done)> goals)
    {
        // only rebuild the labels when a goal changed (or the language did)
        var list = goals.ToList();
        _lastGoals = list;
        var sig = string.Join(";", list.Select(g => g.key + (g.done ? "1" : "0"))) + Loc.Language + _goalsOpen;
        if (sig == _goalsSig) return;
        _goalsSig = sig;
        foreach (var c in _goals.GetChildren()) c.QueueFree();
        var doneCount = list.Count(g => g.done);
        // once every goal is done the checklist steps aside; it stays under Overview › Goals
        _goalsPanel.Visible = doneCount < list.Count;
        _goalsHead.Text = Loc.T("goals_title") + "   " + doneCount + " / " + list.Count + "   " + (_goalsOpen ? "▴" : "▾");
        _goals.Visible = _goalsOpen;
        if (!_goalsOpen) return;
        foreach (var (key, done) in list)
        {
            var l = Ui.Text((done ? "✓  " : "○  ") + Loc.T(key), 14, done ? Brand.Ok : Brand.Ink);
            l.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
            _goals.AddChild(l);
        }
    }

    string _projectsSig;

    /// <summary>Building projects in progress, with a button that cycles the priority.</summary>
    public void UpdateProjects(IReadOnlyList<(int id, string nameKey, double progress, int priority, bool waiting)> list)
    {
        var sig = string.Join(";", list.Select(p => $"{p.id}:{(int)(p.progress * 100)}:{p.priority}:{p.waiting}")) + Loc.Language;
        if (sig == _projectsSig) return;
        _projectsSig = sig;
        foreach (var c in _projects.GetChildren()) c.QueueFree();
        _projectsPanel.Visible = list.Count > 0;
        if (list.Count == 0) return;
        _projects.AddChild(Ui.Heading("projects_title", 13));
        foreach (var p in list.Take(8))
        {
            var h = new HBoxContainer(); h.AddThemeConstantOverride("separation", 6);
            var v = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; v.AddThemeConstantOverride("separation", 1);
            var name = Ui.Text(Loc.T(p.nameKey) + (p.waiting ? "  ·  " + Loc.T("projects_waiting") : ""), 14, Brand.Ink);
            name.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
            v.AddChild(name);
            var bar = new ProgressBar { MinValue = 0, MaxValue = 1, Value = p.progress, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 4) };
            v.AddChild(bar);
            h.AddChild(v);
            var prio = new Button { Text = Loc.T("prio_" + p.priority), CustomMinimumSize = new Vector2(78, 28), AutoTranslateMode = AutoTranslateModeEnum.Disabled, TooltipText = Loc.T("projects_priority") };
            prio.AddThemeColorOverride("font_color", p.priority == 3 ? Brand.Amber : p.priority == 1 ? Brand.InkDim : Brand.Ink);
            var id = p.id;
            prio.Pressed += () => CyclePriority?.Invoke(id);
            h.AddChild(prio);
            _projects.AddChild(h);
        }
    }

    string _inspectSig;

    /// <summary>Show the inspector with info lines and optional action buttons (label, action, enabled).</summary>
    public void Inspect(string title, IEnumerable<string> lines, IEnumerable<(string label, Action action, bool enabled)> actions = null)
    {
        var lineList = lines?.ToList() ?? new List<string>();
        var actList = actions?.ToList() ?? new List<(string, Action, bool)>();
        var sig = title + "|" + string.Join("|", lineList) + "|" + string.Join("|", actList.Select(a => a.Item1 + a.Item3));
        if (sig == _inspectSig && _inspectActions.Count == actList.Count)
        {
            for (var i = 0; i < actList.Count; i++) _inspectActions[i] = actList[i].Item2;
            return;
        }
        _inspectSig = sig;
        foreach (var c in _inspect.GetChildren()) c.QueueFree();
        _inspectActions.Clear();
        if (title == null) { _inspectPanel.Visible = false; return; }
        _inspectPanel.Visible = true;
        var t = Ui.Text(title, 17, Brand.Ink, Ui.BodySemi); t.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        _inspect.AddChild(t);
        foreach (var line in lineList)
        {
            var l = Ui.Text(line, 14, line.StartsWith("⚠") ? Brand.Critical : Brand.InkDim); l.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
            _inspect.AddChild(l);
        }
        for (var i = 0; i < actList.Count; i++)
        {
            var (label, act, enabled) = actList[i];
            var idx = i;
            _inspectActions.Add(act);
            var b = Ui.Secondary(label, () => { if (idx < _inspectActions.Count) _inspectActions[idx]?.Invoke(); });
            b.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
            b.Disabled = !enabled;
            b.AddThemeFontSizeOverride("font_size", 14);
            _inspect.AddChild(b);
        }
    }

    readonly List<Action> _inspectActions = new();

    public void RefreshLanguage()
    {
        if (_langBtn != null) _langBtn.Text = Loc.Language == "en" ? "NL" : "EN";
        _goalsSig = null; if (_goalsHead != null) UpdateGoals(_lastGoals);
        for (var i = 0; i < _railBtns.Count; i++) _railBtns[i].TooltipText = Loc.T(Sections.All[i].Key) + " (" + Sections.All[i].Hotkey + ")";
        if (_sub != null) RefreshCategory();
    }

    // ---------------- bottom ----------------
    void BuildBottom()
    {
        var time = new PanelContainer();
        time.AnchorLeft = time.AnchorRight = time.AnchorTop = time.AnchorBottom = 1;
        // time panel sits on the bottom row, between the build bar and the bulldozer
        time.OffsetLeft = time.OffsetRight = -12 - (BarHeight + 12) - 8; time.OffsetTop = time.OffsetBottom = -12;
        time.CustomMinimumSize = new Vector2(0, BarHeight);
        time.GrowHorizontal = GrowDirection.Begin; time.GrowVertical = GrowDirection.Begin;
        AddChild(time);
        var h = new HBoxContainer(); h.AddThemeConstantOverride("separation", 4);
        time.AddChild(h);
        // the clock with the weather underneath, so the panel does not get wider
        var clock = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        clock.AddThemeConstantOverride("separation", -4);
        h.AddChild(clock);
        _date = Ui.Number("", 16); _date.CustomMinimumSize = new Vector2(180, 0); _date.VerticalAlignment = VerticalAlignment.Center;
        _date.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        clock.AddChild(_date);
        _weather = Ui.Text("", 12, Brand.InkDim);
        _weather.AutoTranslateMode = AutoTranslateModeEnum.Disabled; _weather.TooltipText = "hud_weather";
        _weather.MouseFilter = MouseFilterEnum.Stop;
        clock.AddChild(_weather);
        string[] icons = { "pause", "play", "fast", "faster" };
        for (var i = 0; i < 4; i++)
        {
            var speed = i;
            var b = Ui.IconButton(icons[i], $"hud_speed_{i}", () => { SetSpeed(speed); SpeedChanged?.Invoke(speed); });
            _speedBtns.Add(b); h.AddChild(b);
        }
        SetSpeed(1);
    }

    /// <summary>Weather and temperature next to the clock, for example "Rain · 8 °C" (step 10).</summary>
    public void SetWeather(Sim.WeatherNow w, bool weatherOn, Sim.GameState state = null)
    {
        if (_weather == null) return;
        string Key(Sim.WeatherNow x) => x.Kind switch
        {
            Sim.WeatherKind.Clear => x.Daylight < 0.3 ? "wx_clear_night" : "wx_clear",
            Sim.WeatherKind.Cloudy => "wx_cloudy", Sim.WeatherKind.Rain => "wx_rain", Sim.WeatherKind.Storm => "wx_storm",
            Sim.WeatherKind.Snow => "wx_snow", _ => "wx_fog",
        };
        var season = Loc.T("season_" + w.Season.ToString().ToLowerInvariant());
        _weather.Text = !weatherOn ? season : Loc.T(Key(w)) + " · " + Math.Round(w.TempC).ToString("0") + " °C · " + season;
        // the tooltip is the weather report: what the weather does now and the next three days
        var tip = season + " · " + Loc.T("hud_weather");
        if (state != null && weatherOn)
        {
            if (state.WeatherMatters)
            {
                var f = Sim.GameState.CoolingFactorAt(w.TempC);
                tip += "\n" + Loc.T(f < 0.7 ? "wx_effect_cold" : f < 0.9 ? "wx_effect_mild" : f <= 1 ? "wx_effect_warm" : "wx_effect_hot");
                if (w.Kind == Sim.WeatherKind.Storm) tip += "\n" + Loc.T("wx_effect_storm");
                if (w.Kind is Sim.WeatherKind.Snow or Sim.WeatherKind.Fog) tip += "\n" + Loc.T("wx_effect_slow");
            }
            else tip += "\n" + Loc.T("wx_effect_off");
            var culture = new System.Globalization.CultureInfo(Loc.Language == "nl" ? "nl-NL" : "en-GB");
            tip += "\n\n" + Loc.T("hud_forecast");
            foreach (var (day, f) in state.Forecast(3))
                tip += "\n" + day.ToString("ddd d MMM", culture) + ": " + Loc.T(Key(f with { Daylight = 1 })) + ", " + Math.Round(f.TempC).ToString("0") + " °C";
        }
        _weather.TooltipText = tip;
    }

    public void SetSpeed(int s)
    {
        for (var i = 0; i < _speedBtns.Count; i++) Active(_speedBtns[i], i == s);
    }

    public void Notify(string text, Color? accent = null)
    {
        var p = new PanelContainer();
        var s = Ui.Box(Brand.Panel, 8, null, 0, 10);
        s.BorderColor = accent ?? Brand.Amber; s.BorderWidthLeft = 3;
        p.AddThemeStyleboxOverride("panel", s);
        var l = Ui.Text(text, 14); l.CustomMinimumSize = new Vector2(300, 0);
        l.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        p.AddChild(l);
        p.MouseFilter = MouseFilterEnum.Ignore;
        p.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        _notes.AddChild(p);
        History.Insert(0, new HudMessage(_date?.Text ?? "", text, accent ?? Brand.Amber));
        if (History.Count > 200) History.RemoveAt(History.Count - 1);
        World.UiSounds.Instance?.Chime();
        // remove (not only queue) the oldest, or a burst of messages in one frame never leaves this loop
        while (_notes.GetChildCount() > 3) { var old = _notes.GetChild(0); _notes.RemoveChild(old); old.QueueFree(); }
        var tw = CreateTween();
        tw.TweenInterval(7);
        tw.TweenProperty(p, "modulate:a", 0f, 0.6f);
        tw.TweenCallback(Callable.From(p.QueueFree));
    }
}
