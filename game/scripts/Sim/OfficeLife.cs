using System;
using System.Collections.Generic;
using System.Linq;

namespace DatacenterArchitect.Sim;

/// <summary>
/// Office life (playtest): directors, managers and sales reps come to work on weekdays, each at their own desk. The board
/// sits in its director's offices, managers and reps at the desks in office rooms. They arrive between about 7:45 and 9:00,
/// work at the desk, now and then walk to a meeting room, the canteen or a colleague, and pack up between about 16:15 and
/// 17:45. Purely visual: what they do for the company runs in GameState.
/// </summary>
public partial class Simulation
{
    /// <summary>At most this many office figures at once, however big the company, to keep the game light.</summary>
    public const int MaxOfficeAgents = 20;
    public const int ExecEmpBase = 700000, RepEmpBase = 710000;

    static uint Mix(int id, uint salt) => (uint)(id * 2654435761u ^ salt) * 2246822519u >> 7;

    /// <summary>Whether this office worker is at work now: weekdays, with own arrival and leaving times.</summary>
    public bool OfficeHours(int empId)
    {
        var d = S.Date;
        if (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) return false;
        var h = d.Hour + d.Minute / 60.0;
        // they set off early: the drive and the walk from the car park to the desk take a while in game time
        var arrive = 6.5 + Mix(empId, 0x9e37) % 100 / 100.0 * 1.25;
        var leave = 16.25 + Mix(empId, 0x7f4a) % 100 / 100.0 * 1.5;
        return h >= arrive && h < leave;
    }

    List<PlacedObject> DesksIn(Room r) => S.Objects.Where(o => o.Kind == ObjKind.Desk && o.Floor == r.Floor && r.Contains(o.X, o.Z) && o.Status == ObjStatus.Live)
        .OrderBy(o => o.Id).ToList();

    bool RoomReady(Room r) => r.Stage == Stage.Done && S.BuildingOf(r)?.Stage == Stage.Done && !S.IsPlanned(r);

    /// <summary>Who works at which desk: the board in the director's offices, then managers and sales reps at office desks.</summary>
    public List<(int emp, int desk)> OfficePeople()
    {
        var list = new List<(int, int)>();
        var execRooms = S.Rooms.Where(r => r.Type == RoomType.Executive && RoomReady(r)).OrderBy(r => r.Id)
            .Select(DesksIn).Where(d => d.Count > 0).ToList();
        var ri = 0;
        foreach (var e in S.Execs.Where(x => Org.IsBoard(x.Role)).OrderBy(x => x.Role))
            if (ri < execRooms.Count) list.Add((ExecEmpBase + (int)e.Role, execRooms[ri++][0].Id));
        var desks = S.Rooms.Where(r => r.Type == RoomType.Office && RoomReady(r)).OrderBy(r => r.Id).SelectMany(DesksIn).ToList();
        var di = 0;
        foreach (var e in S.Execs.Where(x => !Org.IsBoard(x.Role)).OrderBy(x => x.Role))
            if (di < desks.Count) list.Add((ExecEmpBase + (int)e.Role, desks[di++].Id));
        for (var i = 0; i < S.SalesReps && di < desks.Count; i++) list.Add((RepEmpBase + i, desks[di++].Id));
        return list;
    }

    void SyncOffice()
    {
        var want = OfficePeople().Where(p => OfficeHours(p.emp)).Take(MaxOfficeAgents).ToDictionary(p => p.emp, p => p.desk);
        var present = Agents.Where(a => a.Role == Role.Staff && a.Desk != 0).ToList();
        foreach (var a in present)
        {
            if (Leaving(a)) continue;
            if (!want.TryGetValue(a.EmpId, out var desk)) { a.Phase = 7; a.Timer = _rng.NextDouble() * 0.3; a.Working = false; continue; }
            if (desk != a.Desk) { a.Desk = desk; if (!Arriving(a)) { a.Phase = 0; a.Timer = 0; } }
        }
        var have = present.Where(a => !Leaving(a)).Select(a => a.EmpId).ToHashSet();
        var started = 0;
        foreach (var (id, deskId) in want)
        {
            if (have.Contains(id)) continue;
            if (_settled && started >= ArrivalsPerTick) break;
            var desk = S.Objects.FirstOrDefault(o => o.Id == deskId);
            if (desk == null) continue;
            var start = ArrivalCell(id);
            var ag = new Agent { Id = _nextAgent++, Role = Role.Staff, EmpId = id, Staff = StaffRole.Helpdesk, Desk = deskId, X = start.x + 0.5, Z = start.z + 0.5 };
            var spot = DeskSpot(desk) ?? (desk.X, desk.Z);
            if (!_settled)
            {
                Settle(ag, spot, desk.Floor);
                if (ag.Floor == desk.Floor && (int)Math.Floor(ag.X) == spot.x && (int)Math.Floor(ag.Z) == spot.z) { ag.Phase = 2; ag.Timer = 0.5 + _rng.NextDouble() * 2; }
            }
            else SendToWork(ag, spot);
            Agents.Add(ag);
            started++;
        }
    }

