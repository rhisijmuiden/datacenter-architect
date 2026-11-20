"""Builds the game's datacenter equipment and street furniture as .glb files (own models, CC0 like the game).

    python3 tools/models/blender/infra.py game/assets/models/made

Each model fills its footprint in the game (1 cell = 1 m), stands on the origin and faces -Y in Blender (+Z, the
front, in Godot). Fan rotors are separate nodes named Fan* so the game can spin them. Materials named glow_* and
lamp become the game's glowing brand colours.
"""
import os, sys, math, random
sys.path.insert(0, os.path.dirname(__file__))
from kit import *
from mathutils import Vector as V

OUT = sys.argv[-1] if len(sys.argv) > 1 and not sys.argv[-1].endswith('.py') else 'game/assets/models/made'
os.makedirs(OUT, exist_ok=True)
P = lambda n: os.path.join(OUT, n + '.glb')


def generator():
    reset(); o = []
    W, D, H = 2.9, 1.85, 2.35
    o.append(box((3.0, 1.95, 0.18), V((0, 0, 0.09)), 'dark_steel', 0.02))               # skid
    o.append(box((W, D, H), V((0, 0, 0.18 + H / 2)), 'paint_white', 0.06, segs=3))       # enclosure
    o.append(box((W + 0.04, D + 0.04, 0.08), V((0, 0, 0.18 + H - 0.04)), 'paint_light', 0.03))
    o.append(box((W + 0.02, D + 0.02, 0.16), V((0, 0, 0.18 + H * 0.78)), 'accent_power', 0.01))  # amber band
    for x in (-0.95, 0.0, 0.95):                                                           # doors on the front
        o.append(box((0.86, 0.02, 1.7), V((x, -D / 2 - 0.005, 0.18 + 1.0)), 'paint_light', 0.01))
        o.append(box((0.03, 0.04, 0.22), V((x + 0.34, -D / 2 - 0.02, 0.18 + 1.05)), 'steel', 0.008))
    o += louvres(0.8, 0.9, V((W / 2 + 0.005, -0.4, 1.25)), '+X')
    o += louvres(0.8, 0.9, V((W / 2 + 0.005, 0.4, 1.25)), '+X')
    o += louvres(1.4, 0.5, V((0.5, D / 2 + 0.005, 1.6)), '+Y')
    o.append(box((0.42, 0.04, 0.3), V((-0.95, -D / 2 - 0.02, 1.62)), 'screen', 0.01))   # control panel
    o.append(box((0.3, 0.02, 0.05), V((-0.95, -D / 2 - 0.04, 1.69)), 'glow_ok', 0.004))
    # exhaust: silencer on the roof and a stack with a rain cap
    o.append(cyl(0.22, 1.4, V((0.2, 0.25, 0.18 + H + 0.3)), 'steel', 20, (0, math.radians(90), 0)))
    for x in (-0.55, 0.95):
        o.append(box((0.12, 0.3, 0.3), V((x, 0.25, 0.18 + H + 0.12)), 'dark_steel', 0.01))
    o.append(cyl(0.1, 0.9, V((0.95, 0.25, 0.18 + H + 0.75)), 'steel', 16))
    o.append(cyl(0.13, 0.04, V((0.95, 0.25, 0.18 + H + 1.22)), 'dark_steel', 16, (math.radians(25), 0, 0)))
    o.append(box((0.5, 0.05, 0.06), V((0, -D / 2 - 0.02, 0.18 + 2.0)), 'hazard', 0.005))
    save(P('generator'), o)


