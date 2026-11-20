"""Renders a contact sheet of .glb models (Cycles, CPU) to check shapes and shading.

    python3 tools/models/blender/preview.py OUT.png model1.glb model2.glb ...
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(__file__))
from common import reset
import bpy
from mathutils import Vector

out, files = sys.argv[1], sys.argv[2:]
reset()
sc = bpy.context.scene
sc.render.engine = 'CYCLES'; sc.cycles.samples = 24; sc.cycles.device = 'CPU'
sc.render.resolution_x = 360 * min(len(files), 6); sc.render.resolution_y = 360 * math.ceil(len(files) / 6)
x = 0.0; row = 0; spacing = 0
objs = []
for i, f in enumerate(files):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=f)
    new = [o for o in bpy.data.objects if o not in before]
    objs.append(new)
size = max(max(max(o.dimensions) for o in new) for new in objs)
cell = size * 1.25
for i, new in enumerate(objs):
    cx, cy = (i % 6) * cell, -(i // 6) * cell
    for o in new:
        if o.parent is None:
            o.location += Vector((cx, cy, 0))
    for o in new:
        for slot in o.material_slots:
            m = slot.material
            if m and m.use_nodes:
                nt = m.node_tree
                bsdf = nt.nodes.get('Principled BSDF')
                attr = nt.nodes.new('ShaderNodeVertexColor')
                mix = nt.nodes.new('ShaderNodeMix'); mix.data_type = 'RGBA'; mix.blend_type = 'MULTIPLY'; mix.inputs[0].default_value = 1
                base = tuple(bsdf.inputs['Base Color'].default_value)
                mix.inputs[6].default_value = base
                nt.links.new(attr.outputs[0], mix.inputs[7])
                nt.links.new(mix.outputs[2], bsdf.inputs['Base Color'])
cols = min(len(files), 6); rows = math.ceil(len(files) / 6)
cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam'))
sc.collection.objects.link(cam); sc.camera = cam
cam.data.type = 'ORTHO'; cam.data.ortho_scale = cell * max(cols, rows * cols / cols) * 1.0
centre = Vector(((cols - 1) * cell / 2, -(rows - 1) * cell / 2, size * 0.45))
cam.rotation_euler = (math.radians(68), 0, 0)
cam.location = centre + Vector((0, -size * 6, size * 2.4))
sun = bpy.data.objects.new('sun', bpy.data.lights.new('sun', 'SUN')); sc.collection.objects.link(sun)
sun.data.energy = 3.5; sun.rotation_euler = (math.radians(45), math.radians(20), math.radians(-30))
world = bpy.data.worlds.new('w'); sc.world = world; world.use_nodes = True
world.node_tree.nodes['Background'].inputs[0].default_value = (0.7, 0.82, 0.95, 1); world.node_tree.nodes['Background'].inputs[1].default_value = 0.8
bpy.ops.mesh.primitive_plane_add(size=cell * 20, location=(centre.x, centre.y, 0))
sc.render.filepath = out
bpy.ops.render.render(write_still=True)
