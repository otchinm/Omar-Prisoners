"""Character voices (iteration 2): Audio/Voices/prisoner1..prisoner7/{hurt_1..3, scream_1..2, breath_1..3, gasp_1..2,
struggle_1..2, death_1} (mono). Seven distinct voices cut from the human screams in the user's recording
(SourceAssets/sound "screams_long", never Omar's own screams), re-pitched and re-shaped per character (pitch, vocal
tract colour, tremble, breathiness), then worn out like old VHS dialogue. Breathing / gasps use the shared breath
model with each character's own voiced pitch."""
import numpy as np

import dsp
import sfx
from common import sound
from dsp import N, T

ONE = dict(ch=1, norm=("peak", -1.0))

# Usable stretches of screams_long (start, length, f0 around): low cries, women's screams, a sustained shriek.
LOW = [(0.50, 1.40)]                                      # f0 ~ 200..380 Hz
HIGH = [(3.62, 2.70), (8.00, 2.00), (12.00, 1.70), (15.00, 1.30), (17.82, 2.20)]   # f0 ~ 520..800 Hz

# per character: re-pitch ratio for the low / high material, tract colour (peak Hz, gain dB), shelf cut, tremble,
# breath voicing pitch, breathiness
VOICES = {
    "prisoner1": dict(low=0.86, high=0.50, peak=(520, 4.0), lp=3600, tremble=0.0, f0=125, airy=0.20),   # young athletic man
    "prisoner2": dict(low=1.30, high=0.94, peak=(1100, 3.0), lp=5200, tremble=0.0, f0=205, airy=0.30),  # young woman
    "prisoner3": dict(low=1.42, high=1.06, peak=(1500, 3.5), lp=5600, tremble=0.08, f0=235, airy=0.35), # younger woman, higher
    "prisoner4": dict(low=0.98, high=0.60, peak=(820, 4.0), lp=4200, tremble=0.18, f0=150, airy=0.30),  # lanky, nervous
    "prisoner5": dict(low=1.18, high=0.84, peak=(900, 3.0), lp=4800, tremble=0.0, f0=180, airy=0.22),   # adult woman, lower
    "prisoner6": dict(low=1.06, high=0.70, peak=(1000, 3.0), lp=4800, tremble=0.06, f0=165, airy=0.28), # teenage boy
    "prisoner7": dict(low=0.74, high=0.44, peak=(380, 5.0), lp=3200, tremble=0.0, f0=100, airy=0.15),   # adult man, deep
}


def colour(y, v, rng, bits=9):
    """Vocal tract colour of the character + old tape dialogue."""
    y = dsp.hp(dsp.norm(y), 120)
    y = dsp.peq(y, v["peak"][0], 1.1, v["peak"][1])
    y = dsp.lp(y, v["lp"])
    if v["tremble"] > 0:
        n = len(y)
        y = y * (1 - v["tremble"] + v["tremble"] * (0.5 + 0.5 * np.sin(2 * np.pi * dsp.phase(7.0 + rng.uniform(-1, 1), n))))
    y = dsp.sat(dsp.norm(y), 1.8)
    y = dsp.reverb(y, rng, 0.35, 0.1, bright=3800, dark=900)
    return dsp.vhs(y, rng, bits=bits, factor=2.0, drive=1.6, lp_hz=5500, hiss=0.025)


def piece(v, rng, dur, high=True, which=None):
    """A fragment of the human screams re-pitched for this character (output length `dur`)."""
    segs = HIGH if high else LOW
    st, ln = segs[which % len(segs)] if which is not None else segs[rng.integers(len(segs))]
    ratio = v["high"] if high else v["low"]
    src_len = dur * ratio
    start = st + rng.uniform(0.0, max(0.0, ln - src_len - 0.05))
    return dsp.norm(sfx.scream_fragment("screams_long", start, dur, ratio))


def push_breath(n, rng, v, dur):
    return sfx.breath(n, rng, [(0.0, dur, "out", 1.0)], voice=0.2, f0=v["f0"])


def hurt(v, rng, k):
    dur = [0.34, 0.5, 0.62][k]
    x = piece(v, rng, dur, high=(k != 0), which=k + 1)
    n = len(x) + N(0.35)
    x = dsp.pad(x, n) * dsp.env([(0, 0), (0.01, 1), (dur * 0.45, 0.75), (dur, 0)], n) ** 1.3
    y = dsp.norm(x) + v["airy"] * dsp.norm(push_breath(n, rng, v, dur * 0.9))
    if k == 1:  # cry then a sharp breath in
        dsp.place(y, dsp.norm(sfx.breath(N(0.45), rng, [(0, 0.3, "in", 1.0)])) * 0.45, N(dur + 0.03))
    return colour(y, v, rng)


