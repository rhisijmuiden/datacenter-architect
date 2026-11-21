using System.Linq;
using DatacenterArchitect.Core;
using DatacenterArchitect.Sim;

namespace DatacenterArchitect.Tests;

/// <summary>Infra sketch board 9: building management, every hall in one table and levelling halls up in one go.</summary>
public static partial class TestRunner
{
    static void LevelUpTests()
    {
        var (s, hall, _, _) = PowerSite();
        var hall2 = s.AddRoom(RoomType.DataHall, 24, 10, 10, 14, 0);
        hall2.Stage = Stage.Done;
        Live(s, ObjKind.Switchgear, 24, 4); Live(s, ObjKind.Ups, 29, 4);
        LiveRaw(s, ObjKind.Chiller, 40, 30);
        for (var i = 0; i < 10; i++) Live(s, ObjKind.Rack, 8 + i, 10);
        Live(s, ObjKind.Pdu, 8, 14);
        s.Money += 50_000_000;
        s.Researched.Remove("cool_corridor");

        var all = new[] { LevelUpStep.Busbar, LevelUpStep.Containment, LevelUpStep.PowerAB, LevelUpStep.NetN1, LevelUpStep.CoolN1 };
        var plan = s.PlanLevelUp(new[] { hall, hall2 }, all);
        Check(plan.Halls == 2, "both finished halls are in the plan");
        Check(plan.Actions.Count(a => a.Step == LevelUpStep.Busbar) == 2, "both halls get a busbar");
        Check(plan.Skipped.Any(x => x.Hall == hall && x.Step == LevelUpStep.PowerAB && x.Why == "bms_skip_busbar"), "no extra PDUs where the busbar brings A+B");
        Check(plan.Actions.Count(a => a.Step == LevelUpStep.Containment) == 2 && plan.Skipped.Count(x => x.Why == "bms_skip_research") == 2,
            "without the research the halls only get hot/cold aisles");
        Check(plan.Skipped.Any(x => x.Hall == hall2 && x.Step == LevelUpStep.CoolN1 && x.Why == "bms_skip_empty"), "an empty hall needs no CRAHs");
        var crahs = plan.Actions.FirstOrDefault(a => a.Hall == hall && a.Step == LevelUpStep.CoolN1);
        var net = plan.Actions.FirstOrDefault(a => a.Hall == hall && a.Step == LevelUpStep.NetN1);
        Check(crahs != null && crahs.Count >= 2 && net != null && net.Count >= 1, $"the racked hall gets CRAHs for N+1 and edge switches ({crahs?.Count}, {net?.Count})");
        Check(plan.Hours >= Catalog.BusbarHours + Catalog.Containments[Containment.HotCold].Hours - 1e-6, "steps in one hall run one after the other");
        Check(plan.PueAfter < plan.PueBefore, $"aisle containment lowers the PUE ({plan.PueBefore} → {plan.PueAfter})");

        var money = s.Money;
        var res = s.LevelUp(plan);
        Check(res.Error == null && res.Upgrades == 4 && res.Placed == crahs.Count + net.Count && res.NoRoom == 0, $"level up starts 4 hall upgrades and places the devices ({res.Upgrades}, {res.Placed}, {res.NoRoom})");
        Check(money - s.Money == plan.Cost, "everything is paid now");
        Check(hall.Upgrade == 2 && hall.UpgradeQueue.SequenceEqual(new[] { 1 }) && GameState.BusbarComing(hall) && GameState.PendingContainment(hall) == Containment.HotCold,
            "the busbar runs first, the containment waits its turn");
        var placed = s.Objects.Where(o => o.Kind == ObjKind.Crah && hall.Contains(o.X, o.Z)).ToList();
        Check(placed.Count == crahs.Count && placed.All(o => !hall.Contains(o.X - 1, o.Z) || !hall.Contains(o.X + 1, o.Z) || !hall.Contains(o.X, o.Z - 1) || !hall.Contains(o.X, o.Z + 1)),
            "CRAHs stand along the hall walls");
        var card = HallReport.Build(s, hall);
        Check(card.Upgrades.All(u => u.Kind != HallUpgradeKind.Busbar || u.Blocked == "msg_upgrade_busy"), "the hall card does not offer the busbar again");

        s.UpdateInfra(Catalog.BusbarHours + 0.5);
        Check(hall.Busbar && hall.Upgrade == 1 && hall.UpgradeQueue.Count == 0, "after the busbar the queued containment starts by itself");
        s.UpdateInfra(Catalog.Containments[Containment.HotCold].Hours + 0.5);
        Check(hall.Containment == Containment.HotCold && hall.Upgrade == 0, "then the hall has hot/cold aisles");

        var again = s.PlanLevelUp(new[] { hall }, new[] { LevelUpStep.Busbar });
        Check(again.Actions.Count == 0 && again.Skipped.Single().Why == "bms_skip_has", "what a hall has is skipped");
        Check(s.LevelUp(again).Error == "msg_nothing_changed", "nothing to do says so");
        s.Money = 0;
        Check(s.LevelUp(s.PlanLevelUp(new[] { hall }, all)).Error is "msg_no_money" or "msg_nothing_changed", "short of money nothing happens");

        // the queue survives a save and an undo copy
        hall.Upgrade = 2; hall.UpgradeQueue.Add(1);
        var json = System.Text.Json.JsonSerializer.Serialize(hall);
        var back = System.Text.Json.JsonSerializer.Deserialize<Room>(json);
        Check(back.UpgradeQueue.SequenceEqual(new[] { 1 }) && hall.Copy().UpgradeQueue.SequenceEqual(new[] { 1 }) && !ReferenceEquals(hall.Copy().UpgradeQueue, hall.UpgradeQueue), "the upgrade queue is saved and copied");
        hall.Upgrade = 0; hall.UpgradeQueue.Clear();

        // the screen builds in both languages
        s.Money = 50_000_000;
        foreach (var lang in new[] { "en", "nl" })
        {
            Loc.SetLanguage(lang);
            var p = new UI.BuildingPanel(() => s, _ => { }, _ => { }, pl => s.LevelUp(pl));
            Check(p.GetChildCount() > 0, $"building management builds ({lang})");
            p.Free();
        }
        Loc.SetLanguage("en");
        Check(UI.Sections.Find(UI.PanelKind.Building, 0).section == UI.Sections.Infra, "building management sits under Infrastructure");
    }
}
