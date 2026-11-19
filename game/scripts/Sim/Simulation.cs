using System;
using System.Collections.Generic;
using System.Linq;

namespace DatacenterArchitect.Sim;

public enum Role { Builder, Fitter, Engineer, Truck, Visitor }

public enum TruckLoad { Material, Equipment }

/// <summary>A worker or a truck walking or driving over the site. Agents are not saved; they restart from the game state.</summary>
public class Agent
{
    public int Id;
    public Role Role;
    public double X, Z;
    public int Floor;
    public double Heading;
    public readonly List<(double x, double z, int f)> Way = new();
    public int WayI;
    public int Phase;
    public int Target;
    public (int x, int z) Goal = (-1, -1);
    /// <summary>Material units reserved (builders) or carried once Loaded.</summary>
    public int Carry;
    public bool Loaded;
    public int CarryObj;
    public double Timer;
    public bool Working;
    public TruckLoad Load;
    public List<int> Items = new();
    public bool Gone;
    /// <summary>From this waypoint on the agent drives backwards (a truck backing up to a dock); -1 for never.</summary>
    public int ReverseFrom = -1;
    /// <summary>Engineer patching a cross-connect at the ODF in Target.</summary>
    public bool Xc;
    public bool Moving => WayI < Way.Count;
}

/// <summary>A construction job: a building stage or a room's inner walls.</summary>
public sealed class Site
{
    public Building B;
    public Room R;
    public int Id => B?.Id ?? R.Id;
    public int Priority { get => B?.Priority ?? R.Priority; set { if (B != null) B.Priority = value; else R.Priority = value; } }
    public string NameKey => B != null ? Catalog.Buildings[B.Kind].NameKey : Catalog.Rooms[R.Type].NameKey;
    public double Progress => B != null ? GameState.Progress(B) : GameState.StageProgress(R);
    public int Floor => R?.Floor ?? 0;
    public int Need => B != null ? GameState.MaterialNeed(B) : GameState.MaterialNeed(R);
    public int Material { get => B?.Material ?? R.Material; set { if (B != null) B.Material = value; else R.Material = value; } }
    public double Work { get => B?.Work ?? R.Work; set { if (B != null) B.Work = value; else R.Work = value; } }
    public double WorkNeed => B != null ? GameState.WorkNeed(B) : GameState.WorkNeed(R);
    public double WorkAvailable => Math.Min(WorkNeed, Material * GameState.WorkPerMaterial) - Work;
    public (int x, int z, int w, int d) Rect => B != null ? (B.X, B.Z, B.W, B.D) : (R.X, R.Z, R.W, R.D);
    /// <summary>Foundation and roof are worked on from everywhere inside; walls from the inside edge.</summary>
    public bool Ring => R != null || B.Stage == Stage.Walls;
}

/// <summary>
/// Runs construction and the rack delivery chain:
/// trucks bring material to the yard, builders carry it to sites and build stage by stage;
/// trucks bring racks to the loading dock, fitters place them, an engineer connects power and then fiber.
/// </summary>
public class Simulation
{
    public const double WalkSpeed = 16, CarrySpeed = 12, TruckSpeed = 60; // cells per game hour
    public const int CarryUnits = 10, TruckMaterial = 40, TruckItems = 12;
    public const double PlaceHours = 0.1, ConnectHours = 0.25, UnloadHours = 0.2, LiftHours = 0.08;

    public readonly GameState S;
    public readonly Nav Nav;
    public readonly List<Agent> Agents = new();
    /// <summary>Notification keys with an optional argument, drained by the game screen.</summary>
    public readonly List<(string key, string arg)> Messages = new();
    readonly Dictionary<string, double> _said = new();
    readonly Dictionary<int, double> _cooldown = new();
    int _nextAgent = 1;
    double _now;
    bool _firstLive;

    public Simulation(GameState s)
    {
        S = s; Nav = new Nav(s);
        _firstLive = s.LiveRacks > 0;
    }

    void Say(string key, string arg = null, double every = 12)
    {
        var k = key + arg;
        if (_said.TryGetValue(k, out var t) && _now - t < every) return;
        _said[k] = _now;
        Messages.Add((key, arg));
    }

    // ---------------- queries ----------------
    public IEnumerable<Site> Sites()
    {
        var list = new List<Site>();
        foreach (var b in S.Buildings.Where(b => b.Stage != Stage.Done)) list.Add(new Site { B = b });
        foreach (var r in S.Rooms.Where(r => r.Stage != Stage.Done))
            if (S.BuildingOf(r)?.Stage == Stage.Done) list.Add(new Site { R = r });
        return list.OrderByDescending(s => s.Priority).ThenBy(s => s.Id);
    }

    Site SiteById(int id)
    {
        var b = S.Buildings.FirstOrDefault(x => x.Id == id);
        if (b != null) return b.Stage == Stage.Done ? null : new Site { B = b };
        var r = S.Rooms.FirstOrDefault(x => x.Id == id);
        return r == null || r.Stage == Stage.Done ? null : new Site { R = r };
    }

