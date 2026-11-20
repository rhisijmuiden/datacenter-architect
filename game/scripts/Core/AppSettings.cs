using Godot;

namespace DatacenterArchitect.Core;

/// <summary>Window modes offered in the Options menu.</summary>
public enum WindowModeSetting { Windowed, Borderless, Fullscreen }

/// <summary>Player settings stored in user://settings.cfg.</summary>
public class AppSettings
{
    public const string DefaultPath = "user://settings.cfg";
    static AppSettings _instance;
    public static AppSettings Instance => _instance ??= Load(DefaultPath);

    // general
    public string Language = "en";
    /// <summary>Update channel the player follows: stable or test.</summary>
    public string UpdateChannel = "stable";
    public bool CheckUpdatesOnStart = true;
    public int AutosaveMinutes = 5;

    // display
    public WindowModeSetting WindowMode = WindowModeSetting.Borderless;
    /// <summary>Chosen resolution; (0, 0) means the monitor's own resolution.</summary>
    public Vector2I Resolution = Vector2I.Zero;
    /// <summary>Monitor index, -1 = the one the game opens on.</summary>
    public int Monitor = -1;
    public bool VSync = true;
    /// <summary>Frame cap, 0 = unlimited.</summary>
    public int MaxFps = 0;
    /// <summary>Interface size on top of the automatic scaling, 0.75 to 1.5.</summary>
    public float UiScale = 0.75f;

    // graphics and visual effects
    public bool Shadows = true;
    public bool AmbientOcclusion = true;
    public bool Fog = true;
    /// <summary>3D anti-aliasing: 0 = off, 2, 4 or 8 samples.</summary>
    public int Msaa = 2;
    /// <summary>0 = standard, 1 = high (bounce light, SSIL), 2 = ultra (global illumination, SDFGI).</summary>
    public int Lighting = 1;
    /// <summary>Grass tufts and flowers on open land.</summary>
    public bool Grass = true;
    public bool BlinkingLights = true;
    public bool MovingParts = true;
    public bool SmoothCamera = true;
    /// <summary>Step 10 looks: day/night cycle, weather (rain, snow, fog, storms) and seasonal colours.</summary>
    public bool DayNight = true, Weather = true, Seasons = true;
    /// <summary>First-person walking: mouse sensitivity (percent) and invert the vertical look.</summary>
    public int MouseSensitivity = 100;
    public bool InvertMouse;

    // audio, volumes 0..100
    public int MasterVolume = 80;
    public int AmbienceVolume = 80;
    public int InterfaceVolume = 70;
    public bool AmbienceSound = true;
    /// <summary>Step 11: generative background music per area.</summary>
    public int MusicVolume = 45;
    public bool MusicOn = true;
    public bool ClickSounds = true;
    public bool NotificationSounds = true;
    public bool MuteInBackground = true;

    public static AppSettings Load(string path)
    {
        var s = new AppSettings();
        var cfg = new ConfigFile();
        if (cfg.Load(path) != Error.Ok)
        {
            if (GameVersion.BuildChannel == "test") s.UpdateChannel = "test";
            return s;
        }
        T Get<[MustBeVariant] T>(string section, string key, T fallback)
        {
            var v = cfg.GetValue(section, key, Variant.From(fallback));
            try { return v.As<T>(); } catch { return fallback; }
        }
        s.Language = Get("general", "language", s.Language);
        s.UpdateChannel = Get("updates", "channel", GameVersion.BuildChannel == "test" ? "test" : "stable");
        if (s.UpdateChannel is not ("stable" or "test")) s.UpdateChannel = GameVersion.BuildChannel == "test" ? "test" : "stable";
        s.CheckUpdatesOnStart = Get("updates", "check_on_start", s.CheckUpdatesOnStart);
        s.AutosaveMinutes = Mathf.Clamp(Get("saves", "autosave_minutes", s.AutosaveMinutes), 1, 60);

        s.WindowMode = (WindowModeSetting)Mathf.Clamp(Get("display", "window_mode", (int)s.WindowMode), 0, 2);
        s.Resolution = Get("display", "resolution", s.Resolution);
        if (s.Resolution.X < 0 || s.Resolution.Y < 0) s.Resolution = Vector2I.Zero;
        s.Monitor = Get("display", "monitor", s.Monitor);
        s.VSync = Get("display", "vsync", s.VSync);
        s.MaxFps = Mathf.Max(0, Get("display", "max_fps", s.MaxFps));
        s.UiScale = Mathf.Clamp(Get("display", "ui_scale", s.UiScale), 0.75f, 1.5f);

        s.Shadows = Get("graphics", "shadows", s.Shadows);
        s.AmbientOcclusion = Get("graphics", "ambient_occlusion", s.AmbientOcclusion);
        s.Fog = Get("graphics", "fog", s.Fog);
        s.Msaa = Get("graphics", "msaa", s.Msaa) switch { 0 => 0, 2 => 2, 4 => 4, 8 => 8, _ => 2 };
        s.Lighting = Mathf.Clamp(Get("graphics", "lighting", s.Lighting), 0, 2);
        s.Grass = Get("graphics", "grass", s.Grass);
        s.BlinkingLights = Get("graphics", "blinking_lights", s.BlinkingLights);
        s.MovingParts = Get("graphics", "moving_parts", s.MovingParts);
        s.DayNight = Get("graphics", "day_night", s.DayNight);
        s.Weather = Get("graphics", "weather", s.Weather);
        s.Seasons = Get("graphics", "seasons", s.Seasons);
        s.MouseSensitivity = Mathf.Clamp(Get("game", "mouse_sensitivity", s.MouseSensitivity), 25, 300);
        s.InvertMouse = Get("game", "invert_mouse", s.InvertMouse);
        s.SmoothCamera = Get("graphics", "smooth_camera", s.SmoothCamera);

        s.MasterVolume = Mathf.Clamp(Get("audio", "master", s.MasterVolume), 0, 100);
        s.AmbienceVolume = Mathf.Clamp(Get("audio", "ambience", s.AmbienceVolume), 0, 100);
        s.InterfaceVolume = Mathf.Clamp(Get("audio", "interface", s.InterfaceVolume), 0, 100);
        s.AmbienceSound = Get("audio", "ambience_on", s.AmbienceSound);
        s.MusicVolume = Mathf.Clamp(Get("audio", "music", s.MusicVolume), 0, 100);
        s.MusicOn = Get("audio", "music_on", s.MusicOn);
        s.ClickSounds = Get("audio", "clicks", s.ClickSounds);
        s.NotificationSounds = Get("audio", "notifications", s.NotificationSounds);
        s.MuteInBackground = Get("audio", "mute_in_background", s.MuteInBackground);
        return s;
    }

