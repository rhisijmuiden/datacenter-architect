using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace DatacenterArchitect.Sim;

/// <summary>One year of the "Datacenter of the year" award: your score, your place and who won.</summary>
public class AwardResult
{
    public int Year { get; set; }
    /// <summary>1 = winner (gold statuette), 2 and 3 = silver and bronze, 0 = not in the top three.</summary>
    public int Place { get; set; }
    public double Score { get; set; }
    public string Winner { get; set; } = "";
    public double WinnerScore { get; set; }
    public int Entrants { get; set; }
}

/// <summary>
/// Step 11 round 2: every New Year's Day a jury picks the "Datacenter of the year" over the past year. It weighs reputation,
/// uptime, PUE, ESG and growth, against the rivals. A top-three place adds a statuette to the CEO's desk and some reputation.
/// Nothing to manage: run a good datacenter and the jury notices.
/// </summary>
public partial class GameState
{
    /// <summary>Racks a site needs before the jury looks at it.</summary>
    public const int AwardMinRacks = 10;

    public List<AwardResult> Awards { get; set; } = new();
    /// <summary>The calendar year the game was in at the last day (0 = not seen yet).</summary>
    public int AwardYearSeen { get; set; }

    [JsonIgnore] public int AwardWins => Awards.Count(a => a.Place == 1);
    /// <summary>Statuettes on the CEO desk: gold, silver and bronze, oldest first.</summary>
    [JsonIgnore] public IEnumerable<AwardResult> Statuettes => Awards.Where(a => a.Place is >= 1 and <= 3);

    /// <summary>What the jury weighs, with its weight: reputation, uptime, PUE, ESG, growth and size.</summary>
    public static readonly (string key, double weight)[] AwardCriteria =
    {
        ("aw_rep", 0.28), ("aw_uptime", 0.22), ("aw_pue", 0.16), ("aw_esg", 0.12), ("aw_growth", 0.10), ("aw_size", 0.12),
    };

    /// <summary>The jury's marks (0..100 each, in the order of <see cref="AwardCriteria"/>) from the statistics between two days; null when the site is too small.</summary>
    public double[] AwardMarks(int fromDay, int toDay)
    {
        var year = Stats.Where(x => x.Day > fromDay && x.Day <= toDay).ToList();
        if (year.Count == 0) return null;
        var last = year[^1];
        if (last.LiveRacks < AwardMinRacks) return null;
        var live = year.Where(x => x.LiveRacks > 0).ToList();
        var uptime = live.Count == 0 ? 0 : live.Average(x => x.Uptime);
        var pues = live.Where(x => x.Pue > 0).Select(x => x.Pue).ToList();
        var pue = pues.Count == 0 ? 2 : pues.Average();
        var first = year[0];
        var growth = last.LiveRacks <= first.LiveRacks ? 0 : (last.LiveRacks - first.LiveRacks) / (double)Math.Max(20, first.LiveRacks);
        return new[]
        {
            Math.Clamp(last.Reputation, 0, 100),
            Math.Clamp((uptime - 0.95) / 0.05, 0, 1) * 100,
            Math.Clamp((2.0 - pue) / 0.85, 0, 1) * 100,
            Math.Clamp(last.Esg, 0, 100),
            Math.Clamp(growth, 0, 1) * 100,
            Math.Clamp(Math.Log10(Math.Max(1, last.LiveRacks)) / 3.5, 0, 1) * 100,
        };
    }

    /// <summary>The jury's score for your site (0..100), from the statistics between two days.</summary>
    public double AwardScore(int fromDay, int toDay)
    {
        var m = AwardMarks(fromDay, toDay);
        if (m == null) return 0;
        return Math.Round(m.Select((v, i) => v * AwardCriteria[i].weight).Sum(), 1);
    }

    /// <summary>First game day of a calendar year (can be negative before the start).</summary>
    public static int DayOfYearStart(int year) => (int)(new DateTime(year, 1, 1) - Epoch).TotalDays;

    /// <summary>A rival's score: quality and size, with a fixed spread per year so it is the same every time.</summary>
    public static double RivalAwardScore(Competitor c, int year)
    {
        var h = (uint)HashCode.Combine(c.Id, year, c.Name?.Length ?? 0);
        var spread = ((h % 1000) / 1000.0 - 0.5) * 12;
        var size = Math.Clamp(Math.Log10(Math.Max(1, c.CapacityMw * 250)) / 3.5, 0, 1) * 100;
        return Math.Round(Math.Clamp(c.Quality * 0.78 + size * 0.12 + 4 + spread, 0, 100), 1);
    }

    /// <summary>Once a game day: on the first day of a new calendar year the jury judges the year before.</summary>
    internal void AwardsDay(int day)
    {
        Awards ??= new();
        var year = Epoch.AddDays(day).Year;
        if (AwardYearSeen == 0) { AwardYearSeen = year; return; }
        if (year == AwardYearSeen) return;
        var judged = AwardYearSeen;
        AwardYearSeen = year;
        var from = DayOfYearStart(judged);
        // the jury only looks at sites that played most of the year
        var played = Stats.Count(x => x.Day > from && x.Day <= day);
        if (played < 120 || Awards.Any(a => a.Year == judged)) return;
        var mine = AwardScore(from, day);
        if (mine <= 0) return;
        Judge(judged, mine);
    }

    /// <summary>Ranks you against the rivals for a year and hands out the statuette.</summary>
    public AwardResult Judge(int year, double mine)
    {
        var field = Rivals.Select(c => (name: c.Name, score: RivalAwardScore(c, year))).ToList();
        field.Add((Options.Company, mine));
        var ranked = field.OrderByDescending(f => f.score).ThenBy(f => f.name == Options.Company ? 0 : 1).ToList();
        var place = ranked.FindIndex(f => f.name == Options.Company && Math.Abs(f.score - mine) < 1e-9) + 1;
        var r = new AwardResult
        {
            Year = year, Place = place <= 3 ? place : 0, Score = mine,
            Winner = ranked[0].name, WinnerScore = ranked[0].score, Entrants = ranked.Count,
        };
        Awards.Add(r);
        switch (r.Place)
        {
            case 1: Reputation = Math.Min(100, Reputation + 4); Notes.Add(("msg_award_won", year.ToString())); Unlock("dc_of_year"); break;
            case 2: Reputation = Math.Min(100, Reputation + 2); Notes.Add(("msg_award_place", year + "¦medal_silver")); break;
            case 3: Reputation = Math.Min(100, Reputation + 1); Notes.Add(("msg_award_place", year + "¦medal_bronze")); break;
            default: Notes.Add(("msg_award_lost", year + "¦" + r.Winner)); break;
        }
        Touch(r.Place > 0);
        return r;
    }

    /// <summary>The desk the statuettes stand on: the first desk in an executive office (the CEO's), or null.</summary>
    [JsonIgnore] public PlacedObject TrophyDesk => Objects
        .Where(o => o.Kind == ObjKind.Desk && o.Status == ObjStatus.Live && RoomAt(o.X, o.Z, o.Floor)?.Type == RoomType.Executive)
        .OrderBy(o => o.Id).FirstOrDefault();
}