    public IEnumerable<(int x, int z)> YardCells()
    {
        for (var z = 0; z < S.PlotSize; z++)
            for (var x = 0; x < S.PlotSize; x++)
                if (S.SurfaceAt(x, z) == Surface.Yard) yield return (x, z);
    }

    /// <summary>Asphalt cells on the plot edge next to the public road, where trucks come in.</summary>
    public List<(int x, int z)> Entries()
    {
        var z = S.PlotSize - 1;
        return Enumerable.Range(0, S.PlotSize).Where(x => Nav.TruckCell(x, z)).Select(x => (x, z)).ToList();
    }

    /// <summary>Loading docks: the apron cell outside each roll-up door, and the dock cell inside it.</summary>
    public List<(Room dock, (int x, int z) apron, (int x, int z) inside)> Docks()
    {
        var list = new List<(Room, (int, int), (int, int))>();
        foreach (var r in S.Rooms.Where(r => r.Type == RoomType.LoadingDock && r.Floor == 0 && r.Stage == Stage.Done))
        {
            if (S.BuildingOf(r)?.Stage != Stage.Done) continue;
            var edges = GameState.Perimeter(r.X, r.Z, r.W, r.D).ToHashSet();
            foreach (var d in S.Doors.Where(d => d.Floor == 0 && d.Type == DoorType.RollUp))
                foreach (var e in d.Edges())
                {
                    if (!edges.Contains(e) || !S.IsOuterEdge(e)) continue;
                    // the two cells on either side of the edge
                    var a = e.dir == 0 ? (e.x - 1, e.z) : (e.x, e.z - 1);
                    var b = (e.x, e.z);
                    var inside = r.Contains(a.Item1, a.Item2) ? a : b;
                    var apron = inside == a ? b : a;
                    list.Add((r, apron, inside));
                }
        }
        return list;
    }

    public Room HallOf(PlacedObject o) => S.RoomAt(o.X, o.Z, o.Floor);

    bool HallReady(PlacedObject o)
    {
        var r = HallOf(o);
        return r != null && r.Stage == Stage.Done && S.BuildingOf(r)?.Stage == Stage.Done;
    }

    (int x, int z) SpawnCell()
    {
        var yard = YardCells().FirstOrDefault(c => Nav.Walkable(c.x, c.z, 0));
        if (yard != default || S.SurfaceAt(0, 0) == Surface.Yard) return yard;
        var z = S.PlotSize - 1;
        for (var i = 0; i < S.PlotSize / 2; i++)
            foreach (var x in new[] { S.PlotSize / 2 - i, S.PlotSize / 2 + i })
                if (Nav.Walkable(x, z, 0)) return (x, z);
        return (S.PlotSize / 2, z);
    }

    // ---------------- main loop ----------------
    public void Tick(double hours)
    {
        while (hours > 1e-9)
        {
            var dt = Math.Min(0.05, hours);
            hours -= dt; _now += dt;
            Step(dt);
        }
    }

    void Step(double dt)
    {
        if (S.Creative.InstantBuild) FinishEverything();
        SyncCrew();
        DispatchTrucks();
        UpdateGates(dt);
        foreach (var a in Agents.ToList())
        {
            switch (a.Role)
            {
                case Role.Builder: Builder(a, dt); break;
                case Role.Fitter: Fitter(a, dt); break;
                case Role.Engineer: Engineer(a, dt); break;
                case Role.Truck: Truck(a, dt); break;
                case Role.Visitor: Visitor(a, dt); break;
            }
        }
        PlanVisits(dt);
        Agents.RemoveAll(a => a.Gone);
        if (!_firstLive && S.LiveRacks > 0) { _firstLive = true; Say("msg_first_rack_live"); }
    }

    public void FinishEverything()
    {
        var changed = false;
        foreach (var b in S.Buildings.Where(b => b.Stage != Stage.Done)) { b.Stage = Stage.Done; b.Material = 0; b.Work = 0; changed = true; }
        foreach (var r in S.Rooms.Where(r => r.Stage != Stage.Done)) { r.Stage = Stage.Done; r.Material = 0; r.Work = 0; changed = true; }
        foreach (var o in S.Objects.Where(o => o.Status != ObjStatus.Live)) { o.Status = ObjStatus.Live; changed = true; }
        foreach (var a in Agents.Where(a => a.Role != Role.Visitor && (a.Role != Role.Builder || a.Phase != 0))) Reset(a);
        Agents.RemoveAll(a => a.Role == Role.Truck);
        if (changed) S.Touch();
    }

    void SyncCrew()
    {
        Sync(Role.Builder, S.Builders);
        Sync(Role.Fitter, S.Fitters);
        Sync(Role.Engineer, S.Engineers);
    }