def scream(v, rng, k):
    dur = [1.5, 2.1][k]
    x = piece(v, rng, dur, high=True, which=k * 2)
    n = len(x) + N(0.3)
    x = dsp.pad(x, n) * dsp.env([(0, 0), (0.03, 1), (dur * 0.8, 0.85), (dur, 0)], n)
    wob = 1.0 + 0.015 * np.sin(2 * np.pi * dsp.phase(5.5, n))
    y = dsp.pad(dsp.varispeed(x, wob), n)
    y = dsp.norm(y) + 0.5 * v["airy"] * dsp.norm(push_breath(n, rng, v, dur))
    return colour(y, v, rng, bits=8)


def breath(v, rng, k):
    segs, t = [], 0.0
    if k == 0:    # exhausted panting
        for _ in range(3):
            a, b = rng.uniform(0.28, 0.36), rng.uniform(0.36, 0.46)
            segs += [(t, a, "in", 0.8), (t + a + 0.02, b, "out", 1.0)]
            t += a + b + 0.05
    elif k == 1:  # deep scared trembling breath
        segs = [(0.0, 0.9, "in", 0.85), (1.0, 1.15, "out", 1.0)]
    else:         # shallow hyperventilating
        for _ in range(4):
            a, b = rng.uniform(0.16, 0.21), rng.uniform(0.2, 0.25)
            segs += [(t, a, "in", 0.9), (t + a + 0.01, b, "out", 0.8)]
            t += a + b + 0.03
    end = max(s[0] + s[1] for s in segs) + 0.15
    n = N(end)
    y = sfx.breath(n, rng, segs, voice=0.12 + 0.15 * v["airy"], f0=v["f0"])
    if k == 1 or v["tremble"] > 0.1:
        y = y * (0.7 + 0.3 * (0.5 + 0.5 * np.sin(2 * np.pi * dsp.phase(7.5, n))))
    return colour(y, v, rng, bits=10)


def gasp(v, rng, k):
    n = N(0.75)
    y = np.zeros(n)
    g = sfx.breath(N(0.5), rng, [(0, 0.28 + 0.08 * k, "in", 1.0)], voice=0.0)
    dsp.place(y, dsp.norm(g), 0)
    onset = piece(v, rng, 0.22, high=True, which=3 + k) * dsp.env([(0, 0), (0.02, 1), (0.22, 0)], N(0.22))
    dsp.place(y, onset, N(0.2 + 0.05 * k), 0.55)   # a strangled little cry on the end of the breath
    return colour(y, v, rng, bits=9)


def struggle(v, rng, k):
    dur = 1.0 + 0.4 * k
    n = N(dur + 0.3)
    y = np.zeros(n)
    t = 0.03
    while t < dur - 0.2:
        d = rng.uniform(0.14, 0.26)
        x = piece(v, rng, d, high=False) * dsp.env([(0, 0), (0.03, 1), (d, 0)], N(d))
        dsp.place(y, x, N(t), rng.uniform(0.6, 1.0))
        dsp.place(y, dsp.norm(sfx.breath(N(0.3), rng, [(0, 0.2, "in", 1.0)])), N(t + d + 0.02), 0.3)
        t += d + rng.uniform(0.18, 0.32)
    y = dsp.lp(y, 3200)   # strained, through clenched teeth
    return colour(y, v, rng, bits=9)


def death(v, rng):
    x = piece(v, rng, 0.9, high=True, which=0)
    n = N(2.0)
    y = np.zeros(n)
    glide = np.interp(T(len(x)), [0, len(x) / dsp.SR], [1.0, 0.75])
    x = dsp.pad(dsp.varispeed(x, glide), len(x)) * dsp.env([(0, 1), (0.6, 0.7), (0.85, 0)], len(x))
    dsp.place(y, x, 0)
    tail = sfx.breath(N(1.0), rng, [(0.0, 0.8, "out", 0.7)], voice=0.35, f0=v["f0"] * 0.8)
    dsp.place(y, dsp.lp(dsp.norm(tail), 1800), N(0.85), 0.5)   # the last breath goes out of them
    return colour(y, v, rng, bits=8)


def _register():
    for name, v in VOICES.items():
        base = "Voices/" + name + "/"
        for k in range(3):
            sound(base + "hurt_%d" % (k + 1), desc="%s: pained cry" % name, **ONE)((lambda v, k: lambda rng: hurt(v, rng, k))(v, k))
            sound(base + "breath_%d" % (k + 1), desc="%s: heavy breathing" % name, **ONE)((lambda v, k: lambda rng: breath(v, rng, k))(v, k))
        for k in range(2):
            sound(base + "scream_%d" % (k + 1), desc="%s: terrified scream" % name, **ONE)((lambda v, k: lambda rng: scream(v, rng, k))(v, k))
            sound(base + "gasp_%d" % (k + 1), desc="%s: gasp when spotted" % name, **ONE)((lambda v, k: lambda rng: gasp(v, rng, k))(v, k))
            sound(base + "struggle_%d" % (k + 1), desc="%s: straining against a lock / trap" % name, **ONE)((lambda v, k: lambda rng: struggle(v, rng, k))(v, k))
        sound(base + "death_1", desc="%s: last capture" % name, **ONE)((lambda v: lambda rng: death(v, rng))(v))


_register()
