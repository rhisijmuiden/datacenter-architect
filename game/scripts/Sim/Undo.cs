using System.Collections.Generic;
using System.Linq;

namespace DatacenterArchitect.Sim;

/// <summary>
/// Undo and redo for the player's building actions (Ctrl+Z / Ctrl+Y). Each action is recorded as the difference
/// it made to the layout: buildings, rooms, doors, objects, curves and fiber ducts it added or removed, paving and fence cells
/// it changed, and the money it cost. Undo puts exactly that back, so the simulation keeps running (time, staff,
/// customers and anything built since stay as they are).
/// </summary>
public class UndoHistory
{
    public const int Limit = 60;

    /// <summary>The layout just before an action.</summary>
    public sealed class Snapshot
    {
        internal byte[] Surfaces, Fences;
        internal List<Building> Buildings;
        internal List<Room> Rooms;
        internal List<Door> Doors;
        internal List<PlacedObject> Objects;
        internal List<CurvePath> Curves;
        internal List<FiberDuct> Ducts;
        internal long Money;
    }

    internal sealed class Diff<T>
    {
        public List<T> Added = new(), Removed = new();
        public bool Empty => Added.Count == 0 && Removed.Count == 0;
    }

    /// <summary>One recorded action: what it changed, under a name for the toast.</summary>
    public sealed class Entry
    {
        public string Label;
        internal Diff<Building> Buildings;
        internal Diff<Room> Rooms;
        internal Diff<Door> Doors;
        internal Diff<PlacedObject> Objects;
        internal Diff<CurvePath> Curves;
        internal Diff<FiberDuct> Ducts;
        internal List<(int i, byte before, byte after)> Surfaces = new(), Fences = new();
        internal long Spent;
    }

    readonly List<Entry> _undo = new(), _redo = new();

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public string NextUndo => _undo.Count > 0 ? _undo[^1].Label : null;
    public string NextRedo => _redo.Count > 0 ? _redo[^1].Label : null;
    public int Count => _undo.Count;

    public void Clear() { _undo.Clear(); _redo.Clear(); }

    public static Snapshot Take(GameState s) => new()
    {
        Surfaces = (byte[])s.Surfaces.Clone(), Fences = (byte[])s.Fences.Clone(),
        Buildings = s.Buildings.ToList(), Rooms = s.Rooms.ToList(), Doors = s.Doors.ToList(),
        Objects = s.Objects.ToList(), Curves = s.Curves.ToList(), Ducts = s.Ducts.ToList(), Money = s.Money,
    };

    /// <summary>Record what changed since <paramref name="before"/>. Returns false when the action changed nothing.</summary>
    public bool Commit(GameState s, Snapshot before, string label)
    {
        var e = new Entry
        {
            Label = label,
            Buildings = DiffOf(before.Buildings, s.Buildings), Rooms = DiffOf(before.Rooms, s.Rooms), Doors = DiffOf(before.Doors, s.Doors),
            Objects = DiffOf(before.Objects, s.Objects), Curves = DiffOf(before.Curves, s.Curves), Ducts = DiffOf(before.Ducts, s.Ducts),
            Spent = before.Money - s.Money,
        };
        // a resized plot (more land bought) cannot be undone cell by cell: start a fresh history
        if (before.Surfaces.Length != s.Surfaces.Length) { Clear(); return false; }
        for (var i = 0; i < s.Surfaces.Length; i++)
        {
            if (before.Surfaces[i] != s.Surfaces[i]) e.Surfaces.Add((i, before.Surfaces[i], s.Surfaces[i]));
            if (before.Fences[i] != s.Fences[i]) e.Fences.Add((i, before.Fences[i], s.Fences[i]));
        }
        if (e.Buildings.Empty && e.Rooms.Empty && e.Doors.Empty && e.Objects.Empty && e.Curves.Empty && e.Ducts.Empty && e.Surfaces.Count == 0 && e.Fences.Count == 0)
            return false;
        _undo.Add(e);
        if (_undo.Count > Limit) _undo.RemoveAt(0);
        _redo.Clear();
        return true;
    }

    /// <summary>Take back the last action. Returns its label, or null when there is nothing to undo.</summary>
    public string Undo(GameState s)
    {
        if (_undo.Count == 0) return null;
        var e = _undo[^1]; _undo.RemoveAt(_undo.Count - 1);
        Apply(s, e, back: true);
        _redo.Add(e);
        return e.Label;
    }

    /// <summary>Do the last undone action again. Returns its label, null when there is nothing to redo,
    /// or "msg_no_money" when it can no longer be paid for.</summary>
    public string Redo(GameState s)
    {
        if (_redo.Count == 0) return null;
        var e = _redo[^1];
        if (e.Spent > 0 && !s.CanAfford(e.Spent)) return "msg_no_money";
        _redo.RemoveAt(_redo.Count - 1);
        Apply(s, e, back: false);
        _undo.Add(e);
        return e.Label;
    }

    static Diff<T> DiffOf<T>(List<T> before, List<T> after) where T : class
    {
        var b = new HashSet<T>(before, ReferenceEqualityComparer.Instance as IEqualityComparer<T>);
        var a = new HashSet<T>(after, ReferenceEqualityComparer.Instance as IEqualityComparer<T>);
        return new Diff<T> { Added = after.Where(x => !b.Contains(x)).ToList(), Removed = before.Where(x => !a.Contains(x)).ToList() };
    }

    static void Swap<T>(List<T> list, Diff<T> d, bool back)
    {
        foreach (var x in back ? d.Added : d.Removed) list.Remove(x);
        foreach (var x in back ? d.Removed : d.Added) if (!list.Contains(x)) list.Add(x);
    }

    static void Apply(GameState s, Entry e, bool back)
    {
        Swap(s.Buildings, e.Buildings, back);
        Swap(s.Rooms, e.Rooms, back);
        Swap(s.Doors, e.Doors, back);
        Swap(s.Objects, e.Objects, back);
        Swap(s.Curves, e.Curves, back);
        Swap(s.Ducts, e.Ducts, back);
        foreach (var (i, before, after) in e.Surfaces) s.Surfaces[i] = back ? before : after;
        foreach (var (i, before, after) in e.Fences) s.Fences[i] = back ? before : after;
        if (e.Spent != 0) s.Refund(back ? e.Spent : -e.Spent);
        s.Touch();
    }
}
