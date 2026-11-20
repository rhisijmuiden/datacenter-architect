using System;
using System.Collections.Generic;
using System.Linq;

namespace DatacenterArchitect.Sim;

/// <summary>
/// One achievement: an id (text keys "ach_{id}" and "ach_{id}_desc"), an icon, and the check on the game.
/// Money achievements do not count with unlimited money. Achievements without a check are unlocked by the game screen
/// (taking a photo, walking around, watching the timelapse).
/// </summary>
public record AchievementDef(string Id, string Icon, Func<GameState, bool> Check, bool Money = false, bool Secret = false);

/// <summary>Step 11: achievements. Checked once a game day; also remembered over all savegames (Core.Profile).</summary>
public static class Achievements
{
    public const string Photo = "photographer", Walk = "tourist", Timelapse = "director_cut";

    public static readonly AchievementDef[] All =
    {
        new("first_rack", "cat_halls", s => s.LiveRacks >= 1),
        new("racks_100", "cat_halls", s => s.LiveRacks >= 100),
        new("racks_1000", "cat_halls", s => s.LiveRacks >= 1000),
        new("racks_5000", "cat_halls", s => s.LiveRacks >= 5000),
        new("mw_1", "bolt", s => s.Infra.ItRunningKw >= 1000),
        new("mw_10", "bolt", s => s.Infra.ItRunningKw >= 10_000),
        new("mw_50", "bolt", s => s.Infra.ItRunningKw >= 50_000),
        new("n_plus_1", "bolt", s => s.Infra.Ats && s.Infra.GenRedundancy is "N+1" or "N+2" or "2N"),
        new("pue_14", "snow", s => s.Infra.LiveRacks >= 50 && s.Infra.Pue > 0 && s.Infra.Pue <= 1.4),
        new("pue_12", "snow", s => s.Infra.LiveRacks >= 200 && s.Infra.Pue > 0 && s.Infra.Pue <= 1.2),
        new("carriers_3", "cat_network", s => s.Net.Carriers.Count(c => c.Up && !c.Def.Ix) >= 3),
        new("carriers_4", "cat_network", s => s.Net.Carriers.Count(c => c.Up && !c.Def.Ix) >= 4),
        new("tbit", "cat_network", s => s.Net.DemandGbps >= 1000),
        new("first_contract", "contract", s => s.Deals.Any(d => d.State == DealState.Active && !d.Legacy)),
        new("clients_25", "cat_customers", s => s.Deals.Count(d => d.State == DealState.Active) >= 25),
        new("clients_100", "cat_customers", s => s.Deals.Count(d => d.State == DealState.Active) >= 100),
        new("millionaire", "money", s => s.Money >= 1_000_000, Money: true),
        new("revenue_1m", "money", s => s.MonthlyRevenue + s.HostingMonthly >= 1_000_000, Money: true),
        new("profit_month", "money", s => s.LastMonthProfit > 0 && s.Stats.Count >= 30, Money: true),
        new("ipo", "chart", s => s.Listed, Money: true),
        new("reputation_90", "star", s => s.Reputation >= 90),
        new("esg_80", "leaf", s => s.Esg >= 80),
        new("first_cert", "certificate", s => s.Certs.Any(c => c.Valid)),
        new("research_10", "flask", s => s.Researched.Count >= 10),
        new("staff_50", "crew", s => s.Staff.Count >= 50),
        new("staff_200", "crew", s => s.Staff.Count >= 200),
        new("second_site", "globe", s => s.Sites.Count >= 1),
        new("countries_3", "globe", s => s.OpenCountries.Count >= 3),
        new("hosting", "cloud", s => s.Hosting.Started),
        new("hosting_10k", "cloud", s => s.Hosting.TotalCustomers >= 10_000),
        new("uptime_30", "check", s => s.UptimeStreak >= 30 && s.LiveRacks >= 20),
        new("uptime_365", "check", s => s.UptimeStreak >= 365 && s.LiveRacks >= 50),
        new("year_1", "dashboard", s => s.Hours >= 365 * 24),
        new("year_10", "dashboard", s => s.Hours >= 3650 * 24),
        new("campus_256", "cat_land", s => s.PlotSize >= 256),
        new(Photo, "camera", null),
        new(Walk, "walk", null),
        new(Timelapse, "film", null),
    };

    public static readonly Dictionary<string, AchievementDef> ById = All.ToDictionary(a => a.Id);

    /// <summary>Whether an achievement can be earned in this game (money achievements need a money game).</summary>
    public static bool Counts(GameState s, AchievementDef a) => !(a.Money && s.Creative.UnlimitedMoney);
}

public partial class GameState
{
    /// <summary>Achievements earned in this game, in the order they came.</summary>
    public List<string> Achieved { get; set; } = new();

    public bool HasAchievement(string id) => Achieved.Contains(id);

    /// <summary>Earn an achievement; returns false when it was already earned, unknown or does not count here.</summary>
    public bool Unlock(string id)
    {
        if (!Achievements.ById.TryGetValue(id, out var def) || Achieved.Contains(id) || !Achievements.Counts(this, def)) return false;
        Achieved.Add(id);
        Notes.Add(("msg_achievement", "ach_" + id));
        return true;
    }

    /// <summary>Checks every achievement that has a rule.</summary>
    public void AchievementsDay()
    {
        Achieved ??= new();
        foreach (var a in Achievements.All)
        {
            if (a.Check == null || Achieved.Contains(a.Id) || !Achievements.Counts(this, a)) continue;
            bool ok;
            try { ok = a.Check(this); } catch (NullReferenceException) { ok = false; }
            if (ok) Unlock(a.Id);
        }
    }
}
