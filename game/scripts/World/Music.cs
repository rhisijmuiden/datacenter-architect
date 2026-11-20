using System;
using Godot;

namespace DatacenterArchitect.World;

/// <summary>What the music plays: a mood per area of the game (step 11 round 3).</summary>
public enum MusicMood { Campus, Hall, Office, Night, Building }

/// <summary>
/// Step 11 round 3: generative background music, no audio files. Slow chord progressions per mood with soft pads,
/// a bass and a plucked arpeggio; each mood has its own key, scale, tempo and sound. The mood follows the area the
/// camera looks at (campus by day, data hall, office, night, building site) and changes on the next chord, so it
/// never cuts. It plays on its own "Music" bus.
/// </summary>
public partial class Music : Node
{
    const float Rate = 22050;
    AudioStreamPlayer _player;
    AudioStreamGeneratorPlayback _pb;
    Vector2[] _buf = Array.Empty<Vector2>();
    readonly Random _rng = new(11);

    public MusicMood Mood { get; set; } = MusicMood.Campus;
    /// <summary>0..1, fades the whole mix (e.g. off on the title screen).</summary>
    public float Level { get => _target; set => _target = Mathf.Clamp(value, 0, 1); }
    float _target = 1, _level;

    sealed record Style(int Root, int[] Scale, int[][] Chords, float Bpm, float Pluck, float Bass, float Bright, float PadLevel);

    // chords are scale degrees (0-based) of the root of each chord; triads stacked in the scale
    static readonly int[] Major = { 0, 2, 4, 5, 7, 9, 11 }, Minor = { 0, 2, 3, 5, 7, 8, 10 }, Dorian = { 0, 2, 3, 5, 7, 9, 10 }, Lydian = { 0, 2, 4, 6, 7, 9, 11 };
    static Style StyleOf(MusicMood m) => m switch
    {
        MusicMood.Hall => new(45, Dorian, new[] { new[] { 0 }, new[] { 5 }, new[] { 2 }, new[] { 6 } }, 84, 0.75f, 0.8f, 0.55f, 0.75f),
        MusicMood.Office => new(53, Major, new[] { new[] { 0 }, new[] { 3 }, new[] { 5 }, new[] { 4 } }, 92, 0.6f, 0.55f, 0.85f, 0.6f),
        MusicMood.Night => new(50, Minor, new[] { new[] { 0 }, new[] { 5 }, new[] { 3 }, new[] { 4 } }, 60, 0.25f, 0.0f, 0.35f, 0.9f),
        MusicMood.Building => new(55, Major, new[] { new[] { 0 }, new[] { 4 }, new[] { 5 }, new[] { 3 } }, 100, 0.85f, 0.9f, 0.8f, 0.55f),
        _ => new(48, Lydian, new[] { new[] { 0 }, new[] { 1 }, new[] { 4 }, new[] { 0 } }, 72, 0.5f, 0.6f, 0.7f, 0.8f),
    };

    Style _style = StyleOf(MusicMood.Campus);
    MusicMood _playing = MusicMood.Campus;
    int _chord, _step;
    double _t, _stepLen;
    readonly float[] _padFreq = new float[3];
    readonly double[] _padPh = new double[6];
    float _padAmp, _padLow;
    double _bassPh, _plPh, _plPh2;
    float _bassFreq, _bassAmp, _plFreq, _plAmp;

    public override void _Ready()
    {
        Core.AudioMixer.EnsureBuses();
        _player = new AudioStreamPlayer { Stream = new AudioStreamGenerator { MixRate = Rate, BufferLength = 0.3f }, VolumeDb = -8, Bus = Core.AudioMixer.Music };
        AddChild(_player);
        _player.Play();
        _pb = _player.GetStreamPlayback() as AudioStreamGeneratorPlayback;
        _stepLen = 60.0 / _style.Bpm / 2;
        NextChord();
    }

    static float Hz(int midi) => 440f * Mathf.Pow(2, (midi - 69) / 12f);

