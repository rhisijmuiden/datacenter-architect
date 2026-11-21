using System;
using System.Collections.Generic;
using System.Linq;
using DatacenterArchitect.Core;
using DatacenterArchitect.Sim;
using Godot;

namespace DatacenterArchitect.UI;

/// <summary>
/// In-game help (playtest): what every room is for, when you need it and what must go inside, and how power,
/// cooling, network and redundancy work. The "minimum inside" list is built from <see cref="GameState.RoomMinimum"/>,
/// the same rules the room inspector checks, so it never goes out of date. Texts in game/locale/help.tsv.
/// </summary>
public partial class HelpPanel : Modal
{
    public static readonly RoomType[] DatacenterRooms =
    {
        RoomType.DataHall, RoomType.MeetMe, RoomType.Switchgear, RoomType.UpsRoom, RoomType.Battery, RoomType.Transformer,
        RoomType.LoadingDock, RoomType.Storage, RoomType.Corridor, RoomType.Noc, RoomType.Soc, RoomType.Lab, RoomType.Warehouse,
    };

    public static readonly RoomType[] OfficeRooms =
    {
        RoomType.Executive, RoomType.Office, RoomType.Meeting, RoomType.Canteen, RoomType.Lounge, RoomType.Reception, RoomType.Security,
    };

    public static readonly string[] Topics = { "redundancy", "power", "cooling", "network" };

    readonly VBoxContainer _page;
    readonly StableScroll _pageScroll;
    readonly List<(Button b, string id)> _items = new();
    string _open;

    /// <summary>Opens on a room (<paramref name="room"/>) or a topic ("redundancy", "power", ...); the data hall otherwise.</summary>
    public HelpPanel(RoomType? room = null, string topic = null) : base("help_title", 1040)
    {
        var split = new HBoxContainer();
        split.AddThemeConstantOverride("separation", 16);
        var listScroll = new StableScroll { CustomMinimumSize = new Vector2(250, 560) };
        var list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 2);
        listScroll.AddChild(list);
        split.AddChild(listScroll);
        split.AddChild(new VSeparator());

