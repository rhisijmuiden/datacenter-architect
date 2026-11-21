using System;
using System.Collections.Generic;
using System.Linq;

namespace DatacenterArchitect.Sim;

/// <summary>A hall the player can pick for a contract, with how many racks are free there and why it would not work yet.</summary>
public record HallChoice(Room Hall, int Free, string Problem);

/// <summary>
/// Infra sketch board 7: every data hall has a fixed code (DH-03), a name the player chooses and a colour band.
/// Contracts can be placed in a hall picked by the player; a customer who rents a whole hall gets its logo on the wall.
/// What a hall is sold as (full racks, per U, cages, private hall) is set only under Company › Contracts.
/// </summary>
public partial class GameState
{
    /// <summary>Colour bands a hall can carry on its floor edge, signs and in lists.</summary>
    public static readonly string[] HallBands = { "#38c6f4", "#f2a33a", "#3fbf7f", "#8a6cf0", "#ff7a1a", "#e2493b", "#f2c94c", "#c2185b" };
    public const int MaxHallName = 32;

    /// <summary>Gives every data hall without a number the next free one, in the order they were drawn.</summary>
    public void NumberHalls()
    {
        var next = Rooms.Where(r => r.Type == RoomType.DataHall).Select(r => r.HallNo).DefaultIfEmpty(0).Max() + 1;
        foreach (var r in Rooms.Where(r => r.Type == RoomType.DataHall && r.HallNo <= 0)) r.HallNo = next++;
    }

    /// <summary>Short code of a data hall: DH-01, DH-02 ... Numbers stay with their hall when another hall is removed.</summary>
    public string HallCode(Room hall)
    {
        if (hall == null || hall.Type != RoomType.DataHall) return "DH";
        if (hall.HallNo <= 0) NumberHalls();
        return $"DH-{hall.HallNo:00}";
    }

    /// <summary>"DH-03 · Private hall Nordwind", or only the code when the hall has no name.</summary>
    public string HallTitle(Room hall) => string.IsNullOrWhiteSpace(hall?.Name) ? HallCode(hall) : $"{HallCode(hall)} · {hall.Name}";

    /// <summary>The colour band of a hall (picked, or one per hall number).</summary>
    public static string HallBand(Room hall)
    {
        var i = hall.Band >= 0 && hall.Band < HallBands.Length ? hall.Band : Math.Max(0, hall.HallNo - 1) % HallBands.Length;
        return HallBands[i];
    }

