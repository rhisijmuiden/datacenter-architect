using System;
using System.Collections.Generic;
using System.Linq;

namespace DatacenterArchitect.Sim;

/// <summary>What the building-management screen can bring a group of halls up to in one go.</summary>
public enum LevelUpStep { Busbar, Containment, PowerAB, NetN1, CoolN1 }

/// <summary>One thing a level-up does in one hall: start (or queue) a hall upgrade, or place devices.</summary>
public class LevelUpAction
{
    public Room Hall;
    public LevelUpStep Step;
    /// <summary>Containment: the level this step builds. Devices: what to place and how many.</summary>
    public Containment Level;
    public ObjKind Obj;
    public int Count;
    public long Cost;
    public double Hours;
}

/// <summary>A hall that a chosen step leaves alone, and why (text key).</summary>
public record LevelUpSkip(Room Hall, LevelUpStep Step, string Why);

/// <summary>
/// The plan to bring the chosen halls level with the chosen steps (infra sketch board 9). What a hall already has, or
/// will get from an upgrade already under way, is skipped. Hall upgrades run one after the other per hall; the rest are
/// devices placed along the hall walls.
/// </summary>
public class LevelUpPlan
{
    public readonly List<LevelUpAction> Actions = new();
    public readonly List<LevelUpSkip> Skipped = new();
    public long Cost => Actions.Sum(a => a.Cost);
    /// <summary>Hours until the last hall upgrade is done (halls work side by side, steps in one hall one after the other).</summary>
    public double Hours;
    /// <summary>Average PUE of the chosen halls with racks, weighted by their IT load, now and after the plan.</summary>
    public double PueBefore, PueAfter;
    public int Halls;
}

/// <summary>What a level-up did: hall upgrades started or queued, devices placed, and devices that found no room.</summary>
public record LevelUpResult(int Upgrades, int Placed, int NoRoom, string Error);

public partial class GameState
{
    /// <summary>Containment level the hall will have once its running and queued upgrades are done.</summary>
    public static Containment PendingContainment(Room r)
        => (Containment)Math.Min((int)Containment.ColdCorridor, (int)r.Containment + (r.Upgrade == 1 ? 1 : 0) + r.UpgradeQueue.Count(q => q == 1));

    /// <summary>The hall has a busbar or one is being installed or queued.</summary>
    public static bool BusbarComing(Room r) => r.Busbar || r.Upgrade == 2 || r.UpgradeQueue.Contains(2);

    /// <summary>Hours of hall upgrades still to do in this hall (running and queued).</summary>
    public static double UpgradeHoursLeft(Room r)
    {
        var h = r.Upgrade != 0 ? Math.Max(0, r.UpgradeLeft) : 0;
        var level = r.Containment + (r.Upgrade == 1 ? 1 : 0);
        foreach (var q in r.UpgradeQueue)
        {
            if (q == 2) h += Catalog.BusbarHours;
            else if (level < Containment.ColdCorridor) { level++; h += Catalog.Containments[level].Hours; }
        }
        return h;
    }

    double PueOf(Containment c) => Math.Max(1.08, Catalog.Containments[c].Pue - PueSaving);

