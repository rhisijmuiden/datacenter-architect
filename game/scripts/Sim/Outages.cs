using System;
using System.Collections.Generic;
using System.Linq;

namespace DatacenterArchitect.Sim;

/// <summary>Why a group of racks is off or offline.</summary>
public enum OutageCause
{
    // power
    NoHall, NoPdu, PduUnfed, PduFull, Chain, Blackout,
    // cooling
    NoCrah, NoReject, Suppressed,
    // network
    NoCarrier, NoMmr, NoCore, NoEdge, NoUplink, EdgeFull,
}

/// <summary>What the fix button of an outage does.</summary>
public enum FixKind { None, Place, PowerTree, Power, Network, Incidents }

/// <summary>
/// One reason why racks in a hall are down, with the hall, how many racks, a text and the fix:
/// "DH-03: 6 racks off, no PDU in this hall" with a button "Place PDU in DH-03".
/// Text keys and their arguments stay here as data; the UI formats them.
/// </summary>
public class Outage
{
    public Room Hall;
    public OutageCause Cause;
    public int Racks;
    /// <summary>Text key (outage_*) and its arguments; {0} is always the hall code and {1} the number of racks.</summary>
    public string Key;
    public object[] Args = Array.Empty<object>();
    /// <summary>Text key of the fix button (fix_*) and its arguments.</summary>
    public string FixKey;
    public object[] FixArgs = Array.Empty<object>();
    public FixKind Fix;
    /// <summary>With <see cref="FixKind.Place"/>: what to place.</summary>
    public ObjKind Place;
    /// <summary>Where to look: a rack (racks outside a hall) or the room the fix goes into (switchgear room, MMR).</summary>
    public PlacedObject At;
    public Room FixRoom;
    /// <summary>Power and cooling switch racks off; the network leaves them running but offline.</summary>
    public bool Offline => Cause >= OutageCause.NoCarrier;
    /// <summary>Stable id for "is this warning new" (hall + cause).</summary>
    public string Id => $"{Hall?.Id ?? -1}:{Cause}";
}

public static class Outages
{
    /// <summary>The smallest PDU with enough connections for this many racks (the largest when none is big enough).</summary>
    public static ObjKind PduFor(int racks)
    {
        foreach (var k in Catalog.PduKinds) if (Catalog.Objects[k].Outlets >= racks) return k;
        return Catalog.PduKinds[^1];
    }

    /// <summary>How many PDUs of this size the racks that are off need.</summary>
    static int Count(int racks, ObjDef d) => Math.Max(1, (int)Math.Ceiling(racks / (double)Math.Max(1, d.Outlets)));

