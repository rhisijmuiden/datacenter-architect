using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace DatacenterArchitect.Sim;

/// <summary>Directors and managers. Each one needs its own furnished office (an executive room with a desk and a chair).</summary>
public enum Exec { Ceo, Cfo, Hr, Sales, Legal, CustomerSuccess, Procurement, Operations, Facility, Network, Security, ProjectManager, Marketing, Compliance, Research, OfficeManager, Hosting, Sustainability, International }

/// <summary>A director or manager: what it needs first, salary per month, one-off hiring fee and two training levels (cost, days).</summary>
public record ExecDef(Exec Role, string Key, Exec[] Requires, long Salary, long HireFee, long[] TrainingCost, int[] TrainingDays);

public class ExecHire
{
    public Exec Role { get; set; }
    /// <summary>Finished training levels (0..2).</summary>
    public int Training { get; set; }
    /// <summary>Training in progress: days left (0 = none).</summary>
    public double TrainingDaysLeft { get; set; }
}

/// <summary>What a data hall is sold as. Products only use halls with the matching zone.</summary>
public enum HallZone { FullRacks, PerU, Cage, PrivateHall, OwnCloud }

/// <summary>What a customer rents.</summary>
public enum Product { PerU, Rack, Cage, Hall }

public enum CustomerType { Startup, Smb, Hosting, WebHosting, Streaming, Social, Crypto, Cloud, Municipality, Government, Bank, Hyperscaler, Ai }

/// <summary>
/// A customer type: products it buys, size range in racks (units for per-U), traffic per rack in Gbit/s, carriers it wants,
/// whether it wants 32 A racks, 2N power, green energy or a security clearance, the reputation it needs, a price factor and a weight for how often it shows up.
/// </summary>
public record CustomerDef(CustomerType Type, string Key, Product[] Products, int Min, int Max, double GbpsPerRack, int Carriers,
    double Amp32, bool TwoN, bool Green, bool Security, int MinReputation, double PriceFactor, double Weight, bool TenderOnly = false);

public enum DealState { Lead, Tender, Bid, Building, Active, Ended, Lost }

/// <summary>A customer: first a lead (or a tender), then a signed contract with racks or units in your halls.</summary>
public class Deal
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    /// <summary>Translated name pattern for public bodies ("cust_municipality" + city); Name holds the argument then.</summary>
    public string NameKey { get; set; }
    public CustomerType Type { get; set; }
    public Product Product { get; set; }
    public bool Tender { get; set; }
    public DealState State { get; set; }
    public int Racks { get; set; }
    public int Units { get; set; }
    /// <summary>32 A racks (7.4 kW) instead of 16 A (3.7 kW): needs an A/B fed hall with hot/cold aisles.</summary>
    public bool Amp32 { get; set; }
    public bool AB { get; set; }
    public bool TwoN { get; set; }
    public double Gbps { get; set; }
    public int Carriers { get; set; }
    public bool Green { get; set; }
    /// <summary>Lost tenders: the rival that won.</summary>
    public string LostTo { get; set; }
    public bool Security { get; set; }
    public long Monthly { get; set; }
    public int Months { get; set; }
    /// <summary>Game day the lead expires or the tender closes.</summary>
    public double ExpiresDay { get; set; }
    /// <summary>Tenders: days to deliver after winning, and the deadline once won.</summary>
    public int DeliveryDays { get; set; }
    public double DeadlineDay { get; set; }
    public double StartDay { get; set; }
    public double EndDay { get; set; }
    /// <summary>Tenders: the bid as a percentage of the asking price (85, 100 or 115).</summary>
    public int BidPercent { get; set; }
    public List<int> RackIds { get; set; } = new();
    /// <summary>Data hall the player placed this contract in (0 = sales picks one). Whole halls and cages keep the hall they got.</summary>
    public int HallId { get; set; }
    public double Satisfaction { get; set; } = 80;
    public double Served { get; set; } = 1;
    public bool Legacy { get; set; }
    public double Kw => Racks * (Amp32 ? Catalog.Amp32Kw : Catalog.Amp16Kw);
}

/// <summary>A loan from the bank, paid back in monthly instalments (annuity).</summary>
public class Loan
{
    public int Id { get; set; }
    public long Principal { get; set; }
    public long Balance { get; set; }
    public double Rate { get; set; }
    public int MonthsLeft { get; set; }
    public long Payment { get; set; }
}

/// <summary>One month of the books: income and costs per category (positive = income).</summary>
public class MonthBook
{
    public int Month { get; set; }
    public Dictionary<string, long> Lines { get; set; } = new();
    public long Total => Lines.Values.Sum();
}

/// <summary>Requirement of a deal against what you built: ok, warning (allowed) or missing.</summary>
public enum ReqStatus { Ok, Warn, Fail }
public record Req(string Key, string Have, string Need, ReqStatus Status);

public static class Org
{
    static readonly Exec[] None = Array.Empty<Exec>();

