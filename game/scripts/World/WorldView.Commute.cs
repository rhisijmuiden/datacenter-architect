using System;
using System.Collections.Generic;
using System.Linq;
using DatacenterArchitect.Sim;
using Godot;

namespace DatacenterArchitect.World;

/// <summary>
/// Commuting (playtest): bikes of the staff on duty stand in the bike racks and the bike shed (own Blender models,
/// tools/models/blender/commute.py), refreshed every game hour so they come and go with the shifts.
/// </summary>
public partial class WorldView
{
    public static readonly string[] BikeModels = { "bicycle_a", "bicycle_b", "bicycle_c", "bicycle_d", "ebike" };

    Node3D _bikes;
    (int layout, int hour, int staff) _bikeKey = (-1, -1, -1);

    void SyncBikes()
    {
        var key = (_state.LayoutRevision, (int)_state.Hours, _state.Staff.Count);
        if (key == _bikeKey) return;
        _bikeKey = key;
        if (_bikes == null) { _bikes = new Node3D { Name = "Bikes" }; AddChild(_bikes); }
        foreach (var c in _bikes.GetChildren()) c.QueueFree();
        var modes = Commute.Today(_state);
        var riders = _state.Staff.Where(e => _state.OnDuty(e) && modes.TryGetValue(e.Id, out var m) && m == CommuteMode.Bike).Select(e => e.Id).ToList();
        // a few bikes stay overnight (night shift, or someone who took the train home)
        var count = riders.Count + (int)Math.Min(2, _state.Staff.Count / 10);
        var xf = BikeModels.ToDictionary(m => m, _ => new List<Transform3D>());
        var i = 0;
        foreach (var o in Commute.BikePlaces(_state).OrderBy(o => o.Id))
        {
            var shed = o.Kind == ObjKind.BikeShed;
            var places = shed ? Commute.ShedPlaces : Commute.RackPlaces;
            var (w, d) = GameState.Footprint(o);
            var basis = new Basis(Vector3.Up, -Mathf.Pi / 2 * o.Rot);
            var centre = new Vector3(o.X + w / 2f, 0, o.Z + d / 2f);
            for (var k = 0; k < places && i < count; k++, i++)
            {
                var id = i < riders.Count ? riders[i] : 1000 + i;
                // e-bikes go in the shed first, where they can charge
                var model = shed && id % 3 != 0 ? "ebike" : BikeModels[Math.Abs(id) % 4];
                var local = shed ? new Vector3(-1.71f + k * 0.38f, 0, -0.05f) : new Vector3(-0.8f + k * 0.4f, 0, 0.1f);
                var lean = new Basis(Vector3.Forward, (k % 2 == 0 ? 1 : -1) * 0.04f);
                xf[model].Add(new Transform3D(basis * lean, centre + basis * local));
            }
        }
        foreach (var (m, list) in xf)
            if (list.Count > 0) Assets.Many(_bikes, Assets.Mesh("made/" + m + ".glb", Models.MadeMat), list);
    }
}
