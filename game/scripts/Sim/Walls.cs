using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace DatacenterArchitect.Sim;

/// <summary>
/// A straight run of inner wall on cell edges inside a building (Reinder, 2026-11-21): the player builds one big
/// hall and divides it with walls. Dir 0 = the line x = X running along z; dir 1 = the line z = Z running along x
/// (the same edges doors use). Builders put it up like a room's walls.
/// </summary>
public class Wall
{
    public int Id { get; set; }
    public int BuildingId { get; set; }
    public int Floor { get; set; }
    public int X { get; set; }
    public int Z { get; set; }
    public int Dir { get; set; }
    public int Len { get; set; }
    /// <summary>A glass partition: works the same, you look through it.</summary>
    public bool Glass { get; set; }
    public Stage Stage { get; set; } = Stage.Walls;
    public int Priority { get; set; } = 2;
    public int Material { get; set; }
    public double Work { get; set; }

    public IEnumerable<(int x, int z, int dir)> Edges()
    {
        for (var i = 0; i < Len; i++) yield return Dir == 0 ? (X, Z + i, 0) : (X + i, Z, 1);
    }

    public Wall Copy() => (Wall)MemberwiseClone();
}

/// <summary>What the bulldozer takes away.</summary>
public enum EraseKind { All, Room, Wall, Door, Interior }

public partial class GameState
{
    public List<Wall> Walls { get; set; } = new();

    public const long WallCostPerMetre = 300, GlassCostPerMetre = 450;
    public static long WallCost(int len, bool glass) => len * (glass ? GlassCostPerMetre : WallCostPerMetre);
    public static int MaterialNeed(Wall w) => w.Stage == Stage.Done ? 0 : (int)Math.Ceiling(w.Len / 3.0) + 1;
    public static double WorkNeed(Wall w) => MaterialNeed(w) * WorkPerMaterial;
    public static double StageProgress(Wall w) => w.Stage == Stage.Done ? 1 : WorkNeed(w) <= 0 ? 0 : Math.Clamp(w.Work / WorkNeed(w), 0, 1);

    /// <summary>The edges around a room: between a cell of the room and one outside it.</summary>
    public static HashSet<(int x, int z, int dir)> Boundary(Room r)
    {
        if (r.Cells.Count == 0) return Perimeter(r.X, r.Z, r.W, r.D).ToHashSet();
        var set = new HashSet<(int, int, int)>();
        foreach (var (x, z) in r.CellList())
        {
            if (!r.Contains(x - 1, z)) set.Add((x, z, 0));
            if (!r.Contains(x + 1, z)) set.Add((x + 1, z, 0));
            if (!r.Contains(x, z - 1)) set.Add((x, z, 1));
            if (!r.Contains(x, z + 1)) set.Add((x, z + 1, 1));
        }
        return set;
    }

    /// <summary>The two cells on either side of an edge.</summary>
    public static ((int x, int z) a, (int x, int z) b) Sides((int x, int z, int dir) e) =>
        e.dir == 0 ? ((e.x - 1, e.z), (e.x, e.z)) : ((e.x, e.z - 1), (e.x, e.z));

    /// <summary>The edges of a straight wall dragged from grid corner (ax, az) to (bx, bz), along the longer direction.</summary>
    public static List<(int x, int z, int dir)> WallLine(int ax, int az, int bx, int bz)
    {
        var list = new List<(int, int, int)>();
        if (Math.Abs(bx - ax) >= Math.Abs(bz - az))
            for (var x = Math.Min(ax, bx); x < Math.Max(ax, bx); x++) list.Add((x, az, 1));
        else
            for (var z = Math.Min(az, bz); z < Math.Max(az, bz); z++) list.Add((ax, z, 0));
        return list;
    }

    /// <summary>The building an edge lies inside of (both sides in it), or null.</summary>
    Building InnerEdgeBuilding((int x, int z, int dir) e, int floor)
    {
        var (a, b) = Sides(e);
        var ba = BuildingAt(a.x, a.z);
        return ba != null && ba.HasFloor(floor) && ba.Contains(b.x, b.z) ? ba : null;
    }