    public static readonly Dictionary<Exec, ExecDef> Execs = new()
    {
        [Exec.Ceo] = new(Exec.Ceo, "exec_ceo", None, 9000, 15000, new long[] { 40000, 90000 }, new[] { 10, 20 }),
        [Exec.Cfo] = new(Exec.Cfo, "exec_cfo", new[] { Exec.Ceo }, 7500, 10000, new long[] { 25000, 60000 }, new[] { 10, 20 }),
        [Exec.Hr] = new(Exec.Hr, "exec_hr", new[] { Exec.Ceo }, 5500, 8000, new long[] { 20000, 45000 }, new[] { 8, 16 }),
        [Exec.Sales] = new(Exec.Sales, "exec_sales", new[] { Exec.Ceo }, 7000, 10000, new long[] { 30000, 70000 }, new[] { 10, 20 }),
        [Exec.Legal] = new(Exec.Legal, "exec_legal", new[] { Exec.Sales }, 6500, 12000, new long[] { 30000, 65000 }, new[] { 12, 24 }),
        [Exec.CustomerSuccess] = new(Exec.CustomerSuccess, "exec_cs", new[] { Exec.Sales }, 5500, 8000, new long[] { 20000, 45000 }, new[] { 8, 16 }),
        [Exec.Procurement] = new(Exec.Procurement, "exec_procurement", new[] { Exec.Cfo }, 6000, 8000, new long[] { 25000, 55000 }, new[] { 10, 20 }),
        [Exec.Operations] = new(Exec.Operations, "exec_ops", new[] { Exec.Ceo, Exec.Hr }, 7000, 10000, new long[] { 30000, 70000 }, new[] { 10, 20 }),
        [Exec.Facility] = new(Exec.Facility, "exec_facility", new[] { Exec.Operations }, 6500, 9000, new long[] { 60000, 150000 }, new[] { 15, 30 }),
        [Exec.Network] = new(Exec.Network, "exec_network", new[] { Exec.Operations }, 6500, 9000, new long[] { 30000, 70000 }, new[] { 10, 20 }),
        [Exec.Security] = new(Exec.Security, "exec_security", new[] { Exec.Operations }, 5500, 8000, new long[] { 25000, 60000 }, new[] { 10, 20 }),
        [Exec.ProjectManager] = new(Exec.ProjectManager, "exec_pm", new[] { Exec.Operations }, 6000, 8000, new long[] { 25000, 55000 }, new[] { 10, 20 }),
        // step 8: marketing (more leads, good reviews count more), compliance (audits and certificates), R&D (the research tree)
        [Exec.Marketing] = new(Exec.Marketing, "exec_marketing", new[] { Exec.Sales }, 6000, 8000, new long[] { 25000, 55000 }, new[] { 10, 20 }),
        [Exec.Compliance] = new(Exec.Compliance, "exec_compliance", new[] { Exec.Ceo }, 6500, 9000, new long[] { 30000, 60000 }, new[] { 12, 20 }),
        [Exec.Research] = new(Exec.Research, "exec_research", new[] { Exec.Ceo }, 7000, 10000, new long[] { 35000, 80000 }, new[] { 12, 24 }),
        // office manager (Reinder, step 8): runs the offices; with research he rents out office space to other companies
        [Exec.OfficeManager] = new(Exec.OfficeManager, "exec_office", new[] { Exec.Operations }, 5500, 7000, new long[] { 20000, 45000 }, new[] { 8, 16 }),
        // step 9: the hosting director starts the own hosting company, the sustainability manager raises the ESG score,
        // the international director opens other countries and runs the locations there
        [Exec.Hosting] = new(Exec.Hosting, "exec_hosting", new[] { Exec.Ceo, Exec.Sales }, 8000, 12000, new long[] { 35000, 80000 }, new[] { 12, 24 }),
        [Exec.Sustainability] = new(Exec.Sustainability, "exec_sustainability", new[] { Exec.Operations }, 6000, 8000, new long[] { 25000, 55000 }, new[] { 10, 20 }),
        [Exec.International] = new(Exec.International, "exec_international", new[] { Exec.Ceo, Exec.Cfo }, 9000, 15000, new long[] { 40000, 90000 }, new[] { 12, 24 }),
    };

    /// <summary>Order in the organisation chart: CEO, then the directors, then what sits under them.</summary>
    public static readonly Exec[] Chart = { Exec.Ceo, Exec.Cfo, Exec.Procurement, Exec.Hr, Exec.Sales, Exec.Legal, Exec.CustomerSuccess, Exec.Marketing, Exec.Compliance, Exec.Research, Exec.Hosting, Exec.International, Exec.Operations, Exec.Facility, Exec.Network, Exec.Security, Exec.ProjectManager, Exec.OfficeManager, Exec.Sustainability };

    public const long SalesRepSalary = 4200;
    public const int MaxLeads = 8;

    public static readonly Dictionary<CustomerType, CustomerDef> Customers = new()
    {
        [CustomerType.Startup] = new(CustomerType.Startup, "cust_startup", new[] { Product.PerU }, 2, 8, 0.05, 1, 0, false, false, false, 0, 1.0, 3),
        [CustomerType.Smb] = new(CustomerType.Smb, "cust_smb", new[] { Product.PerU, Product.Rack }, 1, 4, 0.1, 1, 0.1, false, false, false, 0, 1.0, 3),
        [CustomerType.Hosting] = new(CustomerType.Hosting, "cust_hosting", new[] { Product.Rack }, 4, 16, 0.4, 2, 0.4, false, false, false, 10, 1.0, 2.5),
        [CustomerType.WebHosting] = new(CustomerType.WebHosting, "cust_webhosting", new[] { Product.Rack }, 2, 10, 0.6, 1, 0.3, false, false, false, 5, 1.05, 2),
        [CustomerType.Streaming] = new(CustomerType.Streaming, "cust_streaming", new[] { Product.Rack, Product.Cage }, 8, 30, 1.5, 2, 0.6, false, false, false, 25, 1.0, 1.5),
        [CustomerType.Social] = new(CustomerType.Social, "cust_social", new[] { Product.Rack, Product.Cage }, 8, 24, 1.0, 2, 0.6, false, false, false, 30, 1.0, 1.2),
        [CustomerType.Crypto] = new(CustomerType.Crypto, "cust_crypto", new[] { Product.Rack, Product.Cage }, 12, 40, 0.02, 1, 1, false, false, false, 0, 0.8, 1.5),
        [CustomerType.Cloud] = new(CustomerType.Cloud, "cust_cloud", new[] { Product.Cage, Product.Hall }, 16, 48, 0.8, 2, 0.8, true, true, false, 40, 1.1, 1),
        [CustomerType.Municipality] = new(CustomerType.Municipality, "cust_municipality", new[] { Product.Rack }, 1, 4, 0.1, 1, 0, false, false, true, 15, 1.2, 1),
        [CustomerType.Government] = new(CustomerType.Government, "cust_government", new[] { Product.Cage }, 6, 20, 0.3, 2, 0.3, true, false, true, 45, 1.3, 0.6),
        [CustomerType.Bank] = new(CustomerType.Bank, "cust_bank", new[] { Product.Cage, Product.Hall }, 24, 80, 0.5, 2, 0.5, true, false, true, 0, 1.25, 1, true),
        [CustomerType.Hyperscaler] = new(CustomerType.Hyperscaler, "cust_hyperscaler", new[] { Product.Hall }, 60, 200, 1.2, 3, 1, true, true, false, 0, 1.0, 1, true),
        // step 8: AI and GPU customers with dense 32 A racks, once liquid cooling is researched
        [CustomerType.Ai] = new(CustomerType.Ai, "cust_ai", new[] { Product.Rack, Product.Cage }, 8, 30, 0.6, 2, 1, false, false, false, 25, 1.3, 1.4),
    };

    /// <summary>List prices per month: per U, per 16 A rack and per 32 A rack. Cages and halls add a premium for the space.</summary>
    public const long PricePerU = 42, PriceRack16 = 780, PriceRack32 = 1250;
    public const double CagePremium = 1.15, HallPremium = 1.1;
    /// <summary>Tenders are priced per kW of IT capacity per month.</summary>
    public const long TenderPerKw = 165;
    public const int UnitsPerRack = 42;
    /// <summary>Per-U customers need more hands-on support: a monthly cost per rented U.</summary>
    public const long SupportPerU = 6;

