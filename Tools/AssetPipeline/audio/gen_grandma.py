"""Grandmother (iteration 2): Audio/Grandma/* - shrieks and screams cut from the women's screams in the user's
recording (natural pitch, made old with an uneven vibrato, tremolo and a wet rasp, torn by the tape; no synthetic voice
layers and never Omar's screams), breathy mutters, death gurgle, wheelchair squeak loop, her TV."""
import numpy as np

import common
import dsp
import sfx
from common import sound
from dsp import N, T, SR, TAU
from gen_world import xfade_loop

ONE = dict(ch=1, norm=("peak", -1.0))
LOUD = dict(ch=1, norm=("loud", -1.2))
LOUDER = dict(ch=1, norm=("loud", -0.3))


def shaky(x, rng, rate=6.0, depth=0.035):
    """Old voice: vibrato + tremolo, a bit irregular."""
    n = len(x)
    wob = 1.0 + depth * (np.sin(TAU * dsp.phase(rate + 0.8 * dsp.smooth_rand(n, rng, 1.5, False), n)) + 0.4 * dsp.smooth_rand(n, rng, 9, False))
    y = dsp.pad(dsp.varispeed(x, wob), n)
    trem = 1.0 - 0.25 * (0.5 + 0.5 * np.sin(TAU * dsp.phase(rate * 0.97, n)))
    return y * trem


def rasp(x, rng, amount=0.35):
    """Old, wet throat: band noise riding the voice envelope + a little irregular crackle (phlegm)."""
    n = len(x)
    e = dsp.lp(np.abs(x), 30)
    e = e / (e.max() + 1e-9)
    hiss = dsp.bp(rng.standard_normal(n), 1800, 6500) * e
    wet = dsp.lp(dsp.crackle(n, rng, 22.0), 2500) * e
    return x + amount * dsp.norm(hiss) * 0.6 + amount * dsp.norm(wet) * 0.35


def old_voice(x, rng, wobble=0.05, rate=6.2):
    """A real scream made old: uneven vibrato + tremolo and a wet rasp."""
    return rasp(shaky(dsp.norm(x), rng, rate + rng.uniform(-0.5, 0.5), wobble), rng)


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


def tear(y, rng, bits=7, drive=2.4):
    """Loud, distorted, VHS-degraded finish for her voice (a real voice, just worn out by the tape)."""
    y = dsp.bp(dsp.norm(y), 260, 6000, 2)
    y = dsp.peq(y, 2400, 1.2, 4)               # shrill presence
    y = dsp.asym_sat(dsp.norm(y) * drive, drive, 0.15)
    y = dsp.reverb(y, rng, 0.7, 0.25, bright=3800, dark=900)
    return dsp.vhs(y, rng, bits=bits, factor=2.0, drive=2.0, lp_hz=6000, hiss=0.03)


def trash(y, rng, bits=5, drive=4.5):
    """Like Omar's own recordings: blown-out, clipped, crushed to a few bits and a low sample rate - just female."""
    y = dsp.peq(dsp.norm(y), 2600, 1.0, 6)      # shrill
    y = dsp.sat(dsp.norm(y) * drive, drive)
    y = dsp.hardclip(dsp.norm(y) * 1.6, 0.7)
    y = dsp.reverb(y, rng, 0.8, 0.25, bright=4000, dark=900)
    y = dsp.vhs(y, rng, bits=bits, factor=3.0, drive=3.0, lp_hz=4800, hiss=0.05)
    return dsp.hold(dsp.crush(dsp.norm(y), bits + 1), 2)


def _scream(rng, start, dur, ratio, vowels):
    """The original grandmother scream (a pitched-up scream + the shrill old-voice layer), now louder and trashier."""
    x = sfx.scream_fragment("screams_long", start, dur, ratio)
    x = shaky(dsp.norm(x), rng, rng.uniform(5.5, 7.0), 0.04)
    layer = old_shriek(rng, dur, [rng.uniform(780, 900), rng.uniform(950, 1100), rng.uniform(700, 820)], vowels)
    y = dsp.norm(x) + 0.55 * layer
    return trash(y, rng)


@sound("Grandma/scream_1", desc="the old woman screams for Omar: shrill, shaky, blown-out like Omar's tapes", **LOUDER)
def scream_1(rng):
    return _scream(rng, 1.2, 2.4, 1.42, ["a", "ae", "a"])


@sound("Grandma/scream_2", desc="second scream variant: higher, cracking", **LOUDER)
def scream_2(rng):
    return _scream(rng, 7.4, 2.2, 1.55, ["e", "a", "uh"])


@sound("Grandma/scream_3", desc="third scream variant: long wail into a sob", **LOUDER)
def scream_3(rng):
    return _scream(rng, 13.0, 2.8, 1.36, ["a", "o", "uh"])


@sound("Grandma/spot", desc="sudden shriek when she first sees someone (the tape catches on the attack)", **LOUDER)
def spot(rng):
    x = sfx.scream_fragment("screams_long", 8.0, 1.3, 1.5)
    x = shaky(dsp.norm(x), rng, 7.5, 0.03)
    head = x[:N(0.07)].copy()
    y = np.zeros(N(1.6))
    for k in range(3):  # the tape catches on the attack
        dsp.place(y, head * (0.6 + 0.2 * k), N(0.07 * k))
    dsp.place(y, x, N(0.21))
    dsp.place(y, old_shriek(rng, 1.2, [1050, 1250, 880], ["i", "e", "a"]), N(0.21), 0.6)
    return trash(y, rng, bits=5, drive=5.0)


