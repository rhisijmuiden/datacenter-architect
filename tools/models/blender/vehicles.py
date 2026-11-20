"""Builds the game's own site vehicles and building material as .glb files (CC0 like the game).

    python3 tools/models/blender/vehicles.py game/assets/models/made

Positions below are written in Godot coordinates (x right, y up, z forward) through g() and gs(), so the layouts
match the node pivots in Models.cs: the lorry's origin is the kingpin, the trailer pivots there; the excavator parts
are exported per joint; crane mast and jib come as 1.5 m and 1.2 m lattice sections the game stacks to any height.
The material 'body' is recoloured by the game (cab colour per load); 'headlight' becomes the night headlight material.
"""
import os, sys, math
sys.path.insert(0, os.path.dirname(__file__))
import kit
from kit import *
from mathutils import Vector as V

OUT = sys.argv[-1] if len(sys.argv) > 1 and not sys.argv[-1].endswith('.py') else 'game/assets/models/made'
os.makedirs(OUT, exist_ok=True)
P = lambda n: os.path.join(OUT, n + '.glb')

kit.PALETTE.update({
    'body': ((0.92, 0.93, 0.94, 1), 0.1, 0.35),
    'headlight': ((1.0, 0.97, 0.85, 1), 0.0, 0.2),
    'trailer_white': ((0.93, 0.94, 0.95, 1), 0.0, 0.5),
    'crane_yellow': ((0.96, 0.75, 0.0, 1), 0.1, 0.45),
    'exc_orange': ((0.94, 0.5, 0.1, 1), 0.1, 0.4),
    'bucket_steel': ((0.5, 0.47, 0.43, 1), 0.6, 0.6),
    'soil': ((0.38, 0.3, 0.2, 1), 0.0, 1.0),
    'brick_stack': ((0.68, 0.4, 0.3, 1), 0.0, 0.85),
    'block_stack': ((0.78, 0.76, 0.72, 1), 0.0, 0.9),
    'wrap': ((0.85, 0.88, 0.9, 1), 0.0, 0.25),
    'ballast': ((0.35, 0.37, 0.4, 1), 0.0, 0.85),
})


def g(x, y, z):
    """A Godot position as a Blender one."""
    return V((x, -z, y))


def gs(w, h, d):
    """A Godot size (x, y, z) as a Blender size."""
    return (w, d, h)


def wheel(o, x, z, r=0.5, w=0.36, twin=False):
    """Tyre with tread shoulders, a steel rim and wheel nuts, axle along x."""
    side = 1 if x > 0 else -1
    rot = (0, math.radians(90), 0)
    o.append(cyl(r, w, g(x, r, z), 'rubber', 28, rot, bevel=0.07))
    o.append(cyl(r * 0.62, w + 0.02, g(x, r, z), 'steel', 24, rot, bevel=0.02))
    o.append(cyl(r * 0.28, w + 0.06, g(x, r, z), 'dark_steel', 16, rot, bevel=0.01))
    for k in range(8):
        a = 2 * math.pi * k / 8
        o.append(cyl(0.025, 0.04, g(x + side * (w / 2 + 0.03), r + math.sin(a) * r * 0.4, z + math.cos(a) * r * 0.4), 'steel', 6, rot, 0))


# ---------------------------------------------------------------- lorry

