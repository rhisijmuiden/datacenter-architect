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
    public Facade Facade { get; set; }
    /// <summary>1 = low, 2 = normal, 3 = high. Builders work on higher priority projects first.</summary>
    public int Priority { get; set; } = 2;
    public Stage Stage { get; set; }
    /// <summary>Material delivered to the site for the current stage.</summary>
    public int Material { get; set; }
    /// <summary>Builder-hours spent on the current stage.</summary>
    public double Work { get; set; }
    public bool Contains(int x, int z) => x >= X && z >= Z && x < X + W && z < Z + D;
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
    public int Priority { get; set; } = 2;
    public int Material { get; set; }
    public double Work { get; set; }
    public bool Contains(int x, int z) => x >= X && z >= Z && x < X + W && z < Z + D;
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
    /// <summary>Loading dock room where a delivered item waits.</summary>
    public int Dock { get; set; }
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
    /// <summary>0 = easy, 1 = normal, 2 = hard. Sets the starting balance.</summary>
    public int Difficulty { get; set; } = 1;
    public bool Creative { get; set; }
    public CreativeOptions CreativeOptions { get; set; } = new();
    public long StartMoney => Difficulty switch { 0 => 25_000_000, 2 => 6_000_000, _ => 12_000_000 };
    [JsonIgnore] public CreativeOptions C => Creative ? CreativeOptions : None;
    static readonly CreativeOptions None = new();
}

/// <summary>All state that goes into a savegame. Workers and trucks are not saved; they restart from this state.</summary>
public class GameState
{
    public const int CurrentFormat = 2;
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
    public int NextId { get; set; } = 1;
    public int MaterialStock { get; set; }
    public int Builders { get; set; } = 4;
    public int Fitters { get; set; } = 2;
    public int Engineers { get; set; } = 1;
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

    public static readonly DateTime Epoch = new(2027, 1, 1);
    [JsonIgnore] public DateTime Date => Epoch.AddHours(Hours);
    [JsonIgnore] public CreativeOptions Creative => Options.C;

    /// <summary>Cells of the grid operator's connection building, near the road.</summary>
    [JsonIgnore] public (int x, int z, int w, int d) GridStation => (PlotSize - 6, PlotSize - 5, 4, 3);