    void Sync(Role role, int want)
    {
        var have = Agents.Where(a => a.Role == role).ToList();
        for (var i = have.Count; i < want; i++)
        {
            var c = SpawnCell();
            Agents.Add(new Agent { Id = _nextAgent++, Role = role, X = c.x + 0.5 + (i % 3) * 0.2, Z = c.z + 0.5, Timer = i * 0.05 });
        }
        foreach (var a in have.OrderBy(a => a.Phase == 0 ? 0 : 1).Take(Math.Max(0, have.Count - want)))
        {
            Reset(a); a.Gone = true;
        }
        Agents.RemoveAll(a => a.Gone);
    }

    /// <summary>Drop the current job; reserved material goes back to the yard.</summary>
    void Reset(Agent a)
    {
        if (a.Role == Role.Builder && a.Carry > 0) S.MaterialStock += a.Carry;
        a.Carry = 0; a.Loaded = false; a.CarryObj = 0; a.Target = 0; a.Phase = 0; a.Working = false; a.Xc = false;
        a.Way.Clear(); a.WayI = 0; a.Goal = (-1, -1);
    }

    // ---------------- movement ----------------
    (int x, int z) Cell(Agent a) => ((int)Math.Floor(a.X), (int)Math.Floor(a.Z));

    bool Go(Agent a, int floor, Func<int, int, bool> goal, NavMode mode)
    {
        var (cx, cz) = Cell(a);
        var path = Nav.Find(new Pt(cx, cz, a.Floor), floor, goal, mode);
        a.Way.Clear(); a.WayI = 0;
        if (path == null) return false;
        foreach (var p in path) a.Way.Add((p.X + 0.5, p.Z + 0.5, p.F));
        a.Goal = path.Count > 0 ? (path[^1].X, path[^1].Z) : (cx, cz);
        return true;
    }

    /// <summary>Move along the way. Returns true when the agent has arrived.</summary>
    bool Move(Agent a, double dt, double speed)
    {
        var budget = dt * speed;
        while (budget > 1e-9 && a.WayI < a.Way.Count)
        {
            var (tx, tz, tf) = a.Way[a.WayI];
            if (tf != a.Floor)
            {
                a.Timer += dt;
                if (a.Timer < LiftHours) return false;
                a.Timer = 0; a.Floor = tf; a.WayI++;
                continue;
            }
            var dx = tx - a.X; var dz = tz - a.Z;
            var dist = Math.Sqrt(dx * dx + dz * dz);
            if (dist > 1e-6) a.Heading = Math.Atan2(dx, dz) + (a.ReverseFrom >= 0 && a.WayI >= a.ReverseFrom ? Math.PI : 0);
            if (dist <= budget) { a.X = tx; a.Z = tz; budget -= dist; a.WayI++; }
            else { a.X += dx / dist * budget; a.Z += dz / dist * budget; budget = 0; }
        }
        return a.WayI >= a.Way.Count;
    }

    bool Cooling(int id) => _cooldown.TryGetValue(id, out var t) && t > _now;
    void Cool(int id, double hours) => _cooldown[id] = _now + hours;

    // ---------------- customer visits ----------------
    /// <summary>At most this many customers are on site at the same time, however big the campus.</summary>
    public const int MaxVisitors = 15;
    /// <summary>Days between visits of one customer, per product: colocation customers come by now and then, not every day.</summary>
    public static double VisitDays(Product p) => p switch { Product.PerU => 60, Product.Rack => 40, Product.Cage => 25, _ => 18 };
    readonly Random _rng = new(1234);
    double _visitClock;

    /// <summary>Expected customer visits per day for the contracts that run now.</summary>
    public double VisitsPerDay()
    {
        var sum = 0.0;
        foreach (var d in S.Deals.Where(d => d.State == DealState.Active))
            sum += (d.Legacy ? Math.Max(1, d.RackIds.Count / 6.0) : 1) / VisitDays(d.Product);
        return sum;
    }

    /// <summary>Once per game hour, during the day, a customer may come by to work on its equipment.</summary>
    void PlanVisits(double dt)
    {
        _visitClock += dt;
        if (_visitClock < 1) return;
        _visitClock -= 1;
        var hour = S.Hours % 24;
        if (hour < 8 || hour >= 18) return;
        if (Agents.Count(a => a.Role == Role.Visitor) >= MaxVisitors) return;
        var perHour = VisitsPerDay() / 10;
        if (_rng.NextDouble() >= perHour) return;
        var deals = S.Deals.Where(d => d.State == DealState.Active).ToList();
        var weights = deals.Select(d => (d.Legacy ? Math.Max(1, d.RackIds.Count / 6.0) : 1) / VisitDays(d.Product)).ToList();
        var x = _rng.NextDouble() * weights.Sum();
        Deal pick = null;
        for (var i = 0; i < deals.Count && pick == null; i++) { x -= weights[i]; if (x <= 0) pick = deals[i]; }
        pick ??= deals.LastOrDefault();
        if (pick == null) return;
        var ids = pick.RackIds.Count > 0 ? pick.RackIds : S.Objects.Where(o => o.Kind == ObjKind.Rack && S.HallOf(o)?.Zone == HallZone.PerU).Select(o => o.Id).ToList();
        var rack = ids.Count == 0 ? null : S.Objects.FirstOrDefault(o => o.Id == ids[_rng.Next(ids.Count)]);
        if (rack == null) return;
        StartVisit(rack);
    }

