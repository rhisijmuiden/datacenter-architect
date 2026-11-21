using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DatacenterArchitect.Core;
using DatacenterArchitect.Sim;
using Godot;

namespace DatacenterArchitect.UI;

/// <summary>
/// Company screen with tabs on the left: organisation (directors and managers, training), sales (team, leads, hall zones),
/// contracts, tenders and finance (books, loans, investors, bankruptcy).
/// </summary>
public partial class CompanyPanel : Modal
{
    public const int TabOrg = 0, TabSales = 1, TabContracts = 2, TabTenders = 3, TabFinance = 4, TabBrand = 5;
    static readonly string[] Tabs = { "co_tab_org", "co_tab_sales", "co_tab_contracts", "co_tab_tenders", "co_tab_finance", "co_tab_brand" };
    static readonly string[] TabIcons = { "org", "cat_customers", "contract", "tender", "money", "palette" };

    readonly Func<GameState> _state;
    readonly Action<string> _fail;
    readonly List<Button> _tabBtns = new();
    readonly VBoxContainer _content;
    readonly ScrollContainer _scroll;
    int _tab;
    string _sig;
    double _timer;

    static CultureInfo Culture => Loc.Language == "nl" ? new CultureInfo("nl-NL") : new CultureInfo("en-GB");
    static string N(double v, string f = "N0") => v.ToString(f, Culture);

    public static string DealName(Deal d) => d.NameKey == null ? d.Name : Loc.T(d.NameKey, d.Name);

    public CompanyPanel(Func<GameState> state, Action<string> fail, int tab = 0) : base("co_title", 1080)
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
        tabs.AddChild(DynText(s => Loc.T("co_reputation", N(s.Reputation, "0")), 14, Brand.Ink));
        tabs.AddChild(DynText(s => Loc.T("co_revenue", Ui.Money(s.MonthlyRevenue)), 14, Brand.Ok));
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

