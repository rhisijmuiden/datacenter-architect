using System;
using System.Collections.Generic;
using System.Linq;

namespace DatacenterArchitect.Sim;

/// <summary>A car standing in a parking bay: whose it is, where, which way it points and whether it is electric.</summary>
public readonly record struct ParkedCar(int Emp, double X, double Z, double Heading, bool Electric);

/// <summary>
/// Commuting by car (playtest): staff drive in from the street, take a free bay (EV bays for electric cars), get out and
/// walk to work. At the end of their shift or office day they walk back and drive off. The car park so fills and empties
/// with the day instead of being full from the start. Purely visual, like the rest of the figures.
/// </summary>
public partial class Simulation
{
    public const double CarSpeed = 70; // cells per game hour
    /// <summary>At most this many people set off for work per staff update (every quarter of a game hour).</summary>
    public const int ArrivalsPerTick = 3;

    /// <summary>False until the first staff update: whoever is at work when a game is loaded is already there, car parked.</summary>
    bool _settled;

    List<ParkingLayout.Bay> _bays = new();
    int _baysRev = -1;

    /// <summary>Cars in the bays, by bay key (<see cref="BayKey"/>).</summary>
    public readonly Dictionary<int, ParkedCar> Parked = new();

    public static int BayKey(ParkingLayout.Bay b) => (int)Math.Round(b.X * 2) * 10000 + (int)Math.Round(b.Z * 2);

    /// <summary>The painted bays of the site; when the layout changes, cars in bays that are gone disappear.</summary>
    public List<ParkingLayout.Bay> Bays
    {
        get
        {
            if (_baysRev == S.LayoutRevision) return _bays;
            _baysRev = S.LayoutRevision;
            _bays = ParkingLayout.SiteBays(S);
            var keys = _bays.Select(BayKey).ToHashSet();
            foreach (var k in Parked.Keys.Where(k => !keys.Contains(k)).ToList()) Parked.Remove(k);
            return _bays;
        }
    }

    /// <summary>Send everyone on site home at once and forget the parked cars (time jumps in screenshots and tests).</summary>
    public void ResetCommute()
    {
        Agents.RemoveAll(a => a.Role == Role.Staff && !a.Escort);
        Parked.Clear();
        _settled = false;
    }

    static bool Leaving(Agent a) => a.Phase is 7 or 8 or 9 or 22 or 31 or 33;
    static bool Arriving(Agent a) => a.Phase is 20 or 30;

    static (int x, int z) CellOf(ParkingLayout.Bay b) => ((int)Math.Floor(b.X), (int)Math.Floor(b.Z));

    /// <summary>A free bay near where the person works (an EV bay for an electric car if there is one).</summary>
    ParkingLayout.Bay? FreeBay(bool electric, (int x, int z) near)
    {
        var taken = Parked.Keys.ToHashSet();
        foreach (var a in Agents.Where(a => a.Car && a.Bay != 0)) taken.Add(a.Bay);
        var free = Bays.Where(b => !taken.Contains(BayKey(b)) && Nav.TruckCell(CellOf(b).x, CellOf(b).z)).ToList();
        if (free.Count == 0) return null;
        var pref = free.Where(b => b.Ev == electric).ToList();
        if (pref.Count == 0) pref = free;
        // the nearest few, then one of those so not everyone fights over the same bay
        var near3 = pref.OrderBy(b => Math.Abs(b.X - near.x) + Math.Abs(b.Z - near.z)).Take(3).ToList();
        return near3[_rng.Next(near3.Count)];
    }

    /// <summary>Sends a person on their way in: by bike, by car to a bay, or (no road or no bay) on foot from the car park as before.</summary>
    void SendToWork(Agent a, (int x, int z) dest)
    {
        if (a.EmpId <= 0) return;
        if (ModeOf(a.EmpId) == CommuteMode.Bike) { StartRide(a); return; }
        StartDrive(a, dest);
    }