    /// <summary>A customer parks (or comes in from the road), walks to the rack, works there for a while and leaves.</summary>
    public Agent StartVisit(PlacedObject rack)
    {
        var park = Enumerable.Range(0, S.PlotSize * S.PlotSize).Select(i => (x: i % S.PlotSize, z: i / S.PlotSize))
            .Where(c => S.SurfaceAt(c.x, c.z) == Surface.Parking && Nav.Walkable(c.x, c.z, 0)).ToList();
        var start = park.Count > 0 ? park[_rng.Next(park.Count)] : SpawnCell();
        var a = new Agent { Id = _nextAgent++, Role = Role.Visitor, X = start.x + 0.5, Z = start.z + 0.5, Target = rack.Id, Goal = start, Phase = 1 };
        var home = start;
        if (!Go(a, rack.Floor, (x, z) => Math.Abs(x - rack.X) + Math.Abs(z - rack.Z) == 1, NavMode.Walk)) return null;
        a.CarryObj = home.x * 1000 + home.z; // where the visitor parked, to walk back to
        Agents.Add(a);
        return a;
    }

    void Visitor(Agent a, double dt)
    {
        a.Working = false;
        switch (a.Phase)
        {
            case 1: // walking in
                if (!Move(a, dt, WalkSpeed * 0.8)) return;
                a.Phase = 2; a.Timer = 0.5 + _rng.NextDouble() * 2;
                return;
            case 2: // at the rack
                a.Working = true;
                a.Timer -= dt;
                if (a.Timer > 0 && S.Objects.Any(o => o.Id == a.Target)) return;
                a.Phase = 0;
                return;
            case 3: // walking out
                if (Move(a, dt, WalkSpeed * 0.8)) a.Gone = true;
                return;
            default: // leave
                int hx = a.CarryObj / 1000, hz = a.CarryObj % 1000;
                if (!Go(a, 0, (x, z) => x == hx && z == hz, NavMode.Walk)) { a.Gone = true; return; }
                a.Phase = 3;
                return;
        }
    }

    // ---------------- builders ----------------
    void Builder(Agent a, double dt)
    {
        a.Working = false;
        switch (a.Phase)
        {
            case 0: // idle
                a.Timer -= dt;
                if (a.Timer > 0) return;
                a.Timer = 0.25;
                BuilderDecide(a);
                return;
            case 1: // to the yard
                if (!Move(a, dt, WalkSpeed)) return;
                var site = SiteById(a.Target);
                if (site == null) { Reset(a); return; }
                a.Loaded = true;
                if (!GoToSite(a, site)) { Unreachable(a, site); return; }
                a.Phase = 2;
                return;
            case 2: // carrying material to the site
                if (!Move(a, dt, WalkSpeed)) return;
                var s2 = SiteById(a.Target);
                if (s2 == null) { Reset(a); return; }
                s2.Material += a.Carry; a.Carry = 0; a.Loaded = false;
                a.Phase = 4; // start working right away
                return;
            case 3: // walking to work
                if (!Move(a, dt, WalkSpeed)) return;
                a.Phase = 4;
                return;
            case 4: // working
                var s4 = SiteById(a.Target);
                if (s4 == null) { Reset(a); return; }
                var add = Math.Min(dt, s4.WorkAvailable);
                if (add <= 1e-9) { Reset(a); a.Timer = 0.05; return; }
                a.Working = true;
                s4.Work += add * S.WorkSpeed;
                if (s4.Work >= s4.WorkNeed - 1e-9) Complete(s4);
                return;
        }
    }

    int Inbound(int siteId) => Agents.Where(x => x.Role == Role.Builder && x.Target == siteId).Sum(x => x.Carry);

    void BuilderDecide(Agent a)
    {
        var sites = Sites().Where(s => !Cooling(s.Id)).ToList();
        if (sites.Count == 0) return;
        var preferHaul = a.Id % 2 == 0;
        Site haul = null;
        var yard = YardCells().ToHashSet();
        if (S.MaterialStock > 0 && yard.Count > 0)
            haul = sites.FirstOrDefault(s => s.Need - s.Material - Inbound(s.Id) > 0);
        var work = sites.FirstOrDefault(s => s.WorkAvailable > 1e-6);
        var pick = preferHaul ? (haul != null ? 1 : work != null ? 2 : 0) : (work != null ? 2 : haul != null ? 1 : 0);
        if (pick == 1)
        {
            var need = haul.Need - haul.Material - Inbound(haul.Id);
            var take = Math.Min(CarryUnits, Math.Min(need, S.MaterialStock));
            if (!Go(a, 0, (x, z) => yard.Contains((x, z)), NavMode.Walk)) { Say("msg_yard_unreachable"); return; }
            S.MaterialStock -= take;
            a.Carry = take; a.Target = haul.Id; a.Phase = 1;
        }
        else if (pick == 2)
        {
            a.Target = work.Id;
            if (!GoToSite(a, work)) { Unreachable(a, work); return; }
            a.Phase = 3;
        }
    }

