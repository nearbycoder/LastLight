"""Original score for Last Light, synthesized: the Keeper's Waltz (title), a night bed with a
tension layer, dawn, and the ending (the waltz on a music box)."""
import numpy as np

from dsp import (SR, adsr, bell, compress, expdecay, fm_bell, highpass, loopify, lowpass, music_box, note_freq, pad, pan,
                 place, reed, reverb, rng, saturate, saw, seconds, sine, t_axis, white, write)

CHORDS = {
    "Dm": ["D3", "F3", "A3"], "Bb": ["Bb2", "D3", "F3"], "F": ["F2", "A2", "C3"], "C": ["C3", "E3", "G3"],
    "Gm": ["G2", "Bb2", "D3"], "A7": ["A2", "C#3", "E3", "G3"], "Dm/A": ["A2", "D3", "F3"],
    "D": ["D3", "F#3", "A3"], "G": ["G2", "B2", "D3"], "A": ["A2", "C#3", "E3"], "Bm": ["B2", "D3", "F#3"],
    "Dm9": ["D3", "F3", "A3", "C4", "E4"], "Bbmaj7": ["Bb2", "D3", "F3", "A3"], "Fmaj7": ["F2", "A2", "C3", "E3"],
    "Csus2": ["C3", "D3", "G3"], "Em": ["E3", "G3", "B3"],
}

BASS = {"Dm": "D2", "Bb": "Bb1", "F": "F2", "C": "C2", "Gm": "G1", "A7": "A1", "Dm/A": "A1", "D": "D2", "G": "G1", "A": "A1", "Bm": "B1", "Em": "E2"}


def up(name, octaves=1):
    return name[:-1] + str(int(name[-1]) + octaves)


def bass_note(name, dur):
    f = note_freq(name)
    t = t_axis(dur)
    x = np.sin(2 * np.pi * f * t) + 0.35 * np.sin(2 * np.pi * 2 * f * t) + 0.12 * np.sin(2 * np.pi * 3 * f * t)
    return saturate(x * expdecay(dur, 0.9, 0.01) * 0.6, 1.2)


def soft_chord(names, dur, vol=0.12):
    t = t_axis(dur)
    x = np.zeros(len(t))
    for n in names:
        f = note_freq(up(n))
        x += (np.sin(2 * np.pi * f * t) + 0.3 * np.sin(2 * np.pi * 2 * f * t)) * expdecay(dur, 0.35, 0.01)
    return lowpass(x, 2500) * vol


WALTZ_CHORDS = ["Dm", "Bb", "F", "C", "Dm", "Gm", "A7", "Dm", "F", "C", "Dm", "Bb", "Gm", "Dm/A", "A7", "Dm"]
WALTZ_MELODY = [
    [("D5", 2), ("E5", 1)], [("F5", 2), ("D5", 1)], [("C5", 3)], [("E5", 2), ("C5", 1)],
    [("D5", 2), ("A4", 1)], [("Bb4", 1), ("A4", 1), ("G4", 1)], [("A4", 2), ("C#5", 1)], [("D5", 3)],
    [("A5", 2), ("G5", 1)], [("F5", 1), ("E5", 1), ("D5", 1)], [("E5", 2), ("F5", 1)], [("D5", 3)],
    [("G5", 2), ("F5", 1)], [("E5", 1), ("D5", 1), ("C#5", 1)], [("E5", 2), ("C#5", 1)], [("D5", 3)],
]