    /// <summary>Puts someone straight at work (a loaded game): their car is already in a bay.</summary>
    void Settle(Agent a, (int x, int z) dest, int floor)
    {
        if (a.EmpId > 0 && ModeOf(a.EmpId) != CommuteMode.Bike && FreeBay(ModeOf(a.EmpId) == CommuteMode.ElectricCar, dest) is { } bay)
        {
            var h = bay.Yaw + ((bay.Hash / 7) % 2 == 0 ? 0 : Math.PI);
            Parked[BayKey(bay)] = new ParkedCar(a.EmpId, bay.X, bay.Z, h, ModeOf(a.EmpId) == CommuteMode.ElectricCar);
        }
        if (Nav.Walkable(dest.x, dest.z, floor)) { a.X = dest.x + 0.5; a.Z = dest.z + 0.5; a.Floor = floor; }
    }

    /// <summary>Drives in from the street along the road to a free bay. False when there is no bay or no way to it.</summary>
    bool StartDrive(Agent a, (int x, int z) dest)
    {
        var electric = ModeOf(a.EmpId) == CommuteMode.ElectricCar;
        if (FreeBay(electric, dest) is not { } bay) return false;
        var (cx, cz) = CellOf(bay);
        var entries = Entries();
        if (entries.Count == 0) return false;
        var path = Nav.FindOnFloor(entries, 0, (x, z) => x == cx && z == cz, NavMode.Truck);
        if (path == null)
        {
            // a car park that no road leads to: the car is just there, and its driver walks from it
            var h = bay.Yaw + ((bay.Hash / 7) % 2 == 0 ? 0 : Math.PI);
            Parked[BayKey(bay)] = new ParkedCar(a.EmpId, bay.X, bay.Z, h, electric);
            a.X = bay.X; a.Z = bay.Z; a.Floor = 0;
            return true;
        }
        var first = path.Count > 0 ? path[0] : new Pt(cx, cz, 0);
        var entry = entries.OrderBy(e => Math.Abs(e.x - first.X) + Math.Abs(e.z - first.Z)).First();
        var lane = S.PlotSize + 3.2;
        var lanePath = Nav.CenterLane(path);
        var ex0 = lanePath.Count > 0 && Math.Abs(lanePath[0].x - (entry.x + 0.5)) <= 4 ? lanePath[0].x : entry.x + 0.5;
        a.Way.Clear(); a.WayI = 0; a.ReverseFrom = -1;
        a.X = -20 - _rng.NextDouble() * 6; a.Z = lane; a.Floor = 0;
        a.Way.Add((ex0, lane, 0));
        a.Way.Add((ex0, S.PlotSize + 0.5, 0));
        a.Way.Add((ex0, entry.z + 0.5, 0));
        // stay on the lane until the bay's own cell, then pull into the bay
        foreach (var (px, pz) in lanePath.Take(Math.Max(0, lanePath.Count - 1))) a.Way.Add((px, pz, 0));
        a.Way.Add((bay.X, bay.Z, 0));
        a.Route = new List<Pt> { new(entry.x, entry.z, 0) };
        a.Route.AddRange(path);
        a.Car = true; a.Bay = BayKey(bay); a.Phase = 30; a.Goal = (cx, cz); a.ReverseTo = 0;
        return true;
    }

    /// <summary>Parked: the car stays in the bay (pointing along it) and the driver gets out beside it.</summary>
    void ArriveByCar(Agent a)
    {
        var bay = Bays.FirstOrDefault(b => BayKey(b) == a.Bay);
        a.Car = false;
        if (bay == default) { a.Bay = 0; return; }
        var h = a.Heading;
        var d0 = Math.Abs(Math.IEEERemainder(h - bay.Yaw, 2 * Math.PI));
        var heading = d0 <= Math.PI / 2 ? bay.Yaw : bay.Yaw + Math.PI;
        Parked[a.Bay] = new ParkedCar(a.EmpId, bay.X, bay.Z, heading, ModeOf(a.EmpId) == CommuteMode.ElectricCar);
        // the driver's door: on the left of the car
        double sx = -Math.Cos(heading), sz = Math.Sin(heading);
        var ox = bay.X + sx * 1.1; var oz = bay.Z + sz * 1.1;
        if (Nav.Walkable((int)Math.Floor(ox), (int)Math.Floor(oz), 0)) { a.X = ox; a.Z = oz; }
        else { a.X = bay.X; a.Z = bay.Z; }
        a.Way.Clear(); a.WayI = 0; a.Route = null;
    }

