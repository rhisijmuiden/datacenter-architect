using System;
using System.Collections.Generic;
using System.Linq;
using DatacenterArchitect.Sim;

namespace DatacenterArchitect.Tests;

/// <summary>Playtest: cars at a shift change get into the bays and out again, without getting stuck or driving through each other.</summary>
public static partial class TestRunner
{
    static void ParkingJamTests()
    {
        var s = Quiet();
        // a big car park west of the driveway, reached through a 6 m wide aisle, like a real site
        s.PaintSurface(10, 38, 18, 16, Surface.Parking, out _);
        s.PaintSurface(10, 36, 18, 2, Surface.Asphalt, out _);
        // and a driveway widened to two lanes, so traffic in and out crosses at the junctions
        s.PaintSurface(25, 34, 3, 26, Surface.Asphalt, out _);
        var roles = new[] { StaffRole.Cleaner, StaffRole.Support, StaffRole.RemoteHands, StaffRole.Electrical, StaffRole.Cooling, StaffRole.NetworkEng, StaffRole.Helpdesk, StaffRole.Logistics };
        foreach (var shift in new[] { Shift.Night, Shift.Day })
            for (var i = 0; i < 16; i++) s.Staff.Add(new Employee { Id = 3000 + (int)shift * 500 + i * 7, Name = "Demo", Role = roles[i % roles.Length], Shift = shift });
        s.Hours = (new DateTime(2027, 6, 15, 5, 0, 0) - GameState.Epoch).TotalHours;
        var sim = new Simulation(s);
        var still = new Dictionary<int, (double x, double z, double t)>();
        var worstStuck = 0.0; var overlaps = 0;
        string stuckWho = "";
        for (var t = 0.0; t < 6; t += 0.05)
        {
            s.Hours += 0.05; sim.Tick(0.05);
            var cars = sim.Agents.Where(a => a.Car).ToList();
            foreach (var a in cars)
            {
                if (still.TryGetValue(a.Id, out var p) && Math.Abs(p.x - a.X) + Math.Abs(p.z - a.Z) < 0.05)
                {
                    still[a.Id] = (p.x, p.z, p.t + 0.05);
                    if (p.t + 0.05 > worstStuck) { worstStuck = p.t + 0.05; stuckWho = $"car {a.Id} phase {a.Phase} at {a.X:0.0},{a.Z:0.0} heading {a.Heading:0.00}"; }
                }
                else still[a.Id] = (a.X, a.Z, 0);
            }
            for (var i = 0; i < cars.Count; i++)
                for (var j = i + 1; j < cars.Count; j++)
                {
                    var d = Math.Sqrt(Math.Pow(cars[i].X - cars[j].X, 2) + Math.Pow(cars[i].Z - cars[j].Z, 2));
                    if (d < 1.6) overlaps++;
                }
        }
        Check(worstStuck < 0.5, $"no car stands still on the road for long ({worstStuck:0.00} h: {stuckWho})");
        Check(sim.Parked.Count >= 4 && !sim.Agents.Any(a => a.Car && a.Phase == 30), $"everyone who drove in reached a bay ({sim.Parked.Count} parked)");
        Check(overlaps < 10, $"cars do not drive through each other ({overlaps} close samples)");
    }
}
