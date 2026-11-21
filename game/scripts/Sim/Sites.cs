using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace DatacenterArchitect.Sim;

public enum SiteKind { Datacenter, Office }

/// <summary>
/// A country you can expand to: energy price, building cost and demand against the Netherlands, the share of green energy on the grid,
/// water stress (0..1), the months a grid connection takes, the dot on the map and fictional cities with plots.
/// </summary>
public record CountryDef(string Code, string Key, double Energy, double Cost, double Demand, double Green, double Water, int GridDays, float MapX, float MapY, string[] Cities);

/// <summary>Size of a location built from a template: racks, price and build time (in the Netherlands).</summary>
public record SiteSizeDef(int Size, string Key, int Racks, long Cost, int Days, long StaffPerMonth);

/// <summary>
/// Another location (step 9): a datacenter run by its own site team, or an office building you let out.
/// No micromanagement: the local team builds, sells and runs it; you choose where, how big and when to expand.
/// </summary>
public class RemoteSite
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Country { get; set; } = "nl";
    public string City { get; set; } = "";
    public SiteKind Kind { get; set; }
    public int Size { get; set; }
    public int Racks { get; set; }
    public int Occupied { get; set; }
    public bool Built { get; set; }
    public double BuildDaysLeft { get; set; }
    /// <summary>Own fiber to the home campus: geo-redundant services and hosting regions.</summary>
    public bool Linked { get; set; }
    public double OfflineUntil { get; set; }
    public bool Acquired { get; set; }
    public double OpenedDay { get; set; }
    /// <summary>Office buildings: floor area and the share that is let.</summary>
    public int M2 { get; set; }
    public double Let { get; set; }
    /// <summary>Upgrade under way to the next size (racks added when done).</summary>
    public int UpgradeTo { get; set; } = -1;
    [JsonIgnore] public double Fill => Racks <= 0 ? 0 : Occupied / (double)Racks;
}

/// <summary>An office room on your campus let to a tenant: one of your datacenter customers, or another company.</summary>
public class OfficeLease
{
    public int RoomId { get; set; }
    /// <summary>The customer's contract, 0 for another company.</summary>
    public int DealId { get; set; }
    public string Tenant { get; set; } = "";
    public long Monthly { get; set; }
    public double StartDay { get; set; }
}

public static class Countries
{
    public static readonly CountryDef[] All =
    {
        new("nl", "country_nl", 1.0, 1.0, 1.0, 0.35, 0.3, 120, 0.47f, 0.42f, new[] { "Lindedam", "Vlietstad", "Eikenburg", "Waalhoven" }),
        new("be", "country_be", 1.05, 0.95, 0.7, 0.3, 0.3, 100, 0.44f, 0.5f, new[] { "Scheldehaven", "Leiemond" }),
        new("de", "country_de", 1.2, 1.05, 1.3, 0.5, 0.25, 160, 0.57f, 0.46f, new[] { "Mainhaven", "Rheinfurt", "Elbstadt" }),
        new("fr", "country_fr", 0.8, 1.0, 1.0, 0.25, 0.35, 140, 0.4f, 0.62f, new[] { "Seinecourt", "Rhônville" }),
        new("ie", "country_ie", 1.15, 1.1, 1.1, 0.4, 0.15, 200, 0.18f, 0.38f, new[] { "Liffeyford", "Shannonmore" }),
        new("dk", "country_dk", 1.0, 1.1, 0.7, 0.8, 0.15, 90, 0.58f, 0.3f, new[] { "Øresby" }),
        new("se", "country_se", 0.6, 0.95, 0.8, 0.9, 0.1, 80, 0.64f, 0.16f, new[] { "Norrvik", "Lulefjord" }),
        new("es", "country_es", 0.9, 0.85, 0.8, 0.5, 0.8, 120, 0.3f, 0.82f, new[] { "Costamar", "Tajoverde" }),
    };

    public static CountryDef Get(string code) => All.FirstOrDefault(c => c.Code == code) ?? All[0];

