using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace DatacenterArchitect.Sim;

/// <summary>Operational staff (step 7). Construction crews (builders, fitters, engineers) stay separate in the crew tray.</summary>
public enum StaffRole
{
    Support, Electrical, Cooling, NetworkEng, Noc, Guard, Logistics,
    Receptionist, Cleaner, RemoteHands, FireSafety, Canteen, GeneratorTech, Gardener, Helpdesk,
    // step 8
    Researcher,
    // step 9: SOC analysts watch for cyberattacks, hosting engineers run the own hosting company
    SocAnalyst, HostingEngineer,
}

/// <summary>Department that hires the role: its manager must be in place (and HR for any hire).</summary>
public enum Dept { Operations, Facility, Network, Security, Research }

public enum Trait { None, Fast, Precise, NightOwl, Loyal }

/// <summary>Three shifts of eight hours: 07-15, 15-23 and 23-07.</summary>
public enum Shift { Day, Evening, Night }

/// <summary>
/// A staff role: department, monthly salary for a medior, whether it is staffed around the clock, the room it needs before you can hire,
/// world colour, what it maintains and repairs, how many of those one person can look after, and whether it needs a desk.
/// </summary>
public record StaffDef(StaffRole Role, string Key, Dept Dept, long Salary, bool AllDay, RoomType? Room, string Color,
    ObjKind[] Maintains, int Capacity, bool Desk);

public class Employee
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public StaffRole Role { get; set; }
    /// <summary>0 = junior, 1 = medior, 2 = senior.</summary>
    public int Level { get; set; }
    public Trait Trait { get; set; }
    public long Salary { get; set; }
    public double Satisfaction { get; set; } = 70;
    public Shift Shift { get; set; }
    public double TrainingDaysLeft { get; set; }
    public double HiredDay { get; set; }
    /// <summary>Candidates only: the day the candidate takes another job.</summary>
    public double ExpiresDay { get; set; }
    /// <summary>Repair the person is working on (object id), 0 when free.</summary>
    public int Busy { get; set; }
    [JsonIgnore] public StaffDef Def => Staffing.Roles[Role];
    [JsonIgnore] public double Speed => Staffing.LevelSpeed[Math.Clamp(Level, 0, 2)] * (Trait == Trait.Fast ? 1.2 : 1);
}

/// <summary>Spare part categories. A repair uses one part; without it the repair waits for a rush delivery and takes three times as long.</summary>
public enum Part { Server, Cooling, Electrical, Battery, Optics }

public class PartOrder
{
    public Part Part { get; set; }
    public int Qty { get; set; }
    public double ArriveDay { get; set; }
}

public static class Staffing
{
    static readonly ObjKind[] Nothing = Array.Empty<ObjKind>();
    public static readonly double[] LevelSpeed = { 0.8, 1.0, 1.25 };
    public static readonly double[] LevelPay = { 0.8, 1.0, 1.3 };
    public static readonly long[] TrainCost = { 3500, 7000 };