    bool GoToSite(Agent a, Site s)
    {
        var (x0, z0, w, d) = s.Rect;
        var taken = Agents.Where(o => o != a && o.Role == Role.Builder && o.Target == s.Id).Select(o => o.Goal).ToHashSet();
        bool Inside(int x, int z) => x >= x0 && z >= z0 && x < x0 + w && z < z0 + d;
        bool OnRing(int x, int z) => Inside(x, z) && (x == x0 || z == z0 || x == x0 + w - 1 || z == z0 + d - 1);
        Func<int, int, bool> goal = s.Ring
            ? (x, z) => OnRing(x, z) && !taken.Contains((x, z)) && Nav.Walkable(x, z, s.Floor)
            : (x, z) => Inside(x, z) && !taken.Contains((x, z)) && Nav.Walkable(x, z, s.Floor);
        if (Go(a, s.Floor, goal, NavMode.Walk)) return true;
        return Go(a, s.Floor, (x, z) => (s.Ring ? OnRing(x, z) : Inside(x, z)) && Nav.Walkable(x, z, s.Floor), NavMode.Walk);
    }

    void Unreachable(Agent a, Site s)
    {
        var name = s.NameKey;
        Say(s.R != null && s.R.Floor > 0 ? "msg_site_needs_lift" : "msg_site_unreachable", name);
        Cool(s.Id, 2);
        Reset(a);
    }

    void Complete(Site s)
    {
        if (s.B != null)
        {
            var b = s.B;
            b.Material = 0; b.Work = 0;
            b.Stage++;
            if (b.Stage == Stage.Done)
            {
                EvictFrom(b.X, b.Z, b.W, b.D, 0, true);
                Say("msg_building_done", Catalog.Buildings[b.Kind].NameKey, 0);
            }
        }
        else
        {
            var r = s.R;
            r.Material = 0; r.Work = 0; r.Stage = Stage.Done;
            EvictFrom(r.X, r.Z, r.W, r.D, r.Floor, false);
            Say("msg_room_done", Catalog.Rooms[r.Type].NameKey, 0);
        }
        S.Touch();
        foreach (var x in Agents.Where(x => x.Role == Role.Builder && x.Target == s.Id && x.Phase >= 3)) Reset(x);
    }

    /// <summary>Workers standing inside a just-closed building or room step outside so nobody gets walled in.</summary>
    void EvictFrom(int x0, int z0, int w, int d, int floor, bool building)
    {
        foreach (var a in Agents.Where(a => a.Floor == floor && a.Role != Role.Truck))
        {
            var (cx, cz) = Cell(a);
            if (cx < x0 || cz < z0 || cx >= x0 + w || cz >= z0 + d) continue;
            if (!building && Nav.Find(new Pt(cx, cz, floor), floor, (x, z) => x < x0 || z < z0 || x >= x0 + w || z >= z0 + d, NavMode.Walk) != null) continue;
            var best = (x: cx, z: z0 + d); var bestD = double.MaxValue;
            for (var x = x0 - 1; x <= x0 + w; x++)
                foreach (var z in new[] { z0 - 1, z0 + d })
                    Consider(x, z);
            for (var z = z0; z < z0 + d; z++)
                foreach (var x in new[] { x0 - 1, x0 + w })
                    Consider(x, z);
            void Consider(int x, int z)
            {
                if (!S.InPlot(x, z)) return;
                var dd = Math.Abs(x - cx) + Math.Abs(z - cz);
                if (dd < bestD) { bestD = dd; best = (x, z); }
            }
            a.X = best.x + 0.5; a.Z = best.z + 0.5;
            Reset(a);
        }
    }

    // ---------------- trucks ----------------
    int MaterialOutstanding()
    {
        var need = Sites().Sum(s => Math.Max(0, s.Need - s.Material - Inbound(s.Id)));
        var coming = Agents.Where(a => a.Role == Role.Truck && a.Load == TruckLoad.Material && a.Phase <= 2).Sum(a => a.Carry);
        return need - S.MaterialStock - coming;
    }

