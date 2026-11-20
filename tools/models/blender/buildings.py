"""Builds window units for the building facades and the warehouse fittings as .glb files (own models, CC0).

    python3 tools/models/blender/buildings.py game/assets/models/made

Windows are centred on their origin on the wall surface and stand out along +z (Godot), so the game can place
them on any face; the glass is the material 'window_glass', which the game swaps for its night-lit window material.
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
    'window_glass': ((0.2, 0.32, 0.43, 1), 0.2, 0.1),
    'frame_white': ((0.92, 0.91, 0.88, 1), 0.0, 0.45),
    'frame_alu': ((0.23, 0.27, 0.32, 1), 0.6, 0.35),
    'stone_sill': ((0.85, 0.82, 0.76, 1), 0.0, 0.8),
    'brick_dark': ((0.5, 0.26, 0.19, 1), 0.0, 0.9),
    'rack_orange': ((1.0, 0.48, 0.18, 1), 0.1, 0.45),
    'rack_blue': ((0.18, 0.37, 0.63, 1), 0.2, 0.4),
    'carton': ((0.78, 0.6, 0.36, 1), 0.0, 0.85),
    'esd_blue': ((0.29, 0.42, 0.53, 1), 0.0, 0.6),
})


def frame(o, w, h, t, d, m, z=0.0):
    """A rectangular frame of bars, t wide and d deep, centred at the origin on the wall."""
    o.append(box(gs(w, t, d), g(0, h / 2 - t / 2, z + d / 2), m, 0.008))
    o.append(box(gs(w, t, d), g(0, -h / 2 + t / 2, z + d / 2), m, 0.008))
    o.append(box(gs(t, h - 2 * t, d), g(-w / 2 + t / 2, 0, z + d / 2), m, 0.008))
    o.append(box(gs(t, h - 2 * t, d), g(w / 2 - t / 2, 0, z + d / 2), m, 0.008))


def window_brick():
    """1.5 x 1.5 m white window in brick: two casements over a fixed transom light, stone sill, soldier-course lintel."""
    reset(); o = []
    W = H = 1.5
    o.append(box(gs(W - 0.06, H - 0.06, 0.02), g(0, 0, 0.01), 'window_glass', 0))
    frame(o, W, H, 0.08, 0.07, 'frame_white')
    o.append(box(gs(W - 0.1, 0.06, 0.06), g(0, 0.32, 0.04), 'frame_white', 0.006))         # transom
    o.append(box(gs(0.06, H * 0.62, 0.06), g(0, -0.13, 0.04), 'frame_white', 0.006))       # mullion
    for x in (-0.36, 0.36):                                                                 # casement sashes
        frame(o, 0.64, 0.92, 0.045, 0.03, 'frame_white', 0.05)
    # move the two sash frames into place, left and right of the mullion, below the transom
    sashes = o[-8:]
    for i, s in enumerate(sashes):
        s.location.x += -0.36 if i < 4 else 0.36
        s.location.z += -0.13
    o.append(box(gs(W + 0.14, 0.06, 0.16), g(0, -H / 2 - 0.03, 0.06), 'stone_sill', 0.01))  # sill
    for k in range(9):                                                                      # brick soldier course
        o.append(box(gs(0.17, 0.22, 0.03), g(-W / 2 - 0.02 + k * 0.193, H / 2 + 0.13, 0.015), 'brick_dark', 0.004))
    save(P('window_brick'), o)


def window_office():
    """1.6 x 1.7 m dark aluminium window with three panes and a narrow vent at the top, for office facades."""
    reset(); o = []
    W, H = 1.6, 1.7
    o.append(box(gs(W - 0.06, H - 0.06, 0.02), g(0, 0, 0.01), 'window_glass', 0))
    frame(o, W, H, 0.07, 0.08, 'frame_alu')
    for x in (-W / 6, W / 6):
        o.append(box(gs(0.05, H - 0.1, 0.06), g(x, 0, 0.04), 'frame_alu', 0.005))
    o.append(box(gs(W - 0.1, 0.05, 0.06), g(0, H / 2 - 0.32, 0.04), 'frame_alu', 0.005))
    o.append(box(gs(W + 0.06, 0.05, 0.14), g(0, -H / 2 - 0.025, 0.06), 'galvanised', 0.008))  # alu sill
    o.append(box(gs(W + 0.1, 0.04, 0.06), g(0, H / 2 + 0.02, 0.03), 'frame_alu', 0.005))
    save(P('window_office'), o)


def pallet_rack():
    """3 x 1 m: orange uprights, blue beams, three levels with pallets of cartons and a few wrapped servers."""
    reset(); o = []
    for x in (-1.45, 0.0, 1.45):
        for z in (-0.4, 0.4):
            o.append(box(gs(0.08, 2.4, 0.06), g(x, 1.2, z), 'rack_orange', 0.01))
            o.append(box(gs(0.16, 0.02, 0.14), g(x, 0.01, z), 'rack_orange', 0.005))       # foot plates
        for y in (0.4, 1.0, 1.6, 2.2):                                                      # frame bracing
            o.append(box(gs(0.04, 0.03, 0.8), g(x, y, 0), 'rack_orange', 0))
        for k in range(4):
            y = 0.25 + k * 0.6
            o.append(box(gs(0.03, 0.03, 0.95), g(x, y + 0.3, 0), 'rack_orange', 0, (math.radians(39), 0, 0)))
    for lvl in range(3):
        y = 0.1 + lvl * 0.8
        for z in (-0.4, 0.4):
            o.append(box(gs(2.95, 0.1, 0.05), g(0, y, z), 'rack_blue', 0.01))
        for x in (-0.72, 0.72):
            o.append(box(gs(1.1, 0.03, 0.75), g(x, y + 0.07, 0), 'wood', 0.005))
            o.append(box(gs(1.1, 0.06, 0.1), g(x, y + 0.11, -0.3), 'wood', 0.005))
            o.append(box(gs(1.1, 0.06, 0.1), g(x, y + 0.11, 0.3), 'wood', 0.005))
            o.append(box(gs(1.1, 0.03, 0.75), g(x, y + 0.15, 0), 'wood', 0.005))
            kind = (lvl * 2 + (x > 0)) % 4
            if kind == 3: continue
            if kind == 2:                                                                   # a wrapped server chassis
                o.append(box(gs(0.95, 0.3, 0.68), g(x, y + 0.32, 0), 'paint_dark', 0.02))
                o.append(box(gs(0.97, 0.32, 0.7), g(x, y + 0.32, 0), 'wrap', 0.02))
                continue
            for i in range(2):
                for j in range(2):
                    h = 0.22 if kind == 0 else 0.3
                    for s in range(2 if kind == 0 else 1):
                        o.append(box(gs(0.5, h, 0.34), g(x - 0.26 + i * 0.52, y + 0.17 + h / 2 + s * h, -0.18 + j * 0.36), 'carton', 0.015))
    save(P('pallet_rack'), o)


def workbench():
    reset(); o = []
    o.append(box(gs(1.9, 0.05, 0.8), g(0, 0.86, 0), 'esd_blue', 0.01))
    for x in (-0.88, 0.88):
        o.append(box(gs(0.06, 0.84, 0.06), g(x, 0.42, -0.34), 'galvanised', 0.01))
        o.append(box(gs(0.06, 0.84, 0.06), g(x, 0.42, 0.34), 'galvanised', 0.01))
        o.append(box(gs(0.06, 0.04, 0.72), g(x, 0.15, 0), 'galvanised', 0.005))
    o.append(box(gs(1.8, 0.03, 0.7), g(0, 0.18, 0), 'galvanised', 0.005))                       # lower shelf
    o.append(box(gs(1.9, 0.6, 0.04), g(0, 1.2, -0.38), 'galvanised', 0.005))                    # tool wall
    for k in range(10):
        o.append(box(gs(0.03, 0.12 + (k % 3) * 0.04, 0.02), g(-0.8 + k * 0.18, 1.25, -0.35), 'dark_steel', 0.004))
    o.append(box(gs(0.9, 0.09, 0.6), g(-0.2, 0.93, 0.05), 'paint_dark', 0.01))                   # server on the bench
    o.append(box(gs(0.86, 0.06, 0.01), g(-0.2, 0.93, 0.355), 'dark_steel', 0))
    o.append(box(gs(0.04, 0.02, 0.01), g(0.05, 0.95, 0.36), 'glow_ok', 0))
    o.append(box(gs(0.5, 0.32, 0.03), g(0.6, 1.12, -0.2), 'paint_dark', 0.01))                   # monitor
    o.append(box(gs(0.46, 0.28, 0.01), g(0.6, 1.12, -0.18), 'screen', 0))
    o.append(box(gs(0.04, 0.2, 0.04), g(0.6, 0.97, -0.22), 'paint_dark', 0.005))
    o.append(box(gs(0.4, 0.05, 0.3), g(0.65, 0.91, 0.2), 'hazard', 0.008))                       # parts tray
    o.append(cyl(0.06, 0.4, g(-0.85, 1.05, 0.25), 'accent_power', 10, (0, 0, math.radians(90)), 0))  # cable reel
    save(P('workbench'), o)


def pallet_jack():
    reset(); o = []
    for x in (-0.16, 0.16):
        o.append(box(gs(0.14, 0.07, 0.85), g(x, 0.06, 0.08), 'rack_orange', 0.01))
        o.append(cyl(0.035, 0.06, g(x, 0.035, 0.45), 'rubber', 10, (0, math.radians(90), 0), 0))
    o.append(box(gs(0.46, 0.26, 0.14), g(0, 0.2, -0.38), 'rack_orange', 0.02))
    o.append(cyl(0.05, 0.3, g(0, 0.4, -0.42), 'steel', 12))                                       # pump
    o.append(cyl(0.08, 0.06, g(0, 0.08, -0.42), 'rubber', 14, (0, math.radians(90), 0), 0))
    o.append(tube_path([g(0, 0.5, -0.44), g(0, 0.95, -0.6), g(0, 1.08, -0.64)], 0.02, 'paint_dark', 6))
    o.append(tube_path([g(-0.14, 1.08, -0.64), g(0, 1.12, -0.66), g(0.14, 1.08, -0.64)], 0.022, 'rubber', 6))
    save(P('pallet_jack'), o)


def shredder():
    reset(); o = []
    o.append(box(gs(0.8, 0.95, 0.8), g(0, 0.5, 0), 'paint_grey', 0.04))
    o.append(box(gs(0.6, 0.18, 0.5), g(0, 1.06, 0.05), 'paint_dark', 0.03))                      # hopper
    o.append(box(gs(0.5, 0.02, 0.2), g(0, 1.16, 0.12), 'rubber', 0))
    o.append(box(gs(0.5, 0.12, 0.01), g(0, 0.75, 0.405), 'accent_power', 0))
    o.append(box(gs(0.2, 0.14, 0.02), g(-0.2, 0.92, 0.41), 'screen', 0.005))
    o.append(box(gs(0.06, 0.06, 0.02), g(0.28, 0.92, 0.41), 'glow_ok', 0))
    o.append(box(gs(0.6, 0.4, 0.02), g(0, 0.3, 0.41), 'paint_light', 0.01))                      # bin door
    o.append(box(gs(0.15, 0.03, 0.03), g(0, 0.45, 0.43), 'steel', 0.005))
    for x in (-0.32, 0.32):
        for z in (-0.32, 0.32):
            o.append(cyl(0.04, 0.05, g(x, 0.025, z), 'rubber', 10, (0, math.radians(90), 0), 0))
    save(P('shredder'), o)


if __name__ == '__main__':
    only = set(os.environ.get('ONLY', '').split(',')) - {''}
    for f in (window_brick, window_office, pallet_rack, workbench, pallet_jack, shredder):
        if not only or f.__name__ in only:
            f()
