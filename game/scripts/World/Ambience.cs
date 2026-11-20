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
    /// <summary>Rain on the roof or the ground, 0 to 1 (step 10 weather).</summary>
    public float Rain { get => _rainT; set => _rainT = Mathf.Clamp(value, 0, 1); }
    float _rainT, _rain, _rainLow, _thunder, _thunderLow, _thunderDelay = -1, _thunderLevel;

    /// <summary>Step 11 round 3, sound per area (0..1 each): outdoors (wind, birds by day, crickets at night), office (murmur, keyboards), building site (hammering, an engine).</summary>
    public float Outdoor { get => _outT; set => _outT = Mathf.Clamp(value, 0, 1); }
    public float Office { get => _offT; set => _offT = Mathf.Clamp(value, 0, 1); }
    public float Site { get => _siteT; set => _siteT = Mathf.Clamp(value, 0, 1); }
    /// <summary>1 = full daylight, 0 = night.</summary>
    public float Daylight { get => _dayT; set => _dayT = Mathf.Clamp(value, 0, 1); }
    float _outT, _offT, _siteT, _dayT = 1, _out, _off, _site, _day = 1;
    float _wind, _windLfo, _murmur, _murLfo, _click, _chirp, _chirpF, _chirpLen, _cricket, _thunk, _thunkF, _engine;
    double _chirpPh, _crPh, _crGate, _thunkPh, _engPh, _windT;
    int _chirpsLeft;

    /// <summary>A rumble of thunder a moment after a lightning flash; nearer storms sound louder.</summary>
    public void Thunder(float level = 1)
    {
        _thunderDelay = 0.6f + (float)_rng.NextDouble() * 1.6f;
        _thunderLevel = Mathf.Clamp(level, 0, 1);
    }

    public override void _Ready()
    {
        _player = new AudioStreamPlayer { Stream = new AudioStreamGenerator { MixRate = Rate, BufferLength = 0.25f }, VolumeDb = -6, Bus = Core.AudioMixer.Ambience };
        AddChild(_player);
        _player.Play();
        _pb = _player.GetStreamPlayback() as AudioStreamGeneratorPlayback;
    }

    Vector2[] _buf = System.Array.Empty<Vector2>();

    /// <summary>One sample of the area sounds, from the same white noise as the fans.</summary>
    float AreaSample(float white)
    {
        _out += (_outT - _out) * 0.00004f; _off += (_offT - _off) * 0.00004f; _site += (_siteT - _site) * 0.00004f; _day += (_dayT - _day) * 0.00002f;
        float s = 0;
        if (_out > 0.001f)
        {
            // wind: low noise that swells and falls over several seconds
            _windT += 1 / Rate;
            var gust = 0.55f + 0.45f * (float)Math.Sin(_windT * 0.37) * (float)Math.Sin(_windT * 0.11 + 1);
            _wind += (white - _wind) * 0.012f;
            s += _wind * 0.9f * gust * _out;
            // birds by day: short whistled chirps in little bursts
            if (_chirp <= 0.0001f && _day > 0.3f && _rng.NextDouble() < (_chirpsLeft > 0 ? 0.0009 : 0.00006) * _day)
            {
                _chirpsLeft = _chirpsLeft > 0 ? _chirpsLeft - 1 : _rng.Next(2, 6);
                _chirp = 1; _chirpF = 2600 + (float)_rng.NextDouble() * 2200; _chirpLen = 0.9985f + (float)_rng.NextDouble() * 0.001f;
            }
            if (_chirp > 0.0001f)
            {
                _chirpF *= 1.00004f;
                _chirpPh += _chirpF / Rate;
                s += (float)Math.Sin(_chirpPh * Math.Tau) * _chirp * 0.045f * _out * _day;
                _chirp *= _chirpLen;
            }
            // crickets at night: a high tone in quick pulses
            var night = 1 - _day;
            if (night > 0.05f)
            {
                _crPh += 4400 / Rate; _crGate += 28 / Rate;
                var pulse = (_crGate % 1) < 0.35 && (_crGate % 6) < 3.2 ? 1f : 0f;
                _cricket += (pulse - _cricket) * 0.02f;
                s += (float)Math.Sin(_crPh * Math.Tau) * _cricket * 0.018f * night * _out;
            }
        }
        if (_off > 0.001f)
        {
            // office: a soft murmur of voices and now and then a keyboard
            _murmur += (white - _murmur) * 0.08f;
            _murLfo += ((float)_rng.NextDouble() - _murLfo) * 0.0004f;
            s += (_murmur - _wind * 0.5f) * 0.12f * (0.4f + _murLfo) * _off;
            if (_rng.NextDouble() < 0.0016) _click = 0.6f + (float)_rng.NextDouble() * 0.4f;
            s += white * _click * 0.05f * _off;
            _click *= 0.985f;
        }
        if (_site > 0.001f)
        {
            // building site: hammer blows and a diesel engine rumbling along
            if (_thunk < 0.001f && _rng.NextDouble() < 0.00012) { _thunk = 1; _thunkF = 90 + (float)_rng.NextDouble() * 70; }
            _thunkPh += _thunkF / Rate;
            s += ((float)Math.Sin(_thunkPh * Math.Tau) * 0.6f + white * 0.4f) * _thunk * 0.35f * _site;
            _thunk *= 0.9993f;
            _engPh += 34 / Rate;
            _engine += (white - _engine) * 0.03f;
            s += ((float)Math.Sin(_engPh * Math.Tau) * 0.5f + _engine) * 0.12f * (0.8f + 0.2f * (float)Math.Sin(_engPh * 0.05)) * _site;
        }
        return s;
    }

    public override void _Process(double delta)
    {
        if (_pb == null) return;
        var frames = _pb.GetFramesAvailable();
        if (frames <= 0) return;
        if (_thunderDelay >= 0)
        {
            _thunderDelay -= (float)delta;
            if (_thunderDelay < 0) _thunder = _thunderLevel;
        }
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
            // rain: hissing high noise with a soft patter; thunder: deep rumbling noise that dies away over a few seconds
            _rain += (_rainT - _rain) * 0.00003f;
            if (_rain > 0.001f)
            {
                _rainLow += (white - _rainLow) * 0.35f;
                var patter = _rng.NextDouble() < 0.0015 * _rain ? (float)(_rng.NextDouble() - 0.5) * 0.6f : 0;
                s += ((white - _rainLow) * 0.22f + _rainLow * 0.08f + patter) * _rain;
            }
            if (_thunder > 0.0005f)
            {
                _thunderLow += (white - _thunderLow) * 0.006f;
                s += _thunderLow * 6f * _thunder;
                _thunder *= 0.99993f;
            }
            s += AreaSample(white);
            _buf[i] = new Vector2(s, s);
        }
        _pb.PushBuffer(_buf);
        if (_phase1 > 1e6) { _phase1 %= 2 * Math.PI; _phase2 %= 2 * Math.PI; _phase3 %= 2 * Math.PI; _chirpPh %= 1; _crPh %= 1; _thunkPh %= 1; _engPh %= 1; }
    }
}
