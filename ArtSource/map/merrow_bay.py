"""Merrow Bay: the single coastal map of Last Light.

Source of truth for everything positional: coast, stacks, hidden reefs, shoals, buoys, captains'
routes, the harbour and the wreckers' sites. Writes Assets/Resources/Data/merrow_bay.json, which is
read by the Unity simulation *and* by the Blender scripts that build the coast geometry, so the
art and the gameplay cannot drift apart.

World units: x = east, z = north, sea level y = 0. One unit is roughly ten metres.

    python3 ArtSource/map/merrow_bay.py          # regenerate the JSON
    Tools/.venv/bin/python Tools/chart.py         # render a top-down chart PNG for review
"""
import json
import os

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Assets", "Resources", "Data", "merrow_bay.json")

# The mainland, west to east along the shore, then closed far to the south.
MAINLAND = [
    (-230, -10), (-160, -9), (-145, -6), (-137, -1),
    (-131, 4), (-125, 5), (-120, 0), (-116, -8),          # West Point
    (-113, -15), (-106, -16.5), (-100, -18.5),             # west breakwater, north face
    (-98.5, -20.5),                                        # west breakwater head
    (-105, -20.5), (-110, -23),                            # west breakwater, south face
    (-109, -31), (-103, -39), (-93, -43.5), (-83, -41),    # Porthkell basin
    (-78, -35), (-80.5, -29.5),
    (-85.5, -25.5), (-87.5, -23),                          # east breakwater head
    (-83, -23.5), (-75, -30),
    (-66, -35.5), (-50, -39.5), (-36, -37), (-26, -30),    # Sandy Cove
    (-18, -20), (-13, -10), (-10, -2), (-7, 5), (-3, 9),   # Gannet Head, west flank
    (3, 9), (7, 5), (10, -2), (14, -10), (20, -18),        # Gannet Head, east flank
    (30, -25), (44, -30), (60, -29), (76, -31), (90, -27),
    (100, -23), (106, -27), (110, -36), (116, -34),        # Corley Cove
    (120, -24), (130, -18), (150, -15), (230, -14),
    (230, -130), (-230, -130),
]

# Tall rocks and islets: always visible, always avoided by captains, and they throw beam shadows.
STACKS = [
    dict(id="blackhen", name="Black Hen", x=-50, z=56, r=7.0, h=11),
    dict(id="sentinel_w", name="West Sentinel", x=26, z=-5, r=2.6, h=9),
    dict(id="sentinel_e", name="East Sentinel", x=33, z=-10, r=2.2, h=7),
    dict(id="gullrock", name="Gull Rock", x=82, z=66, r=3.6, h=8),
    dict(id="westneedle", name="West Needle", x=-138, z=13, r=2.4, h=7),
    dict(id="corleyneedle", name="Corley Needle", x=106, z=-4, r=2.2, h=8),
]

# Captains' routes: where they *intend* to sail. Waypoints sit in clear water; the legs between
# them run straight over hidden hazards on purpose. Routes start off-map so ships sail in.
ROUTES = [
    # Into Porthkell
    dict(id="n1_harbor", pts=[(-74, 132), (-72, 84), (-58, 36), (-74, 4), (-93, -22)]),
    dict(id="n2_harbor", pts=[(-6, 132), (-12, 94), (-34, 50), (-56, 24), (-80, 0), (-93, -22)]),
    dict(id="n3_harbor", pts=[(84, 132), (64, 96), (36, 40), (-24, 32), (-64, 16), (-82, -2), (-93, -22)]),
    dict(id="e2_harbor", pts=[(160, 22), (120, 22), (76, 16), (40, 27), (-24, 30), (-64, 16), (-82, -2), (-93, -22)]),
    dict(id="e1_harbor", pts=[(160, 70), (112, 56), (76, 40), (40, 36), (0, 40), (-40, 26), (-70, 6), (-93, -22)]),
    dict(id="w1_harbor", pts=[(-160, 70), (-112, 56), (-88, 32), (-92, 6), (-93, -22)]),
    # Out of Porthkell
    dict(id="harbor_n2", pts=[(-93, -22), (-80, 0), (-52, 28), (-30, 58), (-14, 96), (-8, 132)]),
    dict(id="harbor_e2", pts=[(-93, -22), (-78, 0), (-36, 22), (10, 30), (60, 24), (110, 28), (160, 24)]),
    # Through traffic
    dict(id="e1_w1", pts=[(160, 78), (110, 86), (60, 90), (0, 96), (-60, 92), (-110, 84), (-160, 78)]),
    dict(id="w1_e1", pts=[(-160, 90), (-100, 98), (-40, 100), (20, 92), (80, 92), (120, 84), (160, 78)]),
    dict(id="w2_e2", pts=[(-160, 32), (-112, 36), (-62, 34), (-20, 34), (40, 30), (80, 26), (120, 22), (160, 18)]),
    dict(id="e2_w2", pts=[(160, 14), (120, 18), (80, 22), (40, 32), (-20, 38), (-70, 40), (-112, 40), (-160, 38)]),
    dict(id="n3_e1", pts=[(70, 132), (66, 100), (66, 74), (84, 46), (120, 48), (160, 56)]),
    dict(id="w1_n3", pts=[(-160, 60), (-100, 66), (-50, 82), (0, 94), (60, 106), (80, 132)]),
]
_ROUTE = {r["id"]: r["pts"] for r in ROUTES}