    void DispatchTrucks()
    {
        var trucks = Agents.Where(a => a.Role == Role.Truck && a.Phase <= 2).ToList();
        // building material to the yard
        var outstanding = MaterialOutstanding();
        if (outstanding > 0 && trucks.Count(t => t.Load == TruckLoad.Material) < 2 && !Cooling(-1))
        {
            var yard = YardCells().ToHashSet();
            if (yard.Count == 0) { Say("msg_need_yard", null, 24); Cool(-1, 1); }
            else if (!SendTruck(TruckLoad.Material, (x, z) => yard.Contains((x, z)), Math.Min(TruckMaterial, (outstanding + 9) / 10 * 10), null))
            { Say("msg_yard_no_road", null, 24); Cool(-1, 1); }
        }
        // racks and cooling units to the loading dock
        var onTruck = trucks.SelectMany(t => t.Items).ToHashSet();
        var waiting = S.Objects.Where(o => o.Status == ObjStatus.Ordered && !onTruck.Contains(o.Id) && HallReady(o)).OrderBy(o => o.Id).ToList();
        if (waiting.Count > 0 && trucks.Count(t => t.Load == TruckLoad.Equipment) < 2 && !Cooling(-2))
        {
            var docks = Docks();
            var aprons = docks.Select(d => d.apron).ToHashSet();
            if (docks.Count == 0) { Say("msg_need_dock", null, 24); Cool(-2, 1); }
            else if (!SendTruck(TruckLoad.Equipment, (x, z) => aprons.Contains((x, z)), 0, waiting.Take(TruckItems).Select(o => o.Id).ToList()))
            { Say("msg_dock_no_road", null, 24); Cool(-2, 1); }
        }
    }

    bool SendTruck(TruckLoad load, Func<int, int, bool> goal, int material, List<int> items)
    {
        var entries = Entries();
        if (entries.Count == 0) return false;
        var path = Nav.FindOnFloor(entries, 0, goal, NavMode.Truck);
        if (path == null) return false;
        // the path starts after the entry cell; find which entry it came from
        var first = path.Count > 0 ? path[0] : default;
        var entry = path.Count > 0 ? entries.OrderBy(e => Math.Abs(e.x - first.X) + Math.Abs(e.z - first.Z)).First() : entries.First(e => goal(e.x, e.z));
        var lane = S.PlotSize + 3.2;
        var t = new Agent { Id = _nextAgent++, Role = Role.Truck, Load = load, Carry = material, Items = items ?? new(), X = -20, Z = lane, Phase = 1 };
        t.Way.Add((entry.x + 0.5, lane, 0));
        t.Way.Add((entry.x + 0.5, S.PlotSize + 0.5, 0));
        t.Way.Add((entry.x + 0.5, entry.z + 0.5, 0));
        foreach (var p in path) t.Way.Add((p.X + 0.5, p.Z + 0.5, 0));
        // at a dock the truck stops short of the door instead of driving into the building
        if (load == TruckLoad.Equipment && t.Way.Count > 0)
        {
            var (lx, lz, _) = t.Way[^1];
            if (ParkSpot(((int)Math.Floor(lx), (int)Math.Floor(lz))) is { } spot)
            {
                // drop the last cells that lie between the parking spot and the door
                double ox = Math.Sin(spot.heading), oz = Math.Cos(spot.heading);
                double ex = spot.x - ox * 2.75, ez = spot.z - oz * 2.75;
                t.Way.RemoveAt(t.Way.Count - 1);
                while (t.Way.Count > 3)
                {
                    var (wx, wz, _) = t.Way[^1];
                    var along = (wx - ex) * ox + (wz - ez) * oz;
                    var side = Math.Abs((wx - ex) * oz - (wz - ez) * ox);
                    if (along < 2.75 && side < 1.5) t.Way.RemoveAt(t.Way.Count - 1); else break;
                }
                // turn round a few metres out and back up to the door
                t.Way.Add((spot.x + ox * 3, spot.z + oz * 3, 0));
                t.ReverseFrom = t.Way.Count;
                t.Way.Add((spot.x, spot.z, 0));
            }
        }
        Agents.Add(t);
        return true;
    }

    void Truck(Agent a, double dt)
    {
        switch (a.Phase)
        {
            case 1:
                if (Drive(a, dt)) { a.Phase = 2; a.Timer = UnloadHours; Park(a); }
                return;
            case 2:
                a.Timer -= dt;
                if (a.Timer > 0) return;
                if (a.Load == TruckLoad.Material) S.MaterialStock += a.Carry;
                else
                {
                    var (cx, cz) = Cell(a);
                    var dock = Docks().OrderBy(d => Math.Abs(d.apron.x - cx) + Math.Abs(d.apron.z - cz)).FirstOrDefault();
                    foreach (var o in S.Objects.Where(o => a.Items.Contains(o.Id) && o.Status == ObjStatus.Ordered))
                    {
                        o.Status = ObjStatus.Delivered; o.Dock = dock.dock?.Id ?? 0;
                    }
                    S.Touch(false);
                }
                a.Carry = 0; a.Items.Clear();
                // drive back the same way and off along the road
                var back = a.Way.Take(a.Way.Count - 1).Reverse().ToList();
                a.Way.Clear(); a.WayI = 0; a.ReverseFrom = -1;
                a.Way.AddRange(back);
                a.Way.Add((S.PlotSize + 40, S.PlotSize + 3.2, 0));
                a.Phase = 3;
                return;
            case 3:
                if (Drive(a, dt)) a.Gone = true;
                return;
        }
    }

