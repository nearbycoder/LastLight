"""Builds Last Light's models in Blender and exports them to Assets/Resources/Models.

    blender -b --threads 4 -P ArtSource/blender/build_assets.py -- [asset ...] [--preview] [--no-export]

Assets: trawler steamer ferry lighthouse rocks buoys coast debris (default: all).
--preview renders review images to ArtSource/_renders/<asset>.png (and a night variant for some).
Each asset is built in a fresh scene and also saved as ArtSource/<asset>.blend.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import ll_lib as L  # noqa: E402

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
preview = "--preview" in argv
export = "--no-export" not in argv
names = [a for a in argv if not a.startswith("--")]


def build(name):
    L.reset_scene()
    if name in ("trawler", "steamer", "ferry"):
        import ships
        root = ships.BUILDERS[name]()
        roots = [root]
        view = dict(target=(0, 0, 2), distance={"trawler": 16, "steamer": 28, "ferry": 23}[name])
    elif name == "lighthouse":
        import lighthouse
        roots = [lighthouse.build()]
        view = dict(target=(0, 0, 11), distance=34, elevation=18)
    elif name == "rocks":
        import rocks
        roots = rocks.build()
        view = dict(target=(0, 0, 2), distance=70, elevation=30)
    elif name == "buoys":
        import buoys
        roots = buoys.build()
        view = dict(target=(0, 0, 2), distance=14, elevation=20)
    elif name == "coast":
        import coast
        roots = coast.build()
        view = dict(target=(-60, -30, 0), distance=260, elevation=52, azimuth=270, lens=35)
    elif name == "debris":
        import debris
        roots = debris.build()
        view = dict(target=(0, 0, 0), distance=12, elevation=35)
    else:
        raise SystemExit(f"unknown asset {name}")

    if preview:
        L.render_preview(name, **view)
    L.save_blend(name)
    if export:
        if name == "buoys":
            for r in roots:
                r.location = (0, 0, 0)
                L.export_fbx(os.path.join(L.MODELS, r.name + ".fbx"), [r])
        else:
            L.export_fbx(os.path.join(L.MODELS, name if name != "coast" else "merrow_bay") + ".fbx", roots)


ALL = ["trawler", "steamer", "ferry", "lighthouse", "rocks", "buoys", "debris", "coast"]
for n in names or ALL:
    print("=== building", n)
    build(n)
