using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace DatacenterArchitect.Sim;

public class Building
{
    public int Id { get; set; }
    public BuildingKind Kind { get; set; }
    public int X { get; set; }
    public int Z { get; set; }
    public int W { get; set; }
    public int D { get; set; }
    public int Floors { get; set; } = 1;
    /// <summary>A basement under the ground floor (floor id <see cref="GameState.BasementFloor"/>), a design choice when building.</summary>
    public bool Basement { get; set; }
    public Facade Facade { get; set; }
    /// <summary>1 = low, 2 = normal, 3 = high. Builders work on higher priority projects first.</summary>
    public int Priority { get; set; } = 2;
    public Stage Stage { get; set; }
    /// <summary>Material delivered to the site for the current stage.</summary>
    public int Material { get; set; }
    /// <summary>Builder-hours spent on the current stage.</summary>
    public double Work { get; set; }
    /// <summary>A floor is being added on top of the finished building: hours left and the total, for progress.</summary>
    public bool Raising { get; set; }
    public double RaiseLeft { get; set; }
    public double RaiseHours { get; set; }
    public bool Contains(int x, int z) => x >= X && z >= Z && x < X + W && z < Z + D;
    /// <summary>Whether the building has this floor (the basement included).</summary>
    public bool HasFloor(int f) => f == GameState.BasementFloor ? Basement : f >= 0 && f < Floors;
    /// <summary>Storeys including the basement.</summary>
    [System.Text.Json.Serialization.JsonIgnore] public int Levels => Floors + (Basement ? 1 : 0);
}

public class Room
{
    public int Id { get; set; }
    public int BuildingId { get; set; }
    public int Floor { get; set; }
    public RoomType Type { get; set; }
    public int X { get; set; }
    public int Z { get; set; }
    public int W { get; set; }
    public int D { get; set; }
    /// <summary>Walls while the inner walls are being built, Done when finished.</summary>
    public Stage Stage { get; set; } = Stage.Walls;
    /// <summary>Office rooms: interior style (automatic unless the player picks one).</summary>
    public InteriorStyle Style { get; set; }
    public int Priority { get; set; } = 2;
    public int Material { get; set; }
    public double Work { get; set; }
    /// <summary>Data halls: air management level (open, hot/cold aisles, cold corridor).</summary>
    public Containment Containment { get; set; }
    /// <summary>Data halls: overhead busbar with an A and a B run.</summary>
    public bool Busbar { get; set; }
    /// <summary>Upgrade in progress: 1 = next containment level, 2 = busbar. Hours left in UpgradeLeft.</summary>
    public int Upgrade { get; set; }
    public double UpgradeLeft { get; set; }
    /// <summary>Data halls: air temperature in °C, and whether racks are shut down to protect them from heat.</summary>
    public double Temp { get; set; } = 21;
    public bool Hot { get; set; }
    /// <summary>Data halls: what the hall is sold as (full racks, per U, cages, a private hall).</summary>
    public HallZone Zone { get; set; }
    /// <summary>Data halls: the customer types hosted here. Empty means every type is welcome.</summary>
    public List<CustomerType> Clients { get; set; } = new();
    /// <summary>Data halls: fixed number for the code DH-01, DH-02 ... (given once, kept when other halls go).</summary>
    public int HallNo { get; set; }
    /// <summary>Data halls: colour band (index in GameState.HallBands, -1 = one per hall number).</summary>
    public int Band { get; set; } = -1;
    /// <summary>Data halls: show the logo of a customer who rents the whole hall on its wall.</summary>
    public bool ShowLogo { get; set; } = true;
    /// <summary>
    /// True for a room drawn as a rectangle with its own walls around it (the "room with walls" tool and every room
    /// from before free walls). False for a room painted on the floor tiles of a space closed by separate walls.
    /// </summary>
    public bool Walled { get; set; } = true;
    /// <summary>Painted rooms: the floor cells (<see cref="Key"/>). Empty means the whole X/Z/W/D rectangle.</summary>
    public List<int> Cells { get; set; } = new();
    /// <summary>Optional name the player gave the room.</summary>
    public string Name { get; set; }

    HashSet<int> _set;
    public static int Key(int x, int z) => z * 4096 + x;
    public static (int x, int z) Unkey(int k) => (k % 4096, k / 4096);

    public bool Contains(int x, int z)
    {
        if (x < X || z < Z || x >= X + W || z >= Z + D) return false;
        if (Cells.Count == 0) return true;
        _set ??= Cells.ToHashSet();
        return _set.Contains(Key(x, z));
    }

    /// <summary>Floor area in m² (cells).</summary>
    [JsonIgnore] public int Area => Cells.Count == 0 ? W * D : Cells.Count;

    /// <summary>Every floor cell of the room.</summary>
    public IEnumerable<(int x, int z)> CellList()
    {
        if (Cells.Count > 0) { foreach (var k in Cells) yield return Unkey(k); yield break; }
        for (var z = Z; z < Z + D; z++)
            for (var x = X; x < X + W; x++) yield return (x, z);
    }

    /// <summary>Set the floor cells and the bounding box around them.</summary>
    public void SetCells(IEnumerable<(int x, int z)> cells)
    {
        var list = cells.Distinct().ToList();
        Cells = list.Select(c => Key(c.x, c.z)).OrderBy(k => k).ToList();
        _set = null;
        if (list.Count == 0) { W = D = 0; return; }
        X = list.Min(c => c.x); Z = list.Min(c => c.z);
        W = list.Max(c => c.x) - X + 1; D = list.Max(c => c.z) - Z + 1;
    }

    /// <summary>A copy (lists copied too), so a change can be undone by swapping the old object back.</summary>
    public Room Copy()
    {
        var r = (Room)MemberwiseClone();
        r.Clients = new List<CustomerType>(Clients);
        r.Cells = new List<int>(Cells);
        r._set = null;
        return r;
    }
}

/// <summary>A door sits on wall edges. Dir 0 = the wall line x = X running along z; dir 1 = the line z = Z running along x.</summary>
public class Door
{
    public int Id { get; set; }
    public int Floor { get; set; }
    public int X { get; set; }
    public int Z { get; set; }
    public int Dir { get; set; }
    public DoorType Type { get; set; }
    public IEnumerable<(int x, int z, int dir)> Edges()
    {
        var w = Catalog.Doors[Type].Width;
        for (var i = 0; i < w; i++) yield return Dir == 0 ? (X, Z + i, 0) : (X + i, Z, 1);
    }
}

public class PlacedObject
{
    public int Id { get; set; }
    public ObjKind Kind { get; set; }
    public int X { get; set; }
    public int Z { get; set; }
    public int Rot { get; set; }
    public int Floor { get; set; }
    public ObjStatus Status { get; set; } = ObjStatus.Live;
    /// <summary>Upgrade level for CRAH units, chillers and cooling towers (1 = as bought).</summary>
    public int Level { get; set; } = 1;
    /// <summary>Loading dock room where a delivered item waits.</summary>
    public int Dock { get; set; }
    /// <summary>Step 7: wear (0..100) raises the chance of a fault; a broken object stops working until it is repaired.</summary>
    public double Wear { get; set; }
    public bool Broken { get; set; }
    public double BrokenHour { get; set; }
    public bool Noticed { get; set; }
    /// <summary>Hours of repair work left, who works on it (employee id, -1 = contractor), and whether a spare part was available.</summary>
    public double RepairLeft { get; set; }
    public int RepairBy { get; set; }
    public bool PartUsed { get; set; }
    public double ContractorHour { get; set; }
    /// <summary>Game day the object was installed (UPS units and batteries age).</summary>
    public double Installed { get; set; }
    /// <summary>Main switchboards, UPS units and PDUs belong to the A or the B side of the power distribution.</summary>
    public FeedSide Feed { get; set; }
    [JsonIgnore] public ObjDef Def => Catalog.Objects[Kind];
}