    public static readonly SiteSizeDef[] Sizes =
    {
        new(0, "size_edge", 48, 9_000_000, 150, 22_000),
        new(1, "size_medium", 192, 30_000_000, 240, 60_000),
        new(2, "size_large", 768, 105_000_000, 330, 170_000),
        new(3, "size_hyperscale", 1920, 240_000_000, 420, 360_000),
    };

    /// <summary>Office buildings you can buy and let: m² and the price per m².</summary>
    public static readonly int[] OfficeSizes = { 2000, 5000, 12000 };
    public const long OfficePricePerM2 = 2400, OfficeRentPerM2 = 24, OfficeCostPerM2 = 5;
    public const long CountryOfficeCost = 750_000, LinkCost = 400_000, LinkMonthly = 4_000;
    public const long RackPricePerMonth = 820;
    /// <summary>Land you can buy around the campus: the plot grows on the north and west side.</summary>
    public static readonly int[] LandSizes = { 64, 96, 128, 192, 256 };
    public const long LandPricePerM2 = 45;
}

/// <summary>Campus land, other locations and countries, and office rental (step 9).</summary>
public partial class GameState
{
    public List<RemoteSite> Sites { get; set; } = new();
    public List<string> OpenCountries { get; set; } = new();
    public List<OfficeLease> Leases { get; set; } = new();
    /// <summary>Who may rent your empty offices: 0 = customers first, 1 = only customers, 2 = anyone.</summary>
    public int TenantPolicy { get; set; }
    /// <summary>The street pits move along when the plot grows to the west.</summary>
    public int StreetShift { get; set; }
    /// <summary>Raised when land is bought, so the view and the people on site rebuild.</summary>
    public int LandRevision { get; set; }
    [JsonIgnore] public int LastLandShift { get; private set; }

    public const double RemoteRackKw = 4.2;

    public int StreetPit(int i) => Catalog.StreetPits[i] + StreetShift;

    // ---------------- campus land ----------------
    [JsonIgnore] public int NextLandSize => Countries.LandSizes.FirstOrDefault(n => n > PlotSize);
    [JsonIgnore] public long NextLandCost => NextLandSize <= 0 ? 0 : (long)(NextLandSize * NextLandSize - PlotSize * PlotSize) * Countries.LandPricePerM2;

    public string CanBuyLand()
    {
        if (NextLandSize <= 0) return "msg_max_level";
        if (!CanAfford(NextLandCost)) return "msg_no_money";
        return null;
    }

    /// <summary>Buy the land around the campus: the plot grows to the north and west, everything you built keeps its place by the road.</summary>
    public string BuyLand()
    {
        var why = CanBuyLand();
        if (why != null) return why;
        var cost = NextLandCost;
        if (!Creative.UnlimitedMoney) { Money -= cost; Book("cost_land", -cost); }
        Grow(NextLandSize);
        Notes.Add(("msg_land_bought", PlotSize.ToString()));
        AddNews("news_you_land", Options.Company, 3);
        return null;
    }

    /// <summary>Make the plot bigger: shift all content by the growth so the public road stays on the south edge.</summary>
    public void Grow(int size)
    {
        var d = size - PlotSize;
        if (d <= 0) return;
        var old = PlotSize;
        var surf = new byte[size * size]; var fen = new byte[size * size];
        for (var z = 0; z < old; z++)
            for (var x = 0; x < old; x++)
            {
                surf[(z + d) * size + x + d] = Surfaces[z * old + x];
                fen[(z + d) * size + x + d] = Fences[z * old + x];
            }
        Surfaces = surf; Fences = fen;
        foreach (var b in Buildings) { b.X += d; b.Z += d; }
        foreach (var r in Rooms) { r.X += d; r.Z += d; }
        foreach (var o in Doors) { o.X += d; o.Z += d; }
        foreach (var o in Objects) { o.X += d; o.Z += d; }
        foreach (var c in Curves)
        {
            c.Ax += d; c.Az += d; c.Bx += d; c.Bz += d; c.Cx += d; c.Cz += d;
            c.Cells = c.Cells.Select(i => (i / old + d) * size + i % old + d).ToList();
        }
        foreach (var duct in Ducts) duct.Shift(d);
        if (CamTarget is { Length: 3 }) { CamTarget[0] += d; CamTarget[2] += d; }
        PlotSize = size;
        StreetShift += d;
        LastLandShift = d;
        LandRevision++;
        InvalidateInfra();
        Touch();
    }

