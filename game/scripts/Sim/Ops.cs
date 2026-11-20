using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace DatacenterArchitect.Sim;

public enum EventKind { CityOutage, Heatwave, CableCut, CustomerBankrupt, FireAlarm, PressVisit, Flood }

/// <summary>A random event that is running: a city-wide power cut, a heatwave, or a hall after a fire alarm.</summary>
public class GameEvent
{
    public EventKind Kind { get; set; }
    public double StartHour { get; set; }
    public double EndHour { get; set; }
    /// <summary>Room id for a fire alarm.</summary>
    public int Target { get; set; }
}

/// <summary>A line in the incident log: faults, repairs, events and people leaving.</summary>
public class LogEntry
{
    public double Hour { get; set; }
    public string Key { get; set; }
    public string Arg { get; set; }
}

/// <summary>Maintenance, faults and repairs, spare parts and random events (step 7).</summary>
public partial class GameState
{
    public List<GameEvent> ActiveEvents { get; set; } = new();
    public List<LogEntry> Incidents { get; set; } = new();
    public Dictionary<Part, int> PartStock { get; set; } = new();
    public Dictionary<Part, int> PartMin { get; set; } = new();
    public List<PartOrder> PartOrders { get; set; } = new();
    public bool AutoOrderParts { get; set; } = true;
    public const int MaxLog = 60, DefaultMinParts = 2, OrderSize = 5;
    /// <summary>Wear that preventive maintenance cannot get below, per policy (light, normal, thorough).</summary>
    public static readonly double[] WearFloor = { 14, 8, 3 };

    // ---------------- events in force ----------------
    GameEvent Active(EventKind k) => ActiveEvents.FirstOrDefault(e => e.Kind == k && Hours < e.EndHour);
    [JsonIgnore] public bool GridDown => Active(EventKind.CityOutage) != null;
    [JsonIgnore] public double OutageElapsed => Active(EventKind.CityOutage) is { } e ? Hours - e.StartHour : 0;
    [JsonIgnore] public bool Heatwave => Active(EventKind.Heatwave) != null;
    /// <summary>Heat rejection outside works less well in a heatwave.</summary>
    [JsonIgnore] public double RejectFactor => Heatwave ? 0.8 : 1;
    /// <summary>A hall where the gas extinguishing system went off: its racks stay down until it is reset.</summary>
    public bool Suppressed(Room r) => ActiveEvents.Any(e => e.Kind is EventKind.FireAlarm or EventKind.Flood && e.Target == r.Id && Hours < e.EndHour);
    /// <summary>The generators run out of diesel in a long outage, unless a generator technician arranges extra deliveries.</summary>
    public bool GeneratorsDry(double fuelHours) => GridDown && OutageElapsed > fuelHours && !HasStaff(StaffRole.GeneratorTech);

    public void Log(string key, string arg = null)
    {
        Incidents.Add(new LogEntry { Hour = Hours, Key = key, Arg = arg });
        if (Incidents.Count > MaxLog) Incidents.RemoveRange(0, Incidents.Count - MaxLog);
    }

    /// <summary>Pay a running cost (not during unlimited money).</summary>
    void Pay(string key, long amount)
    {
        if (amount == 0 || Creative.UnlimitedMoney) return;
        Money -= amount; Book(key, -amount);
    }

    [JsonIgnore] public IEnumerable<PlacedObject> Broken => Objects.Where(o => o.Broken);

    // ---------------- faults ----------------
    /// <summary>Something breaks: it stops working at once. The NOC notices it straight away, otherwise it takes a couple of hours.</summary>
    public void Break(PlacedObject o)
    {
        if (o.Broken || Creative.NoBreakdowns) return;
        o.Broken = true; o.BrokenHour = Hours; o.Noticed = false; o.RepairBy = 0; o.PartUsed = false;
        o.RepairLeft = Staffing.RepairHours(o.Kind);
        if (Working(StaffRole.Noc).Any()) Notice(o);
        InvalidateInfra();
        Touch(false);
    }

