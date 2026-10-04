"""Grandmother (iteration 2): Audio/Grandma/* - shrieks and screams made from the user's recordings (pitched up,
shaky old voice, torn apart by the tape), mutters, death rattle, wheelchair squeak loop, her TV."""
import numpy as np

import common
import dsp
import sfx
from common import sound
from dsp import N, T, SR, TAU
from gen_world import xfade_loop

ONE = dict(ch=1, norm=("peak", -1.0))
LOUD = dict(ch=1, norm=("loud", -1.2))


def shaky(x, rng, rate=6.0, depth=0.035):
    """Old voice: vibrato + tremolo, a bit irregular."""
    n = len(x)
    wob = 1.0 + depth * (np.sin(TAU * dsp.phase(rate + 0.8 * dsp.smooth_rand(n, rng, 1.5, False), n)) + 0.4 * dsp.smooth_rand(n, rng, 9, False))
    y = dsp.pad(dsp.varispeed(x, wob), n)
    trem = 1.0 - 0.25 * (0.5 + 0.5 * np.sin(TAU * dsp.phase(rate * 0.97, n)))
    return y * trem


def old_shriek(rng, dur, f0s, vowels):
    """Synthetic shrill old-woman voice layer (glottal + formants), very ragged."""
    n = N(dur)
    f0 = np.interp(T(n), np.linspace(0, dur, len(f0s)), f0s)
    g = sfx.glottal(f0, n, rng, jitter=0.06, shimmer=0.4)
    seq = [(dur * i / max(1, len(vowels) - 1), v) for i, v in enumerate(vowels)]
    v = sfx.vowel_track(n, seq, rng, 0.06)
    y = sfx.formant_filter(g, v, bw=(120, 150, 220, 300), gains=(0.9, 1.0, 0.8, 0.5))
    y += 0.25 * dsp.bp(rng.standard_normal(n), 2000, 7000)  # breath / rasp
    return dsp.norm(y) * dsp.env([(0, 0), (0.04, 1), (dur * 0.75, 0.85), (dur, 0)], n)


def tear(y, rng, bits=7, drive=3.2):
    """Loud, distorted, VHS-degraded finish for her voice."""
    y = dsp.peq(dsp.norm(y), 2600, 1.0, 6)      # shrill
    y = dsp.sat(dsp.norm(y) * drive, drive)
    y = dsp.reverb(y, rng, 0.9, 0.3, bright=4000, dark=900)
    return dsp.vhs(y, rng, bits=bits, factor=2.0, drive=2.4, lp_hz=6500, hiss=0.03)


def _scream(rng, start, dur, ratio, vowels):
    x = sfx.scream_fragment("screams_long", start, dur, ratio)
    x = shaky(dsp.norm(x), rng, rng.uniform(5.5, 7.0), 0.04)
    layer = old_shriek(rng, dur, [rng.uniform(780, 900), rng.uniform(950, 1100), rng.uniform(700, 820)], vowels)
    y = dsp.norm(x) + 0.55 * layer
    return tear(y, rng)


@sound("Grandma/scream_1", desc="the old woman screams for Omar: shrill, shaky, torn", **LOUD)
def scream_1(rng):
    return _scream(rng, 1.2, 2.4, 1.42, ["a", "ae", "a"])


@sound("Grandma/scream_2", desc="second scream variant: higher, cracking", **LOUD)
def scream_2(rng):
    return _scream(rng, 7.4, 2.2, 1.55, ["e", "a", "uh"])


@sound("Grandma/scream_3", desc="third scream variant: long wail into a sob", **LOUD)
def scream_3(rng):
    return _scream(rng, 13.0, 2.8, 1.36, ["a", "o", "uh"])


@sound("Grandma/spot", desc="sudden shriek when she first sees someone (stutters on the tape)", **LOUD)
def spot(rng):
    x = sfx.scream_fragment("scream_4", 0.05, 1.3, 1.62)
    x = shaky(dsp.norm(x), rng, 7.5, 0.03)
    head = x[:N(0.07)].copy()
    y = np.zeros(N(1.6))
    for k in range(3):  # the tape catches on the attack
        dsp.place(y, head * (0.6 + 0.2 * k), N(0.07 * k))
    dsp.place(y, x, N(0.21))
    y += 0.5 * dsp.pad(old_shriek(rng, 1.2, [1100, 1250, 900], ["i", "e", "a"]), len(y)) * 0
    dsp.place(y, old_shriek(rng, 1.2, [1050, 1250, 880], ["i", "e", "a"]), N(0.21), 0.6)
    return tear(y, rng, bits=6, drive=3.6)


