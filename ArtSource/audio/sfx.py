"""Sound effects and ambience loops for Last Light."""
import numpy as np

from dsp import (SR, adsr, bandpass, bell, brown, compress, expdecay, fade, fm_bell, glide, highpass, loopify, lowpass, mix, note_freq,
                 pan, pink, place, resonator, reverb, rng, saturate, saw, seconds, sine, square, sweep_lowpass, t_axis, white, write)


# ------------------------------------------------------------------ ambience loops

def amb_sea():
    dur = 34.0
    t = t_axis(dur)
    base = lowpass(brown(dur), 420, 2) * 0.9 + lowpass(pink(dur), 900, 2) * 0.25
    # Swells: slow sets of waves.
    swell = 0.55 + 0.45 * np.sin(2 * np.pi * t / 7.3) ** 2 * (0.7 + 0.3 * np.sin(2 * np.pi * t / 23.0))
    left = base * swell
    right = lowpass(brown(dur), 420, 2) * 0.9 * (0.55 + 0.45 * np.sin(2 * np.pi * t / 7.3 + 1.1) ** 2)
    out = np.stack([left, right])
    # Waves washing on rocks: bursts of bright noise.
    for i in range(14):
        at = rng.uniform(0, dur - 4)
        d = rng.uniform(2.0, 3.6)
        wash = bandpass(white(d), 500, 5000) * (adsr(int(d * SR), 0.5, 0.6, 0.5, d - 1.2) ** 1.5)
        wash = sweep_lowpass(wash, 5000, 900)
        out = place(out, pan(wash * rng.uniform(0.15, 0.35), rng.uniform(-0.8, 0.8)), at)
    out = out[:, :int(dur * SR)]
    return loopify(out, 2.0)


def amb_wind():
    dur = 24.0
    t = t_axis(dur)
    n = pink(dur)
    gust = 0.5 + 0.5 * np.sin(2 * np.pi * t / 6.1) * np.sin(2 * np.pi * t / 15.7)
    x = bandpass(n, 250, 2500) * (0.4 + 0.6 * gust)
    whistle = resonator(white(dur), 720, 30) * 0.03 * gust + resonator(white(dur), 1180, 40) * 0.02 * (1 - gust)
    out = np.stack([x + whistle, bandpass(pink(dur), 250, 2500) * (0.4 + 0.6 * gust[::-1]) + whistle * 0.6])
    return loopify(out, 2.0)


def amb_rain():
    dur = 16.0
    n = int(dur * SR)
    out = np.zeros((2, n))
    hiss = highpass(white(dur), 3000) * 0.08
    out += np.stack([hiss, highpass(white(dur), 3000) * 0.08])
    for c in range(2):
        idx = rng.integers(0, n - 400, 9000)
        drops = np.zeros(n)
        drops[idx] = rng.uniform(0.1, 0.6, len(idx)) * rng.choice([-1, 1], len(idx))
        out[c] += bandpass(drops, 1500, 9000)
    out += np.stack([lowpass(brown(dur), 300) * 0.25] * 2)
    return loopify(out, 1.5)


def lens_whirr():
    dur = 4.0
    t = t_axis(dur)
    hum = (np.sin(2 * np.pi * 62 * t) * 0.5 + np.sin(2 * np.pi * 124 * t) * 0.25 + np.sin(2 * np.pi * 186.5 * t) * 0.12)
    hum *= 1 + 0.2 * np.sin(2 * np.pi * 2 * t)
    rumble = lowpass(brown(dur), 160) * 0.5
    ticks = np.zeros(len(t))
    for k in range(int(dur * 8)):
        i = int(k / 8 * SR)
        click = bandpass(white(0.012), 1800, 6000) * np.linspace(1, 0, int(0.012 * SR))
        ticks[i:i + len(click)] += click * (0.4 if k % 2 else 0.25)
    gear = bandpass(white(dur), 300, 900) * 0.12 * (0.6 + 0.4 * np.sin(2 * np.pi * 16 * t))
    x = hum * 0.5 + rumble + ticks + gear
    return loopify(x, 0.3)


def lens_focus():
    dur = 3.0
    t = t_axis(dur)
    crackle = bandpass(white(dur), 2000, 7000)
    am = np.abs(lowpass(white(dur), 40)) * 4
    sizzle = crackle * np.clip(am, 0, 1) * 0.5
    hum = np.sin(2 * np.pi * 100 * t) * 0.15 + np.sin(2 * np.pi * 200 * t) * 0.08
    return loopify(sizzle + hum, 0.3)


