"""Render a top-down chart of Merrow Bay (coast, stacks, reefs, shoals, buoys, routes, beam
shadows) to a PNG, for reviewing the map layout.

    Tools/.venv/bin/python Tools/chart.py [out.png]
"""
import json
import math
import os
import sys

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.patches import Circle, Ellipse, Polygon

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
data = json.load(open(os.path.join(ROOT, "Assets/Resources/Data/merrow_bay.json")))
out = sys.argv[1] if len(sys.argv) > 1 else "/tmp/merrow_chart.png"

fig, ax = plt.subplots(figsize=(16, 10), dpi=100)
ax.set_facecolor("#0d2433")
b = data["bounds"]
ax.set_xlim(b[0] - 15, b[2] + 15)
ax.set_ylim(b[1] - 10, b[3] + 12)
ax.set_aspect("equal")
ax.add_patch(plt.Rectangle((b[0], b[1]), b[2] - b[0], b[3] - b[1], fill=False, ec="#557", ls="--"))

lx, lz = data["lighthouse"]
# beam ranges
for r, c in ((95, "#ffe4a855"), (175, "#ffe4a822")):
    ax.add_patch(Circle((lx, lz), r, fill=False, ec=c, lw=1))
# beam shadows behind stacks
for s in data["stacks"]:
    dx, dz = s["x"] - lx, s["z"] - lz
    d = math.hypot(dx, dz)
    half = math.asin(min(1, s["r"] / d))
    ang = math.atan2(dz, dx)
    far = 260
    pts = [(s["x"], s["z"])]
    for a in (ang - half, ang + half):
        pts.append((lx + math.cos(a) * far, lz + math.sin(a) * far))
    ax.add_patch(Polygon([pts[1], pts[2], (s["x"] + math.cos(ang + half) * 0, s["z"])], closed=True, fc="#00000055", ec="none"))
    ax.add_patch(Polygon([(lx + math.cos(ang - half) * d, lz + math.sin(ang - half) * d), pts[1], pts[2], (lx + math.cos(ang + half) * d, lz + math.sin(ang + half) * d)], closed=True, fc="#00000066", ec="none"))

for poly in data["land"]:
    p = poly["pts"]
    ax.add_patch(Polygon(list(zip(p[0::2], p[1::2])), closed=True, fc="#3a3a2c", ec="#a99", lw=1))
for s in data["shoals"]:
    ax.add_patch(Ellipse((s["x"], s["z"]), 2 * s["rx"], 2 * s["rz"], angle=s["angle"], fc="#c9b27a55", ec="#c9b27a"))
    ax.text(s["x"], s["z"], s["name"], color="#e8d8a8", fontsize=7, ha="center")
for s in data["stacks"]:
    ax.add_patch(Circle((s["x"], s["z"]), s["r"], fc="#6b6b6b", ec="#ddd"))
    ax.text(s["x"], s["z"] + s["r"] + 2, s["name"], color="#ddd", fontsize=7, ha="center")
colors = dict(teeth="#ff6060", widow="#ff9a40", hens="#ff60c0", collar="#ffd040", outer="#a080ff")
for r in data["reefs"]:
    ax.add_patch(Circle((r["x"], r["z"]), r["r"], fc=colors.get(r["group"], "#f00"), ec="white", lw=0.5))
for bu in data["buoys"]:
    c = dict(red="#ff3030", green="#30e070", bell="#ffd060")[bu["kind"]]
    ax.add_patch(Circle((bu["x"], bu["z"]), 20, fill=False, ec=c + "66", ls=":"))
    ax.plot(bu["x"], bu["z"], "^", color=c, ms=8)
    ax.text(bu["x"] + 2, bu["z"] - 4, bu["id"], color=c, fontsize=6)
h = data["harbor"]
ax.add_patch(Circle((h["x"], h["z"]), h["r"], fill=False, ec="#7f7", lw=1.5))
for w in data["wreckerSites"]:
    ax.plot(w["x"], w["z"], "*", color="#ff7a3a", ms=12)
    ax.text(w["x"], w["z"] - 6, w["name"], color="#ff7a3a", fontsize=7, ha="center")
cmap = plt.get_cmap("tab20")
for i, r in enumerate(data["routes"]):
    p = r["pts"]
    xs, zs = p[0::2], p[1::2]
    ax.plot(xs, zs, "-", color=cmap(i % 20), lw=1.2, alpha=0.9)
    ax.annotate("", xy=(xs[1], zs[1]), xytext=(xs[0], zs[0]), arrowprops=dict(arrowstyle="->", color=cmap(i % 20)))
    mx, mz = (xs[1] + xs[2]) / 2, (zs[1] + zs[2]) / 2
    ax.text(mx, mz, r["id"], color=cmap(i % 20), fontsize=6)
ax.plot(lx, lz, "o", color="#ffe4a8", ms=10)
ax.grid(color="#ffffff11")
fig.tight_layout()
fig.savefig(out)
print(out)
