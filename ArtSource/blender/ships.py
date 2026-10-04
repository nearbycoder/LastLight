"""The three vessel types: trawler, collier steamer, passenger ferry.

Bow points +Y (north), starboard is +X. The waterline is z = 0. Each ship carries empties:
lamp_mast*, lamp_port, lamp_stbd, lamp_stern, lamp_window* (anchors for lamps and lights) and
fx_smoke / fx_wake (effects).
"""
import math

import bmesh
from mathutils import Vector

from ll_lib import (box, col, cylinder, empty, from_bmesh, glow, hex_lin, join, lamp, metal, prism, sphere, vertex_colors, wet)


def paint_hull(o, bands, deck_hex, deck_z):
    """bands: [(z_top, hex), ...] from the bottom up; deck faces (pointing up, above deck_z) get deck_hex."""
    cols = [(z, hex_lin(h)) for z, h in bands]
    deck = hex_lin(deck_hex)

    def fn(p, n):
        if n.z > 0.8 and p.z > deck_z:
            return deck
        for z, c in cols:
            if p.z <= z:
                return c
        return cols[-1][1]
    vertex_colors(o, fn, per_face=True)


def hull(name, length, beam, freeboard, draft, material, parent, bow_rise=0.5, stern_width=0.75, flare=1.0, stations=14):
    """Lofted hull: stations along Y from stern to bow, rounded V sections, sheer rising to the bow."""
    bm = bmesh.new()
    rings = []
    half = length / 2
    for i in range(stations + 1):
        t = i / stations                       # 0 stern .. 1 bow
        y = -half + length * t
        if t < 0.65:
            w = stern_width + (1 - stern_width) * math.sin(min(1, t / 0.45) * math.pi / 2)
        else:
            u = (t - 0.65) / 0.35
            w = math.cos(u * math.pi / 2) ** 0.85
        w = max(w, 0.02) * beam / 2
        deck = freeboard + bow_rise * max(0.0, t - 0.55) ** 2 * 4 + 0.15 * (1 - t) ** 3
        keel = -draft * (0.55 + 0.45 * math.sin(min(1, t * 1.6) * math.pi / 2)) * (1 - 0.6 * max(0, t - 0.8) / 0.2)
        side = [(1.0, deck), (1.0, deck * 0.8), (1.0, deck * 0.6), (0.99, deck * 0.35), (0.97, deck * 0.15), (0.92, 0.0), (0.62, keel * 0.65)]
        section = [(-w * a * (flare if z == deck else 1.0), z) for a, z in side] + [(0.0, keel)] + [(w * a * (flare if z == deck else 1.0), z) for a, z in reversed(side)]
        rings.append([bm.verts.new((x, y, z)) for x, z in section])
    n = len(rings[0])
    for i in range(len(rings) - 1):
        a, b = rings[i], rings[i + 1]
        for k in range(n - 1):
            bm.faces.new((a[k], a[k + 1], b[k + 1], b[k]))
    # Deck and transom.
    for i in range(len(rings) - 1):
        a, b = rings[i], rings[i + 1]
        bm.faces.new((a[-1], b[-1], b[0], a[0]))
    bm.faces.new(list(reversed(rings[0])))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return from_bmesh(name, bm, material, parent)


def deck_plank(name, length, beam, z, material, parent, y=0.0):
    return box(name, (beam, length, 0.08), (0, y, z), material, parent)


def window_strip(name, size, loc, parent, color="FFC774"):
    return box(name, size, loc, glow(color), parent)


# ---------------------------------------------------------------------------- trawler