    public static GameState New(NewGameOptions o)
    {
        var s = new GameState { Options = o, Money = o.StartMoney };
        s.EnsureGrids();
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
            SaveFormat = CurrentFormat;
        }
        Touch();
    }

    int Idx(int x, int z) => z * PlotSize + x;
    public bool InPlot(int x, int z) => x >= 0 && z >= 0 && x < PlotSize && z < PlotSize;
    public Surface SurfaceAt(int x, int z) => InPlot(x, z) ? (Surface)Surfaces[Idx(x, z)] : Surface.Grass;
    public FenceType FenceAt(int x, int z) => InPlot(x, z) ? (FenceType)Fences[Idx(x, z)] : FenceType.None;

    // ---------------- money ----------------
    public bool CanAfford(long cost) => Creative.UnlimitedMoney || Money >= cost;
    public bool Spend(long cost)
    {
        if (Creative.UnlimitedMoney) return true;
        if (Money < cost) return false;
        Money -= cost; return true;
    }
    public void Refund(long amount) { if (!Creative.UnlimitedMoney) Money += amount; }

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
    public PlacedObject ObjectAt(int x, int z, int floor = 0) => Objects.FirstOrDefault(o =>
    {
        if (o.Floor != floor && o.Kind != ObjKind.Lift) return false;
        var (w, d) = Footprint(o);
        return x >= o.X && x < o.X + w && z >= o.Z && z < o.Z + d;
    });

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
            if (b.Floors > floor && (!builtOnly || b.Stage == Stage.Done))
                foreach (var e in Perimeter(b.X, b.Z, b.W, b.D)) set.Add(e);
        foreach (var r in Rooms)
            if (r.Floor == floor && (!builtOnly || r.Stage == Stage.Done))
                foreach (var e in Perimeter(r.X, r.Z, r.W, r.D)) set.Add(e);
        return set;
    }

    public bool IsOuterEdge((int x, int z, int dir) e) => Buildings.Any(b => Perimeter(b.X, b.Z, b.W, b.D).Contains(e));

    // ---------------- construction numbers ----------------
    public static int MaterialNeed(Building b) => b.Stage switch
    {
        Stage.Foundation => (int)Math.Ceiling(b.W * b.D / 5.0),
        Stage.Walls => (b.W + b.D) * b.Floors + (b.Floors - 1) * (int)Math.Ceiling(b.W * b.D / 8.0),
        Stage.Roof => (int)Math.Ceiling(b.W * b.D / 8.0),
        _ => 0,
    };
    public static int MaterialNeed(Room r) => r.Stage == Stage.Done ? 0 : (int)Math.Ceiling((r.W + r.D) * 2 / 3.0) + 1;
    public static double WorkNeed(Building b) => MaterialNeed(b) * WorkPerMaterial;
    public static double WorkNeed(Room r) => MaterialNeed(r) * WorkPerMaterial;
    public static long BuildingCost(BuildingKind k, int w, int d, int floors) => Catalog.Buildings[k].CostPerCellPerFloor * w * d * floors;

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
        if (changed > 0) Touch();
        return changed == 0 ? "msg_nothing_changed" : null;
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
        if (def.NeedsResearch && !Creative.AllUnlocks) return "msg_needs_research";
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
    public string CanBuild(BuildingKind k, int x, int z, int w, int d, int floors)
    {
        if (w < 4 || d < 4) return "msg_too_small";
        if (floors < 1 || floors > Catalog.MaxFloors) return "msg_bad_floors";
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
        if (!CanAfford(BuildingCost(k, w, d, floors))) return "msg_no_money";
        return null;
    }

    public static Facade DefaultFacade(BuildingKind k) => k switch { BuildingKind.Office => Facade.Brick, BuildingKind.Warehouse => Facade.Cladding, _ => Facade.Concrete };

    public Building AddBuilding(BuildingKind k, int x, int z, int w, int d, int floors, Facade? facade = null)
    {
        if (CanBuild(k, x, z, w, d, floors) != null) return null;
        Spend(BuildingCost(k, w, d, floors));
        var f = k == BuildingKind.Office && facade.HasValue ? facade.Value : DefaultFacade(k);
        var b = new Building { Id = NextId++, Kind = k, Facade = f, X = x, Z = z, W = w, D = d, Floors = floors, Stage = Creative.InstantBuild ? Stage.Done : Stage.Foundation };
        Buildings.Add(b);
        Touch();
        return b;
    }

    public string CanRoom(RoomType t, int x, int z, int w, int d, int floor)
    {
        if (w < 2 || d < 2) return "msg_too_small";
        var b = BuildingAt(x, z);
        if (b == null || !b.Contains(x + w - 1, z + d - 1)) return "msg_room_in_building";
        if (b.Floors <= floor) return "msg_no_such_floor";
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
            if (door.Floor > 0 && IsOuterEdge(e)) return "msg_door_upper_outer";
        }
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
        return door;
    }

    public Door DoorOn((int x, int z, int dir) e, int floor) => Doors.FirstOrDefault(d => d.Floor == floor && d.Edges().Contains(e));

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
            switch (def.Where)
            {
                case Where.Outside:
                case Where.Paved:
                    if (b != null || o.Floor > 0) return "msg_outside_only";
                    if (FenceAt(x, z) != FenceType.None) return "msg_fence_in_way";
                    if (def.Where == Where.Paved && !Catalog.Surfaces[SurfaceAt(x, z)].Trucks) return "msg_needs_asphalt";
                    break;
                case Where.Fence:
                    if (FenceAt(x, z) == FenceType.None) return "msg_needs_fence";
                    break;
                case Where.Building:
                    if (b == null || o.Floor != 0) return "msg_inside_building";
                    if (b.Floors < 2) return "msg_lift_floors";
                    break;
                case Where.Room:
                    if (room == null || room.Type == RoomType.DataHall) return "msg_needs_room";
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
        if (!CanAfford(def.Cost)) return "msg_no_money";
        return null;
    }

    public PlacedObject AddObject(ObjKind k, int x, int z, int rot, int floor)
    {
        var o = new PlacedObject { Kind = k, X = x, Z = z, Rot = rot, Floor = floor };
        if (CanPlace(o) != null) return null;
        o.Id = NextId++;
        Spend(o.Def.Cost);
        o.Status = o.Def.Chain && !Creative.InstantBuild ? ObjStatus.Ordered : ObjStatus.Live;
        Objects.Add(o);
        Touch();
        return o;
    }

    /// <summary>A row of objects (racks) placed side by side along one axis, all facing the same way.</summary>
    public static List<(int x, int z)> RowCells(int ax, int az, int bx, int bz, ObjKind k, int rot)
    {
        var def = Catalog.Objects[k];
        var (w, d) = rot % 2 == 0 ? (def.W, def.D) : (def.D, def.W);
        var list = new List<(int, int)>();
        var alongX = Math.Abs(bx - ax) >= Math.Abs(bz - az);
        var step = alongX ? w : d;
        var len = alongX ? bx - ax : bz - az;
        var n = Math.Min(Catalog.MaxRow, Math.Abs(len) / step + 1);
        var sign = len < 0 ? -1 : 1;
        for (var i = 0; i < n; i++) list.Add(alongX ? (ax + sign * i * step, az) : (ax, az + sign * i * step));
        return list;
    }

    // ---------------- erase ----------------
    /// <summary>Remove what lies in a rectangle on a floor: objects, doors, rooms, then buildings and paving.</summary>
    public bool Erase(int x0, int z0, int w, int d, int floor)
    {
        bool In(int x, int z) => x >= x0 && z >= z0 && x < x0 + w && z < z0 + d;
        var changed = false;
        foreach (var o in Objects.Where(o => (o.Floor == floor || (o.Kind == ObjKind.Lift && floor == 0)) && Cells(o).Any(c => In(c.x, c.z))).ToList())
        {
            Objects.Remove(o); Refund(o.Def.Cost / 2); changed = true;
        }
        foreach (var door in Doors.Where(o => o.Floor == floor && In(o.X, o.Z)).ToList()) { Doors.Remove(door); changed = true; }
        if (changed) { Touch(); return true; }
        foreach (var r in Rooms.Where(r => r.Floor == floor && In(r.X, r.Z) && In(r.X + r.W - 1, r.Z + r.D - 1)).ToList())
        {
            RemoveRoom(r); changed = true;
        }
        if (changed) { Touch(); return true; }
        foreach (var b in Buildings.Where(b => floor == 0 && In(b.X, b.Z) && In(b.X + b.W - 1, b.Z + b.D - 1)).ToList())
        {
            foreach (var r in Rooms.Where(r => r.BuildingId == b.Id).ToList()) RemoveRoom(r);
            Objects.RemoveAll(o => b.Contains(o.X, o.Z));
            Doors.RemoveAll(o => Perimeter(b.X, b.Z, b.W, b.D).Intersect(o.Edges()).Any());
            Buildings.Remove(b);
            Refund(BuildingCost(b.Kind, b.W, b.D, b.Floors) / 4);
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
        if (changed) Touch();
        return changed;
    }

    void RemoveRoom(Room r)
    {
        Objects.RemoveAll(o => o.Floor == r.Floor && o.Kind != ObjKind.Lift && r.Contains(o.X, o.Z));
        var edges = Perimeter(r.X, r.Z, r.W, r.D).ToHashSet();
        Doors.RemoveAll(o => o.Floor == r.Floor && o.Edges().Any(edges.Contains) && !o.Edges().Any(IsOuterEdge));
        Rooms.Remove(r);
        Refund(Catalog.RoomCost(r.W, r.D) / 4);
    }

    // ---------------- derived resources ----------------
    IEnumerable<PlacedObject> LiveObjects => Objects.Where(o => o.Status == ObjStatus.Live);
    [JsonIgnore] public double PowerDemandKw => LiveObjects.Sum(o => o.Def.PowerKw);
    [JsonIgnore] public double CoolingDemandKw => LiveObjects.Where(o => o.Kind == ObjKind.Rack).Sum(o => o.Def.PowerKw);
    [JsonIgnore] public double CoolingCapacityKw => LiveObjects.Sum(o => o.Def.CoolingKw);
    /// <summary>Backup capacity with N+1: the largest generator is kept in reserve.</summary>
    [JsonIgnore]
    public double BackupCapacityKw
    {
        get
        {
            var gens = Objects.Where(o => o.Kind == ObjKind.Generator).Select(o => o.Def.GenKw).OrderByDescending(k => k).ToList();
            return gens.Count <= 1 ? 0 : gens.Skip(1).Sum();
        }
    }
    [JsonIgnore] public double GridCapacityKw => 2000; // 2 MVA starting connection from the grid operator
    [JsonIgnore] public int Racks => Objects.Count(o => o.Kind == ObjKind.Rack);
    [JsonIgnore] public int LiveRacks => Objects.Count(o => o.Kind == ObjKind.Rack && o.Status == ObjStatus.Live);
    [JsonIgnore] public int FiberPits => Objects.Count(o => o.Kind == ObjKind.FiberPit);
    [JsonIgnore] public long WagesPerDay => Builders * Catalog.WagePerDay[Role.Builder] + Fitters * Catalog.WagePerDay[Role.Fitter] + Engineers * Catalog.WagePerDay[Role.Engineer];

    /// <summary>Advance the clock; costs and income are a placeholder until the economy step.</summary>
    public void Advance(double hours)
    {
        var before = (int)(Hours / 24);
        Hours += hours;
        var days = (int)(Hours / 24) - before;
        if (Creative.UnlimitedMoney) return;
        for (var i = 0; i < days; i++)
        {
            var energy = PowerDemandKw * 24 * 0.18;       // €0.18 per kWh
            var rent = LiveRacks * 900.0 / 30;            // €900 per rack per month
            Money += (long)Math.Round(rent - energy - 400 - WagesPerDay);
        }
    }
}
