using System.Linq;
using DatacenterArchitect.Sim;

namespace DatacenterArchitect.Tests;

/// <summary>Playtest: put an extra storey on a building that is already finished.</summary>
public static partial class TestRunner
{
    static void AddLevelTests()
    {
        var c = new CreativeOptions { UnlimitedMoney = true, InstantBuild = false, NoPowerLimit = true };
        var s = GameState.New(new NewGameOptions { Creative = true, CreativeOptions = c });
        Unlock(s);
        var office = s.AddBuilding(BuildingKind.Office, 4, 4, 12, 10, 1);
        Check(s.CanAddLevel(office) == "msg_building_not_ready", "a building under construction cannot grow yet");
        office.Stage = Stage.Done;
        var roofUnit = s.AddObject(ObjKind.Chiller, 6, 6, 0, GameState.RoofFloor);
        Check(s.CanAddLevel(office) == null && GameState.LevelCost(office) == GameState.BuildingCost(BuildingKind.Office, 12, 10, 1), "a storey costs what a storey costs");
        Check(s.AddLevel(office) == null && office.Raising && office.Floors == 1, "the crane starts on the roof; the floor is not there yet");
        Check(s.CanAddLevel(office) == "msg_upgrade_busy", "one storey at a time");
        Check(s.CanRoom(RoomType.Office, 4, 4, 6, 5, 1) != null, "no rooms on the new floor while it is being built");
        s.UpdateInfra(GameState.LevelHours(office) / 2);
        Check(office.Raising && GameState.RaiseProgress(office) > 0.4 && GameState.RaiseProgress(office) < 0.6, "half way after half the time");
        s.UpdateInfra(GameState.LevelHours(office));
        Check(!office.Raising && office.Floors == 2 && office.HasFloor(1), "the second floor is finished");
        Check(s.Notes.Any(n => n.key == "msg_level_done"), "the player hears about it");
        Check(s.CanRoom(RoomType.Office, 4, 4, 6, 5, 1) == null, "rooms can go on the new floor");
        Check(roofUnit == null || s.Objects.Contains(roofUnit), "rooftop plant stays on the roof");
        // every kind of building can grow, up to the maximum
        var dc = s.AddBuilding(BuildingKind.Datacenter, 30, 4, 10, 10, Catalog.MaxFloors - 1);
        var wh = s.AddBuilding(BuildingKind.Warehouse, 4, 30, 8, 8, 1);
        dc.Stage = wh.Stage = Stage.Done;
        s.Options.CreativeOptions = new CreativeOptions { UnlimitedMoney = true, InstantBuild = true, NoPowerLimit = true };
        Check(s.AddLevel(wh) == null && wh.Floors == 2, "warehouses can grow too");
        Check(s.AddLevel(dc) == null && dc.Floors == Catalog.MaxFloors && s.CanAddLevel(dc) == "msg_max_floors", "up to the maximum number of floors");
        var back = System.Text.Json.JsonSerializer.Deserialize<GameState>(System.Text.Json.JsonSerializer.Serialize(s));
        Check(back.Buildings.First(b => b.Id == office.Id).Floors == 2, "the extra floor survives a save");
    }
}