    static readonly string[] Prefix = { "Tulip", "Polder", "Dijk", "Gracht", "Molen", "Noorder", "Kade", "Duin", "Brug", "Veen", "Haven", "Kroon", "Lindt", "Vliet", "Eiland", "Zilver", "Storm", "Wad" };
    static readonly Dictionary<CustomerType, string[]> Suffix = new()
    {
        [CustomerType.Startup] = new[] { "Labs", "Apps", "Bytes", "Studio" },
        [CustomerType.Smb] = new[] { "IT", "Systems", "Office", "Logistics" },
        [CustomerType.Hosting] = new[] { "Hosting", "Servers", "VPS" },
        [CustomerType.WebHosting] = new[] { "Web", "Sites", "Media" },
        [CustomerType.Streaming] = new[] { "Stream", "Play", "TV" },
        [CustomerType.Social] = new[] { "Social", "Connect", "Chat" },
        [CustomerType.Crypto] = new[] { "Mining", "Hash", "Coin" },
        [CustomerType.Cloud] = new[] { "Cloud", "Compute", "Stack" },
        [CustomerType.Bank] = new[] { "Bank", "Finance", "Capital" },
        [CustomerType.Hyperscaler] = new[] { "Scale", "Global", "Cloud" },
        [CustomerType.Ai] = new[] { "AI", "Neural", "Tensor", "GPU" },
    };
    static readonly string[] Cities = { "Lindedam", "Vlietstad", "Eikenburg", "Waalhoven", "Duinwijk", "Molendorp" };
    static readonly string[] Ministries = { "min_digital", "min_infrastructure", "min_finance", "min_health" };

    public static string City(Random rng) => Cities[rng.Next(Cities.Length)];
    static readonly string[] CompanySuffix = { "Advocaten", "Consultancy", "Software", "Media", "Engineering", "Logistics", "Design", "Finance" };
    /// <summary>A fictional company name, for office tenants and big hosting customers.</summary>
    public static string Company(Random rng) => Prefix[rng.Next(Prefix.Length)] + " " + CompanySuffix[rng.Next(CompanySuffix.Length)];

    public static void Name(Deal d, Random rng)
    {
        switch (d.Type)
        {
            case CustomerType.Municipality: d.NameKey = "cust_name_municipality"; d.Name = Cities[rng.Next(Cities.Length)]; break;
            case CustomerType.Government: d.NameKey = Ministries[rng.Next(Ministries.Length)]; d.Name = ""; break;
            default:
                var suf = Suffix[d.Type];
                d.Name = Prefix[rng.Next(Prefix.Length)] + suf[rng.Next(suf.Length)];
                break;
        }
    }

    public static long ListPrice(Deal d)
    {
        double price = d.Product switch
        {
            Product.PerU => d.Units * PricePerU,
            _ => d.Racks * (d.Amp32 ? PriceRack32 : PriceRack16),
        };
        if (d.Product == Product.Cage) price *= CagePremium;
        if (d.Product == Product.Hall) price *= HallPremium;
        if (d.TwoN) price *= 1.1;
        return (long)Math.Round(price * Customers[d.Type].PriceFactor / 10) * 10;
    }
}

/// <summary>Organisation, customers, contracts and money (step 6).</summary>
public partial class GameState
{
    public List<ExecHire> Execs { get; set; } = new();
    /// <summary>Roles that older savegames get for free so nothing they built stops working.</summary>
    public List<Exec> Grandfathered { get; set; } = new();
    public int SalesReps { get; set; }
    public List<Deal> Deals { get; set; } = new();
    public List<Loan> Loans { get; set; } = new();
    /// <summary>Share of the company owned by investors (0..0.49); they get that share of each month's profit.</summary>
    public double InvestorShare { get; set; }
    public List<MonthBook> Books { get; set; } = new();
    public double Reputation { get; set; } = 30;
    public int Seed { get; set; } = Environment.TickCount & 0xffff;
    public double NextTenderDay { get; set; } = 20;
    /// <summary>Game day the current energy contract started (fixed contracts have a term).</summary>
    public double EnergyStartDay { get; set; }
    public int DaysInRed { get; set; }
    public bool Bankrupt { get; set; }

    [JsonIgnore] public double Day => Hours / 24;

    // ---------------- organisation ----------------
    public ExecHire Hire(Exec e) => Execs.FirstOrDefault(x => x.Role == e);
    public bool Has(Exec e) => Creative.AllUnlocks || Grandfathered.Contains(e) || Hire(e) != null;
    public int Training(Exec e) => Creative.AllUnlocks || Grandfathered.Contains(e) ? 2 : Hire(e)?.Training ?? 0;

    bool Furnished(Room r) => r.Stage == Stage.Done &&
        Objects.Any(o => o.Kind == ObjKind.Desk && o.Floor == r.Floor && r.Contains(o.X, o.Z)) &&
        Objects.Any(o => o.Kind == ObjKind.Chair && o.Floor == r.Floor && r.Contains(o.X, o.Z));

    /// <summary>Executive rooms with at least a desk and a chair: one per director.</summary>
    public int Offices => Rooms.Count(r => r.Type == RoomType.Executive && Furnished(r));
    /// <summary>Workplaces for the sales team: desks in finished office rooms.</summary>
    public int Workplaces => Rooms.Where(r => r.Type == RoomType.Office && r.Stage == Stage.Done)
        .Sum(r => Objects.Count(o => o.Kind == ObjKind.Desk && o.Floor == r.Floor && r.Contains(o.X, o.Z)));

    public string CanHire(Exec e)
    {
        if (Hire(e) != null) return "msg_nothing_changed";
        foreach (var req in Org.Execs[e].Requires)
            if (!Has(req)) return "msg_needs_exec";
        if (Offices <= Execs.Count) return e == Exec.Ceo ? "msg_needs_ceo_office" : "msg_needs_office";
        if (!CanAfford(Org.Execs[e].HireFee)) return "msg_no_money";
        return null;
    }

    public string HireExec(Exec e)
    {
        var why = CanHire(e);
        if (why != null) return why;
        Spend(Org.Execs[e].HireFee);
        Execs.Add(new ExecHire { Role = e });
        Notes.Add(("msg_hired", Org.Execs[e].Key));
        if (e == Exec.Sales && Deals.All(d => d.State != DealState.Lead)) _leadSoon = true;
        Touch(false);
        return null;
    }

    public string CanDismiss(Exec e)
    {
        if (Hire(e) == null) return "msg_nothing_changed";
        if (Execs.Any(x => Org.Execs[x.Role].Requires.Contains(e))) return "msg_exec_needed";
        return null;
    }

    public string DismissExec(Exec e)
    {
        var why = CanDismiss(e);
        if (why != null) return why;
        Execs.RemoveAll(x => x.Role == e);
        if (e == Exec.Sales) SalesReps = 0;
        Touch(false);
        return null;
    }

    public string CanTrain(Exec e)
    {
        var h = Hire(e);
        if (h == null) return "msg_needs_exec";
        if (h.TrainingDaysLeft > 0) return "msg_upgrade_busy";
        if (h.Training >= 2) return "msg_max_level";
        if (!CanAfford(Org.Execs[e].TrainingCost[h.Training])) return "msg_no_money";
        return null;
    }

    public string Train(Exec e)
    {
        var why = CanTrain(e);
        if (why != null) return why;
        var h = Hire(e); var def = Org.Execs[e];
        Spend(def.TrainingCost[h.Training]);
        h.TrainingDaysLeft = Creative.InstantBuild ? 0 : def.TrainingDays[h.Training];
        if (h.TrainingDaysLeft <= 0) FinishTraining(h);
        Touch(false);
        return null;
    }