def trawler():
    root = empty("trawler")
    L, B = 8.0, 2.7
    h = hull("Hull", L, B, 1.05, 0.9, wet("FFFFFF"), root, bow_rise=0.45, stern_width=0.8)
    paint_hull(h, [(0.05, "6B2A22"), (0.82, "2C4A5C"), (9, "D9D2BF")], "7A6248", 0.9)
    bulwark_rail = box("Rail", (B * 0.9, 0.08, 0.25), (0, -L / 2 + 0.25, 1.25), col("D9D2BF"), root)
    # Wheelhouse aft of midships.
    wh = box("Wheelhouse", (1.55, 1.7, 1.3), (0, -1.0, 1.75), col("E3DCCB"), root, bevel=0.05)
    roof = box("WheelhouseRoof", (1.8, 1.95, 0.14), (0, -1.0, 2.47), col("3A3F44"), root, bevel=0.03)
    wf = window_strip("WindowsFront", (1.2, 0.04, 0.36), (0, -0.13, 2.0), root)
    ws1 = window_strip("WindowsPort", (0.04, 1.1, 0.32), (-0.79, -1.0, 2.0), root)
    ws2 = window_strip("WindowsStbd", (0.04, 1.1, 0.32), (0.79, -1.0, 2.0), root)
    funnel = cylinder("Stack", 0.16, 0.14, 0.9, (0.45, -1.5, 2.5), col("2A2A2A"), 10, root)
    # Foremast with crosstree, and the stern gantry with its trawl.
    mast = cylinder("Mast", 0.09, 0.06, 4.6, (0, 1.7, 1.1), col("4A4038"), 8, root)
    cross = box("Crosstree", (1.1, 0.08, 0.08), (0, 1.7, 4.3), col("4A4038"), root)
    boom = cylinder("Boom", 0.05, 0.05, 2.6, (0, 1.7, 2.0), col("4A4038"), 6, root, rot=(math.radians(-62), 0, 0))
    g1 = cylinder("GantryP", 0.08, 0.08, 2.4, (-0.95, -3.4, 1.1), col("B85A2E"), 6, root, rot=(math.radians(-12), 0, 0))
    g2 = cylinder("GantryS", 0.08, 0.08, 2.4, (0.95, -3.4, 1.1), col("B85A2E"), 6, root, rot=(math.radians(-12), 0, 0))
    g3 = box("GantryTop", (2.0, 0.12, 0.12), (0, -3.9, 3.45), col("B85A2E"), root)
    net = box("Net", (1.6, 0.9, 0.35), (0, -3.0, 1.3), col("5B4A34"), root, bevel=0.12)
    floats = [sphere(f"Float{i}", 0.17, (-0.6 + i * 0.4, -3.0, 1.55), col("D8742C"), 1, root) for i in range(4)]
    body = join([bulwark_rail, roof, funnel, mast, cross, boom, g1, g2, g3, net] + floats, "Body")
    body.parent = root
    h.parent = root
    for w in (wh, wf, ws1, ws2):
        w.parent = root
    empty("lamp_mast", (0, 1.7, 5.75), root)
    empty("lamp_port", (-0.82, -0.35, 2.15), root)
    empty("lamp_stbd", (0.82, -0.35, 2.15), root)
    empty("lamp_stern", (0, -3.9, 3.6), root)
    empty("lamp_cabin", (0, -1.0, 2.0), root)
    empty("fx_smoke", (0.45, -1.5, 3.45), root)
    empty("fx_wake", (0, -4.0, 0.0), root)
    return root


# ---------------------------------------------------------------------------- collier steamer

def steamer():
    root = empty("steamer")
    L, B = 16.0, 4.6
    h = hull("Hull", L, B, 1.6, 1.6, wet("FFFFFF"), root, bow_rise=1.1, stern_width=0.85, stations=20)
    paint_hull(h, [(0.22, "7E2B22"), (9, "1D2024")], "5B4B3C", 1.5)
    poop = box("Poop", (B * 0.66, 1.8, 0.5), (0, -L / 2 + 1.6, 1.85), col("1D2024"), root, bevel=0.05)
    hatches = [box(f"Hatch{i}", (2.2, 1.8, 0.35), (0, y, 1.8), col("3D3328"), root, bevel=0.05) for i, y in enumerate((4.2, 2.0, -3.4))]
    # Bridge house amidships.
    bh = box("BridgeHouse", (3.0, 2.6, 1.6), (0, -0.4, 2.45), col("CFC4AA"), root, bevel=0.05)
    bridge = box("Bridge", (3.6, 1.2, 0.9), (0, 0.4, 3.7), col("CFC4AA"), root, bevel=0.04)
    broof = box("BridgeRoof", (3.9, 1.5, 0.12), (0, 0.4, 4.2), col("3A3A3A"), root)
    bw = window_strip("BridgeWindows", (3.0, 0.04, 0.32), (0, 1.02, 3.8), root)
    bw2 = window_strip("HouseWindowsP", (0.04, 2.0, 0.28), (-1.51, -0.4, 2.6), root)
    bw3 = window_strip("HouseWindowsS", (0.04, 2.0, 0.28), (1.51, -0.4, 2.6), root)
    funnel = cylinder("Funnel", 0.75, 0.7, 3.4, (0, -2.3, 2.6), col("1A1A1A"), 14, root)
    fband = cylinder("FunnelBand", 0.77, 0.76, 0.5, (0, -2.3, 4.6), col("A3322A"), 14, root)
    vent1 = cylinder("Vent1", 0.18, 0.18, 1.0, (1.1, -1.4, 3.2), col("CFC4AA"), 8, root)
    vent2 = cylinder("Vent2", 0.18, 0.18, 1.0, (-1.1, -1.4, 3.2), col("CFC4AA"), 8, root)
    fmast = cylinder("ForeMast", 0.14, 0.09, 7.2, (0, 5.6, 1.6), col("3A322A"), 8, root)
    amast = cylinder("AftMast", 0.14, 0.09, 6.4, (0, -5.4, 1.6), col("3A322A"), 8, root)
    d1 = cylinder("Derrick1", 0.07, 0.07, 3.6, (0, 5.2, 2.2), col("3A322A"), 6, root, rot=(math.radians(-58), 0, 0))
    d2 = cylinder("Derrick2", 0.07, 0.07, 3.6, (0, -5.0, 2.2), col("3A322A"), 6, root, rot=(math.radians(58), 0, 0))
    boat = box("Lifeboat", (0.7, 2.0, 0.45), (1.75, -1.2, 3.45), col("D8D2C4"), root, bevel=0.15)
    boat2 = box("Lifeboat2", (0.7, 2.0, 0.45), (-1.75, -1.2, 3.45), col("D8D2C4"), root, bevel=0.15)
    body = join([poop, *hatches, broof, fmast, amast, d1, d2], "Body")
    body.parent = root
    for o in (h, bh, bridge, bw, bw2, bw3, funnel, fband, vent1, vent2, boat, boat2):
        o.parent = root
    empty("lamp_mast", (0, 5.6, 8.9), root)
    empty("lamp_mast2", (0, -5.4, 8.1), root)
    empty("lamp_port", (-1.85, 0.4, 3.75), root)
    empty("lamp_stbd", (1.85, 0.4, 3.75), root)
    empty("lamp_stern", (0, -7.8, 2.6), root)
    empty("lamp_cabin", (0, 0.4, 3.8), root)
    empty("fx_smoke", (0, -2.3, 6.1), root)
    empty("fx_wake", (0, -8.0, 0.0), root)
    return root


