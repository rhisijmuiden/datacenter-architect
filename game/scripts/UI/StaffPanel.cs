using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DatacenterArchitect.Core;
using DatacenterArchitect.Sim;
using Godot;

namespace DatacenterArchitect.UI;

/// <summary>
/// Staff screen with tabs on the left: team, policy, maintenance and spare parts, faults and events.
/// No micromanagement: the managers hire and plan their teams themselves; the player sets policies and service plans.
/// </summary>
public partial class StaffPanel : Modal
{
    public const int TabTeam = 0, TabPolicy = 1, TabMaintenance = 2, TabIncidents = 3;
    static readonly string[] Tabs = { "st_tab_team", "st_tab_policy", "st_tab_maintenance", "st_tab_incidents" };
    static readonly string[] TabIcons = { "crew", "roster", "wrench", "alert" };

    readonly Func<GameState> _state;
    readonly Action<string> _fail;
    readonly List<Button> _tabBtns = new();
    readonly VBoxContainer _content;
    readonly ScrollContainer _scroll;
    readonly List<(Label l, Func<GameState, string> f)> _dyn = new();
    int _tab;
    string _sig;
    double _timer;

    static CultureInfo Culture => Loc.Language == "nl" ? new CultureInfo("nl-NL") : new CultureInfo("en-GB");
    static string N(double v, string f = "N0") => v.ToString(f, Culture);

    /// <summary>Message arguments: a translation key, or "@key|arg" for a translated pattern with an argument.</summary>
    public static string Arg(string arg)
    {
        if (arg == null) return "";
        if (arg.StartsWith('@')) { var p = arg[1..].Split('|'); return Loc.T(p[0], p.Length > 1 ? p[1] : ""); }
        return Loc.T(arg);
    }