    /// <summary>Which of these edges can get a wall now, and why not when none can.</summary>
    public string CanWalls(int floor, IEnumerable<(int x, int z, int dir)> line, out List<(int x, int z, int dir)> free, bool glass = false)
    {
        free = new();
        var walls = WallEdges(floor, false);
        var doors = Doors.Where(d => d.Floor == floor).SelectMany(d => d.Edges()).ToHashSet();
        string why = null;
        foreach (var e in line)
        {
            if (InnerEdgeBuilding(e, floor) == null) { why ??= "msg_wall_in_building"; continue; }
            if (walls.Contains(e) || doors.Contains(e)) continue;
            var (a, b) = Sides(e);
            var oa = ObjectAt(a.x, a.z, floor);
            if (oa != null && oa == ObjectAt(b.x, b.z, floor)) { why ??= "msg_wall_through_object"; continue; }
            free.Add(e);
        }
        if (free.Count == 0) return why ?? "msg_nothing_changed";
        if (!CanAfford(WallCost(free.Count, glass))) return "msg_no_money";
        return null;
    }

    /// <summary>Put up a wall along a drag. Edges that already have a wall or door are skipped; each straight
    /// stretch becomes one wall. Returns the new walls (empty when nothing could be placed).</summary>
    public List<Wall> AddWalls(int floor, int ax, int az, int bx, int bz, bool glass = false)
    {
        var added = new List<Wall>();
        if (CanWalls(floor, WallLine(ax, az, bx, bz), out var free, glass) != null) return added;
        Spend(WallCost(free.Count, glass));
        foreach (var run in Runs(free))
        {
            var b = InnerEdgeBuilding(run[0], floor);
            var w = new Wall
            {
                Id = NextId++, BuildingId = b.Id, Floor = floor, X = run[0].x, Z = run[0].z, Dir = run[0].dir, Len = run.Count, Glass = glass,
                Stage = Creative.InstantBuild ? Stage.Done : Stage.Walls,
            };
            Walls.Add(w); added.Add(w);
        }
        Touch();
        RefreshRooms();
        return added;
    }

    /// <summary>Consecutive edges along one line, grouped into straight runs (one building each).</summary>
    List<List<(int x, int z, int dir)>> Runs(IEnumerable<(int x, int z, int dir)> edges)
    {
        var runs = new List<List<(int, int, int)>>();
        foreach (var g in edges.GroupBy(e => (e.dir, line: e.dir == 0 ? e.x : e.z)))
        {
            var sorted = g.OrderBy(e => e.dir == 0 ? e.z : e.x).ToList();
            List<(int x, int z, int dir)> cur = null;
            foreach (var e in sorted)
            {
                var along = e.dir == 0 ? e.z : e.x;
                if (cur != null)
                {
                    var last = cur[^1];
                    var lastAlong = last.dir == 0 ? last.z : last.x;
                    if (along == lastAlong + 1 && BuildingAt(Sides(e).b.x, Sides(e).b.z) == BuildingAt(Sides(last).b.x, Sides(last).b.z)) { cur.Add(e); continue; }
                }
                cur = new List<(int x, int z, int dir)> { e };
                runs.Add(cur);
            }
        }
        return runs;
    }

    /// <summary>Take wall edges away: separate walls are split around them, and the doors in those edges go too.
    /// A quarter of the price comes back (nothing for a planned wall). Returns how many metres went.</summary>
    int RemoveWallEdges(int floor, ICollection<(int x, int z, int dir)> edges)
    {
        var gone = 0;
        var set = edges.ToHashSet();
        foreach (var w in Walls.Where(w => w.Floor == floor && w.Edges().Any(set.Contains)).ToList())
        {
            var keep = w.Edges().Where(e => !set.Contains(e)).ToList();
            var cut = w.Len - keep.Count;
            gone += cut;
            var planned = IsPlanned(w.Id);
            Walls.Remove(w);
            if (planned) Planned.Remove(w.Id);
            else Refund(WallCost(cut, w.Glass) / 4);
            var first = true;
            foreach (var run in Runs(keep))
            {
                var piece = w.Copy();
                if (!first) piece.Id = NextId++;
                piece.X = run[0].x; piece.Z = run[0].z; piece.Len = run.Count;
                Walls.Add(piece);
                if (planned) Planned.Add(piece.Id);
                first = false;
            }
        }
        foreach (var d in Doors.Where(d => d.Floor == floor && d.Edges().Any(set.Contains)).ToList())
        {
            Doors.Remove(d);
            if (IsPlanned(d.Id)) Planned.Remove(d.Id);
        }
        if (gone > 0) Touch();
        return gone;
    }

