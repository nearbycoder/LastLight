#!/usr/bin/env python3
"""Edits the trailer shoot into the Steam-style feature trailer, the README teaser loop and the
trailer's poster frame.

The shoot comes from the built game: `Tools/tour.sh trailer <dir> -llFresh -llSeed 4242` plays
every shot with the AutoKeeper and records video.mp4, the game's audio mix (music muted) and
clips.json (see Assets/Scripts/Automation/Trailer.cs). Tools/make_trailer.sh does both steps.

    Tools/.venv/bin/python Tools/make_trailer.py [--capture Captures/trailer] [--out docs/media]

The edit is cut to the bars of the game's own waltz, re-synthesized here without its loop seam by
ArtSource/audio/music.py. Captions use the game's fonts and colours. Needs numpy, scipy and Pillow
(Tools/.venv) and ffmpeg with libx264, aac and libwebp.
"""
import argparse
import json
import math
import os
import subprocess
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FONTS = os.path.join(ROOT, "Assets", "Resources", "Fonts")
AUDIO = os.path.join(ROOT, "Assets", "Resources", "Audio")
W, H, FPS, RATE = 1920, 1080, 30, 48000
SPF = RATE // FPS                      # output audio samples per video frame

PAPER = (233, 223, 199)
BRASS = (201, 163, 90)
BRASS_BRIGHT = (236, 196, 116)
MUTED = (150, 160, 170)

BAR = 60.0 / 84 * 3                    # the waltz: 84 bpm in 3/4
BEAT = BAR / 3
PULSE = 60.0 / 105 / 2                 # the tension layer's eighth notes


def font(name, size):
    return ImageFont.truetype(os.path.join(FONTS, name + ".ttf"), size)


def ease_out(t):
    t = min(1.0, max(0.0, t))
    return 1 - (1 - t) ** 3


def smooth(t):
    t = min(1.0, max(0.0, t))
    return t * t * (3 - 2 * t)


def frames(seconds):
    return int(round(seconds * FPS))


# ---------------------------------------------------------------------------- the edit

class Shot:
    """`dur` seconds of a clip starting `at` seconds into it. `mix` crossfades from the previous
    shot (0 is a cut); a shot with no clip is black. `gain` scales the shot's game audio."""

    def __init__(self, clip, at, dur, mix=0.0, gain=1.0, dark=0.0):
        self.clip, self.at, self.dur, self.mix, self.gain, self.dark = clip, at, dur, mix, gain, dark


class Beat:
    """A run of shots under one caption (kicker, title, body)."""

    def __init__(self, shots, caption=None, card=None):
        self.shots, self.caption, self.card = shots, caption, card