    public static readonly Dictionary<StaffRole, StaffDef> Roles = new()
    {
        [StaffRole.Support] = new(StaffRole.Support, "staff_support", Dept.Operations, 4300, true, null, "#eef3f6", new[] { ObjKind.Rack }, 150, true),
        [StaffRole.Electrical] = new(StaffRole.Electrical, "staff_electrical", Dept.Facility, 5200, true, null, "#eef3f6",
            new[] { ObjKind.Transformer, ObjKind.Switchgear, ObjKind.Ups, ObjKind.Battery, ObjKind.Pdu, ObjKind.Pdu75, ObjKind.Pdu300 }, 30, false),
        [StaffRole.Cooling] = new(StaffRole.Cooling, "staff_cooling", Dept.Facility, 5000, true, null, "#eef3f6",
            new[] { ObjKind.Crah, ObjKind.Chiller, ObjKind.CoolingTower }, 20, false),
        [StaffRole.NetworkEng] = new(StaffRole.NetworkEng, "staff_network", Dept.Network, 5400, true, null, "#eef3f6",
            new[] { ObjKind.CoreRouter, ObjKind.Spine, ObjKind.Odf, ObjKind.FiberPit }, 30, false),
        [StaffRole.Noc] = new(StaffRole.Noc, "staff_noc", Dept.Network, 4600, true, RoomType.Noc, "#3a4450", Nothing, 0, false),
        [StaffRole.Guard] = new(StaffRole.Guard, "staff_guard", Dept.Security, 3600, true, RoomType.Security, "#1f3b66", Nothing, 0, false),
        [StaffRole.Logistics] = new(StaffRole.Logistics, "staff_logistics", Dept.Operations, 3300, false, null, "#ff8a2a", Nothing, 0, false),
        [StaffRole.Receptionist] = new(StaffRole.Receptionist, "staff_reception", Dept.Operations, 3200, false, RoomType.Reception, "#2e8f8a", Nothing, 0, false),
        [StaffRole.Cleaner] = new(StaffRole.Cleaner, "staff_cleaner", Dept.Operations, 2900, false, null, "#7fbf6a", Nothing, 0, false),
        [StaffRole.RemoteHands] = new(StaffRole.RemoteHands, "staff_remote_hands", Dept.Operations, 3900, true, null, "#ffd23e", Nothing, 0, false),
        [StaffRole.FireSafety] = new(StaffRole.FireSafety, "staff_fire", Dept.Facility, 4400, false, null, "#d23c32", Nothing, 0, false),
        [StaffRole.Canteen] = new(StaffRole.Canteen, "staff_canteen", Dept.Operations, 2900, false, RoomType.Canteen, "#f4f4f0", Nothing, 0, false),
        [StaffRole.GeneratorTech] = new(StaffRole.GeneratorTech, "staff_generator", Dept.Facility, 4800, false, null, "#3b6b4a",
            new[] { ObjKind.Generator, ObjKind.DieselTank, ObjKind.UndergroundTank }, 12, false),
        [StaffRole.Gardener] = new(StaffRole.Gardener, "staff_gardener", Dept.Operations, 3000, false, null, "#4f8a3c", Nothing, 0, false),
        [StaffRole.Helpdesk] = new(StaffRole.Helpdesk, "staff_helpdesk", Dept.Operations, 3800, false, null, "#6c5aa8", Nothing, 0, true),
        [StaffRole.Researcher] = new(StaffRole.Researcher, "staff_researcher", Dept.Research, 5600, false, RoomType.Lab, "#f4f6f8", Nothing, 0, false),
        [StaffRole.SocAnalyst] = new(StaffRole.SocAnalyst, "staff_soc", Dept.Security, 5200, true, RoomType.Soc, "#26324a", Nothing, 0, false),
        [StaffRole.HostingEngineer] = new(StaffRole.HostingEngineer, "staff_hosting", Dept.Operations, 4700, false, null, "#2f6f9a", Nothing, 0, false),
    };

    /// <summary>Order in the staff screen: technical roles first, then services.</summary>
    public static readonly StaffRole[] Order =
    {
        StaffRole.Electrical, StaffRole.Cooling, StaffRole.GeneratorTech, StaffRole.FireSafety, StaffRole.NetworkEng, StaffRole.Noc,
        StaffRole.Support, StaffRole.RemoteHands, StaffRole.Guard, StaffRole.Receptionist, StaffRole.Logistics, StaffRole.Cleaner,
        StaffRole.Canteen, StaffRole.Gardener, StaffRole.Helpdesk, StaffRole.Researcher, StaffRole.SocAnalyst, StaffRole.HostingEngineer,
    };

    public static Exec Manager(Dept d) => d switch { Dept.Facility => Exec.Facility, Dept.Network => Exec.Network, Dept.Security => Exec.Security, Dept.Research => Exec.Research, _ => Exec.Operations };

    /// <summary>Who repairs a broken object. Generators fall back to electrical engineers (slower) when there is no generator technician.</summary>
    public static StaffRole? RepairRole(ObjKind k)
    {
        foreach (var (role, def) in Roles) if (def.Maintains.Contains(k)) return role;
        return null;
    }

    public static Part PartFor(ObjKind k) => k switch
    {
        ObjKind.Rack => Part.Server,
        ObjKind.Crah or ObjKind.Chiller or ObjKind.CoolingTower => Part.Cooling,
        ObjKind.Battery or ObjKind.Ups => Part.Battery,
        ObjKind.CoreRouter or ObjKind.Spine or ObjKind.Odf or ObjKind.FiberPit => Part.Optics,
        _ => Part.Electrical,
    };

