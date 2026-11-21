using System;
using System.Collections.Generic;
using System.Linq;

namespace DatacenterArchitect.Sim;

/// <summary>A wall edge in a blueprint, relative to the blueprint's corner. Dir as <see cref="Wall"/>.</summary>
public class BlueprintEdge
{
    public int X { get; set; }
    public int Z { get; set; }
    public int Dir { get; set; }
    public bool Glass { get; set; }
}

/// <summary>A door in a blueprint, relative to the blueprint's corner.</summary>
public class BlueprintDoor
{
    public int X { get; set; }
    public int Z { get; set; }
    public int Dir { get; set; }
    public DoorType Type { get; set; }
}

/// <summary>An object in a blueprint (rack, PDU, CRAH, edge switch ...), relative to the blueprint's corner.</summary>
public class BlueprintObject
{
    public ObjKind Kind { get; set; }
    public int X { get; set; }
    public int Z { get; set; }
    public int Rot { get; set; }
    public FeedSide Feed { get; set; }
}

/// <summary>
/// Infra sketch board 8 (Reinder, 2026-11-21): a hall designed once and saved as a template, with everything in it
/// (walls, doors, racks, PDUs, edge switches, CRAHs, containment and busbar), to be placed again next to it.
/// It keeps what stands in the hall, never who rents it. Coordinates are relative to the corner of the W × D box.
/// Blueprints belong to the player profile (<see cref="Core.BlueprintLibrary"/>), so they work in every game.
/// </summary>
public class Blueprint
{
    public const int MaxSize = 120, MaxObjects = 4000;

    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public RoomType Type { get; set; } = RoomType.DataHall;
    public int W { get; set; }
    public int D { get; set; }
    /// <summary>A rectangle with its own walls (placed as a walled room). False: a painted room inside separate walls.</summary>
    public bool Walled { get; set; } = true;
    /// <summary>Floor cells (<see cref="Room.Key"/> of the relative cell). Empty means the whole W × D box.</summary>
    public List<int> Cells { get; set; } = new();
    /// <summary>Walls that come with the hall: for a painted hall its whole boundary plus the walls inside; for a
    /// walled hall only the walls inside (the room brings its own around it).</summary>
    public List<BlueprintEdge> Walls { get; set; } = new();
    public List<BlueprintDoor> Doors { get; set; } = new();
    public List<BlueprintObject> Objects { get; set; } = new();
    public Containment Containment { get; set; }
    public bool Busbar { get; set; }
    /// <summary>When it was saved (yyyy-MM-dd), for the library list.</summary>
    public string Saved { get; set; } = "";

    public IEnumerable<(int x, int z)> CellList()
    {
        if (Cells.Count > 0) { foreach (var k in Cells) yield return Room.Unkey(k); yield break; }
        for (var z = 0; z < D; z++)
            for (var x = 0; x < W; x++) yield return (x, z);
    }

    public int Area => Cells.Count > 0 ? Cells.Count : W * D;
    public int Count(ObjKind k) => Objects.Count(o => o.Kind == k);
    public int Count(Func<ObjKind, bool> f) => Objects.Count(o => f(o.Kind));
    public int Racks => Count(ObjKind.Rack);
    public double RackKw => Objects.Where(o => o.Kind == ObjKind.Rack).Sum(o => Catalog.Objects[o.Kind].PowerKw);

    /// <summary>Whether the data is sane (a library file can be edited or broken by hand).</summary>
    public bool Valid()
    {
        if (W < 2 || D < 2 || W > MaxSize || D > MaxSize) return false;
        if (!Enum.IsDefined(Type) || !Enum.IsDefined(Containment)) return false;
        if (Name == null || Name.Length > 60) return false;
        Cells ??= new(); Walls ??= new(); Doors ??= new(); Objects ??= new();
        if (Objects.Count > MaxObjects || Walls.Count > 8 * MaxSize || Doors.Count > 4 * MaxSize || Cells.Count > W * D) return false;
        foreach (var (x, z) in CellList()) if (x < 0 || z < 0 || x >= W || z >= D) return false;
        foreach (var e in Walls) if (e.Dir is < 0 or > 1 || e.X < 0 || e.Z < 0 || e.X > W || e.Z > D) return false;
        foreach (var d in Doors) if (!Enum.IsDefined(d.Type) || d.Dir is < 0 or > 1 || d.X < 0 || d.Z < 0 || d.X > W || d.Z > D) return false;
        foreach (var o in Objects)
        {
            if (!Enum.IsDefined(o.Kind) || !Catalog.Objects.ContainsKey(o.Kind) || Catalog.IsShaft(o.Kind) || o.Rot is < 0 or > 3 || !Enum.IsDefined(o.Feed)) return false;
            if (o.X < 0 || o.Z < 0 || o.X >= W || o.Z >= D) return false;
        }
        return true;
    }

