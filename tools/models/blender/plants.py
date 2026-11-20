"""Builds the game's trees, shrubs, grass tufts and flowers as .glb files.

    python3 tools/models/blender/plants.py game/assets/models/made   (needs the 'bpy' module, or run inside Blender)

Crowns are clouds of leaf clumps: many spheres around the crown shape, merged with a voxel remesh, roughened with
noise and reduced to a game budget. A baked shade (vertex colour) darkens the inside and underside of a crown, so
the trees read as full and soft even with simple lighting. The game swaps the named materials for its own
wind-swaying foliage shader and recolours them with the seasons.
"""
import os, sys, math, random
sys.path.insert(0, os.path.dirname(__file__))
from common import *
import bpy, bmesh
from mathutils import Vector, noise, Matrix

OUT = sys.argv[-1] if len(sys.argv) > 1 and not sys.argv[-1].endswith('.py') else 'game/assets/models/made'
os.makedirs(OUT, exist_ok=True)


def tube(points, radii, segs, mat, name):
    """A tapered tube along points (list of Vector) with radii."""
    bm = bmesh.new()
    rings = []
    for i, (p, r) in enumerate(zip(points, radii)):
        d = (points[min(i + 1, len(points) - 1)] - points[max(i - 1, 0)]).normalized()
        a = d.orthogonal().normalized(); b = d.cross(a).normalized()
        ring = [bm.verts.new(p + (a * math.cos(t) + b * math.sin(t)) * r) for t in (2 * math.pi * k / segs for k in range(segs))]
        rings.append(ring)
    for r0, r1 in zip(rings, rings[1:]):
        for k in range(segs):
            bm.faces.new((r0[k], r0[(k + 1) % segs], r1[(k + 1) % segs], r1[k]))
    bm.faces.new(list(reversed(rings[0])))
    bm.faces.new(rings[-1])
    return obj_from_bmesh(bm, name, mat)


def branchy_trunk(height, r0, rnd, mat, branches=4, branch_h=0.55, spread=1.6, lean=0.15):
    pts, rad = [], []
    bend = Vector((rnd.uniform(-lean, lean), rnd.uniform(-lean, lean), 0))
    for i in range(7):
        t = i / 6
        pts.append(Vector((0, 0, t * height)) + bend * (t * t) * height * 0.3)
        rad.append(r0 * (1.15 - 0.6 * t) * (1.25 if i == 0 else 1))
    parts = [tube(pts, rad, 8, mat, 'trunk')]
    for k in range(branches):
        ang = 2 * math.pi * k / branches + rnd.uniform(-0.4, 0.4)
        h0 = height * rnd.uniform(branch_h, 0.85)
        start = Vector((0, 0, h0)) + bend * 0.3 * (h0 / height) ** 2 * height
        end = start + Vector((math.cos(ang) * spread, math.sin(ang) * spread, height * rnd.uniform(0.25, 0.45)))
        mid = start.lerp(end, 0.5) + Vector((0, 0, 0.25))
        parts.append(tube([start, mid, end], [r0 * 0.45, r0 * 0.32, r0 * 0.18], 6, mat, 'branch'))
    return join(parts, 'trunk')


def crown(blobs, voxel, disp, freq, seed, target, mat, name='crown'):
    objs = []
    for c, r in blobs:
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=r, location=c)
        objs.append(bpy.context.view_layer.objects.active)
    ob = join(objs, name)
    m = ob.modifiers.new('rm', 'REMESH'); m.mode = 'VOXEL'; m.voxel_size = voxel
    apply_all(ob)
    me = ob.data
    off = Vector((seed * 3.1, seed * 1.7, seed * 2.3))
    for v in me.vertices:
        n = noise.noise(v.co * freq + off) + 0.5 * noise.noise(v.co * freq * 2.7 + off)
        v.co += v.normal * n * disp
    cur = tris(ob)
    if cur > target:
        d = ob.modifiers.new('dc', 'DECIMATE'); d.ratio = target / cur
        apply_all(ob)
    shade_smooth(ob)
    ob.data.materials.clear(); ob.data.materials.append(mat)
    return ob