def on_leg(route, leg, t, offset=0.0):
    """A point on a route leg (t along it, offset to the leg's right)."""
    (ax, az), (bx, bz) = _ROUTE[route][leg], _ROUTE[route][leg + 1]
    dx, dz = bx - ax, bz - az
    n = (dx * dx + dz * dz) ** 0.5
    return (round(ax + dx * t + dz / n * offset, 1), round(az + dz * t - dx / n * offset, 1))


# Hidden reefs: awash, invisible in the dark, unknown to captains until charted by the beam.
# Most sit on route legs (that is what makes them dangerous); a few are strays.
REEFS = [
    # The Merrow Teeth: a reef field across the north-east approach.
    ("teeth", *on_leg("n3_harbor", 1, 0.30), 2.6),
    ("teeth", *on_leg("n3_harbor", 1, 0.52, 1.5), 2.8),
    ("teeth", *on_leg("n3_harbor", 1, 0.74, -1.0), 2.4),
    ("teeth", *on_leg("e1_harbor", 2, 0.45), 2.4),
    ("teeth", *on_leg("n3_e1", 2, 0.35), 2.3),
    ("teeth", 60, 58, 2.2), ("teeth", 40, 70, 2.0), ("teeth", 74, 82, 2.2),
    # Widow's Ledge: off the east cliffs, on the coastal runs.
    ("widow", *on_leg("e2_harbor", 1, 0.5), 2.6),
    ("widow", *on_leg("harbor_e2", 4, 0.62), 2.4),
    ("widow", *on_leg("w2_e2", 5, 0.45), 2.2),
    ("widow", *on_leg("e2_w2", 1, 0.5), 2.0),
    # The Hen's Chicks: around Black Hen, across the north-west approaches.
    ("hens", *on_leg("n1_harbor", 1, 0.7), 2.4),
    ("hens", *on_leg("w1_harbor", 1, 0.5), 2.3),
    ("hens", *on_leg("harbor_n2", 2, 0.5), 2.2),
    ("hens", *on_leg("w1_n3", 1, 0.45), 2.2),
    # Gannet's Collar: close under the light, where ships round the Head.
    ("collar", *on_leg("e2_harbor", 3, 0.3), 2.2),
    ("collar", *on_leg("harbor_e2", 2, 0.45), 2.2),
    ("collar", *on_leg("e2_w2", 3, 0.3), 1.9),
    # Outer ground: lonely heads on the through-lanes far out.
    ("outer", *on_leg("n2_harbor", 1, 0.3), 2.4),
    ("outer", *on_leg("e1_w1", 2, 0.5), 2.6),
    ("outer", *on_leg("w1_e1", 1, 0.5), 2.4),
    ("outer", *on_leg("w1_n3", 3, 0.6), 2.4),
]

# Shallow sandbanks: deadly only to deep-draught steamers.
SHOALS = [
    dict(id="longsands", name="Long Sands", x=-64, z=8, rx=21, rz=5.5, angle=-18),
    dict(id="cocklebank", name="Cockle Bank", x=62, z=-12, rx=15, rz=4.5, angle=8),
]

BUOYS = [
    dict(id="sands_n", name="Long Sands North", x=-56, z=19, kind="green"),
    dict(id="sands_s", name="Long Sands South", x=-74, z=-8, kind="red"),
    dict(id="hen_bell", name="Hen Bell", x=-84, z=70, kind="bell"),
    dict(id="teeth_bell", name="Teeth Bell", x=72, z=36, kind="bell"),
    dict(id="fairway", name="Porthkell Fairway", x=-84, z=-10, kind="red"),
    dict(id="widow", name="Widow Buoy", x=82, z=8, kind="green"),
    dict(id="collar", name="Collar Buoy", x=2, z=48, kind="bell"),
]

