using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace DatacenterArchitect.Sim;

/// <summary>Branches of the research tree (step 8).</summary>
public enum Branch { Power, Cooling, Network, Construction, Security, Business }

/// <summary>
/// A research project: branch, research points, money, the projects it needs first, the first game year it can start
/// (new technology arrives as the years pass) and its column in the tree.
/// </summary>
public record ResearchDef(string Id, Branch Branch, int Points, long Cost, string[] Requires, int Year, int Tier);

public static class ResearchTree
{
    static readonly string[] None = Array.Empty<string>();
    static string[] R(params string[] ids) => ids;

    public static readonly ResearchDef[] All =
    {
        // power: batteries, UPS, busbar and buying energy smarter
        new("pwr_lithium", Branch.Power, 40, 150_000, None, 0, 0),
        new("pwr_ups_eco", Branch.Power, 50, 200_000, R("pwr_lithium"), 0, 1),
        new("pwr_busbar", Branch.Power, 60, 250_000, R("pwr_ups_eco"), 0, 2),
        new("pwr_peak", Branch.Power, 80, 300_000, R("pwr_busbar"), 2028, 3),
        // cooling: from hot/cold aisles to the cold corridor, free cooling, liquid cooling and selling heat
        new("cool_corridor", Branch.Cooling, 30, 100_000, None, 0, 0),
        new("cool_ecfans", Branch.Cooling, 40, 160_000, R("cool_corridor"), 0, 1),
        new("cool_free", Branch.Cooling, 70, 300_000, R("cool_ecfans"), 0, 2),
        new("cool_heat", Branch.Cooling, 90, 400_000, R("cool_free"), 0, 3),
        new("cool_liquid", Branch.Cooling, 100, 500_000, R("cool_free"), 2028, 3),
        // network
        new("net_optics", Branch.Network, 30, 120_000, None, 0, 0),
        new("net_sdn", Branch.Network, 50, 220_000, R("net_optics"), 0, 1),
        new("net_ddos", Branch.Network, 60, 250_000, R("net_sdn"), 0, 2),
        // construction
        new("bld_prefab", Branch.Construction, 40, 150_000, None, 0, 0),
        new("bld_rows", Branch.Construction, 50, 200_000, R("bld_prefab"), 0, 1),
        new("bld_logistics", Branch.Construction, 60, 220_000, R("bld_rows"), 0, 2),
        // security
        new("sec_biometrics", Branch.Security, 30, 120_000, None, 0, 0),
        new("sec_anpr", Branch.Security, 30, 90_000, None, 0, 0),
        new("sec_mantrap", Branch.Security, 50, 200_000, R("sec_biometrics"), 0, 1),
        new("sec_electric", Branch.Security, 50, 180_000, R("sec_anpr"), 0, 1),
        new("sec_ai_video", Branch.Security, 80, 320_000, R("sec_mantrap"), 2028, 2),
        // business
        new("biz_crm", Branch.Business, 30, 100_000, None, 0, 0),
        new("biz_sla", Branch.Business, 50, 200_000, R("biz_crm"), 0, 1),
        new("biz_esg", Branch.Business, 50, 180_000, R("biz_crm"), 0, 1),
        new("biz_gov", Branch.Business, 60, 250_000, R("biz_sla"), 0, 2),
        new("biz_office_rent", Branch.Business, 50, 160_000, R("biz_crm"), 0, 1),
    };

    public static readonly Dictionary<string, ResearchDef> ById = All.ToDictionary(d => d.Id);

    public static Branch[] Branches => Enum.GetValues<Branch>();

    /// <summary>Lab benches a researcher works at; each bench brings one researcher.</summary>
    public const double PointsPerResearcher = 1.0;
    public const int MaxQueue = 5;
}

/// <summary>Research: R&D lab, researchers, the queue and what finished research changes (step 8).</summary>
public partial class GameState
{
    /// <summary>Finished research projects.</summary>
    public List<string> Researched { get; set; } = new();
    /// <summary>Projects waiting in line; the first one is being worked on. Their money is paid when they are queued.</summary>
    public List<string> ResearchQueue { get; set; } = new();
    /// <summary>Points gathered for the project at the front of the queue.</summary>
    public double ResearchProgress { get; set; }

    public bool Done(string id) => Creative.AllUnlocks || Researched.Contains(id);

    /// <summary>Lab benches in finished R&D labs.</summary>
    [JsonIgnore] public int LabBenches => Objects.Count(o => o.Kind == ObjKind.LabBench && o.Status == ObjStatus.Live && Rooms.Any(r => r.Type == RoomType.Lab && r.Stage == Stage.Done && r.Floor == o.Floor && r.Contains(o.X, o.Z)));

    /// <summary>Research points per day: researchers at their benches, more with the R&D manager's training, extra for certificates and happy customers.</summary>
    [JsonIgnore]
    public double ResearchPerDay
    {
        get
        {
            if (!Has(Exec.Research)) return 0;
            var people = Staff.Where(e => e.Role == StaffRole.Researcher && e.TrainingDaysLeft <= 0).Sum(e => e.Speed);
            if (people <= 0) return 0;
            var pts = people * ResearchTree.PointsPerResearcher * (1 + 0.15 * Training(Exec.Research));
            pts += 0.25 * Certs.Count(c => c.Valid);
            var active = Deals.Where(d => d.State == DealState.Active).ToList();
            if (active.Count > 0 && active.Average(d => d.Satisfaction) >= 75) pts += 0.5;
            return pts;
        }
    }

