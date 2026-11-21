using System;
using System.Collections.Generic;
using System.Linq;

namespace DatacenterArchitect.Sim;

/// <summary>
/// Doors changed in place: a door gets a badge reader, becomes a fire door or is narrowed without being removed
/// first. The swap is instant (no builders, no hole in the wall); the player pays the difference in price, and a
/// cheaper door gives a quarter of the difference back, as demolishing does.
/// </summary>
public partial class GameState
{
    /// <summary>The door types this door can be changed into on the spot: same width or narrower, never a roll-up door.</summary>
    public IEnumerable<DoorType> DoorSwaps(Door d)
    {
        var w = Catalog.Doors[d.Type].Width;
        if (d.Type == DoorType.RollUp) yield break;
        foreach (var t in Enum.GetValues<DoorType>())
        {
            if (t == d.Type || t == DoorType.RollUp) continue;
            if (Catalog.Doors[t].Width > w) continue;
            yield return t;
        }
    }

    /// <summary>What the swap costs: positive is paid, negative comes back.</summary>
    public long DoorSwapCost(Door d, DoorType t)
    {
        if (IsPlanned(d.Id)) return 0;
        var diff = Catalog.Doors[t].Cost - Catalog.Doors[d.Type].Cost;
        return diff >= 0 ? diff : diff / 4;
    }

    public string CanSwapDoor(Door d, DoorType t)
    {
        if (d == null || !Doors.Contains(d)) return "msg_nothing_changed";
        if (!DoorSwaps(d).Contains(t)) return "msg_door_swap_width";
        if (t == DoorType.Mantrap && !Done("sec_mantrap")) return "msg_needs_research";
        var cost = DoorSwapCost(d, t);
        if (cost > 0 && !CanAfford(cost)) return "msg_no_money";
        return null;
    }

    /// <summary>Change a door into another type on the same spot. Returns null when done, else the reason.
    /// The door object is replaced (same id and place) so undo can put the old one back.</summary>
    public string SwapDoor(Door d, DoorType t)
    {
        var why = CanSwapDoor(d, t);
        if (why != null) return why;
        var cost = DoorSwapCost(d, t);
        if (cost > 0) Spend(cost);
        else if (cost < 0) Refund(-cost);
        var i = Doors.IndexOf(d);
        Doors[i] = new Door { Id = d.Id, Floor = d.Floor, X = d.X, Z = d.Z, Dir = d.Dir, Type = t };
        Touch();
        return null;
    }

    /// <summary>The badge-reader version of a door of this width (null for a roll-up door).</summary>
    public static DoorType? BadgeVersion(Door d) => Catalog.Doors[d.Type].Width switch
    {
        1 => DoorType.Badge,
        2 => DoorType.BadgeDouble,
        _ => null,
    };

    /// <summary>Doors of data halls and meet-me rooms that still have no badge reader, with the type each becomes.</summary>
    public List<(Door door, DoorType to)> UnsecuredHallDoors()
    {
        var list = new List<(Door, DoorType)>();
        foreach (var r in SecureRooms)
            foreach (var d in DoorsOf(r))
                if (!Secure(d.Type) && BadgeVersion(d) is { } to && !list.Any(x => x.Item1 == d))
                    list.Add((d, to));
        return list;
    }

    public long UnsecuredHallDoorsCost() => UnsecuredHallDoors().Sum(x => DoorSwapCost(x.door, x.to));

    /// <summary>Give every hall and MMR door a badge reader in one go. Returns how many doors were upgraded.</summary>
    public int SecureHallDoors()
    {
        var n = 0;
        foreach (var (d, to) in UnsecuredHallDoors())
            if (SwapDoor(d, to) == null) n++;
        return n;
    }
}
