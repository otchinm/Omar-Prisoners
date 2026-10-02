"""World: doors, machines, electrics, radio, car, explosion... Audio/World/* (mono 3D one-shots)."""
import numpy as np

import dsp
import sfx
from common import sound
from dsp import N, T, SR, TAU
from gen_items import latch_clack, small_metal
from gen_ui import bleep

ONE = dict(ch=1, norm=("peak", -1.0))
LOFI22 = dict(ch=1, sr=22050, norm=("peak", -1.0))


def verb(y, rng, rt=0.8, wet=0.25, bright=4000, dark=900, **kw):
    return dsp.reverb(y, rng, rt, wet, bright=bright, dark=dark, **kw)


def fin(y, rng, rt=0.7, wet=0.2, bits=11, drive=1.3, sat=0.0):
    y = dsp.norm(y)
    if sat:
        y = dsp.sat(y, sat)
    return dsp.lofi(verb(y, rng, rt, wet), bits, 1.4, drive, 10000)


# --------------------------------------------------------------------------------------------- doors
def handle(rng):
    """Door handle turned: latch bolt retracts with a spring."""
    n = N(0.25)
    y = latch_clack(rng, n, rng.uniform(850, 1050), 0.8) * 0.8
    sp = sfx.creak(N(0.08), rng, np.full(N(0.08), 180.0), kind="metal", base=2600,
                   amp_env=dsp.env([(0, 0), (0.02, 1), (0.08, 0)], N(0.08)))
    dsp.place(y, sp, N(0.02), 0.15)
    return y


def door_creak(rng, dur, pts, base, metal=0.25):
    n = N(dur)
    t, r = zip(*pts)
    rate = np.maximum(np.interp(T(n), t, r), 1.0)
    e = dsp.env([(0, 0), (0.06, 1), (dur * 0.7, 0.8), (dur, 0)], n)
    y = sfx.creak(n, rng, rate, kind="wood", base=base, amp_env=e, jitter=0.28)
    if metal:
        y += metal * sfx.creak(n, rng, rate * 1.5, kind="metal", base=base * 1.9, amp_env=e ** 2, jitter=0.2)
    y += 0.06 * sfx.whoosh(n, rng, 150, 500, 0.5, 0.7, 0.3)  # air moved by the door
    return dsp.norm(y)


def door_bang(rng, n, f=70, hard=0.6, rattle=0.3):
    y = sfx.strike(n, rng, sfx.wood_modes(rng, f * 1.6, 10, 0.12), hard, noise_mix=0.4)
    y += 1.2 * sfx.thud(n, rng, f, 0.1, 0.7, noise=0.5)
    if rattle:
        r = np.zeros(n)
        for k in range(4):
            dsp.place(r, small_metal(rng, N(0.12), rng.uniform(1100, 1500), (0.01, 0.04)), N(0.02 + 0.035 * k),
                      0.7 ** k)
        y = dsp.norm(y) + rattle * dsp.norm(r)
    return dsp.norm(y)


@sound("World/door_open_1", desc="door handle + long squeaky creak", **ONE)
def door_open_1(rng):
    n = N(1.9)
    y = np.zeros(n)
    dsp.place(y, handle(rng), 0, 0.8)
    dsp.place(y, door_creak(rng, 1.6, [(0, 45), (0.5, 120), (1.0, 95), (1.6, 40)], 820, 0.3), N(0.15), 0.9)
    return fin(y, rng, 0.8, 0.25)


@sound("World/door_open_2", desc="door handle + low short groan", **ONE)
def door_open_2(rng):
    n = N(1.4)
    y = np.zeros(n)
    dsp.place(y, handle(rng), 0, 0.8)
    dsp.place(y, door_creak(rng, 1.1, [(0, 25), (0.4, 55), (1.1, 28)], 430, 0.15), N(0.12), 0.9)
    return fin(y, rng, 0.8, 0.25)


@sound("World/door_close_1", desc="short creak, latch catches, door settles in frame", **ONE)
def door_close_1(rng):
    n = N(0.9)
    y = np.zeros(n)
    dsp.place(y, door_creak(rng, 0.3, [(0, 70), (0.3, 40)], 700, 0.2), 0, 0.5)
    dsp.place(y, door_bang(rng, N(0.5), 85, 0.5, 0.2), N(0.27), 0.7)
    dsp.place(y, latch_clack(rng, N(0.2), 950), N(0.275), 0.6)
    return fin(y, rng, 0.8, 0.22)


@sound("World/door_close_2", desc="soft door close + latch", **ONE)
def door_close_2(rng):
    n = N(0.6)
    y = np.zeros(n)
    dsp.place(y, dsp.lp(door_bang(rng, N(0.5), 80, 0.35, 0.1), 2500), 0, 0.7)
    dsp.place(y, latch_clack(rng, N(0.2), 1000), N(0.01), 0.55)
    return fin(y, rng, 0.8, 0.22)


@sound("World/door_slam", desc="door slammed hard", **ONE)
def door_slam(rng):
    n = N(1.5)
    y = door_bang(rng, n, 62, 0.9, 0.5)
    dsp.place(y, latch_clack(rng, N(0.25), 800), N(0.003), 0.6)
    return fin(y, rng, 1.0, 0.4, sat=2.5)


