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
        using var f = FileAccess.Open(PathOf(slot), FileAccess.ModeFlags.Write);
        f.StoreString(JsonSerializer.Serialize(envelope, Json));
    }

    public static GameState Load(string slot)
    {
        using var f = FileAccess.Open(PathOf(slot), FileAccess.ModeFlags.Read);
        if (f == null) return null;
        var env = JsonSerializer.Deserialize<SaveEnvelope>(f.GetAsText(), Json);
        return env?.State;
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
            try
            {
                using var f = FileAccess.Open($"{Dir}/{file}", FileAccess.ModeFlags.Read);
                var env = JsonSerializer.Deserialize<SaveEnvelope>(f.GetAsText(), Json);
                if (env?.State == null) continue;
                var slot = file[..^5];
                list.Add(new SaveInfo(slot, env.State.Options.Company, env.SavedAt, env.State.Date, slot.StartsWith("autosave")));
            }
            catch (Exception e) { GD.PushWarning($"Skipping broken save {file}: {e.Message}"); }
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
