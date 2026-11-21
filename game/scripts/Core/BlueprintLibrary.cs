using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using DatacenterArchitect.Sim;
using Godot;

namespace DatacenterArchitect.Core;

/// <summary>
/// The player's hall blueprints (infra sketch board 8). They belong to the profile, not to one savegame, so a
/// design made in one game can be used in the next. Stored locally in user://blueprints.json, written atomically.
/// </summary>
public class BlueprintLibrary
{
    public const string FilePath = "user://blueprints.json";
    public const int MaxBlueprints = 60;
    /// <summary>A library bigger than this is not read (60 full halls are well under 2 MB).</summary>
    public const long MaxBytes = 8L << 20;

    static BlueprintLibrary _instance;
    public static BlueprintLibrary Instance => _instance ??= Load(FilePath);

    static readonly JsonSerializerOptions Json = new() { WriteIndented = false, Converters = { new JsonStringEnumConverter() } };

    readonly string _path;
    public readonly List<Blueprint> All = new();
    /// <summary>Bumped on every change, so the build tray knows when to redraw its cards.</summary>
    public int Revision { get; private set; }

    BlueprintLibrary(string path) { _path = path; }

    /// <summary>Screenshots: work on an empty library in memory, never on the player's own file.</summary>
    public static void UseInMemory() => _instance = InMemory();

    /// <summary>An empty library that is never written to disk (tests).</summary>
    public static BlueprintLibrary InMemory() => new(null);

    sealed class FileData
    {
        public int Version { get; set; } = 1;
        public List<Blueprint> Blueprints { get; set; } = new();
    }

    public static BlueprintLibrary Load(string path)
    {
        var lib = new BlueprintLibrary(path);
        if (path == null || !FileAccess.FileExists(path)) return lib;
        using var f = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (f == null || f.GetLength() > (ulong)MaxBytes) return lib;
        lib.Read(f.GetAsText());
        return lib;
    }

    /// <summary>Reads library text; broken files and broken blueprints are skipped instead of crashing.</summary>
    public int Read(string text)
    {
        All.Clear();
        try
        {
            var data = JsonSerializer.Deserialize<FileData>(text ?? "", Json);
            foreach (var bp in data?.Blueprints ?? new List<Blueprint>())
                if (bp != null && bp.Valid() && All.Count < MaxBlueprints && All.All(b => b.Id != bp.Id)) All.Add(bp);
        }
        catch (Exception e) when (e is JsonException or NotSupportedException or ArgumentException or InvalidOperationException)
        {
            GD.PushWarning($"Skipping broken blueprint library: {e.Message}");
        }
        Revision++;
        return All.Count;
    }

    public string Text() => JsonSerializer.Serialize(new FileData { Blueprints = All }, Json);

    public void Save()
    {
        Revision++;
        if (_path == null) return;
        var tmp = _path + ".tmp";
        using (var f = FileAccess.Open(tmp, FileAccess.ModeFlags.Write))
        {
            if (f == null) { GD.PushError($"Cannot write {_path}: {FileAccess.GetOpenError()}"); return; }
            f.StoreString(Text());
        }
        if (FileAccess.FileExists(_path)) DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(_path));
        DirAccess.RenameAbsolute(ProjectSettings.GlobalizePath(tmp), ProjectSettings.GlobalizePath(_path));
    }

    /// <summary>Adds a blueprint (the newest goes first). Returns null, or why it cannot be added.</summary>
    public string Add(Blueprint bp)
    {
        if (bp == null || !bp.Valid()) return "msg_bp_invalid";
        if (All.Count >= MaxBlueprints) return "msg_bp_full";
        bp.Name = Clean(bp.Name);
        if (bp.Name == "") bp.Name = UniqueName("Blueprint");
        while (All.Any(b => b.Id == bp.Id)) bp.Id = Guid.NewGuid().ToString("N")[..12];
        All.Insert(0, bp);
        Save();
        return null;
    }

    public void Remove(Blueprint bp) { if (All.Remove(bp)) Save(); }

    public void Rename(Blueprint bp, string name)
    {
        var n = Clean(name);
        if (n == "" || bp == null) return;
        bp.Name = n;
        Save();
    }

    public Blueprint Get(int index) => index >= 0 && index < All.Count ? All[index] : null;

    /// <summary>A name not used yet: "Name", "Name (2)", "Name (3)" ...</summary>
    public string UniqueName(string name)
    {
        var n = Clean(name);
        if (All.All(b => b.Name != n)) return n;
        for (var i = 2; ; i++) if (All.All(b => b.Name != $"{n} ({i})")) return $"{n} ({i})";
    }

    static string Clean(string s)
    {
        var t = new string((s ?? "").Where(c => !char.IsControl(c)).ToArray()).Trim();
        return t.Length > 40 ? t[..40].TrimEnd() : t;
    }
}