def waltz(bpm=84, passes=2, lead="reed", music_box_counter=True, major=False):
    beat = 60.0 / bpm
    bar = beat * 3
    total = bar * 16 * passes + bar * 2
    out = np.zeros((2, int(total * SR) + SR * 4))
    t0 = bar  # one bar of intro
    # Intro: a held pad.
    out = place(out, pan(pad([note_freq(n) for n in CHORDS["Dm"]], bar + 0.5, cutoff=1100, attack=0.6, release=0.6) * 0.6, 0), 0)
    for p in range(passes):
        for b in range(16):
            name = WALTZ_CHORDS[b]
            start = t0 + (p * 16 + b) * bar
            chord = CHORDS[name]
            out = place(out, pan(bass_note(BASS[name], beat * 1.4), -0.1), start)
            for k in (1, 2):
                out = place(out, pan(soft_chord(chord, beat * 0.9, 0.09), 0.25), start + k * beat)
            out = place(out, pan(pad([note_freq(n) for n in chord], bar + 0.3, cutoff=1000, attack=0.25, release=0.5, brightness=0.6) * 0.35, -0.3), start)
            # Melody.
            mt = start
            for note, beats in WALTZ_MELODY[b]:
                d = beats * beat
                if lead == "reed":
                    x = reed(note_freq(note), d + 0.08, cutoff=2000 if p == 0 else 2600) * 0.32
                    out = place(out, pan(x, 0.05), mt)
                else:
                    x = music_box(note_freq(up(note)), min(2.5, d + 1.2)) * 0.32
                    out = place(out, pan(x, 0.1), mt)
                mt += d
            # A music-box countermelody on the repeat.
            if music_box_counter and p == 1:
                arp = [up(c, 1) for c in chord[:3]]
                for k, nn in enumerate((arp[0], arp[2], arp[1])):
                    out = place(out, pan(music_box(note_freq(nn), 1.6) * 0.12, 0.5), start + k * beat + beat * 0.5)
    end = t0 + passes * 16 * bar
    out = place(out, pan(pad([note_freq(n) for n in CHORDS["Dm"]], bar * 2, cutoff=900, attack=0.3, release=1.5) * 0.5, 0), end)
    return out, total


def music_title():
    x, total = waltz(84, 2)
    x = reverb(x, 0.35, 3.5, 1.6, 6000)
    x = x[:, :int(total * SR)]
    return loopify(x, 2.0)


NIGHT_CHORDS = ["Dm9", "Bbmaj7", "Fmaj7", "Csus2"]
NIGHT_LEN = 64.0


def music_night():
    dur = NIGHT_LEN
    out = np.zeros((2, int((dur + 6) * SR)))
    seg = dur / 8
    for i in range(8):
        name = NIGHT_CHORDS[i % 4]
        x = pad([note_freq(n) for n in CHORDS[name]], seg + 3.0, detune=0.1, cutoff=900, attack=3.0, release=3.0, brightness=0.7) * 0.55
        out = place(out, pan(x, -0.2 if i % 2 else 0.2), i * seg)
        bass = sine(note_freq(BASS.get(name, "D2") if name in BASS else {"Dm9": "D2", "Bbmaj7": "Bb1", "Fmaj7": "F2", "Csus2": "C2"}[name]), seg + 2)
        bass *= adsr(len(bass), 2.0, 1.0, 0.6, 2.5) * 0.18
        out = place(out, bass, i * seg)
    # Sparse celesta notes from D minor pentatonic.
    pent = ["D5", "F5", "G5", "A5", "C6", "D6", "A4", "C5"]
    t = 1.5
    while t < dur - 3:
        n = rng.choice(pent)
        out = place(out, pan(fm_bell(note_freq(n), 3.0, 3.5, 1.6, 1.3) * rng.uniform(0.07, 0.13), rng.uniform(-0.6, 0.6)), t)
        if rng.random() < 0.35:
            out = place(out, pan(fm_bell(note_freq(rng.choice(pent)), 2.5, 3.5, 1.2, 1.1) * 0.07, rng.uniform(-0.6, 0.6)), t + rng.uniform(0.3, 0.8))
        t += rng.uniform(2.5, 6.0)
    out = reverb(out, 0.5, 4.5, 2.2, 5000)
    out = out[:, :int(dur * SR)]
    return loopify(out, 3.0)


