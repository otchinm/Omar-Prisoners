"""UI: VCR mechanics + menu blips. Audio/UI/* (stereo, 2D)."""
import numpy as np

import dsp
import sfx
from common import sound
from dsp import N, T, SR


def plastic_clunk(rng, n=None, size=1.0, hardness=0.6):
    n = n or N(0.25)
    s = 1.0 / size
    modes = [(185 * s, 0.035, 0.8), (420 * s, 0.028, 1.0), (880 * s, 0.018, 0.7), (1650 * s, 0.01, 0.5),
             (2950 * s, 0.006, 0.4), (4300 * s, 0.004, 0.25)]
    y = sfx.strike(n, rng, modes, hardness, noise_mix=0.35)
    y += 0.5 * sfx.thud(n, rng, 95 * s, 0.03, 0.4, noise=0.2)
    return dsp.norm(y)


def latch(rng, n=None):
    """Small metallic latch / switch click."""
    n = n or N(0.12)
    y = sfx.strike(n, rng, [(2300, 0.012, 1), (3900, 0.008, 0.7), (5600, 0.006, 0.5), (1200, 0.01, 0.4)],
                   0.95, noise_mix=0.4)
    return dsp.norm(y)


def motor(n, rng, f, gear=12.0, brush=0.4, whine=0.25, circular=False):
    """Small DC motor: rotation harmonics, gear whine and commutator brush noise."""
    f = np.broadcast_to(np.asarray(f, float), (n,))
    y = np.zeros(n)
    for k, a in ((1, 1.0), (2, 0.6), (3, 0.35), (4, 0.25), (6, 0.15)):
        y += a * dsp.sine(f * k, n, rng.uniform())
    y += whine * dsp.sine(f * gear, n, rng.uniform()) + 0.5 * whine * dsp.sine(f * gear * 2.01, n, rng.uniform())
    nz = dsp.band_noise(n, rng, 1500, 7000) if circular else dsp.bp(rng.standard_normal(n), 1500, 7000)
    comm = 0.5 * (1 + np.sin(TAU_ * dsp.phase(f * 6, n)))
    y += brush * dsp.norm(nz) * comm
    return dsp.circ_filter(y, dsp._sos("highpass", 140, 2)) if circular else dsp.hp(y, 140, 2)


TAU_ = 2 * np.pi


def bleep(f, dur, rng, glide=None, pw=0.5):
    n = N(dur)
    fr = f if glide is None else np.linspace(f, glide, n)
    y = dsp.square(fr, n, pw) * 0.6 + 0.4 * dsp.sine(fr, n)
    y *= dsp.ar(n, 0.003, dur * 0.35, hold=dur * 0.45)
    y = dsp.lp(y, 5000)
    return dsp.crush(dsp.norm(y) * 0.95, 6)


def finish_ui(y, rng, width=0.25, room=0.07, bits=10):
    y = dsp.lofi(dsp.norm(y), bits, 1.6, 1.2, 10000)
    st = dsp.reverb(y, rng, 0.25, room, stereo=True, predelay=0.002, bright=6000, dark=2000)
    st = st + width * dsp.widen(dsp.pad(y, len(st)), rng, 0.6)
    return st


@sound("UI/ui_move", ch=2, desc="tiny tape-counter tick")
def ui_move(rng):
    n = N(0.09)
    y = dsp.click(n, 3300, 3, 0.0018, rng) + 0.35 * np.roll(dsp.click(n, 1200, 3, 0.004, rng), N(0.006))
    y += 0.04 * dsp.band_noise(n, rng, 3000, 9000) * dsp.expdec(n, 0.015)
    return finish_ui(y, rng, 0.15, 0.05)


@sound("UI/ui_select", ch=2, desc="VCR button clunk + bleep")
def ui_select(rng):
    n = N(0.32)
    y = plastic_clunk(rng, n, 1.0, 0.6) * 0.9
    y += 0.25 * np.roll(dsp.pad(latch(rng), n), N(0.004))
    dsp.place(y, bleep(1245.0, 0.085, rng), N(0.035), 0.4)
    return finish_ui(y, rng)


@sound("UI/ui_back", ch=2, desc="lighter clunk + descending bleep")
def ui_back(rng):
    n = N(0.32)
    y = plastic_clunk(rng, n, 0.8, 0.5) * 0.7
    dsp.place(y, bleep(880.0, 0.11, rng, glide=590.0), N(0.03), 0.35)
    return finish_ui(y, rng)


