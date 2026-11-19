using System;
using System.Collections.Generic;
using System.Linq;
using DatacenterArchitect.Core;
using DatacenterArchitect.Sim;
using Godot;

namespace DatacenterArchitect.UI;

public enum Mode { Select, Erase, Surface, Fence, Building, Room, Door, Object, Row }

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
        Mode.Erase => "tool_erase",
        _ => "tool_select",
    };
}

/// <summary>In-game HUD: build menu left, resource bars top, camera/info and site checklist right, notifications and time bottom.</summary>
public partial class Hud : Control
{
    public Action<BuildItem> ToolChanged;
    public Action<int> SpeedChanged, FloorsChanged;
    public Action<Role, int> CrewChanged;
    public Action<Facade> FacadeChanged;
    public Action<int> CyclePriority;
    public Action RotateLeft, RotateRight, ToggleGrid, ToggleRoofs, FloorUp, FloorDown, OpenInfo, OpenMenu, ToggleLanguage, OpenPower;
    public Action<int> ToggleOverlay;
    public Func<GameState> State;

    readonly Dictionary<string, (Label value, ProgressBar bar)> _bars = new();
    Label _money, _date, _company, _toolHint, _floorLabel, _creative;
    Button _langBtn, _roofBtn, _powerBtn, _coolBtn;
    Label _pue;
    VBoxContainer _notes, _goals, _inspect, _sub;
    PanelContainer _inspectPanel;
    readonly List<Button> _speedBtns = new();
    readonly Dictionary<string, Button> _catBtns = new();
    Button _selectBtn, _eraseBtn;
    HBoxContainer _floorsRow, _facadeRow;
    SpinBox _floors;
    OptionButton _facade;
    PanelContainer _projectsPanel;
    VBoxContainer _projects;
    static readonly Facade[] OfficeFacades = { Facade.Brick, Facade.Concrete, Facade.Glass };
    string _category = "cat_land";
    BuildItem _item = BuildItem.Select;

    static BuildItem O(ObjKind k) => new(Mode.Object, (int)k);

    // Categories follow the brand guide; the build menu sits on the left.
    static readonly (string cat, BuildItem[] items)[] Categories =
    {
        ("cat_land", new[]
        {
            new BuildItem(Mode.Surface, (int)Surface.Asphalt), new BuildItem(Mode.Surface, (int)Surface.Parking),
            new BuildItem(Mode.Surface, (int)Surface.Footpath), new BuildItem(Mode.Surface, (int)Surface.BikePath),
            new BuildItem(Mode.Surface, (int)Surface.Yard), new BuildItem(Mode.Surface, (int)Surface.Grass),
            O(ObjKind.Tree), O(ObjKind.Shrub), O(ObjKind.LampPost), O(ObjKind.Bench), O(ObjKind.BikeRack),
        }),
        ("cat_security", new[]
        {
            new BuildItem(Mode.Fence, (int)FenceType.Mesh), new BuildItem(Mode.Fence, (int)FenceType.Welded),
            new BuildItem(Mode.Fence, (int)FenceType.Palisade), new BuildItem(Mode.Fence, (int)FenceType.Wall),
            new BuildItem(Mode.Fence, (int)FenceType.Electric),
            O(ObjKind.SlidingGate), O(ObjKind.SlidingGateLarge), O(ObjKind.PedGate), O(ObjKind.Barrier), O(ObjKind.GuardBooth), O(ObjKind.Intercom),
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
            O(ObjKind.Sofa), O(ObjKind.Coffee), O(ObjKind.Vending), O(ObjKind.Microwave), O(ObjKind.Terminal),
        }),
        ("cat_halls", new[] { new BuildItem(Mode.Row, (int)ObjKind.Rack), O(ObjKind.Crah), O(ObjKind.Pdu), O(ObjKind.CrashCart) }),
        ("cat_power", new[]
        {
            O(ObjKind.Transformer), O(ObjKind.Switchgear), O(ObjKind.Ups), O(ObjKind.Battery), O(ObjKind.Pdu),
            O(ObjKind.Generator), O(ObjKind.DieselTank), O(ObjKind.UndergroundTank),
        }),
        ("cat_cooling", new[] { O(ObjKind.Crah), O(ObjKind.Chiller), O(ObjKind.CoolingTower) }),
        ("cat_network", new[] { O(ObjKind.FiberPit) }),
        ("cat_staff", Array.Empty<BuildItem>()),
        ("cat_customers", Array.Empty<BuildItem>()),
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
        var top = new HBoxContainer { Position = new Vector2(12, 12) };
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
        var sh = new HBoxContainer(); sh.AddThemeConstantOverride("separation", 18);
        stats.AddChild(sh);
        AddBar(sh, "res_power", Brand.Power);
        AddBar(sh, "res_backup", Brand.Power);
        AddBar(sh, "res_cooling", Brand.Cooling);
        AddBar(sh, "res_racks", Brand.Ok, false);
        AddBar(sh, "res_material", Brand.Amber, false);
        var pv = new VBoxContainer { CustomMinimumSize = new Vector2(56, 0) }; pv.AddThemeConstantOverride("separation", 2);
        pv.AddChild(Ui.Heading("res_pue", 12));
        _pue = Ui.Number("-", 18); _pue.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        pv.AddChild(_pue);
        sh.AddChild(pv);
        var mv = new VBoxContainer(); mv.AddThemeConstantOverride("separation", 2);
        mv.AddChild(Ui.Heading("res_money", 12));
        _money = Ui.Number("€ 0", 20); _money.AddThemeColorOverride("font_color", Brand.Ok);
        _money.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        mv.AddChild(_money);
        sh.AddChild(mv);
        top.AddChild(stats);
    }

