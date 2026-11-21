using System;
using System.Collections.Generic;
using System.Linq;
using DatacenterArchitect.Core;
using DatacenterArchitect.Sim;
using Godot;

namespace DatacenterArchitect.UI;

/// <summary>
/// Network tree tab: incoming fiber pairs per carrier, core routers with their capacity, distribution switches and the
/// edge switches of every hall with their ports (= racks they can connect). An edge switch only serves its own hall.
/// The selected row shows its details, a port map and a tip on the right.
/// </summary>
public partial class NetworkPanel
{
    /// <summary>Changes that need a rebuild of the tree (the numbers in it update live).</summary>
    string TreeSig(NetReport n) =>
        string.Join(",", n.CoreList.Select(c => c.Obj.Id)) + "|" + string.Join(",", n.Dists.Select(d => $"{d.Obj.Id}:{d.Linked}")) + "|" +
        string.Join(",", n.Uplink.OrderBy(x => x.Key).Select(x => $"{x.Key}:{x.Value}")) + "|" +
        string.Join(",", n.Halls.Select(h => $"{h.Room.Id}:{h.Switches.Count}:{(h.Racks > 0 ? 1 : 0)}")) + $"|{n.External}|{_sel}";

    static int HallNo(NetReport n, Room room) => n.Halls.FindIndex(h => h.Room == room) + 1;
    public static string HallLabel(NetReport n, Room room) => Loc.T("ntree_hall", HallNo(n, room));
    static string CoreName(NetReport n, PlacedObject o) => $"CR-{n.CoreList.FindIndex(c => c.Obj == o) + 1}";
    static string DistName(NetReport n, PlacedObject o) => $"DS-{n.Dists.FindIndex(d => d.Obj == o) + 1}";
    static string EdgeGroupName(NetReport n, HallNet h) => $"ES-{HallNo(n, h.Room):00}" + (h.Switches.Count >= 2 ? " A/B" : "");

    static string Model(ObjKind k) => k switch
    {
        ObjKind.EdgeSwitch24 => "Edge 24", ObjKind.EdgeSwitch48 => "Edge 48", ObjKind.Spine => "Spine 400", _ => Loc.T(Catalog.Objects[k].NameKey),
    };

    static string Models(HallNet h)
    {
        var kinds = h.Switches.Select(o => o.Kind).Distinct().ToList();
        return kinds.Count == 1 ? $"{Model(kinds[0])} × {h.Switches.Count}" : string.Join(" + ", h.Switches.GroupBy(o => o.Kind).Select(g => $"{Model(g.Key)} × {g.Count()}"));
    }

    static string PortSpeed(ObjKind k) => k == ObjKind.Spine ? "100G" : "25G";
    static string UplinkText(ObjKind k) => $"{Catalog.Uplinks(k)} × {(k is ObjKind.Spine or ObjKind.DistSwitch ? "400G" : "100G")}";

    /// <summary>The hall switch group shown under a distribution switch (or under "straight on the core", key 0; -1 = no uplink).</summary>
    static int GroupParent(NetReport n, HallNet h)
    {
        foreach (var e in h.Switches) if (n.Uplink.TryGetValue(e.Id, out var up) && up >= 0) return up;
        return -1;
    }

    void Select(string key) { _sel = key; _sig = null; Refresh(); }

    TreeRow Row(Container parent, string key, string icon, Color accent, Action<TreeRow, GameState, NetReport> update, float bar = 80, float value = 118)
    {
        var row = new TreeRow(icon, accent, () => Select(key), bar, value) { Selected = _sel == key };
        parent.AddChild(row);
        _updaters.Add((s, r) => { update(row, s, r); row.TooltipText = row.Title.Text + "\n" + row.Sub.Text; });
        return row;
    }

    void BuildTree(GameState s)
    {
        var n = s.Net;
        if (_sel == null || !ValidSel(n, _sel)) _sel = DefaultSel(n);
        var cols = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        cols.AddThemeConstantOverride("separation", 20);
        _content.AddChild(cols);
        var left = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = 1.7f };
        left.AddThemeConstantOverride("separation", 7);
        var right = new VBoxContainer { CustomMinimumSize = new Vector2(290, 0) };
        right.AddThemeConstantOverride("separation", 12);
        cols.AddChild(left); cols.AddChild(right);

        left.AddChild(Ui.Heading("ntree_title", 14, Brand.Fiber));
        left.AddChild(Ui.Text("ntree_help", 12, Brand.InkDim));

