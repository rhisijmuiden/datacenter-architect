using System;
using System.Collections.Generic;
using System.Linq;

namespace DatacenterArchitect.Sim;

public enum Season { Winter, Spring, Summer, Autumn }

public enum WeatherKind { Clear, Cloudy, Rain, Storm, Snow, Fog }

/// <summary>What the sky looks like at one moment: the weather, how strong it is, the temperature and the light.</summary>
public readonly record struct WeatherNow(WeatherKind Kind, double Intensity, double TempC, Season Season, double SunElevation, double Daylight, double SnowCover);

/// <summary>
/// Seasons, weather and the sun, purely for looks (step 10). There are deliberately no climate rules:
/// the weather never changes what a building does. The weather is deterministic per half day so a save
/// that is loaded again shows the same sky. A heatwave event always means a hot clear sky, a flood means heavy rain.
/// </summary>
public static class Climate
{
    /// <summary>The weather holds for half a day (00-12 and 12-24) and blends into the next block over this many hours.</summary>
    public const double BlockHours = 12, BlendHours = 1.5;

    public static Season SeasonOf(DateTime d) => d.Month switch
    {
        12 or 1 or 2 => Season.Winter,
        3 or 4 or 5 => Season.Spring,
        6 or 7 or 8 => Season.Summer,
        _ => Season.Autumn,
    };

    /// <summary>Hours of daylight on this day of the year, about 52° north: 7.8 h at midwinter, 16.6 h at midsummer.</summary>
    public static double DayLength(int dayOfYear) => 12.2 + 4.4 * Math.Sin(2 * Math.PI * (dayOfYear - 80) / 365.0);

    public static (double rise, double set) SunTimes(int dayOfYear)
    {
        var len = DayLength(dayOfYear);
        // solar noon around 13:40 summer time, 12:40 winter time; keep it simple at 13:00
        return (13 - len / 2, 13 + len / 2);
    }

    /// <summary>Sun height in degrees: up to ~61° in June and ~15° in December, negative at night.</summary>
    public static double SunElevation(DateTime d)
    {
        var doy = d.DayOfYear;
        var (rise, set) = SunTimes(doy);
        var hour = d.TimeOfDay.TotalHours;
        var noonMax = 38 + 23.4 * Math.Sin(2 * Math.PI * (doy - 80) / 365.0);
        if (hour >= rise && hour <= set) return noonMax * Math.Sin(Math.PI * (hour - rise) / (set - rise));
        // below the horizon: down to -30° around midnight
        var night = 24 - (set - rise);
        var t = hour > set ? hour - set : hour + 24 - set;
        return -30 * Math.Sin(Math.PI * t / night);
    }

    /// <summary>0 at night, 1 in full daylight, with dawn and dusk between -6° and +8° of sun height.</summary>
    public static double Daylight(DateTime d) => Smooth(Math.Clamp((SunElevation(d) + 6) / 14, 0, 1));

    static double Smooth(double x) => x * x * (3 - 2 * x);

    /// <summary>A stable hash so every company gets its own, repeatable weather.</summary>
    public static int SeedOf(string name)
    {
        unchecked
        {
            var h = (int)2166136261;
            foreach (var c in name ?? "") h = (h ^ c) * 16777619;
            return h;
        }
    }

    static double Rand(int seed, long block, int salt)
    {
        unchecked
        {
            var x = (ulong)(seed * 0x9E3779B1L) ^ (ulong)(block * 0x85EBCA77L) ^ (ulong)(salt * 0xC2B2AE3DL);
            x ^= x >> 33; x *= 0xff51afd7ed558ccdUL; x ^= x >> 33; x *= 0xc4ceb9fe1a85ec53UL; x ^= x >> 33;
            return (x >> 11) * (1.0 / (1UL << 53));
        }
    }

    /// <summary>Chances per season of clear, cloudy, rain, storm, snow and fog (in that order, they add up to 100).</summary>
    public static readonly int[][] Chances =
    {
        new[] { 25, 30, 12, 3, 20, 10 }, // winter
        new[] { 40, 28, 24, 5, 0, 3 },   // spring
        new[] { 55, 20, 14, 10, 0, 1 },  // summer
        new[] { 24, 30, 28, 6, 2, 10 },  // autumn
    };

    /// <summary>The weather in one half-day block, without events.</summary>
    public static (WeatherKind kind, double intensity) Block(int seed, long block, Season season)
    {
        var roll = Rand(seed, block, 1) * 100;
        var odds = Chances[(int)season];
        var kind = WeatherKind.Clear;
        for (var i = 0; i < odds.Length; i++)
        {
            if (roll < odds[i]) { kind = (WeatherKind)i; break; }
            roll -= odds[i];
        }
        // weather likes to stay: half the time a block repeats the one before
        if (Rand(seed, block, 2) < 0.45 && block > 0)
        {
            var prevSeason = season; // close enough at season edges
            var prev = BlockNoRepeat(seed, block - 1, prevSeason);
            kind = prev;
        }
        return (kind, 0.45 + 0.55 * Rand(seed, block, 3));
    }

    static WeatherKind BlockNoRepeat(int seed, long block, Season season)
    {
        var roll = Rand(seed, block, 1) * 100;
        var odds = Chances[(int)season];
        for (var i = 0; i < odds.Length; i++)
        {
            if (roll < odds[i]) return (WeatherKind)i;
            roll -= odds[i];
        }
        return WeatherKind.Clear;
    }

