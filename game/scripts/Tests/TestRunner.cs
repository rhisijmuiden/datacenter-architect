using System;
using System.Collections.Generic;
using System.Linq;
using DatacenterArchitect.Core;
using DatacenterArchitect.Sim;
using Godot;
using FileAccess = Godot.FileAccess;

namespace DatacenterArchitect.Tests;

/// <summary>Headless checks: godot --headless --path game -- --run-tests (exit code 0 = all passed).</summary>
public static class TestRunner
{
    static int _fail, _pass;

    static void Check(bool ok, string name)
    {
        if (ok) _pass++; else { _fail++; GD.PrintErr($"FAIL {name}"); }
    }

    public static int RunAll()
    {
        _fail = _pass = 0;
        var tests = new List<(string, Action)>
        {
            ("semver", SemVerTests), ("updates", UpdateTests), ("translations", TranslationTests),
            ("saves", SaveTests), ("building", BuildTests), ("construction", ConstructionTests),
            ("rack chain", RackChainTests), ("navigation", NavTests), ("creative", CreativeTests), ("demo", DemoTests),
        };
        foreach (var (name, t) in tests)
        {
            try { t(); }
            catch (Exception e) { _fail++; GD.PrintErr($"FAIL {name}: {e}"); }
        }
        GD.Print($"Tests: {_pass} passed, {_fail} failed");
        return _fail;
    }

    static SemVer V(string s) { SemVer.TryParse(s, out var v); return v; }

    static void SemVerTests()
    {
        Check(V("v1.2.3").ToString() == "1.2.3", "parse v-prefix");
        Check(V("0.2.0").CompareTo(V("0.1.9")) > 0, "minor beats patch");
        Check(V("1.0.0").CompareTo(V("1.0.0-test.4")) > 0, "release beats pre-release");
        Check(V("1.0.0-test.10").CompareTo(V("1.0.0-test.9")) > 0, "numeric pre-release order");
        Check(!SemVer.TryParse("banana", out _), "reject garbage");
    }

    const string Releases = """
    [
      {"tag_name":"v0.3.0-test.2","prerelease":true,"draft":false,"html_url":"u1","body":"",
       "assets":[{"name":"DatacenterArchitect-0.3.0-test.2-Setup.exe","browser_download_url":"d1","size":10}]},
      {"tag_name":"v0.2.0","prerelease":false,"draft":false,"html_url":"u2","body":"",
       "assets":[{"name":"DatacenterArchitect-0.2.0-Setup.exe","browser_download_url":"d2","size":10}]},
      {"tag_name":"v0.4.0","prerelease":false,"draft":true,"html_url":"u3","body":"","assets":[]}
    ]
    """;

    static void UpdateTests()
    {
        var list = UpdateService.ParseReleases(Releases);
        Check(list.Count == 2, "drafts skipped");
        var stable = UpdateService.Decide(list, "stable", V("0.1.0"), "stable");
        Check(stable.Verdict == UpdateVerdict.NewerAvailable && stable.Release.Tag == "v0.2.0", "stable picks newest release");
        var test = UpdateService.Decide(list, "test", V("0.2.0"), "stable");
        Check(test.Verdict == UpdateVerdict.NewerAvailable && test.Release.Tag == "v0.3.0-test.2", "test channel includes pre-releases");
        var same = UpdateService.Decide(list, "stable", V("0.2.0"), "stable");
        Check(same.Verdict == UpdateVerdict.UpToDate, "up to date");
        var back = UpdateService.Decide(list, "stable", V("0.3.0-test.2"), "test");
        Check(back.Verdict == UpdateVerdict.ChannelSwitch && back.Release.Tag == "v0.2.0", "switch from test back to stable");
    }

