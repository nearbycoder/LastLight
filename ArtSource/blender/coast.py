"""The coast of Merrow Bay, generated from the same map JSON the game uses.

A heightfield driven by the signed distance to the coastline: sheer cliffs along most of the
shore, a low quay round Porthkell's basin, a beach at Sandy Cove, the lighthouse plateau on Gannet
Head at exactly z = 5, rolling heath inland and an underwater shelf (so the sea shader draws
shoreline foam). Plus Porthkell's houses (lit windows), the chapel, both breakwaters, trees and
the wreckers' huts.
"""
import json
import math
import os
import random

import bmesh
import numpy as np
from mathutils import Vector, noise

from ll_lib import (ROOT, box, col, cylinder, empty, from_bmesh, glow, hex_lin, join, lerp3, prism, vertex_colors, wet)

STEP = 1.25
X0, X1 = -250.0, 250.0
Y0, Y1 = -150.0, 22.0


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def signed_distance(px, py, poly):
    """Signed distance (positive inside) from points to a polygon, vectorised."""
    ax = poly[:, 0][None, :]
    ay = poly[:, 1][None, :]
    bx = np.roll(poly[:, 0], -1)[None, :]
    by = np.roll(poly[:, 1], -1)[None, :]
    x = px[:, None]
    y = py[:, None]
    dx, dy = bx - ax, by - ay
    l2 = dx * dx + dy * dy
    t = np.clip(((x - ax) * dx + (y - ay) * dy) / np.where(l2 > 0, l2, 1), 0, 1)
    d = np.sqrt((ax + dx * t - x) ** 2 + (ay + dy * t - y) ** 2).min(axis=1)
    cond = ((ay > y) != (by > y)) & (x < (bx - ax) * (y - ay) / np.where(dy != 0, dy, 1e-9) + ax)
    inside = (cond.sum(axis=1) % 2) == 1
    return np.where(inside, d, -d)


def fbm(x, y, octaves=4):
    s, a, f = 0.0, 0.5, 1.0
    for _ in range(octaves):
        s += a * noise.noise(Vector((x * f, y * f, 0.37 * f)))
        a *= 0.5
        f *= 2.03
    return s


def cliff_height(x, y):
    """Height of the land just behind the shore at (x, y)."""
    h = np.full_like(x, 6.5)
    head = smoothstep(26, 14, np.abs(x)) * smoothstep(-34, -20, y)                    # Gannet Head plateau
    h = h * (1 - head) + 5.0 * head
    harbour = smoothstep(-128, -118, x) * smoothstep(-66, -76, x) * smoothstep(-8, -16, y)
    h = h * (1 - harbour) + 1.7 * harbour
    beach = smoothstep(-66, -58, x) * smoothstep(-24, -32, x) * smoothstep(-26, -34, y)
    h = h * (1 - beach) + 1.0 * beach
    east = smoothstep(40, 70, x)
    h = h + east * 2.5
    corley = smoothstep(96, 104, x) * smoothstep(126, 118, x) * smoothstep(-20, -28, y)
    h = h - corley * 2.0
    return h, beach, harbour, head