    /// <summary>
    /// A room with its own walls (drawn as a rectangle) becomes a painted room inside separate walls, so its walls
    /// can be changed one by one. Nothing changes on screen or in the simulation. Returns the new room object.
    /// </summary>
    public Room Unwall(Room r)
    {
        if (!r.Walled) return r;
        var own = Perimeter(r.X, r.Z, r.W, r.D).Where(e => !IsOuterEdge(e)).ToList();
        // edges another walled room or a separate wall still holds stay theirs
        var others = new HashSet<(int, int, int)>();
        foreach (var o in Rooms.Where(o => o != r && o.Walled && o.Floor == r.Floor)) foreach (var e in Perimeter(o.X, o.Z, o.W, o.D)) others.Add(e);
        foreach (var w in Walls.Where(w => w.Floor == r.Floor)) foreach (var e in w.Edges()) others.Add(e);
        var planned = IsPlanned(r.Id);
        foreach (var run in Runs(own.Where(e => !others.Contains(e))))
        {
            var w = new Wall
            {
                Id = NextId++, BuildingId = r.BuildingId, Floor = r.Floor, X = run[0].x, Z = run[0].z, Dir = run[0].dir, Len = run.Count,
                Stage = r.Stage == Stage.Done ? Stage.Done : Stage.Walls,
                Material = r.Stage == Stage.Done ? 0 : r.Material, Work = 0,
            };
            Walls.Add(w);
            if (planned) Planned.Add(w.Id);
        }
        var n = r.Copy();
        n.Walled = false;
        n.SetCells(r.CellList().ToList());
        n.Material = 0; n.Work = 0;
        Rooms[Rooms.IndexOf(r)] = n;
        Touch();
        return n;
    }

    // ---------------- spaces: floor closed in by walls ----------------

    /// <summary>The spaces of one floor: every building cell gets a label; cells with the same label are not
    /// separated by a wall. Access: a door in the boundary, or stairs/a lift inside.</summary>
    public sealed class SpaceMap
    {
        public int N;
        public int[] Label;
        public readonly List<int> Size = new() { 0 };
        public readonly List<bool> Access = new() { false };
        public readonly List<bool> Done = new() { false };
        public int At(int x, int z) => x < 0 || z < 0 || x >= N || z >= N ? 0 : Label[z * N + x];
    }

    readonly Dictionary<(int floor, bool built), SpaceMap> _spaces = new();
    int _spacesRev = -1;

    /// <summary>Spaces of a floor. With built, only finished walls separate (what counts for the simulation);
    /// without, walls still being built separate too (what the player drew).</summary>
    public SpaceMap Spaces(int floor, bool built = true)
    {
        if (_spacesRev != LayoutRevision) { _spaces.Clear(); _spacesRev = LayoutRevision; }
        if (_spaces.TryGetValue((floor, built), out var m)) return m;
        m = new SpaceMap { N = PlotSize, Label = new int[PlotSize * PlotSize] };
        var walls = WallEdges(floor, built);
        var doors = Doors.Where(d => d.Floor == floor).SelectMany(d => d.Edges()).ToHashSet();
        var shafts = Objects.Where(o => Catalog.IsShaft(o.Kind)).SelectMany(o => Cells(o)).ToHashSet();
        var q = new Queue<(int x, int z)>();
        foreach (var b in Buildings.Where(b => b.HasFloor(floor)))
            for (var z0 = b.Z; z0 < b.Z + b.D; z0++)
                for (var x0 = b.X; x0 < b.X + b.W; x0++)
                {
                    if (!InPlot(x0, z0) || m.Label[z0 * m.N + x0] != 0) continue;
                    var id = m.Size.Count;
                    m.Size.Add(0); m.Access.Add(false); m.Done.Add(b.Stage == Stage.Done);
                    m.Label[z0 * m.N + x0] = id;
                    q.Enqueue((x0, z0));
                    while (q.Count > 0)
                    {
                        var (x, z) = q.Dequeue();
                        m.Size[id]++;
                        if (shafts.Contains((x, z))) m.Access[id] = true;
                        foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                        {
                            var e = dx == 1 ? (x + 1, z, 0) : dx == -1 ? (x, z, 0) : dz == 1 ? (x, z + 1, 1) : (x, z, 1);
                            if (walls.Contains(e))
                            {
                                if (doors.Contains(e)) m.Access[id] = true;
                                continue;
                            }
                            int nx = x + dx, nz = z + dz;
                            if (!b.Contains(nx, nz) || !InPlot(nx, nz) || m.Label[nz * m.N + nx] != 0) continue;
                            m.Label[nz * m.N + nx] = id;
                            q.Enqueue((nx, nz));
                        }
                    }
                }
        _spaces[(floor, built)] = m;
        return m;
    }

