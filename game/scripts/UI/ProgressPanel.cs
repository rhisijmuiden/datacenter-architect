using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DatacenterArchitect.Core;
using DatacenterArchitect.Sim;
using DatacenterArchitect.World;
using Godot;

namespace DatacenterArchitect.UI;

/// <summary>
/// Progress (F8, step 11): statistics and charts through time, the scenario with its medals, achievements and the timelapse of the campus.
/// Everything here is recorded by itself; the player only looks.
/// </summary>
public partial class ProgressPanel : Modal
{
    public const int TabStats = 0, TabScenario = 1, TabAchievements = 2, TabTimelapse = 3, TabAwards = 4;
    static readonly string[] Tabs = { "pg_tab_stats", "pg_tab_scenario", "pg_tab_achievements", "pg_tab_timelapse", "pg_tab_awards" };
    static readonly string[] TabIcons = { "stats", "medal", "trophy", "film", "trophy" };
    static readonly int[] Ranges = { 30, 365, 0 };
    static readonly string[] RangeKeys = { "pg_range_30", "pg_range_365", "pg_range_all" };

    readonly Func<GameState> _state;
    readonly Action _photo;
    readonly List<Button> _tabBtns = new();
    readonly VBoxContainer _content;
    readonly ScrollContainer _scroll;
    int _tab;
    static int _range = 1;
    string _sig;
    double _timer;

    // timelapse player
    List<(int day, string path)> _frames = new();
    int _frame;
    bool _playingLapse;
    double _lapseT;
    static readonly float[] LapseFps = { 4, 8, 16, 30 };
    static int _lapseSpeed = 1;
    TextureRect _lapseView;
    Label _lapseDate;
    HSlider _lapseSlider;
    Button _lapsePlay;

    static CultureInfo Culture => Loc.Language == "nl" ? new CultureInfo("nl-NL") : new CultureInfo("en-GB");
    static string N(double v, string f = "N0") => v.ToString(f, Culture);

