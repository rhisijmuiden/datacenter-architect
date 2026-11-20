using System;
using System.Collections.Generic;
using System.Linq;
using DatacenterArchitect.Core;
using DatacenterArchitect.Sim;
using DatacenterArchitect.UI;

namespace DatacenterArchitect.Tests;

/// <summary>Step 11 round 2: grid congestion queue, Datacenter of the year, tutorial coach and plan mode.</summary>
public static partial class TestRunner
{
    static void Round2Tests()
    {
        // ---- grid congestion: the queue comes before the works ----
        var g = GameState.New(new NewGameOptions { Difficulty = 1, GridQueue = true });
        Unlock(g);
        g.Money = 50_000_000;
        Check(g.GridQueueLength(1) == 1 && g.GridQueueLength(2) == 3 && g.GridQueueLength(3) == 5, "heavier connections wait longer in the queue");
        Check(g.GridWaitDays(1) == 30 + Catalog.GridLevels[1].LeadDays, "the wait is queue plus works");
        Check(g.RequestGrid(2) == null && g.InGridQueue && g.GridQueuePlace == 3, "a request joins the queue at place 3");
        Check(g.Notes.Any(n => n.key == "msg_grid_queue_joined"), "the player is told the queue place");
        var lead = g.GridDaysLeft;
        g.Advance(24 * 31);
        Check(g.GridQueuePlace == 2 && Math.Abs(g.GridDaysLeft - lead) < 1e-6, "the works wait while in the queue; one place per month");
        Check(g.Notes.Any(n => n.key == "msg_grid_queue_place"), "moving up is announced");
        g.Advance(24 * 60);
        Check(!g.InGridQueue && g.GridDaysLeft < lead && g.GridLevel == 0, "after the queue the works run");
        g.Advance(24 * (Catalog.GridLevels[2].LeadDays + 1));
        Check(g.GridLevel == 2 && g.GridRequest == -1 && g.GridQueueDays == 0, "connected after queue and works");
        var easy = GameState.New(new NewGameOptions { Difficulty = 0, GridQueue = true });
        Check(easy.GridQueueLength(1) == 0, "on easy the first upgrade does not queue");
        var off = GameState.New(new NewGameOptions { GridQueue = false });
        Check(off.GridQueueLength(3) == 0, "congestion off: no queue (older saves)");
        var bat = Quiet(); bat.Options.GridQueue = true;
        var withoutBattery = bat.GridQueueLength(2);
        bat.Objects.Add(new PlacedObject { Id = bat.NextId++, Kind = ObjKind.BatteryContainer, X = 1, Z = 1, Status = ObjStatus.Live });
        Check(bat.GridQueueLength(2) == withoutBattery - 1, "a battery container moves you up one place");
        var json = System.Text.Json.JsonSerializer.Serialize(g);
        var gq = GameState.New(new NewGameOptions { GridQueue = true }); Unlock(gq); gq.Money = 50_000_000; gq.RequestGrid(3);
        var gb = System.Text.Json.JsonSerializer.Deserialize<GameState>(System.Text.Json.JsonSerializer.Serialize(gq));
        Check(gb.Options.GridQueue && Math.Abs(gb.GridQueueDays - gq.GridQueueDays) < 1e-6 && gb.GridQueuePlace == gq.GridQueuePlace, "the queue survives a save");

        // ---- Datacenter of the year ----
        var a = Quiet();
        Check(a.TrophyDesk != null, "the demo has a desk in the director's office for the statuettes");
        a.Stats.Clear();
        for (var d = 1; d <= 365; d++)
            a.Stats.Add(new StatSample { Day = d, LiveRacks = 60 + d, Uptime = 1, Pue = 1.25, Reputation = 92, Esg = 85 });
        var marks = a.AwardMarks(0, 365);
        Check(marks != null && marks.Length == GameState.AwardCriteria.Length && marks.All(m => m >= 0 && m <= 100), "the jury gives a mark per criterion");
        Check(Math.Abs(GameState.AwardCriteria.Sum(c => c.weight) - 1) < 1e-9, "the criteria weigh 100% together");
        var great = a.AwardScore(0, 365);
        Check(great > 75, $"a great year scores high ({great})");
        var poor = Quiet(); poor.Stats.Clear();
        for (var d = 1; d <= 365; d++) poor.Stats.Add(new StatSample { Day = d, LiveRacks = 20, Uptime = 0.96, Pue = 1.9, Reputation = 30, Esg = 10 });
        Check(poor.AwardScore(0, 365) < great - 30, "a poor year scores much lower");
        var tiny = Quiet(); tiny.Stats.Clear(); tiny.Stats.Add(new StatSample { Day = 5, LiveRacks = 3 });
        Check(tiny.AwardMarks(0, 10) == null && tiny.AwardScore(0, 10) == 0, "small sites are not judged");
        foreach (var c in a.Competitors) Check(GameState.RivalAwardScore(c, 2027) == GameState.RivalAwardScore(c, 2027), "rival scores are the same every time");
        // New Year's Day: the jury judges the past year
        a.AwardYearSeen = 2027;
        var rep = a.Reputation;
        a.AwardsDay(366);
        Check(a.Awards.Count == 1 && a.Awards[0].Year == 2027 && a.AwardYearSeen == 2028, "on New Year's Day the jury judges last year");
        var won = a.Awards[0];
        Check(won.Place >= 1 && won.Place <= 3 && a.Statuettes.Count() == 1 && a.Reputation > rep, $"a great year ends in the top three (place {won.Place}) with reputation");
        Check(a.Notes.Any(n => n.key is "msg_award_won" or "msg_award_place"), "the result is announced");
        a.AwardsDay(367);
        Check(a.Awards.Count == 1, "the jury meets once a year");
        var win = Quiet(); win.Competitors.Clear();
        win.Judge(2030, 50);
        Check(win.AwardWins == 1 && win.HasAchievement("dc_of_year"), "winning earns the Datacenter of the year achievement");
        var p = Quiet();
        foreach (var c in p.Competitors) c.Quality = 100;
        var lost = p.Judge(2031, 5);
        Check(lost.Place == 0 && p.Notes.Any(n => n.key == "msg_award_lost") && !p.Statuettes.Any(), "losing tells who won, no statuette");
        var fresh = Quiet(); fresh.Stats.Clear();
        fresh.AwardsDay(10); fresh.AwardsDay(400);
        Check(fresh.Awards.Count == 0, "no award without a year of statistics");
        var ab = System.Text.Json.JsonSerializer.Deserialize<GameState>(System.Text.Json.JsonSerializer.Serialize(a));
        Check(ab.Awards.Count == 1 && ab.Awards[0].Place == won.Place && ab.AwardYearSeen == 2028, "awards survive a save");
        Check(GameState.DayOfYearStart(2028) == 365, "years follow the calendar");

        // ---- tutorial coach ----
        var t = GameState.New(new NewGameOptions { Tutorial = true });
        var none = Tutorial.Steps.Select(st => (st.Goal, false)).ToList();
        Check(Tutorial.Current(t, none) == Tutorial.Welcome, "the tutorial starts with a welcome");
        t.TutorialSkipped.Add(Tutorial.Welcome);
        Check(Tutorial.Current(t, none) == "goal_driveway", "then the first goal");
        var some = none.Select(x => (x.Goal, x.Goal is "goal_driveway" or "goal_yard")).ToList();
        Check(Tutorial.Current(t, some) == "goal_building", "done goals are passed");
        t.TutorialSkipped.Add("goal_building");
        Check(Tutorial.Current(t, some) == "goal_hall", "a skipped tip is passed");
        Check(Tutorial.Current(t, none.Select(x => (x.Goal, true)).ToList()) == Tutorial.Finished, "all done: the coach says so");
        t.Options.Tutorial = false;
        Check(Tutorial.Current(t, none) == null, "stopped tutorial shows nothing");
        Check(Tutorial.Steps.All(st => st.Category != null || st.Section >= 0), "every tip points somewhere");
        Check(Tutorial.Steps.Where(st => st.Section >= 0).All(st => st.Section < Sections.All.Length && st.Tab < Sections.All[st.Section].Tabs.Length), "tips open existing screens");
        foreach (var st in Tutorial.Steps)
        {
            Check(Loc.Table["en"].ContainsKey("tut_" + st.Goal[5..]), $"tutorial text for {st.Goal}");
            if (st.Category != null) Check(Loc.Table["en"].ContainsKey(st.Category), $"category {st.Category} exists");
        }

        // ---- plan mode ----
        var pl = GameState.New(new NewGameOptions { Difficulty = 1 });
        Unlock(pl);
        pl.Money = 100_000;
        var cost = GameState.BuildingCost(BuildingKind.Datacenter, 20, 14, 1);
        Check(cost > pl.Money, "the building costs more than we have (to test planning beyond the budget)");
        Check(pl.CanBuild(BuildingKind.Datacenter, 10, 10, 20, 14, 1) == "msg_no_money", "without plan mode it cannot be built");
        var ids = pl.LayoutIds(); pl.Planning = true;
        Check(pl.CanBuild(BuildingKind.Datacenter, 10, 10, 20, 14, 1) == null, "in plan mode money does not stop drawing");
        var b = pl.AddBuilding(BuildingKind.Datacenter, 10, 10, 20, 14, 1);
        pl.Planning = false;
        Check(pl.MarkPlanned(ids) == 1 && pl.IsPlanned(b) && pl.Money == 100_000, "the building is planned, nothing paid");
        ids = pl.LayoutIds(); pl.Planning = true;
        var hall = pl.AddRoom(RoomType.DataHall, 11, 11, 10, 8, 0);
        var rack = pl.AddObject(ObjKind.Rack, 12, 12, 0, 0);
        pl.Planning = false; pl.MarkPlanned(ids);
        Check(hall != null && rack != null && pl.IsPlanned(hall) && pl.IsPlanned(rack) && rack.Status == ObjStatus.Ordered, "rooms and objects inside a planned building can be planned");
        Check(pl.PlanCount == 3 && pl.PlanCost == cost + Catalog.RoomCost(10, 8) + rack.Def.Cost, "the plan adds up its costs");
        var sim = new Simulation(pl);
        Check(!sim.Sites().Any(), "builders leave a blueprint alone");
        Check(pl.BuildPlan() == "msg_no_money" && pl.IsPlanned(b), "a plan that cannot be paid stays a plan");
        pl.Money = 50_000_000;
        var m0 = pl.Money; var planCost = pl.PlanCost;
        Check(pl.BuildPlan() == null && pl.Money == m0 - planCost && !pl.HasPlan && pl.Planned.Count == 0, "build plan pays everything at once");
        Check(sim.Sites().Any(st => st.B == b) && b.Stage == Stage.Foundation && rack.Status == ObjStatus.Ordered, "then the crews start and the rack is ordered");
        // discard and erase never give money back for a blueprint
        var dz = GameState.New(new NewGameOptions());
        Unlock(dz); dz.Money = 10_000_000;
        ids = dz.LayoutIds(); dz.Planning = true;
        dz.AddBuilding(BuildingKind.Office, 40, 40, 8, 8, 1);
        dz.AddObject(ObjKind.Generator, 30, 30, 0, 0);
        dz.Planning = false; dz.MarkPlanned(ids);
        var m1 = dz.Money;
        dz.Erase(30, 30, 4, 4, 0);
        Check(dz.Money == m1 && dz.PlanCount == 1, "erasing a planned object gives no money back");
        Check(dz.DiscardPlan() == 1 && dz.Buildings.Count == 0 && dz.Money == m1, "discard removes the blueprint at no cost");
        // instant build: a blueprint stays a blueprint until it is built
        var ib = GameState.New(new NewGameOptions { Creative = true, CreativeOptions = new CreativeOptions { InstantBuild = true, UnlimitedMoney = true } });
        Unlock(ib);
        ids = ib.LayoutIds(); ib.Planning = true;
        var ibb = ib.AddBuilding(BuildingKind.Datacenter, 10, 10, 10, 10, 1);
        ib.Planning = false; ib.MarkPlanned(ids);
        Check(ibb.Stage == Stage.Foundation, "with instant build a planned building is not finished");
        new Simulation(ib).FinishEverything();
        Check(ibb.Stage == Stage.Foundation, "finish-everything skips the blueprint");
        Check(ib.BuildPlan() == null && ibb.Stage == Stage.Done, "building the plan with instant build finishes it");
        var sv = GameState.New(new NewGameOptions()); Unlock(sv); sv.Money = 10_000_000;
        ids = sv.LayoutIds(); sv.Planning = true; sv.AddBuilding(BuildingKind.Office, 40, 40, 8, 8, 1); sv.Planning = false; sv.MarkPlanned(ids);
        var svb = System.Text.Json.JsonSerializer.Deserialize<GameState>(System.Text.Json.JsonSerializer.Serialize(sv));
        Check(svb.PlanCount == 1 && !svb.Planning, "the blueprint survives a save");
    }
}
