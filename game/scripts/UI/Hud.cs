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
    public Action<Role, int> CrewChanged;
    public Action<Facade> FacadeChanged;
    public Action<int> CyclePriority;
    public Action Walk;
    public Action RotateLeft, RotateRight, ToggleGrid, ToggleRoofs, FloorUp, FloorDown, OpenInfo, OpenOptions, OpenMenu, ToggleLanguage, OpenPower, OpenNetwork;
    public Action<int> ToggleOverlay, OpenCompany, OpenStaff, OpenReputation, OpenGrowth;
    public Func<GameState> State;

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
    Button _selectBtn, _eraseBtn;
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
            new BuildItem(Mode.Building, (int)BuildingKind.Warehouse), O(ObjKind.Lift),
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
        ("cat_staff", Array.Empty<BuildItem>()),
        ("cat_customers", Array.Empty<BuildItem>()),
        ("cat_growth", Array.Empty<BuildItem>()),
    };

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        BuildTop(); BuildLeft(); BuildRight(); BuildBottom();
    }

    // ---------------- top ----------------
    void BuildTop()
    {
        var top = _topRow = new HBoxContainer { Position = new Vector2(12, 12) };
        top.AddThemeConstantOverride("separation", 10);
        AddChild(top);
        var brand = new PanelContainer();
        var bh = new HBoxContainer(); bh.AddThemeConstantOverride("separation", 10);
        bh.AddChild(new TextureRect { Texture = GD.Load<Texture2D>("res://icon.svg"), CustomMinimumSize = new Vector2(34, 34), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered });
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
        AddBar(sh, "res_power", Brand.Power);
        AddBar(sh, "res_backup", Brand.Power);
        AddBar(sh, "res_cooling", Brand.Cooling);
        AddBar(sh, "res_network", Brand.Fiber);
        AddBar(sh, "res_racks", Brand.Ok, false);
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
        _ticker = new PanelContainer { Position = new Vector2(12, 0), MouseFilter = MouseFilterEnum.Stop, TooltipText = "hud_news_tip", MouseDefaultCursorShape = CursorShape.PointingHand, Visible = false };
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
        _ticker.Position = new Vector2(12, _topRow.Position.Y + _topRow.Size.Y + 6);
        _tickerClip.CustomMinimumSize = new Vector2(Math.Max(320, _topRow.Size.X - 90), 20);
        var sig = Loc.Language + s.News.Count + s.News[0].Day + s.News[0].Key;
        if (sig == _tickerSig) return;
        _tickerSig = sig;
        _tickerText.Text = string.Join("     ◆     ", s.News.Take(6).Select(GrowthPanel.NewsText));
    }

    void AddBar(HBoxContainer parent, string key, Color c, bool bar = true)
    {
        var v = new VBoxContainer { CustomMinimumSize = new Vector2(bar ? 118 : 72, 0) };
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
        if (_category == "cat_staff") RefreshCrewValues(s);
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
    Label _trayTitle;

    void BuildLeft()
    {
        // category bar along the bottom edge; the select tool is the first entry
        var bar = new PanelContainer();
        bar.AnchorTop = bar.AnchorBottom = 1;
        bar.OffsetLeft = 12; bar.OffsetBottom = -12; bar.OffsetTop = -12 - BarHeight;
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
            if (items.Length == 0 && cat is not ("cat_staff" or "cat_customers" or "cat_growth")) { b.Disabled = true; b.TooltipText = "hud_coming_soon"; }
            _catBtns[cat] = b;
            h.AddChild(b);
        }

        // tray with the items of the open category, just above the bar
        _tray = new PanelContainer();
        _tray.AnchorTop = _tray.AnchorBottom = 1;
        _tray.OffsetLeft = 12; _tray.OffsetBottom = -12 - BarHeight - 6; _tray.OffsetTop = _tray.OffsetBottom - TrayHeight;
        _tray.OffsetRight = 12 + 1000;
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
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, VerticalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _sub = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; _sub.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(_sub);
        tv.AddChild(scroll);

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
        RefreshCategory();
    }

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
            if (cat == "cat_staff")
            {
                _sub.AddChild(TrayButton("st_tab_team", "crew", () => OpenStaff?.Invoke(StaffPanel.TabTeam)));
                _sub.AddChild(TrayButton("st_tab_policy", "roster", () => OpenStaff?.Invoke(StaffPanel.TabPolicy)));
                _sub.AddChild(TrayButton("st_tab_maintenance", "wrench", () => OpenStaff?.Invoke(StaffPanel.TabMaintenance)));
                _sub.AddChild(TrayButton("st_tab_incidents", "alert", () => OpenStaff?.Invoke(StaffPanel.TabIncidents)));
                _sub.AddChild(TrayButton("co_tab_org", "org", () => OpenCompany?.Invoke(CompanyPanel.TabOrg)));
                BuildCrew(); continue;
            }
            if (cat == "cat_customers")
            {
                _sub.AddChild(TrayButton("co_tab_sales", "cat_customers", () => OpenCompany?.Invoke(CompanyPanel.TabSales)));
                _sub.AddChild(TrayButton("co_tab_contracts", "contract", () => OpenCompany?.Invoke(CompanyPanel.TabContracts)));
                _sub.AddChild(TrayButton("co_tab_tenders", "tender", () => OpenCompany?.Invoke(CompanyPanel.TabTenders)));
                _sub.AddChild(TrayButton("co_tab_finance", "money", () => OpenCompany?.Invoke(CompanyPanel.TabFinance)));
                _sub.AddChild(TrayButton("co_tab_org", "org", () => OpenCompany?.Invoke(CompanyPanel.TabOrg)));
                _sub.AddChild(TrayButton("rp_tab_reputation", "star", () => OpenReputation?.Invoke(ReputationPanel.TabReputation)));
                _sub.AddChild(TrayButton("rp_tab_research", "flask", () => OpenReputation?.Invoke(ReputationPanel.TabResearch)));
                var s = State?.Invoke();
                if (s != null)
                {
                    var help = Ui.Text(Loc.T("customers_tray", s.Deals.Count(d => d.State == DealState.Lead), s.Deals.Count(d => d.State == DealState.Active), Ui.Money(s.MonthlyRevenue)), 13, Brand.InkDim);
                    help.AutoTranslateMode = AutoTranslateModeEnum.Disabled; help.CustomMinimumSize = new Vector2(240, 0);
                    _sub.AddChild(help);
                }
                continue;
            }
            if (cat == "cat_growth")
            {
                string[] keys = { "gr_tab_locations", "gr_tab_offices", "gr_tab_hosting", "gr_tab_market", "gr_tab_energy", "gr_tab_risks", "gr_tab_people" };
                string[] icons = { "globe", "office", "cloud", "chart", "leaf", "shield", "key" };
                for (var i = 0; i < keys.Length; i++) { var tab = i; _sub.AddChild(TrayButton(keys[i], icons[i], () => OpenGrowth?.Invoke(tab))); }
                continue;
            }
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

    Button TrayButton(string key, string icon, Action a)
    {
        var b = Ui.Secondary(key, a);
        b.Icon = GD.Load<Texture2D>($"res://assets/icons/{icon}.svg");
        b.AddThemeConstantOverride("icon_max_width", 20);
        b.IconAlignment = HorizontalAlignment.Center; b.VerticalIconAlignment = VerticalAlignment.Top;
        b.CustomMinimumSize = new Vector2(140, 0); b.SizeFlagsVertical = SizeFlags.ExpandFill;
        b.AddThemeFontOverride("font", Ui.DisplaySemi); b.AddThemeFontSizeOverride("font_size", 17);
        return b;
    }

    // ---------------- crew ----------------
    readonly Dictionary<Role, Label> _crewLabels = new();

    void BuildCrew()
    {
        _crewLabels.Clear();
        var help = Ui.Text("crew_help", 13, Brand.InkDim); help.CustomMinimumSize = new Vector2(260, 0);
        _sub.AddChild(help);
        foreach (var role in new[] { Role.Builder, Role.Fitter, Role.Engineer })
        {
            var box = new VBoxContainer { CustomMinimumSize = new Vector2(190, 0) }; box.AddThemeConstantOverride("separation", 0);
            var h = new HBoxContainer(); h.AddThemeConstantOverride("separation", 4);
            var name = Ui.Text("crew_" + role.ToString().ToLowerInvariant(), 15, Brand.Ink, Ui.BodySemi);
            name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            h.AddChild(name);
            var r = role;
            var minus = new Button { Text = "−", CustomMinimumSize = new Vector2(30, 28), AutoTranslateMode = AutoTranslateModeEnum.Disabled };
            minus.Pressed += () => CrewChanged?.Invoke(r, -1);
            var plus = new Button { Text = "+", CustomMinimumSize = new Vector2(30, 28), AutoTranslateMode = AutoTranslateModeEnum.Disabled };
            plus.Pressed += () => CrewChanged?.Invoke(r, 1);
            var count = Ui.Number("0", 18); count.CustomMinimumSize = new Vector2(28, 0); count.HorizontalAlignment = HorizontalAlignment.Center;
            count.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
            h.AddChild(minus); h.AddChild(count); h.AddChild(plus);
            box.AddChild(h);
            var wage = Ui.Text(Ui.Money(Catalog.WagePerDay[role]) + " " + Loc.T("crew_per_day"), 12, Brand.InkDim);
            wage.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
            box.AddChild(wage);
            _sub.AddChild(box);
            _crewLabels[role] = count;
        }
        var s = State?.Invoke();
        if (s != null) RefreshCrewValues(s);
    }

    void RefreshCrewValues(GameState s)
    {
        if (_crewLabels.TryGetValue(Role.Builder, out var b)) b.Text = s.Builders.ToString();
        if (_crewLabels.TryGetValue(Role.Fitter, out var f)) f.Text = s.Fitters.ToString();
        if (_crewLabels.TryGetValue(Role.Engineer, out var e)) e.Text = s.Engineers.ToString();
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
        h.AddChild(_powerBtn); h.AddChild(_coolBtn); h.AddChild(_netBtn);
        _langBtn = new Button { CustomMinimumSize = new Vector2(44, 38), TooltipText = "hud_language", AutoTranslateMode = AutoTranslateModeEnum.Disabled };
        _langBtn.AddThemeFontOverride("font", Ui.DisplayBold);
        _langBtn.Pressed += () => ToggleLanguage?.Invoke();
        h.AddChild(_langBtn);
        h.AddChild(Ui.IconButton("org", "hud_company", () => OpenCompany?.Invoke(CompanyPanel.TabOrg)));
        h.AddChild(Ui.IconButton("crew", "hud_staff", () => OpenStaff?.Invoke(StaffPanel.TabTeam)));
        h.AddChild(Ui.IconButton("star", "hud_reputation", () => OpenReputation?.Invoke(ReputationPanel.TabReputation)));
        h.AddChild(Ui.IconButton("flask", "hud_research", () => OpenReputation?.Invoke(ReputationPanel.TabResearch)));
        h.AddChild(Ui.IconButton("globe", "hud_growth", () => OpenGrowth?.Invoke(GrowthPanel.TabLocations)));
        h.AddChild(Ui.IconButton("settings", "hud_options", () => OpenOptions?.Invoke()));
        h.AddChild(Ui.IconButton("info", "hud_info", () => OpenInfo?.Invoke()));
        h.AddChild(Ui.IconButton("menu", "hud_menu", () => OpenMenu?.Invoke()));

        var gp = new PanelContainer { CustomMinimumSize = new Vector2(300, 0), SizeFlagsHorizontal = SizeFlags.ShrinkEnd };
        _goals = new VBoxContainer(); _goals.AddThemeConstantOverride("separation", 3);
        gp.AddChild(_goals);
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

    public void UpdateGoals(IEnumerable<(string key, bool done)> goals)
    {
        // only rebuild the labels when a goal changed (or the language did)
        var list = goals.ToList();
        var sig = string.Join(";", list.Select(g => g.key + (g.done ? "1" : "0"))) + Loc.Language;
        if (sig == _goalsSig) return;
        _goalsSig = sig;
        foreach (var c in _goals.GetChildren()) c.QueueFree();
        _goals.AddChild(Ui.Heading("goals_title", 13));
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
        _langBtn.Text = Loc.Language == "en" ? "NL" : "EN";
        if (_sub != null) RefreshCategory();
    }

    // ---------------- bottom ----------------
    void BuildBottom()
    {
        _notes = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        _notes.AnchorTop = _notes.AnchorBottom = 1;
        _notes.OffsetTop = _notes.OffsetBottom = -12 - BarHeight - 6 - TrayHeight - 10; _notes.OffsetLeft = 12;
        _notes.GrowVertical = GrowDirection.Begin;
        _notes.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_notes);

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
        var l = Ui.Text(text, 14); l.CustomMinimumSize = new Vector2(340, 0);
        l.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        p.AddChild(l);
        p.MouseFilter = MouseFilterEnum.Ignore;
        p.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        _notes.AddChild(p);
        World.UiSounds.Instance?.Chime();
        // remove (not only queue) the oldest, or a burst of more than four messages in one frame never leaves this loop
        while (_notes.GetChildCount() > 4) { var old = _notes.GetChild(0); _notes.RemoveChild(old); old.QueueFree(); }
        var tw = CreateTween();
        tw.TweenInterval(7);
        tw.TweenProperty(p, "modulate:a", 0f, 0.6f);
        tw.TweenCallback(Callable.From(p.QueueFree));
    }
}