def truck_tractor():
    reset(); o = []
    dark, glass = 'paint_dark', 'glass'
    o.append(box(gs(1.1, 0.24, 4.6), g(0, 0.62, 0.9), dark, 0.03))                              # chassis rails
    o.append(cyl(0.55, 0.1, g(0, 0.8, 0), 'dark_steel', 28, (0, 0, 0), 0.02))                  # fifth wheel
    o.append(box(gs(0.5, 0.06, 0.4), g(0, 0.86, -0.2), 'dark_steel', 0.01))
    # cab: rounded box with a high roof, deflector and visor
    o.append(box(gs(2.4, 1.9, 1.5), g(0, 1.55, 2.6), 'body', 0.12, segs=4))
    o.append(box(gs(2.36, 0.55, 1.75), g(0, 2.75, 2.48), 'body', 0.18, segs=4))
    o.append(box(gs(2.3, 0.45, 1.0), g(0, 3.15, 2.0), 'body', 0.2, (math.radians(-14), 0, 0), segs=4))
    o.append(box(gs(2.3, 0.14, 0.16), g(0, 2.92, 3.37), 'body', 0.05))                         # sun visor
    o.append(box(gs(2.12, 0.86, 0.05), g(0, 2.2, 3.36), glass, 0.03))                          # windscreen
    o.append(box(gs(0.05, 0.86, 0.04), g(0, 2.2, 3.385), dark, 0.01))
    o.append(box(gs(1.75, 1.0, 0.06), g(0, 1.22, 3.36), dark, 0.03))                           # grille
    for i in range(5):
        o.append(box(gs(1.6, 0.05, 0.03), g(0, 0.85 + i * 0.18, 3.395), 'steel', 0.01))
    o.append(box(gs(2.44, 0.42, 0.2), g(0, 0.55, 3.33), 'body', 0.08))                          # bumper
    o.append(box(gs(2.0, 0.08, 0.05), g(0, 0.4, 3.43), dark, 0.02))
    for i in range(4):                                                                         # roof marker lights
        o.append(box(gs(0.14, 0.06, 0.06), g(-0.45 + i * 0.3, 3.04, 3.32), 'glow_power', 0.02))
    for x in (-1, 1):
        o.append(box(gs(0.03, 0.75, 0.85), g(x * 1.2, 2.1, 2.75), glass, 0.02))                # side window
        o.append(box(gs(0.02, 1.15, 0.03), g(x * 1.205, 1.6, 2.0), dark, 0.005))               # door gap
        o.append(box(gs(0.05, 0.05, 0.22), g(x * 1.21, 1.6, 2.4), 'steel', 0.01))             # door handle
        o.append(tube_path([g(x * 1.2, 2.45, 3.25), g(x * 1.4, 2.4, 3.3), g(x * 1.45, 2.3, 3.2)], 0.025, dark, 6))
        o.append(box(gs(0.1, 0.5, 0.16), g(x * 1.46, 2.05, 3.2), dark, 0.04))                  # mirror
        o.append(box(gs(0.07, 0.4, 0.12), g(x * 1.5, 2.05, 3.2), glass, 0.02))
        o.append(box(gs(0.42, 0.14, 1.2), g(x * 0.95, 1.08, 2.5), dark, 0.05))                # front mudguard
        o.append(box(gs(0.42, 0.1, 1.1), g(x * 0.95, 1.08, 0), dark, 0.04))                   # rear mudguard
        o.append(box(gs(0.42, 0.5, 0.03), g(x * 0.95, 0.75, -0.58), 'rubber', 0.01))          # mud flap
        o.append(box(gs(0.12, 0.6, 1.5), g(x * 0.99, 0.82, 0.95), 'body', 0.05))              # side skirt
        o.append(box(gs(0.16, 0.04, 0.3), g(x * 1.0, 0.45, 1.85), 'steel', 0.01))             # steps
        o.append(box(gs(0.16, 0.04, 0.3), g(x * 1.0, 0.85, 1.85), 'steel', 0.01))
        o.append(box(gs(0.38, 0.16, 0.05), g(x * 0.82, 0.62, 3.43), 'headlight', 0.03))       # headlights
        o.append(box(gs(0.14, 0.12, 0.05), g(x * 0.5, 0.62, 3.43), 'glow_power', 0.02))       # indicators
        wheel(o, x * 1.04, 2.45)
        wheel(o, x * 1.04, 0)
    o.append(cyl(0.3, 1.0, g(-0.75, 0.9, 1.0), 'steel', 20, (math.radians(90), 0, 0), 0.03))   # fuel tank behind the skirt
    o.append(cyl(0.09, 1.6, g(0.95, 2.6, 1.75), 'steel', 12))                                  # exhaust stack
    o.append(cyl(0.1, 0.12, g(0.95, 3.45, 1.75), 'dark_steel', 12))
    o.append(box(gs(1.6, 1.2, 0.08), g(0, 1.6, 1.82), dark, 0.02))                              # cab back wall
    for i, c in enumerate(('accent_cooling', 'glow_power')):                                    # air and power lines to the trailer
        o.append(tube_path([g(-0.2 + i * 0.4, 1.9, 1.8), g(-0.2 + i * 0.4, 1.6, 1.2), g(-0.2 + i * 0.4, 1.4, 0.95)], 0.02, c, 6))
    save(P('truck_tractor'), o)


