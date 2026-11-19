using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace DatacenterArchitect.Sim;

public enum HostProduct { Vps, Dedicated, Gpu, Storage, Backup }

/// <summary>
/// A hosting product: monthly price per customer at the normal price level, customers that fit on one server,
/// what a server costs, daily sign-ups at the start, and whether every customer needs a prepared server from the warehouse.
/// </summary>
public record HostDef(HostProduct Product, string Key, long Price, int PerServer, long ServerCost, double Signups, bool Dedicated, string Research = null);

/// <summary>A big hosting contract that the hosting sales team brings up for you to sign.</summary>
public class HostDeal
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int Servers { get; set; }
    public long Monthly { get; set; }
    public int Months { get; set; }
    public double ExpiresDay { get; set; }
    public double DeadlineDay { get; set; }
    public bool Signed { get; set; }
    public int Delivered { get; set; }
    public double EndDay { get; set; }
}

public class ServerOrder
{
    public int Qty { get; set; }
    public double ArriveDay { get; set; }
    public string Brand { get; set; } = "";
}

/// <summary>The own hosting company (step 9): a second game in the game, without micromanagement.</summary>
public class HostingCompany
{
    public bool Started { get; set; }
    public string Name { get; set; } = "";
    public double StartedDay { get; set; }
    /// <summary>Price level per product: 0 = budget (-20%), 1 = normal, 2 = premium (+25%).</summary>
    public Dictionary<HostProduct, int> PriceLevel { get; set; } = new();
    public Dictionary<HostProduct, int> Customers { get; set; } = new();
    /// <summary>Servers installed per product, and dedicated customers waiting for a server.</summary>
    public Dictionary<HostProduct, int> Servers { get; set; } = new();
    public Dictionary<HostProduct, int> Waiting { get; set; } = new();
    /// <summary>Servers delivered to the warehouse and not yet prepared, and ready ones in stock.</summary>
    public int Unpacked { get; set; }
    public int Stock { get; set; }
    public int MinStock { get; set; } = 10;
    public List<ServerOrder> Orders { get; set; } = new();
    public double Satisfaction { get; set; } = 70;
    public double Uptime { get; set; } = 1;
    public double Tickets { get; set; }
    public int Churned { get; set; }
    public string Campaign { get; set; }
    public double CampaignUntil { get; set; }
    public List<HostDeal> BigDeals { get; set; } = new();
    public List<long> Revenue { get; set; } = new();
    public double NextBigDealDay { get; set; } = 60;
    /// <summary>Servers installed today, for the people on site.</summary>
    public int InstalledToday { get; set; }

    public int Level(HostProduct p) => PriceLevel.TryGetValue(p, out var v) ? v : 1;
    public int Count(Dictionary<HostProduct, int> d, HostProduct p) => d.TryGetValue(p, out var v) ? v : 0;
    [JsonIgnore] public int TotalCustomers => Customers.Values.Sum();
    [JsonIgnore] public int TotalServers => Servers.Values.Sum() + BigDeals.Where(b => b.Signed).Sum(b => b.Delivered);
}

public static class HostingCatalog
{
    public static readonly HostDef[] Products =
    {
        new(HostProduct.Vps, "host_vps", 12, 40, 7000, 1.6, false),
        new(HostProduct.Dedicated, "host_dedicated", 140, 1, 3200, 0.35, true),
        new(HostProduct.Gpu, "host_gpu", 1600, 1, 28000, 0.06, true, "cool_liquid"),
        new(HostProduct.Storage, "host_storage", 18, 25, 9000, 0.8, false),
        new(HostProduct.Backup, "host_backup", 9, 60, 6000, 0.9, false),
    };
    public static HostDef Def(HostProduct p) => Products[(int)p];
    public static readonly double[] PriceFactor = { 0.8, 1.0, 1.25 };
    /// <summary>More customers sign up at a low price, fewer at premium; churn the other way round.</summary>
    public static readonly double[] DemandFactor = { 1.5, 1.0, 0.65 };
    public static readonly double[] ChurnFactor = { 1.4, 1.0, 0.7 };
    public const int ServersPerRack = 20;
    public const long StartCost = 250_000, LicencePerServer = 9;
    public const int ServerLeadDays = 10, PrepPerBenchDay = 4, StockPerPalletRack = 24;
    public static readonly string[] Brands = { "Brikserve", "Nordblade", "Kade Systems", "Tulpcompute" };
    public static readonly (string key, long cost, int days, double boost)[] Campaigns =
    {
        ("camp_black_friday", 60_000, 14, 1.8), ("camp_students", 20_000, 30, 1.3), ("camp_trial", 30_000, 30, 1.5),
    };
}