def heavy_scrape(rng, n, env_):
    res = [(f, rng.uniform(15, 40), g) for f, g in ((210, 1.0), (470, 0.8), (890, 0.6), (1520, 0.4), (2350, 0.3))]
    return sfx.scrape(n, rng, env_, 150, 4000, grit=900, res=res)


@sound("World/metal_door_open", desc="heavy metal door: lever clank, scrape, hinge groan", **ONE)
def metal_door_open(rng):
    n = N(2.6)
    y = np.zeros(n)
    dsp.place(y, sfx.clank(N(0.6), rng, 240, (0.1, 0.4), 0.8), 0, 0.9)
    m = N(1.7)
    e = dsp.env([(0, 0), (0.15, 1), (1.2, 0.8), (1.7, 0)], m) * np.exp(0.3 * dsp.smooth_rand(m, rng, 5, False))
    dsp.place(y, heavy_scrape(rng, m, e), N(0.3), 0.6)
    groan = sfx.creak(m, rng, np.interp(T(m), [0, 0.8, 1.7], [18, 38, 22]), kind="metal", base=560,
                      amp_env=e, jitter=0.25)
    dsp.place(y, groan, N(0.3), 0.7)
    dsp.place(y, sfx.clank(N(0.8), rng, 160, (0.15, 0.6), 0.6), N(2.0), 0.6)
    return fin(y, rng, 1.6, 0.35, sat=1.6)


@sound("World/metal_door_close", desc="heavy metal door slams: huge clank + boom", **ONE)
def metal_door_close(rng):
    n = N(2.4)
    y = np.zeros(n)
    m = N(0.5)
    dsp.place(y, sfx.creak(m, rng, np.linspace(40, 25, m), kind="metal", base=600,
                           amp_env=dsp.env([(0, 0), (0.1, 1), (0.5, 0)], m)), 0, 0.4)
    big = sfx.clank(N(1.8), rng, 115, (0.3, 1.1), 0.9) + 0.8 * sfx.thud(N(1.8), rng, 48, 0.25, 0.9)
    dsp.place(y, dsp.norm(big), N(0.45), 1.0)
    dsp.place(y, latch_clack(rng, N(0.3), 700), N(0.47), 0.6)
    return fin(y, rng, 1.8, 0.4, sat=2.0)


@sound("World/wardrobe_open", desc="wardrobe doors creak open, hangers jingle", **ONE)
def wardrobe_open(rng):
    n = N(1.1)
    y = np.zeros(n)
    dsp.place(y, sfx.strike(N(0.15), rng, [(1900, 0.01, 1), (3100, 0.008, 0.6)], 0.9), 0, 0.5)  # catch
    dsp.place(y, door_creak(rng, 0.7, [(0, 70), (0.3, 150), (0.7, 60)], 1050, 0.1), N(0.04), 0.7)
    for k in range(3):
        dsp.place(y, small_metal(rng, N(0.3), rng.uniform(1700, 2600), (0.05, 0.2), 7), N(0.3 + 0.09 * k + rng.uniform(0, 0.04)), 0.25)
    return fin(y, rng, 0.5, 0.15)


@sound("World/wardrobe_close", desc="wardrobe doors clack shut", **ONE)
def wardrobe_close(rng):
    n = N(0.7)
    y = np.zeros(n)
    for t, g in ((0.0, 1.0), (0.07, 0.75)):
        k = sfx.strike(N(0.3), rng, sfx.wood_modes(rng, 190, 9, 0.05), 0.7, noise_mix=0.3)
        dsp.place(y, dsp.norm(k), N(t), g)
    for k in range(2):
        dsp.place(y, small_metal(rng, N(0.3), rng.uniform(1800, 2600), (0.05, 0.15), 7), N(0.1 + 0.08 * k), 0.15)
    return fin(y, rng, 0.5, 0.15)


@sound("World/gate_creak", desc="rusty metal yard gate swinging", **ONE)
def gate_creak(rng):
    n = N(2.7)
    y = np.zeros(n)
    m = N(2.2)
    rate = np.interp(T(m), [0, 0.6, 1.4, 2.2], [50, 150, 110, 45])
    e = dsp.env([(0, 0), (0.1, 1), (1.8, 0.9), (2.2, 0)], m)
    sq = sfx.creak(m, rng, rate, kind="metal", base=1150, amp_env=e, jitter=0.22)
    dsp.place(y, sq, 0, 0.9)
    _, pos = dsp.pulse_train(np.full(m, 7.0), m, rng, 0.5, 0.6)
    for p in pos:  # chain-link mesh shivering
        dsp.place(y, sfx.shards(N(0.1), rng, 4, 0, 0.02, 1500, 4500, (0.01, 0.04), 0.05), p, 0.15 * e[p])
    dsp.place(y, sfx.clank(N(0.5), rng, 330, (0.08, 0.3)), N(2.15), 0.6)
    return fin(y, rng, 0.9, 0.18)


