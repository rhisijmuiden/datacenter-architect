using System;
using System.Collections.Generic;
using System.Linq;

namespace DatacenterArchitect.Sim;

/// <summary>Step 9 (growth): the daily turn of all growth systems and their lines in the books.</summary>
public partial class GameState
{
    /// <summary>Runs once a game day, after the organisation and before the money.</summary>
    void GrowthDay(int day)
    {
        EnsureCompetitors();
        MarketDay(day);
        EnergyDay(day, GrowthRng(day, 2));
        SitesDay(day, GrowthRng(day, 3));
        OfficeDay(day, GrowthRng(day, 4));
        HostingDay(day, GrowthRng(day, 5));
        KeyDay(day, GrowthRng(day, 6));
        CyberDay(day, GrowthRng(day, 7));
        DisasterDay(day, GrowthRng(day, 8));
        SupplyDay(day, GrowthRng(day, 9));
    }

    /// <summary>Money lines of the growth systems for one day (key, euros).</summary>
    IEnumerable<(string key, double v)> GrowthMoney()
    {
        var (sites, siteCost, offices) = SitesMoney();
        yield return ("inc_sites", sites);
        yield return ("inc_office_property", offices);
        yield return ("cost_sites", -siteCost);
        yield return ("inc_hosting", HostingMonthly / 30.0);
        yield return ("cost_hosting", -HostingCostMonthly / 30.0);
        yield return ("inc_battery", BatteryEarnedDay);
        yield return ("cost_water", -WaterM3Day * WaterPrice);
        yield return ("cost_insurance", -InsurancePerMonth / 30.0);
        yield return ("cost_security_ops", -(Soc247 ? Soc247Fee : 0) / 30.0);
        yield return ("cost_salaries", -KeySalaries / 30.0);
    }

    /// <summary>Once a month: dividends for shareholders on the stock exchange.</summary>
    void GrowthMonthEnd(long lastProfit) => PublicDividend(lastProfit);

    /// <summary>The growth systems in an older savegame.</summary>
    void MigrateGrowth()
    {
        Competitors ??= new(); News ??= new(); Offers ??= new(); PriceHistory ??= new();
        Sites ??= new(); OpenCountries ??= new(); Leases ??= new(); EnergyHistory ??= new(); KeyPeople ??= new();
        Hosting ??= new();
        Hosting.PriceLevel ??= new(); Hosting.Customers ??= new(); Hosting.Servers ??= new(); Hosting.Waiting ??= new();
        Hosting.Orders ??= new(); Hosting.BigDeals ??= new(); Hosting.Revenue ??= new();
        if (DemandMw <= 0) DemandMw = Market.StartDemandMw;
    }
}