    public string RenameHall(Room hall, string name)
    {
        if (hall == null || hall.Type != RoomType.DataHall) return "msg_nothing_changed";
        name = new string((name ?? "").Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (name.Length > MaxHallName) name = name[..MaxHallName];
        if (name == (hall.Name ?? "")) return null;
        hall.Name = name;
        Touch();
        return null;
    }

    public void SetHallBand(Room hall, int band)
    {
        if (hall == null || hall.Type != RoomType.DataHall) return;
        hall.Band = Math.Clamp(band, -1, HallBands.Length - 1);
        Touch();
    }

    public void SetHallLogo(Room hall, bool on)
    {
        if (hall == null || hall.Type != RoomType.DataHall || hall.ShowLogo == on) return;
        hall.ShowLogo = on;
        Touch();
    }

    /// <summary>The customer that rents this whole hall (signed, or being built for), or null.</summary>
    public Deal HallTenant(Room hall)
    {
        if (hall == null) return null;
        foreach (var d in Deals)
        {
            if (d.Product != Product.Hall || d.State is not (DealState.Active or DealState.Building)) continue;
            if (d.HallId == hall.Id) return d;
            if (d.State == DealState.Active && d.HallId == 0 && d.RackIds.Count > 0 && Objects.FirstOrDefault(o => o.Id == d.RackIds[0]) is { } rack && hall.Floor == rack.Floor && hall.Contains(rack.X, rack.Z)) return d;
        }
        return null;
    }

    /// <summary>The halls where a contract's racks stand (or the hall it is reserved for).</summary>
    public List<Room> DealHalls(Deal d)
    {
        var list = new List<Room>();
        if (d.HallId != 0 && Rooms.FirstOrDefault(r => r.Id == d.HallId) is { } picked) list.Add(picked);
        var ids = d.RackIds.ToHashSet();
        foreach (var o in Objects.Where(o => ids.Contains(o.Id)))
            if (HallOf(o) is { } h && !list.Contains(h)) list.Add(h);
        return list.OrderBy(h => h.HallNo).ToList();
    }

    /// <summary>A hall is held by another customer: rented whole, or reserved for a contract that is being built.</summary>
    bool HeldByOther(Room hall, Deal d, Dictionary<int, int> owners)
    {
        var tenant = HallTenant(hall);
        if (tenant != null && tenant != d) return true;
        foreach (var o in Deals)
            if (o != d && o.HallId == hall.Id && o.State is DealState.Building && o.Product is Product.Cage or Product.Hall) return true;
        if (d.Product == Product.Hall)
            foreach (var o in Objects)
                if (o.Kind == ObjKind.Rack && o.Floor == hall.Floor && hall.Contains(o.X, o.Z) && owners.TryGetValue(o.Id, out var id) && id != d.Id) return true;
        return false;
    }

    /// <summary>Racks rented to someone in a hall.</summary>
    int RentedIn(Room hall, Dictionary<int, int> owners) =>
        Objects.Count(o => o.Kind == ObjKind.Rack && o.Floor == hall.Floor && hall.Contains(o.X, o.Z) && owners.ContainsKey(o.Id));

    /// <summary>
    /// The data halls offered in a contract's "place in" list, each with its free racks and what is missing (null = fits now).
    /// A picked hall may have another zone as long as nobody rents racks there yet: signing then makes it the right zone.
    /// </summary>
    public List<HallChoice> HallChoices(Deal d)
    {
        var list = new List<HallChoice>();
        if (d.Product == Product.PerU) return list;
        var owners = RackOwners();
        var working = WorkingRacks();
        var zone = ZoneFor(d.Product);
        foreach (var hall in Rooms.Where(r => r.Type == RoomType.DataHall && !IsPlanned(r)).OrderBy(r => r.HallNo))
        {
            if (hall.HallNo <= 0) NumberHalls();
            string problem = null;
            var racks = Objects.Where(o => o.Kind == ObjKind.Rack && o.Floor == hall.Floor && hall.Contains(o.X, o.Z)).ToList();
            var free = racks.Count(o => !owners.ContainsKey(o.Id) && working.Contains(o.Id));
            var info = Infra.Halls.FirstOrDefault(h => h.Room == hall);
            if (HeldByOther(hall, d, owners)) problem = "place_taken";
            else if (hall.Zone != zone && RentedIn(hall, owners) > 0 && !d.RackIds.Any(id => racks.Any(o => o.Id == id))) problem = "place_other_zone";
            else if (!HallTakes(hall, d.Type)) problem = "place_clients";
            else if (hall.Stage != Stage.Done) problem = "place_building";
            else if (info != null && !HallFits(d, info)) problem = d.Amp32 && hall.Containment == Containment.Open ? "place_aisles" : "place_ab";
            else if (free < d.Racks) problem = "place_short";
            list.Add(new HallChoice(hall, free, problem));
        }
        return list;
    }

    /// <summary>Put a contract in one hall (0 = let sales pick a hall). Only before the customer moves in.</summary>
    public string PlaceDeal(Deal d, int hallId)
    {
        if (d == null || d.Product == Product.PerU || d.State is not (DealState.Lead or DealState.Tender or DealState.Bid or DealState.Building)) return "msg_nothing_changed";
        if (hallId != 0)
        {
            var c = HallChoices(d).FirstOrDefault(x => x.Hall.Id == hallId);
            if (c == null) return "msg_nothing_changed";
            if (c.Problem is "place_taken" or "place_other_zone") return "msg_" + c.Problem;
        }
        d.HallId = hallId;
        Touch(false);
        return null;
    }

    /// <summary>Days a customer waits while you build or finish the hall picked for them.</summary>
    public static int BuildDays(Deal d) => d.Product == Product.Hall ? 60 + d.Racks / 2 : 40 + d.Racks / 2;

    /// <summary>Sign now and build later: only for a cage or a whole hall, placed in a hall that is not ready yet.</summary>
    public string CanSignAndBuild(Deal d)
    {
        if (d == null || d.State != DealState.Lead || d.Tender) return "msg_nothing_changed";
        if (d.Product is not (Product.Cage or Product.Hall)) return "msg_nothing_changed";
        if (d.HallId == 0) return "msg_pick_hall";
        var c = HallChoices(d).FirstOrDefault(x => x.Hall.Id == d.HallId);
        if (c == null) return "msg_pick_hall";
        if (c.Problem is "place_taken" or "place_other_zone" or "place_clients") return "msg_" + c.Problem;
        return null;
    }

    public string SignAndBuild(Deal d)
    {
        var why = CanSignAndBuild(d);
        if (why != null) return why;
        if (Meets(d)) { Activate(d); return null; }
        d.State = DealState.Building;
        d.DeadlineDay = Day + BuildDays(d);
        if (Rooms.FirstOrDefault(r => r.Id == d.HallId) is { } hall && RentedIn(hall, RackOwners()) == 0) hall.Zone = ZoneFor(d.Product);
        Notes.Add(("msg_signed_build", DisplayArg(d)));
        Touch();
        return null;
    }
}