    /// <summary>Why a painted room does not work yet (not closed, no door, two types in one space), or null.</summary>
    [JsonIgnore] public Dictionary<int, string> RoomIssues { get; } = new();
    int _roomsRev = -1;

    /// <summary>
    /// Keeps painted rooms in step with the walls: a room cut in two by a new wall becomes two rooms, two rooms of
    /// the same type in one space become one, and a room works (Stage Done) only when its space is closed by
    /// finished walls, has a door (or stairs/a lift) and holds no other room.
    /// </summary>
    public void RefreshRooms()
    {
        if (_roomsRev == LayoutRevision) return;
        var changed = false;
        foreach (var f in FloorIds)
        {
            var painted = Rooms.Where(r => !r.Walled && r.Floor == f && !IsPlanned(r)).ToList();
            if (painted.Count == 0) continue;
            var m = Spaces(f, true);
            var drawn = Spaces(f, false);
            // split a room that now lies in more than one space
            foreach (var r in painted.ToList())
            {
                var groups = r.CellList().GroupBy(c => m.At(c.x, c.z)).OrderByDescending(g => g.Count()).ToList();
                if (groups.Count <= 1 && groups.All(g => g.Key != 0)) continue;
                var keep = r.Copy();
                var first = groups.FirstOrDefault(g => g.Key != 0);
                keep.SetCells(first?.ToList() ?? new List<(int x, int z)>());
                var i = Rooms.IndexOf(r);
                if (keep.Area == 0) Rooms.RemoveAt(i); else Rooms[i] = keep;
                painted.Remove(r);
                if (keep.Area > 0) painted.Add(keep);
                foreach (var g in groups.Where(g => g.Key != 0 && g != first))
                {
                    var part = r.Copy();
                    part.Id = NextId++;
                    part.Name = null;
                    part.SetCells(g.ToList());
                    Rooms.Add(part); painted.Add(part);
                }
                changed = true;
            }
            // merge rooms of the same type that share a space
            foreach (var g in painted.GroupBy(r => (m.At(r.CellList().First().x, r.CellList().First().z), r.Type)).Where(g => g.Count() > 1).ToList())
            {
                var rooms = g.OrderBy(r => r.Id).ToList();
                var into = rooms[0].Copy();
                into.SetCells(rooms.SelectMany(r => r.CellList()).ToList());
                Rooms[Rooms.IndexOf(rooms[0])] = into;
                foreach (var r in rooms.Skip(1)) { Rooms.Remove(r); painted.Remove(r); }
                painted.Remove(rooms[0]); painted.Add(into);
                changed = true;
            }
            // does each room work?
            var walledCells = new Dictionary<int, int>();
            foreach (var r in Rooms.Where(r => r.Walled && r.Floor == f))
                foreach (var (x, z) in r.CellList()) { var l = m.At(x, z); if (l != 0) walledCells[l] = r.Id; }
            var perSpace = painted.GroupBy(r => m.At(r.CellList().First().x, r.CellList().First().z)).ToDictionary(g => g.Key, g => g.Count());
            foreach (var r in painted.ToList())
            {
                var c0 = r.CellList().First();
                var l = m.At(c0.x, c0.z);
                string issue = null;
                // a wall the player drew around the space is not finished yet: the built space is bigger than the drawn one
                if (l == 0 || !m.Done[l] || m.Size[l] != drawn.Size[drawn.At(c0.x, c0.z)]) issue = "room_issue_building";
                else if (perSpace[l] > 1 || walledCells.ContainsKey(l)) issue = "room_issue_two_types";
                else if (!m.Access[l]) issue = "room_issue_no_door";
                if (issue == null) RoomIssues.Remove(r.Id); else RoomIssues[r.Id] = issue;
                var stage = issue == null ? Stage.Done : Stage.Walls;
                if (r.Stage == stage) continue;
                var n = r.Copy();
                n.Stage = stage;
                Rooms[Rooms.IndexOf(r)] = n;
                changed = true;
                if (stage == Stage.Done) RoomFinished(n);
            }
        }
        if (changed) Touch();
        _roomsRev = LayoutRevision;
    }