    /// <summary>Walk back to the parked car; false when this person has no car on the site.</summary>
    bool LeaveByCar(Agent a)
    {
        foreach (var (key, car) in Parked)
        {
            if (car.Emp != a.EmpId) continue;
            var (cx, cz) = ((int)Math.Floor(car.X), (int)Math.Floor(car.Z));
            if (!GoTo(a, 0, cx, cz, NavMode.Walk)) return false;
            a.Bay = key; a.Phase = 31;
            return true;
        }
        return false;
    }

    /// <summary>At the car: get in and drive off the site and away down the street.</summary>
    void DriveOff(Agent a)
    {
        if (!Parked.TryGetValue(a.Bay, out var car)) { a.Gone = true; return; }
        Parked.Remove(a.Bay);
        var cell = ((int)Math.Floor(car.X), (int)Math.Floor(car.Z));
        var entries = Entries().ToHashSet();
        var path = entries.Count == 0 ? null : Nav.FindOnFloor(new[] { cell }, 0, (x, z) => entries.Contains((x, z)), NavMode.Truck);
        if (path == null) { a.Gone = true; return; }
        a.Car = true; a.X = car.X; a.Z = car.Z; a.Floor = 0; a.Heading = car.Heading;
        a.Way.Clear(); a.WayI = 0; a.ReverseFrom = -1;
        // back out of the bay first, then drive forwards along the road
        double fx = Math.Sin(car.Heading), fz = Math.Cos(car.Heading);
        a.Way.Add((car.X - fx * 2.6, car.Z - fz * 2.6, 0));
        a.ReverseTo = 1;
        var lane = Nav.CenterLane(path);
        var exit = lane.Count > 0 ? lane[^1].x : cell.Item1 + 0.5;
        foreach (var (px, pz) in lane) a.Way.Add((px, pz, 0));
        a.Way.Add((exit, S.PlotSize + 0.5, 0));
        a.Way.Add((exit, S.PlotSize + 3.2, 0));
        a.Way.Add((S.PlotSize + 40, S.PlotSize + 3.2, 0));
        a.Phase = 33;
    }

    /// <summary>
    /// The coming and going shared by all staff figures. Returns true when it handled this step.
    /// 7 finishing up, 8 walking out, 9 on the way off the site, 20 cycling in, 22 at the bike, 30 driving in, 31 walking to the car, 33 driving off.
    /// </summary>
    bool Commuting(Agent a, double dt)
    {
        switch (a.Phase)
        {
            case 7:
                a.Working = false;
                a.Timer -= dt;
                if (a.Timer <= 0) a.Phase = 8;
                return true;
            case 8:
                a.Working = false; a.Ride = false;
                if (a.EmpId > 0 && LeaveByCar(a)) return true;
                var (hx, hz) = a.EmpId < 0 ? SpawnCell() : ArrivalCell(a.EmpId);
                if (!GoTo(a, 0, hx, hz, NavMode.Walk)) { a.Gone = true; return true; }
                a.Phase = a.EmpId > 0 && ModeOf(a.EmpId) == CommuteMode.Bike ? 22 : 9;
                return true;
            case 20: // riding in from the road
                if (!Move(a, dt, RideSpeed * (a.Floor == 0 ? S.TravelFactor : 1))) return true;
                a.Ride = false; a.Phase = 0; a.Timer = 0.15;
                return true;
            case 22: // at the bike: get on and ride home
                if (!Move(a, dt, WalkSpeed)) return true;
                var (ex, ez) = RoadEdge();
                if (!GoTo(a, 0, ex, ez, NavMode.Walk)) { a.Gone = true; return true; }
                a.Ride = true; a.Phase = 9;
                return true;
            case 30: // driving in to the bay
                if (!Drive(a, dt, CarSpeed)) return true;
                ArriveByCar(a);
                a.Phase = 0; a.Timer = 0.1;
                return true;
            case 31: // walking to the car
                if (!Move(a, dt, WalkSpeed)) return true;
                DriveOff(a);
                return true;
            case 33:
                if (Drive(a, dt, CarSpeed)) a.Gone = true;
                return true;
            case 9:
                if (Move(a, dt, a.Ride ? RideSpeed : WalkSpeed)) a.Gone = true;
                return true;
        }
        return false;
    }
}
