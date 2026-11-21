using System;
using System.Linq;
using DatacenterArchitect.Core;
using DatacenterArchitect.Sim;
using DatacenterArchitect.UI;
using DatacenterArchitect.World;
using Godot;

namespace DatacenterArchitect;

/// <summary>
/// Hall blueprints (infra sketch board 8): save a hall from its inspector, pick a blueprint from the hall tray of the
/// build bar, turn it with R and mirror it with M, and click to put the whole hall on the building list at once.
/// </summary>
public partial class Main
{
    /// <summary>The blueprint in hand is mirrored (M).</summary>
    bool _bpMirror;

    /// <summary>The blueprint in hand, turned and mirrored as the player set it; null when the tool is not a blueprint.</summary>
    Blueprint HeldBlueprint()
    {
        if (_item.Mode != Mode.Blueprint) return null;
        return BlueprintLibrary.Instance.Get(_item.Arg)?.Oriented(_rot, _bpMirror);
    }

    /// <summary>The blueprint's corner when its centre is under the mouse.</summary>
    static Vector2I BlueprintCorner(Blueprint bp, Vector2I hover) => new(hover.X - bp.W / 2, hover.Y - bp.D / 2);

    void GhostBlueprint()
    {
        var bp = HeldBlueprint();
        if (bp == null) { _world.ClearGhost(); return; }
        var c = BlueprintCorner(bp, _hoverCell);
        var fit = _state.FitBlueprint(bp, c.X, c.Y, _world.ViewFloor);
        var text = $"{bp.Name} · {Loc.T("bp_spec", bp.Racks, bp.W, bp.D)}\n{Ui.Money(fit.Cost)} · {BlueprintUi.Contents(bp)}";
        if (fit.CappedContainment) text += "\n" + Loc.T("bp_capped_containment");
        if (fit.Why != null) text += "\n⚠ " + Loc.T(fit.Why);
        text += "\n" + Loc.T("bp_keys");
        _world.ShowGhostBlueprint(bp, c.X, c.Y, _world.ViewFloor, fit.Why == null, text, fit.NewWalls);
    }

    void PlaceHeldBlueprint()
    {
        var bp = HeldBlueprint();
        if (bp == null) return;
        var c = BlueprintCorner(bp, _hoverCell);
        _state.Planning = _planMode;
        var why = _state.FitBlueprint(bp, c.X, c.Y, _world.ViewFloor).Why;
        _state.Planning = false;
        if (why != null) { Fail(why); return; }
        var before = UndoHistory.Take(_state);
        PlanBegin(true);
        var res = _state.PlaceBlueprint(bp, c.X, c.Y, _world.ViewFloor);
        PlanEnd();
        Record(before, "bp_tool");
        if (res.Why != null) { Fail(res.Why); return; }
        var code = res.Room != null && res.Room.Type == RoomType.DataHall ? _state.HallCode(res.Room) : Loc.T(Catalog.Rooms[bp.Type].NameKey);
        _hud.Notify(Loc.T(_planMode ? "msg_bp_planned" : "msg_bp_placed", bp.Name, code, res.Objects, Ui.Money(res.Cost)), _planMode ? Brand.Cooling : Brand.Ok);
        if (res.Skipped > 0) _hud.Notify(Loc.T("msg_bp_skipped", res.Skipped), Brand.Amber);
        _hud.UpdateStats(_state);
    }

    /// <summary>Hall inspector: save this hall as a blueprint (asks for a name).</summary>
    void SaveHallBlueprint(Room hall)
    {
        var bp = _state.CaptureBlueprint(hall, "");
        if (bp == null) return;
        bp.Name = BlueprintLibrary.Instance.UniqueName(BlueprintUi.DefaultName(_state, hall, bp));
        _ui.AddChild(new BlueprintSaveDialog(bp, name =>
        {
            bp.Name = name;
            var why = BlueprintLibrary.Instance.Add(bp);
            if (why != null) { Fail(why); return; }
            _hud.Notify(Loc.T("msg_bp_saved", bp.Name), Brand.Cooling);
            _hud.ShowCategory("cat_halls");
            _hud.RefreshBlueprints();
        }));
    }

    void OpenBlueprintLibrary()
    {
        if (_ui.GetChildren().OfType<BlueprintLibraryDialog>().Any()) return;
        _ui.AddChild(new BlueprintLibraryDialog(i => _hud.Pick(new BuildItem(Mode.Blueprint, i)), BlueprintsChanged));
    }

    /// <summary>A blueprint was renamed or deleted: redraw the tray and put the blueprint in hand down (its place in the list may have moved).</summary>
    void BlueprintsChanged()
    {
        if (_item.Mode == Mode.Blueprint) _hud.Pick(BuildItem.Select);
        _hud.RefreshBlueprints();
        UpdateGhost();
    }

