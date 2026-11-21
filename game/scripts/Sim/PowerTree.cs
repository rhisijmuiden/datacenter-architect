using System;
using System.Collections.Generic;
using System.Linq;

namespace DatacenterArchitect.Sim;

public enum PowerNodeKind { Msb, Ups, Pdu, Busbar, Mech }

/// <summary>Why a rack is off. The hall inspector and tips use it to say what to build.</summary>
public enum DownReason { None, NoHall, NoPdu, PduFull, Chain, Heat, Suppressed }

/// <summary>
/// One device in the power distribution tree: a main switchboard (MSB), a UPS, a PDU or a busbar run.
/// Loads are in kW: <see cref="LoadKw"/> is the normal load (racks fed from A and B draw half from each side),
/// <see cref="FailoverKw"/> the load when the other side fails and every rack draws everything from this side.
/// </summary>
public class PowerNode
{
    public PowerNodeKind Kind;
    public PlacedObject Obj;
    /// <summary>The data hall a PDU or busbar serves (only that hall).</summary>
    public Room Hall;
    public FeedSide Side;
    public string Name;
    public double CapacityKw;
    public double LoadKw, FailoverKw;
    /// <summary>Rack connections (PDU or busbar). A busbar has a tap-off box for every rack: <see cref="int.MaxValue"/>.</summary>
    public int Outlets, UsedOutlets;
    /// <summary>Outgoing feeders (MSB and UPS) for the devices below.</summary>
    public int Outputs, UsedOutputs;
    public int Racks, DualRacks;
    public PowerNode Parent;
    public readonly List<PowerNode> Children = new();
    /// <summary>A PDU or busbar fed straight from the switchboard because no UPS had a free output: no battery behind it.</summary>
    public bool Direct;

    public bool Connected => Kind is PowerNodeKind.Msb or PowerNodeKind.Mech || Parent != null;
    public bool Unlimited => Outlets == int.MaxValue;
    public int FreeOutlets => Unlimited ? int.MaxValue : Outlets - UsedOutlets;
    public int FreeOutputs => Outputs - UsedOutputs;
    public double Ratio => CapacityKw <= 0 ? (LoadKw > 0 ? 9 : 0) : LoadKw / CapacityKw;
    public double FailoverRatio => CapacityKw <= 0 ? (FailoverKw > 0 ? 9 : 0) : FailoverKw / CapacityKw;
    public bool Overloaded => CapacityKw > 0 && LoadKw > CapacityKw + 1e-6;
    /// <summary>When the other side fails this device cannot carry everything: no real 2N behind it.</summary>
    public bool FailoverShort => CapacityKw > 0 && FailoverKw > CapacityKw + 1e-6;
    public bool Full => !Unlimited && Outlets > 0 && UsedOutlets >= Outlets;
    /// <summary>The switchboard at the top of this branch.</summary>
    public PowerNode Root { get { var n = this; while (n.Parent != null) n = n.Parent; return n; } }
}

/// <summary>
/// The power distribution of a site as a tree: main switchboards (8 outputs) feed UPS units (6 outputs),
/// UPS units feed the PDUs and busbar runs in the halls, and every PDU feeds only racks in its own hall,
/// one outlet per rack, up to its kW. Devices pick a feeder on their own side first.
/// Built by <see cref="Infra.Analyze"/>; the tree screens, the inspector and tips read it.
/// </summary>
public class PowerTree
{
    public readonly List<PowerNode> Msbs = new();
    public readonly List<PowerNode> Ups = new();
    /// <summary>PDUs and busbar runs, in hall order.</summary>
    public readonly List<PowerNode> Sources = new();
    /// <summary>UPS units, PDUs and busbar runs that found no free output upstream.</summary>
    public readonly List<PowerNode> Unconnected = new();
    public PowerNode Mech;
    public readonly Dictionary<int, PowerNode> ByObject = new();
    /// <summary>The PDU or busbar run each rack draws from, per side (null = no feed from that side).</summary>
    public readonly Dictionary<int, (PowerNode a, PowerNode b)> RackFeeds = new();

    public PowerNode Node(PlacedObject o) => o != null && ByObject.TryGetValue(o.Id, out var n) ? n : null;
    public IEnumerable<PowerNode> SourcesIn(Room hall) => Sources.Where(n => n.Hall == hall);

