using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace DatacenterArchitect.Sim;

/// <summary>What the battery containers do: store your own surplus, trade on the market, or both (own surplus first).</summary>
public enum BatteryMode { Own, Trade, Smart }

/// <summary>One hour of the energy balance (kW averaged over the hour = kWh).</summary>
public class EnergyHour
{
    public double Use { get; set; }
    public double Solar { get; set; }
    public double Wind { get; set; }
    /// <summary>Own power used straight away.</summary>
    public double Direct { get; set; }
    public double Charged { get; set; }
    /// <summary>Own power that came back out of the battery.</summary>
    public double Discharged { get; set; }
    public double Sold { get; set; }
    public double Curtailed { get; set; }
    public double Grid { get; set; }
    /// <summary>Battery charge at the end of the hour (0..1).</summary>
    public double Soc { get; set; }
    [JsonIgnore] public double Own => Solar + Wind;
}

/// <summary>One game day of the energy balance, in kWh and euros.</summary>
public class EnergyDayRecord
{
    public int Day { get; set; }
    public double Use { get; set; }
    public double Solar { get; set; }
    public double Wind { get; set; }
    public double Direct { get; set; }
    public double Charged { get; set; }
    public double Discharged { get; set; }
    public double Sold { get; set; }
    public double Curtailed { get; set; }
    public double Grid { get; set; }
    public double SoldEur { get; set; }
    public double GridEur { get; set; }
    [JsonIgnore] public double Own => Solar + Wind;
    /// <summary>Part of what you used that was your own power (straight away or out of the battery).</summary>
    [JsonIgnore] public double OwnShare => Use <= 0 ? 0 : Math.Min(1, (Direct + Discharged) / Use);
}

/// <summary>
/// Own energy (Reinder, 2026-11-21): solar and wind feed your own site first, every hour. Surplus charges the battery,
/// then is sold to the grid (up to the grid connection). The share of own power over 30 days earns the green label.
/// </summary>
public partial class GameState
{
    public BatteryMode BatteryUse { get; set; } = BatteryMode.Own;
    /// <summary>Part of the battery kept charged for a grid outage; own use does not go below it.</summary>
    public double BatteryReserve { get; set; } = 0.2;
    /// <summary>Sell surplus to the grid; off = the surplus is switched off.</summary>
    public bool SellSurplus { get; set; } = true;
    /// <summary>Energy in the battery containers (kWh).</summary>
    public double BatterySoc { get; set; }
    /// <summary>Daily energy balance, last year.</summary>
    public List<EnergyDayRecord> EnergyLog { get; set; } = new();
    /// <summary>The 24 hours of the last full day, for the chart.</summary>
    public List<EnergyHour> EnergyHours { get; set; } = new();
    /// <summary>Day of the nomination as green datacenter (-1 = not yet), and as Green Gold.</summary>
    public int GreenNominatedDay { get; set; } = -1;
    public int GoldNominatedDay { get; set; } = -1;
    /// <summary>Days in a row under the threshold while holding the label (grace period).</summary>
    public int GreenBelowDays { get; set; }
    public int GoldBelowDays { get; set; }

    public const double GreenThreshold = 0.5, GoldThreshold = 0.8, GreenPue = 1.45, GoldPue = 1.30;
    public const int GreenWindow = 30, GreenGraceDays = 60, GreenMinDays = 7;
    /// <summary>Own power sells at this part of the market price.</summary>
    public const double SellFactor = 0.6, BatteryEfficiency = 0.9;

    /// <summary>Storage of the battery containers (kWh); they charge or discharge a quarter of it per hour.</summary>
    [JsonIgnore] public double BatteryKwh => TradeKwh;
    [JsonIgnore] public double BatteryPowerKw => BatteryKwh / 4;
    [JsonIgnore] public double SolarKwpTotal => SolarPanels * SolarKwp;
    [JsonIgnore] public double WindKwTotal => WindTurbines * WindKw;
    [JsonIgnore] public EnergyDayRecord LastEnergy => EnergyLog.Count == 0 ? null : EnergyLog[^1];

    /// <summary>Own share of energy use over the last 30 days (weighted by use). Before there is data: a typical-month estimate.</summary>
    [JsonIgnore]
    public double OwnShare30
    {
        get
        {
            var recent = EnergyLog.Skip(Math.Max(0, EnergyLog.Count - GreenWindow)).ToList();
            var use = recent.Sum(r => r.Use);
            if (recent.Count == 0 || use <= 0) return TypicalOwnShare;
            return Math.Min(1, recent.Sum(r => r.Direct + r.Discharged) / use);
        }
    }