/// <summary>Creative mode switches, chosen when starting a new game.</summary>
public class CreativeOptions
{
    public bool UnlimitedMoney { get; set; }
    public bool NoPowerLimit { get; set; }
    public bool NoCoolingLimit { get; set; }
    public bool NoNetworkLimit { get; set; }
    public bool NoBreakdowns { get; set; }
    public bool InstantBuild { get; set; }
    public bool AllUnlocks { get; set; }
    public static CreativeOptions All() => new() { UnlimitedMoney = true, NoPowerLimit = true, NoCoolingLimit = true, NoNetworkLimit = true, NoBreakdowns = true, InstantBuild = false, AllUnlocks = true };
}

public class NewGameOptions
{
    public string Company { get; set; } = "My Datacenter";
    public bool AiCompetitors { get; set; } = true;
    /// <summary>Random events: city-wide power cuts, heatwaves, cable cuts, fire alarms, customers going bankrupt.</summary>
    public bool Events { get; set; } = true;
    /// <summary>0 = easy, 1 = normal, 2 = hard. Sets the starting balance.</summary>
    public int Difficulty { get; set; } = 1;
    public bool Creative { get; set; }
    public CreativeOptions CreativeOptions { get; set; } = new();
    /// <summary>Step 9 switches: how many rivals (1-5) and how aggressive (0-2), the news ticker, dynamic energy prices, disasters and supply delays.</summary>
    public int Competitors { get; set; } = 3;
    public int Aggression { get; set; } = 1;
    public bool News { get; set; } = true;
    public bool DynamicPrices { get; set; } = true;
    public bool Disasters { get; set; } = true;
    public bool SupplyDelays { get; set; } = true;
    /// <summary>
    /// Step 10: the weather plays along (free cooling when cold, storms cause power cuts, snow and fog slow trucks, clouds dim solar).
    /// Off unless chosen: the new-game dialog ticks it by default, older saves and the demo keep the weather for looks only.
    /// </summary>
    public bool WeatherEffects { get; set; }
    /// <summary>Step 11: the scenario chosen in the new-game dialog (null = free play).</summary>
    public string Scenario { get; set; }
    /// <summary>Step 11: grid congestion. A bigger grid connection waits in the operator's queue first. On in the new-game dialog, off for older saves.</summary>
    public bool GridQueue { get; set; }
    /// <summary>Step 11: the tutorial coach guides the first site.</summary>
    public bool Tutorial { get; set; }
    public long StartMoney => Difficulty switch { 0 => 25_000_000, 2 => 6_000_000, _ => 12_000_000 };
    [JsonIgnore] public CreativeOptions C => Creative ? CreativeOptions : None;
    static readonly CreativeOptions None = new();
}

/// <summary>All state that goes into a savegame. Workers and trucks are not saved; they restart from this state.</summary>
public partial class GameState
{
    public const int CurrentFormat = 13;
    /// <summary>Floor value for objects that stand on the roof of a building.</summary>
    public const int RoofFloor = -1;
    /// <summary>Floor id of a basement. Kept apart from the storey numbers so roof (-1) and storeys (0..) stay as they were.</summary>
    public const int BasementFloor = 9;
    /// <summary>Height level of a floor id: the basement is one level below the ground floor.</summary>
    public static int Level(int floor) => floor == BasementFloor ? -1 : floor;
    /// <summary>All floor ids people can be on, bottom to top.</summary>
    public static readonly int[] FloorIds = new[] { BasementFloor }.Concat(Enumerable.Range(0, Catalog.MaxFloors)).ToArray();
    /// <summary>Building types that can have a basement.</summary>
    public static bool BasementAllowed(BuildingKind k) => k is BuildingKind.Datacenter or BuildingKind.Office;
    public const double WorkPerMaterial = 0.15;

    public int SaveFormat { get; set; } = CurrentFormat;
    public string GameVersion { get; set; } = "";
    public NewGameOptions Options { get; set; } = new();
    public long Money { get; set; }
    /// <summary>In-game time in hours since 1 January 2027 00:00.</summary>
    public double Hours { get; set; } = 8;
    public int PlotSize { get; set; } = 64;
    public byte[] Surfaces { get; set; }
    public byte[] Fences { get; set; }
    public List<Building> Buildings { get; set; } = new();
    public List<Room> Rooms { get; set; } = new();
    public List<Door> Doors { get; set; } = new();
    public List<PlacedObject> Objects { get; set; } = new();
    /// <summary>Curved roads and paths drawn with the curve tool (their cells are also in Surfaces).</summary>
    public List<CurvePath> Curves { get; set; } = new();
    public int NextId { get; set; } = 1;
    public int MaterialStock { get; set; }
    public int Builders { get; set; } = 4;
    public int Fitters { get; set; } = 2;
    public int Engineers { get; set; } = 1;
    /// <summary>Grid connection level (see Catalog.GridLevels), and a requested upgrade with the days left.</summary>
    public int GridLevel { get; set; }
    public int GridRequest { get; set; } = -1;
    public double GridDaysLeft { get; set; }
    public int Supplier { get; set; }
    /// <summary>Network (step 5): carrier ports, private peering with rivals and cross-connects in the MMR.</summary>
    public List<CarrierContract> Contracts { get; set; } = new();
    public List<PeeringDeal> Peers { get; set; } = new();
    public int CrossConnects { get; set; }
    public int XcPending { get; set; }
    public float CamYaw { get; set; } = 0.6f;
    public float CamPitch { get; set; } = 0.9f;
    public float CamDistance { get; set; } = 60f;
    public float[] CamTarget { get; set; } = { 0, 0, 0 };
    /// <summary>Only read from version 1 savegames.</summary>
    public List<int[]> Foundation { get; set; }

    /// <summary>Raised after any change to the layout, so views and navigation can rebuild.</summary>
    [JsonIgnore] public int Revision { get; private set; }
    /// <summary>Changes to buildings, rooms, doors, fences and paving only (not object status).</summary>
    [JsonIgnore] public int LayoutRevision { get; private set; }
    public void Touch(bool layout = true) { Revision++; if (layout) LayoutRevision++; }

    /// <summary>Notifications from the infrastructure (grid ready, hall too hot), drained by the game screen.</summary>
    [JsonIgnore] public readonly List<(string key, string arg)> Notes = new();

    public static readonly DateTime Epoch = new(2027, 1, 1);
    [JsonIgnore] public DateTime Date => Epoch.AddHours(Hours);
    [JsonIgnore] public CreativeOptions Creative => Options.C;

    /// <summary>Cells of the grid operator's connection building, near the road.</summary>
    [JsonIgnore] public (int x, int z, int w, int d) GridStation => (PlotSize - 6, PlotSize - 5, 4, 3);

    public static GameState New(NewGameOptions o)
    {
        var s = new GameState { Options = o, Money = o.StartMoney };
        s.EnsureGrids();
        s.EnsureCompetitors();
        return s;
    }

    public void EnsureGrids()
    {
        var n = PlotSize * PlotSize;
        if (Surfaces == null || Surfaces.Length != n) Surfaces = new byte[n];
        if (Fences == null || Fences.Length != n) Fences = new byte[n];
    }