    /// <summary>Both feeds of a rack, primary first.</summary>
    public (PowerNode a, PowerNode b) Feeds(int rackId) => RackFeeds.TryGetValue(rackId, out var f) ? f : (null, null);

    static int Order(PowerNode n) => n.Obj?.Id ?? 0;

    /// <summary>Pick the least used candidate with a free output, the device's own side first.</summary>
    static PowerNode Pick(IEnumerable<PowerNode> pool, FeedSide side)
    {
        PowerNode best = null;
        foreach (var n in pool)
        {
            if (!n.Connected || n.FreeOutputs <= 0) continue;
            if (best == null) { best = n; continue; }
            var ns = n.Side == side; var bs = best.Side == side;
            if (ns != bs) { if (ns) best = n; continue; }
            if (n.UsedOutputs < best.UsedOutputs) best = n;
        }
        return best;
    }

    static void Attach(PowerNode child, PowerNode parent)
    {
        child.Parent = parent;
        parent.Children.Add(child);
        parent.UsedOutputs++;
    }

    /// <summary>Build the tree for these live devices and racks per hall (racks in id order).</summary>
    public static PowerTree Build(GameState s, List<PlacedObject> live, IReadOnlyList<Room> halls, Dictionary<Room, List<PlacedObject>> byHall)
    {
        var t = new PowerTree();
        var counters = new Dictionary<(string, FeedSide), int>();
        string Name(string prefix, FeedSide side)
        {
            counters.TryGetValue((prefix, side), out var n);
            counters[(prefix, side)] = ++n;
            return $"{prefix}-{side}{n}";
        }
        foreach (var o in live.Where(o => o.Kind == ObjKind.Switchgear).OrderBy(o => o.Id))
        {
            var n = new PowerNode { Kind = PowerNodeKind.Msb, Obj = o, Side = o.Feed, Name = Name("MSB", o.Feed), CapacityKw = o.Def.CapacityKw, Outputs = o.Def.Outputs };
            t.Msbs.Add(n); t.ByObject[o.Id] = n;
        }
        foreach (var o in live.Where(o => o.Kind == ObjKind.Ups).OrderBy(o => o.Id))
        {
            var n = new PowerNode { Kind = PowerNodeKind.Ups, Obj = o, Side = o.Feed, Name = Name("UPS", o.Feed), CapacityKw = o.Def.CapacityKw * s.UpsFactor, Outputs = o.Def.Outputs };
            t.Ups.Add(n); t.ByObject[o.Id] = n;
            var p = Pick(t.Msbs, n.Side);
            if (p != null) Attach(n, p); else t.Unconnected.Add(n);
        }
        var pdus = live.Where(o => Catalog.IsPdu(o.Kind)).OrderBy(o => o.Id).ToList();
        foreach (var h in halls)
        {
            var list = new List<PowerNode>();
            foreach (var o in pdus.Where(o => o.Floor == h.Floor && h.Contains(o.X, o.Z)))
            {
                var n = new PowerNode { Kind = PowerNodeKind.Pdu, Obj = o, Hall = h, Side = o.Feed, Name = Name("PDU", o.Feed), CapacityKw = o.Def.CapacityKw, Outlets = o.Def.Outlets };
                list.Add(n); t.ByObject[o.Id] = n;
            }
            if (h.Busbar)
                foreach (var side in new[] { FeedSide.A, FeedSide.B })
                    list.Add(new PowerNode { Kind = PowerNodeKind.Busbar, Hall = h, Side = side, Name = $"Busbar {side}", CapacityKw = s.BusbarKw, Outlets = int.MaxValue });
            foreach (var n in list)
            {
                var p = Pick(t.Ups, n.Side);
                if (p == null) { p = Pick(t.Msbs, n.Side); n.Direct = p != null; }
                if (p != null) Attach(n, p); else t.Unconnected.Add(n);
                t.Sources.Add(n);
            }
            if (byHall.TryGetValue(h, out var racks)) t.Connect(list.Where(n => n.Connected).ToList(), racks);
        }
        // loads up the tree
        foreach (var u in t.Ups)
        {
            u.LoadKw = u.Children.Sum(c => c.LoadKw); u.FailoverKw = u.Children.Sum(c => c.FailoverKw);
            u.Racks = u.Children.Sum(c => c.Racks); u.DualRacks = u.Children.Sum(c => c.DualRacks);
        }
        foreach (var m in t.Msbs)
        {
            m.LoadKw = m.Children.Sum(c => c.LoadKw); m.FailoverKw = m.Children.Sum(c => c.FailoverKw);
            m.Racks = m.Children.Sum(c => c.Racks); m.DualRacks = m.Children.Sum(c => c.DualRacks);
        }
        return t;
    }