def _mutter(rng, dur, words):
    n = N(dur)
    out = np.zeros(n)
    t = 0.08
    for _ in range(words):
        d = rng.uniform(0.14, 0.32)
        m = N(d)
        f0 = rng.uniform(175, 215) * np.linspace(1.04, 0.94, m)
        g = sfx.glottal(f0, m, rng, jitter=0.05, shimmer=0.35)
        vw = ["a", "e", "o", "uh", "u", "ae"]
        v = sfx.vowel_track(m, [(0, vw[rng.integers(len(vw))]), (d, vw[rng.integers(len(vw))])], rng, 0.05)
        y = sfx.formant_filter(g, v, gains=(1.0, 0.7, 0.35, 0.15))
        y = dsp.norm(y) * dsp.env([(0, 0), (d * 0.25, 1), (d, 0)], m)
        y = shaky(y, rng, 6.0, 0.03)
        dsp.place(out, y, N(t))
        if rng.uniform() < 0.4:
            sm = N(rng.uniform(0.05, 0.1))
            dsp.place(out, dsp.bp(rng.standard_normal(sm), 3500, 8000) * np.hanning(sm), N(t + d), 0.25)
        t += d + rng.uniform(0.03, 0.2)
        if t > dur - 0.3:
            break
    out += 0.15 * sfx.breath(n, rng, [(dur - 0.5, 0.45, "out", 0.8)])
    out = dsp.lp(out, 3200)
    out = dsp.reverb(dsp.norm(out), rng, 0.6, 0.2, bright=3000, dark=800)
    return dsp.vhs(out, rng, bits=9, drive=1.6, lp_hz=5500, hiss=0.03)


@sound("Grandma/mutter_1", desc="mumbling at the TV", **ONE)
def mutter_1(rng):
    return _mutter(rng, 1.8, 7)


@sound("Grandma/mutter_2", desc="short grumble and a sigh", **ONE)
def mutter_2(rng):
    return _mutter(rng, 1.3, 4)


@sound("Grandma/mutter_3", desc="longer muttering, as if talking to someone who isn't there", **ONE)
def mutter_3(rng):
    return _mutter(rng, 2.6, 10)


@sound("Grandma/death", desc="shot: a cut-off scream, a wet rattle, the body slumps in the chair", **LOUD)
def death(rng):
    n = N(2.6)
    y = np.zeros(n)
    x = sfx.scream_fragment("scream_2", 0.1, 0.9, 1.35)
    glide = np.interp(T(len(x)), [0, len(x) / SR], [1.0, 0.7])
    x = dsp.pad(dsp.varispeed(dsp.norm(x), glide), len(x)) * dsp.env([(0, 1), (0.6, 0.8), (0.9, 0)], len(x))
    dsp.place(y, x, 0, 1.0)
    m = N(0.9)  # rattle: voiced fry gated, bubbles
    g = sfx.glottal(np.linspace(90, 60, m), m, rng, 0.1, 0.6)
    fry = g * (0.5 + 0.5 * np.sign(np.sin(TAU * dsp.phase(28, m))))
    dsp.place(y, dsp.lp(dsp.norm(fry), 1200) * dsp.env([(0, 0), (0.1, 1), (0.9, 0)], m), N(0.85), 0.5)
    for k in range(6):
        dsp.place(y, sfx.bubble(rng.uniform(300, 700), rng), N(0.9 + 0.1 * k + rng.uniform(0, 0.05)), 0.25)
    dsp.place(y, sfx.thud(N(0.6), rng, 70, 0.1, 0.6, noise=0.6), N(1.5), 0.7)   # slumps forward
    dsp.place(y, sfx.creak(N(0.5), rng, np.full(N(0.5), 40.0), kind="metal", base=900,
                            amp_env=dsp.env([(0, 0), (0.05, 1), (0.5, 0)], N(0.5))), N(1.52), 0.3)
    return tear(y, rng, bits=7, drive=2.6)