    /// <summary>Plan the chosen steps for the chosen halls. Halls that are not finished data halls are left out.</summary>
    public LevelUpPlan PlanLevelUp(IEnumerable<Room> halls, ICollection<LevelUpStep> steps)
    {
        var plan = new LevelUpPlan();
        double kwSum = 0, before = 0, after = 0;
        foreach (var hall in halls.Where(h => h.Type == RoomType.DataHall && h.Stage == Stage.Done).Distinct())
        {
            plan.Halls++;
            var rep = HallReport.Build(this, hall);
            var hours = UpgradeHoursLeft(hall);
            var cont = PendingContainment(hall);
            var busbar = BusbarComing(hall);
            if (steps.Contains(LevelUpStep.Busbar))
            {
                if (busbar) plan.Skipped.Add(new(hall, LevelUpStep.Busbar, "bms_skip_has"));
                else
                {
                    plan.Actions.Add(new LevelUpAction { Hall = hall, Step = LevelUpStep.Busbar, Cost = Catalog.BusbarCost, Hours = Catalog.BusbarHours });
                    hours += Catalog.BusbarHours; busbar = true;
                }
            }
            if (steps.Contains(LevelUpStep.Containment))
            {
                if (cont >= Containment.ColdCorridor) plan.Skipped.Add(new(hall, LevelUpStep.Containment, "bms_skip_has"));
                while (cont < Containment.ColdCorridor)
                {
                    var next = cont + 1;
                    if (next == Containment.ColdCorridor && !Done("cool_corridor"))
                    {
                        plan.Skipped.Add(new(hall, LevelUpStep.Containment, "bms_skip_research"));
                        break;
                    }
                    var d = Catalog.Containments[next];
                    plan.Actions.Add(new LevelUpAction { Hall = hall, Step = LevelUpStep.Containment, Level = next, Cost = d.CostPerM2 * hall.Area, Hours = d.Hours });
                    hours += d.Hours; cont = next;
                }
            }
            if (steps.Contains(LevelUpStep.PowerAB))
            {
                if (rep.Racks == 0) plan.Skipped.Add(new(hall, LevelUpStep.PowerAB, "bms_skip_empty"));
                else if (rep.PowerLevel >= HallLevel.AB || busbar) plan.Skipped.Add(new(hall, LevelUpStep.PowerAB, busbar && rep.PowerLevel < HallLevel.AB ? "bms_skip_busbar" : "bms_skip_has"));
                else if (rep.Upgrades.FirstOrDefault(u => u.Kind == HallUpgradeKind.Pdu) is { } u)
                    plan.Actions.Add(new LevelUpAction { Hall = hall, Step = LevelUpStep.PowerAB, Obj = u.Obj, Count = u.Count, Cost = u.Cost });
            }
            if (steps.Contains(LevelUpStep.NetN1))
            {
                if (rep.Racks == 0) plan.Skipped.Add(new(hall, LevelUpStep.NetN1, "bms_skip_empty"));
                else if (rep.NetLevel >= HallLevel.NPlus1) plan.Skipped.Add(new(hall, LevelUpStep.NetN1, "bms_skip_has"));
                else if (rep.Upgrades.FirstOrDefault(u => u.Kind == HallUpgradeKind.EdgeSwitch) is { } u)
                    plan.Actions.Add(new LevelUpAction { Hall = hall, Step = LevelUpStep.NetN1, Obj = u.Obj, Count = u.Count, Cost = u.Cost });
            }
            if (steps.Contains(LevelUpStep.CoolN1))
            {
                // with better containment every CRAH does more, so count with the level the hall is getting
                if (rep.NeedKw <= 0) plan.Skipped.Add(new(hall, LevelUpStep.CoolN1, "bms_skip_empty"));
                else if (rep.CoolLevel >= HallLevel.NPlus1) plan.Skipped.Add(new(hall, LevelUpStep.CoolN1, "bms_skip_has"));
                else
                {
                    var n = rep.CrahsForNPlus1(this, cont);
                    if (n <= 0) plan.Skipped.Add(new(hall, LevelUpStep.CoolN1, "bms_skip_has"));
                    else plan.Actions.Add(new LevelUpAction { Hall = hall, Step = LevelUpStep.CoolN1, Obj = ObjKind.Crah, Count = n, Cost = Catalog.Objects[ObjKind.Crah].Cost * n });
                }
            }
            plan.Hours = Math.Max(plan.Hours, hours);
            if (rep.NeedKw > 0)
            {
                kwSum += rep.NeedKw;
                before += PueOf(hall.Containment) * rep.NeedKw;
                after += PueOf(cont) * rep.NeedKw;
            }
        }
        plan.PueBefore = kwSum > 0 ? before / kwSum : 0;
        plan.PueAfter = kwSum > 0 ? after / kwSum : 0;
        return plan;
    }