def build_terrain(poly):
    xs = np.arange(X0, X1 + 1e-3, STEP)
    ys = np.arange(Y0, Y1 + 1e-3, STEP)
    gx, gy = np.meshgrid(xs, ys)
    px, py = gx.ravel(), gy.ravel()
    sd = signed_distance(px, py, poly)
    ch, beach, harbour, head = cliff_height(px, py)
    width = 1.1 + beach * 7.0 + harbour * -0.4
    land = ch * smoothstep(0.0, np.maximum(width, 0.5), sd)
    hills = np.array([fbm(a * 0.018, b * 0.018) for a, b in zip(px, py)])
    inland = smoothstep(8, 45, sd) * (7.0 + 5.0 * hills) * (1 - head * 0.85) + np.maximum(sd - 12, 0) * 0.06
    town = smoothstep(-128, -116, px) * smoothstep(-64, -74, px) * smoothstep(-30, -42, py)
    inland = inland * (1 - town * 0.55) + town * np.maximum(sd - 2, 0) * 0.22
    h_land = land + inland
    h_sea = -0.9 - np.minimum(-sd, 14) * 0.42
    h = np.where(sd > 0, h_land, h_sea)
    # Keep the lighthouse plateau flat for the tower.
    near = np.hypot(px - 0, py - 2) < 13
    h = np.where(near & (sd > 1.2), 5.0, h)
    keep = sd > -16
    nx, ny = len(xs), len(ys)
    H = h.reshape(ny, nx)
    K = keep.reshape(ny, nx)

    bm = bmesh.new()
    verts = {}
    for j in range(ny):
        for i in range(nx):
            if K[j, i] or (j + 1 < ny and K[j + 1, i]) or (i + 1 < nx and K[j, i + 1]):
                verts[(i, j)] = bm.verts.new((xs[i], ys[j], H[j, i]))
    for j in range(ny - 1):
        for i in range(nx - 1):
            q = [(i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1)]
            if not all(k in verts for k in q):
                continue
            if not any(K[k[1], k[0]] for k in q):
                continue
            a, b, c, d = (verts[k] for k in q)
            # Split along the shorter diagonal so cliff edges stay crisp.
            if abs(H[j, i] - H[j + 1, i + 1]) < abs(H[j, i + 1] - H[j + 1, i]):
                bm.faces.new((a, b, c)); bm.faces.new((a, c, d))
            else:
                bm.faces.new((a, b, d)); bm.faces.new((b, c, d))
    o = from_bmesh("Terrain", bm, col("FFFFFF"))
    return o, (xs, ys, H)


def height_at(field, x, y):
    xs, ys, H = field
    i = int(round((x - xs[0]) / STEP))
    j = int(round((y - ys[0]) / STEP))
    i = max(0, min(len(xs) - 1, i))
    j = max(0, min(len(ys) - 1, j))
    return float(H[j, i])


ROCK = hex_lin("6A645B")
ROCK_DARK = hex_lin("4A4640")
WEED = hex_lin("1E2A26")
SAND = hex_lin("A39270")
GRASS = hex_lin("4A5A3C")
HEATH = hex_lin("64603F")
GRASS_DARK = hex_lin("33402B")
COBBLE = hex_lin("767066")


def paint_terrain(o):
    def fn(p, n):
        if p.z < -0.35:
            return lerp3(WEED, ROCK_DARK, 0.3)
        steep = n.z < 0.6
        if steep:
            band = 0.5 + 0.5 * math.sin(p.z * 1.7 + noise.noise(p * 0.15) * 3)
            c = lerp3(ROCK_DARK, ROCK, band)
            if p.z < 1.2:
                c = lerp3(c, WEED, 0.6)
            return c
        if p.z < 1.4 and -66 < p.x < -26 and p.y < -26:
            return SAND
        if -120 < p.x < -70 and p.y < -20 and p.z < 3.0:
            return COBBLE
        v = noise.noise(Vector((p.x * 0.05, p.y * 0.05, 0.5)))
        c = lerp3(GRASS, HEATH, max(0.0, v) * 1.4)
        c = lerp3(c, GRASS_DARK, max(0.0, -v) * 1.2)
        return c
    vertex_colors(o, fn, per_face=True)


HOUSE_WALLS = ["DDD5C3", "CFC2A8", "B9C2C4", "D9C6A2", "A9B4A0", "E2DDD0", "C9B9A0"]
ROOFS = ["3A3F46", "3A3F46", "44484E", "5A4038"]


