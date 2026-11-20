using System;
using System.Collections.Generic;
using Godot;

namespace DatacenterArchitect.World;

/// <summary>
/// What is mounted in a rack: a deterministic mix of servers, storage, blade chassis and switches of
/// different heights (1U to 7U), picked from the rack's id so a rack always looks the same.
/// The front of every unit is drawn by one shader (bays, vents, ports and blinking LEDs), so a whole hall
/// of servers is a handful of instanced quads and the LEDs blink on the GPU without any per-frame work.
/// </summary>
public static class ServerFill
{
    public enum Model { Server1U, Server2U, Gpu3U, Storage4U, Blade7U, Switch1U, Blank }

    public static int Units(Model m, int blank = 1) => m switch
    {
        Model.Server1U or Model.Switch1U => 1,
        Model.Server2U => 2,
        Model.Gpu3U => 3,
        Model.Storage4U => 4,
        Model.Blade7U => 7,
        _ => blank,
    };

    /// <summary>One unit in a rack: its lowest U (0 = bottom), height in U, model and faceplate colour.</summary>
    public readonly record struct Unit(int U, int Height, Model Model, Color Color);

    public const int RackUnits = 42;
    /// <summary>Height of one U in the world, and where U0 starts above the rack's base.</summary>
    public const float UHeight = 0.0445f, Bottom = 0.11f, Width = 0.84f, FrontZ = 0.4835f;

    static readonly Color[] Vendors =
    {
        new("#2b3138"), // graphite
        new("#a7b0ba"), // silver
        new("#1b1f25"), // black
        new("#2a3b57"), // navy
        new("#d5dade"), // white
    };

    /// <summary>The units in a rack, bottom to top. Racks are themed (compute, storage, blades, GPU, network) and partly empty.</summary>
    public static List<Unit> For(int rackId)
    {
        var rng = new Random(rackId * 7919 + 17);
        var list = new List<Unit>();
        var vendor = Vendors[rng.Next(Vendors.Length)];
        var second = Vendors[rng.Next(Vendors.Length)];
        Color Pick() => rng.Next(4) == 0 ? second : vendor;
        // top of rack: one or two switches and a blank cable manager under them
        var top = RackUnits;
        var switches = rng.Next(3) == 0 ? 2 : 1;
        for (var i = 0; i < switches; i++) { top--; list.Add(new Unit(top, 1, Model.Switch1U, i == 0 ? Vendors[2] : Vendors[0])); }
        top--; list.Add(new Unit(top, 1, Model.Blank, Vendors[2]));
        // theme of the rest of the rack
        var theme = rng.Next(10);
        (Model m, int w)[] mix = theme switch
        {
            < 4 => new[] { (Model.Server1U, 5), (Model.Server2U, 4), (Model.Storage4U, 1) },             // compute
            < 6 => new[] { (Model.Storage4U, 4), (Model.Server2U, 2), (Model.Server1U, 1) },             // storage
            < 8 => new[] { (Model.Blade7U, 3), (Model.Server2U, 1), (Model.Server1U, 1) },               // blades
            _ => new[] { (Model.Gpu3U, 4), (Model.Server2U, 1), (Model.Server1U, 1) },                   // GPU
        };
        var total = 0; foreach (var (_, w) in mix) total += w;
        // fill from the bottom up (heavy kit low), leaving the occasional gap and a free block near the top
        var fill = 0.55 + rng.NextDouble() * 0.4;
        var limit = (int)(top * fill);
        var u = 0;
        while (u < limit)
        {
            if (rng.Next(9) == 0) { u += 1 + rng.Next(2); continue; }
            var r = rng.Next(total); var m = mix[0].m;
            foreach (var (mm, w) in mix) { if (r < w) { m = mm; break; } r -= w; }
            var h = Units(m);
            if (u + h > top) break;
            // servers of the same kind usually come in a run of identical boxes
            var run = m is Model.Blade7U or Model.Storage4U ? 1 : 1 + rng.Next(4);
            var c = Pick();
            for (var i = 0; i < run && u + h <= limit; i++) { list.Add(new Unit(u, h, m, c)); u += h; }
            if (rng.Next(6) == 0 && u < limit) { list.Add(new Unit(u, 1, Model.Blank, Vendors[2])); u++; }
        }
        return list;
    }

    /// <summary>Faceplate transform in rack space (rack base at y = 0, front facing +z).</summary>
    public static Transform3D Plate(Unit unit) =>
        new(Basis.Identity.Scaled(new Vector3(Width, unit.Height * UHeight - 0.003f, 1)),
            new Vector3(0, Bottom + (unit.U + unit.Height / 2f) * UHeight, FrontZ));

    static ShaderMaterial _mat;

    /// <summary>The shared faceplate material. Blinking can be switched off (the Options menu) with <see cref="SetBlinking"/>.</summary>
    public static ShaderMaterial Material => _mat ??= new ShaderMaterial { Shader = new Shader { Code = ShaderCode } };

    public static void SetBlinking(bool on) => Material.SetShaderParameter("blink", on ? 1f : 0f);

