using System.Linq;
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
    static bool _loaded;
    public static readonly Dictionary<string, Dictionary<string, string>> Table = new();

    /// <summary>Extra string tables next to strings.tsv (same columns), so a step can keep its own texts.</summary>
    public static readonly string[] ExtraFiles = { "res://locale/progress.tsv", "res://locale/outages.tsv", "res://locale/hall.tsv", "res://locale/bms.tsv" };

    public static void Load()
    {
        if (_loaded) return;
        _loaded = true;
        var translations = new Dictionary<string, Translation>();
        foreach (var path in new[] { File }.Concat(ExtraFiles))
        {
            using var f = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            if (f == null) { GD.PushError($"Missing {path}"); continue; }
            var header = f.GetLine().Split('\t');
            for (var c = 1; c < header.Length; c++)
            {
                if (translations.ContainsKey(header[c])) continue;
                translations[header[c]] = new Translation { Locale = header[c] };
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