HARBOR = dict(x=-93, z=-22, r=7.0, dock=[(-93, -30), (-95, -37)])

# Where the wreckers light their false lanterns (cliff tops and the islet).
WRECKER_SITES = [
    dict(id="corley", name="Corley Cove", x=111, z=-30, h=9, aim=[300, 360], hazard=[106, -4]),
    dict(id="blackhen", name="Black Hen", x=-50, z=52, h=12, aim=[300, 60], hazard=[-50, 56]),
    dict(id="westpoint", name="West Point", x=-128, z=0, h=8, aim=[10, 80], hazard=[-138, 13]),
    dict(id="sentinels", name="The Sentinels", x=29, z=-12, h=9, aim=[320, 40], hazard=[26, -5]),
]

DATA = dict(
    name="Merrow Bay",
    lighthouse=[0, 2],
    lensHeight=19.0,
    bounds=[-136, -46, 136, 120],
    land=[dict(name="mainland", pts=[c for p in MAINLAND for c in p])],
    stacks=STACKS,
    reefs=[dict(id=f"{g}{i}", group=g, x=x, z=z, r=r) for i, (g, x, z, r) in enumerate(REEFS)],
    shoals=SHOALS,
    buoys=BUOYS,
    harbor=dict(x=HARBOR["x"], z=HARBOR["z"], r=HARBOR["r"], dock=[c for p in HARBOR["dock"] for c in p]),
    routes=[dict(id=r["id"], pts=[c for p in r["pts"] for c in p]) for r in ROUTES],
    wreckerSites=[dict(w, aim=list(w["aim"]), hazard=list(w["hazard"])) for w in WRECKER_SITES],
)

def _seg_dist(a, b, p):
    ax, az = a; bx, bz = b; px, pz = p
    dx, dz = bx - ax, bz - az
    l2 = dx * dx + dz * dz
    t = max(0.0, min(1.0, ((px - ax) * dx + (pz - az) * dz) / l2)) if l2 > 0 else 0.0
    return ((ax + dx * t - px) ** 2 + (az + dz * t - pz) ** 2) ** 0.5


def validate():
    """Waypoints must sit in clear water; report which hazards each route's legs cross."""
    import math
    problems = 0
    lx, lz = DATA["lighthouse"]
    hazards = [(f"{g}{i}", g, x, z, r) for i, (g, x, z, r) in enumerate(REEFS)]
    for r in ROUTES:
        pts = r["pts"]
        for k, (x, z) in enumerate(pts[1:-1], 1):
            for hid, g, hx, hz, hr in hazards:
                if math.hypot(x - hx, z - hz) < hr + 7:
                    print(f"  ! {r['id']} waypoint {k} ({x},{z}) sits on reef {hid}")
                    problems += 1
            for st in STACKS:
                if math.hypot(x - st["x"], z - st["z"]) < st["r"] + 8:
                    print(f"  ! {r['id']} waypoint {k} ({x},{z}) next to stack {st['id']}")
                    problems += 1
        crossed = set()
        for a, b in zip(pts, pts[1:]):
            for hid, g, hx, hz, hr in hazards:
                if _seg_dist(a, b, (hx, hz)) < hr + 2.5:
                    crossed.add(g)
            for sh in SHOALS:
                if _seg_dist(a, b, (sh["x"], sh["z"])) < sh["rz"] + 3:
                    crossed.add("shoal:" + sh["id"])
            for st in STACKS:
                if _seg_dist(a, b, (st["x"], st["z"])) < st["r"] + 2:
                    crossed.add("STACK:" + st["id"])
        print(f"  {r['id']:12s} crosses {sorted(crossed)}")
    for b in BUOYS:
        # in a beam shadow?
        for st in STACKS:
            dx, dz = st["x"] - lx, st["z"] - lz
            d = math.hypot(dx, dz)
            bx, bz = b["x"] - lx, b["z"] - lz
            bd = math.hypot(bx, bz)
            if bd <= d:
                continue
            ang = abs((math.atan2(bz, bx) - math.atan2(dz, dx) + math.pi) % (2 * math.pi) - math.pi)
            if ang < math.asin(min(1, st["r"] / d)):
                print(f"  ! buoy {b['id']} is in the beam shadow of {st['id']}")
                problems += 1
    return problems


if __name__ == "__main__":
    print("problems:", validate())
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w") as f:
        json.dump(DATA, f, indent=1)
    print("wrote", OUT)