def radio_static():
    dur = 5.0
    x = bandpass(white(dur), 500, 3500) * 0.4
    pops = np.zeros(len(x))
    idx = rng.integers(0, len(x) - 50, 120)
    pops[idx] = rng.uniform(-1, 1, len(idx))
    x += bandpass(pops, 800, 4000) * 0.8
    return loopify(x, 0.3)


# ------------------------------------------------------------------ radio & ui

def radio_squelch():
    d = 0.28
    x = bandpass(white(d), 600, 4000) * expdecay(d, 0.08)
    click = np.zeros(int(0.01 * SR)); click[0] = 1
    x[:len(click)] += lowpass(click, 3000) * 4
    return fade(x, 0.001, 0.05)


def radio_tick():
    d = 0.03
    x = bandpass(white(d), 1500, 6000) * expdecay(d, 0.006)
    x += np.sin(2 * np.pi * 2300 * t_axis(d)) * expdecay(d, 0.004) * 0.3
    return x


def radio_letter():
    d = 0.9
    out = seconds(d)
    for k in range(5):
        at = k * 0.13 + rng.uniform(0, 0.04)
        seg = bandpass(white(0.12), 1500, 7000) * adsr(int(0.12 * SR), 0.01, 0.04, 0.4, 0.06)
        out = place(out, seg * rng.uniform(0.4, 1), at)
    return out


def ui_hover():
    d = 0.08
    t = t_axis(d)
    x = np.sin(2 * np.pi * 1850 * t) * expdecay(d, 0.015) * 0.4 + bandpass(white(d), 3000, 8000) * expdecay(d, 0.004) * 0.3
    return x


def ui_click():
    d = 0.35
    x = bandpass(white(d), 800, 5000) * expdecay(d, 0.006) * 0.8
    x += fm_bell(note_freq("E6"), d, ratio=2.0, index=1.2, tau=0.12) * 0.35
    x += np.sin(2 * np.pi * 180 * t_axis(d)) * expdecay(d, 0.03) * 0.4
    return reverb(x, 0.15, 0.8, 0.3)


def ui_tick():
    d = 0.04
    return bandpass(white(d), 2000, 7000) * expdecay(d, 0.005) + np.sin(2 * np.pi * 3000 * t_axis(d)) * expdecay(d, 0.006) * 0.3


def ui_hint():
    out = seconds(1.6)
    out = place(out, fm_bell(note_freq("A5"), 1.4, 2.0, 1.4, 0.5) * 0.5, 0)
    out = place(out, fm_bell(note_freq("D6"), 1.2, 2.0, 1.2, 0.45) * 0.4, 0.12)
    return reverb(out, 0.35, 1.8, 0.5)


def ui_page():
    d = 0.5
    x = bandpass(white(d), 1200, 7000) * adsr(int(d * SR), 0.05, 0.15, 0.4, 0.25)
    x = sweep_lowpass(x, 8000, 2000)
    return x


def ui_begin():
    d = 2.6
    t = t_axis(d)
    whoosh = sweep_lowpass(bandpass(white(d), 200, 6000) * adsr(int(d * SR), 0.6, 0.4, 0.3, 1.4), 400, 6000)
    chord = sum(np.sin(2 * np.pi * note_freq(n) * t) for n in ("D3", "A3", "D4", "F#4")) * adsr(int(d * SR), 0.8, 0.5, 0.5, 1.2) * 0.15
    return reverb(whoosh * 0.6 + chord, 0.4, 2.5, 0.8)


def lamp(i):
    notes = ["C5", "E5", "G5"]
    f = note_freq(notes[i])
    x = mix(fm_bell(f, 2.6, 3.5, 2.5, 1.0) * 0.6, fm_bell(f * 2, 2.0, 2.0, 1.0, 0.6) * 0.25)
    x += np.sin(2 * np.pi * f * 0.5 * t_axis(2.6)) * expdecay(2.6, 0.8) * 0.2
    return reverb(x, 0.35, 2.5, 0.7)


# ------------------------------------------------------------------ ships