def transformer():
    reset(); o = []
    W, D = 1.9, 1.9
    o.append(box((1.95, 1.95, 0.12), V((0, 0, 0.06)), 'concrete', 0.02))
    o.append(box((1.1, 1.0, 1.6), V((0, 0, 0.12 + 0.8)), 'paint_grey', 0.04))           # tank
    o.append(box((1.16, 1.06, 0.06), V((0, 0, 0.12 + 1.62)), 'paint_grey', 0.02))
    for side in (-1, 1):                                                                    # radiator fins
        for i in range(9):
            o.append(box((0.025, 0.36, 1.3), V((-0.42 + i * 0.105, side * 0.7, 0.85)), 'paint_grey', 0.006))
        o.append(cyl(0.035, 0.95, V((0, side * 0.7, 1.45)), 'paint_grey', 10, (0, math.radians(90), 0)))
    for i, x in enumerate((-0.35, 0, 0.35)):                                               # HV bushings
        for k in range(6):
            o.append(cyl(0.085 if k % 2 == 0 else 0.065, 0.06, V((x, -0.2, 1.8 + k * 0.07)), 'porcelain', 16))
        o.append(cyl(0.025, 0.12, V((x, -0.2, 2.26)), 'copper', 10))
    for x in (-0.3, 0, 0.3):                                                                # LV bushings
        o.append(cyl(0.05, 0.18, V((x, 0.25, 1.85)), 'porcelain', 12))
    o.append(cyl(0.2, 0.9, V((0, 0.42, 2.05)), 'paint_grey', 20, (0, math.radians(90), 0)))  # conservator
    o.append(cyl(0.03, 0.4, V((0.2, 0.3, 1.88)), 'paint_grey', 8))
    o.append(box((0.4, 0.02, 0.3), V((0, -0.51, 1.2)), 'hazard', 0.005))
    o.append(box((0.3, 0.02, 0.06), V((0, -0.52, 1.0)), 'glow_power', 0.004))
    save(P('transformer'), o)


def cabinets(name, n, w, d, h, body, door, extra):
    reset(); o = []
    o.append(box((n * w, d, 0.08), V((0, 0, 0.04)), 'paint_dark', 0.01))
    for i in range(n):
        x = -n * w / 2 + w * (i + 0.5)
        o.append(box((w - 0.01, d, h), V((x, 0, 0.08 + h / 2)), body, 0.015))
        o.append(box((w - 0.06, 0.015, h - 0.12), V((x, -d / 2 - 0.004, 0.08 + h / 2)), door, 0.008))
        o.append(box((0.025, 0.03, 0.18), V((x + w / 2 - 0.08, -d / 2 - 0.02, 0.08 + h * 0.55)), 'steel', 0.006))
        extra(o, i, x, -d / 2 - 0.012, h)
    o.append(box((n * w + 0.02, d + 0.02, 0.05), V((0, 0, 0.08 + h + 0.025)), 'paint_dark', 0.01))
    save(P(name), o)


def switchgear():
    def extra(o, i, x, y, h):
        o.append(box((0.22, 0.02, 0.14), V((x, y - 0.006, 1.55)), 'glass', 0.004))        # meter window
        for k, c in enumerate(('glow_ok', 'glow_power', 'glow_alert')):
            o.append(cyl(0.018, 0.02, V((x - 0.08 + k * 0.08, y - 0.01, 1.35)), c, 10, (math.radians(90), 0, 0), 0))
        o.append(box((0.3, 0.02, 0.06), V((x, y - 0.006, 1.9)), 'accent_power', 0.004))
        o.append(box((0.12, 0.03, 0.12), V((x, y - 0.01, 1.0)), 'paint_dark', 0.01))   # breaker handle
    cabinets('switchgear', 4, 0.48, 0.85, 2.1, 'paint_light', 'paint_white', extra)


def ups():
    def extra(o, i, x, y, h):
        if i == 1:
            o.append(box((0.26, 0.02, 0.18), V((x, y - 0.008, 1.5)), 'screen', 0.004))
            o.append(box((0.22, 0.01, 0.12), V((x, y - 0.02, 1.5)), 'glow_cooling', 0.002))
        for k in range(10):
            o.append(box((w - 0.16, 0.01, 0.025), V((x, y - 0.006, 0.35 + k * 0.06)), 'paint_dark', 0.0))
        o.append(box((w - 0.1, 0.012, 0.05), V((x, y - 0.008, 1.95)), 'accent_power', 0.003))
    w = 0.62
    cabinets('ups', 3, w, 0.85, 2.0, 'paint_dark', 'paint_grey', extra)