@sound("Grandma/wheelchair_loop", ch=1, loop=True, norm=("lufs", -17.0, -2.0), desc="wheelchair rolling over floorboards: squeaky wheel once per turn, rumble, rattles (loop)")
def wheelchair_loop(rng):
    rev = 1.9  # seconds per wheel turn at ~1 m/s
    m = N(0.3)
    n = N(rev * 2) + m
    y = np.zeros(n)
    rum = dsp.lp(dsp.brown(n, rng), 260) * (0.8 + 0.2 * dsp.smooth_rand(n, rng, 4, False))
    y += 0.6 * dsp.norm(rum)
    for k in range(3):
        t0 = k * rev + rng.uniform(0.0, 0.05)
        sq = sfx.creak(N(0.28), rng, np.interp(T(N(0.28)), [0, 0.14, 0.28], [70, 160, 60]), kind="metal", base=1400,
                       amp_env=dsp.env([(0, 0), (0.05, 1), (0.28, 0)], N(0.28)))
        dsp.place(y, sq, N(t0 + 0.4), 0.45)
        dsp.place(y, sfx.thud(N(0.15), rng, 110, 0.02, 0.3, noise=0.5), N(t0 + 1.2), 0.25)  # floorboard joint
    for t0 in rng.uniform(0, rev * 2, 10):
        dsp.place(y, sfx.strike(N(0.08), rng, dsp.metal_modes(rng.uniform(1600, 2600), 5, rng, tau=(0.01, 0.04)), 0.6), N(t0), 0.12)
    y = dsp.vhs(dsp.norm(y), rng, bits=10, drive=1.4, lp_hz=6500, hiss=0.02)
    return xfade_loop(dsp.norm(y), m)


@sound("Grandma/tv_loop", ch=1, loop=True, norm=("lufs", -18.0, -2.0), desc="her TV: snow, line buzz, a garbled old programme drifting in and out (loop)")
def tv_loop(rng):
    m = N(0.4)
    dur = 9.0
    n = N(dur) + m
    snow = dsp.norm(dsp.band_noise(n, rng, 800, 9000)) * (0.55 + 0.25 * dsp.smooth_rand(n, rng, 0.7, False))
    buzz = dsp.norm(dsp.lp(dsp.square(np.full(n, 59.94), n, 0.08), 3000)) * 0.12
    whine = dsp.sine(np.full(n, 15734.0 / 2), n) * 0.015
    # a garbled programme: an old waltz and a talking voice, through a tiny speaker, fading in and out of the snow
    prog = np.zeros(n)
    notes = [392, 330, 330, 349, 294, 294, 262, 294, 330, 349, 392, 392]
    t = 0.0
    for f in notes * 2:
        d = 0.36
        k = N(d)
        tone = (dsp.sine(np.full(k, f), k) + 0.4 * dsp.sine(np.full(k, f * 2.01), k)) * dsp.env([(0, 0), (0.02, 1), (d, 0.2)], k)
        if N(t) + k < n:
            dsp.place(prog, tone, N(t), 0.5)
        t += d
    for _ in range(5):
        st = rng.uniform(0, dur - 1.5)
        k = N(rng.uniform(0.8, 1.4))
        g = sfx.glottal(np.full(k, rng.uniform(110, 140)), k, rng, 0.05, 0.3)
        v = sfx.vowel_track(k, [(0, "a"), (k / SR * 0.3, "o"), (k / SR * 0.6, "e"), (k / SR, "uh")], rng, 0.1)
        voice = sfx.formant_filter(g, v) * (0.5 + 0.5 * np.abs(np.sin(TAU * dsp.phase(4.5, k))))
        dsp.place(prog, dsp.norm(voice) * 0.6, N(st))
    prog = sfx.radio(prog, rng, 400, 3000, 2.0, 0.05, 6)
    fade = np.clip(0.5 + 0.7 * dsp.smooth_rand(n, rng, 0.35, False), 0, 1)
    y = snow * 0.6 + buzz + whine + dsp.norm(prog) * 0.55 * fade
    y = dsp.vhs(dsp.norm(y), rng, bits=9, drive=1.5, lp_hz=7000, hiss=0.0)
    return xfade_loop(dsp.norm(y), m)
