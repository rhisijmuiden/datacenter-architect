using System;
using Godot;

namespace DatacenterArchitect.Core;

/// <summary>Semantic version with an optional pre-release tag (for example 0.2.0-test.3).</summary>
public readonly struct SemVer : IComparable<SemVer>
{
    public readonly int Major, Minor, Patch;
    public readonly string Pre;

    public SemVer(int major, int minor, int patch, string pre = "")
    {
        Major = major; Minor = minor; Patch = patch; Pre = pre ?? "";
    }

    public static bool TryParse(string text, out SemVer v)
    {
        v = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        text = text.Trim();
        if (text.StartsWith('v') || text.StartsWith('V')) text = text[1..];
        var plus = text.IndexOf('+');
        if (plus >= 0) text = text[..plus];
        var pre = "";
        var dash = text.IndexOf('-');
        if (dash >= 0) { pre = text[(dash + 1)..]; text = text[..dash]; }
        var parts = text.Split('.');
        if (parts.Length < 2 || parts.Length > 3) return false;
        if (!int.TryParse(parts[0], out var a) || !int.TryParse(parts[1], out var b)) return false;
        var c = 0;
        if (parts.Length == 3 && !int.TryParse(parts[2], out c)) return false;
        v = new SemVer(a, b, c, pre);
        return true;
    }

    public int CompareTo(SemVer o)
    {
        if (Major != o.Major) return Major.CompareTo(o.Major);
        if (Minor != o.Minor) return Minor.CompareTo(o.Minor);
        if (Patch != o.Patch) return Patch.CompareTo(o.Patch);
        if (Pre == o.Pre) return 0;
        if (Pre == "") return 1;      // 1.0.0 > 1.0.0-test.1
        if (o.Pre == "") return -1;
        var a = Pre.Split('.'); var b = o.Pre.Split('.');
        for (var i = 0; i < Math.Min(a.Length, b.Length); i++)
        {
            var na = int.TryParse(a[i], out var ia); var nb = int.TryParse(b[i], out var ib);
            var c = na && nb ? ia.CompareTo(ib) : string.CompareOrdinal(a[i], b[i]);
            if (c != 0) return c;
        }
        return a.Length.CompareTo(b.Length);
    }

    public override string ToString() => Pre == "" ? $"{Major}.{Minor}.{Patch}" : $"{Major}.{Minor}.{Patch}-{Pre}";
}

public static class GameVersion
{
    public static string Text => (string)ProjectSettings.GetSetting("application/config/version", "0.0.0");
    public static SemVer Current => SemVer.TryParse(Text, out var v) ? v : new SemVer(0, 0, 0);
    /// <summary>Channel this build was published on: stable, test or dev (local build).</summary>
    public static string BuildChannel => (string)ProjectSettings.GetSetting("datacenter/build/channel", "dev");
    public static string Repository => (string)ProjectSettings.GetSetting("datacenter/updates/repository", "rhisijmuiden/datacenter-architect");
}
