"""Player: heartbeat, breathing, hurt, falls, cage. Audio/Player/* (mono)."""
import numpy as np

import common
import dsp
import sfx
from common import sound
from dsp import N, T, SR

ONE = dict(ch=1, norm=("peak", -1.0))


def room(y, rng, rt=0.45, wet=0.12):
    return dsp.reverb(y, rng, rt, wet, bright=4000, dark=900)


@sound("Player/heartbeat", desc="single realistic lub-dub", **ONE)
def heartbeat(rng):
    y = sfx.heartbeat(rng, 47.0)
    y += 0.08 * dsp.pad(dsp.lp(dsp.burst(N(0.03), rng, 0.001, 0.006), 300), len(y))
    return dsp.lofi(y, 12, 1.3, 1.1, 4000)


def _breaths(rng, segs, voice=0.12, f0=150.0, tremble=0.0):
    end = max(s[0] + s[1] for s in segs) + 0.15
    n = N(end)
    y = sfx.breath(n, rng, segs, voice=voice, f0=f0)
    if tremble:
        y = y * (1 - tremble + tremble * (0.5 + 0.5 * np.sin(2 * np.pi * dsp.phase(7.5, n))))
    y = dsp.lp(y, 7000)
    return dsp.lofi(room(y, rng, 0.35, 0.08), 12, 1.3, 1.2, 9000)


@sound("Player/breath_heavy_1", desc="exhausted panting after running", **ONE)
def breath_heavy_1(rng):
    segs, t = [], 0.0
    for k in range(3):
        a, b = rng.uniform(0.3, 0.38), rng.uniform(0.38, 0.48)
        segs += [(t, a, "in", 0.8), (t + a + 0.02, b, "out", 1.0)]
        t += a + b + 0.05
    return _breaths(rng, segs, 0.15, 165)


@sound("Player/breath_heavy_2", desc="deep scared trembling breath", **ONE)
def breath_heavy_2(rng):
    return _breaths(rng, [(0.0, 0.95, "in", 0.85), (1.05, 1.25, "out", 1.0)], 0.1, 150, tremble=0.35)


@sound("Player/breath_heavy_3", desc="shallow hyperventilating breaths", **ONE)
def breath_heavy_3(rng):
    segs, t = [], 0.0
    for k in range(5):
        a, b = rng.uniform(0.17, 0.22), rng.uniform(0.2, 0.26)
        segs += [(t, a, "in", 0.9), (t + a + 0.01, b, "out", 0.8)]
        t += a + b + 0.03
    return _breaths(rng, segs, 0.2, 175)


def _hurt(rng, name, start, dur, ratio, gasp=0.0):
    x = sfx.scream_fragment(name, start, dur, ratio)
    n = len(x) + N(0.4)
    x = dsp.pad(x, n)
    e = dsp.env([(0, 0), (0.012, 1), (dur * 0.4, 0.8), (dur, 0)], n) ** 1.3
    y = dsp.norm(x * e)
    y = dsp.lp(dsp.hp(y, 160), 4200)
    y = dsp.peq(y, 450, 1.0, 3)
    # breathy push behind the voice
    b = sfx.breath(n, rng, [(0.0, dur * 0.9, "out", 1.0)])
    y = dsp.norm(y) + 0.25 * dsp.norm(b)
    if gasp:
        g = sfx.breath(N(0.5), rng, [(0.0, 0.35, "in", 1.0)])
        dsp.place(y, dsp.norm(g) * gasp, N(dur + 0.04))
    y = dsp.sat(dsp.norm(y), 2.0)
    return dsp.lofi(room(y, rng, 0.35, 0.1), 11, 1.4, 1.2, 8000)


@sound("Player/hurt_1", desc="pained grunt (from scream_2, pitched down)", **ONE)
def hurt_1(rng):
    return _hurt(rng, "scream_2", 0.08, 0.38, 0.74)


@sound("Player/hurt_2", desc="sharp cry + gasp (from scream_4)", **ONE)
def hurt_2(rng):
    return _hurt(rng, "scream_4", 0.2, 0.45, 0.68, gasp=0.5)


