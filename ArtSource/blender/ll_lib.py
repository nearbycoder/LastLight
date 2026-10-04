"""Modelling helpers for Last Light's Blender scripts.

Authoring space is Blender-natural: X = east, Y = north (a ship's bow), Z = up. `export_fbx`
rotates everything 180 degrees about Z and applies it before exporting with the standard FBX
axis settings, which lands models in Unity with x = east, y = up, z = north and identity rotations
(verified with an asymmetric test model).

Materials carry their colour in the name so Unity can rebuild them (MaterialLibrary.FromBlenderName):
    col_RRGGBB   painted / plain       wet_RRGGBB   rock & hulls, darker at the waterline
    metal_RRGGBB metal                 glass_RRGGBB glazing
    glow_RRGGBB  emissive (windows)    lamp_RRGGBB  bright emissive (lenses)
Empties named lamp_* / fx_* are anchors for lights and effects placed by the game.
"""
import math
import os
import random

import bmesh
import bpy
from mathutils import Matrix, Vector, noise

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
MODELS = os.path.join(ROOT, "Assets", "Resources", "Models")
RENDERS = os.path.join(ROOT, "ArtSource", "_renders")


# ----------------------------------------------------------------------------- scene

def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    for coll in (bpy.data.meshes, bpy.data.materials, bpy.data.objects):
        for block in list(coll):
            coll.remove(block)


def link(obj):
    bpy.context.scene.collection.objects.link(obj)
    return obj


def empty(name, loc=(0, 0, 0), parent=None):
    o = bpy.data.objects.new(name, None)
    link(o)
    o.location = loc
    o.parent = parent
    o.empty_display_size = 0.5
    return o


def parent_all(children, parent):
    for c in children:
        c.parent = parent
    return parent


# ----------------------------------------------------------------------------- materials

def _srgb_to_linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def mat(name):
    """Material from a coded name such as col_8A6F4E."""
    m = bpy.data.materials.get(name)
    if m:
        return m
    m = bpy.data.materials.new(name)
    kind, hexstr = name.split("_", 1)
    rgb = [_srgb_to_linear(int(hexstr[i:i + 2], 16) / 255) for i in (0, 2, 4)]
    m.diffuse_color = (*rgb, 1.0)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = (*rgb, 1)
        bsdf.inputs["Roughness"].default_value = {"metal": 0.35, "glass": 0.08, "wet": 0.4}.get(kind, 0.7)
        bsdf.inputs["Metallic"].default_value = 0.8 if kind == "metal" else 0.0
        if kind in ("glow", "lamp"):
            bsdf.inputs["Emission Color"].default_value = (*rgb, 1)
            bsdf.inputs["Emission Strength"].default_value = 3.0 if kind == "glow" else 8.0
    # Vertex colours multiply the base colour in the game; mirror that in preview renders.
    if bsdf and kind in ("col", "wet"):
        attr = m.node_tree.nodes.new("ShaderNodeVertexColor")
        attr.layer_name = "Col"
        mix = m.node_tree.nodes.new("ShaderNodeMix")
        mix.data_type = "RGBA"
        mix.blend_type = "MULTIPLY"
        mix.inputs["Factor"].default_value = 1.0
        mix.inputs[6].default_value = (*rgb, 1)
        m.node_tree.links.new(attr.outputs["Color"], mix.inputs[7])
        m.node_tree.links.new(mix.outputs[2], bsdf.inputs["Base Color"])
    return m


def col(h): return "col_" + h.lstrip("#").upper()
def wet(h): return "wet_" + h.lstrip("#").upper()
def metal(h): return "metal_" + h.lstrip("#").upper()
def glass(h): return "glass_" + h.lstrip("#").upper()
def glow(h): return "glow_" + h.lstrip("#").upper()
def lamp(h): return "lamp_" + h.lstrip("#").upper()


# ----------------------------------------------------------------------------- mesh building

def from_bmesh(name, bm, material, parent=None, smooth=False):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    if isinstance(material, (list, tuple)):
        for m in material:
            me.materials.append(mat(m))
    else:
        me.materials.append(mat(material))
    for p in me.polygons:
        p.use_smooth = smooth
    o = bpy.data.objects.new(name, me)
    link(o)
    o.parent = parent
    return o


