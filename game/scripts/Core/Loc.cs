using System.Collections.Generic;
using Godot;

namespace DatacenterArchitect.Core;

/// <summary>
/// Loads locale/strings.tsv (key, en, nl) into Godot's TranslationServer.
/// English is the default; every key must also have a Dutch text.
/// </summary>
public static class Loc
{
    public const string File = "res://locale/strings.tsv";
    public static readonly string[] Languages = { "en", "nl" };
    static bool _loaded;
    public static readonly Dictionary<string, Dictionary<string, string>> Table = new();

    public static void Load()
    {
        if (_loaded) return;
        _loaded = true;
        using var f = FileAccess.Open(File, FileAccess.ModeFlags.Read);
        if (f == null) { GD.PushError($"Missing {File}"); return; }
        var header = f.GetLine().Split('\t');
        var translations = new Dictionary<string, Translation>();
        for (var c = 1; c < header.Length; c++)
        {
            var t = new Translation { Locale = header[c] };
            translations[header[c]] = t;
            Table[header[c]] = new Dictionary<string, string>();
        }
        while (!f.EofReached())
        {
            var line = f.GetLine();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#')) continue;
            var cols = line.Split('\t');
            for (var c = 1; c < header.Length && c < cols.Length; c++)
            {
                var text = cols[c].Replace("\\n", "\n");
                translations[header[c]].AddMessage(cols[0], text);
                Table[header[c]][cols[0]] = text;
            }
        }
        foreach (var t in translations.Values) TranslationServer.AddTranslation(t);
    }

    public static string Language => TranslationServer.GetLocale().StartsWith("nl") ? "nl" : "en";

    public static void SetLanguage(string lang)
    {
        TranslationServer.SetLocale(lang);
        AppSettings.Instance.Language = lang;
        AppSettings.Instance.Save();
    }

    /// <summary>Translate a key and fill {0}, {1} ... placeholders.</summary>
    public static string T(string key, params object[] args)
    {
        var s = TranslationServer.Translate(key).ToString();
        return args.Length == 0 ? s : string.Format(s, args);
    }
}