def shade_crown(ob, z0, z1, centre, radius, dark=0.5):
    def f(co, nrm):
        t = max(0.0, min(1.0, (co.z - z0) / max(z1 - z0, 0.01)))
        flat = Vector((co.x - centre.x, co.y - centre.y, 0))
        out = min(1.0, flat.length / max(radius, 0.01))
        up = max(0.0, nrm.z)
        v = dark + (1 - dark) * (0.45 * t + 0.3 * out + 0.25 * up)
        speck = noise.noise(co * 1.7) * 0.06
        return (min(1, v + speck + 0.03), min(1, v + speck), min(1, v + speck - 0.04))
    vertex_shade(ob, f)


def shade_flat(ob, fn=lambda co, n: (1, 1, 1)):
    vertex_shade(ob, fn)


def cloud_blobs(rnd, centre, rx, ry, rz, count, rmin, rmax):
    out = [(centre, min(rx, ry, rz) * 0.7)]
    for _ in range(count):
        # points on and inside an ellipsoid, biased to the surface so the crown gets clumps
        while True:
            p = Vector((rnd.uniform(-1, 1), rnd.uniform(-1, 1), rnd.uniform(-1, 1)))
            if 0.35 < p.length <= 1:
                break
        p = p.normalized() * (0.65 + 0.35 * p.length)
        out.append((centre + Vector((p.x * rx, p.y * ry, p.z * rz)), rnd.uniform(rmin, rmax)))
    return out


SPECS = {
    'bark': (0.45, 0.33, 0.24, 1), 'bark_birch': (0.9, 0.88, 0.84, 1), 'leaves': (0.36, 0.6, 0.25, 1),
    'leaves2': (0.48, 0.7, 0.3, 1), 'needles': (0.25, 0.48, 0.28, 1), 'bush': (0.3, 0.54, 0.22, 1), 'grass': (0.4, 0.65, 0.26, 1),
}


def M(name):
    """Material by name; created after each reset (a reset clears all materials)."""
    return material(name, SPECS[name])


BARK, BIRCH, LEAVES, LEAVES2, NEEDLES, BUSH, GRASS = 'bark', 'bark_birch', 'leaves', 'leaves2', 'needles', 'bush', 'grass'


def save(name, objs):
    path = os.path.join(OUT, name + '.glb')
    n = sum(tris(o) for o in objs)
    # one object with a surface per material, so Godot builds level-of-detail meshes for the whole model
    objs = [join(objs, name)] if len(objs) > 1 else objs
    export(path, objs)
    print(name, n, 'tris')


def broadleaf(name, seed, height, trunk_h, crown_r, crown_h, r0, leaves=LEAVES, bark=BARK, count=18, rmin=0.9, rmax=1.5,
              voxel=0.32, target=1600, branches=4, birch=False):
    reset()
    rnd = random.Random(seed)
    t = branchy_trunk(trunk_h + crown_h * 0.35, r0, rnd, M(bark), branches, 0.6, crown_r * 0.55)
    if birch:
        shade_flat(t, lambda co, n: (0.25, 0.25, 0.25) if noise.noise(Vector((co.x * 3, co.y * 3, co.z * 6))) > 0.32 else (1, 1, 1))
    else:
        shade_flat(t, lambda co, n: tuple([0.75 + 0.25 * min(1, co.z / 3)] * 3))
    centre = Vector((0, 0, trunk_h + crown_h * 0.5))
    c = crown(cloud_blobs(rnd, centre, crown_r, crown_r * rnd.uniform(0.85, 1.0), crown_h * 0.5, count, rmin, rmax),
              voxel, 0.32, 0.9, seed, target, M(leaves))
    shade_crown(c, trunk_h, trunk_h + crown_h, centre, crown_r)
    save(name, [t, c])


