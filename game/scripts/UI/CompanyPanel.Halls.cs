using System;
using System.Linq;
using DatacenterArchitect.Core;
using DatacenterArchitect.Sim;
using DatacenterArchitect.World;
using Godot;

namespace DatacenterArchitect.UI;

/// <summary>
/// Infra sketch board 7, under Company › Contracts: every data hall with its code, name, colour band, what it is sold as,
/// which customers it takes and who rents it whole. Contracts get a "place in" list to put them in a hall.
/// </summary>
public partial class CompanyPanel
{
    static string ZoneText(HallZone z) => Loc.T("zone_" + z.ToString().ToLowerInvariant());

    /// <summary>A customer's logo, made from its name (the same one hangs on the wall of a hall it rents).</summary>
    public static Control CustomerLogo(Deal d, float size)
    {
        var name = DealName(d);
        var view = new LogoView { CustomMinimumSize = new Vector2(size, size), MouseFilter = MouseFilterEnum.Ignore };
        view.Set(CompanyLogo.ForCustomer(d), CompanyLogo.InitialsOf(name));
        return view;
    }

    void HallsSection(GameState s)
    {
        _content.AddChild(Ui.Heading("halls_title", 16, Brand.Cooling));
        _content.AddChild(Plain(Loc.T("halls_help"), 13, Brand.InkDim));
        var halls = s.Rooms.Where(r => r.Type == RoomType.DataHall && !s.IsPlanned(r)).OrderBy(r => r.HallNo).ToList();
        if (halls.Count == 0) _content.AddChild(Plain(Loc.T("zones_none"), 14, Brand.Warning));
        var owners = s.RackOwners();
        var working = s.WorkingRacks();
        foreach (var hall in halls)
        {
            s.HallCode(hall);
            var band = new Color(GameState.HallBand(hall));
            var card = Card(band, out var v);
            var racks = s.Objects.Where(o => o.Kind == ObjKind.Rack && o.Floor == hall.Floor && hall.Contains(o.X, o.Z)).ToList();

            var head = Row(10);
            var code = Plain(s.HallCode(hall), 18, band); code.AddThemeFontOverride("font", Ui.DisplayBold);
            code.CustomMinimumSize = new Vector2(70, 0);
            head.AddChild(code);
            var room = hall;
            var name = new LineEdit
            {
                Text = hall.Name ?? "", PlaceholderText = Loc.T("halls_name_hint"), MaxLength = GameState.MaxHallName,
                CustomMinimumSize = new Vector2(230, 0), SizeFlagsHorizontal = SizeFlags.ExpandFill, AutoTranslateMode = AutoTranslateModeEnum.Disabled,
                TooltipText = Loc.T("halls_name_tip"),
            };
            void Save() { var st = _state(); if (st != null && name.Text != (room.Name ?? "")) { st.RenameHall(room, name.Text); _sig = null; } }
            name.TextSubmitted += _ => { Save(); name.ReleaseFocus(); };
            name.FocusExited += Save;
            head.AddChild(name);
            head.AddChild(Plain(Loc.T("zones_hall_short", racks.Count, racks.Count(o => owners.ContainsKey(o.Id)), racks.Count(o => working.Contains(o.Id) && !owners.ContainsKey(o.Id)))
                + (hall.Stage != Stage.Done ? "  ·  " + Loc.T("place_building") : "") + (hall.Floor != 0 ? "  ·  " + Ui.FloorName(hall.Floor) : ""), 13, Brand.InkDim));
            v.AddChild(head);

            var row = Row(10);
            row.AddChild(Plain(Loc.T("halls_sold_as"), 13, Brand.InkDim));
            var opt = new OptionButton { CustomMinimumSize = new Vector2(200, 0), AutoTranslateMode = AutoTranslateModeEnum.Disabled, TooltipText = Loc.T("zones_help") };
            foreach (var z in Enum.GetValues<HallZone>()) opt.AddItem(ZoneText(z), (int)z);
            opt.Selected = (int)hall.Zone;
            opt.ItemSelected += idx => { _state().SetZone(room, (HallZone)(int)idx); _sig = null; };
            row.AddChild(opt);
            row.AddChild(ClientPicker(_state, room));
            row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
            for (var i = 0; i < GameState.HallBands.Length; i++)
            {
                var idx = i;
                var sw = new Button { CustomMinimumSize = new Vector2(20, 20), FocusMode = FocusModeEnum.None, TooltipText = Loc.T("halls_band") };
                var on = GameState.HallBand(hall) == GameState.HallBands[i];
                var st = Ui.Box(new Color(GameState.HallBands[i]), 4, on ? Brand.Ink : new Color(0, 0, 0, 0), on ? 2 : 0, 0);
                sw.AddThemeStyleboxOverride("normal", st); sw.AddThemeStyleboxOverride("hover", st); sw.AddThemeStyleboxOverride("pressed", st);
                sw.Pressed += () => { _state()?.SetHallBand(room, idx); _sig = null; Refresh(true); };
                row.AddChild(sw);
            }
            v.AddChild(row);

            var tenant = s.HallTenant(hall);
            if (tenant != null)
            {
                var t = Row(10);
                t.AddChild(CustomerLogo(tenant, 34));
                var tl = Plain(Loc.T(tenant.State == DealState.Building ? "halls_reserved" : "halls_rented", DealName(tenant), tenant.Racks), 14, Brand.Ok);
                tl.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                t.AddChild(tl);
                var logo = new CheckBox { Text = Loc.T("halls_logo"), ButtonPressed = hall.ShowLogo, AutoTranslateMode = AutoTranslateModeEnum.Disabled };
                logo.Toggled += on => { _state()?.SetHallLogo(room, on); _sig = null; };
                t.AddChild(logo);
                v.AddChild(t);
            }
            else
            {
                var tenants = s.Deals.Where(d => d.State == DealState.Active && s.DealHalls(d).Contains(hall)).Select(DealName).Distinct().ToList();
                if (tenants.Count > 0)
                    v.AddChild(Plain(Loc.T("halls_customers", tenants.Count, string.Join(", ", tenants.Take(4)) + (tenants.Count > 4 ? " …" : "")), 13, Brand.InkDim));
            }
            _content.AddChild(card);
        }
        _content.AddChild(new HSeparator());
    }

