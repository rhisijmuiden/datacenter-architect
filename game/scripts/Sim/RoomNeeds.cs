using System;
using System.Collections.Generic;
using System.Linq;

namespace DatacenterArchitect.Sim;

/// <summary>A line of a room's checklist: what it needs, how many there are and how many it needs at least.</summary>
public record RoomNeed(string Key, int Have, int Need)
{
    public bool Ok => Have >= Need;
}

/// <summary>
/// What each room function needs at least (Reinder, 2026-11-21): you paint a room type, and the room shows a
/// checklist of the equipment that must go in before it does its job.
/// </summary>
public partial class GameState
{
    /// <summary>Equipment a room type needs at least: groups of object kinds that count for one line, with the minimum.</summary>
    public static readonly Dictionary<RoomType, (string key, ObjKind[] kinds, int min)[]> RoomMinimum = new()
    {
        [RoomType.DataHall] = new[]
        {
            ("obj_rack", new[] { ObjKind.Rack }, 1),
            ("obj_crah", new[] { ObjKind.Crah }, 1),
            ("need_pdu", new[] { ObjKind.Pdu75, ObjKind.Pdu, ObjKind.Pdu300 }, 1),
            ("need_hall_switch", new[] { ObjKind.EdgeSwitch24, ObjKind.EdgeSwitch48, ObjKind.Spine }, 1),
        },
        [RoomType.MeetMe] = new[] { ("obj_odf", new[] { ObjKind.Odf }, 1), ("obj_core_router", new[] { ObjKind.CoreRouter }, 1) },
        [RoomType.UpsRoom] = new[] { ("obj_ups", new[] { ObjKind.Ups }, 1), ("obj_battery", new[] { ObjKind.Battery }, 1) },
        [RoomType.Battery] = new[] { ("obj_battery", new[] { ObjKind.Battery }, 1) },
        [RoomType.Switchgear] = new[] { ("obj_switchgear", new[] { ObjKind.Switchgear }, 1) },
        [RoomType.Transformer] = new[] { ("obj_transformer", new[] { ObjKind.Transformer }, 1) },
        [RoomType.Noc] = new[] { ("obj_noc_wall", new[] { ObjKind.NocWall }, 1), ("obj_desk", new[] { ObjKind.Desk }, 1) },
        [RoomType.Soc] = new[] { ("obj_soc_desk", new[] { ObjKind.SocDesk }, 1) },
        [RoomType.Lab] = new[] { ("obj_lab_bench", new[] { ObjKind.LabBench }, 1) },
        [RoomType.Security] = new[] { ("obj_security_wall", new[] { ObjKind.SecurityWall }, 1) },
        [RoomType.Reception] = new[] { ("obj_desk", new[] { ObjKind.Desk }, 1) },
        [RoomType.Office] = new[] { ("obj_desk", new[] { ObjKind.Desk }, 1), ("obj_chair", new[] { ObjKind.Chair }, 1) },
        [RoomType.Executive] = new[] { ("obj_desk", new[] { ObjKind.Desk }, 1), ("obj_chair", new[] { ObjKind.Chair }, 1) },
        [RoomType.Meeting] = new[] { ("obj_meeting_table", new[] { ObjKind.MeetingTable }, 1) },
        [RoomType.Canteen] = new[] { ("need_kitchen", new[] { ObjKind.Coffee, ObjKind.Vending, ObjKind.Microwave }, 1) },
        [RoomType.Lounge] = new[] { ("obj_sofa", new[] { ObjKind.Sofa }, 1) },
        [RoomType.Storage] = new[] { ("obj_pallet_rack", new[] { ObjKind.PalletRack }, 1) },
        [RoomType.Warehouse] = new[] { ("obj_pallet_rack", new[] { ObjKind.PalletRack }, 1) },
    };

    /// <summary>
    /// The checklist of a room: closed with a door (painted rooms), the equipment its function needs, and for
    /// data halls and meet-me rooms badge readers on the doors. The last line is never required, only advised.
    /// </summary>
    public List<RoomNeed> RoomChecklist(Room r)
    {
        var list = new List<RoomNeed>();
        if (!r.Walled) list.Add(new RoomNeed("need_closed", RoomIssue(r) == null ? 1 : 0, 1));
        else list.Add(new RoomNeed("need_door", DoorsOf(r).Any() ? 1 : 0, 1));
        if (r.Type == RoomType.LoadingDock)
            list.Add(new RoomNeed("door_rollup", DoorsOf(r).Count(d => d.Type == DoorType.RollUp && d.Edges().Any(IsOuterEdge)), 1));
        if (RoomMinimum.TryGetValue(r.Type, out var mins))
            foreach (var (key, kinds, min) in mins)
            {
                var have = Objects.Count(o => o.Floor == r.Floor && kinds.Contains(o.Kind) && r.Contains(o.X, o.Z));
                // a busbar feeds a hall without PDUs
                if (key == "need_pdu" && r.Busbar) have = Math.Max(have, 1);
                list.Add(new RoomNeed(key, have, min));
            }
        return list;
    }

    /// <summary>Whether everything on the room's checklist is there.</summary>
    public bool RoomComplete(Room r) => RoomChecklist(r).All(n => n.Ok);

    /// <summary>Advice under the checklist of halls and meet-me rooms: are all doors behind a badge reader?</summary>
    public (int secured, int doors) RoomBadges(Room r)
    {
        var ds = DoorsOf(r).ToList();
        return (ds.Count(d => Secure(d.Type)), ds.Count);
    }
}