    void AddBar(HBoxContainer parent, string key, Color c, bool bar = true)
    {
        var v = new VBoxContainer { CustomMinimumSize = new Vector2(bar ? 130 : 80, 0) };
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
        var down = inf.LiveRacks - inf.RunningRacks;
        SetBar(_bars["res_racks"], down > 0 ? $"{inf.RunningRacks} / {s.Racks} ⚠" : $"{s.LiveRacks} / {s.Racks}", 0, Brand.Ok);
        _bars["res_racks"].value.AddThemeColorOverride("font_color", down > 0 ? Brand.Critical : Brand.Ink);
        _pue.Text = inf.Pue > 0 ? inf.Pue.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) : "-";
        SetBar(_bars["res_material"], s.MaterialStock.ToString(), 0, Brand.Amber);
        _money.Text = c.UnlimitedMoney ? "∞" : Ui.Money(s.Money);
        _money.AddThemeColorOverride("font_color", s.Money < 0 && !c.UnlimitedMoney ? Brand.Critical : Brand.Ok);
        var culture = new System.Globalization.CultureInfo(Loc.Language == "nl" ? "nl-NL" : "en-GB");
        _date.Text = s.Date.ToString("ddd d MMM yyyy · HH:mm", culture);
        if (_category == "cat_staff") RefreshCrewValues(s);
    }

    // ---------------- left: build menu ----------------
    void BuildLeft()
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(286, 0) };
        panel.AnchorTop = 0; panel.AnchorBottom = 1;
        panel.OffsetLeft = 12; panel.OffsetRight = 298; panel.OffsetTop = 96; panel.OffsetBottom = -70;
        AddChild(panel);
        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 4);
        panel.AddChild(v);
        v.AddChild(Ui.Heading("hud_build"));
        var tools = new HBoxContainer(); tools.AddThemeConstantOverride("separation", 4);
        _selectBtn = ToolButton("tool_select", "select", () => Pick(BuildItem.Select));
        _eraseBtn = ToolButton("tool_erase", "erase", () => Pick(new BuildItem(Mode.Erase)));
        tools.AddChild(_selectBtn); tools.AddChild(_eraseBtn);
        v.AddChild(tools);
        v.AddChild(new HSeparator());
        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 4); grid.AddThemeConstantOverride("v_separation", 2);
        foreach (var (cat, items) in Categories)
        {
            var b = new Button { Text = cat, Alignment = HorizontalAlignment.Left, CustomMinimumSize = new Vector2(0, 30), SizeFlagsHorizontal = SizeFlags.ExpandFill, ClipText = true };
            b.AddThemeFontOverride("font", Ui.DisplaySemi); b.AddThemeFontSizeOverride("font_size", 16);
            if (items.Length == 0 && cat != "cat_staff") { b.Disabled = true; b.TooltipText = "hud_coming_soon"; }
            var c = cat;
            b.Pressed += () => { _category = c; RefreshCategory(); };
            _catBtns[cat] = b;
            grid.AddChild(b);
        }
        v.AddChild(grid);
        v.AddChild(new HSeparator());
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _sub = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; _sub.AddThemeConstantOverride("separation", 1);
        scroll.AddChild(_sub);
        v.AddChild(scroll);
        _floorsRow = new HBoxContainer(); _floorsRow.AddThemeConstantOverride("separation", 6);
        var fl = Ui.Heading("hud_floors", 13); fl.SizeFlagsHorizontal = SizeFlags.ExpandFill; fl.VerticalAlignment = VerticalAlignment.Center;
        _floorsRow.AddChild(fl);
        _floors = new SpinBox { MinValue = 1, MaxValue = Catalog.MaxFloors, Value = 1, CustomMinimumSize = new Vector2(90, 0) };
        _floors.ValueChanged += val => FloorsChanged?.Invoke((int)val);
        _floorsRow.AddChild(_floors);
        v.AddChild(_floorsRow);
        _facadeRow = new HBoxContainer(); _facadeRow.AddThemeConstantOverride("separation", 6);
        var fa = Ui.Heading("hud_facade", 13); fa.SizeFlagsHorizontal = SizeFlags.ExpandFill; fa.VerticalAlignment = VerticalAlignment.Center;
        _facadeRow.AddChild(fa);
        _facade = new OptionButton { CustomMinimumSize = new Vector2(120, 0) };
        _facade.ItemSelected += i => FacadeChanged?.Invoke(OfficeFacades[i]);
        _facadeRow.AddChild(_facade);
        v.AddChild(_facadeRow);
        _toolHint = Ui.Text("", 13, Brand.InkDim);
        _toolHint.CustomMinimumSize = new Vector2(256, 0);
        _toolHint.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        v.AddChild(_toolHint);
        RefreshCategory();
    }

    Button ToolButton(string key, string icon, Action a)
    {
        var b = new Button { Text = key, Icon = GD.Load<Texture2D>($"res://assets/icons/{icon}.svg"), ExpandIcon = false, SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = HorizontalAlignment.Left };
        b.AddThemeConstantOverride("icon_max_width", 18);
        b.Pressed += a;
        return b;
    }

    string CostText(BuildItem i) => i.Mode switch
    {
        Mode.Surface => Ui.Money(Catalog.Surfaces[(Surface)i.Arg].Cost) + " /m²",
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
        foreach (var (cat, items) in Categories)
        {
            Active(_catBtns[cat], cat == _category);
            if (cat != _category) continue;
            if (cat == "cat_staff") { BuildCrew(); continue; }
            if (cat is "cat_power" or "cat_cooling")
            {
                var ov = Ui.Secondary("power_overview", () => OpenPower?.Invoke());
                ov.Icon = GD.Load<Texture2D>(cat == "cat_power" ? "res://assets/icons/bolt.svg" : "res://assets/icons/snow.svg");
                ov.AddThemeConstantOverride("icon_max_width", 18);
                _sub.AddChild(ov);
            }
            foreach (var it in items)
            {
                var row = new Button { Alignment = HorizontalAlignment.Left, CustomMinimumSize = new Vector2(0, 30), AutoTranslateMode = AutoTranslateModeEnum.Disabled, ClipText = true };
                var cost = CostText(it);
                row.Text = Loc.T(it.NameKey) + (cost == "" ? "" : "   " + cost);
                if (it.Mode == Mode.Room) row.Icon = Swatch(new Color(Catalog.Rooms[(RoomType)it.Arg].Color));
                if (it.Mode == Mode.Fence && Catalog.Fences[(FenceType)it.Arg].NeedsResearch && locked) { row.Disabled = true; row.TooltipText = Loc.T("hud_needs_research"); }
                var item = it;
                row.Pressed += () => Pick(item);
                Active(row, _item == it);
                _sub.AddChild(row);
            }
        }
        Active(_selectBtn, _item.Mode == Mode.Select);
        Active(_eraseBtn, _item.Mode == Mode.Erase);
        _floorsRow.Visible = _item.Mode == Mode.Building;
        _facadeRow.Visible = _item.Mode == Mode.Building && _item.Arg == (int)BuildingKind.Office;
        var sel = Math.Max(0, _facade.Selected);
        _facade.Clear();
        foreach (var f in OfficeFacades) _facade.AddItem(Loc.T("facade_" + f.ToString().ToLowerInvariant()));
        _facade.Selected = sel;
        _toolHint.Text = Loc.T(_item.Mode switch
        {
            Mode.Erase => "hint_erase", Mode.Surface => "hint_surface", Mode.Fence => "hint_fence", Mode.Building => "hint_building",
            Mode.Room => "hint_room", Mode.Door => "hint_door", Mode.Row => "hint_row", Mode.Object => "hint_place", _ => "hint_select",
        });
    }

    static ImageTexture Swatch(Color c)
    {
        var img = Image.CreateEmpty(14, 14, false, Image.Format.Rgba8);
        img.Fill(c);
        return ImageTexture.CreateFromImage(img);
    }

    // ---------------- crew ----------------
    readonly Dictionary<Role, Label> _crewLabels = new();

    void BuildCrew()
    {
        _crewLabels.Clear();
        _sub.AddChild(Ui.Text("crew_help", 13, Brand.InkDim));
        foreach (var role in new[] { Role.Builder, Role.Fitter, Role.Engineer })
        {
            var box = new VBoxContainer(); box.AddThemeConstantOverride("separation", 0);
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
        var col = new VBoxContainer();
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
        h.AddChild(_powerBtn); h.AddChild(_coolBtn);
        _langBtn = new Button { CustomMinimumSize = new Vector2(44, 38), TooltipText = "hud_language", AutoTranslateMode = AutoTranslateModeEnum.Disabled };
        _langBtn.AddThemeFontOverride("font", Ui.DisplayBold);
        _langBtn.Pressed += () => ToggleLanguage?.Invoke();
        h.AddChild(_langBtn);
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
    }

    public void UpdateGoals(IEnumerable<(string key, bool done)> goals)
    {
        foreach (var c in _goals.GetChildren()) c.QueueFree();
        _goals.AddChild(Ui.Heading("goals_title", 13));
        foreach (var (key, done) in goals)
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
        _notes.OffsetTop = _notes.OffsetBottom = -12; _notes.OffsetLeft = 312;
        _notes.GrowVertical = GrowDirection.Begin;
        _notes.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_notes);

        var time = new PanelContainer();
        time.AnchorLeft = time.AnchorRight = time.AnchorTop = time.AnchorBottom = 1;
        time.OffsetLeft = time.OffsetRight = -12; time.OffsetTop = time.OffsetBottom = -12;
        time.GrowHorizontal = GrowDirection.Begin; time.GrowVertical = GrowDirection.Begin;
        AddChild(time);
        var h = new HBoxContainer(); h.AddThemeConstantOverride("separation", 4);
        time.AddChild(h);
        _date = Ui.Number("", 17); _date.CustomMinimumSize = new Vector2(200, 0); _date.VerticalAlignment = VerticalAlignment.Center;
        _date.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        h.AddChild(_date);
        string[] icons = { "pause", "play", "fast", "faster" };
        for (var i = 0; i < 4; i++)
        {
            var speed = i;
            var b = Ui.IconButton(icons[i], $"hud_speed_{i}", () => { SetSpeed(speed); SpeedChanged?.Invoke(speed); });
            _speedBtns.Add(b); h.AddChild(b);
        }
        SetSpeed(1);
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
        while (_notes.GetChildCount() > 4) _notes.GetChild(0).QueueFree();
        var tw = CreateTween();
        tw.TweenInterval(7);
        tw.TweenProperty(p, "modulate:a", 0f, 0.6f);
        tw.TweenCallback(Callable.From(p.QueueFree));
    }
}