def mesh_rattle(rng, n, rate=10.0, f_bar=380):
    y = np.zeros(n)
    _, pos = dsp.pulse_train(np.full(n, rate), n, rng, 0.4, 0.5)
    for p in pos:
        h = sfx.strike(N(0.3), rng, dsp.metal_modes(f_bar, 9, rng, tau=(0.05, 0.25)), 0.7, noise_mix=0.3)
        h += 0.6 * sfx.shards(N(0.3), rng, 5, 0.0, 0.02, 1500, 5000, (0.01, 0.05), 0.05)
        dsp.place(y, dsp.norm(h), p, rng.uniform(0.4, 1.0))
    return y * dsp.env([(0, 1), (n / SR, 0)], n)


@sound("World/cage_open", desc="cage latch unhooked, hinge squeak, mesh rattle", **ONE)
def cage_open(rng):
    n = N(1.4)
    y = np.zeros(n)
    dsp.place(y, sfx.clank(N(0.5), rng, 300, (0.08, 0.3), 0.8), 0, 0.9)
    m = N(0.6)
    sq = sfx.creak(m, rng, np.interp(T(m), [0, 0.3, 0.6], [80, 160, 70]), kind="metal", base=1400,
                   amp_env=dsp.env([(0, 0), (0.05, 1), (0.6, 0)], m))
    dsp.place(y, sq, N(0.2), 0.55)
    dsp.place(y, mesh_rattle(rng, N(0.6), 11), N(0.15), 0.45)
    return fin(y, rng, 0.6, 0.18)


@sound("World/cage_close", desc="cage door slammed + latch", **ONE)
def cage_close(rng):
    n = N(1.2)
    y = np.zeros(n)
    big = sfx.clank(N(1.0), rng, 230, (0.12, 0.5), 0.9)
    dsp.place(y, big, 0, 1.0)
    dsp.place(y, mesh_rattle(rng, N(0.7), 13), N(0.01), 0.6)
    dsp.place(y, latch_clack(rng, N(0.2), 900), N(0.06), 0.6)
    return fin(y, rng, 0.6, 0.2, sat=1.8)


# --------------------------------------------------------------------------------------------- keypad
def key_press(rng):
    return sfx.strike(N(0.05), rng, [(1800, 0.006, 1), (3200, 0.004, 0.6)], 0.9, noise_mix=0.4)


@sound("World/keypad_beep", desc="keypad button press beep", **ONE)
def keypad_beep(rng):
    n = N(0.15)
    y = np.zeros(n)
    dsp.place(y, key_press(rng), 0, 0.5)
    dsp.place(y, bleep(1400.0, 0.08, rng, pw=0.4), N(0.008), 0.8)
    return dsp.lofi(dsp.norm(y), 9, 1.6, 1.0, 8000)


@sound("World/keypad_wrong", desc="harsh low double buzz: wrong code", **ONE)
def keypad_wrong(rng):
    n = N(0.5)
    y = np.zeros(n)
    for t in (0.0, 0.24):
        m = N(0.18)
        b = dsp.square(330.0, m, 0.5) + 0.6 * dsp.square(337.0, m, 0.3)
        b = dsp.sat(b, 2) * dsp.ar(m, 0.003, 0.02, hold=0.15)
        dsp.place(y, b, N(t))
    y = dsp.lp(y, 3500)
    return dsp.lofi(dsp.norm(y), 8, 1.6, 1.0, 8000)


@sound("World/keypad_ok", desc="three rising beeps + solenoid release", **ONE)
def keypad_ok(rng):
    n = N(1.0)
    y = np.zeros(n)
    for k, f in enumerate((1000.0, 1330.0, 1780.0)):
        dsp.place(y, bleep(f, 0.08, rng, pw=0.4), N(0.1 * k), 0.7)
    m = N(0.07)
    sol = dsp.square(120.0, m, 0.5) * dsp.ar(m, 0.002, 0.02, hold=0.04)
    dsp.place(y, dsp.lp(sol, 2000), N(0.36), 0.35)
    dsp.place(y, latch_clack(rng, N(0.3), 700), N(0.42), 0.9)
    return dsp.lofi(verb(dsp.norm(y), rng, 0.6, 0.15), 9, 1.6, 1.0, 8000)


@sound("World/shelter_door_open", desc="fallout shelter hatch: wheel turns, bolts retract, massive hinge groan", **ONE)
def shelter_door_open(rng):
    n = N(5.0)
    y = np.zeros(n)
    t = 0.0
    for k in range(3):  # wheel valve turns
        m = N(0.45)
        g = sfx.creak(m, rng, np.interp(T(m), [0, 0.45], [25, 45]), kind="metal", base=480,
                      amp_env=dsp.env([(0, 0), (0.1, 1), (0.45, 0)], m), jitter=0.3)
        dsp.place(y, g, N(t), 0.6)
        dsp.place(y, sfx.clank(N(0.5), rng, 260, (0.08, 0.3), 0.7), N(t + 0.42), 0.5)
        t += 0.55
    for k in range(3):  # bolts
        dsp.place(y, latch_clack(rng, N(0.4), 420) + 0.5 * sfx.thud(N(0.4), rng, 90, 0.05, 0.4), N(1.75 + 0.12 * k), 0.8)
    m = N(0.5)
    hiss = dsp.bp(rng.standard_normal(m), 1200, 7000) * dsp.env([(0, 0), (0.02, 1), (0.5, 0)], m)
    dsp.place(y, hiss, N(2.15), 0.3)
    m = N(2.4)
    e = dsp.env([(0, 0), (0.3, 1), (1.8, 0.9), (2.4, 0)], m)
    groan = sfx.creak(m, rng, np.interp(T(m), [0, 1.2, 2.4], [12, 26, 14]), kind="metal", base=310, amp_env=e,
                      jitter=0.3)
    dsp.place(y, groan + 0.5 * heavy_scrape(rng, m, e * 0.6), N(2.3), 0.9)
    dsp.place(y, sfx.clank(N(1.2), rng, 105, (0.3, 1.0), 0.8) + 0.8 * sfx.thud(N(1.2), rng, 45, 0.25, 0.8),
              N(4.4), 0.8)
    return fin(y, rng, 2.0, 0.35, sat=1.5)