def skirt(bm, z_top, z_bottom, r, tips, rnd, droop):
    """One tier of a conifer: a cone whose rim is a star of drooping branch tips."""
    top = bm.verts.new((0, 0, z_top))
    rim = []
    n = tips * 2
    for k in range(n):
        a = 2 * math.pi * k / n + rnd.uniform(-0.08, 0.08)
        tip = k % 2 == 0
        rr = r * (1.0 if tip else 0.62) * rnd.uniform(0.9, 1.08)
        z = z_bottom - (droop if tip else -droop * 0.3)
        rim.append(bm.verts.new((math.cos(a) * rr, math.sin(a) * rr, z)))
    mid = []
    for k in range(n):
        p = rim[k].co * 0.55 + top.co * 0.45
        p.z += r * 0.08
        mid.append(bm.verts.new(p))
    under = bm.verts.new((0, 0, z_bottom + (z_top - z_bottom) * 0.25))
    for k in range(n):
        k2 = (k + 1) % n
        bm.faces.new((top, mid[k], mid[k2]))
        bm.faces.new((mid[k], rim[k], rim[k2], mid[k2]))
        bm.faces.new((under, rim[k2], rim[k]))


def conifer(name, seed, height, r_base, tiers=6, target=1400):
    reset()
    rnd = random.Random(seed)
    t = tube([Vector((0, 0, 0)), Vector((0, 0, height * 0.5)), Vector((0, 0, height))], [0.24, 0.15, 0.04], 7, M(BARK), 'trunk')
    shade_flat(t, lambda co, n: (0.8, 0.8, 0.8))
    bm = bmesh.new()
    z0 = height * 0.14
    step = (height - z0) / (tiers + 0.6)
    for i in range(tiers):
        f = i / max(tiers - 1, 1)
        r = r_base * (1 - f * 0.78)
        zb = z0 + i * step
        skirt(bm, zb + step * 1.9, zb, r, rnd.randint(7, 9) if f < 0.6 else 6, rnd, step * 0.35)
    c = obj_from_bmesh(bm, 'crown', M(NEEDLES))
    m = c.modifiers.new('sub', 'SUBSURF'); m.levels = 1
    apply_all(c)
    off = Vector((seed, seed * 2, seed * 3))
    for v in c.data.vertices:
        v.co += v.normal * noise.noise(v.co * 2.2 + off) * 0.12
    shade_smooth(c)
    shade_crown(c, z0, height, Vector((0, 0, height * 0.5)), r_base, 0.4)
    save(name, [t, c])


def shrub(name, seed, w, h, count=8, target=500, mat=BUSH):
    reset()
    rnd = random.Random(seed)
    c = crown(cloud_blobs(rnd, Vector((0, 0, h * 0.5)), w * 0.5, w * 0.45, h * 0.45, count, w * 0.18, w * 0.3),
              max(0.08, w * 0.07), w * 0.06, 2.2, seed, target, M(mat))
    # sit it on the ground
    zmin = min(v.co.z for v in c.data.vertices)
    for v in c.data.vertices:
        v.co.z -= zmin + 0.05
    shade_crown(c, 0, h, Vector((0, 0, h * 0.5)), w * 0.5, 0.45)
    save(name, [c])


