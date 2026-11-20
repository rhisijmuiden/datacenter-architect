using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DatacenterArchitect.Core;
using DatacenterArchitect.Sim;
using Godot;

namespace DatacenterArchitect.UI;

/// <summary>
/// Step 8 screen with tabs on the left: reputation and reviews, security (level and policies), certificates, the research tree and proposals from the managers.
/// The player chooses what to research and which policies to run; the teams do the rest.
/// </summary>
public partial class ReputationPanel : Modal
{
    public const int TabReputation = 0, TabSecurity = 1, TabCerts = 2, TabResearch = 3, TabProposals = 4;
    static readonly string[] Tabs = { "rp_tab_reputation", "rp_tab_security", "rp_tab_certs", "rp_tab_research", "rp_tab_proposals" };
    static readonly string[] TabIcons = { "star", "shield", "certificate", "flask", "bulb" };

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

    public ReputationPanel(Func<GameState> state, Action<string> fail, int tab = 0) : base("rp_title", 1120)
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
        tabs.AddChild(DynText(s => Loc.T("rp_reputation_short", N(s.Reputation, "0")), 15, Brand.Amber));
        tabs.AddChild(DynText(s => Loc.T("rp_security_short", s.SecurityLevel), 14, Brand.Ink));
        tabs.AddChild(DynText(s => Loc.T("rp_points_day", N(s.ResearchPerDay, "0.0")), 14, Brand.Cooling));
        split.AddChild(tabs);
        var tabLine = new VSeparator();
        split.AddChild(tabLine);
        OwnTabs(tabs, tabLine);
        _scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 620), SizeFlagsHorizontal = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
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
            case TabReputation: ReputationTab(s); break;
            case TabSecurity: SecurityTab(s); break;
            case TabCerts: CertsTab(s); break;
            case TabResearch: ResearchTab(s); break;
            default: ProposalsTab(s); break;
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

    string Signature(GameState s) =>
        $"{_tab}|{Loc.Language}|{(int)s.Day}|{s.Money / 10000}|{(int)s.Reputation}|{s.Reviews.Count}:{s.Reviews.FirstOrDefault()?.Day}|{s.SecurityLevel}|{s.PolicyIdCheck}{s.PolicyEscort}{s.PolicyOfficeHours}|" +
        $"{string.Join(",", s.Certs.Select(c => $"{c.Kind}{c.Valid}{c.AuditEnds}"))}|{string.Join(",", s.Researched)}|{string.Join(",", s.ResearchQueue)}|{(int)s.ResearchProgress}|" +
        $"{s.OpenProposals.Count}|{s.Upgrades.Count}|{s.Execs.Count}|{s.Staff.Count}|{s.Revision / 50}";

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
    static string Stars(int n) => new string('★', n) + new string('☆', 5 - n);
    static string When(double day) => GameState.Epoch.AddDays(day).ToString("d MMM yyyy", Culture);

    static Color TopicColor(ReviewTopic t) => t switch
    {
        ReviewTopic.Power => Brand.Power, ReviewTopic.Network => Brand.Fiber, ReviewTopic.Security => Brand.Critical, _ => Brand.Cooling,
    };

    // ---------------- reputation and reviews ----------------
    void ReputationTab(GameState s)
    {
        _content.AddChild(Ui.Heading("rp_tab_reputation", 16, Brand.Amber));
        _content.AddChild(Wrapped(Loc.T("rp_reputation_help")));
        var top = Row(18);
        var big = Ui.Number(N(s.Reputation, "0"), 40); big.AddThemeColorOverride("font_color", Score(s.Reputation)); big.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        top.AddChild(big);
        var tv = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        tv.AddChild(Plain(Loc.T("rp_out_of"), 13, Brand.InkDim));
        tv.AddChild(Bar(s.Reputation, Score(s.Reputation)));
        if (s.RepHistory.Count > 0)
        {
            var then = s.RepHistory[Math.Max(0, s.RepHistory.Count - 3)];
            var diff = s.Reputation - then;
            tv.AddChild(Plain(Loc.T(diff >= 0 ? "rp_trend_up" : "rp_trend_down", N(Math.Abs(diff), "0.0")), 13, diff >= 0 ? Brand.Ok : Brand.Warning));
        }
        top.AddChild(tv);
        _content.AddChild(top);
        _content.AddChild(Wrapped(Loc.T("rp_unlocks")));

        _content.AddChild(new HSeparator());
        _content.AddChild(Ui.Heading("rp_quality", 15, Brand.Ink));
        foreach (var t in Enum.GetValues<ReviewTopic>())
        {
            var r = Row(12);
            var n = Plain(Loc.T("rev_topic_" + t.ToString().ToLowerInvariant()), 14); n.CustomMinimumSize = new Vector2(150, 0);
            r.AddChild(n);
            var q = s.QualityOf(t);
            r.AddChild(Bar(q, TopicColor(t)));
            var v = Plain(N(q, "0"), 14, Score(q)); v.CustomMinimumSize = new Vector2(36, 0); v.HorizontalAlignment = HorizontalAlignment.Right;
            r.AddChild(v);
            _content.AddChild(r);
            _content.AddChild(Wrapped(Loc.T("rp_quality_" + t.ToString().ToLowerInvariant() + "_help"), 12));
        }
        var certs = s.Certs.Where(c => c.Valid).ToList();
        _content.AddChild(Plain(certs.Count == 0 ? Loc.T("rp_no_certs") : Loc.T("rp_certs_list", string.Join(", ", certs.Select(c => Loc.T(CertCatalog.Defs[c.Kind].Key)))), 14, certs.Count == 0 ? Brand.InkDim : Brand.Ok));
        if (s.Has(Exec.Marketing)) _content.AddChild(Plain(Loc.T("rp_marketing_on", N((s.MarketingFactor - 1) * 100)), 13, Brand.Ok));
        else _content.AddChild(Plain(Loc.T("rp_marketing_off"), 13, Brand.InkDim));

        _content.AddChild(new HSeparator());
        _content.AddChild(Ui.Heading("rp_reviews", 15, Brand.Ink));
        if (s.Reviews.Count == 0) _content.AddChild(Plain(Loc.T("rp_no_reviews"), 14, Brand.InkDim));
        foreach (var rv in s.Reviews.Take(14))
        {
            var card = Card(TopicColor(rv.Topic), out var v);
            var head = Row();
            var who = Plain(StaffPanel.Arg(rv.Who.StartsWith('@') ? rv.Who : "@rev_who|" + rv.Who), 14); who.AddThemeFontOverride("font", Ui.BodySemi); who.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            head.AddChild(who);
            head.AddChild(Plain(Stars(rv.Stars), 15, rv.Stars >= 4 ? Brand.Ok : rv.Stars >= 3 ? Brand.Amber : Brand.Critical));
            head.AddChild(Plain(When(rv.Day), 12, Brand.InkDim));
            v.AddChild(head);
            var band = rv.Stars >= 4 ? "good" : rv.Stars >= 3 ? "ok" : "bad";
            v.AddChild(Wrapped(Loc.T($"rev_{rv.Topic.ToString().ToLowerInvariant()}_{band}"), 13, Brand.Ink));
            _content.AddChild(card);
        }
    }

    // ---------------- security ----------------
    static readonly string[] PolicyKeys = { "pol_id_check", "pol_escort", "pol_office_hours" };

    void SecurityTab(GameState s)
    {
        _content.AddChild(Ui.Heading("rp_tab_security", 16, Brand.Critical));
        _content.AddChild(Wrapped(Loc.T("sec_help")));
        var lvl = s.SecurityLevel;
        var top = Row(18);
        var big = Ui.Number(lvl.ToString(), 40); big.AddThemeColorOverride("font_color", Score(lvl)); big.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        top.AddChild(big);
        var tv = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        tv.AddChild(Plain(Loc.T("sec_level_of"), 13, Brand.InkDim));
        tv.AddChild(Bar(lvl, Score(lvl)));
        tv.AddChild(Plain(Loc.T("sec_needs_line", GameState.SecurityNeed(CustomerType.Municipality), GameState.SecurityNeed(CustomerType.Bank), GameState.SecurityNeed(CustomerType.Government)), 13, Brand.InkDim));
        top.AddChild(tv);
        _content.AddChild(top);
        foreach (var l in s.SecurityLines())
        {
            var r = Row(12);
            var n = Plain(Loc.T(l.Key), 14); n.CustomMinimumSize = new Vector2(200, 0);
            r.AddChild(n);
            r.AddChild(Bar(100.0 * l.Points / l.Max, l.Points >= l.Max ? Brand.Ok : Brand.Amber));
            var v = Plain($"{l.Points} / {l.Max}", 14, Brand.InkDim); v.CustomMinimumSize = new Vector2(64, 0); v.HorizontalAlignment = HorizontalAlignment.Right;
            r.AddChild(v);
            _content.AddChild(r);
            _content.AddChild(Wrapped(Loc.T(l.Key + "_tip"), 12));
        }
        _content.AddChild(new HSeparator());
        _content.AddChild(Ui.Heading("sec_policies_title", 15, Brand.Ink));
        _content.AddChild(Wrapped(Loc.T("sec_policies_help")));
        if (!s.Has(Exec.Security)) _content.AddChild(Plain(Loc.T("msg_needs_security_mgr"), 13, Brand.Warning));
        var on = new[] { s.PolicyIdCheck, s.PolicyEscort, s.PolicyOfficeHours };
        for (var i = 0; i < PolicyKeys.Length; i++)
        {
            var idx = i;
            var card = Card(on[i] ? Brand.Ok : Brand.Line, out var v);
            var cb = new CheckButton { Text = Loc.T(PolicyKeys[i]), ButtonPressed = on[i], AutoTranslateMode = AutoTranslateModeEnum.Disabled };
            cb.AddThemeFontOverride("font", Ui.BodySemi);
            cb.Toggled += b => { _state().SetPolicy(idx, b); _sig = null; };
            v.AddChild(cb);
            v.AddChild(Wrapped(Loc.T(PolicyKeys[i] + "_desc"), 13));
            _content.AddChild(card);
        }
        var guards = s.CountOf(StaffRole.Guard);
        var want = s.Target(StaffRole.Guard);
        _content.AddChild(Plain(Loc.T("sec_guards", guards, want), 14, guards >= want ? Brand.Ok : Brand.Warning));
    }

    // ---------------- certificates ----------------
    void CertsTab(GameState s)
    {
        _content.AddChild(Ui.Heading("rp_tab_certs", 16, Brand.Ok));
        _content.AddChild(Wrapped(Loc.T("cert_help")));
        if (!s.Has(Exec.Compliance)) _content.AddChild(Plain(Loc.T("msg_needs_compliance"), 14, Brand.Warning));
        foreach (var k in Enum.GetValues<CertKind>())
        {
            var def = CertCatalog.Defs[k];
            var c = s.Cert(k);
            var card = Card(c?.Valid == true ? Brand.Ok : c?.AuditEnds > 0 ? Brand.Amber : Brand.Line, out var v);
            var head = Row();
            var n = Plain(Loc.T(def.Key), 17); n.AddThemeFontOverride("font", Ui.BodySemi); n.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            head.AddChild(n);
            string status = c == null ? Loc.T("cert_none")
                : c.AuditEnds > 0 ? Loc.T("cert_auditing", N(Math.Max(0, c.AuditEnds - s.Day), "0"))
                : Loc.T("cert_valid_until", When(c.ExpiresDay));
            head.AddChild(Plain(status, 13, c?.Valid == true ? Brand.Ok : c?.AuditEnds > 0 ? Brand.Amber : Brand.InkDim));
            v.AddChild(head);
            v.AddChild(Wrapped(Loc.T(def.Key + "_desc"), 13));
            foreach (var r in s.CertChecks(k))
            {
                var mark = r.Status == ReqStatus.Ok ? "✓" : "✕";
                var text = $"{mark}  {Loc.T(r.Key)}" + (r.Have != "" || r.Need != "" ? $"  ({(r.Have != "" ? r.Have : "-")}{(r.Need != "" ? " / " + r.Need : "")})" : "");
                v.AddChild(Plain(text, 13, r.Status == ReqStatus.Ok ? Brand.Ok : Brand.Critical));
            }
            var acts = Row();
            var kind = k;
            acts.AddChild(Act(Loc.T(c?.Valid == true ? "cert_renew" : "cert_audit", Ui.Money(def.Cost), def.AuditDays), st => st.RequestAudit(kind), s.CanAudit(k) == null, c == null));
            acts.AddChild(Plain(Loc.T("cert_reward", N(def.Reputation)), 13, Brand.InkDim));
            v.AddChild(acts);
            _content.AddChild(card);
        }
    }

    // ---------------- research ----------------
    static Color BranchColor(Branch b) => b switch
    {
        Branch.Power => Brand.Power, Branch.Cooling => Brand.Cooling, Branch.Network => Brand.Fiber,
        Branch.Security => Brand.Critical, Branch.Business => Brand.Ok, _ => Brand.Wall,
    };

    void ResearchTab(GameState s)
    {
        _content.AddChild(Ui.Heading("rp_tab_research", 16, Brand.Cooling));
        _content.AddChild(Wrapped(Loc.T("rs_help")));
        if (!s.Has(Exec.Research)) _content.AddChild(Plain(Loc.T("msg_needs_research_mgr"), 14, Brand.Warning));
        else if (s.LabBenches == 0) _content.AddChild(Plain(Loc.T("rs_needs_lab"), 14, Brand.Warning));
        _content.AddChild(Plain(Loc.T("rs_status", s.CountOf(StaffRole.Researcher), s.LabBenches, N(s.ResearchPerDay, "0.0")), 14, Brand.Ink));
        // the queue
        if (s.ResearchQueue.Count > 0)
        {
            var card = Card(Brand.Cooling, out var v);
            v.AddChild(Ui.Heading("rs_queue", 13, Brand.Cooling));
            for (var i = 0; i < s.ResearchQueue.Count; i++)
            {
                var id = s.ResearchQueue[i];
                var def = ResearchTree.ById[id];
                var r = Row(10);
                var n = Plain($"{i + 1}. {Loc.T("rs_" + id)}", 14); n.CustomMinimumSize = new Vector2(260, 0);
                r.AddChild(n);
                if (i == 0) r.AddChild(Bar(100 * s.ResearchProgress / def.Points, BranchColor(def.Branch)));
                else r.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
                var days = s.ResearchPerDay > 0 ? N(Math.Ceiling(s.ResearchQueue.Take(i + 1).Sum(s.ResearchLeft) / s.ResearchPerDay)) : "∞";
                r.AddChild(Plain(Loc.T("rs_days", days), 13, Brand.InkDim));
                var rid = id;
                r.AddChild(Act(Loc.T("rs_cancel"), st => { st.CancelResearch(rid); return null; }));
                v.AddChild(r);
            }
            _content.AddChild(card);
        }
        foreach (var b in ResearchTree.Branches)
        {
            _content.AddChild(Ui.Heading("branch_" + b.ToString().ToLowerInvariant(), 14, BranchColor(b)));
            var flow = new HFlowContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            flow.AddThemeConstantOverride("h_separation", 8); flow.AddThemeConstantOverride("v_separation", 8);
            foreach (var def in ResearchTree.All.Where(d => d.Branch == b).OrderBy(d => d.Tier))
            {
                var done = s.Done(def.Id);
                var queued = s.ResearchQueue.IndexOf(def.Id);
                var why = done || queued >= 0 ? null : s.CanResearch(def.Id);
                var accent = done ? Brand.Ok : queued >= 0 ? Brand.Cooling : why == null ? BranchColor(b) : Brand.Line;
                var card = Card(accent, out var v, 300);
                var head = Row(6);
                var n = Plain(Loc.T("rs_" + def.Id), 14); n.AddThemeFontOverride("font", Ui.BodySemi); n.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                n.AutowrapMode = TextServer.AutowrapMode.WordSmart; n.CustomMinimumSize = new Vector2(230, 0);
                head.AddChild(n);
                if (done) head.AddChild(Plain("✓", 16, Brand.Ok));
                v.AddChild(head);
                var desc = Wrapped(Loc.T("rs_" + def.Id + "_desc"), 12);
                desc.CustomMinimumSize = new Vector2(250, 0); desc.SizeFlagsHorizontal = SizeFlags.Fill;
                v.AddChild(desc);
                if (done) { flow.AddChild(card); continue; }
                v.AddChild(Plain(Loc.T("rs_cost", def.Points, Ui.Money(def.Cost)), 12, Brand.InkDim));
                if (def.Requires.Length > 0) v.AddChild(Plain(Loc.T("rs_requires", string.Join(", ", def.Requires.Select(r => Loc.T("rs_" + r)))), 12, def.Requires.All(s.Done) ? Brand.InkDim : Brand.Warning));
                if (def.Year > s.Date.Year) v.AddChild(Plain(Loc.T("rs_from_year", def.Year), 12, Brand.Warning));
                if (queued >= 0) v.AddChild(Plain(Loc.T("rs_in_queue", queued + 1), 12, Brand.Cooling));
                else
                {
                    var id = def.Id;
                    v.AddChild(Act(Loc.T("rs_start"), st => st.QueueResearch(id), why == null, why == null));
                }
                flow.AddChild(card);
            }
            _content.AddChild(flow);
        }
    }

    // ---------------- proposals ----------------
    void ProposalsTab(GameState s)
    {
        _content.AddChild(Ui.Heading("rp_tab_proposals", 16, Brand.Amber));
        _content.AddChild(Wrapped(Loc.T("prop_help")));
        if (s.OpenProposals.Count == 0) _content.AddChild(Plain(Loc.T("prop_none"), 14, Brand.InkDim));
        foreach (var p in s.OpenProposals)
        {
            var key = "prop_" + p.Kind.ToString().ToLowerInvariant();
            var card = Card(Brand.Amber, out var v);
            var head = Row();
            var n = Plain(Loc.T(Org.Execs[Proposals.From(p.Kind)].Key), 13, Brand.InkDim); n.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            head.AddChild(n);
            head.AddChild(Plain(Loc.T("prop_expires", N(Math.Max(0, p.ExpiresDay - s.Day))), 12, Brand.InkDim));
            v.AddChild(head);
            var quote = Wrapped(Loc.T(key + "_quote", Ui.Money(p.Cost), p.Brand, N(p.Value * 100)), 15, Brand.Ink);
            v.AddChild(quote);
            var acts = Row();
            var prop = p;
            acts.AddChild(Act(Loc.T("prop_accept"), st => st.AcceptProposal(prop), s.CanAfford(p.Cost), true));
            acts.AddChild(Act(Loc.T("prop_decline"), st => { st.DeclineProposal(prop); return null; }));
            v.AddChild(acts);
            _content.AddChild(card);
        }
        if (s.Upgrades.Count > 0)
        {
            _content.AddChild(new HSeparator());
            _content.AddChild(Ui.Heading("prop_accepted", 14, Brand.Ok));
            foreach (var u in s.Upgrades.AsEnumerable().Reverse().Take(12))
                _content.AddChild(Plain("✓  " + Loc.T("prop_" + u.Kind.ToString().ToLowerInvariant(), u.Brand) + $" (+{N(u.Value * 100)}%)", 13, Brand.Ink));
        }
    }
}
