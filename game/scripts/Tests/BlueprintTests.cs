using System.Linq;
using DatacenterArchitect.Core;
using DatacenterArchitect.Sim;

namespace DatacenterArchitect.Tests;

/// <summary>Infra sketch board 8: save a hall as a blueprint and place copies, turned and mirrored.</summary>
public static partial class TestRunner
{
    /// <summary>A finished 48 × 24 datacenter with one walled 12 × 10 hall full of kit in its corner.</summary>
    static (GameState s, Room hall) BlueprintSite()
    {
        var s = GameState.New(new NewGameOptions { Difficulty = 0 });
        s.Money = 50_000_000;
        var b = s.AddBuilding(BuildingKind.Datacenter, 4, 4, 48, 24, 1);
        b.Stage = Stage.Done;
        var hall = s.AddRoom(RoomType.DataHall, 4, 4, 12, 10, 0);
        hall.Stage = Stage.Done;
        hall.Containment = Containment.HotCold; hall.Busbar = true;
        for (var x = 6; x < 12; x++) { s.AddObject(ObjKind.Rack, x, 6, 0, 0); s.AddObject(ObjKind.Rack, x, 8, 2, 0); }
        s.AddObject(ObjKind.Pdu, 14, 5, 0, 0);
        s.AddObject(ObjKind.Pdu, 14, 9, 0, 0);
        s.AddObject(ObjKind.Crah, 5, 11, 0, 0);
        s.AddObject(ObjKind.EdgeSwitch24, 13, 12, 0, 0);
        s.AddDoor(DoorType.Badge, 16, 10, 0, 0);
        return (s, hall);
    }

