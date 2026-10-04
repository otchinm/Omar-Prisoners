"""Omar: cleaver, growls, traps, violent hiding-spot rip, grab. Audio/Omar/* (mono)."""
import numpy as np

import common
import dsp
import sfx
from common import sound
from dsp import N, T, SR, TAU
from gen_items import small_metal
from gen_world import door_bang, mesh_rattle

ONE = dict(ch=1, norm=("peak", -1.0))
LOUD = dict(ch=1, norm=("loud", -0.5))


def fin(y, rng, rt=0.5, wet=0.12, sat=0.0, bits=11):
    y = dsp.norm(y)
    if sat:
        y = dsp.sat(y, sat)
    y = dsp.reverb(y, rng, rt, wet, bright=4000, dark=900)
    return dsp.lofi(y, bits, 1.5, 1.3, 9500)


def sack(y, amount=1.0):
    """Voice heard through Omar's burlap sack: muffled, boxy."""
    y = dsp.lp(y, 1500 - 300 * amount, 2)
    return dsp.peq(y, 360, 1.0, 5 * amount)


def omar_voice(rng, name, start, dur, ratio, fry=0.5, sub=0.5):
    """Scream fragment turned into a monstrous growl: pitched way down, sub octave, vocal fry, sack."""
    x = sfx.scream_fragment(name, start, dur, ratio)
    n = len(x)
    drift = np.interp(T(n), [0, n / SR], [1.0, 0.93])
    x = dsp.pad(dsp.varispeed(x, drift), n)
    lo = dsp.pad(dsp.resample(x, 0.5), n * 2)[:n]  # sub octave, time stretched (rough)
    y = dsp.norm(x) + sub * dsp.norm(lo)
    rattle = 1 - fry + fry * (0.5 + 0.5 * np.sign(np.sin(TAU * dsp.phase(34 + 6 * dsp.smooth_rand(n, rng, 2, False), n))))
    rattle = np.convolve(rattle, np.ones(30) / 30, "same")
    y = y * rattle
    y = dsp.sat(dsp.norm(y) * 2.0, 2.5)
    br = sfx.breath(n, rng, [(0, n / SR * 0.95, "out", 1.0)])
    y = dsp.norm(y) + 0.3 * dsp.norm(br)
    return sack(y)


# --------------------------------------------------------------------------------------------- cleaver
def _swing(rng, dur, f0, f1, heavy):
    n = N(dur + 0.15)
    y = dsp.pad(sfx.whoosh(N(dur), rng, f0, f1, 0.55, 1.4, 0.16), n)
    sing = dsp.sine(np.interp(T(N(dur)), [0, dur], [3200, 3900]), N(dur)) * \
        np.exp(-0.5 * ((T(N(dur)) - dur * 0.55) / (dur * 0.12)) ** 2)
    y += 0.08 * dsp.pad(sing, n)
    y += 0.25 * sfx.cloth(n, rng, dsp.env([(0, 0), (0.03, 1), (dur * 0.5, 0.3), (dur + 0.15, 0)], n), 400, 3000, 100)
    if heavy:
        g = omar_voice(rng, "scream_4", 0.3, 0.3, 0.45, 0.6, 0.6)
        g *= dsp.env([(0, 0), (0.03, 1), (0.3, 0)], len(g))
        dsp.place(y, dsp.norm(g), 0, 0.35)
    return y


@sound("Omar/cleaver_swing_1", desc="fast cleaver whoosh", **ONE)
def cleaver_swing_1(rng):
    return fin(_swing(rng, 0.32, 350, 3200, False), rng, 0.4, 0.08)


@sound("Omar/cleaver_swing_2", desc="heavy cleaver swing with a grunt", **ONE)
def cleaver_swing_2(rng):
    return fin(_swing(rng, 0.45, 220, 2400, True), rng, 0.4, 0.08)


def flesh_hit(rng, bone=0.0, n=None):
    n = n or N(0.8)
    y = np.zeros(n)
    punch = sfx.thud(n, rng, 85, 0.07, 0.8, noise=0.7, noise_lp=1200)
    y += 1.2 * punch
    m = N(0.22)
    sq = dsp.tvf(rng.standard_normal(m), "bp", np.interp(T(m), [0, 0.22], [2200, 350]), 2.5, block=16)
    sq *= dsp.perc(m, 0.001, 0.05) * (1 + 0.8 * np.abs(dsp.smooth_rand(m, rng, 90, False)))
    dsp.place(y, dsp.norm(sq), N(0.002), 0.9)
    slosh = sfx.liquid(N(0.4), rng, 260 * dsp.env([(0, 1), (0.25, 0.2), (0.4, 0)], N(0.4)), 180, 800, 0.2)
    dsp.place(y, slosh, N(0.01), 0.35)
    chop = dsp.hp(rng.standard_normal(N(0.006)), 2500) * np.linspace(1, 0, N(0.006))
    dsp.place(y, chop, 0, 0.6)
    if bone:
        m = N(0.08)
        cr = sfx.grains(m, rng, np.full(m, 9000.0) * np.linspace(1, 0.2, m), 900, 4500, (0.0008, 0.003))
        dsp.place(y, dsp.norm(cr), N(0.004), bone)
    return y


