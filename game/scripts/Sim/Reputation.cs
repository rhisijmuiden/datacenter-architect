using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace DatacenterArchitect.Sim;

/// <summary>What customers write reviews about.</summary>
public enum ReviewTopic { Power, Network, Support, Security }

public class Review
{
    public double Day { get; set; }
    /// <summary>Customer name argument (a company name or "@key|arg").</summary>
    public string Who { get; set; } = "";
    public ReviewTopic Topic { get; set; }
    public int Stars { get; set; }
}

/// <summary>Proposals that managers send now and then. Accepted ones give a lasting bonus.</summary>
public enum ProposalKind { Router, Ddos, CrahFans, UpsFirmware, SecurityTech, Crm }

public class Proposal
{
    public int Id { get; set; }
    public ProposalKind Kind { get; set; }
    /// <summary>Fictional brand of the kit.</summary>
    public string Brand { get; set; } = "";
    public long Cost { get; set; }
    /// <summary>Size of the bonus (0.05 = 5%, or quality points / 100).</summary>
    public double Value { get; set; }
    public double ExpiresDay { get; set; }
}

public static class Proposals
{
    public static readonly string[] NetBrands = { "Corelink", "NordSwitch", "Aster Networks", "Kestrel Optics" };
    public static readonly string[] CoolBrands = { "Frostline", "Polar Air", "Windmolen Klimaat" };
    public static readonly string[] PowerBrands = { "Voltaris", "Dijkstra Power", "Ampere Noord" };
    public static readonly string[] SecBrands = { "Wachter Systems", "Argus Vision", "Slot & Sleutel" };
    public static readonly string[] SalesBrands = { "Leadstroom", "Pipeline Pro" };

    /// <summary>The manager that sends a kind of proposal.</summary>
    public static Exec From(ProposalKind k) => k switch
    {
        ProposalKind.Router or ProposalKind.Ddos => Exec.Network,
        ProposalKind.CrahFans or ProposalKind.UpsFirmware => Exec.Facility,
        ProposalKind.SecurityTech => Exec.Security,
        _ => Exec.Marketing,
    };

    /// <summary>Highest total bonus a kind can reach, so proposals stay a nice extra.</summary>
    public static double Cap(ProposalKind k) => k switch
    {
        ProposalKind.CrahFans => 0.15,
        ProposalKind.Crm => 0.3,
        ProposalKind.SecurityTech => 0.06,
        _ => 0.25,
    };
}

/// <summary>Reputation: quality scores, reviews, proposals and the marketing team (step 8).</summary>
public partial class GameState
{
    /// <summary>Quality per topic (0..100) as customers feel it; it follows how the site performs, slowly.</summary>
    public Dictionary<ReviewTopic, double> Quality { get; set; } = new();
    public List<Review> Reviews { get; set; } = new();
    public List<Proposal> OpenProposals { get; set; } = new();
    /// <summary>Accepted proposals: their kind and size.</summary>
    public List<Proposal> Upgrades { get; set; } = new();
    public double NextReviewDay { get; set; } = 5;
    public double NextProposalDay { get; set; } = 25;
    /// <summary>Reputation at the start of each month, for the trend.</summary>
    public List<double> RepHistory { get; set; } = new();

    public double Bonus(ProposalKind k) => Math.Min(Proposals.Cap(k), Upgrades.Where(u => u.Kind == k).Sum(u => u.Value));

    public double QualityOf(ReviewTopic t) => Quality.TryGetValue(t, out var v) ? v : 50;

    /// <summary>What quality the site delivers now on a topic (0..100). Quality moves towards this every day.</summary>
    public double QualityTarget(ReviewTopic t)
    {
        var active = Deals.Where(d => d.State == DealState.Active).ToList();
        var served = active.Count == 0 ? 1 : active.Average(d => d.Served);
        switch (t)
        {
            case ReviewTopic.Power:
            {
                if (Creative.NoPowerLimit) return 90;
                var inf = Infra;
                var ups = inf.Link(PowerLink.Ups).Redundancy;
                var q = 45 + (inf.GenRedundancy is "N+1" or "2N" ? 15 : inf.GenKw > 0 ? 6 : 0) + (ups == "2N" ? 15 : ups == "N+1" ? 10 : 0)
                        + (inf.Halls.Count(h => h.LiveRacks > 0) > 0 && inf.Halls.Where(h => h.LiveRacks > 0).All(h => h.AB) ? 10 : 0) + Bonus(ProposalKind.UpsFirmware) * 100;
                return Math.Clamp(q - (1 - served) * 120 - (Objects.Any(o => o.Broken && Staffing.PartFor(o.Kind) is Part.Electrical or Part.Battery) ? 8 : 0), 0, 100);
            }
            case ReviewTopic.Network:
            {
                if (Creative.NoNetworkLimit) return 90;
                var net = Net;
                var carriers = net.Carriers.Count(c => c.Up && !c.Def.Ix);
                var q = 40 + Math.Min(3, carriers) * 8 + (net.Noc ? 8 : 0) + (Peers.Any(p => p.State == PeerState.Active) ? 5 : 0)
                        + (Done("net_ddos") ? 8 : 0) + Bonus(ProposalKind.Router) * 100 + Bonus(ProposalKind.Ddos) * 100;
                return Math.Clamp(q - (1 - net.Served) * 150 - (1 - served) * 60, 0, 100);
            }
            case ReviewTopic.Support:
            {
                var q = 40 + (Support247 && CountOf(StaffRole.Support) >= 3 ? 20 : HasStaff(StaffRole.Support) ? 10 : 0) + (RemoteHandsService ? 8 : 0)
                        + (Rooms.Any(r => r.Type == RoomType.Lounge && r.Stage == Stage.Done) ? 6 : 0) + (HasStaff(StaffRole.Receptionist) ? 4 : 0)
                        + (Staff.Count > 0 ? (AverageSatisfaction - 60) / 4 : 0) + (Has(Exec.CustomerSuccess) ? 6 : 0);
                return Math.Clamp(q, 0, 100);
            }
            default:
                return SecurityLevel;
        }
    }