    /// <summary>Rough share from the monthly averages, used only until the first day has been measured.</summary>
    [JsonIgnore]
    public double TypicalOwnShare
    {
        get
        {
            var use = Infra.FacilityKw * 24;
            if (use <= 0) return 0;
            return Math.Min(1, (SolarKwh(Date.Month) + WindKwh(Date.Month)) / use);
        }
    }

    [JsonIgnore] public bool HasEnergyData => EnergyLog.Count >= GreenMinDays;
    /// <summary>Holds the green datacenter label (Green or Green Gold, not suspended).</summary>
    [JsonIgnore] public bool GreenLabel => HasCert(CertKind.Green) || HasCert(CertKind.GreenGold);
    [JsonIgnore] public bool GoldLabel => HasCert(CertKind.GreenGold);

    /// <summary>How much of the day's sun falls in each hour (weights add up to 1), from the sun's height.</summary>
    static double[] SunShape(DateTime dayStart)
    {
        var w = new double[24];
        for (var h = 0; h < 24; h++) w[h] = Math.Max(0, Math.Sin(Climate.SunElevation(dayStart.AddHours(h + 0.5)) * Math.PI / 180));
        var sum = w.Sum();
        if (sum > 0) for (var h = 0; h < 24; h++) w[h] /= sum;
        return w;
    }

    static double SunWeather(WeatherKind k) => k switch
    {
        WeatherKind.Clear => 1.2, WeatherKind.Cloudy => 0.65, WeatherKind.Rain => 0.4, WeatherKind.Storm => 0.3, WeatherKind.Snow => 0.3, _ => 0.55,
    };

    static double WindWeather(WeatherKind k) => k switch
    {
        WeatherKind.Clear => 0.8, WeatherKind.Cloudy => 1.0, WeatherKind.Rain => 1.15, WeatherKind.Storm => 1.7, WeatherKind.Snow => 1.0, _ => 0.35,
    };

    /// <summary>
    /// The energy balance of one day, hour by hour: own use first, then the battery, then selling (up to the grid
    /// connection), and the grid for what is left. Stores the hours and a day record; changes the battery charge.
    /// </summary>
    public EnergyDayRecord SimulateEnergyDay(int day)
    {
        var start = Epoch.AddDays(day - 1);
        var month = start.Month;
        var shape = SunShape(start);
        var solarDay = SolarPanels * SolarKwp * SunHours[Math.Clamp(month - 1, 0, 11)] * 0.85;
        var windCf = 0.24 + 0.1 * Math.Cos(2 * Math.PI * (month - 1) / 12.0);
        var use = Infra.FacilityKw;
        var cap = BatteryKwh;
        var store = cap > 0 && BatteryUse != BatteryMode.Trade;
        var pmax = BatteryPowerKw;
        var reserve = Math.Clamp(BatteryReserve, 0, 0.9) * cap;
        BatterySoc = Math.Clamp(BatterySoc, 0, cap);
        var price = EnergyPrice() * (1 - EnergyDiscount);
        var rec = new EnergyDayRecord { Day = day };
        var hours = new List<EnergyHour>(24);
        for (var h = 0; h < 24; h++)
        {
            var at = start.AddHours(h + 0.5);
            var kind = WeatherMatters ? Climate.At(at, Seed).Kind : WeatherKind.Cloudy;
            var sun = solarDay * shape[h] * (WeatherMatters ? SunWeather(kind) : 1);
            // wind wobbles through the day; the weather decides how much there is
            var wobble = 1 + 0.25 * Math.Sin((day * 24 + h) / 3.7 + (Seed & 0xff));
            var wind = WindTurbines * WindKw * windCf * wobble * (WeatherMatters ? WindWeather(kind) : 1);
            var x = new EnergyHour { Use = use, Solar = sun, Wind = wind };
            var own = sun + wind;
            x.Direct = Math.Min(own, use);
            var surplus = own - x.Direct;
            var deficit = use - x.Direct;
            if (store)
            {
                var room = (cap - BatterySoc) / BatteryEfficiency;
                x.Charged = Math.Max(0, Math.Min(Math.Min(surplus, pmax), room));
                BatterySoc += x.Charged * BatteryEfficiency;
                surplus -= x.Charged;
                x.Discharged = Math.Max(0, Math.Min(Math.Min(deficit, pmax), BatterySoc - reserve));
                BatterySoc -= x.Discharged;
                deficit -= x.Discharged;
            }
            if (SellSurplus) { x.Sold = Math.Min(surplus, GridCapacityKw); x.Curtailed = surplus - x.Sold; }
            else x.Curtailed = surplus;
            x.Grid = deficit;
            x.Soc = cap > 0 ? BatterySoc / cap : 0;
            rec.Use += x.Use; rec.Solar += sun; rec.Wind += wind; rec.Direct += x.Direct; rec.Charged += x.Charged;
            rec.Discharged += x.Discharged; rec.Sold += x.Sold; rec.Curtailed += x.Curtailed; rec.Grid += x.Grid;
            rec.SoldEur += x.Sold * SpotPrice(h) * SellFactor;
            rec.GridEur += x.Grid * price;
            hours.Add(x);
        }
        EnergyHours = hours;
        EnergyLog.Add(rec);
        if (EnergyLog.Count > 365) EnergyLog.RemoveAt(0);
        SolarKwhDay = rec.Solar; WindKwhDay = rec.Wind;
        return rec;
    }

