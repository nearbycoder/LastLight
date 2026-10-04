"""Channel buoys: red can, green cone, and the big bell buoy. Waterline z = 0; lamp_top marks
the lamp."""
import math

from ll_lib import box, col, cylinder, empty, join, metal, sphere, wet


def frame(parts, top, radius=0.55, posts=4, colr="2B2D30"):
    for i in range(posts):
        a = i / posts * math.tau + math.pi / 4
        parts.append(cylinder(f"Post{i}", 0.06, 0.05, top - 1.0, (math.cos(a) * radius, math.sin(a) * radius, 1.0), col(colr), 6))
    parts.append(cylinder("Platform", radius + 0.2, radius + 0.2, 0.1, (0, 0, top), col(colr), 10))
    parts.append(cylinder("LampHousing", 0.22, 0.2, 0.45, (0, 0, top + 0.1), col("1E1F22"), 10))
    parts.append(cylinder("LampGlass", 0.18, 0.18, 0.3, (0, 0, top + 0.55), metal("C9C2B0"), 10))
    parts.append(cylinder("LampCap", 0.24, 0.05, 0.2, (0, 0, top + 0.85), col("1E1F22"), 10))


def red():
    root = empty("buoy_red")
    p = [cylinder("Float", 1.0, 1.0, 0.75, (0, 0, -0.45), wet("2A2A2A"), 14, bevel=0.08)]
    p.append(cylinder("Can", 0.72, 0.72, 1.5, (0, 0, 0.3), col("B3261E"), 14, bevel=0.05))
    p.append(cylinder("Band", 0.74, 0.74, 0.25, (0, 0, 1.2), col("E8E2D4"), 14))
    frame(p, 3.1)
    p.append(cylinder("Topmark", 0.28, 0.28, 0.45, (0, 0, 3.0), col("B3261E"), 10))
    join(p, "Body").parent = root
    empty("lamp_top", (0, 0, 3.75), root)
    return root


def green():
    root = empty("buoy_green")
    p = [cylinder("Float", 1.0, 1.0, 0.75, (0, 0, -0.45), wet("2A2A2A"), 14, bevel=0.08)]
    p.append(cylinder("Cone", 0.85, 0.12, 2.0, (0, 0, 0.3), col("1F6B3A"), 14))
    p.append(cylinder("Band", 0.62, 0.55, 0.25, (0, 0, 1.0), col("E8E2D4"), 14))
    frame(p, 3.1, radius=0.45)
    p.append(cylinder("Topmark", 0.3, 0.02, 0.5, (0, 0, 2.95), col("1F6B3A"), 10))
    join(p, "Body").parent = root
    empty("lamp_top", (0, 0, 3.75), root)
    return root


def bell():
    root = empty("buoy_bell")
    p = [cylinder("Float", 1.35, 1.25, 0.9, (0, 0, -0.5), wet("2A2A2A"), 16, bevel=0.1)]
    p.append(cylinder("Deck", 1.2, 1.2, 0.3, (0, 0, 0.4), col("1E3C46"), 16))
    p.append(cylinder("Skirt", 1.1, 0.9, 0.5, (0, 0, 0.7), col("C7A63A"), 16))
    frame(p, 3.6, radius=0.85, posts=4, colr="1E3C46")
    p.append(cylinder("Bell", 0.5, 0.32, 0.7, (0, 0, 1.75), metal("A8803C"), 14))
    p.append(sphere("BellTop", 0.3, (0, 0, 2.5), metal("A8803C"), 1))
    p.append(box("Beam", (1.9, 0.12, 0.12), (0, 0, 2.6), col("1E3C46")))
    join(p, "Body").parent = root
    empty("lamp_top", (0, 0, 4.25), root)
    return root


def build():
    roots = [red(), green(), bell()]
    for i, r in enumerate(roots):
        r.location = (i * 4 - 4, 0, 0)
    return roots
