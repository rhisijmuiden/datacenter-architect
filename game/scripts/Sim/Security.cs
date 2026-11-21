using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace DatacenterArchitect.Sim;

/// <summary>Certificates a compliance manager can have audited (step 8).</summary>
public enum CertKind { Iso27001, TierIII, Pci, Government, Green }

/// <summary>An audit: cost, days the auditors take, and how long the certificate stays valid.</summary>
public record CertDef(CertKind Kind, string Key, long Cost, int AuditDays, int ValidDays, double Reputation);

public class Certificate
{
    public CertKind Kind { get; set; }
    /// <summary>Audit in progress: the day it ends (0 = none).</summary>
    public double AuditEnds { get; set; }
    public double IssuedDay { get; set; }
    public double ExpiresDay { get; set; }
    /// <summary>Next yearly surveillance check.</summary>
    public double CheckDay { get; set; }
    public bool Valid { get; set; }
}

public static class CertCatalog
{
    public static readonly Dictionary<CertKind, CertDef> Defs = new()
    {
        [CertKind.Iso27001] = new(CertKind.Iso27001, "cert_iso27001", 45_000, 30, 1095, 4),
        [CertKind.TierIII] = new(CertKind.TierIII, "cert_tier3", 80_000, 45, 1095, 5),
        [CertKind.Pci] = new(CertKind.Pci, "cert_pci", 40_000, 30, 1095, 3),
        [CertKind.Government] = new(CertKind.Government, "cert_gov", 60_000, 40, 1095, 4),
        [CertKind.Green] = new(CertKind.Green, "cert_green", 30_000, 20, 1095, 3),
    };
}

/// <summary>A line of the security level: what it is, points now and the maximum.</summary>
public record SecLine(string Key, int Points, int Max);

/// <summary>Security: the security level of the site, policies and certificates (step 8).</summary>
public partial class GameState
{
    /// <summary>Policies: customers show ID to a guard, a guard walks along to open the rack, no visits outside office hours.</summary>
    public bool PolicyIdCheck { get; set; }
    public bool PolicyEscort { get; set; }
    public bool PolicyOfficeHours { get; set; }
    public List<Certificate> Certs { get; set; } = new();

    /// <summary>Halls and meet-me rooms: the rooms that must be behind badge doors.</summary>
    IEnumerable<Room> SecureRooms => Rooms.Where(r => r.Type is RoomType.DataHall or RoomType.MeetMe && r.Stage == Stage.Done);

    /// <summary>Doors in the walls of a room.</summary>
    public IEnumerable<Door> DoorsOf(Room r) => Doors.Where(d => d.Floor == r.Floor && d.Edges().Any(e =>
        e.dir == 0 ? (r.Contains(e.x, e.z) || r.Contains(e.x - 1, e.z)) : (r.Contains(e.x, e.z) || r.Contains(e.x, e.z - 1))));

    public static bool Secure(DoorType t) => Catalog.Doors[t].Secure;