    /// <summary>The marketing manager brings more leads and makes good news travel further.</summary>
    [JsonIgnore] public double MarketingFactor => Has(Exec.Marketing) ? 1.2 + 0.1 * Training(Exec.Marketing) : 1;
    [JsonIgnore] public double LeadFactor => MarketingFactor * (Done("biz_crm") ? 1.15 : 1) * (1 + Bonus(ProposalKind.Crm));

    void ReputationDay(int day, Random rng)
    {
        foreach (var t in Enum.GetValues<ReviewTopic>())
        {
            var cur = QualityOf(t);
            Quality[t] = cur + (QualityTarget(t) - cur) * 0.1;
        }
        if (day % 30 == 0) { RepHistory.Add(Math.Round(Reputation, 1)); while (RepHistory.Count > 24) RepHistory.RemoveAt(0); }
        if (Done("biz_esg") && Catalog.Suppliers[Supplier].Green) Reputation = Math.Min(100, Reputation + 0.02);
        // customers write a review now and then; how it lands depends on quality and their own mood
        var active = Deals.Where(d => d.State == DealState.Active && !d.Legacy).ToList();
        if (active.Count > 0 && day >= NextReviewDay)
        {
            NextReviewDay = day + Math.Max(2, 9 - active.Count / 3) + rng.Next(4);
            var d = active[rng.Next(active.Count)];
            var topic = (ReviewTopic)rng.Next(4);
            var feel = QualityOf(topic) * 0.7 + d.Satisfaction * 0.3 + (rng.NextDouble() - 0.5) * 20;
            var stars = Math.Clamp((int)Math.Round(feel / 20), 1, 5);
            AddReview(day, DisplayArg(d), topic, stars);
        }
        // managers send proposals for better kit
        if (day >= NextProposalDay)
        {
            NextProposalDay = day + 30 + rng.Next(25);
            MakeProposal(day, rng);
        }
        foreach (var p in OpenProposals.Where(p => day >= p.ExpiresDay).ToList()) OpenProposals.Remove(p);
    }

    public Review AddReview(double day, string who, ReviewTopic topic, int stars)
    {
        var r = new Review { Day = day, Who = who, Topic = topic, Stars = stars };
        Reviews.Insert(0, r);
        while (Reviews.Count > 40) Reviews.RemoveAt(Reviews.Count - 1);
        var delta = (stars - 3) * 0.6;
        if (delta > 0) delta *= MarketingFactor;
        Reputation = Math.Clamp(Reputation + delta, 0, 100);
        if (stars >= 5 || stars <= 1) Notes.Add((stars >= 5 ? "msg_review_good" : "msg_review_bad", "rev_topic_" + topic.ToString().ToLowerInvariant()));
        return r;
    }

    public Proposal MakeProposal(int day, Random rng, ProposalKind? kind = null)
    {
        var kinds = Enum.GetValues<ProposalKind>().Where(k => Has(Proposals.From(k)) && Bonus(k) < Proposals.Cap(k) - 1e-9 && OpenProposals.All(p => p.Kind != k)).ToList();
        if (kind != null) kinds = new List<ProposalKind> { kind.Value };
        if (kinds.Count == 0) return null;
        var k = kinds[rng.Next(kinds.Count)];
        var size = Math.Max(1, Objects.Count(o => o.Kind is ObjKind.Rack) / 100.0);
        var (brands, cost, value) = k switch
        {
            ProposalKind.Router => (Proposals.NetBrands, 120_000 + 40_000 * size, 0.08),
            ProposalKind.Ddos => (Proposals.NetBrands, 60_000 + 15_000 * size, 0.06),
            ProposalKind.CrahFans => (Proposals.CoolBrands, 80_000 + 30_000 * size, 0.05),
            ProposalKind.UpsFirmware => (Proposals.PowerBrands, 40_000 + 10_000 * size, 0.06),
            ProposalKind.SecurityTech => (Proposals.SecBrands, 50_000 + 10_000 * size, 0.03),
            _ => (Proposals.SalesBrands, 30_000 + 8_000 * size, 0.1),
        };
        var p = new Proposal
        {
            Id = NextId++, Kind = k, Brand = brands[rng.Next(brands.Length)], Cost = (long)Math.Round(cost * (0.85 + rng.NextDouble() * 0.3) / 1000) * 1000,
            Value = value, ExpiresDay = day + 20,
        };
        OpenProposals.Add(p);
        Notes.Add(("msg_proposal", Org.Execs[Proposals.From(k)].Key));
        return p;
    }

    public string AcceptProposal(Proposal p)
    {
        if (p == null || !OpenProposals.Contains(p)) return "msg_nothing_changed";
        if (!CanAfford(p.Cost)) return "msg_no_money";
        Pay("cost_proposals", p.Cost);
        OpenProposals.Remove(p);
        Upgrades.Add(p);
        Log("ev_proposal_accepted", "@prop_" + p.Kind.ToString().ToLowerInvariant() + "|" + p.Brand);
        InvalidateInfra();
        Touch(false);
        return null;
    }

    public void DeclineProposal(Proposal p) { OpenProposals.Remove(p); Touch(false); }
}