# --------------------------------------------------------------------------------------------- electrics
def hum(n, rng, f=60.0, amp=None):
    """Buzzy mains hum (rectified + hard driven -> rich 120 Hz harmonic series)."""
    s = np.sin(TAU * dsp.phase(f, n))
    y = (np.abs(s) - 2 / np.pi) * 1.0 + 0.3 * s
    y = dsp.sat(y * 6, 3)
    y = dsp.peq(dsp.peq(y, 1800, 1.0, 9), 3200, 2.0, 5)
    y = dsp.hp(y, 90)
    return dsp.norm(y) * (amp if amp is not None else 1.0)


def relay(rng, heavy=1.0):
    n = N(0.35)
    y = latch_clack(rng, n, 520 / heavy, 0.9) + 0.6 * sfx.thud(n, rng, 110 / heavy, 0.04, 0.4)
    return dsp.norm(y)


def spark(rng, dur=0.08):
    m = N(dur)
    cr = dsp.crackle(m, rng, 900) * dsp.env([(0, 1), (dur, 0.2)], m)
    return dsp.hp(cr, 1500) + 0.3 * dsp.hp(rng.standard_normal(m), 3000) * dsp.perc(m, 0.001, dur * 0.3)


@sound("World/fuse_insert", desc="fuse pushed into the box: scrape, snap, sparks", **ONE)
def fuse_insert(rng):
    n = N(0.9)
    y = np.zeros(n)
    m = N(0.14)
    dsp.place(y, sfx.scrape(m, rng, dsp.env([(0, 0.2), (0.14, 1)], m), 1200, 6000, 700), 0, 0.4)
    dsp.place(y, sfx.strike(N(0.25), rng, [(1350, 0.02, 1), (2400, 0.015, 0.7), (3900, 0.01, 0.4)], 0.95, noise_mix=0.5), N(0.14), 1.0)
    dsp.place(y, spark(rng, 0.09), N(0.16), 0.5)
    m = N(0.6)
    dsp.place(y, hum(m, rng) * dsp.env([(0, 0), (0.1, 1), (0.6, 0)], m), N(0.2), 0.1)
    return fin(y, rng, 0.5, 0.12)


@sound("World/power_on", desc="breaker clunk, mains hum swells, tubes ping and flicker on", **ONE)
def power_on(rng):
    n = N(2.8)
    y = np.zeros(n)
    dsp.place(y, relay(rng, 1.4), 0, 1.0)
    dsp.place(y, spark(rng, 0.05), N(0.005), 0.4)
    m = N(2.6)
    e = dsp.env([(0, 0), (0.15, 0.4), (1.0, 1.0), (2.6, 0.9)], m)
    dsp.place(y, hum(m, rng) * e, N(0.05), 0.35)
    for t in (0.35, 0.55, 0.62, 0.9, 1.0, 1.35):  # starters ping, tubes flicker
        dsp.place(y, sfx.strike(N(0.2), rng, [(rng.uniform(2800, 3600), 0.04, 1), (rng.uniform(5000, 6200), 0.03, 0.5)], 1.0),
                  N(t), rng.uniform(0.2, 0.4))
        b = N(rng.uniform(0.04, 0.09))
        dsp.place(y, hum(b, rng) * np.hanning(b), N(t), 0.4)
    return fin(y, rng, 0.9, 0.2)


@sound("World/power_off", desc="relay clunk, pop, hum dies down", **ONE)
def power_off(rng):
    n = N(2.3)
    y = np.zeros(n)
    dsp.place(y, relay(rng, 1.4), 0, 1.0)
    dsp.place(y, spark(rng, 0.12), N(0.004), 0.6)
    m = N(2.0)
    f = np.interp(T(m), [0, 2.0], [60, 46])
    dsp.place(y, hum(m, rng, f) * dsp.env([(0, 1), (0.3, 0.6), (2.0, 0)], m) ** 1.5, N(0.02), 0.4)
    return fin(y, rng, 0.9, 0.2)