        void Group(string key)
        {
            if (list.GetChildCount() > 0) list.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) });
            list.AddChild(Ui.Heading(key, 12));
        }
        void Item(string id, string text, Color? swatch)
        {
            var b = new Button { Text = text, Alignment = HorizontalAlignment.Left, CustomMinimumSize = new Vector2(0, 32), AutoTranslateMode = AutoTranslateModeEnum.Disabled };
            if (swatch is { } c)
            {
                var img = Image.CreateEmpty(12, 12, false, Image.Format.Rgba8);
                img.Fill(c);
                b.Icon = ImageTexture.CreateFromImage(img);
            }
            b.Pressed += () => ShowPage(id);
            _items.Add((b, id));
            list.AddChild(b);
        }
        Group("help_group_topics");
        foreach (var t in Topics) Item("topic:" + t, Loc.T("help_topic_" + t), null);
        Group("help_group_dc");
        foreach (var r in DatacenterRooms) Item("room:" + r, Loc.T(Catalog.Rooms[r].NameKey), Color.FromHtml(Catalog.Rooms[r].Color));
        Group("help_group_office");
        foreach (var r in OfficeRooms) Item("room:" + r, Loc.T(Catalog.Rooms[r].NameKey), Color.FromHtml(Catalog.Rooms[r].Color));

        _pageScroll = new StableScroll { CustomMinimumSize = new Vector2(0, 560), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _page = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _page.AddThemeConstantOverride("separation", 10);
        var pad = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        pad.AddThemeConstantOverride("margin_right", 14);
        pad.AddChild(_page);
        _pageScroll.AddChild(pad);
        split.AddChild(_pageScroll);
        Body.AddChild(Plain(Loc.T("help_intro"), 13, Brand.InkDim));
        Body.AddChild(split);
        ShowPage(room is { } r0 ? "room:" + r0 : topic != null ? "topic:" + topic : "room:" + RoomType.DataHall);
    }

    /// <summary>The page now showing ("room:DataHall", "topic:redundancy").</summary>
    public string Open => _open;

    public void ShowPage(string id)
    {
        _open = id;
        foreach (var (b, i) in _items)
        {
            if (i == id)
            {
                var st = Ui.Box(Brand.PanelHover, 7, null, 0, 8);
                st.BorderColor = Brand.Amber; st.BorderWidthLeft = 3;
                b.AddThemeStyleboxOverride("normal", st); b.AddThemeColorOverride("font_color", Brand.Ink);
            }
            else { b.RemoveThemeStyleboxOverride("normal"); b.RemoveThemeColorOverride("font_color"); }
        }
        foreach (var c in _page.GetChildren()) { _page.RemoveChild(c); c.QueueFree(); }
        if (id.StartsWith("topic:")) TopicPage(id[6..]);
        else if (Enum.TryParse<RoomType>(id[5..], out var room)) RoomPage(room);
        _pageScroll.ScrollVertical = 0;
    }

    void TopicPage(string t)
    {
        _page.AddChild(Title(Loc.T("help_topic_" + t), Brand.Amber));
        foreach (var para in Loc.T("help_topic_" + t + "_body").Split("\n\n"))
            _page.AddChild(Plain(para, 15, Brand.Ink));
    }

    void RoomPage(RoomType r)
    {
        var def = Catalog.Rooms[r];
        _page.AddChild(Title(Loc.T(def.NameKey), Color.FromHtml(def.Color)));
        Section("help_what", Loc.T(Key(r, "what")));
        Section("help_when", Loc.T(Key(r, "when")));
        _page.AddChild(Ui.Heading("help_minimum", 12));
        foreach (var line in Minimum(r))
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            row.AddChild(Plain(line.advised ? "○" : "■", 13, line.advised ? Brand.InkDim : Brand.Amber, false));
            var l = Plain(line.text, 15, line.advised ? Brand.InkDim : Brand.Ink);
            l.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            row.AddChild(l);
            _page.AddChild(row);
        }
        var tip = new PanelContainer();
        tip.AddThemeStyleboxOverride("panel", Ui.Box(new Color(Brand.Amber, 0.08f), 8, new Color(Brand.Amber, 0.3f), 1, 12));
        tip.AddChild(Plain(Loc.T("help_tip") + ": " + Loc.T(Key(r, "tip")), 14, Brand.Ink));
        _page.AddChild(tip);
    }

    void Section(string headKey, string text)
    {
        _page.AddChild(Ui.Heading(headKey, 12));
        _page.AddChild(Plain(text, 15, Brand.Ink));
    }

    public static string Key(RoomType r, string part) => $"help_{r.ToString().ToLowerInvariant()}_{part}";

    /// <summary>One line on what a room is for (tooltips in the build tray and the room inspector).</summary>
    public static string Summary(RoomType r) => Loc.T(Key(r, "what"));

    /// <summary>What a room needs at least, from the game's own rules: a door, then the equipment per <see cref="GameState.RoomMinimum"/>.</summary>
    public static List<(string text, bool advised)> Minimum(RoomType r)
    {
        var list = new List<(string, bool)> { (Loc.T("help_door_any"), false) };
        if (r == RoomType.LoadingDock) list.Add((Loc.T("help_door_rollup"), false));
        if (GameState.RoomMinimum.TryGetValue(r, out var mins))
            foreach (var (key, _, min) in mins) list.Add(($"{min}× {Loc.T(key)}", false));
        else if (r != RoomType.LoadingDock) list.Add((Loc.T("help_nothing_inside"), false));
        if (r is RoomType.DataHall or RoomType.MeetMe) list.Add((Loc.T("help_badge_advised"), true));
        return list;
    }

    static Label Title(string text, Color c)
    {
        var l = new Label { Text = text, AutoTranslateMode = AutoTranslateModeEnum.Disabled };
        l.AddThemeFontOverride("font", Ui.DisplayBold);
        l.AddThemeFontSizeOverride("font_size", 26);
        l.AddThemeColorOverride("font_color", c);
        return l;
    }

    static Label Plain(string text, int size, Color color, bool wrap = true)
    {
        var l = Ui.Text(text, size, color);
        if (!wrap) l.AutowrapMode = TextServer.AutowrapMode.Off;
        l.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        return l;
    }
}