def house(name, x, y, z, w, d, hgt, facing, rng, lit, parts, glows, anchors, idx):
    wall = box(name, (w, d, hgt), (x, y, z + hgt / 2), col(rng.choice(HOUSE_WALLS)), bevel=0.03)
    wall.rotation_euler = (0, 0, facing)
    parts.append(wall)
    roof = prism(name + "Roof", [(-w / 2 - 0.25, 0), (w / 2 + 0.25, 0), (0, min(w, d) * 0.55)], d + 0.4,
                 (x, y, z + hgt), col(rng.choice(ROOFS)), axis="y")
    roof.rotation_euler = (0, 0, facing)
    parts.append(roof)
    if rng.random() < 0.6:
        ch = box(name + "Chimney", (0.45, 0.45, 1.2), (x + math.cos(facing) * w * 0.3, y + math.sin(facing) * w * 0.3, z + hgt + 0.6), col("6E665C"))
        parts.append(ch)
    # Front windows face the harbour (local -Y of the facing rotation is the front).
    fx, fy = -math.sin(facing), math.cos(facing)
    for k in range(2 if w > 3 else 1):
        off = (k - (0.5 if w > 3 else 0)) * w * 0.45
        wx = x + math.cos(facing) * off + fx * (d / 2 + 0.02)
        wy = y + math.sin(facing) * off + fy * (d / 2 + 0.02)
        on = lit and rng.random() < 0.75
        win = box(f"{name}Win{k}", (0.55, 0.06, 0.6), (wx, wy, z + hgt * 0.55), glow("FFC172") if on else col("1B1F24"))
        win.rotation_euler = (0, 0, facing)
        glows.append(win)
        if on and rng.random() < 0.25:
            anchors.append(empty(f"lamp_window_{idx}_{k}", (wx + fx * 0.6, wy + fy * 0.6, z + hgt * 0.55)))


def build_town(field, poly, rng):
    parts, glows, anchors = [], [], []
    harbour = Vector((-93, -36))
    idx = 0
    tries = 0
    placed = []
    while idx < 46 and tries < 3000:
        tries += 1
        x = rng.uniform(-126, -66)
        y = rng.uniform(-66, -24)
        sd = signed_distance(np.array([x]), np.array([y]), poly)[0]
        if sd < 3.5 or sd > 34:
            continue
        if any((Vector((x, y)) - q).length < 5.2 for q in placed):
            continue
        z = height_at(field, x, y)
        # Avoid steep spots.
        zs = [height_at(field, x + dx, y + dy) for dx, dy in ((2, 0), (-2, 0), (0, 2), (0, -2))]
        if max(zs) - min(zs) > 2.2:
            continue
        to_h = harbour - Vector((x, y))
        facing = math.atan2(to_h.y, to_h.x) - math.pi / 2
        facing = round(facing / (math.pi / 8)) * (math.pi / 8)
        w = rng.uniform(3.0, 4.6)
        d = rng.uniform(2.6, 3.3)
        hgt = rng.uniform(2.2, 3.2)
        house(f"House{idx}", x, y, min(zs) - 0.2, w, d, hgt, facing, rng, True, parts, glows, anchors, idx)
        placed.append(Vector((x, y)))
        idx += 1
    # The chapel on the rise above the town.
    cx, cy = -104.0, -58.0
    cz = height_at(field, cx, cy) - 0.2
    parts.append(box("Chapel", (4.0, 7.5, 4.0), (cx, cy, cz + 2.0), col("CFC7B4"), bevel=0.04))
    parts.append(prism("ChapelRoof", [(-2.3, 0), (2.3, 0), (0, 2.2)], 7.9, (cx, cy, cz + 4.0), col("3A3F46"), axis="y"))
    parts.append(box("Tower", (2.4, 2.4, 6.5), (cx, cy + 4.6, cz + 3.25), col("CFC7B4")))
    parts.append(cylinder("Spire", 1.6, 0.05, 4.5, (cx, cy + 4.6, cz + 6.5), col("3A3F46"), 4, rot=(0, 0, math.pi / 4)))
    glows.append(box("ChapelWindow", (0.08, 1.2, 1.6), (cx + 2.02, cy, cz + 2.4), glow("FFB45C")))
    anchors.append(empty("lamp_chapel", (cx + 3, cy, cz + 2.5)))
    # Street lamps along the quay.
    for i, (x, y) in enumerate(((-106, -36), (-99, -43), (-88, -44), (-80, -38), (-96, -26), (-89, -28))):
        z = height_at(field, x, y)
        parts.append(cylinder(f"LampPost{i}", 0.09, 0.07, 2.6, (x, y, z), col("26282B"), 6))
        glows.append(box(f"LampHead{i}", (0.35, 0.35, 0.35), (x, y, z + 2.75), glow("FFD08A")))
        anchors.append(empty(f"lamp_street_{i}", (x, y, z + 2.8)))
    town = join(parts, "Town")
    lights = join(glows, "TownWindows")
    return [town, lights], anchors