    /// <summary>Per-instance custom data: model, height in U, a random seed and whether the rack is live.</summary>
    public static Color Custom(Unit unit, int rackId, bool live) =>
        new((float)unit.Model, unit.Height, (rackId * 0.6180339f + unit.U * 0.137f) % 1f, live ? 1 : 0);

    /// <summary>A small model of the rack's contents for single racks (build menu pictures, carried racks).</summary>
    public static MultiMeshInstance3D Instance(int rackId, bool live)
    {
        var units = For(rackId);
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, UseCustomData = true, Mesh = PlateMesh, InstanceCount = units.Count };
        for (var i = 0; i < units.Count; i++)
        {
            mm.SetInstanceTransform(i, Plate(units[i]));
            mm.SetInstanceColor(i, units[i].Color);
            mm.SetInstanceCustomData(i, Custom(units[i], rackId, live));
        }
        return new MultiMeshInstance3D { Multimesh = mm, MaterialOverride = Material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
    }

    static QuadMesh _plate;
    public static QuadMesh PlateMesh => _plate ??= new QuadMesh { Size = Vector2.One };

    // Positions on the faceplate are in centimetres: x across the 84 cm wide plate, y up from the bottom edge.
    const string ShaderCode = @"
shader_type spatial;
render_mode cull_back;

uniform float blink = 1.0;

varying flat vec4 v_custom;

void vertex() {
    v_custom = INSTANCE_CUSTOM;
}

float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }

float rect(vec2 p, vec2 lo, vec2 hi, vec2 aa) {
    vec2 a = smoothstep(lo - aa, lo + aa, p) * (1.0 - smoothstep(hi - aa, hi + aa, p));
    return a.x * a.y;
}

// state of one LED: 1 = on. rate in blinks per second; steady LEDs pass rate 0.
float led_on(vec2 id, float rate, float duty) {
    if (rate <= 0.0 || blink < 0.5) return 1.0;
    float h = hash(id + v_custom.z * 91.7);
    float r = rate * mix(0.6, 1.6, hash(id.yx + 3.1 + v_custom.z));
    // most activity LEDs flicker, some sit idle for a while
    float idle = step(0.8, fract(TIME * 0.11 + h * 5.0));
    return max(idle * step(0.5, h), step(duty, fract(TIME * r + h)));
}

// a round LED: adds its colour to emission when on, a dim lens when off
void led(vec2 p, vec2 c, float radius, vec3 color, float on, vec2 aa, inout vec3 albedo, inout vec3 emit) {
    float r = max(radius, aa.x * 1.2);
    float m = 1.0 - smoothstep(r - aa.x, r + aa.x, length(p - c));
    float energy = (radius * radius) / (r * r);
    albedo = mix(albedo, color * 0.25, m);
    emit += color * m * on * energy * 2.6;
}