    void Notice(PlacedObject o)
    {
        if (o.Noticed) return;
        o.Noticed = true;
        Notes.Add(("msg_fault", o.Def.NameKey));
        Log("ev_fault", "@" + o.Def.NameKey);
    }

    void Fixed(PlacedObject o)
    {
        var by = Staff.FirstOrDefault(e => e.Id == o.RepairBy);
        if (by != null) by.Busy = 0;
        o.Broken = false; o.RepairBy = 0; o.RepairLeft = 0; o.PartUsed = false;
        o.Wear = by?.Trait == Trait.Precise ? 5 : Math.Min(o.Wear, 20);
        Notes.Add(("msg_repaired", o.Def.NameKey));
        Log("ev_repaired", "@" + o.Def.NameKey);
        InvalidateInfra();
        Touch(false);
    }

    bool TakePart(Part p)
    {
        PartStock.TryGetValue(p, out var n);
        if (n <= 0) return false;
        PartStock[p] = n - 1;
        return true;
    }

    /// <summary>Repair someone can start on: free and on duty, in the right role (electrical engineers stand in for generators).</summary>
    Employee FreeTech(ObjKind k)
    {
        var role = Staffing.RepairRole(k);
        if (role == null) return null;
        var e = Working(role.Value).Where(x => x.Busy == 0).OrderByDescending(x => x.Speed).FirstOrDefault();
        if (e == null && role == StaffRole.GeneratorTech) e = Working(StaffRole.Electrical).Where(x => x.Busy == 0).OrderByDescending(x => x.Speed).FirstOrDefault();
        return e;
    }

    bool AnyoneFor(ObjKind k)
    {
        var role = Staffing.RepairRole(k);
        if (role == null) return false;
        return HasStaff(role.Value) || (role == StaffRole.GeneratorTech && HasStaff(StaffRole.Electrical));
    }

    /// <summary>Call a contractor at once for a broken object (also used when nobody of your own can do it).</summary>
    public string CallContractor(PlacedObject o)
    {
        if (!o.Broken || o.RepairBy == -1) return "msg_nothing_changed";
        var cost = Staffing.ContractorCost(o.Kind);
        if (!CanAfford(cost)) return "msg_no_money";
        var by = Staff.FirstOrDefault(e => e.Id == o.RepairBy);
        if (by != null) by.Busy = 0;
        Notice(o);
        Pay("cost_maintenance", cost);
        o.RepairBy = -1; o.ContractorHour = Hours; o.PartUsed = true;
        Touch(false);
        return null;
    }

    /// <summary>Replace an aged UPS or battery string: half the price of a new one, and it is like new.</summary>
    public long ReplaceCost(PlacedObject o) => o.Def.Cost / 2;
    public bool Aged(PlacedObject o) => Staffing.Ages(o.Kind) && Day - o.Installed > Staffing.LifeDays;

    public string Replace(PlacedObject o)
    {
        if (!Staffing.Ages(o.Kind) && !Staffing.Breaks(o.Kind)) return "msg_nothing_changed";
        if (!CanAfford(ReplaceCost(o))) return "msg_no_money";
        Pay("cost_maintenance", ReplaceCost(o));
        if (o.Broken) { var by = Staff.FirstOrDefault(e => e.Id == o.RepairBy); if (by != null) by.Busy = 0; }
        o.Broken = false; o.RepairBy = 0; o.RepairLeft = 0; o.Wear = 0; o.Installed = Day;
        Notes.Add(("msg_replaced", o.Def.NameKey));
        InvalidateInfra();
        Touch(false);
        return null;
    }

    /// <summary>Repairs, events and their effect on customers move on with the clock, in steps of at most an hour.</summary>
    public void UpdateOps(double hours)
    {
        if (ActiveEvents.Count == 0 && !Objects.Any(o => o.Broken)) return;
        var end = Hours; var t = Hours - hours;
        while (t < end - 1e-9)
        {
            var dt = Math.Min(1, end - t);
            t += dt;
            OpsStep(dt, t);
        }
    }