def box(name, size, loc=(0, 0, 0), material="col_888888", parent=None, rot=(0, 0, 0), bevel=0.0):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co = Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2]))
    o = from_bmesh(name, bm, material, parent)
    o.location = loc
    o.rotation_euler = rot
    if bevel > 0:
        add_bevel(o, bevel)
    return o


def cylinder(name, r1, r2, h, loc=(0, 0, 0), material="col_888888", segments=16, parent=None, rot=(0, 0, 0), cap=True, bevel=0.0):
    """Cylinder/cone from z = 0 to z = h (base at loc)."""
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=cap, cap_tris=False, segments=segments, radius1=r1, radius2=r2, depth=h)
    for v in bm.verts:
        v.co.z += h / 2
    o = from_bmesh(name, bm, material, parent)
    o.location = loc
    o.rotation_euler = rot
    if bevel > 0:
        add_bevel(o, bevel)
    return o


def sphere(name, r, loc=(0, 0, 0), material="col_888888", subdiv=2, parent=None, scale=(1, 1, 1)):
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=r)
    for v in bm.verts:
        v.co = Vector((v.co.x * scale[0], v.co.y * scale[1], v.co.z * scale[2]))
    o = from_bmesh(name, bm, material, parent)
    o.location = loc
    return o


def prism(name, profile, depth, loc=(0, 0, 0), material="col_888888", parent=None, axis="y", bevel=0.0):
    """Extrude a 2D profile [(a, b), ...] by depth. axis='y': profile in XZ, extruded along Y."""
    bm = bmesh.new()
    verts_a, verts_b = [], []
    for a, b in profile:
        if axis == "y":
            verts_a.append(bm.verts.new((a, -depth / 2, b)))
            verts_b.append(bm.verts.new((a, depth / 2, b)))
        else:  # axis x: profile in YZ
            verts_a.append(bm.verts.new((-depth / 2, a, b)))
            verts_b.append(bm.verts.new((depth / 2, a, b)))
    n = len(profile)
    bm.faces.new(verts_a)
    bm.faces.new(list(reversed(verts_b)))
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((verts_a[i], verts_a[j], verts_b[j], verts_b[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    o = from_bmesh(name, bm, material, parent)
    o.location = loc
    if bevel > 0:
        add_bevel(o, bevel)
    return o


def add_bevel(o, width, segments=1):
    mod = o.modifiers.new("Bevel", "BEVEL")
    mod.width = width
    mod.segments = segments
    mod.limit_method = "ANGLE"
    mod.angle_limit = math.radians(35)
    return mod


def apply_modifiers(o):
    bpy.context.view_layer.objects.active = o
    for m in list(o.modifiers):
        bpy.ops.object.modifier_apply(modifier=m.name)


def join(objs, name):
    objs = [o for o in objs if o is not None]
    for o in objs:
        if o.modifiers:
            apply_modifiers(o)
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    parent = objs[0].parent
    # Bake parents into world space so the join is correct, then restore the first parent.
    for o in objs:
        mw = o.matrix_world.copy()
        o.parent = None
        o.matrix_world = mw
    bpy.ops.object.join()
    j = bpy.context.active_object
    j.name = name
    if parent is not None:
        mw = j.matrix_world.copy()
        j.parent = parent
        j.matrix_world = mw
    return j


def displace(o, amount, scale, seed=0, axis_weights=(1, 1, 1), zmin=None):
    """Noise-displace every vertex (in object space)."""
    me = o.data
    off = Vector((seed * 17.3, seed * 5.1, seed * 9.7))
    for v in me.vertices:
        p = v.co * scale + off
        d = Vector((noise.noise(p), noise.noise(p + Vector((31.4, 0, 0))), noise.noise(p + Vector((0, 27.1, 0)))))
        if zmin is not None and v.co.z < zmin:
            continue
        v.co += Vector((d.x * axis_weights[0], d.y * axis_weights[1], d.z * axis_weights[2])) * amount
    me.update()


def vertex_colors(o, fn, per_face=False):
    """fn(world_co, normal) -> (r, g, b) linear. Stored as the 'Col' corner attribute (FLOAT_COLOR is
    linear in Blender; the FBX exporter writes it as sRGB and LL/Lit decodes it).
    per_face: evaluate once at each face centre (crisp bands) instead of at every corner."""
    me = o.data
    if "Col" not in me.color_attributes:
        me.color_attributes.new("Col", "FLOAT_COLOR", "CORNER")
    attr = me.color_attributes["Col"]
    mw = o.matrix_world
    for poly in me.polygons:
        n = (mw.to_3x3() @ poly.normal).normalized()
        if per_face:
            c = fn(mw @ poly.center, n)
            for li in poly.loop_indices:
                attr.data[li].color = (c[0], c[1], c[2], 1.0)
            continue
        for li in poly.loop_indices:
            vi = me.loops[li].vertex_index
            c = fn(mw @ me.vertices[vi].co, n)
            attr.data[li].color = (c[0], c[1], c[2], 1.0)


def hex_lin(h):
    h = h.lstrip("#")
    return tuple(_srgb_to_linear(int(h[i:i + 2], 16) / 255) for i in (0, 2, 4))


def lerp3(a, b, t):
    return tuple(a[i] + (b[i] - a[i]) * t for i in range(3))


def ensure_colors(objs=None):
    """Every mesh gets a 'Col' layer (white where none was painted), so the game's vertex-colour
    multiply is always defined."""
    for o in (objs or bpy.data.objects):
        if o.type != "MESH":
            continue
        me = o.data
        if "Col" in me.color_attributes:
            continue
        attr = me.color_attributes.new("Col", "FLOAT_COLOR", "CORNER")
        for d in attr.data:
            d.color = (1, 1, 1, 1)


# ----------------------------------------------------------------------------- export

def export_fbx(path, objects):
    """Export the given root objects (and their children) so they import into Unity with
    x = east, y = up, z = north and identity rotations."""
    os.makedirs(os.path.dirname(path), exist_ok=True)
    everything = set()
    for o in objects:
        everything.add(o)
        everything.update(o.children_recursive)
    ensure_colors(list(everything))
    R = Matrix.Rotation(math.pi, 4, "Z")
    for o in objects:
        o.matrix_world = R @ o.matrix_world
    bpy.ops.object.select_all(action="DESELECT")
    for o in everything:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, apply_scale_options="FBX_SCALE_UNITS",
        axis_forward="-Z", axis_up="Y", bake_space_transform=True, object_types={"MESH", "EMPTY"},
        mesh_smooth_type="FACE", use_mesh_modifiers=True, add_leaf_bones=False, bake_anim=False,
        colors_type="SRGB", path_mode="STRIP")
    print("exported", path)


def save_blend(name):
    path = os.path.join(ROOT, "ArtSource", name + ".blend")
    bpy.ops.wm.save_as_mainfile(filepath=path, compress=True)
    print("saved", path)


# ----------------------------------------------------------------------------- preview renders

def render_preview(name, target=(0, 0, 0), distance=20.0, elevation=28.0, azimuth=215.0, size=(900, 700), night=False, lens=50):
    """Quick Eevee render of the current scene for review."""
    os.makedirs(RENDERS, exist_ok=True)
    ensure_colors()
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE_NEXT" if "BLENDER_EEVEE_NEXT" in [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items] else "BLENDER_EEVEE"
    scene.render.resolution_x, scene.render.resolution_y = size
    scene.render.film_transparent = False
    world = bpy.data.worlds.new("W") if not scene.world else scene.world
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    bg.inputs[0].default_value = (0.02, 0.03, 0.05, 1) if night else (0.35, 0.4, 0.48, 1)
    bg.inputs[1].default_value = 1.0
    cam_data = bpy.data.cameras.new("PreviewCam")
    cam_data.lens = lens
    cam = bpy.data.objects.new("PreviewCam", cam_data)
    link(cam)
    t = Vector(target)
    az, el = math.radians(azimuth), math.radians(elevation)
    cam.location = t + Vector((math.cos(az) * math.cos(el), math.sin(az) * math.cos(el), math.sin(el))) * distance
    cam.rotation_euler = (t - cam.location).to_track_quat("-Z", "Y").to_euler()
    scene.camera = cam
    sun_data = bpy.data.lights.new("PreviewSun", "SUN")
    sun_data.energy = 0.6 if night else 3.0
    sun_data.color = (0.7, 0.8, 1.0) if night else (1.0, 0.96, 0.9)
    sun = bpy.data.objects.new("PreviewSun", sun_data)
    link(sun)
    sun.rotation_euler = (math.radians(50), 0, math.radians(azimuth + 40))
    out = os.path.join(RENDERS, name + ".png")
    scene.render.filepath = out
    bpy.ops.render.render(write_still=True)
    for o in (cam, sun):
        bpy.data.objects.remove(o)
    print("rendered", out)
    return out


rng = random.Random(7)