def battery():
    reset(); o = []
    o.append(box((0.9, 0.8, 2.0), V((0, 0, 1.0)), 'paint_dark', 0.015))
    o.append(box((0.8, 0.02, 1.85), V((0, -0.395, 1.0)), 'paint_grey', 0.006))
    for r in range(6):                                                                      # battery strings behind a mesh door
        for c in range(4):
            o.append(box((0.17, 0.05, 0.2), V((-0.285 + c * 0.19, -0.42, 0.25 + r * 0.29)), 'rubber', 0.01))
            o.append(box((0.04, 0.02, 0.02), V((-0.285 + c * 0.19 - 0.05, -0.45, 0.36 + r * 0.29)), 'glow_alert' if c % 2 else 'accent_power', 0.0))
    o.append(box((0.6, 0.02, 0.05), V((0, -0.43, 1.92)), 'accent_power', 0.003))
    save(P('battery'), o)


def pdu():
    reset(); o = []
    o.append(box((0.9, 0.85, 1.9), V((0, 0, 0.95)), 'paint_light', 0.02))
    o.append(box((0.8, 0.02, 1.7), V((0, -0.43, 0.95)), 'paint_white', 0.008))
    o.append(box((0.5, 0.02, 0.6), V((0, -0.445, 1.2)), 'paint_dark', 0.004))
    for r in range(4):
        for c in range(6):
            o.append(box((0.05, 0.02, 0.09), V((-0.18 + c * 0.072, -0.455, 0.98 + r * 0.14)), 'paint_white', 0.003))
    o.append(box((0.24, 0.02, 0.12), V((0, -0.45, 1.65)), 'screen', 0.003))
    o.append(box((0.2, 0.01, 0.08), V((0, -0.462, 1.65)), 'glow_power', 0.002))
    o.append(box((0.92, 0.87, 0.06), V((0, 0, 1.88)), 'accent_power', 0.01))
    save(P('pdu'), o)


def diesel_tank():
    reset(); o = []
    o.append(box((2.95, 0.98, 0.25), V((0, 0, 0.125)), 'concrete', 0.03))                  # bund
    for x in (-0.9, 0.9):
        o.append(box((0.18, 0.7, 0.3), V((x, 0, 0.38)), 'dark_steel', 0.02))
    o.append(cyl(0.4, 2.5, V((0, 0, 0.82)), 'paint_white', 32, (0, math.radians(90), 0), 0.04))
    for x in (-1.25, 1.25):
        o.append(cyl(0.4, 0.02, V((x, 0, 0.82)), 'paint_light', 32, (0, math.radians(90), 0), 0))
    o.append(cyl(0.405, 0.12, V((0, 0, 0.82)), 'accent_power', 32, (0, math.radians(90), 0), 0))
    o.append(cyl(0.12, 0.12, V((-0.6, 0, 1.25)), 'dark_steel', 16))                          # manhole
    o.append(cyl(0.03, 0.7, V((0.8, 0.15, 1.5)), 'steel', 10))                              # vent
    o.append(cyl(0.05, 0.08, V((0.8, 0.15, 1.88)), 'dark_steel', 10))
    o.append(box((0.2, 0.05, 0.2), V((0, -0.42, 0.85)), 'hazard', 0.005))
    save(P('diesel_tank'), o)


def underground_tank():
    reset(); o = []
    o.append(box((2.9, 1.9, 0.1), V((0, 0, 0.05)), 'concrete', 0.03))
    for x in (-0.8, 0.8):
        o.append(cyl(0.32, 0.04, V((x, 0, 0.12)), 'dark_steel', 24))
        o.append(cyl(0.26, 0.02, V((x, 0, 0.145)), 'steel', 24))
    o.append(cyl(0.04, 1.2, V((1.25, 0.75, 0.7)), 'steel', 10))
    o.append(cyl(0.07, 0.1, V((1.25, 0.75, 1.32)), 'dark_steel', 10))
    o.append(box((0.4, 0.3, 0.5), V((-1.15, 0.7, 0.35)), 'paint_white', 0.03))            # fill point
    o.append(box((0.42, 0.32, 0.06), V((-1.15, 0.7, 0.58)), 'accent_power', 0.01))
    save(P('underground_tank'), o)