    public Blueprint Copy()
    {
        var b = (Blueprint)MemberwiseClone();
        b.Cells = new List<int>(Cells);
        b.Walls = Walls.Select(e => new BlueprintEdge { X = e.X, Z = e.Z, Dir = e.Dir, Glass = e.Glass }).ToList();
        b.Doors = Doors.Select(d => new BlueprintDoor { X = d.X, Z = d.Z, Dir = d.Dir, Type = d.Type }).ToList();
        b.Objects = Objects.Select(o => new BlueprintObject { Kind = o.Kind, X = o.X, Z = o.Z, Rot = o.Rot, Feed = o.Feed }).ToList();
        return b;
    }

    // ---------------- turning and mirroring ----------------

    /// <summary>
    /// The blueprint turned a quarter <paramref name="rot"/> times (R) and, first, mirrored left to right (M).
    /// Racks keep facing their aisles: a mirrored rack row faces the mirrored aisle.
    /// </summary>
    public Blueprint Oriented(int rot, bool mirror)
    {
        var b = Copy();
        if (mirror) b.Transform(c => (W - 1 - c.x, c.z), v => (-v.dx, v.dz), W, D);
        for (var i = 0; i < ((rot % 4) + 4) % 4; i++)
        {
            var d0 = b.D;
            b.Transform(c => (d0 - 1 - c.z, c.x), v => (-v.dz, v.dx), b.D, b.W);
        }
        return b;
    }

    void Transform(Func<(int x, int z), (int x, int z)> cell, Func<(int dx, int dz), (int dx, int dz)> vec, int newW, int newD)
    {
        var full = Cells.Count == 0;
        if (!full) Cells = Cells.Select(k => { var c = cell(Room.Unkey(k)); return Room.Key(c.x, c.z); }).OrderBy(k => k).ToList();
        (int x, int z, int dir) Edge(int x, int z, int dir)
        {
            var (a, b) = GameState.Sides((x, z, dir));
            var ta = cell(a); var tb = cell(b);
            return ta.z == tb.z ? (Math.Max(ta.x, tb.x), ta.z, 0) : (ta.x, Math.Max(ta.z, tb.z), 1);
        }
        foreach (var e in Walls) { var t = Edge(e.X, e.Z, e.Dir); e.X = t.x; e.Z = t.z; e.Dir = t.dir; }
        foreach (var d in Doors)
        {
            var edges = new Door { Type = d.Type, X = d.X, Z = d.Z, Dir = d.Dir }.Edges().Select(e => Edge(e.x, e.z, e.dir)).ToList();
            var first = edges.OrderBy(e => e.x).ThenBy(e => e.z).First();
            d.X = first.x; d.Z = first.z; d.Dir = first.dir;
        }
        foreach (var o in Objects)
        {
            var po = new PlacedObject { Kind = o.Kind, X = o.X, Z = o.Z, Rot = o.Rot };
            var cells = GameState.Cells(po).Select(cell).ToList();
            o.X = cells.Min(c => c.x); o.Z = cells.Min(c => c.z);
            var f = vec(GameState.FrontDir(o.Rot));
            o.Rot = Enumerable.Range(0, 4).First(r => GameState.FrontDir(r) == f);
        }
        W = newW; D = newD;
    }
}

public partial class GameState
{
    // ---------------- saving a hall as a blueprint ----------------