    /// <summary>Bring an older savegame up to the current format.</summary>
    public void Migrate()
    {
        EnsureGrids();
        // free walls (2026-11-21): rooms from older saves keep their own walls (Walled defaults to true)
        Walls ??= new();
        foreach (var r in Rooms) r.Cells ??= new();
        if (SaveFormat < 2)
        {
            // v1 kinds were Foundation, Rack, Crah, Generator, FiberPit; the foundation is now a finished building.
            foreach (var o in Objects) { o.Kind = (ObjKind)((int)o.Kind - 1); o.Status = ObjStatus.Live; }
            Objects.RemoveAll(o => (int)o.Kind < 0);
            if (Foundation is { Count: > 0 })
            {
                var x0 = Foundation.Min(f => f[0]); var z0 = Foundation.Min(f => f[1]);
                var x1 = Foundation.Max(f => f[0]); var z1 = Foundation.Max(f => f[1]);
                var b = new Building { Id = NextId++, X = x0, Z = z0, W = x1 - x0 + 1, D = z1 - z0 + 1, Stage = Stage.Done };
                Buildings.Add(b);
                Rooms.Add(new Room { Id = NextId++, BuildingId = b.Id, Type = RoomType.DataHall, X = b.X, Z = b.Z, W = b.W, D = b.D, Stage = Stage.Done });
            }
            foreach (var o in Objects) o.Id = NextId++;
            Foundation = null;
        }
        if (SaveFormat < 3)
        {
            foreach (var o in Objects) if (o.Level < 1) o.Level = 1;
            foreach (var r in Rooms) r.Temp = 21;
            GridRequest = -1;
        }
        if (SaveFormat < 4)
        {
            // racks now need a network; tell players of older games where to start
            if (Objects.Any(o => o.Kind == ObjKind.Rack && o.Status == ObjStatus.Live)) Notes.Add(("msg_network_new", null));
        }
        if (SaveFormat < 5)
        {
            // step 6: customers pay per contract. Racks that already earned rent keep a contract with your existing customers,
            // and the operational managers come for free so nothing that worked stops working.
            Grandfathered = new List<Exec> { Exec.Hr, Exec.Operations, Exec.Facility, Exec.Network };
            var live = Objects.Where(o => o.Kind == ObjKind.Rack && o.Status == ObjStatus.Live).Select(o => o.Id).ToList();
            if (live.Count > 0)
                Deals.Add(new Deal { Id = NextId++, NameKey = "cust_legacy", Type = CustomerType.Smb, Product = Product.Rack, Racks = live.Count, RackIds = live,
                    Monthly = 900L * live.Count, Months = 120, State = DealState.Active, StartDay = Hours / 24, EndDay = Hours / 24 + 3600, Legacy = true });
            EnergyStartDay = Hours / 24 - 3650;
            Notes.Add(("msg_org_new", null));
        }
        Contracts ??= new(); Peers ??= new();
        Execs ??= new(); Grandfathered ??= new(); Deals ??= new(); Loans ??= new(); Books ??= new();
        foreach (var d in Deals) d.RackIds ??= new();
        if (SaveFormat < 6)
        {
            // step 7: staff, maintenance and events. Existing equipment counts as installed today.
            foreach (var o in Objects) o.Installed = Hours / 24;
            Notes.Add(("msg_staff_new", null));
        }
        if (SaveFormat < 7)
        {
            // step 8: research. What an older game already used stays available: the cold corridor and the electric fence.
            Researched ??= new();
            if (Rooms.Any(r => r.Containment == Containment.ColdCorridor || (r.Upgrade == 1 && r.Containment == Containment.HotCold))) Researched.Add("cool_corridor");
            if (Fences != null && Fences.Any(f => f == (byte)FenceType.Electric)) Researched.Add("sec_electric");
            // the old security requirement (security manager and guard booth) now is a security level; signed contracts simply run on
            Notes.Add(("msg_research_new", null));
        }
        Researched ??= new(); ResearchQueue ??= new(); Certs ??= new(); Quality ??= new(); Reviews ??= new(); OpenProposals ??= new(); Upgrades ??= new(); RepHistory ??= new();
        Staff ??= new(); Candidates ??= new(); StaffingLevel ??= new(); TrainingPoints ??= new();
        ActiveEvents ??= new(); Incidents ??= new(); PartStock ??= new(); PartMin ??= new(); PartOrders ??= new();
        foreach (var k in Contracts) k.Samples ??= new();
        if (SaveFormat < 8)
        {
            // step 9: growth. Older games keep their settings: no disasters or supply delays they did not choose, prices as before.
            Options.DynamicPrices = false; Options.Disasters = false; Options.SupplyDelays = false;
            Notes.Add(("msg_growth_new", null));
        }
        if (SaveFormat < 9)
        {
            // step 10: older games get the new looks, but the weather only plays along in new games
            Options.WeatherEffects = false;
        }
        if (SaveFormat < 11)
        {
            // network tree: spine switches stay where they are (now the big Spine 400 with 96 ports); edges hang straight on the
            // core until a distribution switch stands in the MMR, so nothing that was online goes offline
            if (Objects.Any(o => Catalog.IsHallSwitch(o.Kind))) Notes.Add(("msg_nettree_new", null));
        }
        if (SaveFormat < 12)
        {
            // power tree: switchboards, UPS units and PDUs now belong to an A or a B side. Older games had no sides,
            // so they alternate A, B, A ... per hall (PDUs) and over the site (switchboards and UPS units), as a player would.
            AssignFeedSides();
            if (Objects.Any(o => Catalog.HasFeed(o.Kind))) Notes.Add(("msg_power_tree_new", null));
        }
        Ducts ??= new();
        if (SaveFormat < 13)
        {
            // own main fiber duct: there are no street fiber pits any more; old pits become cable entries with a free duct
            MigrateDucts();
        }
        // hall names (sketch board 7): halls get a fixed number in the order they were drawn, so codes match older games
        NumberHalls();
        MigrateGrowth();
        EnsureCompetitors();
        SaveFormat = CurrentFormat;
        Touch();
    }

    int Idx(int x, int z) => z * PlotSize + x;
    public bool InPlot(int x, int z) => x >= 0 && z >= 0 && x < PlotSize && z < PlotSize;
    public Surface SurfaceAt(int x, int z) => InPlot(x, z) ? (Surface)Surfaces[Idx(x, z)] : Surface.Grass;
    public FenceType FenceAt(int x, int z) => InPlot(x, z) ? (FenceType)Fences[Idx(x, z)] : FenceType.None;

    // ---------------- money ----------------
    public bool CanAfford(long cost) => Creative.UnlimitedMoney || Planning || Money >= cost;
    public bool Spend(long cost)
    {
        if (Creative.UnlimitedMoney || Planning) return true;
        if (Money < cost) return false;
        Money -= cost; Book("cost_build", -cost); return true;
    }
    public void Refund(long amount) { if (!Creative.UnlimitedMoney) { Money += amount; Book("cost_build", amount); } }

    // ---------------- lookups ----------------
    public Building BuildingAt(int x, int z) => Buildings.FirstOrDefault(b => b.Contains(x, z));
    public Room RoomAt(int x, int z, int floor) => Rooms.FirstOrDefault(r => r.Floor == floor && r.Contains(x, z));
    public Building BuildingOf(Room r) => Buildings.FirstOrDefault(b => b.Id == r.BuildingId);
    public bool InStation(int x, int z) { var g = GridStation; return x >= g.x && z >= g.z && x < g.x + g.w && z < g.z + g.d; }

    public static (int w, int d) Footprint(PlacedObject o)
    {
        var d = o.Def;
        return o.Rot % 2 == 0 ? (d.W, d.D) : (d.D, d.W);
    }

    public static IEnumerable<(int x, int z)> Cells(PlacedObject o)
    {
        var (w, d) = Footprint(o);
        for (var x = o.X; x < o.X + w; x++)
            for (var z = o.Z; z < o.Z + d; z++) yield return (x, z);
    }

    /// <summary>Object covering a cell on a floor. Lifts cover every floor of their building.</summary>
    public PlacedObject ObjectAt(int x, int z, int floor = 0)
    {
        // A cell index keeps this O(1) with thousands of racks; it is rebuilt whenever the state is touched
        // or the object list changes size, and a hit is checked against the object itself.
        EnsureObjectIndex();
        PlacedObject best = null; var bestI = int.MaxValue;
        if (_objIndex.TryGetValue((x, z, floor), out var hit) && Covers(Objects[hit], x, z, floor)) { best = Objects[hit]; bestI = hit; }
        if (floor >= 0)
            foreach (var i in _liftIndex)
                if (i < bestI && Covers(Objects[i], x, z, floor)) { best = Objects[i]; bestI = i; }
        return best;
    }