def trailer_base(o, deck=True):
    """Chassis, three axles, side guards, landing legs, lights; origin is the kingpin. Returns (front, rear)."""
    front, rear = 0.9, -8.6
    mid, ln = (front + rear) / 2, front - rear
    o.append(box(gs(1.0, 0.22, ln), g(0, 1.08, mid), 'paint_dark', 0.03))
    for z in (rear + 2.1, rear + 1.3, rear + 0.5):
        for x in (-1, 1):
            wheel(o, x * 0.94, z, 0.48, 0.38)
        o.append(cyl(0.07, 1.6, g(0, 0.48, z), 'dark_steel', 10, (0, math.radians(90), 0), 0))
    for x in (-1, 1):
        o.append(box(gs(0.04, 0.32, 3.2), g(x * 1.12, 0.74, rear + 4.3), 'galvanised', 0.01))
        o.append(box(gs(0.04, 0.05, 3.2), g(x * 1.12, 0.9, rear + 4.3), 'galvanised', 0.01))
        o.append(box(gs(0.42, 0.06, 2.6), g(x * 0.98, 1.0, rear + 1.3), 'paint_dark', 0.02))
        o.append(box(gs(0.42, 0.5, 0.03), g(x * 0.98, 0.72, rear + 0.0), 'rubber', 0.01))
        o.append(box(gs(0.12, 0.85, 0.12), g(x * 0.75, 0.5, rear + 5.6), 'steel', 0.02))         # landing legs
        o.append(box(gs(0.3, 0.06, 0.3), g(x * 0.75, 0.05, rear + 5.6), 'dark_steel', 0.01))
        o.append(box(gs(0.3, 0.16, 0.05), g(x * 0.95, 0.9, rear - 0.03), 'glow_alert', 0.02))   # tail lights
        o.append(box(gs(0.1, 0.16, 0.05), g(x * 0.62, 0.9, rear - 0.03), 'glow_power', 0.02))
        for k in range(5):                                                                     # side marker lights
            o.append(box(gs(0.03, 0.05, 0.08), g(x * 1.27, 1.18, rear + 1 + k * 1.8), 'glow_power', 0.01))
    o.append(box(gs(2.2, 0.12, 0.1), g(0, 0.62, rear - 0.02), 'paint_dark', 0.02))             # under-run bar
    o.append(box(gs(0.5, 0.2, 0.05), g(0, 0.9, rear - 0.03), 'paint_light', 0.01))             # number plate
    o.append(cyl(0.22, 0.5, g(0, 0.85, rear + 3.3), 'steel', 16, (math.radians(90), 0, 0), 0.02))  # air tank
    return front, rear, mid, ln


def trailer_flat():
    reset(); o = []
    front, rear, mid, ln = trailer_base(o)
    o.append(box(gs(2.5, 0.18, ln), g(0, 1.28, mid), 'paint_grey', 0.03))
    for i in range(int(ln / 0.4)):                                                              # wooden deck planks
        o.append(box(gs(2.38, 0.02, 0.36), g(0, 1.38, rear + 0.2 + i * 0.4), 'wood', 0.008))
    for x in (-1, 1):
        o.append(box(gs(0.05, 0.3, ln), g(x * 1.25, 1.42, mid), 'paint_dark', 0.01))
        for k in range(7):                                                                     # stake pockets
            o.append(box(gs(0.07, 0.12, 0.1), g(x * 1.27, 1.25, rear + 0.6 + k * 1.3), 'dark_steel', 0.01))
    o.append(box(gs(2.5, 1.2, 0.1), g(0, 1.95, front - 0.06), 'paint_grey', 0.02))             # headboard
    for k in range(5):
        o.append(box(gs(2.4, 0.04, 0.03), g(0, 1.45 + k * 0.25, front - 0.12), 'dark_steel', 0.005))
    save(P('trailer_flat'), o)