    // ---------------- countries and other locations ----------------
    public bool CountryOpen(string code) => code == "nl" || OpenCountries.Contains(code);

    public string CanOpenCountry(string code)
    {
        if (CountryOpen(code)) return "msg_nothing_changed";
        if (!Has(Exec.International)) return "msg_needs_international";
        if (!CanAfford(Countries.CountryOfficeCost)) return "msg_no_money";
        return null;
    }

    /// <summary>Open a country office: local permits, a small team and access to the market there.</summary>
    public string OpenCountry(string code)
    {
        var why = CanOpenCountry(code);
        if (why != null) return why;
        if (!Creative.UnlimitedMoney) { Money -= Countries.CountryOfficeCost; Book("cost_sites", -Countries.CountryOfficeCost); }
        OpenCountries.Add(code);
        AddNews("news_you_country", "@" + Countries.Get(code).Key, 3);
        Notes.Add(("msg_country_open", Countries.Get(code).Key));
        Touch(false);
        return null;
    }

    public long SiteCost(string country, int size) => (long)Math.Round(Countries.Sizes[size].Cost * Countries.Get(country).Cost / 10000) * 10000;
    public long OfficeCost(string country, int m2) => (long)Math.Round(m2 * Countries.OfficePricePerM2 * Countries.Get(country).Cost / 10000) * 10000;
    public double SiteBuildDays(string country, int size) => Countries.Sizes[size].Days * (Countries.Get(country).GridDays / 120.0 * 0.4 + 0.6) * (Has(KeyRole.CountryManager) ? 0.7 : 1) * SupplyFactor;

    public string CanBuildSite(string country, int size)
    {
        if (!CountryOpen(country)) return "msg_needs_country";
        if (!Has(Exec.International) && Sites.Count(s => s.Kind == SiteKind.Datacenter) >= 1) return "msg_needs_international";
        if (!Has(Exec.Operations)) return "msg_needs_ops";
        if (size < 0 || size >= Countries.Sizes.Length) return "msg_nothing_changed";
        if (!CanAfford(SiteCost(country, size))) return "msg_no_money";
        return null;
    }

    /// <summary>Open a new datacenter location from a template. The site team builds it, then the local sales fill it.</summary>
    public string BuildSite(string country, int size, string city = null)
    {
        var why = CanBuildSite(country, size);
        if (why != null) return why;
        var cost = SiteCost(country, size);
        if (!Creative.UnlimitedMoney) { Money -= cost; Book("cost_sites", -cost); }
        var def = Countries.Get(country);
        city ??= def.Cities[Sites.Count(s => s.Country == country) % def.Cities.Length];
        var site = new RemoteSite
        {
            Id = NextId++, Name = $"{Options.Company} {city}", Country = country, City = city, Kind = SiteKind.Datacenter, Size = size,
            Racks = Countries.Sizes[size].Racks, Built = Creative.InstantBuild, BuildDaysLeft = Creative.InstantBuild ? 0 : SiteBuildDays(country, size), OpenedDay = Day,
        };
        Sites.Add(site);
        Notes.Add(("msg_site_started", city));
        AddNews("news_you_site", city, 3);
        Touch(false);
        return null;
    }

    public string CanUpgradeSite(RemoteSite s)
    {
        if (s == null || s.Kind != SiteKind.Datacenter) return "msg_nothing_changed";
        if (!s.Built || s.UpgradeTo >= 0) return "msg_upgrade_busy";
        if (s.Size >= Countries.Sizes.Length - 1) return "msg_max_level";
        if (!CanAfford(UpgradeSiteCost(s))) return "msg_no_money";
        return null;
    }

    public long UpgradeSiteCost(RemoteSite s) => s.Size >= Countries.Sizes.Length - 1 ? 0 : SiteCost(s.Country, s.Size + 1) - SiteCost(s.Country, s.Size);

