"""Plot ship trajectories from a sim run (Tools/eval.sh /tmp/ll_traj.cs) over the chart."""
import sys, json, os
import matplotlib; matplotlib.use("Agg")
import matplotlib.pyplot as plt
sys.argv = [sys.argv[0], "/tmp/_chart_base.png"]
exec(open(os.path.join(os.path.dirname(__file__), "chart.py")).read().replace("fig.savefig(out)", "pass"))
tracks = {}
wrecks = []
charted = []
for line in open("/tmp/ll_traj_out.txt"):
    p = line.split()
    if not p: continue
    if p[0] == "S": tracks.setdefault(p[1], []).append((float(p[2]), float(p[3]), int(p[4])))
    elif p[0] == "W": wrecks.append((float(p[2]), float(p[3])))
    elif p[0] == "C": charted.append((float(p[1]), float(p[2])))
for sid, pts in tracks.items():
    xs = [q[0] for q in pts]; zs = [q[1] for q in pts]
    ax.plot(xs, zs, "-", color="white", lw=1.5, alpha=0.9)
    lost = [(q[0], q[1]) for q in pts if q[2] == 1]
    if lost: ax.plot([q[0] for q in lost], [q[1] for q in lost], ".", color="red", ms=3)
    ax.text(xs[-1], zs[-1], sid, color="white", fontsize=8)
for x, z in wrecks: ax.plot(x, z, "X", color="red", ms=14)
for x, z in charted: ax.plot(x, z, "o", mfc="none", mec="cyan", ms=10)
fig.savefig("/tmp/ll_traj.png")
print("/tmp/ll_traj.png")