    void FinishTraining(ExecHire h)
    {
        h.Training++; h.TrainingDaysLeft = 0;
        Notes.Add(("msg_trained", $"{Org.Execs[h.Role].Key}_train_{h.Training}"));
    }

    public string CanAddRep(int delta)
    {
        if (!Has(Exec.Sales)) return "msg_needs_exec";
        if (delta > 0 && SalesReps + delta > Workplaces) return "msg_needs_workplace";
        if (SalesReps + delta < 0) return "msg_nothing_changed";
        return null;
    }

    public string ChangeReps(int delta)
    {
        var why = CanAddRep(delta);
        if (why != null) return why;
        SalesReps += delta; Touch(false);
        return null;
    }

    /// <summary>Hire or let go builders, fitters and engineers. HR hires; technical staff also needs an operations manager.</summary>
    public string ChangeCrew(Role role, int delta)
    {
        if (delta > 0)
        {
            var start = role switch { Role.Builder => 4, Role.Fitter => 2, _ => 1 };
            var now = role switch { Role.Builder => Builders, Role.Fitter => Fitters, _ => Engineers };
            if (now + delta > start)
            {
                if (!Has(Exec.Hr)) return "msg_needs_hr";
                if (role != Role.Builder && !Has(Exec.Operations)) return "msg_needs_ops";
            }
        }
        switch (role)
        {
            case Role.Builder: Builders = Math.Clamp(Builders + delta, 0, 40); break;
            case Role.Fitter: Fitters = Math.Clamp(Fitters + delta, 0, 20); break;
            case Role.Engineer: Engineers = Math.Clamp(Engineers + delta, 0, 20); break;
        }
        Touch(false);
        return null;
    }

    /// <summary>The operations manager makes the crews work faster (10% plus 10% per training level).</summary>
    [JsonIgnore] public double WorkSpeed => (Has(Exec.Operations) && !Grandfathered.Contains(Exec.Operations) ? 1.1 + 0.1 * Training(Exec.Operations) : 1) * BuildSpeed;
    /// <summary>The construction project manager: builders and fitters 15% faster (+10% per training), and fitters place equipment in parallel.</summary>
    [JsonIgnore] public double BuildSpeed => (Has(Exec.ProjectManager) && !Grandfathered.Contains(Exec.ProjectManager) ? 1.15 + 0.1 * Training(Exec.ProjectManager) : 1) * ResearchBuildSpeed;

    [JsonIgnore] public long SalariesPerMonth => Execs.Sum(x => Org.Execs[x.Role].Salary) + SalesReps * Org.SalesRepSalary;

    /// <summary>Grid levels: 10 MVA needs a facility manager, 20 MVA its first and 50 MVA its second training.</summary>
    public string GridUnlock(int level)
    {
        if (level <= 0) return null;
        if (!Has(Exec.Facility)) return "msg_needs_facility";
        if (level - 1 > Training(Exec.Facility)) return "msg_needs_hv_training";
        return null;
    }

    // ---------------- halls and racks for sale ----------------

    public void SetZone(Room r, HallZone z)
    {
        if (r.Type != RoomType.DataHall) return;
        r.Zone = z; Touch(false);
    }

    /// <summary>Choose which customer types a data hall hosts (empty or all types = everyone). Racks already rented stay rented.</summary>
    public void SetHallClients(Room r, IEnumerable<CustomerType> types)
    {
        if (r.Type != RoomType.DataHall) return;
        var list = types.Distinct().OrderBy(t => t).ToList();
        r.Clients = list.Count == Enum.GetValues<CustomerType>().Length ? new List<CustomerType>() : list;
        Touch(false);
    }

    /// <summary>Whether a data hall takes this customer type.</summary>
    public static bool HallTakes(Room r, CustomerType t) => r.Clients == null || r.Clients.Count == 0 || r.Clients.Contains(t);

    /// <summary>Rack id → deal id, for racks rented out.</summary>
    public Dictionary<int, int> RackOwners()
    {
        var map = new Dictionary<int, int>();
        foreach (var d in Deals.Where(d => d.State == DealState.Active))
            foreach (var id in d.RackIds) map[id] = d.Id;
        return map;
    }

    public Room HallOf(PlacedObject o) => Rooms.FirstOrDefault(r => r.Type == RoomType.DataHall && r.Floor == o.Floor && r.Contains(o.X, o.Z));

    /// <summary>Racks that are live, running and online.</summary>
    public HashSet<int> WorkingRacks()
    {
        var inf = Infra; var net = Net;
        return Objects.Where(o => o.Kind == ObjKind.Rack && o.Status == ObjStatus.Live && !o.Broken && !inf.Down.Contains(o.Id) && (Creative.NoNetworkLimit || !net.Offline.Contains(o.Id)))
            .Select(o => o.Id).ToHashSet();
    }

    bool HallFits(Deal d, HallInfo h)
    {
        if ((d.AB || d.Amp32 || d.TwoN) && !(Creative.NoPowerLimit || h.AB || (h.Sides >= 2 && h.ItKw <= 0))) return false;
        if (d.Amp32 && h.Room.Containment == Containment.Open) return false;
        return true;
    }

    public static HallZone ZoneFor(Product p) => p switch { Product.PerU => HallZone.PerU, Product.Cage => HallZone.Cage, Product.Hall => HallZone.PrivateHall, _ => HallZone.FullRacks };

    /// <summary>
    /// Free racks that could go to this deal, grouped by hall (cages and halls must fit in one hall). A deal placed in a hall
    /// by the player only looks there; halls rented whole or reserved for another customer are skipped.
    /// </summary>
    public List<int> FreeRacksFor(Deal d, out int bestHall)
    {
        bestHall = 0;
        var owners = RackOwners();
        var working = WorkingRacks();
        var inf = Infra;
        var zone = ZoneFor(d.Product);
        var pick = new List<int>();
        foreach (var h in inf.Halls.Where(h => h.Room.Stage == Stage.Done && HallTakes(h.Room, d.Type) && HallFits(d, h)))
        {
            if (d.HallId != 0 ? h.Room.Id != d.HallId : h.Room.Zone != zone) continue;
            if (HeldByOther(h.Room, d, owners)) continue;
            var racks = Objects.Where(o => o.Kind == ObjKind.Rack && o.Floor == h.Room.Floor && h.Room.Contains(o.X, o.Z)).ToList();
            // a picked hall of another zone only works while nobody else rents racks there
            if (h.Room.Zone != zone && racks.Any(o => owners.TryGetValue(o.Id, out var id) && id != d.Id)) continue;
            var free = racks.Where(o => !owners.ContainsKey(o.Id) && working.Contains(o.Id)).OrderBy(o => o.Id).Select(o => o.Id).ToList();
            if (d.Product is Product.Cage or Product.Hall)
            {
                if (free.Count > pick.Count) { pick = free; bestHall = h.Room.Id; }
            }
            else pick.AddRange(free);
        }
        return pick;
    }

