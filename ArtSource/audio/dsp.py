"""Small DSP toolkit for Last Light's synthesized audio (numpy + scipy)."""
import os

import numpy as np
from scipy import signal
from scipy.io import wavfile

SR = 44100
rng = np.random.default_rng(1871)
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Resources", "Audio")


def t_axis(dur):
    return np.arange(int(dur * SR)) / SR


def seconds(n):
    return np.zeros(int(n * SR))


# ------------------------------------------------------------------ noise & oscillators

def white(dur):
    return rng.standard_normal(int(dur * SR))


def pink(dur):
    n = int(dur * SR)
    w = rng.standard_normal(n)
    b, a = [0.049922035, -0.095993537, 0.050612699, -0.004408786], [1, -2.494956002, 2.017265875, -0.522189400]
    return signal.lfilter(b, a, w) * 3.5


def brown(dur):
    w = rng.standard_normal(int(dur * SR))
    b = np.cumsum(w)
    b = signal.lfilter([1, -1], [1, -0.999], b)
    return b / (np.max(np.abs(b)) + 1e-9)


def sine(freq, dur, phase=0.0):
    t = t_axis(dur)
    if np.ndim(freq) == 0:
        return np.sin(2 * np.pi * freq * t + phase)
    return np.sin(2 * np.pi * np.cumsum(freq) / SR + phase)


def saw(freq, dur):
    """Band-limited-ish sawtooth via additive partials up to ~ 8 kHz."""
    t = t_axis(dur)
    f = np.full_like(t, freq) if np.ndim(freq) == 0 else freq
    ph = 2 * np.pi * np.cumsum(f) / SR
    out = np.zeros_like(t)
    fmax = float(np.max(f))
    k = 1
    while k * fmax < 9000 and k < 60:
        out += np.sin(k * ph) / k
        k += 1
    return out * 0.6


def square(freq, dur):
    t = t_axis(dur)
    f = np.full_like(t, freq) if np.ndim(freq) == 0 else freq
    ph = 2 * np.pi * np.cumsum(f) / SR
    out = np.zeros_like(t)
    fmax = float(np.max(f))
    k = 1
    while k * fmax < 9000 and k < 60:
        out += np.sin(k * ph) / k
        k += 2
    return out * 0.8


def glide(f0, f1, dur, curve=1.0):
    x = np.linspace(0, 1, int(dur * SR)) ** curve
    return f0 * (f1 / f0) ** x


# ------------------------------------------------------------------ envelopes

def adsr(n, a, d, s, r, sr=SR):
    n = int(n)
    a, d, r = int(a * sr), int(d * sr), int(r * sr)
    sus = max(0, n - a - d - r)
    e = np.concatenate([
        np.linspace(0, 1, max(a, 1), endpoint=False),
        np.linspace(1, s, max(d, 1), endpoint=False),
        np.full(sus, s),
        np.linspace(s, 0, max(r, 1)),
    ])
    if len(e) < n:
        e = np.pad(e, (0, n - len(e)))
    return e[:n]


def expdecay(dur, tau, attack=0.002):
    t = t_axis(dur)
    e = np.exp(-t / tau)
    a = int(attack * SR)
    if a > 0:
        e[:a] *= np.linspace(0, 1, a)
    return e


def fade(x, fin=0.01, fout=0.05):
    x = x.copy()
    a, b = int(fin * SR), int(fout * SR)
    if a > 0:
        x[:a] *= np.linspace(0, 1, a)
    if b > 0:
        x[-b:] *= np.linspace(1, 0, b)
    return x


# ------------------------------------------------------------------ filters

def lowpass(x, fc, order=4):
    sos = signal.butter(order, min(fc, SR * 0.45), "low", fs=SR, output="sos")
    return signal.sosfilt(sos, x, axis=-1)


def highpass(x, fc, order=2):
    sos = signal.butter(order, fc, "high", fs=SR, output="sos")
    return signal.sosfilt(sos, x, axis=-1)


def bandpass(x, lo, hi, order=2):
    sos = signal.butter(order, [lo, min(hi, SR * 0.45)], "band", fs=SR, output="sos")
    return signal.sosfilt(sos, x, axis=-1)


def resonator(x, freq, q):
    """Two-pole resonant band-pass (formant)."""
    w = 2 * np.pi * freq / SR
    r = np.exp(-np.pi * (freq / q) / SR)
    b = [1 - r]
    a = [1, -2 * r * np.cos(w), r * r]
    return signal.lfilter(b, a, x)


