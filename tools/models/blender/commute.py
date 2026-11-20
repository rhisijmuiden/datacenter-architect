"""Builds the commuting models (own models, CC0): Dutch city bikes and an e-bike, a bike shed with e-bike charging,
and an EV charging post.

    python3 tools/models/blender/commute.py game/assets/models/made

Godot coordinates through g()/gs() as in vehicles.py. Bikes stand on the ground with the front wheel towards +z.
"""
import os, sys, math
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import *
from vehicles import g, gs
from mathutils import Vector as V

OUT = sys.argv[-1] if len(sys.argv) > 1 and not sys.argv[-1].endswith('.py') else 'game/assets/models/made'
os.makedirs(OUT, exist_ok=True)
P = lambda n: os.path.join(OUT, n + '.glb')

kit.PALETTE.update({
    'bike_green': ((0.12, 0.27, 0.2, 1), 0.3, 0.35),
    'bike_black': ((0.05, 0.055, 0.06, 1), 0.3, 0.3),
    'bike_cream': ((0.86, 0.8, 0.66, 1), 0.2, 0.35),
    'bike_blue': ((0.16, 0.3, 0.52, 1), 0.3, 0.35),
    'bike_grey': ((0.42, 0.45, 0.48, 1), 0.5, 0.3),
    'chrome': ((0.8, 0.82, 0.84, 1), 1.0, 0.18),
    'leather': ((0.25, 0.16, 0.1, 1), 0.0, 0.6),
    'sedum': ((0.36, 0.48, 0.22, 1), 0.0, 0.95),
    'polycarb': ((0.7, 0.82, 0.86, 1), 0.1, 0.1),
})


def tube(a, b, r, m):
    return tube_path([g(*a), g(*b)], r, m, 8)


def wheel(o, z, r=0.34):
    """Tyre, rim, hub and a few spokes; axle along x."""
    y = r
    o.append(torus(r - 0.02, 0.022, g(0, y, z), 'rubber', rot=(0, math.pi / 2, 0), major=32, minor=6))
    o.append(torus(r - 0.05, 0.008, g(0, y, z), 'chrome', rot=(0, math.pi / 2, 0), major=32, minor=4))
    o.append(cyl(0.03, 0.08, g(0, y, z), 'chrome', verts=12, rot=(0, math.pi / 2, 0)))
    for k in range(12):
        a = 2 * math.pi * k / 12
        o.append(tube((0, y, z), (0, y + math.sin(a) * (r - 0.05), z + math.cos(a) * (r - 0.05)), 0.0025, 'chrome'))


def arc(cz, cy, R, a0, a1, n=10):
    """Points along an arc in the y/z plane around (cy, cz), angles from +z towards +y."""
    return [g(0, cy + R * math.sin(a0 + (a1 - a0) * i / n), cz + R * math.cos(a0 + (a1 - a0) * i / n)) for i in range(n + 1)]