    int Note(int degree, int octave = 0)
    {
        var sc = _style.Scale;
        var o = Math.DivRem(degree, sc.Length, out var d);
        if (d < 0) { d += sc.Length; o--; }
        return _style.Root + sc[d] + 12 * (o + octave);
    }

    void NextChord()
    {
        if (Mood != _playing) { _playing = Mood; _style = StyleOf(Mood); _chord = 0; _stepLen = 60.0 / _style.Bpm / 2; }
        else _chord = (_chord + 1) % _style.Chords.Length;
        var deg = _style.Chords[_chord][0];
        for (var i = 0; i < 3; i++) _padFreq[i] = Hz(Note(deg + i * 2, 1));
        _bassFreq = Hz(Note(deg, -1));
    }

    public override void _Process(double delta)
    {
        if (_pb == null) return;
        var frames = _pb.GetFramesAvailable();
        if (frames <= 0) return;
        if (_buf.Length != frames) _buf = new Vector2[frames];
        var dt = 1.0 / Rate;
        for (var i = 0; i < frames; i++)
        {
            _level += (_target - _level) * 0.00003f;
            _t += dt;
            if (_t >= _stepLen)
            {
                _t -= _stepLen;
                _step = (_step + 1) % 16;   // 16 eighth notes = 2 bars per chord
                if (_step == 0) NextChord();
                if ((_step == 0 || _step == 8) && _style.Bass > 0) _bassAmp = _style.Bass;
                if (_rng.NextDouble() < _style.Pluck * (_step % 2 == 0 ? 0.9 : 0.45))
                {
                    var deg = _style.Chords[_chord][0] + new[] { 0, 2, 4, 7, 9 }[_rng.Next(5)];
                    _plFreq = Hz(Note(deg, 2));
                    _plAmp = 0.5f + (float)_rng.NextDouble() * 0.3f;
                }
            }
            // pad: three chord tones, two slightly detuned oscillators each, a soft swell over the chord
            var pos = (float)(_step * _stepLen + _t) / (float)(16 * _stepLen);
            _padAmp = _style.PadLevel * Mathf.Clamp(Mathf.Min(pos * 4, (1 - pos) * 6 + 0.35f), 0, 1);
            float pad = 0;
            for (var k = 0; k < 3; k++)
            {
                _padPh[k * 2] += _padFreq[k] * dt; _padPh[k * 2 + 1] += _padFreq[k] * 1.004 * dt;
                var a = _padPh[k * 2] % 1; var b = _padPh[k * 2 + 1] % 1;
                // a triangle-ish wave: warmer than sine, softer than saw
                pad += (float)(Tri(a) + Tri(b)) * 0.5f;
            }
            _padLow += (pad - _padLow) * (0.05f + 0.1f * _style.Bright);
            var padOut = _padLow * _padAmp * 0.07f;
            // bass: a sine with a slow decay
            _bassPh += _bassFreq * dt;
            var bass = (float)Math.Sin(_bassPh * Math.Tau) * _bassAmp * 0.16f;
            _bassAmp *= 0.99992f;
            // pluck: a bell-like tone with a quick decay
            _plPh += _plFreq * dt; _plPh2 += _plFreq * 2.01 * dt;
            var pl = (float)(Math.Sin(_plPh * Math.Tau) + Math.Sin(_plPh2 * Math.Tau) * 0.3 * _style.Bright) * _plAmp * 0.07f;
            _plAmp *= 0.99975f;
            var m = (padOut + bass + pl) * _level;
            // a little stereo width: the pluck leans left, the pad right
            _buf[i] = new Vector2(m + pl * 0.15f * _level, m + padOut * 0.15f * _level);
        }
        _pb.PushBuffer(_buf);
        if (_bassPh > 1e6) { _bassPh %= 1; _plPh %= 1; _plPh2 %= 1; for (var k = 0; k < 6; k++) _padPh[k] %= 1; }
    }

    static double Tri(double p) => 1 - 4 * Math.Abs(p - 0.5);
}