    // ---------------- gates and barriers ----------------
    /// <summary>How far each sliding gate or barrier is open, 0 (closed) to 1 (open), by object id.</summary>
    public readonly Dictionary<int, double> GateOpen = new();
    public const double GateHours = 0.06, GateReach = 9, TruckNose = 3.6;

    void UpdateGates(double dt)
    {
        var trucks = Agents.Where(a => a.Role == Role.Truck).ToList();
        foreach (var o in S.Objects.Where(o => Catalog.IsVehicleStop(o.Kind)))
        {
            var (w, d) = GameState.Footprint(o);
            var cx = o.X + w / 2.0; var cz = o.Z + d / 2.0;
            var want = trucks.Any(t => Math.Abs(t.X - cx) + Math.Abs(t.Z - cz) < GateReach + w / 2.0);
            GateOpen.TryGetValue(o.Id, out var v);
            v = Math.Clamp(v + (want ? 1 : -1) * dt / GateHours, 0, 1);
            GateOpen[o.Id] = v;
        }
    }

    /// <summary>A gate or barrier on this cell that is not fully open.</summary>
    bool Closed(double x, double z)
    {
        var o = S.ObjectAt((int)Math.Floor(x), (int)Math.Floor(z));
        return o != null && Catalog.IsVehicleStop(o.Kind) && (!GateOpen.TryGetValue(o.Id, out var v) || v < 0.98);
    }

    /// <summary>Drives a truck along its way, stopping with its nose before a gate or barrier until it is open.</summary>
    bool Drive(Agent a, double dt)
    {
        var budget = dt * TruckSpeed;
        var look = budget + TruckNose;
        // walk the remaining way and find the first closed gate within reach of the nose
        double px = a.X, pz = a.Z, travelled = 0, stop = double.MaxValue;
        for (var i = a.WayI; i < a.Way.Count && travelled < look && stop == double.MaxValue; i++)
        {
            var (tx, tz, _) = a.Way[i];
            var seg = Math.Sqrt((tx - px) * (tx - px) + (tz - pz) * (tz - pz));
            for (var d = 0.25; d <= seg && travelled + d < look; d += 0.25)
                if (Closed(px + (tx - px) * d / seg, pz + (tz - pz) * d / seg)) { stop = travelled + d; break; }
            travelled += seg; px = tx; pz = tz;
        }
        if (stop == double.MaxValue) return Move(a, dt, TruckSpeed);
        var allowed = Math.Max(0, stop - TruckNose);
        if (allowed <= 1e-6) return false;
        return Move(a, dt, Math.Min(TruckSpeed, allowed / dt));
    }

    /// <summary>A truck at a loading dock turns round and backs up to the roll-up door, so it never pokes into the building.</summary>
    void Park(Agent a)
    {
        if (a.Load != TruckLoad.Equipment) return;
        foreach (var d in Docks())
            if (ParkSpot(d.apron) is { } spot && Math.Abs(spot.x - a.X) + Math.Abs(spot.z - a.Z) < 0.2)
            {
                a.Heading = spot.heading;
                return;
            }
    }

    /// <summary>Where a truck stands at a dock: its rear at the door, nose pointing away from the building.</summary>
    (double x, double z, double heading)? ParkSpot((int x, int z) cell)
    {
        foreach (var (_, apron, inside) in Docks())
        {
            if (apron != cell) continue;
            double ox = apron.x - inside.x, oz = apron.z - inside.z;
            var ex = (apron.x + inside.x) / 2.0 + 0.5; var ez = (apron.z + inside.z) / 2.0 + 0.5;
            return (ex + ox * 2.75, ez + oz * 2.75, Math.Atan2(ox, oz));
        }
        return null;
    }

    // ---------------- fitters ----------------
    HashSet<int> Reserved(Role r) => Agents.Where(a => a.Role == r && a.Target != 0).Select(a => a.Target).ToHashSet();