    /// <summary>Units for sale in per-U halls: 42 U per working rack, minus what is rented.</summary>
    public int FreeUnits(CustomerType? type = null)
    {
        var working = WorkingRacks();
        var owners = RackOwners();
        var racks = Objects.Count(o => o.Kind == ObjKind.Rack && working.Contains(o.Id) && !owners.ContainsKey(o.Id) &&
            HallOf(o) is { Zone: HallZone.PerU } h && (type == null || HallTakes(h, type.Value)));
        return racks * Org.UnitsPerRack - Deals.Where(d => d.State == DealState.Active && d.Product == Product.PerU).Sum(d => d.Units);
    }

    /// <summary>Transit capacity not yet sold to customers.</summary>
    public double FreeGbps(Deal except = null)
    {
        if (Creative.NoNetworkLimit) return double.MaxValue;
        var cap = Net.Carriers.Where(c => c.Up).Sum(c => (double)c.Contract.PortGbps);
        return cap - Deals.Where(d => d.State == DealState.Active && d != except).Sum(d => d.Gbps);
    }

    /// <summary>Each requirement of a deal against what is built now.</summary>
    public List<Req> Check(Deal d)
    {
        var list = new List<Req>();
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        if (d.Product == Product.PerU)
        {
            var free = FreeUnits(d.Type);
            list.Add(new Req("req_units", free.ToString(), d.Units + " U", free >= d.Units ? ReqStatus.Ok : ReqStatus.Fail));
        }
        else
        {
            var free = FreeRacksFor(d, out _).Count;
            list.Add(new Req(d.Product switch { Product.Cage => "req_cage", Product.Hall => "req_hall", _ => "req_racks" }, free.ToString(), d.Racks.ToString(), free >= d.Racks ? ReqStatus.Ok : ReqStatus.Fail));
        }
        var picked = d.HallId != 0 ? Rooms.FirstOrDefault(r => r.Id == d.HallId && r.Type == RoomType.DataHall) : null;
        bool InScope(Room r) => picked != null ? r == picked : r.Zone == ZoneFor(d.Product);
        if (picked != null)
        {
            var problem = HallChoices(d).FirstOrDefault(c => c.Hall == picked)?.Problem;
            list.Add(new Req("req_place", HallCode(picked), "", problem is null or "place_short" ? ReqStatus.Ok : ReqStatus.Fail));
        }
        var zoneHalls = Rooms.Where(r => r.Type == RoomType.DataHall && InScope(r)).ToList();
        if (zoneHalls.Count > 0 && !zoneHalls.Any(r => HallTakes(r, d.Type)))
        {
            list.Add(new Req("req_hall_clients", "", "", ReqStatus.Fail));
        }
        if (d.Amp32 || d.AB || d.TwoN)
        {
            var anyAb = Creative.NoPowerLimit || Infra.Halls.Any(h => InScope(h.Room) && HallTakes(h.Room, d.Type) && HallFits(d, h));
            list.Add(new Req(d.Amp32 ? "req_amp32" : "req_ab", "", "", anyAb ? ReqStatus.Ok : ReqStatus.Fail));
        }
        if (d.TwoN)
        {
            var red = Infra.Link(PowerLink.Ups).Redundancy;
            list.Add(new Req("req_2n", red, "2N", Creative.NoPowerLimit || red == "2N" ? ReqStatus.Ok : ReqStatus.Fail));
        }
        if (d.Product != Product.PerU)
        {
            var gen = Infra.GenRedundancy;
            var ok = Creative.NoPowerLimit || gen is "N+1" or "2N";
            list.Add(new Req("req_backup", gen, "N+1", ok ? ReqStatus.Ok : ReqStatus.Warn));
        }
        if (d.Gbps > 0)
        {
            var free = FreeGbps();
            list.Add(new Req("req_bandwidth", Creative.NoNetworkLimit ? "∞" : Math.Max(0, free).ToString("0.#", inv) + " Gbit/s", d.Gbps.ToString("0.#", inv) + " Gbit/s", free >= d.Gbps - 1e-9 ? ReqStatus.Ok : ReqStatus.Fail));
        }
        if (d.Carriers > 1)
        {
            var n = Creative.NoNetworkLimit ? d.Carriers : Net.Carriers.Count(c => c.Up && !c.Def.Ix);
            list.Add(new Req("req_carriers", n.ToString(), d.Carriers.ToString(), n >= d.Carriers ? ReqStatus.Ok : ReqStatus.Fail));
        }
        if (d.Green)
            list.Add(new Req("req_green", Catalog.Suppliers[Supplier].Green ? Catalog.Suppliers[Supplier].Name : $"{RenewableShare * 100:0}%", "", GreenPower ? ReqStatus.Ok : ReqStatus.Fail));
        if (d.Security)
        {
            var need = SecurityNeed(d.Type);
            var level = SecurityLevel;
            var ok = Has(Exec.Security) && level >= need;
            list.Add(new Req("req_security", level.ToString(), need.ToString(), ok ? ReqStatus.Ok : ReqStatus.Fail));
        }
        foreach (var (cert, must) in CertsWanted(d.Type))
            list.Add(new Req("req_" + CertCatalog.Defs[cert].Key, "", "", HasCert(cert) ? ReqStatus.Ok : must ? ReqStatus.Fail : ReqStatus.Warn));
        return list;
    }

    public bool Meets(Deal d) => Check(d).All(r => r.Status != ReqStatus.Fail);

    public Deal DealById(int id) => Deals.FirstOrDefault(d => d.Id == id);

    public string CanSign(Deal d)
    {
        if (d == null || d.State != DealState.Lead) return "msg_nothing_changed";
        if (!Meets(d)) return "msg_reqs_missing";
        return null;
    }

    public string Sign(Deal d)
    {
        var why = CanSign(d);
        if (why != null) return why;
        Activate(d);
        return null;
    }

    void Activate(Deal d)
    {
        var owners = RackOwners();
        var best = 0;
        d.RackIds = d.Product == Product.PerU ? new List<int>() : FreeRacksFor(d, out best).Take(d.Racks).ToList();
        // cages and whole halls stay in the hall they got; a picked hall becomes the right zone when nobody else rents there
        if (d.Product is Product.Cage or Product.Hall && best != 0) d.HallId = best;
        if (d.HallId != 0 && Rooms.FirstOrDefault(r => r.Id == d.HallId) is { } hall && hall.Zone != ZoneFor(d.Product) && RentedIn(hall, owners) == 0)
            hall.Zone = ZoneFor(d.Product);
        d.State = DealState.Active;
        d.StartDay = Day;
        d.EndDay = Day + d.Months * 30;
        d.Served = 1;
        Notes.Add(("msg_signed", DisplayArg(d)));
        Touch(false);
    }

    /// <summary>Name argument for messages: a company name, or "@key|arg" for a translated public body.</summary>
    public static string DisplayArg(Deal d) => d.NameKey == null ? d.Name : "@" + d.NameKey + "|" + d.Name;