@sound("Omar/cleaver_hit_1", desc="cleaver into flesh: thick wet impact", **LOUD)
def cleaver_hit_1(rng):
    return fin(flesh_hit(rng, 0.0), rng, 0.45, 0.12, sat=3.0)


@sound("Omar/cleaver_hit_2", desc="cleaver into flesh and bone: wet chop + crunch", **LOUD)
def cleaver_hit_2(rng):
    return fin(flesh_hit(rng, 0.7), rng, 0.45, 0.12, sat=3.5)


@sound("Omar/cleaver_hit_wall", desc="cleaver thwacks into a wooden wall and quivers", **ONE)
def cleaver_hit_wall(rng):
    n = N(1.1)
    y = sfx.strike(n, rng, sfx.wood_modes(rng, 170, 10, 0.06), 1.0, noise_mix=0.7)
    y = dsp.norm(y) + 0.6 * sfx.thud(n, rng, 95, 0.04, 0.4)
    ring = sfx.strike(n, rng, dsp.metal_modes(2300, 8, rng, tau=(0.08, 0.3)), 0.9, noise_mix=0.0)
    ring *= 1 - 0.6 * (0.5 + 0.5 * np.sin(TAU * dsp.phase(np.interp(T(n), [0, 1.1], [22, 14]), n)))  # quiver
    y += 0.3 * dsp.norm(ring)
    dsp.place(y, sfx.grains(N(0.2), rng, np.linspace(3000, 0, N(0.2)), 700, 5000, (0.001, 0.004)), N(0.003), 0.3)
    return fin(y, rng, 0.7, 0.18, sat=2.0)


# --------------------------------------------------------------------------------------------- growls
@sound("Omar/growl_1", desc="low muffled growl through the sack (from scream_2, ~an octave down)", **ONE)
def growl_1(rng):
    y = omar_voice(rng, "scream_2", 0.1, 2.0, 0.5, 0.55, 0.6)
    y *= dsp.env([(0, 0), (0.12, 1), (1.4, 0.85), (2.0, 0)], len(y))
    return fin(y, rng, 0.5, 0.12, sat=1.5)


@sound("Omar/growl_2", desc="long menacing growl / snarl (from the long scream bed)", **ONE)
def growl_2(rng):
    y = omar_voice(rng, "screams_long", 9.0, 2.6, 0.42, 0.65, 0.7)
    y *= dsp.env([(0, 0), (0.3, 0.8), (1.2, 1), (2.2, 0.7), (2.6, 0)], len(y))
    return fin(y, rng, 0.5, 0.12, sat=1.5)


# --------------------------------------------------------------------------------------------- traps
def chain_jingle(rng, n, count=10, spread=0.3):
    y = np.zeros(n)
    for t in np.sort(rng.uniform(0, spread, count)):
        dsp.place(y, dsp.norm(small_metal(rng, N(0.15), rng.uniform(1500, 2600), (0.01, 0.06), 6, 0.9)), N(t),
                  rng.uniform(0.2, 0.7))
    return y


@sound("Omar/trap_place", desc="bear trap set on the ground, spring creaks, chain", **ONE)
def trap_place(rng):
    n = N(1.6)
    y = np.zeros(n)
    dsp.place(y, sfx.clank(N(0.6), rng, 220, (0.1, 0.35), 0.6) + 0.8 * sfx.thud(N(0.6), rng, 80, 0.05, 0.5, noise=0.7), 0)
    y += 0.5 * chain_jingle(rng, n, 9, 0.35)
    m = N(0.8)
    spr = sfx.creak(m, rng, np.interp(T(m), [0, 0.8], [25, 55]), kind="metal", base=700,
                    amp_env=dsp.env([(0, 0), (0.2, 1), (0.8, 0)], m), jitter=0.3)
    dsp.place(y, spr, N(0.5), 0.5)
    dsp.place(y, small_metal(rng, N(0.3), 1300, (0.03, 0.12)), N(1.32), 0.6)  # catch set
    return fin(y, rng, 0.6, 0.12)


@sound("Omar/trap_snap", desc="bear trap jaws slam shut: violent snap", **LOUD)
def trap_snap(rng):
    n = N(1.2)
    y = sfx.strike(n, rng, dsp.metal_modes(620, 14, rng, tau=(0.06, 0.35)), 1.0, noise_mix=0.8)
    y = dsp.norm(y) + 0.7 * sfx.thud(n, rng, 110, 0.05, 0.6)
    burst = dsp.hp(rng.standard_normal(N(0.015)), 1500) * np.linspace(1, 0, N(0.015))
    dsp.place(y, burst, 0, 1.2)
    m = N(0.7)
    tw = dsp.sine(np.interp(T(m), [0, 0.7], [240, 190]) * (1 + 0.03 * np.sin(TAU * dsp.phase(16, m))), m)
    dsp.place(y, tw * dsp.expdec(m, 0.18), N(0.01), 0.35)
    y += 0.4 * chain_jingle(rng, n, 8, 0.25)
    return fin(y, rng, 0.6, 0.15, sat=2.5)