    readonly List<(Label l, Func<GameState, string> f)> _dyn = new();

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
            case TabOrg: OrgTab(s); break;
            case TabSales: SalesTab(s); break;
            case TabContracts: ContractsTab(s); break;
            case TabTenders: TendersTab(s); break;
            case TabBrand: BrandTab(s); break;
            default: FinanceTab(s); break;
        }
        NoWrapInRows(_content);
        _scroll.SetDeferred("scroll_vertical", scroll);
    }

    /// <summary>Labels side by side in a row keep their natural width; only the one that fills the row wraps.</summary>
    static void NoWrapInRows(Node n)
    {
        foreach (var c in n.GetChildren())
        {
            if (c is Label l && n is HBoxContainer && (l.SizeFlagsHorizontal & SizeFlags.Expand) == 0) l.AutowrapMode = TextServer.AutowrapMode.Off;
            NoWrapInRows(c);
        }
    }

    string Signature(GameState s) => _tab == TabBrand ? $"{_tab}|{Loc.Language}|{s.Logo.Version}|{s.Options.Company}" :
        $"{_tab}|{Loc.Language}|{(int)s.Day}|{s.Money / 1000}|{s.Execs.Count}:{string.Join(",", s.Execs.Select(x => $"{x.Role}{x.Training}{x.TrainingDaysLeft > 0}"))}|{s.Offices}|{s.Workplaces}|{s.SalesReps}|" +
        string.Join(",", s.Deals.Select(d => $"{d.Id}{d.State}")) + $"|{s.Loans.Count}|{s.InvestorShare}|{s.Rooms.Count}|{s.Supplier}|{s.Revision / 50}";

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
        Button b = null;
        Action run = () => { var why = act(_state()); if (why != null) _fail(why); _sig = null; Refresh(true); };
        b = primary ? Ui.Primary(text, run) : Ui.Secondary(text, run);
        b.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        b.AddThemeFontSizeOverride("font_size", primary ? 15 : 13);
        if (primary) b.CustomMinimumSize = new Vector2(0, 34);
        b.Disabled = !enabled;
        return b;
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

    // ---------------- organisation ----------------
    void OrgTab(GameState s)
    {
        _content.AddChild(Ui.Heading("co_tab_org", 16, Brand.Amber));
        _content.AddChild(Plain(Loc.T("org_help"), 13, Brand.InkDim));
        var offices = Plain(Loc.T("org_offices", s.BoardCount, s.Offices) + "\n" + Loc.T("org_manager_desks", s.ManagerCount, s.SalesReps, s.Workplaces), 14,
            s.Offices > s.BoardCount && s.Workplaces > s.ManagerCount + s.SalesReps ? Brand.Ok : Brand.Warning);
        _content.AddChild(offices);
        if (s.Creative.AllUnlocks) _content.AddChild(Plain(Loc.T("org_all_unlocked"), 14, Brand.Cooling));
        foreach (var e in Org.Chart)
        {
            var def = Org.Execs[e];
            var hire = s.Hire(e);
            var has = s.Has(e);
            var free = !s.Creative.AllUnlocks && s.Grandfathered.Contains(e);
            var depth = e switch { Exec.Ceo => 0, Exec.Procurement or Exec.Legal or Exec.CustomerSuccess or Exec.Facility or Exec.Network or Exec.Security or Exec.ProjectManager => 2, _ => 1 };
            var card = Card(hire != null || free ? Brand.Ok : s.CanHire(e) == null ? Brand.Amber : Brand.Line, out var v);
            var wrap = Ui.Pad(card, depth * 28, 0, 0, 0);
            var head = Row();
            var name = Plain(Loc.T(def.Key), 17, Brand.Ink); name.AddThemeFontOverride("font", Ui.BodySemi); name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            head.AddChild(name);
            string status;
            if (free) status = Loc.T("org_status_legacy");
            else if (s.Creative.AllUnlocks && hire == null) status = Loc.T("org_status_creative");
            else if (hire != null) status = Loc.T("org_status_hired", Ui.Money(def.Salary));
            else
            {
                var missing = def.Requires.Where(r => !s.Has(r)).Select(r => Loc.T(Org.Execs[r].Key)).ToList();
                status = missing.Count > 0 ? Loc.T("org_status_needs", string.Join(", ", missing)) : Loc.T("org_status_open", Ui.Money(def.Salary));
            }
            head.AddChild(Plain(status, 13, hire != null || free ? Brand.Ok : Brand.InkDim));
            v.AddChild(head);
            v.AddChild(Plain(Loc.T(def.Key + "_unlocks"), 13, Brand.InkDim));
            var acts = Row();
            if (hire == null && !free && !s.Creative.AllUnlocks)
            {
                var why = s.CanHire(e);
                var b = Act(Loc.T("org_hire", Ui.Money(def.HireFee)), st => st.HireExec(e), why == null || why == "msg_no_money", true);
                if (why != null) b.TooltipText = Loc.T(why);
                acts.AddChild(b);
                if (why is "msg_needs_office" or "msg_needs_ceo_office") acts.AddChild(Plain(Loc.T(why), 13, Brand.Warning));
            }
            if (hire != null)
            {
                if (hire.TrainingDaysLeft > 0)
                    acts.AddChild(Plain(Loc.T("org_training_busy", Loc.T($"{def.Key}_train_{hire.Training + 1}"), N(Math.Ceiling(hire.TrainingDaysLeft))), 13, Brand.Cooling));
                else if (hire.Training < 2)
                {
                    var lvl = hire.Training;
                    acts.AddChild(Act(Loc.T("org_train", Loc.T($"{def.Key}_train_{lvl + 1}"), Ui.Money(def.TrainingCost[lvl]), def.TrainingDays[lvl]), st => st.Train(e)));
                }
                else acts.AddChild(Plain(Loc.T("org_trained_full"), 13, Brand.Ok));
                acts.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
                acts.AddChild(Act(Loc.T("org_dismiss"), st => st.DismissExec(e), s.CanDismiss(e) == null));
            }
            else if (free && s.Training(e) > 0) acts.AddChild(Plain(Loc.T("org_trained_full"), 13, Brand.Ok));
            if (hire != null && hire.Training > 0)
                v.AddChild(Plain("✓ " + string.Join("   ✓ ", Enumerable.Range(1, hire.Training).Select(i => Loc.T($"{def.Key}_train_{i}"))), 13, Brand.Ok));
            if (acts.GetChildCount() > 0) v.AddChild(acts);
            _content.AddChild(wrap);
        }
        _content.AddChild(Plain(Loc.T("org_later"), 13, Brand.InkDim));
    }

    // ---------------- sales ----------------
    void SalesTab(GameState s)
    {
        _content.AddChild(Ui.Heading("sales_team", 16, Brand.Amber));
        if (!s.Has(Exec.Sales)) { _content.AddChild(Plain(Loc.T("sales_needs_director"), 14, Brand.Warning)); }
        else
        {
            var h = Row();
            h.AddChild(Plain(Loc.T("sales_reps", s.SalesReps, s.Workplaces, Ui.Money(Org.SalesRepSalary)), 15));
            h.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
            h.AddChild(Act("−", st => st.ChangeReps(-1), s.SalesReps > 0));
            h.AddChild(Act("+", st => st.ChangeReps(1), s.CanAddRep(1) == null));
            _content.AddChild(h);
            _content.AddChild(Plain(Loc.T("sales_help"), 13, Brand.InkDim));
        }
        _content.AddChild(new HSeparator());

        // hall zones
        _content.AddChild(Ui.Heading("zones_title", 16, Brand.Ok));
        _content.AddChild(Plain(Loc.T("zones_help"), 13, Brand.InkDim));
        var halls = s.Rooms.Where(r => r.Type == RoomType.DataHall).ToList();
        if (halls.Count == 0) _content.AddChild(Plain(Loc.T("zones_none"), 14, Brand.Warning));
        var owners = s.RackOwners();
        var working = s.WorkingRacks();
        for (var i = 0; i < halls.Count; i++)
        {
            var hall = halls[i];
            var racks = s.Objects.Where(o => o.Kind == ObjKind.Rack && o.Floor == hall.Floor && hall.Contains(o.X, o.Z)).ToList();
            var row = Row();
            row.AddChild(Plain(Loc.T("zones_hall", i + 1, racks.Count, racks.Count(o => owners.ContainsKey(o.Id)), racks.Count(o => working.Contains(o.Id) && !owners.ContainsKey(o.Id))), 14));
            row.GetChild<Label>(0).SizeFlagsHorizontal = SizeFlags.ExpandFill;
            var opt = new OptionButton { CustomMinimumSize = new Vector2(220, 0), AutoTranslateMode = AutoTranslateModeEnum.Disabled };
            foreach (var z in Enum.GetValues<HallZone>()) opt.AddItem(Loc.T("zone_" + z.ToString().ToLowerInvariant()), (int)z);
            opt.Selected = (int)hall.Zone;
            var room = hall;
            opt.ItemSelected += idx => { _state().SetZone(room, (HallZone)(int)idx); _sig = null; };
            row.AddChild(opt);
            row.AddChild(ClientPicker(_state, room));
            _content.AddChild(row);
        }
        _content.AddChild(new HSeparator());

        // leads
        var leads = s.Deals.Where(d => d.State == DealState.Lead).OrderBy(d => d.ExpiresDay).ToList();
        _content.AddChild(Ui.Heading("leads_title", 16, Brand.Amber));
        if (leads.Count == 0) _content.AddChild(Plain(Loc.T(s.Has(Exec.Sales) ? "leads_none" : "leads_no_sales"), 14, Brand.InkDim));
        foreach (var d in leads) _content.AddChild(DealCard(s, d));
    }

    /// <summary>Short text for the customer types a hall takes: everyone, one or two names, or a count.</summary>
    public static string ClientsText(Room hall)
    {
        var c = hall.Clients ?? new List<CustomerType>();
        if (c.Count == 0) return Loc.T("hall_clients_all");
        var first = Loc.T(Org.Customers[c[^1]].Key);
        return c.Count == 1 ? first : $"{first} +{c.Count - 1}";
    }

    /// <summary>
    /// A drop-down with a tick per customer type for one data hall. Everyone is welcome by default; picking a type
    /// while everyone is welcome narrows the hall to that type, and further ticks add or remove types.
    /// </summary>
    public static MenuButton ClientPicker(Func<GameState> state, Room hall)
    {
        var types = Enum.GetValues<CustomerType>();
        var btn = new MenuButton { Flat = false, Alignment = HorizontalAlignment.Left, ClipText = true, CustomMinimumSize = new Vector2(240, 0), AutoTranslateMode = AutoTranslateModeEnum.Disabled, TooltipText = Loc.T("hall_clients_tip") };
        btn.AddThemeStyleboxOverride("normal", Ui.Box(new Color(1, 1, 1, 0.06f), 7, Brand.Line, 1, 9));
        btn.AddThemeStyleboxOverride("hover", Ui.Box(Brand.PanelHover, 7, Brand.Line, 1, 9));
        btn.AddThemeStyleboxOverride("pressed", Ui.Box(Brand.PanelHover, 7, Brand.Amber, 1, 9));
        btn.AddThemeColorOverride("font_color", Brand.Ink);
        var pop = btn.GetPopup();
        pop.HideOnCheckableItemSelection = false;
        pop.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        void Sync()
        {
            btn.Text = ClientsText(hall);
            btn.TooltipText = Loc.T("hall_clients_tip") + (hall.Clients.Count > 1 ? "\n\n" + string.Join(", ", hall.Clients.Select(t => Loc.T(Org.Customers[t].Key))) : "");
            pop.SetItemChecked(0, hall.Clients.Count == 0);
            for (var i = 0; i < types.Length; i++) pop.SetItemChecked(i + 2, hall.Clients.Contains(types[i]));
        }
        pop.AddCheckItem(Loc.T("hall_clients_all"), 0);
        pop.AddSeparator();
        for (var i = 0; i < types.Length; i++) pop.AddCheckItem(Loc.T(Org.Customers[types[i]].Key), i + 1);
        pop.IdPressed += id =>
        {
            var s = state();
            if (s == null) return;
            if (id == 0) s.SetHallClients(hall, Array.Empty<CustomerType>());
            else
            {
                var t = types[id - 1];
                var set = hall.Clients.ToHashSet();
                if (set.Count == 0) set.Add(t);
                else if (!set.Remove(t)) set.Add(t);
                s.SetHallClients(hall, set);
            }
            Sync();
        };
        Sync();
        return btn;
    }

    static string Product(Deal d) => d.Product switch
    {
        Sim.Product.PerU => Loc.T("prod_peru", d.Units),
        Sim.Product.Cage => Loc.T("prod_cage", d.Racks, d.Amp32 ? "32 A" : "16 A"),
        Sim.Product.Hall => Loc.T("prod_hall", d.Racks, d.Amp32 ? "32 A" : "16 A"),
        _ => Loc.T("prod_racks", d.Racks, d.Amp32 ? "32 A" : "16 A"),
    };

    string Needs(Deal d)
    {
        var parts = new List<string>();
        if (d.Product != Sim.Product.PerU) parts.Add(N(d.Kw, "0") + " kW");
        if (d.TwoN) parts.Add("2N"); else if (d.AB) parts.Add("A/B");
        if (d.Gbps > 0) parts.Add(N(d.Gbps, d.Gbps >= 10 ? "0" : "0.#") + " Gbit/s");
        if (d.Carriers > 1) parts.Add(Loc.T("deal_carriers", d.Carriers));
        if (d.Green) parts.Add(Loc.T("deal_green"));
        if (d.Security) parts.Add(Loc.T("deal_security"));
        return string.Join(" · ", parts);
    }

    void Reqs(GameState s, Deal d, VBoxContainer v)
    {
        foreach (var r in s.Check(d))
        {
            var mark = r.Status switch { ReqStatus.Ok => "✓", ReqStatus.Warn => "!", _ => "✕" };
            var color = r.Status switch { ReqStatus.Ok => Brand.Ok, ReqStatus.Warn => Brand.Warning, _ => Brand.Critical };
            var text = $"{mark}  {Loc.T(r.Key)}" + (r.Have != "" || r.Need != "" ? $"  ({(r.Have != "" ? r.Have : "-")}{(r.Need != "" ? " / " + r.Need : "")})" : "");
            v.AddChild(Plain(text, 13, color));
        }
    }

    Control DealCard(GameState s, Deal d)
    {
        var def = Org.Customers[d.Type];
        var card = Card(s.Meets(d) ? Brand.Ok : Brand.Amber, out var v);
        var head = Row();
        var name = Plain(DealName(d), 17); name.AddThemeFontOverride("font", Ui.BodySemi); name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        head.AddChild(name);
        head.AddChild(Plain(Loc.T(def.Key), 13, Brand.InkDim));
        v.AddChild(head);
        v.AddChild(Plain(Product(d) + "  ·  " + Needs(d), 14));
        v.AddChild(Plain(Loc.T("deal_price", Ui.Money(d.Monthly), d.Months, N(Math.Max(0, Math.Ceiling(d.ExpiresDay - s.Day)))), 14, Brand.Ok));
        Reqs(s, d, v);
        var acts = Row();
        acts.AddChild(Act(Loc.T("deal_sign"), st => st.Sign(d), s.CanSign(d) == null, true));
        acts.AddChild(Act(Loc.T("deal_decline"), st => { st.Decline(d); return null; }));
        v.AddChild(acts);
        return card;
    }

    // ---------------- contracts ----------------
    void ContractsTab(GameState s)
    {
        var building = s.Deals.Where(d => d.State == DealState.Building).ToList();
        if (building.Count > 0)
        {
            _content.AddChild(Ui.Heading("contracts_building", 16, Brand.Warning));
            foreach (var d in building)
            {
                var card = Card(Brand.Warning, out var v);
                v.AddChild(Plain(DealName(d) + "  ·  " + Product(d), 16));
                v.AddChild(Plain(Loc.T("contracts_deadline", N(Math.Max(0, Math.Ceiling(d.DeadlineDay - s.Day))), Ui.Money(d.Monthly)), 14, Brand.Warning));
                Reqs(s, d, v);
                _content.AddChild(card);
            }
            _content.AddChild(new HSeparator());
        }
        var active = s.Deals.Where(d => d.State == DealState.Active).OrderByDescending(d => d.Monthly).ToList();
        _content.AddChild(Ui.Heading("contracts_title", 16, Brand.Amber));
        _content.AddChild(Plain(Loc.T("contracts_summary", active.Count, Ui.Money(s.MonthlyRevenue), active.Sum(d => d.RackIds.Count), active.Where(d => d.Product == Sim.Product.PerU).Sum(d => d.Units)), 14));
        if (active.Count == 0) _content.AddChild(Plain(Loc.T("contracts_none"), 14, Brand.InkDim));
        foreach (var d in active)
        {
            var color = d.Satisfaction >= 70 ? Brand.Ok : d.Satisfaction >= 40 ? Brand.Warning : Brand.Critical;
            var card = Card(color, out var v);
            var head = Row();
            var name = Plain(DealName(d), 16); name.AddThemeFontOverride("font", Ui.BodySemi); name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            head.AddChild(name);
            head.AddChild(Plain(d.Legacy ? "" : Loc.T(Org.Customers[d.Type].Key), 13, Brand.InkDim));
            v.AddChild(head);
            v.AddChild(Plain(Product(d) + (d.Legacy ? "" : "  ·  " + Needs(d)), 13, Brand.InkDim));
            var stats = Row(18);
            stats.AddChild(Plain(Loc.T("contracts_monthly", Ui.Money(d.Monthly)), 14, Brand.Ok));
            stats.AddChild(Plain(Loc.T("contracts_served", N(d.Served * 100, "0")), 14, d.Served >= 0.999 ? Brand.Ink : Brand.Critical));
            stats.AddChild(Plain(Loc.T("contracts_satisfaction", N(d.Satisfaction, "0")), 14, color));
            stats.AddChild(Plain(Loc.T("contracts_ends", N(Math.Max(0, Math.Ceiling(d.EndDay - s.Day)))), 14, Brand.InkDim));
            stats.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
            if (!d.Legacy) stats.AddChild(Act(Loc.T("contracts_terminate", Ui.Money(d.Monthly * 3)), st => { st.Terminate(d); return null; }));
            v.AddChild(stats);
            _content.AddChild(card);
        }
    }

    // ---------------- tenders ----------------
    void TendersTab(GameState s)
    {
        _content.AddChild(Ui.Heading("tenders_title", 16, Brand.Amber));
        _content.AddChild(Plain(Loc.T("tenders_help"), 13, Brand.InkDim));
        if (!s.Has(Exec.Legal)) _content.AddChild(Plain(Loc.T("tenders_needs_legal"), 14, Brand.Warning));
        var list = s.Deals.Where(d => d.Tender && d.State is DealState.Tender or DealState.Bid).OrderBy(d => d.ExpiresDay).ToList();
        if (list.Count == 0) _content.AddChild(Plain(Loc.T(s.Has(Exec.Sales) ? "tenders_none" : "leads_no_sales"), 14, Brand.InkDim));
        foreach (var d in list)
        {
            var card = Card(d.State == DealState.Bid ? Brand.Cooling : Brand.Amber, out var v);
            var head = Row();
            var name = Plain(DealName(d), 18); name.AddThemeFontOverride("font", Ui.BodySemi); name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            head.AddChild(name);
            head.AddChild(Plain(Loc.T(Org.Customers[d.Type].Key), 13, Brand.InkDim));
            v.AddChild(head);
            v.AddChild(Plain(Product(d) + "  ·  " + Needs(d), 14));
            v.AddChild(Plain(Loc.T("tender_terms", Ui.Money(d.Monthly), N(d.Kw / 1000, "0.00"), d.Months, d.DeliveryDays), 14, Brand.Ok));
            Reqs(s, d, v);
            var days = N(Math.Max(0, Math.Ceiling(d.ExpiresDay - s.Day)));
            if (d.State == DealState.Bid)
                v.AddChild(Plain(Loc.T("tender_bid_placed", d.BidPercent, Ui.Money(d.Monthly * d.BidPercent / 100), days), 14, Brand.Cooling));
            else
            {
                v.AddChild(Plain(Loc.T("tender_closes", days), 13, Brand.InkDim));
                var acts = Row();
                var ok = s.CanBid(d) == null;
                foreach (var pct in new[] { 85, 100, 115 })
                {
                    var p = pct;
                    acts.AddChild(Act(Loc.T("tender_bid", p, Ui.Money(d.Monthly * p / 100)), st => st.PlaceBid(d, p), ok, p == 100));
                }
                acts.AddChild(Act(Loc.T("deal_decline"), st => { st.Decline(d); return null; }));
                v.AddChild(acts);
            }
            _content.AddChild(card);
        }
        var past = s.Deals.Where(d => d.Tender && d.State == DealState.Lost).ToList();
        if (past.Count > 0)
        {
            _content.AddChild(new HSeparator());
            _content.AddChild(Ui.Heading("tenders_lost", 14));
            foreach (var d in past) _content.AddChild(Plain("✕  " + DealName(d) + "  ·  " + Product(d), 13, Brand.InkDim));
        }
    }

    // ---------------- finance ----------------
    void FinanceTab(GameState s)
    {
        _content.AddChild(Ui.Heading("fin_books", 16, Brand.Amber));
        if (s.Creative.UnlimitedMoney) _content.AddChild(Plain(Loc.T("fin_unlimited"), 14, Brand.Cooling));
        if (s.DaysInRed > 0) _content.AddChild(Plain(Loc.T("fin_in_red", 30 - s.DaysInRed), 15, Brand.Critical));
        var cols = Row(28);
        var cur = s.Books.LastOrDefault(b => b.Month == s.MonthIndex);
        var last = s.Books.LastOrDefault(b => b.Month == s.MonthIndex - 1);
        cols.AddChild(BookColumn(Loc.T("fin_this_month"), cur));
        cols.AddChild(BookColumn(Loc.T("fin_last_month"), last));
        _content.AddChild(cols);
        _content.AddChild(Plain(Loc.T("fin_fixed", Ui.Money(s.SalariesPerMonth), Ui.Money(s.WagesPerDay * 30)), 13, Brand.InkDim));
        _content.AddChild(new HSeparator());

        _content.AddChild(Ui.Heading("fin_loans", 16, Brand.Amber));
        if (!s.Has(Exec.Cfo)) _content.AddChild(Plain(Loc.T("fin_needs_cfo"), 14, Brand.Warning));
        else
        {
            _content.AddChild(Plain(Loc.T("fin_credit", Ui.Money(s.CreditLimit), N(s.LoanRate * 100, "0.0"), GameState.LoanMonths), 14));
            var acts = Row();
            foreach (var size in GameState.LoanSizes)
            {
                var amount = size;
                acts.AddChild(Act(Loc.T("fin_borrow", Ui.Money(amount), Ui.Money(GameState.Annuity(amount, s.LoanRate, GameState.LoanMonths))), st => st.Borrow(amount), s.CanBorrow(amount) == null));
            }
            _content.AddChild(acts);
        }
        foreach (var l in s.Loans)
        {
            var row = Row();
            var t = Plain(Loc.T("fin_loan", Ui.Money(l.Balance), Ui.Money(l.Principal), N(l.Rate * 100, "0.0"), Ui.Money(l.Payment), l.MonthsLeft), 14);
            t.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            row.AddChild(t);
            var loan = l;
            row.AddChild(Act(Loc.T("fin_repay"), st => st.Repay(loan), s.CanAfford(l.Balance)));
            _content.AddChild(row);
        }
        _content.AddChild(new HSeparator());

        _content.AddChild(Ui.Heading("fin_investors", 16, Brand.Amber));
        _content.AddChild(Plain(Loc.T("fin_investors_help"), 13, Brand.InkDim));
        _content.AddChild(Plain(Loc.T("fin_share", N(s.InvestorShare * 100, "0"), Ui.Money(s.Valuation)), 14));
        if (s.Has(Exec.Cfo))
            _content.AddChild(Act(Loc.T("fin_invest", Ui.Money(s.InvestorOffer)), st => st.TakeInvestment(), s.CanInvest() == null));
        _content.AddChild(new HSeparator());

        _content.AddChild(Ui.Heading("fin_energy", 16, Brand.Power));
        var sup = Catalog.Suppliers[s.Supplier];
        var leftDays = sup.Years * 365 - (s.Day - s.EnergyStartDay);
        _content.AddChild(Plain($"{sup.Name} · € {N(s.EnergyPrice(), "0.000")}/kWh · " + (sup.Variable ? Loc.T("supplier_term_free") : leftDays > 0 ? Loc.T("supplier_term_left", N(Math.Ceiling(leftDays))) : Loc.T("supplier_term_done")), 14));
        _content.AddChild(Plain(Loc.T("fin_energy_help"), 13, Brand.InkDim));
    }

    static readonly string[] BookOrder = { "inc_contracts", "inc_network", "inc_remote_hands", "cost_energy", "cost_network", "cost_crew", "cost_salaries", "cost_staff", "cost_maintenance", "cost_parts", "cost_site", "cost_sla", "cost_interest", "cost_build", "fin_loans", "fin_investors", "fin_repay", "fin_dividend" };

    Control BookColumn(string title, MonthBook b)
    {
        var v = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; v.AddThemeConstantOverride("separation", 2);
        v.AddChild(Ui.Heading(title, 13));
        if (b == null) { v.AddChild(Plain(Loc.T("fin_no_data"), 13, Brand.InkDim)); return v; }
        foreach (var key in BookOrder)
        {
            if (!b.Lines.TryGetValue(key, out var val) || val == 0) continue;
            var row = Row();
            var l = Plain(Loc.T("book_" + key), 14, Brand.InkDim); l.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            row.AddChild(l);
            row.AddChild(Plain(Ui.Money(val), 14, val >= 0 ? Brand.Ok : Brand.Ink));
            v.AddChild(row);
        }
        var op = b.Lines.Where(kv => kv.Key.StartsWith("inc_") || kv.Key.StartsWith("cost_") && kv.Key != "cost_build").Sum(kv => kv.Value);
        var tr = Row();
        var tl = Plain(Loc.T("fin_operating"), 15); tl.AddThemeFontOverride("font", Ui.BodySemi); tl.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        tr.AddChild(tl);
        tr.AddChild(Plain(Ui.Money(op), 15, op >= 0 ? Brand.Ok : Brand.Critical));
        v.AddChild(new HSeparator());
        v.AddChild(tr);
        return v;
    }
}