    public void Decline(Deal d)
    {
        if (d == null || d.State is not (DealState.Lead or DealState.Tender)) return;
        Deals.Remove(d); Touch(false);
    }

    /// <summary>Tenders: bid at 85, 100 or 115% of the asking price. Needs a legal counsel.</summary>
    public string CanBid(Deal d)
    {
        if (d == null || d.State != DealState.Tender) return "msg_nothing_changed";
        if (!Has(Exec.Legal)) return "msg_needs_legal";
        return null;
    }

    public string PlaceBid(Deal d, int percent)
    {
        var why = CanBid(d);
        if (why != null) return why;
        d.BidPercent = Math.Clamp(percent, 70, 130);
        d.State = DealState.Bid;
        Touch(false);
        return null;
    }

    /// <summary>End a contract early: the customer leaves, and you pay three months as a fee.</summary>
    public void Terminate(Deal d)
    {
        if (d == null || d.State is not (DealState.Active or DealState.Building)) return;
        if (!Creative.UnlimitedMoney) { Money -= d.Monthly * 3; Book("cost_sla", -d.Monthly * 3); }
        d.State = DealState.Ended; d.RackIds.Clear();
        Reputation = Math.Max(0, Reputation - 3);
        Touch(false);
    }

    // ---------------- finance ----------------
    [JsonIgnore] public long MonthlyRevenue => Deals.Where(d => d.State == DealState.Active).Sum(d => d.Monthly);
    [JsonIgnore] public long Debt => Loans.Sum(l => l.Balance);
    [JsonIgnore] public double LoanRate => Math.Max(0.025, 0.065 - 0.01 * Training(Exec.Cfo) - (Reputation >= 60 ? 0.005 : 0));
    [JsonIgnore] public long CreditLimit => Math.Max(0, 2_000_000 + MonthlyRevenue * 24 - Debt);
    public static readonly long[] LoanSizes = { 1_000_000, 5_000_000, 20_000_000 };
    public const int LoanMonths = 60;

    public static long Annuity(long principal, double yearRate, int months)
    {
        var r = yearRate / 12;
        return (long)Math.Round(r <= 0 ? (double)principal / months : principal * r / (1 - Math.Pow(1 + r, -months)));
    }

    public string CanBorrow(long amount)
    {
        if (!Has(Exec.Cfo)) return "msg_needs_cfo";
        if (amount > CreditLimit) return "msg_credit_limit";
        return null;
    }

    public string Borrow(long amount)
    {
        var why = CanBorrow(amount);
        if (why != null) return why;
        var rate = LoanRate;
        Loans.Add(new Loan { Id = NextId++, Principal = amount, Balance = amount, Rate = rate, MonthsLeft = LoanMonths, Payment = Annuity(amount, rate, LoanMonths) });
        if (!Creative.UnlimitedMoney) { Money += amount; Book("fin_loans", amount); }
        Touch(false);
        return null;
    }

    public string Repay(Loan l)
    {
        if (l == null) return "msg_nothing_changed";
        if (!CanAfford(l.Balance)) return "msg_no_money";
        if (!Creative.UnlimitedMoney) { Money -= l.Balance; Book("fin_repay", -l.Balance); }
        Loans.Remove(l); Touch(false);
        return null;
    }

    /// <summary>What the company is worth to an investor: a base, two and a half years of revenue and what is built.</summary>
    [JsonIgnore] public long Valuation => 4_000_000 + MonthlyRevenue * 30 + Objects.Sum(o => o.Def.Cost) / 2 + Buildings.Sum(b => BuildingCost(b)) / 2;
    public const double InvestorStep = 0.1, MaxInvestorShare = 0.49;
    public long InvestorOffer => (long)(Valuation * InvestorStep * (1 - InvestorShare) / 1000) * 1000;

    public string CanInvest()
    {
        if (!Has(Exec.Cfo)) return "msg_needs_cfo";
        if (InvestorShare + InvestorStep > MaxInvestorShare + 1e-9) return "msg_investors_max";
        return null;
    }

    /// <summary>Sell 10% of the company to an investor. From then on they get their share of each month's profit.</summary>
    public string TakeInvestment()
    {
        var why = CanInvest();
        if (why != null) return why;
        var cash = InvestorOffer;
        InvestorShare += InvestorStep;
        if (!Creative.UnlimitedMoney) { Money += cash; Book("fin_investors", cash); }
        Notes.Add(("msg_investor", null));
        Touch(false);
        return null;
    }

    /// <summary>Fee for leaving a fixed energy contract early: about three months of the current energy bill.</summary>
    public long SwitchFee(int supplier)
    {
        if (supplier == Supplier) return 0;
        var cur = Catalog.Suppliers[Supplier];
        if (cur.Variable || Day - EnergyStartDay < 14 || Day - EnergyStartDay >= cur.Years * 365) return 0;
        return (long)Math.Round(Infra.FacilityKw * 24 * 90 * EnergyPrice());
    }

    [JsonIgnore] public double EnergyDiscount => (Has(Exec.Procurement) ? 0.05 + 0.025 * Training(Exec.Procurement) : 0) + (Done("pwr_peak") ? 0.06 : 0);

    public int MonthIndex => (int)(Day / 30);

    public void Book(string key, long amount)
    {
        if (amount == 0) return;
        LedgerDay(key, amount);
        var m = MonthIndex;
        var b = Books.LastOrDefault();
        if (b == null || b.Month != m) { b = new MonthBook { Month = m }; Books.Add(b); while (Books.Count > 13) Books.RemoveAt(0); }
        b.Lines[key] = (b.Lines.TryGetValue(key, out var v) ? v : 0) + amount;
    }

    // ---------------- the day ----------------
    bool _leadSoon;

    Random DayRng(int day) => new(unchecked(Seed * 7919 + day * 104729));

    /// <summary>Trainings, new leads and tenders, contracts, loans and the books. Runs once per game day.</summary>
    public void OrgDay(int day)
    {
        var rng = DayRng(day);
        foreach (var h in Execs.Where(h => h.TrainingDaysLeft > 0))
        {
            h.TrainingDaysLeft -= 1;
            if (h.TrainingDaysLeft <= 0) FinishTraining(h);
        }
        // leads and tenders expire
        foreach (var d in Deals.Where(d => d.State == DealState.Lead && day >= d.ExpiresDay).ToList()) Deals.Remove(d);
        foreach (var d in Deals.Where(d => d.State == DealState.Tender && day >= d.ExpiresDay).ToList()) Deals.Remove(d);
        foreach (var d in Deals.Where(d => d.State == DealState.Bid && day >= d.ExpiresDay).ToList()) ResolveBid(d, rng);
        if (Has(Exec.Sales)) NewLeads(day, rng);
        if (Has(Exec.Sales) && day >= NextTenderDay)
        {
            NextTenderDay = day + 25 + rng.Next(20);
            NewTender(day, rng);
        }
        foreach (var d in Deals.Where(d => d.State == DealState.Building).ToList())
        {
            if (Meets(d)) { Activate(d); Reputation = Math.Min(100, Reputation + 3); Notes.Add(("msg_tender_delivered", DisplayArg(d))); }
            else if (day >= d.DeadlineDay)
            {
                d.State = DealState.Lost;
                Reputation = Math.Max(0, Reputation - 8);
                if (!Creative.UnlimitedMoney) { Money -= d.Monthly * 3; Book("cost_sla", -d.Monthly * 3); }
                Notes.Add(("msg_tender_failed", DisplayArg(d)));
            }
        }
        RunContracts(day);
        Deals.RemoveAll(d => d.State is DealState.Lost or DealState.Ended && day - d.EndDay > 60);
        Touch(false);
    }

