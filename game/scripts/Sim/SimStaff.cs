using System;
using System.Collections.Generic;
using System.Linq;

namespace DatacenterArchitect.Sim;

/// <summary>
/// Staff on site (step 7): technicians walk to broken equipment and work on it while the repair runs,
/// contractors come in from the road, and a limited number of other people on duty go about their work
/// (guards patrol, cleaners walk the halls, gardeners tend the green). Purely visual: repairs run in GameState.
/// </summary>
public partial class Simulation
{
    /// <summary>At most this many staff figures walk around, however big the team, to keep the game light.</summary>
    public const int MaxStaffAgents = 24;
    double _staffClock;
    readonly Dictionary<StaffRole, List<(int x, int z, int f)>> _areas = new();
    int _areaRev = -1;

    void SyncStaff(double dt)
    {
        _staffClock -= dt;
        if (_staffClock > 0) return;
        _staffClock = 0.25;
        var want = new Dictionary<int, (StaffRole role, int target)>();
        foreach (var o in S.Objects.Where(o => o.Broken && o.Noticed))
        {
            if (o.RepairBy > 0 && S.Staff.FirstOrDefault(e => e.Id == o.RepairBy) is { } e) want[e.Id] = (e.Role, o.Id);
            else if (o.RepairBy == -1 && S.Hours - o.ContractorHour >= Staffing.ContractorDelay - 0.5) want[-o.Id] = (Staffing.RepairRole(o.Kind) ?? StaffRole.Electrical, o.Id);
        }
        foreach (var e in S.Staff.Where(e => S.OnDuty(e) && e.Busy == 0).OrderBy(e => e.Id))
        {
            if (want.Count >= MaxStaffAgents) break;
            if (Area(e.Role).Count > 0) want[e.Id] = (e.Role, 0);
        }
        var present = Agents.Where(a => a.Role == Role.Staff && !a.Escort).ToList();
        foreach (var a in present)
        {
            if (a.Phase >= 8) continue;
            if (!want.TryGetValue(a.EmpId, out var w)) { a.Phase = 8; a.Working = false; continue; }
            if (w.target != a.Target) { a.Target = w.target; a.Phase = 0; a.Working = false; }
        }
        var have = present.Where(a => a.Phase < 8).Select(a => a.EmpId).ToHashSet();
        foreach (var (id, (role, target)) in want)
        {
            if (have.Contains(id) || Agents.Count(a => a.Role == Role.Staff) >= MaxStaffAgents + 4) continue;
            var start = id < 0 ? SpawnCell() : StaffEntry();
            Agents.Add(new Agent { Id = _nextAgent++, Role = Role.Staff, EmpId = id, Staff = role, Target = target, X = start.x + 0.5, Z = start.z + 0.5 });
        }
    }

    (int x, int z) StaffEntry()
    {
        var park = Enumerable.Range(0, S.PlotSize * S.PlotSize).Select(i => (x: i % S.PlotSize, z: i / S.PlotSize))
            .Where(c => S.SurfaceAt(c.x, c.z) == Surface.Parking && Nav.Walkable(c.x, c.z, 0)).ToList();
        return park.Count > 0 ? park[_rng.Next(park.Count)] : SpawnCell();
    }

    /// <summary>Where a role goes about its work when it has no repair to do.</summary>
    public List<(int x, int z, int f)> Area(StaffRole r)
    {
        if (_areaRev != S.LayoutRevision) { _areas.Clear(); _areaRev = S.LayoutRevision; }
        if (_areas.TryGetValue(r, out var list)) return list;
        list = new List<(int, int, int)>();
        RoomType[] rooms = r switch
        {
            StaffRole.Cleaner or StaffRole.Support or StaffRole.RemoteHands or StaffRole.Cooling or StaffRole.FireSafety => new[] { RoomType.DataHall },
            StaffRole.Electrical => new[] { RoomType.UpsRoom, RoomType.Switchgear, RoomType.Transformer, RoomType.Battery, RoomType.DataHall },
            StaffRole.NetworkEng => new[] { RoomType.MeetMe, RoomType.DataHall },
            StaffRole.Noc => new[] { RoomType.Noc },
            StaffRole.Receptionist => new[] { RoomType.Reception },
            StaffRole.Canteen => new[] { RoomType.Canteen },
            StaffRole.Helpdesk => new[] { RoomType.Office },
            StaffRole.Logistics => new[] { RoomType.LoadingDock, RoomType.Storage },
            StaffRole.Researcher => new[] { RoomType.Lab },
            _ => Array.Empty<RoomType>(),
        };
        foreach (var room in S.Rooms.Where(x => rooms.Contains(x.Type) && x.Stage == Stage.Done && S.BuildingOf(x)?.Stage == Stage.Done))
            for (var z = room.Z; z < room.Z + room.D; z++)
                for (var x = room.X; x < room.X + room.W; x++)
                    if (Nav.Walkable(x, z, room.Floor)) list.Add((x, z, room.Floor));
        if (r is StaffRole.Guard or StaffRole.Gardener or StaffRole.GeneratorTech)
            for (var z = 0; z < S.PlotSize; z++)
                for (var x = 0; x < S.PlotSize; x++)
                {
                    if (S.BuildingAt(x, z) != null || !Nav.Walkable(x, z, 0)) continue;
                    var surf = S.SurfaceAt(x, z);
                    var ok = r == StaffRole.Gardener ? surf == Surface.Grass && (x + z) % 3 == 0 : surf is Surface.Footpath or Surface.Asphalt or Surface.Parking;
                    if (ok) list.Add((x, z, 0));
                }
        // a sample is plenty to wander between
        if (list.Count > 300) list = list.OrderBy(c => (c.Item1 * 7919 + c.Item2 * 104729 + c.Item3) % 1009).Take(300).ToList();
        _areas[r] = list;
        return list;
    }

