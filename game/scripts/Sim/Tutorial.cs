using System.Collections.Generic;
using System.Linq;

namespace DatacenterArchitect.Sim;

/// <summary>
/// One tip of the tutorial: the goal of the checklist it explains, the build-bar category that holds what you need,
/// or the rail section (and tab) to open. Texts: "tut_{goal}" (what to do and why).
/// </summary>
public record TutorialStep(string Goal, string Category = null, int Section = -1, int Tab = 0);

/// <summary>
/// Step 11 round 2: the tutorial coach. It follows the goal checklist of a first site and explains each next goal:
/// where it is in the build bar, how to build it and why a datacenter needs it. Ticked in the new-game dialog.
/// </summary>
public static class Tutorial
{
    public const string Welcome = "welcome", Finished = "finished";

    public static readonly TutorialStep[] Steps =
    {
        new("goal_driveway", "cat_land"),
        new("goal_yard", "cat_land"),
        new("goal_building", "cat_buildings"),
        new("goal_hall", "cat_rooms"),
        new("goal_dock", "cat_rooms"),
        new("goal_rack", "cat_halls"),
        new("goal_office", "cat_rooms"),
        new("goal_ceo", null, 1, 0),
        new("goal_sales", null, 1, 0),
        new("goal_switchgear", "cat_power"),
        new("goal_ups", "cat_power"),
        new("goal_pdu", "cat_power"),
        new("goal_cooling", "cat_cooling"),
        new("goal_generators", "cat_power"),
        new("goal_fiber", "cat_network"),
        new("goal_carrier", null, 3, 1),
        new("goal_network", "cat_network"),
        new("goal_contract", null, 2, 0),
    };

    /// <summary>
    /// The tip to show now: the welcome first, then the first step whose goal is not done and was not skipped,
    /// and "finished" once all are done. Null when the tutorial is off.
    /// </summary>
    public static string Current(GameState s, IEnumerable<(string key, bool done)> goals)
    {
        if (s == null || !s.Options.Tutorial) return null;
        if (!s.TutorialSkipped.Contains(Welcome)) return Welcome;
        var done = goals.Where(g => g.done).Select(g => g.key).ToHashSet();
        foreach (var st in Steps)
            if (!done.Contains(st.Goal) && !s.TutorialSkipped.Contains(st.Goal)) return st.Goal;
        return Finished;
    }

    public static TutorialStep Step(string goal) => Steps.FirstOrDefault(x => x.Goal == goal);

    /// <summary>Number of the tip (1-based) for "step 3 of 18".</summary>
    public static int Index(string goal) => System.Array.FindIndex(Steps, x => x.Goal == goal) + 1;
}

public partial class GameState
{
    /// <summary>Tutorial tips the player clicked away (goal keys, "welcome").</summary>
    public List<string> TutorialSkipped { get; set; } = new();
}
