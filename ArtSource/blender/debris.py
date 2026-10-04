"""Wreckage floating after a ship strikes: planks, a crate, a barrel and a lifeboat."""
import math

from ll_lib import box, col, cylinder, empty, join, wet


def build():
    roots = []
    plank = box("debris_plank", (0.35, 2.6, 0.12), (0, 0, 0.05), col("6E5539"), bevel=0.03)
    roots.append(plank)
    crate = box("debris_crate", (0.9, 0.9, 0.9), (2, 0, 0.2), col("8A6B42"), bevel=0.06)
    roots.append(crate)
    barrel = cylinder("debris_barrel", 0.45, 0.45, 1.1, (4, 0, -0.4), col("5A3D26"), 10, rot=(math.radians(90), 0, 0), bevel=0.05)
    roots.append(barrel)
    boat = empty("debris_boat", (6, 0, 0))
    hull = box("BoatHull", (1.1, 2.4, 0.5), (0, 0, 0.15), col("E39A3A"), boat, bevel=0.25)
    seat = box("BoatSeat", (1.0, 0.25, 0.08), (0, 0.2, 0.35), col("6E5539"), boat)
    j = join([hull, seat], "BoatBody")
    j.parent = boat
    j.location = (0, 0, 0.15)
    empty("lamp_boat", (0, 0.6, 1.0), boat)
    roots.append(boat)
    return roots
