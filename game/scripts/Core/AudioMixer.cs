using System;
using Godot;

namespace DatacenterArchitect.Core;

/// <summary>
/// Audio buses: Master with Ambience (server room and area sound), Interface (clicks and notification chimes) and Music under it.
/// Volumes come from <see cref="AppSettings"/> as 0..100.
/// </summary>
public static class AudioMixer
{
    public const string Ambience = "Ambience", Interface = "Interface", Music = "Music";
    static bool _inBackground;

    public static void EnsureBuses()
    {
        foreach (var name in new[] { Ambience, Interface, Music })
        {
            if (AudioServer.GetBusIndex(name) >= 0) continue;
            AudioServer.AddBus();
            var i = AudioServer.BusCount - 1;
            AudioServer.SetBusName(i, name);
            AudioServer.SetBusSend(i, "Master");
        }
    }

    /// <summary>Perceptual volume: 0 = silent, 100 = 0 dB. A squared curve feels even over the whole slider.</summary>
    public static float ToDb(int volume)
    {
        if (volume <= 0) return -80f;
        var lin = Mathf.Clamp(volume / 100f, 0f, 1f);
        return Mathf.LinearToDb(lin * lin);
    }

    public static void Apply(AppSettings s)
    {
        EnsureBuses();
        Set("Master", s.MasterVolume, s.MuteInBackground && _inBackground);
        Set(Ambience, s.AmbienceVolume, !s.AmbienceSound);
        Set(Interface, s.InterfaceVolume, false);
        Set(Music, s.MusicVolume, !s.MusicOn);
    }

    static void Set(string bus, int volume, bool mute)
    {
        var i = AudioServer.GetBusIndex(bus);
        if (i < 0) return;
        AudioServer.SetBusVolumeDb(i, ToDb(volume));
        AudioServer.SetBusMute(i, mute || volume <= 0);
    }

    /// <summary>Called when the game window loses or regains focus.</summary>
    public static void SetInBackground(bool background, AppSettings s)
    {
        _inBackground = background;
        Apply(s);
    }
}