    /// <summary>Expand a location to the next template; it keeps running while the new halls are built.</summary>
    public string UpgradeSite(RemoteSite s)
    {
        var why = CanUpgradeSite(s);
        if (why != null) return why;
        var cost = UpgradeSiteCost(s);
        if (!Creative.UnlimitedMoney) { Money -= cost; Book("cost_sites", -cost); }
        s.UpgradeTo = s.Size + 1;
        s.BuildDaysLeft = Creative.InstantBuild ? 0 : SiteBuildDays(s.Country, s.Size + 1) * 0.5;
        Touch(false);
        return null;
    }

    public string CanLinkSite(RemoteSite s)
    {
        if (s == null || s.Linked || s.Kind != SiteKind.Datacenter) return "msg_nothing_changed";
        if (!Has(Exec.Network)) return "msg_needs_network_mgr";
        if (!CanAfford(Countries.LinkCost)) return "msg_no_money";
        return null;
    }

    /// <summary>Own fiber between the campus and a location: customers can sit in two datacenters (geo-redundancy, +8% price).</summary>
    public string LinkSite(RemoteSite s)
    {
        var why = CanLinkSite(s);
        if (why != null) return why;
        if (!Creative.UnlimitedMoney) { Money -= Countries.LinkCost; Book("cost_sites", -Countries.LinkCost); }
        s.Linked = true; Touch(false);
        return null;
    }

    public string CanBuyOffice(string country, int m2)
    {
        if (!CountryOpen(country)) return "msg_needs_country";
        if (!Has(Exec.OfficeManager)) return "msg_needs_office_mgr";
        if (!CanAfford(OfficeCost(country, m2))) return "msg_no_money";
        return null;
    }

    /// <summary>Buy an office building elsewhere; the office manager lets it to companies.</summary>
    public string BuyOffice(string country, int m2)
    {
        var why = CanBuyOffice(country, m2);
        if (why != null) return why;
        var cost = OfficeCost(country, m2);
        if (!Creative.UnlimitedMoney) { Money -= cost; Book("cost_sites", -cost); }
        var def = Countries.Get(country);
        var city = def.Cities[Sites.Count(s => s.Country == country && s.Kind == SiteKind.Office) % def.Cities.Length];
        Sites.Add(new RemoteSite { Id = NextId++, Name = city, Country = country, City = city, Kind = SiteKind.Office, M2 = m2, Built = true, Let = 0.2, OpenedDay = Day });
        Notes.Add(("msg_office_bought", city));
        Touch(false);
        return null;
    }

    public string SellSite(RemoteSite s)
    {
        if (s == null || !Sites.Contains(s)) return "msg_nothing_changed";
        var value = s.Kind == SiteKind.Office ? OfficeCost(s.Country, s.M2) * 9 / 10 : (long)Math.Round(SiteCost(s.Country, s.Size) * (0.55 + 0.45 * s.Fill));
        if (!s.Built) value = value / 2;
        if (!Creative.UnlimitedMoney) { Money += value; Book("fin_sale", value); }
        Sites.Remove(s);
        Notes.Add(("msg_site_sold", s.City));
        Touch(false);
        return null;
    }

    /// <summary>Price per rack per month at a location: the local market, geo-redundancy and your name.</summary>
    public double SitePrice(RemoteSite s) => Countries.RackPricePerMonth * (0.85 + 0.15 * Countries.Get(s.Country).Demand) * (s.Linked ? 1.08 : 1) * (0.9 + Reputation / 500);

