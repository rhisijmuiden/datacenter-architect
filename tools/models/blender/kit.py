"""Small modelling kit for hard-surface models: bevelled boxes, cylinders, louvres, fan rotors and grilles.

Coordinates are Blender's (X right, Y depth, Z up). Models face -Y (that becomes +Z, the front, in Godot).
"""
import bpy, bmesh, math
from mathutils import Vector, Matrix
from common import material, join, apply_all, shade_smooth, reset, export, tris

PALETTE = {
    'paint_white': ((0.9, 0.92, 0.93, 1), 0.0, 0.45),
    'paint_light': ((0.78, 0.81, 0.83, 1), 0.0, 0.5),
    'paint_grey': ((0.45, 0.48, 0.5, 1), 0.1, 0.5),
    'paint_dark': ((0.13, 0.145, 0.16, 1), 0.2, 0.45),
    'paint_green': ((0.24, 0.36, 0.3, 1), 0.1, 0.5),
    'steel': ((0.62, 0.64, 0.66, 1), 0.85, 0.35),
    'galvanised': ((0.66, 0.68, 0.68, 1), 0.7, 0.5),
    'dark_steel': ((0.22, 0.23, 0.25, 1), 0.7, 0.4),
    'rubber': ((0.05, 0.05, 0.055, 1), 0.0, 0.85),
    'coil': ((0.38, 0.42, 0.4, 1), 0.6, 0.55),
    'porcelain': ((0.55, 0.33, 0.22, 1), 0.0, 0.25),
    'copper': ((0.8, 0.45, 0.28, 1), 0.9, 0.3),
    'concrete': ((0.62, 0.62, 0.6, 1), 0.0, 0.9),
    'wood': ((0.55, 0.36, 0.2, 1), 0.0, 0.7),
    'hazard': ((0.95, 0.72, 0.1, 1), 0.0, 0.5),
    'accent_power': ((1.0, 0.7, 0.16, 1), 0.0, 0.45),
    'accent_cooling': ((0.33, 0.82, 0.95, 1), 0.0, 0.45),
    'glass': ((0.25, 0.35, 0.42, 1), 0.2, 0.08),
    'screen': ((0.1, 0.14, 0.2, 1), 0.0, 0.2),
    'glow_power': ((1.0, 0.7, 0.16, 1), 0.0, 0.4),
    'glow_cooling': ((0.33, 0.82, 0.95, 1), 0.0, 0.4),
    'glow_fiber': ((1.0, 0.48, 0.18, 1), 0.0, 0.4),
    'glow_ok': ((0.37, 0.83, 0.6, 1), 0.0, 0.4),
    'glow_alert': ((1.0, 0.42, 0.37, 1), 0.0, 0.4),
    'lamp': ((1.0, 0.95, 0.77, 1), 0.0, 0.3),
    'fiber': ((1.0, 0.48, 0.18, 1), 0.0, 0.5),
    'cable_dark': ((0.08, 0.08, 0.09, 1), 0.0, 0.7),
    'red_paint': ((0.75, 0.16, 0.13, 1), 0.1, 0.45),
}


def mat(name):
    c, m, r = PALETTE[name]
    return material(name, c, m, r)


def _finish(ob, m, bevel, segs=2):
    if bevel > 0:
        b = ob.modifiers.new('bv', 'BEVEL'); b.width = bevel; b.segments = segs; b.limit_method = 'ANGLE'
        b.harden_normals = True
        apply_all(ob)
    ob.data.materials.clear(); ob.data.materials.append(mat(m))
    shade_smooth(ob, 40)
    return ob


def box(size, loc, m, bevel=0.015, rot=(0, 0, 0), segs=2):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot)
    ob = bpy.context.view_layer.objects.active
    ob.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return _finish(ob, m, min(bevel, min(size) * 0.45), segs)


def cyl(r, h, loc, m, verts=24, rot=(0, 0, 0), bevel=0.01, r2=None):
    if r2 is None:
        bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=h, location=loc, rotation=rot)
    else:
        bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r, radius2=r2, depth=h, location=loc, rotation=rot)
    ob = bpy.context.view_layer.objects.active
    return _finish(ob, m, min(bevel, r * 0.3, h * 0.3))


def torus(R, r, loc, m, rot=(0, 0, 0), major=24, minor=8):
    bpy.ops.mesh.primitive_torus_add(major_radius=R, minor_radius=r, location=loc, rotation=rot, major_segments=major, minor_segments=minor)
    ob = bpy.context.view_layer.objects.active
    return _finish(ob, m, 0)