def tuft(name, seed, blades=14, h=0.45, spread=0.18, mat=GRASS, petals=None):
    reset()
    rnd = random.Random(seed)
    bm = bmesh.new()
    for i in range(blades):
        a = rnd.uniform(0, 2 * math.pi); r = rnd.uniform(0, spread)
        base = Vector((math.cos(a) * r, math.sin(a) * r, 0))
        lean = Vector((math.cos(a), math.sin(a), 0)) * rnd.uniform(0.05, 0.22)
        hh = h * rnd.uniform(0.6, 1.15)
        w = rnd.uniform(0.018, 0.03)
        side = Vector((-math.sin(a + 1.2), math.cos(a + 1.2), 0)) * w
        mid = base + lean * 0.4 + Vector((0, 0, hh * 0.55))
        tip = base + lean + Vector((0, 0, hh))
        v = [bm.verts.new(base - side), bm.verts.new(base + side), bm.verts.new(mid + side * 0.7), bm.verts.new(mid - side * 0.7), bm.verts.new(tip)]
        bm.faces.new((v[0], v[1], v[2], v[3])); bm.faces.new((v[3], v[2], v[4]))
    ob = obj_from_bmesh(bm, name, M(mat))
    shade_flat(ob, lambda co, n: tuple([0.45 + 0.6 * min(1, co.z / h)] * 3))
    objs = [ob]
    if petals:
        pm = material(petals[0], petals[1])
        bm = bmesh.new()
        for i in range(petals[2]):
            a = rnd.uniform(0, 2 * math.pi); r = rnd.uniform(0, spread * 0.8)
            c = Vector((math.cos(a) * r, math.sin(a) * r, h * rnd.uniform(0.85, 1.2)))
            ring = [bm.verts.new(c + Vector((math.cos(t) * 0.05, math.sin(t) * 0.05, 0.01 * math.sin(3 * t)))) for t in (2 * math.pi * k / 6 for k in range(6))]
            bm.faces.new(ring)
        po = obj_from_bmesh(bm, name + '_p', pm)
        shade_flat(po)
        objs.append(po)
    save(name, objs)


def weeping_willow(name, seed):
    """Treurwilg: a short leaning trunk, a broad dome and long curtains of leaves hanging almost to the ground."""
    reset()
    rnd = random.Random(seed)
    t = branchy_trunk(5.0, 0.36, rnd, M(BARK), 5, 0.55, 2.4, 0.3)
    shade_flat(t, lambda co, n: tuple([0.7 + 0.2 * min(1, co.z / 4)] * 3))
    centre = Vector((0, 0, 6.0))
    blobs = cloud_blobs(rnd, centre, 3.4, 3.2, 1.5, 16, 0.9, 1.3)
    c = crown(blobs, 0.3, 0.22, 1.4, seed, 1400, M(LEAVES2))
    shade_crown(c, 4.6, 7.6, centre, 3.4, 0.45)
    strands = []
    for k in range(18):                                                    # thin hanging curtains with gaps between
        a = 2 * math.pi * k / 18 + rnd.uniform(-0.08, 0.08)
        r = rnd.uniform(3.2, 3.7)
        bottom = rnd.uniform(1.2, 2.6)
        z = 5.6
        while z > bottom:
            strands.append((Vector((math.cos(a) * r, math.sin(a) * r, z)), rnd.uniform(0.26, 0.34)))
            r += 0.07; z -= 0.4
    cur = crown(strands, 0.16, 0.08, 2.5, seed + 1, 1400, M(LEAVES2))
    shade_crown(cur, 1.2, 6.0, centre, 3.6, 0.55)
    save(name, [t, c, cur])



def pollard_willow(name, seed):
    """Knotwilg: a thick, short trunk with a knobbly head and a bush of straight young shoots."""
    reset()
    rnd = random.Random(seed)
    pts = [Vector((0, 0, 0)), Vector((0.05, 0, 0.8)), Vector((0.1, 0.05, 1.7)), Vector((0.08, 0.05, 2.2))]
    t = tube(pts, [0.42, 0.34, 0.36, 0.48], 10, M(BARK), 'trunk')
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=0.55, location=(0.08, 0.05, 2.3))
    head = bpy.context.view_layer.objects.active; head.scale = (1, 1, 0.6)
    head.data.materials.append(M(BARK))
    parts = [t, head]
    tips = []
    for k in range(14):
        a = rnd.uniform(0, 2 * math.pi); s = rnd.uniform(0.2, 0.9)
        start = Vector((0.08 + math.cos(a) * 0.3, 0.05 + math.sin(a) * 0.3, 2.4))
        end = start + Vector((math.cos(a) * s, math.sin(a) * s, rnd.uniform(1.6, 2.6)))
        parts.append(tube([start, start.lerp(end, 0.5), end], [0.05, 0.035, 0.015], 5, M(BARK), 'shoot'))
        tips.append(end)
    trunk = join(parts, 'trunk')
    shade_flat(trunk, lambda co, n: (0.62, 0.6, 0.56) if co.z < 2.0 else (0.85, 0.82, 0.78))
    blobs = [(Vector((0.08, 0.05, 4.0)), 1.2)] + [(p - Vector((0, 0, 0.3)), rnd.uniform(0.55, 0.8)) for p in tips]
    c = crown(blobs, 0.22, 0.2, 1.8, seed, 1100, M(LEAVES2))
    shade_crown(c, 2.6, 5.2, Vector((0, 0, 3.9)), 1.6, 0.5)
    save(name, [trunk, c])