    void NewLeads(int day, Random rng)
    {
        var open = Deals.Count(d => d.State == DealState.Lead);
        if (open >= Org.MaxLeads) return;
        var rate = (0.25 + 0.2 * SalesReps) * (1 + 0.25 * Training(Exec.Sales)) * (0.7 + Reputation / 100) * LeadFactor;
        var n = (int)rate + (rng.NextDouble() < rate - (int)rate ? 1 : 0);
        if (_leadSoon) { n = Math.Max(n, 1); _leadSoon = false; }
        for (var i = 0; i < n && open + i < Org.MaxLeads; i++) Deals.Add(MakeLead(day, rng));
        if (n > 0) Notes.Add(("msg_new_leads", n.ToString()));
    }

    CustomerType PickType(Random rng, bool tender)
    {
        var pool = Org.Customers.Values.Where(c => c.TenderOnly == tender && (tender || MinReputation(c) <= Reputation) && (c.Type != CustomerType.Ai || Done("cool_liquid"))).ToList();
        var total = pool.Sum(Weight);
        var x = rng.NextDouble() * total;
        foreach (var c in pool) { x -= Weight(c); if (x <= 0) return c.Type; }
        return pool[^1].Type;
    }

    /// <summary>The government framework (research) lets public bodies in earlier and more often.</summary>
    bool PublicBody(CustomerType t) => t is CustomerType.Municipality or CustomerType.Government;
    int MinReputation(CustomerDef c) => PublicBody(c.Type) && Done("biz_gov") ? Math.Max(0, c.MinReputation - 15) : c.MinReputation;
    double Weight(CustomerDef c) => PublicBody(c.Type) && Done("biz_gov") ? c.Weight * 2 : c.Weight;

    public Deal MakeLead(int day, Random rng, CustomerType? type = null)
    {
        var def = Org.Customers[type ?? PickType(rng, false)];
        var d = new Deal { Id = NextId++, Type = def.Type, State = DealState.Lead };
        Org.Name(d, rng);
        d.Product = def.Products[rng.Next(def.Products.Length)];
        // bigger teams and a better name bring bigger customers
        var scale = 1 + 0.08 * SalesReps + Reputation / 200;
        if (d.Product == Product.PerU) d.Units = Math.Max(1, (int)Math.Round(rng.Next(def.Min, def.Max + 1) * Math.Min(2, scale)));
        else d.Racks = Math.Max(1, (int)Math.Round(rng.Next(def.Min, def.Max + 1) * Math.Min(2.5, scale)));
        if (d.Product == Product.Cage) d.Racks = Math.Max(4, d.Racks);
        if (d.Product == Product.Hall) d.Racks = Math.Max(12, d.Racks);
        d.Amp32 = d.Product != Product.PerU && rng.NextDouble() < def.Amp32;
        d.TwoN = def.TwoN && d.Product != Product.PerU;
        d.AB = d.TwoN || (d.Product != Product.PerU && rng.NextDouble() < 0.4);
        var units = d.Product == Product.PerU ? d.Units / (double)Org.UnitsPerRack : d.Racks;
        d.Gbps = Math.Round(Math.Max(0.1, units * def.GbpsPerRack) * 10) / 10;
        d.Carriers = def.Carriers;
        d.Green = def.Green || rng.NextDouble() < 0.1;
        d.Security = def.Security;
        d.Months = d.Product switch { Product.PerU => 12, Product.Rack => rng.Next(2) == 0 ? 12 : 24, _ => 36 };
        d.Monthly = (long)Math.Round(Org.ListPrice(d) * (1 + 0.05 * Training(Exec.Sales)) * (Done("biz_sla") ? 1.05 : 1) / 10) * 10;
        d.ExpiresDay = day + 12 + rng.Next(10);
        return d;
    }

    public Deal NewTender(int day, Random rng)
    {
        var def = Org.Customers[PickType(rng, true)];
        var d = new Deal { Id = NextId++, Type = def.Type, State = DealState.Tender, Tender = true };
        Org.Name(d, rng);
        d.Product = def.Products[rng.Next(def.Products.Length)];
        d.Racks = (int)Math.Round(rng.Next(def.Min, def.Max + 1) * (1 + Reputation / 100) / 4.0) * 4;
        d.Amp32 = rng.NextDouble() < def.Amp32;
        d.TwoN = def.TwoN; d.AB = true;
        d.Gbps = Math.Round(d.Racks * def.GbpsPerRack);
        d.Carriers = def.Carriers; d.Green = def.Green; d.Security = def.Security;
        d.Months = 36 + 12 * rng.Next(3);
        d.Monthly = (long)Math.Round(d.Kw * Org.TenderPerKw * def.PriceFactor / 100) * 100;
        d.ExpiresDay = day + 10;
        d.DeliveryDays = 90 + 30 * rng.Next(4);
        Deals.Add(d);
        Notes.Add(("msg_new_tender", DisplayArg(d)));
        return d;
    }

    /// <summary>The customer compares your bid with the rivals'. Price, reputation and your legal counsel's experience count.</summary>
    public bool ResolveBid(Deal d, Random rng)
    {
        var mine = 0.35 + Reputation / 250 + (1 - d.BidPercent / 100.0) * 2.2 + 0.05 * Training(Exec.Legal) + (Meets(d) ? 0.1 : 0) + 0.05 * CertsWanted(d.Type).Count(c => HasCert(c.kind)) + rng.NextDouble() * 0.15;
        mine += Has(KeyRole.StarSales) ? 0.06 : 0;
        var best = RivalBid(rng, out var winner);
        if (mine >= best)
        {
            d.Monthly = (long)Math.Round(d.Monthly * d.BidPercent / 100.0 / 100) * 100;
            d.DeadlineDay = Day + d.DeliveryDays;
            d.State = DealState.Building;
            Notes.Add(("msg_tender_won", DisplayArg(d)));
            Reputation = Math.Min(100, Reputation + 1);
            if (Meets(d)) { Activate(d); Reputation = Math.Min(100, Reputation + 3); }
            return true;
        }
        d.State = DealState.Lost; d.EndDay = Day; d.LostTo = winner;
        if (winner != null) { Notes.Add(("msg_tender_lost_to", $"{DisplayArg(d)}¦{winner}")); AddNews("news_tender_won_by", $"{winner}¦{DisplayArg(d)}", 2); }
        else Notes.Add(("msg_tender_lost", DisplayArg(d)));
        return false;
    }