def horn(freqs, dur, breath=0.15, cutoff=1800, vib=0.004):
    t = t_axis(dur)
    x = np.zeros(len(t))
    for f in freqs:
        fv = f * (1 + vib * np.sin(2 * np.pi * 4.5 * t))
        x += saw(fv, dur) * 0.6 + square(fv, dur) * 0.3
    x = lowpass(x, cutoff, 2)
    x += bandpass(white(dur), 400, 3000) * breath
    env = adsr(len(t), 0.08, 0.1, 0.85, 0.35)
    return saturate(x * env * 0.5, 1.3)


def horn_trawler():
    x = horn([note_freq("A3"), note_freq("A3") * 1.005], 0.9, 0.2, 1600)
    x = np.concatenate([x, seconds(0.12), horn([note_freq("A3")], 0.5, 0.2, 1600)])
    return reverb(lowpass(x, 3000), 0.5, 3.0, 1.2, 5000)


def horn_steamer():
    x = horn([note_freq("A2"), note_freq("E3"), note_freq("A3") * 0.997], 2.4, 0.35, 900, 0.002)
    return reverb(lowpass(x, 1800), 0.55, 3.5, 1.5, 4000)


def horn_ferry():
    a = horn([note_freq("D4"), note_freq("F#4")], 0.8, 0.15, 2400)
    b = horn([note_freq("A3"), note_freq("D4")], 1.0, 0.15, 2000)
    x = np.concatenate([a, seconds(0.05), b])
    return reverb(x, 0.5, 3.0, 1.2, 5000)


def ship_lit():
    d = 1.2
    f = note_freq("B5")
    x = fm_bell(f, d, 2.0, 1.4, 0.45) * 0.6 + fm_bell(f * 1.5, d, 2.0, 0.8, 0.3) * 0.3
    # A soft swell of warm air under the bell: the light washing over the deck.
    t = t_axis(d)
    swell = bandpass(pink(d), 300, 2400) * adsr(len(t), 0.06, 0.25, 0.25, 0.7) * 0.22
    swell += sine(f * 0.25, d) * adsr(len(t), 0.03, 0.3, 0.2, 0.7) * 0.18
    return reverb(x + swell, 0.35, 1.8, 0.6)


def wreck_sink():
    """The hull going under: a timber groan, then a deep gulp and bubbles."""
    d = 4.0
    out = seconds(d)
    groan_f = glide(92, 61, 1.6, 0.8) * (1 + 0.03 * np.sin(2 * np.pi * 5.5 * t_axis(1.6)))
    groan = lowpass(saw(groan_f, 1.6), 700) * adsr(int(1.6 * SR), 0.25, 0.4, 0.6, 0.8) * 0.35
    groan = resonator(groan, 240, 6) * 0.5 + groan
    out = place(out, groan, 0)
    gulp = sine(glide(150, 48, 0.5, 0.6), 0.5) * expdecay(0.5, 0.16) * 0.9
    gulp += lowpass(white(0.5), 300) * expdecay(0.5, 0.1) * 0.6
    out = place(out, gulp, 1.1)
    for k in range(26):
        at = 1.25 + rng.uniform(0, 1.9) * (k / 26) ** 0.6
        f = rng.uniform(180, 520)
        blip = sine(glide(f, f * 1.8, 0.06), 0.06) * expdecay(0.06, 0.02) * rng.uniform(0.08, 0.22)
        out = place(out, blip, at)
    out = place(out, lowpass(brown(2.5), 220) * expdecay(2.5, 0.8) * 0.4, 1.1)
    return reverb(out, 0.4, 2.5, 0.9, 3000)


def ship_answer():
    """The captain answering the light: two clacks of a signal-lamp shutter."""
    out = seconds(1.3)
    for at in (0.3, 0.62):
        clack = bandpass(white(0.03), 900, 4200) * expdecay(0.03, 0.006) * 0.7
        ping = fm_bell(2350.0, 0.25, 1.37, 0.6, 0.05, 0.02) * 0.12
        out = place(out, clack, at)
        out = place(out, ping, at + 0.002)
    return reverb(out, 0.3, 1.2, 0.5, 6000)


def ship_lost():
    d = 2.2
    t = t_axis(d)
    f = glide(note_freq("D3"), note_freq("C#3"), d)
    x = saw(f, d) * 0.4 + saw(f * 1.059, d) * 0.35 + saw(f * 0.5, d) * 0.3
    x = lowpass(x, 900) * adsr(len(t), 0.25, 0.4, 0.6, 1.1)
    x += lowpass(white(d), 200) * adsr(len(t), 0.1, 0.4, 0.3, 1.2) * 0.4
    return reverb(x, 0.45, 2.5, 0.9, 3000)


