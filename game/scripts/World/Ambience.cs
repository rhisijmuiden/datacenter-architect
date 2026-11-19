using System;
using Godot;

namespace DatacenterArchitect.World;

/// <summary>
/// Procedural datacenter sound: the low roar of fans and cooling plus the high whine of 1U servers.
/// No audio files needed. Loudness follows how close the camera is and how many live racks are near.
/// </summary>
public partial class Ambience : Node
{
    AudioStreamPlayer _player;
    AudioStreamGeneratorPlayback _pb;
    const float Rate = 22050;
    readonly Random _rng = new(3);
    double _phase1, _phase2, _phase3;
    float _low, _band, _level, _target;

    /// <summary>0 = silent, 1 = standing in a full hall.</summary>
    public float Level { get => _target; set => _target = Mathf.Clamp(value, 0, 1); }

    public override void _Ready()
    {
        _player = new AudioStreamPlayer { Stream = new AudioStreamGenerator { MixRate = Rate, BufferLength = 0.25f }, VolumeDb = -6, Bus = Core.AudioMixer.Ambience };
        AddChild(_player);
        _player.Play();
        _pb = _player.GetStreamPlayback() as AudioStreamGeneratorPlayback;
    }

    Vector2[] _buf = System.Array.Empty<Vector2>();

    public override void _Process(double delta)
    {
        if (_pb == null) return;
        var frames = _pb.GetFramesAvailable();
        if (frames <= 0) return;
        // fill one buffer and hand it over in a single call: one engine call per frame instead of one per sample
        if (_buf.Length != frames) _buf = new Vector2[frames];
        for (var i = 0; i < frames; i++)
        {
            _level += (_target - _level) * 0.00005f;
            // fans: brown-ish noise
            var white = (float)(_rng.NextDouble() * 2 - 1);
            _low += (white - _low) * 0.02f;
            _band += (white - _band) * 0.25f;
            var fans = _low * 2.2f + (_band - _low) * 0.25f;
            // server whine: a few close high tones that beat slowly against each other
            _phase1 += 2 * Math.PI * 4180 / Rate;
            _phase2 += 2 * Math.PI * 4213 / Rate;
            _phase3 += 2 * Math.PI * 118 / Rate;
            var whine = (float)(Math.Sin(_phase1) + Math.Sin(_phase2)) * 0.035f;
            var hum = (float)Math.Sin(_phase3) * 0.08f;
            var s = (fans * 0.5f + whine + hum) * _level;
            _buf[i] = new Vector2(s, s);
        }
        _pb.PushBuffer(_buf);
        if (_phase1 > 1e6) { _phase1 %= 2 * Math.PI; _phase2 %= 2 * Math.PI; _phase3 %= 2 * Math.PI; }
    }
}