    /// <summary>Where someone stands to work at a desk: next to it, on the chair's cell when there is one.</summary>
    (int x, int z)? DeskSpot(PlacedObject desk)
    {
        var cells = GameState.Cells(desk).ToHashSet();
        var around = new List<(int x, int z)>();
        foreach (var (x, z) in cells)
            foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                var c = (x + dx, z + dz);
                if (!cells.Contains(c) && Nav.Walkable(c.Item1, c.Item2, desk.Floor) && !around.Contains(c)) around.Add(c);
            }
        if (around.Count == 0) return null;
        var chair = around.FirstOrDefault(c => S.ObjectAt(c.x, c.z, desk.Floor)?.Kind == ObjKind.Chair);
        return chair != default ? chair : around[0];
    }

    readonly List<(int x, int z, int f)> _breakSpots = new();
    int _breakRev = -1;

    /// <summary>Places to walk to for a moment: meeting rooms, the canteen, lounges, reception and other offices.</summary>
    List<(int x, int z, int f)> BreakSpots()
    {
        if (_breakRev == S.LayoutRevision) return _breakSpots;
        _breakRev = S.LayoutRevision;
        _breakSpots.Clear();
        foreach (var r in S.Rooms.Where(r => r.Type is RoomType.Meeting or RoomType.Canteen or RoomType.Lounge or RoomType.Office or RoomType.Reception && RoomReady(r)))
            for (var z = r.Z; z < r.Z + r.D; z++)
                for (var x = r.X; x < r.X + r.W; x++)
                    if (Nav.Walkable(x, z, r.Floor) && S.ObjectAt(x, z, r.Floor) == null) _breakSpots.Add((x, z, r.Floor));
        if (_breakSpots.Count > 300)
        {
            var keep = _breakSpots.OrderBy(c => (c.x * 7919 + c.z * 104729 + c.f) % 1009).Take(300).ToList();
            _breakSpots.Clear(); _breakSpots.AddRange(keep);
        }
        return _breakSpots;
    }

    /// <summary>0 to the desk, 1 walking there, 2 working, 3 walking to a break spot, 4 there for a moment.</summary>
    void OfficeWalk(Agent a, double dt)
    {
        if (Commuting(a, dt)) return;
        a.Working = false;
        var desk = S.Objects.FirstOrDefault(o => o.Id == a.Desk);
        if (desk == null) { a.Phase = 7; a.Timer = 0; return; }
        switch (a.Phase)
        {
            case 0:
                a.Timer -= dt;
                if (a.Timer > 0) return;
                a.Timer = 0.5;
                if (DeskSpot(desk) is not { } spot || !GoTo(a, desk.Floor, spot.x, spot.z, NavMode.Walk)) { a.Timer = 1; return; }
                a.Phase = 1;
                return;
            case 1:
                if (!Move(a, dt, WalkSpeed)) return;
                a.Phase = 2; a.Timer = 1.2 + _rng.NextDouble() * 2.5;
                return;
            case 2:
                a.Working = true;
                var (w, d) = GameState.Footprint(desk);
                a.Heading = Math.Atan2(desk.X + w / 2.0 - a.X, desk.Z + d / 2.0 - a.Z);
                a.Timer -= dt;
                if (a.Timer > 0) return;
                var spots = BreakSpots();
                if (spots.Count == 0) { a.Timer = 1; return; }
                var c = spots[_rng.Next(spots.Count)];
                if (!GoTo(a, c.f, c.x, c.z, NavMode.Walk)) { a.Timer = 0.5; return; }
                a.Working = false; a.Phase = 3;
                return;
            case 3:
                if (!Move(a, dt, WalkSpeed * 0.8)) return;
                a.Phase = 4; a.Timer = 0.15 + _rng.NextDouble() * 0.35;
                return;
            case 4:
                a.Timer -= dt;
                if (a.Timer <= 0) { a.Phase = 0; a.Timer = 0; }
                return;
        }
    }
}