@sound("World/tv_on", desc="CRT TV switched on: click, degauss thunk, whine, snow", **ONE)
def tv_on(rng):
    n = N(2.0)
    y = np.zeros(n)
    dsp.place(y, relay(rng, 0.8), 0, 0.6)
    m = N(0.7)
    deg = hum(m, rng, 60) * dsp.expdec(m, 0.18) + 1.0 * sfx.thud(m, rng, 60, 0.2, 0.3)
    dsp.place(y, dsp.norm(deg), N(0.02), 0.8)
    m = N(1.8)
    snow = dsp.band_noise(m, rng, 400, 9000) * dsp.env([(0, 0), (0.3, 0.2), (0.6, 1), (1.8, 0.9)], m)
    dsp.place(y, dsp.norm(snow), N(0.2), 0.35)
    whine = np.sin(TAU * dsp.phase(np.interp(T(m), [0, 0.5], [9000, 15734]), m)) * dsp.env([(0, 0), (0.5, 1), (1.8, 1)], m)
    dsp.place(y, whine, N(0.2), 0.03)
    return dsp.lofi(verb(dsp.norm(y), rng, 0.6, 0.12), 9, 1.6, 1.0, 12000)


# --------------------------------------------------------------------------------------------- radio
def static_bed(n, rng, level=1.0):
    nz = dsp.bp(rng.standard_normal(n), 300, 3500, 3)
    nz *= 0.6 + 0.4 * np.abs(dsp.smooth_rand(n, rng, 1.2, False))
    cr = dsp.hp(dsp.crackle(n, rng, 30), 1200)
    return (dsp.norm(nz) + 0.4 * cr) * level


def radio_voice_signal(n, rng, f0=118.0):
    """Unintelligible voiced syllables (formant synthesis) with speech-like intonation and pauses."""
    vow = ["a", "e", "i", "o", "u", "uh", "ae"]
    seq, t = [], 0.0
    gate = np.zeros(n)
    while t < n / SR - 0.2:
        word = rng.integers(2, 5)
        for _ in range(word):
            d = rng.uniform(0.09, 0.2)
            seq.append((t, rng.choice(vow)))
            a, b = N(t), N(t + d)
            gate[a:b] = 1.0
            t += d + rng.uniform(0.0, 0.04)
        t += rng.uniform(0.12, 0.35)
    seq.append((n / SR, "uh"))
    v = sfx.vowel_track(n, seq, rng, 0.08)
    gate = np.convolve(gate, np.hanning(N(0.03)) / np.hanning(N(0.03)).sum(), "same")
    pitch = f0 * (1 + 0.12 * dsp.smooth_rand(n, rng, 2.0, False)) * np.interp(T(n), [0, n / SR], [1.1, 0.9])
    src = sfx.glottal(pitch, n, rng, 0.02, 0.2) * gate
    cons = dsp.hp(rng.standard_normal(n), 2500) * np.clip(np.abs(np.diff(gate, prepend=0)) * 40, 0, 1)
    cons = np.convolve(cons, np.ones(N(0.03)) / N(0.01), "same")
    y = sfx.formant_filter(src, v) + 0.15 * cons
    return dsp.norm(y)


@sound("World/radio_tune", desc="tuning dial sweep through static, whistles and voice fragments", **LOFI22)
def radio_tune(rng):
    n = N(2.8)
    y = static_bed(n, rng)
    sweep = np.interp(T(n), [0, 0.8, 1.5, 2.2, 2.8], [3000, 400, 1800, 600, 1200])
    het = np.sin(TAU * dsp.phase(sweep, n)) * (0.3 + 0.7 * np.abs(np.sin(TAU * dsp.phase(1.7, n))))
    y += 0.25 * het
    v = radio_voice_signal(N(0.6), rng)
    dsp.place(y, sfx.radio(v, rng, 500, 2500, 4.0, 0.0, 0) * 0.6, N(1.4))
    m = N(0.4)
    dsp.place(y, np.sin(TAU * dsp.phase(np.linspace(700, 520, m), m)) * np.hanning(m) * 0.4, N(0.55))
    y = dsp.sat(y * 0.8, 2.0)
    return dsp.lofi(dsp.bp(y, 250, 4000), 9, 1.5, 1.0, None)


@sound("World/radio_sos", desc="morse SOS over shortwave static", **LOFI22)
def radio_sos(rng):
    unit = 0.085
    code = "... --- ...   ... --- ..."
    t = 0.3
    keys = []
    for ch in code:
        if ch == ".":
            keys.append((t, unit)); t += unit * 2
        elif ch == "-":
            keys.append((t, unit * 3)); t += unit * 4
        else:
            t += unit * 2
    n = N(t + 0.5)
    tone = np.zeros(n)
    gate = np.zeros(n)
    for a, d in keys:
        gate[N(a):N(a + d)] = 1.0
    gate = np.convolve(gate, np.hanning(N(0.006)) / np.hanning(N(0.006)).sum(), "same")
    tone = np.sin(TAU * dsp.phase(720.0 * (1 + 0.002 * dsp.smooth_rand(n, rng, 0.5, False)), n)) * gate
    fading = 0.55 + 0.45 * np.abs(dsp.smooth_rand(n, rng, 0.6, False)).clip(0, 1)
    y = 0.8 * tone * fading + 0.45 * static_bed(n, rng)
    y = dsp.sat(y, 1.8)
    return dsp.lofi(dsp.bp(y, 250, 3800), 9, 1.5, 1.0, None)


