using System;
using System.Linq;
using System.Text.Json.Serialization;

namespace DatacenterArchitect.Sim;

/// <summary>
/// Step 11 round 2: grid congestion. Like in the real Dutch grid, a bigger connection is not built at once: you join the
/// grid operator's queue first. Each place in the queue takes a month; then the normal lead time of the works starts.
/// A switch in the new-game dialog (on by default, off for older saves). Sites that help the grid (battery containers)
/// move up one place.
/// </summary>
public partial class GameState
{
    /// <summary>Days one place in the queue takes.</summary>
    public const double GridQueueStep = 30;

    /// <summary>Days still waiting in the queue before the works of <see cref="GridRequest"/> start (0 = works running).</summary>
    public double GridQueueDays { get; set; }

    [JsonIgnore] public bool InGridQueue => GridRequest >= 0 && GridQueueDays > 0;
    /// <summary>Place in the queue, 1 = next in line.</summary>
    [JsonIgnore] public int GridQueuePlace => InGridQueue ? (int)Math.Ceiling(GridQueueDays / GridQueueStep) : 0;

    /// <summary>How many places are before you for a level: more for heavier connections, more on hard, one less with batteries.</summary>
    public int GridQueueLength(int level)
    {
        if (!Options.GridQueue || Creative.InstantBuild) return 0;
        var n = level switch { <= 0 => 0, 1 => 1, 2 => 3, _ => 5 };
        if (Options.Difficulty >= 2) n += 1;
        if (Options.Difficulty == 0) n -= 1;
        if (Objects.Any(o => o.Kind == ObjKind.BatteryContainer && o.Status == ObjStatus.Live)) n -= 1;
        return Math.Max(0, n);
    }

    /// <summary>Total days from request to connection: queue plus works.</summary>
    public double GridWaitDays(int level) => GridQueueLength(level) * GridQueueStep + Catalog.GridLevels[Math.Clamp(level, 0, Catalog.GridLevels.Length - 1)].LeadDays;

    /// <summary>Moves the queue on; returns the hours left over for the works (0 while still waiting).</summary>
    double GridQueueTick(double hours)
    {
        if (GridQueueDays <= 0) return hours;
        var before = GridQueuePlace;
        var left = Math.Max(0, hours - GridQueueDays * 24);
        GridQueueDays = Math.Max(0, GridQueueDays - hours / 24);
        var after = GridQueuePlace;
        if (after != before)
        {
            if (after == 0) Notes.Add(("msg_grid_queue_done", Catalog.GridLevels[GridRequest].Mva.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            else Notes.Add(("msg_grid_queue_place", after.ToString()));
            Touch(false);
        }
        return GridQueueDays > 0 ? 0 : left;
    }
}
