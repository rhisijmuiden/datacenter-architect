using System;
using System.Collections.Generic;
using DatacenterArchitect.Core;
using DatacenterArchitect.Sim;
using Godot;

namespace DatacenterArchitect.UI;

/// <summary>
/// Step 11 round 2: the tutorial coach. A small card at the top left that explains the next goal of the checklist:
/// what to build, where it is in the build bar (the button glows), and why a datacenter needs it.
/// "Show me" opens the right build category or screen; "Next tip" skips a tip; "Stop tutorial" ends it for this game.
/// </summary>
public partial class TutorialCoach : PanelContainer
{
    public Func<GameState> State;
    public Func<IReadOnlyList<(string key, bool done)>> Goals;
    public Hud Hud;
    public Action<int, int> OpenSection;

    Label _step, _title, _text;
    Button _show, _next, _stop;
    string _current;
    Button _glow;
    double _t;

    public override void _Ready()
    {
        Position = new Vector2(Hud.LeftEdge, 84);
        CustomMinimumSize = new Vector2(420, 0);
        Size = new Vector2(420, 0);
        MouseFilter = MouseFilterEnum.Stop;
        var box = Ui.Box(Brand.Panel, 10, Brand.Amber, 1, 14, true);
        box.BorderWidthLeft = 4;
        AddThemeStyleboxOverride("panel", box);
        var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", 6);
        AddChild(v);
        var head = new HBoxContainer(); head.AddThemeConstantOverride("separation", 8);
        head.AddChild(new TextureRect { Texture = GD.Load<Texture2D>("res://assets/icons/bulb.svg"), CustomMinimumSize = new Vector2(18, 18), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, Modulate = Brand.Amber });
        _step = Ui.Heading("", 12, Brand.Amber); _step.AutoTranslateMode = AutoTranslateModeEnum.Disabled; _step.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        head.AddChild(_step);
        v.AddChild(head);
        _title = Ui.Text("", 18, Brand.Ink, Ui.DisplaySemi); _title.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        // autowrap labels without a width report a tall minimum size: give every wrapping label the card's width
        _title.CustomMinimumSize = new Vector2(390, 0);
        v.AddChild(_title);
        _text = Ui.Text("", 14, Brand.Ink); _text.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        _text.CustomMinimumSize = new Vector2(390, 0);
        v.AddChild(_text);
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 8);
        _show = Ui.Secondary("tut_show", Show); _show.CustomMinimumSize = new Vector2(0, 32);
        _next = Ui.Secondary("tut_next", Skip); _next.CustomMinimumSize = new Vector2(0, 32);
        var spacer = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _stop = new Button { Text = "tut_stop", Flat = true, CustomMinimumSize = new Vector2(0, 32) };
        _stop.AddThemeColorOverride("font_color", Brand.InkDim);
        _stop.Pressed += Stop;
        row.AddChild(_show); row.AddChild(_next); row.AddChild(spacer); row.AddChild(_stop);
        v.AddChild(row);
        Visible = false;
    }

    /// <summary>Called once a second by the game screen.</summary>
    public void Refresh()
    {
        var s = State?.Invoke();
        var cur = Tutorial.Current(s, Goals?.Invoke() ?? Array.Empty<(string, bool)>());
        Visible = cur != null && (Hud == null || !IsInstanceValid(Hud) || Hud.Visible);
        if (cur == _current && !_langChanged()) return;
        _current = cur; _lang = Loc.Language;
        Glow(null);
        if (cur == null) return;
        if (cur == Tutorial.Welcome)
        {
            _step.Text = Loc.T("tut_heading");
            _title.Text = Loc.T("tut_welcome");
            _text.Text = Loc.T("tut_welcome_text");
            _show.Visible = false; _next.Text = "tut_start";
        }
        else if (cur == Tutorial.Finished)
        {
            _step.Text = Loc.T("tut_heading");
            _title.Text = Loc.T("tut_finished");
            _text.Text = Loc.T("tut_finished_text");
            _show.Visible = false; _next.Text = "tut_close";
        }
        else
        {
            var st = Tutorial.Step(cur);
            _step.Text = Loc.T("tut_step", Tutorial.Index(cur), Tutorial.Steps.Length);
            _title.Text = Loc.T(cur);
            var where = st.Category != null ? Loc.T("tut_where_build", Loc.T(st.Category)) : Loc.T("tut_where_screen", Loc.T(Sections.All[st.Section].Key));
            _text.Text = Loc.T("tut_" + cur[5..]) + "\n" + where;
            _show.Visible = true; _next.Text = "tut_next";
            Glow(Hud?.CategoryButton(st.Category));
        }
        Size = new Vector2(420, 0);
        ResetSize();
    }

    string _lang;
    bool _langChanged() => _lang != Loc.Language;

    void Glow(Button b)
    {
        if (_glow != null && IsInstanceValid(_glow)) _glow.SelfModulate = Colors.White;
        _glow = b;
    }

    public override void _Process(double delta)
    {
        if (Visible && Hud != null && IsInstanceValid(Hud) && !Hud.Visible) Visible = false;
        if (_glow == null || !IsInstanceValid(_glow) || !Visible) return;
        _t += delta;
        var k = 0.5f + 0.5f * Mathf.Sin((float)_t * 5f);
        _glow.SelfModulate = Colors.White.Lerp(new Color(1.6f, 1.25f, 0.55f), k);
    }

    void Show()
    {
        var st = Tutorial.Step(_current);
        if (st == null) return;
        if (st.Category != null) Hud?.OpenCategory(st.Category);
        else OpenSection?.Invoke(st.Section, st.Tab);
    }

    void Skip()
    {
        var s = State?.Invoke();
        if (s == null || _current == null) return;
        if (_current == Tutorial.Finished) { s.Options.Tutorial = false; }
        else s.TutorialSkipped.Add(_current);
        _current = "";
        Refresh();
    }

    void Stop()
    {
        var s = State?.Invoke();
        if (s != null) s.Options.Tutorial = false;
        Glow(null);
        Refresh();
    }

    public override void _ExitTree() => Glow(null);
}