def sweep_lowpass(x, f_start, f_end, blocks=64):
    """Time-varying low-pass by processing in blocks with overlap-add."""
    n = len(x)
    out = np.zeros(n)
    size = max(256, n // blocks)
    hop = size // 2
    win = np.hanning(size)
    freqs = np.geomspace(f_start, f_end, (n // hop) + 2)
    for i, start in enumerate(range(0, n, hop)):
        seg = x[start:start + size]
        if len(seg) < 16:
            break
        y = lowpass(np.pad(seg, (0, size - len(seg))), freqs[i], 2)
        out[start:start + size] += (y * win)[:len(seg)]
    return out


# ------------------------------------------------------------------ space & dynamics

def impulse_response(dur=2.5, decay=0.6, bright=6000, stereo=True, seed=3):
    r = np.random.default_rng(seed)
    n = int(dur * SR)
    t = np.arange(n) / SR
    env = np.exp(-t / decay * 3.0)
    chans = []
    for c in range(2 if stereo else 1):
        noise = r.standard_normal(n) * env
        noise = lowpass(noise, bright, 2)
        early = np.zeros(n)
        for k in range(10):
            idx = int(r.uniform(0.005, 0.07) * SR)
            early[idx] += r.uniform(0.2, 0.6) * (1 if r.random() > 0.5 else -1)
        chans.append(noise + early)
    ir = np.stack(chans) if stereo else chans[0]
    return ir / np.max(np.abs(ir))


def reverb(x, wet=0.3, dur=2.5, decay=0.6, bright=6000, seed=3):
    """Convolution reverb; mono or (2, n) input -> stereo (2, n + tail)."""
    ir = impulse_response(dur, decay, bright, True, seed)
    if x.ndim == 1:
        x = np.stack([x, x])
    out = []
    for c in range(2):
        w = signal.fftconvolve(x[c], ir[c]) * 0.08
        dry = np.pad(x[c], (0, len(w) - x.shape[1]))
        out.append(dry * (1 - wet) + w * wet)
    return np.stack(out)


def saturate(x, drive=1.5):
    return np.tanh(x * drive) / np.tanh(drive)


def compress(x, threshold=0.5, ratio=3.0):
    a = np.abs(x)
    gain = np.where(a > threshold, (threshold + (a - threshold) / ratio) / np.maximum(a, 1e-9), 1.0)
    gain = signal.lfilter([0.01], [1, -0.99], gain)
    return x * gain


def pan(x, p):
    """Mono -> stereo with equal-power pan (-1 left, 1 right)."""
    a = (p + 1) * np.pi / 4
    return np.stack([x * np.cos(a), x * np.sin(a)])


def mix(*parts):
    n = max(p.shape[-1] for p in parts)
    stereo = any(p.ndim == 2 for p in parts)
    out = np.zeros((2, n)) if stereo else np.zeros(n)
    for p in parts:
        if stereo and p.ndim == 1:
            p = np.stack([p, p])
        out[..., :p.shape[-1]] += p
    return out


def place(dst, src, at):
    """Add src into dst starting at time `at` (seconds); grows dst if needed."""
    i = int(at * SR)
    end = i + src.shape[-1]
    if end > dst.shape[-1]:
        pad = [(0, 0)] * (dst.ndim - 1) + [(0, end - dst.shape[-1])]
        dst = np.pad(dst, pad)
    if dst.ndim == 2 and src.ndim == 1:
        src = np.stack([src, src])
    dst[..., i:end] += src
    return dst


def normalize(x, peak=0.89):
    m = np.max(np.abs(x))
    return x * (peak / m) if m > 0 else x


def loopify(x, cross=1.0):
    """Make a seamless loop by crossfading the tail into the head."""
    n = int(cross * SR)
    if x.ndim == 1:
        head, body, tail = x[:n], x[n:-n] if n > 0 else x, x[-n:]
        f = np.linspace(0, 1, n)
        return np.concatenate([tail * (1 - f) + head * f, body])
    out = []
    for c in range(x.shape[0]):
        out.append(loopify(x[c], cross))
    return np.stack(out)


def write(name, x, peak=0.89, sr=SR):
    """Normalise and save as high-quality Ogg Vorbis (via ffmpeg) in Resources/Audio."""
    import subprocess
    import tempfile
    os.makedirs(OUT, exist_ok=True)
    x = normalize(x, peak) if peak else x
    data = (np.clip(x, -1, 1) * 32767).astype(np.int16)
    if data.ndim == 2:
        data = data.T
    with tempfile.NamedTemporaryFile(suffix=".wav", delete=False) as tmp:
        wavfile.write(tmp.name, sr, data)
    path = os.path.join(OUT, name + ".ogg")
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", tmp.name, "-c:a", "libvorbis", "-q:a", "8", path], check=True)
    os.unlink(tmp.name)
    return path


# ------------------------------------------------------------------ instruments

def note_freq(name):
    """'A4' -> 440, supports sharps/flats: C#4, Bb3."""
    names = {"C": -9, "D": -7, "E": -5, "F": -4, "G": -2, "A": 0, "B": 2}
    n = names[name[0]]
    i = 1
    if name[i] == "#":
        n += 1; i += 1
    elif name[i] == "b":
        n -= 1; i += 1
    octave = int(name[i:])
    return 440.0 * 2 ** ((n + (octave - 4) * 12) / 12)


def fm_bell(freq, dur, ratio=3.5, index=3.0, tau=1.2, bright_tau=0.25):
    t = t_axis(dur)
    idx = index * np.exp(-t / bright_tau)
    mod = np.sin(2 * np.pi * freq * ratio * t) * idx
    car = np.sin(2 * np.pi * freq * t + mod)
    return car * expdecay(dur, tau, 0.001)


def bell(freq, dur=3.0, tau=1.4):
    """Church/ship bell: inharmonic partials."""
    partials = [(0.5, 1.0, 1.2), (1.0, 0.8, 1.0), (1.19, 0.6, 0.8), (1.56, 0.4, 0.6), (2.0, 0.5, 0.5), (2.51, 0.3, 0.35), (2.66, 0.25, 0.3), (3.01, 0.2, 0.25), (4.1, 0.12, 0.15)]
    t = t_axis(dur)
    out = np.zeros_like(t)
    for r, a, tf in partials:
        out += a * np.sin(2 * np.pi * freq * r * t + rng.uniform(0, 6)) * np.exp(-t / (tau * tf))
    strike = bandpass(white(0.02), 2000, 8000) * np.linspace(1, 0, int(0.02 * SR))
    out[:len(strike)] += strike * 0.5
    return out * np.minimum(1, t / 0.002)


def pad(freqs, dur, detune=0.12, cutoff=1400, attack=1.5, release=2.0, brightness=0.5):
    """Warm detuned-saw pad chord."""
    n = int(dur * SR)
    out = np.zeros(n)
    for f in freqs:
        for d in (-detune, 0, detune):
            out += saw(f * 2 ** (d / 12), dur) * 0.33
    lfo = 1 + 0.15 * np.sin(2 * np.pi * 0.2 * t_axis(dur))
    out = lowpass(out, cutoff, 2) * (1 - brightness) + lowpass(out, cutoff * 0.4, 2) * brightness
    out *= lfo
    return out * adsr(n, attack, 0.5, 0.85, release) / max(1, len(freqs))


def reed(freq, dur, vib=5.2, vib_depth=0.006, cutoff=2200, breath=0.05):
    """Concertina-ish reed: saw + square, gentle vibrato and tremolo, a breath of noise."""
    t = t_axis(dur)
    f = freq * (1 + vib_depth * np.sin(2 * np.pi * vib * t) * np.minimum(1, t / 0.4))
    x = saw(f, dur) * 0.6 + square(f * 1.002, dur) * 0.35
    x = lowpass(x, cutoff, 2)
    x *= 1 + 0.08 * np.sin(2 * np.pi * 4.3 * t)
    x += bandpass(white(dur), freq * 2, freq * 6) * breath
    return x * adsr(len(t), 0.06, 0.15, 0.8, 0.25)


def pluck(freq, dur, tau=0.6, bright=4000):
    """Soft harp/guitar-like pluck (Karplus-Strong)."""
    n = int(dur * SR)
    period = int(SR / freq)
    buf = rng.uniform(-1, 1, period)
    out = np.zeros(n)
    decay = np.exp(-1 / (tau * freq))
    for i in range(n):
        j = i % period
        out[i] = buf[j]
        nxt = buf[(i + 1) % period]
        buf[j] = decay * 0.5 * (buf[j] + nxt)
    return lowpass(out, bright, 2)


def music_box(freq, dur=2.5):
    t = t_axis(dur)
    x = np.sin(2 * np.pi * freq * t) * np.exp(-t / 1.1)
    x += 0.35 * np.sin(2 * np.pi * freq * 4.02 * t) * np.exp(-t / 0.25)
    x += 0.2 * np.sin(2 * np.pi * freq * 6.9 * t) * np.exp(-t / 0.12)
    tick = bandpass(white(0.01), 3000, 9000) * np.linspace(1, 0, int(0.01 * SR))
    x[:len(tick)] += tick * 0.3
    return x * np.minimum(1, t / 0.0015)
