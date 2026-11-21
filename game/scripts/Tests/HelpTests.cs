using System;
using System.Linq;
using DatacenterArchitect.Core;
using DatacenterArchitect.Sim;
using DatacenterArchitect.UI;

namespace DatacenterArchitect.Tests;

/// <summary>Playtest: the in-game help covers every room in both languages, and the power tree shows the route to N+1 and 2N.</summary>
public static partial class TestRunner
{
    static void HelpTests()
    {
        Loc.Load();
        var en = Loc.Table["en"]; var nl = Loc.Table["nl"];
        var rooms = HelpPanel.DatacenterRooms.Concat(HelpPanel.OfficeRooms).ToList();
        Check(rooms.Distinct().Count() == rooms.Count && Enum.GetValues<RoomType>().All(rooms.Contains), "the help lists every room type once");
        var missing = rooms.SelectMany(r => new[] { "what", "when", "tip" }.Select(p => HelpPanel.Key(r, p)))
            .Concat(HelpPanel.Topics.SelectMany(t => new[] { "help_topic_" + t, "help_topic_" + t + "_body" }))
            .Where(k => !en.ContainsKey(k) || !nl.ContainsKey(k)).ToList();
        Check(missing.Count == 0, "every help page has English and Dutch text: " + string.Join(", ", missing));
        foreach (var r in rooms)
        {
            var min = HelpPanel.Minimum(r);
            Check(min.Count >= 2 && !min[0].advised, $"{r}: the help lists a door and what goes inside");
        }
        var hall = HelpPanel.Minimum(RoomType.DataHall);
        Check(hall.Count(x => !x.advised) == 1 + GameState.RoomMinimum[RoomType.DataHall].Length, "the data hall minimum comes from the same rules as the inspector");

        // the route to N+1 and 2N on the demo site
        var s = Quiet();
        var r0 = s.Infra;
        var route = PowerPanel.Route(s, r0);
        Check(route.Count > 0, $"the power tree shows a route ({route.Count} lines)");
        Check(route.All(x => x.level is "N-" or "N" or "N+1" or "2N" or "A+B" && !string.IsNullOrWhiteSpace(x.text) && !x.text.StartsWith("ptree_")), "every route line has a level and a text");
        Check(route.Count(x => x.text.Contains(s.HallCode(s.Rooms.First(h => h.Type == RoomType.DataHall)))) >= 1, "each hall with racks gets a line");
        // a UPS link at N tells you how big the extra unit must be
        var ups = r0.Links.First(l => l.Link == PowerLink.Ups);
        Check(ups.Redundancy == "-" || route.Any(x => x.text.StartsWith(Loc.T("ptree_route_ups"))), "the UPS link has a route line");
    }
}