    public static readonly Dictionary<Part, long> PartPrice = new()
    {
        [Part.Server] = 900, [Part.Cooling] = 2400, [Part.Electrical] = 3200, [Part.Battery] = 2800, [Part.Optics] = 1500,
    };

    /// <summary>Hours of work to repair a broken object, for a medior.</summary>
    public static double RepairHours(ObjKind k) => k switch
    {
        ObjKind.Rack or ObjKind.Battery or ObjKind.Spine or ObjKind.Odf => 2,
        ObjKind.CoreRouter or ObjKind.Pdu or ObjKind.Pdu75 or ObjKind.Pdu300 or ObjKind.DieselTank or ObjKind.UndergroundTank => 3,
        ObjKind.Crah => 4,
        ObjKind.Ups => 5,
        ObjKind.Chiller or ObjKind.CoolingTower or ObjKind.Generator => 6,
        ObjKind.Switchgear or ObjKind.FiberPit => 8,
        ObjKind.Transformer => 10,
        _ => 3,
    };

    /// <summary>Chance per day that an object breaks when it is brand new; wear raises it steeply.</summary>
    public static double FailRate(ObjKind k) => k switch
    {
        ObjKind.Rack => 0.0004,
        ObjKind.Crah => 0.002, ObjKind.Chiller or ObjKind.CoolingTower => 0.0015,
        ObjKind.Generator => 0.0012, ObjKind.Ups => 0.001, ObjKind.Battery => 0.0008, ObjKind.Pdu or ObjKind.Pdu75 or ObjKind.Pdu300 => 0.0006,
        ObjKind.Transformer or ObjKind.Switchgear => 0.0003,
        ObjKind.CoreRouter or ObjKind.Spine => 0.0006, ObjKind.Odf => 0.0002, ObjKind.FiberPit => 0.0001,
        ObjKind.DieselTank or ObjKind.UndergroundTank => 0.0003,
        _ => 0,
    };

    /// <summary>Wear points per day without maintenance (100 = worn out).</summary>
    public static double WearRate(ObjKind k) => k switch
    {
        ObjKind.Crah or ObjKind.Chiller or ObjKind.CoolingTower => 0.5,
        ObjKind.Generator or ObjKind.Ups or ObjKind.Battery => 0.35,
        ObjKind.Rack => 0.15,
        _ => FailRate(k) > 0 ? 0.25 : 0,
    };

    public static bool Breaks(ObjKind k) => FailRate(k) > 0;

    /// <summary>A contractor comes when you have nobody for the job: after this many hours, at this cost.</summary>
    public const double ContractorDelay = 12;
    public static long ContractorCost(ObjKind k) => (long)(RepairHours(k) * 650 + 1500);

    /// <summary>Hardware that ages: after this many days UPS units and batteries keep a minimum wear until replaced.</summary>
    public const double LifeDays = 1800;
    public static bool Ages(ObjKind k) => k is ObjKind.Ups or ObjKind.Battery;

    public static readonly string[] First = { "Sanne", "Daan", "Fatima", "Luuk", "Noor", "Bram", "Yara", "Thijs", "Eva", "Mohamed", "Lisa", "Jesse", "Emma", "Sem", "Anouk", "Ruben",
        "Iris", "Milan", "Femke", "Kevin", "Priya", "Joris", "Esra", "Tom", "Lotte", "Ahmed", "Mila", "Niels", "Sophie", "Stijn", "Jasmijn", "Wouter" };
    public static readonly string[] Last = { "de Vries", "Jansen", "Bakker", "Visser", "Smit", "Meijer", "de Boer", "Mulder", "de Groot", "Bos", "Vos", "Peters", "Hendriks",
        "van Dijk", "Kaya", "Yilmaz", "El Amrani", "Dekker", "Brouwer", "Kok", "Willems", "van Leeuwen", "Postma", "Huisman" };

