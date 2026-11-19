using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace DatacenterArchitect.Sim;

/// <summary>Cyberattacks and the SOC, disasters and insurance, and delays in the supply chain (step 9).</summary>
public partial class GameState
{
    /// <summary>0 = no insurance, 1 = basic (60% after a high deductible), 2 = full (all damage and lost income).</summary>
    public int Insurance { get; set; }
    public bool Soc247 { get; set; }
    public double SupplyUntil { get; set; }
    /// <summary>0 = chips (racks and servers), 1 = electrical (transformers, UPS, switchgear), 2 = shipping (everything).</summary>
    public int SupplyKind { get; set; }
    public double SupplyDays { get; set; }
    public long DamageThisYear { get; set; }
    public long PaidOutThisYear { get; set; }
    public int AttacksBlocked { get; set; }
    public int AttacksHit { get; set; }

    public const long Soc247Fee = 5000;
    public static readonly double[] InsuranceRate = { 0, 0.0007, 0.0013 };
    public static readonly long[] Deductible = { 0, 75_000, 15_000 };
    public static readonly double[] InsuranceCover = { 0, 0.6, 1.0 };

    // ---------------- supply chain ----------------
    [JsonIgnore] public bool SupplyCrunch => Options.SupplyDelays && Day < SupplyUntil;
    /// <summary>Everything that takes time to deliver or build takes longer in a crunch.</summary>
    [JsonIgnore] public double SupplyFactor => SupplyCrunch && SupplyKind == 2 ? 1.25 : 1;

    /// <summary>Extra days before a truck brings an ordered piece of equipment.</summary>
    public double SupplyDelay(ObjKind k)
    {
        if (!SupplyCrunch || Creative.InstantBuild) return 0;
        var hit = SupplyKind switch
        {
            0 => k is ObjKind.Rack or ObjKind.Spine or ObjKind.CoreRouter,
            1 => k is ObjKind.Transformer or ObjKind.Switchgear or ObjKind.Ups or ObjKind.Pdu or ObjKind.Battery,
            _ => true,
        };
        return hit ? SupplyDays : 0;
    }

    /// <summary>An ordered object still waits at the supplier.</summary>
    public bool SupplyHeld(PlacedObject o) => o.Status == ObjStatus.Ordered && Day < o.Installed + SupplyDelay(o.Kind);

    void SupplyDay(int day, Random rng)
    {
        if (!Options.SupplyDelays || day < 40) return;
        if (Day >= SupplyUntil && rng.NextDouble() < 1 / 160.0)
        {
            SupplyKind = rng.Next(3);
            SupplyDays = SupplyKind == 2 ? 3 : 5 + rng.Next(6);
            SupplyUntil = day + 30 + rng.Next(60);
            AddNews("news_supply_" + SupplyKind, null, 0);
            Notes.Add(("msg_supply_" + SupplyKind, ((int)SupplyDays).ToString()));
            Log("ev_supply_" + SupplyKind);
        }
    }

    // ---------------- cyberattacks and the SOC ----------------
    [JsonIgnore] public int SocDesks => Objects.Count(o => o.Kind == ObjKind.SocDesk && o.Status == ObjStatus.Live && Rooms.Any(r => r.Type == RoomType.Soc && r.Stage == Stage.Done && r.Floor == o.Floor && r.Contains(o.X, o.Z)));

    /// <summary>How well you are protected against attacks (0..100): analysts in the SOC, 24/7 monitoring, research and the security manager.</summary>
    [JsonIgnore]
    public int SocLevel
    {
        get
        {
            var analysts = Math.Min(4, Staff.Count(e => e.Role == StaffRole.SocAnalyst && e.TrainingDaysLeft <= 0));
            var lvl = analysts * 12 + (Soc247 && analysts > 0 ? 16 : 0) + (Done("net_ddos") ? 14 : 0) + (Done("sec_ai_video") ? 5 : 0)
                + (Has(Exec.Security) ? 4 + 3 * Training(Exec.Security) : 0) + (HasCert(CertKind.Iso27001) ? 6 : 0) + (Has(KeyRole.SocLead) ? 12 : 0);
            return Math.Clamp(lvl, 0, 100);
        }
    }

    static readonly string[] Attacks = { "ddos", "ransomware", "phishing", "intrusion" };

    void CyberDay(int day, Random rng)
    {
        if (!Options.Events || day < 25) return;
        var exposure = 1 / 55.0 * (1 + Math.Min(2, LiveRacks / 400.0)) * (Listed ? 1.3 : 1);
        if (rng.NextDouble() >= exposure) return;
        var kind = Attacks[rng.Next(Attacks.Length)];
        var block = 0.2 + SocLevel / 115.0 + (kind == "ddos" && Done("net_ddos") ? 0.25 : 0);
        if (rng.NextDouble() < block)
        {
            AttacksBlocked++;
            Reputation = Math.Min(100, Reputation + 0.5);
            Notes.Add(("msg_cyber_blocked", "cyber_" + kind));
            Log("ev_cyber_blocked", "cyber_" + kind);
            return;
        }
        AttacksHit++;
        var monthly = Deals.Where(d => d.State == DealState.Active).Sum(d => d.Monthly);
        long cost = kind switch
        {
            "ddos" => (long)(monthly * 0.03) + 5000,
            "ransomware" => 120_000 + rng.Next(200_000),
            "phishing" => 20_000 + rng.Next(40_000),
            _ => 60_000 + rng.Next(120_000),
        };
        if (!HasCert(CertKind.Iso27001) && kind is "ransomware" or "intrusion") cost = cost * 3 / 2;
        Pay("cost_cyber", cost);
        Reputation = Math.Max(0, Reputation - (kind == "phishing" ? 1 : 3));
        foreach (var d in Deals.Where(d => d.State == DealState.Active)) d.Satisfaction = Math.Max(0, d.Satisfaction - (kind == "ddos" ? 4 : 2));
        if (Hosting.Started) Hosting.Satisfaction = Math.Max(0, Hosting.Satisfaction - 6);
        Notes.Add(("msg_cyber_hit", "cyber_" + kind));
        Log("ev_cyber_hit", "cyber_" + kind);
        AddNews("news_cyber_" + kind, Options.Company, 5);
    }