    /// <summary>Objects the building and fence layout depends on: gates in fences, lifts and plant on roofs.</summary>
    public static bool ShapesLayout(PlacedObject o) => Catalog.IsGate(o.Kind) || Catalog.IsShaft(o.Kind) || o.Floor == RoofFloor;

    static bool Covers(PlacedObject o, int x, int z, int floor)
    {
        if (o.Floor != floor && (!Catalog.IsShaft(o.Kind) || floor < 0)) return false;
        var (w, d) = Footprint(o);
        return x >= o.X && x < o.X + w && z >= o.Z && z < o.Z + d;
    }

    readonly Dictionary<(int x, int z, int floor), int> _objIndex = new();
    readonly List<int> _liftIndex = new();
    int _objIndexRev = -1, _objIndexCount = -1;

    void EnsureObjectIndex()
    {
        if (_objIndexRev == Revision && _objIndexCount == Objects.Count) return;
        _objIndexRev = Revision; _objIndexCount = Objects.Count;
        _objIndex.Clear(); _liftIndex.Clear();
        for (var i = 0; i < Objects.Count; i++) IndexAdd(i);
    }

    /// <summary>Add the object just appended to the list to an up-to-date index, so placing a row stays cheap.</summary>
    void IndexObject(int i)
    {
        _objIndexRev = Revision; _objIndexCount = Objects.Count;
        IndexAdd(i);
    }

    void IndexAdd(int i)
    {
        var o = Objects[i];
        if (Catalog.IsShaft(o.Kind)) { _liftIndex.Add(i); return; }
        var (w, d) = Footprint(o);
        for (var x = o.X; x < o.X + w; x++)
            for (var z = o.Z; z < o.Z + d; z++)
                _objIndex.TryAdd((x, z, o.Floor), i);
    }

    public static IEnumerable<(int x, int z, int dir)> Perimeter(int x, int z, int w, int d)
    {
        for (var i = z; i < z + d; i++) { yield return (x, i, 0); yield return (x + w, i, 0); }
        for (var i = x; i < x + w; i++) { yield return (i, z, 1); yield return (i, z + d, 1); }
    }

    /// <summary>Wall edges on a floor. With builtOnly, only finished walls count (used by navigation).</summary>
    public HashSet<(int, int, int)> WallEdges(int floor, bool builtOnly)
    {
        var set = new HashSet<(int, int, int)>();
        foreach (var b in Buildings)
            if (b.HasFloor(floor) && (!builtOnly || b.Stage == Stage.Done))
                foreach (var e in Perimeter(b.X, b.Z, b.W, b.D)) set.Add(e);
        foreach (var r in Rooms)
            if (r.Walled && r.Floor == floor && (!builtOnly || r.Stage == Stage.Done))
                foreach (var e in Perimeter(r.X, r.Z, r.W, r.D)) set.Add(e);
        foreach (var w in Walls)
            if (w.Floor == floor && (!builtOnly || w.Stage == Stage.Done))
                foreach (var e in w.Edges()) set.Add(e);
        return set;
    }

    public bool IsOuterEdge((int x, int z, int dir) e) => Buildings.Any(b => Perimeter(b.X, b.Z, b.W, b.D).Contains(e));

    // ---------------- construction numbers ----------------
    public static int MaterialNeed(Building b) => b.Stage switch
    {
        // digging out and pouring a basement makes the foundation three times the work
        Stage.Foundation => (int)Math.Ceiling(b.W * b.D / 5.0) * (b.Basement ? 3 : 1),
        Stage.Walls => (b.W + b.D) * b.Levels + (b.Levels - 1) * (int)Math.Ceiling(b.W * b.D / 8.0),
        Stage.Roof => (int)Math.Ceiling(b.W * b.D / 8.0),
        _ => 0,
    };
    public static int MaterialNeed(Room r) => r.Stage == Stage.Done || !r.Walled ? 0 : (int)Math.Ceiling((r.W + r.D) * 2 / 3.0) + 1;
    public static double WorkNeed(Building b) => MaterialNeed(b) * WorkPerMaterial;
    public static double WorkNeed(Room r) => MaterialNeed(r) * WorkPerMaterial;
    public static long BuildingCost(BuildingKind k, int w, int d, int floors, bool basement = false) =>
        Catalog.Buildings[k].CostPerCellPerFloor * w * d * floors + (basement ? BasementCost(k, w, d) : 0);
    /// <summary>A basement costs one and a half storeys: digging, waterproofing and a heavy floor.</summary>
    public static long BasementCost(BuildingKind k, int w, int d) => Catalog.Buildings[k].CostPerCellPerFloor * w * d * 3 / 2;
    public static long BuildingCost(Building b) => BuildingCost(b.Kind, b.W, b.D, b.Floors, b.Basement);

    /// <summary>Overall progress of a building, 0..1.</summary>
    public static double Progress(Building b)
    {
        if (b.Stage == Stage.Done) return 1;
        var need = WorkNeed(b);
        return ((int)b.Stage + (need <= 0 ? 0 : Math.Min(1, b.Work / need))) / 3.0;
    }

    public static double StageProgress(Building b) { var n = WorkNeed(b); return n <= 0 ? 1 : Math.Min(1, b.Work / n); }
    public static double StageProgress(Room r) { var n = WorkNeed(r); return r.Stage == Stage.Done ? 1 : n <= 0 ? 1 : Math.Min(1, r.Work / n); }

    // ---------------- surfaces and fences ----------------
    public string PaintSurface(int x0, int z0, int w, int d, Surface s, out int changed)
    {
        changed = 0;
        var cost = Catalog.Surfaces[s].Cost;
        for (var x = x0; x < x0 + w; x++)
            for (var z = z0; z < z0 + d; z++)
            {
                if (!InPlot(x, z) || InStation(x, z) || BuildingAt(x, z) != null) continue;
                if (SurfaceAt(x, z) == s) continue;
                if (s == Surface.Grass && SurfaceAt(x, z) == Surface.Grass) continue;
                if (!Spend(cost)) { Touch(); return "msg_no_money"; }
                Surfaces[Idx(x, z)] = (byte)s; changed++;
            }
        if (changed > 0) { PruneCurves(); Touch(); }
        return changed == 0 ? "msg_nothing_changed" : null;
    }

    /// <summary>
    /// Paves a curve: every cell its strip covers gets the surface (paid per m² like painting), and the
    /// curve is kept so it is drawn smooth. Cells with a building or the grid station are left out.
    /// </summary>
    public string AddCurve(CurvePath c)
    {
        if (!CurvePath.DefaultWidth.ContainsKey(c.Surface)) return "msg_nothing_changed";
        c.Width = Math.Clamp(c.Width, CurvePath.MinWidth, CurvePath.MaxWidth);
        var cost = Catalog.Surfaces[c.Surface].Cost;
        c.Cells.Clear();
        string why = null;
        foreach (var (x, z) in c.Covered(PlotSize))
        {
            if (InStation(x, z) || BuildingAt(x, z) != null) continue;
            if (SurfaceAt(x, z) == c.Surface) continue;
            if (!Spend(cost)) { why = "msg_no_money"; break; }
            Surfaces[Idx(x, z)] = (byte)c.Surface;
            c.Cells.Add(Idx(x, z));
        }
        if (c.Cells.Count == 0) { Touch(); return why ?? "msg_nothing_changed"; }
        Curves.Add(c);
        PruneCurves();
        Touch();
        return why;
    }

    /// <summary>The curve's paving cost before it is built.</summary>
    public long CurveCost(CurvePath c) =>
        c.Covered(PlotSize).Count(p => !InStation(p.x, p.z) && BuildingAt(p.x, p.z) == null && SurfaceAt(p.x, p.z) != c.Surface) * Catalog.Surfaces[c.Surface].Cost;

