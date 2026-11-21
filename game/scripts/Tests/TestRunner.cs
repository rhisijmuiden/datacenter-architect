using System;
using System.Collections.Generic;
using System.Linq;
using DatacenterArchitect.Core;
using DatacenterArchitect.Sim;
using DatacenterArchitect.UI;
using Godot;
using FileAccess = Godot.FileAccess;

namespace DatacenterArchitect.Tests;

/// <summary>Headless checks: godot --headless --path game -- --run-tests (exit code 0 = all passed).</summary>
public static partial class TestRunner
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
            ("rack chain", RackChainTests), ("gates", GateTests), ("curves", CurveTests), ("navigation", NavTests), ("stairs and lifts", StairsTests), ("undo", UndoTests), ("smart drag", SmartDragTests), ("rack access", RackAccessTests), ("creative", CreativeTests), ("demo", DemoTests),
            ("power chain", PowerTests), ("backup power", BackupTests), ("grid and suppliers", GridTests),
            ("cooling", CoolingTests), ("roof and underground", RoofTests), ("overlay rows", OverlayTests),
            ("options", OptionsTests), ("thumbnails", ThumbnailTests), ("network", NetworkTests), ("network money", NetworkMoneyTests), ("peering", PeeringTests),
            ("organisation", OrgTests), ("customers", CustomerTests), ("tenders", TenderTests), ("finance", FinanceTests), ("old saves get customers", LegacyTests), ("customer visits", VisitTests),
            ("servers in racks", ServerTests), ("large halls", LargeHallTests),
            ("staff", StaffTests), ("faults and repairs", FaultTests), ("maintenance and parts", MaintenanceTests), ("events", EventTests), ("old saves get staff", StaffMigrationTests),
            ("research", ResearchTests), ("security", SecurityTests), ("certificates", CertTests), ("reputation", ReputationTests), ("old saves get research", ResearchMigrationTests),
            ("market and rivals", MarketTests), ("campus land", LandTests), ("locations", SiteTests), ("office tenants", OfficeLeaseTests),
            ("hosting company", HostingTests), ("energy and ESG", EnergyTests), ("risks", RiskTests), ("key people", KeyPeopleTests), ("old saves get growth", GrowthMigrationTests),
            ("seasons, weather and the sun", ClimateTests), ("customer migrations", MigrationTests), ("first-person walking", WalkTests),
            ("management rail", RailTests), ("playtest batch 1", PlaytestTests),
            ("progress (step 11)", ProgressTests),
            ("round 2 (step 11)", Round2Tests),
            ("round 3 (step 11)", Round3Tests),
            ("playtest round 3", Playtest3Tests),
            ("basement", BasementTests),
            ("customers per hall", HallClientTests),
            ("add a floor", AddLevelTests),
            ("office life", OfficeLifeTests),
        };
        // DCA_TESTS=basement,stairs runs only the groups whose name contains one of the words
        var only = System.Environment.GetEnvironmentVariable("DCA_TESTS")?.Split(',', StringSplitOptions.RemoveEmptyEntries);
        foreach (var (name, t) in tests)
        {
            if (only != null && !only.Any(w => name.Contains(w.Trim()))) continue;
            try { t(); }
            catch (Exception e) { _fail++; GD.PrintErr($"FAIL {name}: {e}"); }
        }
        GD.Print($"Tests: {_pass} passed, {_fail} failed");
        return _fail;
    }

    /// <summary>Gives a test game every director and manager, fully trained, as older savegames get them.</summary>
    static void Unlock(GameState s) => s.Grandfathered = Enum.GetValues<Exec>().ToList();

    static SemVer V(string s) { SemVer.TryParse(s, out var v); return v; }

    static void SemVerTests()
    {
        Check(V("v1.2.3").ToString() == "1.2.3", "parse v-prefix");
        Check(V("0.2.0").CompareTo(V("0.1.9")) > 0, "minor beats patch");
        Check(V("1.0.0").CompareTo(V("1.0.0-test.4")) > 0, "release beats pre-release");
        Check(V("1.0.0-test.10").CompareTo(V("1.0.0-test.9")) > 0, "numeric pre-release order");
        Check(!SemVer.TryParse("banana", out _), "reject garbage");
    }

    const string Gh = "https://github.com/rhisijmuiden/datacenter-architect/releases";
    const string Sha = "sha256:3a6eb0790f39ac87c94f3856b2dd2c5d110e6811602261a9a923d3bb23adc8b7";

    const string Releases = $$"""
    [
      {"tag_name":"v0.3.0-test.2","prerelease":true,"draft":false,"html_url":"{{Gh}}/tag/v0.3.0-test.2","body":"",
       "assets":[{"name":"DatacenterArchitect-0.3.0-test.2-Setup.exe","browser_download_url":"{{Gh}}/download/v0.3.0-test.2/DatacenterArchitect-0.3.0-test.2-Setup.exe","size":10,"digest":"{{Sha}}"}]},
      {"tag_name":"v0.2.0","prerelease":false,"draft":false,"html_url":"{{Gh}}/tag/v0.2.0","body":"",
       "assets":[{"name":"DatacenterArchitect-0.2.0-Setup.exe","browser_download_url":"{{Gh}}/download/v0.2.0/DatacenterArchitect-0.2.0-Setup.exe","size":10},
                 {"name":"DatacenterArchitect-0.2.0-Setup.exe.sha256","browser_download_url":"{{Gh}}/download/v0.2.0/DatacenterArchitect-0.2.0-Setup.exe.sha256","size":100}]},
      {"tag_name":"v0.4.0","prerelease":false,"draft":true,"html_url":"u3","body":"","assets":[]},
      {"tag_name":"v9.0.0","prerelease":false,"draft":false,"html_url":"http://evil.example/x","body":"",
       "assets":[{"name":"DatacenterArchitect-9.0.0-Setup.exe","browser_download_url":"http://evil.example/DatacenterArchitect-9.0.0-Setup.exe","size":10}]},
      {"tag_name":"v9.1.0","prerelease":false,"draft":false,"html_url":"{{Gh}}/tag/v9.1.0","body":"",
       "assets":[{"name":"..\\..\\evil-Setup.exe","browser_download_url":"{{Gh}}/download/v9.1.0/evil-Setup.exe","size":10}]},
      {"tag_name":"v9.2.0","prerelease":false,"draft":false,"html_url":"https://github.com/someone/else/releases/tag/v9.2.0","body":"",
       "assets":[{"name":"DatacenterArchitect-9.2.0-Setup.exe","browser_download_url":"https://github.com/someone/else/releases/download/v9.2.0/DatacenterArchitect-9.2.0-Setup.exe","size":10}]}
    ]
    """;

    static void UpdateTests()
    {
        var list = UpdateService.ParseReleases(Releases);
        Check(list.Count == 5, "drafts skipped");
        var stable = UpdateService.Decide(list, "stable", V("0.1.0"), "stable");
        Check(stable.Verdict == UpdateVerdict.NewerAvailable && stable.Release.Tag == "v0.2.0", "stable picks newest release");
        var test = UpdateService.Decide(list, "test", V("0.2.0"), "stable");
        Check(test.Verdict == UpdateVerdict.NewerAvailable && test.Release.Tag == "v0.3.0-test.2", "test channel includes pre-releases");
        var same = UpdateService.Decide(list, "stable", V("0.2.0"), "stable");
        Check(same.Verdict == UpdateVerdict.UpToDate, "up to date");
        var back = UpdateService.Decide(list, "stable", V("0.3.0-test.2"), "test");
        Check(back.Verdict == UpdateVerdict.ChannelSwitch && back.Release.Tag == "v0.2.0", "switch from test back to stable");

        // security: only HTTPS links into our own releases, our own installer names, and a checksum
        Check(list.Where(r => r.Version.Major == 9).All(r => r.InstallerUrl == null), "untrusted installers ignored (http, other repo, odd name)");
        Check(list.First(r => r.Tag == "v9.0.0").PageUrl == null, "untrusted release page not opened");
        Check(test.Release.Sha256 == Sha[7..], "GitHub asset digest read");
        Check(stable.Release.Sha256 == null && stable.Release.ChecksumUrl.EndsWith(".sha256"), "checksum file found when there is no digest");
        Check(!UpdateService.IsTrustedUrl("https://github.com.evil.example/rhisijmuiden/datacenter-architect/releases/download/x"), "look-alike host refused");
        Check(!UpdateService.IsTrustedUrl("https://user@github.com/rhisijmuiden/datacenter-architect/releases/download/x"), "user info refused");
        Check(!UpdateService.IsTrustedUrl("https://github.com:8443/rhisijmuiden/datacenter-architect/releases/download/x"), "other port refused");
        Check(!UpdateService.IsInstallerName("../DatacenterArchitect-1.0.0-Setup.exe") && !UpdateService.IsInstallerName("DatacenterArchitect-1.0.0-Setup.exe.bat"), "installer name checked");
        var hex = Sha[7..];
        Check(UpdateService.ParseChecksum($"{hex}  DatacenterArchitect-0.2.0-Setup.exe\n", "DatacenterArchitect-0.2.0-Setup.exe") == hex, "checksum file parsed");
        Check(UpdateService.ParseChecksum($"{hex}  other.exe", "DatacenterArchitect-0.2.0-Setup.exe") == null, "checksum for another file refused");
        var refused = false;
        try { UpdateService.DownloadAsync(list.First(r => r.Tag == "v9.0.0"), null).GetAwaiter().GetResult(); }
        catch (InvalidOperationException) { refused = true; }
        Check(refused, "download of an untrusted release refused");
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
        used.AddRange(Enum.GetNames<NetLink>().Select(n => "netlink_" + n.ToLowerInvariant()));
        used.AddRange(Catalog.Carriers.Select(c => c.DescKey));
        used.AddRange(Catalog.GridLevels.Select(g => g.NameKey));
        used.AddRange(Catalog.Suppliers.Select(g => g.DescKey));
        used.AddRange(Catalog.Containments.Values.Select(g => g.NameKey));
        used.AddRange(Catalog.Objects.Values.Where(d => d.Rooms != null).Select(GameState.NeedsRoomKey));
        // organisation, customers and the books build keys from the catalogue
        foreach (var e in Org.Execs.Values) used.AddRange(new[] { e.Key, e.Key + "_unlocks", e.Key + "_train_1", e.Key + "_train_2" });
        used.AddRange(Org.Customers.Values.Select(c => c.Key));
        used.AddRange(Enum.GetNames<HallZone>().Select(n => "zone_" + n.ToLowerInvariant()));
        used.AddRange(new[] { "inc_contracts", "inc_network", "cost_energy", "cost_network", "cost_crew", "cost_salaries", "cost_site", "cost_sla", "cost_interest", "cost_build", "fin_loans", "fin_investors", "fin_repay", "fin_dividend",
            "inc_heat", "inc_office_rent", "cost_compliance", "cost_proposals", "cost_research" }.Select(k => "book_" + k));
        // step 8: research, certificates, reviews, proposals and policies build keys from the catalogue
        foreach (var r in ResearchTree.All) used.AddRange(new[] { "rs_" + r.Id, "rs_" + r.Id + "_desc" });
        used.AddRange(Enum.GetNames<Branch>().Select(n => "branch_" + n.ToLowerInvariant()));
        foreach (var c in CertCatalog.Defs.Values) used.AddRange(new[] { c.Key, c.Key + "_desc", "req_" + c.Key });
        foreach (var t in Enum.GetNames<ReviewTopic>().Select(n => n.ToLowerInvariant()))
            used.AddRange(new[] { "rev_topic_" + t, "rp_quality_" + t + "_help", $"rev_{t}_good", $"rev_{t}_ok", $"rev_{t}_bad" });
        foreach (var p in Enum.GetNames<ProposalKind>().Select(n => n.ToLowerInvariant())) used.AddRange(new[] { "prop_" + p, "prop_" + p + "_quote" });
        foreach (var l in GameState.New(new NewGameOptions()).SecurityLines()) used.AddRange(new[] { l.Key, l.Key + "_tip" });
        used.AddRange(new[] { "pol_id_check", "pol_escort", "pol_office_hours" }.SelectMany(k => new[] { k, k + "_desc" }));
        used.AddRange(Staffing.Roles.Values.SelectMany(r => new[] { r.Key, r.Key + "_desc" }));
        used.AddRange(Enum.GetNames<Season>().Select(n => "season_" + n.ToLowerInvariant()));
        used.AddRange(new[] { "min_digital", "min_infrastructure", "min_finance", "min_health", "cust_name_municipality", "cust_legacy" });
        // every msg_/hint_/goal_ key mentioned in the scripts must exist
        foreach (var file in new[] { "res://scripts/Main.cs", "res://scripts/Sim/GameState.cs", "res://scripts/Sim/Simulation.cs", "res://scripts/UI/Hud.cs", "res://scripts/UI/Menus.cs", "res://scripts/UI/PowerPanel.cs", "res://scripts/UI/NetworkPanel.cs", "res://scripts/Sim/GameState.Network.cs", "res://scripts/Sim/Network.cs", "res://scripts/World/WorldView.cs", "res://scripts/UI/OptionsDialog.cs", "res://scripts/UI/CompanyPanel.cs", "res://scripts/Sim/Org.cs", "res://scripts/Sim/Research.cs", "res://scripts/Sim/Security.cs", "res://scripts/Sim/Reputation.cs", "res://scripts/UI/ReputationPanel.cs", "res://scripts/Sim/Migrations.cs", "res://scripts/World/WorldView.Sky.cs" })
        {
            var text = FileAccess.GetFileAsString(file);
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(text, "\"((msg|hint|goal|cr|crew|hud|new|inspect|projects|prio|cat|power|grid|supplier|backup|chain|link|cool|halls?|advice|action|res|opt|menu|tool|net|spof|carrier|co|org|sales|zones?|leads|prod|deal|req|contracts|tenders?|fin|book|cust|bankrupt|customers|rp|rs|sec|cert|prop|pol|rev|ev|exec|staff|gr|loc|off|host|mk|en|esg|risk|key|news|cyber|supply|camp|country|size|wx|walk|season)_[a-z0-9_]+)\""))
                if (!ResearchTree.ById.ContainsKey(m.Groups[1].Value) && !m.Groups[1].Value.EndsWith('_')) used.Add(m.Groups[1].Value);
        }
        var absent = used.Distinct().Where(k => !en.ContainsKey(k)).ToList();
        Check(absent.Count == 0, "keys used in code exist: " + string.Join(", ", absent));
        TranslationServer.SetLocale("nl");
        Check(Loc.T("menu_new_game") != "menu_new_game", "Dutch lookup works");
        TranslationServer.SetLocale("en");
    }

    /// <summary>Every build item has a little 3D scene for its card picture.</summary>
    static void ThumbnailTests()
    {
        var items = new List<UI.BuildItem>();
        items.AddRange(Catalog.Objects.Keys.Select(k => new UI.BuildItem(UI.Mode.Object, (int)k)));
        items.AddRange(Catalog.Surfaces.Keys.Select(k => new UI.BuildItem(UI.Mode.Surface, (int)k)));
        items.AddRange(Catalog.Fences.Keys.Select(k => new UI.BuildItem(UI.Mode.Fence, (int)k)));
        items.AddRange(Catalog.Buildings.Keys.Select(k => new UI.BuildItem(UI.Mode.Building, (int)k)));
        items.AddRange(Catalog.Rooms.Keys.Select(k => new UI.BuildItem(UI.Mode.Room, (int)k)));
        items.AddRange(Catalog.Doors.Keys.Select(k => new UI.BuildItem(UI.Mode.Door, (int)k)));
        items.Add(new UI.BuildItem(UI.Mode.Row, (int)ObjKind.Rack));
        items.Add(new UI.BuildItem(UI.Mode.Curve, (int)Surface.Asphalt));
        var missing = new List<string>();
        foreach (var it in items)
        {
            var n = World.Thumbnails.Scene(it);
            if (n == null || n.GetChildCount() == 0) missing.Add(it.ToString());
            n?.Free();
        }
        Check(missing.Count == 0, "every build item has a picture: " + string.Join(", ", missing));
        Check(World.Thumbnails.Scene(UI.BuildItem.Select) == null, "select tool has no picture");
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
            DayNight = false, Weather = false, Seasons = false, MouseSensitivity = 150, InvertMouse = true,
        };
        a.Save(path);
        var b = AppSettings.Load(path);
        Check(b.WindowMode == WindowModeSetting.Windowed && b.Resolution == new Vector2I(2560, 1080) && b.Monitor == 1, "display settings round-trip");
        Check(!b.VSync && b.MaxFps == 144 && Mathf.IsEqualApprox(b.UiScale, 1.25f), "performance settings round-trip");
        Check(!b.Shadows && !b.AmbientOcclusion && !b.Fog && b.Msaa == 4 && !b.BlinkingLights && !b.MovingParts && !b.SmoothCamera, "effect switches round-trip");
        Check(b.MasterVolume == 55 && b.AmbienceVolume == 10 && b.InterfaceVolume == 0 && !b.AmbienceSound && !b.ClickSounds && !b.NotificationSounds && !b.MuteInBackground, "audio round-trip");
        Check(b.Language == "nl" && b.AutosaveMinutes == 15, "general settings round-trip");
        Check(!b.DayNight && !b.Weather && !b.Seasons && b.MouseSensitivity == 150 && b.InvertMouse, "day/night, weather and walking settings round-trip");

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
        Check(SaveSystem.Sanitize("..\\evil:x") == "___evil_x", "windows separators sanitised");
        DirAccess.MakeDirRecursiveAbsolute(SaveSystem.Dir);
        using (var bad = FileAccess.Open(SaveSystem.Dir + "/unit-test-broken.json", FileAccess.ModeFlags.Write)) bad.StoreString("{ not json");
        using (var huge = FileAccess.Open(SaveSystem.Dir + "/unit-test-huge.json", FileAccess.ModeFlags.Write))
            huge.StoreString("{\"Slot\":\"x\",\"State\":{\"PlotSize\":2000000,\"Options\":{}}}");
        Check(SaveSystem.Load("unit-test-broken") == null && SaveSystem.Load("unit-test-huge") == null, "broken or impossible saves refused without a crash");
        Check(SaveSystem.List().All(x => !x.Slot.StartsWith("unit-test-broken") && !x.Slot.StartsWith("unit-test-huge")), "refused saves not listed");
        SaveSystem.Delete("unit-test-broken"); SaveSystem.Delete("unit-test-huge");
        Check(!FileAccess.FileExists(SaveSystem.Dir + "/unit-test.json.tmp"), "no temporary save file left behind");
        var evil = new GameState { GameId = "../../Windows" };
        Check(GameState.IsSafeId(evil.GameId) && !World.Timelapse.DirOf("..\\x").Contains(".."), "timelapse folder cannot leave user://timelapse");
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
        Check(sim.Docks().Count == 1, "a roll-up door makes one loading bay");
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
        // connected areas: a closed room is its own area until it gets a door, and the aimed search agrees
        var s2 = GameState.New(new NewGameOptions());
        s2.AddBuilding(BuildingKind.Office, 10, 10, 8, 8, 1).Stage = Stage.Done;
        var nav2 = new Nav(s2);
        Check(!nav2.Connected(5, 5, 12, 12, 0, NavMode.Walk) && nav2.Find(new Pt(5, 5, 0), 0, (x, z) => x == 12 && z == 12, NavMode.Walk, (12, 12)) == null, "a closed room is not connected");
        s2.AddDoor(DoorType.Single, 12, 18, 1, 0);
        Check(nav2.Connected(5, 5, 12, 12, 0, NavMode.Walk) && nav2.Find(new Pt(5, 5, 0), 0, (x, z) => x == 12 && z == 12, NavMode.Walk, (12, 12)) != null, "a door joins the areas");
        Check(!nav2.Connected(5, 5, 12, 12, 0, NavMode.Carry), "a single door does not join them for carried racks");
    }

    static void UndoTests()
    {
        var s = GameState.New(new NewGameOptions());
        var h = new UndoHistory();
        var money = s.Money;
        var snap = UndoHistory.Take(s);
        Check(!h.Commit(s, snap, "tool_select"), "an action that changes nothing is not recorded");
        snap = UndoHistory.Take(s);
        s.PaintSurface(2, 2, 4, 3, Surface.Asphalt, out _);
        Check(h.Commit(s, snap, "surface_asphalt") && s.SurfaceAt(3, 3) == Surface.Asphalt, "paving is recorded");
        snap = UndoHistory.Take(s);
        var m1 = s.Money;
        var b = s.AddBuilding(BuildingKind.Office, 20, 20, 8, 8, 2);
        h.Commit(s, snap, "building_office");
        var paid = m1 - s.Money;
        Check(paid > 0 && h.NextUndo == "building_office", "a building is recorded with its cost");
        var hours = s.Hours;
        s.Hours += 3;
        Check(h.Undo(s) == "building_office" && !s.Buildings.Contains(b), "undo takes the building away");
        Check(s.Money == m1 && s.Hours == hours + 3, "undo refunds the building and leaves the clock running");
        Check(s.SurfaceAt(3, 3) == Surface.Asphalt, "undo leaves earlier actions alone");
        Check(h.Redo(s) == "building_office" && s.Buildings.Contains(b) && h.CanUndo && !h.CanRedo, "redo puts the building back");
        h.Undo(s); h.Undo(s);
        Check(s.SurfaceAt(3, 3) == Surface.Grass && s.Buildings.Count == 0, "undo goes back step by step");
        Check(h.Undo(s) == null, "nothing left to undo");
        // erasing can be undone too, and the erase refund is taken back
        h.Redo(s); h.Redo(s);
        b.Stage = Stage.Done;
        s.AddDoor(DoorType.Single, 22, 28, 1, 0);
        var lift = s.AddObject(ObjKind.Stairs, 21, 21, 0, 0);
        var before = s.Money;
        snap = UndoHistory.Take(s);
        s.Erase(21, 21, 2, 5, 0);
        h.Commit(s, snap, "tool_erase");
        Check(!s.Objects.Contains(lift) && s.Money > before, "erasing refunds half");
        h.Undo(s);
        Check(s.Objects.Contains(lift) && s.Money == before, "undoing an erase brings the stairs back and returns the refund");
        snap = UndoHistory.Take(s);
        s.PaintSurface(40, 40, 2, 2, Surface.Footpath, out _);
        h.Commit(s, snap, "surface_footpath");
        Check(!h.CanRedo, "a new action clears redo");
    }

    static void RackAccessTests()
    {
        var (s, hall, _) = NetSite();
        // hall 4..23 x 4..23
        Check(s.AddObject(ObjKind.Rack, 8, 4, 0, 0) == null && s.CanPlace(new PlacedObject { Kind = ObjKind.Rack, X = 8, Z = 4, Rot = 0 }) == "msg_rack_back", "a rack with its back against the wall is refused");
        Check(s.CanPlace(new PlacedObject { Kind = ObjKind.Rack, X = 8, Z = 23, Rot = 0 }) == "msg_rack_front", "a rack facing the wall is refused");
        var a = s.AddObject(ObjKind.Rack, 8, 8, 0, 0);
        Check(a != null, "a rack with free aisles on both sides is fine");
        Check(s.AddObject(ObjKind.Rack, 8, 9, 2, 0) == null, "racks face to face without an aisle are refused");
        Check(s.AddObject(ObjKind.Rack, 8, 7, 0, 0) == null, "a rack right behind another is refused");
        Check(s.CanPlace(new PlacedObject { Kind = ObjKind.Crah, X = 8, Z = 9, Rot = 0 }) == "msg_blocks_rack", "a CRAH in front of a rack is refused");
        Check(s.AddObject(ObjKind.Rack, 9, 8, 0, 0) != null, "racks side by side in a row are fine");
        Check(s.AddObject(ObjKind.Rack, 8, 10, 2, 0) != null, "a facing row across a 1 m cold aisle is fine");
        var block = GameState.RowBlock(5, 13, 20, 20, ObjKind.Rack, 0);
        Check(block.All(c => s.CanPlace(new PlacedObject { Kind = ObjKind.Rack, X = c.x, Z = c.z, Rot = c.rot }) == null), "a smart-drag block keeps every rack reachable");
    }

    static void SmartDragTests()
    {
        var one = GameState.RowBlock(12, 16, 24, 16, ObjKind.Rack, 0);
        Check(one.Count == 13 && one.All(c => c.z == 16 && c.rot == 0), "a flat drag is one row");
        var block = GameState.RowBlock(12, 16, 24, 23, ObjKind.Rack, 0);
        var rows = block.GroupBy(c => c.z).ToDictionary(g => g.Key, g => g.First().rot);
        Check(rows.Count == 4 && rows[16] == 0 && rows[18] == 2 && rows[21] == 0 && rows[23] == 2, "a wide drag lays rows like the demo hall: cold aisles between fronts, hot aisles between backs");
        Check(block.Count == 4 * 13, "every row runs the full length");
        var up = GameState.RowBlock(12, 23, 24, 16, ObjKind.Rack, 0).GroupBy(c => c.z).ToDictionary(g => g.Key, g => g.First().rot);
        Check(up[23] == 2 && up[21] == 0, "dragging the other way mirrors the pattern");
        var flipped = GameState.RowBlock(12, 16, 24, 23, ObjKind.Rack, 2).GroupBy(c => c.z).ToDictionary(g => g.Key, g => g.First().rot);
        Check(flipped.Count == 3 && flipped[16] == 2 && flipped[19] == 0 && flipped[21] == 2, "R starts the block with the backs");
        var alongZ = GameState.RowBlock(10, 10, 12, 20, ObjKind.Rack, 0).GroupBy(c => c.x).ToDictionary(g => g.Key, g => g.First().rot);
        Check(alongZ.Count == 2 && alongZ[10] == 3 && alongZ[12] == 1, "rows along z face each other across x");
    }

    static void StairsTests()
    {
        var s = GameState.New(new NewGameOptions());
        var b = s.AddBuilding(BuildingKind.Office, 10, 10, 12, 12, 3);
        b.Stage = Stage.Done;
        s.AddDoor(DoorType.Single, 12, 22, 1, 0);
        var nav = new Nav(s);
        Check(s.CanPlace(new PlacedObject { Kind = ObjKind.Stairs, X = 11, Z = 11 }) == null, "stairs can be placed in a building");
        var stairs = s.AddObject(ObjKind.Stairs, 11, 11, 0, 0);
        var stairCells = GameState.Cells(stairs).ToHashSet();
        Check(stairCells.Count == 10, "stairs take 2 x 5 cells");
        var up = nav.Find(new Pt(15, 18, 0), 1, (x, z) => x == 18 && z == 18, NavMode.Walk);
        Check(up != null && up[^1].F == 1, "people take the stairs to the next floor");
        Check(up != null && up.Zip(up.Skip(1)).Any(p => p.First.F == 0 && p.Second.F == 1 && stairCells.Contains((p.First.X, p.First.Z))), "the floor change happens on the stairs");
        Check(nav.Find(new Pt(15, 18, 0), 1, (x, z) => x == 18 && z == 18, NavMode.Carry) == null, "loads cannot be carried up the stairs");
        var lift = s.AddObject(ObjKind.PassengerLift, 19, 11, 0, 0);
        var liftCells = GameState.Cells(lift).ToHashSet();
        var high = nav.Find(new Pt(19, 14, 0), 2, (x, z) => x == 20 && z == 14, NavMode.Walk);
        Check(high != null && high.Zip(high.Skip(1)).Any(p => p.First.F != p.Second.F && liftCells.Contains((p.First.X, p.First.Z))), "people next to a passenger lift ride it two floors up");
        var near = nav.Find(new Pt(13, 17, 0), 1, (x, z) => x == 13 && z == 17, NavMode.Walk);
        Check(near != null && near.Zip(near.Skip(1)).Any(p => p.First.F != p.Second.F && stairCells.Contains((p.First.X, p.First.Z))), "for one floor next to the stairs people walk up");
        Check(nav.Find(new Pt(19, 14, 0), 1, (x, z) => x == 20 && z == 14, NavMode.Carry) == null, "a passenger lift does not take racks");
        Check(s.ObjectAt(12, 12, 2) == stairs, "stairs serve every floor");
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

    static void CurveTests()
    {
        var s = GameState.New(new NewGameOptions { Difficulty = 0 });
        var money = s.Money;
        // a quarter bend from the road edge up and round to the west
        var c = new CurvePath { Surface = Surface.Asphalt, Ax = 40, Az = 63.5, Cx = 40, Cz = 40, Bx = 20, Bz = 40, Width = 4 };
        Check(Math.Abs(c.At(0).x - 40) < 1e-9 && Math.Abs(c.At(1).x - 20) < 1e-9 && c.Length > 30 && c.Length < 40, "curve runs from start to end with a sensible length");
        var cost = s.CurveCost(c);
        Check(s.AddCurve(c) == null && s.Curves.Count == 1, "curve is built");
        Check(c.Cells.Count > 80 && c.Cells.All(i => s.Surfaces[i] == (byte)Surface.Asphalt), "curve paves the cells under it");
        Check(money - s.Money == cost && cost == c.Cells.Count * Catalog.Surfaces[Surface.Asphalt].Cost, "curve costs its paved m²");
        var (mx, mz) = c.At(0.5);
        Check(s.SurfaceAt((int)mx, (int)mz) == Surface.Asphalt && s.SurfaceAt(40, 40) == Surface.Grass, "the bend is paved, the control point corner is not");
        // trucks can use it: a path along the curve from the road edge to its far end
        var nav = new Nav(s);
        var path = nav.FindOnFloor(new List<(int, int)> { (40, 63) }, 0, (x, z) => x == 20 && z == 40, NavMode.Truck);
        Check(path != null, "trucks can drive along a curved road");
        // the aimed search (A*) finds a route as short as the full search, and gives up at once on unreachable cells
        var full = nav.FindOnFloor(new[] { (5, 60) }, 0, (x, z) => x == 50 && z == 10, NavMode.Walk);
        var aimed = nav.FindOnFloor(new[] { (5, 60) }, 0, (x, z) => x == 50 && z == 10, NavMode.Walk, (50, 10));
        Check(full != null && aimed != null && aimed.Count == full.Count && aimed[^1] == full[^1], "aimed search is as short as the full search");
        // erasing part keeps the rest, painting it all over removes the curve
        s.Erase(30, 35, 20, 4, 0);
        Check(s.Curves.Count == 1 && c.Cells.All(i => s.Surfaces[i] == (byte)Surface.Asphalt), "erasing part of a curve keeps the rest");
        var json = System.Text.Json.JsonSerializer.Serialize(s);
        var l = System.Text.Json.JsonSerializer.Deserialize<GameState>(json);
        Check(l.Curves.Count == 1 && l.Curves[0].Cells.Count == c.Cells.Count && Math.Abs(l.Curves[0].Cx - 40) < 1e-9, "curve survives save and load");
        s.PaintSurface(0, 0, 64, 64, Surface.Grass, out _);
        Check(s.Curves.Count == 0, "repaving all its cells removes the curve");
        Check(s.AddCurve(new CurvePath { Surface = Surface.Yard, Ax = 1, Az = 1, Bx = 9, Bz = 9, Cx = 1, Cz = 9 }) != null, "only roads and paths can be curved");
    }

    static void GateTests()
    {
        // a dock at the end of a driveway with a barrier and a sliding gate in a fence across it
        var (s, sim) = Site();
        var b = s.AddBuilding(BuildingKind.Datacenter, 22, 26, 14, 10, 1);
        b.Stage = Stage.Done;
        var hall = s.AddRoom(RoomType.DataHall, 22, 26, 8, 10, 0);
        var dock = s.AddRoom(RoomType.LoadingDock, 30, 26, 6, 10, 0);
        hall.Stage = dock.Stage = Stage.Done;
        s.PaintSurface(30, 36, 3, 4, Surface.Asphalt, out _);
        s.AddDoor(DoorType.RollUp, 30, 36, 1, 0);
        s.AddDoor(DoorType.Double, 30, 30, 0, 0);
        s.PlaceFence(20, 54, 30, 1, FenceType.Palisade, out _);
        var gate = s.AddObject(ObjKind.SlidingGate, 30, 54, 0, 0);
        var barrier = s.AddObject(ObjKind.Barrier, 30, 48, 0, 0);
        Check(gate != null && barrier != null, "gate and barrier placed on the driveway");
        Check(s.AddObject(ObjKind.SlidingGateLarge, 40, 54, 0, 0) != null, "large sliding gate fits in a fence");
        s.AddObject(ObjKind.Rack, 24, 28, 0, 0);
        var waited = false; var crashed = false; var parked = false; var poked = false;
        for (var t = 0.0; t < 20 && !parked; t += 0.02)
        {
            var before = sim.Agents.Where(a => a.Role == Role.Truck).ToDictionary(a => a.Id, a => a.Z);
            sim.Tick(0.02);
            foreach (var a in sim.Agents.Where(a => a.Role == Role.Truck && a.Phase == 1))
            {
                if (!before.TryGetValue(a.Id, out var z0)) continue;
                if (Math.Abs(a.Z - z0) < 1e-9 && a.Z > 48) waited = true;
                foreach (var (o, row) in new[] { (gate, 54.5), (barrier, 48.5) })
                {
                    var open = sim.GateOpen.TryGetValue(o.Id, out var v) ? v : 0;
                    // the nose is TruckNose ahead of the centre while driving north
                    if (a.Z - Simulation.TruckNose < row && z0 - Simulation.TruckNose >= row && open < 0.98) crashed = true;
                }
                // nose (forwards) or tail (backing up) never past the dock wall at z = 36
                if (a.Z + Math.Cos(a.Heading) * 3.2 < 36 - 0.05 || a.Z - Math.Cos(a.Heading) * Simulation.TruckRear < 36 - 0.05) poked = true;
            }
            parked = sim.Agents.Any(a => a.Role == Role.Truck && a.Phase == 2 && a.Load == TruckLoad.Equipment);
        }
        Check(parked, "truck reaches the dock through the gate and barrier");
        Check(waited, "truck waits for the gate to open");
        Check(!crashed, "truck never drives through a closed gate or barrier");
        var tr = sim.Agents.First(a => a.Role == Role.Truck && a.Phase == 2 && a.Load == TruckLoad.Equipment);
        Check(!poked && Math.Abs(tr.Z - (36 + Simulation.TruckRear + 0.1)) < 0.01 && Math.Abs(Math.Cos(tr.Heading) - 1) < 1e-6, $"truck backs up to the dock door, nose away from the building ({tr.X:0.00},{tr.Z:0.00} h{tr.Heading:0.00})");
        Run(sim, 6, () => sim.Agents.All(a => a.Role != Role.Truck));
        Check(sim.GateOpen[gate.Id] < 0.01 && sim.GateOpen[barrier.Id] < 0.01, "gate and barrier close after the trucks have left");
    }

    static void PlaytestTests()
    {
        // a new game starts on empty land: no buildings, rooms, objects or paving (the demo site only sits behind the title screen)
        var fresh = GameState.New(new NewGameOptions());
        Check(fresh.Buildings.Count == 0 && fresh.Rooms.Count == 0 && fresh.Objects.Count == 0 && fresh.Surfaces.All(b => b == 0), "a new game starts on empty land");
        // parking bays: 2.5 m wide, 5 m deep, one row on a 6 m deep strip, two rows on a 12 m deep lot
        bool In(int x, int z, int x0, int z0, int w, int d) => x >= x0 && x < x0 + w && z >= z0 && z < z0 + d;
        var one = ParkingLayout.Lines(40, (x, z) => In(x, z, 5, 5, 10, 6));
        Check(ParkingLayout.Bays(one) == 4 && one.All(l => Math.Abs(l.Ax - l.Bx) < 1e-9 && l.Length > 4 && l.Length < 5.01), "a 10 x 6 m lot gets one row of 4 bays");
        var two = ParkingLayout.Lines(40, (x, z) => In(x, z, 2, 2, 12, 12));
        Check(ParkingLayout.Bays(two) == 8, "a 12 x 12 m lot gets two rows of 4 bays");
        var tall = ParkingLayout.Lines(40, (x, z) => In(x, z, 2, 2, 6, 15));
        Check(ParkingLayout.Bays(tall) == 6 && tall.All(l => Math.Abs(l.Az - l.Bz) < 1e-9), "bays follow the long side");
        // the aisle goes next to the road: asphalt at z = 4 above a lot from z = 5 puts the bays on the far side
        var side = ParkingLayout.Lines(40, (x, z) => In(x, z, 5, 5, 10, 7), (x, z) => z == 4);
        Check(side.All(l => Math.Min(l.Az, l.Bz) >= 6.9), "bays sit away from the road");
        Check(ParkingLayout.Lines(40, (x, z) => In(x, z, 0, 0, 10, 6) || In(x, z, 20, 20, 10, 6), null, (20, 20, 21, 21)).All(l => l.Ax >= 20), "the preview only shows the lot being dragged");

        // trucks drive in the middle of a 5 m wide driveway instead of along its edge
        var g = GameState.New(new NewGameOptions());
        var mid = g.PlotSize / 2;
        g.PaintSurface(mid - 2, 10, 5, g.PlotSize - 10, Surface.Asphalt, out _);
        var nav = new Nav(g);
        var path = Enumerable.Range(10, g.PlotSize - 10).Reverse().Select(z => new Pt(mid - 2, z, 0)).ToList();
        var lane = nav.CenterLane(path);
        Check(lane.Count >= 2 && lane.All(p => Math.Abs(p.x - (mid + 0.5)) < 0.01), "a truck route along the kerb moves to the middle of the road");
        var found = nav.FindOnFloor(new[] { (mid, g.PlotSize - 1) }, 0, (x, z) => z == 12, NavMode.Truck);
        Check(found != null && found.All(p => p.X == mid), "trucks prefer the middle lane");
        // on a 6 m road traffic keeps right: driving north (-z) the right-hand side is +x... and back on the other side
        var r6 = GameState.New(new NewGameOptions());
        r6.PaintSurface(20, 10, 6, r6.PlotSize - 10, Surface.Asphalt, out _);
        var nav6 = new Nav(r6);
        var north = Enumerable.Range(10, r6.PlotSize - 10).Reverse().Select(z => new Pt(22, z, 0)).ToList();
        var inLane = nav6.CenterLane(north);
        var outLane = nav6.CenterLane(Enumerable.Reverse(north).ToList());
        Check(inLane.All(p => Math.Abs(p.x - 24.5) < 0.01), "driving in on a 6 m road keeps to the right lane");
        Check(outLane.All(p => Math.Abs(p.x - 21.5) < 0.01), "driving out keeps to the other lane");

        // a large site gets several cranes with normal jibs, all on the plot
        var big = new Building { X = 0, Z = 0, W = 90, D = 50, Floors = 1 };
        var cranes = World.WorldView.CranesFor(big, 120);
        Check(cranes.Count >= 4 && cranes.All(c => c.reach <= 30 && c.x >= 1 && c.z >= 1), "a big site gets several cranes with jibs of at most 30 m");
        var small = World.WorldView.CranesFor(new Building { X = 0, Z = 0, W = 12, D = 10, Floors = 1 }, 120);
        Check(small.Count == 1 && small[0].x >= 1 && small[0].z >= 1, "a small site gets one crane, on the plot");
    }

    static void RailTests()
    {
        // every screen tab is reachable from exactly the section the rail promises
        Check(Sections.All.Length == 8, "eight sections on the rail (F8 progress since step 11)");
        Check(Sections.All.Select(x => x.Hotkey).Distinct().Count() == 8 && Sections.ForKey(Key.F1) == 0 && Sections.ForKey(Key.F7) == 6 && Sections.ForKey(Key.F8) == 7, "F1 to F8 open the sections");
        var keys = Sections.All.SelectMany(x => x.Tabs.Select(t => t.Key).Append(x.Key).Append(x.ShortKey)).ToList();
        Check(keys.All(k => Loc.T(k) != k), "every section and tab has a text: " + string.Join(", ", keys.Where(k => Loc.T(k) == k)));
        var icons = Sections.All.SelectMany(x => x.Tabs.Select(t => t.Icon).Append(x.Icon)).Distinct().ToList();
        Check(icons.All(i => ResourceLoader.Exists($"res://assets/icons/{i}.svg")), "every section icon exists");
        foreach (var (kind, count) in new[] { (PanelKind.Company, 5), (PanelKind.Staff, 4), (PanelKind.Reputation, 5), (PanelKind.Growth, 7), (PanelKind.Overview, 3) })
            for (var t = 0; t < count; t++)
            {
                var (sec, tab) = Sections.Find(kind, t);
                var e = Sections.All[sec].Tabs[tab];
                Check(e.Kind == kind && e.Tab == t, $"{kind} tab {t} has a place on the rail");
            }
        Check(Sections.Find(PanelKind.Growth, GrowthPanel.TabEnergy).section == Sections.Infra, "energy lives under infrastructure");
        Check(Sections.Find(PanelKind.Company, CompanyPanel.TabSales).section == Sections.Customers, "sales lives under customers");
        Check(Sections.Find(PanelKind.Network, 0).section == Sections.Infra, "network lives under infrastructure");

        // the overview dashboard and its advice on the demo campus
        var s = Demo.Build();
        var figs = OverviewPanel.Figures(s);
        Check(figs.Count == 8 && figs.All(f => !string.IsNullOrEmpty(f.value)), "dashboard shows eight figures");
        s.Deals.Add(new Deal { Id = s.NextId++, State = DealState.Lead, Racks = 2, Name = "Test" });
        var adv = OverviewPanel.Advice(s, new[] { ("goal_rack", true), ("goal_ceo", false) });
        Check(adv.Any(a => a.kind == PanelKind.Company && a.tab == CompanyPanel.TabSales), "new leads show up as advice");
        Check(adv.Any(a => a.kind == PanelKind.Overview && a.text.Contains(Loc.T("goal_ceo"))), "the next goal shows up as advice");
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
        Check(inf.CoolRatio <= 1 && Math.Abs(inf.Pue - (1.5 - s.PueSaving)) < 1e-6, "demo cooling copes and PUE is 1.50 (minus free cooling when researched)");
        var net = s.Net;
        Check(net.OnlineRacks == inf.RunningRacks && net.Served >= 0.999, $"demo network has every running rack online ({net.OnlineRacks}/{inf.RunningRacks}, served {net.Served:0.00})");
        Check(net.Noc && net.Routes == 2 && net.Cores == 2 && net.IxGbps > 0 && net.PeerGbps > 0, "demo has a NOC, two routes, two cores, the IX and a peer");
        Check(s.CrossConnects + s.XcPending >= 14, "demo keeps its cross-connects");
        var json = System.Text.Json.JsonSerializer.Serialize(s);
        Check(json.Length < 200_000, "savegame stays small: " + json.Length);
    }

    /// <summary>A finished datacenter with a hall, a switchgear room and a UPS room, all built (creative instant build off, money on).</summary>

    // ---------------- network ----------------
    static (GameState s, Room hall, Room mmr) NetSite(bool ai = false, bool instant = true)
    {
        var c = new CreativeOptions { NoPowerLimit = true, NoCoolingLimit = true, UnlimitedMoney = true, InstantBuild = instant };
        var s = GameState.New(new NewGameOptions { Difficulty = 0, AiCompetitors = ai, Creative = true, CreativeOptions = c });
        Unlock(s);
        var b = s.AddBuilding(BuildingKind.Datacenter, 4, 4, 30, 20, 1);
        b.Stage = Stage.Done;
        var hall = s.AddRoom(RoomType.DataHall, 4, 4, 20, 20, 0);
        var mmr = s.AddRoom(RoomType.MeetMe, 24, 9, 5, 5, 0);
        foreach (var r in s.Rooms) r.Stage = Stage.Done;
        return (s, hall, mmr);
    }

    static void NetworkTests()
    {
        var (s, hall, mmr) = NetSite();
        for (var i = 0; i < 50; i++) Live(s, ObjKind.Rack, 6 + i % 16, 6 + i / 16 * 3);
        var n = s.Net;
        Check(n.OnlineRacks == 0 && n.OfflineRacks == 50, "without a network every running rack is offline");
        Check(s.CanPlace(new PlacedObject { Kind = ObjKind.Odf, X = 6, Z = 20 }) == "msg_needs_mmr_room", "an ODF goes in the meet-me room");
        Check(s.CanPlace(new PlacedObject { Kind = ObjKind.Spine, X = 25, Z = 10 }) == "msg_needs_hall", "a spine switch goes in a hall");
        Check(s.CanOrderCarrier(1, 10) == "msg_needs_mmr", "a carrier needs the MMR first");
        Live(s, ObjKind.Odf, 25, 10);
        Check(s.CanOrderCarrier(1, 10) == "msg_needs_pit", "and a fiber pit near its street pit");
        var pit = Live(s, ObjKind.FiberPit, 30, 62);
        Check(Network.NearestStreetPit(s, pit) == 1, "a pit connects to the nearest street pit");
        Check(s.CanOrderCarrier(0, 10) == "msg_needs_pit", "a carrier at another street pit is out of reach");
        Check(s.OrderCarrier(1, 10) == null && s.ContractOf(1).Active, "order NordFiber with a 10G port (instant build)");
        Check(s.OrderCarrier(1, 10) == "msg_carrier_exists", "one port per carrier");
        n = s.Net;
        Check(n.Upstream && n.OnlineRacks == 0, "no core router and no spine: still offline");
        Live(s, ObjKind.CoreRouter, 27, 10);
        var sp = Live(s, ObjKind.Spine, 5, 20);
        n = s.Net;
        Check(n.OnlineRacks == 48 && n.OfflineRacks == 2, $"one spine has 48 ports: 48 online ({n.OnlineRacks})");
        Check(n.Halls[0].Spines == 1 && n.Spofs.Contains("spof_spine") && n.Spofs.Contains("spof_core"), "single spine and core are flagged");
        Live(s, ObjKind.Spine, 6, 20);
        n = s.Net;
        Check(n.OnlineRacks == 50 && n.Link(NetLink.Spine).Redundancy == "N" && !n.Spofs.Contains("spof_spine"), "a second spine brings all racks online");
        // traffic: 50 racks × 0.25 Gbit/s × time of day
        var expect = 50 * Catalog.RackGbps * Network.DayCurve(s.Hours);
        Check(Math.Abs(n.DemandGbps - expect) < 1e-6 && n.Served >= 0.999 && n.TransitGbps > 0, "traffic goes out over transit");
        Check(Network.DayCurve(20) > 1.3 && Network.DayCurve(8) < 0.7 && Math.Abs(Enumerable.Range(0, 24).Average(h => Network.DayCurve(h)) - 1) < 1e-6, "evening peak, morning low, average 1");
        // a 1G port is far too small
        s.ContractOf(1).PortGbps = 1; s.ContractOf(1).Commit = 1; s.Touch(false);
        n = s.Net;
        Check(n.Served < 0.2 && n.Overflow > 5, $"a 1G port congests ({n.Served:0.00})");
        Check(NetworkPanel.Advice(s, n).Contains("net_advice_congested"), "advice says the network is full");
        Check(s.CanUpgradePort(s.ContractOf(1)) == null && s.UpgradePort(s.ContractOf(1)) == null && s.ContractOf(1).PortGbps == 10, "port upgrade 1G to 10G");
        // the IX takes a share for free
        var pitC = Live(s, ObjKind.FiberPit, 52, 62);
        Check(s.OrderCarrier(4, 10) == null, "order a port on the IX at street pit C");
        n = s.Net;
        Check(n.IxGbps > 0 && Math.Abs(n.IxGbps - n.DemandGbps * Network.IxShare(s)) < 1e-6 && n.Routes == 2, "the IX carries its share, two routes now");
        // ducts: a level 1 pit has 4 subducts, an upgrade gives 8
        Check(Catalog.Subducts(1) == 4 && Catalog.Subducts(2) == 8 && Catalog.Subducts(3) == 12, "subducts per level");
        s.Objects.Remove(pitC); s.Touch();
        n = s.Net;
        Check(n.Carriers.Any(x => x.Def.Ix && !x.Routed) && n.IxGbps == 0, "without its pit the IX has no route");
        // commit steps never go above the port
        var k = s.ContractOf(1);
        k.Commit = 5; s.StepCommit(k, 1); Check(k.Commit == 10, "commit up one step");
        s.StepCommit(k, 1); Check(k.Commit == 10, "commit never above the port");
        s.StepCommit(k, -1); Check(k.Commit == 5, "commit down one step");
        // creative: network is no limit
        var (c, _, _) = NetSite();
        c.Creative.NoNetworkLimit = true;
        Live(c, ObjKind.Rack, 6, 6);
        Check(c.Net.OnlineRacks == 1, "creative: network is no limit");
        _ = hall; _ = mmr; _ = sp;
    }

    static void NetworkMoneyTests()
    {
        Check(Math.Abs(Network.P95(Enumerable.Range(1, 100).Select(i => (float)i).ToList()) - 95) < 1e-6, "95th percentile of 1..100 is 95");
        var k = new CarrierContract { Carrier = 1, PortGbps = 10, Commit = 2 };
        Check(Math.Abs(Network.MonthlyCost(k) - (450 + 2000 * 0.26)) < 1e-6, "monthly cost: port plus commit per Mbit");
        for (var i = 0; i < 100; i++) k.Samples.Add(i < 90 ? 1 : 6);
        Check(Network.BurstCost(k, false) > 0 && Network.BurstCost(k, true) < Network.BurstCost(k, false), "burst above the commit is billed, cheaper with a NOC");
        k.Samples.Clear(); for (var i = 0; i < 100; i++) k.Samples.Add(i < 96 ? 1 : 9);
        Check(Network.BurstCost(k, false) == 0, "the top 5% of hours is free");
        Check(Catalog.PricePerMbit(Catalog.Carriers[0], 100) < Catalog.PricePerMbit(Catalog.Carriers[0], 1), "bigger commit, lower price");
        Check(Network.MonthlyCost(new CarrierContract { Carrier = 4, PortGbps = 10 }) == Catalog.Port(10).MonthlyFee, "the IX only charges the port");

        // lead time, hourly samples and daily money in a career game
        var s = GameState.New(new NewGameOptions { Difficulty = 0, AiCompetitors = false });
        Unlock(s);
        var b = s.AddBuilding(BuildingKind.Datacenter, 4, 4, 30, 20, 1); b.Stage = Stage.Done;
        s.AddRoom(RoomType.DataHall, 4, 4, 20, 20, 0); s.AddRoom(RoomType.MeetMe, 24, 9, 5, 5, 0);
        foreach (var r in s.Rooms) r.Stage = Stage.Done;
        Live(s, ObjKind.Odf, 25, 10); Live(s, ObjKind.FiberPit, 30, 62);
        var money = s.Money;
        Check(s.OrderCarrier(1, 10) == null && !s.ContractOf(1).Active && s.ContractOf(1).DaysLeft == Catalog.NewPortDays, "a new port takes 20 days");
        Check(s.Money == money - Catalog.Port(10).SetupFee, "the setup fee is paid");
        s.Advance(24 * 21);
        Check(s.ContractOf(1).Active && s.Notes.Any(x => x.key == "msg_carrier_live"), "after 21 days the port is live");
        Check(s.ContractOf(1).Samples.Count > 0 && s.ContractOf(1).Samples.Count < 24 * 30, "hourly samples while in service, cleared every month");
        Check(s.NetworkPerDay() < 0, "a port costs money every day");
        s.XcPending = 1; s.CrossConnects = 0;
        var before = s.Money;
        s.CompleteCrossConnect();
        Check(s.CrossConnects == 1 && s.XcPending == 0 && s.Money == before + Catalog.XcSetupFee, "a patched cross-connect pays its setup fee");
        // savegame
        SaveSystem.Save(s, "unit-test-net");
        var l = SaveSystem.Load("unit-test-net");
        Check(l != null && l.Contracts.Count == 1 && l.Contracts[0].PortGbps == 10 && l.Contracts[0].Samples.Count == s.Contracts[0].Samples.Count && l.CrossConnects == 1, "save roundtrip network");
    }

    static void PeeringTests()
    {
        var (s, _, _) = NetSite(ai: false);
        Live(s, ObjKind.Odf, 25, 10); Live(s, ObjKind.FiberPit, 30, 62);
        Check(s.CanPeer(0, true) == "msg_no_rivals", "no rivals, no private peering");
        var (a, _, _) = NetSite(ai: true);
        for (var i = 0; i < 20; i++) Live(a, ObjKind.Rack, 6 + i, 6);
        Live(a, ObjKind.Odf, 25, 10); Live(a, ObjKind.CoreRouter, 27, 10); Live(a, ObjKind.Spine, 5, 20); Live(a, ObjKind.FiberPit, 30, 62);
        a.OrderCarrier(1, 10);
        a.NetworkDay(Catalog.Rivals[0].RequestDay);
        Check(a.PeerOf(0)?.State == PeerState.Requested && a.Notes.Any(x => x.key == "msg_peer_request"), "a rival asks to peer");
        Check(a.PeerOf(1) == null, "the others ask later");
        Check(a.StartPeering(0) == null && a.PeerOf(0).State == PeerState.Active, "accepting builds the dark fiber (instant build)");
        var n = a.Net;
        Check(n.PeerFlows.ContainsKey(0) && n.PeerGbps > 0 && n.TransitGbps < n.DemandGbps, "peering takes traffic off transit");
        Check(a.CanPeer(1, true) == "msg_peer_too_small", "a big rival is not interested in a small network");
        a.DeclinePeering(0);
        Check(a.PeerOf(0).State == PeerState.Declined && !a.Net.PeerFlows.ContainsKey(0), "ending peering");
        // customers order cross-connects as networks sit in the MMR
        a.NetworkDay(1);
        Check(a.XcPending > 0, "customers order cross-connects");
    }

    /// <summary>A career game with a finished datacenter, an executive room and an office; power, cooling and network are no limit.</summary>
    static (GameState s, Room hall, Room exec, Room office) OrgSite(bool ai = false)
    {
        var c = new CreativeOptions { NoPowerLimit = true, NoCoolingLimit = true, NoNetworkLimit = true };
        var s = GameState.New(new NewGameOptions { Difficulty = 0, AiCompetitors = ai, Creative = true, CreativeOptions = c });
        s.Seed = 42;
        var b = s.AddBuilding(BuildingKind.Datacenter, 4, 4, 30, 20, 1); b.Stage = Stage.Done;
        var hall = s.AddRoom(RoomType.DataHall, 4, 4, 20, 20, 0);
        var exec = s.AddRoom(RoomType.Executive, 24, 4, 6, 4, 0);
        var office = s.AddRoom(RoomType.Office, 24, 8, 6, 6, 0);
        foreach (var r in s.Rooms) r.Stage = Stage.Done;
        return (s, hall, exec, office);
    }

    static void Furnish(GameState s, Room r, int n)
    {
        for (var i = 0; i < n; i++) { s.AddObject(ObjKind.Desk, r.X, r.Z + i, 0, 0); s.AddObject(ObjKind.Chair, r.X + 3, r.Z + i, 0, 0); }
    }

    static void OrgTests()
    {
        var (s, _, exec, office) = OrgSite();
        Check(s.CanHire(Exec.Ceo) == "msg_needs_ceo_office", "the CEO needs a furnished office");
        s.AddObject(ObjKind.Desk, exec.X, exec.Z, 0, 0);
        Check(s.Offices == 0, "a desk alone is not an office");
        s.AddObject(ObjKind.Chair, exec.X + 3, exec.Z, 0, 0);
        Check(s.Offices == 1, "desk and chair make an office");
        Check(s.CanHire(Exec.Sales) == "msg_needs_exec", "the sales director reports to the CEO");
        var money = s.Money;
        Check(s.HireExec(Exec.Ceo) == null && s.Has(Exec.Ceo) && s.Money == money - Org.Execs[Exec.Ceo].HireFee, "hire the CEO");
        Check(s.CanHire(Exec.Sales) == "msg_needs_office", "every director needs an own office");
        Furnish(s, exec, 3);
        Check(s.Offices == 1, "one room is one office, however many desks");
        // more executive rooms
        var b2 = s.AddBuilding(BuildingKind.Office, 4, 30, 30, 12, 1); b2.Stage = Stage.Done;
        for (var i = 0; i < 6; i++) { var r = s.AddRoom(RoomType.Executive, 4 + i * 5, 30, 5, 4, 0); r.Stage = Stage.Done; Furnish(s, r, 1); }
        Check(s.Offices == 7, "seven offices");
        Check(s.HireExec(Exec.Sales) == null && s.HireExec(Exec.Hr) == null, "sales director and HR");
        Check(s.CanHire(Exec.Legal) == "msg_needs_manager_desk", "managers work at a desk in an office room, not in a director's office");
        Furnish(s, office, Math.Min(office.D, 6));
        Check(s.CanHire(Exec.Legal) == null, "legal counsel unlocks after the sales director");
        Check(s.CanHire(Exec.Facility) == "msg_needs_exec", "the facility manager reports to operations");
        Check(s.HireExec(Exec.Operations) == null && s.CanHire(Exec.Facility) == null, "operations unlocks the facility manager");
        Check(s.CanDismiss(Exec.Ceo) == "msg_exec_needed", "the CEO cannot go while others report to it");
        // crew needs HR and operations
        var fresh = OrgSite().s;
        Check(fresh.ChangeCrew(Role.Builder, 1) == "msg_needs_hr" && fresh.Builders == 4, "more builders need HR");
        Check(fresh.ChangeCrew(Role.Builder, -1) == null && fresh.ChangeCrew(Role.Builder, 1) == null, "back up to the starting crew is fine");
        Check(s.ChangeCrew(Role.Builder, 2) == null && s.Builders == 6, "HR recruits builders");
        Check(s.ChangeCrew(Role.Fitter, 1) == null, "operations allows technical staff");
        Check(s.WorkSpeed > 1, "operations makes crews faster");
        // grid needs the facility manager and training
        Check(fresh.CanRequestGrid(1) == "msg_needs_facility", "10 MVA needs a facility manager");
        Check(s.HireExec(Exec.Facility) == null && s.CanRequestGrid(1) == null && s.CanRequestGrid(2) == "msg_needs_hv_training", "facility manager: 10 MVA, training for 20 MVA");
        Check(s.Train(Exec.Facility) == null && s.Hire(Exec.Facility).TrainingDaysLeft > 0, "training takes days");
        for (var d = 1; d <= 16; d++) s.OrgDay(d);
        Check(s.Training(Exec.Facility) == 1 && s.CanRequestGrid(2) == null && s.CanRequestGrid(3) == "msg_needs_hv_training", "after the course 20 MVA is open");
        // network manager for carriers
        Check(fresh.CanOrderCarrier(1, 10) == "msg_needs_netmgr", "carriers need a network manager");
        // sales reps need workplaces
        var freeDesks = s.Workplaces - s.ManagerCount;
        Check(freeDesks >= 2, "the office desks the managers do not use are free for reps");
        Check(s.ChangeReps(freeDesks) == null && s.CanAddRep(1) == "msg_needs_workplace", "a rep needs a free desk in an office room: reps and managers share them");
        Check(s.SalariesPerMonth == Org.Execs.Values.Where(e => s.Hire(e.Role) != null).Sum(e => e.Salary) + s.SalesReps * Org.SalesRepSalary, "salaries add up");
        // creative unlocks everything
        var cr = GameState.New(new NewGameOptions { Creative = true, CreativeOptions = CreativeOptions.All() });
        Check(cr.Has(Exec.Legal) && cr.CanRequestGrid(3) == null, "creative: all unlocked");
    }

    static void CustomerTests()
    {
        var (s, hall, exec, _) = OrgSite();
        for (var i = 0; i < 10; i++) Live(s, ObjKind.Rack, 6 + i, 6);
        var rng = new Random(1);
        var lead = s.MakeLead(0, rng, CustomerType.Hosting);
        lead.Product = Product.Rack; lead.Racks = 4; lead.Amp32 = false; lead.AB = false; lead.TwoN = false; lead.Green = false; lead.Carriers = 1;
        s.Deals.Add(lead);
        Check(s.Meets(lead) && s.CanSign(lead) == null, "4 racks free in a full-rack hall");
        var big = s.MakeLead(0, rng, CustomerType.Hosting);
        big.Product = Product.Rack; big.Racks = 20; big.Amp32 = false; big.AB = false; big.TwoN = false; big.Green = false;
        s.Deals.Add(big);
        Check(s.Check(big).Any(r => r.Key == "req_racks" && r.Status == ReqStatus.Fail) && s.Sign(big) == "msg_reqs_missing", "not enough racks");
        Check(s.Sign(lead) == null && lead.State == DealState.Active && lead.RackIds.Count == 4, "sign: four racks rented");
        Check(s.RackOwners().Count == 4 && s.FreeRacksFor(big, out _).Count == 6, "rented racks are no longer for sale");
        var green = s.MakeLead(0, rng, CustomerType.Smb); green.Product = Product.Rack; green.Racks = 1; green.Green = true; green.AB = green.Amp32 = green.TwoN = false;
        Check(s.Check(green).Any(r => r.Key == "req_green" && r.Status == ReqStatus.Fail), "green customers need a green energy contract");
        s.SetSupplier(2);
        Check(s.Check(green).First(r => r.Key == "req_green").Status == ReqStatus.Ok, "switching to green energy");
        // zones
        var peru = s.MakeLead(0, rng, CustomerType.Startup); peru.Product = Product.PerU; peru.Units = 10; peru.Green = false;
        Check(s.FreeUnits() == 0 && !s.Meets(peru), "no per-U hall, no units");
        var b2 = s.Buildings[0];
        var hall2 = s.AddRoom(RoomType.DataHall, 4, 4 + 20, 10, 0, 0);
        // a second hall inside the first building is not possible here; switch the zone of the first hall instead
        var cage = s.MakeLead(0, rng, CustomerType.Streaming); cage.Product = Product.Cage; cage.Racks = 6; cage.Amp32 = cage.AB = cage.TwoN = cage.Green = false;
        Check(s.Check(cage).First(r => r.Key == "req_cage").Status == ReqStatus.Fail, "cages need a cage hall");
        s.SetZone(hall, HallZone.Cage);
        Check(s.FreeRacksFor(cage, out var hid).Count == 6 && hid == hall.Id, "the six free racks form a cage");
        s.SetZone(hall, HallZone.PerU);
        Check(s.FreeUnits() == 6 * Org.UnitsPerRack, "per U: 42 units per free rack; rented racks keep their tenant");
        // 32 A needs A/B and aisles; with power as no limit A/B counts as met, aisles do not
        s.SetZone(hall, HallZone.FullRacks);
        var dense = s.MakeLead(0, rng, CustomerType.Crypto); dense.Product = Product.Rack; dense.Racks = 2; dense.Amp32 = true; dense.Green = false;
        Check(s.FreeRacksFor(dense, out _).Count == 0, "32 A racks need hot/cold aisles");
        hall.Containment = Containment.HotCold;
        Check(s.FreeRacksFor(dense, out _).Count == 6, "with aisles 32 A is fine");
        // income, service and satisfaction
        var money = s.Money;
        s.Advance(24);
        Check(s.Books.Last().Lines["inc_contracts"] == (long)Math.Round(lead.Monthly / 30.0), "a day of rent");
        var rack = s.Objects.First(o => o.Id == lead.RackIds[0]);
        rack.Status = ObjStatus.Placed; s.Touch(false);
        s.Advance(24);
        Check(lead.Served < 1 && lead.Satisfaction < 80, "a rack down: lower service and satisfaction");
        rack.Status = ObjStatus.Live; s.Touch(false);
        // demolished racks are replaced
        s.Objects.Remove(rack); s.Touch();
        s.Advance(24);
        Check(lead.RackIds.Count == 4 && !lead.RackIds.Contains(rack.Id), "a demolished rack is replaced by a free one");
        // renewals
        lead.Satisfaction = 90; lead.EndDay = s.Day + 0.5;
        s.Advance(24);
        Check(lead.State == DealState.Active && lead.EndDay > s.Day + 300, "a happy customer renews");
        lead.Satisfaction = 30; lead.EndDay = s.Day + 0.5;
        s.Advance(24);
        Check(lead.State == DealState.Ended && lead.RackIds.Count == 0 && s.Notes.Any(n => n.key == "msg_contract_ended"), "an unhappy customer leaves at the end");
        // leads come from sales
        var (t, _, te, _) = OrgSite();
        t.Advance(24 * 10);
        Check(t.Deals.Count == 0, "no sales director, no customers");
        Furnish(t, te, 1);
        var b3 = t.AddBuilding(BuildingKind.Office, 4, 30, 12, 6, 1); b3.Stage = Stage.Done;
        var e2 = t.AddRoom(RoomType.Executive, 4, 30, 6, 4, 0); e2.Stage = Stage.Done; Furnish(t, e2, 1);
        t.HireExec(Exec.Ceo); t.HireExec(Exec.Sales);
        t.Advance(24 * 10);
        Check(t.Deals.Any(d => d.State == DealState.Lead), "the sales director brings in requests");
        Check(t.Deals.All(d => d.Monthly > 0 && (d.Racks > 0 || d.Units > 0)), "requests have a size and a price");
        Check(t.Deals.Count(d => d.State == DealState.Lead) <= Org.MaxLeads, "at most eight open requests");
        _ = hall2; _ = b2; _ = exec; _ = money;
    }

    static void TenderTests()
    {
        var (s, hall, _, _) = OrgSite(ai: true);
        s.Grandfathered = new List<Exec> { Exec.Ceo, Exec.Sales };
        s.NextTenderDay = 1;
        s.OrgDay(1);
        var t = s.Deals.FirstOrDefault(d => d.State == DealState.Tender);
        Check(t != null && t.Tender && t.Racks >= 12 && t.Monthly > 0 && s.Notes.Any(n => n.key == "msg_new_tender"), "a tender comes in");
        Check(s.CanBid(t) == "msg_needs_legal", "bidding needs a legal counsel");
        s.Grandfathered.Add(Exec.Legal);
        Check(s.PlaceBid(t, 85) == null && t.State == DealState.Bid, "bid 85%");
        var asked = t.Monthly;
        s.Reputation = 100;
        var won = s.ResolveBid(t, new Random(3));
        Check(won && t.State == DealState.Building && t.Monthly == (long)Math.Round(asked * 0.85 / 100) * 100 && t.DeadlineDay > s.Day, "a low bid with a good name wins; build before the deadline");
        var money = s.Money; var rep = s.Reputation;
        t.DeadlineDay = s.Day + 0.5;
        s.Advance(24);
        Check(t.State == DealState.Lost && s.Money < money && s.Reputation < rep && s.Notes.Any(n => n.key == "msg_tender_failed"), "a missed deadline costs a fine and reputation");
        // delivering on time
        var t2 = new Deal { Id = s.NextId++, Name = "Test", Type = CustomerType.Hyperscaler, Product = Product.Hall, Tender = true, Racks = 4, Monthly = 50000, Months = 36, State = DealState.Building, DeadlineDay = s.Day + 100 };
        s.Deals.Add(t2);
        s.SetZone(hall, HallZone.PrivateHall);
        for (var i = 0; i < 4; i++) Live(s, ObjKind.Rack, 6 + i, 10);
        s.Advance(24);
        Check(t2.State == DealState.Active && t2.RackIds.Count == 4, "delivered: the hall is the customer's");
        var other = new Deal { Product = Product.Hall, Racks = 1 };
        Check(s.FreeRacksFor(other, out _).Count == 0, "a private hall has one tenant");
    }

    static void FinanceTests()
    {
        var (s, _, _, _) = OrgSite();
        Check(s.CanBorrow(1_000_000) == "msg_needs_cfo", "loans need a CFO");
        s.Grandfathered = new List<Exec> { Exec.Ceo, Exec.Cfo };
        var money = s.Money;
        Check(s.Borrow(1_000_000) == null && s.Money == money + 1_000_000 && s.Debt == 1_000_000, "borrow 1 million");
        Check(s.Borrow(20_000_000) == "msg_credit_limit", "the bank has a limit");
        var l = s.Loans[0];
        Check(l.Payment == GameState.Annuity(1_000_000, l.Rate, GameState.LoanMonths) && l.Payment > 1_000_000 / 60, "annuity above the bare principal");
        s.Advance(24 * 31);
        Check(s.Loans[0].Balance < 1_000_000 && s.Loans[0].MonthsLeft == GameState.LoanMonths - 1 && s.Books.Any(b => b.Lines.ContainsKey("cost_interest")), "a monthly instalment");
        money = s.Money;
        Check(s.Repay(s.Loans[0]) == null && s.Loans.Count == 0 && s.Money < money, "repay early");
        var offer = s.InvestorOffer;
        money = s.Money;
        Check(s.TakeInvestment() == null && s.InvestorShare > 0.09 && s.Money == money + offer, "sell 10% to an investor");
        for (var i = 0; i < 4; i++) s.TakeInvestment();
        Check(s.CanInvest() == "msg_investors_max" && s.InvestorShare <= 0.49 + 1e-9, "you keep the majority");
        Check(s.Books.Last().Lines.ContainsKey("cost_crew") && s.Books.Last().Lines.ContainsKey("cost_site"), "the books keep crew and site costs");
        // energy contracts
        s.Hours += 24 * 30;
        s.SetSupplier(0);
        Check(s.SwitchFee(1) == 0, "nothing running: no fee");
        // bankruptcy
        var (b, _, _, _) = OrgSite();
        b.Money = -10;
        b.Advance(24);
        Check(b.DaysInRed == 1 && b.Notes.Any(n => n.key == "msg_in_red"), "in the red: a warning");
        b.Money = -1_000_000;
        b.Advance(24 * 30);
        Check(b.Bankrupt && b.Notes.Any(n => n.key == "msg_bankrupt"), "30 days in the red: bankrupt");
        var h = b.Hours; b.Advance(24);
        Check(b.Hours == h, "a bankrupt game stands still");
        var u = GameState.New(new NewGameOptions { Creative = true, CreativeOptions = new CreativeOptions { UnlimitedMoney = true } });
        u.Money = -5; u.Advance(24 * 40);
        Check(!u.Bankrupt, "unlimited money never goes bankrupt");
    }

    static void VisitTests()
    {
        var s = Demo.Build();
        var sim = new Simulation(s);
        var perDay = sim.VisitsPerDay();
        Check(perDay > 0 && perDay < 2, $"a few customers visit per day, not all of them ({perDay:0.00})");
        var rack = s.Objects.First(o => o.Id == s.Deals.First(d => d.State == DealState.Active).RackIds[0]);
        var a = sim.StartVisit(rack);
        Check(a != null && a.Role == Role.Visitor, "a customer walks to its rack");
        for (var i = 0; i < 400 && !a.Gone; i++) sim.Tick(0.05);
        Check(a.Gone, "and leaves again after the visit");
        // a huge campus still has only a handful of customers on site
        for (var i = 0; i < 200; i++) s.Deals.Add(new Deal { Id = s.NextId++, Product = Product.Rack, State = DealState.Active, RackIds = new List<int> { rack.Id }, Racks = 1, Monthly = 100, EndDay = 9999 });
        s.Hours = 24 * 20 + 9;
        for (var i = 0; i < 24 * 20; i++) sim.Tick(0.05);
        Check(sim.Agents.Count(x => x.Role == Role.Visitor) <= sim.MaxVisitors && sim.MaxVisitors <= 40, "customers on site stay a small share of the rented racks");
    }

    static void LegacyTests()
    {
        var (s, _, _, _) = OrgSite();
        for (var i = 0; i < 5; i++) Live(s, ObjKind.Rack, 6 + i, 6);
        s.SaveFormat = 4; s.Deals.Clear();
        s.Migrate();
        var d = s.Deals.SingleOrDefault();
        Check(d != null && d.Legacy && d.RackIds.Count == 5 && d.Monthly == 4500, "old racks keep their rent with existing customers");
        Check(s.Has(Exec.Network) && s.Has(Exec.Facility) && s.CanRequestGrid(3) == null && !s.Has(Exec.Sales), "old games keep their network and grid options");
        SaveSystem.Save(s, "unit-test-org");
        var l = SaveSystem.Load("unit-test-org");
        Check(l != null && l.Deals.Count == 1 && l.Deals[0].RackIds.Count == 5 && l.Grandfathered.Count == 4, "save roundtrip customers");
    }

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

    /// <summary>A live object packed in without the placement rules (power and cooling sums only).</summary>
    static PlacedObject LiveRaw(GameState s, ObjKind k, int x, int z)
    {
        var o = new PlacedObject { Id = s.NextId++, Kind = k, X = x, Z = z, Status = ObjStatus.Live };
        s.Objects.Add(o); s.Touch(false);
        return o;
    }

    static PlacedObject Live(GameState s, ObjKind k, int x, int z, int floor = 0)
    {
        var o = s.AddObject(k, x, z, 0, floor);
        if (o != null) { o.Status = ObjStatus.Live; s.Touch(false); }
        return o;
    }

    static void ServerTests()
    {
        var heights = new HashSet<int>();
        var models = new HashSet<World.ServerFill.Model>();
        var ok = true;
        for (var id = 1; id <= 300; id++)
        {
            var units = World.ServerFill.For(id);
            var used = new bool[World.ServerFill.RackUnits];
            foreach (var u in units)
            {
                heights.Add(u.Height); models.Add(u.Model);
                if (u.Height != World.ServerFill.Units(u.Model)) ok = false;
                for (var i = u.U; i < u.U + u.Height; i++)
                {
                    if (i < 0 || i >= used.Length || used[i]) { ok = false; continue; }
                    used[i] = true;
                }
            }
            if (units.Count < 4 || !units.Any(u => u.Model == World.ServerFill.Model.Switch1U)) ok = false;
        }
        Check(ok, "servers fit in 42U without overlapping, with a switch on top");
        Check(new[] { 1, 2, 3, 4, 7 }.All(heights.Contains), "servers of 1U, 2U, 3U, 4U and 7U");
        Check(models.Count == Enum.GetValues<World.ServerFill.Model>().Length, "every server model is used");
        Check(World.ServerFill.For(42).SequenceEqual(World.ServerFill.For(42)), "a rack always holds the same servers");
        Check(!World.ServerFill.For(42).SequenceEqual(World.ServerFill.For(43)), "racks differ from each other");
        var code = World.ServerFill.Material.Shader.Code;
        Check(code.Contains("TIME") && code.Contains("INSTANCE_CUSTOM"), "LEDs blink in the shader, per instance");
    }

    /// <summary>Thousands of racks: lookups stay correct and placing a rack only redraws its own chunk.</summary>
    static void LargeHallTests()
    {
        var s = Bench.Build(2000);
        Check(s.Racks == 2000, "benchmark hall holds 2000 racks");
        // the cell index agrees with a plain search, also after adding and erasing
        bool Same()
        {
            foreach (var (x, z) in new[] { (7, 5), (30, 6), (8, 8), (5, 6), (66, 40), (2, 2), (67, 6) })
            {
                var slow = s.Objects.FirstOrDefault(o => o.Floor == 0 && GameState.Cells(o).Contains((x, z)));
                if (s.ObjectAt(x, z) != slow) return false;
            }
            return true;
        }
        Check(Same(), "cell index matches a full search");
        var added = s.AddObject(ObjKind.Rack, 67, 6, 0, 0);
        Check(added != null && s.ObjectAt(67, 6) == added && Same(), "index follows a placed rack");
        s.Erase(67, 6, 1, 1, 0);
        Check(s.ObjectAt(67, 6) == null && Same(), "index follows an erased rack");
        var layout = s.LayoutRevision;
        s.AddObject(ObjKind.Rack, 67, 6, 0, 0);
        Check(s.LayoutRevision == layout, "placing a rack does not rebuild walls and paving");

        var r = new World.RackRenderer();
        IEnumerable<(PlacedObject, float)> Racks() => s.Objects.Where(o => o.Kind == ObjKind.Rack).Select(o => (o, 0.3f));
        var first = r.Update(Racks());
        Check(first == r.ChunkCount && first > 4, "racks are split into chunks");
        Check(r.Update(Racks()) == 0, "nothing changed, nothing rebuilt");
        s.AddObject(ObjKind.Rack, 68, 6, 0, 0);
        Check(r.Update(Racks()) == 1, "one new rack rebuilds one chunk");
        s.Objects.First(o => o.Kind == ObjKind.Rack && o.X == 30).Status = ObjStatus.Placed;
        Check(r.Update(Racks()) == 1, "a rack going offline rebuilds only its chunk");
        r.Clear(); r.Free();
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
        Unlock(s);
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
        Unlock(instant);
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
        for (var i = 0; i < 220; i++) LiveRaw(ps, ObjKind.Rack, 5 + i % 18, 5 + i / 18);
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
        for (var i = 0; i < 20; i++) LiveRaw(s, ObjKind.Rack, 6 + i % 10, 18 + i / 10);
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
        Check(s.UpgradeHall(hall, false) == "msg_needs_research", "the cold corridor needs research first");
        s.Researched.Add("cool_corridor");
        s.UpgradeHall(hall, false); s.Advance(73);
        r = Infra.Analyze(s);
        Check(hall.Containment == Containment.ColdCorridor && Math.Abs(r.Pue - 1.3) < 1e-9, "cold corridor: PUE 1.30");
        Live(s, ObjKind.Crah, 4, 12);
        s.AddObject(ObjKind.CoolingTower, 44, 30, 0, 0);
        for (var i = 0; i < 20; i++) s.Advance(0.5);
        r = Infra.Analyze(s);
        Check(!hall.Hot && r.DownForHeat == 0 && r.RunningRacks == 60, $"with enough cooling the racks come back ({hall.Hot} {r.DownForHeat} {r.RunningRacks} broken {string.Join(",", s.Objects.Where(o => o.Broken).Select(o => o.Kind))})");
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

    /// <summary>A demo site with events switched off, so a test decides what happens.</summary>
    static GameState Quiet()
    {
        var s = Demo.Build();
        s.Options.Creative = false; s.Options.CreativeOptions = new CreativeOptions();
        s.Options.Events = false;
        s.Options.Disasters = false; s.Options.SupplyDelays = false; s.Options.WeatherEffects = false;
        s.Seed = 4242;
        return s;
    }

    static void StaffTests()
    {
        var s = Quiet();
        Check(s.Staff.Count == 0, "a new site starts without operational staff");
        Check(s.Recommended(StaffRole.Cooling) >= 1 && s.Recommended(StaffRole.Electrical) >= 1, "the site needs cooling and electrical technicians");
        Check(s.RoleLocked(StaffRole.Guard) == "msg_needs_security_mgr", "guards need a security manager");
        Check(s.RoleLocked(StaffRole.Canteen) == "msg_needs_canteen", "canteen staff need a canteen");
        s.Advance(24);
        Check(s.CountOf(StaffRole.Cooling) == s.Target(StaffRole.Cooling) && s.CountOf(StaffRole.Electrical) == s.Target(StaffRole.Electrical), "managers hire the team by themselves");
        Check(s.CountOf(StaffRole.Guard) == 0, "no guards without a security manager");
        var pay = s.StaffPayPerMonth;
        Check(pay > 0, "staff cost salaries");
        // staffing policy
        s.SetLevel(Dept.Facility, 2);
        s.Advance(24);
        Check(s.CountOf(StaffRole.Cooling) >= 3, "generous staffing covers every shift of a round-the-clock role");
        var cov = s.Coverage(StaffRole.Cooling);
        Check(cov[0] >= 1 && cov[1] >= 1 && cov[2] >= 1, "the automatic roster spreads them over day, evening and night");
        Check(s.Staff.Where(e => !e.Def.AllDay).All(e => e.Shift == Shift.Day), "daytime roles work days");
        s.SetLevel(Dept.Facility, 0);
        for (var i = 0; i < 6; i++) s.Advance(24);
        Check(s.CountOf(StaffRole.Cooling) <= s.Target(StaffRole.Cooling) + 1, "lean staffing: contracts are not renewed");
        // plans
        var before = s.Staff.Count(e => e.Role == StaffRole.Support);
        s.Support247 = true; s.RemoteHandsService = true;
        s.Advance(24);
        Check(s.CountOf(StaffRole.Support) >= 3 && s.Coverage(StaffRole.Support).All(c => c >= 1), "24/7 support: support engineers on every shift");
        Check(s.CountOf(StaffRole.RemoteHands) >= 1 && s.RemoteHandsIncome > 0, "remote hands earn money");
        Check(s.PlansPerMonth == GameState.Support247Fee + GameState.RemoteHandsFee, "service plans cost a monthly fee");
        var month = s.Books.FirstOrDefault(b => b.Month == s.MonthIndex);
        Check(month != null && month.Lines.TryGetValue("cost_staff", out var cs) && cs < 0 && month.Lines.TryGetValue("inc_remote_hands", out var rh) && rh > 0, "staff and remote hands in the books");
        // wellbeing and leaving
        Check(s.Wellbeing().Any(w => w.key == "wb_canteen" && w.points < 0), "staff miss a canteen");
        s.PayPolicy = 0; s.TrainingBudget = 0;
        foreach (var e in s.Staff) e.Satisfaction = 10;
        var n = s.Staff.Count;
        for (var i = 0; i < 20; i++) s.Advance(24);
        Check(s.Incidents.Any(l => l.Key == "ev_staff_quit") || s.Staff.Count != n, "unhappy people leave (and are replaced)");
        // training budget levels people up
        var tr = Quiet();
        tr.Advance(24);
        tr.TrainingBudget = 20000;
        var levels = tr.Staff.Sum(e => e.Level);
        for (var i = 0; i < 60; i++) tr.Advance(24);
        Check(tr.Staff.Sum(e => e.Level) > levels, "the training budget levels people up");
        // without HR nobody is hired
        var t = Quiet();
        t.Execs.RemoveAll(x => x.Role == Exec.Hr);
        t.Advance(24);
        Check(t.Staff.Count == 0 && t.RoleLocked(StaffRole.Cooling) == "msg_needs_hr", "hiring needs HR");
        Check(GameState.ShiftAt(10) == Shift.Day && GameState.ShiftAt(16) == Shift.Evening && GameState.ShiftAt(2) == Shift.Night && GameState.ShiftAt(24 + 23.5) == Shift.Night, "shift times");
    }

    static void FaultTests()
    {
        var s = Quiet();
        s.Advance(24);
        s.Hours = Math.Floor(s.Hours / 24) * 24 + 9; // day shift
        var crah = s.Objects.First(o => o.Kind == ObjKind.Crah);
        var cool = s.Infra.CrahKw;
        s.Break(crah);
        Check(crah.Broken && s.Infra.CrahKw < cool, "a broken CRAH stops cooling");
        Check(!crah.Noticed || s.Working(StaffRole.Noc).Any(), "without a NOC operator nobody notices at once");
        s.Advance(2.5);
        Check(crah.Noticed, "after two hours someone notices");
        Check(crah.RepairBy > 0 && s.Staff.First(e => e.Id == crah.RepairBy).Role == StaffRole.Cooling, "a cooling technician on duty picks it up");
        s.Advance(48);
        Check(!crah.Broken && s.Infra.CrahKw >= cool - 1e-6, "and repairs it");
        Check(s.Incidents.Any(l => l.Key == "ev_fault") && s.Incidents.Any(l => l.Key == "ev_repaired"), "fault and repair are in the log");
        // a broken rack is not working for its customer
        var deal = s.Deals.First(d => d.State == DealState.Active && d.RackIds.Count > 0);
        var rack = s.Objects.First(o => o.Id == deal.RackIds[0]);
        s.Break(rack);
        Check(!s.WorkingRacks().Contains(rack.Id), "a broken rack does not serve its customer");
        // without anyone for the job a contractor comes
        var t = Quiet();
        t.Hours = 9;
        var money = t.Money;
        var router = t.Objects.First(o => o.Kind == ObjKind.CoreRouter);
        t.Execs.RemoveAll(x => x.Role == Exec.Hr);
        t.Break(router);
        t.Advance(3);
        Check(router.RepairBy == -1 && t.Money < money, "nobody of your own: a contractor is called and paid");
        t.Advance(Staffing.ContractorDelay + Staffing.RepairHours(ObjKind.CoreRouter) + 1);
        Check(!router.Broken, "the contractor repairs it after a delay");
        // calling a contractor yourself
        var o2 = t.Objects.First(o => o.Kind == ObjKind.Ups);
        t.Break(o2);
        Check(t.CallContractor(o2) == null && o2.RepairBy == -1 && o2.Noticed, "you can call a contractor straight away");
        // creative mode without breakdowns
        var c = Quiet();
        c.Options.Creative = true; c.Options.CreativeOptions = new CreativeOptions { NoBreakdowns = true };
        var any = c.Objects.First(o => o.Kind == ObjKind.Crah);
        c.Break(any);
        Check(!any.Broken, "no breakdowns in creative mode when switched off");
    }

    static void MaintenanceTests()
    {
        var s = Quiet();
        s.Execs.RemoveAll(x => x.Role == Exec.Hr);
        var crahs = s.Objects.Where(o => o.Kind == ObjKind.Crah).ToList();
        for (var i = 0; i < 30; i++) s.Advance(24);
        var unmaintained = crahs.Average(o => o.Wear);
        Check(unmaintained > 8, $"without technicians equipment wears ({unmaintained:0.0})");
        var t = Quiet();
        t.MaintenancePolicy = 2;
        for (var i = 0; i < 30; i++) t.Advance(24);
        var maintained = t.Objects.Where(o => o.Kind == ObjKind.Crah).Average(o => o.Wear);
        Check(maintained < unmaintained / 2, $"technicians keep it in shape ({maintained:0.0})");
        var month = t.Books.Where(b => b.Lines.ContainsKey("cost_maintenance")).Sum(b => b.Lines["cost_maintenance"]);
        Check(month < 0, "maintenance costs money");
        // spare parts are ordered and used
        Check(t.NeedsPart(Part.Cooling) && !t.NeedsPart(Part.Server) == false, "parts for the equipment you have");
        t.PartStock.TryGetValue(Part.Cooling, out var stock);
        Check(stock >= t.MinParts(Part.Cooling), "stock is reordered up to the minimum");
        t.Hours = Math.Floor(t.Hours / 24) * 24 + 9;
        var crah = t.Objects.First(o => o.Kind == ObjKind.Crah);
        t.Break(crah); t.Advance(2.5);
        t.PartStock.TryGetValue(Part.Cooling, out var after);
        Check(after == stock - 1 && crah.PartUsed, "a repair uses a spare part");
        t.PartStock[Part.Battery] = 0;
        var ups = t.Objects.First(o => o.Kind == ObjKind.Ups);
        t.Break(ups); t.Advance(2.5);
        Check(!ups.PartUsed, "no part in stock: the repair takes longer");
        // aged batteries
        var bat = t.Objects.First(o => o.Kind == ObjKind.Battery);
        bat.Installed = t.Day - Staffing.LifeDays - 300;
        t.Advance(24);
        Check(t.Aged(bat) && bat.Wear >= 40, "batteries past their life keep wearing");
        Check(t.Replace(bat) == null && bat.Wear == 0 && !t.Aged(bat), "replacing makes them new");
    }

    static void EventTests()
    {
        var s = Quiet();
        var inf = s.Infra;
        var running = inf.RunningRacks;
        Check(inf.Generators >= 2 && inf.Ats, "the demo has generators behind an ATS");
        s.StartEvent(EventKind.CityOutage, 4);
        Check(s.GridDown && s.Infra.OnGenerators && s.Infra.RunningRacks == running, "a city power cut: generators take over");
        s.Advance(5);
        Check(!s.GridDown && s.Incidents.Any(l => l.Key == "ev_outage_over"), "the power comes back");
        // without generators the site goes dark
        var t = Quiet();
        t.Objects.RemoveAll(o => o.Kind == ObjKind.Generator); t.Touch();
        var sat = t.Deals.Where(d => d.State == DealState.Active && !d.Legacy).Average(d => d.Satisfaction);
        t.StartEvent(EventKind.CityOutage, 3);
        Check(t.Infra.Blackout && t.Infra.RunningRacks == 0, "no generators: a blackout");
        t.Advance(3.5);
        Check(t.Deals.Where(d => d.State == DealState.Active && !d.Legacy).Average(d => d.Satisfaction) < sat, "customers notice a blackout");
        // long outage: diesel runs out without a generator technician
        var u = Quiet();
        u.StartEvent(EventKind.CityOutage, 100000);
        var fuel = u.Infra.FuelHours;
        u.Hours += fuel + 1; u.Touch();
        Check(u.Infra.Blackout, "the diesel runs out in a very long outage");
        // fire alarm
        var f = Quiet();
        var hall = f.Rooms.First(r => r.Type == RoomType.DataHall);
        f.StartEvent(EventKind.FireAlarm, 4, hall.Id);
        Check(f.Infra.Halls.First(h => h.Room == hall).RunningRacks == 0, "gas extinguishing: the hall is down");
        f.Advance(5);
        Check(f.Infra.Halls.First(h => h.Room == hall).RunningRacks > 0, "and runs again after the reset");
        // heatwave lowers heat rejection
        var h = Quiet();
        var reject = h.Infra.RejectKw;
        h.StartEvent(EventKind.Heatwave, 48);
        Check(Math.Abs(h.Infra.RejectKw - reject * 0.8) < 1e-6, "a heatwave: 20% less heat rejection");
        // random events happen over a year when switched on, never when off
        var on = Quiet(); on.Seed = 77; on.Options.Events = true;
        var off = Quiet(); off.Seed = 77;
        for (var i = 0; i < 365; i++) { on.Advance(24); off.Advance(24); }
        Check(on.Incidents.Any(l => l.Key is "ev_outage" or "ev_fire_gas" or "ev_fire_false" or "ev_press_good" or "ev_press_bad" or "ev_cable_cut" or "ev_bankrupt"), "events happen in a year");
        Check(!off.Incidents.Any(l => l.Key is "ev_outage" or "ev_fire_gas" or "ev_fire_false" or "ev_press_good" or "ev_press_bad" or "ev_cable_cut" or "ev_bankrupt"), "no events when switched off");
        // visits: busy by day, quiet at night
        Check(Simulation.HourWeight(10) > Simulation.HourWeight(20) && Simulation.HourWeight(20) > Simulation.HourWeight(3) && Simulation.HourWeight(3) > 0, "customers also come at night, but fewer");
    }

    // ---------------- step 8: security, research and reputation ----------------
    /// <summary>A quiet demo site with an R&D lab of two benches upstairs in the office.</summary>
    static (GameState s, Room lab) LabSite()
    {
        var s = Quiet();
        return (s, s.Rooms.FirstOrDefault(r => r.Type == RoomType.Lab));
    }

    static void ResearchTests()
    {
        var (s, lab) = LabSite();
        Check(lab != null && s.LabBenches == 2, "an R&D lab with two lab benches");
        Check(s.CanResearch("pwr_lithium") == "msg_needs_research_mgr", "research needs an R&D manager");
        Check(s.RoleLocked(StaffRole.Researcher) == "msg_needs_research_mgr", "researchers need an R&D manager");
        s.Execs.Add(new ExecHire { Role = Exec.Research });
        for (var i = 0; i < 3; i++) s.Advance(24);
        Check(s.CountOf(StaffRole.Researcher) == 2 && s.ResearchPerDay > 1, $"the R&D manager hires a researcher per bench ({s.ResearchPerDay:0.0} points/day)");
        Check(s.CanResearch("pwr_ups_eco") == "msg_research_needs", "a project needs the one before it");
        var money = s.Money;
        Check(s.QueueResearch("pwr_lithium") == null && s.Money == money - ResearchTree.ById["pwr_lithium"].Cost, "queueing research costs its money");
        Check(s.CanResearch("pwr_ups_eco") == null, "the next one can wait in the queue behind it");
        Check(s.CanResearch("pwr_lithium") == "msg_nothing_changed", "not twice");
        s.QueueResearch("bld_prefab");
        var m2 = s.Money;
        s.CancelResearch("bld_prefab");
        Check(s.Money == m2 + ResearchTree.ById["bld_prefab"].Cost && !s.ResearchQueue.Contains("bld_prefab"), "cancelling gives the money back");
        var kwh = s.Infra.BatteryKwh;
        for (var i = 0; i < 80 && !s.Researched.Contains("pwr_lithium"); i++) s.Advance(24);
        Check(s.Researched.Contains("pwr_lithium") && s.ResearchQueue.Count == 0 && s.Notes.Any(n => n.key == "msg_research_done"), "researchers finish the project");
        Check(Math.Abs(s.Infra.BatteryKwh - kwh * 1.5) < 1e-6, "lithium batteries hold 50% more");
        // eras: some technology only arrives in later years
        s.Researched.AddRange(new[] { "pwr_ups_eco", "pwr_busbar" });
        Check(s.CanResearch("pwr_peak") == "msg_research_year", "peak shaving only from 2028");
        s.Hours = (new DateTime(2028, 1, 2) - GameState.Epoch).TotalHours;
        Check(s.CanResearch("pwr_peak") == null, "and then it can start");
        // unlocks
        Check(s.PlaceFence(0, 40, 10, 1, FenceType.Electric, out _) == "msg_needs_research", "electric fence needs research");
        s.Researched.Add("sec_electric");
        Check(s.PlaceFence(0, 40, 10, 1, FenceType.Electric, out _) != "msg_needs_research", "and is unlocked by it");
        Check(s.MaxRowLength == 24, "rows of 24 racks");
        s.Researched.Add("bld_rows");
        Check(s.MaxRowLength == 48 && GameState.RowCells(0, 0, 60, 0, ObjKind.Rack, 0, s.MaxRowLength).Count == 48, "long rows after research");
        var pue = s.Infra.Pue;
        s.Researched.Add("cool_free"); s.InvalidateInfra();
        Check(Math.Abs(s.Infra.Pue - (pue - 0.08)) < 1e-6, "free cooling lowers the PUE");
        // AI customers only after liquid cooling
        var rng = new Random(9);
        s.Reputation = 70;
        Check(Enumerable.Range(0, 300).All(_ => s.MakeLead(1, rng).Type != CustomerType.Ai), "no AI customers without liquid cooling");
        s.Researched.Add("cool_liquid");
        Check(Enumerable.Range(0, 300).Any(_ => s.MakeLead(1, rng).Type == CustomerType.Ai), "liquid cooling brings AI customers");
        // waste heat is sold
        s.Researched.Add("cool_heat");
        Check(s.HeatIncomePerMonth > 0, "waste heat earns money");
        // office rental with an office manager
        Check(s.CanResearch("biz_office_rent") is "msg_needs_office_mgr" or "msg_research_needs", "office rental needs an office manager");
        s.Researched.AddRange(new[] { "biz_crm", "biz_office_rent" });
        Check(s.OfficeRentPerMonth == 0, "no rent without an office manager");
        s.Execs.Add(new ExecHire { Role = Exec.OfficeManager });
        var empty = s.AddRoom(RoomType.Office, 38, 18, 8, 6, 1);
        if (empty != null) empty.Stage = Stage.Done;
        Check(s.RentableOffices.Any(), "the empty office can be let");
        for (var d = 0; d < 120 && s.OfficeRentPerMonth == 0; d++) s.Advance(24);
        Check(s.OfficeRentPerMonth > 0, "empty offices are let out");
        // creative mode with all unlocks has everything researched
        var c = Quiet(); c.Options.Creative = true; c.Options.CreativeOptions = new CreativeOptions { AllUnlocks = true };
        Check(c.Done("sec_mantrap") && c.MaxRowLength == 48, "all unlocks: everything researched");
        // save roundtrip
        s.QueueResearch("net_optics");
        SaveSystem.Save(s, "unit-test-research");
        var l = SaveSystem.Load("unit-test-research");
        Check(l != null && l.Researched.Contains("pwr_lithium") && l.ResearchQueue.Contains("net_optics"), "save roundtrip research");
    }

    static void SecurityTests()
    {
        var s = Quiet();
        var lvl = s.SecurityLevel;
        Check(lvl > 0 && lvl < 60, $"the demo has some security: fence, barrier, booth, badge doors ({lvl})");
        var lines = s.SecurityLines();
        Check(lines.Sum(l => l.Max) >= 100 && lines.All(l => l.Points <= l.Max), "security lines add up");
        foreach (var x in new[] { 8, 14, 20, 24 }) { var cam = s.AddObject(ObjKind.CameraPole, x, 50, 0, 0); if (cam != null) cam.Status = ObjStatus.Live; }
        var dome = s.AddObject(ObjKind.CameraDome, 11, 15, 0, 0);
        Check(dome != null, "a dome camera hangs in the hall");
        Check(s.SecurityLevel > lvl, "cameras raise the security level");
        lvl = s.SecurityLevel;
        Check(s.AddObject(ObjKind.SecurityWall, 40, 22, 0, 0) == null, "the video wall needs a security room");
        Check(s.AddObject(ObjKind.GlassLodge, 42, 23, 0, 0) != null && s.SecurityLevel > lvl, "a glass lodge in the reception");
        // the mantrap door needs research
        var door = new Door { Type = DoorType.Mantrap, X = 44, Z = 26, Dir = 1, Floor = 0 };
        Check(s.CanDoor(door) == "msg_needs_research", $"mantrap needs research ({s.CanDoor(door)})");
        s.Researched.Add("sec_mantrap");
        Check(s.CanDoor(door) == null && s.AddDoor(DoorType.Mantrap, 44, 26, 1, 0) != null, "and can be built after it");
        // policies need guards
        s.Execs.Add(new ExecHire { Role = Exec.Security });
        var guards = s.Target(StaffRole.Guard);
        lvl = s.SecurityLevel;
        s.SetPolicy(0, true); s.SetPolicy(1, true);
        Check(s.Target(StaffRole.Guard) >= guards + 3 && s.SecurityLevel >= lvl + 7, "ID check and escort policies: more guards, higher level");
        // customers that need a clearance check the level
        var gov = s.MakeLead(1, new Random(3), CustomerType.Government);
        var req = s.Check(gov).First(r => r.Key == "req_security");
        Check(req.Need == GameState.SecurityNeed(CustomerType.Government).ToString(), "ministries ask for a security level");
        Check(s.Check(gov).Any(r => r.Key == "req_cert_gov" && r.Status == ReqStatus.Fail), "ministries need the government seal");
        var muni = s.MakeLead(1, new Random(3), CustomerType.Municipality);
        Check(s.Check(muni).Any(r => r.Key == "req_cert_iso27001" && r.Status == ReqStatus.Warn), "municipalities like ISO 27001 but do not require it");
        // the escort policy shows a guard walking along
        for (var i = 0; i < 3; i++) s.Advance(24);
        s.Hours = Math.Floor(s.Hours / 24) * 24 + 10;
        var sim = new Simulation(s);
        var rack = s.Objects.First(o => o.Id == s.Deals.First(d => d.State == DealState.Active && d.RackIds.Count > 0).RackIds[0]);
        Check(s.Working(StaffRole.Guard).Any(), "a guard is on duty");
        var v = sim.StartVisit(rack);
        Check(v != null && sim.Agents.Any(a => a.Escort && a.Target == v.Id), "a guard walks along with the visitor");
        for (var i = 0; i < 600 && sim.Agents.Any(a => a.Escort); i++) sim.Tick(0.05);
        Check(!sim.Agents.Any(a => a.Escort), "and leaves again afterwards");
        // office hours: no visitors at night
        s.SetPolicy(2, true);
        s.Hours = Math.Floor(s.Hours / 24) * 24 + 23;
        var before = sim.Agents.Count(a => a.Role == Role.Visitor);
        for (var i = 0; i < 24 * 5; i++) sim.Tick(0.05);
        Check(sim.Agents.Count(a => a.Role == Role.Visitor) <= before, "no visits outside office hours");
        SaveSystem.Save(s, "unit-test-security");
        var l = SaveSystem.Load("unit-test-security");
        Check(l != null && l.PolicyIdCheck && l.PolicyEscort && l.PolicyOfficeHours, "save roundtrip policies");
    }

    static void CertTests()
    {
        var s = Quiet();
        Check(s.CanAudit(CertKind.Iso27001) == "msg_needs_compliance", "audits need a compliance manager");
        s.Execs.Add(new ExecHire { Role = Exec.Compliance });
        var money = s.Money;
        Check(s.RequestAudit(CertKind.Iso27001) == null && s.Money == money - CertCatalog.Defs[CertKind.Iso27001].Cost, "an audit costs money");
        Check(s.CanAudit(CertKind.Iso27001) == "msg_audit_busy", "one audit at a time");
        for (var i = 0; i < 31; i++) s.Advance(24);
        Check(!s.HasCert(CertKind.Iso27001) && s.Notes.Any(n => n.key == "msg_cert_failed"), "without a security manager and ID checks the audit fails");
        s.Execs.Add(new ExecHire { Role = Exec.Security });
        s.SetPolicy(0, true);
        foreach (var x in new[] { 8, 14, 20, 24 }) { var cam = s.AddObject(ObjKind.CameraPole, x, 50, 0, 0); if (cam != null) cam.Status = ObjStatus.Live; }
        s.AddObject(ObjKind.CameraDome, 11, 15, 0, 0);
        s.AddObject(ObjKind.CameraDome, 30, 24, 0, 0);
        s.AddObject(ObjKind.GlassLodge, 42, 23, 0, 0);
        for (var i = 0; i < 4; i++) s.AddObject(ObjKind.Bollard, 34 + i, 52, 0, 0);
        s.Advance(48);
        Check(s.PassesAudit(CertKind.Iso27001), $"the site is ready for ISO 27001 (level {s.SecurityLevel})");
        var rep = s.Reputation;
        s.RequestAudit(CertKind.Iso27001);
        for (var i = 0; i < 31; i++) s.Advance(24);
        Check(s.HasCert(CertKind.Iso27001) && s.Reputation > rep, "certificate passed: reputation up");
        // a municipality now has everything it wants
        var muni = s.MakeLead(1, new Random(3), CustomerType.Municipality);
        Check(s.Check(muni).All(r => r.Status == ReqStatus.Ok || r.Key == "req_racks"), "municipality: security and ISO 27001 ok");
        // the yearly check withdraws a certificate when the site no longer complies
        s.SetPolicy(0, false);
        var c = s.Cert(CertKind.Iso27001);
        c.CheckDay = s.Day + 0.5;
        s.Advance(24);
        Check(!s.HasCert(CertKind.Iso27001) && s.Notes.Any(n => n.key == "msg_cert_withdrawn"), "the yearly check withdraws it when policies lapse");
        // Tier III and the green label check the technical site
        Check(s.CertChecks(CertKind.TierIII).Count >= 4, "Tier III checks UPS, generators, A/B power and carriers");
        Check(s.CertChecks(CertKind.Green).Any(r => r.Key == "cert_req_green_energy" && r.Status == ReqStatus.Fail), "the green label wants green energy");
        SaveSystem.Save(s, "unit-test-certs");
        Check(SaveSystem.Load("unit-test-certs") != null, "save roundtrip certificates");
    }

    static void ReputationTests()
    {
        var s = Quiet();
        for (var i = 0; i < 20; i++) s.Advance(24);
        Check(s.Quality.Count == 4 && s.Quality.Values.All(q => q >= 0 && q <= 100), "quality per topic between 0 and 100");
        Check(s.Reviews.Count > 0, $"customers write reviews ({s.Reviews.Count} in 20 days)");
        var rep = s.Reputation;
        s.AddReview(s.Day, "Test", ReviewTopic.Power, 5);
        Check(s.Reputation > rep, "a good review lifts the reputation");
        rep = s.Reputation;
        s.AddReview(s.Day, "Test", ReviewTopic.Power, 1);
        Check(s.Reputation < rep, "a bad review lowers it");
        // marketing makes good reviews count more and brings leads
        var lead = s.LeadFactor;
        s.Execs.Add(new ExecHire { Role = Exec.Marketing });
        Check(s.LeadFactor > lead, "a marketing manager brings more requests");
        rep = s.Reputation;
        s.AddReview(s.Day, "Test", ReviewTopic.Support, 5);
        Check(s.Reputation - rep > 1.2 - 1e-9, "and good reviews count more");
        // proposals from managers
        var p = s.MakeProposal((int)s.Day, new Random(1), ProposalKind.CrahFans);
        Check(p != null && s.OpenProposals.Contains(p) && p.Cost > 0 && p.Brand != "", "the facility manager proposes new CRAH fans");
        var crah = s.Infra.CrahKw;
        Check(s.AcceptProposal(p) == null && s.Infra.CrahKw > crah, "accepting gives the CRAH units more capacity");
        var q = s.MakeProposal((int)s.Day, new Random(2), ProposalKind.Router);
        var day = s.Day;
        for (var i = 0; i < 21; i++) s.Advance(24);
        Check(!s.OpenProposals.Contains(q), "unanswered proposals expire");
        Check(s.Bonus(ProposalKind.CrahFans) <= Proposals.Cap(ProposalKind.CrahFans), "bonuses are capped");
        // a broken power chain hurts the power rating
        var t = Quiet();
        t.Advance(24);
        var good = t.QualityTarget(ReviewTopic.Power);
        foreach (var d in t.Deals.Where(d => d.State == DealState.Active)) d.Served = 0.5;
        Check(t.QualityTarget(ReviewTopic.Power) < good - 30, "outages hurt the power rating");
        SaveSystem.Save(s, "unit-test-reputation");
        var l = SaveSystem.Load("unit-test-reputation");
        Check(l != null && l.Reviews.Count == s.Reviews.Count && l.Upgrades.Count == 1, "save roundtrip reviews and proposals");
        _ = day;
    }

    static void ResearchMigrationTests()
    {
        var s = Quiet();
        s.Rooms.First(r => r.Type == RoomType.DataHall).Containment = Containment.ColdCorridor;
        s.Researched.Clear(); s.Certs = null; s.Reviews = null;
        s.SaveFormat = 6;
        s.Migrate();
        Check(s.Researched.Contains("cool_corridor") && s.Certs != null && s.Reviews != null && s.SaveFormat == GameState.CurrentFormat, "old games keep the cold corridor they built");
        Check(s.Notes.Any(n => n.key == "msg_research_new"), "and hear about the new features");
    }

    static void StaffMigrationTests()
    {
        var s = Demo.Build();
        s.SaveFormat = 5;
        foreach (var o in s.Objects) o.Installed = 0;
        s.Staff = null; s.ActiveEvents = null; s.PartStock = null;
        s.Hours = 24 * 400;
        s.Migrate();
        Check(s.Staff != null && s.ActiveEvents != null && s.PartStock != null, "older saves get empty staff and events");
        Check(s.Objects.All(o => Math.Abs(o.Installed - 400) < 1e-6), "existing equipment counts as installed on load");
        Check(s.Notes.Any(n => n.key == "msg_staff_new"), "players are told about staff");
    }

    static void MarketTests()
    {
        var s = Quiet();
        Check(s.Competitors.Count(c => !c.Gone) == s.Options.Competitors, "a new game has the chosen number of rivals");
        var a = new Random(5); var b = new Random(5);
        Check(Math.Abs(s.RivalBid(a, out var w1) - s.RivalBid(b, out var w2)) < 1e-9 && w1 == w2, "rival bids are deterministic");
        for (var d = 0; d < 60; d++) s.Advance(24);
        Check(s.MarketShare >= 0 && s.MarketShare <= 1, $"market share is a fraction ({s.MarketShare:0.000})");
        Check(s.News.Count <= Market.MaxNews, "the news list stays short");
        var n = s.News.Count;
        s.Options.News = false; s.AddNews("news_tech_0");
        Check(s.News.Count == n, "no news when the ticker is off");
        var off = Quiet(); off.Options.AiCompetitors = false;
        Check(off.RivalBid(new Random(1), out _) >= 0, "rival bids work without AI rivals");
        Check(s.CanIpo() != null, "a small company cannot go public");
    }

    static void LandTests()
    {
        var s = Quiet();
        s.Money = 100_000_000;
        var size = s.PlotSize;
        var o = s.Objects.First();
        var (x, z) = (o.X, o.Z);
        var pit = s.StreetPit(0);
        Check(s.NextLandSize > size && s.NextLandCost > 0, "bigger land is for sale");
        Check(s.BuyLand() == null && s.PlotSize > size, "buying land grows the plot");
        var d = s.PlotSize - size;
        Check(o.X == x + d && o.Z == z + d, "everything keeps its place by the road");
        Check(s.StreetPit(0) == pit + d && s.LandRevision > 0, "street pits move along");
        Check(s.Surfaces.Length == s.PlotSize * s.PlotSize, "the ground grows too");
        s.InvalidateInfra();
        Check(s.Infra != null, "the infra still works after growing");
    }

    static void SiteTests()
    {
        var s = Quiet(); Unlock(s);
        s.Money = 500_000_000;
        Check(s.CanBuildSite("de", 0) == "msg_needs_country", "other countries open first");
        Check(s.BuildSite("nl", 0) == null && s.Sites.Count == 1, "a new location is started");
        Check(s.OpenCountry("de") == null && s.CountryOpen("de"), "a country can be opened");
        var site = s.Sites[0];
        for (var d = 0; d < 260 && site.Occupied < 1; d++) s.Advance(24);
        Check(site.Built && site.Occupied > 0, $"the location is built and fills ({site.Occupied}/{site.Racks})");
        Check(s.CanUpgradeSite(site) != "msg_nothing_changed", "a location can grow");
        Check(s.MySoldMw > 0, "the location counts in the market");
        var before = s.Money;
        Check(s.SellSite(site) == null && s.Sites.Count == 0 && s.Money > before, "a location can be sold");
    }

    static void OfficeLeaseTests()
    {
        var s = Quiet(); Unlock(s);
        s.Execs.Add(new ExecHire { Role = Exec.OfficeManager });
        var r = s.AddRoom(RoomType.Office, 38, 18, 8, 6, 1);
        if (r != null) r.Stage = Stage.Done;
        Check(s.CanLetToCustomers, "the office manager can let offices to customers");
        s.SetTenantPolicy(1);
        for (var d = 0; d < 200 && s.Leases.Count == 0; d++) s.Advance(24);
        Check(s.Leases.Count > 0 && s.Leases.All(l => l.DealId != 0), "with 'only customers' the offices go to datacenter customers");
        var l = s.Leases[0];
        var room = s.Rooms.First(x => x.Id == l.RoomId);
        Check(l.Monthly >= s.Rent(room, false), "customers pay a premium for an office near their racks");
        s.EndLease(room);
        Check(s.LeaseOf(room) == null, "a lease can be ended");
    }

    static void HostingTests()
    {
        var s = Quiet(); Unlock(s);
        s.Money = 100_000_000;
        Check(s.OrderServers(10) == "msg_needs_hosting", "no servers without a hosting company");
        Check(s.StartHosting("Test Cloud") == null && s.Hosting.Started, "the hosting company starts");
        foreach (var h in s.Rooms.Where(x => x.Type == RoomType.DataHall)) h.Zone = HallZone.OwnCloud;
        Check(s.CloudRacksBuilt > 0, "racks in own cloud halls host servers");
        Check(s.OrderServers(30) == null && s.Hosting.Orders.Count == 1, "servers are ordered");
        for (var d = 0; d < 40; d++) s.Advance(24);
        Check(s.Hosting.TotalServers + s.Hosting.Stock > 0, $"servers arrive, get prepared and installed ({s.Hosting.TotalServers} live, {s.Hosting.Stock} in stock)");
        Check(s.Hosting.TotalCustomers > 0 && s.HostingMonthly > 0, "customers sign up and pay");
        s.SetHostPrice(HostProduct.Vps, 2);
        Check(s.HostPrice(HostProduct.Vps) > HostingCatalog.Def(HostProduct.Vps).Price, "a premium price is higher");
        Check(s.HostingEngineersWanted >= 1, "the hosting team asks for engineers");
    }

    static void EnergyTests()
    {
        var s = Quiet();
        s.Options.DynamicPrices = true;
        Check(s.SpotPrice(3) < s.SpotPrice(18), "night power is cheaper than the evening peak");
        s.Options.DynamicPrices = false;
        Check(Math.Abs(s.SpotPrice(3) - s.SpotPrice(18)) < 1e-9, "a flat price without dynamic prices");
        Check(s.Esg >= 0 && s.Esg <= 100 && s.EsgLines().Sum(l => l.Max) == 100, $"ESG is a score out of 100 ({s.Esg:0})");
        Check(s.WaterM3PerDay >= 0, "water use is known");
        Check(s.BatteryProfit() == 0, "no battery, no trading profit");
        Check(s.SolarKwh(6) == 0 && s.RenewableShare >= 0, "no panels, no solar power");
    }

    static void RiskTests()
    {
        var s = Quiet();
        s.Money = 10_000_000;
        s.SetInsurance(0); var m0 = s.Money; s.Damage(200_000); var lost0 = m0 - s.Money;
        s.SetInsurance(2); var m2 = s.Money; s.Damage(200_000); var lost2 = m2 - s.Money;
        Check(lost0 == 200_000 && lost2 < lost0, $"insurance pays out ({lost2} instead of {lost0})");
        Check(s.InsurancePerMonth > 0, "insurance costs a premium");
        var before = s.DamageThisYear;
        s.Disaster("storm", new Random(3));
        Check(s.DamageThisYear > before, "a storm causes damage");
        s.Options.SupplyDelays = true; s.SupplyUntil = s.Day + 20; s.SupplyKind = 0; s.SupplyDays = 10;
        Check(s.SupplyCrunch && s.SupplyDelay(ObjKind.Rack) == 10 && s.SupplyDelay(ObjKind.Ups) == 0, "a chip shortage delays racks, not power");
        s.Options.SupplyDelays = false;
        Check(!s.SupplyCrunch, "no delays when switched off");
        Check(s.SocLevel >= 0, "the SOC level is known");
    }

    static void KeyPeopleTests()
    {
        var s = Quiet(); Unlock(s);
        s.Money = 10_000_000;
        var k = new KeyPerson { Id = s.NextId++, Name = "Test Person", Role = KeyRole.Architect, Salary = 11000, Candidate = true, ExpiresDay = s.Day + 20 };
        s.KeyPeople.Add(k);
        Check(s.HireKey(k) == null && s.Has(KeyRole.Architect) && s.KeySalaries == 11000, "a key person is hired");
        k.PoachedBy = "Rival"; k.PoachDay = s.Day + 10;
        s.AnswerPoach(k, true);
        Check(k.PoachedBy == null && k.Salary > 11000, "matching an offer keeps the person at a higher salary");
        k.PoachedBy = "Rival";
        s.AnswerPoach(k, false);
        Check(!s.Has(KeyRole.Architect), "letting go means the person leaves");
    }

    static void GrowthMigrationTests()
    {
        var s = Quiet();
        s.Competitors = null; s.Sites = null; s.Hosting = null; s.KeyPeople = null; s.News = null;
        s.SaveFormat = 7;
        s.Migrate();
        Check(s.Competitors != null && s.Sites != null && s.Hosting != null && s.KeyPeople != null && s.SaveFormat == GameState.CurrentFormat, "old saves get the growth systems");
        Check(!s.Options.Disasters && !s.Options.SupplyDelays && !s.Options.DynamicPrices, "old saves are not surprised by disasters");
        Check(s.Notes.Any(n => n.key == "msg_growth_new"), "and hear about growth");
        // save roundtrip
        var t = Quiet(); Unlock(t); t.Money = 500_000_000;
        t.BuildSite("nl", 1); t.StartHosting(); t.SetInsurance(1);
        SaveSystem.Save(t, "unit-test-growth");
        var l = SaveSystem.Load("unit-test-growth");
        Check(l != null && l.Sites.Count == 1 && l.Hosting.Started && l.Insurance == 1 && l.Competitors.Count == t.Competitors.Count, "save roundtrip growth");
    }

    static void ClimateTests()
    {
        Check(Climate.SeasonOf(new DateTime(2027, 1, 15)) == Season.Winter && Climate.SeasonOf(new DateTime(2027, 7, 1)) == Season.Summer
            && Climate.SeasonOf(new DateTime(2027, 4, 1)) == Season.Spring && Climate.SeasonOf(new DateTime(2027, 10, 1)) == Season.Autumn, "months map to seasons");
        Check(Climate.DayLength(172) > 16 && Climate.DayLength(355) < 8.5, $"long summer days, short winter days ({Climate.DayLength(172):0.0} h / {Climate.DayLength(355):0.0} h)");
        Check(Climate.Daylight(new DateTime(2027, 6, 21, 13, 0, 0)) > 0.99 && Climate.Daylight(new DateTime(2027, 6, 21, 2, 0, 0)) < 0.01, "noon is light, the night is dark");
        Check(Climate.Daylight(new DateTime(2027, 12, 21, 7, 0, 0)) < 0.05 && Climate.Daylight(new DateTime(2027, 6, 21, 7, 0, 0)) > 0.9, "7 o'clock is dark in December and light in June");
        Check(Climate.SunElevation(new DateTime(2027, 6, 21, 13, 0, 0)) > Climate.SunElevation(new DateTime(2027, 12, 21, 13, 0, 0)) + 30, "the summer sun stands higher");
        var d = new DateTime(2027, 3, 3, 15, 30, 0);
        Check(Climate.At(d, 77) == Climate.At(d, 77), "the weather is the same when you look again (loading a save)");
        var kinds = new Dictionary<WeatherKind, int>();
        int snowOutside = 0, clearSummer = 0, clearWinter = 0;
        for (var h = 0; h < 24 * 365 * 2; h += 6)
        {
            var t = GameState.Epoch.AddHours(h);
            var w = Climate.At(t, 1234);
            kinds[w.Kind] = kinds.GetValueOrDefault(w.Kind) + 1;
            if (w.Kind == WeatherKind.Snow && t.Month is >= 5 and <= 9) snowOutside++;
            if (w.Kind == WeatherKind.Clear && w.Season == Season.Summer) clearSummer++;
            if (w.Kind == WeatherKind.Clear && w.Season == Season.Winter) clearWinter++;
            if (w.SnowCover > 0 && t.Month is >= 5 and <= 9) snowOutside++;
        }
        Check(Enum.GetValues<WeatherKind>().All(k => kinds.GetValueOrDefault(k) > 0), "two years bring every kind of weather: " + string.Join(", ", kinds.Select(k => $"{k.Key} {k.Value}")));
        Check(snowOutside == 0, "no snow and no snow cover in summer");
        Check(clearSummer > clearWinter, $"summer is sunnier than winter ({clearSummer} vs {clearWinter})");
        var cold = Climate.At(new DateTime(2027, 1, 20, 14, 0, 0), 5);
        var hot = Climate.At(new DateTime(2027, 1, 20, 14, 0, 0), 5, heatwave: true);
        Check(hot.Kind == WeatherKind.Clear && hot.TempC > cold.TempC + 10 && hot.SnowCover == 0, "a heatwave shows as a hot clear sky");
        Check(Climate.At(d, 5, flood: true).Kind == WeatherKind.Storm, "a flood comes with a storm");
        var s = Quiet();
        var before = s.Money; var rep = s.Reputation;
        var now = s.Weather;
        s.Advance(24 * 3);
        Check(now.Season == Climate.SeasonOf(s.Date.AddDays(-3)) && s.Weather.Daylight >= 0, "the game knows its weather");
        Check(Climate.SeedOf("A") != Climate.SeedOf("B") && Climate.SeedOf("A") == Climate.SeedOf("A"), "stable weather seeds");
        // weather effects (new-game switch)
        Check(GameState.CoolingFactorAt(2) < GameState.CoolingFactorAt(14) && GameState.CoolingFactorAt(14) < GameState.CoolingFactorAt(22) && GameState.CoolingFactorAt(22) < GameState.CoolingFactorAt(30), "colder weather means cheaper cooling");
        var e = Quiet();
        Check(e.CoolingWeatherFactor == 1 && e.SunFactor == 1 && e.TravelFactor == 1 && e.OutageWeatherFactor == 1, "without weather effects nothing changes");
        var pueOff = e.Infra.Pue;
        e.Options.WeatherEffects = true;
        e.Hours = (new DateTime(2027, 1, 20, 6, 0, 0) - GameState.Epoch).TotalHours; e.InvalidateInfra();
        var pueWinter = e.Infra.Pue;
        Check(e.Weather.TempC < 10 && pueWinter < pueOff, $"a cold winter morning lowers the PUE ({pueWinter:0.00} < {pueOff:0.00})");
        e.StartEvent(EventKind.Heatwave, 48); e.InvalidateInfra();
        Check(e.Infra.Pue > pueWinter && e.CoolingWeatherFactor > 1, "a heatwave makes cooling harder");
        Check(e.Forecast(3).Count == 3 && e.Forecast(3)[0].day.Date == e.Date.Date.AddDays(1), "a three-day weather report");
        var old = Quiet(); old.Options.WeatherEffects = true; old.SaveFormat = 8; old.Migrate();
        Check(!old.Options.WeatherEffects && old.SaveFormat == GameState.CurrentFormat, "older saves keep the weather for looks only");
        Check(!new NewGameOptions().WeatherEffects, "the weather plays along only when chosen in the new-game dialog");
    }

    static void MigrationTests()
    {
        var s = Quiet();
        var sim = new Simulation(s);
        sim.TrackMigrations();
        Check(Simulation.ConvoySize(4) == 1 && Simulation.ConvoySize(25) == 3 && Simulation.ConvoySize(200) == 4, "one moving truck per ten racks, at most four");
        var racks = s.Objects.Where(o => o.Kind == ObjKind.Rack && o.Status == ObjStatus.Live).Take(12).Select(o => o.Id).ToList();
        var d = new Deal { Id = s.NextId++, Product = Product.Rack, State = DealState.Active, Racks = 12, RackIds = racks.ToList(), Monthly = 1000, StartDay = s.Day, EndDay = s.Day + 900, Months = 30, Name = "Test Movers" };
        s.Deals.Add(d);
        sim.TrackMigrations();
        Check(sim.MoveQueue.Count(j => j.DealId == d.Id && !j.Out) == 2, "a big customer moves in with a convoy of two trucks");
        Check(sim.Messages.Any(m => m.key == "msg_moving_in"), "and you hear about it");
        sim.TrackMigrations();
        Check(sim.MoveQueue.Count(j => j.DealId == d.Id && !j.Out) == 2, "the convoy is planned only once");
        d.State = DealState.Ended; d.RackIds.Clear();
        sim.TrackMigrations();
        Check(sim.MoveQueue.Count(j => j.DealId == d.Id && j.Out) == 2 && sim.MoveQueue.Where(j => j.Out).All(j => j.Racks.Count == 12), "a leaving customer sends trucks to collect their servers");
        Check(sim.Messages.Any(m => m.key == "msg_moving_out"), "and you hear about that too");
        // run in daylight until a moving truck has arrived and its movers carry servers
        s.Hours = Math.Floor(s.Hours / 24) * 24 + 9;
        sim.MoveQueue.RemoveAll(j => j.DealId != d.Id);
        var sawTruck = false; var sawMover = false;
        for (var i = 0; i < 400 && !(sawTruck && sawMover); i++)
        {
            sim.Tick(0.05); s.Hours += 0.05;
            sawTruck |= sim.Agents.Any(a => a.Role == Role.Truck && a.Load == TruckLoad.Customer);
            sawMover |= sim.Agents.Any(a => a.Role == Role.Visitor && a.Carry > 0);
        }
        Check(sawTruck, "a moving truck drives to the dock");
        Check(sawMover, "movers walk between the truck and the racks");
        Check(d.State == DealState.Ended && s.Deals.Contains(d), "migrations are only for looks: the contract is unchanged");
    }

    static void WalkTests()
    {
        var s = Quiet();
        var nav = new Nav(s);
        var walls = s.WallEdges(0, true);
        var doorEdges = s.Doors.Where(x => x.Floor == 0).SelectMany(x => x.Edges()).ToHashSet();
        var wall = walls.Where(e => e.Item3 == 0 && !doorEdges.Contains(e)).FirstOrDefault(e => nav.Walkable(e.Item1 - 1, e.Item2, 0) && nav.Walkable(e.Item1, e.Item2, 0));
        Check(wall != default && !nav.CanStep(wall.Item1 - 1, wall.Item2, wall.Item1, wall.Item2, 0), "the walker cannot walk through a wall");
        var door = doorEdges.FirstOrDefault(e => e.dir == 0 && nav.Walkable(e.x - 1, e.z, 0) && nav.Walkable(e.x, e.z, 0));
        if (door == default)
        {
            var dz = doorEdges.First(e => e.dir == 1 && nav.Walkable(e.x, e.z - 1, 0) && nav.Walkable(e.x, e.z, 0));
            Check(nav.CanStep(dz.x, dz.z - 1, dz.x, dz.z, 0), "but walks through a door");
        }
        else Check(nav.CanStep(door.x - 1, door.z, door.x, door.z, 0), "but walks through a door");
        var rack = s.Objects.First(o => o.Kind == ObjKind.Rack && o.Status == ObjStatus.Live);
        Check(!nav.CanStep(rack.X, rack.Z - 1, rack.X, rack.Z, rack.Floor) && !nav.CanStep(rack.X, rack.Z + 1, rack.X, rack.Z, rack.Floor), "racks are solid");
        Check(nav.CanStep(-5, -5, -4, -5, 0) && !nav.CanStep(-5, -5, -4, -5, 1), "outside the plot you can roam the grass, but not on an upper floor");
        Check(!nav.CanStep(0, 0, 2, 0, 0), "one cell at a time");
    }
}
