using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DatacenterArchitect.Core;
using DatacenterArchitect.Sim;
using Godot;

namespace DatacenterArchitect.UI;

/// <summary>
/// Step 9 screen with tabs on the left: locations (campus land, the map, other sites and countries, datacenters for sale), offices,
/// the hosting company, the market (rivals, market share, stock exchange, news), energy and ESG, risks and key people.
/// The player decides where and how big to grow; the teams do the rest.
/// </summary>
public partial class GrowthPanel : Modal
{
    public const int TabLocations = 0, TabOffices = 1, TabHosting = 2, TabMarket = 3, TabEnergy = 4, TabRisks = 5, TabPeople = 6;
    static readonly string[] Tabs = { "gr_tab_locations", "gr_tab_offices", "gr_tab_hosting", "gr_tab_market", "gr_tab_energy", "gr_tab_risks", "gr_tab_people" };
    static readonly string[] TabIcons = { "globe", "office", "cloud", "chart", "leaf", "shield", "key" };

    readonly Func<GameState> _state;
    readonly Action<string> _fail;
    readonly List<Button> _tabBtns = new();
    readonly VBoxContainer _content;
    readonly StableScroll _scroll;
    readonly List<(Label l, Func<GameState, string> f)> _dyn = new();
    int _tab;
    string _sig;
    double _timer;
    string _country = "nl";

    static CultureInfo Culture => Loc.Language == "nl" ? new CultureInfo("nl-NL") : new CultureInfo("en-GB");
    static string N(double v, string f = "N0") => v.ToString(f, Culture);
    static string Pct(double v) => N(v * 100, "0") + "%";

