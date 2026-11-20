using System.Linq;
using DatacenterArchitect.Sim;

namespace DatacenterArchitect.Tests;

/// <summary>Basements under datacenters and offices: a design choice for the power plant (transformers, switchgear, UPS).</summary>
public static partial class TestRunner
{
    static void BasementTests()
    {
        var c = new CreativeOptions { UnlimitedMoney = true, InstantBuild = true, NoPowerLimit = true };
        var s = GameState.New(new NewGameOptions { Creative = true, CreativeOptions = c });
        Unlock(s);
        const int B = GameState.BasementFloor;
        Check(GameState.Level(B) == -1 && GameState.Level(0) == 0 && GameState.Level(2) == 2, "the basement is the level below the ground floor");
        Check(GameState.BuildingCost(BuildingKind.Datacenter, 20, 12, 1, true) ==
              GameState.BuildingCost(BuildingKind.Datacenter, 20, 12, 1) + GameState.BasementCost(BuildingKind.Datacenter, 20, 12) &&
              GameState.BasementCost(BuildingKind.Datacenter, 20, 12) > 0, "a basement costs extra");
        Check(s.CanBuild(BuildingKind.Warehouse, 40, 4, 10, 10, 1, true) == "msg_no_basement", "warehouses have no basement");
        var b = s.AddBuilding(BuildingKind.Datacenter, 4, 20, 20, 12, 1, null, true);
        Check(b != null && b.Basement && b.HasFloor(B) && b.HasFloor(0) && !b.HasFloor(1) && b.Levels == 2, "a datacenter can be built with a basement");
        var plain = s.AddBuilding(BuildingKind.Datacenter, 30, 20, 10, 10, 1);
        Check(plain != null && !plain.HasFloor(B), "without the option there is no basement");
        Check(s.CanRoom(RoomType.Transformer, 32, 22, 4, 4, B) != null, "no basement rooms in a building without a basement");

        var trafo = s.AddRoom(RoomType.Transformer, 14, 24, 6, 6, B);
        Check(trafo != null && trafo.Floor == B, "a transformer room fits in the basement");
        Check(s.CanRoom(RoomType.LoadingDock, 6, 24, 6, 6, B) == "msg_not_in_basement", "the loading dock cannot go underground");
        Check(s.AddObject(ObjKind.Transformer, 15, 25, 0, B) != null, "transformers can stand in the basement");
        Check(s.AddDoor(DoorType.Single, 4, 24, 1, B) == null, "no outer doors underground");
        Check(s.AddDoor(DoorType.Double, 14, 26, 0, B) != null, "the basement room gets a door");
        Check(s.AddObject(ObjKind.Stairs, 6, 26, 0, 0) != null, "stairs lead down into the basement");

        var nav = new Nav(s);
        Check(nav.Walkable(10, 28, B), "the basement floor is walkable");
        var path = nav.Find(new Pt(10, 22, 0), B, (x, z) => x == 16 && z == 28, NavMode.Walk);
        Check(path != null && path[^1].F == B, "staff walk down the stairs to the transformer room");
        Check(nav.Find(new Pt(10, 22, 0), B, (x, z) => x == 16 && z == 28, NavMode.Carry) == null, "heavy loads need a freight lift to go down");
        Check(s.AddObject(ObjKind.Lift, 10, 21, 0, 0) != null, "a freight lift serves the basement");
        nav = new Nav(s);
        Check(nav.Find(new Pt(9, 26, 0), B, (x, z) => x == 16 && z == 28, NavMode.Carry) != null, "with a freight lift equipment can go down");

        var back = System.Text.Json.JsonSerializer.Deserialize<GameState>(System.Text.Json.JsonSerializer.Serialize(s));
        var bb = back.Buildings.First(x => x.Id == b.Id);
        Check(bb.Basement && back.Rooms.Any(r => r.Floor == B && r.Type == RoomType.Transformer), "the basement survives a save");
    }
}