    /// <summary>"Place in": let sales choose, or put the contract in one hall. Halls that cannot take it say why.</summary>
    Control PlaceRow(GameState s, Deal d)
    {
        var row = Row(10);
        row.AddChild(Plain(Loc.T("place_in"), 13, Brand.InkDim));
        var opt = new OptionButton { CustomMinimumSize = new Vector2(420, 0), AutoTranslateMode = AutoTranslateModeEnum.Disabled, ClipText = true };
        opt.AddItem(Loc.T("place_auto"), 0);
        var choices = s.HallChoices(d);
        var sel = 0;
        for (var i = 0; i < choices.Count; i++)
        {
            var c = choices[i];
            var text = $"{s.HallTitle(c.Hall)}  ·  " + Loc.T("place_free", c.Free, d.Racks) + (c.Problem != null ? "  ·  " + Loc.T(c.Problem) : "  ·  " + Loc.T("place_fits"));
            opt.AddItem(text, c.Hall.Id);
            var idx = opt.ItemCount - 1;
            opt.SetItemIcon(idx, Swatch(new Color(GameState.HallBand(c.Hall))));
            if (c.Problem is "place_taken" or "place_other_zone") opt.SetItemDisabled(idx, true);
            if (c.Hall.Id == d.HallId) sel = idx;
        }
        opt.Selected = sel;
        opt.ItemSelected += idx =>
        {
            var st = _state();
            if (st == null) return;
            var why = st.PlaceDeal(d, opt.GetItemId((int)idx));
            if (why != null) _fail(why);
            _sig = null; Refresh(true);
        };
        row.AddChild(opt);
        if (d.HallId != 0 && choices.FirstOrDefault(c => c.Hall.Id == d.HallId) is { Problem: not null } bad)
            row.AddChild(Plain(Loc.T(bad.Problem), 13, bad.Problem == "place_short" || bad.Problem == "place_building" ? Brand.Warning : Brand.Critical));
        return row;
    }

    static readonly System.Collections.Generic.Dictionary<Color, Texture2D> SwatchIcons = new();

    /// <summary>A small square of colour for list items.</summary>
    static Texture2D Swatch(Color c)
    {
        if (SwatchIcons.TryGetValue(c, out var t)) return t;
        var img = Image.CreateEmpty(12, 12, false, Image.Format.Rgba8);
        img.Fill(c);
        return SwatchIcons[c] = ImageTexture.CreateFromImage(img);
    }
}