    public GrowthPanel(Func<GameState> state, Action<string> fail, int tab = 0) : base("gr_title", 1160)
    {
        _state = state; _fail = fail; _tab = Math.Clamp(tab, 0, Tabs.Length - 1);
        var split = new HBoxContainer(); split.AddThemeConstantOverride("separation", 16);
        Body.AddChild(split);
        var tabs = new VBoxContainer { CustomMinimumSize = new Vector2(210, 0) }; tabs.AddThemeConstantOverride("separation", 4);
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
        tabs.AddChild(DynText(s => Loc.T("gr_share_short", Pct(s.MarketShare)), 15, Brand.Amber));
        tabs.AddChild(DynText(s => Loc.T("gr_esg_short", N(s.Esg, "0")), 14, Brand.Ok));
        tabs.AddChild(DynText(s => Loc.T("gr_sites_short", s.Sites.Count + 1), 14, Brand.Ink));
        split.AddChild(tabs);
        var tabLine = new VSeparator();
        split.AddChild(tabLine);
        OwnTabs(tabs, tabLine);
        _scroll = new StableScroll { CustomMinimumSize = new Vector2(0, 620), SizeFlagsHorizontal = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
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
        for (var i = 0; i < _tabBtns.Count; i++) Mark(_tabBtns[i], i == _tab);
        foreach (var c in _content.GetChildren()) c.QueueFree();
        switch (_tab)
        {
            case TabLocations: LocationsTab(s); break;
            case TabOffices: OfficesTab(s); break;
            case TabHosting: HostingTab(s); break;
            case TabMarket: MarketTab(s); break;
            case TabEnergy: EnergyTab(s); break;
            case TabRisks: RisksTab(s); break;
            default: PeopleTab(s); break;
        }
        NoWrapInRows(_content);
    }

    static void NoWrapInRows(Node n)
    {
        foreach (var c in n.GetChildren())
        {
            if (c is Label l && n is HBoxContainer && (l.SizeFlagsHorizontal & SizeFlags.Expand) == 0) l.AutowrapMode = TextServer.AutowrapMode.Off;
            NoWrapInRows(c);
        }
    }

    string Signature(GameState s) =>
        $"{_tab}|{_country}|{Loc.Language}|{(int)s.Day}|{s.Money / 10000}|{s.PlotSize}|{s.Sites.Count}:{string.Join(",", s.Sites.Select(x => $"{x.Occupied}{x.Built}{x.Linked}{x.UpgradeTo}"))}|{string.Join(",", s.OpenCountries)}|" +
        $"{s.Offers.Count}|{s.Leases.Count}|{s.TenantPolicy}|{s.Hosting.Started}{s.Hosting.TotalCustomers}{s.Hosting.Stock}{s.Hosting.BigDeals.Count}{s.Hosting.Campaign}|{string.Join(",", s.Hosting.PriceLevel.Values)}|" +
        $"{s.Listed}|{s.News.Count}:{s.News.FirstOrDefault()?.Day}|{s.Insurance}{s.Soc247}{s.BatteryUse}|{s.KeyPeople.Count}:{string.Join(",", s.KeyPeople.Select(k => $"{k.Candidate}{k.PoachedBy}"))}|{s.Execs.Count}|{s.Revision / 50}";

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

    // ---------------- small building blocks ----------------
    static Label Plain(string text, int size = 15, Color? color = null)
    {
        var l = Ui.Text(text, size, color ?? Brand.Ink);
        l.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        return l;
    }

    static Label Wrapped(string text, int size = 13, Color? color = null)
    {
        var l = Plain(text, size, color ?? Brand.InkDim);
        l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        l.SizeFlagsHorizontal = SizeFlags.ExpandFill;
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

    static PanelContainer Card(Color accent, out VBoxContainer v, float width = 0)
    {
        var p = new PanelContainer { SizeFlagsHorizontal = width > 0 ? SizeFlags.Fill : SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(width, 0) };
        var st = Ui.Box(new Color(1, 1, 1, 0.035f), 8, Brand.Line, 1, 12);
        st.BorderColor = accent; st.BorderWidthLeft = 3;
        p.AddThemeStyleboxOverride("panel", st);
        v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 4);
        p.AddChild(v);
        return p;
    }

    static HBoxContainer Row(int sep = 8) { var h = new HBoxContainer(); h.AddThemeConstantOverride("separation", sep); return h; }

    static ProgressBar Bar(double value, Color c, float width = 0)
    {
        var bar = new ProgressBar { MinValue = 0, MaxValue = 100, Value = value, ShowPercentage = false, CustomMinimumSize = new Vector2(width, 8), SizeFlagsHorizontal = width > 0 ? SizeFlags.Fill : SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        bar.AddThemeStyleboxOverride("fill", Ui.Box(c, 4, null, 0, 0));
        bar.AddThemeStyleboxOverride("background", Ui.Box(new Color(1, 1, 1, 0.08f), 4, null, 0, 0));
        return bar;
    }

    static Color Score(double v) => v >= 65 ? Brand.Ok : v >= 40 ? Brand.Warning : Brand.Critical;

    static Label Title(string text, int size = 17)
    {
        var n = Plain(text, size); n.AddThemeFontOverride("font", Ui.BodySemi); n.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        return n;
    }

    /// <summary>A row of three choices (budget, normal, premium or similar) as toggle buttons.</summary>
    HBoxContainer Choice(string[] keys, int current, Action<GameState, int> set)
    {
        var h = Row(4);
        var group = new ButtonGroup();
        for (var i = 0; i < keys.Length; i++)
        {
            var idx = i;
            var b = new Button { Text = Loc.T(keys[i]), ToggleMode = true, ButtonPressed = i == current, ButtonGroup = group, AutoTranslateMode = AutoTranslateModeEnum.Disabled, CustomMinimumSize = new Vector2(96, 32) };
            b.AddThemeFontSizeOverride("font_size", 13);
            b.Pressed += () => { set(_state(), idx); _sig = null; Refresh(true); };
            h.AddChild(b);
        }
        return h;
    }

    static void Stat(HFlowContainer flow, string label, string value, Color c)
    {
        var p = new PanelContainer { CustomMinimumSize = new Vector2(150, 0) };
        p.AddThemeStyleboxOverride("panel", Ui.Box(new Color(1, 1, 1, 0.04f), 8, Brand.Line, 1, 10));
        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 0);
        var l = Ui.Heading(label, 11); v.AddChild(l);
        var n = Ui.Number(value, 21); n.AddThemeColorOverride("font_color", c); n.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        v.AddChild(n);
        p.AddChild(v);
        flow.AddChild(p);
    }

    static HFlowContainer Stats()
    {
        var f = new HFlowContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        f.AddThemeConstantOverride("h_separation", 8); f.AddThemeConstantOverride("v_separation", 8);
        return f;
    }

    // ---------------- locations ----------------
    void LocationsTab(GameState s)
    {
        _content.AddChild(Ui.Heading("gr_tab_locations", 16, Brand.Amber));
        _content.AddChild(Wrapped(Loc.T("loc_help")));
        // the campus and the land around it
        var land = Card(Brand.Amber, out var lv);
        var lh = Row();
        lh.AddChild(Title(Loc.T("loc_campus", s.Options.Company)));
        lh.AddChild(Plain(Loc.T("loc_plot", s.PlotSize), 14, Brand.InkDim));
        lv.AddChild(lh);
        lv.AddChild(Wrapped(Loc.T("loc_land_help")));
        if (s.NextLandSize > 0)
            lv.AddChild(Act(Loc.T("loc_buy_land", s.NextLandSize, Ui.Money(s.NextLandCost)), st => st.BuyLand(), s.CanBuyLand() == null, true));
        else lv.AddChild(Plain(Loc.T("loc_land_max"), 13, Brand.Ok));
        _content.AddChild(land);

        // the map
        var map = new SiteMap(s, c => { _country = c; _sig = null; Refresh(true); }) { CustomMinimumSize = new Vector2(0, 300), SizeFlagsHorizontal = SizeFlags.ExpandFill, Selected = _country };
        _content.AddChild(map);

        // the chosen country
        var def = Countries.Get(_country);
        var cc = Card(s.CountryOpen(_country) ? Brand.Ok : Brand.Line, out var cv);
        var ch = Row();
        ch.AddChild(Title(Loc.T(def.Key)));
        ch.AddChild(Plain(s.CountryOpen(_country) ? Loc.T("loc_country_open") : Loc.T("loc_country_closed"), 13, s.CountryOpen(_country) ? Brand.Ok : Brand.InkDim));
        cv.AddChild(ch);
        cv.AddChild(Plain(Loc.T("loc_country_stats", Pct(def.Energy), Pct(def.Cost), Pct(def.Demand), Pct(def.Green), N(def.GridDays)), 13, Brand.InkDim));
        if (def.Water >= 0.6) cv.AddChild(Plain(Loc.T("loc_water_stress"), 13, Brand.Warning));
        if (!s.CountryOpen(_country))
        {
            cv.AddChild(Act(Loc.T("loc_open_country", Ui.Money(Countries.CountryOfficeCost)), st => st.OpenCountry(_country), s.CanOpenCountry(_country) == null, true));
            if (!s.Has(Exec.International)) cv.AddChild(Plain(Loc.T("msg_needs_international"), 13, Brand.Warning));
        }
        else
        {
            cv.AddChild(Ui.Heading("loc_build_site", 12, Brand.InkDim));
            var flow = Stats();
            foreach (var size in Countries.Sizes)
            {
                var card = Card(Brand.Power, out var v, 200);
                v.AddChild(Title(Loc.T(size.Key), 15));
                v.AddChild(Plain(Loc.T("loc_size_line", size.Racks, N(size.Racks * GameState.RemoteRackKw / 1000, "0.0")), 12, Brand.InkDim));
                v.AddChild(Plain(Loc.T("loc_size_days", N(s.SiteBuildDays(_country, size.Size))), 12, Brand.InkDim));
                var sz = size.Size;
                v.AddChild(Act(Ui.Money(s.SiteCost(_country, sz)), st => st.BuildSite(_country, sz), s.CanBuildSite(_country, sz) == null));
                flow.AddChild(card);
            }
            cv.AddChild(flow);
            cv.AddChild(Ui.Heading("loc_buy_office", 12, Brand.InkDim));
            var of = Row();
            foreach (var m2 in Countries.OfficeSizes)
            {
                var m = m2;
                of.AddChild(Act(Loc.T("loc_office_size", N(m), Ui.Money(s.OfficeCost(_country, m))), st => st.BuyOffice(_country, m), s.CanBuyOffice(_country, m) == null));
            }
            cv.AddChild(of);
            if (!s.Has(Exec.OfficeManager)) cv.AddChild(Plain(Loc.T("msg_needs_office_mgr"), 12, Brand.InkDim));
        }
        _content.AddChild(cc);

        // your locations
        _content.AddChild(new HSeparator());
        _content.AddChild(Ui.Heading("loc_yours", 15, Brand.Ink));
        if (s.Sites.Count == 0) _content.AddChild(Plain(Loc.T("loc_none"), 14, Brand.InkDim));
        foreach (var site in s.Sites)
        {
            var c = Countries.Get(site.Country);
            var card = Card(site.Kind == SiteKind.Office ? Brand.Cooling : !site.Built ? Brand.Amber : s.Day < site.OfflineUntil ? Brand.Critical : Brand.Ok, out var v);
            var head = Row();
            head.AddChild(Title(site.Kind == SiteKind.Office ? Loc.T("loc_office_name", site.City) : site.Name, 16));
            head.AddChild(Plain(Loc.T(c.Key), 13, Brand.InkDim));
            v.AddChild(head);
            var (inc, cost) = s.SiteDay(site);
            if (site.Kind == SiteKind.Office)
            {
                v.AddChild(Plain(Loc.T("loc_office_line", N(site.M2), Pct(site.Let), Ui.Money((long)Math.Round((inc - cost) * 30))), 13, Brand.Ink));
            }
            else if (!site.Built)
            {
                v.AddChild(Plain(Loc.T("loc_building", Loc.T(Countries.Sizes[site.Size].Key), N(Math.Ceiling(site.BuildDaysLeft))), 13, Brand.Amber));
            }
            else
            {
                var r = Row(10);
                r.AddChild(Plain(Loc.T("loc_racks", site.Occupied, site.Racks), 13));
                r.AddChild(Bar(site.Fill * 100, Brand.Ok));
                r.AddChild(Plain(Loc.T("loc_net_month", Ui.Money((long)Math.Round((inc - cost) * 30))), 13, inc >= cost ? Brand.Ok : Brand.Critical));
                v.AddChild(r);
                var tags = new List<string> { Loc.T(Countries.Sizes[site.Size].Key) };
                if (site.Acquired) tags.Add(Loc.T("loc_acquired"));
                if (site.Linked) tags.Add(Loc.T("loc_linked"));
                if (site.UpgradeTo >= 0) tags.Add(Loc.T("loc_expanding", N(Math.Ceiling(site.BuildDaysLeft))));
                if (s.Day < site.OfflineUntil) tags.Add(Loc.T("loc_offline"));
                v.AddChild(Plain(string.Join(" · ", tags), 12, Brand.InkDim));
                var acts = Row();
                var target = site;
                if (site.Size < Countries.Sizes.Length - 1 && site.UpgradeTo < 0)
                    acts.AddChild(Act(Loc.T("loc_expand", Loc.T(Countries.Sizes[site.Size + 1].Key), Ui.Money(s.UpgradeSiteCost(site))), st => st.UpgradeSite(target), s.CanUpgradeSite(site) == null));
                if (!site.Linked) acts.AddChild(Act(Loc.T("loc_link", Ui.Money(Countries.LinkCost)), st => st.LinkSite(target), s.CanLinkSite(site) == null));
                v.AddChild(acts);
            }
            var sell = site;
            v.AddChild(Act(Loc.T("loc_sell"), st => st.SellSite(sell)));
            _content.AddChild(card);
        }

        // for sale
        _content.AddChild(new HSeparator());
        _content.AddChild(Ui.Heading("loc_for_sale", 15, Brand.Ink));
        _content.AddChild(Wrapped(Loc.T("loc_for_sale_help")));
        if (s.Offers.Count == 0) _content.AddChild(Plain(Loc.T("loc_no_offers"), 14, Brand.InkDim));
        foreach (var o in s.Offers)
        {
            var card = Card(o.Competitor >= 0 ? Brand.Critical : Brand.Amber, out var v);
            var head = Row();
            head.AddChild(Title(o.Name, 16));
            head.AddChild(Plain(Loc.T("loc_offer_ends", N(Math.Max(0, o.ExpiresDay - s.Day))), 12, Brand.InkDim));
            v.AddChild(head);
            v.AddChild(Plain(Loc.T(o.Competitor >= 0 ? "loc_offer_rival" : "loc_offer_indie", o.City, Loc.T(Countries.Get(o.Country).Key), o.Racks, o.Occupied), 13));
            var offer = o;
            v.AddChild(Act(Loc.T("loc_offer_buy", Ui.Money(o.Price)), st => st.BuyOffer(offer), s.CanBuyOffer(o) == null, true));
            _content.AddChild(card);
        }
    }

    // ---------------- offices ----------------
    void OfficesTab(GameState s)
    {
        _content.AddChild(Ui.Heading("gr_tab_offices", 16, Brand.Cooling));
        _content.AddChild(Wrapped(Loc.T("off_help")));
        if (!s.CanLetToCustomers) _content.AddChild(Plain(Loc.T("msg_needs_office_mgr"), 14, Brand.Warning));
        else if (!s.CanLetToOthers) _content.AddChild(Plain(Loc.T("off_only_customers"), 13, Brand.InkDim));
        _content.AddChild(Ui.Heading("off_policy", 13, Brand.InkDim));
        _content.AddChild(Choice(new[] { "off_policy_0", "off_policy_1", "off_policy_2" }, s.TenantPolicy, (st, i) => st.SetTenantPolicy(i)));
        var flow = Stats();
        var rooms = s.RentableOffices.ToList();
        Stat(flow, "off_rooms", rooms.Count.ToString(), Brand.Ink);
        Stat(flow, "off_let", s.Leases.Count.ToString(), Brand.Ok);
        Stat(flow, "off_to_customers", s.Leases.Count(l => l.DealId != 0).ToString(), Brand.Cooling);
        Stat(flow, "off_rent", Ui.Money(s.OfficeRentPerMonth), Brand.Ok);
        _content.AddChild(flow);
        if (rooms.Count == 0) _content.AddChild(Wrapped(Loc.T("off_no_rooms"), 14));
        foreach (var b in s.Buildings.Where(b => rooms.Any(r => r.BuildingId == b.Id)))
        {
            var card = Card(Brand.Cooling, out var v);
            v.AddChild(Title(Loc.T(Catalog.Buildings[b.Kind].NameKey) + $" · {b.W} × {b.D} m", 15));
            foreach (var r in rooms.Where(r => r.BuildingId == b.Id).OrderBy(r => r.Floor).ThenBy(r => r.Id))
            {
                var row = Row(10);
                var n = Plain(Loc.T("off_room", Ui.FloorName(r.Floor), r.Area), 13); n.CustomMinimumSize = new Vector2(220, 0);
                row.AddChild(n);
                var l = s.LeaseOf(r);
                if (l == null) { var f = Plain(Loc.T("off_free"), 13, Brand.InkDim); f.SizeFlagsHorizontal = SizeFlags.ExpandFill; row.AddChild(f); }
                else
                {
                    var who = Plain(Main.ArgText(l.Tenant) + (l.DealId != 0 ? "  · " + Loc.T("off_customer") : "") + (s.WholeBuilding(l) ? "  · " + Loc.T("off_whole") : ""), 13, l.DealId != 0 ? Brand.Cooling : Brand.Ink);
                    who.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                    row.AddChild(who);
                    row.AddChild(Plain(Loc.T("off_month", Ui.Money(l.Monthly)), 13, Brand.Ok));
                    var room = r;
                    row.AddChild(Act(Loc.T("off_end"), st => { st.EndLease(room); return null; }));
                }
                v.AddChild(row);
            }
            _content.AddChild(card);
        }
    }

    // ---------------- hosting ----------------
    static readonly string[] PriceKeys = { "host_price_0", "host_price_1", "host_price_2" };

    void HostingTab(GameState s)
    {
        var h = s.Hosting;
        _content.AddChild(Ui.Heading("gr_tab_hosting", 16, Brand.Cooling));
        _content.AddChild(Wrapped(Loc.T("host_help")));
        if (!h.Started)
        {
            if (!s.Has(Exec.Hosting)) _content.AddChild(Plain(Loc.T("msg_needs_hosting_dir"), 14, Brand.Warning));
            _content.AddChild(Act(Loc.T("host_start", Ui.Money(HostingCatalog.StartCost)), st => st.StartHosting(), s.CanStartHosting() == null, true));
            return;
        }
        _content.AddChild(Title(h.Name, 20));
        var flow = Stats();
        Stat(flow, "host_customers", N(h.TotalCustomers), Brand.Ink);
        Stat(flow, "host_revenue", Ui.Money(s.HostingMonthly), Brand.Ok);
        Stat(flow, "host_servers", $"{N(h.TotalServers)} / {N(s.ServerCapacity)}", h.TotalServers >= s.ServerCapacity ? Brand.Warning : Brand.Cooling);
        Stat(flow, "host_stock", $"{h.Stock} / {s.StockCapacity}", h.Stock == 0 ? Brand.Warning : Brand.Ink);
        Stat(flow, "host_uptime", N(h.Uptime * 100, "0.00") + "%", h.Uptime >= 0.999 ? Brand.Ok : Brand.Critical);
        Stat(flow, "host_satisfaction", N(h.Satisfaction, "0"), Score(h.Satisfaction));
        Stat(flow, "host_tickets", N(h.Tickets, "0"), Brand.Ink);
        Stat(flow, "host_churn", N(h.Churned), Brand.InkDim);
        _content.AddChild(flow);
        if (h.Revenue.Count > 1)
        {
            _content.AddChild(Ui.Heading("host_revenue_chart", 12));
            _content.AddChild(new MiniChart(h.Revenue.Select(v => (double)v).ToList(), Brand.Ok) { CustomMinimumSize = new Vector2(0, 90), SizeFlagsHorizontal = SizeFlags.ExpandFill });
        }
        if (s.CloudRacksBuilt == 0) _content.AddChild(Wrapped(Loc.T("host_no_racks"), 14, Brand.Warning));
        _content.AddChild(Plain(Loc.T("host_racks_line", s.CloudRacks, s.CloudRacksBuilt, HostingCatalog.ServersPerRack), 13, Brand.InkDim));
        _content.AddChild(Plain(Loc.T("host_warehouse_line", s.Workbenches, s.PalletRacks, s.CountOf(StaffRole.Logistics), s.PrepPerDay, s.CountOf(StaffRole.HostingEngineer)), 13, Brand.InkDim));

        _content.AddChild(new HSeparator());
        _content.AddChild(Ui.Heading("host_products", 15, Brand.Ink));
        foreach (var def in HostingCatalog.Products)
        {
            var p = def.Product;
            var open = s.ProductOpen(p);
            var card = Card(open ? Brand.Cooling : Brand.Line, out var v);
            var head = Row();
            head.AddChild(Title(Loc.T(def.Key), 15));
            head.AddChild(Plain(open ? Loc.T("host_product_line", N(h.Count(h.Customers, p)), N(h.Count(h.Servers, p)), Ui.Money(s.HostPrice(p))) : Loc.T("host_needs_research", Loc.T("rs_" + def.Research)), 13, open ? Brand.Ink : Brand.Warning));
            v.AddChild(head);
            v.AddChild(Wrapped(Loc.T(def.Key + "_desc"), 12));
            if (open)
            {
                v.AddChild(Choice(PriceKeys, h.Level(p), (st, i) => st.SetHostPrice(p, i)));
                if (def.Dedicated && h.Count(h.Waiting, p) > 0) v.AddChild(Plain(Loc.T("host_waiting", h.Count(h.Waiting, p)), 12, Brand.Warning));
            }
            _content.AddChild(card);
        }

        _content.AddChild(new HSeparator());
        _content.AddChild(Ui.Heading("host_stock_title", 15, Brand.Ink));
        _content.AddChild(Wrapped(Loc.T("host_stock_help")));
        var sr = Row();
        sr.AddChild(Plain(Loc.T("host_min_stock", h.MinStock), 14));
        sr.AddChild(Act("−", st => { st.SetMinStock(st.Hosting.MinStock - 10); return null; }));
        sr.AddChild(Act("+", st => { st.SetMinStock(st.Hosting.MinStock + 10); return null; }));
        sr.AddChild(Act(Loc.T("host_order", 20, Ui.Money(20 * HostingCatalog.Def(HostProduct.Dedicated).ServerCost)), st => st.OrderServers(20)));
        _content.AddChild(sr);
        foreach (var o in h.Orders) _content.AddChild(Plain(Loc.T("host_order_line", o.Qty, o.Brand, N(Math.Max(0, o.ArriveDay - s.Day))), 13, Brand.InkDim));
        if (h.Unpacked > 0) _content.AddChild(Plain(Loc.T("host_unpacked", h.Unpacked), 13, Brand.Amber));

        _content.AddChild(new HSeparator());
        _content.AddChild(Ui.Heading("host_campaigns", 15, Brand.Ink));
        var cr = Row();
        for (var i = 0; i < HostingCatalog.Campaigns.Length; i++)
        {
            var idx = i; var c = HostingCatalog.Campaigns[i];
            cr.AddChild(Act(Loc.T(c.key, Ui.Money(c.cost), c.days), st => st.StartCampaign(idx), s.CanCampaign(i) == null));
        }
        _content.AddChild(cr);
        if (h.Campaign != null && s.Day < h.CampaignUntil) _content.AddChild(Plain(Loc.T("host_campaign_on", Loc.T(h.Campaign + "_name"), N(Math.Ceiling(h.CampaignUntil - s.Day))), 13, Brand.Ok));

        _content.AddChild(new HSeparator());
        _content.AddChild(Ui.Heading("host_big", 15, Brand.Ink));
        if (h.BigDeals.Count == 0) _content.AddChild(Plain(Loc.T("host_big_none"), 14, Brand.InkDim));
        foreach (var b in h.BigDeals)
        {
            var card = Card(b.Signed ? Brand.Ok : Brand.Amber, out var v);
            v.AddChild(Wrapped(Loc.T("host_big_quote", Main.ArgText(b.Name), N(b.Servers), Ui.Money(b.Monthly), b.Months), 15, Brand.Ink));
            var deal = b;
            if (!b.Signed)
            {
                var acts = Row();
                acts.AddChild(Act(Loc.T("host_big_sign"), st => st.SignHostDeal(deal), true, true));
                acts.AddChild(Act(Loc.T("host_big_decline"), st => { st.DeclineHostDeal(deal); return null; }));
                acts.AddChild(Plain(Loc.T("host_big_expires", N(Math.Max(0, b.ExpiresDay - s.Day))), 12, Brand.InkDim));
                v.AddChild(acts);
            }
            else
            {
                var r = Row(10);
                r.AddChild(Plain(Loc.T("host_big_delivered", b.Delivered, b.Servers), 13));
                r.AddChild(Bar(100.0 * b.Delivered / Math.Max(1, b.Servers), Brand.Ok));
                if (b.Delivered < b.Servers) r.AddChild(Plain(Loc.T("host_big_deadline", N(Math.Max(0, b.DeadlineDay - s.Day))), 12, Brand.Amber));
                v.AddChild(r);
            }
            _content.AddChild(card);
        }
    }

    // ---------------- market ----------------
    static Color NewsColor(int kind) => kind switch { 1 => Brand.Power, 2 => Brand.Critical, 3 => Brand.Ok, 4 => Brand.Cooling, 5 => Brand.Warning, _ => Brand.Amber };

    public static string NewsText(NewsItem n)
    {
        if (n.Arg == null) return Loc.T(n.Key);
        if (n.Arg.Contains('¦')) return Loc.T(n.Key, n.Arg.Split('¦').Select(Main.ArgText).ToArray<object>());
        return Loc.T(n.Key, Main.ArgText(n.Arg));
    }

    void MarketTab(GameState s)
    {
        _content.AddChild(Ui.Heading("gr_tab_market", 16, Brand.Amber));
        _content.AddChild(Wrapped(Loc.T("mk_help")));
        var flow = Stats();
        Stat(flow, "mk_share", Pct(s.MarketShare), Brand.Amber);
        Stat(flow, "mk_sold", N(s.MySoldMw, "0.0") + " MW", Brand.Power);
        Stat(flow, "mk_demand", N(s.DemandMw, "0") + " MW", Brand.Ink);
        Stat(flow, "mk_value", Ui.Money(s.MarketValue), Brand.Ok);
        _content.AddChild(flow);
        if (!s.Options.AiCompetitors) _content.AddChild(Plain(Loc.T("mk_no_rivals"), 14, Brand.InkDim));
        else
        {
            // market share as one bar: you and the rivals
            var total = s.MySoldMw + s.Rivals.Sum(c => c.SoldMw);
            var parts = new List<(string, double, Color)> { (s.Options.Company, s.MySoldMw, Brand.Amber) };
            Color[] cols = { Brand.Cooling, Brand.Fiber, Brand.Wall, Brand.Ok, Brand.Critical };
            foreach (var c in s.Rivals) parts.Add((c.Name, c.SoldMw, cols[c.Id % cols.Length]));
            _content.AddChild(new ShareBar(parts) { CustomMinimumSize = new Vector2(0, 26), SizeFlagsHorizontal = SizeFlags.ExpandFill });
            foreach (var c in s.Competitors)
            {
                var card = Card(c.Gone ? Brand.Line : cols[c.Id % cols.Length], out var v);
                var head = Row();
                head.AddChild(Title(c.Name, 15));
                head.AddChild(Plain(c.Gone ? Loc.T("mk_gone") : Loc.T("mk_rival_share", Pct(total > 0 ? c.SoldMw / total : 0)), 13, Brand.InkDim));
                v.AddChild(head);
                if (!c.Gone) v.AddChild(Plain(Loc.T("mk_rival_line", N(c.CapacityMw, "0"), Pct(c.Utilisation), Loc.T(c.Price < 0.95 ? "mk_price_low" : c.Price > 1.05 ? "mk_price_high" : "mk_price_mid"), N(c.Quality, "0"), c.Sites), 13));
                _content.AddChild(card);
            }
        }
        // stock exchange
        _content.AddChild(new HSeparator());
        _content.AddChild(Ui.Heading("mk_stock", 15, Brand.Ink));
        if (!s.Listed)
        {
            _content.AddChild(Wrapped(Loc.T("mk_ipo_help", Ui.Money(GameState.IpoMinRevenue))));
            _content.AddChild(Plain(Loc.T("mk_revenue_year", Ui.Money(s.AnnualRevenue)), 13, Brand.InkDim));
            var why = s.CanIpo();
            if (why != null) _content.AddChild(Plain(Loc.T(why), 13, Brand.Warning));
            var r = Row();
            foreach (var pct in new[] { 0.15, 0.25, 0.35 })
            {
                var p = pct;
                r.AddChild(Act(Loc.T("mk_ipo", N(p * 100), Ui.Money((long)(s.MarketValue * p * 0.95))), st => st.Ipo(p), why == null, p == 0.25));
            }
            _content.AddChild(r);
        }
        else
        {
            var f2 = Stats();
            Stat(f2, "mk_share_price", "€ " + N(s.SharePrice, "0.00"), Brand.Ok);
            Stat(f2, "mk_public", Pct(s.PublicShare), Brand.Ink);
            Stat(f2, "mk_value", Ui.Money((long)(s.SharePrice * Market.Shares)), Brand.Ok);
            _content.AddChild(f2);
            if (s.PriceHistory.Count > 1) _content.AddChild(new MiniChart(s.PriceHistory, Brand.Ok, true) { CustomMinimumSize = new Vector2(0, 90), SizeFlagsHorizontal = SizeFlags.ExpandFill });
            _content.AddChild(Wrapped(Loc.T("mk_listed_help")));
        }
        // news
        _content.AddChild(new HSeparator());
        _content.AddChild(Ui.Heading("mk_news", 15, Brand.Ink));
        if (!s.Options.News) _content.AddChild(Plain(Loc.T("mk_news_off"), 14, Brand.InkDim));
        else if (s.News.Count == 0) _content.AddChild(Plain(Loc.T("mk_news_none"), 14, Brand.InkDim));
        foreach (var n in s.News.Take(20))
        {
            var r = Row(10);
            var d = Plain(GameState.Epoch.AddDays(n.Day).ToString("d MMM", Culture), 12, Brand.InkDim); d.CustomMinimumSize = new Vector2(60, 0);
            r.AddChild(d);
            r.AddChild(Wrapped(NewsText(n), 13, NewsColor(n.Kind)));
            _content.AddChild(r);
        }
    }

    // ---------------- energy and ESG ----------------
    void EnergyTab(GameState s)
    {
        _content.AddChild(Ui.Heading("gr_tab_energy", 16, Brand.Power));
        _content.AddChild(Wrapped(Loc.T(s.Options.DynamicPrices ? "en_help" : "en_help_static")));
        var flow = Stats();
        Stat(flow, "en_now", "€ " + N(s.SpotNow, "0.000"), Brand.Power);
        Stat(flow, "en_index", Pct(s.EnergyIndex), s.EnergyIndex > 1.2 ? Brand.Critical : Brand.Ink);
        Stat(flow, "en_solar", N(s.SolarKwhDay, "0") + " kWh", Brand.Amber);
        Stat(flow, "en_wind", N(s.WindKwhDay, "0") + " kWh", Brand.Cooling);
        Stat(flow, "en_battery", Ui.Money((long)Math.Round(s.BatteryProfit() * 30)), Brand.Ok);
        Stat(flow, "en_renewable", Pct(s.RenewableShare), s.GreenPower ? Brand.Ok : Brand.Ink);
        _content.AddChild(flow);
        _content.AddChild(Ui.Heading("en_today", 12));
        _content.AddChild(new MiniChart(Enumerable.Range(0, 24).Select(s.SpotPrice).ToList(), Brand.Power, false, (int)(s.Hours % 24)) { CustomMinimumSize = new Vector2(0, 90), SizeFlagsHorizontal = SizeFlags.ExpandFill });
        if (s.EnergyHistory.Count > 2)
        {
            _content.AddChild(Ui.Heading("en_history", 12));
            _content.AddChild(new MiniChart(s.EnergyHistory, Brand.Amber, true) { CustomMinimumSize = new Vector2(0, 70), SizeFlagsHorizontal = SizeFlags.ExpandFill });
        }
        _content.AddChild(Wrapped(Loc.T("en_battery_mode", Loc.T("own_mode_" + s.BatteryUse.ToString().ToLowerInvariant()))));
        _content.AddChild(Wrapped(Loc.T("en_trading_help", N(s.TradeKwh, "0"))));
        _content.AddChild(Plain(Loc.T("en_objects", s.SolarPanels, s.WindTurbines, s.Objects.Count(o => o.Kind == ObjKind.BatteryContainer)), 13, Brand.InkDim));

        _content.AddChild(new HSeparator());
        _content.AddChild(Ui.Heading("esg_title", 15, Brand.Ok));
        _content.AddChild(Wrapped(Loc.T("esg_help")));
        var top = Row(18);
        var big = Ui.Number(N(s.Esg, "0"), 40); big.AddThemeColorOverride("font_color", Score(s.Esg)); big.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        top.AddChild(big);
        var tv = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        tv.AddChild(Plain(Loc.T("rp_out_of"), 13, Brand.InkDim));
        tv.AddChild(Bar(s.Esg, Score(s.Esg)));
        tv.AddChild(Plain(Loc.T("esg_water_line", N(s.WaterM3PerDay, "0.0"), N(s.Wue, "0.00"), Ui.Money((long)Math.Round(s.WaterM3PerDay * GameState.WaterPrice * 30))), 13, Brand.Cooling));
        top.AddChild(tv);
        _content.AddChild(top);
        foreach (var l in s.EsgLines())
        {
            var r = Row(12);
            var n = Plain(Loc.T(l.Key), 14); n.CustomMinimumSize = new Vector2(220, 0);
            r.AddChild(n);
            r.AddChild(Bar(100.0 * l.Points / l.Max, l.Points >= l.Max ? Brand.Ok : Brand.Amber));
            var v = Plain($"{l.Points:0} / {l.Max:0}", 14, Brand.InkDim); v.CustomMinimumSize = new Vector2(64, 0); v.HorizontalAlignment = HorizontalAlignment.Right;
            r.AddChild(v);
            _content.AddChild(r);
            _content.AddChild(Wrapped(Loc.T(l.Key + "_tip"), 12));
        }
    }

    // ---------------- risks ----------------
    void RisksTab(GameState s)
    {
        _content.AddChild(Ui.Heading("gr_tab_risks", 16, Brand.Critical));
        _content.AddChild(Wrapped(Loc.T("risk_help")));
        // SOC
        var soc = Card(Brand.Critical, out var sv);
        var sh = Row();
        sh.AddChild(Title(Loc.T("risk_soc")));
        sh.AddChild(Plain(Loc.T("risk_soc_level", s.SocLevel), 15, Score(s.SocLevel)));
        sv.AddChild(sh);
        sv.AddChild(Bar(s.SocLevel, Score(s.SocLevel)));
        sv.AddChild(Wrapped(Loc.T("risk_soc_help")));
        sv.AddChild(Plain(Loc.T("risk_soc_line", s.SocDesks, s.CountOf(StaffRole.SocAnalyst), s.AttacksBlocked, s.AttacksHit), 13));
        var cb = new CheckButton { Text = Loc.T("risk_soc247", Ui.Money(GameState.Soc247Fee)), ButtonPressed = s.Soc247, AutoTranslateMode = AutoTranslateModeEnum.Disabled };
        cb.Toggled += b => { _state().Soc247 = b; _sig = null; };
        sv.AddChild(cb);
        if (!s.Options.Events) sv.AddChild(Plain(Loc.T("risk_events_off"), 12, Brand.InkDim));
        _content.AddChild(soc);
        // insurance
        var ins = Card(Brand.Ok, out var iv);
        iv.AddChild(Title(Loc.T("risk_insurance")));
        iv.AddChild(Wrapped(Loc.T("risk_insurance_help")));
        iv.AddChild(Choice(new[] { "risk_ins_0", "risk_ins_1", "risk_ins_2" }, s.Insurance, (st, i) => st.SetInsurance(i)));
        iv.AddChild(Plain(Loc.T("risk_ins_line", Ui.Money(s.InsurancePerMonth), Ui.Money(s.AssetValue), Ui.Money(GameState.Deductible[Math.Clamp(s.Insurance, 0, 2)])), 13));
        iv.AddChild(Plain(Loc.T("risk_ins_year", Ui.Money(s.DamageThisYear), Ui.Money(s.PaidOutThisYear)), 13, Brand.InkDim));
        if (!s.Options.Disasters) iv.AddChild(Plain(Loc.T("risk_disasters_off"), 12, Brand.InkDim));
        _content.AddChild(ins);
        // supply chain
        var sup = Card(s.SupplyCrunch ? Brand.Warning : Brand.Line, out var pv);
        pv.AddChild(Title(Loc.T("risk_supply")));
        if (!s.Options.SupplyDelays) pv.AddChild(Plain(Loc.T("risk_supply_off"), 13, Brand.InkDim));
        else if (s.SupplyCrunch) pv.AddChild(Wrapped(Loc.T("risk_supply_on", Loc.T("supply_" + s.SupplyKind), N(s.SupplyDays), N(Math.Ceiling(s.SupplyUntil - s.Day))), 14, Brand.Warning));
        else pv.AddChild(Plain(Loc.T("risk_supply_ok"), 13, Brand.Ok));
        pv.AddChild(Wrapped(Loc.T("risk_supply_help")));
        _content.AddChild(sup);
        // recent incidents of this kind
        var log = s.Incidents.Where(l => l.Key.StartsWith("ev_cyber") || l.Key.StartsWith("ev_disaster") || l.Key.StartsWith("ev_supply")).Reverse().Take(10).ToList();
        if (log.Count > 0)
        {
            _content.AddChild(Ui.Heading("risk_log", 13));
            foreach (var l in log)
                _content.AddChild(Plain($"{GameState.Epoch.AddHours(l.Hour).ToString("d MMM", Culture)}  ·  {Loc.T(l.Key, Main.ArgText(l.Arg))}", 13, Brand.InkDim));
        }
    }

    // ---------------- key people ----------------
    void PeopleTab(GameState s)
    {
        _content.AddChild(Ui.Heading("gr_tab_people", 16, Brand.Amber));
        _content.AddChild(Wrapped(Loc.T("key_help")));
        if (!s.Has(Exec.Hr)) _content.AddChild(Plain(Loc.T("msg_needs_hr"), 14, Brand.Warning));
        var people = s.KeyPeople.OrderBy(k => k.Candidate).ThenBy(k => k.Role).ToList();
        if (people.Count == 0) _content.AddChild(Plain(Loc.T("key_none"), 14, Brand.InkDim));
        foreach (var k in people)
        {
            var card = Card(k.Candidate ? Brand.Amber : k.PoachedBy != null ? Brand.Critical : Brand.Ok, out var v);
            var head = Row();
            head.AddChild(Title(k.Name, 16));
            head.AddChild(Plain(Loc.T(KeyStaff.Key(k.Role)), 14, Brand.Amber));
            v.AddChild(head);
            v.AddChild(Wrapped(Loc.T(KeyStaff.Key(k.Role) + "_desc"), 13, Brand.Ink));
            var person = k;
            if (k.Candidate)
            {
                v.AddChild(Plain(Loc.T("key_candidate_line", Ui.Money(k.Salary), Ui.Money(k.Salary * KeyStaff.FeeMonths), N(Math.Max(0, k.ExpiresDay - s.Day))), 13, Brand.InkDim));
                v.AddChild(Act(Loc.T("key_hire"), st => st.HireKey(person), s.CanHireKey(k) == null, true));
            }
            else
            {
                var r = Row(10);
                r.AddChild(Plain(Loc.T("key_line", Ui.Money(k.Salary)), 13));
                r.AddChild(Plain(Loc.T("key_loyalty"), 13, Brand.InkDim));
                r.AddChild(Bar(k.Loyalty, Score(k.Loyalty), 160));
                v.AddChild(r);
                if (k.PoachedBy != null)
                {
                    v.AddChild(Wrapped(Loc.T("key_poach", k.PoachedBy, N(Math.Max(0, k.PoachDay - s.Day))), 14, Brand.Critical));
                    var acts = Row();
                    acts.AddChild(Act(Loc.T("key_match", Ui.Money((long)(k.Salary * 1.2))), st => st.AnswerPoach(person, true), true, true));
                    acts.AddChild(Act(Loc.T("key_let_go"), st => st.AnswerPoach(person, false)));
                    v.AddChild(acts);
                }
                else v.AddChild(Act(Loc.T("key_dismiss"), st => { st.DismissKey(person); return null; }));
            }
            _content.AddChild(card);
        }
    }
}

/// <summary>A schematic map of north-west Europe with the countries you can build in, your campus and your locations.</summary>
public partial class SiteMap : Control
{
    readonly GameState _s;
    readonly Action<string> _pick;
    public string Selected;