@sound("World/radio_voice", desc="garbled distorted voice transmission", **LOFI22)
def radio_voice(rng):
    n = N(4.2)
    v = radio_voice_signal(n, rng, 112.0)
    v = dsp.wowflutter(v, rng, 0.002, 1.5, 0.0005, 9)
    drops = (np.abs(dsp.smooth_rand(n, rng, 3.0, False)) < 1.6).astype(float)
    drops = np.convolve(drops, np.ones(200) / 200, "same")
    y = sfx.radio(v * drops, rng, 450, 2600, 5.0, 0.0, 0) + 0.35 * static_bed(n, rng)
    y = dsp.sat(dsp.norm(y), 2.0)
    return dsp.lofi(dsp.bp(y, 300, 3500), 8, 1.6, 1.0, None)


# --------------------------------------------------------------------------------------------- car
def exhaust(rng, rate, n, rough=0.03, lp_hz=900, pattern=(1.0, 0.85, 0.95, 0.8)):
    _, pos = dsp.pulse_train(rate, n, rng, rough, 0.0)
    y = np.zeros(n)
    for i, p in enumerate(pos):
        m = N(0.06)
        pul = dsp.burst(m, rng, 0.001, 0.009, lp_hz=lp_hz) + 0.9 * sfx.thud(m, rng, 80, 0.02, 0.3, noise=0.3)
        dsp.place(y, pul, p, pattern[i % 4] * rng.uniform(0.85, 1.1))
    y = dsp.peq(y, 120, 1.2, 6)
    return dsp.norm(dsp.lp(y, lp_hz))


def starter(rng, n, cyc_pts, f=170.0, die=False):
    t, c = zip(*cyc_pts)
    cyc = np.interp(T(n), t, c)
    ph = np.cumsum(cyc) / SR
    comp = 0.5 + 0.5 * np.cos(TAU * ph)  # compression stroke loads the motor
    fm = f * (1 - 0.25 * comp ** 2) * (np.interp(T(n), [0, n / SR], [1.0, 0.6]) if die else 1.0)
    y = np.zeros(n)
    for k, a in ((1, 1.0), (2, 0.6), (3, 0.5), (5, 0.3), (7, 0.2)):
        y += a * np.sin(TAU * dsp.phase(fm * k, n))
    grind = dsp.bp(rng.standard_normal(n), 800, 4000) * (0.3 + 0.7 * comp)
    y = dsp.norm(y) * (0.6 + 0.4 * (1 - comp)) + 0.3 * dsp.norm(grind)
    return dsp.sat(y, 1.8)


@sound("World/car_door", desc="car door opened and slammed", **ONE)
def car_door(rng):
    n = N(1.0)
    y = np.zeros(n)
    dsp.place(y, latch_clack(rng, N(0.2), 1200), 0, 0.5)
    slam = sfx.strike(N(0.8), rng, dsp.metal_modes(260, 12, rng, tau=(0.04, 0.15)), 0.5, noise_mix=0.3)
    slam = dsp.norm(slam) + 1.3 * sfx.thud(N(0.8), rng, 95, 0.07, 0.5, noise=0.5)
    dsp.place(y, dsp.norm(slam), N(0.33), 1.0)
    dsp.place(y, latch_clack(rng, N(0.2), 800), N(0.335), 0.6)
    return fin(y, rng, 0.4, 0.1, sat=1.8)


@sound("World/car_crank_fail", desc="starter cranks on a weak battery and dies", **ONE)
def car_crank_fail(rng):
    n = N(2.9)
    y = np.zeros(n)
    dsp.place(y, latch_clack(rng, N(0.2), 700), 0, 0.5)  # solenoid
    m = N(2.1)
    cr = starter(rng, m, [(0, 5.5), (1.0, 4.2), (2.1, 2.2)], 165, die=True) * dsp.env([(0, 0), (0.05, 1), (1.7, 0.8), (2.1, 0)], m)
    dsp.place(y, cr, N(0.05), 0.8)
    for k in range(4):  # solenoid chatter as the battery gives up
        dsp.place(y, latch_clack(rng, N(0.15), 900), N(2.25 + 0.11 * k), 0.4)
    return fin(dsp.lp(y, 6000), rng, 0.4, 0.08)


@sound("World/car_start", desc="crank, engine catches, revs and settles to idle", **ONE)
def car_start(rng):
    n = N(4.0)
    y = np.zeros(n)
    dsp.place(y, latch_clack(rng, N(0.2), 700), 0, 0.5)
    m = N(1.25)
    cr = starter(rng, m, [(0, 5.0), (1.25, 5.8)], 170) * dsp.env([(0, 0), (0.05, 1), (1.15, 1), (1.25, 0)], m)
    dsp.place(y, cr, N(0.05), 0.7)
    m = N(2.8)
    rate = np.interp(T(m), [0, 0.15, 0.6, 1.2, 2.8], [12, 30, 62, 32, 27.5])
    eng = exhaust(rng, rate, m, 0.05) * dsp.env([(0, 0), (0.05, 1), (2.8, 0.9)], m)
    dsp.place(y, eng, N(1.15), 1.0)
    return fin(dsp.sat(dsp.norm(y), 1.6), rng, 0.4, 0.08)