    /// <summary>
    /// Takes a hall's design as a blueprint: its floor, the walls around and inside it, its doors, every object in it
    /// (not lifts or stairs, which belong to the building), containment and busbar. Tenants and names stay behind.
    /// </summary>
    public Blueprint CaptureBlueprint(Room r, string name)
    {
        if (r == null) return null;
        var cells = r.CellList().ToList();
        var bp = new Blueprint
        {
            Id = Guid.NewGuid().ToString("N")[..12], Name = name ?? "", Type = r.Type, W = r.W, D = r.D, Walled = r.Walled,
            Containment = r.Containment, Busbar = r.Busbar, Saved = Date.ToString("yyyy-MM-dd"),
        };
        bool In(int x, int z) => r.Contains(x, z);
        if (!r.Walled) bp.Cells = cells.Select(c => Room.Key(c.x - r.X, c.z - r.Z)).OrderBy(k => k).ToList();
        // walls: separate walls on or inside the hall, plus (painted hall) every boundary edge, whatever closes it now
        var glass = Walls.Where(w => w.Floor == r.Floor && w.Glass).SelectMany(w => w.Edges()).ToHashSet();
        var all = WallEdges(r.Floor, false);
        var boundary = r.Walled ? new HashSet<(int x, int z, int dir)>() : Boundary(r);
        var keep = new HashSet<(int x, int z, int dir)>(boundary);
        foreach (var e in all)
        {
            var (a, b) = Sides(e);
            if (In(a.x, a.z) && In(b.x, b.z)) keep.Add(e);
        }
        foreach (var e in keep.OrderBy(e => e.dir).ThenBy(e => e.z).ThenBy(e => e.x))
            bp.Walls.Add(new BlueprintEdge { X = e.x - r.X, Z = e.z - r.Z, Dir = e.dir, Glass = glass.Contains(e) });
        // doors in those walls (and, for a walled hall, in its own rectangle)
        var mine = new HashSet<(int x, int z, int dir)>(keep);
        if (r.Walled) foreach (var e in Perimeter(r.X, r.Z, r.W, r.D)) mine.Add(e);
        foreach (var d in Doors.Where(d => d.Floor == r.Floor && d.Edges().All(mine.Contains)))
            bp.Doors.Add(new BlueprintDoor { X = d.X - r.X, Z = d.Z - r.Z, Dir = d.Dir, Type = d.Type });
        foreach (var o in Objects.Where(o => o.Floor == r.Floor && !Catalog.IsShaft(o.Kind) && Cells(o).All(c => In(c.x, c.z))).OrderBy(o => o.Id))
            bp.Objects.Add(new BlueprintObject { Kind = o.Kind, X = o.X - r.X, Z = o.Z - r.Z, Rot = o.Rot, Feed = o.Feed });
        return bp;
    }

    // ---------------- placing a blueprint ----------------

    /// <summary>What placing a blueprint at a corner adds up to, for the ghost and the check.</summary>
    public sealed class BlueprintFit
    {
        public string Why;
        public long Cost, RoomCost, WallCost, DoorCost, ObjectCost, FitOut;
        /// <summary>New wall edges to build (edges that already have a wall are left out).</summary>
        public List<(int x, int z, int dir, bool glass)> NewWalls = new();
        public Containment Containment;
        public bool CappedContainment;
        public Building Building;
    }

    /// <summary>Price of fitting a new hall out to this containment level and busbar (what the upgrades would cost).</summary>
    public static long FitOutCost(int area, Containment c, bool busbar)
    {
        long sum = 0;
        for (var l = Containment.HotCold; l <= c; l++) sum += Catalog.Containments[l].CostPerM2 * area;
        return sum + (busbar ? Catalog.BusbarCost : 0);
    }

    /// <summary>Whether the (already turned) blueprint fits with its corner at (x0, z0) on a floor, and what it costs.</summary>
    public BlueprintFit FitBlueprint(Blueprint bp, int x0, int z0, int floor)
    {
        var fit = new BlueprintFit();
        string Fail(string why) { fit.Why = why; return why; }
        var cells = bp.CellList().Select(c => (x: c.x + x0, z: c.z + z0)).ToList();
        var set = cells.ToHashSet();
        Building b = null;
        foreach (var (x, z) in cells)
        {
            if (!InPlot(x, z)) { Fail("msg_outside_plot"); break; }
            var here = BuildingAt(x, z);
            if (here == null || !here.HasFloor(floor)) { Fail("msg_bp_in_building"); break; }
            if (b != null && here != b) { Fail("msg_bp_one_building"); break; }
            b = here;
            if (RoomAt(x, z, floor) != null) { Fail("msg_bp_room_in_way"); break; }
            if (ObjectAt(x, z, floor) != null) { Fail("msg_bp_object_in_way"); break; }
        }
        fit.Building = b;
        if (fit.Why == null && floor == BasementFloor && bp.Type is RoomType.LoadingDock or RoomType.Reception) Fail("msg_not_in_basement");
        // a wall already standing across the new hall would cut it in two
        var walls = WallEdges(floor, false);
        var bpEdges = bp.Walls.Select(e => (e.X + x0, e.Z + z0, e.Dir)).ToHashSet();
        if (fit.Why == null)
            foreach (var e in walls)
            {
                var (a, c) = Sides(e);
                if (set.Contains(a) && set.Contains(c) && !bpEdges.Contains(e)) { Fail("msg_bp_wall_in_way"); break; }
            }
        // walls to build: a painted hall's boundary and the walls inside, where none stands yet
        foreach (var e in bp.Walls)
        {
            var edge = (x: e.X + x0, z: e.Z + z0, dir: e.Dir);
            if (walls.Contains(edge)) continue;
            if (bp.Walled && IsOuterEdge(edge)) continue;
            var (a, c) = Sides(edge);
            // an edge on the building's outside wall is already a wall; one with nothing on either side needs none
            var ba = BuildingAt(a.x, a.z); var bc = BuildingAt(c.x, c.z);
            if (ba == null || bc == null || ba != bc) continue;
            fit.NewWalls.Add((edge.x, edge.z, edge.dir, e.Glass));
        }
        fit.RoomCost = bp.Walled ? Catalog.RoomCost(bp.W, bp.D) : 0;
        fit.WallCost = fit.NewWalls.Sum(w => WallCost(1, w.glass));
        fit.DoorCost = bp.Doors.Sum(d => Catalog.Doors[d.Type].Cost);
        fit.ObjectCost = bp.Objects.Sum(o => Catalog.Objects[o.Kind].Cost);
        fit.Containment = bp.Containment;
        if (fit.Containment == Containment.ColdCorridor && !Done("cool_corridor")) { fit.Containment = Containment.HotCold; fit.CappedContainment = true; }
        fit.FitOut = FitOutCost(bp.Area, fit.Containment, bp.Busbar);
        fit.Cost = fit.RoomCost + fit.WallCost + fit.DoorCost + fit.ObjectCost + fit.FitOut;
        if (fit.Why == null && bp.Doors.Any(d => d.Type == DoorType.Mantrap) && !Done("sec_mantrap")) Fail("msg_needs_research");
        if (fit.Why == null && !CanAfford(fit.Cost)) Fail("msg_no_money");
        return fit;
    }

