"""Builds the gatehouse, barrier, gates, intercom, solar panels and wind turbine as .glb files (own models, CC0).

    python3 tools/models/blender/site.py game/assets/models/made

Godot coordinates through g()/gs() as in vehicles.py. Moving parts are separate files placed on the game's own
pivot nodes: the barrier arm on "Arm" (hinge at the origin, arm along +x), the gate leaf on "Leaf", the rotor of
the wind turbine on "FanRotor" (blades in the x/z plane, the game tilts the node to face the wind).
"""
import os, sys, math
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import *
from vehicles import g, gs
from common import obj_from_bmesh
from mathutils import Vector as V

OUT = sys.argv[-1] if len(sys.argv) > 1 and not sys.argv[-1].endswith('.py') else 'game/assets/models/made'
os.makedirs(OUT, exist_ok=True)
P = lambda n: os.path.join(OUT, n + '.glb')

kit.PALETTE.update({
    'navy': ((0.18, 0.29, 0.42, 1), 0.1, 0.45),
    'solar_cell': ((0.07, 0.12, 0.22, 1), 0.3, 0.12),
    'alu': ((0.75, 0.77, 0.79, 1), 0.8, 0.3),
    'turbine_white': ((0.93, 0.94, 0.95, 1), 0.0, 0.4),
})


def guard_booth():
    """2 x 2 m gatehouse: plinth, white panels, glass all round, a flat overhanging roof with an amber band."""
    reset(); o = []
    o.append(box(gs(1.98, 0.2, 1.98), g(0, 0.1, 0), 'concrete', 0.03))
    o.append(box(gs(1.8, 0.9, 1.8), g(0, 0.65, 0), 'paint_white', 0.04))                       # lower panels
    o.append(box(gs(1.82, 0.04, 1.82), g(0, 1.12, 0), 'paint_light', 0.01))                    # sill
    o.append(box(gs(1.74, 1.0, 1.74), g(0, 1.62, 0), 'glass', 0.02))                           # glazing
    for x, z in ((-1, -1), (1, -1), (-1, 1), (1, 1)):
        o.append(box(gs(0.08, 1.05, 0.08), g(x * 0.87, 1.62, z * 0.87), 'paint_white', 0.01))   # corner posts
    for x in (-0.3, 0.3):
        o.append(box(gs(0.03, 1.0, 0.03), g(x, 1.62, 0.885), 'paint_white', 0))                 # mullions
    o.append(box(gs(1.82, 0.2, 1.82), g(0, 2.22, 0), 'paint_white', 0.03))
    o.append(box(gs(2.3, 0.18, 2.3), g(0, 2.42, 0), 'navy', 0.04))                              # roof
    o.append(box(gs(2.32, 0.06, 2.32), g(0, 2.32, 0), 'accent_power', 0.01))
    o.append(box(gs(0.7, 0.3, 0.5), g(0.4, 2.65, -0.3), 'paint_light', 0.03))                  # AC unit on the roof
    o += fan_shroud(0.14, g(0.4, 2.82, -0.3), 0.05)
    o.append(box(gs(0.9, 0.05, 0.4), g(0, 1.13, 1.0), 'paint_light', 0.01))                    # counter at the window
    o.append(box(gs(0.5, 0.12, 0.04), g(0, 2.22, 0.92), 'glow_cooling', 0.01))                 # sign
    o.append(box(gs(0.12, 0.12, 0.08), g(-0.75, 2.2, 0.92), 'lamp', 0.02))
    o.append(box(gs(0.75, 1.95, 0.04), g(0.88, 1.08, -0.25), 'paint_light', 0.01, (0, 0, math.radians(90))))  # door on the side
    save(P('guard_booth'), o)


def intercom():
    reset(); o = []
    o.append(cyl(0.12, 0.08, g(0, 0.04, 0), 'concrete', 16))
    o.append(box(gs(0.1, 1.15, 0.1), g(0, 0.62, 0), 'dark_steel', 0.02))
    o.append(box(gs(0.3, 0.42, 0.18), g(0, 1.25, 0.02), 'paint_grey', 0.04))
    o.append(box(gs(0.2, 0.14, 0.02), g(0, 1.33, 0.12), 'screen', 0.01))
    o.append(box(gs(0.1, 0.04, 0.02), g(0, 1.16, 0.12), 'glow_ok', 0.01))
    o.append(cyl(0.03, 0.02, g(0, 1.08, 0.115), 'steel', 12, (math.radians(90), 0, 0), 0))
    o.append(box(gs(0.34, 0.04, 0.24), g(0, 1.48, 0.04), 'dark_steel', 0.01))               # rain hood
    save(P('intercom'), o)


