using System;
using System.Linq;
using DatacenterArchitect.Core;
using DatacenterArchitect.Sim;
using DatacenterArchitect.UI;
using Godot;

namespace DatacenterArchitect.Tests;

/// <summary>Step 11 round 3: company name and logo, music and area sound settings.</summary>
public static partial class TestRunner
{
    static void Round3Tests()
    {
        Check(CompanyLogo.InitialsOf("Polder Datacenters") == "PD" && CompanyLogo.InitialsOf("nordwind") == "NO" && CompanyLogo.InitialsOf("") == "DC", "initials come from the company name");
        Check(CompanyLogo.InitialsOf("  de  Kade-Hosting ") == "DK", "initials skip spaces and dashes");
        var a = CompanyLogo.For("Polder Datacenters"); var b = CompanyLogo.For("Polder Datacenters");
        Check(a.Shape == b.Shape && a.Primary == b.Primary && a.Secondary == b.Secondary, "the starting logo is the same for the same name");
        Check(Enumerable.Range(0, 40).Select(i => CompanyLogo.For("Company " + i)).Select(l => (l.Shape, l.Primary)).Distinct().Count() > 10, "different names get different logos");
        Check(CompanyLogo.Palette.All(h => h.StartsWith('#') && h.Length == 7), "palette colours are hex");
        var l = new CompanyLogo();
        Check(l.Letters("Polder Datacenters") == "PD", "letters follow the name");
        l.Initials = "abcd";
        Check(l.Letters("Polder Datacenters") == "ABC", "own letters: upper case, at most 3");
        var v = l.Version; l.Primary = "#38c6f4";
        Check(l.Version != v, "a changed colour redraws the logo");
        v = l.Version; l.OnRacks = false;
        Check(l.Version != v, "switching racks off redraws them");

        var s = GameState.New(new NewGameOptions { Company = "Kade Hosting" });
        Check(s.Logo != null && s.Logo.Letters(s.Options.Company) == "KH", "a new game has a logo from its name");
        Check(s.Rename("X") == "msg_name_short" && s.Options.Company == "Kade Hosting", "too short names are refused");
        Check(s.Rename("  Grachtsoft Cloud ") == null && s.Options.Company == "Grachtsoft Cloud" && s.Logo.Letters(s.Options.Company) == "GC", "renaming changes the letters");
        s.Logo.Shape = 4; s.Logo.Primary = "#8a6cf0"; s.Logo.OnVehicles = false; s.Logo.Initials = "GC1";
        var back = System.Text.Json.JsonSerializer.Deserialize<GameState>(System.Text.Json.JsonSerializer.Serialize(s));
        Check(back.Logo.Shape == 4 && back.Logo.Primary == "#8a6cf0" && !back.Logo.OnVehicles && back.Logo.Initials == "GC1", "the logo survives a save");
        var old = System.Text.Json.JsonSerializer.Deserialize<GameState>("{\"Options\":{\"Company\":\"Delta Web\"}}");
        Check(old.Logo != null && old.Logo.Letters(old.Options.Company) == "DW", "older saves get a logo from their name");

        // audio: a music bus beside ambience and interface, with its own volume
        AudioMixer.EnsureBuses();
        Check(AudioServer.GetBusIndex(AudioMixer.Music) >= 0, "there is a music bus");
        var set = new AppSettings();
        Check(set.MusicOn && set.MusicVolume > 0 && set.MusicVolume < set.AmbienceVolume, "music is on by default, softer than the ambience");
        Check(Sections.All[1].Tabs.Any(t => t.Key == "co_tab_brand"), "the brand screen sits under Company");
    }
}