    // ---------------- disasters and insurance ----------------
    /// <summary>What the insurer sees as the value of your equipment and buildings.</summary>
    [JsonIgnore] public long AssetValue => Objects.Sum(o => o.Def.Cost) + Buildings.Sum(b => BuildingCost(b.Kind, b.W, b.D, b.Floors));
    [JsonIgnore] public long InsurancePerMonth => (long)Math.Round(AssetValue * InsuranceRate[Math.Clamp(Insurance, 0, 2)] / 10) * 10;
    public void SetInsurance(int level) { Insurance = Math.Clamp(level, 0, 2); Touch(false); }

    /// <summary>Damage is paid at once; the insurer pays its part after the deductible.</summary>
    public void Damage(long cost, long lostIncome = 0)
    {
        if (cost <= 0 || Creative.UnlimitedMoney) return;
        Money -= cost; Book("cost_disaster", -cost);
        DamageThisYear += cost;
        var lvl = Math.Clamp(Insurance, 0, 2);
        var claim = cost + (lvl == 2 ? lostIncome : 0) - Deductible[lvl];
        var pay = (long)Math.Round(Math.Max(0, claim) * InsuranceCover[lvl]);
        if (pay > 0)
        {
            Money += pay; Book("inc_insurance", pay); PaidOutThisYear += pay;
            Notes.Add(("msg_insurance_paid", pay.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)));
        }
    }

    static readonly string[] DisasterKinds = { "storm", "lightning", "flood", "substation", "fire" };

    void DisasterDay(int day, Random rng)
    {
        if (day % 365 == 0) { DamageThisYear = 0; PaidOutThisYear = 0; }
        if (!Options.Disasters || day < 30) return;
        if (rng.NextDouble() < 1 / 200.0) Disaster(DisasterKinds[rng.Next(DisasterKinds.Length)], rng);
        // other locations are hit now and then too
        foreach (var s in Sites.Where(x => x.Built && x.Kind == SiteKind.Datacenter && Day >= x.OfflineUntil))
            if (rng.NextDouble() < 1 / 500.0 * (1 + Countries.Get(s.Country).Water))
            {
                s.OfflineUntil = day + 1 + rng.Next(4);
                var lost = (long)(s.Occupied * SitePrice(s) / 30 * (s.OfflineUntil - day));
                Damage(s.Racks * 900L, lost);
                Notes.Add(("msg_site_disaster", s.City)); AddNews("news_disaster_site", s.City, 5);
            }
    }

    /// <summary>A natural disaster or accident at the campus. Public so tests and the screens can start one.</summary>
    public void Disaster(string kind, Random rng)
    {
        long damage = 0;
        var monthly = Deals.Where(d => d.State == DealState.Active).Sum(d => d.Monthly);
        switch (kind)
        {
            case "storm":
                // outdoor units and panels are damaged
                foreach (var o in Objects.Where(o => o.Kind is ObjKind.CoolingTower or ObjKind.Chiller or ObjKind.DryCooler or ObjKind.SolarPanel or ObjKind.WindTurbine or ObjKind.CameraPole && !o.Broken).OrderBy(_ => rng.Next()).Take(3).ToList())
                { if (!Creative.NoBreakdowns) Break(o); damage += o.Def.Cost / 5; }
                damage += 15_000;
                break;
            case "lightning":
                var hit = Objects.Where(o => o.Kind is ObjKind.Transformer or ObjKind.Switchgear or ObjKind.Ups && !o.Broken).OrderBy(_ => rng.Next()).FirstOrDefault();
                if (hit != null) { if (!Creative.NoBreakdowns) Break(hit); damage += hit.Def.Cost / 3; }
                damage += 10_000;
                break;
            case "flood":
                var hall = Rooms.Where(r => r.Type == RoomType.DataHall && r.Floor == 0 && r.Stage == Stage.Done).OrderBy(_ => rng.Next()).FirstOrDefault();
                if (hall != null)
                {
                    StartEvent(EventKind.Flood, 18 + rng.Next(30), hall.Id);
                    damage += 80_000 + hall.W * hall.D * 150L;
                }
                else damage += 30_000;
                break;
            case "substation":
                if (!GridDown) StartEvent(EventKind.CityOutage, 10 + rng.Next(20));
                damage += 5_000;
                break;
            default: // fire in an office or warehouse
                var b = Buildings.Where(x => x.Kind != BuildingKind.Datacenter && x.Stage == Stage.Done).OrderBy(_ => rng.Next()).FirstOrDefault();
                damage += b != null ? BuildingCost(b.Kind, b.W, b.D, b.Floors) / 6 : 20_000;
                break;
        }
        Damage(damage, (long)(monthly * 0.05));
        Reputation = Math.Max(0, Reputation - 1);
        Notes.Add(("msg_disaster_" + kind, null));
        Log("ev_disaster_" + kind);
        AddNews("news_disaster_" + kind, Options.Company, 5);
        InvalidateInfra();
    }
}
