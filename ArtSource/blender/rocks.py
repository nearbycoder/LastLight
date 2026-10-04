"""Sea stacks (one per stack in the map, sized to it) and five hidden-reef variants.

stack_<id>: base at z = -4, waterline z = 0, top at the stack's height. Wet weed at the waterline,
banded rock above, guano-white tops on the bird rocks.
reef_0..4: radius ~2.4, tops near z = +0.6; the game sinks them by 1.25 and scales by r / 2.4.
"""
import json
import math
import os

import bmesh
from mathutils import Vector, noise

from ll_lib import ROOT, empty, from_bmesh, hex_lin, lerp3, vertex_colors, wet

WEED = hex_lin("1D2822")
ROCK = hex_lin("4A4642")
ROCK2 = hex_lin("5E5850")
GUANO = hex_lin("CFCBBE")
GRASS = hex_lin("3A4A30")


def column(name, radius, height, seed, rings=8, segments=12, taper=0.55, lump=0.35, islet=False):
    bm = bmesh.new()
    zs = [-4.0] + [height * (i / (rings - 1)) for i in range(rings)]
    loops = []
    for k, z in enumerate(zs):
        t = max(0.0, z) / max(height, 0.01)
        if islet:
            r = radius * (1.12 - 0.12 * t - 0.3 * t ** 4)
        else:
            r = radius * (1.1 - (1 - taper) * t ** 1.4)
        ring = []
        for i in range(segments):
            a = i / segments * math.tau
            p = Vector((math.cos(a), math.sin(a), 0))
            n = noise.noise(Vector((p.x * 1.7 + seed, p.y * 1.7, z * 0.35 + seed * 3.1)))
            rr = r * (1 + lump * n)
            ring.append(bm.verts.new((p.x * rr, p.y * rr, z)))
        loops.append(ring)
    for k in range(len(loops) - 1):
        a, b = loops[k], loops[k + 1]
        for i in range(segments):
            j = (i + 1) % segments
            bm.faces.new((a[i], a[j], b[j], b[i]))
    top = bm.verts.new((radius * 0.1 * noise.noise(Vector((seed, 1, 2))), 0, height + radius * (0.06 if islet else 0.15)))
    last = loops[-1]
    for i in range(segments):
        bm.faces.new((last[i], last[(i + 1) % segments], top))
    bm.faces.new(list(reversed(loops[0])))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    o = from_bmesh(name, bm, wet("FFFFFF"))
    return o


def paint_stack(o, height, guano=False, grass=False):
    def fn(p, n):
        if p.z < 0.7:
            return WEED
        band = 0.5 + 0.5 * math.sin(p.z * 2.3 + noise.noise(p * 0.4) * 2)
        c = lerp3(ROCK, ROCK2, band * 0.6)
        if n.z > 0.55 and p.z > height * 0.6:
            if grass:
                c = lerp3(GRASS, c, 0.25)
            if guano:
                c = lerp3(c, GUANO, 0.75)
        elif guano and p.z > height * 0.5 and noise.noise(p * 0.9) > 0.15:
            c = lerp3(c, GUANO, 0.55)
        return c
    vertex_colors(o, fn, per_face=True)


def reef(name, seed):
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=2, radius=1.0)
    for v in bm.verts:
        p = v.co.copy()
        n = noise.noise(p * 1.4 + Vector((seed * 2.3, seed, 0)))
        s = 1 + 0.35 * n
        v.co = Vector((p.x * 2.4 * s, p.y * 2.0 * s, p.z * 0.9 * s - 0.25 + max(0, p.z) * 0.3 * noise.noise(p * 3 + Vector((seed, 0, 0)))))
    # A couple of jagged heads.
    o = from_bmesh(name, bm, wet("FFFFFF"))
    vertex_colors(o, lambda p, n: lerp3(WEED, ROCK, max(0.0, min(1.0, (p.z + 0.4) * 0.8))), per_face=True)
    return o


def build():
    data = json.load(open(os.path.join(ROOT, "Assets/Resources/Data/merrow_bay.json")))
    roots = []
    x = 0.0
    for i, s in enumerate(data["stacks"]):
        islet = s["id"] == "blackhen"
        o = column("stack_" + s["id"], s["r"], s["h"], seed=i * 3.7 + 1.3, rings=9 if islet else 8,
                   segments=16 if islet else 11, taper=0.8 if islet else 0.5, lump=0.25 if islet else 0.32, islet=islet)
        paint_stack(o, s["h"], guano=s["id"] in ("blackhen", "gullrock"), grass=islet)
        o.location = (x, 0, 0)
        x += s["r"] * 2 + 6
        roots.append(o)
    for i in range(5):
        o = reef(f"reef_{i}", i * 1.9 + 0.4)
        o.location = (i * 6 - 12, -18, 0)
        roots.append(o)
    # The game places each part at its own map position, so reset offsets for export.
    return roots