    /// <summary>Replace lost racks, measure service, move satisfaction, renew or end contracts.</summary>
    void RunContracts(int day)
    {
        var working = WorkingRacks();
        var owners = RackOwners();
        var rackIds = Objects.Where(o => o.Kind == ObjKind.Rack).Select(o => o.Id).ToHashSet();
        var perURacks = Objects.Where(o => o.Kind == ObjKind.Rack && o.Status == ObjStatus.Live && HallOf(o)?.Zone == HallZone.PerU).ToList();
        var perUServed = perURacks.Count == 0 ? 0 : perURacks.Count(o => working.Contains(o.Id)) / (double)perURacks.Count;
        var cs = Has(Exec.CustomerSuccess) ? 1 + Training(Exec.CustomerSuccess) : 0;
        foreach (var d in Deals.Where(d => d.State == DealState.Active).ToList())
        {
            if (!d.Legacy && d.Product != Product.PerU)
            {
                // demolished racks are replaced by free ones where possible
                var gone = d.RackIds.Where(id => !rackIds.Contains(id)).ToList();
                if (gone.Count > 0)
                {
                    d.RackIds.RemoveAll(gone.Contains);
                    foreach (var id in FreeRacksFor(d, out _).Where(id => !owners.ContainsKey(id)).Take(d.Racks - d.RackIds.Count)) { d.RackIds.Add(id); owners[id] = d.Id; }
                }
            }
            d.Served = d.Product == Product.PerU ? perUServed
                : d.Legacy ? (d.RackIds.Count == 0 ? 0 : d.RackIds.Count(working.Contains) / (double)d.RackIds.Count)
                : d.RackIds.Count(working.Contains) / (double)Math.Max(1, d.Racks);
            if (d.Served >= 0.999) d.Satisfaction = Math.Min(100, d.Satisfaction + 0.4 + 0.2 * cs);
            else d.Satisfaction = Math.Max(0, d.Satisfaction - (1 - d.Served) * (cs > 0 ? 25.0 / (1 + cs * 0.5) : 25));
            if (d.Satisfaction < 15 && !d.Legacy)
            {
                d.State = DealState.Ended; d.EndDay = day; d.RackIds.Clear();
                Reputation = Math.Max(0, Reputation - 2);
                Notes.Add(("msg_customer_left", DisplayArg(d)));
                continue;
            }
            if (day >= d.EndDay)
            {
                var keep = d.Legacy || d.Satisfaction >= (cs > 0 ? 45 : 60);
                if (keep)
                {
                    d.EndDay = day + d.Months * 30;
                    Reputation = Math.Min(100, Reputation + 1);
                    if (!d.Legacy) Notes.Add(("msg_renewed", DisplayArg(d)));
                }
                else
                {
                    d.State = DealState.Ended; d.RackIds.Clear();
                    Notes.Add(("msg_contract_ended", DisplayArg(d)));
                }
            }
        }
        var active = Deals.Where(d => d.State == DealState.Active).ToList();
        if (active.Count > 0 && active.Average(d => d.Satisfaction) >= 80) Reputation = Math.Min(100, Reputation + 0.05);
    }

    /// <summary>One day of money: contracts, network, energy, staff, site, loans and investors. Then the bankruptcy check.</summary>
    void MoneyDay(int day)
    {
        if (Creative.UnlimitedMoney) { DaysInRed = 0; return; }
        var infra = Infra;
        var energy = EnergyCostDay(infra);
        var rent = 0.0; var sla = 0.0; var support = 0.0;
        foreach (var d in Deals.Where(d => d.State == DealState.Active))
        {
            rent += d.Monthly / 30.0 * d.Served;
            if (d.Served < 0.999 && !d.Legacy) sla += d.Monthly / 30.0 * (1 - d.Served) * 0.5;
            if (d.Product == Product.PerU) support += d.Units * Org.SupportPerU / 30.0;
        }
        var net = NetworkPerDay();
        var netIn = Peers.Where(p => p.State == PeerState.Active && Catalog.Rivals[p.Rival].Monthly > 0).Sum(p => Catalog.Rivals[p.Rival].Monthly / 30.0) + CrossConnects * Catalog.XcMonthlyFee / 30.0;
        var lines = new (string key, double v)[]
        {
            ("inc_contracts", rent), ("inc_network", netIn), ("cost_network", net - netIn), ("cost_energy", -energy),
            ("cost_crew", -WagesPerDay), ("cost_salaries", -SalariesPerMonth / 30.0), ("cost_staff", -(StaffPayPerMonth + PlansPerMonth) / 30.0), ("inc_remote_hands", RemoteHandsIncome / 30.0), ("inc_heat", HeatIncomePerMonth / 30.0), ("inc_office_rent", OfficeRentPerMonth / 30.0), ("cost_site", -400 - support), ("cost_sla", -sla),
        }.Concat(GrowthMoney()).ToArray();
        foreach (var (key, v) in lines)
        {
            var amount = (long)Math.Round(v);
            Money += amount; Book(key, amount);
        }
        if (day % 30 == 0 && day > 0) MonthEnd();
        if (Money < 0)
        {
            DaysInRed++;
            if (DaysInRed is 1 or 15 or 25) Notes.Add(("msg_in_red", (30 - DaysInRed).ToString()));
            if (DaysInRed >= 30 && !Bankrupt) { Bankrupt = true; Notes.Add(("msg_bankrupt", null)); }
        }
        else DaysInRed = 0;
    }

    /// <summary>Monthly: loan instalments and the investors' share of last month's profit.</summary>
    void MonthEnd()
    {
        foreach (var l in Loans.ToList())
        {
            var interest = (long)Math.Round(l.Balance * l.Rate / 12);
            var pay = Math.Min(l.Payment, l.Balance + interest);
            Money -= pay; Book("cost_interest", -interest); Book("fin_repay", -(pay - interest));
            l.Balance -= pay - interest; l.MonthsLeft--;
            if (l.Balance <= 0 || l.MonthsLeft <= 0) Loans.Remove(l);
        }
        if (InvestorShare > 0)
        {
            var last = Books.FirstOrDefault(b => b.Month == MonthIndex - 1);
            var profit = last?.Lines.Where(kv => kv.Key.StartsWith("inc_") || kv.Key.StartsWith("cost_")).Sum(kv => kv.Value) ?? 0;
            if (profit > 0)
            {
                var div = (long)Math.Round(profit * InvestorShare);
                Money -= div; Book("fin_dividend", -div);
            }
        }
        var lastMonth = Books.FirstOrDefault(b => b.Month == MonthIndex - 1);
        GrowthMonthEnd(lastMonth?.Lines.Where(kv => kv.Key.StartsWith("inc_") || kv.Key.StartsWith("cost_")).Sum(kv => kv.Value) ?? 0);
    }
}
