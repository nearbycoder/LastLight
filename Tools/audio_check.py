"""Objective checks on every synthesized sound, since nobody has listened to them yet:
peak and clipping, loudness (EBU R128 via ffmpeg), DC offset, clicks at the start and end of
one-shots, and the seam of every looping clip (the jump from the last sample to the first).

    Tools/.venv/bin/python Tools/audio_check.py [--json out.json]

Exits non-zero if anything fails.
"""
import json
import re
import subprocess
import sys
from pathlib import Path

import numpy as np

ROOT = Path(__file__).resolve().parent.parent
AUDIO = ROOT / "Assets" / "Resources" / "Audio"
SR = 48000

# Clips the game plays on loop (Sfx.StartLoop and Music.PlayTrack with loop on).
LOOPS = {"amb_sea", "amb_wind", "amb_rain", "lens_whirr", "lens_focus", "radio_static",
         "music_title", "music_night", "music_tension", "music_dawn"}
# Steady beds whose level should match across the seam (music is allowed its phrasing).
BEDS = {"amb_sea", "amb_wind", "amb_rain", "lens_whirr", "lens_focus", "radio_static"}


def decode(path):
    raw = subprocess.run(["ffmpeg", "-v", "error", "-i", str(path), "-f", "f32le", "-ac", "2", "-ar", str(SR), "-"],
                         capture_output=True, check=True).stdout
    return np.frombuffer(raw, dtype=np.float32).reshape(-1, 2).T.copy()


def loudness(path):
    err = subprocess.run(["ffmpeg", "-v", "info", "-nostats", "-i", str(path), "-af", "ebur128=peak=true", "-f", "null", "-"],
                         capture_output=True, text=True).stderr
    summary = err[err.rfind("Summary:"):]
    i = re.search(r"I:\s+(-?[\d.]+|-inf) LUFS", summary)
    tp = re.search(r"Peak:\s+(-?[\d.]+|-inf) dBFS", summary)
    return (float(i.group(1)) if i and i.group(1) != "-inf" else float("-inf"),
            float(tp.group(1)) if tp and tp.group(1) != "-inf" else float("-inf"))


def db(x):
    return float(20 * np.log10(max(float(x), 1e-9)))


def check(path):
    name = path.stem
    x = decode(path)
    mono = x.mean(axis=0)
    n = x.shape[1]
    peak = float(np.abs(x).max())
    clipped = int((np.abs(x) >= 0.999).sum())
    rms = float(np.sqrt((mono ** 2).mean()))
    dc = float(np.abs(x.mean(axis=1)).max())
    lufs, true_peak = loudness(path)
    r = dict(name=name, seconds=round(n / SR, 2), peak_db=round(db(peak), 1), true_peak_db=round(true_peak, 1),
             lufs=round(lufs, 1), rms_db=round(db(rms), 1), clipped=clipped, dc=round(dc, 4), problems=[])
    if clipped > 0:
        r["problems"].append(f"{clipped} clipped samples")
    if true_peak > -0.3:
        r["problems"].append(f"true peak {true_peak:.1f} dBTP")
    if dc > 0.01:
        r["problems"].append(f"DC offset {dc:.3f}")
    if peak < 1e-3:
        r["problems"].append("silent")
    w = int(0.01 * SR)   # 10 ms
    if name in LOOPS:
        # The seam: the step from the last sample to the first, against the typical sample-to-sample step.
        step = np.abs(np.diff(x, axis=1)).mean()
        seam = float(np.abs(x[:, 0] - x[:, -1]).max())
        r["seam_ratio"] = round(float(seam / max(step, 1e-9)), 1)
        # Level either side of the seam over a second (a loop that dips or swells there pumps audibly).
        head = db(np.sqrt((mono[:w * 100] ** 2).mean()))
        tail = db(np.sqrt((mono[-w * 100:] ** 2).mean()))
        r["seam_level_db"] = round(head - tail, 1)
        if seam > max(8 * step, 0.02):
            r["problems"].append(f"click at the loop seam ({seam:.3f}, {r['seam_ratio']}x a normal step)")
        if name in BEDS and abs(head - tail) > 3:
            r["problems"].append(f"level jumps {head - tail:+.1f} dB across the loop seam")
    else:
        # One-shots should start and end at rest, without a click.
        start, end = float(np.abs(x[:, :3]).max()), float(np.abs(x[:, -3:]).max())
        r["edge"] = round(max(start, end), 4)
        if start > 0.05:
            r["problems"].append(f"starts abruptly ({start:.3f})")
        if end > 0.02:
            r["problems"].append(f"cut off at the end ({end:.3f})")
    return r


def main():
    files = sorted(AUDIO.glob("*.ogg"))
    results = [check(p) for p in files]
    bad = [r for r in results if r["problems"]]
    groups = {"music": [], "amb": [], "voice": [], "other": []}
    for r in results:
        key = next((k for k in ("music", "amb", "voice") if r["name"].startswith(k)), "other")
        groups[key].append(r)
    print(f"{len(results)} clips checked")
    for key, rs in groups.items():
        if not rs:
            continue
        # R128 integrated loudness needs at least 400 ms; shorter ticks report the -70 floor.
        l = [r["lufs"] for r in rs if np.isfinite(r["lufs"]) and r["seconds"] >= 0.4]
        p = [r["true_peak_db"] for r in rs]
        print(f"  {key:6s} {len(rs):3d} clips   loudness {min(l):6.1f} .. {max(l):6.1f} LUFS   true peak max {max(p):5.1f} dBTP")
    for r in results:
        if r["name"] in LOOPS:
            print(f"  loop {r['name']:14s} seam step {r['seam_ratio']:5.1f}x   level across seam {r['seam_level_db']:+.1f} dB")
    for r in bad:
        print(f"FAIL {r['name']}: " + "; ".join(r["problems"]))
    print("PASS all clips" if not bad else f"{len(bad)} clips with problems")
    if "--json" in sys.argv:
        Path(sys.argv[sys.argv.index("--json") + 1]).write_text(json.dumps(results, indent=1))
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