@sound("World/car_drive_away", desc="engine revs through gears, car drives off over gravel into the distance", **ONE)
def car_drive_away(rng):
    n = N(7.0)
    rate = np.interp(T(n), [0, 0.3, 1.6, 1.8, 3.4, 3.6, 7.0], [28, 40, 72, 48, 78, 55, 70])
    eng = exhaust(rng, rate, n, 0.03, 1400)
    dist = np.interp(T(n), [0, 1.0, 7.0], [1.0, 1.4, 12.0])
    eng = dsp.tvf(eng, "lp", 3500 / dist ** 0.7, 0.7, block=128)
    tires = sfx.grains(n, rng, 2500 / dist, 900, 5000, (0.001, 0.004))
    tires = dsp.lp(tires, 4000) * dsp.env([(0, 0), (0.4, 1), (7.0, 1)], n)
    y = dsp.norm(eng) / dist ** 0.6 + 0.25 * dsp.norm(tires) / dist
    y = dsp.varispeed(y, np.interp(T(n), [0, 2, 7.0], [1.0, 1.0, 0.96]))
    return fin(dsp.sat(dsp.norm(y), 1.4), rng, 1.5, 0.3, bits=11)


@sound("World/gas_pour", desc="gas can glugging into a tank", **ONE)
def gas_pour(rng):
    n = N(3.3)
    y = np.zeros(n)
    t = 0.15
    while t < 3.0:  # big glugs
        f0 = rng.uniform(140, 320)
        g = sfx.bubble(f0, rng, rng.uniform(0.03, 0.05))
        dsp.place(y, g, N(t), rng.uniform(0.6, 1.0))
        t += rng.uniform(0.17, 0.26)
    rate = 110 * dsp.env([(0, 0), (0.12, 1), (3.0, 1), (3.2, 0)], n)
    y = dsp.norm(y) + 0.6 * sfx.liquid(n, rng, rate, 500, 2200, stream=0.5)
    y = dsp.peq(y, 450, 2.0, 5)  # inside the tank
    return fin(y, rng, 0.4, 0.15)


@sound("World/hood_open", desc="hood latch pops, hood lifted on creaky springs", **ONE)
def hood_open(rng):
    n = N(1.6)
    y = np.zeros(n)
    dsp.place(y, latch_clack(rng, N(0.3), 600) + 0.5 * sfx.thud(N(0.3), rng, 120, 0.04, 0.3), 0, 1.0)
    panel = sfx.strike(N(0.8), rng, dsp.metal_modes(140, 10, rng, tau=(0.08, 0.3)), 0.4, noise_mix=0.2)
    dsp.place(y, dsp.norm(panel), N(0.02), 0.5)
    m = N(0.8)
    spr = sfx.creak(m, rng, np.interp(T(m), [0, 0.8], [45, 90]), kind="metal", base=900,
                    amp_env=dsp.env([(0, 0), (0.1, 1), (0.8, 0)], m))
    dsp.place(y, spr, N(0.45), 0.4)
    dsp.place(y, small_metal(rng, N(0.3), 1300, (0.05, 0.2)), N(1.25), 0.5)
    return fin(y, rng, 0.5, 0.12)


# --------------------------------------------------------------------------------------------- big events
@sound("World/explosion", norm=("loud", -0.5), desc="car / gas explosion: crack, sub boom, rumble, debris", ch=1)
def explosion(rng):
    n = N(5.5)
    t = T(n)
    y = np.zeros(n)
    m = N(0.06)
    crack = rng.standard_normal(m) * dsp.perc(m, 0.0003, 0.012)
    dsp.place(y, crack, 0, 2.0)
    body = dsp.lp(rng.standard_normal(n), 2500) * dsp.perc(n, 0.002, 0.3)
    body += 0.8 * dsp.bp(rng.standard_normal(n), 200, 1200) * dsp.perc(n, 0.01, 0.9)
    y += 1.8 * dsp.norm(body)
    boom = dsp.sine(32 + 45 * np.exp(-t / 0.08), n) * dsp.perc(n, 0.004, 0.9)
    y += 1.1 * boom
    rum = dsp.lp(dsp.brown(n, rng), 180) * dsp.perc(n, 0.05, 1.4) * np.exp(0.4 * dsp.smooth_rand(n, rng, 2, False))
    y += 1.0 * dsp.norm(rum)
    deb = np.zeros(n)
    for _ in range(35):
        tt = 0.25 + rng.exponential(0.8)
        if tt > 4.8:
            continue
        kind = rng.uniform()
        if kind < 0.5:
            h = sfx.thud(N(0.4), rng, rng.uniform(60, 140), 0.05, 0.4, noise=0.6, noise_lp=1500)
        elif kind < 0.8:
            h = small_metal(rng, N(0.4), rng.uniform(300, 1200), (0.05, 0.25), 8, 0.7)
        else:
            h = sfx.shards(N(0.4), rng, 15, 0, 0.05, 2500, 8000, (0.005, 0.03), 0.1)
        dsp.place(deb, dsp.norm(h), N(tt), rng.uniform(0.2, 0.6) * np.exp(-tt / 2.5))
    y += 0.6 * deb
    y = dsp.reverb(y, rng, 2.5, 0.35, bright=3000, dark=500, predelay=0.02)
    y = dsp.peq(dsp.peq(y, 700, 0.8, 6), 2500, 1.0, 4)
    y = dsp.compress(dsp.norm(y), -22, 4.0, 0.002, 0.25)
    y = dsp.sat(dsp.norm(y) * 1.5, 4.0)
    y = dsp.limiter(dsp.norm(y) * dsp.undb(6.0), -0.5, release=0.12)
    return dsp.lofi(y, 10, 1.5, 1.2, 9000)


