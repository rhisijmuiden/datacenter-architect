using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DatacenterArchitect.Core;
using DatacenterArchitect.Sim;
using Godot;

namespace DatacenterArchitect.UI;

/// <summary>One message as it appeared on screen, kept for the message history.</summary>
public record HudMessage(string When, string Text, Color Accent, System.Action Go = null);

/// <summary>
/// Overview (F1): key figures with advice, the site checklist and the message history.
/// Advice buttons jump to the screen that solves it.
/// </summary>
public partial class OverviewPanel : Modal
{
    public const int TabDashboard = 0, TabGoals = 1, TabMessages = 2;
    static readonly string[] Tabs = { "ov_tab_dashboard", "ov_tab_goals", "ov_tab_messages" };

    readonly Func<GameState> _state;
    readonly Func<IReadOnlyList<(string key, bool done)>> _goals;
    readonly Func<IReadOnlyList<HudMessage>> _messages;
    readonly Action<PanelKind, int> _open;
    /// <summary>Jump to a hall and start the fix of an outage (racks off or offline).</summary>
    public Action<Outage> Fix;
    readonly VBoxContainer _content;
    readonly StableScroll _scroll;
    int _tab;
    string _sig;
    double _timer;

    static CultureInfo Culture => Loc.Language == "nl" ? new CultureInfo("nl-NL") : new CultureInfo("en-GB");
    static string N(double v, string f = "N0") => v.ToString(f, Culture);

    public OverviewPanel(Func<GameState> state, Func<IReadOnlyList<(string key, bool done)>> goals, Func<IReadOnlyList<HudMessage>> messages,
        Action<PanelKind, int> open, int tab = 0) : base("nav_overview", 980)
    {
        _state = state; _goals = goals; _messages = messages; _open = open;
        _tab = Math.Clamp(tab, 0, Tabs.Length - 1);
        _scroll = new StableScroll { CustomMinimumSize = new Vector2(0, 600), SizeFlagsHorizontal = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _content.AddThemeConstantOverride("separation", 10);
        var pad = Ui.Pad(_content, 0, 0, 14, 0); pad.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _scroll.AddChild(pad);
        Body.AddChild(_scroll);
        FillHeight(_scroll);
        Refresh(true);
    }

    public override void SelectTab(int tab)
    {
        _tab = Math.Clamp(tab, 0, Tabs.Length - 1); _sig = null;
        _scroll.ScrollVertical = 0;
        Refresh(true);
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
        var goals = _goals?.Invoke() ?? Array.Empty<(string, bool)>();
        var msgs = _messages?.Invoke() ?? Array.Empty<HudMessage>();
        var sig = $"{_tab}|{Loc.Language}|{(long)(s.Hours * 2)}|{goals.Count(g => g.done)}|{msgs.Count}|{(msgs.Count > 0 ? msgs[0].Text : "")}";
        if (!force && sig == _sig) return;
        _sig = sig;
        foreach (var c in _content.GetChildren()) c.QueueFree();
        switch (_tab)
        {
            case TabDashboard: Dashboard(s); break;
            case TabGoals: Goals(goals); break;
            default: Messages(msgs); break;
        }
    }

    static Label Plain(string text, int size = 15, Color? color = null)
    {
        var l = Ui.Text(text, size, color ?? Brand.Ink);
        l.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        return l;
    }

    static PanelContainer Card(Color accent, out VBoxContainer v)
    {
        var p = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var st = Ui.Box(new Color(1, 1, 1, 0.04f), 8, Brand.Line, 1, 12);
        st.BorderColor = new Color(accent, 0.55f);
        p.AddThemeStyleboxOverride("panel", st);
        v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 2);
        p.AddChild(v);
        return p;
    }