    /// <summary>Each part of the security level. The total is capped at 100.</summary>
    public List<SecLine> SecurityLines()
    {
        var list = new List<SecLine>();
        // perimeter: how much of the plot edge is fenced, and how strong the fence is
        var fenced = 0; var strength = 0;
        for (var i = 0; i < Fences.Length; i++)
        {
            if (Fences[i] == 0) continue;
            fenced++; strength += Catalog.Fences[(FenceType)Fences[i]].Security;
        }
        var need = PlotSize * 3;
        var fence = fenced == 0 ? 0 : (int)Math.Round(Math.Min(1, fenced / (double)need) * (strength / (double)fenced) / 4 * 18);
        list.Add(new SecLine("sec_fence", fence, 18));
        // the gate: guard booth, barrier (with number plate recognition) and intercom
        var gate = (Objects.Any(o => o.Kind == ObjKind.GuardBooth) ? 7 : 0) + (Objects.Any(o => o.Kind == ObjKind.Barrier) ? (Done("sec_anpr") ? 6 : 3) : 0)
                   + (Objects.Any(o => o.Kind == ObjKind.Intercom) ? 2 : 0) + (Objects.Count(o => o.Kind == ObjKind.Bollard) >= 4 ? 3 : 0);
        list.Add(new SecLine("sec_gate", Math.Min(18, gate), 18));
        // cameras on poles outside, and inside in the halls and the MMR
        var poles = Objects.Count(o => o.Kind == ObjKind.CameraPole);
        var secure = SecureRooms.ToList();
        var domes = secure.Count == 0 ? 0 : secure.Count(r => Objects.Any(o => o.Kind == ObjKind.CameraDome && o.Floor == r.Floor && r.Contains(o.X, o.Z))) / (double)secure.Count;
        var cams = (int)Math.Round(Math.Min(8, poles * 2) + domes * 8) + (Done("sec_ai_video") && poles + domes > 0 ? 4 : 0);
        list.Add(new SecLine("sec_cameras", Math.Min(20, cams), 20));
        // access: badge doors on every hall and MMR, biometric readers, a mantrap at the entrance
        var badged = secure.Count == 0 ? 0 : secure.Count(r => { var ds = DoorsOf(r).ToList(); return ds.Count > 0 && ds.All(d => Secure(d.Type)); }) / (double)secure.Count;
        var access = (int)Math.Round(badged * 10) + (Done("sec_biometrics") && badged > 0 ? 3 : 0) + (Doors.Any(d => d.Type == DoorType.Mantrap) ? 7 : 0);
        list.Add(new SecLine("sec_access", Math.Min(20, access), 20));
        // the security room: video wall and a glass lodge where the guard sits
        var room = (Objects.Any(o => o.Kind == ObjKind.SecurityWall) ? 6 : 0) + (Objects.Any(o => o.Kind == ObjKind.GlassLodge) ? 4 : 0);
        list.Add(new SecLine("sec_room", room, 10));
        // people: guards around the clock, a security manager
        var guards = CountOf(StaffRole.Guard);
        var people = (guards >= 3 ? 8 : guards >= 1 ? 4 : 0) + (Has(Exec.Security) ? 4 : 0);
        list.Add(new SecLine("sec_people", people, 12));
        // policies
        var pol = (PolicyIdCheck ? 3 : 0) + (PolicyEscort ? 4 : 0) + (PolicyOfficeHours ? 3 : 0) + (int)Math.Round(Bonus(ProposalKind.SecurityTech) * 100);
        list.Add(new SecLine("sec_policies", Math.Min(14, pol), 14));
        return list;
    }

    [JsonIgnore] public int SecurityLevel => Math.Min(100, SecurityLines().Sum(l => l.Points));

    public void SetPolicy(int which, bool on)
    {
        switch (which)
        {
            case 0: PolicyIdCheck = on; break;
            case 1: PolicyEscort = on; break;
            default: PolicyOfficeHours = on; break;
        }
        Touch(false);
    }

    /// <summary>Policies need guards: one more around the clock for ID checks, more for escorts as customers grow.</summary>
    [JsonIgnore]
    public int GuardsForPolicies
    {
        get
        {
            var n = 0;
            if (PolicyIdCheck) n += 2;
            if (PolicyEscort) n += 1 + Deals.Where(d => d.State == DealState.Active && !d.Legacy).Sum(d => d.Racks + d.Units / Org.UnitsPerRack) / 150;
            return n;
        }
    }

    /// <summary>Minimum security level per customer type that needs a security clearance.</summary>
    public static int SecurityNeed(CustomerType t) => t switch
    {
        CustomerType.Municipality => 25,
        CustomerType.Bank => 50,
        CustomerType.Government => 55,
        _ => 0,
    };

    /// <summary>Certificates a customer type wants; Fail = it will not sign without, Warn = it counts in tenders and lowers satisfaction.</summary>
    public static (CertKind kind, bool must)[] CertsWanted(CustomerType t) => t switch
    {
        CustomerType.Government => new[] { (CertKind.Government, true), (CertKind.Iso27001, true) },
        CustomerType.Municipality => new[] { (CertKind.Iso27001, false) },
        CustomerType.Bank => new[] { (CertKind.Pci, true), (CertKind.TierIII, false) },
        CustomerType.Cloud or CustomerType.Hyperscaler => new[] { (CertKind.TierIII, false), (CertKind.Iso27001, false) },
        CustomerType.Ai => new[] { (CertKind.TierIII, false) },
        _ => Array.Empty<(CertKind, bool)>(),
    };