    static void BlueprintTests()
    {
        var (s, hall) = BlueprintSite();
        var kit = s.Objects.Count;
        Check(kit == 16, $"the hall holds 12 racks, 2 PDUs, a CRAH and an edge switch ({kit})");
        var pdus = s.Objects.Where(o => Catalog.IsPdu(o.Kind)).ToList();
        Check(pdus.Select(p => p.Feed).Distinct().Count() == 2, "the two PDUs are on side A and side B");

        // saving keeps the design, not the tenants
        var bp = s.CaptureBlueprint(hall, "Colo 12");
        Check(bp.Valid() && bp.Walled && bp.W == 12 && bp.D == 10 && bp.Racks == 12 && bp.Objects.Count == 16, "the blueprint has the hall's size and all its kit");
        Check(bp.Doors.Count == 1 && bp.Doors[0].Type == DoorType.Badge && bp.Walls.Count == 0, "the badge door comes along; a walled hall brings its own walls");
        Check(bp.Containment == Containment.HotCold && bp.Busbar, "containment and busbar are part of the design");

        // a copy next to it: the same hall, paid at once
        var fit = s.FitBlueprint(bp, 20, 4, 0);
        Check(fit.Why == null && fit.Cost == Catalog.RoomCost(12, 10) + fit.DoorCost + fit.ObjectCost + GameState.FitOutCost(120, Containment.HotCold, true), $"the copy fits and its price adds up ({fit.Why})");
        var money = s.Money;
        var before = UndoHistory.Take(s);
        var res = s.PlaceBlueprint(bp, 20, 4, 0);
        var history = new UndoHistory();
        Check(history.Commit(s, before, "bp_tool"), "placing a blueprint is one undo step");
        Check(res.Why == null && res.Skipped == 0 && res.Objects == 16 && res.Doors == 1, $"every part of the copy is placed ({res.Why}, skipped {res.Skipped})");
        Check(money - s.Money == fit.Cost && res.Cost == fit.Cost, $"the copy costs what the ghost said ({money - s.Money} vs {fit.Cost})");
        var copy = res.Room;
        Check(copy != null && copy.Type == RoomType.DataHall && copy.Walled && copy.X == 20 && copy.Containment == Containment.HotCold && copy.Busbar, "the copy is a walled data hall with the same fit-out");
        Check(copy.Stage == Stage.Walls && s.Objects.Count(o => o.Status == ObjStatus.Ordered && copy.Contains(o.X, o.Z)) == 16, "the crews still build it: walls go up, the kit is ordered");
        var copyPdus = s.Objects.Where(o => Catalog.IsPdu(o.Kind) && copy.Contains(o.X, o.Z)).OrderBy(o => o.Z).ToList();
        Check(copyPdus.Count == 2 && copyPdus[0].Feed == pdus.OrderBy(o => o.Z).First().Feed && copyPdus[1].Feed != copyPdus[0].Feed, "PDUs keep their A and B sides");
        Check(s.Objects.Count(o => o.Kind == ObjKind.Rack && copy.Contains(o.X, o.Z) && o.Rot == 2) == 6, "racks keep facing their aisles");

        // not on top of another room, not outside the building
        Check(s.FitBlueprint(bp, 24, 6, 0).Why == "msg_bp_room_in_way", "a copy cannot overlap a room");
        Check(s.FitBlueprint(bp, 46, 4, 0).Why == "msg_bp_in_building", "a copy must lie inside the building");
        Check(s.FitBlueprint(bp, 20, 4, 1).Why == "msg_bp_in_building", "a single-storey building has no first floor");

        // undo takes the whole copy away and gives the money back
        history.Undo(s);
        Check(s.Rooms.All(r => r.Id != copy.Id) && s.Objects.Count == kit && s.Money == money, "undo removes the copy and refunds it");
        history.Redo(s);

        // R turns a quarter, M mirrors; four turns or two mirrors give the same blueprint back
        var turned = bp.Oriented(1, false);
        Check(turned.W == 10 && turned.D == 12, "a quarter turn swaps width and depth");
        Check(turned.Objects.Count(o => o.Kind == ObjKind.Rack && o.Rot == 1) == 6 && turned.Objects.Count(o => o.Kind == ObjKind.Rack && o.Rot == 3) == 6, "turned racks face the turned aisle");
        string Sig(Blueprint b) => string.Join(";", b.Objects.Select(o => $"{o.Kind}{o.X},{o.Z},{o.Rot}").OrderBy(x => x)) + "|" + string.Join(";", b.Doors.Select(d => $"{d.X},{d.Z},{d.Dir}")) + $"|{b.W}x{b.D}";
        Check(Sig(bp.Oriented(4, false)) == Sig(bp) && Sig(bp.Oriented(0, true).Oriented(0, true)) == Sig(bp), "four turns or two mirrors are the original");
        Check(Sig(bp.Oriented(2, false)) == Sig(bp.Oriented(1, false).Oriented(1, false)), "two quarter turns are a half turn");
        var mirrored = bp.Oriented(0, true);
        Check(mirrored.Doors[0].X == 0 && mirrored.Doors[0].Dir == 0, "mirroring moves the door to the other side wall");
        var r2 = s.PlaceBlueprint(turned, 34, 4, 0);
        Check(r2.Why == null && r2.Skipped == 0 && r2.Objects == 16 && r2.Doors == 1, $"a turned copy places every part ({r2.Why}, skipped {r2.Skipped})");
        var r3 = s.PlaceBlueprint(bp.Oriented(3, true), 34, 16, 0);
        Check(r3.Why == null && r3.Skipped == 0 && r3.Objects == 16, $"a turned and mirrored copy places every part ({r3.Why}, skipped {r3.Skipped})");

        // plan mode: the copy goes in the plan, the plan costs what the copy would
        var (p, phall) = BlueprintSite();
        var pbp = p.CaptureBlueprint(phall, "x");
        var pfit = p.FitBlueprint(pbp, 20, 4, 0);
        var pmoney = p.Money;
        var ids = p.LayoutIds();
        p.Planning = true;
        p.PlaceBlueprint(pbp, 20, 4, 0);
        p.Planning = false;
        p.MarkPlanned(ids);
        Check(p.Money == pmoney && p.PlanCost == pfit.Cost, $"a planned copy is free until built and the plan costs the same ({p.PlanCost} vs {pfit.Cost})");
        Check(p.BuildPlan() == null && pmoney - p.Money == pfit.Cost, "building the plan pays for the copy");

        BlueprintPaintedTests();
        BlueprintLibraryTests(bp);
    }