    /// <summary>The issue of a room for the inspector, or null when it works.</summary>
    public string RoomIssue(Room r)
    {
        if (r.Walled || IsPlanned(r)) return null;
        RefreshRooms();
        return RoomIssues.TryGetValue(r.Id, out var k) ? k : null;
    }

    // ---------------- painting room types ----------------

    /// <summary>Racks rented out to customers inside these cells.</summary>
    bool RentedIn(IEnumerable<(int x, int z)> cells, int floor)
    {
        var set = cells.ToHashSet();
        var owners = RackOwners();
        return Objects.Any(o => o.Kind == ObjKind.Rack && o.Floor == floor && set.Contains((o.X, o.Z)) && owners.ContainsKey(o.Id));
    }

    /// <summary>Whether an object can stay in a room of this type (false: it needs another room type).</summary>
    static bool Fits(PlacedObject o, RoomType? t)
    {
        var d = o.Def;
        return d.Where switch
        {
            Where.Hall => t == RoomType.DataHall,
            Where.Room when d.Rooms != null => t.HasValue && d.Rooms.Contains(t.Value),
            Where.Room => t.HasValue && t != RoomType.DataHall,
            _ => true,
        };
    }

    /// <summary>Remove the objects in these cells that do not belong in a room of type t (half the price back).</summary>
    int DropMisfits(IEnumerable<(int x, int z)> cells, int floor, RoomType? t)
    {
        var set = cells.ToHashSet();
        var n = 0;
        foreach (var o in Objects.Where(o => o.Floor == floor && !Catalog.IsShaft(o.Kind) && set.Contains((o.X, o.Z)) && !Fits(o, t)).ToList())
        {
            Objects.Remove(o); RefundRemoved(o.Id, o.Def.Cost / 2); n++;
        }
        return n;
    }

    /// <summary>Objects that would have to go if this room became type t.</summary>
    public int MisfitsIf(Room r, RoomType? t) => Objects.Count(o => o.Floor == r.Floor && !Catalog.IsShaft(o.Kind) && r.Contains(o.X, o.Z) && !Fits(o, t));

    /// <summary>Change what a room is (UPS room becomes storage...). Objects that do not fit the new type are
    /// removed (half back). A hall with rented racks cannot change.</summary>
    public string SetRoomType(Room r, RoomType t)
    {
        if (r.Type == t) return null;
        if (r.Type == RoomType.DataHall && RentedIn(r.CellList(), r.Floor)) return "msg_room_has_tenants";
        if (r.Floor == BasementFloor && t is RoomType.LoadingDock or RoomType.Reception) return "msg_not_in_basement";
        DropMisfits(r.CellList(), r.Floor, t);
        var n = r.Copy();
        n.Type = t;
        Rooms[Rooms.IndexOf(r)] = n;
        Touch();
        if (n.Stage == Stage.Done) RoomFinished(n);
        return null;
    }