@sound("Omar/tripwire_snap", desc="taut wire snaps: twang + whip", **ONE)
def tripwire_snap(rng):
    n = N(0.7)
    y = np.zeros(n)
    m = N(0.5)
    f = np.interp(T(m), [0, 0.03, 0.5], [1500, 820, 560])
    tw = (dsp.sine(f, m) + 0.5 * dsp.sine(f * 2.03, m) + 0.25 * dsp.sine(f * 3.1, m)) * dsp.expdec(m, 0.09)
    dsp.place(y, tw, 0, 0.8)
    dsp.place(y, dsp.click(N(0.02), 3500, 2, 0.002, rng), 0, 0.8)
    dsp.place(y, sfx.whoosh(N(0.25), rng, 800, 4000, 0.4, 2.0, 0.2), N(0.02), 0.4)
    return fin(y, rng, 0.4, 0.1)


# --------------------------------------------------------------------------------------------- violence
@sound("Omar/hiding_rip", desc="wardrobe / locker doors torn open violently", **LOUD)
def hiding_rip(rng):
    n = N(1.6)
    y = np.zeros(n)
    crack = sfx.strike(N(0.4), rng, sfx.wood_modes(rng, 160, 10, 0.05), 1.0, noise_mix=0.8)
    dsp.place(y, dsp.norm(crack), 0, 1.0)
    dsp.place(y, sfx.grains(N(0.3), rng, np.linspace(5000, 0, N(0.3)), 700, 5000, (0.001, 0.005)), N(0.003), 0.5)
    m = N(0.3)
    scr = sfx.creak(m, rng, np.linspace(200, 90, m), kind="metal", base=1300,
                    amp_env=dsp.env([(0, 0), (0.02, 1), (0.3, 0)], m), jitter=0.2)
    dsp.place(y, scr, N(0.02), 0.5)
    dsp.place(y, door_bang(rng, N(0.8), 75, 0.9, 0.4), N(0.12), 1.0)
    dsp.place(y, door_bang(rng, N(0.8), 82, 0.9, 0.4), N(0.19), 0.85)
    for k in range(7):  # hangers flying
        dsp.place(y, dsp.norm(small_metal(rng, N(0.3), rng.uniform(1600, 2800), (0.04, 0.15), 7)),
                  N(0.2 + rng.uniform(0, 0.5)), rng.uniform(0.15, 0.35))
    g = omar_voice(rng, "scream_1", 0.4, 0.6, 0.5, 0.6, 0.6) * dsp.env([(0, 0), (0.04, 1), (0.6, 0)], N(0.6))
    dsp.place(y, dsp.norm(g), N(0.05), 0.45)
    return fin(y, rng, 0.7, 0.2, sat=2.5)


@sound("Omar/grab", desc="Omar seizes a prisoner: cloth, body thump, gasp, grunt", **ONE)
def grab(rng):
    n = N(1.1)
    y = np.zeros(n)
    y += 0.8 * sfx.cloth(n, rng, dsp.env([(0, 0), (0.01, 1), (0.25, 0.4), (0.8, 0)], n), 400, 5000, 600)
    dsp.place(y, sfx.thud(N(0.5), rng, 75, 0.08, 0.6, noise=0.7, noise_lp=1200), N(0.03), 1.0)
    v = sfx.scream_fragment("scream_3", 0.3, 0.25, 0.95)
    v = dsp.lp(v * dsp.env([(0, 0), (0.01, 1), (0.25, 0)], len(v)), 3500)
    dsp.place(y, dsp.norm(v), N(0.06), 0.5)
    g = omar_voice(rng, "scream_2", 0.5, 0.45, 0.48, 0.6, 0.6) * dsp.env([(0, 0), (0.04, 1), (0.45, 0)], N(0.45))
    dsp.place(y, dsp.norm(g), N(0.1), 0.45)
    return fin(y, rng, 0.5, 0.12, sat=2.0)


@sound("Omar/door_push", desc="Omar's shoulder slams a door open: body thud, leaf bang, hinge shriek", **LOUD)
def door_push(rng):
    n = N(1.3)
    y = np.zeros(n)
    dsp.place(y, sfx.thud(N(0.6), rng, 52, 0.12, 0.8, noise=0.5), 0, 1.0)
    dsp.place(y, door_bang(rng, N(0.9), 70, 0.8, 0.4), N(0.01), 0.9)
    m = N(0.6)
    sq = sfx.creak(m, rng, np.interp(T(m), [0, 0.15, 0.6], [180, 120, 40]), kind="wood", base=700,
                   amp_env=dsp.env([(0, 0), (0.03, 1), (0.6, 0)], m))
    dsp.place(y, sq, N(0.05), 0.5)
    y = dsp.norm(y) + 0.02 * dsp.norm(dsp.band_noise(n, rng, 1500, 9000))
    return fin(y, rng, 0.8, 0.25, sat=2.2, bits=10)
