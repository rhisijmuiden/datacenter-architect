using System;
using System.Collections.Generic;
using System.Linq;

namespace DatacenterArchitect.Sim;

/// <summary>How a member of staff gets to work. Fixed per person, so the same people always cycle.</summary>
public enum CommuteMode { Car, ElectricCar, Bike }

/// <summary>
/// Commuting (playtest): about half the staff cycle in, the rest come by car, and a share of the drivers have an
/// electric car. Cyclists need bike racks or a bike shed (with e-bike charging); without them everyone drives.
/// Electric cars park at the bays next to a charging point. Purely visual: it decides where staff figures appear.
/// </summary>
public static class Commute
{
    /// <summary>Bikes per rack or shed.</summary>
    public const int RackPlaces = 5, ShedPlaces = 10;

    /// <summary>What this person would like to use (before checking the site has room for bikes).</summary>
    public static CommuteMode Preferred(int empId)
    {
        var h = (uint)(empId * 2654435761u) >> 8;
        var r = h % 100;
        return r < 48 ? CommuteMode.Bike : r < 70 ? CommuteMode.ElectricCar : CommuteMode.Car;
    }

    public static IEnumerable<PlacedObject> BikePlaces(GameState s) =>
        s.Objects.Where(o => o.Kind is ObjKind.BikeRack or ObjKind.BikeShed && o.Floor == 0 && o.Status == ObjStatus.Live && !s.IsPlanned(o));

    public static int BikeCapacity(GameState s) => BikePlaces(s).Sum(o => o.Kind == ObjKind.BikeShed ? ShedPlaces : RackPlaces);

    public static IEnumerable<PlacedObject> Chargers(GameState s) =>
        s.Objects.Where(o => o.Kind == ObjKind.EvCharger && o.Status == ObjStatus.Live && !s.IsPlanned(o));

    /// <summary>
    /// How each person on duty comes in today. Cyclists beyond the bike places drive instead, and electric drivers
    /// without a charging point park as normal (still <see cref="CommuteMode.ElectricCar"/>, the car just does not charge).
    /// </summary>
    public static Dictionary<int, CommuteMode> Today(GameState s)
    {
        var places = BikeCapacity(s);
        var map = new Dictionary<int, CommuteMode>();
        foreach (var e in s.Staff.OrderBy(e => e.Id))
        {
            var m = Preferred(e.Id);
            if (m == CommuteMode.Bike)
            {
                if (places > 0) places--;
                else m = CommuteMode.Car;
            }
            map[e.Id] = m;
        }
        return map;
    }

    /// <summary>Share of the staff that cycles in, 0..1, for the staff screen.</summary>
    public static double BikeShare(GameState s)
    {
        if (s.Staff.Count == 0) return 0;
        var t = Today(s);
        return t.Values.Count(m => m == CommuteMode.Bike) / (double)s.Staff.Count;
    }
}

public partial class Simulation
{
    int _commuteRev = -1;
    List<(int x, int z)> _bikeCells = new(), _evCells = new();

    void CommuteCells()
    {
        if (_commuteRev == S.LayoutRevision) return;
        _commuteRev = S.LayoutRevision;
        _bikeCells = new();
        foreach (var o in Commute.BikePlaces(S))
        {
            var (w, d) = GameState.Footprint(o);
            for (var z = o.Z - 1; z <= o.Z + d; z++)
                for (var x = o.X - 1; x <= o.X + w; x++)
                    if (S.ObjectAt(x, z, 0) == null && S.BuildingAt(x, z) == null && Nav.Walkable(x, z, 0)) _bikeCells.Add((x, z));
        }
        _evCells = new();
        foreach (var o in Commute.Chargers(S))
            for (var z = o.Z - 2; z <= o.Z + 2; z++)
                for (var x = o.X - 2; x <= o.X + 2; x++)
                    if (S.SurfaceAt(x, z) == Surface.Parking && Nav.Walkable(x, z, 0)) _evCells.Add((x, z));
    }

    /// <summary>How this employee comes to work (contractors, with id &lt; 0, always drive).</summary>
    public CommuteMode ModeOf(int empId)
    {
        if (empId <= 0) return CommuteMode.Car;
        var m = Commute.Preferred(empId);
        CommuteCells();
        if (m == CommuteMode.Bike && _bikeCells.Count == 0) return CommuteMode.Car;
        return m;
    }

    /// <summary>Where a person leaves their bike or car: a cell by a bike rack or shed, an EV bay, or any parking cell.</summary>
    (int x, int z) ArrivalCell(int empId)
    {
        CommuteCells();
        var m = ModeOf(empId);
        if (m == CommuteMode.Bike && _bikeCells.Count > 0) return _bikeCells[Math.Abs(empId) % _bikeCells.Count];
        if (m == CommuteMode.ElectricCar && _evCells.Count > 0) return _evCells[Math.Abs(empId) % _evCells.Count];
        return StaffEntryCar();
    }

    /// <summary>Where cyclists ride in from: the plot edge by the public road.</summary>
    (int x, int z) RoadEdge()
    {
        var z = S.PlotSize - 1;
        for (var i = 0; i < S.PlotSize / 2; i++)
            foreach (var x in new[] { S.PlotSize / 2 - i, S.PlotSize / 2 + i })
                if (Nav.Walkable(x, z, 0) && S.SurfaceAt(x, z) != Surface.Grass) return (x, z);
        return SpawnCell();
    }

    public const double RideSpeed = 34; // cells per game hour

    /// <summary>A cyclist rides in from the road to the bike rack, gets off and walks to work; going home the other way.</summary>
    bool StartRide(Agent a)
    {
        if (a.EmpId <= 0 || ModeOf(a.EmpId) != CommuteMode.Bike) return false;
        var bike = ArrivalCell(a.EmpId);
        var (rx, rz) = RoadEdge();
        a.X = rx + 0.5; a.Z = rz + 0.5; a.Floor = 0;
        a.Goal = bike;
        if (!GoTo(a, 0, bike.x, bike.z, NavMode.Walk)) { a.X = bike.x + 0.5; a.Z = bike.z + 0.5; return false; }
        a.Ride = true; a.Phase = 20;
        return true;
    }
}
