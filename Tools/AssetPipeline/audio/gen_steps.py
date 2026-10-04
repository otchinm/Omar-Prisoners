"""Footsteps: Audio/Steps/<surface>_<1..4> and omar_<1..4> (mono, 44.1 kHz)."""
import numpy as np

import dsp
import sfx
from common import register
from dsp import N, T

SURFACES = ["wood", "concrete", "dirt", "grass", "metal", "asphalt", "tile", "carpet", "gravel"]


def _timing(rng):
    p = float(np.exp(rng.normal(0, 0.05)))
    toe = rng.uniform(0.05, 0.10)
    return p, toe


def step_wood(rng, v):
    p, toe = _timing(rng)
    n = N(0.55)
    modes = sfx.wood_modes(rng, 92 * p, 10, 0.085)
    heel = sfx.strike(n, rng, modes, hardness=0.35, noise_mix=0.25, noise_lp=3000)
    roll = sfx.strike(n, rng, sfx.wood_modes(rng, 120 * p, 9, 0.05), hardness=0.3, noise_mix=0.2, noise_lp=2500)
    y = heel + rng.uniform(0.3, 0.5) * np.roll(roll, N(toe))
    y += 0.5 * sfx.thud(n, rng, 75 * p, 0.05, 0.4)
    y = dsp.peq(y, 170 * p, 1.0, 5.0)
    y = dsp.peq(y, 420 * p, 1.5, 3.0)
    scuff = dsp.band_noise(n, rng, 1500, 6000) * dsp.perc(n, 0.004, 0.015, toe * 0.5)
    y = dsp.norm(y) + 0.06 * dsp.norm(scuff)
    if v in (2, 4):  # board creak under the weight
        m = N(rng.uniform(0.12, 0.2))
        rate = np.linspace(rng.uniform(70, 110), rng.uniform(35, 55), m)
        c = sfx.creak(m, rng, rate, amp_env=dsp.env([(0, 0), (0.02, 1), (m / dsp.SR, 0)], m), kind="wood")
        dsp.place(y, c, N(toe + 0.02), rng.uniform(0.18, 0.3))
    y = dsp.reverb(y, rng, 0.35, 0.12, predelay=0.004, bright=5000, dark=900)
    return dsp.lofi(y, 12, 1.4, 1.2)


def step_concrete(rng, v):
    p, toe = _timing(rng)
    n = N(0.45)
    heel = dsp.burst(n, rng, 0.0002, 0.0009, lp_hz=9000, hp_hz=250)
    heel += 0.6 * sfx.strike(n, rng, [(1250 * p, 0.009, 1), (2350 * p, 0.007, 0.7), (3500 * p, 0.005, 0.5),
                                      (520 * p, 0.012, 0.6)], hardness=0.9, noise_mix=0.3)
    heel += 0.6 * sfx.thud(n, rng, 105 * p, 0.028, 0.3)
    toe_hit = 0.45 * np.roll(dsp.burst(n, rng, 0.0002, 0.0007, lp_hz=8000, hp_hz=400), N(toe))
    e = dsp.env([(0, 0), (0.008, 1), (toe, 0.45), (toe + 0.03, 0.6), (toe + 0.12, 0)], n) ** 1.5
    grit = sfx.scrape(n, rng, e, 1500, 7000, grit=1600)
    y = dsp.norm(heel) + toe_hit + 0.5 * grit
    y = dsp.reverb(y, rng, 0.55, 0.09, predelay=0.006, bright=6000, dark=1500)
    return dsp.lofi(y, 12, 1.4, 1.2)


def step_dirt(rng, v):
    p, toe = _timing(rng)
    n = N(0.4)
    y = sfx.thud(n, rng, 68 * p, 0.045, 0.5, noise=0.6, noise_lp=700)
    y += 0.5 * np.roll(sfx.thud(n, rng, 80 * p, 0.03, 0.3, noise=0.6, noise_lp=600), N(toe))
    e = dsp.env([(0, 0), (0.01, 1), (toe, 0.5), (toe + 0.02, 0.7), (toe + 0.14, 0)], n)
    crunch = dsp.lp(dsp.band_noise(n, rng, 150, 2500), 2200) * e
    g = sfx.grains(n, rng, 500 * e, 600, 3000, (0.001, 0.005))
    g = dsp.lp(g, 3200)
    y = dsp.norm(y) + 0.35 * dsp.norm(crunch) + 0.3 * dsp.norm(g)
    return dsp.lofi(y, 12, 1.4, 1.2)


