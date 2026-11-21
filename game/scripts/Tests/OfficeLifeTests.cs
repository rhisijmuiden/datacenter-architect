using System;
using System.Linq;
using DatacenterArchitect.Sim;

namespace DatacenterArchitect.Tests;

/// <summary>Playtest: office workers commute by car, park, work at their desks and go home; only the board needs a director's office.</summary>
public static partial class TestRunner
{
    static void OfficeLifeTests()
    {
        // ---------------- board and managers ----------------
        Check(Org.IsBoard(Exec.Ceo) && Org.IsBoard(Exec.Operations) && !Org.IsBoard(Exec.Legal) && Org.Board.Count == 5, "five board roles need a director's office");
        var o = Quiet();
        Check(o.ManagerCount + o.BoardCount == o.Execs.Count, "every director is either board or manager");
        o.Execs.RemoveAll(x => x.Role is Exec.Legal);
        o.SalesReps = 0;
        var managersBefore = o.ManagerCount;
        var why = o.CanHire(Exec.Legal);
        Check(why == null || why == "msg_needs_manager_desk" || why == "msg_no_money", $"a manager needs a desk, not a director's office ({why})");
        if (o.Workplaces > o.ManagerCount) Check(o.CanHire(Exec.Legal) is null or "msg_no_money", "a free office desk is enough for a manager");
        Check(o.DeskNeed >= managersBefore, "managers count towards the desks the office needs");

        // ---------------- the morning commute ----------------
        var s = Quiet();
        s.Hours = (new DateTime(2027, 6, 15, 6, 0, 0) - GameState.Epoch).TotalHours; // a Tuesday
        var sim = new Simulation(s);
        var people = sim.OfficePeople();
        Check(people.Count > 0 && people.Select(p => p.desk).Distinct().Count() == people.Count, $"office people each get an own desk ({people.Count})");
        Check(sim.Bays.Count > 0, "the demo car park has bays");
        void Run(double hours) { for (var t = 0.0; t < hours; t += 0.05) { s.Hours += 0.05; sim.Tick(0.05); } }
        Run(0.2);
        Check(sim.Parked.Count == 0 && !sim.Agents.Any(a => a.Desk != 0), "before 6:30 nobody is on the way yet and the car park is empty");
        var sawCar = false;
        for (var i = 0; i < 40 && !sawCar; i++) { Run(0.05); sawCar = sim.Agents.Any(a => a.Car); }
        Check(sawCar, "in the morning people drive in");
        Run(5.0);
        var drivers = people.Select(p => p.emp).Where(e => sim.ModeOf(e) != CommuteMode.Bike).ToList();
        Check(sim.Parked.Count > 0 && sim.Parked.Count <= drivers.Count, $"their cars stand in the bays ({sim.Parked.Count} of {drivers.Count} drivers)");
        Check(sim.Parked.Values.All(c => sim.Bays.Any(b => Math.Abs(b.X - c.X) < 0.01 && Math.Abs(b.Z - c.Z) < 0.01)), "parked cars stand in the middle of a bay");
        Check(sim.Parked.Values.Select(c => (c.X, c.Z)).Distinct().Count() == sim.Parked.Count, "one car per bay");
        var working = sim.Agents.Where(a => a.Desk != 0 && a.Working).ToList();
        Check(working.Count > 0, $"at the office they work at their desks ({working.Count})");

        // ---------------- going home ----------------
        s.Hours = (new DateTime(2027, 6, 15, 18, 0, 0) - GameState.Epoch).TotalHours;
        var drove = false;
        for (var i = 0; i < 120 && !drove; i++) { Run(0.05); drove = sim.Agents.Any(a => a.Car && a.Phase == 33); }
        Check(drove, "after work they walk back to the car and drive off");
        Run(5);
        Check(sim.Parked.Count == 0 && !sim.Agents.Any(a => a.Desk != 0), "in the evening the office and the car park are empty");
        // weekends: nobody comes in
        s.Hours = (new DateTime(2027, 6, 19, 10, 0, 0) - GameState.Epoch).TotalHours; // a Saturday
        Check(people.All(p => !sim.OfficeHours(p.emp)), "nobody comes to the office at the weekend");
    }
}