def chiller():
    reset(); o = []
    W, D = 2.9, 1.9
    o.append(box((W, D, 0.15), V((0, 0, 0.075)), 'dark_steel', 0.02))
    o.append(box((W, D, 0.75), V((0, 0, 0.15 + 0.375)), 'paint_white', 0.03))               # compressor section
    o += louvres(1.2, 0.45, V((-0.6, -D / 2 - 0.005, 0.55)), '-Y')
    o.append(box((0.5, 0.04, 0.4), V((0.9, -D / 2 - 0.01, 0.55)), 'paint_light', 0.01))
    o.append(box((0.2, 0.02, 0.12), V((0.9, -D / 2 - 0.035, 0.6)), 'screen', 0.003))
    o.append(box((0.16, 0.01, 0.07), V((0.9, -D / 2 - 0.045, 0.6)), 'glow_cooling', 0.002))
    for side in (-1, 1):                                                                    # V-shaped coil banks
        o.append(box((W - 0.1, 0.06, 1.15), V((0, side * 0.62, 1.45)), 'coil', 0.005, (side * math.radians(-18), 0, 0)))
        for i in range(24):
            o.append(box((0.008, 0.07, 1.1), V((-W / 2 + 0.1 + i * (W - 0.2) / 23, side * 0.62, 1.45)), 'galvanised', 0, (side * math.radians(-18), 0, 0)))
    for x in (-W / 2 + 0.03, W / 2 - 0.03):
        o.append(box((0.06, D, 1.2), V((x, 0, 1.5)), 'paint_white', 0.01))
    o.append(box((W, D, 0.08), V((0, 0, 2.12)), 'paint_white', 0.02))
    for x in (-0.72, 0.72):
        for y in (-0.45, 0.45):
            o += fan_shroud(0.4, V((x, y, 2.25)))
            o.append(fan(0.38, V((x, y, 2.2)), 'Fan'))
    o.append(box((W + 0.01, 0.02, 0.07), V((0, -D / 2 - 0.01, 0.86)), 'accent_cooling', 0.003))
    save(P('chiller'), o)


def dry_cooler():
    reset(); o = []
    W, D = 1.9, 0.9
    for x in (-0.85, 0.85):
        for y in (-0.35, 0.35):
            o.append(box((0.07, 0.07, 0.6), V((x, y, 0.3)), 'galvanised', 0.008))
    o.append(box((W, D, 0.5), V((0, 0, 0.85)), 'paint_white', 0.03))
    o.append(box((W - 0.08, 0.02, 0.38), V((0, -D / 2 - 0.004, 0.85)), 'coil', 0.004))
    for i in range(30):
        o.append(box((0.006, 0.025, 0.36), V((-W / 2 + 0.06 + i * (W - 0.12) / 29, -D / 2 - 0.01, 0.85)), 'galvanised', 0))
    for x in (-0.47, 0.47):
        o += fan_shroud(0.38, V((x, 0, 1.22)), 0.2)
        o.append(fan(0.36, V((x, 0, 1.18)), 'Fan'))
    o.append(box((W + 0.01, 0.02, 0.05), V((0, -D / 2 - 0.012, 0.64)), 'accent_cooling', 0.003))
    save(P('dry_cooler'), o)


def cooling_tower():
    reset(); o = []
    o.append(box((1.95, 1.95, 0.25), V((0, 0, 0.125)), 'concrete', 0.03))                  # basin
    o.append(box((1.85, 1.85, 2.0), V((0, 0, 1.25)), 'paint_light', 0.05))
    for face, c in (('-Y', V((0, -0.93, 0.75))), ('+Y', V((0, 0.93, 0.75))), ('-X', V((-0.93, 0, 0.75))), ('+X', V((0.93, 0, 0.75)))):
        o += louvres(1.6, 0.8, c, face, 9, 'paint_white')
    o.append(box((1.9, 1.9, 0.1), V((0, 0, 2.3)), 'paint_white', 0.02))
    o.append(cyl(0.82, 0.55, V((0, 0, 2.62)), 'paint_white', 32, r2=0.75))                  # fan stack
    o.append(cyl(0.7, 0.02, V((0, 0, 2.85)), 'paint_dark', 32, bevel=0))
    o += fan_shroud(0.7, V((0, 0, 2.85)), 0.12, 'paint_white')
    o.append(fan(0.66, V((0, 0, 2.82)), 'Fan', 6))
    for k in range(9):                                                                      # ladder on the side
        o.append(box((0.4, 0.03, 0.03), V((0.5, -0.97, 0.3 + k * 0.27)), 'galvanised', 0.005))
    for x in (0.3, 0.7):
        o.append(box((0.04, 0.04, 2.6), V((x, -0.97, 1.4)), 'galvanised', 0.008))
    o.append(box((1.86, 0.02, 0.08), V((0, -0.94, 2.15)), 'accent_cooling', 0.003))
    save(P('cooling_tower'), o)


