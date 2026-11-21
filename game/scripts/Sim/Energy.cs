using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace DatacenterArchitect.Sim;

/// <summary>One line of the ESG score: points earned and the maximum.</summary>
public record EsgLine(string Key, double Points, double Max);

/// <summary>Dynamic energy prices, own solar and wind, battery trading, water use and the ESG score (step 9).</summary>
public partial class GameState
{
    /// <summary>Slow movement of the energy market (mean reverting) and a price shock with its end day.</summary>
    public double EnergyDrift { get; set; }
    public double SpikeFactor { get; set; } = 1;
    public double SpikeUntil { get; set; }
    /// <summary>Old switch (before save format 14) for battery trading; read once by MigrateOwnEnergy, now BatteryUse.</summary>
    public bool BatteryTrading { get; set; } = true;
    /// <summary>Yesterday's results, for the screens.</summary>
    public double SolarKwhDay { get; set; }
    public double WindKwhDay { get; set; }
    public double BatteryEarnedDay { get; set; }
    public double WaterM3Day { get; set; }
    public double EsgScore { get; set; } = -1;
    public List<double> EnergyHistory { get; set; } = new();

    public const double SolarKwp = 40, WindKw = 250, ContainerKwh = 2000;
    public const double WaterPerKwh = 1.8, WaterPrice = 2.0, SpotBase = 0.16;

    /// <summary>Hourly shape of the market price over a day; it averages 1. Cheap at night and midday (solar), expensive in the evening.</summary>
    public static readonly double[] HourShape =
    {
        0.74, 0.70, 0.68, 0.68, 0.70, 0.78, 0.95, 1.18, 1.25, 1.12, 0.98, 0.88,
        0.82, 0.80, 0.84, 0.94, 1.08, 1.30, 1.42, 1.38, 1.22, 1.06, 0.92, 0.80,
    };
    static readonly double ShapeMean = HourShape.Average();

    /// <summary>Market index for today (1 = normal). Without dynamic prices it follows the gentle wave of earlier versions.</summary>
    [JsonIgnore]
    public double EnergyIndex
    {
        get
        {
            var day = Hours / 24;
            if (!Options.DynamicPrices) return 1 + 0.15 * Math.Sin(day / 9) + 0.05 * Math.Sin(day / 2.3);
            var season = 1 + 0.12 * Math.Cos(2 * Math.PI * (Date.DayOfYear - 15) / 365.0);
            var spike = day < SpikeUntil ? SpikeFactor : 1;
            return Math.Max(0.4, season * (1 + EnergyDrift) * spike);
        }
    }

    /// <summary>Market price per kWh at an hour of today.</summary>
    public double SpotPrice(int hour) => SpotBase * EnergyIndex * (Options.DynamicPrices ? HourShape[Math.Clamp(hour, 0, 23)] / ShapeMean : 1);
    [JsonIgnore] public double SpotNow => SpotPrice((int)(Hours % 24));

    [JsonIgnore] public int SolarPanels => Objects.Count(o => o.Kind == ObjKind.SolarPanel && o.Status == ObjStatus.Live && !o.Broken);
    [JsonIgnore] public int WindTurbines => Objects.Count(o => o.Kind == ObjKind.WindTurbine && o.Status == ObjStatus.Live && !o.Broken);
    [JsonIgnore] public double TradeKwh => Objects.Count(o => o.Kind == ObjKind.BatteryContainer && o.Status == ObjStatus.Live && !o.Broken) * ContainerKwh * BatteryFactor;

    /// <summary>Sun hours per day by month (Dutch climate) and wind capacity factor (more in winter).</summary>
    static readonly double[] SunHours = { 0.7, 1.3, 2.4, 3.6, 4.6, 5.0, 4.8, 4.2, 3.0, 1.9, 0.9, 0.6 };
    public double SolarKwh(int month) => SolarPanels * SolarKwp * SunHours[Math.Clamp(month - 1, 0, 11)] * 0.85 * SunFactor;
    public double WindKwh(int month) => WindTurbines * WindKw * 24 * (0.24 + 0.1 * Math.Cos(2 * Math.PI * (month - 1) / 12.0));

    /// <summary>Share of the energy you use that is your own (solar, wind and battery), over 30 days. A green supplier no longer counts.</summary>
    [JsonIgnore] public double RenewableShare => OwnShare30;

    /// <summary>Green energy for the ESG score: your own power, or a green supplier contract.</summary>
    [JsonIgnore] public double EsgRenewable => Catalog.Suppliers[Supplier].Green ? 1 : RenewableShare;

    /// <summary>Holds the green datacenter label (customers that want green energy look at the label).</summary>
    [JsonIgnore] public bool GreenPower => GreenLabel;

