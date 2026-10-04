"""Gibberish radio voices: formant-synthesized babble with a personality per speaker,
processed through a VHF radio chain (band-pass, saturation, static)."""
import numpy as np
from scipy import signal

from dsp import SR, bandpass, compress, highpass, lowpass, saturate, white, write

VOWELS = {
    "a": (730, 1090, 2440), "e": (530, 1840, 2480), "i": (300, 2250, 3000), "o": (570, 860, 2410),
    "u": (320, 880, 2240), "ae": (660, 1720, 2410), "@": (500, 1500, 2500), "ai": (700, 1300, 2500),
}
CONS = ["s", "sh", "f", "t", "k", "p", "m", "n", "l", "r", "b", "d", "g", "h", "w"]

# name: (f0 Hz, syllables per second, formant scale, breathiness, pitch range, whisper)
SPEAKERS = {
    "ianto": (118, 5.2, 1.0, 0.08, 0.16, False),
    "maren": (215, 6.8, 1.17, 0.12, 0.28, False),
    "pryce": (92, 4.2, 0.95, 0.06, 0.12, False),
    "dot": (195, 6.0, 1.15, 0.1, 0.32, False),
    "corley": (80, 4.0, 0.92, 0.6, 0.1, True),
    "crew": (135, 5.6, 1.02, 0.1, 0.2, False),
}


def resonate_blocks(src, f_tracks, bws, gains):
    """Filter src through formant resonators whose centre frequencies follow per-sample tracks."""
    n = len(src)
    out = np.zeros(n)
    block = 220
    for k in range(len(bws)):
        zi = np.zeros(2)
        y = np.zeros(n)
        for start in range(0, n, block):
            seg = src[start:start + block]
            f = float(f_tracks[k][min(start + block // 2, n - 1)])
            bw = bws[k]
            r = np.exp(-np.pi * bw / SR)
            w = 2 * np.pi * f / SR
            b = [1 - r]
            a = [1, -2 * r * np.cos(w), r * r]
            ys, zi = signal.lfilter(b, a, seg, zi=zi)
            y[start:start + block] = ys
        out += y * gains[k]
    return out


def phrase_plan(rng, dur, rate):
    """Sequence of (start, length, kind, vowel, consonant) segments."""
    plan = []
    t = 0.05
    while t < dur - 0.3:
        words = rng.integers(2, 6)
        for w in range(words):
            syl = rng.integers(1, 4)
            for s in range(syl):
                length = rng.uniform(0.6, 1.3) / rate
                c = rng.choice(CONS) if rng.random() < 0.8 else None
                v = rng.choice(list(VOWELS.keys()))
                plan.append((t, length, c, v, w == 0 and s == 0))
                t += length
            t += rng.uniform(0.02, 0.08)
            if t > dur - 0.3:
                break
        t += rng.uniform(0.18, 0.45)
    return plan


def synth(speaker, seed, dur=5.0):
    f0, rate, fscale, breath, prange, whisper = SPEAKERS[speaker]
    rng = np.random.default_rng(seed)
    n = int(dur * SR)
    plan = phrase_plan(rng, dur, rate)
    # Pitch contour: phrase declination plus accents.
    f0_track = np.full(n, float(f0))
    amp = np.zeros(n)
    voiced = np.zeros(n)
    ftracks = [np.full(n, 500.0), np.full(n, 1500.0), np.full(n, 2500.0)]
    noise_src = np.zeros(n)
    for (t, length, c, v, accent) in plan:
        i0, i1 = int(t * SR), min(n, int((t + length) * SR))
        if i1 <= i0:
            continue
        L = i1 - i0
        cons_len = int(min(0.35, 0.25 + rng.uniform(-0.05, 0.05)) * L) if c else 0
        # Consonant.
        if c:
            ci1 = i0 + cons_len
            if c in ("s", "sh", "f", "h"):
                lo, hi = {"s": (4500, 9000), "sh": (2200, 5000), "f": (1500, 8000), "h": (500, 3000)}[c]
                burst = bandpass(rng.standard_normal(cons_len + 64), lo, hi)[:cons_len] * (0.5 if c != "h" else 0.25)
                noise_src[i0:ci1] += burst * np.hanning(cons_len)
            elif c in ("t", "k", "p", "b", "d", "g"):
                b_len = max(16, cons_len // 3)
                burst = bandpass(rng.standard_normal(b_len + 64), 1500 if c in "tdk" else 600, 6000)[:b_len] * 0.7
                noise_src[ci1 - b_len:ci1] += burst * np.linspace(1, 0, b_len)
            else:  # nasals, liquids, glides: voiced and low
                voiced[i0:ci1] = 0.6
                amp[i0:ci1] = 0.45
                for k, fv in enumerate((280, 1300 if c not in "mn" else 1000, 2400)):
                    ftracks[k][i0:ci1] = fv * fscale
        # Vowel.
        vi0 = i0 + cons_len
        fv = VOWELS[v]
        for k in range(3):
            ftracks[k][vi0:i1] = fv[k] * fscale * rng.uniform(0.95, 1.05)
        env = np.sin(np.linspace(0, np.pi, i1 - vi0)) ** 0.6
        amp[vi0:i1] = np.maximum(amp[vi0:i1], env)
        voiced[vi0:i1] = 1.0
        bump = (1 + prange * (0.6 if accent else rng.uniform(-0.3, 0.4)))
        f0_track[i0:i1] *= bump
    # Smooth the tracks so formants glide.
    smooth = np.hanning(int(0.03 * SR))
    smooth /= smooth.sum()
    for k in range(3):
        ftracks[k] = np.convolve(ftracks[k], smooth, mode="same")
    t = np.arange(n) / SR
    decl = 1 + 0.12 * np.cos(2 * np.pi * t / 2.3)
    f0_track = np.convolve(f0_track, smooth, mode="same") * decl * (1 + 0.01 * rng.standard_normal(n).cumsum() / np.sqrt(n))
    amp = np.convolve(amp, np.hanning(int(0.015 * SR)) / (0.0075 * SR), mode="same")
    voiced = np.convolve(voiced, np.hanning(int(0.01 * SR)) / (0.005 * SR), mode="same")

    # Glottal source: a band-limited pulse train with spectral tilt.
    phase = np.cumsum(f0_track) / SR
    src = np.zeros(n)
    for h in range(1, 28):
        src += np.sin(2 * np.pi * h * phase) / h ** 1.1
    if whisper:
        src = rng.standard_normal(n) * 0.6
    src = src * voiced + rng.standard_normal(n) * breath * voiced
    src *= amp
    y = resonate_blocks(src, ftracks, [90, 130, 180], [1.0, 0.7, 0.35])
    y += noise_src * 0.6
    # Radio chain.
    y = highpass(y, 120)
    y = bandpass(y, 350, 3200, 3)
    y = saturate(y / (np.max(np.abs(y)) + 1e-9) * 1.4, 2.2)
    hiss = bandpass(white(dur), 500, 4000) * 0.03
    y = compress(y + hiss, 0.5, 3)
    fadein = int(0.02 * SR)
    y[:fadein] *= np.linspace(0, 1, fadein)
    y[-int(0.2 * SR):] *= np.linspace(1, 0, int(0.2 * SR))
    return y


def build():
    for sp in SPEAKERS:
        for v in range(1, 5):
            x = synth(sp, seed=sum(ord(c) for c in sp) * 10 + v, dur=5.5)
            write(f"voice_{sp}_{v}", x, 0.8)
            print("voice", sp, v)