    /// <summary>Forgets cells of curves that were repainted or erased, and curves with none left.</summary>
    void PruneCurves()
    {
        foreach (var c in Curves) c.Cells.RemoveAll(i => i < 0 || i >= Surfaces.Length || Surfaces[i] != (byte)c.Surface);
        Curves.RemoveAll(c => c.Cells.Count == 0);
    }

    /// <summary>Cells for a fence drag: a straight line, or the outline of the dragged rectangle.</summary>
    public static IEnumerable<(int x, int z)> FenceCells(int x0, int z0, int w, int d)
    {
        if (w == 1 || d == 1)
        {
            for (var x = x0; x < x0 + w; x++) for (var z = z0; z < z0 + d; z++) yield return (x, z);
            yield break;
        }
        for (var x = x0; x < x0 + w; x++) { yield return (x, z0); yield return (x, z0 + d - 1); }
        for (var z = z0 + 1; z < z0 + d - 1; z++) { yield return (x0, z); yield return (x0 + w - 1, z); }
    }

    public string PlaceFence(int x0, int z0, int w, int d, FenceType t, out int changed)
    {
        changed = 0;
        var def = Catalog.Fences[t];
        if (def.NeedsResearch && !Done("sec_electric")) return "msg_needs_research";
        foreach (var (x, z) in FenceCells(x0, z0, w, d))
        {
            if (!InPlot(x, z) || InStation(x, z) || BuildingAt(x, z) != null || FenceAt(x, z) == t) continue;
            var o = ObjectAt(x, z);
            if (o != null && o.Def.Blocks) continue;
            if (!Spend(def.Cost)) { Touch(); return "msg_no_money"; }
            Fences[Idx(x, z)] = (byte)t; changed++;
        }
        if (changed > 0) Touch();
        return changed == 0 ? "msg_nothing_changed" : null;
    }

    // ---------------- buildings, rooms, doors ----------------
    public string CanBuild(BuildingKind k, int x, int z, int w, int d, int floors, bool basement = false)
    {
        if (w < 4 || d < 4) return "msg_too_small";
        if (floors < 1 || floors > Catalog.MaxFloors) return "msg_bad_floors";
        if (basement && !BasementAllowed(k)) return "msg_no_basement";
        // a building never goes on top of your own fiber duct: run the duct around it or move it first
        if (Ducts.Any(dc => dc.Cells().Any(c => c.x >= x && c.x < x + w && c.z >= z && c.z < z + d))) return "msg_duct_in_way";
        for (var i = x; i < x + w; i++)
            for (var j = z; j < z + d; j++)
            {
                if (!InPlot(i, j)) return "msg_outside_plot";
                if (InStation(i, j)) return "msg_occupied";
                if (BuildingAt(i, j) != null) return "msg_overlap_building";
                if (FenceAt(i, j) != FenceType.None) return "msg_fence_in_way";
                if (ObjectAt(i, j) != null) return "msg_occupied";
            }
        // keep one free cell around a building so people can walk past and doors open onto something
        for (var i = x - 1; i <= x + w; i++)
            for (var j = z - 1; j <= z + d; j++)
                if (BuildingAt(i, j) != null) return "msg_building_gap";
        if (!CanAfford(BuildingCost(k, w, d, floors, basement))) return "msg_no_money";
        return null;
    }

    public static Facade DefaultFacade(BuildingKind k) => k switch { BuildingKind.Office => Facade.Brick, BuildingKind.Warehouse => Facade.Cladding, _ => Facade.Concrete };

    public Building AddBuilding(BuildingKind k, int x, int z, int w, int d, int floors, Facade? facade = null, bool basement = false)
    {
        if (CanBuild(k, x, z, w, d, floors, basement) != null) return null;
        Spend(BuildingCost(k, w, d, floors, basement));
        var f = k == BuildingKind.Office && facade.HasValue ? facade.Value : DefaultFacade(k);
        var b = new Building { Id = NextId++, Kind = k, Facade = f, X = x, Z = z, W = w, D = d, Floors = floors, Basement = basement, Stage = Creative.InstantBuild ? Stage.Done : Stage.Foundation };
        Buildings.Add(b);
        Touch();
        return b;
    }

    public string CanRoom(RoomType t, int x, int z, int w, int d, int floor)
    {
        if (w < 2 || d < 2) return "msg_too_small";
        var b = BuildingAt(x, z);
        if (b == null || !b.Contains(x + w - 1, z + d - 1)) return "msg_room_in_building";
        if (!b.HasFloor(floor)) return "msg_no_such_floor";
        if (floor == BasementFloor && t is RoomType.LoadingDock or RoomType.Reception) return "msg_not_in_basement";
        for (var i = x; i < x + w; i++)
            for (var j = z; j < z + d; j++)
                if (RoomAt(i, j, floor) != null) return "msg_overlap_room";
        if (!CanAfford(Catalog.RoomCost(w, d))) return "msg_no_money";
        return null;
    }

    public Room AddRoom(RoomType t, int x, int z, int w, int d, int floor)
    {
        if (CanRoom(t, x, z, w, d, floor) != null) return null;
        Spend(Catalog.RoomCost(w, d));
        var b = BuildingAt(x, z);
        var r = new Room { Id = NextId++, BuildingId = b.Id, Floor = floor, Type = t, X = x, Z = z, W = w, D = d, Stage = Creative.InstantBuild ? Stage.Done : Stage.Walls };
        Rooms.Add(r);
        if (t == RoomType.DataHall) NumberHalls();
        Touch();
        return r;
    }

    public string CanDoor(Door door)
    {
        var walls = WallEdges(door.Floor, false);
        foreach (var e in door.Edges())
        {
            if (!walls.Contains(e)) return "msg_door_on_wall";
            if (Doors.Any(o => o.Floor == door.Floor && o.Edges().Contains(e))) return "msg_occupied";
            if (door.Floor != 0 && IsOuterEdge(e)) return "msg_door_upper_outer";
        }
        if (door.Type == DoorType.Mantrap && !Done("sec_mantrap")) return "msg_needs_research";
        if (!CanAfford(Catalog.Doors[door.Type].Cost)) return "msg_no_money";
        return null;
    }

    public Door AddDoor(DoorType t, int x, int z, int dir, int floor)
    {
        var door = new Door { Id = NextId, Type = t, X = x, Z = z, Dir = dir, Floor = floor };
        if (CanDoor(door) != null) return null;
        NextId++;
        Spend(Catalog.Doors[t].Cost);
        Doors.Add(door);
        Touch();
        // with instant building the room is finished before it has a door: furnish it once the door is in
        if (Creative.InstantBuild)
            foreach (var r in Rooms.Where(r => r.Floor == floor && r.Stage == Stage.Done && Furnishable(r.Type) && DoorsOf(r).Contains(door)).ToList())
                RoomFinished(r);
        return door;
    }