def edl():
    """The trailer, beat by beat. Times inside clips come from the shoot's clips.json events."""
    b = []
    # Cold open: dawn on the last day of the season. The Calloway's captain thanks the keeper,
    # and the keeper puts out the light.
    b.append(Beat([Shot("ending", 10.5, 6.2, gain=1.1), Shot("ending", 38.2, 7.3, mix=0.7, gain=1.1)]))
    b.append(Beat([Shot(None, 0, 0.6, mix=0.9)]))
    b.append(Beat([Shot(None, 0, 2.9)], card=["Gannet Head Light is to be switched off", "at the end of the season."]))
    b.append(Beat([Shot(None, 0, 2.2)], card=["Twelve nights left."]))
    # The title, on the waltz's first downbeat.
    b.append(Beat([Shot("title", 1.2, 2 * BAR, mix=0.4, gain=0.8)], card="TITLE"))
    # One beat per feature, cut on the bar lines.
    b.append(Beat([Shot("sweep", 1.7, 3 * BAR, gain=0.85)],
                  ("THE LIGHT", "Sweep the beam", "Ships steer by your light. Leave one in the dark and its captain loses the way.")))
    b.append(Beat([Shot("focus", 0.5, 2 * BAR, gain=0.85)],
                  ("FOCUS", "Narrow the lens", "Hold to focus: a longer reach, a slower swing.")))
    b.append(Beat([Shot("chart", 0.9, 3 * BAR, gain=0.85)],
                  ("HIDDEN REEFS", "Chart the rocks ahead", "Captains can't see the reefs. Light them in time and the surf breaks white.")))
    b.append(Beat([Shot("buoy", 0.6, 2 * BAR, gain=0.85)],
                  ("BUOYS", "Light the channel", "A burning buoy keeps nearby ships steady.")))
    b.append(Beat([Shot("hull_trawler", 0.4, 3 * BEAT, gain=0.85), Shot("hull_steamer", 0.4, 3 * BEAT, gain=0.85),
                   Shot("hull_ferry", 1.2, 3 * BEAT, gain=0.95)],
                  ("THREE HULLS", "Trawlers, colliers and the ferry", "Colliers turn wide and ground on sandbanks. The ferry is worth the most.")))
    b.append(Beat([Shot("fog", 0.9, 2 * BAR, gain=0.85)],
                  ("SEA FRET", "Sound the foghorn", "Fog swallows the beam. The horn steadies every ship in earshot.")))
    b.append(Beat([Shot("mayday", 1.2, 2 * BAR, gain=0.85)],
                  ("MAYDAY", "Find the ships running dark", "Damaged vessels show no lights, only a radio bearing and their flares.")))
    b.append(Beat([Shot("storm", 0.9, 2 * BAR, gain=0.85)],
                  ("SQUALL", "Ride out the storm", "Currents drag ships off course. Lightning shows every reef.")))
    b.append(Beat([Shot("false_light", 1.2, 3 * BAR, gain=0.85)],
                  ("WRECKERS", "Douse the false lights", "Wreckers sweep lanterns that imitate yours. Hold your beam on one to put it out.")))
    b.append(Beat([Shot("wreck", 2.0, 2 * BAR, gain=0.9)],
                  ("WRECKS", "Every ship counts", "Miss a reef and the hull burns and sinks. Lose too many and the night is over.")))
    b.append(Beat([Shot("results", 0.3, 2 * BAR, gain=0.9)],
                  ("TWELVE NIGHTS", "Keep the light until dawn", "Three lamps a night. At dawn, a chart of every ship's track to replay.", "narrow")))
    # The dawn chart replaying a night with a wreck, then Settings (both fill the screen, so they
    # carry no caption: the chart's legend and the line under Settings say what they are).
    b.append(Beat([Shot("replay", 0.6, 2 * BAR, gain=0.9)]))
    b.append(Beat([Shot("settings", 0.4, 2 * BAR, gain=0.9)]))
    b.append(Beat([Shot("watch", 0.5, 2 * BAR, gain=0.85)],
                  ("NIGHT WATCH", "Then keep watch for good", "Finish the season to open an endless watch: every hazard, its own weather, ships without end.")))
    # Escalation: the late nights, cut to the tension layer's pulse, ending on a hit to black.
    p = 4 * PULSE
    b.append(Beat([Shot("finale", 0.6, p, gain=1.0), Shot("finale_lightning", 0.9, p, gain=1.1),
                   Shot("mimic", 1.0, p, gain=1.0), Shot("false_light", 6.0, p, gain=1.0),
                   Shot("wreck_fog", 3.1, p, gain=1.1), Shot("fog", 2.4, p, gain=1.1),
                   Shot("finale", 4.4, p, gain=1.0), Shot("storm", 2.6, p, gain=1.15)]))
    b.append(Beat([Shot(None, 0, 1.1)]))
    b.append(Beat([Shot("endcard", 1.0, 7.5, mix=0.9, gain=0.6, dark=0.5)], card="END"))
    return b


def build_timeline(beats, clips):
    """Lays shots end to end (crossfades overlap) in output frames."""
    shots, captions, cards = [], [], []
    cursor = 0
    for beat in beats:
        beat_start = None
        for s in beat.shots:
            n = frames(s.dur)
            mix = frames(s.mix)
            start = cursor - mix
            s.out_start, s.out_len, s.mix_frames = start, n + mix, mix
            if s.clip:
                c = clips[s.clip]
                s.src = c["start"] + frames(s.at) - mix
                if frames(s.at) - mix < 0 or frames(s.at) + n > c["frames"]:
                    sys.exit(f"shot {s.clip} at {s.at}+{s.dur}s runs outside its clip ({c['frames']} frames)")
            shots.append(s)
            beat_start = cursor if beat_start is None else beat_start
            cursor += n
        if beat.caption:
            captions.append((beat_start, cursor, beat.caption))
        if beat.card:
            cards.append((beat_start, cursor, beat.card))
    return shots, captions, cards, cursor


# ---------------------------------------------------------------------------- graphics

def tracked(draw, xy, text, fnt, fill, tracking):
    """Text with letter spacing (PIL has none)."""
    x, y = xy
    for ch in text:
        draw.text((x, y), ch, font=fnt, fill=fill)
        x += fnt.getlength(ch) + tracking
    return x