def music_tension():
    dur = NIGHT_LEN
    bpm = 105
    eighth = 60.0 / bpm / 2
    out = np.zeros((2, int((dur + 4) * SR)))
    seg = dur / 8
    roots = {"Dm9": ("D2", "A2"), "Bbmaj7": ("Bb1", "F2"), "Fmaj7": ("F2", "C3"), "Csus2": ("C2", "G2")}
    t = 0.0
    k = 0
    while t < dur:
        name = NIGHT_CHORDS[int(t // seg) % 4]
        r1, r2 = roots[name]
        f = note_freq(r1 if k % 4 != 2 else r2)
        x = lowpass(saw(f, eighth * 0.9) + saw(f * 1.005, eighth * 0.9), 700) * adsr(int(eighth * 0.9 * SR), 0.005, 0.05, 0.5, 0.06) * 0.25
        out = place(out, pan(x, -0.3 if k % 2 else 0.3), t)
        if k % 8 == 0:
            thump = sine(55, 0.35) * expdecay(0.35, 0.09) * 0.5
            out = place(out, thump, t)
        t += eighth
        k += 1
    # Rising dissonant swells.
    for i in range(8):
        name = NIGHT_CHORDS[i % 4]
        root = note_freq(roots[name][0]) * 4
        fr = [root, root * 2 ** (1 / 12), root * 2 ** (6 / 12)]
        sw = pad(fr, seg, detune=0.2, cutoff=1600, attack=seg * 0.7, release=0.5, brightness=0.3) * 0.25
        out = place(out, pan(sw, 0), i * seg)
    out = reverb(out, 0.3, 2.5, 1.0, 5000)
    out = out[:, :int(dur * SR)]
    return loopify(out, 3.0)  # same crossfade as the night bed, so both loops are the same length


def music_dawn():
    dur = 52.0
    out = np.zeros((2, int((dur + 6) * SR)))
    seq = ["D", "G", "Bm", "A", "D", "G", "A", "D"]
    seg = dur / len(seq)
    melody = ["F#5", "A5", "B5", "A5", "F#5", "G5", "E5", "D5"]
    for i, name in enumerate(seq):
        x = pad([note_freq(n) for n in CHORDS[name]], seg + 2.5, detune=0.1, cutoff=1500, attack=2.0, release=2.5, brightness=0.4) * 0.5
        out = place(out, pan(x, 0), i * seg)
        out = place(out, bass_note(BASS[name], seg) * 0.5, i * seg)
        out = place(out, pan(reed(note_freq(melody[i]), seg * 0.8, cutoff=2400) * 0.22, 0.1), i * seg + 0.6)
        for k in range(3):
            out = place(out, pan(fm_bell(note_freq(up(CHORDS[name][k], 2)), 2.5, 3.5, 1.4, 1.2) * 0.06, 0.5 - k * 0.4), i * seg + 1.2 + k * 0.9)
    out = reverb(out, 0.45, 4.0, 2.0, 6000)
    out = out[:, :int(dur * SR)]
    return loopify(out, 3.0)


def music_ending():
    x, total = waltz(64, 1, lead="musicbox", music_box_counter=False)
    beat = 60.0 / 64
    end = int(total * SR)
    # A final D major chord, held long.
    final = pad([note_freq(n) for n in CHORDS["D"]] + [note_freq("D4"), note_freq("F#4")], 10.0, cutoff=1500, attack=1.5, release=6.0) * 0.6
    x = place(x, pan(final, 0), total - beat * 2)
    x = place(x, pan(music_box(note_freq("D6"), 4.0) * 0.3, 0.2), total - beat * 2)
    x = reverb(x, 0.45, 5.0, 2.5, 6000)
    return x[:, :int((total + 9) * SR)]


def build():
    for name, fn in (("music_title", music_title), ("music_night", music_night), ("music_tension", music_tension),
                     ("music_dawn", music_dawn), ("music_ending", music_ending)):
        x = fn()
        write(name, compress(x, 0.6, 2.5), 0.8)
        print("music", name, f"{x.shape[-1] / SR:.1f}s")