    public ProgressPanel(Func<GameState> state, Action photo, int tab = 0) : base("nav_progress", 1120)
    {
        _state = state; _photo = photo; _tab = Math.Clamp(tab, 0, Tabs.Length - 1);
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
            b.Pressed += () => SelectTab(idx);
            _tabBtns.Add(b);
            tabs.AddChild(b);
        }
        split.AddChild(tabs);
        var tabLine = new VSeparator();
        split.AddChild(tabLine);
        OwnTabs(tabs, tabLine);
        _scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 620), SizeFlagsHorizontal = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _content.AddThemeConstantOverride("separation", 10);
        var pad = Ui.Pad(_content, 0, 0, 14, 0); pad.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _scroll.AddChild(pad);
        split.AddChild(_scroll);
        FillHeight(split, _scroll);
        Refresh(true);
    }

    public override void SelectTab(int tab)
    {
        _tab = Math.Clamp(tab, 0, Tabs.Length - 1); _sig = null; _playingLapse = false;
        _scroll.ScrollVertical = 0;
        Refresh(true);
    }

    public override void _Process(double delta)
    {
        if (_tab == TabTimelapse && _playingLapse && _frames.Count > 0)
        {
            _lapseT += delta * LapseFps[_lapseSpeed];
            while (_lapseT >= 1)
            {
                _lapseT -= 1;
                if (_frame >= _frames.Count - 1)
                {
                    _playingLapse = false;
                    if (_frames.Count >= 4) _state()?.Unlock(Achievements.Timelapse);
                    break;
                }
                ShowFrame(_frame + 1);
            }
            if (!_playingLapse && _lapsePlay != null) _lapsePlay.Text = "tl_play";
        }
        _timer -= delta;
        if (_timer > 0) return;
        _timer = 0.5;
        Refresh(false);
    }

    void Refresh(bool force)
    {
        var s = _state();
        if (s == null) return;
        var sig = $"{_tab}|{_range}|{Loc.Language}|{s.Stats.Count}:{(s.Stats.Count > 0 ? s.Stats[^1].Day : 0)}|{s.Achieved.Count}|{s.ScenarioMedal}{s.ScenarioEnded}|{(int)s.ScenarioDaysLeft}|{s.Awards.Count}";
        if (_tab == TabTimelapse) sig = $"{_tab}|{Loc.Language}|{Timelapse.Frames(s.GameId).Count}";
        if (!force && sig == _sig) return;
        _sig = sig;
        var keep = _scroll.ScrollVertical;
        for (var i = 0; i < _tabBtns.Count; i++) Mark(_tabBtns[i], i == _tab);
        foreach (var c in _content.GetChildren()) c.QueueFree();
        _lapseView = null; _lapsePlay = null;
        switch (_tab)
        {
            case TabStats: StatsTab(s); break;
            case TabScenario: ScenarioTab(s); break;
            case TabAchievements: AchievementsTab(s); break;
            case TabAwards: AwardsTab(s); break;
            default: TimelapseTab(s); break;
        }
        _scroll.SetDeferred("scroll_vertical", keep);
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

    static Label Plain(string text, int size = 15, Color? color = null, FontFile font = null)
    {
        var l = Ui.Text(text, size, color ?? Brand.Ink, font);
        l.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        l.AutowrapMode = TextServer.AutowrapMode.Off;
        return l;
    }

    static Label Wrapped(string text, int size = 13, Color? color = null)
    {
        var l = Plain(text, size, color ?? Brand.InkDim);
        l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        l.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        return l;
    }

    static PanelContainer Card(Color accent, out VBoxContainer v)
    {
        var p = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var st = Ui.Box(new Color(1, 1, 1, 0.04f), 8, Brand.Line, 1, 12);
        st.BorderColor = new Color(accent, 0.45f);
        p.AddThemeStyleboxOverride("panel", st);
        v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 4);
        p.AddChild(v);
        return p;
    }

    static string DayText(double day) => GameState.Epoch.AddDays(day).ToString("d MMM yyyy", Culture);
    static string Kw(double v) => v >= 1000 ? N(v / 1000, "0.0") + " MW" : N(v) + " kW";
    static string Money(double v) => Ui.Money((long)Math.Round(v));
    static string Short(double v) => Math.Abs(v) >= 1_000_000 ? "€ " + N(v / 1_000_000, "0.0") + " M" : Math.Abs(v) >= 10_000 ? "€ " + N(v / 1000, "0") + "k" : Money(v);

    // ---------------- statistics ----------------
    /// <summary>The charts of the statistics tab: title, accent and series. Also used by tests.</summary>
    public static List<(string title, Color accent, string now, ChartSeries[] series, double? minY, double? maxY)> Charts(GameState s, int days)
    {
        var st = s.StatsSince(days).ToList();
        List<(double, double)> P(Func<StatSample, double> f) => st.Select(x => ((double)x.Day, f(x))).ToList();
        // income and costs per month: the average day over the last 30 days, times 30 (works with weekly samples too)
        List<(double, double)> Monthly(Func<StatSample, double> f)
        {
            var all = s.Stats;
            return st.Select(x =>
            {
                var w = all.Where(y => y.Day > x.Day - 30 && y.Day <= x.Day).ToList();
                return ((double)x.Day, w.Count == 0 ? 0 : w.Average(f) * 30);
            }).ToList();
        }
        var last = st.LastOrDefault();
        var list = new List<(string, Color, string, ChartSeries[], double?, double?)>();
        var unlimited = s.Creative.UnlimitedMoney;
        if (!unlimited)
        {
            list.Add((Loc.T("pg_money"), Brand.Ok, last == null ? "-" : Money(last.Money),
                new[] { new ChartSeries(Loc.T("pg_money"), Brand.Ok, P(x => x.Money), Short, true) }, null, null));
            list.Add((Loc.T("pg_income_costs"), Brand.Ok, last == null ? "-" : Loc.T("pg_profit_month", Short(Monthly(x => x.Profit).LastOrDefault().Item2)),
                new[]
                {
                    new ChartSeries(Loc.T("pg_income"), Brand.Ok, Monthly(x => x.Income), Short),
                    new ChartSeries(Loc.T("pg_costs"), Brand.Critical, Monthly(x => x.Costs), Short),
                }, 0, null));
        }
        list.Add((Loc.T("pg_revenue"), Brand.Ok, last == null ? "-" : Money(last.Revenue) + Loc.T("pg_per_month"),
            new[] { new ChartSeries(Loc.T("pg_revenue"), Brand.Ok, P(x => x.Revenue), Short, true) }, 0, null));
        list.Add((Loc.T("pg_power"), Brand.Power, last == null ? "-" : Kw(last.ItKw),
            new[]
            {
                new ChartSeries(Loc.T("pg_it_load"), Brand.Power, P(x => x.ItKw), Kw, true),
                new ChartSeries(Loc.T("pg_facility_load"), new Color(Brand.Power, 0.5f), P(x => x.FacilityKw), Kw),
            }, 0, null));
        list.Add((Loc.T("res_pue"), Brand.Cooling, last == null || last.Pue <= 0 ? "-" : N(last.Pue, "0.00"),
            new[] { new ChartSeries(Loc.T("res_pue"), Brand.Cooling, st.Where(x => x.Pue > 0).Select(x => ((double)x.Day, x.Pue)).ToList(), v => N(v, "0.00")) }, 1.0, null));
        list.Add((Loc.T("pg_uptime"), Brand.Ok, last == null ? "-" : N(last.Uptime * 100, "0.00") + "%",
            new[] { new ChartSeries(Loc.T("pg_uptime"), Brand.Ok, P(x => x.Uptime * 100), v => N(v, "0.0") + "%", true) }, null, 100));
        list.Add((Loc.T("pg_racks"), Brand.Amber, last == null ? "-" : N(last.LiveRacks),
            new[] { new ChartSeries(Loc.T("pg_racks"), Brand.Amber, P(x => x.LiveRacks), v => N(v), true) }, 0, null));
        list.Add((Loc.T("pg_customers"), Brand.Ok, last == null ? "-" : N(last.Customers) + (last.HostingCustomers > 0 ? " + " + N(last.HostingCustomers) : ""),
            new[] { new ChartSeries(Loc.T("pg_customers"), Brand.Ok, P(x => x.Customers), v => N(v), true) }, 0, null));
        list.Add((Loc.T("pg_network"), Brand.Fiber, last == null ? "-" : N(last.Gbps, "0.0") + " Gbit/s",
            new[] { new ChartSeries(Loc.T("pg_network"), Brand.Fiber, P(x => x.Gbps), v => N(v, "0.0") + " Gbit/s", true) }, 0, null));
        list.Add((Loc.T("pg_rep_esg"), new Color("b79cff"), last == null ? "-" : $"★ {N(last.Reputation)} · ESG {N(last.Esg)}",
            new[]
            {
                new ChartSeries(Loc.T("res_reputation"), new Color("b79cff"), P(x => x.Reputation), v => N(v)),
                new ChartSeries("ESG", Brand.Ok, P(x => x.Esg), v => N(v)),
            }, 0, 100));
        if (st.Any(x => x.HostingCustomers > 0))
            list.Add((Loc.T("pg_hosting"), Brand.Cooling, last == null ? "-" : N(last.HostingCustomers),
                new[] { new ChartSeries(Loc.T("pg_hosting"), Brand.Cooling, P(x => x.HostingCustomers), v => N(v), true) }, 0, null));
        list.Add((Loc.T("res_staff"), Brand.Cooling, last == null ? "-" : N(last.Staff),
            new[] { new ChartSeries(Loc.T("res_staff"), Brand.Cooling, P(x => x.Staff), v => N(v), true) }, 0, null));
        return list;
    }

    void StatsTab(GameState s)
    {
        var head = new HBoxContainer(); head.AddThemeConstantOverride("separation", 8);
        var since = s.Stats.Count > 0 ? Loc.T("pg_since", DayText(s.Stats[0].Day)) : Loc.T("pg_first_day");
        head.AddChild(Wrapped(since, 14));
        for (var i = 0; i < Ranges.Length; i++)
        {
            var idx = i;
            var b = new Button { Text = RangeKeys[i], ToggleMode = true, ButtonPressed = i == _range, CustomMinimumSize = new Vector2(80, 32) };
            b.Pressed += () => { _range = idx; _sig = null; Refresh(true); };
            head.AddChild(b);
        }
        _content.AddChild(head);
        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 10); grid.AddThemeConstantOverride("v_separation", 10);
        foreach (var (title, accent, now, series, minY, maxY) in Charts(s, Ranges[_range]))
        {
            var card = Card(accent, out var v);
            var top = new HBoxContainer();
            var t = Plain(title, 14, Brand.InkDim, Ui.BodySemi); t.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            top.AddChild(t);
            top.AddChild(Plain(now, 17, accent, Ui.DisplaySemi));
            v.AddChild(top);
            var chart = new LineChart(DayText, 140) { MinY = minY, MaxY = maxY };
            foreach (var se in series) chart.Add(se);
            v.AddChild(chart);
            if (series.Length > 1)
            {
                var legend = new HBoxContainer(); legend.AddThemeConstantOverride("separation", 12);
                foreach (var se in series) legend.AddChild(Plain("● " + se.Name, 12, se.Color));
                v.AddChild(legend);
            }
            grid.AddChild(card);
        }
        _content.AddChild(grid);
    }

    // ---------------- scenario ----------------
    public static string GoalText(ScenarioDef d, double v) => d.Unit switch
    {
        GoalUnit.Megawatt => N(v / 1000, "0.0") + " MW",
        GoalUnit.Money => double.IsNegativeInfinity(v) ? "-" : Ui.Money((long)Math.Round(v)) + Loc.T("pg_per_month"),
        GoalUnit.Pue => v >= 9 ? "-" : N(v, "0.00"),
        GoalUnit.Score => N(v),
        _ => N(v),
    };

    public static Color MedalColor(int m) => m switch { 3 => new Color("f2c94c"), 2 => new Color("c9d3dc"), 1 => new Color("d08a4e"), _ => Brand.InkDim };

    /// <summary>How far the value is towards the next medal (0..1).</summary>
    public static double NextProgress(ScenarioDef d, double v, int medal)
    {
        if (medal >= 3) return 1;
        var from = medal switch { 0 => d.LowerIsBetter ? Math.Max(d.Bronze * 1.4, d.Bronze + 0.3) : 0, 1 => d.Bronze, _ => d.Silver };
        var to = medal switch { 0 => d.Bronze, 1 => d.Silver, _ => d.Gold };
        if (double.IsInfinity(v) || Math.Abs(to - from) < 1e-9) return 0;
        return Math.Clamp((v - from) / (to - from), 0, 1);
    }

    void ScenarioTab(GameState s)
    {
        var d = s.Scenario;
        if (d == null)
        {
            var card = Card(Brand.InkDim, out var v);
            v.AddChild(Plain(Loc.T("pg_free_play"), 20, Brand.Ink, Ui.DisplaySemi));
            v.AddChild(Wrapped(Loc.T("pg_free_play_help"), 14));
            _content.AddChild(card);
        }
        else
        {
            var value = s.ScenarioValue;
            var card = Card(MedalColor(Math.Max(1, s.ScenarioMedal)), out var v);
            var top = new HBoxContainer(); top.AddThemeConstantOverride("separation", 10);
            var icon = new TextureRect { Texture = GD.Load<Texture2D>($"res://assets/icons/{d.Icon}.svg"), CustomMinimumSize = new Vector2(28, 28), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, Modulate = Brand.Amber };
            top.AddChild(icon);
            var name = Plain(Loc.T("sc_" + d.Id), 22, Brand.Ink, Ui.DisplayBold); name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            top.AddChild(name);
            top.AddChild(Plain(s.ScenarioEnded ? Loc.T("pg_scenario_over") : Loc.T("pg_days_left", N(s.ScenarioDaysLeft)), 15, s.ScenarioEnded ? Brand.Ok : Brand.Amber, Ui.BodySemi));
            v.AddChild(top);
            v.AddChild(Wrapped(Loc.T("sc_" + d.Id + "_desc"), 14, Brand.Ink));
            v.AddChild(new HSeparator());
            v.AddChild(Plain(Loc.T("sc_" + d.Id + "_goal"), 15, Brand.Ink, Ui.BodySemi));
            v.AddChild(Plain(Loc.T("pg_now", GoalText(d, value)), 18, Brand.Amber, Ui.DisplaySemi));
            var medals = new HBoxContainer(); medals.AddThemeConstantOverride("separation", 10);
            foreach (var (m, t) in new[] { (1, d.Bronze), (2, d.Silver), (3, d.Gold) })
            {
                var got = s.ScenarioMedal >= m;
                var mc = Card(MedalColor(m), out var mv);
                if (!got) mc.Modulate = new Color(1, 1, 1, 0.55f);
                var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 6);
                row.AddChild(new TextureRect { Texture = GD.Load<Texture2D>("res://assets/icons/medal.svg"), CustomMinimumSize = new Vector2(22, 22), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, Modulate = MedalColor(m) });
                row.AddChild(Plain(Loc.T(Scenarios.MedalKey(m)) + (got ? "  ✓" : ""), 16, MedalColor(m), Ui.DisplaySemi));
                mv.AddChild(row);
                mv.AddChild(Plain(GoalText(d, t), 14, Brand.Ink));
                medals.AddChild(mc);
            }
            v.AddChild(medals);
            if (!s.ScenarioEnded && s.ScenarioMedal < 3)
            {
                v.AddChild(Plain(Loc.T("pg_next_medal", Loc.T(Scenarios.MedalKey(s.ScenarioMedal + 1))), 13, Brand.InkDim));
                var bar = new ProgressBar { MinValue = 0, MaxValue = 1, Step = 0.001, Value = NextProgress(d, value, s.ScenarioMedal), ShowPercentage = false, CustomMinimumSize = new Vector2(0, 10) };
                bar.AddThemeStyleboxOverride("fill", Ui.Box(MedalColor(s.ScenarioMedal + 1), 3, null, 0, 0));
                v.AddChild(bar);
            }
            _content.AddChild(card);
        }
        _content.AddChild(Ui.Heading("pg_all_scenarios"));
        foreach (var sc in Scenarios.All)
        {
            var best = Profile.Instance.BestMedal(sc.Id);
            var card = Card(MedalColor(best), out var v);
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 10);
            row.AddChild(new TextureRect { Texture = GD.Load<Texture2D>($"res://assets/icons/{sc.Icon}.svg"), CustomMinimumSize = new Vector2(22, 22), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered });
            var n = Plain(Loc.T("sc_" + sc.Id), 16, Brand.Ink, Ui.BodySemi); n.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            row.AddChild(n);
            row.AddChild(Plain(Loc.T("pg_years", sc.Years), 13, Brand.InkDim));
            row.AddChild(Plain(best > 0 ? Loc.T("pg_best", Loc.T(Scenarios.MedalKey(best))) : Loc.T("pg_not_played"), 14, MedalColor(best), Ui.BodySemi));
            v.AddChild(row);
            v.AddChild(Wrapped(Loc.T("sc_" + sc.Id + "_goal"), 13));
            _content.AddChild(card);
        }
        _content.AddChild(Wrapped(Loc.T("pg_scenario_how"), 13));
    }

    // ---------------- achievements ----------------
    void AchievementsTab(GameState s)
    {
        var prof = Profile.Instance;
        var here = s.Achieved.Count;
        var all = Achievements.All.Count(a => prof.Has(a.Id) || s.HasAchievement(a.Id));
        _content.AddChild(Plain(Loc.T("pg_ach_count", here, Achievements.All.Length, all), 16, Brand.Amber, Ui.DisplaySemi));
        if (s.Creative.UnlimitedMoney) _content.AddChild(Wrapped(Loc.T("pg_ach_creative"), 13));
        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 10); grid.AddThemeConstantOverride("v_separation", 8);
        foreach (var a in Achievements.All.OrderByDescending(a => s.HasAchievement(a.Id)).ThenByDescending(a => prof.Has(a.Id)))
        {
            var got = s.HasAchievement(a.Id);
            var before = !got && prof.Has(a.Id);
            var counts = Achievements.Counts(s, a);
            var card = Card(got ? Brand.Amber : Brand.Line, out var v);
            if (!got) card.Modulate = new Color(1, 1, 1, before ? 0.8f : 0.55f);
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 10);
            row.AddChild(new TextureRect
            {
                Texture = GD.Load<Texture2D>($"res://assets/icons/{a.Icon}.svg"), CustomMinimumSize = new Vector2(30, 30),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                Modulate = got ? Brand.Amber : Brand.InkDim,
            });
            var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; text.AddThemeConstantOverride("separation", 0);
            text.AddChild(Plain(Loc.T("ach_" + a.Id) + (got ? "  ✓" : ""), 16, got ? Brand.Ink : Brand.InkDim, Ui.BodySemi));
            text.AddChild(Wrapped(Loc.T("ach_" + a.Id + "_desc"), 13));
            if (before) text.AddChild(Plain(Loc.T("pg_ach_before"), 12, Brand.Cooling));
            else if (!counts) text.AddChild(Plain(Loc.T("pg_ach_money_only"), 12, Brand.InkDim));
            row.AddChild(text);
            v.AddChild(row);
            grid.AddChild(card);
        }
        _content.AddChild(grid);
    }

    // ---------------- datacenter of the year ----------------
    void AwardsTab(GameState s)
    {
        var day = (int)s.Day;
        var year = s.Date.Year;
        var from = GameState.DayOfYearStart(year);
        var marks = s.AwardMarks(from, day);
        var mine = s.AwardScore(from, day);
        var card = Card(Brand.Amber, out var v);
        var top = new HBoxContainer(); top.AddThemeConstantOverride("separation", 10);
        top.AddChild(new TextureRect { Texture = GD.Load<Texture2D>("res://assets/icons/trophy.svg"), CustomMinimumSize = new Vector2(30, 30), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, Modulate = Brand.Amber });
        var title = Plain(Loc.T("aw_title", year), 22, Brand.Ink, Ui.DisplayBold); title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        top.AddChild(title);
        var left = (int)Math.Ceiling(GameState.DayOfYearStart(year + 1) - s.Day);
        top.AddChild(Plain(Loc.T("aw_days_left", left), 15, Brand.Amber, Ui.BodySemi));
        v.AddChild(top);
        v.AddChild(Wrapped(Loc.T("aw_help"), 14, Brand.Ink));
        v.AddChild(new HSeparator());
        if (marks == null) v.AddChild(Wrapped(Loc.T("aw_too_small", GameState.AwardMinRacks), 14, Brand.InkDim));
        else
        {
            v.AddChild(Plain(Loc.T("aw_score_now", mine.ToString("0.0")), 18, Brand.Amber, Ui.DisplaySemi));
            var grid = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            grid.AddThemeConstantOverride("h_separation", 12); grid.AddThemeConstantOverride("v_separation", 4);
            for (var i = 0; i < marks.Length; i++)
            {
                var (key, weight) = GameState.AwardCriteria[i];
                var name = Plain(Loc.T(key) + $"  ({weight * 100:0}%)", 14, Brand.Ink); name.CustomMinimumSize = new Vector2(190, 0);
                grid.AddChild(name);
                var bar = new ProgressBar { MinValue = 0, MaxValue = 100, Value = marks[i], ShowPercentage = false, CustomMinimumSize = new Vector2(220, 10), SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter };
                bar.AddThemeStyleboxOverride("fill", Ui.Box(Brand.Amber, 3, null, 0, 0));
                grid.AddChild(bar);
                grid.AddChild(Plain($"{marks[i]:0}", 14, Brand.InkDim));
            }
            v.AddChild(grid);
        }
        _content.AddChild(card);

        // the field this year
        _content.AddChild(Ui.Heading("aw_field"));
        var field = s.Rivals.Select(c => (name: c.Name, score: GameState.RivalAwardScore(c, year), me: false)).ToList();
        if (marks != null) field.Add((s.Options.Company, mine, true));
        if (field.Count == 0) _content.AddChild(Wrapped(Loc.T("aw_no_rivals"), 13));
        var rank = 1;
        foreach (var f in field.OrderByDescending(f => f.score))
        {
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 10);
            row.AddChild(Plain($"{rank}.", 15, rank <= 3 ? MedalColor(4 - rank) : Brand.InkDim, Ui.BodySemi));
            var n = Plain(f.name, 15, f.me ? Brand.Amber : Brand.Ink, f.me ? Ui.BodySemi : null); n.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            row.AddChild(n);
            row.AddChild(Plain(f.score.ToString("0.0"), 15, Brand.InkDim));
            _content.AddChild(row);
            rank++;
        }
        _content.AddChild(Wrapped(Loc.T("aw_field_help"), 12));

        // earlier years
        _content.AddChild(Ui.Heading("aw_history"));
        if (s.Awards.Count == 0) _content.AddChild(Wrapped(Loc.T("aw_none_yet"), 13));
        foreach (var a in s.Awards.OrderByDescending(a => a.Year))
        {
            var medal = a.Place switch { 1 => 3, 2 => 2, 3 => 1, _ => 0 };
            var c = Card(MedalColor(medal), out var cv);
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 10);
            row.AddChild(new TextureRect { Texture = GD.Load<Texture2D>("res://assets/icons/trophy.svg"), CustomMinimumSize = new Vector2(22, 22), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, Modulate = MedalColor(medal) });
            var t = Plain(a.Year.ToString(), 16, Brand.Ink, Ui.DisplaySemi); t.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            row.AddChild(t);
            row.AddChild(Plain(a.Place switch { 1 => Loc.T("aw_won"), 2 or 3 => Loc.T("aw_place", a.Place), _ => Loc.T("aw_lost", a.Winner) }, 15, MedalColor(medal), Ui.BodySemi));
            row.AddChild(Plain(Loc.T("aw_score", a.Score.ToString("0.0")), 14, Brand.InkDim));
            cv.AddChild(row);
            _content.AddChild(c);
        }
        if (s.Statuettes.Any()) _content.AddChild(Wrapped(s.TrophyDesk != null ? Loc.T("aw_on_desk") : Loc.T("aw_no_desk"), 13));
    }

    // ---------------- timelapse ----------------
    void TimelapseTab(GameState s)
    {
        _frames = Timelapse.Frames(s.GameId);
        _content.AddChild(Wrapped(Loc.T("tl_help", Timelapse.EveryDays), 14, Brand.Ink));
        if (_frames.Count == 0)
        {
            _content.AddChild(Wrapped(Loc.T("tl_none"), 14));
        }
        else
        {
            _frame = Math.Clamp(_frame, 0, _frames.Count - 1);
            var frameBox = new PanelContainer();
            frameBox.AddThemeStyleboxOverride("panel", Ui.Box(new Color(0, 0, 0, 0.35f), 8, Brand.Line, 1, 4));
            _lapseView = new TextureRect { CustomMinimumSize = new Vector2(0, 470), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            frameBox.AddChild(_lapseView);
            _content.AddChild(frameBox);
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 8);
            row.AddChild(Ui.Secondary("tl_first", () => { _playingLapse = false; ShowFrame(0); }));
            _lapsePlay = Ui.Primary(_playingLapse ? "tl_pause" : "tl_play", () =>
            {
                if (!_playingLapse && _frame >= _frames.Count - 1) ShowFrame(0);
                _playingLapse = !_playingLapse; _lapseT = 0;
                _lapsePlay.Text = _playingLapse ? "tl_pause" : "tl_play";
            });
            row.AddChild(_lapsePlay);
            _lapseSlider = new HSlider { MinValue = 0, MaxValue = Math.Max(0, _frames.Count - 1), Step = 1, Value = _frame, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter };
            _lapseSlider.ValueChanged += x => { if ((int)x != _frame) ShowFrame((int)x); };
            row.AddChild(_lapseSlider);
            _lapseDate = Plain("", 15, Brand.Amber, Ui.BodySemi); _lapseDate.CustomMinimumSize = new Vector2(150, 0);
            row.AddChild(_lapseDate);
            var speed = new OptionButton { TooltipText = "tl_speed" };
            foreach (var f in LapseFps) speed.AddItem(Loc.T("tl_fps", f));
            speed.Selected = _lapseSpeed;
            speed.ItemSelected += i => _lapseSpeed = (int)i;
            row.AddChild(speed);
            _content.AddChild(row);
            ShowFrame(_frame);
            _content.AddChild(Plain(Loc.T("tl_count", _frames.Count, DayText(_frames[0].day)), 13, Brand.InkDim));
        }
        var tools = new HBoxContainer(); tools.AddThemeConstantOverride("separation", 8);
        tools.AddChild(Ui.Secondary("tl_folder", () => { DirAccess.MakeDirRecursiveAbsolute(Timelapse.DirOf(s.GameId)); OS.ShellOpen(ProjectSettings.GlobalizePath(Timelapse.DirOf(s.GameId))); }));
        tools.AddChild(Ui.Primary("ph_open", () => _photo?.Invoke()));
        _content.AddChild(tools);
        _content.AddChild(Wrapped(Loc.T("ph_help"), 13));
    }

    void ShowFrame(int i)
    {
        if (_frames.Count == 0 || _lapseView == null) return;
        _frame = Math.Clamp(i, 0, _frames.Count - 1);
        var img = Image.LoadFromFile(ProjectSettings.GlobalizePath(_frames[_frame].path));
        if (img != null && !img.IsEmpty()) _lapseView.Texture = ImageTexture.CreateFromImage(img);
        if (_lapseDate != null) _lapseDate.Text = DayText(_frames[_frame].day);
        if (_lapseSlider != null && (int)_lapseSlider.Value != _frame) _lapseSlider.SetValueNoSignal(_frame);
    }
}
