using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DatacenterArchitect.Core;

/// <summary>
/// The player's profile over all savegames (step 11): achievements earned anywhere and the best medal per scenario.
/// Stored locally in user://profile.cfg, like the settings. A later Steam build can mirror this.
/// </summary>
public class Profile
{
    public const string FilePath = "user://profile.cfg";
    static Profile _instance;
    public static Profile Instance => _instance ??= Load(FilePath);

    readonly string _path;
    public readonly Dictionary<string, string> Achievements = new();
    public readonly Dictionary<string, int> Medals = new();

    Profile(string path) { _path = path; }

    public static Profile Load(string path)
    {
        var p = new Profile(path);
        var cfg = new ConfigFile();
        if (cfg.Load(path) != Error.Ok) return p;
        if (cfg.HasSection("achievements"))
            foreach (var k in cfg.GetSectionKeys("achievements")) p.Achievements[k] = cfg.GetValue("achievements", k).AsString();
        if (cfg.HasSection("medals"))
            foreach (var k in cfg.GetSectionKeys("medals")) p.Medals[k] = Mathf.Clamp(cfg.GetValue("medals", k).AsInt32(), 0, 3);
        return p;
    }

    public void Save()
    {
        var cfg = new ConfigFile();
        foreach (var (k, v) in Achievements) cfg.SetValue("achievements", k, v);
        foreach (var (k, v) in Medals) cfg.SetValue("medals", k, v);
        cfg.Save(_path);
    }

    public bool Has(string id) => Achievements.ContainsKey(id);

    /// <summary>Remember an achievement with the date it was first earned; returns true when it is new to the profile.</summary>
    public bool Earn(string id, string when)
    {
        if (Achievements.ContainsKey(id)) return false;
        Achievements[id] = when ?? "";
        Save();
        return true;
    }

    /// <summary>Keep the best medal of a scenario.</summary>
    public bool Medal(string scenario, int medal)
    {
        if (Medals.TryGetValue(scenario, out var m) && m >= medal) return false;
        Medals[scenario] = medal;
        Save();
        return true;
    }

    public int BestMedal(string scenario) => Medals.TryGetValue(scenario, out var m) ? m : 0;

    /// <summary>Copies what a game earned into the profile (for games loaded from an older version or another PC).</summary>
    public void Merge(IEnumerable<string> achieved, string scenario, int medal, string when)
    {
        var changed = false;
        foreach (var id in achieved ?? Enumerable.Empty<string>())
            if (!Achievements.ContainsKey(id)) { Achievements[id] = when ?? ""; changed = true; }
        if (scenario != null && medal > BestMedal(scenario)) { Medals[scenario] = medal; changed = true; }
        if (changed) Save();
    }
}