def ped_gate():
    reset(); o = []
    for x in (-0.45, 0.45):
        o.append(box(gs(0.1, 2.2, 0.1), g(x, 1.1, 0), 'paint_dark', 0.02))
    o.append(box(gs(1.0, 0.08, 0.08), g(0, 2.16, 0), 'paint_dark', 0.02))
    o.append(box(gs(0.8, 0.04, 0.04), g(0, 0.15, 0.03), 'paint_dark', 0.01))                   # door frame
    o.append(box(gs(0.8, 0.04, 0.04), g(0, 2.0, 0.03), 'paint_dark', 0.01))
    for i in range(9):
        o.append(box(gs(0.025, 1.85, 0.025), g(-0.36 + i * 0.09, 1.07, 0.03), 'paint_dark', 0))
    o.append(box(gs(0.14, 0.22, 0.08), g(-0.45, 1.3, 0.09), 'paint_grey', 0.02))               # card reader
    o.append(box(gs(0.08, 0.04, 0.01), g(-0.45, 1.36, 0.135), 'glow_ok', 0))
    o.append(box(gs(0.03, 0.03, 0.14), g(0.32, 1.05, 0.1), 'steel', 0.008))                    # handle
    save(P('ped_gate'), o)


def barrier_post():
    reset(); o = []
    o.append(box(gs(0.5, 0.06, 0.5), g(-1.3, 0.03, 0), 'concrete', 0.01))
    o.append(box(gs(0.36, 0.95, 0.36), g(-1.3, 0.5, 0), 'paint_white', 0.05))
    o.append(box(gs(0.38, 0.1, 0.38), g(-1.3, 0.96, 0), 'red_paint', 0.03))
    o.append(box(gs(0.2, 0.06, 0.02), g(-1.3, 0.7, 0.19), 'glow_alert', 0.005))
    o.append(box(gs(0.08, 0.6, 0.08), g(1.45, 0.3, 0), 'paint_white', 0.02))                   # rest post at the far end
    o.append(box(gs(0.16, 0.08, 0.12), g(1.45, 0.62, 0), 'paint_dark', 0.01))
    save(P('barrier_post'), o)


def barrier_arm():
    """The boom, hinged at the origin and running 2.8 m along +x with red and white bands."""
    reset(); o = []
    o.append(cyl(0.08, 0.3, g(0, 0, 0), 'paint_dark', 16, (math.radians(90), 0, 0), 0.01))
    for i in range(7):
        o.append(box(gs(0.4, 0.09, 0.07), g(0.25 + i * 0.4, 0, 0), 'red_paint' if i % 2 == 0 else 'paint_white', 0.0))
    o.append(box(gs(0.1, 0.4, 0.08), g(-0.15, 0, 0), 'paint_grey', 0.02))                     # counterweight
    save(P('barrier_arm'), o)


def sliding_gate(cells):
    half = cells / 2; span = cells - 0.12; h = 2.6 if cells > 3 else 2.4
    reset(); o = []
    for x in (-half + 0.07, half - 0.07):
        o.append(box(gs(0.16, h, 0.16), g(x, h / 2, 0), 'paint_dark', 0.02))
        o.append(box(gs(0.2, 0.04, 0.2), g(x, h + 0.02, 0), 'paint_dark', 0.01))
    o.append(box(gs(0.35, 0.7, 0.3), g(half + 0.25, 0.35, 0.25), 'paint_grey', 0.04))        # motor housing
    o.append(box(gs(0.22, 0.14, 0.22), g(half - 0.07, h + 0.1, 0), 'glow_power', 0.03))      # warning light
    o.append(box(gs(span * 2, 0.06, 0.1), g(span / 2, 0.03, 0.12), 'galvanised', 0.01))       # ground rail
    save(P(f'gate_posts_{cells}'), o)
    reset(); o = []
    o.append(box(gs(span, 0.1, 0.08), g(0, h - 0.2, 0.12), 'paint_dark', 0.02))
    o.append(box(gs(span, 0.16, 0.1), g(0, 0.18, 0.12), 'paint_dark', 0.02))
    bars = int(span / 0.2)
    for i in range(bars + 1):
        o.append(box(gs(0.04, h - 0.4, 0.04), g(-span / 2 + i * span / bars, (h - 0.1) / 2 + 0.1, 0.12), 'paint_dark', 0.008))
    a = math.atan2(h - 0.4, span)
    o.append(box(gs(math.hypot(span, h) * 0.9, 0.06, 0.05), g(0, (h - 0.1) / 2 + 0.1, 0.12), 'paint_dark', 0.01, (0, a, 0)))
    o.append(box(gs(span, 0.06, 0.09), g(0, h - 0.1, 0.12), 'accent_power', 0.01))
    for x in (-span / 2 + 0.3, span / 2 - 0.3):
        o.append(cyl(0.08, 0.06, g(x, 0.08, 0.12), 'steel', 12, (0, math.radians(90), 0), 0))  # rollers
    save(P(f'gate_leaf_{cells}'), o)


