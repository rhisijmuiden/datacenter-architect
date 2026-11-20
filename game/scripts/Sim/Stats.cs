using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;

namespace DatacenterArchitect.Sim;

/// <summary>One day of statistics (step 11): what the charts in the Progress screen draw.</summary>
public class StatSample
{
    public int Day;
    public long Money;
    /// <summary>Money in and out that day (from the books), and the monthly value of all running contracts.</summary>
    public long Income, Costs, Revenue;
    public double Pue, Uptime, ItKw, FacilityKw, Gbps, Reputation, Esg;
    public int LiveRacks, Customers, Staff, HostingCustomers;
    public long Profit => Income - Costs;
}

/// <summary>Step 11: statistics through time. Recorded automatically once a game day; stored compactly in the savegame.</summary>
public partial class GameState
{
    /// <summary>At most this many samples are kept: the last 400 days daily, older ones weekly, the oldest monthly.</summary>
    public const int MaxStats = 800;
    const int DailyDays = 400;

    [JsonIgnore] public List<StatSample> Stats { get; private set; } = new();

    /// <summary>Stats as text, one sample per line, so a long game keeps a small savegame.</summary>
    public string StatLog
    {
        get => EncodeStats(Stats);
        set => Stats = DecodeStats(value);
    }

    /// <summary>A unique id per game, for the timelapse folder.</summary>
    public string GameId { get => _gameId ??= Guid.NewGuid().ToString("N")[..12]; set => _gameId = value; }
    string _gameId;

    // uptime of this day, weighted by hours; money in and out of this day
    [JsonIgnore] double _upAcc, _upHours;
    [JsonIgnore] long _dayIn, _dayOut;
    /// <summary>Days in a row with every live rack running and online.</summary>
    public int UptimeStreak { get; set; }

    /// <summary>Called by <see cref="Book"/>: income and costs of today.</summary>
    void LedgerDay(string key, long amount)
    {
        if (key.StartsWith("inc_")) _dayIn += amount;
        else if (key.StartsWith("cost_")) _dayOut -= amount;
    }

    /// <summary>Every tick: how much of the site is up.</summary>
    void StatsTick(double hours)
    {
        var live = LiveRacks;
        if (live <= 0 || hours <= 0) return;
        _upAcc += hours * CurrentUptime();
        _upHours += hours;
    }

    /// <summary>Fraction of live racks that run (power and cooling) and are online (network).</summary>
    public double CurrentUptime()
    {
        var inf = Infra;
        if (inf.LiveRacks <= 0) return 1;
        var running = inf.RunningRacks / (double)inf.LiveRacks;
        if (Creative.NoNetworkLimit) return running;
        return Math.Min(running, Net.OnlineRacks / (double)inf.LiveRacks);
    }

    /// <summary>Once a game day, after the money: one statistics sample, achievements and the scenario.</summary>
    void ProgressDay(int day)
    {
        RecordStats(day);
        AchievementsDay();
        ScenarioDay(day);
    }

    void RecordStats(int day)
    {
        var inf = Infra;
        var uptime = _upHours > 0 ? _upAcc / _upHours : (inf.LiveRacks > 0 ? CurrentUptime() : 1);
        if (inf.LiveRacks > 0 && uptime >= 0.9999) UptimeStreak++;
        else if (inf.LiveRacks > 0) UptimeStreak = 0;
        var s = new StatSample
        {
            Day = day, Money = Money, Income = _dayIn, Costs = _dayOut,
            Revenue = MonthlyRevenue + HostingMonthly,
            Pue = inf.Pue, Uptime = Math.Clamp(uptime, 0, 1), ItKw = inf.ItRunningKw, FacilityKw = inf.FacilityKw,
            Gbps = Net.DemandGbps, Reputation = Reputation, Esg = Esg,
            LiveRacks = inf.LiveRacks, Customers = Deals.Count(d => d.State == DealState.Active),
            Staff = Staff.Count, HostingCustomers = Hosting?.TotalCustomers ?? 0,
        };
        _upAcc = _upHours = 0; _dayIn = _dayOut = 0;
        Stats.RemoveAll(x => x.Day == day);
        Stats.Add(s);
        ThinStats(Stats);
    }

    /// <summary>Keeps the list short: daily for the last 400 days, weekly before that, monthly for the oldest part.</summary>
    public static void ThinStats(List<StatSample> list)
    {
        if (list.Count <= MaxStats) return;
        var last = list[^1].Day;
        list.RemoveAll(x => x.Day < last - DailyDays && x.Day % 7 != 0);
        if (list.Count <= MaxStats) return;
        list.RemoveAll(x => x.Day < last - DailyDays - 7 * 200 && x.Day % 28 != 0);
        while (list.Count > MaxStats) list.RemoveAt(0);
    }

    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string EncodeStats(List<StatSample> list)
    {
        if (list == null || list.Count == 0) return "";
        var sb = new StringBuilder();
        foreach (var s in list)
        {
            sb.Append(Inv, $"{s.Day},{s.Money},{s.Income},{s.Costs},{s.Revenue},{s.Pue:0.###},{s.Uptime:0.#####},{s.ItKw:0.#},{s.FacilityKw:0.#},");
            sb.Append(Inv, $"{s.Gbps:0.##},{s.Reputation:0.#},{s.Esg:0.#},{s.LiveRacks},{s.Customers},{s.Staff},{s.HostingCustomers};");
        }
        return sb.ToString();
    }

    public static List<StatSample> DecodeStats(string text)
    {
        var list = new List<StatSample>();
        if (string.IsNullOrEmpty(text)) return list;
        foreach (var row in text.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var p = row.Split(',');
            if (p.Length < 16) continue;
            try
            {
                list.Add(new StatSample
                {
                    Day = int.Parse(p[0], Inv), Money = long.Parse(p[1], Inv), Income = long.Parse(p[2], Inv), Costs = long.Parse(p[3], Inv), Revenue = long.Parse(p[4], Inv),
                    Pue = double.Parse(p[5], Inv), Uptime = double.Parse(p[6], Inv), ItKw = double.Parse(p[7], Inv), FacilityKw = double.Parse(p[8], Inv),
                    Gbps = double.Parse(p[9], Inv), Reputation = double.Parse(p[10], Inv), Esg = double.Parse(p[11], Inv),
                    LiveRacks = int.Parse(p[12], Inv), Customers = int.Parse(p[13], Inv), Staff = int.Parse(p[14], Inv), HostingCustomers = int.Parse(p[15], Inv),
                });
            }
            catch (FormatException) { }
        }
        return list;
    }

    /// <summary>Samples of the last <paramref name="days"/> days (0 = all).</summary>
    public IEnumerable<StatSample> StatsSince(int days)
    {
        if (Stats.Count == 0) return Stats;
        var from = days <= 0 ? int.MinValue : Stats[^1].Day - days;
        return Stats.Where(s => s.Day > from);
    }

    /// <summary>Profit of the last full month in the books (0 when there is none yet).</summary>
    [JsonIgnore] public long LastMonthProfit
    {
        get
        {
            var last = Books.FirstOrDefault(b => b.Month == MonthIndex - 1);
            return last?.Lines.Where(kv => kv.Key.StartsWith("inc_") || kv.Key.StartsWith("cost_")).Sum(kv => kv.Value) ?? 0;
        }
    }
}