def _mutter(rng, dur, words):
    """Unintelligible old woman mumbling: mostly breathy whisper through mouth shapes, a weak trembling voice under it."""
    n = N(dur)
    out = np.zeros(n)
    t = 0.08
    vw = ["a", "e", "o", "uh", "u", "ae"]
    for _ in range(words):
        d = rng.uniform(0.12, 0.3)
        m = N(d)
        v = sfx.vowel_track(m, [(0, vw[rng.integers(len(vw))]), (d, vw[rng.integers(len(vw))])], rng, 0.05)
        breathy = sfx.formant_filter(dsp.pink(m, rng), v, gains=(1.0, 0.8, 0.5, 0.3))
        f0 = rng.uniform(170, 210) * np.linspace(1.05, 0.92, m)
        voiced = sfx.formant_filter(sfx.glottal(f0, m, rng, jitter=0.09, shimmer=0.5), v, gains=(1.0, 0.6, 0.3, 0.1))
        y = dsp.norm(breathy) + 0.35 * dsp.norm(voiced)
        y = dsp.norm(y) * dsp.env([(0, 0), (d * 0.3, 1), (d, 0)], m)
        y = shaky(y, rng, 6.0, 0.04)
        dsp.place(out, y, N(t))
        if rng.uniform() < 0.35:  # lip smack / tongue click
            dsp.place(out, dsp.click(N(0.02), rng.uniform(1800, 3200), 6, 0.003, rng), N(t + d + 0.02), 0.3)
        t += d + rng.uniform(0.04, 0.24)
        if t > dur - 0.35:
            break
    out += 0.2 * sfx.breath(n, rng, [(dur - 0.5, 0.45, "out", 0.8)])
    out = rasp(dsp.lp(out, 3000), rng, 0.25)
    out = dsp.reverb(dsp.norm(out), rng, 0.6, 0.2, bright=3000, dark=800)
    return dsp.vhs(out, rng, bits=9, drive=1.6, lp_hz=5000, hiss=0.03)


@sound("Grandma/mutter_1", desc="mumbling at the TV", **ONE)
def mutter_1(rng):
    return _mutter(rng, 1.8, 7)


@sound("Grandma/mutter_2", desc="short grumble and a sigh", **ONE)
def mutter_2(rng):
    return _mutter(rng, 1.3, 4)


@sound("Grandma/mutter_3", desc="longer muttering, as if talking to someone who isn't there", **ONE)
def mutter_3(rng):
    return _mutter(rng, 2.6, 10)


@sound("Grandma/death", desc="shot: a cut-off scream, a wet gurgling rattle, the body slumps in the chair", **LOUD)
def death(rng):
    n = N(2.6)
    y = np.zeros(n)
    x = sfx.scream_fragment("screams_long", 5.0, 0.85, 0.95)
    glide = np.interp(T(len(x)), [0, len(x) / SR], [1.0, 0.72])
    x = dsp.pad(dsp.varispeed(dsp.norm(x), glide), len(x)) * dsp.env([(0, 1), (0.55, 0.8), (0.85, 0)], len(x))
    dsp.place(y, old_voice(x, rng, wobble=0.03), 0, 1.0)
    m = N(1.0)  # wet rattle: gated low noise + bubbles, no tonal synth
    gate = 0.5 + 0.5 * np.sign(np.sin(TAU * dsp.phase(18 + 6 * dsp.smooth_rand(m, rng, 3, False), m)))
    gurgle = dsp.bp(rng.standard_normal(m), 180, 900) * gate
    dsp.place(y, dsp.norm(gurgle) * dsp.env([(0, 0), (0.1, 1), (1.0, 0)], m), N(0.8), 0.45)
    for k in range(7):
        dsp.place(y, sfx.bubble(rng.uniform(260, 650), rng), N(0.85 + 0.12 * k + rng.uniform(0, 0.05)), 0.3)
    dsp.place(y, sfx.thud(N(0.6), rng, 70, 0.1, 0.6, noise=0.6), N(1.55), 0.7)   # slumps forward
    dsp.place(y, sfx.creak(N(0.5), rng, np.full(N(0.5), 40.0), kind="metal", base=900,
                            amp_env=dsp.env([(0, 0), (0.05, 1), (0.5, 0)], N(0.5))), N(1.57), 0.3)
    return tear(y, rng, bits=7, drive=2.2)


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
        # the dry wheel squeals once per turn (rising, then dying away) - you know it is her
        sq = sfx.creak(N(0.42), rng, np.interp(T(N(0.42)), [0, 0.18, 0.42], [90, 230, 70]), kind="metal", base=1650,
                       amp_env=dsp.env([(0, 0), (0.04, 1), (0.3, 0.7), (0.42, 0)], N(0.42)))
        dsp.place(y, sq, N(t0 + 0.4), 0.9)
        sq2 = sfx.creak(N(0.2), rng, np.interp(T(N(0.2)), [0, 0.2], [140, 60]), kind="metal", base=1100,
                        amp_env=dsp.env([(0, 0), (0.03, 1), (0.2, 0)], N(0.2)))
        dsp.place(y, sq2, N(t0 + 1.25), 0.45)
        dsp.place(y, sfx.thud(N(0.15), rng, 110, 0.02, 0.3, noise=0.5), N(t0 + 1.2), 0.3)  # floorboard joint
    for t0 in np.arange(0.07, rev * 2, 0.19):  # loose footrest rattling on every bump
        dsp.place(y, sfx.strike(N(0.06), rng, dsp.metal_modes(rng.uniform(1300, 1900), 4, rng, tau=(0.008, 0.025)), 0.7), N(t0 + rng.uniform(0, 0.04)), rng.uniform(0.05, 0.14))
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