    public SiteMap(GameState s, Action<string> pick) { _s = s; _pick = pick; MouseFilter = MouseFilterEnum.Stop; }

    Vector2 P(CountryDef c) => new(c.MapX * Size.X, c.MapY * Size.Y);

    public override void _Draw()
    {
        DrawStyleBox(Ui.Box(new Color("#0f2233"), 10, Brand.Line, 1, 0), new Rect2(Vector2.Zero, Size));
        // a hint of coastline: soft blobs per country
        foreach (var c in Countries.All) DrawCircle(P(c), Size.Y * 0.09f, new Color(1, 1, 1, 0.045f));
        var home = P(Countries.Get("nl"));
        foreach (var site in _s.Sites.Where(x => x.Linked)) DrawLine(home, P(Countries.Get(site.Country)), Brand.Fiber, 2, true);
        var font = Ui.BodySemi;
        foreach (var c in Countries.All)
        {
            var p = P(c);
            var open = _s.CountryOpen(c.Code);
            var n = _s.Sites.Count(x => x.Country == c.Code);
            var col = c.Code == Selected ? Brand.Amber : open ? Brand.Ok : Brand.InkDim;
            DrawCircle(p, c.Code == Selected ? 9 : 7, col);
            if (c.Code == "nl") DrawArc(p, 13, 0, Mathf.Tau, 24, Brand.Power, 2);
            var label = Loc.T(c.Key) + (n > 0 ? $" · {n}" : "");
            DrawString(font, p + new Vector2(12, 5), label, HorizontalAlignment.Left, -1, 14, col);
        }
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb) return;
        var best = Countries.All.OrderBy(c => P(c).DistanceTo(mb.Position)).First();
        if (P(best).DistanceTo(mb.Position) < 40) { Selected = best.Code; _pick(best.Code); QueueRedraw(); }
    }
}