def bicycle(name, frame, ebike=False):
    """A Dutch city bike: low step frame, upright bars, mudguards, chain guard, rear carrier and kickstand."""
    reset(); o = []
    rz, fz, r = -0.55, 0.55, 0.34
    wheel(o, rz); wheel(o, fz)
    bb = (0, 0.3, -0.05); seat = (0, 0.86, -0.2); head_lo = (0, 0.66, 0.38); head_hi = (0, 0.86, 0.34)
    o.append(tube_path([g(*bb), g(0, 0.36, 0.14), g(0, 0.52, 0.3), g(*head_lo)], 0.022, frame))  # low-step down tube
    o.append(tube(bb, seat, 0.02, frame))
    o.append(tube(head_lo, head_hi, 0.026, frame))
    o.append(tube((0.04, 0.3, -0.05), (0.04, r, rz), 0.011, frame)); o.append(tube((-0.04, 0.3, -0.05), (-0.04, r, rz), 0.011, frame))
    o.append(tube((0.03, 0.74, -0.17), (0.04, r, rz), 0.01, frame)); o.append(tube((-0.03, 0.74, -0.17), (-0.04, r, rz), 0.01, frame))
    o.append(tube((0.035, 0.66, 0.38), (0.035, r, fz), 0.012, frame)); o.append(tube((-0.035, 0.66, 0.38), (-0.035, r, fz), 0.012, frame))
    # upright handlebar curving back to the rider
    o.append(tube(head_hi, (0, 1.0, 0.31), 0.014, 'chrome'))
    o.append(tube_path([g(-0.3, 1.02, 0.12), g(-0.18, 1.02, 0.28), g(0, 1.0, 0.31), g(0.18, 1.02, 0.28), g(0.3, 1.02, 0.12)], 0.011, 'chrome'))
    for x in (-0.3, 0.3): o.append(cyl(0.016, 0.1, g(x, 1.02, 0.1), 'rubber', verts=10, rot=(math.pi / 2, 0, 0)))
    # saddle and seat post
    o.append(tube(seat, (0, 0.93, -0.22), 0.012, 'chrome'))
    o.append(box(gs(0.17, 0.06, 0.27), g(0, 0.96, -0.24), 'leather', 0.025))
    # mudguards
    for z in (rz, fz):
        o.append(tube_path(arc(z, r, r + 0.04, -0.2 if z > 0 else 0.6, math.pi - (0.6 if z > 0 else -0.2), 12), 0.03, frame, 6))
    # rear carrier
    o.append(box(gs(0.16, 0.02, 0.42), g(0, 0.76, -0.5), 'dark_steel', 0.005))
    o.append(tube((0.07, 0.76, -0.7), (0.05, r, rz), 0.008, 'dark_steel')); o.append(tube((-0.07, 0.76, -0.7), (-0.05, r, rz), 0.008, 'dark_steel'))
    # chain guard, crank and pedals
    o.append(box(gs(0.03, 0.1, 0.6), g(0.07, 0.32, -0.3), frame, 0.02, rot=(math.atan2(0.03, 0.5), 0, 0)))
    o.append(cyl(0.09, 0.02, g(0.06, 0.3, -0.05), 'dark_steel', verts=16, rot=(0, math.pi / 2, 0)))
    o.append(tube((0.09, 0.3, -0.05), (0.1, 0.18, 0.02), 0.01, 'dark_steel')); o.append(tube((-0.09, 0.3, -0.05), (-0.1, 0.42, -0.12), 0.01, 'dark_steel'))
    o.append(box(gs(0.09, 0.02, 0.05), g(0.14, 0.18, 0.02), 'rubber', 0.005)); o.append(box(gs(0.09, 0.02, 0.05), g(-0.14, 0.42, -0.12), 'rubber', 0.005))
    # lamps: white at the front, red at the back
    o.append(cyl(0.03, 0.05, g(0, 0.62, 0.48), 'lamp', verts=12, rot=(math.pi / 2, 0, 0)))
    o.append(box(gs(0.06, 0.03, 0.02), g(0, 0.74, -0.72), 'glow_alert', 0.005))
    o.append(tube((0.04, 0.3, -0.12), (0.06, 0.02, -0.3), 0.008, 'dark_steel'))  # kickstand
    if ebike:
        # battery on the carrier, a motor in the front hub and a small display on the bars
        o.append(box(gs(0.11, 0.09, 0.36), g(0, 0.82, -0.48), 'paint_dark', 0.02))
        o.append(box(gs(0.112, 0.012, 0.05), g(0, 0.86, -0.3), 'glow_ok', 0.003))
        o.append(cyl(0.07, 0.1, g(0, r, fz), 'paint_dark', verts=16, rot=(0, math.pi / 2, 0)))
        o.append(box(gs(0.06, 0.035, 0.04), g(0.08, 1.04, 0.28), 'screen', 0.005))
    save(P(name), o)