def tube_path(points, r, m, segs=8):
    """A round bar along a polyline (handrails, pipes, hoops)."""
    bm = bmesh.new()
    rings = []
    for i, p in enumerate(points):
        d = (points[min(i + 1, len(points) - 1)] - points[max(i - 1, 0)]).normalized()
        a = d.orthogonal().normalized(); b = d.cross(a).normalized()
        rings.append([bm.verts.new(p + (a * math.cos(t) + b * math.sin(t)) * r) for t in (2 * math.pi * k / segs for k in range(segs))])
    for r0, r1 in zip(rings, rings[1:]):
        for k in range(segs):
            bm.faces.new((r0[k], r0[(k + 1) % segs], r1[(k + 1) % segs], r1[k]))
    me = bpy.data.meshes.new('tube'); bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new('tube', me); bpy.context.scene.collection.objects.link(ob)
    ob.data.materials.append(mat(m)); shade_smooth(ob)
    return ob


def louvres(w, h, centre, face='-Y', count=None, m='paint_grey', frame='paint_dark', depth=0.04):
    """A louvred vent on a face: a dark recess with slanted slats."""
    count = count or max(3, int(h / 0.07))
    parts = []
    nx = {'-Y': Vector((0, -1, 0)), '+Y': Vector((0, 1, 0)), '-X': Vector((-1, 0, 0)), '+X': Vector((1, 0, 0))}[face]
    along_x = face in ('-Y', '+Y')
    size = (w, depth, h) if along_x else (depth, w, h)
    parts.append(box(size, centre - nx * depth * 0.3, frame, 0.004))
    for i in range(count):
        z = centre.z - h / 2 + (i + 0.5) * h / count
        s = (w * 0.96, 0.012, h / count * 1.25) if along_x else (0.012, w * 0.96, h / count * 1.25)
        ang = math.radians(35)
        rot = (ang if along_x else 0, 0 if along_x else ang, 0)
        if face in ('-Y', '-X'):
            rot = (-rot[0], -rot[1], 0)
        p = Vector((centre.x, centre.y, z)) + nx * depth * 0.25
        parts.append(box(s, p, m, 0, rot))
    return parts


def fan(r, loc, name='Fan', blades=5, m='dark_steel'):
    """A fan rotor (spins about its local up axis in the game). Returns the object, named Fan*."""
    parts = [cyl(r * 0.18, 0.06, loc, m, 16)]
    for k in range(blades):
        a = 2 * math.pi * k / blades
        c = loc + Vector((math.cos(a) * r * 0.55, math.sin(a) * r * 0.55, 0))
        b = box((r * 0.75, r * 0.26, 0.012), c, m, 0, (math.radians(18), 0, a))
        parts.append(b)
    ob = join(parts, name)
    # pivot at the hub so it spins in place
    bpy.context.view_layer.objects.active = ob
    bpy.context.scene.cursor.location = loc
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    bpy.context.scene.cursor.location = (0, 0, 0)
    return ob


def fan_shroud(r, loc, h=0.25, m='paint_light', grille='dark_steel'):
    """Round fan housing with a grille of rings and spokes on top."""
    parts = []
    bpy.ops.mesh.primitive_cylinder_add(vertices=32, radius=r + 0.04, depth=h, location=loc)
    outer = bpy.context.view_layer.objects.active
    sol = outer.modifiers.new('s', 'SOLIDIFY'); sol.thickness = 0.03
    bm = bmesh.new(); bm.from_mesh(outer.data)
    for f in [f for f in bm.faces if abs(f.normal.z) > 0.9]:
        bm.faces.remove(f)
    bm.to_mesh(outer.data); bm.free()
    apply_all(outer)
    outer.data.materials.append(mat(m)); shade_smooth(outer, 40)
    parts.append(outer)
    top = loc + Vector((0, 0, h / 2))
    for k in (0.35, 0.65, 0.95):
        parts.append(torus(r * k, 0.008, top, grille, major=32, minor=4))
    for k in range(8):
        a = math.pi * k / 8
        parts.append(box((r * 2, 0.012, 0.012), top, grille, 0, (0, 0, a)))
    return parts


def ribs(count, start, step, size, m, bevel=0.003):
    return [box(size, start + step * i, m, bevel) for i in range(count)]


def save(path, objs, keep_separate=()):
    """Joins everything except the named objects (fans keep their own node) and exports."""
    static = [o for o in objs if o.name.split('.')[0] not in keep_separate and not o.name.startswith('Fan')]
    sep = [o for o in objs if o not in static]
    body = join(static, 'Body') if len(static) > 1 else static[0]
    export(path, [body] + sep)
    print(path.split('/')[-1], tris(body) + sum(tris(o) for o in sep), 'tris', len(sep), 'fans')
