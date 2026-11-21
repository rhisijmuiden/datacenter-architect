using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using DatacenterArchitect.Sim;
using Godot;

namespace DatacenterArchitect.Core;

public record SaveInfo(string Slot, string Company, DateTime SavedAt, DateTime GameDate, bool IsAutosave);

/// <summary>Local savegames only (offline game): user://saves/*.json plus three rotating autosaves.</summary>
public static class SaveSystem
{
    public const string Dir = "user://saves";
    public const int AutosaveSlots = 3;
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    static string PathOf(string slot) => $"{Dir}/{Sanitize(slot)}.json";

    public static string Sanitize(string slot)
    {
        var chars = slot.Trim().Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' or ' ' ? c : '_').ToArray();
        var s = new string(chars).Trim();
        return string.IsNullOrEmpty(s) ? "save" : s;
    }

    public static void Save(GameState state, string slot)
    {
        DirAccess.MakeDirRecursiveAbsolute(Dir);
        state.GameVersion = GameVersion.Text;
        var envelope = new SaveEnvelope { Slot = Sanitize(slot), SavedAt = DateTime.Now, State = state };
        // write next to the save first and swap it in, so a crash or full disk never leaves half a savegame behind
        var path = PathOf(slot); var tmp = path + ".tmp";
        using (var f = FileAccess.Open(tmp, FileAccess.ModeFlags.Write))
        {
            if (f == null) { GD.PushError($"Cannot write save {path}: {FileAccess.GetOpenError()}"); return; }
            f.StoreString(JsonSerializer.Serialize(envelope, Json));
        }
        if (FileAccess.FileExists(path)) DirAccess.RemoveAbsolute(path);
        DirAccess.RenameAbsolute(tmp, path);
    }

    /// <summary>Savegames bigger than this are not read (a normal one is a few MB).</summary>
    public const long MaxSaveBytes = 512L << 20;

    /// <summary>Reads a save envelope; broken, oversized or impossible files give null instead of an exception.</summary>
    static SaveEnvelope Read(string path)
    {
        using var f = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (f == null || f.GetLength() > (ulong)MaxSaveBytes) return null;
        try
        {
            var env = JsonSerializer.Deserialize<SaveEnvelope>(f.GetAsText(), Json);
            return env?.State != null && Plausible(env.State) ? env : null;
        }
        catch (Exception e) when (e is JsonException or NotSupportedException or ArgumentException or InvalidOperationException)
        {
            GD.PushWarning($"Skipping broken save {path}: {e.Message}");
            return null;
        }
    }

    /// <summary>Guards against edited or damaged saves that would make the game allocate huge grids.</summary>
    public static bool Plausible(GameState s) => s.PlotSize is >= 16 and <= 1024 && s.Options != null
        && (s.Ducts == null || (s.Ducts.Count <= 4096 && s.Ducts.All(d => d?.Points != null && d.Points.Count <= 512 && d.Points.All(p => p >= -1 && p <= s.PlotSize))));

    public static GameState Load(string slot)
    {
        var env = Read(PathOf(slot));
        if (env == null) return null;
        try { env.State.Migrate(); }
        catch (Exception e) { GD.PushWarning($"Cannot upgrade save {slot}: {e.Message}"); return null; }
        return env.State;
    }

    public static bool Delete(string slot) => DirAccess.RemoveAbsolute(PathOf(slot)) == Error.Ok;

    public static List<SaveInfo> List()
    {
        var list = new List<SaveInfo>();
        using var d = DirAccess.Open(Dir);
        if (d == null) return list;
        foreach (var file in d.GetFiles())
        {
            if (!file.EndsWith(".json")) continue;
            var env = Read($"{Dir}/{file}");
            if (env == null) continue;
            var slot = file[..^5];
            list.Add(new SaveInfo(slot, env.State.Options.Company, env.SavedAt, env.State.Date, slot.StartsWith("autosave")));
        }
        return list.OrderByDescending(s => s.SavedAt).ToList();
    }

    /// <summary>Writes autosave-1 and shifts older autosaves down, keeping three.</summary>
    public static void Autosave(GameState state)
    {
        DirAccess.MakeDirRecursiveAbsolute(Dir);
        for (var i = AutosaveSlots; i > 1; i--)
        {
            var from = PathOf($"autosave-{i - 1}");
            if (FileAccess.FileExists(from)) DirAccess.RenameAbsolute(from, PathOf($"autosave-{i}"));
        }
        Save(state, "autosave-1");
    }

    public class SaveEnvelope
    {
        public string Slot { get; set; }
        public DateTime SavedAt { get; set; }
        public GameState State { get; set; }
    }
}