        // ---- incoming fiber pairs ----
        Row(left, "in", "fiber_in", Brand.Fiber, (row, st, r) =>
        {
            row.Title.Text = Loc.T("ntree_incoming", r.MmrRooms);
            row.Sub.Text = Loc.T("ntree_incoming_sub", r.Pairs, r.OdfUsed, r.OdfPorts, r.Routes);
            row.Value.Text = Loc.T("ntree_pairs", r.Pairs);
            row.SetLoad(r.Link(NetLink.Upstream).Ratio, !r.Mmr || r.Pairs == 0);
        }, 0, 110);
        var inner = TreeRows.Branch(left, Brand.Fiber);
        var chips = new HFlowContainer();
        chips.AddThemeConstantOverride("h_separation", 6); chips.AddThemeConstantOverride("v_separation", 6);
        foreach (var c in n.Carriers)
        {
            var state = !c.Contract.Active ? Loc.T("ntree_delivering") : !c.Routed ? Loc.T("ntree_no_route") : $"{c.Contract.PortGbps}G";
            var kind = c.Def.Ix ? "IX" : Loc.T("ntree_transit");
            chips.AddChild(TreeRow.Chip($"{c.Def.Name} · {Loc.T("ntree_one_pair")} · {kind} {state}", null, c.Up ? Brand.Ink : Brand.Warning));
        }
        foreach (var p in s.Peers.Where(p => p.State is PeerState.Active or PeerState.Building))
            chips.AddChild(TreeRow.Chip($"{Catalog.Rivals[p.Rival].Name} · {Loc.T("ntree_one_pair")} · {Loc.T("ntree_peering")} 10G", null, p.State == PeerState.Active ? Brand.Ink : Brand.InkDim));
        if (chips.GetChildCount() == 0) chips.AddChild(TreeRow.Chip(Loc.T("ntree_no_carriers"), new Color(Brand.Warning, 0.16f), Brand.Warning));
        inner.AddChild(chips);

        // ---- core routers ----
        if (n.CoreList.Count == 0)
            Row(inner, "core:0", "router", Brand.Fiber, (row, _, _) => { row.Title.Text = Loc.T("ntree_no_core"); row.Sub.Text = Loc.T("ntree_no_core_sub"); row.Value.Text = ""; row.SetLoad(0, true); });
        foreach (var cn in n.CoreList)
        {
            var id = cn.Obj.Id;
            Row(inner, $"core:{id}", "router", Brand.Fiber, (row, _, r) =>
            {
                var x = r.CoreList.FirstOrDefault(c => c.Obj.Id == id);
                if (x == null) return;
                var i = r.CoreList.IndexOf(x);
                row.Title.Text = $"{CoreName(r, x.Obj)} · {Loc.T("obj_core_router")}" + (i > 0 ? " " + Loc.T("ntree_redundant") : "");
                row.Sub.Text = Loc.T("ntree_core_sub", x.Used, x.Ports, x.Ports - x.Used);
                row.Value.Text = $"{N(x.Load, "0.0")} / {N(x.Capacity)} Gbit/s";
                row.SetLoad(x.Capacity <= 0 ? 0 : x.Load / x.Capacity, r.Cores <= 1 && r.OnlineRacks > 0);
            });
        }
        var dist = TreeRows.Branch(inner, Brand.Fiber);