/// <summary>A small bar or line chart.</summary>
public partial class MiniChart : Control
{
    readonly List<double> _v;
    readonly Color _c;
    readonly bool _line;
    readonly int _mark;

    public MiniChart(List<double> values, Color c, bool line = false, int mark = -1) { _v = values.ToList(); _c = c; _line = line; _mark = mark; }

    public override void _Draw()
    {
        DrawStyleBox(Ui.Box(new Color(1, 1, 1, 0.03f), 8, Brand.Line, 1, 0), new Rect2(Vector2.Zero, Size));
        if (_v.Count == 0) return;
        var max = Math.Max(1e-9, _v.Max());
        var min = _line ? _v.Min() * 0.95 : 0;
        float pad = 8, w = Size.X - 2 * pad, h = Size.Y - 2 * pad;
        float Y(double v) => (float)(pad + h - (v - min) / Math.Max(1e-9, max - min) * h);
        if (_line)
        {
            var pts = _v.Select((v, i) => new Vector2(pad + w * i / Math.Max(1, _v.Count - 1), Y(v))).ToArray();
            DrawPolyline(pts, _c, 2, true);
            return;
        }
        var bw = w / _v.Count;
        for (var i = 0; i < _v.Count; i++)
        {
            var y = Y(_v[i]);
            DrawRect(new Rect2(pad + i * bw + 1, y, Math.Max(1, bw - 2), pad + h - y), i == _mark ? Brand.Ink : _c);
        }
    }
}

/// <summary>Market share as one stacked bar with names.</summary>
public partial class ShareBar : Control
{
    readonly List<(string name, double v, Color c)> _parts;
    public ShareBar(List<(string, double, Color)> parts) { _parts = parts; TooltipText = string.Join("\n", parts.Select(p => p.Item1)); }

    public override void _Draw()
    {
        var total = _parts.Sum(p => p.v);
        if (total <= 0) return;
        float x = 0;
        foreach (var (name, v, c) in _parts)
        {
            var w = (float)(v / total * Size.X);
            DrawRect(new Rect2(x, 0, w, Size.Y), c);
            if (w > 70) DrawString(Ui.BodySemi, new Vector2(x + 6, Size.Y - 8), name, HorizontalAlignment.Left, w - 10, 12, Brand.PanelSolid);
            x += w;
        }
    }
}