    /// <summary>Money a location brings in and costs per day: (income, costs).</summary>
    public (double income, double cost) SiteDay(RemoteSite s)
    {
        var c = Countries.Get(s.Country);
        if (s.Kind == SiteKind.Office)
            return (s.M2 * s.Let * Countries.OfficeRentPerM2 * (0.8 + 0.2 * c.Demand) * (1 + 0.1 * Training(Exec.OfficeManager)) / 30, s.M2 * Countries.OfficeCostPerM2 / 30.0);
        if (!s.Built) return (0, 0);
        var offline = Day < s.OfflineUntil;
        var income = offline ? 0 : s.Occupied * SitePrice(s) / 30;
        var kwh = s.Occupied * RemoteRackKw * 1.35 * 24;
        var energy = kwh * 0.17 * c.Energy * EnergyIndex * (1 - EnergyDiscount);
        var staff = Countries.Sizes[s.Size].StaffPerMonth / 30.0 + (s.Linked ? Countries.LinkMonthly / 30.0 : 0);
        return (income, energy + staff + s.Racks * 3.0);
    }

    void SitesDay(int day, Random rng)
    {
        foreach (var s in Sites)
        {
            var c = Countries.Get(s.Country);
            if (s.Kind == SiteKind.Office)
            {
                var target = Has(Exec.OfficeManager) ? 0.92 : 0.6;
                s.Let = Math.Clamp(s.Let + (target - s.Let) * 0.02 + (rng.NextDouble() - 0.5) * 0.004, 0, 1);
                continue;
            }
            if (!s.Built || s.UpgradeTo >= 0)
            {
                s.BuildDaysLeft -= 1;
                if (s.BuildDaysLeft <= 0)
                {
                    if (!s.Built) { s.Built = true; Notes.Add(("msg_site_ready", s.City)); AddNews("news_you_site_open", s.City, 3); }
                    else { s.Size = s.UpgradeTo; s.Racks = Countries.Sizes[s.Size].Racks; Notes.Add(("msg_site_expanded", s.City)); }
                    s.UpgradeTo = -1; s.BuildDaysLeft = 0;
                }
                if (!s.Built) continue;
            }
            if (day < s.OfflineUntil) continue;
            // the local sales team fills the site; rivals and a high price slow it down
            var rivals = Options.AiCompetitors ? 1 - 0.1 * Rivals.Count() * (0.5 + 0.25 * Aggression) : 1;
            var rate = 0.006 * c.Demand * (0.6 + Reputation / 100) * Math.Max(0.4, rivals) * (Has(KeyRole.CountryManager) ? 1.25 : 1) * (s.Linked ? 1.1 : 1);
            var free = s.Racks - s.Occupied;
            var add = free * rate * (0.6 + rng.NextDouble() * 0.8);
            var churn = s.Occupied * 0.0006 * (Reputation < 40 ? 3 : 1);
            var n = (int)Math.Floor(add - churn + rng.NextDouble());
            s.Occupied = Math.Clamp(s.Occupied + n, 0, s.Racks);
        }
    }

    /// <summary>Money from all locations per day (to the books).</summary>
    (double income, double cost, double office) SitesMoney()
    {
        double inc = 0, cost = 0, office = 0;
        foreach (var s in Sites)
        {
            var (i, c) = SiteDay(s);
            if (s.Kind == SiteKind.Office) office += i; else inc += i;
            cost += c;
        }
        return (inc, cost, office);
    }

    // ---------------- office rental on the campus ----------------
    /// <summary>Offices can be let with an office manager. Your datacenter customers can rent them right away; other companies need the research Office rental.</summary>
    [JsonIgnore] public bool CanLetToCustomers => Has(Exec.OfficeManager);
    [JsonIgnore] public bool CanLetToOthers => Has(Exec.OfficeManager) && Done("biz_office_rent");
    public const long OfficeRentPerM2 = 22;
    public const double CustomerOfficePremium = 1.15, WholeBuildingPremium = 1.1;

    [JsonIgnore] public IEnumerable<Room> RentableOffices => Rooms.Where(r => r.Type == RoomType.Office && r.Stage == Stage.Done && !Objects.Any(o => o.Kind == ObjKind.Desk && o.Floor == r.Floor && r.Contains(o.X, o.Z)));

    [JsonIgnore] public long OfficeRentPerMonth => Leases.Sum(l => l.Monthly);

    public OfficeLease LeaseOf(Room r) => Leases.FirstOrDefault(l => l.RoomId == r.Id);