def tracked_width(text, fnt, tracking):
    return sum(fnt.getlength(ch) for ch in text) + tracking * (len(text) - 1)


def wrap(text, fnt, width):
    """Greedy wrapping, then the same number of lines balanced so the last isn't a lone word."""
    words, lines, line = text.split(), [], ""
    for w in words:
        trial = (line + " " + w).strip()
        if fnt.getlength(trial) > width and line:
            lines.append(line)
            line = w
        else:
            line = trial
    lines.append(line)
    if len(lines) < 2:
        return lines
    target = fnt.getlength(text) / len(lines)
    for _ in range(4):
        out, line = [], ""
        for w in words:
            trial = (line + " " + w).strip()
            if fnt.getlength(trial) > target * 1.04 and line and len(out) < len(lines) - 1:
                out.append(line)
                line = w
            else:
                line = trial
        out.append(line)
        if all(fnt.getlength(l) <= width for l in out):
            return out
        target *= 1.06
    return lines


class Layer:
    """An RGBA image placed on the frame, with a drop shadow baked in."""

    def __init__(self, img, x, y, shadow=0.75, blur=5):
        if shadow:
            a = img.split()[3]
            sh = Image.new("RGBA", img.size, (0, 0, 0, 0))
            sh.putalpha(a.point(lambda v: int(v * shadow)))
            sh = sh.filter(ImageFilter.GaussianBlur(blur))
            base = Image.new("RGBA", img.size, (0, 0, 0, 0))
            base.alpha_composite(sh, (0, 3))
            base.alpha_composite(img)
            img = base
        arr = np.asarray(img).astype(np.float32) / 255.0
        self.rgb, self.a = arr[..., :3] * 255.0, arr[..., 3:]
        self.x, self.y = x, y

    def draw(self, frame, alpha, dx=0, dy=0, reveal=None):
        if alpha <= 0.002:
            return
        h, w = self.a.shape[:2]
        x0, y0 = int(round(self.x + dx)), int(round(self.y + dy))
        fx0, fy0, fx1, fy1 = max(0, x0), max(0, y0), min(W, x0 + w), min(H, y0 + h)
        if fx1 <= fx0 or fy1 <= fy0:
            return
        a = self.a[fy0 - y0:fy1 - y0, fx0 - x0:fx1 - x0] * alpha
        if reveal is not None:
            a = a * reveal[fy0 - y0:fy1 - y0, fx0 - x0:fx1 - x0]
        rgb = self.rgb[fy0 - y0:fy1 - y0, fx0 - x0:fx1 - x0]
        region = frame[fy0:fy1, fx0:fx1]
        region[:] = region * (1 - a) + rgb * a