    /// <summary>Typical temperature for the season and hour, with weather on top.</summary>
    public static double Temperature(DateTime d, WeatherKind kind, int seed)
    {
        var doy = d.DayOfYear;
        var mean = 10.5 - 8 * Math.Cos(2 * Math.PI * (doy - 20) / 365.0); // ~2.5 °C in January, ~18.5 °C in July
        var hour = d.TimeOfDay.TotalHours;
        var daily = 4 * Math.Cos(2 * Math.PI * (hour - 15) / 24);
        var w = kind switch { WeatherKind.Clear => 1.5, WeatherKind.Rain => -2, WeatherKind.Storm => -3, WeatherKind.Snow => -4, WeatherKind.Fog => -1.5, _ => 0 };
        var noise = (Rand(seed, (long)(d - GameState.Epoch).TotalDays, 7) - 0.5) * 5;
        return mean + daily + w + noise;
    }

    /// <summary>The full picture for a moment in the game.</summary>
    public static WeatherNow At(DateTime d, int seed, bool heatwave = false, bool flood = false)
    {
        var season = SeasonOf(d);
        var hours = (d - GameState.Epoch).TotalHours;
        var block = (long)Math.Floor(hours / BlockHours);
        var (kind, intensity) = Block(seed, block, season);
        if (kind == WeatherKind.Snow && Temperature(d, kind, seed) > 4) kind = WeatherKind.Rain;
        var temp = Temperature(d, kind, seed);
        if (heatwave) { kind = WeatherKind.Clear; intensity = 1; temp = Math.Max(temp + 16, 30); }
        if (flood) { kind = WeatherKind.Storm; intensity = 1; }
        var snow = SnowCover(d, seed);
        if (heatwave) snow = 0;
        return new WeatherNow(kind, intensity, temp, season, SunElevation(d), Daylight(d), snow);
    }

    /// <summary>How white the ground is: snow of the last few blocks stays a while, then melts.</summary>
    public static double SnowCover(DateTime d, int seed)
    {
        var hours = (d - GameState.Epoch).TotalHours;
        var block = (long)Math.Floor(hours / BlockHours);
        double cover = 0;
        for (var b = block - 5; b <= block; b++)
        {
            if (b < 0) continue;
            var t = GameState.Epoch.AddHours(b * BlockHours + 6);
            var season = SeasonOf(t);
            if (season != Season.Winter && t.Month != 11 && t.Month != 3) { cover = 0; continue; }
            var (k, i) = Block(seed, b, season);
            if (k == WeatherKind.Snow && Temperature(t, k, seed) <= 4) cover = Math.Min(1, cover + 0.35 + 0.5 * i);
            else cover = Math.Max(0, cover - (Temperature(t, k, seed) > 5 ? 0.5 : 0.18));
        }
        return cover;
    }
}

public partial class GameState
{
    WeatherNow _wx;
    double _wxAt = double.NaN;

    /// <summary>The weather now, for the world view, the top bar and the weather effects. Heatwave and flood events show in the sky.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public WeatherNow Weather
    {
        get
        {
            if (!double.IsNaN(_wxAt) && Math.Abs(Hours - _wxAt) < 0.05) return _wx;
            _wxAt = Hours;
            return _wx = Climate.At(Date, Seed,
                ActiveEvents.Any(e => e.Kind == EventKind.Heatwave && Hours < e.EndHour),
                ActiveEvents.Any(e => e.Kind == EventKind.Flood && Hours < e.EndHour));
        }
    }

    /// <summary>The weather plays along (new-game switch, on by default; older saves keep it off).</summary>
    [System.Text.Json.Serialization.JsonIgnore] public bool WeatherMatters => Options?.WeatherEffects == true;

    /// <summary>Cooling energy on top of IT: free cooling when it is cold, more when it is hot.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public double CoolingWeatherFactor => !WeatherMatters ? 1 : CoolingFactorAt(Weather.TempC);

    public static double CoolingFactorAt(double tempC) => tempC < 10 ? 0.65 : tempC < 18 ? 0.85 : tempC < 27 ? 1.0 : 1.15;

    /// <summary>How much of a normal day's sunshine the solar panels get.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public double SunFactor => !WeatherMatters ? 1 : Weather.Kind switch
    {
        WeatherKind.Clear => 1.2, WeatherKind.Cloudy => 0.65, WeatherKind.Rain => 0.4, WeatherKind.Storm => 0.3, WeatherKind.Snow => 0.3, _ => 0.55,
    };

    /// <summary>Trucks and visitors are slower in snow and fog.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public double TravelFactor => !WeatherMatters ? 1 : Weather.Kind switch { WeatherKind.Snow => 0.6, WeatherKind.Fog => 0.7, WeatherKind.Storm => 0.85, _ => 1 };

    /// <summary>A storm makes a city-wide power cut four times as likely.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public double OutageWeatherFactor => WeatherMatters && Weather.Kind == WeatherKind.Storm ? 4 : 1;

    /// <summary>The weather report: the afternoon of each of the next days.</summary>
    public List<(DateTime day, WeatherNow w)> Forecast(int days)
    {
        var list = new List<(DateTime, WeatherNow)>();
        var today = Date.Date;
        for (var i = 1; i <= days; i++)
        {
            var t = today.AddDays(i).AddHours(14);
            list.Add((t, Climate.At(t, Seed)));
        }
        return list;
    }
}
