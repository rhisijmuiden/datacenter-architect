using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace DatacenterArchitect.Sim;

/// <summary>
/// Step 11 round 2: plan mode. While it is on, buildings, rooms, doors and objects you draw become a blueprint:
/// nothing is paid and the builders leave them alone. The plan bar shows what the plan costs; "Build plan" pays it
/// in one go and the crews start, "Discard" takes the blueprint away. Paving, fences and curves are not planned:
/// they are paid at once, as always. The plan is saved with the game.
/// </summary>
public partial class GameState
{
    /// <summary>Ids of planned buildings, rooms, doors and objects.</summary>
    public List<int> Planned { get; set; } = new();

    /// <summary>True while the game screen draws a blueprint (not saved): building costs nothing yet.</summary>
    [JsonIgnore] public bool Planning { get; set; }

    HashSet<int> _planSet; int _planCount = -1;
    HashSet<int> PlanSet
    {
        get
        {
            Planned ??= new();
            if (_planSet == null || _planCount != Planned.Count) { _planSet = Planned.ToHashSet(); _planCount = Planned.Count; }
            return _planSet;
        }
    }

    public bool IsPlanned(int id) => Planned is { Count: > 0 } && PlanSet.Contains(id);
    public bool IsPlanned(Building b) => b != null && IsPlanned(b.Id);
    public bool IsPlanned(Room r) => r != null && IsPlanned(r.Id);
    public bool IsPlanned(PlacedObject o) => o != null && IsPlanned(o.Id);

    [JsonIgnore] public bool HasPlan => PlanItems().Any();

    /// <summary>The planned things that still exist, with what each costs to build.</summary>
    public IEnumerable<(string kind, int id, string nameKey, long cost)> PlanItems()
    {
        if (Planned is not { Count: > 0 }) yield break;
        var set = PlanSet;
        foreach (var b in Buildings.Where(b => set.Contains(b.Id))) yield return ("building", b.Id, Catalog.Buildings[b.Kind].NameKey, BuildingCost(b.Kind, b.W, b.D, b.Floors));
        foreach (var r in Rooms.Where(r => set.Contains(r.Id))) yield return ("room", r.Id, Catalog.Rooms[r.Type].NameKey, Catalog.RoomCost(r.W, r.D));
        foreach (var d in Doors.Where(d => set.Contains(d.Id))) yield return ("door", d.Id, Catalog.Doors[d.Type].NameKey, Catalog.Doors[d.Type].Cost);
        foreach (var o in Objects.Where(o => set.Contains(o.Id))) yield return ("object", o.Id, o.Def.NameKey, o.Def.Cost);
    }

    [JsonIgnore] public long PlanCost => PlanItems().Sum(p => p.cost);
    [JsonIgnore] public int PlanCount => PlanItems().Count();

    /// <summary>
    /// Marks everything a build action added as planned. Call with the ids that existed before the action
    /// (the game screen does this after each action while plan mode is on).
    /// </summary>
    public int MarkPlanned(ISet<int> before)
    {
        Planned ??= new();
        var n = 0;
        foreach (var id in Buildings.Select(b => b.Id).Concat(Rooms.Select(r => r.Id)).Concat(Doors.Select(d => d.Id)).Concat(Objects.Select(o => o.Id)))
            if (!before.Contains(id) && !IsPlanned(id)) { Planned.Add(id); n++; }
        foreach (var o in Objects.Where(o => IsPlanned(o.Id))) o.Status = ObjStatus.Ordered;
        // with instant building a new building is finished at once: a blueprint is not
        foreach (var b in Buildings.Where(b => IsPlanned(b.Id) && b.Stage == Stage.Done)) b.Stage = Stage.Foundation;
        foreach (var r in Rooms.Where(r => IsPlanned(r.Id) && r.Stage == Stage.Done)) r.Stage = Stage.Walls;
        if (n > 0) Touch();
        return n;
    }

    /// <summary>All ids of the layout now, for <see cref="MarkPlanned"/>.</summary>
    public HashSet<int> LayoutIds() => Buildings.Select(b => b.Id).Concat(Rooms.Select(r => r.Id)).Concat(Doors.Select(d => d.Id)).Concat(Objects.Select(o => o.Id)).ToHashSet();

    /// <summary>Why the plan cannot be built now, or null.</summary>
    public string CanBuildPlan()
    {
        if (!HasPlan) return "msg_plan_empty";
        if (!CanAfford(PlanCost)) return "msg_no_money";
        return null;
    }

    /// <summary>Pays the plan and hands it to the crews: buildings and rooms become building sites, objects are ordered.</summary>
    public string BuildPlan()
    {
        var why = CanBuildPlan();
        if (why != null) return why;
        var cost = PlanCost;
        Spend(cost);
        var set = PlanSet;
        foreach (var o in Objects.Where(o => set.Contains(o.Id)))
        {
            o.Status = o.Def.Chain && !Creative.InstantBuild ? ObjStatus.Ordered : ObjStatus.Live;
            o.Installed = Hours / 24;
        }
        if (Creative.InstantBuild)
        {
            foreach (var b in Buildings.Where(b => set.Contains(b.Id))) b.Stage = Stage.Done;
            foreach (var r in Rooms.Where(r => set.Contains(r.Id))) r.Stage = Stage.Done;
        }
        Planned.Clear();
        if (Creative.InstantBuild) foreach (var r in Rooms.Where(r => set.Contains(r.Id)).ToList()) RoomFinished(r);
        Touch();
        return null;
    }

    /// <summary>Takes the whole blueprint away (nothing was paid for it).</summary>
    public int DiscardPlan()
    {
        if (Planned is not { Count: > 0 }) return 0;
        var set = PlanSet;
        var n = PlanCount;
        Objects.RemoveAll(o => set.Contains(o.Id));
        Doors.RemoveAll(d => set.Contains(d.Id));
        Rooms.RemoveAll(r => set.Contains(r.Id));
        foreach (var b in Buildings.Where(b => set.Contains(b.Id)).ToList())
        {
            Objects.RemoveAll(o => b.Contains(o.X, o.Z));
            Rooms.RemoveAll(r => r.BuildingId == b.Id);
            Buildings.Remove(b);
        }
        Planned.Clear();
        Touch();
        return n;
    }

    /// <summary>Refunds part of a removed thing, unless it was only planned (then it was never paid).</summary>
    void RefundRemoved(int id, long amount)
    {
        if (IsPlanned(id)) { Planned.Remove(id); return; }
        Refund(amount);
    }
}