    /// <summary>Battery trading per day: buy the cheapest hours, sell the dearest, with 90% round-trip efficiency.</summary>
    public double BatteryProfit()
    {
        if (BatteryUse == BatteryMode.Own || TradeKwh <= 0) return 0;
        var prices = Enumerable.Range(0, 24).Select(SpotPrice).OrderBy(p => p).ToList();
        var cheap = prices.Take(4).Average(); var dear = prices.Skip(20).Average();
        var spread = Math.Max(0, dear * 0.9 - cheap);
        if (!Options.DynamicPrices) spread = SpotBase * 0.08;
        // smart: only the part of the battery your own surplus did not need goes to the market
        var free = BatteryUse == BatteryMode.Smart ? Math.Clamp(1 - (LastEnergy?.Charged ?? 0) / TradeKwh, 0, 1) : 1;
        return TradeKwh * free * spread * (Done("pwr_peak") ? 1.3 : 1) * (Has(KeyRole.EnergyTrader) ? 1.25 : 1);
    }

    /// <summary>Energy cost of the day: grid power bought minus surplus sold, from the hourly balance (SimulateEnergyDay).</summary>
    double EnergyCostDay(int day)
    {
        var rec = LastEnergy;
        if (rec == null || rec.Day != day) rec = SimulateEnergyDay(day);
        return rec.GridEur - rec.SoldEur;
    }

    /// <summary>Cooling towers evaporate water (about 1.8 litres per kWh of heat they reject); chillers and dry coolers use none.</summary>
    [JsonIgnore]
    public double WaterM3PerDay
    {
        get
        {
            var inf = Infra;
            double towers = 0, all = 0;
            foreach (var o in Objects.Where(o => o.Def.RejectKw > 0 && o.Status == ObjStatus.Live && !o.Broken))
            {
                var kw = o.Def.RejectKw * Catalog.LevelFactor(o.Level);
                all += kw; if (o.Kind == ObjKind.CoolingTower) towers += kw;
            }
            if (all <= 0) return 0;
            return inf.HeatKw * 24 * towers / all * WaterPerKwh / 1000 * (Heatwave ? 1.3 : 1) * (Done("cool_free") ? 0.8 : 1);
        }
    }

    /// <summary>Water usage effectiveness: litres per kWh of IT energy.</summary>
    [JsonIgnore] public double Wue => Infra.ItRunningKw <= 0 ? 0 : WaterM3PerDay * 1000 / (Infra.ItRunningKw * 24);

    public List<EsgLine> EsgLines()
    {
        var inf = Infra;
        var pue = inf.Pue > 0 ? inf.Pue : 1.6;
        var list = new List<EsgLine>
        {
            new("esg_renewable", Math.Round(30 * EsgRenewable), 30),
            new("esg_pue", Math.Round(Math.Clamp((2.0 - pue) / 0.8, 0, 1) * 25), 25),
            new("esg_water", Math.Round(Math.Clamp(1 - Wue / 1.8, 0, 1) * 15), 15),
            new("esg_heat", Done("cool_heat") ? 10 : 0, 10),
            new("esg_waste", Objects.Any(o => o.Kind == ObjKind.Shredder && o.Status == ObjStatus.Live) ? 5 : 0, 5),
            new("esg_team", (Has(Exec.Sustainability) ? 6 + 2 * Training(Exec.Sustainability) : 0) + (Done("biz_esg") ? 3 : 0), 13),
            new("esg_lead", Has(KeyRole.Sustainability) ? 2 : 0, 2),
        };
        return list;
    }

    /// <summary>ESG score (0..100), refreshed once a day.</summary>
    [JsonIgnore] public double Esg => EsgScore >= 0 ? EsgScore : EsgLines().Sum(l => l.Points);

    void EnergyDay(int day, Random rng)
    {
        if (Options.DynamicPrices)
        {
            EnergyDrift = Math.Clamp(EnergyDrift * 0.97 + (rng.NextDouble() - 0.5) * 0.04, -0.3, 0.4);
            if (day >= SpikeUntil && rng.NextDouble() < 1 / 120.0)
            {
                var cheap = rng.NextDouble() < 0.3;
                SpikeFactor = cheap ? 0.7 : 1.4 + rng.NextDouble() * 0.8;
                SpikeUntil = day + 3 + rng.Next(10);
                AddNews(cheap ? "news_energy_cheap" : "news_energy_spike", ((int)Math.Round((SpikeFactor - 1) * 100)).ToString(), 1);
                Notes.Add((cheap ? "msg_energy_cheap" : "msg_energy_spike", null));
            }
        }
        if (LastEnergy?.Day != day) SimulateEnergyDay(day);
        GreenDay(day);
        EnergyHistory.Add(Math.Round(SpotBase * EnergyIndex * 1000) / 1000);
        if (EnergyHistory.Count > 60) EnergyHistory.RemoveAt(0);
        WaterM3Day = WaterM3PerDay;
        BatteryEarnedDay = BatteryProfit();
        EsgScore = Math.Clamp(EsgLines().Sum(l => l.Points), 0, 100);
        // a good ESG name helps a little; a bad one hurts with the bigger customers
        if (EsgScore >= 70) Reputation = Math.Min(100, Reputation + 0.02);
        else if (EsgScore < 25 && Infra.LiveRacks > 100) Reputation = Math.Max(0, Reputation - 0.01);
    }
}
