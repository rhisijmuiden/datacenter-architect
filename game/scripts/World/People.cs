using System;
using System.Collections.Generic;
using DatacenterArchitect.Core;
using DatacenterArchitect.Sim;
using Godot;
using static DatacenterArchitect.World.Models;

namespace DatacenterArchitect.World;

/// <summary>What a figure is doing; picks the animation and how the arms are held.</summary>
public enum PersonPose { Idle, Walk, Work, Carry, Push, Reel }

/// <summary>
/// A person made from a rigged, animated CC0 model (Quaternius "Ultimate Modular Men/Women", see
/// assets/models/CREDITS.md). Heads can come from another outfit of the same build, and clothes, skin and hair
/// are recoloured per person, so a crowd never looks cloned. The animation player is advanced by hand so the arms
/// can be posed on top of the walk cycle (carrying a crate, pushing a trolley, holding a cable reel).
/// </summary>
public partial class PersonFigure : Node3D
{
    public AnimationPlayer Anim;
    public Skeleton3D Skel;
    /// <summary>The toolbox in the right hand (fixed to the wrist bone), or null.</summary>
    public Node3D Tool;
    int _uaL = -1, _uaR = -1, _laL = -1, _laR = -1;
    string _current = "";
    float _phase;

    internal void Bind(Node3D model)
    {
        Skel = model.FindChild("Skeleton3D", true, false) as Skeleton3D;
        Anim = model.FindChild("AnimationPlayer", true, false) as AnimationPlayer;
        if (Anim != null) Anim.CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Manual;
        if (Skel == null) return;
        _uaL = Skel.FindBone("UpperArm.L"); _uaR = Skel.FindBone("UpperArm.R");
        _laL = Skel.FindBone("LowerArm.L"); _laR = Skel.FindBone("LowerArm.R");
    }

    /// <summary>Advance the animation and hold the arms for the pose. <paramref name="seed"/> offsets the cycle per person.</summary>
    public void Animate(double dt, PersonPose pose, int seed)
    {
        if (Anim == null) return;
        var name = pose switch
        {
            PersonPose.Idle => "Idle_Neutral",
            PersonPose.Work => "Interact",
            _ => "Walk",
        };
        if (name != _current)
        {
            var first = _current == "";
            Anim.Play(name, first ? 0 : 0.25);
            if (first) Anim.Seek((seed * 0.37) % Math.Max(0.1, Anim.CurrentAnimationLength), true);
            _current = name;
        }
        Anim.Advance(dt);
        if (Skel == null || _uaL < 0) return;
        _phase += (float)dt;
        switch (pose)
        {
            case PersonPose.Carry:
                // a crate held in front at chest height
                Aim(_uaL, new Vector3(0.12f, -0.45f, 0.9f)); Aim(_uaR, new Vector3(-0.12f, -0.45f, 0.9f));
                Aim(_laL, new Vector3(-0.35f, 0.25f, 0.9f)); Aim(_laR, new Vector3(0.35f, 0.25f, 0.9f));
                break;
            case PersonPose.Push:
                // both hands on the trolley handle
                Aim(_uaL, new Vector3(0.1f, -0.75f, 0.65f)); Aim(_uaR, new Vector3(-0.1f, -0.75f, 0.65f));
                Aim(_laL, new Vector3(-0.1f, -0.35f, 0.95f)); Aim(_laR, new Vector3(0.1f, -0.35f, 0.95f));
                break;
            case PersonPose.Reel:
                // a cable reel held low in both hands
                Aim(_uaL, new Vector3(0.12f, -0.9f, 0.35f)); Aim(_uaR, new Vector3(-0.12f, -0.9f, 0.35f));
                Aim(_laL, new Vector3(-0.08f, -0.45f, 0.9f)); Aim(_laR, new Vector3(0.08f, -0.45f, 0.9f));
                break;
        }
    }