    // ---------------- dashboard ----------------
    /// <summary>The figures on the dashboard, also used by tests.</summary>
    public static List<(string label, string value, string sub, Color color)> Figures(GameState s)
    {
        var inf = s.Infra; var net = s.Net; var c = s.Creative;
        var faults = s.Objects.Count(o => o.Broken && o.Noticed);
        var leads = s.Deals.Count(d => d.State == DealState.Lead);
        var active = s.Deals.Count(d => d.State == DealState.Active);
        string Pct(double r) => N(Math.Round(r * 100)) + "%";
        var powerRatio = inf.UsableItKw > 0 ? inf.ItDemandKw / inf.UsableItKw : 0;
        return new()
        {
            (Loc.T("res_money"), c.UnlimitedMoney ? "∞" : Ui.Money(s.Money), Loc.T("ov_revenue", Ui.Money(s.MonthlyRevenue)), s.Money < 0 && !c.UnlimitedMoney ? Brand.Critical : Brand.Ok),
            (Loc.T("res_racks"), $"{s.LiveRacks} / {s.Racks}", Loc.T("ov_contracts", active, leads), Brand.Ok),
            (Loc.T("res_reputation"), "★ " + N(Math.Round(s.Reputation)), Loc.T("ov_proposals", s.OpenProposals.Count), s.Reputation >= 65 ? Brand.Ok : s.Reputation >= 40 ? Brand.Amber : Brand.Critical),
            (Loc.T("res_staff"), N(s.Staff.Count), Loc.T("ov_faults", faults), faults > 0 ? Brand.Critical : Brand.Cooling),
            (Loc.T("res_power"), N(inf.ItDemandKw) + " kW", c.NoPowerLimit ? Loc.T("hud_unlimited") : Loc.T("ov_of", N(Math.Max(0, inf.UsableItKw)) + " kW", Pct(powerRatio)), Brand.Power),
            (Loc.T("res_cooling"), N(inf.HeatKw) + " kW", c.NoCoolingLimit ? Loc.T("hud_unlimited") : Loc.T("ov_of", N(inf.CoolingKw) + " kW", Pct(inf.CoolRatio)), Brand.Cooling),
            (Loc.T("res_network"), net.DemandGbps.ToString("0.0", Culture) + " Gbit/s", Loc.T("ov_online", net.OnlineRacks), Brand.Fiber),
            (Loc.T("res_pue"), inf.Pue > 0 ? inf.Pue.ToString("0.00", Culture) : "-", Loc.T("ov_pue_hint"), Brand.Ink),
        };
    }

    /// <summary>What needs attention now, most urgent first, with the screen that solves it.</summary>
    public static List<(string text, Color color, PanelKind kind, int tab)> Advice(GameState s, IReadOnlyList<(string key, bool done)> goals)
    {
        var list = new List<(string, Color, PanelKind, int)>();
        var inf = s.Infra; var c = s.Creative;
        var faults = s.Objects.Count(o => o.Broken && o.Noticed);
        if (s.GridDown) list.Add((Loc.T("ov_adv_grid"), Brand.Critical, PanelKind.Power, 0));
        if (faults > 0) list.Add((Loc.T("ov_adv_faults", faults), Brand.Critical, PanelKind.Staff, StaffPanel.TabIncidents));
        if (!c.NoPowerLimit && inf.UsableItKw > 0 && inf.ItDemandKw / inf.UsableItKw > 0.85) list.Add((Loc.T("ov_adv_power"), Brand.Warning, PanelKind.Power, 0));
        if (!c.NoCoolingLimit && inf.CoolRatio > 0.85) list.Add((Loc.T("ov_adv_cooling"), Brand.Warning, PanelKind.Power, 0));
        var leads = s.Deals.Count(d => d.State == DealState.Lead);
        if (leads > 0) list.Add((Loc.T("ov_adv_leads", leads), Brand.Ok, PanelKind.Company, CompanyPanel.TabSales));
        if (s.OpenProposals.Count > 0) list.Add((Loc.T("ov_adv_proposals", s.OpenProposals.Count), Brand.Amber, PanelKind.Reputation, ReputationPanel.TabProposals));
        var next = goals?.FirstOrDefault(g => !g.done);
        if (next is { key: not null } g) list.Add((Loc.T("ov_adv_goal", Loc.T(g.key)), Brand.InkDim, PanelKind.Overview, TabGoals));
        return list;
    }