@sound("UI/ui_error", ch=2, norm=("peak", -2.0), desc="low harsh double buzz")
def ui_error(rng):
    n = N(0.42)
    y = np.zeros(n)
    for st in (0.0, 0.17):
        m = N(0.13)
        b = dsp.saw(110.0, m) + dsp.square(116.5, m, 0.3) * 0.7 + 0.5 * dsp.saw(55.3, m)
        b = dsp.sat(dsp.norm(b), 3.0) * dsp.ar(m, 0.004, 0.03, hold=0.09)
        dsp.place(y, b, N(st))
    y = dsp.lp(y, 2600)
    y = dsp.crush(dsp.norm(y), 6)
    return finish_ui(y, rng, 0.1, 0.05, bits=8)


@sound("UI/ui_type", ch=2, desc="key / typewriter click")
def ui_type(rng):
    n = N(0.1)
    y = dsp.click(n, 2600, 2, 0.0015, rng)
    y += 0.7 * sfx.strike(n, rng, [(420, 0.012, 1.0), (930, 0.008, 0.8), (1900, 0.005, 0.5)], 0.7, noise_mix=0.2)
    y += 0.06 * dsp.sine(4600, n) * dsp.expdec(n, 0.02, 0.003)
    return finish_ui(y, rng, 0.12, 0.05)


@sound("UI/tape_insert", ch=2, desc="VHS cassette pushed in, loading motor, heads engage")
def tape_insert(rng):
    n = N(1.7)
    y = np.zeros(n)
    dsp.place(y, latch(rng), 0, 0.35)  # flap
    m = N(0.3)
    e = dsp.env([(0, 0), (0.05, 0.5), (0.28, 1.0), (0.3, 0)], m)
    dsp.place(y, sfx.scrape(m, rng, e, 900, 5000, grit=500) * 0.25, N(0.05))
    dsp.place(y, plastic_clunk(rng, N(0.3), 0.9), N(0.35), 0.9)
    m = N(0.85)
    f = np.interp(T(m), [0, 0.15, 0.85], [35, 95, 105])
    mot = motor(m, rng, f, 14, 0.5, 0.2) * dsp.env([(0, 0), (0.1, 1), (0.75, 1), (0.85, 0)], m)
    ratchet, _ = dsp.pulse_train(np.interp(T(m), [0, 0.8], [6, 16]), m, rng, 0.2, 0.3)
    ratchet = dsp.reson(ratchet, 2100, 6) * 3
    dsp.place(y, dsp.norm(mot) * 0.3 + 0.25 * dsp.norm(ratchet), N(0.45))
    dsp.place(y, plastic_clunk(rng, N(0.35), 1.2, 0.8), N(1.3), 1.0)
    dsp.place(y, latch(rng), N(1.31), 0.4)
    return finish_ui(y, rng, 0.2, 0.08)


@sound("UI/tape_eject", ch=2, desc="motor unload, clunk, spring pop, cassette slides out")
def tape_eject(rng):
    n = N(1.4)
    y = np.zeros(n)
    m = N(0.7)
    f = np.interp(T(m), [0, 0.1, 0.7], [40, 100, 90])
    mot = motor(m, rng, f, 11, 0.5, 0.25) * dsp.env([(0, 0), (0.06, 1), (0.6, 1), (0.7, 0)], m)
    dsp.place(y, dsp.norm(mot) * 0.3, 0)
    dsp.place(y, plastic_clunk(rng, N(0.3), 1.1, 0.8), N(0.72), 1.0)
    m = N(0.25)
    pop = dsp.sine(np.linspace(900, 620, m), m) * dsp.expdec(m, 0.05) * 0.4
    dsp.place(y, pop, N(0.74))
    m = N(0.3)
    e = dsp.env([(0, 1), (0.25, 0.4), (0.3, 0)], m)
    dsp.place(y, sfx.scrape(m, rng, e, 900, 5000, grit=400) * 0.22, N(0.78))
    dsp.place(y, latch(rng), N(1.1), 0.35)
    return finish_ui(y, rng, 0.2, 0.08)


@sound("UI/tape_play", ch=2, desc="piano-key clunk + capstan spin-up")
def tape_play(rng):
    n = N(1.1)
    y = plastic_clunk(rng, n, 1.3, 0.85)
    y += 0.4 * dsp.pad(latch(rng), n)
    m = N(0.9)
    f = np.interp(T(m), [0, 0.35, 0.9], [8, 30, 30.5])
    mot = motor(m, rng, f, 20, 0.25, 0.3) * dsp.env([(0, 0), (0.1, 1), (0.7, 0.8), (0.9, 0)], m)
    hiss = dsp.band_noise(m, rng, 2500, 9000) * dsp.env([(0, 0), (0.4, 1), (0.9, 0)], m)
    dsp.place(y, 0.35 * dsp.norm(mot) + 0.05 * hiss, N(0.06))
    return finish_ui(y, rng, 0.2, 0.08)