    public void Save() => Save(DefaultPath);

    public void Save(string path)
    {
        var cfg = new ConfigFile();
        cfg.SetValue("general", "language", Language);
        cfg.SetValue("updates", "channel", UpdateChannel);
        cfg.SetValue("updates", "check_on_start", CheckUpdatesOnStart);
        cfg.SetValue("saves", "autosave_minutes", AutosaveMinutes);

        cfg.SetValue("display", "window_mode", (int)WindowMode);
        cfg.SetValue("display", "resolution", Resolution);
        cfg.SetValue("display", "monitor", Monitor);
        cfg.SetValue("display", "vsync", VSync);
        cfg.SetValue("display", "max_fps", MaxFps);
        cfg.SetValue("display", "ui_scale", UiScale);

        cfg.SetValue("graphics", "shadows", Shadows);
        cfg.SetValue("graphics", "ambient_occlusion", AmbientOcclusion);
        cfg.SetValue("graphics", "fog", Fog);
        cfg.SetValue("graphics", "msaa", Msaa);
        cfg.SetValue("graphics", "lighting", Lighting);
        cfg.SetValue("graphics", "grass", Grass);
        cfg.SetValue("graphics", "blinking_lights", BlinkingLights);
        cfg.SetValue("graphics", "moving_parts", MovingParts);
        cfg.SetValue("graphics", "day_night", DayNight);
        cfg.SetValue("graphics", "weather", Weather);
        cfg.SetValue("graphics", "seasons", Seasons);
        cfg.SetValue("game", "mouse_sensitivity", MouseSensitivity);
        cfg.SetValue("game", "invert_mouse", InvertMouse);
        cfg.SetValue("graphics", "smooth_camera", SmoothCamera);

        cfg.SetValue("audio", "master", MasterVolume);
        cfg.SetValue("audio", "ambience", AmbienceVolume);
        cfg.SetValue("audio", "interface", InterfaceVolume);
        cfg.SetValue("audio", "ambience_on", AmbienceSound);
        cfg.SetValue("audio", "music", MusicVolume);
        cfg.SetValue("audio", "music_on", MusicOn);
        cfg.SetValue("audio", "clicks", ClickSounds);
        cfg.SetValue("audio", "notifications", NotificationSounds);
        cfg.SetValue("audio", "mute_in_background", MuteInBackground);
        cfg.Save(path);
    }

    /// <summary>Puts display, graphics and audio back to their defaults; language, channel and saves stay.</summary>
    public void ResetOptions()
    {
        var d = new AppSettings();
        WindowMode = d.WindowMode; Resolution = d.Resolution; Monitor = d.Monitor; VSync = d.VSync; MaxFps = d.MaxFps; UiScale = d.UiScale;
        Shadows = d.Shadows; AmbientOcclusion = d.AmbientOcclusion; Fog = d.Fog; Msaa = d.Msaa;
        BlinkingLights = d.BlinkingLights; MovingParts = d.MovingParts; SmoothCamera = d.SmoothCamera;
        DayNight = d.DayNight; Weather = d.Weather; Seasons = d.Seasons; MouseSensitivity = d.MouseSensitivity; InvertMouse = d.InvertMouse;
        MasterVolume = d.MasterVolume; AmbienceVolume = d.AmbienceVolume; InterfaceVolume = d.InterfaceVolume;
        AmbienceSound = d.AmbienceSound; MusicVolume = d.MusicVolume; MusicOn = d.MusicOn; ClickSounds = d.ClickSounds; NotificationSounds = d.NotificationSounds; MuteInBackground = d.MuteInBackground;
        AutosaveMinutes = d.AutosaveMinutes;
    }

    public AppSettings Clone() => (AppSettings)MemberwiseClone();
}