def solar_panel():
    """2 x 2 m: two rows of tilted panels on an aluminium frame, facing +z."""
    reset(); o = []
    tilt = math.radians(22)
    for z in (-0.5, 0.5):
        for x in (-0.8, 0.8):
            o.append(box(gs(0.05, 0.3, 0.05), g(x, 0.15, z - 0.25), 'alu', 0.01))
            o.append(box(gs(0.05, 0.62, 0.05), g(x, 0.31, z + 0.22), 'alu', 0.01))
            o.append(box(gs(0.2, 0.04, 0.6), g(x, 0.02, z), 'concrete', 0.01))                 # ballast block
        c = g(0, 0.5, z)
        o.append(box(gs(1.92, 0.04, 0.88), c, 'alu', 0.01, (tilt, 0, 0)))                      # frame
        o.append(box(gs(1.86, 0.02, 0.82), c + V((0, 0, 0.02)), 'solar_cell', 0.0, (tilt, 0, 0)))
        for i in range(1, 6):                                                                  # cell grid lines
            x = -0.93 + i * 0.31
            o.append(box(gs(0.008, 0.005, 0.82), c + V((x, 0, 0.032)), 'alu', 0, (tilt, 0, 0)))
        for k in range(1, 3):
            dz = -0.41 + k * 0.273
            p = c + V((0, -dz * math.cos(tilt), dz * math.sin(tilt) + 0.032))
            o.append(box(gs(1.86, 0.005, 0.008), p, 'alu', 0, (tilt, 0, 0)))
    o.append(box(gs(0.2, 0.15, 0.1), g(0.9, 0.2, -0.85), 'paint_grey', 0.02))                 # junction box
    save(P('solar_panel'), o)


def wind_tower():
    reset(); o = []
    o.append(cyl(0.9, 0.2, g(0, 0.1, 0), 'concrete', 32, bevel=0.03))
    o.append(cyl(0.2, 8.2, g(0, 4.3, 0), 'turbine_white', 24, r2=0.12, bevel=0.01))
    o.append(box(gs(0.42, 0.42, 1.1), g(0, 8.45, -0.1), 'turbine_white', 0.15, segs=3))        # nacelle
    o.append(box(gs(0.44, 0.06, 0.6), g(0, 8.45, -0.2), 'accent_power', 0.01))
    o.append(cyl(0.05, 0.2, g(0, 8.75, -0.5), 'glow_alert', 8))                                  # aviation light
    o.append(box(gs(0.4, 0.8, 0.05), g(0, 0.6, 0.18), 'paint_light', 0.01))                    # door
    save(P('wind_tower'), o)


def wind_rotor():
    """Hub and three blades in the x/z plane (Blender x/y), spinning about the node's up axis."""
    reset(); o = []
    o.append(cyl(0.22, 0.3, V((0, 0, 0)), 'accent_power', 20, bevel=0.06))
    o.append(cyl(0.22, 0.25, V((0, 0, 0.27)), 'turbine_white', 20, r2=0.04, bevel=0.03))
    for k in range(3):
        a = 2 * math.pi * k / 3
        # a tapered, slightly twisted blade
        bm = bmesh.new()
        prof = []
        for i, t in enumerate((0.2, 0.6, 1.4, 2.4, 3.2)):
            w = (0.28, 0.3, 0.24, 0.16, 0.07)[i]; th = 0.06 * (1 - t / 4)
            tw = math.radians(20 - t * 5)
            ring = [V((t, math.cos(tw) * dx - math.sin(tw) * dz, math.sin(tw) * dx + math.cos(tw) * dz))
                    for dx, dz in ((-w / 2, 0), (0, th), (w / 2, 0), (0, -th))]
            prof.append([bm.verts.new(p) for p in ring])
        for r0, r1 in zip(prof, prof[1:]):
            for j in range(4):
                bm.faces.new((r0[j], r0[(j + 1) % 4], r1[(j + 1) % 4], r1[j]))
        bm.faces.new(list(reversed(prof[-1])))
        ob = obj_from_bmesh(bm, 'blade', mat('turbine_white'))
        ob.rotation_euler = (0, 0, a)
        shade_smooth(ob)
        o.append(ob)
    body = join(o, 'Rotor')
    export(P('wind_rotor'), [body])
    print('wind_rotor', tris(body), 'tris')