def crah():
    reset(); o = []
    W, D, H = 0.95, 1.9, 2.0
    o.append(box((W, D, H), V((0, 0, H / 2)), 'paint_white', 0.025))
    # in the game the unit stands 1 x 2 with its long side along z; the front (+Z in Godot) is the short end
    o.append(box((W - 0.12, 0.02, 1.0), V((0, -D / 2 - 0.004, 1.25)), 'paint_dark', 0.004))
    for k in range(14):
        o.append(box((W - 0.16, 0.02, 0.025), V((0, -D / 2 - 0.012, 0.8 + k * 0.066)), 'paint_light', 0.0))
    o.append(box((0.22, 0.02, 0.14), V((0, -D / 2 - 0.012, 0.5)), 'screen', 0.003))
    o.append(box((0.18, 0.01, 0.1), V((0, -D / 2 - 0.022, 0.5)), 'glow_cooling', 0.002))
    o.append(box((W + 0.02, D + 0.02, 0.1), V((0, 0, H - 0.1)), 'glow_cooling', 0.006))
    for y in (-0.5, 0.0, 0.5):
        o.append(box((W - 0.1, 0.4, 0.02), V((0, y, H + 0.01)), 'dark_steel', 0.004))
    save(P('crah'), o)


def rack_cabinet(o, w=0.6, d=1.0, h=2.1):
    o.append(box((w, d, h), V((0, 0, h / 2)), 'paint_dark', 0.012))
    o.append(box((w - 0.02, 0.01, 0.12), V((0, -d / 2 - 0.004, h - 0.08)), 'paint_dark', 0.003))
    for x in (-w / 2 + 0.03, w / 2 - 0.03):
        o.append(box((0.03, 0.02, h - 0.1), V((x, -d / 2 - 0.006, h / 2)), 'dark_steel', 0.004))


def core_router():
    reset(); o = []
    rack_cabinet(o, 0.9, 0.95, 2.1)
    o.append(box((0.8, 0.02, 1.4), V((0, -0.48, 1.05)), 'paint_grey', 0.006))             # chassis
    for i in range(10):                                                                      # line cards with ports
        x = -0.36 + i * 0.08
        o.append(box((0.07, 0.02, 1.0), V((x, -0.495, 1.0)), 'dark_steel', 0.003))
        for k in range(8):
            o.append(box((0.03, 0.01, 0.02), V((x, -0.507, 0.6 + k * 0.1)), 'glow_fiber' if (i + k) % 3 else 'glow_ok', 0))
    o.append(box((0.76, 0.02, 0.16), V((0, -0.497, 1.66)), 'paint_dark', 0.004))
    for k in range(6):
        o.append(box((0.08, 0.01, 0.1), V((-0.3 + k * 0.12, -0.508, 1.66)), 'dark_steel', 0.002))
    for k in range(4):
        o.append(box((0.16, 0.01, 0.03), V((-0.27 + k * 0.18, -0.508, 0.38)), 'glow_ok', 0))
    # orange fibres looping down from the cards
    for i in range(0, 10, 2):
        x = -0.36 + i * 0.08
        o.append(tube_path([V((x, -0.51, 0.75)), V((x + 0.02, -0.6, 0.55)), V((x + 0.08, -0.56, 0.3)), V((0.42, -0.5, 0.25))], 0.007, 'fiber', 5))
    o.append(box((0.92, 0.97, 0.05), V((0, 0, 2.1)), 'glow_fiber', 0.004))
    save(P('core_router'), o)


