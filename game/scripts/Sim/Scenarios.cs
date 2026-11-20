using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace DatacenterArchitect.Sim;

/// <summary>How a scenario's goal value is shown.</summary>
public enum GoalUnit { Megawatt, Money, Score, Count, Pue }

/// <summary>
/// A scenario (step 11): a challenge with a deadline and bronze, silver and gold thresholds on one measured value.
/// Texts: "sc_{id}", "sc_{id}_desc", "sc_{id}_goal". <see cref="Setup"/> prepares the start (for example a site in trouble).
/// </summary>
public record ScenarioDef(string Id, string Icon, int Years, GoalUnit Unit, double Bronze, double Silver, double Gold, Func<GameState, double> Measure,
    bool LowerIsBetter = false, Func<NewGameOptions, GameState> Build = null, int Difficulty = 1);

public static class Scenarios
{
    public static readonly ScenarioDef[] All =
    {
        new("rush", "fast", 1, GoalUnit.Count, 40, 100, 200, s => s.LiveRacks),
        new("rescue", "money", 3, GoalUnit.Money, 1, 25_000, 75_000, s => s.Stats.Count >= 30 ? s.LastMonthProfit : double.NegativeInfinity, Build: BuildRescue),
        new("ten_mw", "bolt", 5, GoalUnit.Megawatt, 2500, 5000, 10_000, s => s.Infra.ItRunningKw),
        new("green", "leaf", 4, GoalUnit.Score, 50, 70, 85, s => s.Esg),
        new("efficient", "snow", 3, GoalUnit.Pue, 1.45, 1.3, 1.2, s => s.Infra.LiveRacks >= 100 && s.Infra.Pue > 0 ? s.Infra.Pue : 9, LowerIsBetter: true),
    };

    public static readonly Dictionary<string, ScenarioDef> ById = All.ToDictionary(x => x.Id);

    /// <summary>Medal for a value: 0 none, 1 bronze, 2 silver, 3 gold.</summary>
    public static int MedalFor(ScenarioDef d, double v)
    {
        bool Reached(double t) => d.LowerIsBetter ? v <= t : v >= t;
        return Reached(d.Gold) ? 3 : Reached(d.Silver) ? 2 : Reached(d.Bronze) ? 1 : 0;
    }

    public static string MedalKey(int medal) => medal switch { 3 => "medal_gold", 2 => "medal_silver", 1 => "medal_bronze", _ => "medal_none" };

    /// <summary>A new game for a scenario: career mode, the scenario's difficulty, then its own start.</summary>
    public static GameState Start(ScenarioDef d, NewGameOptions o)
    {
        o.Creative = false; o.CreativeOptions = new CreativeOptions(); o.Difficulty = d.Difficulty;
        var s = d.Build?.Invoke(o) ?? GameState.New(o);
        s.ScenarioId = d.Id; s.ScenarioStartDay = s.Hours / 24;
        return s;
    }

    /// <summary>
    /// "Rescue a bankrupt datacenter": the demo campus with its racks, but deep in debt, with unhappy customers on cheap contracts,
    /// a bad name and a few broken machines. The managers are still there.
    /// </summary>
    public static GameState BuildRescue(NewGameOptions options)
    {
        var s = Demo.Build();
        s.Options = options;
        foreach (var o in s.Objects) { o.Status = ObjStatus.Live; o.Installed = s.Hours / 24 - 2900; }
        s.Grandfathered = new List<Exec> { Exec.Ceo, Exec.Hr, Exec.Operations, Exec.Facility, Exec.Network };
        foreach (var e in new[] { Exec.Cfo, Exec.Sales }) s.Execs.Add(new ExecHire { Role = e });
        // the racks are let to a handful of unhappy customers on old, cheap contracts
        var racks = s.Objects.Where(o => o.Kind == ObjKind.Rack).Select(o => o.Id).ToList();
        var types = new[] { CustomerType.Smb, CustomerType.WebHosting, CustomerType.Startup, CustomerType.Hosting, CustomerType.Streaming };
        var names = new[] { "Veenstra Webdiensten", "Kade Hosting", "Grachtsoft", "Deltaweb", "Molenstream" };
        var taken = 0;
        for (var i = 0; i < types.Length && taken < racks.Count * 0.6; i++)
        {
            var n = Math.Min(racks.Count - taken, 6 + i * 3);
            var ids = racks.Skip(taken).Take(n).ToList(); taken += n;
            s.Deals.Add(new Deal
            {
                Id = s.NextId++, Name = names[i], Type = types[i], Product = Product.Rack, Racks = n, RackIds = ids, Gbps = n * 0.2, Carriers = 1,
                Monthly = 600L * n, Months = 12 + i * 6, State = DealState.Active, StartDay = s.Hours / 24 - 300, EndDay = s.Hours / 24 + 60 + i * 120, Satisfaction = 42,
            });
        }
        s.Reputation = 28;
        s.Money = 1_500_000;
        s.Builders = 2; s.Fitters = 1; s.Engineers = 1;
        s.Loans.Add(new Loan { Id = s.NextId++, Principal = 3_000_000, Balance = 2_400_000, Rate = 0.08, MonthsLeft = 60, Payment = 48_700 });
        // years of neglect: worn machines, two of them broken
        foreach (var o in s.Objects.Where(o => o.Kind is ObjKind.Crah or ObjKind.Generator or ObjKind.Ups)) o.Wear = 70;
        foreach (var o in s.Objects.Where(o => o.Kind == ObjKind.Crah).Take(2)) { o.Broken = true; o.BrokenHour = s.Hours; }
        s.Notes.Add(("msg_scenario_rescue", null));
        s.Touch();
        return s;
    }
}

public partial class GameState
{
    /// <summary>The scenario of this game (null = free play), the day it started, the best medal so far and whether it is over.</summary>
    public string ScenarioId { get; set; }
    public double ScenarioStartDay { get; set; }
    public int ScenarioMedal { get; set; }
    public bool ScenarioEnded { get; set; }

    [JsonIgnore] public ScenarioDef Scenario => ScenarioId != null && Scenarios.ById.TryGetValue(ScenarioId, out var d) ? d : null;
    [JsonIgnore] public double ScenarioDaysLeft => Scenario == null ? 0 : Math.Max(0, ScenarioStartDay + Scenario.Years * 365 - Hours / 24);
    [JsonIgnore] public double ScenarioValue => Scenario?.Measure(this) ?? 0;

    void ScenarioDay(int day)
    {
        var d = Scenario;
        if (d == null || ScenarioEnded) return;
        var medal = Scenarios.MedalFor(d, d.Measure(this));
        if (medal > ScenarioMedal)
        {
            ScenarioMedal = medal;
            Notes.Add(("msg_medal", Scenarios.MedalKey(medal) + "¦sc_" + d.Id));
        }
        if (ScenarioMedal == 3) { ScenarioEnded = true; Notes.Add(("msg_scenario_won", "sc_" + d.Id)); return; }
        if (ScenarioDaysLeft <= 0 || Bankrupt)
        {
            ScenarioEnded = true;
            Notes.Add(("msg_scenario_over", Scenarios.MedalKey(ScenarioMedal) + "¦sc_" + d.Id));
        }
    }
}