def roof_unit():
    """Packaged rooftop air unit, 2.5 x 1.6 m, on a curb, with two fans on top and a duct into the roof."""
    reset(); o = []
    o.append(box(gs(2.3, 0.12, 1.4), g(0, 0.06, 0), 'galvanised', 0.01))
    o.append(box(gs(2.5, 0.9, 1.6), g(0, 0.57, 0), 'paint_white', 0.05))
    o.append(box(gs(2.52, 0.05, 1.62), g(0, 1.02, 0), 'paint_light', 0.02))
    o += louvres(1.0, 0.55, g(0.55, 0.55, 0.805), '-Y')
    o += louvres(1.2, 0.55, g(0, 0.55, -0.805), '+Y')
    o += louvres(1.2, 0.55, g(1.255, 0.55, 0), '+X')
    o.append(box(gs(0.7, 0.6, 0.03), g(-0.7, 0.57, 0.81), 'paint_light', 0.01))           # service door
    o.append(box(gs(0.03, 0.15, 0.04), g(-0.42, 0.6, 0.83), 'steel', 0.005))
    o.append(box(gs(0.3, 0.08, 0.02), g(-0.7, 0.95, 0.82), 'accent_cooling', 0))
    for x in (-0.6, 0.6):
        o += fan_shroud(0.42, g(x, 1.12, 0), 0.14)
        o.append(fan(0.38, g(x, 1.1, 0), 'FanBlade'))
    o.append(box(gs(0.5, 0.25, 0.5), g(-0.9, 0.12, -0.9), 'galvanised', 0.02))              # duct down into the roof
    # details: refrigerant lines (insulated + copper), isolator box with conduit, lifting lugs, panel handles, nameplate
    for dz, m, r in ((0.25, 'rubber', 0.035), (0.4, 'copper', 0.018)):
        o.append(tube_path([g(-1.25, 0.35, dz), g(-1.45, 0.35, dz), g(-1.5, 0.25, dz), g(-1.5, 0.02, dz)], r, m, 8))
    o.append(box(gs(0.04, 0.3, 0.22), g(-1.27, 0.65, -0.35), 'paint_grey', 0.008))
    o.append(box(gs(0.03, 0.08, 0.04), g(-1.3, 0.68, -0.35), 'red_paint', 0.004))
    o.append(tube_path([g(-1.27, 0.5, -0.35), g(-1.32, 0.3, -0.35), g(-1.35, 0.03, -0.35)], 0.015, 'galvanised', 6))
    for x in (-1.2, 1.2):
        for z in (-0.75, 0.75):
            o.append(torus(0.04, 0.01, g(x, 1.08, z), 'dark_steel', (math.radians(90), 0, 0), 10, 5))
    for x in (0.15, 0.55, 0.95):
        o.append(box(gs(0.1, 0.025, 0.03), g(x, 0.88, 0.815), 'steel', 0.004))
    o.append(box(gs(0.2, 0.1, 0.012), g(-0.15, 0.85, 0.81), 'steel', 0.003))
    save(P('roof_unit'), o, keep_separate=())