def ship_found():
    out = seconds(1.6)
    out = place(out, fm_bell(note_freq("D5"), 1.3, 2.0, 1.2, 0.5) * 0.5, 0)
    out = place(out, fm_bell(note_freq("A5"), 1.2, 2.0, 1.2, 0.5) * 0.45, 0.09)
    return reverb(out, 0.35, 1.8, 0.6)


def lured():
    d = 2.0
    t = t_axis(d)
    f = glide(note_freq("A4"), note_freq("D4"), d, 0.7) * (1 + 0.012 * np.sin(2 * np.pi * 6 * t))
    x = sine(f, d) * 0.6 + sine(f * 1.5, d) * 0.15
    x *= adsr(len(t), 0.3, 0.3, 0.7, 0.9)
    x += lowpass(saw(f * 0.25, d), 500) * adsr(len(t), 0.4, 0.3, 0.5, 0.9) * 0.3
    return reverb(x, 0.55, 3.0, 1.2, 4000)


def arrive():
    out = seconds(3.5)
    out = place(out, bell(note_freq("G4"), 3.0, 1.0) * 0.5, 0)
    out = place(out, bell(note_freq("G4"), 2.5, 0.9) * 0.35, 0.55)
    t = t_axis(3.0)
    chord = sum(np.sin(2 * np.pi * note_freq(n) * t) for n in ("G3", "D4", "B4")) * adsr(len(t), 0.6, 0.6, 0.5, 1.4) * 0.1
    out = place(out, chord, 0.1)
    return reverb(out, 0.4, 3.0, 1.0)


def wreck():
    d = 3.5
    out = seconds(d)
    # Timber crunch: clustered crackles.
    for k in range(40):
        at = rng.uniform(0, 0.6) ** 1.5
        seg = bandpass(white(0.05), rng.uniform(300, 900), rng.uniform(2000, 5000)) * expdecay(0.05, 0.012)
        out = place(out, seg * rng.uniform(0.3, 1.0), at)
    boom = lowpass(white(1.0), 140) * expdecay(1.0, 0.25) * 2.0
    out = place(out, boom, 0)
    # Metal groan.
    t = t_axis(2.5)
    groan = resonator(saw(glide(70, 45, 2.5), 2.5), 300, 8) * adsr(len(t), 0.3, 0.5, 0.5, 1.2) * 0.4
    out = place(out, groan, 0.25)
    splash = sweep_lowpass(bandpass(white(1.8), 300, 7000) * adsr(int(1.8 * SR), 0.05, 0.3, 0.4, 1.2), 7000, 800)
    out = place(out, splash * 0.8, 0.12)
    return reverb(compress(out, 0.5, 4), 0.35, 2.5, 0.9, 5000)


def flare():
    d = 2.4
    t = t_axis(d)
    whoosh = bandpass(white(0.9), 400, 6000) * adsr(int(0.9 * SR), 0.05, 0.3, 0.5, 0.4)
    whoosh = sweep_lowpass(whoosh, 800, 7000)
    out = seconds(d)
    out = place(out, whoosh, 0)
    pop = lowpass(white(0.15), 1500) * expdecay(0.15, 0.03) * 1.5
    out = place(out, pop, 0.85)
    fizz = bandpass(white(1.4), 3000, 9000) * expdecay(1.4, 0.6) * 0.25
    out = place(out, fizz, 0.9)
    return reverb(out, 0.4, 3.0, 1.0)


# ------------------------------------------------------------------ world

def chart():
    d = 1.8
    out = seconds(d)
    for k, n in enumerate(("E6", "B6", "E7")):
        out = place(out, fm_bell(note_freq(n), 1.4, 1.41, 1.2, 0.6) * (0.35 - k * 0.08), k * 0.04)
    hiss = bandpass(white(1.0), 2000, 9000) * adsr(int(SR * 1.0), 0.15, 0.2, 0.3, 0.5) * 0.15
    out = place(out, hiss, 0.05)
    return reverb(out, 0.4, 2.0, 0.7)


def buoy_bell():
    out = seconds(3.2)
    out = place(out, bell(note_freq("D5"), 2.8, 1.1) * 0.6, 0)
    out = place(out, bell(note_freq("D5"), 2.4, 1.0) * 0.4, 0.45)
    return reverb(out, 0.45, 3.0, 1.2)