    /// <summary>
    /// Give every rack in a hall a feed: first one feed per rack (so as many racks as possible run), then a second
    /// feed from the other side for A/B. A connection needs a free outlet and room for the rack's full load.
    /// </summary>
    void Connect(List<PowerNode> sources, List<PlacedObject> racks)
    {
        if (sources.Count == 0) return;
        PowerNode Best(double kw, FeedSide? not)
        {
            PowerNode best = null; var bestRatio = double.MaxValue;
            foreach (var n in sources)
            {
                if (not != null && n.Side == not) continue;
                if (n.FreeOutlets <= 0 || n.FailoverKw + kw > n.CapacityKw + 1e-6) continue;
                var ratio = n.CapacityKw > 0 ? n.FailoverKw / n.CapacityKw : 9;
                if (ratio < bestRatio - 1e-9) { best = n; bestRatio = ratio; }
            }
            return best;
        }
        void Take(PowerNode n, double kw) { n.UsedOutlets++; n.FailoverKw += kw; n.Racks++; }
        foreach (var o in racks)
        {
            var kw = o.Def.PowerKw;
            var n = Best(kw, null);
            if (n == null) continue;
            Take(n, kw);
            RackFeeds[o.Id] = (n, null);
        }
        foreach (var o in racks)
        {
            if (!RackFeeds.TryGetValue(o.Id, out var f)) continue;
            var kw = o.Def.PowerKw;
            var n = Best(kw, f.a.Side);
            if (n == null) continue;
            Take(n, kw);
            n.DualRacks++; f.a.DualRacks++;
            RackFeeds[o.Id] = (f.a, n);
        }
        // normal load: a rack on two feeds draws half from each
        foreach (var o in racks)
        {
            if (!RackFeeds.TryGetValue(o.Id, out var f)) continue;
            var kw = o.Def.PowerKw;
            if (f.b == null) f.a.LoadKw += kw;
            else { f.a.LoadKw += kw / 2; f.b.LoadKw += kw / 2; }
        }
    }
}

public partial class GameState
{
    /// <summary>Which scope a power device is balanced in: PDUs per hall, switchboards and UPS units over the site.</summary>
    Room FeedScope(PlacedObject o) => Catalog.IsPdu(o.Kind) ? RoomAt(o.X, o.Z, o.Floor) : null;
    static int FeedClass(ObjKind k) => Catalog.IsPdu(k) ? 0 : k == ObjKind.Ups ? 1 : 2;

    /// <summary>The side a new power device goes on: the side with fewer of its kind (PDUs counted per hall), A on a tie.</summary>
    public FeedSide NextFeed(PlacedObject o)
    {
        var scope = FeedScope(o);
        int a = 0, b = 0;
        foreach (var x in Objects)
        {
            if (x == o || !Catalog.HasFeed(x.Kind) || FeedClass(x.Kind) != FeedClass(o.Kind)) continue;
            if (Catalog.IsPdu(o.Kind) && FeedScope(x) != scope) continue;
            if (x.Feed == FeedSide.A) a++; else b++;
        }
        return b < a ? FeedSide.B : FeedSide.A;
    }

    /// <summary>Older games: give every switchboard, UPS and PDU a side, alternating in the order they were placed.</summary>
    public void AssignFeedSides()
    {
        var seen = new Dictionary<(int, Room), int>();
        foreach (var o in Objects.Where(o => Catalog.HasFeed(o.Kind)).OrderBy(o => o.Id))
        {
            var key = (FeedClass(o.Kind), FeedScope(o));
            seen.TryGetValue(key, out var n);
            o.Feed = n % 2 == 0 ? FeedSide.A : FeedSide.B;
            seen[key] = n + 1;
        }
    }

    /// <summary>Move a switchboard, UPS or PDU to the other side.</summary>
    public string SwitchFeed(PlacedObject o)
    {
        if (o == null || !Catalog.HasFeed(o.Kind)) return "msg_nothing_changed";
        o.Feed = o.Feed == FeedSide.A ? FeedSide.B : FeedSide.A;
        Touch(false);
        return null;
    }
}