def bike_shed():
    """A 4 x 2 m bike shed open to the front (+z): steel frame, sedum roof sloping to the back, glass sides,
    wheel stands and two e-bike charging boxes on the back wall."""
    reset(); o = []
    for x in (-1.95, 1.95):
        o.append(box(gs(0.08, 2.35, 0.08), g(x, 1.175, 0.95), 'dark_steel', 0.01))
        o.append(box(gs(0.08, 2.05, 0.08), g(x, 1.025, -0.95), 'dark_steel', 0.01))
        o.append(box(gs(0.02, 1.7, 1.8), g(x, 1.05, 0), 'polycarb', 0.005))  # glass side
    o.append(box(gs(0.08, 2.05, 0.08), g(0, 1.025, -0.95), 'dark_steel', 0.01))
    tilt = math.atan2(0.3, 2.0)
    o.append(box(gs(4.2, 0.1, 2.2), g(0, 2.2, 0), 'dark_steel', 0.02, rot=(tilt, 0, 0)))
    o.append(box(gs(4.0, 0.06, 2.0), g(0, 2.28, 0), 'sedum', 0.02, rot=(tilt, 0, 0)))
    for i in range(20):  # slatted back wall
        o.append(box(gs(3.9, 0.07, 0.03), g(0, 0.12 + i * 0.095, -0.97), 'wood', 0.005))
    for k in range(10):  # wheel stands, one per bike
        x = -1.71 + k * 0.38
        o.append(tube_path([g(x, 0.0, -0.75), g(x, 0.45, -0.6), g(x, 0.45, -0.3), g(x, 0.0, -0.2)], 0.012, 'galvanised'))
    for x in (-0.9, 0.9):  # e-bike charging boxes with a green light
        o.append(box(gs(0.3, 0.4, 0.1), g(x, 1.15, -0.9), 'paint_white', 0.02))
        o.append(box(gs(0.2, 0.03, 0.01), g(x, 1.27, -0.845), 'glow_ok', 0.003))
        o.append(tube_path([g(x, 1.0, -0.85), g(x + 0.05, 0.6, -0.8), g(x + 0.1, 0.5, -0.7)], 0.008, 'cable_dark'))
    o.append(box(gs(0.5, 0.18, 0.02), g(0, 1.85, -0.94), 'glow_ok', 0.003))  # e-bike sign
    save(P('bike_shed'), o)


def ev_charger():
    """A charging post for electric cars: concrete foot, white post with a screen, green status light and a coiled cable."""
    reset(); o = []
    o.append(box(gs(0.45, 0.12, 0.35), g(0, 0.06, 0), 'concrete', 0.02))
    o.append(box(gs(0.32, 1.45, 0.2), g(0, 0.84, 0), 'paint_white', 0.04))
    o.append(box(gs(0.26, 0.5, 0.01), g(0, 1.15, 0.101), 'paint_dark', 0.005))
    o.append(box(gs(0.16, 0.1, 0.01), g(0, 1.28, 0.107), 'screen', 0.003))
    o.append(box(gs(0.2, 0.03, 0.01), g(0, 1.48, 0.104), 'glow_ok', 0.003))
    o.append(box(gs(0.34, 0.08, 0.22), g(0, 1.6, 0), 'accent_power', 0.02))
    o.append(box(gs(0.07, 0.12, 0.06), g(0.12, 0.95, 0.13), 'paint_dark', 0.01))  # holster
    o.append(tube_path([g(0.12, 0.9, 0.15), g(0.2, 0.6, 0.25), g(0.18, 0.3, 0.3), g(0.05, 0.25, 0.28), g(-0.05, 0.4, 0.22), g(-0.1, 0.7, 0.14)], 0.014, 'cable_dark'))
    save(P('ev_charger'), o)


if __name__ == '__main__':
    only = set(os.environ.get('ONLY', '').split(',')) - {''}
    fns = [lambda: bicycle('bicycle_a', 'bike_green'), lambda: bicycle('bicycle_b', 'bike_black'),
           lambda: bicycle('bicycle_c', 'bike_cream'), lambda: bicycle('bicycle_d', 'bike_blue'),
           lambda: bicycle('ebike', 'bike_grey', True), bike_shed, ev_charger]
    for f in fns:
        if not only or f.__name__ in only:
            f()