def step_grass(rng, v):
    p, toe = _timing(rng)
    n = N(0.45)
    y = np.zeros(n)
    for st, g, L in ((0.0, 1.0, 0.11), (toe, 0.75, 0.14)):
        m = N(L)
        sw = dsp.bp(rng.standard_normal(m), 1800 * p, 6500 * p, 2)
        sw *= dsp.perc(m, 0.006, L * 0.3) * np.exp(0.5 * dsp.smooth_rand(m, rng, 60, circular=False))
        dsp.place(y, sw, N(st), g)
    e = dsp.env([(0, 0), (0.01, 1), (toe, 0.4), (toe + 0.01, 0.8), (toe + 0.15, 0)], n) ** 1.5
    blades = dsp.hp(sfx.grains(n, rng, 2200 * e, 2500, 9000, (0.0005, 0.002)), 2500)
    y = dsp.norm(y) + 0.45 * dsp.norm(blades)
    y += 0.5 * sfx.thud(n, rng, 70 * p, 0.04, 0.3, noise=0.5, noise_lp=350)
    return dsp.lofi(y, 12, 1.4, 1.2)


def step_metal(rng, v):
    p, toe = _timing(rng)
    n = N(0.8)
    f0 = rng.uniform(230, 300) * p
    modes = dsp.metal_modes(f0, 14, rng, tau=(0.08, 0.3))
    heel = sfx.strike(n, rng, modes, hardness=0.7, noise_mix=0.25)
    rattle = sfx.strike(n, rng, dsp.metal_modes(f0 * 1.9, 10, rng, tau=(0.04, 0.12)), 0.8, noise_mix=0.3)
    toe_hit = sfx.strike(n, rng, modes, hardness=0.6, noise_mix=0.2)
    y = heel + 0.35 * np.roll(rattle, N(rng.uniform(0.012, 0.025)))
    y += rng.uniform(0.4, 0.6) * np.roll(toe_hit, N(toe))
    y += 0.6 * sfx.thud(n, rng, 120 * p, 0.04, 0.4)
    y = dsp.reverb(dsp.norm(y), rng, 0.5, 0.12, predelay=0.005, bright=7000, dark=2000)
    return dsp.lofi(y, 12, 1.4, 1.3)


def step_asphalt(rng, v):
    p, toe = _timing(rng)
    n = N(0.4)
    heel = dsp.burst(n, rng, 0.0002, 0.0012, lp_hz=6000, hp_hz=200) + 0.5 * sfx.thud(n, rng, 85 * p, 0.03, 0.4)
    e = dsp.env([(0, 0), (0.006, 1), (toe, 0.35), (toe + 0.015, 0.8), (toe + 0.13, 0)], n) ** 1.3
    grit = sfx.scrape(n, rng, e, 800, 6000, grit=2600)
    y = dsp.norm(heel) + 0.5 * grit + 0.35 * np.roll(dsp.norm(heel), N(toe))
    return dsp.lofi(y, 11, 1.5, 1.3)


def step_tile(rng, v):
    p, toe = _timing(rng)
    n = N(0.5)
    modes = [(1300 * p, 0.03, 0.6), (2250 * p, 0.022, 1.0), (3700 * p, 0.014, 0.8), (5100 * p, 0.01, 0.6),
             (6900 * p, 0.006, 0.4)]
    heel = sfx.strike(n, rng, modes, hardness=0.95, exc_tau=0.0004, noise_mix=0.4)
    heel += 0.4 * sfx.thud(n, rng, 115 * p, 0.025, 0.3)
    toe_hit = sfx.strike(n, rng, [(m[0] * 1.07, m[1] * 0.8, m[2]) for m in modes], 0.9, exc_tau=0.0004)
    y = dsp.norm(heel) + rng.uniform(0.4, 0.6) * np.roll(dsp.norm(toe_hit), N(toe))
    y = dsp.reverb(y, rng, 0.75, 0.2, predelay=0.003, bright=9000, dark=2500)
    return dsp.lofi(y, 12, 1.4, 1.2)