    public static Employee Candidate(StaffRole role, int id, double day, Random rng)
    {
        var level = rng.NextDouble() switch { < 0.45 => 0, < 0.85 => 1, _ => 2 };
        var trait = rng.NextDouble() < 0.5 ? Trait.None : (Trait)(1 + rng.Next(4));
        var pay = Roles[role].Salary * LevelPay[level] * (0.92 + rng.NextDouble() * 0.16);
        return new Employee
        {
            Id = id, Role = role, Level = level, Trait = trait, Salary = (long)Math.Round(pay / 50) * 50,
            Name = First[rng.Next(First.Length)] + " " + Last[rng.Next(Last.Length)], ExpiresDay = day + 7 + rng.Next(8),
        };
    }
}

/// <summary>Staff: hiring, shifts, training, satisfaction and pay (step 7).</summary>
public partial class GameState
{
    public List<Employee> Staff { get; set; } = new();
    /// <summary>Only kept for savegames made with an early test build; hiring is automatic now.</summary>
    public List<Employee> Candidates { get; set; } = new();
    /// <summary>Training budget per month: spread over staff below senior, who level up when they have had enough.</summary>
    public long TrainingBudget { get; set; }
    /// <summary>0 = below market (-10%), 1 = market, 2 = above market (+15%).</summary>
    public int PayPolicy { get; set; } = 1;
    /// <summary>0 = light, 1 = normal, 2 = thorough preventive maintenance.</summary>
    public int MaintenancePolicy { get; set; } = 1;
    /// <summary>Staffing policy per department: 0 = lean, 1 = normal, 2 = generous. The managers hire and plan the team themselves.</summary>
    public Dictionary<Dept, int> StaffingLevel { get; set; } = new();
    /// <summary>Service plans: 24/7 support for customers, 24/7 NOC monitoring and a remote hands service. Each costs a monthly fee.</summary>
    public bool Support247 { get; set; }
    public bool Noc247 { get; set; }
    public bool RemoteHandsService { get; set; }
    /// <summary>Euros of training each employee has had towards the next level, by employee id.</summary>
    public Dictionary<int, double> TrainingPoints { get; set; } = new();

    public static readonly double[] PayFactor = { 0.9, 1.0, 1.15 };
    public static readonly double[] MaintenanceFactor = { 0.6, 1.0, 1.5 };
    public static readonly long[] MaintenanceCostPerObject = { 4, 8, 14 };
    public const long Support247Fee = 6000, Noc247Fee = 3500, RemoteHandsFee = 1500;
    public int Level(Dept d) => StaffingLevel.TryGetValue(d, out var v) ? v : 1;
    public void SetLevel(Dept d, int v) { StaffingLevel[d] = Math.Clamp(v, 0, 2); Touch(false); }
    [JsonIgnore] public long PlansPerMonth => (Support247 ? Support247Fee : 0) + (Noc247 ? Noc247Fee : 0) + (RemoteHandsService ? RemoteHandsFee : 0);

    public static Shift ShiftAt(double hours)
    {
        var h = hours % 24;
        return h >= 7 && h < 15 ? Shift.Day : h >= 15 && h < 23 ? Shift.Evening : Shift.Night;
    }

    [JsonIgnore] public Shift CurrentShift => ShiftAt(Hours);
    public bool OnDuty(Employee e) => e.TrainingDaysLeft <= 0 && e.Shift == CurrentShift;
    public IEnumerable<Employee> Working(StaffRole r) => Staff.Where(e => e.Role == r && OnDuty(e));
    public int CountOf(StaffRole r) => Staff.Count(e => e.Role == r);
    public bool HasStaff(StaffRole r) => Staff.Any(e => e.Role == r);

    [JsonIgnore] public long StaffPayPerMonth => (long)Math.Round(Staff.Sum(e => e.Salary) * PayFactor[Math.Clamp(PayPolicy, 0, 2)]);

    /// <summary>Why a role cannot be hired now (null = it can).</summary>
    public string RoleLocked(StaffRole r)
    {
        var def = Staffing.Roles[r];
        if (!Has(Exec.Hr)) return "msg_needs_hr";
        if (!Has(Staffing.Manager(def.Dept))) return def.Dept switch
        {
            Dept.Facility => "msg_needs_facility_mgr", Dept.Network => "msg_needs_network_mgr", Dept.Security => "msg_needs_security_mgr", Dept.Research => "msg_needs_research_mgr", _ => "msg_needs_ops",
        };
        if (def.Room is RoomType room)
        {
            var ok = Rooms.Any(x => x.Type == room && x.Stage == Stage.Done);
            if (r == StaffRole.Guard) ok |= Objects.Any(o => o.Kind == ObjKind.GuardBooth);
            if (!ok) return room switch
            {
                RoomType.Noc => "msg_staff_needs_noc", RoomType.Security => "msg_needs_security_room", RoomType.Reception => "msg_needs_reception", RoomType.Lab => "msg_needs_lab", RoomType.Soc => "msg_needs_soc", _ => "msg_needs_canteen",
            };
        }
        return null;
    }