@sound("UI/tape_stop", ch=2, desc="clunk + motor wind-down")
def tape_stop(rng):
    n = N(0.9)
    y = plastic_clunk(rng, n, 1.25, 0.85)
    m = N(0.7)
    f = np.interp(T(m), [0, 0.7], [30, 4]) ** 1.0
    mot = motor(m, rng, f, 20, 0.25, 0.3) * dsp.env([(0, 1), (0.7, 0)], m) ** 1.5
    dsp.place(y, 0.3 * dsp.norm(mot), N(0.01))
    return finish_ui(y, rng, 0.2, 0.08)


@sound("UI/tape_rewind_loop", ch=2, loop=True, norm=("lufs", -17.0, -3.0), desc="high rewind whine (seamless loop)")
def tape_rewind_loop(rng):
    n = N(2.0)
    f = dsp.qfreq(310.0, n)
    wob = 1 + 0.012 * np.sin(2 * np.pi * dsp.phase(dsp.qfreq(2.5, n), n)) + \
        0.004 * np.sin(2 * np.pi * dsp.phase(dsp.qfreq(11.0, n), n))
    # the whine: rotation harmonics with the periodic wobble (integer cycles of the mean frequency)
    y = np.zeros(n)
    for k, a in ((1, 0.5), (2, 1.0), (3, 0.6), (5, 0.45), (8, 0.3), (13, 0.15)):
        ph = np.cumsum(f * k * wob) / SR
        ph -= ph[-1] * np.arange(n) / n - round(ph[-1]) * np.arange(n) / n  # force integer cycles
        y += a * np.sin(2 * np.pi * ph)
    fr = dsp.band_noise(n, rng, 2000, 7000)
    fr *= 1 + 0.6 * np.sin(2 * np.pi * dsp.phase(dsp.qfreq(5.0, n), n))
    ticks = np.zeros(n)
    tick = dsp.click(N(0.01), 2800, 4, 0.002, rng)
    for k in range(20):  # 10 Hz spindle tick
        dsp.place(ticks, tick, int(k * n / 20), 0.6 + 0.4 * rng.uniform(), circular=True)
    y = dsp.norm(y) * 0.6 + 0.25 * dsp.norm(fr) + 0.2 * ticks
    y = dsp.sat(y, 1.5)
    hiss = 0.045 * np.stack([dsp.band_noise(n, rng, 3000, 9000), dsp.band_noise(n, rng, 3000, 9000)], 1)
    return np.stack([y, y], 1) + hiss


@sound("UI/inventory_open", ch=2, desc="bag/cloth rustle + latch click")
def inventory_open(rng):
    n = N(0.55)
    e = dsp.env([(0, 0), (0.03, 1), (0.25, 0.6), (0.5, 0)], n)
    y = sfx.cloth(n, rng, e, 700, 5500, 400) * 0.8
    dsp.place(y, latch(rng), N(0.02), 0.6)
    y += 0.4 * sfx.thud(n, rng, 120, 0.03, 0.3)
    return finish_ui(y, rng, 0.25, 0.06)


@sound("UI/inventory_close", ch=2, desc="short rustle + soft thump")
def inventory_close(rng):
    n = N(0.4)
    e = dsp.env([(0, 0), (0.02, 1), (0.18, 0.3), (0.35, 0)], n)
    y = sfx.cloth(n, rng, e, 600, 4500, 300) * 0.7
    dsp.place(y, sfx.thud(N(0.2), rng, 100, 0.04, 0.4, noise=0.5), N(0.12), 0.8)
    return finish_ui(y, rng, 0.25, 0.06)


@sound("UI/inventory_scroll", ch=2, desc="paper flick + tick")
def inventory_scroll(rng):
    n = N(0.14)
    m = N(0.07)
    fl = dsp.bp(rng.standard_normal(m), 1800, 6500) * dsp.perc(m, 0.004, 0.015)
    y = np.zeros(n)
    dsp.place(y, fl, 0, 0.8)
    y += 0.5 * np.roll(dsp.click(n, 3000, 3, 0.002, rng), N(0.01))
    return finish_ui(y, rng, 0.2, 0.04)