def step_carpet(rng, v):
    p, toe = _timing(rng)
    n = N(0.35)
    y = sfx.thud(n, rng, 72 * p, 0.05, 0.4, noise=0.7, noise_lp=650)
    y += 0.45 * np.roll(sfx.thud(n, rng, 85 * p, 0.035, 0.3, noise=0.7, noise_lp=550), N(toe))
    e = dsp.env([(0, 0), (0.01, 1), (toe + 0.06, 0.4), (0.25, 0)], n)
    fab = sfx.cloth(n, rng, e, 1200, 4000, crinkle=60)
    y = dsp.norm(dsp.lp(y, 900)) + 0.07 * fab
    y = dsp.reverb(y, rng, 0.25, 0.04)
    return dsp.lofi(y, 12, 1.4, 1.1)


def step_gravel(rng, v):
    p, toe = _timing(rng)
    n = N(0.5)
    t = T(n)
    dens = 2600 * np.exp(-0.5 * ((t - 0.035) / 0.03) ** 2) + 1800 * np.exp(-0.5 * ((t - toe - 0.04) / 0.035) ** 2)
    dens += 300 * np.exp(-t / 0.2)
    g = sfx.grains(n, rng, dens, 900 * p, 6500 * p, (0.0015, 0.006))
    rustle = dsp.band_noise(n, rng, 1000, 5000) * (dens / dens.max()) ** 0.7
    y = dsp.norm(g) + 0.25 * dsp.norm(rustle)
    y += 0.45 * sfx.thud(n, rng, 70 * p, 0.04, 0.4, noise=0.4, noise_lp=500)
    return dsp.lofi(y, 11, 1.5, 1.3)


def step_omar(rng, v):
    """Heavy, muffled boot fall: a deep boomy body thump through the floor, soft heel, no wooden creak or click,
    so it reads through walls."""
    p, toe = _timing(rng)
    toe *= 1.35
    n = N(0.9)
    boom = sfx.thud(n, rng, 46 * p, 0.17, 0.85, 0.02, noise=0.35, noise_lp=500, attack=0.004)
    body = sfx.thud(n, rng, 150 * p, 0.07, 0.5, 0.012, noise=0.55, noise_lp=900, attack=0.003)  # audible on small speakers
    roll = sfx.thud(n, rng, 60 * p, 0.12, 0.6, 0.02, noise=0.4, noise_lp=450, attack=0.006)
    knock = sfx.thud(n, rng, 230 * p, 0.05, 0.4, 0.01, noise=0.6, noise_lp=1400, attack=0.002)   # the floor itself
    y = 0.55 * dsp.norm(boom) + dsp.norm(body) + 0.6 * dsp.norm(knock) + 0.45 * np.roll(dsp.norm(roll), N(toe))
    if v == 3:  # something on his belt knocks dully
        dsp.place(y, dsp.lp(sfx.metal_hit(N(0.3), rng, 900, 6, (0.04, 0.1)), 1200), N(0.03), 0.06)
    y = dsp.lp(y, 1400, 2)                     # muffled: no clicks, no creaks, nothing bright
    y = dsp.peq(y, 160, 0.9, 3)                # heavy body
    y = dsp.reverb(y, rng, 0.6, 0.22, predelay=0.01, bright=1500, dark=400)
    y = dsp.sat(dsp.norm(y), 1.8)
    return dsp.lofi(y, 11, 1.5, 1.3, lp_hz=3000)


_FUNCS = {"wood": step_wood, "concrete": step_concrete, "dirt": step_dirt, "grass": step_grass,
          "metal": step_metal, "asphalt": step_asphalt, "tile": step_tile, "carpet": step_carpet,
          "gravel": step_gravel, "omar": step_omar}

for _s, _f in _FUNCS.items():
    for _i in range(1, 5):
        register(f"Steps/{_s}_{_i}", (lambda f, i: (lambda rng: f(rng, i)))(_f, _i), ch=1,
                 norm=("peak", -1.0), desc=f"footstep on {_s}" if _s != "omar" else "Omar heavy boot step")