def box_body(o, front, rear, mid, ln, accent):
    o.append(box(gs(2.5, 2.8, ln), g(0, 2.6, mid), 'trailer_white', 0.06, segs=3))
    o.append(box(gs(2.54, 0.1, ln + 0.02), g(0, 1.22, mid), 'galvanised', 0.02))               # bottom rail
    o.append(box(gs(2.54, 0.07, ln + 0.02), g(0, 3.98, mid), 'galvanised', 0.02))              # top rail
    z = rear + 1.2
    while z < front - 0.5:
        for x in (-1, 1):
            o.append(box(gs(0.02, 2.7, 0.05), g(x * 1.255, 2.6, z), 'paint_light', 0.005))   # posts
        z += 1.2
    for x in (-1, 1):
        for y, h, c in accent:
            o.append(box(gs(0.012, h, ln - 0.4), g(x * 1.258, y, mid), c, 0))


def trailer_box():
    reset(); o = []
    front, rear, mid, ln = trailer_base(o)
    box_body(o, front, rear, mid, ln, [(1.5, 0.18, 'accent_power')])
    o.append(box(gs(2.4, 2.6, 0.04), g(0, 2.6, rear - 0.01), 'paint_light', 0.01))             # closed rear doors
    o.append(box(gs(0.02, 2.6, 0.05), g(0, 2.6, rear - 0.03), 'paint_dark', 0))
    for x in (-0.25, 0.25):
        o.append(box(gs(0.04, 2.5, 0.06), g(x, 2.6, rear - 0.05), 'steel', 0.01))             # locking bars
    save(P('trailer_box'), o)


def trailer_removal():
    reset(); o = []
    front, rear, mid, ln = trailer_base(o)
    box_body(o, front, rear, mid, ln, [(1.7, 0.35, 'accent_cooling'), (3.6, 0.2, 'fiber')])
    o.append(box(gs(2.3, 2.6, 0.02), g(0, 2.6, rear - 0.0), 'paint_dark', 0))                  # open, dark inside
    for x in (-1, 1):                                                                          # doors swung open
        hinge = g(x * 1.25, 2.6, rear - 0.02)
        a = math.radians(100) * (1 if x < 0 else -1)
        off = V((-x * 0.62, 0, 0))
        rot = V((off.x * math.cos(a) - off.y * math.sin(a), off.x * math.sin(a) + off.y * math.cos(a), 0))
        o.append(box(gs(1.24, 2.7, 0.04), hinge + rot, 'trailer_white', 0.01, (0, 0, a)))
    save(P('trailer_removal'), o)


def cargo_pallets():
    """Six pallets of bricks and blocks on the flatbed, wrapped in film (node Cargo)."""
    reset(); o = []
    rear = -8.6
    for i in range(6):
        z = rear + 0.9 + i * 1.4
        o.append(box(gs(2.1, 0.14, 1.15), g(0, 1.43, z), 'wood', 0.01))
        m = 'block_stack' if i % 2 == 0 else 'brick_stack'
        for row in range(4):
            o.append(box(gs(1.9, 0.16, 1.05), g(0, 1.58 + row * 0.17, z), m, 0.02))
        o.append(box(gs(1.94, 0.04, 1.09), g(0, 2.27, z), 'wrap', 0.01))
        for x in (-0.6, 0.6):                                                                  # straps
            o.append(box(gs(0.05, 0.9, 1.1), g(x, 1.85, z), 'accent_power', 0.005))
    save(P('cargo_pallets'), o)


def cargo_racks():
    """Three pairs of wrapped racks in the removal trailer (node Cargo)."""
    reset(); o = []
    rear = -8.6
    for i in range(3):
        for x in (-0.55, 0.55):
            z = rear + 0.8 + i * 1.0
            o.append(box(gs(0.8, 1.9, 0.8), g(x, 2.25, z), 'paint_dark', 0.03))
            o.append(box(gs(0.7, 1.75, 0.02), g(x, 2.25, z - 0.41), 'dark_steel', 0.005))
            for u in range(5):
                o.append(box(gs(0.06, 0.03, 0.01), g(x + 0.2, 1.5 + u * 0.35, z - 0.42), 'glow_ok' if u % 2 == 0 else 'glow_cooling', 0))
            o.append(box(gs(0.84, 0.05, 0.84), g(x, 1.3, z), 'rubber', 0.01))                 # dolly
    save(P('cargo_racks'), o)


