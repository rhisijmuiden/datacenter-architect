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
            ("power chain", PowerTests), ("backup power", BackupTests), ("grid and suppliers", GridTests),
            ("cooling", CoolingTests), ("roof and underground", RoofTests), ("overlay rows", OverlayTests),
            ("options", OptionsTests),
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
        used.AddRange(Enum.GetNames<PowerLink>().Select(n => "link_" + n.ToLowerInvariant()));
        used.AddRange(Catalog.GridLevels.Select(g => g.NameKey));
        used.AddRange(Catalog.Suppliers.Select(g => g.DescKey));
        used.AddRange(Catalog.Containments.Values.Select(g => g.NameKey));
        used.AddRange(Catalog.Objects.Values.Where(d => d.Rooms != null).Select(GameState.NeedsRoomKey));
        // every msg_/hint_/goal_ key mentioned in the scripts must exist
        foreach (var file in new[] { "res://scripts/Main.cs", "res://scripts/Sim/GameState.cs", "res://scripts/Sim/Simulation.cs", "res://scripts/UI/Hud.cs", "res://scripts/UI/Menus.cs", "res://scripts/UI/PowerPanel.cs", "res://scripts/World/WorldView.cs", "res://scripts/UI/OptionsDialog.cs" })
        {
            var text = FileAccess.GetFileAsString(file);
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(text, "\"((msg|hint|goal|cr|crew|hud|new|inspect|projects|prio|cat|power|grid|supplier|backup|chain|link|cool|halls?|advice|action|res|opt|menu|tool)_[a-z0-9_]+)\""))
                used.Add(m.Groups[1].Value);
        }
        var absent = used.Distinct().Where(k => !en.ContainsKey(k)).ToList();
        Check(absent.Count == 0, "keys used in code exist: " + string.Join(", ", absent));
        TranslationServer.SetLocale("nl");
        Check(Loc.T("menu_new_game") != "menu_new_game", "Dutch lookup works");
        TranslationServer.SetLocale("en");
    }

    static void OptionsTests()
    {
        // settings survive a save and load
        const string path = "user://test-settings.cfg";
        var a = new AppSettings
        {
            WindowMode = WindowModeSetting.Windowed, Resolution = new Vector2I(2560, 1080), Monitor = 1, VSync = false, MaxFps = 144, UiScale = 1.25f,
            Shadows = false, AmbientOcclusion = false, Fog = false, Msaa = 4, BlinkingLights = false, MovingParts = false, SmoothCamera = false,
            MasterVolume = 55, AmbienceVolume = 10, InterfaceVolume = 0, AmbienceSound = false, ClickSounds = false, NotificationSounds = false, MuteInBackground = false,
            Language = "nl", AutosaveMinutes = 15,
        };
        a.Save(path);
        var b = AppSettings.Load(path);
        Check(b.WindowMode == WindowModeSetting.Windowed && b.Resolution == new Vector2I(2560, 1080) && b.Monitor == 1, "display settings round-trip");
        Check(!b.VSync && b.MaxFps == 144 && Mathf.IsEqualApprox(b.UiScale, 1.25f), "performance settings round-trip");
        Check(!b.Shadows && !b.AmbientOcclusion && !b.Fog && b.Msaa == 4 && !b.BlinkingLights && !b.MovingParts && !b.SmoothCamera, "effect switches round-trip");
        Check(b.MasterVolume == 55 && b.AmbienceVolume == 10 && b.InterfaceVolume == 0 && !b.AmbienceSound && !b.ClickSounds && !b.NotificationSounds && !b.MuteInBackground, "audio round-trip");
        Check(b.Language == "nl" && b.AutosaveMinutes == 15, "general settings round-trip");

        // broken values fall back to something safe
        var cfg = new ConfigFile();
        cfg.SetValue("display", "window_mode", 9); cfg.SetValue("display", "ui_scale", 7.0f); cfg.SetValue("graphics", "msaa", 3);
        cfg.SetValue("audio", "master", 400); cfg.SetValue("display", "vsync", "banana");
        cfg.Save(path);
        var c = AppSettings.Load(path);
        Check((int)c.WindowMode <= 2 && c.UiScale <= 1.5f && c.Msaa == 2 && c.MasterVolume == 100 && c.VSync, "invalid settings are clamped");
        DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));

        // reset keeps language and update channel
        b.UpdateChannel = "test";
        b.ResetOptions();
        Check(b.Language == "nl" && b.UpdateChannel == "test" && b.Shadows && b.MasterVolume == new AppSettings().MasterVolume && b.WindowMode == WindowModeSetting.Borderless, "reset to defaults");

        // resolutions for 4:3, 16:9, ultrawide and 4K monitors
        var uw = DisplaySettings.ResolutionsFor(new Vector2I(3440, 1440));
        Check(uw[0] == new Vector2I(3440, 1440) && uw.Contains(new Vector2I(2560, 1080)) && uw.Contains(new Vector2I(1920, 1080)) && !uw.Contains(new Vector2I(3840, 2160)), "ultrawide resolution list");
        Check(uw.All(r => r.X <= 3440 && r.Y <= 1440), "only resolutions that fit");
        var k4 = DisplaySettings.ResolutionsFor(new Vector2I(3840, 2160));
        Check(k4[0] == new Vector2I(3840, 2160) && k4.Contains(new Vector2I(2560, 1440)) && k4.Contains(new Vector2I(1600, 1200)), "4K resolution list");
        var old = DisplaySettings.ResolutionsFor(new Vector2I(1024, 768));
        Check(old.Count == 1 && old[0] == new Vector2I(1024, 768), "4:3 1024x768 monitor");
        var odd = DisplaySettings.ResolutionsFor(new Vector2I(1700, 1000));
        Check(odd[0] == new Vector2I(1700, 1000), "unusual monitor size is offered as native");
        Check(DisplaySettings.AspectName(new Vector2I(1600, 1200)) == "4:3" && DisplaySettings.AspectName(new Vector2I(3840, 2160)) == "16:9"
            && DisplaySettings.AspectName(new Vector2I(3440, 1440)) == "21:9" && DisplaySettings.AspectName(new Vector2I(2560, 1080)) == "21:9"
            && DisplaySettings.AspectName(new Vector2I(1920, 1200)) == "16:10" && DisplaySettings.AspectName(new Vector2I(5120, 1440)) == "32:9", "aspect names");
        Check(Mathf.IsEqualApprox(DisplaySettings.RenderScaleFor(new Vector2I(1920, 1080), new Vector2I(3840, 2160)), 0.5f), "1080p on a 4K screen renders 3D at half size");
        Check(Mathf.IsEqualApprox(DisplaySettings.RenderScaleFor(Vector2I.Zero, new Vector2I(3840, 2160)), 1f), "native renders at full size");
        Check(DisplaySettings.RenderScaleFor(new Vector2I(1600, 1200), new Vector2I(3440, 1440)) <= 1f, "render scale never above 1");

        // volume curve
        Check(AudioMixer.ToDb(0) <= -79 && Mathf.IsEqualApprox(AudioMixer.ToDb(100), 0f) && AudioMixer.ToDb(50) < AudioMixer.ToDb(80), "volume curve");
        AudioMixer.Apply(new AppSettings { MasterVolume = 0, AmbienceSound = false });
        Check(AudioServer.GetBusIndex(AudioMixer.Ambience) > 0 && AudioServer.GetBusIndex(AudioMixer.Interface) > 0, "audio buses exist");
        Check(AudioServer.IsBusMute(0) && AudioServer.IsBusMute(AudioServer.GetBusIndex(AudioMixer.Ambience)), "zero volume and switched-off sound mute their bus");
        AudioMixer.Apply(new AppSettings());
        Check(!AudioServer.IsBusMute(0), "default volume is audible");

        // quality presets
        var p = new AppSettings();
        UI.OptionsDialog.ApplyPreset(p, 0);
        Check(!p.Shadows && !p.AmbientOcclusion && p.Msaa == 0, "performance preset");
        UI.OptionsDialog.ApplyPreset(p, 2);
        Check(p.Shadows && p.AmbientOcclusion && p.Fog && p.Msaa == 4, "quality preset");

        // the generated interface sounds are real audio
        var tone = World.UiSounds.Tone(new[] { 880f, 1320f }, 0.3f, 0.2f);
        Check(tone.Data.Length > 1000 && tone.Data.Any(x => x != 0), "interface sounds generated");
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
        s.GridLevel = 1; s.GridRequest = 2; s.GridDaysLeft = 12.5; s.Supplier = 2;
        s.Rooms[0].Containment = Containment.ColdCorridor; s.Rooms[0].Busbar = true;
        SaveSystem.Save(s, "unit-test");
        var l = SaveSystem.Load("unit-test");
        Check(l != null && l.Options.Company == "Test BV" && !l.Options.AiCompetitors && l.Options.Creative && l.Creative.NoCoolingLimit, "save roundtrip options");
        Check(l != null && l.SurfaceAt(2, 2) == Surface.Asphalt && l.FenceAt(12, 10) == FenceType.Palisade && Math.Abs(l.Hours - 1234) < 1e-6, "save roundtrip site");
        Check(l != null && l.Buildings.Count == 1 && l.Buildings[0].Facade == Facade.Glass && l.Buildings[0].Floors == 2 && l.Rooms[0].Floor == 1 && l.Doors.Count == 1, "save roundtrip buildings");
        Check(l != null && l.GridLevel == 1 && l.GridRequest == 2 && Math.Abs(l.GridDaysLeft - 12.5) < 1e-6 && l.Supplier == 2, "save roundtrip grid and supplier");
        Check(l != null && l.Rooms[0].Containment == Containment.ColdCorridor && l.Rooms[0].Busbar && l.SaveFormat == GameState.CurrentFormat, "save roundtrip hall upgrades");
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
        var inf = Infra.Analyze(s);
        Check(inf.RunningRacks == inf.LiveRacks && inf.LiveRacks > 91, $"demo power chain runs every rack ({inf.RunningRacks}/{inf.LiveRacks}, bottleneck {inf.Bottleneck?.Link})");
        Check(inf.Ats && inf.BackupKw >= 3000 && inf.FuelHours > 24 && inf.UpsMinutes > 5, "demo has generators, fuel and UPS autonomy");
        Check(inf.CoolRatio <= 1 && Math.Abs(inf.Pue - 1.5) < 1e-6, "demo cooling copes and PUE is 1.50");
        var json = System.Text.Json.JsonSerializer.Serialize(s);
        Check(json.Length < 200_000, "savegame stays small: " + json.Length);
    }

    /// <summary>A finished datacenter with a hall, a switchgear room and a UPS room, all built (creative instant build off, money on).</summary>
    static (GameState s, Room hall, Room swr, Room upsr) PowerSite(CreativeOptions c = null)
    {
        var s = GameState.New(new NewGameOptions { Difficulty = 0, Creative = c != null, CreativeOptions = c ?? new CreativeOptions() });
        var b = s.AddBuilding(BuildingKind.Datacenter, 4, 4, 30, 20, 1);
        b.Stage = Stage.Done;
        var hall = s.AddRoom(RoomType.DataHall, 4, 4, 20, 20, 0);
        var swr = s.AddRoom(RoomType.Switchgear, 24, 4, 5, 5, 0);
        var upsr = s.AddRoom(RoomType.UpsRoom, 29, 4, 5, 5, 0);
        foreach (var r in s.Rooms) r.Stage = Stage.Done;
        return (s, hall, swr, upsr);
    }

    static PlacedObject Live(GameState s, ObjKind k, int x, int z, int floor = 0)
    {
        var o = s.AddObject(k, x, z, 0, floor);
        if (o != null) { o.Status = ObjStatus.Live; s.Touch(false); }
        return o;
    }

    static void PowerTests()
    {
        var (s, hall, _, _) = PowerSite();
        Check(s.CanPlace(new PlacedObject { Kind = ObjKind.Ups, X = 6, Z = 6 }) == "msg_needs_upsroom", "UPS needs a UPS room");
        Check(s.CanPlace(new PlacedObject { Kind = ObjKind.Switchgear, X = 30, Z = 5 }) == "msg_needs_switchgear", "switchgear needs a switchgear room");
        Check(s.CanPlace(new PlacedObject { Kind = ObjKind.Battery, X = 30, Z = 7 }) == null, "batteries may stand in the UPS room");
        Check(s.CanPlace(new PlacedObject { Kind = ObjKind.Pdu, X = 30, Z = 7 }) == "msg_needs_hall", "PDU goes in the hall");
        var racks = Enumerable.Range(0, 20).Select(i => Live(s, ObjKind.Rack, 6 + i % 10, 6 + i / 10 * 3)).ToList();
        var r = Infra.Analyze(s);
        Check(r.LiveRacks == 20 && r.RunningRacks == 0, "without a power chain no rack runs");
        Check(r.Bottleneck.Link == PowerLink.Switchgear, "missing switchgear is the bottleneck");
        Live(s, ObjKind.Switchgear, 24, 4);
        Check(Infra.Analyze(s).RunningRacks == 0 && Infra.Analyze(s).Bottleneck.Link == PowerLink.Ups, "next the UPS is missing");
        Live(s, ObjKind.Ups, 29, 4);
        Check(Infra.Analyze(s).Bottleneck.Link == PowerLink.Distribution && Infra.Analyze(s).RunningRacks == 0, "then the hall needs a PDU");
        var pdu = Live(s, ObjKind.Pdu, 22, 20);
        r = Infra.Analyze(s);
        Check(r.RunningRacks == 20 && r.ItRunningKw == 120, "full chain: all 20 racks run (120 kW)");
        Check(!r.Halls[0].AB && r.Link(PowerLink.Ups).Redundancy == "N", "one PDU and one UPS: no A/B, N");
        Live(s, ObjKind.Pdu, 22, 18);
        Live(s, ObjKind.Ups, 31, 4);
        r = Infra.Analyze(s);
        Check(r.Halls[0].AB && r.Link(PowerLink.Ups).Redundancy == "2N", "two PDUs and two UPS units: A/B and 2N");
        // overload: 40 racks = 240 kW on 2 × 150 kW PDUs is fine, 60 racks = 360 kW is not
        for (var i = 0; i < 40; i++) Live(s, ObjKind.Rack, 6 + i % 10, 12 + i / 10 * 2);
        r = Infra.Analyze(s);
        Check(r.LiveRacks == 60 && r.RunningRacks == 50 && r.DownForPower == 10, $"PDUs limit the hall to 300 kW ({r.RunningRacks})");
        Check(r.Down.Contains(s.Objects.Where(o => o.Kind == ObjKind.Rack).Max(o => o.Id)) && !r.Down.Contains(racks[0].Id), "the newest racks go off first");
        hall.Busbar = true; s.Touch();
        r = Infra.Analyze(s);
        Check(r.RunningRacks == 60 && r.Halls[0].DistributionKw == 1100, "a busbar adds an A and a B run");
        Check(s.UpgradeHall(hall, true) == "msg_max_level", "busbar only once");
        var creative = PowerSite(new CreativeOptions { NoPowerLimit = true });
        Live(creative.s, ObjKind.Rack, 6, 6);
        Check(Infra.Analyze(creative.s).RunningRacks == 1, "creative: power is no limit");
        _ = pdu;
    }

    static void BackupTests()
    {
        var (s, _, _, _) = PowerSite();
        Live(s, ObjKind.Switchgear, 24, 4); Live(s, ObjKind.Ups, 29, 4); Live(s, ObjKind.Pdu, 22, 20); Live(s, ObjKind.Pdu, 22, 18);
        for (var i = 0; i < 40; i++) Live(s, ObjKind.Rack, 6 + i % 10, 6 + i / 10 * 3);
        var r = Infra.Analyze(s);
        Check(r.UpsMinutes == 0 && PowerPanelAdvice(s).Contains("advice_battery"), "UPS without batteries gives no autonomy");
        Live(s, ObjKind.Battery, 31, 6); Live(s, ObjKind.Battery, 32, 6);
        r = Infra.Analyze(s);
        Check(Math.Abs(r.UpsMinutes - 80.0 / 240 * 60) < 1e-6, "UPS autonomy: 80 kWh on 240 kW = 20 minutes");
        s.Objects.Add(new PlacedObject { Kind = ObjKind.Generator, X = 2, Z = 40, Id = s.NextId++ });
        r = Infra.Analyze(s);
        Check(r.BackupKw == 0 && r.GenRedundancy == "N", "one generator: no N+1 backup");
        s.Objects.Add(new PlacedObject { Kind = ObjKind.Generator, X = 6, Z = 40, Id = s.NextId++ });
        r = Infra.Analyze(s);
        Check(r.BackupKw == 1000 && r.GenRedundancy == "2N" && r.Ats, "two 1 MW generators on 396 kW: 2N behind the ATS");
        var fuel = r.FuelHours;
        Check(Math.Abs(r.FuelLitres - 4000) < 1e-6 && Math.Abs(fuel - 4000 / (r.ItDemandKw * 1.65 * Catalog.FuelPerKwh)) < 1e-6, "belly tanks: 4000 L of diesel");
        Check(s.AddObject(ObjKind.UndergroundTank, 10, 40, 0, 0) != null, "underground tank outside");
        s.Touch();
        Check(Infra.Analyze(s).FuelHours > fuel * 10, "an underground tank adds days of fuel");
        Check(s.CanPlace(new PlacedObject { Kind = ObjKind.UndergroundTank, X = 18, Z = 21 }) == "msg_outside_only", "tanks cannot go under a building");
        Check(!s.Objects.First(o => o.Kind == ObjKind.UndergroundTank).Def.Blocks, "underground tank does not block the way");
    }

    static List<string> PowerPanelAdvice(GameState s) => DatacenterArchitect.UI.PowerPanel.Advice(s, Infra.Analyze(s));

    static void GridTests()
    {
        var s = GameState.New(new NewGameOptions { Difficulty = 0 });
        Check(s.GridCapacityKw == 2000, "every site starts with 2 MVA");
        var money = s.Money;
        Check(s.RequestGrid(1) == null && s.GridRequest == 1 && s.GridDaysLeft == 30, "request 10 MVA: 30 days lead time");
        Check(s.Money == money - Catalog.GridLevels[1].Cost, "the upgrade is paid up front");
        Check(s.RequestGrid(2) == "msg_grid_busy", "one request at a time");
        s.Advance(24 * 29);
        Check(s.GridLevel == 0, "not ready before the lead time");
        s.Advance(24 * 2);
        Check(s.GridLevel == 1 && s.GridRequest == -1 && s.GridCapacityKw == 10000, "after 30 days the site has 10 MVA");
        Check(s.Notes.Any(n => n.key == "msg_grid_ready"), "player is told the grid is ready");
        Check(s.RequestGrid(1) == "msg_nothing_changed", "cannot request the same level");
        var instant = GameState.New(new NewGameOptions { Creative = true, CreativeOptions = new CreativeOptions { InstantBuild = true, UnlimitedMoney = true } });
        Check(instant.RequestGrid(3) == null && instant.GridLevel == 3, "creative instant build: 50 MVA right away");
        s.SetSupplier(0);
        Check(Math.Abs(s.EnergyPrice() - 0.18) < 1e-9, "fixed price supplier");
        s.SetSupplier(1);
        var p1 = s.EnergyPrice(); s.Hours += 24 * 14; var p2 = s.EnergyPrice();
        Check(Math.Abs(p1 - p2) > 1e-4 && p1 > 0.1 && p2 < 0.22, "variable price moves with the market");
        // the grid side carries the IT load times the PUE
        var (ps, _, _, _) = PowerSite();
        ps.GridLevel = 0;
        Live(ps, ObjKind.Switchgear, 24, 4); Live(ps, ObjKind.Ups, 29, 4); Live(ps, ObjKind.Ups, 31, 4); Live(ps, ObjKind.Ups, 29, 6); Live(ps, ObjKind.Ups, 31, 6);
        ps.Rooms[0].Busbar = true;
        foreach (var x in new[] { 18, 20, 22 }) { Live(ps, ObjKind.Pdu, x, 20); Live(ps, ObjKind.Pdu, x, 18); }
        for (var i = 0; i < 220; i++) Live(ps, ObjKind.Rack, 5 + i % 18, 5 + i / 18);
        var r = Infra.Analyze(ps);
        Check(r.Bottleneck.Link is PowerLink.Grid or PowerLink.Transformers && r.RunningRacks == (int)(2000 / 1.65 / 6), $"2 MVA at PUE 1.65 runs {r.RunningRacks} racks");
    }

    static void CoolingTests()
    {
        var (s, hall, _, _) = PowerSite(new CreativeOptions { NoPowerLimit = true });
        for (var i = 0; i < 40; i++) Live(s, ObjKind.Rack, 6 + i % 10, 6 + i / 10 * 3);
        Live(s, ObjKind.Crah, 4, 6);
        var r = Infra.Analyze(s);
        Check(r.HeatKw == 240 && r.CrahKw == 120 && r.RejectKw == 0 && r.CoolingKw == 0, "CRAH without heat rejection outside cools nothing");
        Check(s.AddObject(ObjKind.Chiller, 40, 30, 0, 0) != null, "chiller outside");
        Live(s, ObjKind.Crah, 4, 9);
        r = Infra.Analyze(s);
        Check(r.CoolingKw == 240 && r.CoolRatio <= 1, "two CRAH units and a chiller cool 240 kW");
        Check(r.Pue == 1.65, "open hall: PUE 1.65");
        for (var i = 0; i < 20; i++) Live(s, ObjKind.Rack, 6 + i % 10, 18 + i / 10);
        r = Infra.Analyze(s);
        Check(r.CoolRatio > 1, "more racks than cooling");
        for (var i = 0; i < 40; i++) s.Advance(0.25);
        Check(hall.Hot && hall.Temp >= Infra.ShutdownTemp && s.Notes.Any(n => n.key == "msg_hall_hot"), "the hall overheats");
        r = Infra.Analyze(s);
        Check(r.DownForHeat > 0 && r.RunningRacks * Catalog.RackKw <= r.Halls[0].CoolKw + 1e-6, "racks shut down until the rest fits the cooling");
        // upgrade the hall: hot/cold aisles give +12%, a cold corridor +35% and a lower PUE
        Check(s.UpgradeHall(hall, false) == null && hall.Upgrade == 1, "hall upgrade starts");
        s.Advance(25);
        Check(hall.Containment == Containment.HotCold && hall.Upgrade == 0, "hot/cold aisles after a day");
        r = Infra.Analyze(s);
        Check(Math.Abs(r.Halls[0].CrahKw - 240 * 1.12) < 1e-6 && Math.Abs(r.Pue - 1.5) < 1e-9, "hot/cold aisles: +12% CRAH, PUE 1.50");
        var crah = s.Objects.First(o => o.Kind == ObjKind.Crah);
        Check(s.UpgradeObject(crah) == null && crah.Level == 2, "CRAH upgrade to level 2");
        Check(s.UpgradeObject(crah) == null && s.UpgradeObject(crah) == "msg_max_level", "CRAH has three levels");
        s.UpgradeHall(hall, false); s.Advance(73);
        r = Infra.Analyze(s);
        Check(hall.Containment == Containment.ColdCorridor && Math.Abs(r.Pue - 1.3) < 1e-9, "cold corridor: PUE 1.30");
        Live(s, ObjKind.Crah, 4, 12);
        s.AddObject(ObjKind.CoolingTower, 44, 30, 0, 0);
        for (var i = 0; i < 20; i++) s.Advance(0.5);
        r = Infra.Analyze(s);
        Check(!hall.Hot && r.DownForHeat == 0 && r.RunningRacks == 60, "with enough cooling the racks come back");
        var creative = PowerSite(new CreativeOptions { NoPowerLimit = true, NoCoolingLimit = true });
        Live(creative.s, ObjKind.Rack, 6, 6);
        for (var i = 0; i < 20; i++) creative.s.Advance(0.5);
        Check(!creative.hall.Hot && Infra.Analyze(creative.s).RunningRacks == 1, "creative: cooling is no limit");
    }

    static void RoofTests()
    {
        var (s, _, _, _) = PowerSite();
        Check(s.PlaceFloor(ObjKind.Chiller, 10, 10, 0) == GameState.RoofFloor && s.PlaceFloor(ObjKind.Chiller, 50, 50, 0) == 0, "chillers go on the roof over a building");
        var ch = s.AddObject(ObjKind.Chiller, 10, 10, 0, GameState.RoofFloor);
        Check(ch != null && s.ObjectAt(11, 11, GameState.RoofFloor) == ch && s.ObjectAt(11, 11, 0) == null, "roof units sit on the roof, not in the hall");
        Check(s.CanPlace(new PlacedObject { Kind = ObjKind.Chiller, X = 32, Z = 10, Floor = GameState.RoofFloor }) == "msg_one_roof", "roof units fit on one roof");
        Check(s.CanPlace(new PlacedObject { Kind = ObjKind.Generator, X = 15, Z = 15, Floor = GameState.RoofFloor }) == "msg_not_on_roof", "generators do not go on a roof");
        Check(s.CanPlace(new PlacedObject { Kind = ObjKind.Chiller, X = 11, Z = 11, Floor = GameState.RoofFloor }) == "msg_occupied", "no overlap on the roof");
        var unfinished = s.AddBuilding(BuildingKind.Datacenter, 40, 40, 10, 10, 1);
        Check(s.CanPlace(new PlacedObject { Kind = ObjKind.CoolingTower, X = 42, Z = 42, Floor = GameState.RoofFloor }) == "msg_roof_needs_building", "roofs of unfinished buildings are off limits");
        var nav = new Nav(s);
        Check(nav.Walkable(11, 11, 0), "roof units do not block the hall below");
        Check(!s.Erase(10, 10, 2, 2, 0) && s.ObjectAt(11, 11, GameState.RoofFloor) != null, "erase on the ground leaves the roof alone");
        Check(s.Erase(10, 10, 2, 2, 0, true) && s.ObjectAt(11, 11, GameState.RoofFloor) == null, "erase with roofs shown removes roof units");
        _ = unfinished;
    }

    static void OverlayTests()
    {
        var racks = new List<PlacedObject>();
        for (var x = 0; x < 5; x++) racks.Add(new PlacedObject { Kind = ObjKind.Rack, X = 10 + x, Z = 4 });
        for (var x = 0; x < 3; x++) racks.Add(new PlacedObject { Kind = ObjKind.Rack, X = 20 + x, Z = 4 });
        for (var z = 0; z < 4; z++) racks.Add(new PlacedObject { Kind = ObjKind.Rack, X = 30, Z = 4 + z, Rot = 1 });
        var rows = DatacenterArchitect.World.Overlay.Rows(racks);
        Check(rows.Count == 3 && rows.Any(r => r.Count == 5) && rows.Any(r => r.Count == 4), "racks group into rows");
        Check(DatacenterArchitect.World.WorldView.FrontOffset(0) == (0, 1) && DatacenterArchitect.World.WorldView.FrontOffset(2) == (0, -1), "rack fronts face the cold aisle");
    }
}