    /// <summary>How many people of a role the site needs: maintenance load, customers, halls, offices and visitors.</summary>
    public int Recommended(StaffRole r)
    {
        var def = Staffing.Roles[r];
        int n;
        if (def.Capacity > 0)
        {
            var count = Objects.Count(o => def.Maintains.Contains(o.Kind) && o.Status == ObjStatus.Live);
            n = (int)Math.Ceiling(count / (double)def.Capacity);
        }
        else
        {
            var halls = Rooms.Count(x => x.Type == RoomType.DataHall && x.Stage == Stage.Done);
            var rented = Deals.Where(d => d.State == DealState.Active && !d.Legacy).Sum(d => d.Racks + d.Units / Org.UnitsPerRack);
            n = r switch
            {
                StaffRole.Noc => Noc247 ? 3 : Objects.Any(o => o.Kind == ObjKind.NocWall) ? 1 : 0,
                StaffRole.Guard => Has(Exec.Security) ? 1 + GuardsForPolicies : 0,
                StaffRole.Researcher => LabBenches,
                StaffRole.Receptionist => Deals.Count(d => d.State == DealState.Active) >= 4 ? 1 : 0,
                StaffRole.Cleaner => (int)Math.Ceiling(halls / 2.0),
                StaffRole.RemoteHands => RemoteHandsService ? Math.Max(1, (int)Math.Ceiling(rented / 120.0)) : 0,
                StaffRole.FireSafety => halls >= 2 ? 1 : 0,
                StaffRole.Canteen => Staff.Count + Execs.Count + SalesReps >= 15 ? 1 : 0,
                StaffRole.Gardener => Objects.Count(o => o.Kind is ObjKind.Tree or ObjKind.Shrub) >= 20 ? 1 : 0,
                StaffRole.Helpdesk => Staff.Count(e => e.Def.Desk) + Execs.Count + SalesReps >= 12 ? 1 : 0,
                StaffRole.Logistics => Math.Max(LiveRacks >= 80 ? 1 : 0, WarehouseWorkersWanted),
                StaffRole.SocAnalyst => SocDesks > 0 ? Math.Max(Soc247 ? 3 : 1, SocDesks) : 0,
                StaffRole.HostingEngineer => HostingEngineersWanted,
                _ => 0,
            };
        }
        // round-the-clock roles want someone on every shift once the site needs them at all
        if (def.AllDay && n > 0 && def.Capacity > 0 && LiveRacks >= 60) n = Math.Max(n, 3);
        if (r == StaffRole.Support && Support247) n = Math.Max(n, 3);
        return n;
    }

    /// <summary>The team size the manager aims for: the advice, adjusted by the department's staffing policy.</summary>
    public int Target(StaffRole r)
    {
        var rec = Recommended(r);
        if (rec == 0) return 0;
        var def = Staffing.Roles[r];
        return Level(def.Dept) switch
        {
            0 => Math.Max(1, (int)Math.Floor(rec * 0.75)),
            2 => Math.Max(def.AllDay ? 3 : rec, (int)Math.Ceiling(rec * 1.25)),
            _ => rec,
        };
    }

    void LevelUp(Employee e)
    {
        e.Level = Math.Min(2, e.Level + 1); e.TrainingDaysLeft = 0;
        e.Salary = (long)Math.Round(e.Salary * Staffing.LevelPay[e.Level] / Staffing.LevelPay[e.Level - 1] / 50) * 50;
        e.Satisfaction = Math.Min(100, e.Satisfaction + 10);
        TrainingPoints.Remove(e.Id);
        Log("ev_staff_trained", e.Name);
        Roster();
    }