    void OpsStep(double dt, double now)
    {
        var save = Hours; Hours = now;
        var changed = false;
        foreach (var o in Objects.Where(o => o.Broken).OrderBy(o => o.BrokenHour).ToList())
        {
            if (!o.Noticed)
            {
                if (Working(StaffRole.Noc).Any() || Hours - o.BrokenHour >= 2) Notice(o);
                else continue;
            }
            if (o.RepairBy == -1)
            {
                if (Hours - o.ContractorHour >= Staffing.ContractorDelay) o.RepairLeft -= dt;
            }
            else
            {
                var by = o.RepairBy > 0 ? Staff.FirstOrDefault(e => e.Id == o.RepairBy) : null;
                if (by != null && !OnDuty(by)) { by.Busy = 0; o.RepairBy = 0; by = null; }
                if (by == null)
                {
                    by = FreeTech(o.Kind);
                    if (by != null)
                    {
                        by.Busy = o.Id; o.RepairBy = by.Id;
                        if (!o.PartUsed) o.PartUsed = TakePart(Staffing.PartFor(o.Kind));
                    }
                    else if (!AnyoneFor(o.Kind))
                    {
                        // nobody of your own for this job: a contractor is called automatically
                        o.RepairBy = -1; o.ContractorHour = Hours; o.PartUsed = true;
                        Pay("cost_maintenance", Staffing.ContractorCost(o.Kind));
                        Log("ev_contractor", "@" + o.Def.NameKey);
                    }
                }
                if (by != null)
                {
                    var speed = by.Speed * (Staffing.RepairRole(o.Kind) == by.Role ? 1 : 0.6) * (o.PartUsed ? 1 : 1 / 3.0);
                    o.RepairLeft -= dt * speed;
                }
            }
            if (o.RepairLeft <= 0) { Fixed(o); changed = true; }
        }
        // events that end
        foreach (var e in ActiveEvents.Where(e => Hours >= e.EndHour).ToList())
        {
            ActiveEvents.Remove(e);
            var key = e.Kind switch { EventKind.CityOutage => "msg_ev_outage_over", EventKind.Heatwave => "msg_ev_heatwave_over", EventKind.FireAlarm => "msg_ev_fire_reset", _ => null };
            if (key != null) { Notes.Add((key, null)); Log(key.Replace("msg_", "")); }
            changed = true;
        }
        if (changed) InvalidateInfra();
        Incident(dt);
        Hours = save;
    }

    /// <summary>
    /// While something is down, customers notice it within the hour: SLA penalties and a dent in their satisfaction.
    /// The daily check only sees the moment it runs, so short incidents are counted here.
    /// </summary>
    void Incident(double dt)
    {
        if (Creative.UnlimitedMoney && Creative.NoPowerLimit) return;
        var working = WorkingRacks();
        foreach (var d in Deals.Where(d => d.State == DealState.Active && !d.Legacy && d.Product != Product.PerU && d.RackIds.Count > 0))
        {
            var down = d.RackIds.Count(id => !working.Contains(id)) / (double)Math.Max(1, d.Racks);
            if (down <= 0) continue;
            d.Satisfaction = Math.Max(0, d.Satisfaction - 0.6 * down * dt);
            Pay("cost_sla", (long)Math.Round(d.Monthly / 720.0 * down * dt * 0.5));
        }
    }

    // ---------------- the day ----------------
    Random OpsRng(int day) => new(unchecked(Seed * 31 + day * 7919 + 17));