    /// <summary>Turn a bone so it points (its +Y axis) along <paramref name="dir"/> in model space; dir.x is mirrored for the right side.</summary>
    void Aim(int bone, Vector3 dir)
    {
        var g = Skel.GetBoneGlobalPose(bone);
        var from = g.Basis.Y.Normalized();
        var to = dir.Normalized();
        var axis = from.Cross(to);
        if (axis.LengthSquared() < 1e-6f) return;
        var q = new Quaternion(axis.Normalized(), from.AngleTo(to));
        g.Basis = new Basis(q) * g.Basis;
        Skel.SetBoneGlobalPose(bone, g);
    }
}

public static class People
{
    /// <summary>One outfit model: file, build, and which of its materials are the shirt, trousers and hair.</summary>
    sealed record Outfit(string File, bool Female, string[] Shirt, string[] Trousers, string[] Hair, bool HatHead = false);

    static readonly Dictionary<string, Outfit> Outfits = new()
    {
        ["Male_Worker"] = new("Male_Worker", false, new[] { "LightBrown" }, new[] { "Brown", "Brown2" }, new[] { "Eyebrows", "Moustache" }, true),
        ["Male_Suit"] = new("Male_Suit", false, new[] { "Suit" }, new[] { "Black" }, new[] { "Hair", "Eyebrows" }),
        ["Male_Casual_2"] = new("Male_Casual_2", false, new[] { "LightBrown" }, new[] { "LightBlue" }, new[] { "Hair", "Eyebrows" }),
        ["Male_Casual_Hoodie"] = new("Male_Casual_Hoodie", false, new[] { "Purple" }, new[] { "LightBlue" }, new[] { "Hair", "Eyebrows" }),
        ["Male_Farmer"] = new("Male_Farmer", false, new[] { "LightBlue" }, new[] { "Brown" }, new[] { "Eyebrows" }, true),
        ["Female_Worker"] = new("Female_Worker", true, new[] { "White" }, new[] { "Brown", "Brown_02" }, new[] { "DarkBrown" }, true),
        ["Female_Suit"] = new("Female_Suit", true, new[] { "Black" }, new string[0], new[] { "Hair_Brown", "Hair_Blond" }),
        ["Female_Casual"] = new("Female_Casual", true, new[] { "White" }, new[] { "Orange" }, new[] { "Hair_Brown", "Hair_Blond" }),
        ["Female_Formal"] = new("Female_Formal", true, new[] { "LimeGreen" }, new string[0], new[] { "Brown" }),
    };

    /// <summary>Heads without a hat, swapped between outfits of the same build for more faces and hair styles.</summary>
    static readonly string[] MaleHeads = { "Male_Suit", "Male_Casual_2", "Male_Casual_Hoodie" };
    static readonly string[] FemaleHeads = { "Female_Casual", "Female_Suit", "Female_Formal" };

    // natural skin tones and hair colours
    static readonly Color[] Skin = { new("#f2cfb4"), new("#e3b48f"), new("#c98e66"), new("#a26a45"), new("#74482d"), new("#ecc2a0"), new("#8a5a3a") };
    static readonly Color[] HairColor = { new("#2a1f1a"), new("#5b3b24"), new("#8a5a32"), new("#c79a5c"), new("#e0c48a"), new("#9b3f22"), new("#b9b4ad"), new("#1b1b1f") };
    // everyday clothes: warm, saturated but not neon
    static readonly Color[] Shirts = {
        new("#d9493b"), new("#3f7fc2"), new("#f2c14e"), new("#4f9d5a"), new("#e9e4da"), new("#8a5bb5"), new("#e47a3a"),
        new("#2f9d9a"), new("#f08aa0"), new("#5a6b7d"), new("#b23a5a"), new("#7ab8e0"), new("#f4f1ea"), new("#2b3a55"),
    };
    static readonly Color[] Jeans = { new("#3d5a80"), new("#6f8fb5"), new("#2b2f36"), new("#a8916a"), new("#4b6a8f"), new("#56606b"), new("#7a5c45"), new("#d8cfbd") };
    static readonly Color[] Suits = { new("#2b3442"), new("#3c4656"), new("#5b6370"), new("#1f2630"), new("#6b5a4a"), new("#2f4a6b") };

