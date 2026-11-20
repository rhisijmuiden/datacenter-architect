using System;
using System.Collections.Generic;
using System.Linq;

namespace DatacenterArchitect.Sim;

/// <summary>A planned customer migration: one moving truck for a deal, in or out.</summary>
public readonly record struct MoveJob(int DealId, bool Out, List<int> Racks);

/// <summary>
/// Customer migrations (step 10): a big customer that signs moves in visibly with a convoy of moving trucks
/// full of servers, and a customer that leaves sends trucks to collect them. The trucks back up to the loading
/// dock one after another and movers push trolleys between the dock and the racks. It is only for looks:
/// the contract itself starts and ends as before. Nothing here is saved; a loaded game simply starts fresh.
/// </summary>
public partial class Simulation
{
    public const int ConvoyFrom = 8, MoversPerTruck = 2;
    public const double MoveHours = 1.5;
    /// <summary>Moving trucks still to come, in order.</summary>
    public readonly List<MoveJob> MoveQueue = new();
    readonly Dictionary<int, List<int>> _dealRacks = new();
    readonly HashSet<int> _convoyed = new();
    bool _movesPrimed;

    /// <summary>Trucks for a migration of this many racks: one per ten racks, up to four.</summary>
    public static int ConvoySize(int racks) => Math.Clamp(1 + racks / 10, 1, 4);

    static bool Moves(Deal d) => !d.Legacy && d.Product != Product.PerU;

    /// <summary>Spot deals that just started (big ones get a convoy) and deals that just ended (they move out).</summary>
    public void TrackMigrations()
    {
        var active = S.Deals.Where(d => d.State == DealState.Active && Moves(d) && d.RackIds.Count > 0).ToList();
        if (!_movesPrimed)
        {
            // a loaded game: remember what is there without sending trucks for old news
            _movesPrimed = true;
            foreach (var d in active) { _dealRacks[d.Id] = d.RackIds.ToList(); if (S.Day - d.StartDay >= 2) _convoyed.Add(d.Id); }
        }
        foreach (var (id, racks) in _dealRacks.ToList())
        {
            var d = S.DealById(id);
            if (d != null && d.State == DealState.Active) continue;
            _dealRacks.Remove(id);
            if (racks.Count == 0) continue;
            var n = ConvoySize(racks.Count);
            for (var i = 0; i < n; i++) MoveQueue.Add(new MoveJob(id, true, racks));
            if (d != null) Say("msg_moving_out", GameState.DisplayArg(d), 1);
        }
        foreach (var d in active)
        {
            _dealRacks[d.Id] = d.RackIds.ToList();
            if (_convoyed.Contains(d.Id) || d.Racks < ConvoyFrom) continue;
            _convoyed.Add(d.Id);
            var n = ConvoySize(d.Racks);
            for (var i = 0; i < n; i++) MoveQueue.Add(new MoveJob(d.Id, false, null));
            Say("msg_moving_in", GameState.DisplayArg(d) + "¦" + n, 1);
        }
    }

    /// <summary>Send the next moving truck when the dock is free, during the day. True when a migration is running.</summary>
    bool DispatchMigration(double hour)
    {
        if (MoveQueue.Count == 0) return false;
        if (hour < 6 || hour >= 21) return true;
        if (Agents.Any(a => a.Role == Role.Truck && a.Load == TruckLoad.Customer)) return true;
        var docks = Docks();
        if (docks.Count == 0) { MoveQueue.Clear(); return false; }
        var job = MoveQueue[0];
        MoveQueue.RemoveAt(0);
        var aprons = docks.Select(d => d.apron).ToHashSet();
        var items = job.Out ? job.Racks.ToList() : new List<int> { job.DealId };
        if (!SendTruck(TruckLoad.Customer, (x, z) => aprons.Contains((x, z)), job.Out ? 0 : 1, items)) { MoveQueue.Clear(); return false; }
        var t = Agents[^1];
        t.Target = job.DealId; t.Out = job.Out;
        return true;
    }
}