    // ---------------- objects ----------------
    public string CanPlace(PlacedObject o)
    {
        var def = o.Def;
        var room0 = (Room)null;
        foreach (var (x, z) in Cells(o))
        {
            if (!InPlot(x, z)) return "msg_outside_plot";
            if (InStation(x, z)) return "msg_occupied";
            var other = ObjectAt(x, z, o.Floor);
            if (other != null && other != o) return "msg_occupied";
            var b = BuildingAt(x, z);
            var room = RoomAt(x, z, o.Floor);
            var where = def.Where;
            if (where == Where.OutsideOrRoof)
            {
                if (o.Floor == RoofFloor)
                {
                    if (BuildingAt(o.X, o.Z) != b) return "msg_one_roof";
                    if (b == null || b.Stage != Stage.Done) return "msg_roof_needs_building";
                    continue;
                }
                where = Where.Outside;
            }
            else if (o.Floor == RoofFloor) return "msg_not_on_roof";
            switch (where)
            {
                case Where.Underground:
                    if (b != null || o.Floor != 0) return "msg_outside_only";
                    break;
                case Where.Outside:
                case Where.Paved:
                    if (b != null || o.Floor != 0) return "msg_outside_only";
                    if (FenceAt(x, z) != FenceType.None) return "msg_fence_in_way";
                    if (def.Where == Where.Paved && !Catalog.Surfaces[SurfaceAt(x, z)].Trucks) return "msg_needs_asphalt";
                    break;
                case Where.Fence:
                    if (FenceAt(x, z) == FenceType.None) return "msg_needs_fence";
                    break;
                case Where.Building:
                    if (b == null || o.Floor != 0) return "msg_inside_building";
                    if (b.Levels < 2) return "msg_lift_floors";
                    break;
                case Where.Room:
                    if (def.Rooms != null)
                    {
                        if (room == null || !def.Rooms.Contains(room.Type)) return NeedsRoomKey(def);
                    }
                    else if (room == null || room.Type == RoomType.DataHall) return "msg_needs_room";
                    break;
                case Where.Hall:
                    if (room == null || room.Type != RoomType.DataHall) return "msg_needs_hall";
                    break;
            }
            if (def.Where is Where.Room or Where.Hall)
            {
                if (room0 == null) room0 = room;
                else if (room0 != room) return "msg_one_room";
            }
        }
        if (o.Kind == ObjKind.Rack && RackAccess(o) is { } access) return access;
        if (o.Kind == ObjKind.FiberPit && !OnBorder(o.X, o.Z)) return "msg_entry_border";
        if (def.Blocks && o.Floor >= 0 && BlocksRack(o)) return "msg_blocks_rack";
        if (!CanAfford(def.Cost)) return "msg_no_money";
        return null;
    }

    /// <summary>Direction a rotation's front faces: rot 0 = +z, 1 = -x, 2 = -z, 3 = +x.</summary>
    public static (int dx, int dz) FrontDir(int rot) => (((rot % 4) + 4) % 4) switch { 0 => (0, 1), 1 => (-1, 0), 2 => (0, -1), _ => (1, 0) };

    /// <summary>Engineers work on a rack from both sides: the cells in front and behind must be free floor of the same hall.</summary>
    string RackAccess(PlacedObject o)
    {
        var (dx, dz) = FrontDir(o.Rot);
        var room = RoomAt(o.X, o.Z, o.Floor);
        foreach (var side in new[] { 1, -1 })
        {
            int x = o.X + dx * side, z = o.Z + dz * side;
            if (room == null || RoomAt(x, z, o.Floor) != room) return side > 0 ? "msg_rack_front" : "msg_rack_back";
            var other = ObjectAt(x, z, o.Floor);
            if (other != null && other != o && other.Def.Blocks) return side > 0 ? "msg_rack_front" : "msg_rack_back";
        }
        return null;
    }

    /// <summary>Whether a blocking object would stand right in front of or behind a rack.</summary>
    bool BlocksRack(PlacedObject o)
    {
        foreach (var (x, z) in Cells(o))
            foreach (var (nx, nz) in new[] { (x + 1, z), (x - 1, z), (x, z + 1), (x, z - 1) })
            {
                if (ObjectAt(nx, nz, o.Floor) is not { Kind: ObjKind.Rack } rack || rack == o) continue;
                var (dx, dz) = FrontDir(rack.Rot);
                if ((rack.X + dx == x && rack.Z + dz == z) || (rack.X - dx == x && rack.Z - dz == z)) return true;
            }
        return false;
    }

    /// <summary>"msg_needs_transformer", "msg_needs_switchgear", "msg_needs_upsroom" or "msg_needs_battery".</summary>
    public static string NeedsRoomKey(ObjDef def) => def.Rooms[0] switch
    {
        RoomType.Transformer => "msg_needs_transformer",
        RoomType.Switchgear => "msg_needs_switchgear",
        RoomType.Battery => "msg_needs_battery",
        RoomType.MeetMe => "msg_needs_mmr_room",
        RoomType.Noc => "msg_needs_noc_room",
        RoomType.Lab => "msg_needs_lab",
        RoomType.Security => "msg_needs_security_room",
        RoomType.Reception => "msg_needs_reception",
        RoomType.DataHall => "msg_needs_hall",
        _ => "msg_needs_upsroom",
    };

    /// <summary>Floor an object goes on when placed at a cell: roof units go on the roof of a building, else the viewed floor.</summary>
    public int PlaceFloor(ObjKind k, int x, int z, int viewFloor)
    {
        if (Catalog.Objects[k].Where == Where.OutsideOrRoof) return BuildingAt(x, z) != null ? RoofFloor : 0;
        return viewFloor;
    }

    public PlacedObject AddObject(ObjKind k, int x, int z, int rot, int floor)
    {
        var o = new PlacedObject { Kind = k, X = x, Z = z, Rot = rot, Floor = floor };
        if (CanPlace(o) != null) return null;
        o.Id = NextId++;
        o.Installed = Hours / 24;
        Spend(o.Def.Cost);
        o.Status = o.Def.Chain && !Creative.InstantBuild ? ObjStatus.Ordered : ObjStatus.Live;
        if (Catalog.HasFeed(k)) o.Feed = NextFeed(o);
        var indexed = _objIndexRev == Revision && _objIndexCount == Objects.Count;
        Objects.Add(o);
        // most objects (racks above all) do not change walls, roofs or fences: skip the layout rebuild for them
        Touch(ShapesLayout(o));
        if (indexed) IndexObject(Objects.Count - 1);
        return o;
    }

    /// <summary>A row of objects (racks) placed side by side along one axis, all facing the same way.</summary>
    public static List<(int x, int z)> RowCells(int ax, int az, int bx, int bz, ObjKind k, int rot, int max = Catalog.MaxRow)
    {
        var def = Catalog.Objects[k];
        var (w, d) = rot % 2 == 0 ? (def.W, def.D) : (def.D, def.W);
        var list = new List<(int, int)>();
        var alongX = Math.Abs(bx - ax) >= Math.Abs(bz - az);
        var step = alongX ? w : d;
        var len = alongX ? bx - ax : bz - az;
        var n = Math.Min(max, Math.Abs(len) / step + 1);
        var sign = len < 0 ? -1 : 1;
        for (var i = 0; i < n; i++) list.Add(alongX ? (ax + sign * i * step, az) : (ax, az + sign * i * step));
        return list;
    }

    /// <summary>
    /// Smart drag for racks: a drag one row deep gives one row (fronts by <paramref name="rot"/>); a wider drag fills
    /// the rectangle with pairs of rows that face each other across a 1 m cold aisle, with 2 m hot aisles between
    /// the pairs (row, cold, row, hot, hot, row ...). <paramref name="rot"/> 2 or 3 starts with the backs instead.
    /// </summary>
    public static List<(int x, int z, int rot)> RowBlock(int ax, int az, int bx, int bz, ObjKind k, int rot, int max = Catalog.MaxRow)
    {
        var alongX = Math.Abs(bx - ax) >= Math.Abs(bz - az);
        var perp = alongX ? bz - az : bx - ax;
        if (perp == 0) return RowCells(ax, az, bx, bz, k, rot, max).Select(c => (c.x, c.z, rot)).ToList();
        var sign = perp < 0 ? -1 : 1;
        var flip = rot >= 2;
        // fronts toward +perp / -perp: along X the perpendicular is z (rot 0 = +z, rot 2 = -z); along Z it is x (rot 3 = +x, rot 1 = -x)
        int Toward(bool plus) => alongX ? (plus ? 0 : 2) : (plus ? 3 : 1);
        var depth = Catalog.Objects[k].D;
        var list = new List<(int, int, int)>();
        var off = 0; var first = true;
        while (off * depth <= Math.Abs(perp))
        {
            // the first row of a pair faces the cold aisle ahead (+sign), the second faces back at it
            var facesAhead = first != flip;
            var r = Toward(facesAhead == (sign > 0));
            var o = sign * off * depth;
            var cells = alongX ? RowCells(ax, az + o, bx, az + o, k, r, max) : RowCells(ax + o, az, ax + o, bz, k, r, max);
            foreach (var c in cells) list.Add((c.x, c.z, r));
            // after a front-facing row comes the 1 m cold aisle, after a back-facing row the 2 m hot aisle
            off += facesAhead ? 2 : 3;
            first = !first;
        }
        return list;
    }