    public Certificate Cert(CertKind k) => Certs.FirstOrDefault(c => c.Kind == k);
    public bool HasCert(CertKind k) => Cert(k)?.Valid == true;

    /// <summary>What the auditors check for each certificate, against what is built now.</summary>
    public List<Req> CertChecks(CertKind k)
    {
        var list = new List<Req>();
        var sec = SecurityLevel;
        Req Sec(int min) => new("cert_req_security", sec.ToString(), min.ToString(), sec >= min ? ReqStatus.Ok : ReqStatus.Fail);
        switch (k)
        {
            case CertKind.Iso27001:
                list.Add(Sec(45));
                list.Add(new Req("cert_req_security_mgr", "", "", Has(Exec.Security) ? ReqStatus.Ok : ReqStatus.Fail));
                list.Add(new Req("cert_req_id_check", "", "", PolicyIdCheck ? ReqStatus.Ok : ReqStatus.Fail));
                break;
            case CertKind.TierIII:
            {
                var inf = Infra;
                var ups = inf.Link(PowerLink.Ups).Redundancy;
                list.Add(new Req("cert_req_ups", ups, "N+1", Creative.NoPowerLimit || ups is "N+1" or "2N" ? ReqStatus.Ok : ReqStatus.Fail));
                list.Add(new Req("cert_req_gen", inf.GenRedundancy, "N+1", Creative.NoPowerLimit || inf.GenRedundancy is "N+1" or "2N" ? ReqStatus.Ok : ReqStatus.Fail));
                var halls = inf.Halls.Where(h => h.LiveRacks > 0).ToList();
                var ab = halls.Count > 0 && (Creative.NoPowerLimit || halls.All(h => h.AB));
                list.Add(new Req("cert_req_ab", halls.Count(h => h.AB).ToString(), halls.Count.ToString(), ab ? ReqStatus.Ok : ReqStatus.Fail));
                var carriers = Net.Carriers.Count(c => c.Up && !c.Def.Ix);
                list.Add(new Req("cert_req_carriers", carriers.ToString(), "2", Creative.NoNetworkLimit || carriers >= 2 ? ReqStatus.Ok : ReqStatus.Fail));
                break;
            }
            case CertKind.Pci:
                list.Add(Sec(55));
                list.Add(new Req("cert_req_iso", "", "", HasCert(CertKind.Iso27001) ? ReqStatus.Ok : ReqStatus.Fail));
                list.Add(new Req("cert_req_escort", "", "", PolicyEscort ? ReqStatus.Ok : ReqStatus.Fail));
                break;
            case CertKind.Government:
                list.Add(Sec(65));
                list.Add(new Req("cert_req_iso", "", "", HasCert(CertKind.Iso27001) ? ReqStatus.Ok : ReqStatus.Fail));
                list.Add(new Req("cert_req_mantrap", "", "", Doors.Any(d => d.Type == DoorType.Mantrap) ? ReqStatus.Ok : ReqStatus.Fail));
                list.Add(new Req("cert_req_office_hours", "", "", PolicyOfficeHours ? ReqStatus.Ok : ReqStatus.Fail));
                break;
            case CertKind.Green:
            {
                var pue = Infra.Pue;
                list.Add(new Req("cert_req_green_energy", Catalog.Suppliers[Supplier].Name, "", Catalog.Suppliers[Supplier].Green ? ReqStatus.Ok : ReqStatus.Fail));
                var inv = System.Globalization.CultureInfo.InvariantCulture;
                list.Add(new Req("cert_req_pue", pue > 0 ? pue.ToString("0.00", inv) : "-", "1.45", pue > 0 && pue <= 1.45 + 1e-9 ? ReqStatus.Ok : ReqStatus.Fail));
                break;
            }
        }
        return list;
    }

    public bool PassesAudit(CertKind k) => CertChecks(k).All(r => r.Status != ReqStatus.Fail);