    /// <summary>
    /// The paint tool: give floor cells a room type. One space holds one room: painting into a space with a room
    /// grows that room (and changes its type if it was another); painting over a room of another type changes the
    /// whole room. With fill, the whole space under the cells is painted. Free, instant.
    /// </summary>
    public string PaintRoom(RoomType t, int x0, int z0, int w, int d, int floor, bool fill = false)
    {
        if (floor == BasementFloor && t is RoomType.LoadingDock or RoomType.Reception) return "msg_not_in_basement";
        var drawn = Spaces(floor, false);
        var cells = new HashSet<(int x, int z)>();
        for (var z = z0; z < z0 + d; z++)
            for (var x = x0; x < x0 + w; x++)
                if (InPlot(x, z) && drawn.At(x, z) != 0) cells.Add((x, z));
        if (cells.Count == 0) return "msg_room_in_building";
        if (fill)
        {
            var labels = cells.Select(c => drawn.At(c.x, c.z)).ToHashSet();
            foreach (var b in Buildings.Where(b => b.HasFloor(floor)))
                for (var z = b.Z; z < b.Z + b.D; z++)
                    for (var x = b.X; x < b.X + b.W; x++)
                        if (labels.Contains(drawn.At(x, z))) cells.Add((x, z));
        }
        var changed = false;
        // rooms under the brush of another type change as a whole
        foreach (var r in Rooms.Where(r => r.Floor == floor && r.Type != t && cells.Any(c => r.Contains(c.x, c.z))).ToList())
        {
            var why = SetRoomType(r, t);
            if (why != null) return why;
            changed = true;
        }
        // cells already in a room are done; walled rooms keep their own rectangle
        cells.RemoveWhere(c => Rooms.Any(r => r.Floor == floor && r.Contains(c.x, c.z)));
        foreach (var g in cells.GroupBy(c => drawn.At(c.x, c.z)))
        {
            var room = Rooms.FirstOrDefault(r => !r.Walled && r.Floor == floor && r.CellList().Any(c => drawn.At(c.x, c.z) == g.Key));
            if (room != null)
            {
                if (room.Type != t) { var why = SetRoomType(room, t); if (why != null) return why; room = Rooms.First(r => r.Id == room.Id); }
                var n = room.Copy();
                n.SetCells(room.CellList().Concat(g));
                Rooms[Rooms.IndexOf(room)] = n;
            }
            else
            {
                var first = g.First();
                var b = BuildingAt(first.x, first.z);
                var r = new Room { Id = NextId++, BuildingId = b.Id, Floor = floor, Type = t, Walled = false, Stage = Stage.Walls };
                r.SetCells(g);
                Rooms.Add(r);
            }
            changed = true;
        }
        if (!changed) return "msg_nothing_changed";
        Touch();
        RefreshRooms();
        return null;
    }

    /// <summary>The eraser of the paint tool: the cells lose their room type, walls stay. A room with its own
    /// walls first turns into separate walls. Objects that need a room go (half back).</summary>
    public string UnpaintRoom(int x0, int z0, int w, int d, int floor)
    {
        bool In(int x, int z) => x >= x0 && z >= z0 && x < x0 + w && z < z0 + d;
        var hit = Rooms.Where(r => r.Floor == floor && r.CellList().Any(c => In(c.x, c.z))).ToList();
        if (hit.Count == 0) return "msg_nothing_changed";
        var cut = hit.SelectMany(r => r.CellList().Where(c => In(c.x, c.z))).ToList();
        if (RentedIn(cut, floor)) return "msg_room_has_tenants";
        foreach (var r0 in hit)
        {
            var r = Unwall(r0);
            var keep = r.CellList().Where(c => !In(c.x, c.z)).ToList();
            DropMisfits(r.CellList().Where(c => In(c.x, c.z)), floor, null);
            if (keep.Count == 0) { Rooms.Remove(r); RoomIssues.Remove(r.Id); if (IsPlanned(r.Id)) Planned.Remove(r.Id); continue; }
            var n = r.Copy();
            n.SetCells(keep);
            Rooms[Rooms.IndexOf(r)] = n;
        }
        Touch();
        RefreshRooms();
        return null;
    }

    // ---------------- demolishing ----------------

