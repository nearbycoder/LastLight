"""Gannet Head Light: tower, gallery, lantern room, the rotating Fresnel lens and the keeper's
cottage. Origin = the lighthouse's map point at sea level; the headland's cliff top is z = 5 and
the lens centre sits at z = 19 (the map's lensHeight).
"""
import math

from ll_lib import (box, col, cylinder, empty, glass, glow, join, lamp, metal, prism, sphere)

GROUND = 5.0
LENS_Z = 19.0


def build():
    root = empty("lighthouse")
    parts = []
    # Plinth and tower with two red bands.
    parts.append(cylinder("Plinth", 3.3, 3.1, 0.8, (0, 0, GROUND - 0.2), col("8C877C"), 8))
    z0, z1 = GROUND + 0.6, 16.4

    def r_at(z):
        return 2.45 + (1.75 - 2.45) * (z - z0) / (z1 - z0)
    bands = [(z0, 9.0, "ECE8DE"), (9.0, 10.5, "A3302A"), (10.5, 13.0, "ECE8DE"), (13.0, 14.5, "A3302A"), (14.5, z1, "ECE8DE")]
    tower = [cylinder(f"Tower{i}", r_at(a), r_at(b), b - a, (0, 0, a), col(c), 28) for i, (a, b, c) in enumerate(bands)]
    for o in tower:
        o.parent = root
    # Door and windows (lit windows spiral up the south side, facing the camera).
    parts.append(box("Door", (1.0, 0.3, 1.7), (0, -r_at(z0 + 0.8) + 0.05, z0 + 0.85), col("3B2A20")))
    for i, (z, ang) in enumerate(((8.0, 200), (11.6, 160), (15.2, 210))):
        a = math.radians(ang)
        r = r_at(z)
        w = box(f"Window{i}", (0.42, 0.2, 0.7), (math.cos(a) * r, math.sin(a) * r, z), glow("FFC877"), root, rot=(0, 0, a + math.pi / 2))
    # Gallery and railing.
    parts.append(cylinder("Gallery", 2.75, 2.75, 0.25, (0, 0, z1), col("2E3033"), 28))
    parts.append(cylinder("Corbel", 1.8, 2.7, 0.5, (0, 0, z1 - 0.5), col("D9D4C8"), 28))
    for i in range(20):
        a = i / 20 * math.tau
        parts.append(box(f"Post{i}", (0.07, 0.07, 1.0), (math.cos(a) * 2.6, math.sin(a) * 2.6, z1 + 0.75), col("2E3033")))
    ring = cylinder("Rail", 2.65, 2.65, 0.07, (0, 0, z1 + 1.22), col("2E3033"), 28, cap=False)
    parts.append(ring)
    # Lantern room: a dark base wall, glazing, astragals, a copper roof and ventilator ball.
    parts.append(cylinder("LanternBase", 1.6, 1.6, 0.75, (0, 0, z1 + 0.25), col("2A2C30"), 16))
    glazing = cylinder("Glazing", 1.5, 1.5, 2.55, (0, 0, z1 + 1.0), glass("C8B88A"), 16)
    glazing.parent = root
    for i in range(12):
        a = i / 12 * math.tau
        parts.append(box(f"Astragal{i}", (0.07, 0.07, 2.6), (math.cos(a) * 1.52, math.sin(a) * 1.52, z1 + 2.3), col("26282B")))
    parts.append(cylinder("Cornice", 1.75, 1.7, 0.18, (0, 0, z1 + 3.55), col("26282B"), 16))
    parts.append(cylinder("Roof", 1.75, 0.25, 1.35, (0, 0, z1 + 3.73), col("3D5A4A"), 16))
    parts.append(sphere("Ventilator", 0.3, (0, 0, z1 + 5.25), col("2A2C30"), 1))
    parts.append(cylinder("Rod", 0.04, 0.02, 1.2, (0, 0, z1 + 5.5), col("2A2C30"), 4))
    body = join(parts, "Tower")
    body.parent = root

    # The lens: a brass-framed beehive with two bullseye panels; rotates in the game.
    lens = empty("Lens", (0, 0, LENS_Z), root)
    lp = []
    lp.append(cylinder("LensBarrel", 0.95, 0.95, 1.9, (0, 0, -0.95), lamp("FFE2A6"), 12))
    for z in (-1.0, -0.33, 0.33, 1.0):
        lp.append(cylinder(f"LensRing{z}", 1.0, 1.0, 0.08, (0, 0, z - 0.04), metal("9A7B3E"), 12, cap=False))
    lp.append(cylinder("LensCap", 0.7, 0.3, 0.35, (0, 0, 0.95), metal("9A7B3E"), 12))
    lp.append(cylinder("LensPedestal", 0.35, 0.5, 1.2, (0, 0, -2.2), metal("6D5A35"), 10))
    for side in (1, -1):
        lp.append(cylinder(f"Bullseye{side}", 0.62, 0.62, 0.12, (0, side * 0.95, 0.0), lamp("FFF4D8"), 16, rot=(math.radians(90), 0, 0)))
    lens_mesh = join(lp, "LensMesh")
    lens_mesh.parent = lens
    lens_mesh.location = (0, 0, 0)

    # Keeper's cottage and oil store, nestled behind the tower.
    cot = []
    cx, cy = -6.5, -5.0
    cot.append(box("CottageWalls", (5.4, 3.6, 2.6), (cx, cy, GROUND + 1.3), col("DCD5C4"), bevel=0.04))
    roof = prism("CottageRoof", [(-3.0, 0), (3.0, 0), (0, 1.8)], 4.0, (cx, cy, GROUND + 2.6), col("3B4047"), axis="x")
    roof.rotation_euler = (0, 0, math.radians(90))
    cot.append(roof)
    cot.append(box("Chimney", (0.6, 0.6, 1.4), (cx - 1.8, cy, GROUND + 3.7), col("8C877C")))
    cot.append(box("Store", (2.4, 2.4, 1.8), (cx + 3.7, cy - 0.4, GROUND + 0.9), col("CFC7B4"), bevel=0.04))
    cot.append(prism("StoreRoof", [(-1.4, 0), (1.4, 0), (0, 0.8)], 2.6, (cx + 3.7, cy - 0.4, GROUND + 1.8), col("3B4047"), axis="x"))
    cottage = join(cot, "Cottage")
    cottage.parent = root
    for i, x in enumerate((-1.5, 0.3, 1.8)):
        box(f"CottageWindow{i}", (0.6, 0.12, 0.7), (cx + x, cy + 1.82, GROUND + 1.5), glow("FFC877"), root)
    box("CottageWindowE", (0.12, 0.6, 0.7), (cx - 2.72, cy, GROUND + 1.5), glow("FFC877"), root)
    # A low stone wall around the light.
    wall = []
    for i in range(26):
        a = i / 26 * math.tau
        if 0.55 < a / math.tau < 0.72:
            continue
        wall.append(box(f"Wall{i}", (1.4, 0.45, 0.7), (math.cos(a) * 8.5 - 2, math.sin(a) * 6.5 - 2.5, GROUND + 0.3), col("7B776E"), rot=(0, 0, a + math.pi / 2)))
    walls = join(wall, "Walls")
    walls.parent = root
    empty("lamp_cottage", (cx, cy + 2.6, GROUND + 1.6), root)
    empty("lamp_door", (0, -2.9, GROUND + 2.2), root)
    return root
