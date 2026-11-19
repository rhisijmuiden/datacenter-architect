using System;
using System.Collections.Generic;
using System.Linq;
using DatacenterArchitect.Core;
using DatacenterArchitect.Sim;
using Godot;

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
            ("saves", SaveTests), ("building", BuildTests),
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
        var used = new[] { "menu_new_game", "info_check", "info_update_now", "channel_stable", "channel_test", "channel_dev", "msg_needs_foundation", "build_rack", "res_power", "new_ai" };
        Check(used.All(en.ContainsKey), "core keys present");
        TranslationServer.SetLocale("nl");
        Check(Loc.T("menu_new_game") != "menu_new_game", "Dutch lookup works");
        TranslationServer.SetLocale("en");
    }

    static void SaveTests()
    {
        var s = GameState.New(new NewGameOptions { Company = "Test BV", AiCompetitors = false, Difficulty = 2 });
        s.AddFoundation(3, 4); s.Objects.Add(new PlacedObject { Kind = BuildKind.Rack, X = 3, Z = 4 });
        s.Hours = 1234;
        SaveSystem.Save(s, "unit-test");
        var l = SaveSystem.Load("unit-test");
        Check(l != null && l.Options.Company == "Test BV" && !l.Options.AiCompetitors, "save roundtrip options");
        Check(l != null && l.HasFoundation(3, 4) && l.Objects.Count == 1 && Math.Abs(l.Hours - 1234) < 1e-6, "save roundtrip world");
        Check(SaveSystem.List().Any(x => x.Slot == "unit-test"), "save listed");
        for (var i = 0; i < 4; i++) SaveSystem.Autosave(s);
        Check(SaveSystem.List().Count(x => x.IsAutosave) == SaveSystem.AutosaveSlots, "autosave keeps three");
        foreach (var x in SaveSystem.List().Where(x => x.IsAutosave || x.Slot == "unit-test")) SaveSystem.Delete(x.Slot);
        Check(SaveSystem.Sanitize("../evil") == "___evil", "slot names sanitised");
    }

    static void BuildTests()
    {
        var s = GameState.New(new NewGameOptions());
        var rack = new PlacedObject { Kind = BuildKind.Rack, X = 5, Z = 5 };
        Check(s.CanPlace(rack) == "msg_needs_foundation", "rack needs foundation");
        s.AddFoundation(5, 5);
        Check(s.CanPlace(rack) == null, "rack on foundation");
        s.Objects.Add(rack);
        Check(s.CanPlace(new PlacedObject { Kind = BuildKind.Rack, X = 5, Z = 5 }) == "msg_occupied", "no overlap");
        Check(s.CanPlace(new PlacedObject { Kind = BuildKind.Generator, X = 62, Z = 10 }) == "msg_outside_plot", "inside plot");
        s.Objects.Add(new PlacedObject { Kind = BuildKind.Generator, X = 10, Z = 10 });
        Check(s.BackupCapacityKw == 0, "one generator gives no N+1 capacity");
        s.Objects.Add(new PlacedObject { Kind = BuildKind.Generator, X = 10, Z = 14 });
        Check(Math.Abs(s.BackupCapacityKw - 1000) < 1e-6, "two generators give N+1 1000 kW");
        Check(GameState.Footprint(new PlacedObject { Kind = BuildKind.Generator, Rot = 1 }) == (2, 3), "rotation swaps footprint");
    }
}