    /// <summary>Nomination, grace period and suspension of the green labels, once a day after the balance.</summary>
    internal void GreenDay(int day)
    {
        if (!HasEnergyData) return;
        var share = OwnShare30;
        var full = EnergyLog.Count >= GreenWindow;
        if (GreenNominatedDay < 0 && full && share >= GreenThreshold)
        {
            GreenNominatedDay = day;
            Notes.Add(("msg_green_nominated", Pct(share)));
            AddNews("news_green_nominated", Options.Company, 0);
        }
        if (GoldNominatedDay < 0 && full && share >= GoldThreshold)
        {
            GoldNominatedDay = day;
            Notes.Add(("msg_gold_nominated", Pct(share)));
            AddNews("news_gold_nominated", Options.Company, 0);
        }
        GreenBelowDays = Grace(CertKind.Green, GreenThreshold, GreenBelowDays, share);
        GoldBelowDays = Grace(CertKind.GreenGold, GoldThreshold, GoldBelowDays, share);
    }

    int Grace(CertKind kind, double threshold, int below, double share)
    {
        var c = Cert(kind);
        if (c == null || !c.Valid) return 0;
        var key = CertCatalog.Defs[kind].Key;
        if (share >= threshold)
        {
            if (c.Suspended) { c.Suspended = false; Notes.Add(("msg_green_restored", key)); Log("ev_green_restored", key); }
            return 0;
        }
        below++;
        if (below == 1) Notes.Add(("msg_green_below", $"{key}¦{GreenGraceDays}"));
        if (below == GreenGraceDays / 2) Notes.Add(("msg_green_below", $"{key}¦{GreenGraceDays / 2}"));
        if (below >= GreenGraceDays && !c.Suspended)
        {
            c.Suspended = true;
            Notes.Add(("msg_green_suspended", key));
            Log("ev_green_suspended", key);
        }
        return below;
    }

    static string Pct(double v) => $"{Math.Round(v * 100):0}%";

    /// <summary>The certificate checks for the green labels.</summary>
    void GreenChecks(CertKind k, List<Req> list)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var gold = k == CertKind.GreenGold;
        var need = gold ? GoldThreshold : GreenThreshold;
        var share = OwnShare30;
        list.Add(new Req("cert_req_own_energy", Pct(share), Pct(need), HasEnergyData && share >= need - 1e-9 ? ReqStatus.Ok : ReqStatus.Fail));
        list.Add(new Req(gold ? "cert_req_gold_nominated" : "cert_req_nominated", "", "", (gold ? GoldNominatedDay : GreenNominatedDay) >= 0 ? ReqStatus.Ok : ReqStatus.Fail));
        var pue = Infra.Pue;
        var max = gold ? GoldPue : GreenPue;
        list.Add(new Req("cert_req_pue", pue > 0 ? pue.ToString("0.00", inv) : "-", max.ToString("0.00", inv), pue > 0 && pue <= max + 1e-9 ? ReqStatus.Ok : ReqStatus.Fail));
        if (gold) list.Add(new Req("cert_req_green_label", "", "", HasCert(CertKind.Green) ? ReqStatus.Ok : ReqStatus.Fail));
    }

    /// <summary>Old saves: batteries kept trading, the green label is now about your own power.</summary>
    void MigrateOwnEnergy()
    {
        BatteryUse = BatteryTrading ? BatteryMode.Trade : BatteryMode.Own;
        EnergyLog ??= new(); EnergyHours ??= new();
        if (Objects.Any(o => o.Kind is ObjKind.SolarPanel or ObjKind.WindTurbine or ObjKind.BatteryContainer) || Cert(CertKind.Green) != null)
            Notes.Add(("msg_own_energy_new", null));
    }
}
