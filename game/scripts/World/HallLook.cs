using Godot;

namespace DatacenterArchitect.World;

/// <summary>
/// Materials that make a data hall read like the real thing (playtest batch 2): black rack doors with a fine
/// perforation the servers glow through, a light raised floor of 600 mm tiles, and perforated floor grates in
/// the cold aisle. All are shaders on shared materials, so thousands of racks stay one draw per chunk.
/// </summary>
public static class HallLook
{
    static ShaderMaterial _door, _floor, _grate;

    /// <summary>Perforated rack door: a solid frame, a mesh of round holes you can see the servers through, a lock strip.</summary>
    public static ShaderMaterial Door => _door ??= new ShaderMaterial { Shader = new Shader { Code = DoorCode } };

    /// <summary>Raised floor: light tiles of 0.6 m with dark seams and a little tone variation per tile (world space).</summary>
    public static ShaderMaterial Floor => _floor ??= new ShaderMaterial { Shader = new Shader { Code = FloorCode } };

    /// <summary>Perforated floor grate for the cold aisle: square holes in a grey plate.</summary>
    public static ShaderMaterial Grate => _grate ??= new ShaderMaterial { Shader = new Shader { Code = GrateCode } };

    const string DoorCode = @"
shader_type spatial;
render_mode blend_mix, cull_back, depth_draw_opaque;
void fragment() {
    vec2 uv = UV;
    // frame: 4.5% at the sides, 2.5% top and bottom
    float frame = step(uv.x, 0.045) + step(0.955, uv.x) + step(uv.y, 0.025) + step(0.975, uv.y);
    // a solid strip on the right with the lock
    float strip = step(0.86, uv.x) * step(uv.x, 0.955) * step(0.42, uv.y) * step(uv.y, 0.58);
    // round holes on a staggered grid (about 5 mm pitch at this size)
    vec2 g = vec2(uv.x * 92.0, uv.y * 200.0);
    g.x += mod(floor(g.y), 2.0) * 0.5;
    float d = length(fract(g) - 0.5);
    float hole = 1.0 - smoothstep(0.36, 0.43, d);
    float solid = clamp(frame + strip, 0.0, 1.0);
    vec3 metal = vec3(0.075, 0.08, 0.09);
    ALBEDO = metal;
    ROUGHNESS = 0.55;
    METALLIC = 0.35;
    // far away the holes average out to a see-through grey
    float far = smoothstep(14.0, 40.0, length(VERTEX));
    float open = mix(hole * 0.9, 0.5, far);
    ALPHA = mix(1.0 - open, 1.0, solid);
}
";

    const string FloorCode = @"
shader_type spatial;
render_mode cull_back;
varying vec3 wpos;
float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
void vertex() { wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz; }
void fragment() {
    vec2 t = wpos.xz / 0.6;
    vec2 f = fract(t);
    float seam = 1.0 - smoothstep(0.0, 0.025, min(min(f.x, 1.0 - f.x), min(f.y, 1.0 - f.y)));
    float tone = (hash(floor(t)) - 0.5) * 0.05;
    vec3 tile = vec3(0.86, 0.88, 0.9) + tone;
    ALBEDO = mix(tile, vec3(0.55, 0.58, 0.62), seam * 0.8);
    ROUGHNESS = 0.32;
    SPECULAR = 0.6;
}
";

    const string GrateCode = @"
shader_type spatial;
render_mode cull_back;
void fragment() {
    vec2 g = fract(UV * vec2(14.0, 14.0));
    float hole = step(0.18, g.x) * step(g.x, 0.82) * step(0.18, g.y) * step(g.y, 0.82);
    float edge = step(UV.x, 0.04) + step(0.96, UV.x) + step(UV.y, 0.04) + step(0.96, UV.y);
    hole *= 1.0 - clamp(edge, 0.0, 1.0);
    ALBEDO = mix(vec3(0.66, 0.7, 0.74), vec3(0.16, 0.19, 0.22), hole);
    ROUGHNESS = 0.45;
    METALLIC = 0.4;
}
";
}