def park_bench():
    """2 m park bench: cast-iron sides with armrests, curved wooden seat and back slats."""
    reset(); o = []
    for x in (-0.88, 0.88):
        o.append(tube_path([g(x, 0, 0.22), g(x, 0.43, 0.18), g(x, 0.45, -0.18), g(x, 0, -0.25)], 0.03, 'dark_steel', 8))   # leg frame
        o.append(tube_path([g(x, 0.43, -0.2), g(x, 0.62, -0.26), g(x, 0.88, -0.32)], 0.028, 'dark_steel', 8))           # back post
        o.append(tube_path([g(x, 0.45, 0.2), g(x, 0.66, 0.18), g(x, 0.66, -0.2)], 0.025, 'dark_steel', 8))             # armrest
        o.append(box(gs(0.07, 0.03, 0.3), g(x, 0.67, 0.0), 'wood', 0.01))
    for k in range(5):
        z = 0.17 - k * 0.085
        o.append(box(gs(1.9, 0.035, 0.07), g(0, 0.45 + (0.01 if k in (0, 4) else 0), z), 'wood', 0.012))
    for k in range(3):
        y = 0.56 + k * 0.11
        o.append(box(gs(1.9, 0.09, 0.03), g(0, y, -0.25 - k * 0.025), 'wood', 0.01, (math.radians(-14), 0, 0)))
    o.append(box(gs(0.5, 0.08, 0.01), g(0, 0.36, 0.2), 'accent_power', 0))                                         # small plaque
    save(P('park_bench'), o)


def picnic_table():
    """Wooden picnic table with two attached benches, 2 x 2 m."""
    reset(); o = []
    for k in range(5):
        o.append(box(gs(1.8, 0.04, 0.15), g(0, 0.74, -0.32 + k * 0.16), 'wood', 0.012))                                 # table top
    for x in (-0.7, 0.7):
        for z in (-1, 1):
            o.append(box(gs(0.07, 0.95, 0.07), g(x, 0.37, z * 0.36), 'wood', 0.01, (math.radians(z * 32), 0, 0)))      # A-frame legs
        o.append(box(gs(0.07, 0.07, 1.55), g(x, 0.4, 0), 'wood', 0.01))                                                 # cross bar under the seats
        o.append(box(gs(0.06, 0.06, 0.7), g(x, 0.68, 0), 'wood', 0.01))
    for z in (-0.68, 0.68):
        for k in range(2):
            o.append(box(gs(1.8, 0.04, 0.12), g(0, 0.44, z - 0.065 + k * 0.13), 'wood', 0.01))                         # bench seats
    save(P('picnic_table'), o)


def terrace_set():
    """Round bistro table with four chairs under a canvas parasol, 2 x 2 m."""
    reset(); o = []
    o.append(cyl(0.38, 0.03, g(0, 0.73, 0), 'paint_light', 28, bevel=0.01))
    o.append(cyl(0.025, 0.7, g(0, 0.37, 0), 'dark_steel', 10))
    o.append(cyl(0.22, 0.03, g(0, 0.015, 0), 'dark_steel', 20))
    for k in range(4):
        a = math.pi / 4 + k * math.pi / 2
        cx, cz = math.cos(a) * 0.68, math.sin(a) * 0.68
        rot = (0, 0, -a + math.pi / 2)
        seat = box(gs(0.42, 0.04, 0.42), g(cx, 0.45, cz), 'navy', 0.015)
        seat.rotation_euler = (0, 0, -a - math.pi / 2); o.append(seat)
        bx, bz = math.cos(a) * 0.88, math.sin(a) * 0.88
        back = box(gs(0.42, 0.36, 0.03), g(bx, 0.7, bz), 'navy', 0.01)
        back.rotation_euler = (0, 0, -a - math.pi / 2); o.append(back)
        for dx, dz in ((-0.17, -0.17), (0.17, -0.17), (-0.17, 0.17), (0.17, 0.17)):
            ca, sa = math.cos(a), math.sin(a)
            o.append(cyl(0.012, 0.45, g(cx + dx * ca - dz * sa, 0.225, cz + dx * sa + dz * ca), 'steel', 6, bevel=0))
    o.append(cyl(0.02, 2.3, g(0, 1.15, 0), 'paint_light', 8))                                                          # parasol pole
    o.append(cyl(1.25, 0.45, g(0, 2.2, 0), 'paint_white', 8, r2=0.05, bevel=0.0))                                      # canvas
    o.append(cyl(1.26, 0.12, g(0, 1.98, 0), 'accent_power', 8, r2=1.2, bevel=0.0))                                     # valance
    o.append(cyl(0.05, 0.12, g(0, 2.48, 0), 'paint_light', 8))
    save(P('terrace_set'), o)


if __name__ == '__main__':
    only = set(os.environ.get('ONLY', '').split(',')) - {''}
    fns = [guard_booth, intercom, ped_gate, barrier_post, barrier_arm, lambda: sliding_gate(3), lambda: sliding_gate(6),
           solar_panel, wind_tower, wind_rotor, roof_unit, park_bench, picnic_table, terrace_set]
    for f in fns:
        if not only or f.__name__ in only:
            f()
