using System;
using System.Linq;
using DatacenterArchitect.Core;
using DatacenterArchitect.Sim;
using Godot;

namespace DatacenterArchitect.UI;

/// <summary>
/// Step 11 round 2: the plan bar at the top centre. In plan mode it shows what the blueprint holds and costs, with
/// "Build plan" (pay and start in one click), "Discard" and "Done planning". With plan mode off and a blueprint left,
/// it stays as a slim reminder with the same buttons.
/// </summary>
public partial class PlanBar : PanelContainer
{
    public Func<GameState> State;
    public Func<bool> PlanMode;
    public Action Build, Discard, Toggle;

    Label _title, _info;
    Button _build, _discard, _toggle;
    string _sig;

    public override void _Ready()
    {
        AnchorLeft = AnchorRight = 0.5f;
        OffsetTop = 84;
        GrowHorizontal = GrowDirection.Both;
        var box = Ui.Box(new Color(Brand.Panel, 0.96f), 10, Brand.Cooling, 1, 10, true);
        box.BorderWidthTop = 3;
        AddThemeStyleboxOverride("panel", box);
        var h = new HBoxContainer(); h.AddThemeConstantOverride("separation", 10);
        AddChild(h);
        h.AddChild(new TextureRect { Texture = GD.Load<Texture2D>("res://assets/icons/blueprint.svg"), CustomMinimumSize = new Vector2(26, 26), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, Modulate = Brand.Cooling, SizeFlagsVertical = SizeFlags.ShrinkCenter });
        var v = new VBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter }; v.AddThemeConstantOverride("separation", 0);
        _title = Ui.Heading("", 13, Brand.Cooling); _title.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        _info = Ui.Text("", 15, Brand.Ink, Ui.BodySemi); _info.AutowrapMode = TextServer.AutowrapMode.Off; _info.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        v.AddChild(_title); v.AddChild(_info);
        h.AddChild(v);
        h.AddChild(new VSeparator());
        _build = Ui.Primary("plan_build", () => Build?.Invoke()); _build.CustomMinimumSize = new Vector2(0, 36); _build.AddThemeFontSizeOverride("font_size", 15);
        _discard = Ui.Secondary("plan_discard", () => Discard?.Invoke()); _discard.CustomMinimumSize = new Vector2(0, 36);
        _toggle = Ui.Secondary("plan_done", () => Toggle?.Invoke()); _toggle.CustomMinimumSize = new Vector2(0, 36);
        h.AddChild(_build); h.AddChild(_discard); h.AddChild(_toggle);
        Visible = false;
    }

    public void Refresh()
    {
        var s = State?.Invoke();
        var on = PlanMode?.Invoke() == true;
        if (s == null) { Visible = false; return; }
        var count = s.PlanCount;
        var cost = s.PlanCost;
        Visible = on || count > 0;
        var sig = $"{on}|{count}|{cost}|{s.CanAfford(cost)}|{Loc.Language}";
        if (sig == _sig) return;
        _sig = sig;
        _title.Text = Loc.T(on ? "plan_mode_on" : "plan_waiting");
        _info.Text = count == 0 ? Loc.T("plan_empty_help") : Loc.T("plan_summary", count, Ui.Money(cost));
        _build.Disabled = count == 0;
        _build.TooltipText = count > 0 && !s.CanAfford(cost) ? Loc.T("msg_no_money") : Loc.T("plan_build_tip");
        if (count > 0 && !s.CanAfford(cost)) _info.Text += "  ·  " + Loc.T("plan_short", Ui.Money(cost - s.Money));
        _info.AddThemeColorOverride("font_color", count > 0 && !s.CanAfford(cost) ? Brand.Critical : Brand.Ink);
        _discard.Visible = count > 0;
        _toggle.Text = on ? "plan_done" : "plan_edit";
        ResetSize();
    }
}