    /// <summary>
    /// Automatic roster: round-the-clock roles are spread over day, evening and night (day first, then night, then evening),
    /// people with night-shift experience take the nights; other roles work days. Nobody needs to plan this by hand.
    /// </summary>
    public void Roster()
    {
        foreach (var g in Staff.GroupBy(e => e.Role))
        {
            if (!Staffing.Roles[g.Key].AllDay) { foreach (var e in g) e.Shift = Shift.Day; continue; }
            var order = new[] { Shift.Day, Shift.Night, Shift.Evening };
            var people = g.OrderByDescending(e => e.Trait == Trait.NightOwl).ThenBy(e => e.Id).ToList();
            var slots = Enumerable.Range(0, people.Count).Select(i => order[i % 3]).ToList();
            // night owls first get the night slots
            var nights = slots.Count(s => s == Shift.Night);
            var owls = people.Where(e => e.Trait == Trait.NightOwl).Take(nights).ToList();
            foreach (var e in owls) e.Shift = Shift.Night;
            var rest = people.Except(owls).ToList();
            var left = slots.ToList();
            foreach (var _ in owls) left.Remove(Shift.Night);
            for (var i = 0; i < rest.Count; i++) rest[i].Shift = left[i];
        }
    }

    /// <summary>People on each shift for a role, for the roster screen.</summary>
    public int[] Coverage(StaffRole r)
    {
        var c = new int[3];
        foreach (var e in Staff.Where(e => e.Role == r && e.TrainingDaysLeft <= 0)) c[(int)e.Shift]++;
        return c;
    }

    /// <summary>Desks in offices against the people who need one (sales team, support and helpdesk).</summary>
    [JsonIgnore] public int DeskNeed => SalesReps + Staff.Count(e => e.Def.Desk);

    /// <summary>What makes a workplace pleasant, from -30 to +30; each part is shown in the staff screen.</summary>
    public List<(string key, int points)> Wellbeing()
    {
        var list = new List<(string, int)>();
        list.Add(("wb_pay", PayPolicy switch { 0 => -12, 2 => 10, _ => 0 }));
        var canteen = Rooms.Any(r => r.Type == RoomType.Canteen && r.Stage == Stage.Done);
        list.Add(("wb_canteen", canteen ? (HasStaff(StaffRole.Canteen) ? 8 : 3) : -4));
        var drinks = Objects.Any(o => o.Kind is ObjKind.Coffee or ObjKind.Vending);
        list.Add(("wb_coffee", drinks ? 3 : -2));
        var people = Staff.Count + Execs.Count + SalesReps;
        var parking = 0;
        for (var i = 0; i < Surfaces.Length; i++) if (Surfaces[i] == (byte)Surface.Parking) parking++;
        list.Add(("wb_parking", parking >= people / 2 ? 3 : -5));
        list.Add(("wb_desks", DeskNeed <= Workplaces ? 2 : -10));
        if (people >= 12) list.Add(("wb_helpdesk", HasStaff(StaffRole.Helpdesk) ? 3 : -4));
        if (TrainingBudget > 0) list.Add(("wb_training", 4));
        if (Objects.Count(o => o.Kind is ObjKind.Tree or ObjKind.Shrub or ObjKind.Plant or ObjKind.Bench or ObjKind.ParkBench or ObjKind.PicnicTable or ObjKind.TerraceSet) >= 10) list.Add(("wb_green", 2));
        return list;
    }

    /// <summary>Workload of a role: 1 is a normal day, above 1.3 people get stressed.</summary>
    public double Workload(StaffRole r)
    {
        var n = CountOf(r);
        var need = Recommended(r);
        return n == 0 ? 0 : need / (double)n;
    }

    public double AverageSatisfaction => Staff.Count == 0 ? 0 : Staff.Average(e => e.Satisfaction);