public partial class GameState
{
    public HostingCompany Hosting { get; set; } = new();

    /// <summary>Racks in halls marked as own cloud that are live and working.</summary>
    [JsonIgnore]
    public int CloudRacks
    {
        get
        {
            var working = WorkingRacks();
            return Objects.Count(o => o.Kind == ObjKind.Rack && working.Contains(o.Id) && HallOf(o)?.Zone == HallZone.OwnCloud);
        }
    }
    [JsonIgnore] public int CloudRacksBuilt => Objects.Count(o => o.Kind == ObjKind.Rack && o.Status == ObjStatus.Live && HallOf(o)?.Zone == HallZone.OwnCloud);
    [JsonIgnore] public int ServerCapacity => CloudRacks * HostingCatalog.ServersPerRack;
    [JsonIgnore] public double HostingKw => Hosting.Started ? Hosting.TotalServers * 0.35 : 0;
    [JsonIgnore] public int PalletRacks => Objects.Count(o => o.Kind == ObjKind.PalletRack && o.Status == ObjStatus.Live);
    [JsonIgnore] public int Workbenches => Objects.Count(o => o.Kind == ObjKind.Workbench && o.Status == ObjStatus.Live);
    /// <summary>Room for servers in the warehouse: pallet racks hold 24 each; a little fits on the floor of a storeroom.</summary>
    [JsonIgnore] public int StockCapacity => PalletRacks * HostingCatalog.StockPerPalletRack + (Rooms.Any(r => r.Type is RoomType.Warehouse or RoomType.Storage && r.Stage == Stage.Done) ? 6 : 0);
    [JsonIgnore] public int WarehouseWorkersWanted => Hosting.Started ? Math.Max(Workbenches > 0 ? 1 : 0, (int)Math.Ceiling(Workbenches / 2.0)) : 0;
    [JsonIgnore] public int HostingEngineersWanted => Hosting.Started ? Math.Max(1, (int)Math.Ceiling(Hosting.TotalCustomers / 600.0 + Hosting.TotalServers / 250.0)) : 0;
    /// <summary>Servers the warehouse team prepares per day: per workbench, with a warehouse worker, faster with pallet jacks.</summary>
    [JsonIgnore]
    public int PrepPerDay
    {
        get
        {
            var benches = Math.Min(Workbenches, Math.Max(1, CountOf(StaffRole.Logistics) * 2));
            if (Workbenches == 0 || CountOf(StaffRole.Logistics) == 0) return 1;
            var jacks = Objects.Any(o => o.Kind == ObjKind.PalletJack) ? 1.25 : 1;
            return (int)Math.Round(benches * HostingCatalog.PrepPerBenchDay * jacks * (Done("bld_logistics") ? 1.15 : 1));
        }
    }

    public long HostPrice(HostProduct p) => (long)Math.Round(HostingCatalog.Def(p).Price * HostingCatalog.PriceFactor[Hosting.Level(p)] * (CampaignOn("camp_black_friday") ? 0.9 : 1));
    bool CampaignOn(string key) => Hosting.Campaign == key && Day < Hosting.CampaignUntil;
    public bool ProductOpen(HostProduct p) => HostingCatalog.Def(p).Research is not { } r || Done(r);

    [JsonIgnore]
    public long HostingMonthly
    {
        get
        {
            if (!Hosting.Started) return 0;
            long sum = 0;
            foreach (var d in HostingCatalog.Products) sum += Hosting.Count(Hosting.Customers, d.Product) * HostPrice(d.Product);
            sum += Hosting.BigDeals.Where(b => b.Signed).Sum(b => b.Servers <= 0 ? 0 : b.Monthly * b.Delivered / b.Servers);
            return (long)Math.Round(sum * Hosting.Uptime);
        }
    }
    [JsonIgnore] public long HostingCostMonthly => Hosting.Started ? Hosting.TotalServers * HostingCatalog.LicencePerServer + 2500 : 0;

    public string CanStartHosting()
    {
        if (Hosting.Started) return "msg_nothing_changed";
        if (!Has(Exec.Hosting)) return "msg_needs_hosting_dir";
        if (!CanAfford(HostingCatalog.StartCost)) return "msg_no_money";
        return null;
    }

    /// <summary>Start the hosting company: a separate (fictional) company under your datacenter, with its own brand.</summary>
    public string StartHosting(string name = null)
    {
        var why = CanStartHosting();
        if (why != null) return why;
        if (!Creative.UnlimitedMoney) { Money -= HostingCatalog.StartCost; Book("cost_hosting", -HostingCatalog.StartCost); }
        Hosting.Started = true; Hosting.StartedDay = Day;
        Hosting.Name = string.IsNullOrWhiteSpace(name) ? Options.Company + " Cloud" : name.Trim();
        Notes.Add(("msg_hosting_started", Hosting.Name));
        AddNews("news_you_hosting", Hosting.Name, 3);
        Touch(false);
        return null;
    }