    void Dashboard(GameState s)
    {
        _content.AddChild(Ui.Heading("ov_tab_dashboard", 16, Brand.Amber));
        var grid = new GridContainer { Columns = 4, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 10); grid.AddThemeConstantOverride("v_separation", 10);
        foreach (var (label, value, sub, color) in Figures(s))
        {
            var card = Card(color, out var v);
            v.AddChild(Plain(label, 13, Brand.InkDim));
            var val = Ui.Number(value, 24); val.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
            val.AddThemeColorOverride("font_color", color);
            v.AddChild(val);
            v.AddChild(Plain(sub, 12, Brand.InkDim));
            grid.AddChild(card);
        }
        _content.AddChild(grid);

        Outages(s);
        _content.AddChild(Ui.Heading("ov_advice", 16, Brand.Amber));
        var advice = Advice(s, _goals?.Invoke());
        if (advice.Count == 0) _content.AddChild(Plain(Loc.T("ov_advice_none"), 14, Brand.Ok));
        foreach (var (text, color, kind, tab) in advice.Take(8))
        {
            var card = Card(color, out var v);
            var h = new HBoxContainer(); h.AddThemeConstantOverride("separation", 10);
            var dot = new ColorRect { Color = color, CustomMinimumSize = new Vector2(8, 8), SizeFlagsVertical = SizeFlags.ShrinkCenter };
            h.AddChild(dot);
            var l = Plain(text, 14); l.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            h.AddChild(l);
            if (_open != null)
            {
                var k = kind; var t = tab;
                var go = Ui.Secondary("ov_go", () => _open(k, t));
                go.CustomMinimumSize = new Vector2(96, 32);
                h.AddChild(go);
            }
            v.AddChild(h);
            _content.AddChild(card);
        }
    }

    /// <summary>Racks that are off or offline, per hall and cause, each with its fix.</summary>
    void Outages(GameState s)
    {
        var list = Sim.Outages.For(s);
        if (list.Count == 0) return;
        _content.AddChild(Ui.Heading("outage_title", 16, Brand.Critical));
        foreach (var o in list.Take(8))
        {
            var color = o.Offline ? Brand.Warning : Brand.Critical;
            var card = Card(color, out var v);
            var h = new HBoxContainer(); h.AddThemeConstantOverride("separation", 10);
            h.AddChild(new ColorRect { Color = color, CustomMinimumSize = new Vector2(8, 8), SizeFlagsVertical = SizeFlags.ShrinkCenter });
            var l = Plain(OutageUi.Text(o), 14); l.SizeFlagsHorizontal = SizeFlags.ExpandFill; l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            h.AddChild(l);
            if (Fix != null)
            {
                var target = o;
                var go = Ui.Secondary(OutageUi.FixText(o), () => Fix(target));
                go.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
                go.CustomMinimumSize = new Vector2(150, 32);
                h.AddChild(go);
            }
            v.AddChild(h);
            _content.AddChild(card);
        }
    }

    // ---------------- goals ----------------
    void Goals(IReadOnlyList<(string key, bool done)> goals)
    {
        _content.AddChild(Ui.Heading("goals_title", 16, Brand.Amber));
        _content.AddChild(Plain(Loc.T("ov_goals_count", goals.Count(g => g.done), goals.Count), 14, Brand.InkDim));
        foreach (var (key, done) in goals)
            _content.AddChild(Plain((done ? "✓  " : "○  ") + Loc.T(key), 15, done ? Brand.Ok : Brand.Ink));
    }

    // ---------------- messages ----------------
    void Messages(IReadOnlyList<HudMessage> msgs)
    {
        _content.AddChild(Ui.Heading("ov_tab_messages", 16, Brand.Amber));
        if (msgs.Count == 0) { _content.AddChild(Plain(Loc.T("ov_messages_none"), 14, Brand.InkDim)); return; }
        foreach (var m in msgs.Take(100))
        {
            var card = Card(m.Accent, out var v);
            var h = new HBoxContainer(); h.AddThemeConstantOverride("separation", 12);
            var when = Plain(m.When, 13, Brand.InkDim); when.CustomMinimumSize = new Vector2(150, 0);
            h.AddChild(when);
            var t = Plain(m.Text, 14); t.SizeFlagsHorizontal = SizeFlags.ExpandFill; t.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            h.AddChild(t);
            if (m.Go != null)
            {
                var go = Ui.Secondary("outage_show", m.Go);
                go.CustomMinimumSize = new Vector2(90, 32);
                h.AddChild(go);
            }
            v.AddChild(h);
            _content.AddChild(card);
        }
    }
}