    // ---------------- erase ----------------
    /// <summary>Remove what lies in a rectangle on a floor: objects, doors, rooms, then buildings and paving.</summary>
    public bool Erase(int x0, int z0, int w, int d, int floor, bool roof = false)
    {
        bool In(int x, int z) => x >= x0 && z >= z0 && x < x0 + w && z < z0 + d;
        var changed = false;
        var layout = false;
        foreach (var o in Objects.Where(o => (o.Floor == floor || (Catalog.IsShaft(o.Kind) && floor == 0) || (roof && o.Floor == RoofFloor)) && Cells(o).Any(c => In(c.x, c.z))).ToList())
        {
            Objects.Remove(o); RefundRemoved(o.Id, o.Def.Cost / 2); changed = true; layout |= ShapesLayout(o);
        }
        foreach (var door in Doors.Where(o => o.Floor == floor && In(o.X, o.Z)).ToList()) { Doors.Remove(door); if (IsPlanned(door.Id)) Planned.Remove(door.Id); changed = true; layout = true; }
        if (floor == 0)
            foreach (var duct in Ducts.Where(dc => dc.Cells().Any(c => In(c.x, c.z))).ToList())
            {
                Ducts.Remove(duct); RefundRemoved(duct.Id, DuctCost(duct) / 2); changed = true; layout = true;
            }
        if (changed) { Touch(layout); RefreshRooms(); return true; }
        // separate walls whose both sides lie in the drag
        var wallEdges = Walls.Where(x => x.Floor == floor).SelectMany(x => x.Edges()).Where(e => { var (a, b) = Sides(e); return In(a.x, a.z) && In(b.x, b.z); }).ToList();
        if (wallEdges.Count > 0) { RemoveWallEdges(floor, wallEdges); RefreshRooms(); return true; }
        foreach (var r in Rooms.Where(r => r.Floor == floor && In(r.X, r.Z) && In(r.X + r.W - 1, r.Z + r.D - 1)).ToList())
        {
            DemolishRoom(r); changed = true;
        }
        if (changed) { Touch(); RefreshRooms(); return true; }
        foreach (var b in Buildings.Where(b => floor == 0 && In(b.X, b.Z) && In(b.X + b.W - 1, b.Z + b.D - 1)).ToList())
        {
            foreach (var r in Rooms.Where(r => r.BuildingId == b.Id).ToList()) RemoveRoom(r);
            Walls.RemoveAll(w => w.BuildingId == b.Id);
            Objects.RemoveAll(o => b.Contains(o.X, o.Z));
            Doors.RemoveAll(o => Perimeter(b.X, b.Z, b.W, b.D).Intersect(o.Edges()).Any());
            Buildings.Remove(b);
            RefundRemoved(b.Id, BuildingCost(b) / 4);
            changed = true;
        }
        if (changed) { Touch(); return true; }
        if (floor == 0)
            for (var x = x0; x < x0 + w; x++)
                for (var z = z0; z < z0 + d; z++)
                {
                    if (!InPlot(x, z)) continue;
                    if (Fences[Idx(x, z)] != 0) { Fences[Idx(x, z)] = 0; changed = true; }
                    else if (Surfaces[Idx(x, z)] != 0) { Surfaces[Idx(x, z)] = 0; changed = true; }
                }
        if (changed) { PruneCurves(); Touch(); }
        return changed;
    }

    void RemoveRoom(Room r)
    {
        Objects.RemoveAll(o => o.Floor == r.Floor && !Catalog.IsShaft(o.Kind) && r.Contains(o.X, o.Z));
        var edges = Perimeter(r.X, r.Z, r.W, r.D).ToHashSet();
        Doors.RemoveAll(o => o.Floor == r.Floor && o.Edges().Any(edges.Contains) && !o.Edges().Any(IsOuterEdge));
        Rooms.Remove(r);
        RefundRemoved(r.Id, r.Walled ? Catalog.RoomCost(r.W, r.D) / 4 : 0);
    }

    // ---------------- derived resources ----------------
    IEnumerable<PlacedObject> LiveObjects => Objects.Where(o => o.Status == ObjStatus.Live);
    [JsonIgnore] public double PowerDemandKw => LiveObjects.Sum(o => o.Def.PowerKw);
    /// <summary>Generator capacity with N+1: the largest generator is kept in reserve.</summary>
    [JsonIgnore]
    public double BackupCapacityKw
    {
        get
        {
            var gens = Objects.Where(o => o.Kind == ObjKind.Generator && !o.Broken).Select(o => o.Def.GenKw).OrderByDescending(k => k).ToList();
            return gens.Count <= 1 ? 0 : gens.Skip(1).Sum();
        }
    }
    [JsonIgnore] public double GridCapacityKw => Catalog.GridLevels[Math.Clamp(GridLevel, 0, Catalog.GridLevels.Length - 1)].Mva * 1000;
    [JsonIgnore] public int Racks => Objects.Count(o => o.Kind == ObjKind.Rack);
    [JsonIgnore] public int LiveRacks => Objects.Count(o => o.Kind == ObjKind.Rack && o.Status == ObjStatus.Live);
    [JsonIgnore] public long WagesPerDay => Builders * Catalog.WagePerDay[Role.Builder] + Fitters * Catalog.WagePerDay[Role.Fitter] + Engineers * Catalog.WagePerDay[Role.Engineer];

    InfraReport _infra;
    int _infraRev = -1;
    double _infraHours = -1;

    /// <summary>Power chain, cooling and PUE. Cached until the layout, the clock or a hall temperature changes.</summary>
    [JsonIgnore]
    public InfraReport Infra
    {
        get
        {
            if (_infra == null || _infraRev != Revision || Math.Abs(_infraHours - Hours) > 0.05)
            {
                _infra = Sim.Infra.Analyze(this);
                _infraRev = Revision; _infraHours = Hours;
            }
            return _infra;
        }
    }

    public void InvalidateInfra() { _infra = null; _net = null; _wxAt = double.NaN; }

    NetReport _net;
    int _netRev = -1;
    double _netHours = -1;

    /// <summary>Network: routes, carriers, peering, MMR, switches, traffic and which racks are online. Cached like Infra.</summary>
    [JsonIgnore]
    public NetReport Net
    {
        get
        {
            var infra = Infra;
            if (_net == null || _netRev != Revision || Math.Abs(_netHours - Hours) > 0.05)
            {
                _net = Sim.Network.Analyze(this, infra);
                _netRev = Revision; _netHours = Hours;
            }
            return _net;
        }
    }

    // ---------------- power and cooling actions ----------------
    public string CanRequestGrid(int level)
    {
        if (level <= GridLevel || level >= Catalog.GridLevels.Length) return "msg_nothing_changed";
        if (GridRequest >= 0) return "msg_grid_busy";
        var locked = GridUnlock(level);
        if (locked != null) return locked;
        if (!CanAfford(Catalog.GridLevels[level].Cost)) return "msg_no_money";
        return null;
    }

    public string RequestGrid(int level)
    {
        var why = CanRequestGrid(level);
        if (why != null) return why;
        var def = Catalog.GridLevels[level];
        Spend(def.Cost);
        if (Creative.InstantBuild) { GridLevel = level; Notes.Add(("msg_grid_ready", def.Mva.ToString(System.Globalization.CultureInfo.InvariantCulture))); }
        else
        {
            GridRequest = level; GridDaysLeft = def.LeadDays;
            GridQueueDays = GridQueueLength(level) * GridQueueStep;
            if (GridQueueDays > 0) Notes.Add(("msg_grid_queue_joined", GridQueuePlace.ToString()));
        }
        Touch(false);
        return null;
    }

