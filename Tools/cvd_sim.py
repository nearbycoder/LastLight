"""Colour-vision check for screenshots: crops shown as seen with normal vision, deuteranopia and
protanopia (Machado, Oliveira & Fernandes 2009, full severity), side by side.

    Tools/.venv/bin/python Tools/cvd_sim.py OUT.jpg "Label=image.png:x,y,w,h" [...]
    Tools/.venv/bin/python Tools/cvd_sim.py --pair R,G,B R,G,B     # CIELAB difference of two colours

Each crop becomes a column; the rows are normal, deuteranopia and protanopia.
"""
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFont

MATRICES = {
    "normal": np.eye(3),
    "deuteranopia": np.array([[0.367322, 0.860646, -0.227968], [0.280085, 0.672501, 0.047413], [-0.011820, 0.042940, 0.968881]]),
    "protanopia": np.array([[0.152286, 1.052583, -0.204868], [0.114503, 0.786281, 0.099216], [-0.003882, -0.048116, 1.051998]]),
}


def to_linear(c):
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def to_srgb(c):
    c = np.clip(c, 0, 1)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * c ** (1 / 2.4) - 0.055)


def simulate(rgb, kind):
    """rgb: float array (..., 3) in sRGB 0..1."""
    lin = to_linear(rgb)
    return to_srgb(lin @ MATRICES[kind].T)


def lab(rgb):
    lin = to_linear(np.asarray(rgb, float))
    m = np.array([[0.4124, 0.3576, 0.1805], [0.2126, 0.7152, 0.0722], [0.0193, 0.1192, 0.9505]])
    xyz = lin @ m.T / np.array([0.95047, 1.0, 1.08883])
    f = np.where(xyz > 0.008856, np.cbrt(xyz), 7.787 * xyz + 16 / 116)
    return np.array([116 * f[1] - 16, 500 * (f[0] - f[1]), 200 * (f[1] - f[2])])


def pair(a, b):
    a = np.array([float(v) for v in a.split(",")])
    b = np.array([float(v) for v in b.split(",")])
    for kind in MATRICES:
        d = np.linalg.norm(lab(simulate(a, kind)) - lab(simulate(b, kind)))
        print(f"{kind:13s} delta E {d:5.1f}")


def main():
    if sys.argv[1] == "--pair":
        pair(sys.argv[2], sys.argv[3])
        return
    out, specs = sys.argv[1], sys.argv[2:]
    crops = []
    for spec in specs:
        label, rest = spec.split("=", 1)
        path, box = rest.rsplit(":", 1)
        x, y, w, h = (int(v) for v in box.split(","))
        crops.append((label, Image.open(path).convert("RGB").crop((x, y, x + w, y + h))))
    cw = max(c.width for _, c in crops)
    ch = max(c.height for _, c in crops)
    pad, head, side = 12, 34, 150
    sheet = Image.new("RGB", (side + len(crops) * (cw + pad) + pad, head + 3 * (ch + pad) + pad), (11, 17, 24))
    draw = ImageDraw.Draw(sheet)
    try:
        font = ImageFont.truetype("Assets/Resources/Fonts/AlegreyaSans-Medium.ttf", 22)
    except OSError:
        font = ImageFont.load_default()
    for col, (label, img) in enumerate(crops):
        draw.text((side + pad + col * (cw + pad), 6), label, fill=(233, 223, 199), font=font)
    for row, kind in enumerate(MATRICES):
        y = head + pad + row * (ch + pad)
        draw.text((10, y + ch // 2 - 12), kind, fill=(201, 163, 90), font=font)
        for col, (_, img) in enumerate(crops):
            arr = np.asarray(img, float) / 255.0
            sim = (simulate(arr, kind) * 255).round().astype(np.uint8)
            sheet.paste(Image.fromarray(sim), (side + pad + col * (cw + pad), y))
    sheet.save(out, quality=90)
    print("wrote", out)


if __name__ == "__main__":
    main()