def pallet():
    """A pallet of building material for the site yard."""
    reset(); o = []
    for x in (-0.38, 0, 0.38):
        o.append(box(gs(0.1, 0.09, 0.9), g(x, 0.045, 0), 'wood', 0.008))
    for z in (-0.4, -0.2, 0, 0.2, 0.4):
        o.append(box(gs(0.9, 0.022, 0.12), g(0, 0.1, z), 'wood', 0.005))
    for row in range(3):
        o.append(box(gs(0.82, 0.17, 0.82), g(0, 0.2 + row * 0.18, 0), 'brick_stack', 0.02))
    o.append(box(gs(0.84, 0.03, 0.84), g(0, 0.73, 0), 'wrap', 0.01))
    save(P('pallet'), o)


# ---------------------------------------------------------------- excavator (one file per joint)

def exc_base():
    reset(); o = []
    for x in (-0.95, 0.95):
        o.append(box(gs(0.36, 0.42, 3.0), g(x, 0.42, 0), 'paint_dark', 0.05))
        for i in range(18):
            z = -1.6 + i * 0.19
            o.append(box(gs(0.6, 0.1, 0.16), g(x, 0.07, z), 'dark_steel', 0.02))
            o.append(box(gs(0.6, 0.04, 0.04), g(x, 0.13, z), 'dark_steel', 0.01))             # grousers
            if 1 < i < 16:
                o.append(box(gs(0.6, 0.08, 0.16), g(x, 0.79, z), 'dark_steel', 0.02))
        for z in (-1.62, 1.62):
            o.append(cyl(0.38, 0.6, g(x, 0.43, z), 'dark_steel', 24, (0, math.radians(90), 0), 0.03))
            o.append(cyl(0.27, 0.64, g(x, 0.43, z), 'paint_dark', 20, (0, math.radians(90), 0), 0.02))
        for i in range(5):
            o.append(cyl(0.12, 0.08, g(x * 1.17, 0.24, -1.1 + i * 0.55), 'paint_grey', 12, (0, math.radians(90), 0), 0.01))
    o.append(box(gs(1.6, 0.3, 1.2), g(0, 0.55, 0), 'paint_dark', 0.04))
    save(P('exc_base'), o)


def exc_top():
    """Upper structure, origin at the turntable (Godot y 0.88 above the ground)."""
    reset(); o = []
    o.append(cyl(0.8, 0.14, g(0, 0.06, 0), 'paint_dark', 28, bevel=0.02))
    o.append(box(gs(2.4, 0.16, 2.9), g(0, 0.22, -0.35), 'paint_dark', 0.03))
    o.append(box(gs(1.7, 0.66, 1.9), g(0.35, 0.62, -0.75), 'exc_orange', 0.08, segs=3))       # engine housing
    o.append(cyl(1.2, 0.75, g(0, 0.6, -1.55), 'exc_orange', 32, bevel=0.08))                  # rounded counterweight
    o[-1].scale = (1, 0.42, 1)
    o.append(box(gs(2.38, 0.75, 0.5), g(0, 0.6, -1.42), 'exc_orange', 0.06))
    o.append(box(gs(2.3, 0.06, 0.5), g(0, 0.26, -1.8), 'paint_dark', 0.01))
    o += louvres(0.9, 0.35, g(1.205, 0.72, -0.75), '+X', m='paint_dark')
    o.append(box(gs(1.6, 0.2, 1.1), g(0.35, 1.05, -1.05), 'paint_dark', 0.04))
    o.append(cyl(0.06, 0.5, g(0.85, 1.35, -0.55), 'steel', 12))
    o.append(tube_path([g(1.15, 0.95, -0.2), g(1.15, 1.55, -0.2), g(1.15, 1.55, -1.4), g(1.15, 0.95, -1.4)], 0.025, 'hazard', 6))
    # cab with big glass, roof, wipers and a front guard
    o.append(box(gs(0.95, 1.3, 1.2), g(-0.68, 0.95, 0.65), 'exc_orange', 0.07, segs=3))
    o.append(box(gs(0.82, 0.95, 0.03), g(-0.68, 1.05, 1.26), 'glass', 0.02))
    o.append(box(gs(0.03, 0.85, 1.0), g(-1.16, 1.1, 0.65), 'glass', 0.02))
    o.append(box(gs(0.03, 0.85, 1.0), g(-0.2, 1.1, 0.65), 'glass', 0.02))
    o.append(box(gs(1.0, 0.06, 1.3), g(-0.68, 1.62, 0.62), 'paint_dark', 0.02))
    for x in (-1.05, -0.31):
        o.append(box(gs(0.05, 1.3, 0.05), g(x, 0.95, 1.25), 'paint_dark', 0.01))
    o.append(box(gs(0.02, 0.5, 0.02), g(-0.68, 1.1, 1.29), 'paint_dark', 0, (0, math.radians(30), 0)))
    o.append(box(gs(0.9, 0.2, 0.06), g(-0.68, 0.38, 1.22), 'paint_dark', 0.01))
    o.append(box(gs(0.16, 0.1, 0.08), g(-0.68, 1.7, 1.2), 'glow_power', 0.02))               # beacon
    o.append(box(gs(0.2, 0.12, 0.06), g(0.9, 1.15, -0.1), 'headlight', 0.02))               # work light
    save(P('exc_top'), o)