    /// <summary>Once a day: new candidates, training, satisfaction, people leaving and the managers' automatic hiring.</summary>
    void StaffDay(int day, Random rng)
    {
        // training: courses that finish, and the monthly budget spread over everyone below senior
        foreach (var e in Staff.Where(e => e.TrainingDaysLeft > 0).ToList())
        {
            e.TrainingDaysLeft -= 1;
            if (e.TrainingDaysLeft <= 0) LevelUp(e);
        }
        var learners = Staff.Where(e => e.Level < 2 && e.TrainingDaysLeft <= 0).ToList();
        if (TrainingBudget > 0 && learners.Count > 0)
        {
            var per = TrainingBudget / 30.0 / learners.Count;
            foreach (var e in learners)
            {
                TrainingPoints.TryGetValue(e.Id, out var p);
                p += per;
                if (p >= Staffing.TrainCost[e.Level]) { TrainingPoints.Remove(e.Id); LevelUp(e); }
                else TrainingPoints[e.Id] = p;
            }
        }
        // satisfaction moves towards what the workplace offers; unhappy people leave
        var wb = Wellbeing().Sum(x => x.points);
        // the advised team size per role only changes when someone leaves, so it is worked out once per role, not per person
        var advised = new Dictionary<StaffRole, int>();
        foreach (var e in Staff.ToList())
        {
            var target = 62.0 + wb;
            if (!advised.TryGetValue(e.Role, out var rec)) advised[e.Role] = rec = Recommended(e.Role);
            var have = CountOf(e.Role);
            var load = have == 0 ? 0 : rec / (double)have; // the same as Workload(e.Role)
            if (load > 1.3) target -= Math.Min(20, (load - 1) * 15);
            if (e.Shift == Shift.Night && e.Trait != Trait.NightOwl) target -= 5;
            if (e.Trait == Trait.Loyal) target += 8;
            target = Math.Clamp(target, 5, 100);
            e.Satisfaction += (target - e.Satisfaction) * 0.08;
            if (e.Satisfaction < 30 && rng.NextDouble() < (30 - e.Satisfaction) / 400)
            {
                Staff.Remove(e);
                advised.Clear();
                foreach (var o in Objects.Where(o => o.RepairBy == e.Id)) o.RepairBy = 0;
                TrainingPoints.Remove(e.Id);
                Notes.Add(("msg_staff_quit", "@" + e.Def.Key + "|" + e.Name));
                Log("ev_staff_quit", e.Name);
                Roster();
            }
        }
        // the managers keep their teams at the size the policy asks for: hiring when short, not renewing contracts when there are too many
        var hired = 0;
        foreach (var r in Staffing.Order)
        {
            var def = Staffing.Roles[r];
            if (RoleLocked(r) != null) continue;
            var target = Target(r);
            var have = CountOf(r);
            for (var i = have; i < target && hired < 12; i++)
            {
                var c = Staffing.Candidate(r, NextId++, day, rng);
                c.HiredDay = day; c.ExpiresDay = 0; c.Satisfaction = c.Trait == Trait.Loyal ? 80 : 70;
                Staff.Add(c); hired++;
            }
            if (have > target + (target == 0 ? 0 : 1))
            {
                var leave = Staff.Where(e => e.Role == r && e.Busy == 0).OrderBy(e => e.Satisfaction).FirstOrDefault();
                if (leave != null) { Staff.Remove(leave); TrainingPoints.Remove(leave.Id); Log("ev_staff_left", leave.Name); }
            }
        }
        if (hired > 0) { Roster(); Notes.Add(("msg_staff_auto_hired", hired.ToString())); Log("ev_staff_hired", hired.ToString()); }
        // service plans: customers notice 24/7 support and monitoring
        if (Support247 && CountOf(StaffRole.Support) >= 3)
        {
            Reputation = Math.Min(100, Reputation + 0.03);
            foreach (var d in Deals.Where(d => d.State == DealState.Active)) d.Satisfaction = Math.Min(100, d.Satisfaction + 0.1);
        }
        if (Noc247 && CountOf(StaffRole.Noc) >= 3) Reputation = Math.Min(100, Reputation + 0.02);
    }

    /// <summary>Remote hands earn money per month for the customer tickets they handle.</summary>
    public const long RemoteHandsFeePerRack = 22;
    public const int RemoteHandsCapacity = 120;
    [JsonIgnore]
    public long RemoteHandsIncome
    {
        get
        {
            var rented = Deals.Where(d => d.State == DealState.Active && !d.Legacy).Sum(d => d.Racks + d.Units / Org.UnitsPerRack);
            if (!RemoteHandsService) return 0;
            var cap = Staff.Where(e => e.Role == StaffRole.RemoteHands).Sum(e => RemoteHandsCapacity * e.Speed);
            return (long)(Math.Min(rented, cap) * RemoteHandsFeePerRack);
        }
    }
}