    static void TranslationTests()
    {
        Loc.Load();
        var en = Loc.Table["en"]; var nl = Loc.Table["nl"];
        var missing = en.Keys.Where(k => !nl.TryGetValue(k, out var t) || string.IsNullOrWhiteSpace(t)).ToList();
        Check(missing.Count == 0, "every key has Dutch: " + string.Join(", ", missing));
        Check(en.Values.All(v => !string.IsNullOrWhiteSpace(v)), "every key has English");
        // every key used in code must exist
        var used = new List<string> { "menu_new_game", "info_check", "info_update_now", "channel_stable", "channel_test", "channel_dev", "res_power", "new_ai" };
        used.AddRange(Catalog.Objects.Values.Select(d => d.NameKey));
        used.AddRange(Catalog.Surfaces.Values.Select(d => d.NameKey));
        used.AddRange(Catalog.Fences.Values.Select(d => d.NameKey));
        used.AddRange(Catalog.Buildings.Values.Select(d => d.NameKey));
        used.AddRange(Catalog.Rooms.Values.Select(d => d.NameKey));
        used.AddRange(Catalog.Doors.Values.Select(d => d.NameKey));
        used.AddRange(Enum.GetNames<Stage>().Select(n => "stage_" + n.ToLowerInvariant()));
        used.AddRange(Enum.GetNames<ObjStatus>().Select(n => "status_" + n.ToLowerInvariant()));
        used.AddRange(Enum.GetNames<Facade>().Select(n => "facade_" + n.ToLowerInvariant()));
        // every msg_/hint_/goal_ key mentioned in the scripts must exist
        foreach (var file in new[] { "res://scripts/Main.cs", "res://scripts/Sim/GameState.cs", "res://scripts/Sim/Simulation.cs", "res://scripts/UI/Hud.cs", "res://scripts/UI/Menus.cs" })
        {
            var text = FileAccess.GetFileAsString(file);
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(text, "\"((msg|hint|goal|cr|crew|hud|new|inspect|projects|prio|cat)_[a-z0-9_]+)\""))
                used.Add(m.Groups[1].Value);
        }
        var absent = used.Distinct().Where(k => !en.ContainsKey(k)).ToList();
        Check(absent.Count == 0, "keys used in code exist: " + string.Join(", ", absent));
        TranslationServer.SetLocale("nl");
        Check(Loc.T("menu_new_game") != "menu_new_game", "Dutch lookup works");
        TranslationServer.SetLocale("en");
    }

    static void SaveTests()
    {
        var s = GameState.New(new NewGameOptions { Company = "Test BV", AiCompetitors = false, Difficulty = 2, Creative = true, CreativeOptions = new CreativeOptions { NoCoolingLimit = true } });
        s.PaintSurface(1, 1, 3, 2, Surface.Asphalt, out _);
        s.PlaceFence(10, 10, 5, 1, FenceType.Palisade, out _);
        var b = s.AddBuilding(BuildingKind.Office, 20, 20, 8, 6, 2, Facade.Glass);
        b.Stage = Stage.Done;
        s.AddRoom(RoomType.Office, 20, 20, 4, 4, 1);
        s.AddDoor(DoorType.Double, 22, 26, 1, 0);
        s.Hours = 1234;
        SaveSystem.Save(s, "unit-test");
        var l = SaveSystem.Load("unit-test");
        Check(l != null && l.Options.Company == "Test BV" && !l.Options.AiCompetitors && l.Options.Creative && l.Creative.NoCoolingLimit, "save roundtrip options");
        Check(l != null && l.SurfaceAt(2, 2) == Surface.Asphalt && l.FenceAt(12, 10) == FenceType.Palisade && Math.Abs(l.Hours - 1234) < 1e-6, "save roundtrip site");
        Check(l != null && l.Buildings.Count == 1 && l.Buildings[0].Facade == Facade.Glass && l.Buildings[0].Floors == 2 && l.Rooms[0].Floor == 1 && l.Doors.Count == 1, "save roundtrip buildings");
        Check(SaveSystem.List().Any(x => x.Slot == "unit-test"), "save listed");
        for (var i = 0; i < 4; i++) SaveSystem.Autosave(s);
        Check(SaveSystem.List().Count(x => x.IsAutosave) == SaveSystem.AutosaveSlots, "autosave keeps three");
        foreach (var x in SaveSystem.List().Where(x => x.IsAutosave || x.Slot == "unit-test")) SaveSystem.Delete(x.Slot);
        Check(SaveSystem.Sanitize("../evil") == "___evil", "slot names sanitised");
    }

    static void BuildTests()
    {
        var s = GameState.New(new NewGameOptions());
        Check(s.CanPlace(new PlacedObject { Kind = ObjKind.Rack, X = 5, Z = 5 }) == "msg_needs_hall", "rack needs a data hall");
        Check(s.CanBuild(BuildingKind.Datacenter, 2, 2, 3, 8, 1) == "msg_too_small", "minimum building size");
        var b = s.AddBuilding(BuildingKind.Datacenter, 2, 2, 12, 10, 1);
        Check(b != null && b.Stage == Stage.Foundation && b.Facade == Facade.Concrete, "building starts at the foundation, concrete box");
        Check(s.CanBuild(BuildingKind.Office, 14, 2, 6, 6, 1) == "msg_building_gap", "buildings keep a gap");
        Check(s.CanRoom(RoomType.DataHall, 0, 0, 4, 4, 0) == "msg_room_in_building", "room must be inside a building");
        var hall = s.AddRoom(RoomType.DataHall, 2, 2, 8, 10, 0);
        Check(hall != null && s.CanRoom(RoomType.Office, 4, 4, 3, 3, 0) == "msg_overlap_room", "rooms do not overlap");
        Check(s.CanRoom(RoomType.Office, 10, 2, 4, 4, 1) == "msg_no_such_floor", "no rooms above the top floor");
        var rack = s.AddObject(ObjKind.Rack, 4, 4, 0, 0);
        Check(rack != null && rack.Status == ObjStatus.Ordered, "new racks are ordered, not live");
        Check(s.CanPlace(new PlacedObject { Kind = ObjKind.Rack, X = 4, Z = 4 }) == "msg_occupied", "no overlap");
        Check(s.CanPlace(new PlacedObject { Kind = ObjKind.Desk, X = 11, Z = 4 }) == "msg_needs_room", "desk needs a room");
        Check(s.CanDoor(new Door { Type = DoorType.Single, X = 5, Z = 5, Dir = 0 }) == "msg_door_on_wall", "door must sit on a wall");
        Check(s.AddDoor(DoorType.Double, 10, 4, 0, 0) != null, "double door on the hall wall");
        Check(s.AddDoor(DoorType.RollUp, 5, 12, 1, 0) != null, "roll-up door on the outer wall");
        Check(s.CanDoor(new Door { Type = DoorType.Single, X = 10, Z = 5, Dir = 0 }) == "msg_occupied", "doors do not overlap");
        var row = GameState.RowCells(3, 6, 40, 6, ObjKind.Rack, 0);
        Check(row.Count == Catalog.MaxRow && row[1] == (4, 6), "rows are flush and at most 24 long");
        s.Objects.Add(new PlacedObject { Kind = ObjKind.Generator, X = 30, Z = 30, Id = 90 });
        Check(s.BackupCapacityKw == 0, "one generator gives no N+1 capacity");
        s.Objects.Add(new PlacedObject { Kind = ObjKind.Generator, X = 30, Z = 34, Id = 91 });
        Check(Math.Abs(s.BackupCapacityKw - 1000) < 1e-6, "two generators give N+1 1000 kW");
        Check(GameState.Footprint(new PlacedObject { Kind = ObjKind.Generator, Rot = 1 }) == (2, 3), "rotation swaps footprint");
        var fence = GameState.FenceCells(0, 0, 4, 3).ToList();
        Check(fence.Count == 10 && fence.Distinct().Count() == 10, "fence drag makes an outline");
        Check(s.Erase(4, 4, 1, 1, 0) && !s.Objects.Any(o => o.Kind == ObjKind.Rack), "erase removes the rack");
    }

    /// <summary>A small site with a road, yard and a building; runs the simulation until it is finished.</summary>
    static (GameState s, Simulation sim) Site()
    {
        var s = GameState.New(new NewGameOptions { Difficulty = 0 });
        s.PaintSurface(30, 40, 3, 24, Surface.Asphalt, out _);
        s.PaintSurface(33, 40, 6, 6, Surface.Yard, out _);
        return (s, new Simulation(s));
    }

    static void Run(Simulation sim, double hours, Func<bool> until = null)
    {
        for (var t = 0.0; t < hours; t += 0.25)
        {
            sim.Tick(0.25);
            if (until != null && until()) return;
        }
    }

    static void ConstructionTests()
    {
        var (s, sim) = Site();
        Check(sim.Entries().Count == 3, "driveway reaches the road");
        var b = s.AddBuilding(BuildingKind.Datacenter, 10, 10, 10, 8, 1);
        var money = s.Money;
        Run(sim, 2);
        Check(sim.Agents.Count(a => a.Role == Role.Truck) > 0 || s.MaterialStock > 0, "a truck brings material");
        Check(sim.Agents.Count(a => a.Role == Role.Builder) == s.Builders, "builders on site");
        Run(sim, 400, () => b.Stage == Stage.Done);
        Check(b.Stage == Stage.Done, "builders finish the building");
        var room = s.AddRoom(RoomType.DataHall, 10, 10, 6, 8, 0);
        Run(sim, 100);
        Check(room.Stage == Stage.Walls, "a room in a closed building waits for a door");
        Check(sim.Messages.Any(m => m.key == "msg_site_unreachable"), "player is told the site cannot be reached");
        s.AddDoor(DoorType.Double, 12, 18, 1, 0);
        Run(sim, 200, () => room.Stage == Stage.Done);
        Check(room.Stage == Stage.Done, "builders finish the hall after a door is placed");
        // priority: a high priority project is worked on first
        var low = s.AddBuilding(BuildingKind.Warehouse, 40, 4, 6, 6, 1);
        var high = s.AddBuilding(BuildingKind.Warehouse, 50, 4, 6, 6, 1);
        high.Priority = 3;
        Check(sim.Sites().First().Id == high.Id, "sites are ordered by priority");
        Check(s.Money < money, "building costs money");
        _ = low;
    }

    static void RackChainTests()
    {
        var (s, sim) = Site();
        var b = s.AddBuilding(BuildingKind.Datacenter, 22, 26, 14, 10, 1);
        b.Stage = Stage.Done;
        var hall = s.AddRoom(RoomType.DataHall, 22, 26, 8, 10, 0);
        var dock = s.AddRoom(RoomType.LoadingDock, 30, 26, 6, 10, 0);
        hall.Stage = dock.Stage = Stage.Done;
        s.PaintSurface(30, 36, 3, 4, Surface.Asphalt, out _);
        s.AddDoor(DoorType.RollUp, 30, 36, 1, 0);
        s.AddDoor(DoorType.Single, 30, 30, 0, 0);
        var racks = Enumerable.Range(0, 4).Select(i => s.AddObject(ObjKind.Rack, 24 + i, 28, 0, 0)).ToList();
        Check(sim.Docks().Count == 3, "roll-up door makes a dock");
        Run(sim, 60, () => racks.All(r => r.Status == ObjStatus.Delivered));
        Check(racks.All(r => r.Status == ObjStatus.Delivered), "truck delivers racks to the dock");
        Run(sim, 20);
        Check(racks.All(r => r.Status == ObjStatus.Delivered), "racks do not fit through a single door");
        Check(sim.Messages.Any(m => m.key == "msg_hall_needs_wide_door"), "player is told a wide door is needed");
        s.Erase(30, 30, 1, 1, 0);
        s.AddDoor(DoorType.Double, 30, 30, 0, 0);
        Run(sim, 120, () => racks.All(r => r.Status == ObjStatus.Live));
        Check(racks.All(r => r.Status == ObjStatus.Live), "fitters place racks, engineer connects power and fiber");
        Check(s.LiveRacks == 4 && s.PowerDemandKw >= 24, "live racks draw power");
    }

    static void NavTests()
    {
        var s = GameState.New(new NewGameOptions());
        var b = s.AddBuilding(BuildingKind.Office, 10, 10, 8, 8, 2);
        b.Stage = Stage.Done;
        var nav = new Nav(s);
        var inside = nav.Find(new Pt(5, 5, 0), 0, (x, z) => x == 12 && z == 12, NavMode.Walk);
        Check(inside == null, "closed building cannot be entered");
        s.AddDoor(DoorType.Single, 12, 18, 1, 0);
        Check(nav.Find(new Pt(5, 5, 0), 0, (x, z) => x == 12 && z == 12, NavMode.Walk) != null, "door lets people in");
        Check(nav.Find(new Pt(5, 5, 0), 0, (x, z) => x == 12 && z == 12, NavMode.Carry) == null, "single door is too narrow for a rack");
        Check(nav.Find(new Pt(5, 5, 0), 1, (x, z) => x == 12 && z == 12, NavMode.Walk) == null, "upper floor needs a lift");
        s.AddObject(ObjKind.Lift, 14, 14, 0, 0);
        var up = nav.Find(new Pt(5, 5, 0), 1, (x, z) => x == 12 && z == 12, NavMode.Walk);
        Check(up != null && up[^1].F == 1, "lift reaches the upper floor");
        s.PlaceFence(0, 30, 20, 1, FenceType.Mesh, out _);
        Check(nav.Find(new Pt(5, 35, 0), 0, (x, z) => z == 25, NavMode.Walk) is { } around && around.Count > 10, "people walk around a fence");
        s.PlaceFence(20, 30, 44, 1, FenceType.Mesh, out _);
        Check(nav.Find(new Pt(5, 35, 0), 0, (x, z) => z == 25, NavMode.Walk) == null, "a closed fence blocks");
        s.AddObject(ObjKind.PedGate, 6, 30, 0, 0);
        Check(nav.Find(new Pt(5, 35, 0), 0, (x, z) => z == 25, NavMode.Walk) != null, "a gate lets people through");
    }

    static void CreativeTests()
    {
        var s = GameState.New(new NewGameOptions { Creative = true, CreativeOptions = new CreativeOptions { UnlimitedMoney = true, InstantBuild = true, AllUnlocks = true } });
        var money = s.Money;
        var b = s.AddBuilding(BuildingKind.Datacenter, 5, 5, 20, 20, 4);
        Check(b != null && b.Stage == Stage.Done, "instant build finishes buildings");
        Check(s.Money == money, "unlimited money costs nothing");
        Check(s.PlaceFence(0, 40, 10, 1, FenceType.Electric, out _) == null, "all unlocks allows the electric fence");
        var hall = s.AddRoom(RoomType.DataHall, 5, 5, 10, 10, 0);
        var r = s.AddObject(ObjKind.Rack, 7, 7, 0, 0);
        Check(hall.Stage == Stage.Done && r.Status == ObjStatus.Live, "instant build skips the delivery chain");
        var career = GameState.New(new NewGameOptions());
        Check(career.PlaceFence(0, 40, 10, 1, FenceType.Electric, out _) == "msg_needs_research", "career mode locks the electric fence");
    }

    static void DemoTests()
    {
        var s = Demo.Build();
        var sim = new Simulation(s);
        Check(s.Buildings.Count == 3 && s.Rooms.Count >= 7 && s.Racks > 90, "demo campus builds");
        Check(sim.Entries().Count > 0 && sim.Docks().Count > 0, "demo has road access and a dock");
        for (var h = 0; h < 40; h++)
        {
            Run(sim, 1);
            if (OS.HasEnvironment("DEMO_TRACE"))
                GD.Print($"h{h} trucks={string.Join(",", sim.Agents.Where(a => a.Role == Role.Truck).Select(a => $"{a.Load}:{a.Phase}@{a.X:0},{a.Z:0}"))} " +
                    $"status={string.Join(",", s.Objects.Where(o => o.Kind == ObjKind.Rack).GroupBy(o => o.Status).Select(g => g.Key + "=" + g.Count()))} fitters={string.Join(",", sim.Agents.Where(a => a.Role == Role.Fitter).Select(a => a.Phase))} msgs={string.Join(",", sim.Messages.Select(m => m.key))}");
        }
        Check(s.Objects.Count(o => o.Kind == ObjKind.Rack && o.Status == ObjStatus.Live) > 91, "demo racks get delivered and connected");
        var json = System.Text.Json.JsonSerializer.Serialize(s);
        Check(json.Length < 200_000, "savegame stays small: " + json.Length);
    }
}