# ---------------------------------------------------------------------------- passenger ferry

def ferry():
    root = empty("ferry")
    L, B = 13.0, 3.9
    h = hull("Hull", L, B, 1.5, 1.2, wet("FFFFFF"), root, bow_rise=0.5, stern_width=0.88, stations=16)
    paint_hull(h, [(0.5, "223552"), (9, "ECE7DB")], "8C7458", 1.4)
    # Two decks of saloons with long rows of windows.
    d1 = box("SaloonLower", (B * 0.82, L * 0.62, 1.2), (0, -0.6, 2.12), col("F1EDE3"), root, bevel=0.06)
    d2 = box("SaloonUpper", (B * 0.68, L * 0.42, 1.0), (0, -0.3, 3.2), col("F1EDE3"), root, bevel=0.06)
    roof = box("SunDeck", (B * 0.74, L * 0.46, 0.1), (0, -0.3, 3.75), col("8C7458"), root)
    wl = [window_strip(f"WinLow{s}", (0.04, L * 0.56, 0.42), (s * (B * 0.41 + 0.01), -0.6, 2.25), root) for s in (-1, 1)]
    wu = [window_strip(f"WinUp{s}", (0.04, L * 0.36, 0.36), (s * (B * 0.34 + 0.01), -0.3, 3.3), root) for s in (-1, 1)]
    wf = window_strip("WinBridge", (B * 0.6, 0.04, 0.34), (0, L * 0.21 - 0.3 + 0.02, 3.35), root)
    funnel = cylinder("Funnel", 0.6, 0.55, 2.0, (0, -2.2, 3.7), col("E8DFC8"), 14, root)
    ftop = cylinder("FunnelTop", 0.57, 0.56, 0.55, (0, -2.2, 5.15), col("1A1A1A"), 14, root)
    fband = cylinder("FunnelBand", 0.58, 0.57, 0.3, (0, -2.2, 4.75), col("B0342B"), 14, root)
    mast = cylinder("Mast", 0.1, 0.07, 4.2, (0, 2.8, 3.7), col("E8DFC8"), 8, root)
    boats = [box(f"Boat{s}{i}", (0.55, 1.6, 0.4), (s * 1.55, -2.6 + i * 2.6, 4.0), col("F2A23A"), root, bevel=0.12) for s in (-1, 1) for i in range(2)]
    flag = box("Flag", (0.04, 0.6, 0.35), (0, -6.3, 3.0), col("B0342B"), root)
    pole = cylinder("FlagPole", 0.04, 0.04, 1.6, (0, -6.0, 1.5), col("E8DFC8"), 6, root)
    body = join([roof, mast, pole], "Body")
    body.parent = root
    for o in (h, d1, d2, *wl, *wu, wf, funnel, ftop, fband, *boats, flag):
        o.parent = root
    empty("lamp_mast", (0, 2.8, 8.0), root)
    empty("lamp_port", (-1.6, 1.6, 3.4), root)
    empty("lamp_stbd", (1.6, 1.6, 3.4), root)
    empty("lamp_stern", (0, -6.3, 2.4), root)
    empty("lamp_cabin", (0, -0.6, 2.4), root)
    empty("lamp_cabin2", (0, -0.3, 3.4), root)
    empty("fx_smoke", (0, -2.2, 5.7), root)
    empty("fx_wake", (0, -6.6, 0.0), root)
    return root


BUILDERS = {"trawler": trawler, "steamer": steamer, "ferry": ferry}