    static readonly Dictionary<string, PackedScene> Scenes = new();
    static readonly Dictionary<(Material, Color), Material> Recolored = new();

    static PackedScene Scene(string file)
    {
        if (Scenes.TryGetValue(file, out var s)) return s;
        s = GD.Load<PackedScene>($"res://assets/models/people/{file}.glb");
        Scenes[file] = s;
        return s;
    }

    static readonly Dictionary<string, (Mesh mesh, Skin skin)> Heads = new();

    static (Mesh, Skin) Head(string file)
    {
        if (Heads.TryGetValue(file, out var h)) return h;
        var donor = Scene(file).Instantiate<Node3D>();
        foreach (var c in donor.FindChildren("*_Head", "MeshInstance3D", true, false))
            if (c is MeshInstance3D mi) { h = (mi.Mesh, mi.Skin); break; }
        donor.Free();
        Heads[file] = h;
        return h;
    }

    static Material Tinted(Material m, Color c)
    {
        if (Recolored.TryGetValue((m, c), out var r)) return r;
        var d = (BaseMaterial3D)m.Duplicate();
        d.AlbedoColor = c;
        d.Roughness = 0.75f;
        Recolored[(m, c)] = d;
        return d;
    }

    /// <summary>Step 11: your own staff wear the company colour (shirts under the vests, ties); null = their usual clothes.</summary>
    public static Color? CompanyColor;

    /// <summary>Pick the outfit and colours for a role. Deterministic per id, so a person keeps their look.</summary>
    static (string file, Dictionary<string, Color> colors, bool swapHead) Dress(Role role, int id, StaffRole staff)
    {
        var v = Math.Abs(id);
        var female = (v * 7 + 3) % 5 < 2;  // about 40 % women
        var c = new Dictionary<string, Color>();
        string file;
        var swap = false;
        void Worker(Color vest, Color helmet, Color shirt)
        {
            file = female ? "Female_Worker" : "Male_Worker";
            c["Worker_Vest"] = vest; c["Worker_Yellow"] = helmet;
            foreach (var s in Outfits[file].Shirt) c[s] = shirt;
        }
        void Office(Color suit)
        {
            file = female ? (v % 3 == 0 ? "Female_Formal" : "Female_Suit") : "Male_Suit";
            foreach (var s in Outfits[file].Shirt) c[s] = file == "Female_Formal" ? Shirts[v * 5 % Shirts.Length] : suit;
            if (!female) c["Tie"] = Shirts[v * 3 % Shirts.Length];
            swap = true;
        }
        void Casual(Color? shirt = null)
        {
            file = female ? (v % 2 == 0 ? "Female_Casual" : "Female_Formal") : (v % 2 == 0 ? "Male_Casual_2" : "Male_Casual_Hoodie");
            foreach (var s in Outfits[file].Shirt) c[s] = shirt ?? Shirts[v * 5 % Shirts.Length];
            foreach (var s in Outfits[file].Trousers) c[s] = Jeans[v * 3 % Jeans.Length];
            swap = true;
        }
        file = "Male_Casual_2";
        switch (role)
        {
            case Role.Builder: Worker(new Color("#ff7a1a"), new Color("#ffd23e"), new Color("#c9b48a")); break;
            case Role.Fitter: Worker(new Color("#2f5d9a"), new Color("#24466f"), new Color("#d9dde2")); break;
            case Role.Engineer: Worker(Brand.Power, new Color("#f4f4f0"), new Color("#e9e4da")); break;
            case Role.Visitor:
                if (v % 4 == 0) Office(Suits[v % Suits.Length]); else Casual();
                break;
            case Role.Staff when id < 0: Worker(new Color("#ff7a1a"), new Color("#ff8a2a"), new Color("#c9b48a")); break;
            case Role.Staff:
                switch (staff)
                {
                    case StaffRole.Electrical or StaffRole.GeneratorTech or StaffRole.RemoteHands: Worker(Brand.Power, new Color("#f4f4f0"), CompanyColor ?? new Color("#dfe3e8")); break;
                    case StaffRole.Cooling: Worker(Brand.Cooling, new Color("#f4f4f0"), CompanyColor ?? new Color("#dfe3e8")); break;
                    case StaffRole.NetworkEng: Worker(Brand.Fiber, new Color("#f4f4f0"), CompanyColor ?? new Color("#dfe3e8")); break;
                    case StaffRole.FireSafety: Worker(new Color("#d23c32"), new Color("#d23c32"), new Color("#2b2f36")); break;
                    case StaffRole.Logistics: Worker(new Color("#ff7a1a"), new Color("#3b4f66"), new Color("#3b4f66")); break;
                    case StaffRole.Guard: Office(new Color("#1a2a44")); if (!female) c["Tie"] = new Color("#1a2a44"); break;
                    case StaffRole.Gardener: file = "Male_Farmer"; c["LightBlue"] = new Color("#4f7d3a"); break;
                    case StaffRole.Cleaner: Casual(CompanyColor ?? new Color("#7ab8e0")); break;
                    case StaffRole.Canteen: Casual(new Color("#f4f1ea")); break;
                    default: Office(Suits[v % Suits.Length]); if (CompanyColor is { } cc && !female) c["Tie"] = cc; break;
                }
                break;
        }
        c["Skin"] = Skin[v % Skin.Length];
        c["Skin_Darker"] = Skin[v % Skin.Length].Darkened(0.12f);
        var hair = HairColor[(v * 7 + 3) % HairColor.Length];
        foreach (var h in new[] { "Hair", "Hair_Brown", "Hair_Blond", "Hair_Black", "Eyebrows", "Moustache" }) c[h] = hair;
        return (file, c, swap);
    }

