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
}
