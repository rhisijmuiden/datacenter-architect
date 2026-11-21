using System;
using System.Collections.Generic;
using System.Linq;
using DatacenterArchitect.Core;
using DatacenterArchitect.Sim;
using DatacenterArchitect.UI;
using DatacenterArchitect.World;
using Godot;

namespace DatacenterArchitect;

/// <summary>
/// Racks that are off or offline: a warning per hall and cause ("DH-03: 6 racks off, no PDU"), which jumps the camera
/// to the hall when clicked and starts the fix (the PDU tool, the power tree, the carriers screen ...).
/// </summary>
public partial class Main
{
    /// <summary>Outages already warned about (hall + cause), so each one is told once until it is solved.</summary>
    readonly HashSet<string> _outagesSeen = new();
    bool _outagesPrimed;

    /// <summary>Called once a second: warn about new outages.</summary>
    void CheckOutages()
    {
        if (_state == null) return;
        var list = Outages.For(_state);
        var now = list.Select(o => o.Id).ToHashSet();
        foreach (var o in list)
        {
            if (_outagesSeen.Contains(o.Id)) continue;
            // a loaded game does not shout about everything that was already off
            if (_outagesPrimed)
            {
                var target = o;
                _hud.Notify(OutageUi.Text(o) + "\n▸ " + OutageUi.FixText(o), o.Offline ? Brand.Warning : Brand.Critical, () => FixOutage(target));
            }
        }
        _outagesSeen.Clear();
        _outagesSeen.UnionWith(now);
        _outagesPrimed = true;
    }

    /// <summary>Forget what was warned about (new or loaded game).</summary>
    void ResetOutages() { _outagesSeen.Clear(); _outagesPrimed = false; }

    /// <summary>The outage a rack belongs to, if it is off or offline.</summary>
    Outage OutageFor(PlacedObject rack, InfraReport infra)
    {
        var down = infra.Down.Contains(rack.Id);
        if (!down && !_state.Net.Offline.Contains(rack.Id)) return null;
        var list = Outages.For(_state, infra, _state.Net);
        if (!down) return list.FirstOrDefault(x => x.Offline && x.Hall != null && x.Hall.Floor == rack.Floor && x.Hall.Contains(rack.X, rack.Z));
        var want = infra.Reason(rack.Id) switch
        {
            DownReason.NoHall => new[] { OutageCause.NoHall },
            DownReason.NoPdu => new[] { OutageCause.NoPdu, OutageCause.PduUnfed },
            DownReason.PduFull => new[] { OutageCause.PduFull },
            DownReason.Chain => new[] { OutageCause.Chain, OutageCause.Blackout },
            DownReason.Heat => new[] { OutageCause.NoCrah, OutageCause.NoReject },
            DownReason.Suppressed => new[] { OutageCause.Suppressed },
            _ => Array.Empty<OutageCause>(),
        };
        return list.FirstOrDefault(x => want.Contains(x.Cause) && (x.Hall == null || (x.Hall.Floor == rack.Floor && x.Hall.Contains(rack.X, rack.Z))));
    }

    /// <summary>Look at a room from above, roofs off on its floor, and inspect it.</summary>
    void ShowRoom(Room r)
    {
        if (r == null) return;
        SetView(r.Floor, false);
        _cam.FlyTo(new Vector3(r.X + r.W / 2f, WorldView.Base(r.Floor), r.Z + r.D / 2f), Math.Max(18, Math.Max(r.W, r.D) * 1.6f));
        Inspect(new Vector2I(r.X + r.W / 2, r.Z + r.D / 2));
    }

    /// <summary>Jump to the hall (or the room the fix goes in) and start the fix.</summary>
    void FixOutage(Outage o)
    {
        if (o == null || !_playing) return;
        if (o.At != null) { CloseDrawer(); ShowObject(o.At); return; }
        switch (o.Fix)
        {
            case FixKind.Place:
                CloseDrawer();
                ShowRoom(o.FixRoom ?? o.Hall);
                _hud.Pick(new BuildItem(Mode.Object, (int)o.Place));
                break;
            case FixKind.PowerTree:
                ShowRoom(o.Hall);
                OpenPanel(PanelKind.Power, PowerPanel.TabTree);
                break;
            case FixKind.Power: ShowRoom(o.Hall); OpenPower(); break;
            case FixKind.Network: ShowRoom(o.Hall); OpenNetwork(); break;
            case FixKind.Incidents: ShowRoom(o.Hall); OpenStaff(StaffPanel.TabIncidents); break;
            default: ShowRoom(o.Hall); break;
        }
    }
}
