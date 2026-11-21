using System;
using System.Linq;
using DatacenterArchitect.Sim;

namespace DatacenterArchitect.Tests;

/// <summary>Rooms, walls and doors: doors changed on the spot, badge readers everywhere.</summary>
public static partial class TestRunner
{
    static void DoorSwapTests()
    {
        var s = GameState.New(new NewGameOptions { Difficulty = 0, AiCompetitors = false });
        var b = s.AddBuilding(BuildingKind.Datacenter, 4, 4, 20, 12, 1); b.Stage = Stage.Done;
        var hall = s.AddRoom(RoomType.DataHall, 4, 4, 12, 12, 0);
        var mmr = s.AddRoom(RoomType.MeetMe, 16, 4, 4, 6, 0);
        hall.Stage = mmr.Stage = Stage.Done;
        // every door but the mantrap takes a rack trolley; the badge readers count for security
        Check(Enum.GetValues<DoorType>().All(t => Catalog.Doors[t].Carry == (t != DoorType.Mantrap)), "racks fit through every door but the mantrap");
        Check(Catalog.Doors[DoorType.BadgeDouble].Width == 2 && Catalog.Doors[DoorType.BadgeDouble].Secure, "a double door with a badge reader exists");
        Check(GameState.Secure(DoorType.Badge) && GameState.Secure(DoorType.Fire) && !GameState.Secure(DoorType.Double) && !GameState.Secure(DoorType.Single), "only doors with a reader are secure");

        var hd = s.AddDoor(DoorType.Double, 16, 12, 0, 0);
        var md = s.AddDoor(DoorType.Single, 18, 10, 1, 0);
        Check(hd != null && md != null, "doors into the hall and the MMR");
        Check(s.DoorsOf(hall).Contains(hd) && s.DoorsOf(mmr).Contains(md), "the doors belong to their rooms");
        var access = s.SecurityLines().First(l => l.Key == "sec_access").Points;

        // swap options: same width or narrower, never a roll-up door
        var opts = s.DoorSwaps(hd).ToList();
        Check(opts.Contains(DoorType.BadgeDouble) && opts.Contains(DoorType.Single) && !opts.Contains(DoorType.RollUp), "a double door can get a reader or be narrowed");
        Check(!s.DoorSwaps(md).Contains(DoorType.Double) && !s.DoorSwaps(md).Contains(DoorType.BadgeDouble), "a single door cannot be widened on the spot");
        Check(s.CanSwapDoor(md, DoorType.Double) == "msg_door_swap_width", "widening is refused with a reason");
        Check(s.DoorSwapCost(hd, DoorType.BadgeDouble) == 3000 && s.DoorSwapCost(hd, DoorType.Single) == -200, "the difference is paid, a cheaper door gives a quarter back");

        // the bulk upgrade from the security screen
        var open = s.UnsecuredHallDoors();
        Check(open.Count == 2 && open.Any(x => x.door == hd && x.to == DoorType.BadgeDouble) && open.Any(x => x.door == md && x.to == DoorType.Badge), "the hall and MMR doors without a reader are listed");
        var cost = s.UnsecuredHallDoorsCost();
        Check(cost == 3000 + 2600, $"upgrading both costs the difference ({cost})");
        var money = s.Money;
        var id = hd.Id;
        Check(s.SecureHallDoors() == 2, "both doors get a reader in one go");
        Check(s.Money == money - cost, "and that is what it cost");
        var nd = s.Doors.First(d => d.Id == id);
        Check(nd.Type == DoorType.BadgeDouble && nd.X == 16 && nd.Z == 12 && nd.Dir == 0 && s.Doors.Count == 2, "the door stays on the same spot with the same id");
        Check(s.UnsecuredHallDoors().Count == 0, "nothing left to upgrade");
        Check(s.SecurityLines().First(l => l.Key == "sec_access").Points > access, "access control goes up");

        // undo puts the old door back
        var undo = new UndoHistory();
        var before = UndoHistory.Take(s);
        Check(s.SwapDoor(nd, DoorType.Double) == null, "a reader can be taken off again");
        undo.Commit(s, before, "door");
        undo.Undo(s);
        Check(s.Doors.Any(d => d.Id == id && d.Type == DoorType.BadgeDouble) && s.Doors.Count == 2, "undo brings the badge door back");

        // the mantrap is only offered after research
        var single = s.Doors.First(d => d.Id == md.Id);
        Check(s.CanSwapDoor(single, DoorType.Mantrap) == "msg_needs_research", "a mantrap needs research, also as a swap");

        // racks can be carried through a single door; narrow doors cost the carriers a little more
        var nav = new Nav(s);
        Check(nav.Find(new Pt(10, 8, 0), 0, (x, z) => x == 18 && z == 7, NavMode.Carry) != null, "a rack is wheeled through a single badge door");
    }