def spine():
    reset(); o = []
    rack_cabinet(o, 0.9, 0.95, 2.1)
    for u in range(4):
        z = 0.7 + u * 0.32
        o.append(box((0.8, 0.02, 0.2), V((0, -0.485, z)), 'paint_grey', 0.004))
        for k in range(24):
            o.append(box((0.022, 0.01, 0.03), V((-0.33 + (k % 12) * 0.058, -0.497, z - 0.04 + (k // 12) * 0.07)), 'glow_fiber' if k % 4 else 'glow_ok', 0))
    for k in range(6):
        x = -0.3 + k * 0.12
        o.append(tube_path([V((x, -0.5, 0.95)), V((x, -0.58, 0.8)), V((0.42, -0.52, 0.5))], 0.006, 'fiber', 5))
    o.append(box((0.92, 0.97, 0.05), V((0, 0, 2.1)), 'glow_fiber', 0.004))
    save(P('spine'), o)


def odf():
    reset(); o = []
    rack_cabinet(o, 0.9, 0.5, 2.1)
    for u in range(8):
        z = 0.45 + u * 0.18
        o.append(box((0.8, 0.02, 0.1), V((0, -0.26, z)), 'paint_light', 0.003))
        for k in range(12):
            o.append(box((0.03, 0.01, 0.025), V((-0.33 + k * 0.06, -0.272, z)), 'fiber', 0))
    for k in range(10):
        x = -0.33 + k * 0.07
        o.append(tube_path([V((x, -0.28, 0.5 + (k % 8) * 0.18)), V((x + 0.02, -0.36, 0.4)), V((0.4, -0.3, 0.15))], 0.005, 'fiber', 5))
    o.append(box((0.92, 0.52, 0.05), V((0, 0, 2.1)), 'glow_fiber', 0.004))
    save(P('odf'), o)


def battery_container():
    reset(); o = []
    W, D, H = 2.95, 0.95, 1.9
    o.append(box((W, D, 0.12), V((0, 0, 0.06)), 'concrete', 0.02))
    o.append(box((W - 0.02, D - 0.02, H), V((0, 0, 0.12 + H / 2)), 'paint_white', 0.03))
    for i in range(36):                                                                     # corrugated sides
        x = -W / 2 + 0.08 + i * (W - 0.16) / 35
        for y in (-D / 2 + 0.005, D / 2 - 0.005):
            o.append(box((0.03, 0.025, H - 0.12), V((x, y, 0.12 + H / 2)), 'paint_white', 0.006))
    for x in (-1.1, -0.65):
        o.append(box((0.42, 0.02, H - 0.25), V((x, -D / 2 - 0.02, 0.12 + H / 2)), 'paint_light', 0.006))
        o.append(box((0.025, 0.03, 0.3), V((x + 0.15, -D / 2 - 0.035, 0.12 + H / 2)), 'steel', 0.004))
    o.append(box((0.6, 0.3, 0.45), V((0.9, 0, 0.12 + H + 0.22)), 'paint_light', 0.03))     # HVAC unit
    o += fan_shroud(0.16, V((0.9, 0, 0.12 + H + 0.47)), 0.06)
    o.append(box((W, 0.02, 0.12), V((0, -D / 2 - 0.02, 0.12 + H * 0.85)), 'accent_power', 0.004))
    o.append(box((0.25, 0.02, 0.25), V((0.4, -D / 2 - 0.03, 1.2)), 'hazard', 0.004))
    save(P('battery_container'), o)


def lamp_post():
    reset(); o = []
    o.append(cyl(0.16, 0.35, V((0, 0, 0.175)), 'concrete', 16))
    o.append(cyl(0.075, 4.6, V((0, 0, 2.6)), 'dark_steel', 12, r2=0.045))
    o.append(tube_path([V((0, 0, 4.7)), V((0, -0.15, 4.92)), V((0, -0.45, 5.0))], 0.035, 'dark_steel', 8))
    o.append(box((0.24, 0.62, 0.07), V((0, -0.65, 4.98)), 'dark_steel', 0.03))
    o.append(box((0.19, 0.5, 0.02), V((0, -0.65, 4.94)), 'lamp', 0.008))
    save(P('lamp_post'), o)


def bench():
    reset(); o = []
    for x in (-0.75, 0.75):                                                                # cast legs
        o.append(box((0.07, 0.5, 0.42), V((x, 0, 0.21)), 'dark_steel', 0.02))
        o.append(box((0.07, 0.08, 0.5), V((x, 0.2, 0.62)), 'dark_steel', 0.02, (math.radians(-12), 0, 0)))
        o.append(box((0.07, 0.4, 0.05), V((x, -0.05, 0.62)), 'dark_steel', 0.02))
    for k in range(4):
        o.append(box((1.85, 0.1, 0.035), V((0, -0.17 + k * 0.115, 0.44)), 'wood', 0.012))
    for k in range(3):
        o.append(box((1.85, 0.035, 0.09), V((0, 0.24 + k * 0.012, 0.6 + k * 0.11)), 'wood', 0.012, (math.radians(-12), 0, 0)))
    save(P('bench'), o)


def bike_rack():
    reset(); o = []
    o.append(box((1.9, 0.3, 0.04), V((0, 0, 0.02)), 'concrete', 0.01))
    for k in range(5):
        x = -0.8 + k * 0.4
        pts = [V((x, -0.35, 0.0))] + [V((x, -0.35 * math.cos(t), 0.55 + 0.25 * math.sin(t))) for t in [i * math.pi / 8 for i in range(9)]] + [V((x, 0.35, 0.0))]
        pts[1] = V((x, -0.35, 0.55))
        o.append(tube_path(pts, 0.022, 'galvanised', 8))
    save(P('bike_rack'), o)


def bollard():
    reset(); o = []
    o.append(cyl(0.1, 0.9, V((0, 0, 0.45)), 'dark_steel', 16))
    o.append(cyl(0.104, 0.08, V((0, 0, 0.75)), 'hazard', 16, bevel=0))
    o.append(cyl(0.1, 0.04, V((0, 0, 0.92)), 'dark_steel', 16, r2=0.06))
    save(P('bollard'), o)


def camera_pole():
    reset(); o = []
    o.append(cyl(0.14, 0.25, V((0, 0, 0.125)), 'concrete', 16))
    o.append(cyl(0.06, 3.8, V((0, 0, 2.1)), 'galvanised', 12, r2=0.045))
    o.append(box((0.08, 0.3, 0.06), V((0, -0.12, 3.95)), 'galvanised', 0.01))
    o.append(box((0.16, 0.38, 0.16), V((0, -0.3, 3.85)), 'paint_white', 0.04, (math.radians(-15), 0, 0)))
    o.append(box((0.18, 0.4, 0.03), V((0, -0.32, 3.95)), 'paint_white', 0.01, (math.radians(-15), 0, 0)))
    o.append(cyl(0.05, 0.02, V((0, -0.5, 3.8)), 'glass', 16, (math.radians(75), 0, 0)))
    o.append(box((0.2, 0.12, 0.25), V((0, 0.08, 1.4)), 'paint_light', 0.02))
    save(P('camera_pole'), o)


# ---- fences: one 1 m panel (along X, centred) and one post per type, at real height ----

def fence_mesh():
    reset(); o = []
    H = 1.8
    o.append(box((1.0, 0.03, 0.04), V((0, 0, H - 0.02)), 'galvanised', 0.008))
    o.append(box((1.0, 0.03, 0.04), V((0, 0, 0.08)), 'galvanised', 0.008))
    for i in range(12):
        o.append(box((0.006, 0.006, H - 0.1), V((-0.5 + (i + 0.5) / 12, 0, H / 2)), 'galvanised', 0))
    for k in range(9):
        o.append(box((1.0, 0.006, 0.006), V((0, 0.004, 0.15 + k * 0.2)), 'galvanised', 0))
    save(P('fence_mesh_panel'), o)
    reset(); o = [cyl(0.03, H + 0.05, V((0, 0, (H + 0.05) / 2)), 'galvanised', 10), cyl(0.034, 0.03, V((0, 0, H + 0.06)), 'dark_steel', 10)]
    save(P('fence_mesh_post'), o)


def fence_welded():
    reset(); o = []
    H = 2.4
    for i in range(5):
        o.append(box((0.008, 0.008, H - 0.04), V((-0.5 + (i + 0.5) / 5, 0, H / 2)), 'paint_green', 0))
    for k in range(int(H / 0.2)):
        z = 0.1 + k * 0.2
        for y in (-0.01, 0.01):
            o.append(box((1.0, 0.007, 0.007), V((0, y, z)), 'paint_green', 0))
    for z in (0.5, 1.3, 2.0):                                                               # V-folds
        o.append(box((1.0, 0.04, 0.012), V((0, 0.01, z)), 'paint_green', 0.003, (math.radians(30), 0, 0)))
    save(P('fence_welded_panel'), o)
    reset(); o = [box((0.06, 0.06, H + 0.05), V((0, 0, (H + 0.05) / 2)), 'paint_green', 0.01), box((0.07, 0.07, 0.02), V((0, 0, H + 0.06)), 'paint_dark', 0.005)]
    save(P('fence_welded_post'), o)


def pales(o, H, m):
    for i in range(7):
        x = -0.5 + (i + 0.5) / 7
        o.append(box((0.065, 0.025, H - 0.12), V((x, 0.02, (H - 0.12) / 2)), m, 0.006))
        for dx in (-0.022, 0, 0.022):                                                      # triple-pointed top
            o.append(cyl(0.012, 0.12 if dx == 0 else 0.09, V((x + dx, 0.02, H - 0.12 + (0.06 if dx == 0 else 0.045))), m, 4, r2=0.0, bevel=0))
    for z in (0.35, H - 0.45):
        o.append(box((1.0, 0.04, 0.05), V((0, -0.015, z)), m, 0.006))


def fence_palisade():
    reset(); o = []
    pales(o, 2.8, 'paint_dark')
    save(P('fence_palisade_panel'), o)
    reset(); o = [box((0.1, 0.06, 2.8), V((0, -0.03, 1.4)), 'paint_dark', 0.01)]
    save(P('fence_palisade_post'), o)


def fence_wall():
    reset(); o = []
    H = 2.6
    o.append(box((1.0, 0.25, H - 0.08), V((0, 0, (H - 0.08) / 2)), 'concrete', 0.0))
    o.append(box((1.0, 0.32, 0.08), V((0, 0, H - 0.04)), 'paint_light', 0.015))
    o.append(box((1.0, 0.27, 0.12), V((0, 0, 0.06)), 'paint_grey', 0.01))
    for z in (0.9, 1.75):
        o.append(box((1.0, 0.26, 0.012), V((0, 0, z)), 'paint_light', 0.0))
    save(P('fence_wall_panel'), o)
    reset(); o = [box((0.3, 0.34, 2.68), V((0, 0, 1.34)), 'concrete', 0.02)]
    save(P('fence_wall_post'), o)


def fence_electric():
    reset(); o = []
    pales(o, 2.4, 'paint_dark')
    for k in range(6):
        o.append(box((1.0, 0.004, 0.004), V((0, -0.22, 2.45 + k * 0.1)), 'steel', 0))
    o.append(box((0.22, 0.01, 0.15), V((0.2, -0.03, 1.5)), 'hazard', 0.004))
    o.append(box((0.08, 0.012, 0.06), V((0.2, -0.04, 1.5)), 'paint_dark', 0.0))
    save(P('fence_electric_panel'), o)
    reset(); o = [box((0.1, 0.06, 3.0), V((0, -0.03, 1.5)), 'paint_dark', 0.01),
                  box((0.04, 0.25, 0.04), V((0, -0.14, 2.42)), 'paint_dark', 0.005)]
    o.append(box((0.04, 0.04, 0.62), V((0, -0.24, 2.7)), 'paint_dark', 0.005))
    for k in range(6):
        o.append(cyl(0.015, 0.04, V((0, -0.22, 2.45 + k * 0.1)), 'accent_power', 8, (math.radians(90), 0, 0), 0))
    save(P('fence_electric_post'), o)


if __name__ == '__main__':
    only = set(os.environ.get('ONLY', '').split(',')) - {''}
    for f in (generator, transformer, switchgear, ups, battery, pdu, diesel_tank, underground_tank, chiller, dry_cooler,
              cooling_tower, crah, core_router, spine, odf, battery_container, lamp_post, bench, bike_rack, bollard, camera_pole,
              fence_mesh, fence_welded, fence_palisade, fence_wall, fence_electric):
        if not only or f.__name__ in only:
            f()