    /// <summary>A hall painted inside separate walls, in the corner of the building (two sides are the outside walls).</summary>
    static void BlueprintPaintedTests()
    {
        var s = GameState.New(new NewGameOptions { Difficulty = 0 });
        s.Money = 50_000_000;
        var b = s.AddBuilding(BuildingKind.Datacenter, 4, 4, 48, 24, 1);
        b.Stage = Stage.Done;
        s.AddWalls(0, 12, 4, 12, 10);
        s.AddWalls(0, 4, 10, 12, 10, glass: true);
        foreach (var w in s.Walls) w.Stage = Stage.Done;
        s.AddDoor(DoorType.Single, 12, 6, 0, 0);
        Check(s.PaintRoom(RoomType.DataHall, 4, 4, 8, 6, 0) == null, "paint the space as a data hall");
        var hall = s.RoomAt(5, 5, 0);
        Check(hall != null && !hall.Walled && hall.Area == 48, "the painted hall fills the space");
        s.AddObject(ObjKind.Rack, 6, 6, 0, 0);
        var bp = s.CaptureBlueprint(hall, "Painted");
        Check(!bp.Walled && bp.Walls.Count == 28 && bp.Walls.Count(e => e.Glass) == 8 && bp.Doors.Count == 1 && bp.Racks == 1, $"a painted hall keeps its whole boundary, glass included ({bp.Walls.Count} edges)");
        // in the middle of the building every side needs a wall; along the outside wall one side is there already
        var mid = s.FitBlueprint(bp, 24, 12, 0);
        Check(mid.Why == null && mid.NewWalls.Count == 28, $"in the open floor all 28 m of wall are new ({mid.NewWalls.Count})");
        var edge = s.FitBlueprint(bp, 20, 4, 0);
        Check(edge.Why == null && edge.NewWalls.Count == 20, $"against the outside wall that side needs no wall ({edge.NewWalls.Count})");
        var res = s.PlaceBlueprint(bp, 24, 12, 0);
        var copy = s.RoomAt(25, 13, 0);
        Check(res.Why == null && copy != null && copy.Type == RoomType.DataHall && !copy.Walled && copy.Area == 48, "the copy is a painted data hall of the same shape");
        Check(s.Walls.Where(w => w.Glass).Sum(w => w.Len) == 16 && res.Doors == 1 && res.Objects == 1, "the glass wall, the door and the rack come along");
        // an existing wall through the spot would cut the copy in two
        s.AddWalls(0, 40, 12, 40, 20);
        Check(s.FitBlueprint(bp, 36, 12, 0).Why == "msg_bp_wall_in_way", "a wall through the spot blocks the copy");
    }

    static void BlueprintLibraryTests(Blueprint bp)
    {
        var lib = BlueprintLibrary.InMemory();
        Check(lib.Add(bp.Copy()) == null && lib.Add(bp.Copy()) == null && lib.All.Count == 2 && lib.All[0].Id != lib.All[1].Id, "the library keeps blueprints with their own id");
        Check(lib.UniqueName("Colo 12") == "Colo 12 (2)", "a new name does not repeat an old one");
        var text = lib.Text();
        var back = BlueprintLibrary.InMemory();
        Check(back.Read(text) == 2 && back.All[0].Objects.Count == 16 && back.All[0].Name == "Colo 12" && back.All[0].Busbar, "the library survives a save and load");
        Check(text.Contains("\"Rack\""), "kinds are stored by name, so a later version keeps them apart");
        Check(back.Read("{ not json") == 0, "a broken library file is skipped");
        var huge = bp.Copy(); huge.W = 999;
        var bad = BlueprintLibrary.InMemory();
        Check(bad.Add(huge) == "msg_bp_invalid", "a blueprint with impossible numbers is refused");
        var odd = bp.Copy(); odd.Objects[0].X = -3;
        Check(!odd.Valid(), "parts outside the blueprint are refused");
        lib.Rename(lib.All[0], "  Private 24  ");
        Check(lib.All[0].Name == "Private 24", "rename trims the name");
        lib.Remove(lib.All[0]);
        Check(lib.All.Count == 1, "delete removes it");
    }
}