@sound("World/fire_whoosh", desc="fuel ignites: FWOOMP + roar + crackle", **ONE)
def fire_whoosh(rng):
    n = N(2.6)
    nz = dsp.brown(n, rng) * 0.5 + dsp.pink(n, rng)
    cut = np.interp(T(n), [0, 0.12, 0.4, 2.6], [80, 3200, 1500, 500])
    y = dsp.tvf(nz, "lp", cut, 1.4, block=32, stages=2)
    y *= dsp.env([(0, 0), (0.1, 1), (0.5, 0.6), (2.6, 0)], n) * np.exp(0.35 * dsp.smooth_rand(n, rng, 8, False))
    y = dsp.norm(y) + 0.6 * sfx.thud(n, rng, 50, 0.25, 0.8, noise=0.4)
    cr = np.zeros(n)
    for tt in np.sort(rng.uniform(0.15, 2.3, 30)):
        dsp.place(cr, dsp.click(N(0.02), rng.uniform(1500, 6000), 2, 0.001, rng), N(tt), rng.uniform(0.2, 0.7))
    y = dsp.norm(y) + 0.35 * cr
    return fin(dsp.sat(y, 2.0), rng, 1.0, 0.2)


def blade_chop(rng, n, rate, f_lo=150, f_hi=1800):
    _, pos = dsp.pulse_train(rate, n, rng, 0.01, 0.0)
    y = np.zeros(n)
    for p in pos:
        m = N(0.09)
        s = dsp.bp(rng.standard_normal(m), f_lo, f_hi) * dsp.perc(m, 0.003, 0.018)
        s += 1.2 * sfx.thud(m, rng, 48, 0.03, 0.5, noise=0.3)
        dsp.place(y, s, p, rng.uniform(0.85, 1.05))
    return y


@sound("World/helicopter_flyby", desc="helicopter approaches, passes overhead with doppler, recedes", **ONE)
def helicopter_flyby(rng):
    n = N(10.0)
    t = T(n)
    tp = 4.8
    x = (t - tp) * 45.0  # 45 m/s, 40 m altitude
    d = np.sqrt(x ** 2 + 40.0 ** 2)
    vr = 45.0 * x / d
    dop = 343.0 / (343.0 + vr)
    chop = blade_chop(rng, n, 10.5 * dop)
    wash = dsp.lp(dsp.pink(n, rng), 900) * (0.45 + 0.55 * (0.5 + 0.5 * np.cos(TAU * dsp.phase(10.5 * dop, n))) ** 3)
    turb = np.sin(TAU * dsp.phase(3150 * dop, n)) + 0.5 * np.sin(TAU * dsp.phase(6300 * dop, n))
    jet = dsp.bp(rng.standard_normal(n), 2000, 8000)
    y = dsp.norm(chop) + 0.7 * dsp.norm(wash) + 0.05 * turb + 0.1 * dsp.norm(jet)
    gain = (40.0 / d) ** 1.2
    y = dsp.tvf(y, "lp", np.clip(9000 * (40.0 / d) ** 0.9, 600, 12000), 0.7, block=128) * gain
    return fin(dsp.sat(dsp.norm(y), 1.6), rng, 1.5, 0.25)


@sound("World/fence_breach", desc="chain-link fence torn open: wire snap, mesh crash, post clank", **ONE)
def fence_breach(rng):
    n = N(2.3)
    y = np.zeros(n)
    m = N(0.6)
    tw = dsp.sine(np.interp(T(m), [0, 0.05, 0.6], [1800, 900, 600]), m) * dsp.expdec(m, 0.12)
    tw += 0.5 * dsp.sine(np.interp(T(m), [0, 0.05, 0.6], [2900, 1500, 1000]), m) * dsp.expdec(m, 0.08)
    dsp.place(y, tw + 0.8 * dsp.pad(dsp.burst(N(0.02), rng, 0.0002, 0.002, hp_hz=2000), m), 0, 0.7)
    dsp.place(y, mesh_rattle(rng, N(1.2), 16, 420), N(0.08), 1.0)
    dsp.place(y, 0.5 * sfx.whoosh(N(0.4), rng, 600, 3000, 0.4, 1.5, 0.2), N(0.1))
    dsp.place(y, sfx.clank(N(0.8), rng, 190, (0.1, 0.5)), N(0.55), 0.8)
    dsp.place(y, mesh_rattle(rng, N(0.6), 9, 300), N(1.1), 0.5)
    return fin(dsp.sat(dsp.norm(y), 1.6), rng, 0.9, 0.18)