def exc_arm(name, length, w, h, extra=None):
    """One arm part along +z from its joint (the origin)."""
    reset(); o = []
    o.append(box(gs(w, h, length), g(0, 0, length / 2), 'exc_orange', min(w, h) * 0.25, segs=3))
    o.append(cyl(h * 0.42, w + 0.08, g(0, 0, 0), 'paint_dark', 16, (0, math.radians(90), 0), 0.01))
    o.append(cyl(0.05, w + 0.14, g(0, 0, 0), 'steel', 10, (0, math.radians(90), 0), 0))
    if extra: extra(o)
    save(P(name), o)


def exc_boom():
    exc_arm('exc_boom', 2.5, 0.42, 0.5)


def exc_knee():
    def ex(o):
        o.append(box(gs(0.01, 0.22, 1.0), g(0.195, 0, 0.9), 'paint_dark', 0))
        o.append(cyl(0.07, 1.4, g(0, 0.33, 0.6), 'paint_dark', 12, (math.radians(90), 0, 0), 0.01))   # stick cylinder
        o.append(cyl(0.045, 0.75, g(0, 0.33, 1.5), 'steel', 10, (math.radians(90), 0, 0), 0))
        o.append(tube_path([g(0.12, 0.22, 0.1), g(0.12, 0.25, 1.0), g(0.12, 0.22, 2.0)], 0.02, 'rubber', 6))
    exc_arm('exc_knee', 2.1, 0.38, 0.44, ex)


def exc_stick():
    def ex(o):
        o.append(cyl(0.06, 1.2, g(0, 0.28, 0.9), 'paint_dark', 12, (math.radians(90), 0, 0), 0.01))
        o.append(cyl(0.04, 0.6, g(0, 0.28, 1.6), 'steel', 10, (math.radians(90), 0, 0), 0))
    exc_arm('exc_stick', 2.1, 0.32, 0.38, ex)


def exc_bucket():
    reset(); o = []
    o.append(box(gs(0.36, 0.3, 0.3), g(0, -0.05, 0.05), 'paint_dark', 0.03))
    o.append(box(gs(0.95, 0.55, 0.6), g(0, -0.25, 0.4), 'bucket_steel', 0.08, segs=3))
    o.append(box(gs(0.9, 0.12, 0.52), g(0, -0.0, 0.45), 'soil', 0.05))
    for i in range(5):
        o.append(box(gs(0.09, 0.1, 0.18), g(-0.36 + i * 0.18, -0.55, 0.72), 'steel', 0.02))
    save(P('exc_bucket'), o)


# ---------------------------------------------------------------- mobile self-erecting crane