def breakwater(name, pts, height=2.7, width=2.8):
    segs = []
    for (ax, ay), (bx, by) in zip(pts, pts[1:]):
        dx, dy = bx - ax, by - ay
        length = math.hypot(dx, dy)
        seg = box(f"{name}{len(segs)}", (width, length + width * 0.6, height + 3.0), ((ax + bx) / 2, (ay + by) / 2, height / 2 - 1.5), wet("6A665E"), bevel=0.15)
        seg.rotation_euler = (0, 0, math.atan2(dy, dx) - math.pi / 2)
        segs.append(seg)
    head = cylinder(f"{name}Head", width * 0.75, width * 0.7, height + 3.0, (pts[-1][0], pts[-1][1], -3.0), wet("6A665E"), 12)
    tower = cylinder(f"{name}Light", 0.55, 0.45, 2.0, (pts[-1][0], pts[-1][1], height), col("E3DDD0"), 10)
    return join(segs + [head, tower], name)


def trees(field, poly, rng):
    parts = []
    n = 0
    tries = 0
    while n < 150 and tries < 6000:
        tries += 1
        x = rng.uniform(-240, 240)
        y = rng.uniform(-145, -10)
        sd = signed_distance(np.array([x]), np.array([y]), poly)[0]
        if sd < 14:
            continue
        if -128 < x < -64 and y > -70:
            continue  # leave the town open
        if abs(x) < 30 and y > -40:
            continue  # leave the headland bare
        if noise.noise(Vector((x * 0.02, y * 0.02, 3.3))) < -0.05:
            continue  # clumps, not a carpet
        z = height_at(field, x, y)
        s = rng.uniform(0.8, 1.5)
        if rng.random() < 0.65:
            parts.append(cylinder(f"Pine{n}", 1.6 * s, 0.05, 5.5 * s, (x, y, z + 0.8 * s), col("1E2A1E"), 6))
            parts.append(cylinder(f"Trunk{n}", 0.2 * s, 0.2 * s, 1.0 * s, (x, y, z - 0.1), col("3A2C22"), 4))
        else:
            from ll_lib import sphere
            parts.append(sphere(f"Bush{n}", 1.6 * s, (x, y, z + 1.0 * s), col("26321F"), 1, scale=(1, 1, 0.8)))
        n += 1
    return join(parts, "Trees")


def huts(field, poly, sites):
    parts, anchors = [], []
    for s in sites:
        x, y = s["x"], s["z"]
        z = height_at(field, x, y)
        if z < 1:
            continue
        parts.append(box(f"Hut_{s['id']}", (2.2, 1.8, 1.6), (x + 1.8, y - 1.6, z + 0.8), col("4A3F33"), bevel=0.05))
        parts.append(prism(f"HutRoof_{s['id']}", [(-1.3, 0), (1.3, 0), (0, 0.8)], 2.0, (x + 1.8, y - 1.6, z + 1.6), col("2C2A28"), axis="x"))
        parts.append(cylinder(f"LanternPole_{s['id']}", 0.08, 0.06, 2.2, (x, y, z), col("2C2A28"), 6))
        anchors.append(empty(f"wrecker_{s['id']}", (x, y, z + 2.3)))
    return (join(parts, "Huts") if parts else None), anchors


def build():
    data = json.load(open(os.path.join(ROOT, "Assets/Resources/Data/merrow_bay.json")))
    pts = data["land"][0]["pts"]
    poly = np.array(list(zip(pts[0::2], pts[1::2])), dtype=float)
    rng = random.Random(11)
    root = empty("merrow_bay")
    terrain, field = build_terrain(poly)
    paint_terrain(terrain)
    terrain.parent = root
    town_parts, anchors = build_town(field, poly, rng)
    for p in town_parts:
        p.parent = root
    bw = breakwater("BreakwaterW", [(-113.5, -16.2), (-106, -18.0), (-99.2, -20.2)])
    be = breakwater("BreakwaterE", [(-76.5, -32.5), (-82, -27.0), (-86.8, -23.6)])
    bw.parent = root
    be.parent = root
    tr = trees(field, poly, rng)
    tr.parent = root
    hut, wanchors = huts(field, poly, data["wreckerSites"])
    if hut:
        hut.parent = root
    for a in anchors + wanchors:
        a.parent = root
    return [root]