    public StaffPanel(Func<GameState> state, Action<string> fail, int tab = 0) : base("st_title", 1080)
    {
        _state = state; _fail = fail; _tab = Math.Clamp(tab, 0, Tabs.Length - 1);
        var split = new HBoxContainer(); split.AddThemeConstantOverride("separation", 16);
        Body.AddChild(split);
        var tabs = new VBoxContainer { CustomMinimumSize = new Vector2(200, 0) }; tabs.AddThemeConstantOverride("separation", 4);
        for (var i = 0; i < Tabs.Length; i++)
        {
            var idx = i;
            var b = new Button
            {
                Text = Tabs[i], Alignment = HorizontalAlignment.Left, CustomMinimumSize = new Vector2(0, 42),
                Icon = GD.Load<Texture2D>($"res://assets/icons/{TabIcons[i]}.svg"), ExpandIcon = false,
            };
            b.AddThemeConstantOverride("icon_max_width", 18);
            b.AddThemeFontOverride("font", Ui.DisplaySemi); b.AddThemeFontSizeOverride("font_size", 19);
            b.Pressed += () => { _tab = idx; _sig = null; Refresh(true); };
            _tabBtns.Add(b);
            tabs.AddChild(b);
        }
        tabs.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
        tabs.AddChild(DynText(s => Loc.T("st_people", s.Staff.Count), 14, Brand.Ink));
        tabs.AddChild(DynText(s => Loc.T("st_shift_now", Loc.T("shift_" + s.CurrentShift.ToString().ToLowerInvariant())), 14, Brand.Cooling));
        tabs.AddChild(DynText(s => Loc.T("st_pay_month", Ui.Money(s.StaffPayPerMonth)), 14, Brand.InkDim));
        split.AddChild(tabs);
        var tabLine = new VSeparator();
        split.AddChild(tabLine);
        OwnTabs(tabs, tabLine);
        _scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 600), SizeFlagsHorizontal = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _content.AddThemeConstantOverride("separation", 8);
        var pad = Ui.Pad(_content, 0, 0, 14, 0); pad.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _scroll.AddChild(pad);
        split.AddChild(_scroll);
        FillHeight(split, _scroll);
        Refresh(true);
    }

    public override void SelectTab(int tab)
    {
        _tab = Math.Clamp(tab, 0, Tabs.Length - 1); _sig = null;
        _scroll.ScrollVertical = 0;
        Refresh(true);
    }

    Label DynText(Func<GameState, string> f, int size, Color c)
    {
        var l = Plain("", size, c);
        _dyn.Add((l, f));
        return l;
    }

    public override void _Process(double delta)
    {
        _timer -= delta;
        if (_timer > 0) return;
        _timer = 0.5;
        Refresh(false);
    }

    void Refresh(bool force)
    {
        var s = _state();
        if (s == null) return;
        foreach (var (l, f) in _dyn) l.Text = f(s);
        var sig = Signature(s);
        if (!force && sig == _sig) return;
        _sig = sig;
        var scroll = _scroll.ScrollVertical;
        for (var i = 0; i < _tabBtns.Count; i++) Mark(_tabBtns[i], i == _tab);
        foreach (var c in _content.GetChildren()) c.QueueFree();
        switch (_tab)
        {
            case TabTeam: TeamTab(s); break;
            case TabPolicy: PolicyTab(s); break;
            case TabMaintenance: MaintenanceTab(s); break;
            default: IncidentsTab(s); break;
        }
        NoWrapInRows(_content);
        _scroll.SetDeferred("scroll_vertical", scroll);
    }

    static void NoWrapInRows(Node n)
    {
        foreach (var c in n.GetChildren())
        {
            if (c is Label l && n is HBoxContainer && (l.SizeFlagsHorizontal & SizeFlags.Expand) == 0) l.AutowrapMode = TextServer.AutowrapMode.Off;
            NoWrapInRows(c);
        }
    }

    string Signature(GameState s)
    {
        var broken = string.Join(",", s.Objects.Where(o => o.Broken).Select(o => $"{o.Id}:{o.RepairBy}:{(int)o.RepairLeft}:{o.Noticed}"));
        return $"{_tab}|{Loc.Language}|{s.Builders}{s.Fitters}{s.Engineers}|{(int)s.Day}|{s.CurrentShift}|{s.Money / 5000}|{s.Staff.Count}:{string.Join(",", s.Staff.Select(e => $"{e.Id}{e.Level}{e.Shift}{(int)e.Satisfaction / 5}"))}|" +
               $"{s.TrainingBudget}|{s.PayPolicy}|{s.MaintenancePolicy}|{string.Join(",", s.StaffingLevel.Select(x => $"{x.Key}{x.Value}"))}|{s.Support247}{s.Noc247}{s.RemoteHandsService}|{broken}|{s.ActiveEvents.Count}|{s.Incidents.Count}|" +
               $"{string.Join(",", s.PartStock.Select(p => $"{p.Key}{p.Value}"))}|{string.Join(",", s.PartMin.Select(p => $"{p.Key}{p.Value}"))}|{s.PartOrders.Count}|{s.AutoOrderParts}|{s.Execs.Count}|{s.Rooms.Count}|{s.Revision / 50}";
    }

    static void Mark(Button b, bool on)
    {
        if (on)
        {
            var st = Ui.Box(Brand.PanelHover, 7, null, 0, 8);
            st.BorderColor = Brand.Amber; st.BorderWidthLeft = 3;
            b.AddThemeStyleboxOverride("normal", st); b.AddThemeStyleboxOverride("hover", st);
            b.AddThemeColorOverride("font_color", Brand.Ink);
        }
        else { b.RemoveThemeStyleboxOverride("normal"); b.RemoveThemeStyleboxOverride("hover"); b.RemoveThemeColorOverride("font_color"); }
    }

    static Label Plain(string text, int size = 15, Color? color = null)
    {
        var l = Ui.Text(text, size, color ?? Brand.Ink);
        l.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        return l;
    }

    Button Act(string text, Func<GameState, string> act, bool enabled = true, bool primary = false)
    {
        Action run = () => { var why = act(_state()); if (why != null) _fail(why); _sig = null; Refresh(true); };
        var b = primary ? Ui.Primary(text, run) : Ui.Secondary(text, run);
        b.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        b.AddThemeFontSizeOverride("font_size", primary ? 15 : 13);
        if (primary) b.CustomMinimumSize = new Vector2(0, 34);
        b.Disabled = !enabled;
        return b;
    }

    /// <summary>A choice between a few options; the chosen one is highlighted.</summary>
    HBoxContainer Choice(string[] labels, int current, Action<GameState, int> pick)
    {
        var h = Row(6);
        for (var i = 0; i < labels.Length; i++)
        {
            var idx = i;
            var b = Act(labels[i], st => { pick(st, idx); return null; }, true, i == current);
            if (i == current) b.CustomMinimumSize = new Vector2(0, 30);
            h.AddChild(b);
        }
        return h;
    }

    static PanelContainer Card(Color accent, out VBoxContainer v)
    {
        var p = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var st = Ui.Box(new Color(1, 1, 1, 0.035f), 8, Brand.Line, 1, 12);
        st.BorderColor = accent; st.BorderWidthLeft = 3;
        p.AddThemeStyleboxOverride("panel", st);
        v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 4);
        p.AddChild(v);
        return p;
    }

    static HBoxContainer Row(int sep = 8) { var h = new HBoxContainer(); h.AddThemeConstantOverride("separation", sep); return h; }
    static Control Fill() => new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };

    static Color RoleAccent(StaffRole r) => r switch
    {
        StaffRole.Electrical or StaffRole.GeneratorTech => Brand.Power,
        StaffRole.Cooling or StaffRole.Noc => Brand.Cooling,
        StaffRole.NetworkEng => Brand.Fiber,
        StaffRole.FireSafety => Brand.Critical,
        _ => Brand.Ok,
    };

    static string Level(int l) => Loc.T("level_" + l);
    static string TraitText(Trait t) => t == Trait.None ? "" : Loc.T("trait_" + t.ToString().ToLowerInvariant());
    static string ShiftText(Shift s) => Loc.T("shift_" + s.ToString().ToLowerInvariant());
    static Color Mood(double v) => v >= 65 ? Brand.Ok : v >= 40 ? Brand.Warning : Brand.Critical;
    static string When(double hours) => GameState.Epoch.AddHours(hours).ToString("d MMM HH:mm", Culture);

    // ---------------- construction crew (moved here from the build bar in GUI v2) ----------------
    void CrewCard(GameState s)
    {
        var card = Card(Brand.Amber, out var v);
        var name = Plain(Loc.T("crew_title"), 17); name.AddThemeFontOverride("font", Ui.BodySemi);
        v.AddChild(name);
        v.AddChild(Plain(Loc.T("crew_help"), 13, Brand.InkDim));
        var row = Row(24);
        foreach (var (role, count) in new[] { (Role.Builder, s.Builders), (Role.Fitter, s.Fitters), (Role.Engineer, s.Engineers) })
        {
            var box = new VBoxContainer(); box.AddThemeConstantOverride("separation", 0);
            var h = Row(4);
            var label = Plain(Loc.T("crew_" + role.ToString().ToLowerInvariant()), 15); label.CustomMinimumSize = new Vector2(120, 0);
            h.AddChild(label);
            var r = role;
            var minus = new Button { Text = "−", CustomMinimumSize = new Vector2(30, 28), AutoTranslateMode = AutoTranslateModeEnum.Disabled };
            minus.Pressed += () => ChangeCrew(r, -1);
            var plus = new Button { Text = "+", CustomMinimumSize = new Vector2(30, 28), AutoTranslateMode = AutoTranslateModeEnum.Disabled };
            plus.Pressed += () => ChangeCrew(r, 1);
            var n = Plain(count.ToString(), 17); n.CustomMinimumSize = new Vector2(28, 0); n.HorizontalAlignment = HorizontalAlignment.Center;
            h.AddChild(minus); h.AddChild(n); h.AddChild(plus);
            box.AddChild(h);
            box.AddChild(Plain(Ui.Money(Catalog.WagePerDay[role]) + " " + Loc.T("crew_per_day"), 12, Brand.InkDim));
            row.AddChild(box);
        }
        v.AddChild(row);
        _content.AddChild(card);
    }

    void ChangeCrew(Role role, int delta)
    {
        var why = _state().ChangeCrew(role, delta);
        if (why != null) _fail(why);
        _sig = null; Refresh(true);
    }

    // ---------------- team ----------------
    void TeamTab(GameState s)
    {
        _content.AddChild(Ui.Heading("st_tab_team", 16, Brand.Amber));
        _content.AddChild(Plain(Loc.T("st_team_help"), 13, Brand.InkDim));
        if (!s.Has(Exec.Hr)) _content.AddChild(Plain(Loc.T("st_needs_hr"), 14, Brand.Warning));
        var sum = Row(18);
        sum.AddChild(Plain(Loc.T("st_people", s.Staff.Count), 15));
        if (s.Staff.Count > 0) sum.AddChild(Plain(Loc.T("st_mood", N(s.AverageSatisfaction)), 15, Mood(s.AverageSatisfaction)));
        sum.AddChild(Plain(Loc.T("st_desks", s.DeskNeed, s.Workplaces), 15, s.DeskNeed <= s.Workplaces ? Brand.Ink : Brand.Warning));
        _content.AddChild(sum);
        CrewCard(s);
        foreach (var role in Staffing.Order)
        {
            var def = Staffing.Roles[role];
            var people = s.Staff.Where(e => e.Role == role).OrderBy(e => e.Shift).ThenBy(e => e.Id).ToList();
            var locked = s.RoleLocked(role);
            var target = s.Target(role);
            if (people.Count == 0 && (locked == "msg_needs_hr" || (locked == null && target == 0 && s.Recommended(role) == 0) && !Planned(s, role))) continue;
            var card = Card(people.Count >= target && people.Count > 0 ? RoleAccent(role) : locked != null && target > 0 ? Brand.Warning : Brand.Line, out var v);
            var head = Row();
            var name = Plain(Loc.T(def.Key), 17); name.AddThemeFontOverride("font", Ui.BodySemi); name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            head.AddChild(name);
            head.AddChild(Plain(Loc.T("st_count", people.Count, target), 13, Brand.InkDim));
            v.AddChild(head);
            v.AddChild(Plain(Loc.T(def.Key + "_desc"), 13, Brand.InkDim));
            if (locked != null) v.AddChild(Plain(Loc.T(locked), 13, Brand.Warning));
            if (people.Count > 0)
            {
                var cov = s.Coverage(role);
                var info = Row(14);
                if (def.AllDay) info.AddChild(Plain(Loc.T("st_coverage_line", cov[0], cov[1], cov[2]), 13, cov[2] == 0 ? Brand.Warning : Brand.Cooling));
                info.AddChild(Plain(Loc.T("st_levels", people.Count(e => e.Level == 0), people.Count(e => e.Level == 1), people.Count(e => e.Level == 2)), 13, Brand.InkDim));
                var mood = people.Average(e => e.Satisfaction);
                info.AddChild(Plain(Loc.T("st_mood", N(mood)), 13, Mood(mood)));
                v.AddChild(info);
                var names = string.Join(", ", people.Take(8).Select(e => e.Name + (e.Trait != Trait.None ? $" ({TraitText(e.Trait).ToLowerInvariant()})" : "")));
                if (people.Count > 8) names += " " + Loc.T("st_and_more", people.Count - 8);
                var nl = Plain(names, 12, Brand.InkDim); nl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                v.AddChild(nl);
            }
            _content.AddChild(card);
        }
    }

    static bool Planned(GameState s, StaffRole r) => (r == StaffRole.Support && s.Support247) || (r == StaffRole.Noc && s.Noc247) || (r == StaffRole.RemoteHands && s.RemoteHandsService);

    // ---------------- maintenance ----------------
    void MaintenanceTab(GameState s)
    {
        _content.AddChild(Ui.Heading("st_tab_maintenance", 16, Brand.Amber));
        _content.AddChild(Plain(Loc.T("st_maint_help"), 13, Brand.InkDim));
        if (s.Creative.NoBreakdowns) _content.AddChild(Plain(Loc.T("st_no_breakdowns"), 14, Brand.Cooling));
        _content.AddChild(Ui.Heading("st_maint_policy", 13, Brand.Ink));
        _content.AddChild(Choice(new[] { Loc.T("maint_0"), Loc.T("maint_1"), Loc.T("maint_2") }, s.MaintenancePolicy, (st, i) => { st.MaintenancePolicy = i; st.Touch(false); }));
        _content.AddChild(Plain(Loc.T("maint_" + s.MaintenancePolicy + "_desc"), 13, Brand.InkDim));
        _content.AddChild(new HSeparator());

        foreach (var (role, def) in Staffing.Roles.Where(r => r.Value.Capacity > 0))
        {
            var items = s.Objects.Where(o => def.Maintains.Contains(o.Kind) && o.Status == ObjStatus.Live).ToList();
            if (items.Count == 0) continue;
            var people = s.Staff.Where(e => e.Role == role && e.TrainingDaysLeft <= 0).Sum(e => e.Speed);
            var cov = Math.Min(1, people * def.Capacity / items.Count);
            var r = Row(14);
            var n = Plain(Loc.T(def.Key), 14); n.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            r.AddChild(n);
            r.AddChild(Plain(Loc.T("st_items", items.Count), 13, Brand.InkDim));
            r.AddChild(Plain(Loc.T("st_avg_wear", N(items.Average(o => o.Wear))), 13, Wear(items.Average(o => o.Wear))));
            r.AddChild(Plain(Loc.T("st_coverage", N(cov * 100)), 13, cov >= 0.99 ? Brand.Ok : cov > 0 ? Brand.Warning : Brand.Critical));
            _content.AddChild(r);
        }
        var worn = s.Objects.Where(o => Staffing.Breaks(o.Kind) && o.Status == ObjStatus.Live && !o.Broken && o.Wear >= 40).OrderByDescending(o => o.Wear).Take(8).ToList();
        if (worn.Count > 0)
        {
            _content.AddChild(Ui.Heading("st_worn", 13, Brand.Warning));
            foreach (var o in worn)
            {
                var r = Row();
                var n = Plain(Loc.T(o.Def.NameKey) + $"  ({o.X}, {o.Z})", 14); n.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                r.AddChild(n);
                r.AddChild(Plain(N(o.Wear) + "%", 13, Wear(o.Wear)));
                _content.AddChild(r);
            }
        }
        var aged = s.Objects.Where(s.Aged).OrderBy(o => o.Installed).Take(10).ToList();
        if (aged.Count > 0)
        {
            _content.AddChild(Ui.Heading("st_aged", 13, Brand.Warning));
            _content.AddChild(Plain(Loc.T("st_aged_help"), 13, Brand.InkDim));
            foreach (var o in aged)
            {
                var r = Row();
                var n = Plain(Loc.T(o.Def.NameKey) + "  " + Loc.T("st_years", N((s.Day - o.Installed) / 360, "0.0")), 14); n.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                r.AddChild(n);
                r.AddChild(Act(Loc.T("st_replace", Ui.Money(s.ReplaceCost(o))), st => st.Replace(o)));
                _content.AddChild(r);
            }
        }
        _content.AddChild(new HSeparator());

        // spare parts
        _content.AddChild(Ui.Heading("st_parts", 16, Brand.Fiber));
        _content.AddChild(Plain(Loc.T("st_parts_help", s.PartLeadDays), 13, Brand.InkDim));
        var auto = new CheckButton { Text = Loc.T("st_auto_order"), ButtonPressed = s.AutoOrderParts, AutoTranslateMode = AutoTranslateModeEnum.Disabled };
        auto.Toggled += on => { var st = _state(); st.AutoOrderParts = on; st.Touch(false); };
        _content.AddChild(auto);
        var grid = new GridContainer { Columns = 6 };
        grid.AddThemeConstantOverride("h_separation", 18); grid.AddThemeConstantOverride("v_separation", 6);
        foreach (var h in new[] { "st_col_part", "st_col_stock", "st_col_min", "", "st_col_ordered", "" }) grid.AddChild(h == "" ? new Control() : Ui.Heading(h, 12, Brand.InkDim));
        foreach (var p in Enum.GetValues<Part>())
        {
            s.PartStock.TryGetValue(p, out var have);
            var ordered = s.PartOrders.Where(o => o.Part == p).Sum(o => o.Qty);
            var needed = s.NeedsPart(p);
            grid.AddChild(Plain(Loc.T("part_" + p.ToString().ToLowerInvariant()) + (needed ? "" : " · " + Loc.T("st_not_needed")), 14, needed ? Brand.Ink : Brand.InkDim));
            grid.AddChild(Plain(have.ToString(), 14, needed && have == 0 ? Brand.Critical : Brand.Ink));
            grid.AddChild(Plain(s.MinParts(p).ToString(), 14));
            var pm = Row(4);
            var part = p;
            pm.AddChild(Act("−", st => { st.SetMinParts(part, st.MinParts(part) - 1); return null; }));
            pm.AddChild(Act("+", st => { st.SetMinParts(part, st.MinParts(part) + 1); return null; }));
            grid.AddChild(pm);
            grid.AddChild(Plain(ordered > 0 ? ordered.ToString() : "-", 14, Brand.Cooling));
            grid.AddChild(Act(Loc.T("st_order", GameState.OrderSize, Ui.Money(Staffing.PartPrice[p] * GameState.OrderSize)), st => st.OrderParts(part, GameState.OrderSize)));
        }
        _content.AddChild(grid);
    }

    static Color Wear(double w) => w < 40 ? Brand.Ok : w < 70 ? Brand.Warning : Brand.Critical;

    // ---------------- faults and events ----------------
    void IncidentsTab(GameState s)
    {
        _content.AddChild(Ui.Heading("st_faults", 16, Brand.Critical));
        var broken = s.Objects.Where(o => o.Broken).OrderBy(o => o.BrokenHour).ToList();
        if (broken.Count == 0) _content.AddChild(Plain(Loc.T("st_no_faults"), 14, Brand.Ok));
        foreach (var o in broken)
        {
            var card = Card(Brand.Critical, out var v);
            var head = Row();
            var n = Plain(Loc.T(o.Def.NameKey) + $"  ({o.X}, {o.Z})", 15); n.AddThemeFontOverride("font", Ui.BodySemi); n.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            head.AddChild(n);
            head.AddChild(Plain(Loc.T("st_since", When(o.BrokenHour)), 13, Brand.InkDim));
            v.AddChild(head);
            string status;
            var by = s.Staff.FirstOrDefault(e => e.Id == o.RepairBy);
            if (!o.Noticed) status = Loc.T("st_fault_unnoticed");
            else if (o.RepairBy == -1) status = s.Hours - o.ContractorHour < Staffing.ContractorDelay ? Loc.T("st_fault_contractor_coming", N(Staffing.ContractorDelay - (s.Hours - o.ContractorHour), "0.#")) : Loc.T("st_fault_contractor", N(Math.Max(0, o.RepairLeft), "0.#"));
            else if (by != null) status = Loc.T("st_fault_repairing", by.Name, N(Math.Max(0, o.RepairLeft) / Math.Max(0.1, by.Speed * (o.PartUsed ? 1 : 1 / 3.0)), "0.#"));
            else status = Loc.T("st_fault_waiting", Loc.T(Staffing.Roles[Staffing.RepairRole(o.Kind) ?? StaffRole.Electrical].Key));
            v.AddChild(Plain(status, 13, Brand.Ink));
            if (o.Noticed && !o.PartUsed && o.RepairBy != -1) v.AddChild(Plain(Loc.T("st_fault_no_part", Loc.T("part_" + Staffing.PartFor(o.Kind).ToString().ToLowerInvariant())), 13, Brand.Warning));
            if (o.RepairBy != -1)
            {
                var acts = Row();
                acts.AddChild(Act(Loc.T("st_call_contractor", Ui.Money(Staffing.ContractorCost(o.Kind)), N(Staffing.ContractorDelay)), st => st.CallContractor(o)));
                v.AddChild(acts);
            }
            _content.AddChild(card);
        }
        _content.AddChild(new HSeparator());
        _content.AddChild(Ui.Heading("st_events", 16, Brand.Amber));
        if (!s.Options.Events) _content.AddChild(Plain(Loc.T("st_events_off"), 14, Brand.InkDim));
        var active = s.ActiveEvents.Where(e => s.Hours < e.EndHour).ToList();
        if (active.Count == 0 && s.Options.Events) _content.AddChild(Plain(Loc.T("st_no_events"), 14, Brand.Ok));
        foreach (var e in active)
        {
            var r = Row();
            var n = Plain(Loc.T("event_" + e.Kind.ToString().ToLowerInvariant()), 15, Brand.Warning); n.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            r.AddChild(n);
            r.AddChild(Plain(Loc.T("st_event_left", N(e.EndHour - s.Hours, "0.#")), 13, Brand.InkDim));
            _content.AddChild(r);
            _content.AddChild(Plain(Loc.T("event_" + e.Kind.ToString().ToLowerInvariant() + "_desc"), 13, Brand.InkDim));
        }
        var inf = s.Infra;
        if (s.GridDown) _content.AddChild(Plain(Loc.T(inf.Blackout ? "st_outage_dark" : "st_outage_gen", N(inf.FuelHours, "0.#")), 14, inf.Blackout ? Brand.Critical : Brand.Power));
        _content.AddChild(new HSeparator());
        _content.AddChild(Ui.Heading("st_log", 13, Brand.Ink));
        if (s.Incidents.Count == 0) _content.AddChild(Plain(Loc.T("st_log_empty"), 13, Brand.InkDim));
        foreach (var l in Enumerable.Reverse(s.Incidents).Take(30))
        {
            var r = Row(12);
            r.AddChild(Plain(When(l.Hour), 13, Brand.InkDim));
            var t = Plain(Loc.T(l.Key, Arg(l.Arg)), 13); t.SizeFlagsHorizontal = SizeFlags.ExpandFill; t.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            r.AddChild(t);
            _content.AddChild(r);
        }
    }

    // ---------------- policy ----------------
    static readonly long[] Budgets = { 0, 2500, 5000, 10000, 20000 };

    void PolicyTab(GameState s)
    {
        _content.AddChild(Ui.Heading("st_tab_policy", 16, Brand.Amber));
        _content.AddChild(Plain(Loc.T("st_policy_help"), 13, Brand.InkDim));

        _content.AddChild(Ui.Heading("st_plans", 16, Brand.Cooling));
        Plan(s, "plan_support", GameState.Support247Fee, s.Support247, (st, on) => st.Support247 = on);
        Plan(s, "plan_noc", GameState.Noc247Fee, s.Noc247, (st, on) => st.Noc247 = on);
        Plan(s, "plan_remote_hands", GameState.RemoteHandsFee, s.RemoteHandsService, (st, on) => st.RemoteHandsService = on);
        _content.AddChild(new HSeparator());

        _content.AddChild(Ui.Heading("st_staffing", 16, Brand.Ok));
        _content.AddChild(Plain(Loc.T("st_staffing_help"), 13, Brand.InkDim));
        foreach (var d in Enum.GetValues<Dept>())
        {
            var mgr = Staffing.Manager(d);
            var r = Row();
            var n = Plain(Loc.T("dept_" + d.ToString().ToLowerInvariant()), 14); n.CustomMinimumSize = new Vector2(130, 0);
            r.AddChild(n);
            var dept = d;
            r.AddChild(Choice(new[] { Loc.T("staffing_0"), Loc.T("staffing_1"), Loc.T("staffing_2") }, s.Level(d), (st, i) => st.SetLevel(dept, i)));
            r.AddChild(Fill());
            if (!s.Has(mgr)) r.AddChild(Plain(Loc.T("st_needs_mgr", Loc.T(Org.Execs[mgr].Key)), 12, Brand.Warning));
            _content.AddChild(r);
        }
        _content.AddChild(new HSeparator());

        _content.AddChild(Ui.Heading("st_pay_policy", 16, Brand.Amber));
        _content.AddChild(Choice(new[] { Loc.T("pay_0"), Loc.T("pay_1"), Loc.T("pay_2") }, s.PayPolicy, (st, i) => { st.PayPolicy = i; st.Touch(false); }));
        _content.AddChild(Plain(Loc.T("st_pay_help", Ui.Money(s.StaffPayPerMonth)), 13, Brand.InkDim));
        _content.AddChild(Ui.Heading("st_training_budget", 16, Brand.Amber));
        _content.AddChild(Plain(Loc.T("st_training_help"), 13, Brand.InkDim));
        var idx = Array.IndexOf(Budgets, s.TrainingBudget);
        _content.AddChild(Choice(Budgets.Select(b => b == 0 ? Loc.T("st_none") : Ui.Money(b) + " " + Loc.T("st_per_month")).ToArray(), idx, (st, i) => { st.TrainingBudget = Budgets[i]; st.Touch(false); }));
        _content.AddChild(new HSeparator());

        _content.AddChild(Ui.Heading("st_wellbeing", 16, Brand.Ok));
        _content.AddChild(Plain(Loc.T("st_wellbeing_help"), 13, Brand.InkDim));
        foreach (var (key, pts) in s.Wellbeing())
        {
            var r = Row();
            var n = Plain(Loc.T(key), 14); n.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            r.AddChild(n);
            r.AddChild(Plain((pts > 0 ? "+" : "") + pts, 14, pts > 0 ? Brand.Ok : pts < 0 ? Brand.Critical : Brand.InkDim));
            _content.AddChild(r);
        }
    }

    void Plan(GameState s, string key, long fee, bool on, Action<GameState, bool> set)
    {
        var card = Card(on ? Brand.Cooling : Brand.Line, out var v);
        var head = Row();
        var cb = new CheckButton { Text = Loc.T(key), ButtonPressed = on, AutoTranslateMode = AutoTranslateModeEnum.Disabled, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        cb.AddThemeFontOverride("font", Ui.BodySemi);
        cb.Toggled += t => { var st = _state(); set(st, t); st.Touch(false); _sig = null; Refresh(true); };
        head.AddChild(cb);
        head.AddChild(Plain(Loc.T("st_plan_fee", Ui.Money(fee)), 13, Brand.InkDim));
        v.AddChild(head);
        v.AddChild(Plain(Loc.T(key + "_desc"), 13, Brand.InkDim));
        _content.AddChild(card);
    }
}
