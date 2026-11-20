using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace DatacenterArchitect.Sim;

/// <summary>How an office room is finished: paint, floor, art and how much tech is on the desks.</summary>
public enum InteriorStyle { Auto, Standard, Tech, Formal }

/// <summary>
/// Office interiors (playtest): when an office room is finished the interior designer furnishes it by itself
/// (desks, chairs, plants, cabinets, tables, sofas, coffee) and picks the paint, floor and art for its style:
/// a support or helpdesk office gets screens and a dark tech look, directors' offices are sleek and formal.
/// The player can switch the style or leave it on automatic. The office checklist shows which rooms the company
/// needs. No micromanagement: furnishing happens without the player placing a single chair.
/// </summary>
public partial class GameState
{
    /// <summary>Furnish office rooms automatically when they are finished (on by default).</summary>
    public bool AutoInterior { get; set; } = true;

    public static bool Furnishable(RoomType t) => t is RoomType.Office or RoomType.Executive or RoomType.Meeting
        or RoomType.Canteen or RoomType.Lounge or RoomType.Reception;

    /// <summary>The style a room shows: its own choice, or the automatic one for its type.</summary>
    public InteriorStyle StyleOf(Room r)
    {
        if (r.Style != InteriorStyle.Auto) return r.Style;
        return r.Type switch
        {
            RoomType.Executive => InteriorStyle.Formal,
            RoomType.Noc or RoomType.Soc or RoomType.Lab => InteriorStyle.Tech,
            // the first office becomes the support desk once there are support or helpdesk people
            RoomType.Office when Staff.Any(e => e.Role is StaffRole.Helpdesk or StaffRole.Support) &&
                Rooms.Where(x => x.Type == RoomType.Office && x.Stage == Stage.Done).OrderBy(x => x.Id).FirstOrDefault() == r => InteriorStyle.Tech,
            RoomType.Meeting when Rooms.Any(x => x.Type == RoomType.Executive && x.BuildingId == r.BuildingId && x.Floor == r.Floor) => InteriorStyle.Formal,
            _ => InteriorStyle.Standard,
        };
    }

    public void SetStyle(Room r, InteriorStyle s) { r.Style = s; Touch(); }

    /// <summary>Called when the builders finish a room.</summary>
    public void RoomFinished(Room r)
    {
        if (AutoInterior && Furnishable(r.Type) && !IsPlanned(r)) AutoFurnish(r);
    }

    static readonly ObjKind[] Furniture = { ObjKind.Desk, ObjKind.Chair, ObjKind.Plant, ObjKind.Cabinet, ObjKind.MeetingTable, ObjKind.Vending, ObjKind.Microwave, ObjKind.Coffee, ObjKind.Sofa };

    public int FurnitureIn(Room r) => Objects.Count(o => Furniture.Contains(o.Kind) && o.Floor == r.Floor && r.Contains(o.X, o.Z));