    public string SetSupplier(int id)
    {
        if (id < 0 || id >= Catalog.Suppliers.Length || id == Supplier) return "msg_nothing_changed";
        var fee = SwitchFee(id);
        if (!CanAfford(fee)) return "msg_no_money";
        if (fee > 0 && !Creative.UnlimitedMoney) { Money -= fee; Book("cost_energy", -fee); }
        Supplier = id; EnergyStartDay = Hours / 24; Touch(false);
        return null;
    }

    /// <summary>Price per kWh today. Variable contracts follow a market price that moves over the weeks.</summary>
    public double EnergyPrice(int supplier = -1)
    {
        var d = Catalog.Suppliers[supplier < 0 ? Supplier : supplier];
        if (!d.Variable) return d.PricePerKwh;
        return d.PricePerKwh * EnergyIndex;
    }

    public string CanUpgrade(PlacedObject o)
    {
        if (o.Level >= o.Def.MaxLevel) return "msg_max_level";
        if (!CanAfford(Catalog.UpgradeCost(o.Def, o.Level))) return "msg_no_money";
        return null;
    }

    public string UpgradeObject(PlacedObject o)
    {
        var why = CanUpgrade(o);
        if (why != null) return why;
        Spend(Catalog.UpgradeCost(o.Def, o.Level));
        o.Level++;
        Touch(false);
        return null;
    }

    // ---------------- extra floors ----------------
    /// <summary>A storey on top of a finished building costs what a storey costs when you build it.</summary>
    public static long LevelCost(Building b) => Catalog.Buildings[b.Kind].CostPerCellPerFloor * b.W * b.D;
    /// <summary>Hours to add a storey: a day for setting up and a little longer for bigger buildings.</summary>
    public static double LevelHours(Building b) => 24 + b.W * b.D / 10.0;

    public string CanAddLevel(Building b)
    {
        if (b == null) return "msg_nothing_changed";
        if (b.Stage != Stage.Done || IsPlanned(b)) return "msg_building_not_ready";
        if (b.Raising) return "msg_upgrade_busy";
        if (b.Floors >= Catalog.MaxFloors) return "msg_max_floors";
        if (!CanAfford(LevelCost(b))) return "msg_no_money";
        return null;
    }

    /// <summary>
    /// Put a storey on a finished building. The building keeps working while the crane builds on the roof; the new
    /// floor opens when it is done. Rooftop plant stays on the (new) roof.
    /// </summary>
    public string AddLevel(Building b)
    {
        var why = CanAddLevel(b);
        if (why != null) return why;
        Spend(LevelCost(b));
        b.Raising = true;
        b.RaiseHours = b.RaiseLeft = LevelHours(b);
        if (Creative.InstantBuild) FinishLevel(b);
        Touch();
        return null;
    }

    void FinishLevel(Building b)
    {
        b.Floors++;
        b.Raising = false; b.RaiseLeft = 0;
        Notes.Add(("msg_level_done", Catalog.Buildings[b.Kind].NameKey));
        Touch();
    }

    public static double RaiseProgress(Building b) => b.RaiseHours <= 0 ? 0 : Math.Clamp(1 - b.RaiseLeft / b.RaiseHours, 0, 1);

    public static long HallUpgradeCost(Room r) => r.Containment >= Containment.ColdCorridor ? 0 : Catalog.Containments[r.Containment + 1].CostPerM2 * r.Area;

    public string CanUpgradeHall(Room r, bool busbar)
    {
        if (r.Type != RoomType.DataHall) return "msg_needs_hall";
        if (r.Stage != Stage.Done) return "msg_hall_not_ready";
        if (r.Upgrade != 0) return "msg_upgrade_busy";
        if (busbar ? r.Busbar : r.Containment >= Containment.ColdCorridor) return "msg_max_level";
        if (!busbar && r.Containment == Containment.HotCold && !Done("cool_corridor")) return "msg_needs_research";
        if (!CanAfford(busbar ? Catalog.BusbarCost : HallUpgradeCost(r))) return "msg_no_money";
        return null;
    }

    public string UpgradeHall(Room r, bool busbar)
    {
        var why = CanUpgradeHall(r, busbar);
        if (why != null) return why;
        Spend(busbar ? Catalog.BusbarCost : HallUpgradeCost(r));
        r.Upgrade = busbar ? 2 : 1;
        r.UpgradeLeft = busbar ? Catalog.BusbarHours : Catalog.Containments[r.Containment + 1].Hours;
        if (Creative.InstantBuild) FinishHallUpgrade(r);
        Touch();
        return null;
    }

    void FinishHallUpgrade(Room r)
    {
        if (r.Upgrade == 1) r.Containment++;
        else if (r.Upgrade == 2) r.Busbar = true;
        r.Upgrade = 0; r.UpgradeLeft = 0;
        Touch();
    }

    /// <summary>Grid works, hall upgrades and hall temperatures move on with the clock.</summary>
    public void UpdateInfra(double hours)
    {
        var worksHours = GridRequest >= 0 && !Creative.InstantBuild ? GridQueueTick(hours) : hours;
        if (GridRequest >= 0 && (Creative.InstantBuild || worksHours > 0))
        {
            GridDaysLeft -= worksHours / 24;
            if (GridDaysLeft <= 0 || Creative.InstantBuild)
            {
                GridLevel = GridRequest; GridRequest = -1; GridDaysLeft = 0; GridQueueDays = 0;
                Notes.Add(("msg_grid_ready", Catalog.GridLevels[GridLevel].Mva.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                Touch(false);
            }
        }
        foreach (var b in Buildings.Where(b => b.Raising).ToList())
        {
            b.RaiseLeft -= hours;
            if (b.RaiseLeft <= 0 || Creative.InstantBuild) FinishLevel(b);
        }
        foreach (var r in Rooms.Where(r => r.Upgrade != 0))
        {
            r.UpgradeLeft -= hours;
            if (r.UpgradeLeft <= 0 || Creative.InstantBuild)
            {
                Notes.Add((r.Upgrade == 2 ? "msg_busbar_done" : "msg_hall_upgrade_done", Catalog.Containments[r.Containment + (r.Upgrade == 1 ? 1 : 0)].NameKey));
                FinishHallUpgrade(r);
            }
        }
        var report = Infra;
        foreach (var h in report.Halls)
        {
            var r = h.Room;
            var target = h.Target;
            var k = Math.Min(1, hours / 1.5);
            r.Temp += (target - r.Temp) * k;
            if (!r.Hot && r.Temp >= Sim.Infra.ShutdownTemp && !Creative.NoCoolingLimit)
            {
                r.Hot = true; Notes.Add(("msg_hall_hot", null)); InvalidateInfra();
            }
            else if (r.Hot && (h.CoolRatio <= 1 || Creative.NoCoolingLimit))
            {
                r.Hot = false; Notes.Add(("msg_hall_cool", null)); InvalidateInfra();
            }
        }
    }

    /// <summary>Advance the clock: infrastructure, network, organisation and money move on.</summary>
    public void Advance(double hours)
    {
        if (Bankrupt) return;
        var before = (int)(Hours / 24);
        var hourBefore = (int)Hours;
        Hours += hours;
        RefreshRooms();
        UpdateOps(hours);
        UpdateInfra(hours);
        UpdateNetwork(hours, hourBefore);
        StatsTick(hours);
        var days = (int)(Hours / 24) - before;
        for (var i = 0; i < days; i++)
        {
            var day = before + i + 1;
            NetworkDay(day);
            OrgDay(day);
            var rng = OpsRng(day);
            StaffDay(day, rng);
            OpsDay(day, rng);
            ResearchDay();
            CertDay();
            ReputationDay(day, rng);
            GrowthDay(day);
            MoneyDay(day);
            ProgressDay(day);
        }
    }
}
