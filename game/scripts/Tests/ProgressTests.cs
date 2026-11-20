using System;
using System.Collections.Generic;
using System.Linq;
using DatacenterArchitect.Core;
using DatacenterArchitect.Sim;
using DatacenterArchitect.UI;
using DatacenterArchitect.World;
using Godot;
using FileAccess = Godot.FileAccess;

namespace DatacenterArchitect.Tests;

/// <summary>Step 11: statistics, achievements, scenarios, timelapse and photo mode.</summary>
public static partial class TestRunner
{
    static void ProgressTests()
    {
        // ---- statistics are recorded once a day and survive a save ----
        var s = Quiet();
        for (var d = 0; d < 40; d++) s.Advance(24);
        Check(s.Stats.Count == 40, $"one statistics sample per game day ({s.Stats.Count})");
        var last = s.Stats[^1];
        Check(last.LiveRacks == s.LiveRacks && last.Pue > 1 && last.Uptime > 0.9 && last.ItKw > 0, "a sample holds racks, PUE, uptime and IT load");
        Check(s.Stats.Any(x => x.Costs > 0) && s.Stats.Any(x => x.Income > 0), "a sample holds the day's income and costs");
        var json = System.Text.Json.JsonSerializer.Serialize(s);
        var back = System.Text.Json.JsonSerializer.Deserialize<GameState>(json);
        Check(back.Stats.Count == 40 && back.Stats[^1].Day == last.Day && back.Stats[^1].LiveRacks == last.LiveRacks && Math.Abs(back.Stats[^1].Pue - last.Pue) < 0.001, "statistics survive a save");
        Check(back.GameId == s.GameId && !string.IsNullOrEmpty(s.GameId), "the game id (timelapse folder) survives a save");
        Check(GameState.EncodeStats(s.Stats).Length < 40 * 120, "statistics stay compact in the savegame");

        // thinning: daily for 400 days, weekly before, never more than the limit
        var list = new List<StatSample>();
        for (var d = 0; d < 4000; d++) { list.Add(new StatSample { Day = d }); GameState.ThinStats(list); }
        Check(list.Count <= GameState.MaxStats, $"statistics never grow past the limit ({list.Count})");
        Check(list.Count(x => x.Day > 3999 - 400) == 400 && list[0].Day < 1000, "the last 400 days stay daily, the oldest part stays too");

        // charts exist for every figure; money charts are left out with unlimited money
        var charts = ProgressPanel.Charts(s, 30);
        Check(charts.Count >= 10 && charts.All(c => c.series.Length > 0 && c.series[0].Points.Count > 0), "every chart has data");
        var demo = Demo.Build();
        Check(ProgressPanel.Charts(demo, 0).All(c => c.title != Loc.T("pg_money")), "no money chart with unlimited money");

        // ---- achievements ----
        var a = Quiet();
        a.AchievementsDay();
        Check(a.HasAchievement("first_rack") && a.HasAchievement("n_plus_1"), "the demo site earns the first rack and N+1");
        Check(!a.HasAchievement("racks_1000"), "no achievement that was not earned");
        Check(a.Unlock(Achievements.Photo) && !a.Unlock(Achievements.Photo), "an achievement is earned once");
        Check(a.Notes.Count(n => n.key == "msg_achievement") == a.Achieved.Count, "every achievement tells the player");
        var c = Demo.Build();
        c.Money = 5_000_000;
        c.AchievementsDay();
        Check(!c.HasAchievement("millionaire"), "money achievements do not count with unlimited money");
        Check(Achievements.All.Select(x => x.Id).Distinct().Count() == Achievements.All.Length, "achievement ids are unique");
        foreach (var x in Achievements.All) try { x.Check?.Invoke(GameState.New(new NewGameOptions())); } catch (Exception e) { Check(false, $"achievement {x.Id} works on an empty game: {e.Message}"); }

        // ---- scenarios and medals ----
        var ten = Scenarios.ById["ten_mw"];
        Check(Scenarios.MedalFor(ten, 2000) == 0 && Scenarios.MedalFor(ten, 2500) == 1 && Scenarios.MedalFor(ten, 7000) == 2 && Scenarios.MedalFor(ten, 12000) == 3, "medals for more is better");
        var pue = Scenarios.ById["efficient"];
        Check(Scenarios.MedalFor(pue, 1.6) == 0 && Scenarios.MedalFor(pue, 1.4) == 1 && Scenarios.MedalFor(pue, 1.25) == 2 && Scenarios.MedalFor(pue, 1.15) == 3, "medals for lower is better (PUE)");
        Check(ProgressPanel.NextProgress(ten, 1250, 0) is > 0.49 and < 0.51 && ProgressPanel.NextProgress(pue, 1.3, 1) > 0.99, "progress towards the next medal");
        foreach (var d in Scenarios.All)
        {
            var g = Scenarios.Start(d, new NewGameOptions { Events = false, Disasters = false, SupplyDelays = false });
            Check(g.Scenario == d && !g.Options.Creative && g.ScenarioDaysLeft > d.Years * 365 - 1, $"scenario {d.Id} starts as a career game with its deadline");
            for (var day = 0; day < 3; day++) g.Advance(24);
            Check(!g.Bankrupt && g.Stats.Count == 3, $"scenario {d.Id} runs");
        }
        var r = Scenarios.Start(Scenarios.ById["rescue"], new NewGameOptions { Events = false, Disasters = false, SupplyDelays = false });
        Check(r.Loans.Count == 1 && r.Deals.Count(x => x.State == DealState.Active) >= 4 && r.Reputation < 40 && r.Objects.Any(o => o.Broken), "the rescue starts with a loan, unhappy customers and broken machines");
        Check(r.Racks > 90 && r.LiveRacks == r.Racks && r.Has(Exec.Cfo) && r.Has(Exec.Sales), "the rescue site has its racks and managers");
        var money0 = r.Money;
        for (var day = 0; day < 65; day++) r.Advance(24);
        if (OS.HasEnvironment("RESCUE_TRACE"))
            foreach (var b in r.Books) GD.Print($"month {b.Month}: " + string.Join(", ", b.Lines.OrderBy(kv => kv.Value).Select(kv => $"{kv.Key}={kv.Value}")));
        Check(!r.Bankrupt && r.Money > 0 && r.LastMonthProfit < 75_000, $"the rescue is a challenge, not lost or won at once (profit {r.LastMonthProfit}, money {money0} -> {r.Money})");
        // the medal is given once, gold ends the scenario
        var demoRacks = Quiet();
        demoRacks.ScenarioId = "rush"; demoRacks.ScenarioStartDay = demoRacks.Hours / 24; demoRacks.Notes.Clear();
        demoRacks.Advance(24);
        Check(demoRacks.ScenarioMedal == 1 && demoRacks.Notes.Count(n => n.key == "msg_medal") == 1, "bronze when the goal is reached");
        demoRacks.Advance(24);
        Check(demoRacks.Notes.Count(n => n.key == "msg_medal") == 1, "a medal is announced once");
        demoRacks.ScenarioStartDay -= 400; demoRacks.Advance(24);
        Check(demoRacks.ScenarioEnded && demoRacks.Notes.Any(n => n.key == "msg_scenario_over"), "the scenario ends at its deadline");

        // ---- profile over all savegames ----
        var path = "user://test_profile.cfg";
        DirAccess.RemoveAbsolute(path);
        var p = Profile.Load(path);
        Check(p.Earn("first_rack", "2027-01-02") && !p.Earn("first_rack", "2027-02-02"), "the profile remembers an achievement once");
        Check(p.Medal("rush", 1) && p.Medal("rush", 3) && !p.Medal("rush", 2), "the profile keeps the best medal");
        var p2 = Profile.Load(path);
        Check(p2.Has("first_rack") && p2.BestMedal("rush") == 3 && p2.BestMedal("green") == 0, "the profile survives a restart");
        DirAccess.RemoveAbsolute(path);

        // ---- timelapse and photo mode ----
        Check(Timelapse.Due(11.5, 12.25) && !Timelapse.Due(12.25, 13) && !Timelapse.Due(30, 40) && Timelapse.Due(7 * 24 + 11, 7 * 24 + 13), "timelapse pictures at noon every week");
        Check(Timelapse.Due(0, 24 * 30) && !Timelapse.Due(double.NaN, 100), "a skipped week still gives a picture");
        var (eye, target) = Timelapse.Framing(64);
        var (eye2, _) = Timelapse.Framing(256);
        Check(eye.Y > 40 && eye2.DistanceTo(target) > eye.DistanceTo(target) * 2, "the timelapse camera moves back for a bigger campus");
        Check(PhotoMode.FilterValues(5).sat == 0 && PhotoMode.Filters.Length == 7, "photo filters (black and white has no colour)");

        // ---- menu and texts ----
        Check(Sections.All[Sections.Progress].Hotkey == Key.F8 && Sections.All[Sections.Progress].Tabs.All(t => t.Kind == PanelKind.Progress), "Progress is section F8 on the rail");
        var en = Loc.Table["en"];
        var keys = new List<string> { "medal_none", "medal_bronze", "medal_silver", "medal_gold" };
        foreach (var x in Achievements.All) keys.AddRange(new[] { "ach_" + x.Id, "ach_" + x.Id + "_desc" });
        foreach (var x in Scenarios.All) keys.AddRange(new[] { "sc_" + x.Id, "sc_" + x.Id + "_desc", "sc_" + x.Id + "_goal" });
        keys.AddRange(PhotoMode.Filters);
        foreach (var file in new[] { "res://scripts/UI/ProgressPanel.cs", "res://scripts/UI/PhotoMode.cs", "res://scripts/UI/LineChart.cs", "res://scripts/Sim/Scenarios.cs", "res://scripts/Sim/Achievements.cs", "res://scripts/UI/Sections.cs", "res://scripts/UI/Menus.cs", "res://scripts/Main.cs" })
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(FileAccess.GetFileAsString(file), "\"((pg|sc|ach|medal|ph|tl|nav|msg|new|hud)_[a-z0-9_]+)\""))
                if (!m.Groups[1].Value.EndsWith('_')) keys.Add(m.Groups[1].Value);
        var absent = keys.Distinct().Where(k => !en.ContainsKey(k)).ToList();
        Check(absent.Count == 0, "step 11 texts exist: " + string.Join(", ", absent));
    }
}