    /// <summary>Wear, preventive maintenance, failures, spare parts and random events. Runs once per game day.</summary>
    void OpsDay(int day, Random rng)
    {
        var halls = Rooms.Where(r => r.Type == RoomType.DataHall && r.Stage == Stage.Done).ToList();
        var cleaners = Staff.Count(e => e.Role == StaffRole.Cleaner);
        var dusty = halls.Count > 0 && cleaners * 2 < halls.Count;
        var policy = Math.Clamp(MaintenancePolicy, 0, 2);
        long maintenance = 0;
        // preventive maintenance per role: how much of the equipment the team gets round to
        var coverage = new Dictionary<StaffRole, double>();
        foreach (var (role, def) in Staffing.Roles.Where(r => r.Value.Capacity > 0))
        {
            var n = Objects.Count(o => def.Maintains.Contains(o.Kind) && o.Status == ObjStatus.Live);
            var cap = Staff.Where(e => e.Role == role && e.TrainingDaysLeft <= 0).Sum(e => e.Speed * def.Capacity);
            if (role == StaffRole.GeneratorTech && cap == 0) cap = 0.3 * Staff.Where(e => e.Role == StaffRole.Electrical).Sum(e => e.Speed) * def.Capacity;
            coverage[role] = n == 0 ? 0 : Math.Min(1, cap / n);
        }
        foreach (var o in Objects)
        {
            var rate = Staffing.WearRate(o.Kind);
            if (rate <= 0 || o.Status != ObjStatus.Live || o.Broken) continue;
            var inHall = o.Kind is ObjKind.Rack or ObjKind.Crah or ObjKind.Pdu;
            var wear = rate * (dusty && inHall ? 1.4 : 1) * (Heatwave && Staffing.PartFor(o.Kind) == Part.Cooling ? 1.5 : 1);
            var role = Staffing.RepairRole(o.Kind);
            var cov = role != null && coverage.TryGetValue(role.Value, out var c) ? c : 0;
            var fix = 0.6 * MaintenanceFactor[policy] * cov;
            if (cov > 0) maintenance += (long)Math.Round(MaintenanceCostPerObject[policy] * cov);
            // maintenance keeps wear down to a floor: thorough maintenance keeps equipment almost like new
            var floor = cov > 0 ? WearFloor[policy] : 0;
            o.Wear = Math.Clamp(o.Wear + wear - fix, Math.Min(o.Wear, floor), 100);
            if (cov > 0 && o.Wear < floor) o.Wear = Math.Min(floor, o.Wear + wear);
            if (Staffing.Ages(o.Kind) && Day - o.Installed > Staffing.LifeDays) o.Wear = Math.Max(o.Wear, Math.Min(100, (Day - o.Installed - Staffing.LifeDays) / 6));
            // new equipment does not break; from a little wear on, the chance rises steeply
            if (!Creative.NoBreakdowns && o.Wear >= 5 && rng.NextDouble() < Staffing.FailRate(o.Kind) * (0.3 + Math.Pow(o.Wear / 50, 2))) Break(o);
        }
        Pay("cost_maintenance", maintenance);
        PartsDay(day);
        if (Options.Events) EventsDay(day, rng, halls);
        // a well kept site: gardeners and guards add a little reputation
        if (HasStaff(StaffRole.Gardener) && Objects.Count(o => o.Kind is ObjKind.Tree or ObjKind.Shrub) >= 10) Reputation = Math.Min(100, Reputation + 0.02);
        if (HasStaff(StaffRole.Guard)) Reputation = Math.Min(100, Reputation + 0.01);
    }

    /// <summary>Spare part deliveries arrive, and stock below the minimum is reordered (the warehouse worker gets them in faster).</summary>
    void PartsDay(int day)
    {
        foreach (var o in PartOrders.Where(o => day >= o.ArriveDay).ToList())
        {
            PartOrders.Remove(o);
            PartStock.TryGetValue(o.Part, out var n);
            PartStock[o.Part] = n + o.Qty;
            Notes.Add(("msg_parts_arrived", "part_" + o.Part.ToString().ToLowerInvariant()));
        }
        if (!AutoOrderParts) return;
        foreach (var p in Enum.GetValues<Part>())
        {
            if (!NeedsPart(p)) continue;
            PartStock.TryGetValue(p, out var have);
            var onOrder = PartOrders.Where(o => o.Part == p).Sum(o => o.Qty);
            if (have + onOrder < MinParts(p)) OrderParts(p, OrderSize);
        }
    }