void fragment() {
    int model = int(v_custom.x + 0.5);
    float units = v_custom.y;
    bool live = v_custom.w > 0.5;
    float H = units * 4.45;
    vec2 p = vec2(UV.x * 84.0, (1.0 - UV.y) * H);
    vec2 aa = max(fwidth(p) * 0.7, vec2(0.02));
    vec3 base = COLOR.rgb;
    vec3 col = base;
    vec3 emit = vec3(0.0);
    vec3 green = vec3(0.25, 1.0, 0.4);
    vec3 blue = vec3(0.3, 0.6, 1.0);
    vec3 amber = vec3(1.0, 0.62, 0.12);
    float lit = live ? 1.0 : 0.0;

    // rack ears and a darker seam around each unit
    float inner = rect(p, vec2(0.25), vec2(83.75, H - 0.25), aa);
    col *= mix(0.4, 1.0, inner);
    float ears = 1.0 - rect(p, vec2(2.6, -1.0), vec2(81.4, H + 1.0), aa);
    col = mix(col, base * 1.25 + 0.04, ears * inner);

    if (model == 0 || model == 1 || model == 3) {
        // drive bays: 1U has ten small bays, 2U twelve big ones in three rows, 4U storage 24 in four rows
        float cols = model == 0 ? 10.0 : (model == 1 ? 4.0 : 6.0);
        float rows = model == 0 ? 1.0 : (model == 1 ? 3.0 : 4.0);
        vec2 lo = vec2(4.0, 0.5), hi = vec2(model == 0 ? 52.0 : 62.0, H - 0.5);
        vec2 cell = (hi - lo) / vec2(cols, rows);
        vec2 q = (p - lo) / cell;
        vec2 id = floor(q);
        if (id.x >= 0.0 && id.y >= 0.0 && id.x < cols && id.y < rows) {
            vec2 f = (q - id) * cell;
            float bay = rect(f, vec2(0.25), cell - 0.25, aa);
            col = mix(col, vec3(0.08, 0.09, 0.1), bay);
            // drive caddy handle
            float handle = rect(f, vec2(0.6, cell.y * 0.35), vec2(cell.x - 2.2, cell.y * 0.62), aa);
            col = mix(col, base * 0.75 + 0.05, handle * bay);
            led(f, vec2(cell.x - 1.0, cell.y * 0.5), 0.32, green, lit * led_on(id, 4.0, 0.55), aa, col, emit);
        }
        // vents to the right of the bays
        float vents = rect(p, vec2(hi.x + 2.0, 0.8), vec2(76.0, H - 0.8), aa);
        float slots = smoothstep(0.35, 0.45, abs(fract(p.x * 0.55) - 0.5));
        col = mix(col, col * 0.45, vents * slots);
        led(p, vec2(79.0, H - 1.4), 0.38, green, lit, aa, col, emit);
        led(p, vec2(79.0, max(H - 3.0, 1.2)), 0.32, hash(vec2(v_custom.z, 4.0)) < 0.06 ? blue : amber, lit * step(0.93, hash(vec2(v_custom.z, 9.0))) * led_on(vec2(9.0), 0.7, 0.5), aa, col, emit);
    } else if (model == 2) {
        // GPU server: honeycomb grille over the whole front
        vec2 g = p * vec2(0.9, 1.04);
        vec2 r2 = vec2(1.0, 1.732);
        vec2 a = mod(g, r2) - r2 * 0.5;
        vec2 b = mod(g - r2 * 0.5, r2) - r2 * 0.5;
        float d = min(length(a), length(b));
        float grille = rect(p, vec2(4.0, 1.0), vec2(72.0, H - 1.0), aa);
        col = mix(col, col * 0.3, grille * (1.0 - smoothstep(0.32, 0.42, d)));
        led(p, vec2(77.0, H - 2.0), 0.4, green, lit, aa, col, emit);
        led(p, vec2(77.0, H - 4.0), 0.4, green, lit * led_on(vec2(2.0, 5.0), 2.0, 0.4), aa, col, emit);
    } else if (model == 4) {
        // blade chassis: 14 vertical blades with two LEDs each, power supplies along the bottom
        float cols = 14.0;
        vec2 lo = vec2(4.0, 5.0), hi = vec2(80.0, H - 1.0);
        vec2 cell = (hi - lo) / vec2(cols, 1.0);
        vec2 q = (p - lo) / cell;
        vec2 id = floor(q);
        if (id.x >= 0.0 && id.x < cols && id.y == 0.0) {
            vec2 f = (q - id) * cell;
            float blade = rect(f, vec2(0.2, 0.2), cell - 0.2, aa);
            col = mix(col, base * 0.6, blade);
            float grip = rect(f, vec2(cell.x * 0.3, 1.0), vec2(cell.x * 0.7, cell.y * 0.55), aa);
            col = mix(col, vec3(0.07), grip * blade);
            led(f, vec2(cell.x * 0.5, cell.y - 1.2), 0.38, green, lit, aa, col, emit);
            led(f, vec2(cell.x * 0.5, cell.y - 2.4), 0.32, green, lit * led_on(id, 3.0, 0.5), aa, col, emit);
        }
        float psu = rect(p, vec2(4.0, 0.6), vec2(80.0, 4.4), aa);
        float psus = smoothstep(0.42, 0.48, abs(fract((p.x - 4.0) / 12.66) - 0.5));
        col = mix(col, vec3(0.12), psu * (1.0 - psus));
        for (float i = 0.0; i < 6.0; i += 1.0) led(p, vec2(14.0 + i * 12.66, 2.5), 0.3, green, lit, aa, col, emit);
    } else if (model == 5) {
        // switch: 48 ports in two rows with a link LED each, uplinks on the right
        vec2 lo = vec2(6.0, 0.35), hi = vec2(60.0, H - 0.35);
        vec2 cell = (hi - lo) / vec2(24.0, 2.0);
        vec2 q = (p - lo) / cell;
        vec2 id = floor(q);
        if (id.x >= 0.0 && id.y >= 0.0 && id.x < 24.0 && id.y < 2.0) {
            vec2 f = (q - id) * cell;
            float port = rect(f, vec2(0.3, 0.25), vec2(cell.x - 0.3, cell.y - 0.55), aa);
            col = mix(col, vec3(0.02), port);
            float used = step(0.25, hash(id + floor(v_custom.z * 50.0)));
            led(f, vec2(cell.x * 0.5, cell.y - 0.28), 0.2, green, lit * used * led_on(id, 9.0, 0.35), aa, col, emit);
        }
        float sfp = rect(p, vec2(63.0, 0.6), vec2(74.0, H - 0.6), aa);
        float cages = smoothstep(0.4, 0.46, abs(fract((p.x - 63.0) / 2.75) - 0.5));
        col = mix(col, vec3(0.05), sfp * (1.0 - cages));
        led(p, vec2(78.0, H * 0.5), 0.35, green, lit, aa, col, emit);
    } else {
        // blank panel: two pressed ribs
        float rib = smoothstep(0.42, 0.5, abs(fract(p.y / max(H, 0.01) * 2.0) - 0.5));
        col *= 1.0 - 0.15 * rib * rect(p, vec2(3.0, -1.0), vec2(81.0, H + 1.0), aa);
    }

    ALBEDO = col;
    ROUGHNESS = 0.55;
    METALLIC = 0.35;
    EMISSION = emit;
}
";
}