def crane_carrier():
    reset(); o = []
    Y, K = 'crane_yellow', 'paint_dark'
    o.append(box(gs(2.5, 0.6, 9.0), g(0, 1.05, 0), Y, 0.08, segs=3))
    o.append(box(gs(1.2, 0.3, 9.0), g(0, 0.8, 0), K, 0.03))
    o.append(box(gs(2.5, 1.3, 1.4), g(0, 1.75, 3.9), Y, 0.15, segs=3))                         # driver's cab
    o.append(box(gs(2.2, 0.7, 0.04), g(0, 1.95, 4.61), 'glass', 0.02))
    for x in (-1, 1):
        o.append(box(gs(0.03, 0.6, 0.9), g(x * 1.25, 1.95, 3.9), 'glass', 0.02))
        o.append(box(gs(0.3, 0.12, 0.05), g(x * 0.85, 0.85, 4.52), 'headlight', 0.02))
    for z in (-4.55, 4.62):                                                                    # hazard-striped bumpers
        o.append(box(gs(2.5, 0.3, 0.06), g(0, 0.8, z), K, 0.02))
        for i in range(6):
            o.append(box(gs(0.18, 0.3, 0.02), g(-1.05 + i * 0.42, 0.8, z + (0.04 if z > 0 else -0.04)), 'hazard', 0))
    for x in (-1, 1):
        o.append(box(gs(0.5, 0.45, 1.6), g(x * 0.9, 1.57, 1.6), 'paint_grey', 0.04))              # tool boxes
        o.append(box(gs(0.04, 0.05, 1.4), g(x * 1.16, 1.75, 1.6), 'steel', 0.01))
        o.append(tube_path([g(x * 1.2, 1.35, -4.2), g(x * 1.2, 1.9, -4.2), g(x * 1.2, 1.9, -0.6), g(x * 1.2, 1.35, -0.6)], 0.025, 'hazard', 6))
        o.append(box(gs(0.16, 0.06, 0.3), g(x * 1.3, 0.55, 3.0), 'steel', 0.01))                   # step
    o.append(box(gs(1.6, 0.5, 1.2), g(0, 1.6, 2.6), 'crane_yellow', 0.08))                         # engine cover
    o += louvres(1.0, 0.3, g(0, 1.6, 1.99), '+Y', m='paint_dark')
    o.append(cyl(0.07, 0.9, g(0.6, 2.2, 2.3), 'steel', 10))
    o.append(box(gs(0.3, 0.12, 0.2), g(0, 2.46, 3.9), 'glow_power', 0.03))                         # beacon
    o.append(box(gs(2.4, 0.03, 9.0), g(0, 1.36, 0), 'dark_steel', 0))                            # deck plate
    for z in (3.4, -1.6, -3.2):
        for x in (-1, 1):
            wheel(o, x * 1.1, z, 0.62, 0.5)
    for x in (-1, 1):
        for z in (-1, 1):
            o.append(box(gs(2.0, 0.22, 0.25), g(x * 2.0, 1.0, z * 3.0), Y, 0.04))
            o.append(cyl(0.1, 0.9, g(x * 2.9, 0.55, z * 3.0), 'steel', 12))
            o.append(box(gs(0.8, 0.12, 0.8), g(x * 2.9, 0.06, z * 3.0), 'wood', 0.02))
    o.append(box(gs(0.05, 0.05, 8.6), g(1.26, 1.1, 0), 'hazard', 0))
    save(P('crane_carrier'), o)


def crane_turret():
    """Turntable, ballast and the bottom of the slewing part (origin at the turntable, node Jib)."""
    reset(); o = []
    o.append(cyl(1.1, 0.2, g(0, 0.1, 0), 'paint_dark', 32, bevel=0.03))
    for k in range(3):
        o.append(box(gs(2.2, 0.28, 1.4), g(0, 0.3 + k * 0.3, -1.2), 'ballast', 0.04))            # ballast blocks
    o.append(box(gs(1.6, 0.4, 1.6), g(0, 0.35, 0.3), 'crane_yellow', 0.06))
    o.append(box(gs(0.6, 0.5, 0.5), g(-0.4, 0.75, 0.5), 'paint_grey', 0.04))                    # winch house
    o.append(cyl(0.18, 0.5, g(-0.4, 0.75, 0.5), 'dark_steel', 16, (0, math.radians(90), 0)))
    save(P('crane_turret'), o)


