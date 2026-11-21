using System;
using System.Collections.Generic;
using System.Linq;
using DatacenterArchitect.Core;
using DatacenterArchitect.Sim;
using Godot;

namespace DatacenterArchitect.UI;

/// <summary>Pictures and texts for hall blueprints (infra sketch board 8): the floor plan on a card, the summary line.</summary>
public static class BlueprintUi
{
    static readonly Dictionary<string, ImageTexture> _plans = new();

    /// <summary>A small floor plan of the blueprint: floor, walls, racks, and PDUs (amber), CRAHs (cyan), switches (orange).</summary>
    public static ImageTexture Plan(Blueprint bp, int width = 152, int height = 86)
    {
        var key = $"{bp.Id}|{bp.W}|{bp.D}|{bp.Objects.Count}|{bp.Walls.Count}|{width}x{height}";
        if (_plans.TryGetValue(key, out var cached)) return cached;
        var img = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        img.Fill(new Color(0, 0, 0, 0));
        var s = Math.Max(1, Math.Min((width - 6) / bp.W, (height - 6) / bp.D));
        int ox = (width - bp.W * s) / 2, oz = (height - bp.D * s) / 2;
        void Rect(int x, int z, int w, int d, Color c) => img.FillRect(new Rect2I(ox + x, oz + z, Math.Max(1, w), Math.Max(1, d)), c);
        var floor = new Color(Catalog.Rooms[bp.Type].Color).Lerp(new Color("#C9D0D6"), 0.55f);
        foreach (var (x, z) in bp.CellList()) Rect(x * s, z * s, s, s, floor);
        var line = new Color("#EAF2F8");
        if (bp.Walled)
        {
            Rect(0, 0, bp.W * s, 1, line); Rect(0, bp.D * s - 1, bp.W * s, 1, line);
            Rect(0, 0, 1, bp.D * s, line); Rect(bp.W * s - 1, 0, 1, bp.D * s, line);
        }
        foreach (var e in bp.Walls)
        {
            var c = e.Glass ? Brand.Cooling : line;
            if (e.Dir == 0) Rect(Math.Min(e.X * s, bp.W * s - 1), e.Z * s, 1, s, c); else Rect(e.X * s, Math.Min(e.Z * s, bp.D * s - 1), s, 1, c);
        }
        foreach (var o in bp.Objects)
        {
            var po = new PlacedObject { Kind = o.Kind, X = o.X, Z = o.Z, Rot = o.Rot };
            var (w, d) = GameState.Footprint(po);
            var c = ObjColor(o.Kind);
            var pad = s >= 4 ? 1 : 0;
            Rect(o.X * s + pad, o.Z * s + pad, w * s - pad, d * s - pad, c);
        }
        foreach (var d in bp.Doors)
        {
            var w = Catalog.Doors[d.Type].Width;
            if (d.Dir == 0) Rect(Math.Min(d.X * s, bp.W * s - 2) - 1, d.Z * s, 3, w * s, Brand.Ok); else Rect(d.X * s, Math.Min(d.Z * s, bp.D * s - 2) - 1, w * s, 3, Brand.Ok);
        }
        var tex = ImageTexture.CreateFromImage(img);
        if (_plans.Count > 200) _plans.Clear();
        _plans[key] = tex;
        return tex;
    }

    public static Color ObjColor(ObjKind k) => k switch
    {
        ObjKind.Rack => new Color("#23282F"),
        _ when Catalog.IsPdu(k) => Brand.Power,
        _ when Catalog.IsHallSwitch(k) => Brand.Fiber,
        _ when Catalog.Objects[k].RejectKw > 0 || k == ObjKind.Crah => Brand.Cooling,
        _ => new Color("#6B7785"),
    };

    /// <summary>"24 racks · 18 × 21 m" for the card.</summary>
    public static string Spec(Blueprint bp) => Loc.T("bp_spec", bp.Racks, bp.W, bp.D);

    /// <summary>What is in it: racks, PDUs, edge switches, CRAHs, containment and busbar.</summary>
    public static string Contents(Blueprint bp)
    {
        var parts = new List<string> { Loc.T("bp_racks", bp.Racks) };
        var pdus = bp.Count(Catalog.IsPdu);
        if (pdus > 0) parts.Add(Loc.T("bp_pdus", pdus));
        if (bp.Busbar) parts.Add(Loc.T("bp_busbar"));
        var sw = bp.Count(Catalog.IsHallSwitch);
        if (sw > 0) parts.Add(Loc.T("bp_switches", sw));
        var crah = bp.Count(ObjKind.Crah);
        if (crah > 0) parts.Add(Loc.T("bp_crahs", crah));
        if (bp.Containment != Containment.Open) parts.Add(Loc.T(Catalog.Containments[bp.Containment].NameKey));
        return string.Join(" · ", parts);
    }

    /// <summary>A name to start from: "Hall 24 racks · 50 kW" style.</summary>
    public static string DefaultName(GameState s, Room hall, Blueprint bp)
    {
        var code = s.HallCode(hall);
        return Loc.T("bp_default_name", code, bp.Racks);
    }

    /// <summary>Price of the blueprint on an empty floor (walls, doors, objects, fit-out), for the card.</summary>
    public static long Price(Blueprint bp)
    {
        long walls = bp.Walls.Sum(e => GameState.WallCost(1, e.Glass));
        return (bp.Walled ? Catalog.RoomCost(bp.W, bp.D) : 0) + walls + bp.Doors.Sum(d => Catalog.Doors[d.Type].Cost)
            + bp.Objects.Sum(o => Catalog.Objects[o.Kind].Cost) + GameState.FitOutCost(bp.Area, bp.Containment, bp.Busbar);
    }
}

