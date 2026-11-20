"""Shared helpers for the Blender scripts that build the game's own models (run with Blender 4.2+ or the bpy module).

Every model is built at real size in metres, with +Y up in Godot (Blender Z up is converted by the glTF exporter),
standing on the ground at the origin. Materials carry only a name and a base colour; the game swaps in its own
materials by name (for example 'leaves' and 'bark' become the wind-swaying foliage shader).
"""
import bpy, bmesh, math, random
from mathutils import Vector, noise


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def material(name, rgba, metallic=0.0, rough=0.8):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = rgba
    bsdf.inputs['Metallic'].default_value = metallic
    bsdf.inputs['Roughness'].default_value = rough
    return m


def obj_from_bmesh(bm, name, mat=None):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    if mat:
        ob.data.materials.append(mat)
    return ob


def apply_all(ob):
    bpy.context.view_layer.objects.active = ob
    for o in bpy.context.view_layer.objects:
        o.select_set(o == ob)
    for mod in list(ob.modifiers):
        bpy.ops.object.modifier_apply(modifier=mod.name)


def join(objs, name):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    ob = bpy.context.view_layer.objects.active
    ob.name = name
    return ob


def shade_smooth(ob, angle=None):
    for p in ob.data.polygons:
        p.use_smooth = True
    if angle is not None:
        bpy.context.view_layer.objects.active = ob
        ob.select_set(True)
        bpy.ops.object.shade_auto_smooth(angle=math.radians(angle))


def vertex_shade(ob, fn, name='Col'):
    """Per-corner colour from fn(world_co, normal) -> (r, g, b)."""
    me = ob.data
    attr = me.color_attributes.get(name) or me.color_attributes.new(name, 'BYTE_COLOR', 'CORNER')
    me.color_attributes.active_color = attr
    mw = ob.matrix_world
    for poly in me.polygons:
        for li in poly.loop_indices:
            v = me.vertices[me.loops[li].vertex_index]
            r, g, b = fn(mw @ v.co, (mw.to_3x3() @ v.normal).normalized())
            attr.data[li].color = (r, g, b, 1.0)


def export(path, objs=None):
    bpy.ops.object.select_all(action='DESELECT')
    for o in (objs or bpy.context.scene.objects):
        o.select_set(True)
    bpy.ops.export_scene.gltf(filepath=path, export_format='GLB', use_selection=True, export_apply=True,
                              export_vertex_color='ACTIVE', export_yup=True, export_normals=True,
                              export_texcoords=True, export_materials='EXPORT')


def tris(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons)