@sound("Player/hurt_3", desc="choked painful yell (from scream_1)", **ONE)
def hurt_3(rng):
    y = _hurt(rng, "scream_1", 0.05, 0.55, 0.8)
    n = len(y)
    y *= 1 - 0.5 * (np.sin(2 * np.pi * dsp.phase(11.0, n)) > 0.6) * (T(n) > 0.25)
    return y


@sound("Player/body_fall", desc="body hits the floor", **ONE)
def body_fall(rng):
    n = N(1.4)
    y = np.zeros(n)
    main = sfx.strike(n, rng, sfx.wood_modes(rng, 105, 10, 0.12), 0.4, noise_mix=0.5, noise_lp=2500)
    main += 0.9 * sfx.thud(n, rng, 60, 0.12, 0.8, noise=0.6, noise_lp=700)
    dsp.place(y, dsp.norm(main), 0)
    for t, g, f in ((0.09, 0.45, 95), (0.21, 0.3, 120), (0.36, 0.18, 140)):  # limbs / head
        h = sfx.strike(N(0.5), rng, sfx.wood_modes(rng, f, 8, 0.07), 0.45, noise_mix=0.3)
        h += sfx.thud(N(0.5), rng, f * 0.7, 0.04, 0.4)
        dsp.place(y, dsp.norm(h), N(t), g)
    e = dsp.env([(0, 0), (0.02, 1), (0.4, 0.3), (0.8, 0)], n)
    y += 0.2 * sfx.cloth(n, rng, e, 500, 4000, 300)
    y = room(y, rng, 0.6, 0.18)
    return dsp.lofi(dsp.sat(dsp.norm(y), 1.8), 11, 1.4)


def _rattle(rng, n, rate_pts, f_bar=420, mesh=True, env_pts=None):
    t, r = zip(*rate_pts)
    rate = np.interp(T(n), t, r)
    _, pos = dsp.pulse_train(rate, n, rng, 0.35, 0.4)
    y = np.zeros(n)
    bar_modes = dsp.metal_modes(f_bar, 10, rng, tau=(0.08, 0.35))
    for p in pos:
        L = N(0.4)
        h = sfx.strike(L, rng, bar_modes, rng.uniform(0.5, 0.9), noise_mix=0.3)
        if mesh:
            h += 0.6 * sfx.shards(L, rng, 6, 0.0, 0.02, 1500, 5000, (0.01, 0.05), 0.05)
        dsp.place(y, dsp.norm(h), p, rng.uniform(0.4, 1.0))
    if env_pts:
        y *= dsp.env(env_pts, n)
    return y


@sound("Player/cage_rattle", desc="prisoner shaking the cage bars / mesh", **ONE)
def cage_rattle(rng):
    n = N(1.6)
    y = _rattle(rng, n, [(0, 8), (0.8, 12), (1.6, 7)], 380, True,
                [(0, 0.6), (0.3, 1), (1.0, 0.8), (1.4, 0.3), (1.6, 0)])
    y += 0.4 * dsp.pad(sfx.clank(N(0.6), rng, 260, (0.1, 0.4)), n)  # latch banging
    y = room(y, rng, 0.6, 0.15)
    return dsp.lofi(dsp.sat(dsp.norm(y), 1.6), 11, 1.4)


