using System;
using DatacenterArchitect.Core;
using Godot;

namespace DatacenterArchitect.World;

/// <summary>
/// Small interface sounds, generated in code like the ambience (no audio files):
/// a soft click for buttons and a two-tone chime for notifications. Both play on the Interface bus.
/// </summary>
public partial class UiSounds : Node
{
    public static UiSounds Instance { get; private set; }
    AudioStreamPlayer _click, _chime;
    ulong _lastChime;

    public override void _Ready()
    {
        Instance = this;
        _click = new AudioStreamPlayer { Stream = Tone(new[] { 1800f }, 0.035f, 0.35f), Bus = AudioMixer.Interface, MaxPolyphony = 3 };
        _chime = new AudioStreamPlayer { Stream = Tone(new[] { 880f, 1320f }, 0.32f, 0.22f), Bus = AudioMixer.Interface };
        AddChild(_click); AddChild(_chime);
        // every button in the game clicks, including the ones created later
        GetTree().NodeAdded += OnNodeAdded;
    }

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
        GetTree().NodeAdded -= OnNodeAdded;
    }

    void OnNodeAdded(Node n)
    {
        if (n is BaseButton b) b.Pressed += Click;
        else if (n is Slider sl) sl.DragEnded += _ => Click();
    }

    public void Click()
    {
        if (AppSettings.Instance.ClickSounds && IsInsideTree()) _click.Play();
    }

    public void Chime()
    {
        if (!AppSettings.Instance.NotificationSounds || !IsInsideTree()) return;
        // several messages in the same frame give one chime
        var now = Time.GetTicksMsec();
        if (now - _lastChime < 400) return;
        _lastChime = now;
        _chime.Play();
    }

    /// <summary>A short tone (or a rising sequence of tones) with a quick attack and an exponential fade.</summary>
    public static AudioStreamWav Tone(float[] freqs, float seconds, float gain)
    {
        const int rate = 22050;
        var per = (int)(rate * seconds / freqs.Length);
        var data = new byte[per * freqs.Length * 2];
        var phase = 0.0;
        for (var f = 0; f < freqs.Length; f++)
            for (var i = 0; i < per; i++)
            {
                var t = i / (float)rate;
                phase += 2 * Math.PI * freqs[f] / rate;
                var env = Math.Min(1f, t * 400f) * MathF.Exp(-t * (freqs.Length > 1 ? 9f : 70f));
                var v = (float)(Math.Sin(phase) + 0.25 * Math.Sin(phase * 2)) * env * gain;
                var sample = (short)Math.Clamp(v * short.MaxValue, short.MinValue, short.MaxValue);
                var idx = (f * per + i) * 2;
                data[idx] = (byte)(sample & 0xff); data[idx + 1] = (byte)((sample >> 8) & 0xff);
            }
        return new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = rate, Stereo = false, Data = data };
    }
}