    public void SetHostPrice(HostProduct p, int level) { Hosting.PriceLevel[p] = Math.Clamp(level, 0, 2); Touch(false); }
    public void SetMinStock(int n) { Hosting.MinStock = Math.Clamp(n, 0, 500); Touch(false); }

    public string CanCampaign(int i)
    {
        if (!Hosting.Started) return "msg_needs_hosting";
        if (Day < Hosting.CampaignUntil) return "msg_campaign_busy";
        if (i < 0 || i >= HostingCatalog.Campaigns.Length) return "msg_nothing_changed";
        if (!CanAfford(HostingCatalog.Campaigns[i].cost)) return "msg_no_money";
        return null;
    }

    public string StartCampaign(int i)
    {
        var why = CanCampaign(i);
        if (why != null) return why;
        var c = HostingCatalog.Campaigns[i];
        if (!Creative.UnlimitedMoney) { Money -= c.cost; Book("cost_hosting", -c.cost); }
        Hosting.Campaign = c.key; Hosting.CampaignUntil = Day + c.days;
        Touch(false);
        return null;
    }

    public string SignHostDeal(HostDeal d)
    {
        if (d == null || d.Signed || !Hosting.BigDeals.Contains(d)) return "msg_nothing_changed";
        d.Signed = true; d.DeadlineDay = Day + 60; d.EndDay = Day + d.Months * 30;
        Notes.Add(("msg_host_deal_signed", d.Name));
        Touch(false);
        return null;
    }

    public void DeclineHostDeal(HostDeal d) { if (d != null && !d.Signed) { Hosting.BigDeals.Remove(d); Touch(false); } }

    public string OrderServers(int qty)
    {
        if (!Hosting.Started) return "msg_needs_hosting";
        var cost = qty * HostingCatalog.Def(HostProduct.Dedicated).ServerCost;
        if (!CanAfford(cost)) return "msg_no_money";
        if (!Creative.UnlimitedMoney) { Money -= cost; Book("cost_hosting", -cost); }
        var lead = Creative.InstantBuild ? 0 : HostingCatalog.ServerLeadDays * (SupplyCrunch && SupplyKind != 1 ? 2 : 1);
        Hosting.Orders.Add(new ServerOrder { Qty = qty, ArriveDay = Math.Floor(Day) + lead, Brand = HostingCatalog.Brands[(Hosting.Orders.Count + qty) % HostingCatalog.Brands.Length] });
        Touch(false);
        return null;
    }