    /// <summary>What happened when a blueprint was placed.</summary>
    public sealed class BlueprintResult
    {
        public string Why;
        public Room Room;
        public int Objects, Doors, Walls, Skipped;
        public long Cost;
    }

    /// <summary>
    /// Places a (turned) blueprint with its corner at (x0, z0): the hall, its walls and doors, and every object go on
    /// the building list in one go (paid at once, or free in plan mode); the crews build it themselves.
    /// Objects or doors that no longer fit (an outside door on an upper floor, say) are skipped and counted.
    /// </summary>
    public BlueprintResult PlaceBlueprint(Blueprint bp, int x0, int z0, int floor)
    {
        var res = new BlueprintResult();
        var fit = FitBlueprint(bp, x0, z0, floor);
        if (fit.Why != null) { res.Why = fit.Why; return res; }
        var money = Money;
        var b = fit.Building;
        Room room;
        if (bp.Walled)
        {
            room = AddRoom(bp.Type, x0, z0, bp.W, bp.D, floor);
            if (room == null) { res.Why = CanRoom(bp.Type, x0, z0, bp.W, bp.D, floor) ?? "msg_bp_room_in_way"; return res; }
        }
        else
        {
            room = new Room { Id = NextId++, BuildingId = b.Id, Floor = floor, Type = bp.Type, Walled = false, Stage = Stage.Walls };
            room.SetCells(bp.CellList().Select(c => (c.x + x0, c.z + z0)));
            Rooms.Add(room);
        }
        // separate walls, grouped into straight runs of the same kind
        foreach (var kind in new[] { false, true })
        {
            var edges = fit.NewWalls.Where(w => w.glass == kind).Select(w => (w.x, w.z, w.dir)).ToList();
            if (edges.Count == 0) continue;
            Spend(WallCost(edges.Count, kind));
            foreach (var run in Runs(edges))
            {
                Walls.Add(new Wall
                {
                    Id = NextId++, BuildingId = b.Id, Floor = floor, X = run[0].x, Z = run[0].z, Dir = run[0].dir, Len = run.Count, Glass = kind,
                    Stage = Creative.InstantBuild ? Stage.Done : Stage.Walls,
                });
                res.Walls++;
            }
        }
        // the hall comes fitted out: containment and busbar go in with the walls
        Spend(fit.FitOut);
        room.Containment = fit.Containment;
        room.Busbar = bp.Busbar;
        Touch();
        foreach (var d in bp.Doors)
        {
            if (AddDoor(d.Type, d.X + x0, d.Z + z0, d.Dir, floor) != null) res.Doors++;
            else res.Skipped++;
        }
        // blocking objects first is not needed: the layout was valid where it came from, so every object fits again
        foreach (var o in bp.Objects)
        {
            var po = AddObject(o.Kind, o.X + x0, o.Z + z0, o.Rot, floor);
            if (po == null) { res.Skipped++; continue; }
            if (Catalog.HasFeed(o.Kind)) po.Feed = o.Feed;
            res.Objects++;
        }
        Touch();
        RefreshRooms();
        res.Room = Rooms.FirstOrDefault(r => r.Id == room.Id) ?? room;
        res.Cost = money - Money;
        return res;
    }
}