    /// <summary>The bulldozer with a choice of what goes. All is the old behaviour (objects, then doors, walls,
    /// whole rooms, buildings, paving). Returns null when something went, else the reason.</summary>
    public string Erase(EraseKind kind, int x0, int z0, int w, int d, int floor, bool roof = false)
    {
        bool In(int x, int z) => x >= x0 && z >= z0 && x < x0 + w && z < z0 + d;
        switch (kind)
        {
            case EraseKind.Interior:
            {
                var list = Objects.Where(o => (o.Floor == floor || (roof && o.Floor == RoofFloor)) && !Catalog.IsShaft(o.Kind) && Cells(o).Any(c => In(c.x, c.z))).ToList();
                if (list.Count == 0) return "msg_nothing_changed";
                var layout = false;
                foreach (var o in list) { Objects.Remove(o); RefundRemoved(o.Id, o.Def.Cost / 2); layout |= ShapesLayout(o); }
                Touch(layout);
                return null;
            }
            case EraseKind.Door:
            {
                var list = Doors.Where(o => o.Floor == floor && o.Edges().Any(e => In(e.x, e.z))).ToList();
                if (list.Count == 0) return "msg_nothing_changed";
                foreach (var o in list) { Doors.Remove(o); if (IsPlanned(o.Id)) Planned.Remove(o.Id); }
                Touch(); RefreshRooms();
                return null;
            }
            case EraseKind.Wall:
            {
                // edges inside the drag (a 1-wide drag along a wall line catches it)
                bool EdgeIn((int x, int z, int dir) e) => e.dir == 0 ? e.x >= x0 && e.x <= x0 + w && e.z >= z0 && e.z < z0 + d : e.z >= z0 && e.z <= z0 + d && e.x >= x0 && e.x < x0 + w;
                foreach (var r in Rooms.Where(r => r.Walled && r.Floor == floor && Perimeter(r.X, r.Z, r.W, r.D).Any(e => EdgeIn(e) && !IsOuterEdge(e))).ToList())
                    Unwall(r);
                var edges = Walls.Where(x => x.Floor == floor).SelectMany(x => x.Edges()).Where(EdgeIn).ToList();
                if (edges.Count == 0) return "msg_nothing_changed";
                RemoveWallEdges(floor, edges);
                RefreshRooms();
                return null;
            }
            case EraseKind.Room:
            {
                var hit = Rooms.Where(r => r.Floor == floor && r.CellList().Any(c => In(c.x, c.z))).ToList();
                if (hit.Count == 0) return "msg_nothing_changed";
                if (hit.Any(r => RentedIn(r.CellList(), floor))) return "msg_room_has_tenants";
                foreach (var r in hit) DemolishRoom(r);
                Touch(); RefreshRooms();
                return null;
            }
            default:
                return Erase(x0, z0, w, d, floor, roof) ? null : "msg_nothing_changed";
        }
    }

    /// <summary>A room goes with its walls and doors: a walled room as before, a painted room with the separate
    /// walls around it that do not border another room.</summary>
    void DemolishRoom(Room r)
    {
        if (r.Walled) { RemoveRoom(r); return; }
        var cells = r.CellList().ToHashSet();
        Objects.RemoveAll(o => o.Floor == r.Floor && !Catalog.IsShaft(o.Kind) && cells.Contains((o.X, o.Z)) && RefundObject(o));
        var others = Rooms.Where(o => o != r && o.Floor == r.Floor).ToList();
        var edges = new List<(int x, int z, int dir)>();
        foreach (var w in Walls.Where(w => w.Floor == r.Floor))
            foreach (var e in w.Edges())
            {
                var (a, b) = Sides(e);
                var mine = cells.Contains(a) || cells.Contains(b);
                var other = cells.Contains(a) ? b : a;
                if (mine && !others.Any(o => o.Contains(other.x, other.z))) edges.Add(e);
            }
        RemoveWallEdges(r.Floor, edges);
        Rooms.Remove(r);
        RoomIssues.Remove(r.Id);
        if (IsPlanned(r.Id)) Planned.Remove(r.Id);
    }

    bool RefundObject(PlacedObject o) { RefundRemoved(o.Id, o.Def.Cost / 2); return true; }
}