    void StaffWalk(Agent a, double dt)
    {
        a.Working = false;
        switch (a.Phase)
        {
            case 0: // decide
                a.Timer -= dt;
                if (a.Timer > 0) return;
                a.Timer = 0.5;
                if (a.Target != 0)
                {
                    var o = S.Objects.FirstOrDefault(x => x.Id == a.Target);
                    if (o == null || !o.Broken) { a.Target = 0; return; }
                    var cells = GameState.Cells(o).ToHashSet();
                    bool Next(int x, int z) => Nav.Walkable(x, z, o.Floor) && !cells.Contains((x, z)) &&
                        (cells.Contains((x + 1, z)) || cells.Contains((x - 1, z)) || cells.Contains((x, z + 1)) || cells.Contains((x, z - 1)));
                    if (!Go(a, o.Floor, Next, NavMode.Walk)) { a.Timer = 2; return; }
                }
                else
                {
                    var area = Area(a.Staff);
                    if (area.Count == 0) { a.Phase = 8; return; }
                    var c = area[_rng.Next(area.Count)];
                    if (!Go(a, c.f, (x, z) => x == c.x && z == c.z, NavMode.Walk)) { a.Timer = 1; return; }
                }
                a.Phase = 1;
                return;
            case 1:
                if (!Move(a, dt, WalkSpeed * (a.Staff == StaffRole.Guard ? 0.6 : 0.85))) return;
                a.Phase = 2; a.Timer = a.Target != 0 ? 0 : 0.3 + _rng.NextDouble() * (a.Staff is StaffRole.Noc or StaffRole.Receptionist or StaffRole.Helpdesk or StaffRole.Researcher ? 3 : 0.8);
                return;
            case 2:
                if (a.Target != 0)
                {
                    var o = S.Objects.FirstOrDefault(x => x.Id == a.Target);
                    if (o == null || !o.Broken) { a.Target = 0; a.Phase = a.EmpId < 0 ? 8 : 0; return; }
                    a.Working = true;
                    a.Heading = Math.Atan2(o.X + 0.5 - a.X, o.Z + 0.5 - a.Z);
                    return;
                }
                a.Working = a.Staff is StaffRole.Cleaner or StaffRole.Gardener or StaffRole.Noc or StaffRole.Helpdesk or StaffRole.Researcher;
                a.Timer -= dt;
                if (a.Timer <= 0) a.Phase = 0;
                return;
            case 8: // leave the site
                var (hx, hz) = a.EmpId < 0 ? SpawnCell() : StaffEntry();
                if (!Go(a, 0, (x, z) => x == hx && z == hz, NavMode.Walk)) { a.Gone = true; return; }
                a.Phase = 9;
                return;
            default:
                if (Move(a, dt, WalkSpeed)) a.Gone = true;
                return;
        }
    }

    // ---------------- escorts (step 8) ----------------
    /// <summary>With the escort policy on, a guard walks in with the customer, waits at the rack and walks out again.</summary>
    void StartEscort(Agent visitor, PlacedObject rack)
    {
        if (Agents.Count(a => a.Escort) >= 6) return;
        var g = new Agent { Id = _nextAgent++, Role = Role.Staff, Staff = StaffRole.Guard, EmpId = -900000 - visitor.Id, Escort = true, Target = visitor.Id, X = visitor.X + 0.3, Z = visitor.Z + 0.3 };
        var vx = visitor.Way.Count > 0 ? visitor.Way[^1] : (visitor.X, visitor.Z, rack.Floor);
        if (!Go(g, rack.Floor, (x, z) => Math.Abs(x - rack.X) + Math.Abs(z - rack.Z) <= 2 && !(x == (int)vx.Item1 && z == (int)vx.Item2) && Nav.Walkable(x, z, rack.Floor), NavMode.Walk)) return;
        g.Phase = 1;
        Agents.Add(g);
    }

    void EscortWalk(Agent a, double dt)
    {
        a.Working = false;
        var v = Agents.FirstOrDefault(x => x.Id == a.Target && x.Role == Role.Visitor);
        switch (a.Phase)
        {
            case 1:
                if (!Move(a, dt, WalkSpeed * 0.8)) return;
                a.Phase = 2;
                return;
            case 2: // wait with the customer until the work is done
                if (v != null && v.Phase is 1 or 2) { if (v.Phase == 2) a.Heading = Math.Atan2(v.X - a.X, v.Z - a.Z); return; }
                a.Phase = 8;
                return;
            case 8:
                var (hx, hz) = StaffEntry();
                if (!Go(a, 0, (x, z) => x == hx && z == hz, NavMode.Walk)) { a.Gone = true; return; }
                a.Phase = 9;
                return;
            default:
                if (Move(a, dt, WalkSpeed)) a.Gone = true;
                return;
        }
    }
}