    /// <summary>Build a person; node names Crate, Cart, Rack, Tool, ReelP and ReelF are props the world view shows.</summary>
    public static PersonFigure Create(Role role, int id, StaffRole staff)
    {
        var (file, colors, swap) = Dress(role, id, staff);
        var outfit = Outfits[file];
        var root = new PersonFigure { Name = "Person" };
        var model = Scene(file).Instantiate<Node3D>();
        model.Name = "Model";
        model.Scale = Vector3.One * 0.94f;
        root.AddChild(model);
        var v = Math.Abs(id);
        if (swap)
        {
            var pool = outfit.Female ? FemaleHeads : MaleHeads;
            var donor = pool[(v / 2 + v * 3) % pool.Length];
            if (donor != file)
                foreach (var n in model.FindChildren("*_Head", "MeshInstance3D", true, false))
                    if (n is MeshInstance3D mi) { var (mesh, skin) = Head(donor); if (mesh != null) { mi.Mesh = mesh; mi.Skin = skin; } }
        }
        foreach (var n in model.FindChildren("*", "MeshInstance3D", true, false))
        {
            if (n is not MeshInstance3D mi) continue;
            for (var i = 0; i < mi.Mesh.GetSurfaceCount(); i++)
            {
                var m = mi.Mesh.SurfaceGetMaterial(i);
                if (m != null && colors.TryGetValue(m.ResourceName, out var col)) mi.SetSurfaceOverrideMaterial(i, Tinted(m, col));
            }
        }
        root.Bind(model);
        Props(root, role, id, staff);
        return root;
    }