    /// <summary>
    /// Carry out a level-up plan: hall upgrades start now or wait their turn behind the one running (paid now), devices
    /// are placed along the hall walls and paid as if the player placed them. Nothing happens when the money is short.
    /// </summary>
    /// <remarks>The game runs the hall upgrades and the device placement as two calls, so only the devices go in the undo history.</remarks>
    public LevelUpResult LevelUp(LevelUpPlan plan, bool upgrades = true, bool devices = true)
    {
        var mine = plan.Actions.Where(a => a.Step is LevelUpStep.Busbar or LevelUpStep.Containment ? upgrades : devices).ToList();
        if (mine.Count == 0) return new(0, 0, 0, "msg_nothing_changed");
        if (!CanAfford(mine.Sum(a => a.Cost))) return new(0, 0, 0, "msg_no_money");
        int ups = 0, placed = 0, noRoom = 0;
        foreach (var a in mine)
        {
            switch (a.Step)
            {
                case LevelUpStep.Busbar:
                case LevelUpStep.Containment:
                    if (QueueHallUpgrade(a.Hall, a.Step == LevelUpStep.Busbar, a.Cost)) ups++;
                    break;
                default:
                    var n = PlaceInHall(a.Hall, a.Obj, a.Count);
                    placed += n; noRoom += a.Count - n;
                    break;
            }
        }
        if (ups + placed > 0) Log("ev_level_up", (ups + placed).ToString());
        return new(ups, placed, noRoom, null);
    }

    /// <summary>Start a hall upgrade, or queue it behind the one running. It is paid now; a queued one starts by itself.</summary>
    public bool QueueHallUpgrade(Room r, bool busbar, long cost)
    {
        if (r.Type != RoomType.DataHall || r.Stage != Stage.Done) return false;
        if (busbar ? BusbarComing(r) : PendingContainment(r) >= Containment.ColdCorridor) return false;
        if (!CanAfford(cost)) return false;
        Spend(cost);
        var code = busbar ? 2 : 1;
        if (r.Upgrade == 0) StartHallUpgrade(r, code);
        else r.UpgradeQueue.Add(code);
        Touch();
        return true;
    }

    void StartHallUpgrade(Room r, int code)
    {
        r.Upgrade = code;
        r.UpgradeLeft = code == 2 ? Catalog.BusbarHours : Catalog.Containments[r.Containment + 1].Hours;
        if (Creative.InstantBuild) FinishHallUpgrade(r);
    }

    /// <summary>After an upgrade finishes, the next one in the hall's queue starts.</summary>
    void NextQueuedUpgrade(Room r)
    {
        while (r.Upgrade == 0 && r.UpgradeQueue.Count > 0)
        {
            var code = r.UpgradeQueue[0];
            r.UpgradeQueue.RemoveAt(0);
            if (code == 2 ? r.Busbar : r.Containment >= Containment.ColdCorridor) continue;
            StartHallUpgrade(r, code);
        }
    }

    /// <summary>
    /// Place up to <paramref name="count"/> devices in a hall, along its walls first, facing into the hall, keeping the
    /// doorways and the aisles of the racks free. Returns how many found a spot.
    /// </summary>
    public int PlaceInHall(Room hall, ObjKind kind, int count)
    {
        var keep = new HashSet<(int, int)>();
        foreach (var d in DoorsOf(hall))
            foreach (var e in d.Edges())
                foreach (var c in e.dir == 0 ? new[] { (e.x - 1, e.z), (e.x, e.z) } : new[] { (e.x, e.z - 1), (e.x, e.z) })
                    foreach (var (dx, dz) in new[] { (0, 0), (1, 0), (-1, 0), (0, 1), (0, -1) }) keep.Add((c.Item1 + dx, c.Item2 + dz));
        bool Edge((int x, int z) c) => !hall.Contains(c.x - 1, c.z) || !hall.Contains(c.x + 1, c.z) || !hall.Contains(c.x, c.z - 1) || !hall.Contains(c.x, c.z + 1);
        // wall cells first, then the rest of the hall; same order every time
        var cells = hall.CellList().Where(c => !keep.Contains(c)).OrderBy(c => Edge(c) ? 0 : 1).ThenBy(c => c.z).ThenBy(c => c.x).ToList();
        var placed = 0;
        foreach (var (x, z) in cells)
        {
            if (placed >= count) break;
            if (ObjectAt(x, z, hall.Floor) != null) continue;
            for (var rot = 0; rot < 4; rot++)
            {
                var (fx, fz) = FrontDir(rot);
                if (!hall.Contains(x + fx, z + fz)) continue;
                var probe = new PlacedObject { Kind = kind, X = x, Z = z, Rot = rot, Floor = hall.Floor };
                if (Cells(probe).Any(c => keep.Contains(c) || !hall.Contains(c.x, c.z))) continue;
                if (AddObject(kind, x, z, rot, hall.Floor) != null) { placed++; break; }
            }
        }
        return placed;
    }
}