    /// <summary>Free walls inside a building, painted rooms, the bulldozer's choice and the room checklist.</summary>
    static void WallPaintTests()
    {
        var s = GameState.New(new NewGameOptions { Difficulty = 0, AiCompetitors = false });
        s.Money = 10_000_000;
        var b = s.AddBuilding(BuildingKind.Datacenter, 4, 4, 20, 12, 1); b.Stage = Stage.Done;
        void Build() { foreach (var w in s.Walls) w.Stage = Stage.Done; s.Touch(); s.RefreshRooms(); }
        Room Hall() => s.Rooms.First(r => r.Type == RoomType.DataHall);

        // a wall along x=14 splits the building in two spaces
        var money = s.Money;
        var walls = s.AddWalls(0, 14, 4, 14, 16);
        Check(walls.Count == 1 && walls[0].Len == 12 && walls[0].Stage == Stage.Walls, "a drag puts up one wall of 12 m, still to be built");
        Check(s.Money == money - GameState.WallCost(12, false), "a wall costs per metre");
        Check(s.CanWalls(0, GameState.WallLine(14, 4, 14, 16), out _) == "msg_nothing_changed", "the same wall twice changes nothing");
        Check(s.CanWalls(0, GameState.WallLine(30, 4, 30, 8), out _) == "msg_wall_in_building", "walls only go inside a building");

        // painting gives the space a function; it works once it is closed and has a door
        Check(s.PaintRoom(RoomType.DataHall, 4, 4, 10, 12, 0) == null, "paint a data hall");
        var hall = Hall();
        Check(!hall.Walled && hall.Area == 120 && hall.Stage == Stage.Walls, "the painted hall has 120 m² and does not work yet");
        Check(s.RoomIssue(hall) == "room_issue_building", "its wall is still being built");
        Build();
        Check(s.RoomIssue(Hall()) == "room_issue_no_door", "then it still needs a door");
        Check(s.AddDoor(DoorType.Single, 14, 8, 0, 0) != null, "a door in the new wall");
        s.Touch(); s.RefreshRooms();
        Check(Hall().Stage == Stage.Done && s.RoomIssue(Hall()) == null, "closed with a door: the hall works");

        // painting over a room with another type changes the whole room
        var id = Hall().Id;
        Check(s.PaintRoom(RoomType.Storage, 5, 5, 1, 1, 0) == null, "paint storage over the hall");
        Check(s.Rooms.Count == 1 && s.Rooms[0].Id == id && s.Rooms[0].Type == RoomType.Storage && s.Rooms[0].Area == 120, "the whole room became storage");
        s.PaintRoom(RoomType.DataHall, 5, 5, 1, 1, 0);
        Check(Hall().Id == id, "and back into a hall");

        // fill paints the whole space with one click
        Check(s.PaintRoom(RoomType.Office, 20, 10, 1, 1, 0, fill: true) == null, "fill the other space");
        var office = s.Rooms.First(r => r.Type == RoomType.Office);
        Check(office.Area == 120 && s.RoomIssue(office) == null, "the office fills its space and shares the door");

        // a new wall cuts the hall in two rooms, taking it away joins them again
        s.AddWalls(0, 4, 10, 14, 10);
        Build();
        var halls = s.Rooms.Where(r => r.Type == RoomType.DataHall).ToList();
        Check(halls.Count == 2 && halls.All(h => h.Area == 60), "a wall through the hall makes two halls");
        Check(halls.Count(h => s.RoomIssue(h) == "room_issue_no_door") == 1, "the half without a door does not work");
        money = s.Money;
        Check(s.Erase(EraseKind.Wall, 4, 10, 10, 0, 0) == null, "demolish only the wall");
        Check(s.Money == money + GameState.WallCost(10, false) / 4, "a quarter of the wall comes back");
        s.RefreshRooms();
        Check(s.Rooms.Count(r => r.Type == RoomType.DataHall) == 1 && Hall().Area == 120 && Hall().Id == id, "the two halls are one again");

        // the eraser of the paint tool only removes the type
        Check(s.UnpaintRoom(4, 4, 2, 12, 0) == null && Hall().Area == 96, "unpainting two columns leaves 96 m²");
        Check(s.Walls.Any(w => w.X == 14 && w.Dir == 0), "walls stay");

        // the checklist of a hall
        var list = s.RoomChecklist(Hall());
        Check(list.Select(n => n.Key).SequenceEqual(new[] { "need_closed", "obj_rack", "obj_crah", "need_pdu", "need_hall_switch" }), "a hall needs closing, a rack, a CRAH, a PDU and a hall switch");
        Check(list[0].Ok && !list[1].Ok && !s.RoomComplete(Hall()), "closed, but not complete yet");
        var nb = Hall().Copy(); nb.Busbar = true; s.Rooms[s.Rooms.IndexOf(Hall())] = nb;
        Check(s.RoomChecklist(Hall()).First(n => n.Key == "need_pdu").Ok, "a busbar counts for the PDU");
        Check(s.RoomBadges(Hall()) == (0, 1), "the badge advice counts the hall door");

        // the bulldozer: doors only, then a whole room with its own walls
        Check(s.Erase(EraseKind.Door, 14, 8, 1, 1, 0) == null && s.Doors.Count == 0, "demolish only the door");
        s.RefreshRooms();
        Check(s.RoomIssue(Hall()) == "room_issue_no_door", "without its door the hall stops working");
        s.AddDoor(DoorType.Single, 14, 8, 0, 0);
        s.AddWalls(0, 18, 4, 18, 16);
        Build();
        var east = s.Rooms.Where(r => r.Type == RoomType.Office).ToList();
        Check(east.Count == 2, "a wall through the office makes two offices");
        var far = east.First(r => r.Contains(20, 10));
        Check(s.Erase(EraseKind.Room, 20, 10, 1, 1, 0) == null && !s.Rooms.Any(r => r.Id == far.Id), "demolish a room");
        Check(s.Walls.Any(w => w.X == 18 && w.Dir == 0), "a wall next to another room stays");
        Check(s.Erase(EraseKind.Room, 15, 10, 1, 1, 0) == null, "demolish the other office too");
        Check(!s.Walls.Any(w => w.X == 18 && w.Dir == 0) && s.Walls.Any(w => w.X == 14 && w.Dir == 0), "its own wall went, the wall to the hall stayed");

        // undo takes a wall back down
        var undo = new UndoHistory();
        var before = UndoHistory.Take(s);
        var n0 = s.Walls.Count;
        s.AddWalls(0, 4, 12, 10, 12);
        undo.Commit(s, before, "wall");
        undo.Undo(s);
        Check(s.Walls.Count == n0, "undo removes the new wall");

        // a room drawn with walls (the old way) turns into separate walls when one of its walls is touched
        var b2 = s.AddBuilding(BuildingKind.Datacenter, 30, 4, 12, 10, 1); b2.Stage = Stage.Done;
        var mmr = s.AddRoom(RoomType.MeetMe, 30, 4, 6, 6, 0); mmr.Stage = Stage.Done;
        s.AddDoor(DoorType.Single, 36, 6, 0, 0);
        var plain = s.Unwall(mmr);
        s.RefreshRooms();
        Check(!plain.Walled && plain.Area == 36 && s.Walls.Count(w => w.BuildingId == b2.Id) == 2, "an old room becomes a painted room inside two walls");
        Check(s.Rooms.First(r => r.Id == mmr.Id).Stage == Stage.Done, "and keeps working");
        Check(s.RoomChecklist(plain).Select(n => n.Key).SequenceEqual(new[] { "need_closed", "obj_odf", "obj_core_router" }), "an MMR needs an ODF and a core router");
    }
}