def buoy_lit():
    d = 1.4
    fw = lowpass(white(0.4), 600) * adsr(int(0.4 * SR), 0.03, 0.1, 0.3, 0.2) * 0.6
    out = seconds(d)
    out = place(out, fw, 0)
    out = place(out, fm_bell(note_freq("G5"), 1.2, 2.0, 1.0, 0.5) * 0.4, 0.05)
    return reverb(out, 0.35, 1.8, 0.6)


def buoy_out():
    out = seconds(1.4)
    out = place(out, fm_bell(note_freq("D5"), 1.2, 2.0, 0.8, 0.45) * 0.4, 0)
    out = place(out, fm_bell(note_freq("A4"), 1.2, 2.0, 0.8, 0.5) * 0.35, 0.12)
    return reverb(out, 0.4, 1.8, 0.6)


def foghorn():
    """The diaphone: a buzzy BEEEE dropping into a grunting OHH."""
    d1, d2 = 2.2, 1.0
    t1 = t_axis(d1)
    f1 = np.full(len(t1), 158.0) * (1 + 0.003 * np.sin(2 * np.pi * 5 * t1))
    pulse = np.sign(np.sin(2 * np.pi * np.cumsum(f1) / SR)) * 0.5 + saw(f1, d1) * 0.5
    a = lowpass(pulse, 1300, 2) * adsr(len(t1), 0.12, 0.2, 0.9, 0.15)
    f2 = glide(150, 92, d2, 0.6)
    pulse2 = np.sign(np.sin(2 * np.pi * np.cumsum(f2) / SR)) * 0.6 + saw(f2, d2) * 0.4
    b = lowpass(pulse2, 700, 2) * adsr(int(d2 * SR), 0.02, 0.2, 0.8, 0.5) * 1.1
    x = np.concatenate([a, b])
    x = resonator(x, 420, 3) * 0.5 + x * 0.6
    x = saturate(x * 0.8, 1.6)
    return reverb(x, 0.55, 5.0, 2.5, 3500)


def thunder(seed):
    d = 6.0
    r = np.random.default_rng(seed)
    t = t_axis(d)
    rumble = lowpass(brown(d), 180, 2) * 2
    env = np.zeros(len(t))
    for k in range(6):
        at = r.uniform(0, 2.5)
        width = r.uniform(0.4, 1.4)
        env += np.exp(-((t - at - width) / width) ** 2) * r.uniform(0.4, 1)
    env = np.convolve(env, np.ones(2000) / 2000, mode="same")
    x = rumble * env
    crack = bandpass(white(0.4), 400, 5000) * expdecay(0.4, 0.06) * (1.5 if seed % 2 else 0.6)
    x[:len(crack)] += crack
    return reverb(x, 0.4, 4.0, 2.0, 2000, seed)


def wrecker_lit():
    d = 2.8
    t = t_axis(d)
    match = bandpass(white(0.25), 1500, 8000) * adsr(int(0.25 * SR), 0.005, 0.05, 0.5, 0.15)
    drone = (saw(note_freq("D2"), d) * 0.5 + saw(note_freq("D#2"), d) * 0.4)
    drone = lowpass(drone, 500) * adsr(len(t), 0.8, 0.5, 0.6, 1.2)
    out = seconds(d)
    out = place(out, match * 0.8, 0)
    out = place(out, drone * 0.5, 0.15)
    return reverb(out, 0.5, 3.0, 1.2, 3000)


def douse_sizzle():
    d = 1.2
    return bandpass(white(d), 2500, 9000) * adsr(int(d * SR), 0.05, 0.2, 0.6, 0.6) * (1 + 0.5 * np.sin(2 * np.pi * 13 * t_axis(d)))


def doused():
    d = 2.0
    out = seconds(d)
    hiss = sweep_lowpass(bandpass(white(1.6), 1000, 9000) * adsr(int(1.6 * SR), 0.01, 0.3, 0.4, 1.0), 9000, 1500)
    out = place(out, hiss, 0)
    thud = lowpass(white(0.3), 200) * expdecay(0.3, 0.06) * 1.2
    out = place(out, thud, 0)
    return reverb(out, 0.3, 2.0, 0.8)