    public string CanAudit(CertKind k)
    {
        if (!Has(Exec.Compliance)) return "msg_needs_compliance";
        var c = Cert(k);
        if (c != null && c.AuditEnds > 0) return "msg_audit_busy";
        if (c != null && c.Valid && c.ExpiresDay - Day > 120) return "msg_cert_valid";
        if (!CanAfford(CertCatalog.Defs[k].Cost)) return "msg_no_money";
        return null;
    }

    /// <summary>Ask the auditors to come. When they finish, the site either passes or not; the fee is paid now.</summary>
    public string RequestAudit(CertKind k)
    {
        var why = CanAudit(k);
        if (why != null) return why;
        var def = CertCatalog.Defs[k];
        Pay("cost_compliance", def.Cost);
        var c = Cert(k);
        if (c == null) { c = new Certificate { Kind = k }; Certs.Add(c); }
        c.AuditEnds = Day + (Creative.InstantBuild ? 0 : def.AuditDays * (Training(Exec.Compliance) >= 1 ? 0.75 : 1));
        if (c.AuditEnds <= Day) FinishAudit(c);
        Touch(false);
        return null;
    }

    void FinishAudit(Certificate c)
    {
        c.AuditEnds = 0;
        var def = CertCatalog.Defs[c.Kind];
        if (PassesAudit(c.Kind))
        {
            var renewed = c.Valid;
            c.Valid = true; c.IssuedDay = Day; c.ExpiresDay = Day + def.ValidDays; c.CheckDay = Day + 365;
            if (!renewed) Reputation = Math.Min(100, Reputation + def.Reputation);
            Notes.Add((renewed ? "msg_cert_renewed" : "msg_cert_passed", def.Key));
            Log("ev_cert_passed", def.Key);
        }
        else
        {
            Notes.Add(("msg_cert_failed", def.Key));
            Log("ev_cert_failed", def.Key);
            if (c.Valid && Day >= c.ExpiresDay) Withdraw(c);
            else if (!c.Valid) Certs.Remove(c);
        }
    }

    void Withdraw(Certificate c)
    {
        c.Valid = false;
        Reputation = Math.Max(0, Reputation - CertCatalog.Defs[c.Kind].Reputation);
        Notes.Add(("msg_cert_withdrawn", CertCatalog.Defs[c.Kind].Key));
        Log("ev_cert_withdrawn", CertCatalog.Defs[c.Kind].Key);
        Certs.Remove(c);
    }

    /// <summary>Audits that finish, yearly surveillance checks and renewals. The compliance manager books renewals by himself.</summary>
    void CertDay()
    {
        foreach (var c in Certs.ToList())
        {
            if (c.AuditEnds > 0 && Day >= c.AuditEnds) { FinishAudit(c); continue; }
            if (!c.Valid) continue;
            if (Day >= c.CheckDay && Day < c.ExpiresDay)
            {
                c.CheckDay = Day + 365;
                if (!PassesAudit(c.Kind)) { Withdraw(c); continue; }
                Log("ev_cert_checked", CertCatalog.Defs[c.Kind].Key);
            }
            // renewals: the compliance manager starts the audit 60 days before the certificate runs out
            if (c.AuditEnds <= 0 && c.ExpiresDay - Day <= 60 && Has(Exec.Compliance) && CanAfford(CertCatalog.Defs[c.Kind].Cost))
            {
                Pay("cost_compliance", CertCatalog.Defs[c.Kind].Cost);
                c.AuditEnds = Day + CertCatalog.Defs[c.Kind].AuditDays;
            }
            else if (Day >= c.ExpiresDay && c.AuditEnds <= 0) Withdraw(c);
            if (c.Valid) Reputation = Math.Min(100, Reputation + 0.01);
        }
        // strict policies need guards; without them the policy is only on paper
        if (PolicyOfficeHours) foreach (var d in Deals.Where(d => d.State == DealState.Active && d.Type is not (CustomerType.Government or CustomerType.Bank or CustomerType.Municipality)))
                d.Satisfaction = Math.Max(0, d.Satisfaction - 0.03);
    }
}