def text_image(lines, fnt, fill, tracking=0, line_gap=1.25, pad=14, center=False):
    widths = [tracked_width(l, fnt, tracking) for l in lines]
    asc, desc = fnt.getmetrics()
    lh = int((asc + desc) * line_gap)
    img = Image.new("RGBA", (int(max(widths)) + pad * 2, lh * (len(lines) - 1) + asc + desc + pad * 2), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    for i, l in enumerate(lines):
        x = pad + ((max(widths) - widths[i]) / 2 if center else 0)
        tracked(d, (x, pad + i * lh), l, fnt, fill, tracking)
    return img


def shade_mask(cx, cy, rx, ry):
    """A soft elliptical shadow (1 in the middle, 0 at the rim), as a full-frame float mask."""
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    d = np.sqrt(((xx - cx) / rx) ** 2 + ((yy - cy) / ry) ** 2)
    return (1 - np.clip(d, 0, 1)) ** 1.6


class Caption:
    """Bottom-left: a brass kicker, the title in Cormorant, a brass rule and an italic line. The
    narrow layout keeps to the left margin, beside menus that fill the middle of the screen."""
    X, BOTTOM = 104, 92
    SHADES = {}

    def __init__(self, kicker, title, body, narrow=False):
        tfont = font("CormorantGaramond-SemiBold", 60 if narrow else 66)
        bfont = font("AlegreyaSans-Italic", 29 if narrow else 31)
        k = text_image([kicker], font("AlegreyaSans-Bold", 22), BRASS + (255,), tracking=5)
        t = text_image(wrap(title, tfont, 345) if narrow else [title], tfont, PAPER + (255,), line_gap=1.0)
        bimg = text_image(wrap(body, bfont, 330 if narrow else 880), bfont, (236, 230, 216, 255), line_gap=1.2)
        x = 66 if narrow else self.X
        y = H - self.BOTTOM - bimg.height
        rule_y = y - 4
        ty = rule_y - 10 - t.height + 14
        ky = ty - k.height + 18
        self.layers = [Layer(k, x - 14, ky, 0.8, 4), Layer(t, x - 14, ty, 0.85, 6), Layer(bimg, x - 14, y, 0.85, 4)]
        self.rule = (x, rule_y + 8)
        key = "narrow" if narrow else "wide"
        if key not in Caption.SHADES:
            Caption.SHADES[key] = (shade_mask(200, H - 280, 420, 520) if narrow else shade_mask(430, H - 150, 900, 330))[..., None] * 0.62
        self.shade = Caption.SHADES[key]

    def draw(self, frame, t, length):
        fade_out = 1 - smooth((t - (length - 0.4)) / 0.4)
        frame *= 1 - self.shade * smooth(t / 0.5) * fade_out
        for i, layer in enumerate(self.layers):
            p = ease_out((t - 0.12 - i * 0.09) / 0.6)
            layer.draw(frame, p * fade_out, dx=-34 * (1 - p) - 10 * (1 - fade_out))
        p = ease_out((t - 0.3) / 0.7)
        x0, y0 = self.rule
        w = int(96 * p)
        if w > 0 and fade_out > 0:
            region = frame[y0:y0 + 2, x0:x0 + w]
            a = 0.9 * fade_out
            region[:] = region * (1 - a) + np.array(BRASS, np.float32) * a


class Card:
    """Centered lines on black (the cold open's set-up)."""

    def __init__(self, lines):
        img = text_image(lines, font("CormorantGaramond-SemiBold", 64), PAPER + (255,), line_gap=1.2, center=True)
        self.layer = Layer(img, (W - img.width) // 2, (H - img.height) // 2 - 10, 0.0)

    def draw(self, frame, t, length):
        a = smooth(t / 0.7) * (1 - smooth((t - (length - 0.55)) / 0.55))
        self.layer.draw(frame, a, dy=8 * (1 - ease_out(t / 1.2)))


class TitleCard:
    """LAST LIGHT revealed by a passing light, left of the tower like the game's title screen."""

    def __init__(self):
        big = font("CormorantGaramond-Bold", 176)
        img = text_image(["LAST LIGHT"], big, PAPER + (255,), tracking=14, pad=40)
        self.title = Layer(img, 120, 300, 0.7, 8)
        self.glow = np.asarray(img.split()[3].filter(ImageFilter.GaussianBlur(18))).astype(np.float32)[..., None] / 255.0
        sub = text_image(["Keep the last lighthouse on a wrecking coast."], font("AlegreyaSans-Italic", 40), (236, 230, 216, 255))
        self.sub = Layer(sub, 150, 300 + img.height - 30, 0.8, 5)
        self.rule = (160, 300 + img.height - 34)
        self.width = img.width
        self.shade = shade_mask(560, 470, 900, 420)[..., None] * 0.55

    def draw(self, frame, t, length):
        out = 1 - smooth((t - (length - 0.5)) / 0.5)
        frame *= 1 - self.shade * smooth(t / 0.8) * out
        # A soft front sweeps left to right across the letters, with a warm glow riding on it.
        front = -200 + (self.width + 400) * ease_out((t - 0.15) / 1.5)
        xs = np.arange(self.title.a.shape[1], dtype=np.float32)[None, :, None]
        reveal = np.clip((front - xs) / 220.0, 0, 1)
        self.title.draw(frame, out, reveal=reveal)
        glow = np.exp(-((xs - front + 60) / 110.0) ** 2) * self.glow * 0.9 * out
        h, w = glow.shape[:2]
        region = frame[self.title.y:self.title.y + h, self.title.x:self.title.x + w]
        region[:] = np.minimum(255, region + glow * np.array((255, 214, 150), np.float32))
        p = ease_out((t - 1.1) / 0.8)
        x0, y0 = self.rule
        if p > 0:
            region = frame[y0:y0 + 2, x0:x0 + int(560 * p)]
            region[:] = region * (1 - 0.85 * out) + np.array(BRASS, np.float32) * 0.85 * out
        self.sub.draw(frame, ease_out((t - 1.35) / 0.8) * out, dy=10 * (1 - ease_out((t - 1.35) / 0.8)))


class EndCard:
    def __init__(self):
        cx = W // 2
        title = text_image(["LAST LIGHT"], font("CormorantGaramond-Bold", 132), PAPER + (255,), tracking=11, pad=30)
        tag = text_image(["Twelve nights. One light. Bring them home."], font("AlegreyaSans-Italic", 40), (236, 230, 216, 255))
        url = text_image(["github.com/nearbycoder/LastLight"], font("AlegreyaSans-Medium", 40), BRASS_BRIGHT + (255,), tracking=1)
        small = text_image(["Linux  ·  Mouse, keyboard or gamepad  ·  Made with Unity and Blender"],
                           font("AlegreyaSans-Regular", 25), (196, 202, 208, 255), tracking=1)
        y = 130
        self.layers = [Layer(title, cx - title.width // 2, y, 0.7, 8)]
        y += title.height - 20
        self.rule = (cx - 220, y + 4)
        self.layers.append(Layer(tag, cx - tag.width // 2, y + 22, 0.8, 5))
        y += 22 + tag.height + 70
        self.layers.append(Layer(url, cx - url.width // 2, y, 0.8, 5))
        self.layers.append(Layer(small, cx - small.width // 2, y + url.height + 14, 0.8, 4))

    def draw(self, frame, t, length):
        out = 1 - smooth((t - (length - 1.2)) / 1.2)
        for i, (layer, at) in enumerate(zip(self.layers, (0.5, 1.3, 2.1, 2.5))):
            p = ease_out((t - at) / 0.9)
            layer.draw(frame, p * out, dy=12 * (1 - p))
        p = ease_out((t - 1.0) / 0.9)
        x0, y0 = self.rule
        w = int(440 * p)
        if w > 0:
            region = frame[y0:y0 + 2, x0 + (440 - w) // 2:x0 + (440 + w) // 2]
            region[:] = region * (1 - 0.85 * out) + np.array(BRASS, np.float32) * 0.85 * out
        frame *= out   # fade to black at the very end


# ---------------------------------------------------------------------------- video

class Reader:
    """Frames of the shoot from a given frame on, decoded by ffmpeg."""

    def __init__(self, video, start):
        t = max(0.0, (start - 0.5) / FPS)
        self.p = subprocess.Popen(["ffmpeg", "-v", "fatal", "-ss", f"{t:.5f}", "-i", video, "-f", "rawvideo",
                                   "-pix_fmt", "rgb24", "-"], stdout=subprocess.PIPE, bufsize=W * H * 3 * 2)

    def read(self):
        buf = self.p.stdout.read(W * H * 3)
        if len(buf) < W * H * 3:
            sys.exit("ran out of frames in the shoot")
        return np.frombuffer(buf, np.uint8).reshape(H, W, 3)

    def close(self):
        self.p.stdout.close()
        self.p.kill()
        self.p.wait()


def render_video(shots, captions, cards, total, video, out_path):
    enc = subprocess.Popen(["ffmpeg", "-v", "error", "-y", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{W}x{H}",
                            "-r", str(FPS), "-i", "-", "-c:v", "libx264", "-preset", "veryfast", "-crf", "12",
                            "-pix_fmt", "yuv420p", out_path], stdin=subprocess.PIPE)
    readers = {}
    overlays = []
    for start, end, cap in captions:
        overlays.append((start, end, Caption(*cap[:3], narrow=len(cap) > 3 and cap[3] == "narrow")))
    for start, end, card in cards:
        if card == "TITLE":
            overlays.append((start, end, TitleCard()))
        elif card == "END":
            overlays.append((start, end, EndCard()))
        else:
            overlays.append((start, end, Card(card)))
    black = np.zeros((H, W, 3), np.float32)
    for f in range(total):
        for i in [i for i in readers if f >= shots[i].out_start + shots[i].out_len]:
            readers.pop(i).close()
        frame = None
        for i, s in enumerate(shots):
            if not (s.out_start <= f < s.out_start + s.out_len):
                continue
            if s.clip is None:
                img = black
            else:
                if i not in readers:
                    readers[i] = Reader(video, s.src + (f - s.out_start))
                img = readers[i].read().astype(np.float32)
                if s.dark:
                    img *= 1 - s.dark
            if frame is None:
                frame = img.copy()
            else:
                # An incoming shot dissolves over the outgoing one during its crossfade.
                k = (f - s.out_start + 0.5) / max(1, s.mix_frames)
                frame = frame * (1 - k) + img * k
        if frame is None:
            frame = black.copy()
        for start, end, ov in overlays:
            if start <= f < end:
                ov.draw(frame, (f - start) / FPS, (end - start) / FPS)
        enc.stdin.write(np.clip(frame, 0, 255).astype(np.uint8).tobytes())
        if f % 300 == 0:
            print(f"  frame {f}/{total}", flush=True)
    for r in readers.values():
        r.close()
    enc.stdin.close()
    enc.wait()


# ---------------------------------------------------------------------------- audio

def load_audio(path, rate=RATE):
    raw = subprocess.run(["ffmpeg", "-v", "error", "-i", path, "-f", "f32le", "-ac", "2", "-ar", str(rate), "-"],
                         check=True, capture_output=True).stdout
    return np.frombuffer(raw, np.float32).reshape(-1, 2).copy()


def waltz_unlooped():
    """The title waltz from the game's generator, with its real ending instead of the loop seam."""
    sys.path.insert(0, os.path.join(ROOT, "ArtSource", "audio"))
    import dsp
    import music
    from scipy.signal import resample_poly
    x, total = music.waltz(84, 2)
    x = dsp.reverb(x, 0.35, 3.5, 1.6, 6000)
    x = x[:, :int((total + 3.5) * dsp.SR)]
    x = dsp.compress(x, 0.6, 2.5)
    x = dsp.highpass(x, 12)
    x = dsp.normalize(x, 0.8)
    n = int(0.05 * dsp.SR)
    x[:, -n:] *= np.linspace(1, 0, n)
    return resample_poly(x, 160, 147, axis=1).T.astype(np.float32)


def envelope(n, fade_in, fade_out):
    e = np.ones(n, np.float32)
    if fade_in > 0:
        k = min(n, fade_in)
        e[:k] *= np.sin(np.linspace(0, np.pi / 2, k))
    if fade_out > 0:
        k = min(n, fade_out)
        e[-k:] *= np.cos(np.linspace(0, np.pi / 2, k))
    return e


def place(buf, x, at, gain=1.0):
    at = int(at)
    if at < 0:
        x, at = x[-at:], 0
    n = min(len(x), len(buf) - at)
    if n > 0:
        buf[at:at + n] += x[:n] * gain


def mix_audio(shots, total, game, spf_in, music_cues):
    n = total * SPF
    sfx = np.zeros((n, 2), np.float32)
    for i, s in enumerate(shots):
        if not s.clip:
            continue
        nxt = shots[i + 1] if i + 1 < len(shots) else None
        length = s.out_len * SPF
        src0 = int(round(s.src * spf_in))
        seg = game[src0:src0 + length]
        fin = s.mix_frames * SPF if s.mix_frames else int(0.03 * RATE)
        # Cuts get a click-free 30 ms; the last shot fades out with the picture.
        fout = nxt.mix_frames * SPF if nxt is not None and nxt.mix_frames else int((1.5 if nxt is None else 0.03) * RATE)
        seg = seg * envelope(len(seg), fin, fout)[:, None]
        place(sfx, seg, s.out_start * SPF, s.gain)
    mus = np.zeros((n, 2), np.float32)
    for clip, src_at, out_at, dur, gain_db, fin, fout in music_cues:
        x = clip[int(src_at * RATE):int((src_at + dur) * RATE)]
        x = x * envelope(len(x), int(fin * RATE), int(fout * RATE))[:, None]
        place(mus, x, out_at * RATE, 10 ** (gain_db / 20))
    # Duck the music under loud moments of the game (horns, wrecks, thunder, the radio).
    level = np.sqrt(np.convolve((sfx ** 2).mean(axis=1), np.ones(960) / 960, mode="same")) + 1e-6
    db = 20 * np.log10(level)
    target = np.clip((db + 30) * 0.55, 0, 7.5)          # dB of reduction above -30 dBFS
    duck = np.empty_like(target)
    g, att, rel = 0.0, 1 - math.exp(-1 / (0.015 * RATE)), 1 - math.exp(-1 / (0.45 * RATE))
    for i in range(0, len(target), 48):                    # 1 ms control rate
        tgt = target[i]
        g += (tgt - g) * (1 - (1 - (att if tgt > g else rel)) ** 48)
        duck[i:i + 48] = g
    mus *= (10 ** (-duck / 20))[:, None]
    return sfx + mus, sfx, mus


# ---------------------------------------------------------------------------- main

def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--capture", default=os.path.join(ROOT, "Captures", "trailer"))
    ap.add_argument("--out", default=os.path.join(ROOT, "docs", "media"))
    ap.add_argument("--max-mb", type=float, default=38.0)
    args = ap.parse_args()
    cap, out = args.capture, args.out
    os.makedirs(out, exist_ok=True)
    meta = json.load(open(os.path.join(cap, "clips.json")))
    clips = {c["name"]: c for c in meta["clips"]}
    total_frames = sum(c["frames"] for c in meta["clips"])
    rate, channels = (int(v) for v in open(os.path.join(cap, "audio.txt")).read().split())
    game = np.fromfile(os.path.join(cap, "audio.f32"), np.float32).reshape(-1, channels)[:, :2]
    if rate != RATE:
        sys.exit(f"expected {RATE} Hz game audio, got {rate}")
    spf_in = len(game) / total_frames

    beats = edl()
    shots, captions, cards, total = build_timeline(beats, clips)
    title_at = next(s for s, e, c in cards if c == "TITLE") / FPS
    montage = beats[-3].shots
    montage_at, montage_end = montage[0].out_start / FPS, (montage[-1].out_start + montage[-1].out_len) / FPS
    end_at = next(s for s, e, c in cards if c == "END") / FPS
    print(f"trailer: {total / FPS:.1f} s, title at {title_at:.2f} s, montage {montage_at:.2f}-{montage_end:.2f} s")

    # Music: dawn under the cold open, the waltz from the title to the end of the features, the
    # night bed and its tension layer under the montage, and the ending's final chord on the end card.
    waltz = waltz_unlooped()
    dawn = load_audio(os.path.join(AUDIO, "music_dawn.ogg"))
    night = load_audio(os.path.join(AUDIO, "music_night.ogg"))
    tension = load_audio(os.path.join(AUDIO, "music_tension.ogg"))
    ending = load_audio(os.path.join(AUDIO, "music_ending.ogg"))
    sea = load_audio(os.path.join(AUDIO, "amb_sea.ogg"))
    cold_end = (shots[1].out_start + shots[1].out_len) / FPS
    waltz_in = title_at - BAR                              # one bar of intro pad before bar one
    cues = [
        (dawn, 6.0, 0.0, cold_end - 3.2, -9.0, 1.5, 3.0),
        (sea, 4.0, cold_end - 1.0, title_at - cold_end + 2.5, -15.0, 1.2, 1.8),   # the sea under the black cards
        (waltz, 0.0, waltz_in, montage_at - waltz_in + 0.6, -5.5, 0.8, 0.6),
        (night, 8.0, montage_at - 0.3, montage_end - montage_at + 0.3, -5.0, 0.3, 0.05),
        (tension, 0.0, montage_at, montage_end - montage_at, -3.0, 0.02, 0.05),
        (ending, 48.2, end_at - 0.2, total / FPS - end_at + 0.2, -4.0, 0.05, 2.5),
    ]
    mix, sfx, mus = mix_audio(shots, total, game, spf_in, cues)
    wav = os.path.join(cap, "mix.f32")
    mix.astype(np.float32).tofile(wav)

    edit = os.path.join(cap, "edit.mp4")
    print("rendering the edit...")
    render_video(shots, captions, cards, total, os.path.join(cap, "video.mp4"), edit)

    # Loudness: measure, then normalize to -16 LUFS with a -1.5 dBTP ceiling.
    raw_in = ["-f", "f32le", "-ar", str(RATE), "-ac", "2", "-i", wav]
    meas = subprocess.run(["ffmpeg", "-hide_banner", *raw_in, "-af", "loudnorm=I=-16:TP=-1.5:LRA=11:print_format=json",
                           "-f", "null", "-"], capture_output=True, text=True).stderr
    m = json.loads(meas[meas.rindex("{"):meas.rindex("}") + 1])
    norm = (f"loudnorm=I=-16:TP=-1.5:LRA=11:measured_I={m['input_i']}:measured_TP={m['input_tp']}:"
            f"measured_LRA={m['input_lra']}:measured_thresh={m['input_thresh']}:offset={m['target_offset']}:linear=true")
    audio_aac = os.path.join(cap, "mix.m4a")
    subprocess.run(["ffmpeg", "-v", "error", "-y", *raw_in, "-af", norm + ",aresample=48000", "-c:a", "aac", "-b:a", "160k",
                    audio_aac], check=True)

    # The trailer: two-pass x264 sized to fit the budget.
    seconds = total / FPS
    vbit = int((args.max_mb * 8 * 1024 * 1024 / seconds - 165_000) / 1000)
    trailer = os.path.join(out, "LastLight_trailer.mp4")
    common = ["-c:v", "libx264", "-preset", "slow", "-tune", "film", "-b:v", f"{vbit}k", "-maxrate", f"{int(vbit * 1.8)}k",
              "-bufsize", f"{vbit * 3}k", "-pix_fmt", "yuv420p", "-profile:v", "high", "-level", "4.1", "-g", "60",
              "-passlogfile", os.path.join(cap, "x264")]
    print(f"encoding the trailer at {vbit} kbit/s...")
    subprocess.run(["nice", "ffmpeg", "-v", "error", "-y", "-i", edit, *common, "-pass", "1", "-an", "-f", "null", "-"], check=True)
    subprocess.run(["nice", "ffmpeg", "-v", "error", "-y", "-i", edit, "-i", audio_aac, *common, "-pass", "2",
                    "-c:a", "copy", "-map", "0:v", "-map", "1:a", "-shortest", "-movflags", "+faststart", trailer], check=True)
    print(f"  {trailer}: {os.path.getsize(trailer) / 1e6:.1f} MB")

    poster(edit, title_at + 3.6, os.path.join(out, "trailer_poster.jpg"))
    teaser(os.path.join(cap, "video.mp4"), clips, os.path.join(out, "teaser.webp"))
    json.dump({"duration": seconds, "title_at": title_at, "montage": [montage_at, montage_end], "end_card": end_at,
               "beats": [{"start": s / FPS, "end": e / FPS, "caption": list(c[:3])} for s, e, c in captions]},
              open(os.path.join(cap, "timeline.json"), "w"), indent=1)


def poster(edit, at, path):
    """A still from the title card with a play button, for the README's link to the trailer."""
    raw = subprocess.run(["ffmpeg", "-v", "error", "-ss", f"{at:.3f}", "-i", edit, "-frames:v", "1", "-f", "rawvideo",
                          "-pix_fmt", "rgb24", "-"], check=True, capture_output=True).stdout
    img = Image.frombytes("RGB", (W, H), raw).convert("RGBA")
    over = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(over)
    cx, cy, r = W // 2 + 150, H // 2 + 190, 92        # on the open sea, clear of the title and the tower
    d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=(8, 12, 18, 150), outline=PAPER + (230,), width=5)
    s = 46
    d.polygon([(cx - s * 0.55, cy - s), (cx - s * 0.55, cy + s), (cx + s, cy)], fill=PAPER + (245,))
    label = font("AlegreyaSans-Bold", 30)
    tracked(d, (cx - tracked_width("WATCH THE TRAILER", label, 4) / 2, cy + r + 22), "WATCH THE TRAILER", label, PAPER + (235,), 4)
    img.alpha_composite(over)
    img.convert("RGB").save(path, quality=88, optimize=True, progressive=True)
    print(f"  {path}: {os.path.getsize(path) / 1e3:.0f} kB")


def teaser(video, clips, path, clip="sweep", at=0.6, seconds=8.0):
    """An 8-second loop of the core mechanic for the top of the README: a trawler loses its way in
    the dark and the beam swings over to find it. Animated WebP, 960x540, 15 fps."""
    c = clips[clip]
    start, n = c["start"] + frames(at), frames(seconds)
    fade = frames(1.0)
    if frames(at) + n + fade > c["frames"]:
        sys.exit(f"the teaser runs past the end of the {clip} clip")
    raw = subprocess.run(["ffmpeg", "-v", "error", "-ss", f"{(start - 0.5) / FPS:.4f}", "-i", video, "-frames:v", str(n + fade),
                          "-vf", "scale=960:540:flags=lanczos", "-f", "rawvideo", "-pix_fmt", "rgb24", "-"],
                         check=True, capture_output=True).stdout
    fr = np.frombuffer(raw, np.uint8).reshape(-1, 540, 960, 3).astype(np.float32)
    # Loop seamlessly: the clip's last second dissolves into its first.
    for i in range(fade):
        k = (i + 0.5) / fade
        fr[i] = fr[i] * k + fr[n + i] * (1 - k)
    fr = fr[:n:2]                                          # 15 fps
    p = subprocess.Popen(["ffmpeg", "-v", "error", "-y", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", "960x540", "-r", "15",
                          "-i", "-", "-c:v", "libwebp_anim", "-lossless", "0", "-q:v", "88", "-compression_level", "6",
                          "-loop", "0", "-preset", "picture", path], stdin=subprocess.PIPE)
    p.stdin.write(np.clip(fr, 0, 255).astype(np.uint8).tobytes())
    p.stdin.close()
    p.wait()
    print(f"  {path}: {os.path.getsize(path) / 1e6:.2f} MB")


if __name__ == "__main__":
    main()