    /// <summary>One day of the hosting company: deliveries, preparing servers, sign-ups and churn, installs, tickets and big contracts.</summary>
    void HostingDay(int day, Random rng)
    {
        var h = Hosting;
        if (!h.Started) return;
        h.InstalledToday = 0;
        // deliveries land in the warehouse; the team unpacks and prepares them at the workbenches
        foreach (var o in h.Orders.Where(o => day >= o.ArriveDay).ToList()) { h.Unpacked += o.Qty; h.Orders.Remove(o); }
        var room = Math.Max(0, StockCapacity - h.Stock);
        var prep = Math.Min(h.Unpacked, Math.Min(PrepPerDay, Math.Max(1, room)));
        h.Unpacked -= prep; h.Stock += prep;
        var capacity = ServerCapacity;
        h.Uptime = CloudRacksBuilt == 0 ? 1 : Math.Clamp(CloudRacks / (double)CloudRacksBuilt, 0, 1);
        var boost = CampaignOn("camp_black_friday") ? 1.8 : CampaignOn("camp_trial") ? 1.5 : 1;
        var growth = (0.6 + Reputation / 80) * (0.5 + h.Satisfaction / 100) * boost * (1 + 0.1 * Sites.Count(s => s.Linked)) * (Has(KeyRole.HostingLead) ? 1.15 : 1) * (Has(Exec.Marketing) ? 1.1 : 1);
        var engineers = CountOf(StaffRole.HostingEngineer);
        foreach (var def in HostingCatalog.Products)
        {
            var p = def.Product;
            if (!ProductOpen(p)) continue;
            var lvl = h.Level(p);
            var cust = h.Count(h.Customers, p);
            var sign = def.Signups * HostingCatalog.DemandFactor[lvl] * growth * (p == HostProduct.Vps && CampaignOn("camp_students") ? 1.3 : 1) * (1 + Math.Min(3, cust / 400.0));
            var churn = cust * 0.0025 * HostingCatalog.ChurnFactor[lvl] * (h.Satisfaction < 50 ? 2.5 : 1) * (h.Uptime < 0.99 ? 2 : 1);
            var add = (int)Math.Floor(sign + rng.NextDouble());
            var lose = (int)Math.Floor(churn + rng.NextDouble());
            h.Churned += Math.Min(lose, cust);
            cust = Math.Max(0, cust - lose);
            if (def.Dedicated)
            {
                // every dedicated customer gets a server from the warehouse, prepared by the team and installed by a hosting engineer
                var wait = h.Count(h.Waiting, p) + add;
                var servers = h.Count(h.Servers, p);
                // servers freed by customers that left are reused first
                var reuse = Math.Min(Math.Max(0, servers - cust), wait);
                cust += reuse; wait -= reuse;
                var free = Math.Max(0, capacity - h.TotalServers);
                var install = Math.Min(wait, Math.Min(h.Stock, Math.Min(free, engineers > 0 ? engineers * 15 : 2)));
                h.Stock -= install; servers += install; cust += install; wait -= install; h.InstalledToday += install;
                // customers that wait too long go elsewhere
                if (wait > 0) { var leave = (int)Math.Ceiling(wait * 0.08); wait -= leave; h.Churned += leave; }
                h.Servers[p] = servers; h.Waiting[p] = wait;
            }
            else
            {
                // shared products: the team adds a server from stock when the ones in use are 80% full
                var servers = h.Count(h.Servers, p);
                cust += add;
                var need = (int)Math.Ceiling(cust / (def.PerServer * 0.8));
                while (servers < need && h.Stock > 0 && h.TotalServers < capacity) { servers++; h.Stock--; h.InstalledToday++; h.Servers[p] = servers; }
                cust = Math.Min(cust, servers * def.PerServer);
                h.Servers[p] = servers;
            }
            h.Customers[p] = cust;
        }
        // big contracts: deliver servers from stock until the order is complete
        foreach (var b in h.BigDeals.Where(b => b.Signed).ToList())
        {
            if (b.Delivered < b.Servers)
            {
                var n = Math.Min(b.Servers - b.Delivered, Math.Min(h.Stock, Math.Max(0, capacity - h.TotalServers)));
                b.Delivered += n; h.Stock -= n; h.InstalledToday += n;
                if (b.Delivered < b.Servers && day >= b.DeadlineDay && day < b.DeadlineDay + 1)
                {
                    Pay("cost_sla", b.Monthly);
                    Reputation = Math.Max(0, Reputation - 3);
                    Notes.Add(("msg_host_deal_late", b.Name));
                }
                else if (b.Delivered >= b.Servers) Notes.Add(("msg_host_deal_done", b.Name));
            }
            if (day >= b.EndDay) { h.BigDeals.Remove(b); Notes.Add(("msg_host_deal_end", b.Name)); }
        }
        h.BigDeals.RemoveAll(b => !b.Signed && day >= b.ExpiresDay);
        if (day >= h.NextBigDealDay && h.TotalCustomers >= 40)
        {
            h.NextBigDealDay = day + 45 + rng.Next(45);
            var servers = 50 * (1 + rng.Next(12));
            var kind = rng.Next(4);
            h.BigDeals.Add(new HostDeal
            {
                Id = NextId++, Name = "@host_big_" + kind + "|" + Org.Company(rng), Servers = servers, Monthly = servers * (180 + rng.Next(80)),
                Months = 24 + 12 * rng.Next(3), ExpiresDay = day + 15,
            });
            Notes.Add(("msg_host_deal_new", servers.ToString()));
        }
        // support tickets: more customers, more tickets; hosting engineers and 24/7 support answer them
        h.Tickets = h.TotalCustomers / 250.0 + (h.Uptime < 0.99 ? 20 : 0);
        var answered = engineers * 12 * (Support247 ? 1.3 : 1);
        var target = answered >= h.Tickets ? 85 : 85 - (h.Tickets - answered) * 4;
        h.Satisfaction = Math.Clamp(h.Satisfaction + (target - h.Satisfaction) * 0.05, 0, 100);
        // automatic re-ordering to the minimum stock
        var onOrder = h.Orders.Sum(o => o.Qty) + h.Unpacked;
        if (h.Stock + onOrder < h.MinStock) OrderServers(Math.Max(10, h.MinStock - h.Stock - onOrder));
        if (day % 30 == 0) { h.Revenue.Add(HostingMonthly); if (h.Revenue.Count > 24) h.Revenue.RemoveAt(0); }
        if (h.TotalServers >= capacity && capacity > 0 && day % 7 == 0) Notes.Add(("msg_hosting_full", null));
        if (capacity == 0 && day % 10 == 0) Notes.Add(("msg_hosting_no_racks", null));
    }
}