/// <summary>Asks for a name and saves a hall as a blueprint.</summary>
public partial class BlueprintSaveDialog : Modal
{
    public BlueprintSaveDialog(Blueprint bp, Action<string> save) : base("bp_save_title", 560)
    {
        var top = new HBoxContainer(); top.AddThemeConstantOverride("separation", 14);
        var pic = new PanelContainer();
        pic.AddThemeStyleboxOverride("panel", Ui.Box(new Color(Brand.Sky, 0.12f), 8, Brand.Line, 1, 6));
        pic.AddChild(new TextureRect { Texture = BlueprintUi.Plan(bp, 200, 120), StretchMode = TextureRect.StretchModeEnum.KeepCentered, CustomMinimumSize = new Vector2(200, 120) });
        top.AddChild(pic);
        var info = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        info.AddThemeConstantOverride("separation", 4);
        var c = Ui.Text(BlueprintUi.Contents(bp), 14, Brand.Ink); c.AutowrapMode = TextServer.AutowrapMode.WordSmart; c.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        info.AddChild(c);
        var sz = Ui.Text(Loc.T("bp_size", bp.W, bp.D, bp.Area), 13, Brand.InkDim); sz.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        info.AddChild(sz);
        var price = Ui.Text(Loc.T("bp_price", Ui.Money(BlueprintUi.Price(bp))), 13, Brand.Ok); price.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        info.AddChild(price);
        top.AddChild(info);
        Body.AddChild(top);
        var note = Ui.Text("bp_save_note", 13, Brand.InkDim); note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        Body.AddChild(note);
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 8);
        var name = new LineEdit { Text = bp.Name, MaxLength = 40, SizeFlagsHorizontal = SizeFlags.ExpandFill, PlaceholderText = Loc.T("bp_name") };
        row.AddChild(name);
        void Go() { save?.Invoke(name.Text); Close(); }
        name.TextSubmitted += _ => Go();
        row.AddChild(Ui.Primary("bp_save", Go));
        Body.AddChild(row);
        name.CallDeferred(LineEdit.MethodName.GrabFocus);
        name.CallDeferred(LineEdit.MethodName.SelectAll);
    }
}

/// <summary>The blueprint library: every saved hall with its plan, to place, rename or delete.</summary>
public partial class BlueprintLibraryDialog : Modal
{
    readonly VBoxContainer _list;
    readonly Action<int> _place;
    readonly Action _changed;

    public BlueprintLibraryDialog(Action<int> place, Action changed) : base("bp_library_title", 720)
    {
        _place = place; _changed = changed;
        var note = Ui.Text("bp_library_note", 13, Brand.InkDim); note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        Body.AddChild(note);
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 420), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _list.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(_list);
        Body.AddChild(scroll);
        Fill();
    }

    void Fill()
    {
        foreach (var c in _list.GetChildren()) c.QueueFree();
        var lib = BlueprintLibrary.Instance;
        if (lib.All.Count == 0) { var e = Ui.Text("bp_none", 14, Brand.InkDim); e.AutowrapMode = TextServer.AutowrapMode.WordSmart; _list.AddChild(e); return; }
        for (var i = 0; i < lib.All.Count; i++)
        {
            var bp = lib.All[i];
            var idx = i;
            var p = new PanelContainer();
            p.AddThemeStyleboxOverride("panel", Ui.Box(new Color(1, 1, 1, 0.03f), 10, Brand.Line, 1, 10));
            var h = new HBoxContainer(); h.AddThemeConstantOverride("separation", 12);
            var pic = new PanelContainer();
            pic.AddThemeStyleboxOverride("panel", Ui.Box(new Color(Brand.Sky, 0.12f), 6, null, 0, 4));
            pic.AddChild(new TextureRect { Texture = BlueprintUi.Plan(bp, 128, 76), StretchMode = TextureRect.StretchModeEnum.KeepCentered, CustomMinimumSize = new Vector2(128, 76) });
            h.AddChild(pic);
            var info = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            info.AddThemeConstantOverride("separation", 2);
            var name = new LineEdit { Text = bp.Name, MaxLength = 40, TooltipText = "bp_rename_tip" };
            name.TextSubmitted += t => { lib.Rename(bp, t); _changed?.Invoke(); };
            name.FocusExited += () => { if (name.Text != bp.Name) { lib.Rename(bp, name.Text); _changed?.Invoke(); } };
            info.AddChild(name);
            var c = Ui.Text(BlueprintUi.Contents(bp), 13, Brand.Ink); c.AutowrapMode = TextServer.AutowrapMode.WordSmart; c.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
            info.AddChild(c);
            var sz = Ui.Text(Loc.T("bp_size", bp.W, bp.D, bp.Area) + " · " + Ui.Money(BlueprintUi.Price(bp)), 13, Brand.InkDim); sz.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
            info.AddChild(sz);
            h.AddChild(info);
            var btns = new VBoxContainer(); btns.AddThemeConstantOverride("separation", 6);
            btns.AddChild(Ui.Primary("bp_place", () => { Close(); _place?.Invoke(idx); }));
            var del = Ui.Secondary("bp_delete", null);
            var armed = false;
            // a second click deletes: no browser-style confirm dialog
            del.Pressed += () =>
            {
                if (!armed) { armed = true; del.Text = Loc.T("bp_delete_sure"); del.AddThemeColorOverride("font_color", Brand.Critical); return; }
                lib.Remove(bp); _changed?.Invoke(); Fill();
            };
            btns.AddChild(del);
            h.AddChild(btns);
            p.AddChild(h);
            _list.AddChild(p);
        }
    }
}