    /// <summary>Why a project cannot be queued now (null = it can).</summary>
    public string CanResearch(string id)
    {
        if (!ResearchTree.ById.TryGetValue(id, out var def)) return "msg_nothing_changed";
        if (Done(id) || ResearchQueue.Contains(id)) return "msg_nothing_changed";
        if (!Has(Exec.Research)) return "msg_needs_research_mgr";
        if (ResearchQueue.Count >= ResearchTree.MaxQueue) return "msg_research_queue_full";
        if (def.Requires.Any(r => !Done(r) && !ResearchQueue.Contains(r))) return "msg_research_needs";
        if (def.Year > Date.Year) return "msg_research_year";
        if (id == "biz_office_rent" && !Has(Exec.OfficeManager)) return "msg_needs_office_mgr";
        if (!CanAfford(def.Cost)) return "msg_no_money";
        return null;
    }

    public string QueueResearch(string id)
    {
        var why = CanResearch(id);
        if (why != null) return why;
        var def = ResearchTree.ById[id];
        if (!Creative.UnlimitedMoney) { Money -= def.Cost; Book("cost_research", -def.Cost); }
        ResearchQueue.Add(id);
        if (Creative.InstantBuild) { while (ResearchQueue.Count > 0) FinishResearch(); }
        Touch(false);
        return null;
    }

    /// <summary>Take a project out of the queue: its money comes back, and so do projects that needed it.</summary>
    public void CancelResearch(string id)
    {
        var i = ResearchQueue.IndexOf(id);
        if (i < 0) return;
        foreach (var later in ResearchQueue.Skip(i + 1).Where(x => ResearchTree.ById[x].Requires.Contains(id)).ToList()) CancelResearch(later);
        ResearchQueue.Remove(id);
        if (i == 0) ResearchProgress = 0;
        var cost = ResearchTree.ById[id].Cost;
        if (!Creative.UnlimitedMoney) { Money += cost; Book("cost_research", cost); }
        Touch(false);
    }

    /// <summary>Points still needed for a project (0 when done).</summary>
    public double ResearchLeft(string id)
    {
        if (Done(id)) return 0;
        var pts = ResearchTree.ById[id].Points;
        return ResearchQueue.Count > 0 && ResearchQueue[0] == id ? Math.Max(0, pts - ResearchProgress) : pts;
    }

    void FinishResearch()
    {
        var id = ResearchQueue[0];
        ResearchQueue.RemoveAt(0);
        ResearchProgress = 0;
        if (!Researched.Contains(id)) Researched.Add(id);
        Reputation = Math.Min(100, Reputation + 0.5);
        Notes.Add(("msg_research_done", "rs_" + id));
        Log("ev_research_done", "rs_" + id);
        InvalidateInfra();
        Touch(false);
    }

    void ResearchDay()
    {
        if (ResearchQueue.Count == 0) return;
        ResearchProgress += ResearchPerDay;
        while (ResearchQueue.Count > 0 && ResearchProgress >= ResearchTree.ById[ResearchQueue[0]].Points)
        {
            var carry = ResearchProgress - ResearchTree.ById[ResearchQueue[0]].Points;
            FinishResearch();
            ResearchProgress = ResearchQueue.Count > 0 ? carry : 0;
        }
    }

    // ---------------- what research changes ----------------
    /// <summary>Lithium batteries hold half as much again.</summary>
    [JsonIgnore] public double BatteryFactor => Done("pwr_lithium") ? 1.5 : 1;
    /// <summary>Eco-mode UPS units carry 20% more.</summary>
    [JsonIgnore] public double UpsFactor => Done("pwr_ups_eco") ? 1.2 : 1;
    /// <summary>Heavy busbar: 600 kW per run instead of 400.</summary>
    [JsonIgnore] public double BusbarKw => Done("pwr_busbar") ? 600 : Catalog.BusbarKw;
    /// <summary>EC fans in the CRAH units (research) and fan upgrades from proposals.</summary>
    [JsonIgnore] public double CrahFactor => (Done("cool_ecfans") ? 1.1 : 1) * (1 + Bonus(ProposalKind.CrahFans));
    /// <summary>Free cooling with outside air lowers the PUE of every hall.</summary>
    [JsonIgnore] public double PueSaving => Done("cool_free") ? 0.08 : 0;
    /// <summary>Route optimisation sends more traffic over the exchange and peers.</summary>
    [JsonIgnore] public double IxBonus => Done("net_sdn") ? 0.05 : 0;
    /// <summary>Coherent optics: carrier ports and commits 10% cheaper.</summary>
    [JsonIgnore] public double NetworkCostFactor => Done("net_optics") ? 0.9 : 1;
    /// <summary>Prefab modules: builders and fitters 20% faster. Logistics: everyone 15% faster.</summary>
    [JsonIgnore] public double ResearchBuildSpeed => (Done("bld_prefab") ? 1.2 : 1) * (Done("bld_logistics") ? 1.15 : 1);
    /// <summary>Longer rows of racks in one go.</summary>
    [JsonIgnore] public int MaxRowLength => Done("bld_rows") ? 48 : Catalog.MaxRow;
    /// <summary>Waste heat sold to the district heating: euros per month per kW of IT load.</summary>
    public const double HeatPerKwMonth = 4;
    [JsonIgnore] public long HeatIncomePerMonth => Done("cool_heat") ? (long)Math.Round(Infra.ItRunningKw * HeatPerKwMonth) : 0;
}