def switch_off():
    d = 3.5
    t = t_axis(d)
    clunk = lowpass(white(0.12), 900) * expdecay(0.12, 0.025) * 2.0
    clunk = mix(clunk, bandpass(white(0.05), 2000, 6000) * expdecay(0.05, 0.008))
    hum_f = glide(100, 40, 2.5, 0.5)
    hum = (sine(hum_f, 2.5) * 0.5 + sine(hum_f * 2, 2.5) * 0.25) * expdecay(2.5, 0.9)
    out = seconds(d)
    out = place(out, clunk, 0)
    out = place(out, hum * 0.6, 0.02)
    return reverb(out, 0.35, 2.5, 1.0, 4000)


def lens_stop():
    d = 6.0
    out = seconds(d)
    tt = 0.0
    k = 0
    while tt < 4.6:
        click = bandpass(white(0.015), 1500, 5000) * expdecay(0.015, 0.004)
        out = place(out, click * (0.6 if k % 2 else 0.4), tt)
        tt += 0.125 * (1 + k * 0.035) ** 1.6
        k += 1
    rumble = lowpass(brown(4.6), 120) * np.linspace(0.6, 0, int(4.6 * SR))
    out = place(out, rumble, 0)
    final = lowpass(white(0.2), 500) * expdecay(0.2, 0.04) * 1.2
    out = place(out, final, tt + 0.1)
    return reverb(out, 0.3, 2.0, 0.8)


def lens_brake():
    """The heavy lens carriage braking after a fast swing: a brass clunk and a short ratchet."""
    d = 1.4
    out = seconds(d)
    thump = lowpass(white(0.18), 170) * expdecay(0.18, 0.05) * 2.2
    out = place(out, thump, 0)
    out = place(out, fm_bell(410.0, 0.9, 2.76, 1.6, 0.22, 0.05) * 0.28, 0.004)
    out = place(out, fm_bell(1230.0, 0.5, 1.41, 1.0, 0.09, 0.03) * 0.12, 0.004)
    tt = 0.07
    for k in range(4):
        click = bandpass(white(0.012), 1400, 5200) * expdecay(0.012, 0.003)
        out = place(out, click * (0.35 - k * 0.07), tt)
        tt += 0.045 + k * 0.03
    return reverb(out, 0.25, 1.4, 0.6, 5000)


def build(only=None):
    jobs = {
        "amb_sea": amb_sea, "amb_wind": amb_wind, "amb_rain": amb_rain, "lens_whirr": lens_whirr, "lens_focus": lens_focus,
        "radio_static": radio_static, "radio_squelch": radio_squelch, "radio_tick": radio_tick, "radio_letter": radio_letter,
        "ui_hover": ui_hover, "ui_click": ui_click, "ui_tick": ui_tick, "ui_hint": ui_hint, "ui_page": ui_page, "ui_begin": ui_begin,
        "lamp_1": lambda: lamp(0), "lamp_2": lambda: lamp(1), "lamp_3": lambda: lamp(2),
        "horn_trawler": horn_trawler, "horn_steamer": horn_steamer, "horn_ferry": horn_ferry,
        "ship_lit": ship_lit, "ship_lost": ship_lost, "ship_found": ship_found, "lured": lured, "arrive": arrive,
        "wreck": wreck, "flare": flare, "chart": chart, "buoy_bell": buoy_bell, "buoy_lit": buoy_lit, "buoy_out": buoy_out,
        "foghorn": foghorn, "thunder_1": lambda: thunder(1), "thunder_2": lambda: thunder(2), "thunder_3": lambda: thunder(3),
        "wrecker_lit": wrecker_lit, "douse_sizzle": douse_sizzle, "doused": doused, "switch_off": switch_off, "lens_stop": lens_stop,
        "lens_brake": lens_brake, "ship_answer": ship_answer, "wreck_sink": wreck_sink,
    }
    if only:
        jobs = {k: v for k, v in jobs.items() if k in only}
    peaks = {"amb_sea": 0.7, "amb_wind": 0.6, "amb_rain": 0.6, "lens_whirr": 0.7, "lens_focus": 0.6, "radio_static": 0.5,
             "ui_hover": 0.6, "radio_tick": 0.6, "ui_tick": 0.5}
    for name, fn in jobs.items():
        x = fn()
        write(name, x, peaks.get(name, 0.89))
        print("sfx", name, f"{x.shape[-1] / SR:.1f}s")