def scots_pine(name, seed, height=12):
    """Grove den: a tall bare trunk, warm orange-brown at the top, with a flat, irregular crown of needle clumps."""
    reset()
    rnd = random.Random(seed)
    lean = Vector((rnd.uniform(-0.6, 0.6), rnd.uniform(-0.6, 0.6), 0))
    pts = [Vector((0, 0, 0)) + lean * (i / 6) ** 2 + Vector((0, 0, height * 0.82 * i / 6)) for i in range(7)]
    parts = [tube(pts, [0.3 - 0.03 * i for i in range(7)], 8, M(BARK), 'trunk')]
    top = pts[-1]
    blobs = []
    for k in range(6):                                                      # a few big flat clumps on branches
        a = 2 * math.pi * k / 6 + rnd.uniform(-0.3, 0.3)
        h = top.z - rnd.uniform(0.3, 2.6)
        base = Vector((lean.x * (h / height) ** 2, lean.y * (h / height) ** 2, h))
        tip = base + Vector((math.cos(a) * rnd.uniform(1.4, 2.6), math.sin(a) * rnd.uniform(1.4, 2.6), rnd.uniform(0.3, 0.9)))
        parts.append(tube([base, base.lerp(tip, 0.5) + Vector((0, 0, 0.2)), tip], [0.12, 0.08, 0.04], 6, M(BARK), 'branch'))
        for j in range(4):
            blobs.append((tip + Vector((rnd.uniform(-0.6, 0.6), rnd.uniform(-0.6, 0.6), rnd.uniform(-0.1, 0.3))), rnd.uniform(0.7, 1.0)))
    blobs.append((top + Vector((0, 0, 0.6)), 1.2))
    trunk = join(parts, 'trunk')
    z1 = height
    shade_flat(trunk, lambda co, n: (0.95, 0.62, 0.45) if co.z > z1 * 0.45 else (0.7, 0.62, 0.55))
    crn = []
    for b in blobs:
        crn.append(b)
    c = crown(crn, 0.26, 0.28, 1.3, seed, 1800, M(NEEDLES))
    shade_crown(c, top.z - 3.2, top.z + 1.6, top, 2.4, 0.4)
    save(name, [trunk, c])


def larch(name, seed, height=12, r_base=1.7):
    """Lariks: a slender, open cone with thin tiers of soft light-green needles and a straight trunk."""
    reset()
    rnd = random.Random(seed)
    t = tube([Vector((0, 0, 0)), Vector((0, 0, height * 0.5)), Vector((0, 0, height))], [0.22, 0.13, 0.03], 7, M(BARK), 'trunk')
    shade_flat(t, lambda co, n: (0.75, 0.62, 0.55))
    blobs = []
    tiers = 9
    for i in range(tiers):
        f = i / (tiers - 1)
        z = height * (0.18 + 0.78 * f)
        r = r_base * (1 - f * 0.85)
        for k in range(5 if f < 0.7 else 3):
            a = 2 * math.pi * k / 5 + i * 0.7 + rnd.uniform(-0.2, 0.2)
            blobs.append((Vector((math.cos(a) * r * 0.8, math.sin(a) * r * 0.8, z - r * 0.2)), max(0.28, r * 0.34)))
        blobs.append((Vector((0, 0, z)), max(0.25, r * 0.36)))
    c = crown(blobs, 0.22, 0.18, 1.6, seed, 1500, M(LEAVES2))
    shade_crown(c, height * 0.15, height, Vector((0, 0, height * 0.55)), r_base, 0.45)
    save(name, [t, c])