    void Fitter(Agent a, double dt)
    {
        a.Working = false;
        switch (a.Phase)
        {
            case 0:
                a.Timer -= dt;
                if (a.Timer > 0) return;
                a.Timer = 0.25;
                var taken = Reserved(Role.Fitter);
                var item = S.Objects.Where(o => o.Status == ObjStatus.Delivered && !taken.Contains(o.Id) && !Cooling(o.Id) && HallReady(o)).OrderBy(o => o.Id).FirstOrDefault();
                if (item == null) return;
                var dock = S.Rooms.FirstOrDefault(r => r.Id == item.Dock) ?? Docks().Select(d => d.dock).FirstOrDefault();
                if (dock == null) return;
                a.Target = item.Id;
                if (!Go(a, 0, (x, z) => dock.Contains(x, z) && Nav.Walkable(x, z, 0), NavMode.Walk)) { Say("msg_dock_unreachable"); Cool(item.Id, 2); Reset(a); return; }
                a.Phase = 1;
                return;
            case 1:
                if (!Move(a, dt, WalkSpeed)) return;
                var o1 = S.Objects.FirstOrDefault(o => o.Id == a.Target && o.Status == ObjStatus.Delivered);
                if (o1 == null) { Reset(a); return; }
                var cells = GameState.Cells(o1).ToHashSet();
                if (!Go(a, o1.Floor, (x, z) => cells.Contains((x, z)), NavMode.Carry))
                {
                    Say(o1.Floor > 0 ? "msg_hall_needs_lift" : "msg_hall_needs_wide_door", null, 12);
                    Cool(o1.Id, 3); Reset(a); return;
                }
                a.CarryObj = o1.Id;
                a.Phase = 2;
                return;
            case 2:
                if (!Move(a, dt, CarrySpeed)) return;
                a.Phase = 3; a.Timer = PlaceHours;
                return;
            case 3:
                a.Working = true;
                a.Timer -= dt;
                if (a.Timer > 0) return;
                var o3 = S.Objects.FirstOrDefault(o => o.Id == a.Target);
                if (o3 != null && o3.Status == ObjStatus.Delivered) { o3.Status = ObjStatus.Placed; S.Touch(false); }
                StepAside(a, o3);
                Reset(a);
                return;
        }
    }

    /// <summary>After placing a rack the fitter stands in it; move to a free neighbouring cell.</summary>
    void StepAside(Agent a, PlacedObject o)
    {
        if (o == null) return;
        var cells = GameState.Cells(o).ToList();
        foreach (var (x, z) in cells)
            foreach (var (dx, dz) in new[] { (0, 1), (0, -1), (1, 0), (-1, 0) })
                if (Nav.Walkable(x + dx, z + dz, o.Floor)) { a.X = x + dx + 0.5; a.Z = z + dz + 0.5; return; }
    }

    // ---------------- engineers ----------------
    void Engineer(Agent a, double dt)
    {
        a.Working = false;
        switch (a.Phase)
        {
            case 0:
                a.Timer -= dt;
                if (a.Timer > 0) return;
                a.Timer = 0.25;
                var taken = Reserved(Role.Engineer);
                var item = S.Objects.Where(o => o.Status is ObjStatus.Placed or ObjStatus.Powered && !taken.Contains(o.Id) && !Cooling(o.Id))
                    .OrderByDescending(o => o.Status).ThenBy(o => o.Id).FirstOrDefault();
                if (item == null)
                {
                    // nothing to connect: patch an ordered cross-connect at an ODF in the meet-me room
                    if (S.XcPending <= Agents.Count(x => x.Xc) || S.FreeOdfPorts() <= 0) return;
                    item = S.Objects.Where(o => o.Kind == ObjKind.Odf && o.Status == ObjStatus.Live && !taken.Contains(o.Id) && !Cooling(o.Id)
                        && S.RoomAt(o.X, o.Z, o.Floor)?.Type == RoomType.MeetMe).OrderBy(o => o.Id).FirstOrDefault();
                    if (item == null) return;
                    a.Xc = true;
                }
                var cells = GameState.Cells(item).ToHashSet();
                bool Next(int x, int z) => Nav.Walkable(x, z, item.Floor) && !cells.Contains((x, z)) &&
                    (cells.Contains((x + 1, z)) || cells.Contains((x - 1, z)) || cells.Contains((x, z + 1)) || cells.Contains((x, z - 1)));
                a.Target = item.Id;
                if (!Go(a, item.Floor, Next, NavMode.Walk)) { Say("msg_rack_unreachable", null, 12); Cool(item.Id, 3); Reset(a); return; }
                a.Phase = 1;
                return;
            case 1:
                if (!Move(a, dt, WalkSpeed)) return;
                a.Phase = 2; a.Timer = a.Xc ? Catalog.XcHours : ConnectHours;
                return;
            case 2 when a.Xc:
                var odf = S.Objects.FirstOrDefault(x => x.Id == a.Target);
                if (odf == null || S.XcPending <= 0) { Reset(a); return; }
                a.Working = true;
                a.Heading = Math.Atan2(odf.X + 0.5 - a.X, odf.Z + 0.5 - a.Z);
                a.Timer -= dt;
                if (a.Timer > 0) return;
                S.CompleteCrossConnect();
                Reset(a);
                return;
            case 2:
                var o = S.Objects.FirstOrDefault(x => x.Id == a.Target);
                if (o == null || o.Status is not (ObjStatus.Placed or ObjStatus.Powered)) { Reset(a); return; }
                a.Working = true;
                var face = (o.X + 0.5 - a.X, o.Z + 0.5 - a.Z);
                a.Heading = Math.Atan2(face.Item1, face.Item2);
                a.Timer -= dt;
                if (a.Timer > 0) return;
                o.Status = o.Status == ObjStatus.Placed && o.Def.NeedsFiber ? ObjStatus.Powered : ObjStatus.Live;
                S.Touch(false);
                if (o.Status == ObjStatus.Live) Reset(a); else a.Timer = ConnectHours;
                return;
        }
    }
}