    public long Rent(Room r, bool customer) => (long)Math.Round(r.Area * OfficeRentPerM2 * (1 + 0.1 * Training(Exec.OfficeManager)) * (customer ? CustomerOfficePremium : 1) / 10) * 10;

    /// <summary>A whole office building let to one tenant earns a little more.</summary>
    public bool WholeBuilding(OfficeLease l)
    {
        var room = Rooms.FirstOrDefault(r => r.Id == l.RoomId);
        if (room == null) return false;
        var offices = Rooms.Where(r => r.BuildingId == room.BuildingId && r.Type == RoomType.Office).ToList();
        return offices.Count >= 2 && offices.All(r => LeaseOf(r)?.Tenant == l.Tenant);
    }

    public void EndLease(Room r)
    {
        var l = LeaseOf(r);
        if (l == null) return;
        Leases.Remove(l);
        if (l.DealId != 0 && Deals.FirstOrDefault(d => d.Id == l.DealId) is { } d) d.Satisfaction = Math.Max(0, d.Satisfaction - 5);
        Touch(false);
    }

    public void SetTenantPolicy(int p) { TenantPolicy = Math.Clamp(p, 0, 2); Touch(false); }

    /// <summary>The office manager finds tenants for empty offices: customers that already sit in your datacenter first, other companies too with the research.</summary>
    void OfficeDay(int day, Random rng)
    {
        var rentable = RentableOffices.ToList();
        var ids = rentable.Select(r => r.Id).ToHashSet();
        // leases end when the room is used again (a desk), demolished, or the customer leaves
        foreach (var l in Leases.ToList())
        {
            var gone = !ids.Contains(l.RoomId) || !CanLetToCustomers;
            if (!gone && l.DealId != 0) gone = Deals.FirstOrDefault(d => d.Id == l.DealId)?.State != DealState.Active;
            if (!gone && l.DealId == 0) gone = !CanLetToOthers || TenantPolicy == 1;
            if (gone) Leases.Remove(l);
        }
        if (!CanLetToCustomers) return;
        foreach (var r in rentable.Where(r => LeaseOf(r) == null))
        {
            // customers: active contracts without an office yet, bigger ones are more interested
            var customers = Deals.Where(d => d.State == DealState.Active && !d.Legacy && !Leases.Any(l => l.DealId == d.Id)).ToList();
            var wantCustomer = TenantPolicy != 2 || rng.NextDouble() < 0.5;
            if (wantCustomer && customers.Count > 0 && rng.NextDouble() < 0.12 + 0.02 * Training(Exec.OfficeManager))
            {
                var d = customers.OrderByDescending(x => x.Racks + x.Units / Org.UnitsPerRack + rng.NextDouble() * 4).First();
                Leases.Add(new OfficeLease { RoomId = r.Id, DealId = d.Id, Tenant = DisplayArg(d), Monthly = Rent(r, true), StartDay = day });
                d.Satisfaction = Math.Min(100, d.Satisfaction + 5);
                Notes.Add(("msg_office_let_customer", DisplayArg(d)));
                continue;
            }
            if (CanLetToOthers && TenantPolicy != 1 && rng.NextDouble() < 0.08)
            {
                var name = Org.Company(rng);
                Leases.Add(new OfficeLease { RoomId = r.Id, Tenant = name, Monthly = Rent(r, false), StartDay = day });
                Notes.Add(("msg_office_let_other", name));
            }
        }
        // whole buildings let to one tenant: a premium on the rent
        foreach (var l in Leases)
        {
            var room = Rooms.FirstOrDefault(x => x.Id == l.RoomId);
            if (room == null) continue;
            var baseRent = Rent(room, l.DealId != 0);
            l.Monthly = WholeBuilding(l) ? (long)Math.Round(baseRent * WholeBuildingPremium / 10) * 10 : baseRent;
        }
        // tenants that are also customers are a bit happier
        foreach (var l in Leases.Where(l => l.DealId != 0))
            if (Deals.FirstOrDefault(d => d.Id == l.DealId) is { } d) d.Satisfaction = Math.Min(100, d.Satisfaction + 0.05);
    }
}