    static void Props(Node3D n, Role role, int id, StaffRole staff)
    {
        var crate = Box(n, new Vector3(0, 1.0f, 0.36f), new Vector3(0.42f, 0.3f, 0.32f), new Color("#c79a5b")); crate.Name = "Crate"; crate.Visible = false;
        // movers push a small trolley with a stack of servers (customer migrations)
        var cart = new Node3D { Name = "Cart", Position = new Vector3(0, 0, 0.8f), Visible = false }; n.AddChild(cart);
        Box(cart, new Vector3(0, 0.18f, 0), new Vector3(0.6f, 0.05f, 0.75f), new Color("#8f989f"), 0.5f, 0.5f);
        Box(cart, new Vector3(0, 0.95f, -0.38f), new Vector3(0.56f, 0.04f, 0.04f), new Color("#2b3036"));
        foreach (var x in new[] { -0.26f, 0.26f }) Box(cart, new Vector3(x, 0.56f, -0.38f), new Vector3(0.04f, 0.8f, 0.04f), new Color("#2b3036"));
        for (var i = 0; i < 4; i++)
        {
            Box(cart, new Vector3(0, 0.25f + i * 0.1f, 0.02f), new Vector3(0.5f, 0.085f, 0.62f), Brand.Rack, 0.5f, 0.3f);
            Box(cart, new Vector3(0.18f, 0.25f + i * 0.1f, 0.335f), new Vector3(0.05f, 0.02f, 0.01f), i % 2 == 0 ? Brand.Ok : Brand.Cooling, glow: true);
        }
        foreach (var (x, z) in new[] { (-0.25f, -0.3f), (0.25f, -0.3f), (-0.25f, 0.3f), (0.25f, 0.3f) }) Ball(n.GetNode<Node3D>("Cart"), new Vector3(x, 0.07f, z), 0.06f, new Color("#2b3036"));
        var rack = Box(n, new Vector3(0, 1.05f, 0.7f), new Vector3(0.6f, 2.0f, 1.0f), Brand.Rack, 0.5f, 0.4f); rack.Name = "Rack"; rack.Visible = false;
        if (role == Role.Engineer || role == Role.Staff && staff is StaffRole.Electrical or StaffRole.GeneratorTech or StaffRole.NetworkEng)
        {
            if (role == Role.Engineer || staff != StaffRole.NetworkEng) Models.CableReel(n, "ReelP", Brand.Power, 0.3f);
            if (role == Role.Engineer || staff == StaffRole.NetworkEng) Models.CableReel(n, "ReelF", Brand.Fiber, 0.22f);
        }
        // visitors wear a cyan visitor badge on the chest
        if (role == Role.Visitor && (n as PersonFigure)?.Skel is Skeleton3D sk)
        {
            var chest = new BoneAttachment3D { BoneName = "Chest" };
            sk.AddChild(chest);
            var badge = Box(chest, new Vector3(0.08f, 0.12f, 0.15f), new Vector3(0.07f, 0.09f, 0.015f), Brand.Cooling, glow: true);
            badge.Name = "Badge";
        }
        // a toolbox in the right hand, fixed to the wrist so it swings with the arm
        var tool = new Node3D { Name = "Tool" };
        if (n is PersonFigure pf) pf.Tool = tool;
        var skel = (n as PersonFigure)?.Skel;
        if (skel != null)
        {
            var hand = new BoneAttachment3D { BoneName = "Wrist.R" };
            skel.AddChild(hand);
            hand.AddChild(tool);
        }
        else n.AddChild(tool);
        Box(tool, new Vector3(0, 0.17f, 0), new Vector3(0.12f, 0.16f, 0.3f), role == Role.Engineer ? Brand.Fiber : new Color("#c23b2f"));
        Box(tool, new Vector3(0, 0.07f, 0), new Vector3(0.03f, 0.05f, 0.14f), new Color("#2b3036"));
        tool.Visible = role is Role.Builder or Role.Engineer || role == Role.Staff && staff is StaffRole.Electrical or StaffRole.Cooling or StaffRole.GeneratorTech or StaffRole.NetworkEng or StaffRole.RemoteHands;
    }
}