def lattice_section(name, w, seg, flip):
    """One 'seg' long, w wide square lattice section of the mast (vertical)."""
    reset(); o = []
    Y = 'crane_yellow'
    for x, z in ((-1, -1), (1, -1), (-1, 1), (1, 1)):
        o.append(box(gs(0.09, seg, 0.09), g(x * w / 2, seg / 2, z * w / 2), Y, 0.015))
    for z in (-w / 2, w / 2):
        o.append(box(gs(w, 0.05, 0.05), g(0, 0, z), Y, 0.01))
    for x in (-w / 2, w / 2):
        o.append(box(gs(0.05, 0.05, w), g(x, 0, 0), Y, 0.01))
    d = math.sqrt(seg * seg + w * w); a = math.atan2(seg, w)
    k = -1 if flip else 1
    for z in (-w / 2, w / 2):
        o.append(box(gs(d, 0.04, 0.04), g(0, seg / 2, z), Y, 0, (0, k * a, 0)))
    for x in (-w / 2, w / 2):
        o.append(box(gs(0.04, 0.04, d), g(x, seg / 2, 0), Y, 0, (k * a, 0, 0)))
    save(P(name), o)


def crane_mast():
    lattice_section('crane_mast', 0.9, 1.5, False)


def crane_jib():
    """One 1.2 m section of the triangular jib, running along +z from the origin."""
    reset(); o = []
    Y = 'crane_yellow'; seg = 1.2; h = 0.55
    for x in (-0.3, 0.3):
        o.append(box(gs(0.07, 0.07, seg), g(x, 0, seg / 2), Y, 0.012))
    o.append(box(gs(0.07, 0.07, seg), g(0, h, seg / 2), Y, 0.012))
    o.append(box(gs(0.6, 0.05, 0.05), g(0, 0, 0), Y, 0.01))
    for x in (-0.3, 0.3):
        o.append(tube_path([g(x, 0, 0), g(0, h, seg / 2), g(x, 0, seg)], 0.02, Y, 6))
    save(P('crane_jib'), o)


def crane_cab():
    reset(); o = []
    o.append(box(gs(1.0, 1.3, 1.1), g(0, 0, 0), 'crane_yellow', 0.1, segs=3))
    o.append(box(gs(0.85, 0.85, 0.03), g(0, 0.1, 0.56), 'glass', 0.02))
    o.append(box(gs(0.03, 0.85, 0.9), g(0.51, 0.1, 0), 'glass', 0.02))
    o.append(box(gs(0.03, 0.85, 0.9), g(-0.51, 0.1, 0), 'glass', 0.02))
    o.append(box(gs(1.1, 0.06, 1.2), g(0, 0.68, 0), 'paint_dark', 0.02))
    save(P('crane_cab'), o)


def crane_hook():
    """Trolley-less hook block with a load of material slung below (origin at the top of the block)."""
    reset(); o = []
    o.append(box(gs(0.4, 0.5, 0.3), g(0, -0.25, 0), 'crane_yellow', 0.05))
    o.append(box(gs(0.42, 0.1, 0.02), g(0, -0.25, 0.16), 'paint_dark', 0))
    o.append(torus(0.1, 0.03, g(0, -0.6, 0), 'steel', (0, math.radians(90), 0), 16, 6))
    for x in (-0.55, 0.55):
        for z in (-0.4, 0.4):
            o.append(tube_path([g(0, -0.65, 0), g(x, -1.0, z)], 0.012, 'rubber', 4))
    o.append(box(gs(1.2, 0.1, 0.9), g(0, -1.05, 0), 'wood', 0.01))
    for row in range(3):
        o.append(box(gs(1.1, 0.2, 0.8), g(0, -1.2 - row * 0.2, 0), 'block_stack', 0.02))
    save(P('crane_hook'), o)


def crane_trolley():
    reset(); o = []
    o.append(box(gs(0.7, 0.25, 0.6), g(0, 0, 0), 'paint_dark', 0.04))
    for x in (-0.3, 0.3):
        for z in (-0.2, 0.2):
            o.append(cyl(0.08, 0.05, g(x, 0.12, z), 'steel', 12, (0, math.radians(90), 0), 0))
    save(P('crane_trolley'), o)


if __name__ == '__main__':
    only = set(os.environ.get('ONLY', '').split(',')) - {''}
    for f in (truck_tractor, trailer_flat, trailer_box, trailer_removal, cargo_pallets, cargo_racks, pallet,
              exc_base, exc_top, exc_boom, exc_knee, exc_stick, exc_bucket,
              crane_carrier, crane_turret, crane_mast, crane_jib, crane_cab, crane_hook, crane_trolley):
        if not only or f.__name__ in only:
            f()