def more_trees():
    broadleaf('tree_beech', 21, 10, 2.2, 3.8, 6.0, 0.36, count=26, rmin=1.0, rmax=1.6)
    broadleaf('tree_chestnut', 22, 11, 2.6, 3.3, 7.4, 0.38, count=24, rmin=1.1, rmax=1.7, voxel=0.36, branches=5)
    broadleaf('tree_alder', 23, 10, 2.0, 1.9, 7.0, 0.22, count=18, rmin=0.75, rmax=1.15, branches=3, leaves=LEAVES)
    broadleaf('tree_cherry', 24, 6.5, 1.8, 2.6, 3.8, 0.2, count=16, rmin=0.7, rmax=1.1, voxel=0.24, leaves=LEAVES2)
    weeping_willow('tree_willow', 25)
    pollard_willow('tree_pollard', 26)
    scots_pine('tree_pine', 27)
    larch('tree_larch', 28)



def base_set():
    broadleaf('tree_oak', 1, 8.5, 2.4, 3.2, 5.2, 0.32, count=22)
    broadleaf('tree_lime', 2, 10, 2.8, 2.6, 6.8, 0.3, count=22, leaves=LEAVES2)
    broadleaf('tree_maple', 3, 8, 2.2, 3.0, 5.0, 0.28, count=20, rmin=1.0, rmax=1.6)
    broadleaf('tree_birch', 4, 9, 3.0, 2.0, 5.6, 0.18, bark=BIRCH, leaves=LEAVES2, count=16, rmin=0.7, rmax=1.1, voxel=0.26, branches=3, birch=True)
    broadleaf('tree_poplar', 5, 13, 1.8, 1.5, 10.5, 0.26, count=20, rmin=0.8, rmax=1.2, branches=3)
    broadleaf('tree_young', 6, 4.5, 1.6, 1.4, 2.8, 0.1, count=12, rmin=0.45, rmax=0.75, voxel=0.18, target=900, branches=3, leaves=LEAVES2)
    conifer('tree_spruce', 7, 11, 2.7, tiers=7)
    conifer('tree_fir', 8, 8, 2.2, tiers=5)
    shrub('bush_round', 9, 1.4, 1.1)
    shrub('bush_wide', 10, 2.2, 1.0, count=11, target=650)
    shrub('bush_small', 11, 0.8, 0.6, count=6, target=350)
    tuft('grass_tuft', 12)
    tuft('grass_tall', 13, blades=18, h=0.7, spread=0.22)
    tuft('grass_short', 14, blades=10, h=0.28, spread=0.14)
    tuft('flower_red', 15, blades=9, h=0.38, spread=0.12, petals=('petal_red', (0.9, 0.25, 0.2, 1), 4))
    tuft('flower_yellow', 16, blades=9, h=0.34, spread=0.12, petals=('petal_yellow', (0.98, 0.8, 0.2, 1), 5))
    tuft('flower_purple', 17, blades=9, h=0.36, spread=0.12, petals=('petal_purple', (0.6, 0.42, 0.85, 1), 4))
    tuft('flower_white', 18, blades=9, h=0.3, spread=0.12, petals=('petal_white', (0.97, 0.96, 0.92, 1), 6))


if __name__ == '__main__':
    only = set(os.environ.get('ONLY', '').split(',')) - {''}
    for f in (base_set, more_trees):
        if not only or f.__name__ in only:
            f()