        // ---- distribution switches with their hall switches ----
        foreach (var dn in n.Dists)
        {
            var id = dn.Obj.Id;
            Row(dist, $"dist:{id}", "dist_switch", Brand.Ink, (row, _, r) =>
            {
                var x = r.Dists.FirstOrDefault(d => d.Obj.Id == id);
                if (x == null) return;
                row.Title.Text = $"{DistName(r, x.Obj)} · {Loc.T("obj_dist_switch")}";
                row.Sub.Text = x.Linked ? Loc.T("ntree_dist_sub", x.Used, x.Ports, x.Edges.Count) : Loc.T("ntree_no_core_port");
                row.Value.Text = $"{N(x.Load, "0.0")} / {N(x.Capacity)} Gbit/s";
                row.SetLoad(x.Capacity <= 0 ? 0 : x.Load / x.Capacity, !x.Linked || x.Used >= x.Ports);
            });
            var under = n.Halls.Where(h => GroupParent(n, h) == id).ToList();
            if (under.Count > 0)
            {
                var b = TreeRows.Branch(dist, Brand.Fiber);
                foreach (var h in under) HallRow(b, h);
            }
        }
        // hall switches straight on the core: fine for a small site, a distribution switch keeps core ports free
        var direct = n.Halls.Where(h => GroupParent(n, h) == 0).ToList();
        if (direct.Count > 0)
        {
            dist.AddChild(Plain(Loc.T("ntree_direct"), 13, Brand.InkDim));
            var b = TreeRows.Branch(dist, Brand.Fiber);
            foreach (var h in direct) HallRow(b, h);
        }
        // hall switches without any free uplink port, and halls with racks but no switch at all
        var stuck = n.Halls.Where(h => h.Switches.Count > 0 && GroupParent(n, h) < 0).ToList();
        var bare = n.Halls.Where(h => h.Switches.Count == 0 && h.Racks > 0).ToList();
        if (stuck.Count + bare.Count > 0)
        {
            dist.AddChild(Plain(Loc.T("ntree_not_connected"), 13, Brand.Warning));
            var b = TreeRows.Branch(dist, Brand.Warning);
            foreach (var h in stuck.Concat(bare)) HallRow(b, h);
        }
        if (n.Halls.Count == 0) dist.AddChild(Plain(Loc.T("ntree_no_halls"), 13, Brand.InkDim));