    /// <summary>
    /// Furnish an empty office room in a layout that suits its type, keeping the doorways and a walkway clear.
    /// Each piece is paid for as if the player placed it. Returns how many pieces were placed.
    /// </summary>
    public int AutoFurnish(Room r)
    {
        if (!Furnishable(r.Type) || r.Stage != Stage.Done || FurnitureIn(r) > 0) return 0;
        // keep the cells by each door free, and the cells next to those
        var keep = new HashSet<(int, int)>();
        foreach (var d in DoorsOf(r))
            foreach (var e in d.Edges())
                foreach (var c in e.dir == 0 ? new[] { (e.x - 1, e.z), (e.x, e.z) } : new[] { (e.x, e.z - 1), (e.x, e.z) })
                    if (r.Contains(c.Item1, c.Item2))
                        foreach (var (dx, dz) in new[] { (0, 0), (1, 0), (-1, 0), (0, 1), (0, -1) }) keep.Add((c.Item1 + dx, c.Item2 + dz));
        var placed = 0;
        bool Put(ObjKind k, int x, int z, int rot)
        {
            var o = new PlacedObject { Kind = k, X = x, Z = z, Rot = rot, Floor = r.Floor };
            if (Cells(o).Any(c => keep.Contains(c) || !r.Contains(c.Item1, c.Item2))) return false;
            if (AddObject(k, x, z, rot, r.Floor) == null) return false;
            placed++;
            return true;
        }
        int x0 = r.X, z0 = r.Z, x1 = r.X + r.W - 1, z1 = r.Z + r.D - 1, cx = r.X + r.W / 2, cz = r.Z + r.D / 2;
        void Corners(ObjKind k) { Put(k, x0, z1, 0); Put(k, x1, z1, 0); Put(k, x1, z0, 0); Put(k, x0, z0, 0); }
        switch (r.Type)
        {
            case RoomType.Office:
                // rows of desks with the chair in front, a walkway between the rows and along the walls
                for (var z = z0 + 1; z + 1 <= z1 - 1; z += 3)
                    for (var x = x0 + 1; x + 1 <= x1 - 1; x += 3)
                        if (Put(ObjKind.Desk, x, z, 0)) Put(ObjKind.Chair, x, z + 1, 2);
                Put(ObjKind.Cabinet, x1, z0, 2);
                Corners(ObjKind.Plant);
                break;
            case RoomType.Executive:
                // the director behind a desk facing the door side, two visitor chairs, a cabinet, plants and a sofa in bigger rooms
                if (Put(ObjKind.Desk, cx - 1, z0 + 1, 2)) { Put(ObjKind.Chair, cx - 1, z0, 0); Put(ObjKind.Chair, cx - 1, z0 + 2, 2); Put(ObjKind.Chair, cx, z0 + 2, 2); }
                Put(ObjKind.Cabinet, x0, z0, 0);
                if (r.W >= 4 && r.D >= 5) Put(ObjKind.Sofa, x1 - 1, z1, 2);
                Corners(ObjKind.Plant);
                break;
            case RoomType.Meeting:
                if (Put(ObjKind.MeetingTable, cx - 1, cz - 1, 0))
                    for (var x = cx - 1; x <= cx + 1; x++) { Put(ObjKind.Chair, x, cz - 2, 0); Put(ObjKind.Chair, x, cz + 1, 2); }
                Put(ObjKind.Cabinet, x0, z0, 0);
                Corners(ObjKind.Plant);
                break;
            case RoomType.Canteen:
                Put(ObjKind.Coffee, x0 + 1, z0, 0); Put(ObjKind.Vending, x0 + 2, z0, 0); Put(ObjKind.Microwave, x0 + 3, z0, 0);
                for (var z = z0 + 2; z + 1 <= z1 - 1; z += 4)
                    for (var x = x0 + 1; x + 2 <= x1 - 1; x += 4)
                        if (Put(ObjKind.MeetingTable, x, z, 0))
                            for (var i = 0; i < 3; i++) { Put(ObjKind.Chair, x + i, z - 1, 0); Put(ObjKind.Chair, x + i, z + 2, 2); }
                Corners(ObjKind.Plant);
                break;
            case RoomType.Lounge:
                Put(ObjKind.Sofa, x0 + 1, z0, 0); Put(ObjKind.Sofa, x1 - 2, z0, 0); Put(ObjKind.Sofa, cx - 1, z1, 2);
                Put(ObjKind.Coffee, x1, cz, 0);
                Corners(ObjKind.Plant);
                break;
            case RoomType.Reception:
                if (Put(ObjKind.Desk, cx - 1, z0 + 1, 2)) Put(ObjKind.Chair, cx - 1, z0, 0);
                Put(ObjKind.Sofa, x0, z1, 2);
                Put(ObjKind.Coffee, x1, z1, 2);
                Corners(ObjKind.Plant);
                break;
        }
        if (placed > 0) Log("ev_interior_done", Catalog.Rooms[r.Type].NameKey);
        return placed;
    }

    /// <summary>
    /// What the offices need, company-wide: (room name key, have, need). Need 0 means optional for now.
    /// </summary>
    [JsonIgnore]
    public List<(string key, int have, int need)> OfficeNeeds
    {
        get
        {
            int Done(RoomType t) => Rooms.Count(r => r.Type == t && r.Stage == Stage.Done);
            var people = Staff.Count + Execs.Count + SalesReps;
            return new()
            {
                ("room_reception", Done(RoomType.Reception), 1),
                ("need_workplaces", Workplaces, Math.Max(1, DeskNeed)),
                ("need_director_offices", Offices, Execs.Count + 1),
                ("room_meeting", Done(RoomType.Meeting), people >= 6 ? 1 : 0),
                ("room_canteen", Done(RoomType.Canteen), people >= 10 ? 1 : 0),
                ("room_lounge", Done(RoomType.Lounge), people >= 25 ? 1 : 0),
            };
        }
    }
}
