using System;
using System.Linq;
using DatacenterArchitect.Sim;

namespace DatacenterArchitect.Tests;

/// <summary>Playtest round 3: commuting, loading bays, the R&D budget and office interiors.</summary>
public static partial class TestRunner
{
    static void Playtest3Tests()
    {
        // ---------------- commuting ----------------
        var modes = Enumerable.Range(1, 400).Select(Commute.Preferred).ToList();
        var bikes = modes.Count(m => m == CommuteMode.Bike) / 400.0;
        Check(bikes > 0.38 && bikes < 0.58, $"about half the staff like to cycle ({bikes:0.00})");
        Check(modes.Count(m => m == CommuteMode.ElectricCar) > 40, "a share of the drivers have an electric car");
        Check(Commute.Preferred(17) == Commute.Preferred(17), "the same person always comes the same way");

        var s = Quiet();
        for (var i = 0; i < 40; i++) s.Staff.Add(new Employee { Id = 1000 + i, Role = StaffRole.Cleaner });
        var demoRack = s.Objects.First(o => o.Kind == ObjKind.BikeRack);
        Check(Commute.BikeCapacity(s) == Commute.RackPlaces, "a bike rack holds five bikes");
        var today = Commute.Today(s);
        Check(today.Values.Count(m => m == CommuteMode.Bike) == Commute.RackPlaces, "cyclists beyond the bike places come by car");
        var shed = s.AddObject(ObjKind.BikeShed, 17, 35, 0, 0);
        Check(shed != null && shed.Status == ObjStatus.Live, "a bike shed can be built outside");
        Check(Commute.BikeCapacity(s) == Commute.RackPlaces + Commute.ShedPlaces, "the shed adds ten places");
        Check(Commute.Today(s).Values.Count(m => m == CommuteMode.Bike) == Commute.RackPlaces + Commute.ShedPlaces, "more places, more cyclists");
        var sim = new Simulation(s);
        var cyclist = s.Staff.First(e => Commute.Preferred(e.Id) == CommuteMode.Bike);
        Check(sim.ModeOf(cyclist.Id) == CommuteMode.Bike, "a cyclist cycles when there is a bike rack");
        s.Objects.Remove(demoRack); s.Objects.Remove(shed); s.Touch();
        Check(sim.ModeOf(cyclist.Id) == CommuteMode.Car, "without bike places the cyclist drives");
        Check(s.AddObject(ObjKind.EvCharger, 43, 41, 3, 0) != null && Commute.Chargers(s).Count() == 1, "charging points can be built by the parking");

        // ---------------- loading bays ----------------
        var d = Quiet();
        var dsim = new Simulation(d);
        Check(dsim.Docks().Count == 1, "one roll-up door is one loading bay");
        Check(d.AddDoor(DoorType.RollUp, 31, 34, 1, 0) != null, "a second roll-up door fits in the dock wall");
        var docks = dsim.Docks();
        Check(docks.Count == 2 && docks.Select(x => x.apron).Distinct().Count() == 2, "two doors, two bays with their own apron");
        Check(Math.Abs(docks[0].apron.x - docks[1].apron.x) >= 3, "bays are a truck's width apart");

        // ---------------- R&D budget ----------------
        var r = Quiet();
        Unlock(r);
        r.Staff.Add(new Employee { Id = 77, Role = StaffRole.Researcher });
        var basePts = r.ResearchPerDay;
        Check(basePts > 0, "researchers make research points");
        r.SetResearchBudget(3);
        Check(Math.Abs(r.ResearchPerDay - basePts * ResearchTree.BudgetSpeed[3]) < 1e-9, "a bigger budget makes research faster");
        r.SetResearchBudget(99);
        Check(r.ResearchBudget == ResearchTree.BudgetPerDay.Length - 1, "the budget is capped at the top step");
        Check(ResearchTree.BudgetPerDay[0] == 0 && ResearchTree.BudgetSpeed[0] == 1, "the basic budget costs nothing extra");
        var back = System.Text.Json.JsonSerializer.Deserialize<GameState>(System.Text.Json.JsonSerializer.Serialize(r));
        Check(back.ResearchBudget == r.ResearchBudget, "the budget survives a save");

        // ---------------- office interiors ----------------
        var c = new CreativeOptions { UnlimitedMoney = true, InstantBuild = true, NoPowerLimit = true };
        var o = GameState.New(new NewGameOptions { Creative = true, CreativeOptions = c });
        Unlock(o);
        o.AddBuilding(BuildingKind.Office, 4, 4, 20, 12, 1);
        var office = o.AddRoom(RoomType.Office, 10, 4, 8, 5, 0);
        o.AddRoom(RoomType.Corridor, 4, 9, 20, 2, 0);
        var exec = o.AddRoom(RoomType.Executive, 4, 4, 6, 5, 0);
        Check(o.FurnitureIn(office) == 0, "a room without a door waits for its door before it is furnished");
        o.AddDoor(DoorType.Single, 13, 9, 1, 0);
        o.AddDoor(DoorType.Single, 6, 9, 1, 0);
        Check(o.Objects.Count(x => x.Kind == ObjKind.Desk && office.Contains(x.X, x.Z)) >= 2, "the designer puts desks in a new office");
        Check(o.Objects.Count(x => x.Kind == ObjKind.Plant && office.Contains(x.X, x.Z)) >= 1, "and plants");
        Check(o.ObjectAt(13, 8, 0) == null && o.ObjectAt(12, 8, 0) == null && o.ObjectAt(14, 8, 0) == null, "the doorway stays clear");
        Check(o.Offices >= 1, "a furnished director's office counts as an office for a director");
        Check(o.Workplaces >= 2, "desks in the office are workplaces");
        Check(o.StyleOf(exec) == InteriorStyle.Formal && o.StyleOf(office) == InteriorStyle.Standard, "directors get a formal room, the office a standard one");
        o.Staff.Add(new Employee { Id = 5, Role = StaffRole.Helpdesk });
        Check(o.StyleOf(office) == InteriorStyle.Tech, "with a helpdesk the first office becomes the tech support room");
        o.SetStyle(office, InteriorStyle.Formal);
        Check(o.StyleOf(office) == InteriorStyle.Formal, "the player can pick a style");
        var n = o.FurnitureIn(office);
        Check(o.AutoFurnish(office) == 0 && o.FurnitureIn(office) == n, "a furnished room is not furnished twice");
        var needs = o.OfficeNeeds;
        Check(needs.Any(x => x.key == "room_reception" && x.have == 0 && x.need == 1), "the checklist asks for a reception");
        Check(needs.Any(x => x.key == "need_workplaces" && x.have == o.Workplaces), "the checklist counts workplaces");
        var off = GameState.New(new NewGameOptions { Creative = true, CreativeOptions = c });
        off.AutoInterior = false;
        off.AddBuilding(BuildingKind.Office, 4, 4, 20, 12, 1);
        var bare = off.AddRoom(RoomType.Office, 10, 4, 8, 5, 0);
        off.AddDoor(DoorType.Single, 13, 9, 1, 0);
        Check(off.FurnitureIn(bare) == 0, "automatic furnishing can be switched off");
    }
}