        BuildDetails(s, n, right);
    }

    void HallRow(Container parent, HallNet h)
    {
        var room = h.Room;
        Row(parent, $"hall:{room.Id}", "edge_switch", Brand.Fiber, (row, _, r) =>
        {
            var x = r.HallOf(room);
            if (x == null) return;
            if (x.Switches.Count == 0)
            {
                row.Title.Text = $"{HallLabel(r, x.Room)} · {Loc.T("ntree_no_edge")}";
                row.Sub.Text = Loc.T("ntree_no_edge_sub", x.Racks);
                row.Value.Text = "";
                row.SetLoad(1.5, true);
                return;
            }
            row.Title.Text = $"{EdgeGroupName(r, x)} · {HallLabel(r, x.Room)}";
            row.Sub.Text = Loc.T("ntree_edge_sub", x.UsedPorts, x.Ports, x.Online, x.Racks) + (x.AB ? " · A/B" : x.Racks > 0 && x.Spines < 2 ? " · " + Loc.T("ntree_no_b") : "") + $" · {Models(x)}";
            row.Value.Text = G(x.Gbps);
            row.SetLoad(x.Ports <= 0 ? (x.Racks > 0 ? 1.5 : 0) : (double)x.UsedPorts / x.Ports, x.Online < x.Racks || (x.Racks > 0 && x.Spines < 2) || x.Unlinked > 0);
        }, 56, 86);
    }

    static bool ValidSel(NetReport n, string key)
    {
        var p = key.Split(':');
        if (p[0] == "in") return true;
        if (p.Length < 2 || !int.TryParse(p[1], out var id)) return false;
        return p[0] switch
        {
            "core" => id == 0 ? n.CoreList.Count == 0 : n.CoreList.Any(c => c.Obj.Id == id),
            "dist" => n.Dists.Any(d => d.Obj.Id == id),
            "hall" => n.Halls.Any(h => h.Room.Id == id),
            _ => false,
        };
    }

    /// <summary>Start on the hall that needs attention most, else the incoming fiber.</summary>
    static string DefaultSel(NetReport n)
    {
        var bad = n.Halls.Where(h => h.Racks > 0).OrderBy(h => h.Online - h.Racks).ThenBy(h => h.AB ? 1 : 0).FirstOrDefault(h => h.Online < h.Racks || !h.AB);
        return bad != null ? $"hall:{bad.Room.Id}" : "in";
    }

    // ---------------- details of the selected row ----------------
    void BuildDetails(GameState s, NetReport n, VBoxContainer right)
    {
        // summary tiles
        var tiles = new HBoxContainer(); tiles.AddThemeConstantOverride("separation", 10);
        right.AddChild(tiles);
        foreach (var t in new[] { 0, 1 })
        {
            var card = TreeRows.Card(tiles, t == 0 ? "ntree_tile_uplink" : "ntree_tile_racks");
            card.GetParent<Control>().SizeFlagsHorizontal = SizeFlags.ExpandFill;
            var big = Plain("", 24, Brand.Ink, false); big.AddThemeFontOverride("font", Ui.DisplayBold);
            var small = Plain("", 13, Brand.InkDim);
            card.AddChild(big); card.AddChild(small);
            var tt = t;
            _updaters.Add((st, r) =>
            {
                if (tt == 0)
                {
                    var up = r.Link(NetLink.Upstream);
                    big.Text = G(up.Capacity);
                    small.Text = Loc.T("ntree_tile_uplink_sub", G(up.Load), N(up.Ratio * 100, "0"));
                }
                else
                {
                    big.Text = $"{r.OnlineRacks} / {r.OnlineRacks + r.OfflineRacks}";
                    small.Text = Loc.T("ntree_tile_racks_sub", r.Halls.Count(h => h.AB), r.Halls.Count(h => h.Racks > 0));
                    big.AddThemeColorOverride("font_color", r.OfflineRacks > 0 ? Brand.Critical : Brand.Ink);
                }
            });
        }

        var sel = TreeRows.Card(right, "ntree_selected");
        var p = _sel.Split(':');
        var id = p.Length > 1 && int.TryParse(p[1], out var v) ? v : 0;
        switch (p[0])
        {
            case "hall": HallDetails(s, n, sel, id); break;
            case "dist": DistDetails(n, sel, id); break;
            case "core": CoreDetails(n, sel, id); break;
            default: IncomingDetails(s, n, sel); break;
        }

        // the switch types you can build
        var types = TreeRows.Card(right, "ntree_types");
        foreach (var k in new[] { ObjKind.EdgeSwitch24, ObjKind.EdgeSwitch48, ObjKind.Spine, ObjKind.DistSwitch })
        {
            var d = Catalog.Objects[k];
            var line = new HBoxContainer();
            line.AddChild(new Label { Text = k == ObjKind.DistSwitch ? Loc.T("obj_dist_switch") : Model(k), SizeFlagsHorizontal = SizeFlags.ExpandFill, AutoTranslateMode = AutoTranslateModeEnum.Disabled });
            var spec = k == ObjKind.DistSwitch ? Loc.T("ntree_type_dist", d.Ports, UplinkText(k), Ui.Money(d.Cost))
                                               : Loc.T("ntree_type_edge", d.Ports, UplinkText(k), Ui.Money(d.Cost));
            var l = Plain(spec, 14, Brand.Ink, false); l.AddThemeFontOverride("font", Ui.DisplaySemi);
            line.AddChild(l);
            types.AddChild(line);
        }
        types.AddChild(Plain(Loc.T("ntree_types_help"), 12, Brand.InkDim));
    }

    static Control Facts(params (string key, string value)[] facts)
    {
        var g = new GridContainer { Columns = 3 };
        g.AddThemeConstantOverride("h_separation", 18); g.AddThemeConstantOverride("v_separation", 2);
        foreach (var (k, _) in facts) g.AddChild(Ui.Heading(k, 11, Brand.InkDim));
        foreach (var (_, val) in facts) { var l = Plain(val, 19, Brand.Ink, false); l.AddThemeFontOverride("font", Ui.DisplaySemi); g.AddChild(l); }
        return g;
    }

    Control Tip(string text, bool warn)
    {
        var box = new PanelContainer();
        var col = warn ? Brand.Warning : Brand.Ok;
        box.AddThemeStyleboxOverride("panel", Ui.Box(new Color(col, 0.1f), 8, new Color(col, 0.35f), 1, 10));
        var l = Plain((warn ? Loc.T("ntree_tip") + " " : "") + text, 13, Brand.Ink);
        box.AddChild(l);
        return box;
    }

    Button PlaceButton(ObjKind k, string labelKey, string arg)
    {
        var b = Ui.Primary(Loc.T(labelKey, arg), () => _pick?.Invoke(new BuildItem(Mode.Object, (int)k)));
        b.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        b.AddThemeFontSizeOverride("font_size", 14);
        b.Disabled = _pick == null;
        return b;
    }

    void HallDetails(GameState s, NetReport n, VBoxContainer card, int roomId)
    {
        var h = n.Halls.First(x => x.Room.Id == roomId);
        var label = HallLabel(n, h.Room);
        if (h.Switches.Count == 0)
        {
            card.AddChild(Title($"{label} · {Loc.T("ntree_no_edge")}"));
            card.AddChild(Tip(Loc.T("ntree_tip_no_edge", h.Racks), true));
        }
        else
        {
            var big = h.Switches.OrderByDescending(o => o.Def.Ports).First().Kind;
            card.AddChild(Title($"{EdgeGroupName(n, h)} · {Models(h)}"));
            card.AddChild(Facts(("ntree_f_ports", $"{h.Ports} × {PortSpeed(big)}"), ("ntree_f_uplink", string.Join(" + ", h.Switches.Select(o => UplinkText(o.Kind)).Distinct())), ("ntree_f_only", label)));
            card.AddChild(Ui.Heading("ntree_rack_ports", 11, Brand.InkDim));
            card.AddChild(TreeRows.Ports(h.Ports, h.UsedPorts, h.Dual, Brand.Fiber));
            card.AddChild(Plain(Loc.T("ntree_ports_legend", h.Online, h.Dual, Math.Max(0, h.Ports - h.UsedPorts)), 12, Brand.InkDim));
            foreach (var e in h.Switches)
            {
                n.Uplink.TryGetValue(e.Id, out var up);
                var via = up > 0 ? DistName(n, n.Dists.First(d => d.Obj.Id == up).Obj) : up == 0 ? Loc.T("ntree_via_core") : Loc.T("ntree_via_none");
                card.AddChild(Plain($"• {Model(e.Kind)} · {Network.EdgePortsUsed(n, e)} / {e.Def.Ports} · {Loc.T("ntree_uplink_to", via)}", 13, up < 0 ? Brand.Warning : Brand.Ink));
            }
            var tip = HallTip(h);
            if (tip != null) card.AddChild(Tip(tip, true));
            else card.AddChild(Tip(Loc.T("ntree_tip_ab_ok"), false));
        }
        var row = new HFlowContainer(); row.AddThemeConstantOverride("h_separation", 8); row.AddThemeConstantOverride("v_separation", 8);
        row.AddChild(PlaceButton(ObjKind.EdgeSwitch48, "ntree_place", Model(ObjKind.EdgeSwitch48)));
        row.AddChild(PlaceButton(ObjKind.EdgeSwitch24, "ntree_place", Model(ObjKind.EdgeSwitch24)));
        card.AddChild(row);
        card.AddChild(Plain(Loc.T("ntree_place_help", label), 12, Brand.InkDim));
    }

    static string HallTip(HallNet h)
    {
        if (h.Unlinked > 0) return Loc.T("ntree_tip_uplink");
        if (h.Online < h.Racks) return Loc.T("ntree_tip_full", h.Racks - h.Online);
        if (h.Racks > 0 && h.Spines < 2) return Loc.T("ntree_tip_no_b");
        if (h.Racks > 0 && !h.AB) return Loc.T("ntree_tip_partial_ab", h.Racks - h.Dual);
        return null;
    }

    void DistDetails(NetReport n, VBoxContainer card, int id)
    {
        var d = n.Dists.First(x => x.Obj.Id == id);
        card.AddChild(Title($"{DistName(n, d.Obj)} · {Loc.T("obj_dist_switch")}"));
        card.AddChild(Facts(("ntree_f_ports", $"{d.Used} / {d.Ports}"), ("ntree_f_uplink", UplinkText(ObjKind.DistSwitch)), ("ntree_f_load", G(d.Load))));
        card.AddChild(TreeRows.Ports(d.Ports, d.Used, 0, Brand.Fiber));
        foreach (var g in d.Edges.GroupBy(e => n.Halls.FirstOrDefault(h => h.Switches.Contains(e))).Where(g => g.Key != null))
            card.AddChild(Plain($"• {HallLabel(n, g.Key.Room)} · {string.Join(", ", g.Select(e => Model(e.Kind)))}", 13, Brand.Ink));
        if (!d.Linked) card.AddChild(Tip(Loc.T("ntree_tip_dist_core"), true));
        else if (n.Dists.Count(x => x.Linked) == 1 && d.Edges.Count > 0) card.AddChild(Tip(Loc.T("ntree_tip_dist_single"), true));
        else card.AddChild(Tip(Loc.T("ntree_tip_dist_ok"), false));
        var row = new HFlowContainer();
        row.AddChild(PlaceButton(ObjKind.DistSwitch, "ntree_place_mmr", Loc.T("obj_dist_switch")));
        card.AddChild(row);
    }

    void CoreDetails(NetReport n, VBoxContainer card, int id)
    {
        var c = n.CoreList.FirstOrDefault(x => x.Obj.Id == id);
        if (c == null)
        {
            card.AddChild(Title(Loc.T("ntree_no_core")));
            card.AddChild(Tip(Loc.T("ntree_no_core_sub"), true));
        }
        else
        {
            card.AddChild(Title($"{CoreName(n, c.Obj)} · {Loc.T("obj_core_router")}"));
            card.AddChild(Facts(("ntree_f_ports", $"{c.Used} / {c.Ports}"), ("ntree_f_capacity", G(c.Capacity)), ("ntree_f_load", G(c.Load))));
            card.AddChild(TreeRows.Ports(c.Ports, c.Used, 0, Brand.Fiber));
            card.AddChild(Plain(Loc.T("ntree_core_ports_help", n.External, n.Dists.Count(d => d.Linked), n.DirectEdges.Count), 12, Brand.InkDim));
            card.AddChild(n.Cores <= 1 ? Tip(Loc.T("ntree_tip_core_single"), true) : Tip(Loc.T("ntree_tip_core_ok"), false));
        }
        var row = new HFlowContainer();
        row.AddChild(PlaceButton(ObjKind.CoreRouter, "ntree_place_mmr", Loc.T("obj_core_router")));
        card.AddChild(row);
    }

    void IncomingDetails(GameState s, NetReport n, VBoxContainer card)
    {
        card.AddChild(Title(Loc.T("ntree_incoming", n.MmrRooms)));
        card.AddChild(Facts(("ntree_f_pairs", N(n.Pairs)), ("ntree_f_odf", $"{n.OdfUsed} / {n.OdfPorts}"), ("ntree_f_routes", N(n.Routes))));
        foreach (var c in n.Carriers)
            card.AddChild(Plain($"• {c.Def.Name} · {c.Contract.PortGbps}G · {G(c.Gbps)}" + (c.Up ? "" : " · " + Loc.T(c.Contract.Active ? "ntree_no_route" : "ntree_delivering")), 13, c.Up ? Brand.Ink : Brand.Warning));
        foreach (var (rival, f) in n.PeerFlows) card.AddChild(Plain($"• {Catalog.Rivals[rival].Name} · {Loc.T("ntree_peering")} · {G(f)}", 13, Brand.Ink));
        card.AddChild(Plain(Loc.T("ntree_pairs_help"), 12, Brand.InkDim));
        var tips = Advice(s, n);
        if (tips.Count > 0) card.AddChild(Tip(Loc.T(tips[0]), true));
    }

    static Label Title(string text)
    {
        var l = Plain(text, 22, Brand.Ink);
        l.AddThemeFontOverride("font", Ui.DisplaySemi);
        return l;
    }

    /// <summary>Network lines for the object inspector: where a switch hangs in the tree and how full it is.</summary>
    public static List<string> InspectLines(GameState s, PlacedObject o)
    {
        var n = s.Net;
        var lines = new List<string>();
        if (Catalog.IsHallSwitch(o.Kind))
        {
            var h = n.Halls.FirstOrDefault(x => x.Switches.Contains(o));
            lines.Add(Loc.T("ntree_inspect_edge", h == null ? "-" : HallLabel(n, h.Room), o.Def.Ports, UplinkText(o.Kind)));
            if (h != null)
            {
                n.Uplink.TryGetValue(o.Id, out var up);
                var via = up > 0 ? DistName(n, n.Dists.First(d => d.Obj.Id == up).Obj) : up == 0 ? Loc.T("ntree_via_core") : Loc.T("ntree_via_none");
                lines.Add((up < 0 ? "⚠ " : "") + Loc.T("ntree_ports_of", Network.EdgePortsUsed(n, o), o.Def.Ports) + " · " + Loc.T("ntree_uplink_to", via) + (h.AB ? " · A/B" : ""));
            }
        }
        else if (o.Kind == ObjKind.DistSwitch)
        {
            var d = n.Dists.FirstOrDefault(x => x.Obj == o);
            if (d != null) lines.Add((d.Linked ? "" : "⚠ ") + Loc.T("ntree_dist_sub", d.Used, d.Ports, d.Edges.Count) + $" · {G(d.Load)}");
        }
        else if (o.Kind == ObjKind.CoreRouter)
        {
            var c = n.CoreList.FirstOrDefault(x => x.Obj == o);
            if (c != null) lines.Add(Loc.T("ntree_core_sub", c.Used, c.Ports, c.Ports - c.Used) + $" · {G(c.Load)} / {G(c.Capacity)}");
        }
        return lines;
    }
}