    /// <summary>Only parts for equipment you actually have are kept in stock.</summary>
    public bool NeedsPart(Part p) => Objects.Any(o => Staffing.Breaks(o.Kind) && Staffing.PartFor(o.Kind) == p);
    public int MinParts(Part p) => PartMin.TryGetValue(p, out var m) ? m : DefaultMinParts;
    public void SetMinParts(Part p, int n) { PartMin[p] = Math.Clamp(n, 0, 20); Touch(false); }
    public int PartLeadDays => Math.Max(1, 4 - Staff.Count(e => e.Role == StaffRole.Logistics)) * (SupplyCrunch ? 2 : 1);

    public string OrderParts(Part p, int qty)
    {
        var cost = Staffing.PartPrice[p] * qty;
        if (!CanAfford(cost)) return "msg_no_money";
        Pay("cost_parts", cost);
        PartOrders.Add(new PartOrder { Part = p, Qty = qty, ArriveDay = Math.Floor(Day) + (Creative.InstantBuild ? 0 : PartLeadDays) });
        Touch(false);
        return null;
    }

    public GameEvent StartEvent(EventKind k, double hours, int target = 0)
    {
        var e = new GameEvent { Kind = k, StartHour = Hours, EndHour = Hours + hours, Target = target };
        ActiveEvents.Add(e);
        InvalidateInfra();
        Touch(false);
        return e;
    }

    void EventsDay(int day, Random rng, List<Room> halls)
    {
        if (day < 10) return; // a quiet start
        if (!GridDown && rng.NextDouble() < OutageWeatherFactor / 90.0)
        {
            StartEvent(EventKind.CityOutage, 2 + rng.Next(7));
            var inf = Infra;
            var ok = inf.ItRunningKw > 0 || inf.LiveRacks == 0;
            Notes.Add((ok ? "msg_ev_outage_gen" : "msg_ev_outage_dark", null));
            Log("ev_outage");
        }
        var month = Date.Month;
        if (!Heatwave && month is >= 6 and <= 8 && rng.NextDouble() < 1 / 25.0)
        {
            StartEvent(EventKind.Heatwave, 24 * (3 + rng.Next(4)));
            Notes.Add(("msg_ev_heatwave", null)); Log("ev_heatwave");
        }
        if (!Creative.NoBreakdowns && rng.NextDouble() < 1 / 150.0)
        {
            var pits = Objects.Where(o => o.Kind == ObjKind.FiberPit && o.Status == ObjStatus.Live && !o.Broken).ToList();
            if (pits.Count > 0)
            {
                var pit = pits[rng.Next(pits.Count)];
                Break(pit); Notice(pit);
                Notes.Add(("msg_ev_cable_cut", null)); Log("ev_cable_cut");
            }
        }
        if (rng.NextDouble() < 1 / 240.0)
        {
            var deals = Deals.Where(d => d.State == DealState.Active && !d.Legacy && !d.Tender).ToList();
            if (deals.Count >= 3)
            {
                var d = deals[rng.Next(deals.Count)];
                d.State = DealState.Ended; d.EndDay = day; d.RackIds.Clear();
                Notes.Add(("msg_ev_bankrupt", DisplayArg(d))); Log("ev_bankrupt", DisplayArg(d));
            }
        }
        if (halls.Count > 0 && rng.NextDouble() < 1 / 120.0)
        {
            var hall = halls[rng.Next(halls.Count)];
            if (HasStaff(StaffRole.FireSafety)) { Notes.Add(("msg_ev_fire_false", null)); Log("ev_fire_false"); }
            else
            {
                StartEvent(EventKind.FireAlarm, 4, hall.Id);
                Pay("cost_maintenance", 35000);
                Notes.Add(("msg_ev_fire_gas", null)); Log("ev_fire_gas");
            }
        }
        if (rng.NextDouble() < 1 / 100.0)
        {
            var good = !Objects.Any(o => o.Broken) && (Staff.Count == 0 || AverageSatisfaction >= 60);
            Reputation = Math.Clamp(Reputation + (good ? 2 : -1), 0, 100);
            Notes.Add((good ? "msg_ev_press_good" : "msg_ev_press_bad", null)); Log(good ? "ev_press_good" : "ev_press_bad");
        }
    }
}