@sound("Player/struggle", desc="struggling against a grip: grunts, cloth, scuffs", **ONE)
def struggle(rng):
    n = N(2.2)
    y = np.zeros(n)
    e = dsp.env([(0, 0), (0.1, 1), (1.0, 0.7), (1.6, 1), (2.2, 0)], n)
    y += 0.5 * sfx.cloth(n, rng, e, 400, 4500, 500)
    for t, nm, st, ratio in ((0.1, "scream_2", 0.3, 0.66), (0.85, "scream_4", 0.4, 0.62), (1.5, "scream_3", 0.2, 0.7)):
        g = sfx.scream_fragment(nm, st, 0.32, ratio)
        g = dsp.lp(g * dsp.env([(0, 0), (0.02, 1), (0.32, 0)], len(g)), 2500)
        dsp.place(y, dsp.norm(g), N(t), 0.55)
    for t in (0.3, 0.7, 1.2, 1.8):  # feet scuffing / kicking
        m = N(0.2)
        sc = sfx.scrape(m, rng, dsp.env([(0, 0), (0.01, 1), (0.2, 0)], m), 300, 4000, 600)
        dsp.place(y, sc + 0.6 * sfx.thud(m, rng, 80, 0.03, 0.3), N(t + rng.uniform(-0.05, 0.05)), 0.5)
    y = room(y, rng, 0.5, 0.12)
    return dsp.lofi(dsp.sat(dsp.norm(y), 1.6), 11, 1.4)


@sound("Player/gasp", desc="sharp frightened inhale", **ONE)
def gasp(rng):
    n = N(0.7)
    b = sfx.breath(n, rng, [(0.0, 0.42, "in", 1.0)])
    b *= dsp.env([(0, 0.2), (0.03, 1), (0.42, 0.5), (0.5, 0)], n)
    v = sfx.scream_fragment("scream_3", 0.25, 0.09, 0.85)
    v = dsp.hp(v * np.hanning(len(v)), 300)
    y = dsp.norm(b)
    dsp.place(y, dsp.norm(v), N(0.005), 0.3)
    y = room(dsp.lp(y, 7500), rng, 0.3, 0.08)
    return dsp.lofi(y, 12, 1.3)


# ---- iteration 2: hiding under beds
def _body_drag(rng, dur, rough=1.0):
    n = N(dur)
    e = dsp.env([(0, 0), (0.08, 1), (dur * 0.6, 0.85), (dur, 0)], n) * np.exp(0.35 * dsp.smooth_rand(n, rng, 6.0, circular=False))
    y = sfx.cloth(n, rng, e, 300, 3500, 120)          # clothes on floorboards
    y += 0.5 * rough * sfx.scrape(n, rng, e, 150, 1800, 300)
    for t0 in rng.uniform(0.05, dur - 0.1, 3):         # knees / elbows knocking the boards
        dsp.place(y, dsp.lp(sfx.thud(N(0.2), rng, rng.uniform(80, 120), 0.03, 0.4, noise=0.4), 1500), N(t0), rng.uniform(0.3, 0.6))
    return dsp.norm(y)


@sound("Player/bed_crawl_in", desc="dropping to the floor and sliding under a bed: cloth on boards, knee knocks, held breath", **ONE)
def bed_crawl_in(rng):
    n = N(1.4)
    y = np.zeros(n)
    dsp.place(y, sfx.thud(N(0.3), rng, 75, 0.05, 0.5, noise=0.5), 0, 0.6)  # knees hit the floor
    dsp.place(y, _body_drag(rng, 1.0), N(0.18), 0.9)
    dsp.place(y, sfx.breath(N(0.5), rng, [(0.0, 0.4, "in", 0.5)]), N(0.9), 0.35)
    y = dsp.lp(y, 6000) + 0.02 * dsp.norm(dsp.band_noise(n, rng, 1500, 9000))
    return dsp.lofi(room(y, rng, 0.4, 0.1), 10, 1.5, 1.3, 7500)


@sound("Player/bed_crawl_out", desc="sliding out from under a bed and getting up", **ONE)
def bed_crawl_out(rng):
    n = N(1.3)
    y = np.zeros(n)
    dsp.place(y, _body_drag(rng, 0.8), 0, 0.9)
    dsp.place(y, sfx.cloth(N(0.4), rng, dsp.env([(0, 0), (0.1, 1), (0.4, 0)], N(0.4)), 500, 4000, 200), N(0.85), 0.5)
    y = dsp.lp(y, 6000) + 0.02 * dsp.norm(dsp.band_noise(n, rng, 1500, 9000))
    return dsp.lofi(room(y, rng, 0.4, 0.1), 10, 1.5, 1.3, 7500)