    static string Kw(double kw) => Math.Round(kw).ToString("0", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Every reason racks are off or offline right now, per hall, most serious first.</summary>
    public static List<Outage> For(GameState s) => For(s, s.Infra, s.Net);

    public static List<Outage> For(GameState s, InfraReport inf, NetReport net)
    {
        var list = new List<Outage>();
        if (inf == null) return list;
        var c = s.Creative;
        var racks = s.Objects.Where(o => o.Kind == ObjKind.Rack && o.Status == ObjStatus.Live && !o.Broken).ToList();
        var byCause = new Dictionary<(Room, DownReason), List<PlacedObject>>();
        foreach (var o in racks)
        {
            if (!inf.Down.Contains(o.Id)) continue;
            var why = inf.Reason(o.Id);
            var hall = why == DownReason.NoHall ? null : s.Rooms.FirstOrDefault(r => r.Type == RoomType.DataHall && r.Floor == o.Floor && r.Contains(o.X, o.Z));
            var key = (hall, why);
            if (!byCause.TryGetValue(key, out var l)) byCause[key] = l = new List<PlacedObject>();
            l.Add(o);
        }
        var switchRoom = s.Rooms.FirstOrDefault(r => r.Type == RoomType.Switchgear && r.Stage == Stage.Done);
        var mmr = s.Rooms.FirstOrDefault(r => r.Type == RoomType.MeetMe && r.Stage == Stage.Done);
        foreach (var ((hall, why), group) in byCause)
        {
            var code = hall != null ? s.HallCode(hall) : "-";
            var n = group.Count;
            var hi = inf.Halls.FirstOrDefault(h => h.Room == hall);
            var o = new Outage { Hall = hall, Racks = n, Args = new object[] { code, n } };
            switch (why)
            {
                case DownReason.NoHall:
                    o.Cause = OutageCause.NoHall; o.Key = "outage_nohall"; o.At = group[0];
                    o.FixKey = "fix_show_rack"; o.Fix = FixKind.None;
                    break;
                case DownReason.NoPdu:
                {
                    var unfed = inf.Tree?.SourcesIn(hall).Any(x => !x.Connected) == true;
                    if (unfed)
                    {
                        // there is a PDU, but no switchboard or UPS output is left for it
                        var msbFull = inf.Tree.Msbs.Count > 0;
                        o.Cause = OutageCause.PduUnfed; o.Key = msbFull ? "outage_pdu_unfed_full" : "outage_pdu_unfed";
                        o.Fix = FixKind.Place; o.Place = ObjKind.Switchgear; o.FixRoom = switchRoom;
                        o.FixKey = switchRoom != null ? "fix_place_msb" : "fix_build_switch_room";
                        if (switchRoom == null) o.Fix = FixKind.PowerTree;
                    }
                    else
                    {
                        var kind = PduFor(n);
                        var d = Catalog.Objects[kind];
                        o.Cause = OutageCause.NoPdu; o.Key = "outage_nopdu";
                        o.Fix = FixKind.Place; o.Place = kind;
                        o.FixArgs = new object[] { Kw(d.CapacityKw), code, d.Outlets, Count(n, d) }; o.FixKey = Count(n, d) > 1 ? "fix_place_pdus" : "fix_place_pdu";
                    }
                    break;
                }
                case DownReason.PduFull:
                {
                    var sources = inf.Tree?.SourcesIn(hall).Where(x => x.Connected).ToList() ?? new List<PowerNode>();
                    var used = sources.Sum(x => x.UsedOutlets);
                    var outlets = sources.Where(x => x.Outlets != int.MaxValue).Sum(x => x.Outlets);
                    var kind = PduFor(n);
                    var d = Catalog.Objects[kind];
                    o.Cause = OutageCause.PduFull; o.Key = "outage_pdufull";
                    o.Args = new object[] { code, n, sources.Count, used, outlets, Kw(hi?.DistributionKw ?? 0) };
                    o.Fix = FixKind.Place; o.Place = kind;
                    o.FixArgs = new object[] { Kw(d.CapacityKw), code, d.Outlets, Count(n, d) }; o.FixKey = Count(n, d) > 1 ? "fix_place_pdus" : "fix_place_pdu_extra";
                    break;
                }
                case DownReason.Chain:
                    if (inf.Blackout)
                    {
                        o.Cause = OutageCause.Blackout; o.Key = "outage_blackout"; o.Fix = FixKind.Power; o.FixKey = "fix_open_power";
                    }
                    else
                    {
                        o.Cause = OutageCause.Chain; o.Key = "outage_chain";
                        o.Args = new object[] { code, n, "link_" + inf.Bottleneck?.Link.ToString().ToLowerInvariant() };
                        o.Fix = FixKind.PowerTree; o.FixKey = "fix_open_power_tree";
                    }
                    break;
                case DownReason.Heat:
                {
                    var it = hi?.ItKw ?? 0; var crah = hi?.CrahKw ?? 0;
                    if (crah < it)
                    {
                        var per = Catalog.Objects[ObjKind.Crah].CoolingKw * (1 + Catalog.Containments[hall.Containment].CoolingBonus) * s.CrahFactor;
                        var more = Math.Max(1, (int)Math.Ceiling((it - crah) / Math.Max(1, per)));
                        o.Cause = OutageCause.NoCrah; o.Key = "outage_heat_crah"; o.Args = new object[] { code, n, Kw(crah), Kw(it) };
                        o.Fix = FixKind.Place; o.Place = ObjKind.Crah; o.FixKey = "fix_place_crah"; o.FixArgs = new object[] { more, code };
                    }
                    else
                    {
                        o.Cause = OutageCause.NoReject; o.Key = "outage_heat_reject"; o.Args = new object[] { code, n, Kw(inf.RejectKw), Kw(inf.HeatKw) };
                        o.Fix = FixKind.Place; o.Place = ObjKind.Chiller; o.FixKey = "fix_place_chiller";
                    }
                    break;
                }
                case DownReason.Suppressed:
                    o.Cause = OutageCause.Suppressed; o.Key = "outage_suppressed"; o.Fix = FixKind.Incidents; o.FixKey = "fix_open_incidents";
                    break;
                default:
                    continue;
            }
            list.Add(o);
        }

        // ---- network: racks that run, but have no way to the internet ----
        if (net != null && !c.NoNetworkLimit && net.OfflineRacks > 0)
        {
            foreach (var hn in net.Halls)
            {
                var off = hn.Racks - hn.Online;
                if (off <= 0) continue;
                var code = s.HallCode(hn.Room);
                var o = new Outage { Hall = hn.Room, Racks = off, Args = new object[] { code, off } };
                if (!net.Upstream) { o.Cause = OutageCause.NoCarrier; o.Key = "outage_nocarrier"; o.Fix = FixKind.Network; o.FixKey = "fix_open_carriers"; }
                else if (!net.Mmr) { o.Cause = OutageCause.NoMmr; o.Key = "outage_nommr"; o.Fix = mmr != null ? FixKind.Place : FixKind.Network; o.Place = ObjKind.Odf; o.FixRoom = mmr; o.FixKey = mmr != null ? "fix_place_odf" : "fix_build_mmr"; }
                else if (net.Cores == 0) { o.Cause = OutageCause.NoCore; o.Key = "outage_nocore"; o.Fix = FixKind.Place; o.Place = ObjKind.CoreRouter; o.FixRoom = mmr; o.FixKey = "fix_place_core"; }
                else if (hn.Switches.Count == 0)
                {
                    var kind = off > 24 ? ObjKind.EdgeSwitch48 : ObjKind.EdgeSwitch24;
                    o.Cause = OutageCause.NoEdge; o.Key = "outage_noedge"; o.Fix = FixKind.Place; o.Place = kind;
                    o.FixKey = "fix_place_edge"; o.FixArgs = new object[] { Catalog.Objects[kind].Ports, code };
                }
                else if (hn.Spines == 0)
                {
                    o.Cause = OutageCause.NoUplink; o.Key = "outage_nouplink"; o.Fix = FixKind.Place; o.Place = ObjKind.DistSwitch; o.FixRoom = mmr; o.FixKey = "fix_place_dist";
                }
                else
                {
                    var kind = off > 24 ? ObjKind.EdgeSwitch48 : ObjKind.EdgeSwitch24;
                    o.Cause = OutageCause.EdgeFull; o.Key = "outage_edgefull"; o.Args = new object[] { code, off, hn.UsedPorts, hn.Ports };
                    o.Fix = FixKind.Place; o.Place = kind; o.FixKey = "fix_place_edge_extra"; o.FixArgs = new object[] { Catalog.Objects[kind].Ports, code };
                }
                list.Add(o);
            }
        }
        return list.OrderBy(x => x.Offline).ThenByDescending(x => x.Racks).ThenBy(x => x.Hall?.Id ?? -1).ToList();
    }
}
