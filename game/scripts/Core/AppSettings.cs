using Godot;

namespace DatacenterArchitect.Core;

/// <summary>Player settings stored in user://settings.cfg.</summary>
public class AppSettings
{
    const string Path = "user://settings.cfg";
    static AppSettings _instance;
    public static AppSettings Instance => _instance ??= Load();

    public string Language = "en";
    /// <summary>Update channel the player follows: stable or test.</summary>
    public string UpdateChannel = "stable";
    public bool CheckUpdatesOnStart = true;
    public int AutosaveMinutes = 5;

    static AppSettings Load()
    {
        var s = new AppSettings();
        var cfg = new ConfigFile();
        if (cfg.Load(Path) == Error.Ok)
        {
            s.Language = (string)cfg.GetValue("general", "language", s.Language);
            s.UpdateChannel = (string)cfg.GetValue("updates", "channel", GameVersion.BuildChannel == "test" ? "test" : "stable");
            s.CheckUpdatesOnStart = (bool)cfg.GetValue("updates", "check_on_start", s.CheckUpdatesOnStart);
            s.AutosaveMinutes = (int)cfg.GetValue("saves", "autosave_minutes", s.AutosaveMinutes);
        }
        else if (GameVersion.BuildChannel == "test") s.UpdateChannel = "test";
        return s;
    }

    public void Save()
    {
        var cfg = new ConfigFile();
        cfg.SetValue("general", "language", Language);
        cfg.SetValue("updates", "channel", UpdateChannel);
        cfg.SetValue("updates", "check_on_start", CheckUpdatesOnStart);
        cfg.SetValue("saves", "autosave_minutes", AutosaveMinutes);
        cfg.Save(Path);
    }
}