    // ---------------- screenshots ----------------

    /// <summary>
    /// --blueprint-demo [library|save|placed]: a long datacenter with two designed halls, both saved as blueprints;
    /// the first one in hand, mirrored, between them (or the library, the save dialog, or the copy placed and built).
    /// </summary>
    void BlueprintDemo(string mode)
    {
        BlueprintLibrary.UseInMemory();
        var s = GameState.New(new NewGameOptions { Company = "Polder Datacenters", Creative = true, CreativeOptions = new CreativeOptions { UnlimitedMoney = true, AllUnlocks = true, InstantBuild = true } });
        s.PaintSurface(28, 34, 3, 30, Surface.Asphalt, out _);
        s.PaintSurface(6, 35, 52, 3, Surface.Asphalt, out _);
        s.AddBuilding(BuildingKind.Datacenter, 6, 8, 52, 26, 1);
        s.AddRoom(RoomType.Corridor, 6, 28, 52, 6, 0);
        // hall 1: private 24, cold corridor, PDU 300 A+B, two edge 24s, four CRAHs
        var h1 = s.AddRoom(RoomType.DataHall, 8, 10, 14, 13, 0);
        foreach (var (dz, rot) in new[] { (2, 0), (4, 2), (7, 0), (9, 2) })
            for (var x = 4; x < 10; x++) s.AddObject(ObjKind.Rack, 8 + x, 10 + dz, rot, 0);
        foreach (var dz in new[] { 1, 4, 7, 10 }) s.AddObject(ObjKind.Crah, 9, 10 + dz, 0, 0);
        s.AddObject(ObjKind.Pdu300, 19, 13, 0, 0); s.AddObject(ObjKind.Pdu300, 19, 18, 0, 0);
        s.AddObject(ObjKind.EdgeSwitch24, 20, 13, 0, 0); s.AddObject(ObjKind.EdgeSwitch24, 20, 18, 0, 0);
        s.AddDoor(DoorType.BadgeDouble, 14, 23, 1, 0);
        h1.Containment = Containment.ColdCorridor;
        // hall 2: colocation 48 with hot/cold aisles, a busbar and two edge 48s
        var h2 = s.AddRoom(RoomType.DataHall, 40, 10, 16, 17, 0);
        foreach (var (dz, rot) in new[] { (2, 0), (4, 2), (7, 0), (9, 2), (12, 0), (14, 2) })
            for (var x = 4; x < 12; x++) s.AddObject(ObjKind.Rack, 40 + x, 10 + dz, rot, 0);
        foreach (var dz in new[] { 1, 5, 9, 13 }) s.AddObject(ObjKind.Crah, 41, 10 + dz, 0, 0);
        s.AddObject(ObjKind.EdgeSwitch48, 54, 13, 0, 0); s.AddObject(ObjKind.EdgeSwitch48, 54, 21, 0, 0);
        s.AddDoor(DoorType.BadgeDouble, 46, 27, 1, 0);
        h2.Containment = Containment.HotCold; h2.Busbar = true;
        s.Options.CreativeOptions.InstantBuild = false;
        s.Hours = (new DateTime(2027, 6, 21, 13, 0, 0) - GameState.Epoch).TotalHours;
        foreach (var o in s.Objects) o.Status = ObjStatus.Live;
        StartGame(s, false);
        var lib = BlueprintLibrary.Instance;
        var b1 = _state.CaptureBlueprint(h1, Loc.Language == "nl" ? "Privé 24 · koude gang" : "Private 24 · cold corridor");
        var b2 = _state.CaptureBlueprint(h2, Loc.Language == "nl" ? "Colocatie 48 · busbar" : "Colocation 48 · busbar");
        lib.Add(b2); lib.Add(b1);
        _speed = 0; _hud.SetSpeed(0);
        SetView(0, false);
        _cam.SetView(0.15f, 0.95f, 46, new Vector3(31, 0, 20));
        if (mode == "library") { OpenBlueprintLibrary(); return; }
        if (mode == "save")
        {
            Inspect(new Vector2I(10, 22));
            SaveHallBlueprint(_state.Rooms.First(r => r.Id == h1.Id));
            return;
        }
        _hud.Pick(new BuildItem(Mode.Blueprint, 0));
        _bpMirror = true;
        _hoverCell = new Vector2I(31, 16);
        if (mode == "placed")
        {
            _state.Options.CreativeOptions.InstantBuild = true;
            PlaceHeldBlueprint();
            foreach (var o in _state.Objects) o.Status = ObjStatus.Live;
            _state.Touch();
            _hud.Pick(BuildItem.Select);
            _world.Build(_state, _sim);
            _cam.SetView(0.5f, 0.85f, 34, new Vector3(31, 0, 18));
            return;
        }
        UpdateGhost();
    }
}
